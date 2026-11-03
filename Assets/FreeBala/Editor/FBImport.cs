// FreeBala > Importar tudo (ou em lote: Unity -batchmode -executeMethod FreeBala.EditorTools.FBImport.ImportAllBatch)
// Lê os assets processados do jogo web (../../assets: GLB, anims.json, controllers.json, avatar/sheet, level.json) e monta o projeto:
// clipes Humanoid, AnimatorControllers, Avatars + prefabs dos personagens, armas, cenas dos mapas (colisão, NavMesh, luz, spawns),
// cena do menu e Build Settings. Pode ser rodado de novo a qualquer momento (recria tudo a partir do jogo web).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace FreeBala.EditorTools
{
    public static class FBImport
    {
        public static string GameRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        public static string Src => Path.Combine(GameRoot, "assets");
        public const string Imp = "Assets/FreeBala/Resources/FBImported", SceneDir = "Assets/FreeBala/Scenes";

        [MenuItem("FreeBala/Importar tudo", priority = 1)]
        public static void ImportAll() => Run(false);
        // colisor da fonte SÓ com as partes sólidas (pedra/azulejo): os jatos/cascatas (PA_jet) e a lâmina d'água (PA_water) ficam sem
        // colisão — antes a malha inteira virava colisor e a água da cascata era uma parede. Malha salva como asset (cena referencia).
        static Mesh SolidOnly(MeshFilter mf, Renderer r, string id)
        {
            var m = mf.sharedMesh; var mats = r ? r.sharedMaterials : new Material[0];
            bool Liquid(int i) => i < mats.Length && mats[i] && (System.Text.RegularExpressions.Regex.IsMatch(mats[i].name, @"jet|jato|water|agua|cascata|spray", System.Text.RegularExpressions.RegexOptions.IgnoreCase) || mats[i].renderQueue >= 2450);
            if (!m || m.subMeshCount <= 1) return m;
            var tris = new List<int>(); for (int i = 0; i < m.subMeshCount; i++) if (!Liquid(i)) tris.AddRange(m.GetTriangles(i));
            var c = new Mesh { name = m.name + "_colisor", indexFormat = m.indexFormat }; c.SetVertices(m.vertices); c.SetTriangles(tris, 0); c.RecalculateBounds();
            var dir = "Assets/FreeBala/Art/Colliders"; System.IO.Directory.CreateDirectory(dir); var path = $"{dir}/{id}_{mf.name}_col.asset";
            AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(c, path); return c;
        }
        public static void ImportAllBatch() { Run(true); EditorApplication.Exit(0); }

        static void Run(bool batch)
        {
            FBProjectSetup.Ensure();
            // SSR desligado só durante a importação (câmeras do editor sem normais quebram o SSR); no asset ele fica LIGADO — senão o
            // build remove os recursos do SSR e o jogo enche o console de "Couldn't find the required resources"
            var ssr = FBProjectSetup.SuspendSSR();
            try { RunCore(batch); } finally { FBProjectSetup.ResumeSSR(ssr); }
        }
        static void RunCore(bool batch)
        {
            // migração: Imported/Prefabs para dentro de Resources (carga sob demanda); MoveAsset mantém os GUIDs
            foreach (var (from, to) in new[] { ("Assets/FreeBala/Imported", Imp), ("Assets/FreeBala/Prefabs", "Assets/FreeBala/Resources/FBPrefabs") })
                if (AssetDatabase.IsValidFolder(from) && !AssetDatabase.IsValidFolder(to)) { var e = AssetDatabase.MoveAsset(from, to); if (!string.IsNullOrEmpty(e)) Debug.LogError("[FreeBala] mover " + from + ": " + e); else Debug.Log("[FreeBala] movido " + from + " -> " + to); }
            GameDataFile();
            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = false;            // glTFast 6.14: corrida no SortAndNormalizeBoneWeightsJob com o depurador de jobs ligado
            CopyGlbs();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            RetryFailedGlbs();
            var clips = FBAnimImport.ImportClips(Path.Combine(Src, "anims.json"));
            var ctrls = Json.Parse(File.ReadAllText(Path.Combine(Src, "controllers.json"))) as Dictionary<string, object>;
            AnimatorController main = null, ped = null;
            foreach (var kv in ctrls)
            {
                var c = FBAnimImport.ImportController(kv.Key, kv.Value as Dictionary<string, object>, clips);
                if (kv.Key.StartsWith("EnemyAnimator_AAA")) main = c; else if (kv.Key == "PedestrianAnimator") ped = c;
            }
            Chars(main);
            Registry(main, ped);
            Maps();
            MenuScene();
            Debug.Log("[FreeBala] importação concluída");
        }

        static void GameDataFile()
        {
            var sa = Path.Combine(Application.dataPath, "StreamingAssets/fb_data.json");
            if (!File.Exists(sa)) { Debug.LogError("[FreeBala] rode antes: node tools/unity/export_data.mjs"); return; }
            Directory.CreateDirectory("Assets/FreeBala/Resources");
            File.Copy(sa, "Assets/FreeBala/Resources/fb_data.json", true);                         // Android/WebGL leem pelo Resources
        }

        internal static void Copy(string from, string toAsset)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(toAsset));
            if (toAsset.EndsWith(".glb"))                                                  // GLB -> glTF com imagens soltas (texturas comprimidas por plataforma)
            {
                var gltf = Path.ChangeExtension(toAsset, ".gltf"); FBGlbUnpack.Unpack(from, gltf);
                if (File.Exists(toAsset)) { AssetDatabase.DeleteAsset(toAsset.Replace(Path.DirectorySeparatorChar, '/')); }   // .glb antigo (texturas embutidas)
                return;
            }
            if (!File.Exists(toAsset) || new FileInfo(toAsset).Length != new FileInfo(from).Length) File.Copy(from, toAsset, true);
        }
        public static string G(string assetGlb) => Path.ChangeExtension(assetGlb, ".gltf").Replace(Path.DirectorySeparatorChar, '/');   // caminho do asset importado (desempacotado)

        static IEnumerable<object> SkinList() => Json.Parse(File.ReadAllText(Path.Combine(Src, "skins.json"))) as List<object>;

        internal static void CopyGlbs()
        {
            foreach (var f in Directory.GetFiles(Path.Combine(Src, "chars"), "*.glb")) if (File.Exists(Path.ChangeExtension(f, ".avatar.json"))) Copy(f, $"{Imp}/Chars/{Path.GetFileName(f)}");   // todos: jogáveis (skins.json) + elenco antigo + pedestres p_*
            foreach (var f in Directory.GetFiles(Path.Combine(Src, "weapons"), "*.glb")) Copy(f, $"{Imp}/Weapons/{Path.GetFileName(f)}");
            foreach (var m in Json.Parse(File.ReadAllText(Path.Combine(Src, "maps.json"))) as List<object>)
            {
                if (Json.B(m, "disabled")) continue; var id = Json.S(m, "id"); var f = Path.Combine(Src, "maps", id, "map.glb");
                if (File.Exists(f)) Copy(f, $"{Imp}/Maps/{id}.glb");
            }
            var ie = Path.Combine(Src, "vehicles", "ie"); if (Directory.Exists(ie)) foreach (var f in Directory.GetFiles(ie, "*.glb")) Copy(f, $"{Imp}/Vehicles/{Path.GetFileName(f)}");
            foreach (var f in Directory.GetFiles(Path.Combine(Src, "vehicles"), "*.glb")) Copy(f, $"{Imp}/Vehicles/{Path.GetFileName(f)}");          // carros do REB (vehicles.json)
            foreach (var (sub, dst) in new[] { ("wheels", "Wheels"), ("int", "Int"), ("acc", "Acc"), ("parts", "Parts") })   // aros (wheels.json), interiores (volante/forros), acessórios (viatura)
            { var dd = Path.Combine(Src, "vehicles", sub); if (Directory.Exists(dd)) foreach (var f in Directory.GetFiles(dd, "*.glb")) Copy(f, $"{Imp}/{dst}/{Path.GetFileName(f)}"); }
            foreach (var (sub, dst, pat) in new[] { ("skins", "CarSkins", "*.jpg"), ("fx", "CarFX", "*.png") })
            { var dd = Path.Combine(Src, "vehicles", sub); if (Directory.Exists(dd)) foreach (var f in Directory.GetFiles(dd, pat)) Copy(f, $"Assets/FreeBala/Resources/{dst}/{Path.GetFileName(f)}"); }
            var somDir = Path.Combine(Src, "vehicles", "som"); if (Directory.Exists(somDir)) foreach (var f in Directory.GetFiles(somDir, "*.glb")) Copy(f, $"{Imp}/Som/{Path.GetFileName(f)}");   // módulos do paredão (som.json)
            foreach (var (dir, sub) in new[] { ("wsnd", "wsnd"), ("audio", "sfx") })                       // sons: Resources/Audio/<sub>/<nome>
            { var d = Path.Combine(Src, dir); if (Directory.Exists(d)) foreach (var f in Directory.GetFiles(d, "*.ogg")) Copy(f, $"Assets/FreeBala/Resources/Audio/{sub}/{Path.GetFileName(f)}"); }
            var ofi = Path.Combine(Src, "oficina", "icons"); if (Directory.Exists(ofi)) foreach (var f in Directory.GetFiles(ofi, "*.png")) if (Path.GetFileName(f).StartsWith("Oficina_") || Path.GetFileName(f).StartsWith("Editor_Som_")) Copy(f, $"Assets/FreeBala/Resources/UI/Oficina/{Path.GetFileName(f)}");   // ícones da oficina do REB
            var fonts = Path.Combine(Src, "ui", "fonts"); if (Directory.Exists(fonts)) foreach (var f in Directory.GetFiles(fonts, "*.ttf")) Copy(f, $"Assets/FreeBala/Resources/Fonts/{Path.GetFileName(f)}");
            var logo = Path.Combine(Src, "ui", "logo.webp"); var thumbs = Path.Combine(GameRoot, "data", "mapshots");
            if (Directory.Exists(thumbs)) foreach (var f in Directory.GetFiles(thumbs, "*.jpg"))
            {   // [unificação 02/11] praca_pt/orla/centro_civico agora são as arenas do time do Ruan (capas próprias em MapShots); as capas do web são dos mapas antigos
                var n = Path.GetFileNameWithoutExtension(f); if (n == "praca_pt" || n == "orla" || n == "centro_civico") n += "_antiga";
                Copy(f, $"Assets/FreeBala/Resources/MapShots/{n}.jpg");
            }
            Copy(Path.Combine(Src, "fp_arms.glb"), $"{Imp}/FP/fp_arms.glb");
            var bird = Path.Combine(Src, "birds", "hummingbird.glb"); if (File.Exists(bird)) Copy(bird, $"{Imp}/Birds/hummingbird.glb");   // céu vivo (sky.js)
            var ksb = Path.Combine(Src, "killstreak", "caveirao_bope.glb"); if (File.Exists(ksb)) Copy(ksb, $"{Imp}/Vehicles/caveirao_bope.glb");   // caveirão do BOPE (Vila Jardim)                                   // rig de 1ª pessoa (LPSP) + clipes Generic
            Copy(Path.Combine(Src, "fp_anims.json"), "Assets/FreeBala/Resources/FP/fp_anims.json"); Copy(Path.Combine(Src, "wanims.json"), "Assets/FreeBala/Resources/FP/wanims.json");
            var modes = Path.Combine(Src, "modes"); if (Directory.Exists(modes)) foreach (var f in Directory.GetFiles(modes, "*.jpg")) Copy(f, $"Assets/FreeBala/Resources/Modes/{Path.GetFileName(f)}");
            foreach (var m in Directory.GetDirectories(Path.Combine(Src, "maps"))) { var th = Path.Combine(m, "thumb.jpg"); var id = Path.GetFileName(m); if (File.Exists(th) && !File.Exists($"Assets/FreeBala/Resources/MapShots/{id}.jpg")) Copy(th, $"Assets/FreeBala/Resources/MapShots/{id}.jpg"); }
            var veg = Path.Combine(Src, "vegetation"); if (Directory.Exists(veg)) foreach (var f in Directory.GetFiles(veg, "*.glb")) Copy(f, $"{Imp}/Vegetation/{Path.GetFileName(f)}");   // grama/capim leves (tools/unity/veg_prep.py)
            var tex = Path.Combine(Src, "textures", "ie"); if (Directory.Exists(tex)) foreach (var f in Directory.GetFiles(tex, "*.jpg")) Copy(f, $"{Imp}/Textures/{Path.GetFileName(f)}");
        }

        // GLB que falhou na importação (sem GameObject principal): importa de novo, um por vez
        static void RetryFailedGlbs()
        {
            for (int pass = 0; pass < 2; pass++)
            {
                var bad = Directory.GetFiles(Imp, "*.gltf", SearchOption.AllDirectories).Select(f => f.Replace(Path.DirectorySeparatorChar, '/')).Where(f => !AssetDatabase.LoadAssetAtPath<GameObject>(f)).ToList();
                if (bad.Count == 0) return;
                Debug.Log($"[FreeBala] reimportando {bad.Count} GLB (passo {pass + 1})");
                foreach (var f in bad) AssetDatabase.ImportAsset(f, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
            foreach (var f in Directory.GetFiles(Imp, "*.gltf", SearchOption.AllDirectories)) if (!AssetDatabase.LoadAssetAtPath<GameObject>(f.Replace(Path.DirectorySeparatorChar, '/'))) Debug.LogError("[FreeBala] GLB não importou: " + f);
        }

        static void Chars(AnimatorController ctrl)
        {
            int n = 0; var list = Directory.GetFiles(Path.Combine(Src, "chars"), "*.avatar.json").Select(f => Path.GetFileName(f).Replace(".avatar.json", "")).ToList();
            try
            {
                foreach (var s in list)
                {
                    var id = s; EditorUtility.DisplayProgressBar("FreeBala: personagens", id, (float)n++ / list.Count);
                    var av = Path.Combine(Src, "chars", id + ".avatar.json"); if (!File.Exists(av)) continue;
                    FBCharImport.Import(id, $"{Imp}/Chars/{id}.gltf", av, Path.Combine(Src, "chars", id + ".sheet.json"), ctrl);
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        static void Registry(AnimatorController main, AnimatorController ped)
        {
            Directory.CreateDirectory("Assets/FreeBala/Resources");
            const string rp = "Assets/FreeBala/Resources/FBRegistry.asset";
            var reg = AssetDatabase.LoadAssetAtPath<FBRegistry>(rp); if (!reg) { reg = ScriptableObject.CreateInstance<FBRegistry>(); AssetDatabase.CreateAsset(reg, rp); }
            List<string> Ids(string dir, string pat) => Directory.Exists(dir) ? Directory.GetFiles(dir, pat).Where(f => AssetDatabase.LoadAssetAtPath<GameObject>(f.Replace(Path.DirectorySeparatorChar, '/'))).Select(Path.GetFileNameWithoutExtension).ToList() : new List<string>();
            List<T> Load<T>(string dir, string pat) where T : Object => Directory.Exists(dir) ? Directory.GetFiles(dir, pat).Select(f => AssetDatabase.LoadAssetAtPath<T>(f.Replace('\\', '/'))).Where(o => o).ToList() : new List<T>();
            reg.charIds = Ids(FBCharImport.PrefabDir, "*.prefab");
            reg.weaponIds = Ids($"{Imp}/Weapons", "*.gltf");
            reg.vehicleIds = Ids($"{Imp}/Vehicles", "*.gltf"); reg.somIds = Ids($"{Imp}/Som", "*.gltf"); reg.wheelIds = Ids($"{Imp}/Wheels", "*.gltf"); reg.intIds = Ids($"{Imp}/Int", "*.gltf"); reg.accIds = Ids($"{Imp}/Acc", "*.gltf"); reg.partIds = Ids($"{Imp}/Parts", "*.gltf");
            reg.clips = Load<AnimationClip>(FBAnimImport.ClipDir, "*.anim");
            reg.mainController = main; reg.pedestrianController = ped; reg.fpArms = AssetDatabase.LoadAssetAtPath<GameObject>($"{Imp}/FP/fp_arms.gltf");
            { var bc = AssetDatabase.LoadAllAssetsAtPath($"{Imp}/Birds/hummingbird.gltf").OfType<AnimationClip>().ToList(); reg.birdClip = bc.FirstOrDefault(c => System.Text.RegularExpressions.Regex.IsMatch(c.name, "02_Forward_Flight_Loop|01_Hover_Idle_Loop")) ?? bc.FirstOrDefault(); }
            EditorUtility.SetDirty(reg); AssetDatabase.SaveAssets();
            Debug.Log($"[FreeBala] registro: {reg.charIds.Count} personagens, {reg.weaponIds.Count} armas, {reg.vehicleIds.Count} veículos, {reg.somIds.Count} módulos de som, {reg.wheelIds.Count} aros, {reg.clips.Count} clipes");
        }

        // ---- mapas: cena por mapa (geometria + colisão + NavMesh + luz/neblina + spawns)
        static readonly string[] NoCol = { "_nocol", "alum_win", "water", "_rio", "brejo", "_jet", "_decal", "Folhas", "leaves", "Leaf", "grass_tuft", "_fl_", "flor", "flower", "petal", "plant", "bush", "arbust", "canteiro_veg" };   // vegetação/flores: só visual
        public static string OnlyMap;                                                     // FBImportMap: refaz só a cena deste mapa
        static void Maps()
        {
            Directory.CreateDirectory(SceneDir);
            foreach (var m in Json.Parse(File.ReadAllText(Path.Combine(Src, "maps.json"))) as List<object>)
            {
                var id = Json.S(m, "id"); var glb = $"{Imp}/Maps/{id}.gltf";
                if (OnlyMap != null && id != OnlyMap) continue;
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(glb); if (!src) continue;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var map = (GameObject)PrefabUtility.InstantiatePrefab(src); map.name = "Map_" + id; map.isStatic = true;
                foreach (var t in map.GetComponentsInChildren<Transform>(true)) if (System.Array.IndexOf(FreeBala.FBMapRetire.NODES, t.name) >= 0) { t.gameObject.SetActive(false); t.gameObject.tag = "EditorOnly"; }   // r47: objetos artificiais fora do build
                foreach (var t in map.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = FBProjectSetup.LMap;
                FBGlassSplit.Split(id, map);        // vidro dentro das malhas dos prédios vira objeto próprio (quebrável, acende à noite)
                foreach (var mf in map.GetComponentsInChildren<MeshFilter>(true))
                {
                    var r = mf.GetComponent<Renderer>(); string mat = r && r.sharedMaterial ? r.sharedMaterial.name : ""; string nm = mf.name + " " + mat;
                    // vidro de janela: só painel dedicado com 1 submesh (prédios com múltiplos materiais têm o vidro separado pelo FBGlassSplit)
                    if (mf.sharedMesh && mf.sharedMesh.subMeshCount == 1 && System.Text.RegularExpressions.Regex.IsMatch(mat, @"^(VJ_|PA_|PT_|OR_)?glass$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    { mf.gameObject.layer = FBProjectSetup.LGlass; var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; mc.convex = false; mf.gameObject.AddComponent<FBGlass>(); mf.gameObject.isStatic = false; continue; }
                    if (mat.EndsWith("_colonly") && r) r.enabled = false;                         // rampa invisível da escada: só colisão
                    // fonte/chafariz/espelho d'água: a malha mistura pedra e água (1º material transparente) e era pulada inteira → personagem atravessava
                    if (System.Text.RegularExpressions.Regex.IsMatch(mf.name, @"fonte|chafariz|espelho_d?agua|fountain", System.Text.RegularExpressions.RegexOptions.IgnoreCase) && !mf.GetComponent<Collider>())
                    { var fcol = mf.gameObject.AddComponent<MeshCollider>(); fcol.sharedMesh = SolidOnly(mf, r, id); mf.gameObject.AddComponent<FBSurface>().kind = FreeBala.FBWorld.SurfOf("concreto"); continue; }
                    if (mat.EndsWith("_nocol") || System.Text.RegularExpressions.Regex.IsMatch(mat, @"^(VJ_)?alum_win$")) continue;
                    if (NoCol.Any(k => nm.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                    if (r && r.sharedMaterial && (r.sharedMaterial.renderQueue >= 2450 || r.sharedMaterial.IsKeywordEnabled("_ALPHATEST_ON"))) continue;   // folhas/grades com alfa: sem colisão (como o web)
                    if (!mf.GetComponent<Collider>()) { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; int k = FreeBala.FBWorld.SurfOf(nm); if (k > 0) mf.gameObject.AddComponent<FBSurface>().kind = k; }
                }
                // muros invisíveis do nível (level.walls: beira do rio, limites — Orla)
                var wl = Json.L(Json.Parse(File.ReadAllText(Path.Combine(Src, "maps", id, "level.json"))), "walls");
                if (wl != null) { var wr = new GameObject("MurosInvisiveis"); wr.layer = FBProjectSetup.LMap; foreach (var wo in wl) { var w = wo as List<object>; if (w == null || w.Count < 4) continue; var a = Coords.FromWeb(Json.Fv(w[0]), 0, Json.Fv(w[1])); var b = Coords.FromWeb(Json.Fv(w[2]), 0, Json.Fv(w[3])); var g = new GameObject("muro"); g.layer = FBProjectSetup.LMap; g.transform.SetParent(wr.transform); g.transform.position = (a + b) / 2 + Vector3.up * 16; g.transform.rotation = Quaternion.LookRotation((b - a).normalized); var bc = g.AddComponent<BoxCollider>(); bc.size = new Vector3(.8f, 48, (b - a).magnitude + .6f); } }
                // luz e neblina (level.json)
                var lvl = Json.Parse(File.ReadAllText(Path.Combine(Src, "maps", id, "level.json")));
                var sun = new GameObject("Sol").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.25f; sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(48, -35, 0); RenderSettings.sun = sun;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(.78f, .84f, .92f); RenderSettings.ambientEquatorColor = new Color(.62f, .62f, .6f); RenderSettings.ambientGroundColor = new Color(.35f, .32f, .28f);
                var fog = Json.O(lvl, "fog");
                if (fog != null && ColorUtility.TryParseHtmlString(Json.S(fog, "color", "#a9bdcc"), out var fc))
                { RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = fc; RenderSettings.fogStartDistance = Json.F(fog, "near", 60); RenderSettings.fogEndDistance = Json.F(fog, "far", 280) * 1.6f; }
                // materiais URP/Lit por tipo + detail maps + vegetação com vento + reflexo assado (FBMapLook; depois da luz: o reflexo usa o sol)
                FBMapLook.Apply(map, id);
                // props por mapa do Agente 8 (FBMapProps.Apply(id, raiz)) — por reflexão: compila antes de o arquivo existir
                System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("FreeBala.EditorTools.FBMapProps")).FirstOrDefault(t => t != null)?.GetMethod("Apply", new[] { typeof(string), typeof(GameObject) })?.Invoke(null, new object[] { id, map });
                FBWaterSetup.Apply(id, map);        // água animada, fontes e rios (Agente 6)
                FBStreetPolesImport.Apply(id, map); // postes no meio da pista: corta só a coluna (lista medida, postes_na_rua.json)
                FBPropColliders.Apply(id, map);
                FBGlassSplit.Measure(map);          // janelas medidas para acender à noite (FBWindowLights)     // colisão de postes, árvores, portões e props (Agente 3)
                FBLampData.Measure(map);            // luminárias medidas (malha legível só aqui) para os postes acesos (FBStreetLights)
                FBMonumento.Apply(id, map);         // letreiro EU AMO BOA VISTA: materiais aprovados congelados (Art/Monumento) — sempre por último
                // pontos do nível (o jogo também lê do fb_data.json; ficam na cena para edição visual)
                var pts = new GameObject("Pontos").transform;
                foreach (var key in new[] { "spawnA", "spawnB", "bots", "points" })
                {
                    var l = Json.L(lvl, key); if (l == null) continue; int i = 0;
                    foreach (var p in l) { var g = new GameObject($"{key}_{i++}"); g.transform.SetParent(pts); g.transform.position = Coords.FromWeb(p as List<object>); }
                }
                // NavMesh dos bots (construída no editor e salva como asset; o FBNavData a registra ao carregar a cena)
                var srcs = new List<NavMeshBuildSource>();
                NavMeshBuilder.CollectSources(new Bounds(Vector3.zero, Vector3.one * 2000), ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), srcs);
                var bs = NavMesh.GetSettingsByID(0); bs.agentRadius = .35f; bs.agentHeight = 1.9f; bs.agentClimb = .45f; bs.agentSlope = 42;
                var data = NavMeshBuilder.BuildNavMeshData(bs, srcs, new Bounds(Vector3.zero, Vector3.one * 2000), Vector3.zero, Quaternion.identity);
                var npath = $"{SceneDir}/Map_{id}_NavMesh.asset"; AssetDatabase.DeleteAsset(npath); if (data != null) AssetDatabase.CreateAsset(data, npath);
                var nav = new GameObject("NavMesh").AddComponent<FBNavData>(); nav.data = data;
                new GameObject("Jogo").AddComponent<FBMatch>().mapId = id;
                EditorSceneManager.SaveScene(scene, $"{SceneDir}/Map_{id}.unity");
                Debug.Log($"[FreeBala] mapa {id}: cena salva, NavMesh {(data ? "ok" : "falhou")}");
            }
        }

        static void MenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; cam.transform.position = new Vector3(0, 1.35f, -4.6f); cam.fieldOfView = 30; cam.gameObject.AddComponent<AudioListener>();
            cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
            new GameObject("Menu").AddComponent<FBMenu>();
            EditorSceneManager.SaveScene(scene, $"{SceneDir}/Menu.unity");
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene($"{SceneDir}/Menu.unity", true) };
            foreach (var f in Directory.GetFiles(SceneDir, "Map_*.unity")) scenes.Add(new EditorBuildSettingsScene(f.Replace('\\', '/'), true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
