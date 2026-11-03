// FreeBala > Otimizar (Agente 9) — passe de desempenho rodado DEPOIS do "Importar tudo" (o import recria cenas e prefabs do zero).
// Em lote: Unity -batchmode -executeMethod FreeBala.EditorTools.FBOptimize.AllBatch [-fbopt-semocc] [-fbopt-meshlod]
// Etapas (cada uma também no menu, isoladas):
//  · Cenas dos mapas: flags estáticas (static batching/oclusão), props pequenos sem sombra, LODGroup de corte por tamanho na tela,
//    vetores de movimento só da câmera, bake de occlusion culling por cena (Umbra). Mesh LOD (decimação do Unity 6) opcional.
//  · Personagens (prefabs em Resources/FBPrefabs/Chars): LODGroup de corte, sem skinned motion vectors, sem updateWhenOffscreen.
//  · Texturas: tamanho máximo e formato por plataforma (ASTC no Android/iOS, DXT/BC no PC/WebGL), mip streaming nas texturas 3D.
//  · Áudio: streaming (longos), comprimido em memória (médios), descomprimido ao carregar (curtos), mono nos efeitos 3D.
//  · Player: static batching só no PC (no mobile/WebGL a memória extra não compensa: SRP Batcher cuida), strip de malha.
// Os limites daqui são os do nível ALTO; o FBQuality corta mais em tempo de execução nos níveis baixos (sombra de props, LOD bias).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FreeBala.EditorTools
{
    public static class FBOptimize
    {
        // limites (meia-diagonal do bounds, em metros)
        public const float NoShadowBelow = .45f;      // props menores que isso nunca projetam sombra (garrafas, pedras, latas, placas pequenas)
        public const float CullLodBelow = 3f;         // props menores ganham LODGroup de corte
        public const float OccluderAbove = 2.5f;      // só peças grandes e opacas tampam a visão no Umbra
        public const float MeshLodMinTris = 4000;     // Mesh LOD só em malhas pesadas
        const string OptDir = "Assets/FreeBala/Optimized";

        static string Arg(string a) => System.Environment.GetCommandLineArgs().Contains(a) ? a : null;

        [MenuItem("FreeBala/Otimizar/Tudo (depois do import)", priority = 20)]
        public static void All() => Run(true, false);
        public static void AllBatch() { Run(Arg("-fbopt-semocc") == null, Arg("-fbopt-meshlod") != null); EditorApplication.Exit(0); }
        static void Run(bool occlusion, bool meshLod)
        {
            var t0 = System.DateTime.Now;
            PlayerOpts(); Textures(); Audio(); Characters(); Scenes(occlusion, meshLod);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FreeBala][otimizar] concluído em {(System.DateTime.Now - t0).TotalMinutes:0.0} min");
        }

        // ---------------- cenas ----------------
        [MenuItem("FreeBala/Otimizar/Cenas dos mapas (com occlusion)", priority = 21)] static void ScenesMenu() => Scenes(true, false);
        [MenuItem("FreeBala/Otimizar/Cenas dos mapas (sem occlusion)", priority = 22)] static void ScenesFastMenu() => Scenes(false, false);
        [MenuItem("FreeBala/Otimizar/Cenas + Mesh LOD (experimental)", priority = 23)] static void ScenesMeshLodMenu() => Scenes(true, true);
        public static void Scenes(bool occlusion, bool meshLod)
        {
            var files = Directory.Exists(FBImport.SceneDir) ? Directory.GetFiles(FBImport.SceneDir, "Map_*.unity").Select(f => f.Replace('\\', '/')).ToArray() : new string[0];
            int k = 0;
            try
            {
                foreach (var f in files)
                {
                    EditorUtility.DisplayProgressBar("FreeBala: otimizando mapas", Path.GetFileNameWithoutExtension(f), (float)k++ / files.Length);
                    var sc = EditorSceneManager.OpenScene(f, OpenSceneMode.Single);
                    var rep = OptimizeScene(sc, meshLod);
                    if (occlusion) rep += " · " + BakeOcclusion();
                    EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc);
                    Debug.Log($"[FreeBala][otimizar] {sc.name}: {rep}");
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        static bool Transparent(Renderer r) { foreach (var m in r.sharedMaterials) if (m && (m.renderQueue >= 2450 || m.IsKeywordEnabled("_ALPHATEST_ON") || m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"))) return true; return false; }

        // Uma cena de mapa: só mexe no que está sob a raiz "Map_<id>" (geometria importada) e nos muros; carros/jogo nascem em runtime.
        public static string OptimizeScene(Scene sc, bool meshLod)
        {
            int rends = 0, noShadow = 0, lods = 0, occluders = 0, mlod = 0; long tris = 0;
            foreach (var root in sc.GetRootGameObjects())
            {
                if (!root.name.StartsWith("Map_")) continue;
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var go = r.gameObject; rends++;
                    var mf = r.GetComponent<MeshFilter>(); var mesh = mf ? mf.sharedMesh : null; if (mesh) tris += Tris(mesh);
                    if (go.GetComponent<FBGlass>() || go.layer == FBProjectSetup.LGlass) continue;          // vidro quebrável: dinâmico
                    if (go.GetComponentInParent<FBPole>()) continue;                                         // poste que entorta/cai (FBPolePrep): fora do batching
                    if (!r.enabled) continue;                                                                // só colisão (rampa _colonly)
                    float size = r.bounds.extents.magnitude; bool transp = Transparent(r);
                    var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
                    if (size >= OccluderAbove && !transp && !CoverContainer(go.transform)) { flags |= StaticEditorFlags.OccluderStatic; occluders++; }   // contêiner de cobertura some no De Rolê: não pode tampar a visão no Umbra
                    GameObjectUtility.SetStaticEditorFlags(go, flags);
                    r.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
                    r.lightProbeUsage = LightProbeUsage.Off; r.allowOcclusionWhenDynamic = true;
                    if (r.shadowCastingMode == ShadowCastingMode.On && (size < NoShadowBelow || (transp && size < 1.5f))) { r.shadowCastingMode = ShadowCastingMode.Off; noShadow++; }
                    // Malhas estáticas de glTF têm vértices assados em espaço global, gerando centro incorreto no LODGroup e corte falso:
                    // corte por LODGroup fica restrito ao Mesh LOD quando explicitamente solicitado.
                    if (meshLod && mesh && Tris(mesh) >= MeshLodMinTris && !go.GetComponentInParent<LODGroup>() && MeshLod(sc.name, mf)) mlod++;
                }
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.GetComponent<Collider>() && !t.GetComponent<Renderer>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.OccludeeStatic);
            }
            foreach (var root in sc.GetRootGameObjects())
                if (root.name == "MurosInvisiveis") foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);   // sem renderer: fora do batching/oclusão
            return $"{rends} renderers · {tris / 1000}k tri · {noShadow} sem sombra · {lods} LODGroups de corte · {occluders} oclusores" + (meshLod ? $" · {mlod} Mesh LOD" : "");
        }
        static bool CoverContainer(Transform t) { for (; t; t = t.parent) if (FreeBala.FBRoads.IsCoverContainer(t)) return true; return false; }
        static uint Fnv(string s) { uint h = 2166136261; foreach (var c in s) h = (h ^ c) * 16777619; return h; }   // hash estável entre sessões
        static long Tris(Mesh m) { long n = 0; for (int i = 0; i < m.subMeshCount; i++) n += m.GetIndexCount(i) / 3; return n; }

        // Mesh LOD (Unity 6): cópia da malha importada (o .gltf é somente leitura) com níveis gerados; a cena passa a usar a cópia.
        static bool MeshLod(string scene, MeshFilter mf)
        {
            try
            {
                var src = mf.sharedMesh; var dir = $"{OptDir}/{scene}"; Directory.CreateDirectory(dir);
                var path = $"{dir}/{FBRegistry.Safe(src.name)}_{Fnv(AssetDatabase.GetAssetPath(src) + "|" + src.name) & 0xffff:x4}.asset";
                var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (!m) { m = Object.Instantiate(src); m.name = src.name + "_lod"; MeshLodUtility.GenerateMeshLods(m, 4); AssetDatabase.CreateAsset(m, path); }
                mf.sharedMesh = m;                                                                       // o MeshCollider continua com a malha original
                GameObjectUtility.SetStaticEditorFlags(mf.gameObject, GameObjectUtility.GetStaticEditorFlags(mf.gameObject) & ~StaticEditorFlags.BatchingStatic);   // Mesh LOD não combina com static batching
                return true;
            }
            catch (System.Exception e) { Debug.LogWarning($"[FreeBala][otimizar] Mesh LOD falhou em {mf.name}: {e.Message}"); return false; }
        }

        static string BakeOcclusion()
        {
            // Umbra: oclusor mínimo 3 m (paredes/casas), buraco mínimo 25 cm (janelas/becos não somem), limiar de face traseira padrão
            StaticOcclusionCulling.smallestOccluder = 3; StaticOcclusionCulling.smallestHole = .25f; StaticOcclusionCulling.backfaceThreshold = 100;
            var t = System.DateTime.Now; bool ok = StaticOcclusionCulling.Compute();
            return ok ? $"occlusion {StaticOcclusionCulling.umbraDataSize / 1024} KB em {(System.DateTime.Now - t).TotalSeconds:0}s" : "occlusion FALHOU";
        }

        // ---------------- personagens ----------------
        [MenuItem("FreeBala/Otimizar/Personagens", priority = 24)]
        public static void Characters()
        {
            if (!Directory.Exists(FBCharImport.PrefabDir)) return; int n = 0;
            var files = Directory.GetFiles(FBCharImport.PrefabDir, "*.prefab").Select(f => f.Replace('\\', '/')).ToArray();
            try
            {
                for (int i = 0; i < files.Length; i++)
                {
                    if (i % 10 == 0) EditorUtility.DisplayProgressBar("FreeBala: otimizando personagens", Path.GetFileName(files[i]), (float)i / files.Length);
                    var root = PrefabUtility.LoadPrefabContents(files[i]); bool dirty = false;
                    try
                    {
                        var smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                        foreach (var s in smrs) { if (s.skinnedMotionVectors || s.updateWhenOffscreen) { s.skinnedMotionVectors = false; s.updateWhenOffscreen = false; dirty = true; } }
                        if (smrs.Length > 0 && !root.GetComponent<LODGroup>())
                        {
                            // um nível só: some quando ocupa < 0,5% da altura da tela (a luneta estreita o FOV, então de mira o inimigo continua visível)
                            var lg = root.AddComponent<LODGroup>(); lg.fadeMode = LODFadeMode.None;
                            lg.SetLODs(new[] { new LOD(.005f, smrs.Cast<Renderer>().ToArray()) }); lg.RecalculateBounds(); dirty = true;
                        }
                        if (dirty) { PrefabUtility.SaveAsPrefabAsset(root, files[i]); n++; }
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            Debug.Log($"[FreeBala][otimizar] personagens: {n}/{files.Length} prefabs ajustados");
        }

        // ---------------- texturas ----------------
        static readonly string[] TexRoots = { "Assets/FreeBala/Resources", "Assets/FreeBala/Art" };
        public enum TexKind { Albedo, Normal, Map, UI }
        public static TexKind Classify(string path, TextureImporter ti)
        {
            var p = path.Replace('\\', '/');
            if (ti != null && (ti.textureType == TextureImporterType.Sprite || ti.textureType == TextureImporterType.GUI)) return TexKind.UI;
            if (Regex(p, "/(MapShots|Modes|UI|Fonts|Icons|Thumbs)/")) return TexKind.UI;
            if ((ti != null && ti.textureType == TextureImporterType.NormalMap) || Regex(Path.GetFileNameWithoutExtension(p), "(_n|_nrm|_normal|normal)$")) return TexKind.Normal;
            if (p.Contains("/FBImported/Maps/") || p.Contains("/FBImported/Textures/")) return TexKind.Map;
            return TexKind.Albedo;
        }
        static bool Regex(string s, string pat) => System.Text.RegularExpressions.Regex.IsMatch(s, pat, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        // tamanho máximo por plataforma/tipo (PC, mobile, WebGL)
        public static int MaxSize(TexKind k, string plat) => plat == "Standalone" ? (k == TexKind.Map ? 4096 : 2048) : plat == "WebGL" ? (k == TexKind.UI ? 1024 : k == TexKind.Map ? 2048 : 1024) : (k == TexKind.Map ? 2048 : 1024);

        [MenuItem("FreeBala/Otimizar/Texturas por plataforma", priority = 25)]
        public static void Textures()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", TexRoots.Where(AssetDatabase.IsValidFolder).ToArray()); int changed = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var g in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(g); if (AssetImporter.GetAtPath(path) is not TextureImporter ti) continue;
                    var k = Classify(path, ti); bool ch = false;
                    if (k != TexKind.UI && !ti.streamingMipmaps && ti.mipmapEnabled) { ti.streamingMipmaps = true; ch = true; }
                    if (k == TexKind.UI && ti.mipmapEnabled && ti.textureType == TextureImporterType.Sprite) { ti.mipmapEnabled = false; ch = true; }
                    foreach (var plat in new[] { "Standalone", "Android", "iPhone", "WebGL" })
                    {
                        // Etapa 4 (ruído): antes PC/WebGL ficavam em "Compressed" = DXT1/DXT5 (blocos 4x4 visíveis em gradientes, ícones e texto da UI),
                        // por cima do CompressedHQ (BC7) que o pós-processador dava à UI. Agora: UI sem compressão no PC/WebGL (poucos MB), 3D em BC7/BC5
                        // no PC; WebGL continua DXT nas texturas 3D (download) — BC7 exige EXT_texture_compression_bptc.
                        var s = ti.GetPlatformTextureSettings(plat); bool mobile = plat == "Android" || plat == "iPhone";
                        int max = plat == "Standalone" ? MaxSize(k, plat) : Mathf.Min(MaxSize(k, plat), ti.maxTextureSize);
                        var fmt = mobile ? (k == TexKind.Normal ? TextureImporterFormat.ASTC_5x5 : k == TexKind.UI ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6)
                                : k == TexKind.UI ? TextureImporterFormat.RGBA32 : TextureImporterFormat.Automatic;
                        var comp = !mobile && k == TexKind.UI ? TextureImporterCompression.Uncompressed : plat == "Standalone" ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Compressed;
                        if (s.overridden && s.maxTextureSize == max && s.format == fmt && s.textureCompression == comp && !s.crunchedCompression) continue;
                        s.overridden = true; s.maxTextureSize = max; s.format = fmt; s.crunchedCompression = false; s.textureCompression = comp; s.compressionQuality = mobile ? 50 : 100;
                        ti.SetPlatformTextureSettings(s); ch = true;
                    }
                    if (ch) { ti.SaveAndReimport(); changed++; }
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            Debug.Log($"[FreeBala][otimizar] texturas: {changed}/{guids.Length} ajustadas");
        }

        // ---------------- áudio ----------------
        public static AudioClipLoadType LoadTypeFor(float seconds) => seconds >= 20 ? AudioClipLoadType.Streaming : seconds >= 3 ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        [MenuItem("FreeBala/Otimizar/Áudio", priority = 26)]
        public static void Audio()
        {
            var guids = AssetDatabase.FindAssets("t:AudioClip", TexRoots.Where(AssetDatabase.IsValidFolder).ToArray()); int changed = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var g in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(g); if (AssetImporter.GetAtPath(path) is not AudioImporter ai) continue;
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path); float len = clip ? clip.length : 1;
                    var lt = LoadTypeFor(len); bool music = Regex(path, "/(Music|Musica|Som|Paredao|Radio|Ambience|Ambiente)/");
                    var s = ai.defaultSampleSettings; bool ch = false;
                    if (s.loadType != lt || s.compressionFormat != AudioCompressionFormat.Vorbis || s.sampleRateSetting != AudioSampleRateSetting.OptimizeSampleRate)
                    { s.loadType = lt; s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = lt == AudioClipLoadType.Streaming ? .5f : .6f; s.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate; s.preloadAudioData = lt != AudioClipLoadType.Streaming; ai.defaultSampleSettings = s; ch = true; }
                    bool mono = len < 20 && !music;                                       // efeitos 3D: mono (metade da memória; o espacializador mistura mono)
                    if (ai.forceToMono != mono) { ai.forceToMono = mono; ch = true; }
                    if (lt == AudioClipLoadType.Streaming && !ai.loadInBackground) { ai.loadInBackground = true; ch = true; }
                    var mob = s; mob.quality = lt == AudioClipLoadType.Streaming ? .35f : .45f;
                    foreach (var plat in new[] { "Android", "iOS" }) if (!ai.ContainsSampleSettingsOverride(plat) || ai.GetOverrideSampleSettings(plat).quality != mob.quality) { ai.SetOverrideSampleSettings(plat, mob); ch = true; }
                    if (ch) { ai.SaveAndReimport(); changed++; }
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            Debug.Log($"[FreeBala][otimizar] áudio: {changed}/{guids.Length} ajustados");
        }

        // ---------------- player ----------------
        [MenuItem("FreeBala/Otimizar/Ajustes do player", priority = 27)]
        public static void PlayerOpts()
        {
            PlayerSettings.SetStaticBatchingForPlatform(BuildTarget.StandaloneWindows64, true);
            foreach (var t in new[] { BuildTarget.Android, BuildTarget.iOS, BuildTarget.WebGL }) PlayerSettings.SetStaticBatchingForPlatform(t, false);
            PlayerSettings.stripUnusedMeshComponents = true; GpuSkinning();
            Debug.Log("[FreeBala][otimizar] player: static batching só no PC, strip de malha ligado, deformação de malha na GPU (batched)");
        }
        // Mesh Deformation = GPU (Batched): o skinning sai da CPU (MeshSkinning.Update). Sem API pública no Unity 6.6: campo gpuSkinning do
        // ProjectSettings (0 CPU, 1 GPU, 2 GPU Batched). Onde não há compute (WebGL2) o Unity volta sozinho para a CPU.
        public static void GpuSkinning(int mode = 2)
        {
            var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset"); if (ps == null || ps.Length == 0) return;
            var so = new SerializedObject(ps[0]); var p = so.FindProperty("gpuSkinning"); if (p == null) { Debug.LogWarning("[FreeBala][otimizar] campo gpuSkinning não encontrado"); return; }
            if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = mode > 0; else p.intValue = mode;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
