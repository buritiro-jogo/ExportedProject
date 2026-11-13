// Monta a cena Map_bv_city juntando os DOIS mapas que ja existem, sem alterar nenhum deles:
//   - praca_aguas (mapa web, Assets/FreeBala/Resources/FBImported/Maps/praca_aguas.gltf) -> fica no lugar,
//     porque o referencial do corredor do BV City E o dele;
//   - centro_civico (mapa do parceiro, Assets/BURITIRO_Mapas/centro_civico/centro_civico_unity.glb) ->
//     entra pela transformacao MEDIDA (giro 0,144 graus, t = +821,97 / -1572,14 m), conferida de quatro
//     maneiras (ver tools/bv_city/01_georef/montagem_bv_city.json);
//   - os cinco marcos levantados nas rodadas 2 a 5 (Assembleia, Palacio Helio Campos, Tribunal de Justica,
//     Catedral e Palacio da Cultura), em Assets/BURITIRO_Mapas/bv_city/marcos/.
//
// Os dois mapas estao em 1 unidade = 1 metro (medido: ajuste com residuo 0,0000 m nas duas cenas montadas).
//
// PENDENCIA CONHECIDA: os dois mapas posicionam seus dados por convencoes diferentes (um pelo x/y do
// corredor, o outro por leste/norte) e as duas diferem por uma REFLEXAO. Por isso existe -fbespelho:
// rode com 0 e com 1 e compare o print de cima com a ortoimagem; so uma das duas encaixa.
//
// Lote:  -executeMethod FreeBala.EditorTools.FBMontaBVCity.Batch [-fbespelho 0|1] [-fbnav 1]
// Menu:  BURITIRO > Mapas > Montar Map_bv_city
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace FreeBala.EditorTools
{
    public static class FBMontaBVCity
    {
        const string CENAS = "Assets/FreeBala/Scenes";
        const string MODELO = CENAS + "/Map_praca_aguas.unity";
        const string PA_GLTF = "Assets/FreeBala/Resources/FBImported/Maps/praca_aguas.gltf";
        const string CC_GLB = "Assets/BURITIRO_Mapas/centro_civico/centro_civico_unity.glb";
        const string MARCOS = "Assets/BURITIRO_Mapas/bv_city/marcos";
        const int LAYER_MAPA = 8;

        // transformacao medida do centro_civico para o referencial do BV City
        const float GIRO = 0.144f, TX = 821.97f, TY = -1572.14f;
        // costura da faixa que os dois mapas repetem (x 410..648 do corredor)
        const float COSTURA = 530f;
        // perimetro do BV City
        static readonly Vector2 PX = new Vector2(-175, 1095), PY = new Vector2(-250, 215);

        // r66: MEDIDO. Com espelho=0 a pose do centro_civico nao fecha (o terceiro ponto de prova
        // erra 399,99 m); com espelho=1 fecha em 0,009 m, e o print de cima bate com a ortoimagem.
        static bool ESPELHO = true;   // corredor -> Unity = (-x, h, +y)

        static string Arg(string k, string pad = null)
        { var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, k); return i >= 0 && i + 1 < a.Length ? a[i + 1] : pad; }

        public static void Batch()
        {
            int code = 0;
            try { ESPELHO = Arg("-fbespelho", "1") == "1"; Monta(Arg("-fbnav", "0") == "1"); }
            catch (Exception e) { Debug.LogError("[BVCITY] " + e); code = 1; }
            EditorApplication.Exit(code);
        }

        [MenuItem("BURITIRO/Mapas/Montar Map_bv_city (Praca das Aguas + Centro Civico)")]
        static void Menu() { ESPELHO = true; Seguro(false); }

        // chamadas pelo laboratorio do terminal: "metodo FBMontaBVCity.MontaNormal"
        public static void MontaNormal() { ESPELHO = true; Seguro(false); }
        public static void MontaEspelho() { ESPELHO = false; Seguro(false); }   // so para repetir a conferencia

        /// <summary>Controle: mesmo print de cima, na cena do parceiro que ja existe. Serve para saber
        /// se o rosa (material sem shader) e defeito da nossa montagem ou do render em lote.</summary>
        public static void PrintControle()
        {
            EditorSceneManager.OpenScene(CENAS + "/Map_centro_civico.unity", OpenSceneMode.Single);
            var alvo = GameObject.Find("Map_centro_civico");
            var c = alvo ? alvo.transform.position : Vector3.zero;
            PrintOrto(c + new Vector3(175, 500, -1045), 700f, "controle_centro_civico");
        }

        public static void BatchControle()
        {
            int code = 0;
            try { PrintControle(); } catch (Exception e) { Debug.LogError("[BVCITY] " + e); code = 1; }
            EditorApplication.Exit(code);
        }

        /// <summary>Nao monta por cima de cena com mudanca nao salva: a montagem abre uma cena NOVA
        /// e o que nao foi salvo se perderia.</summary>
        static void Seguro(bool nav)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var c = SceneManager.GetSceneAt(i);
                if (c.isDirty)
                {
                    Debug.LogError($"[BVCITY] a cena '{c.name}' tem mudanca nao salva. Salve ou descarte antes " +
                                   "(o laboratorio aceita o pedido 'salvar'). Nada foi feito.");
                    return;
                }
            }
            Monta(nav);
        }

        // ---------------------------------------------------------------- referencial
        /// <summary>corredor (x, y em metros, h altura) -> posicao no Unity do mapa praca_aguas</summary>
        public static Vector3 Mundo(float x, float y, float h)
            => new Vector3(-x, h, ESPELHO ? y : -y);

        /// <summary>dados do centro_civico (leste, norte) -> corredor (x, y)</summary>
        public static Vector2 CcParaCorredor(float E_cc, float N_cc)
        {
            float t = GIRO * Mathf.Deg2Rad;
            float E = Mathf.Cos(t) * E_cc - Mathf.Sin(t) * N_cc + TX;
            float N = Mathf.Sin(t) * E_cc + Mathf.Cos(t) * N_cc + TY;
            return new Vector2(0.745f * E - 0.667f * N, -0.667f * E - 0.745f * N);
        }

        /// <summary>posicao no Unity do centro_civico -> corredor (x, y). La, Unity = (-E, h, -N).</summary>
        public static Vector2 CcUnityParaCorredor(Vector3 p) => CcParaCorredor(-p.x, -p.z);

        // ---------------------------------------------------------------- montagem
        static void Monta(bool nav)
        {
            var cc = AssetDatabase.LoadAssetAtPath<GameObject>(CC_GLB);
            if (!cc) throw new FileNotFoundException(CC_GLB);

            // r66: a BASE e a propria cena do praca_aguas. Ela ja traz a malha do mapa (instancia de
            // prefab do .gltf), os colisores FBCol_*, os postes, a vegetacao e os pontos. Antes eu
            // instanciava o .gltf cru numa cena vazia e o mapa ficava sem colisao nenhuma.
            // Gravar com outro nome e "salvar como": Map_praca_aguas.unity nao e tocado.
            var cena = EditorSceneManager.OpenScene(MODELO, OpenSceneMode.Single);
            foreach (var go in cena.GetRootGameObjects())
                if (go.name == "Jogo")
                    foreach (var mb in go.GetComponents<MonoBehaviour>())
                    {
                        var so = new SerializedObject(mb); var pr = so.FindProperty("mapId");
                        if (pr != null) { pr.stringValue = "bv_city"; so.ApplyModifiedPropertiesWithoutUndo(); }
                    }

            // --- centro_civico: entra pela transformacao medida
            var inst = Instancia(cc, cena, "BVC_centro_civico");
            var mapa = BuritiroEditor.BuritiroMontaMapa.Acha(inst.transform, "Mapa_centro_civico") ?? inst.transform;
            mapa.SetParent(null, true);
            if (mapa.gameObject != inst) UnityEngine.Object.DestroyImmediate(inst);
            mapa.gameObject.name = "BVC_centro_civico";
            PoeCentroCivico(mapa);

            // os mesmos passos do montador do parceiro: a geometria de colisao vira MeshCollider
            // (senao ela continua sendo DESENHADA, em rosa) e cada celula ganha LODGroup.
            int nCol = BuritiroEditor.BuritiroMontaMapa.MontaColisores(mapa);
            int nLod = BuritiroEditor.BuritiroMontaMapa.MontaLODs(mapa);
            var refs = BuritiroEditor.BuritiroMontaMapa.Acha(mapa, "REF");
            if (refs) UnityEngine.Object.DestroyImmediate(refs.gameObject);

            foreach (var t in mapa.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LAYER_MAPA;

            // --- marcos levantados nas rodadas 2 a 5
            int marcos = PoeMarcos(cena);

            // --- costura da faixa que os dois mapas repetem
            int sumiu = Costura(cena, mapa);

            if (nav) AssaNavMesh();

            Directory.CreateDirectory(CENAS);
            string path = $"{CENAS}/Map_bv_city.unity";
            EditorSceneManager.SaveScene(cena, path);
            AdicionaNoBuild(path);
            Debug.Log($"[BVCITY] {path} montada. espelho={ESPELHO} colisores={nCol} LODs={nLod} " +
                      $"marcos={marcos} desligados na costura={sumiu}");
            PrintDeCima(path);
        }

        /// <summary>NavMesh do corredor inteiro. Mesmo caminho do montador do parceiro: coleta os
        /// COLISORES da camada do mapa e grava um NavMeshData que o FBNavData liga em tempo de jogo.
        /// A area e o perimetro do BV City (1.270 x 465 m), nao os 600 x 600 de uma arena.</summary>
        static void AssaNavMesh()
        {
            var c0 = Mundo(PX.x, PY.x, 0f); var c1 = Mundo(PX.y, PY.y, 0f);
            var min = Vector3.Min(c0, c1); var max = Vector3.Max(c0, c1);
            var caixa = new Bounds(new Vector3((min.x + max.x) / 2, 20f, (min.z + max.z) / 2),
                                   new Vector3(max.x - min.x + 40f, 120f, max.z - min.z + 40f));
            var fontes = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(caixa, 1 << LAYER_MAPA, NavMeshCollectGeometry.PhysicsColliders,
                                          0, new List<NavMeshBuildMarkup>(), fontes);
            Debug.Log($"[BVCITY] NavMesh: {fontes.Count} colisores em {caixa.size.x:0} x {caixa.size.z:0} m");
            var data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), fontes, caixa,
                                                       Vector3.zero, Quaternion.identity);
            if (data == null) { Debug.LogError("[BVCITY] NavMesh falhou"); return; }
            const string pasta = "Assets/BURITIRO_Mapas/bv_city";
            Directory.CreateDirectory(pasta);
            string pa = pasta + "/NavMesh_bv_city.asset";
            if (File.Exists(pa)) AssetDatabase.DeleteAsset(pa);
            AssetDatabase.CreateAsset(data, pa);
            var go = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "NavMesh")
                     ?? new GameObject("NavMesh");
            var tipo = AppDomain.CurrentDomain.GetAssemblies()
                       .Select(x => x.GetType("FreeBala.FBNavData")).FirstOrDefault(t => t != null);
            if (tipo == null) { Debug.LogWarning("[BVCITY] FreeBala.FBNavData nao encontrado; asset em " + pa); return; }
            var comp = go.GetComponent(tipo); if (!comp) comp = go.AddComponent(tipo);
            var so = new SerializedObject(comp);
            so.FindProperty("data").objectReferenceValue = data; so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[BVCITY] NavMesh gravado em {pa}");
            ConfereNav(data);
        }

        /// <summary>Quanto do corredor o NavMesh cobre, e se os pontos da cena caem nele.</summary>
        static void ConfereNav(NavMeshData data)
        {
            var h = NavMesh.AddNavMeshData(data);
            try
            {
                int dentro = 0, total = 0;
                for (float x = PX.x; x <= PX.y; x += 8f)
                    for (float y = PY.x; y <= PY.y; y += 8f)
                    {
                        total++;
                        if (NavMesh.SamplePosition(Mundo(x, y, 3f), out _, 6f, NavMesh.AllAreas)) dentro++;
                    }
                Debug.Log($"[BVCITY] NavMesh cobre {100f * dentro / total:0.0}% da grade de 8 m " +
                          $"({dentro} de {total} pontos; telhado, mato e quadra sem passagem entram nos que faltam)");

                var pontos = SceneManager.GetActiveScene().GetRootGameObjects()
                             .FirstOrDefault(g => g.name == "Pontos");
                if (pontos)
                {
                    int ok = 0, n = 0; var longe = new List<string>();
                    foreach (Transform t in pontos.GetComponentsInChildren<Transform>(true))
                    {
                        if (t == pontos.transform) continue;
                        n++;
                        if (NavMesh.SamplePosition(t.position, out _, 2.5f, NavMesh.AllAreas)) ok++;
                        else if (longe.Count < 8) longe.Add(t.name);
                    }
                    Debug.Log($"[BVCITY] pontos da cena no NavMesh: {ok} de {n}" +
                              (longe.Count > 0 ? "  fora: " + string.Join(", ", longe) : ""));
                }
            }
            finally { if (h.valid) NavMesh.RemoveNavMeshData(h); }
        }

        /// <summary>Confere o NavMesh ja gravado, sem assar de novo. Mede o que interessa: do CHAO
        /// que existe (achado por raio de cima para baixo na camada do mapa), quanto e navegavel a
        /// menos de 1 m. O teste da assadura usava raio de 6 m e dava 100% de graca - de cima de um
        /// telhado ele achava a rua.</summary>
        public static void BatchConfereNav()
        {
            int code = 0;
            try
            {
                EditorSceneManager.OpenScene(CENAS + "/Map_bv_city.unity", OpenSceneMode.Single);
                var go = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "NavMesh");
                var tipo = AppDomain.CurrentDomain.GetAssemblies()
                           .Select(x => x.GetType("FreeBala.FBNavData")).FirstOrDefault(t => t != null);
                var comp = go ? go.GetComponent(tipo) : null;
                var data = comp ? (NavMeshData)new SerializedObject(comp).FindProperty("data").objectReferenceValue : null;
                if (!data) { Debug.LogError("[BVCITY] cena sem NavMeshData"); EditorApplication.Exit(1); return; }
                var h = NavMesh.AddNavMeshData(data);
                Physics.SyncTransforms();
                int chao = 0, nav = 0, semChao = 0;
                for (float x = PX.x; x <= PX.y; x += 8f)
                    for (float y = PY.x; y <= PY.y; y += 8f)
                    {
                        var alto = Mundo(x, y, 80f);
                        if (!Physics.Raycast(alto, Vector3.down, out var hit, 200f, 1 << LAYER_MAPA)) { semChao++; continue; }
                        chao++;
                        if (NavMesh.SamplePosition(hit.point, out var nh, 1.0f, NavMesh.AllAreas)
                            && Mathf.Abs(nh.position.y - hit.point.y) < 1.0f) nav++;
                    }
                Debug.Log($"[BVCITY] grade de 8 m: {chao} pontos com chao, {semChao} sem nada embaixo. " +
                          $"Navegavel a menos de 1 m: {nav} ({100f * nav / Mathf.Max(1, chao):0.0}%)");
                // os pontos que o jogo vai usar sao os do fb_data do bv_city (os "Pontos" da cena sao
                // os do praca_aguas, herdados da base, e nao valem para este mapa)
                string lista = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../_diag/bvcity_pontos.txt"));
                if (File.Exists(lista))
                {
                    int ok = 0, n = 0; var fora = new List<string>();
                    foreach (var ln in File.ReadAllLines(lista))
                    {
                        var c = ln.Split(' '); if (c.Length < 4) continue;
                        var pos = new Vector3(float.Parse(c[1], CultureInfo.InvariantCulture),
                                              float.Parse(c[2], CultureInfo.InvariantCulture),
                                              float.Parse(c[3], CultureInfo.InvariantCulture));
                        n++;
                        if (NavMesh.SamplePosition(pos, out _, 1.5f, NavMesh.AllAreas)) ok++;
                        else if (fora.Count < 12) fora.Add(c[0]);
                    }
                    Debug.Log($"[BVCITY] pontos do fb_data a menos de 1,5 m do NavMesh: {ok} de {n}" +
                              (fora.Count > 0 ? "  fora: " + string.Join(", ", fora) : ""));
                }
                else Debug.LogWarning("[BVCITY] sem _diag/bvcity_pontos.txt; pulei a conferencia dos pontos");
                if (h.valid) NavMesh.RemoveNavMeshData(h);
            }
            catch (Exception e) { Debug.LogError("[BVCITY] " + e); code = 1; }
            EditorApplication.Exit(code);
        }

        static void AdicionaNoBuild(string cenaPath)
        {
            var lista = EditorBuildSettings.scenes.ToList();
            if (lista.Any(s => s.path == cenaPath)) return;
            lista.Add(new EditorBuildSettingsScene(cenaPath, true));
            EditorBuildSettings.scenes = lista.ToArray();
            Debug.Log("[BVCITY] cena adicionada ao build");
        }

        static GameObject Instancia(GameObject asset, Scene cena, string nome)
        {
            var g = (GameObject)PrefabUtility.InstantiatePrefab(asset, cena);
            PrefabUtility.UnpackPrefabInstance(g, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            g.name = nome;
            return g;
        }

        /// <summary>
        /// Pousa o centro_civico resolvendo a pose por tres pontos: pega tres pontos no espaco do
        /// Unity dele, leva ao corredor pela transformacao medida, e dai ao mundo do BV City.
        /// Assim nao e preciso supor como o glTFast converteu os eixos do arquivo.
        /// </summary>
        static void PoeCentroCivico(Transform t)
        {
            // pontos de prova no sistema do centro_civico (Unity dele) — 200 m de lado
            Vector3[] a = { new Vector3(0, 0, 0), new Vector3(200, 0, 0), new Vector3(0, 0, 200) };
            Vector3[] b = new Vector3[3];
            for (int i = 0; i < 3; i++)
            {
                var c = CcUnityParaCorredor(a[i]);
                b[i] = Mundo(c.x, c.y, a[i].y);
            }
            // a pose e uma rotacao em Y + translacao (a escala e 1 nos dois mapas)
            Vector3 u = (a[1] - a[0]).normalized, v = (b[1] - b[0]).normalized;
            float ang = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg - Mathf.Atan2(u.x, u.z) * Mathf.Rad2Deg;
            var rot = Quaternion.Euler(0, ang, 0);
            t.rotation = rot;
            t.position = b[0] - rot * a[0];

            // confere: o terceiro ponto tem de cair onde a transformacao manda
            var erro = (t.TransformPoint(a[2]) - b[2]).magnitude;
            Debug.Log($"[BVCITY] centro_civico: giro {ang:0.000} graus, pos {t.position}, erro no 3o ponto {erro:0.000} m");
            if (erro > 0.5f)
                Debug.LogWarning("[BVCITY] o terceiro ponto nao fechou: a transformacao precisa de REFLEXAO. " +
                                 "Rode de novo com -fbespelho invertido e compare os prints.");
        }

        static int PoeMarcos(Scene cena)
        {
            // (id, x do corredor, y do corredor, rumo da fachada em graus)
            var lista = new (string id, float x, float y, float rumo)[]
            {
                ("BVC_CCV_ASSEMBLEIA", 814.0f, -193.0f, 48.5f),
                ("BVC_CCV_PALACIO",    935.9f, -183.2f, 0f),
                ("BVC_CCV_TJ",         704.4f, -203.9f, 165.1f),
                ("BVC_CCV_CATEDRAL",   797.3f,  163.6f, 103.8f),
                ("BVC_CCV_CULTURA",    926.0f, -176.5f, 221.8f),
            };
            int n = 0;
            foreach (var m in lista)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{MARCOS}/{m.id}.glb");
                if (!asset) { Debug.LogWarning("[BVCITY] marco nao importado: " + m.id); continue; }
                var g = Instancia(asset, cena, m.id);
                g.transform.position = Mundo(m.x, m.y, 0f);
                // o rumo do corredor cresce no sentido horario a partir do norte; no Unity o giro
                // em Y e horario visto de cima, e o eixo x do corredor aponta para -X do mundo
                g.transform.rotation = Quaternion.Euler(0, -(m.rumo - 131.83f), 0);
                foreach (var t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LAYER_MAPA;
                n++;
            }
            return n;
        }

        /// <summary>Na faixa que os dois mapas repetem (x 410..648 do corredor), o praca_aguas manda
        /// antes da costura e o centro_civico depois. Desliga o que sobra - RENDERIZADOR E COLISOR.
        /// Desligar so o renderizador deixaria colisao invisivel na faixa e sujaria o NavMesh, que e
        /// assado a partir dos colisores.</summary>
        static int Costura(Scene cena, Transform cc)
        {
            Physics.SyncTransforms();                       // o centro_civico acabou de ser movido
            var doCc = new HashSet<Transform>(cc.GetComponentsInChildren<Transform>(true));
            var alvo = new Dictionary<GameObject, Vector3>();
            foreach (var raiz in cena.GetRootGameObjects())
            {
                foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(true))
                    alvo[r.gameObject] = r.bounds.center;
                foreach (var k in raiz.GetComponentsInChildren<Collider>(true))
                    if (!alvo.ContainsKey(k.gameObject)) alvo[k.gameObject] = k.bounds.center;
            }
            int n = 0;
            foreach (var kv in alvo)
            {
                float x = -kv.Value.x;                      // corredor x = -Unity.x
                bool ehCc = doCc.Contains(kv.Key.transform);
                bool sobra = ehCc ? (x < COSTURA && x > 300f) : (x > COSTURA && x < 760f);
                if (sobra) { kv.Key.SetActive(false); n++; }
            }
            return n;
        }

        static void CopiaDoModelo(Scene cena)
        {
            if (!File.Exists(MODELO)) { Debug.LogWarning("[BVCITY] sem " + MODELO); return; }
            var modelo = EditorSceneManager.OpenScene(MODELO, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(modelo);
            foreach (var go in modelo.GetRootGameObjects())
            {
                if (go.name != "Jogo" && go.name != "Sol" && go.name != "Reflexo") continue;
                var c = UnityEngine.Object.Instantiate(go); c.name = go.name;
                SceneManager.MoveGameObjectToScene(c, cena);
                if (go.name == "Jogo")
                    foreach (var mb in c.GetComponents<MonoBehaviour>())
                    {
                        var so = new SerializedObject(mb); var p = so.FindProperty("mapId");
                        if (p != null) { p.stringValue = "bv_city"; so.ApplyModifiedPropertiesWithoutUndo(); }
                    }
            }
            SceneManager.SetActiveScene(cena);
            EditorSceneManager.CloseScene(modelo, true);
        }

        /// <summary>Print ortografico de cima, centrado em c, cobrindo 'tam' metros de profundidade.</summary>
        static void PrintOrto(Vector3 c, float tam, string nome)
        {
            // a cena do praca_aguas tem nevoa linear terminando em 512 m; o print e feito de 500 m
            // de altura, entao sem desligar a nevoa a imagem sai toda da cor dela
            bool fog0 = RenderSettings.fog; RenderSettings.fog = false;
            var go = new GameObject("CamPrint");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = tam / 2;
            cam.nearClipPlane = 1; cam.farClipPlane = 1500; cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.07f, .08f, .10f);
            go.transform.position = c; go.transform.rotation = Quaternion.Euler(90, 0, 0);
            int W = 1800, H = 1800;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt; cam.aspect = 1f;
            for (int i = 0; i < 2; i++) cam.Render();
            var tx = new Texture2D(W, H, TextureFormat.RGB24, false);
            RenderTexture.active = rt; tx.ReadPixels(new Rect(0, 0, W, H), 0, 0); tx.Apply(); RenderTexture.active = null;
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../tools/bv_city/07_comparacoes"));
            Directory.CreateDirectory(dir);
            string saida = Path.Combine(dir, nome + ".png");
            File.WriteAllBytes(saida, tx.EncodeToPNG());
            cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(go); rt.Release(); RenderSettings.fog = fog0;
            Debug.Log("[BVCITY] print -> " + saida);
        }

        /// <summary>Print ortografico de cima do perimetro inteiro, para comparar com a ortoimagem.</summary>
        static void PrintDeCima(string cenaPath)
        {
            float cx = (PX.x + PX.y) / 2, cy = (PY.x + PY.y) / 2;
            float larg = PX.y - PX.x, prof = PY.y - PY.x;
            bool fog1 = RenderSettings.fog; RenderSettings.fog = false;
            var go = new GameObject("CamBVCity");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = prof / 2 + 20;
            cam.nearClipPlane = 1; cam.farClipPlane = 1200; cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.07f, .08f, .10f);
            var c = Mundo(cx, cy, 500f);
            go.transform.position = c;
            go.transform.rotation = Quaternion.Euler(90, 0, 0);
            int W = 2400, H = Mathf.RoundToInt(2400f * prof / larg);
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt; cam.aspect = larg / prof;
            cam.orthographicSize = prof / 2 + 20;
            for (int i = 0; i < 2; i++) cam.Render();
            var tx = new Texture2D(W, H, TextureFormat.RGB24, false);
            RenderTexture.active = rt; tx.ReadPixels(new Rect(0, 0, W, H), 0, 0); tx.Apply(); RenderTexture.active = null;
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../tools/bv_city/07_comparacoes"));
            Directory.CreateDirectory(dir);
            string saida = Path.Combine(dir, $"bv_city_de_cima_espelho{(ESPELHO ? 1 : 0)}.png");
            File.WriteAllBytes(saida, tx.EncodeToPNG());
            cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(go); rt.Release(); RenderSettings.fog = fog1;
            Debug.Log("[BVCITY] print de cima -> " + saida);
        }
    }
}
