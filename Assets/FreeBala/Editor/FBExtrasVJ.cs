// r63 (08/11/2026): estruturas da Vila Jardim vistas no Street View (jun/2025) que o gerador não tinha (tools/vj_full/INVENTARIO_R53.md):
//  · p019_dir — PONTO DE ÔNIBUS DE VIDRO da Av. Jardim, lado do Samaúma: laje de concreto com faixa azul de acessibilidade nas pontas e
//    piso tátil amarelo no meio-fio, 4 pilares de aço, fundo e laterais de vidro, banco, teto branco fino. Pilares da frente projetados no
//    chão pelo panorama real (refs/sv/panos_reais.json, f = 360/tan(85°/2), rumo +1,6°): (113,5; 91,3) e (115,9; 98,0) → 7,2 m, rumo 19,6°.
//  · p018_dir — ABRIGO DE LIXO de alvenaria pichado na entrada do Ipê Amarelo: frente (132,2; 152,7)–(133,5; 157,0), portão amarelo de tela.
//  · (p025_dir, abrigo verde do Araçá, já existia no gerador: caixa_verde54 da r54t)
// Cores = mediana das caixas nas próprias fotos. Coordenadas no quadro do gerador (x leste / y norte, m) → Unity pelo PAI dos nós do mapa
// (o glTF converte (x, y, z) do Blender em (−x, z, −y) no espaço local da raiz importada). Altura do chão por raio.
// Materiais = cópias de materiais LISOS do próprio mapa (mesmo shader/palavras-chave → nenhuma variante nova; ver shader-stutter-rules).
//   Unity -batchmode -executeMethod FreeBala.EditorTools.FBExtrasVJ.Batch
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBExtrasVJ
    {
        const string Dir = "Assets/FreeBala/Art/ExtrasVJ";
        const float GenScale = 1.2f;
        static Transform P; static float S; static int Layer; static Transform Root;
        static readonly Dictionary<string, Material> mats = new();
        static Mesh cube;

        public static void Batch()
        {
            int code = 0;
            try { Build(); } catch (System.Exception e) { Debug.LogError("[FreeBala] extras VJ falharam: " + e); code = 1; }
            EditorApplication.Exit(code);
        }

        public static void Build()
        {
            var sc = EditorSceneManager.OpenScene($"{FBImport.SceneDir}/Map_vila_jardim.unity");
            foreach (var g in sc.GetRootGameObjects()) if (g.name == "ExtrasVJ_r63") Object.DestroyImmediate(g);
            var all = sc.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
            var ref0 = all.FirstOrDefault(t => t.name.StartsWith("VJ54_Abrigo_Onibus_AvJardim")) ?? all.FirstOrDefault(t => t.name.StartsWith("VJ54_"));
            if (!ref0 || !ref0.parent) throw new System.Exception("nó do mapa de referência (VJ54_*) não achado");
            P = ref0.parent; S = GenScale * P.lossyScale.x;   // o gerador exporta com SCALE = 1,2 (build_vila_jardim.py), igual ao FBTour Layer = ref0.gameObject.layer;
            Debug.Log($"[FreeBala] extras VJ: pai dos nós = {P.name} escala {S:0.###} camada {Layer} (referência {ref0.name})");
            var mapMats = sc.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).SelectMany(r => r.sharedMaterials).Where(m => m).Distinct().ToList();
            // base lisa opaca (sem textura) e vidro do mapa
            string N(Material m) => m.name.StartsWith("VJ_") ? m.name.Substring(3) : m.name;
            var liso = mapMats.FirstOrDefault(m => N(m).StartsWith("abrigo54")) ?? mapMats.FirstOrDefault(m => N(m).StartsWith("galv54")) ?? mapMats.FirstOrDefault(m => m.shader == FBShaders.Lit && !m.GetTexture("_BaseMap") && m.renderQueue < 2450);
            var vidro = mapMats.FirstOrDefault(m => N(m) == "glass") ?? mapMats.FirstOrDefault(m => N(m).ToLowerInvariant().StartsWith("glass")) ?? mapMats.FirstOrDefault(m => N(m).ToLowerInvariant().StartsWith("vidro")) ?? mapMats.FirstOrDefault(m => m.name.ToLowerInvariant().Contains("vidro"));
            var conc = mapMats.FirstOrDefault(m => N(m).StartsWith("calcada54")) ?? mapMats.FirstOrDefault(m => N(m).StartsWith("concrete"));
            if (!liso) throw new System.Exception("material liso de base não achado");
            Debug.Log($"[FreeBala] extras VJ: base {liso.name} ({liso.shader.name}) · vidro {(vidro ? vidro.name : "—")} · concreto {(conc ? conc.name : "—")}");
            System.IO.Directory.CreateDirectory(Dir);
            Material M(string nome, Color32 c, Material src = null, float liso01 = .25f)
            {
                if (mats.TryGetValue(nome, out var m0) && m0) return m0;
                var path = $"{Dir}/{nome}.mat"; AssetDatabase.DeleteAsset(path);
                var m = new Material(src ? src : liso) { name = nome }; foreach (var pc in new[] { "_BaseColor", "baseColorFactor", "_Color" }) if (m.HasProperty(pc)) m.SetColor(pc, c);   // URP/Lit ou glTF-pbr (Shader Graph)
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", liso01); if (m.HasProperty("roughnessFactor")) m.SetFloat("roughnessFactor", 1 - liso01);
                foreach (var pt in new[] { "_BaseMap", "baseColorTexture" }) if (m.HasProperty(pt)) m.SetTexture(pt, null);
                AssetDatabase.CreateAsset(m, path); return mats[nome] = m;
            }
            cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            Root = new GameObject("ExtrasVJ_r63").transform;

            // ---------------- ponto de ônibus de vidro (p019_dir)
            {
                Vector2 A = new(113.49f, 91.27f), Bp = new(115.90f, 98.03f); var along = (Bp - A).normalized; var back = new Vector2(-along.y, along.x);   // trás = oeste (longe da pista)
                if (Vector2.Dot(back, new Vector2(-1, 0)) < 0) back = -back;
                var c = (A + Bp) * .5f; float W = Vector2.Distance(A, Bp), D = 1.7f, H = 2.65f;
                float y0 = Ground(c + back * .8f);
                var g = Part("Ponto_Onibus_Vidro_p019");
                var mLaje = conc ? conc : M("ext_laje", new Color32(150, 141, 125, 255));
                Box(g, "laje", mLaje, c + back * .45f, along, 0, .16f, W + 1.6f, D + 1.9f, true);                                  // laje elevada (meio-fio)
                Box(g, "azul_n", M("ext_azul_acess", new Color32(70, 110, 170, 255), null, .35f), c - back * .55f - along * (W * .5f + .1f), along, .16f, .006f, 1.9f, .9f, false);
                Box(g, "azul_s", M("ext_azul_acess", new Color32(70, 110, 170, 255)), c - back * .55f + along * (W * .5f + .1f), along, .16f, .006f, 1.9f, .9f, false);
                Box(g, "tatil", M("ext_tatil", new Color32(196, 156, 92, 255), null, .2f), c - back * .55f, along, .16f, .008f, W - 1.0f, .4f, false);   // piso tátil
                var aco = M("ext_aco", new Color32(112, 107, 96, 255), null, .45f);
                foreach (float u in new[] { -.5f, .5f }) foreach (float v in new[] { 0f, 1f })
                        Box(g, "pilar", aco, c + along * (u * W) + back * (v * D), along, .16f, H, .1f, .1f, true);
                Box(g, "teto", M("ext_teto_branco", new Color32(196, 199, 199, 255), null, .4f), c + back * (D * .5f - .1f), along, .16f + H, .12f, W + .5f, D + .7f, true);
                Box(g, "viga", aco, c - back * .02f, along, .16f + H - .18f, .16f, W, .08f, false);
                var mv = vidro ? vidro : M("ext_vidro", new Color32(51, 59, 45, 120));
                Box(g, "vidro_fundo", mv, c + back * D, along, .36f, 2.2f, W - .1f, .02f, true);
                foreach (float u in new[] { -.5f, .5f }) Box(g, "vidro_lado", mv, c + along * (u * W) + back * (D * .5f), along, .36f, 2.2f, .02f, D - .1f, true);
                Box(g, "banco", M("ext_banco", new Color32(58, 60, 62, 255), null, .3f), c + back * (D - .35f), along, .45f + .16f, .06f, W * .55f, .38f, true);
                Box(g, "banco_pe", aco, c + back * (D - .35f), along, .16f, .45f, W * .5f, .05f, false);
                g.transform.position += Vector3.up * y0;
            }
            // ---------------- abrigo de lixo pichado do Ipê Amarelo (p018_dir)
            {
                Vector2 A = new(132.18f, 152.72f), Bp = new(133.54f, 156.97f); var along = (Bp - A).normalized; var back = new Vector2(-along.y, along.x);
                if (Vector2.Dot(back, new Vector2(-1, 0)) < 0) back = -back;
                var c = (A + Bp) * .5f; float W = 4.6f, D = 3.2f, H = 2.3f; float y0 = Ground(c + back * (D * .5f));
                var g = Part("Abrigo_Lixo_Ipe_p018");
                var reboco = M("ext_reboco_cinza", new Color32(168, 162, 146, 255), null, .1f);
                Box(g, "corpo", reboco, c + back * (D * .5f), along, 0, H, W, D, true);
                Box(g, "laje", reboco, c + back * (D * .5f), along, H, .1f, W + .2f, D + .2f, true);
                // frente (de quem vem da avenida, da esquerda p/ a direita = −along → +along): portão amarelo de tela, vão escuro, parede
                var ama = M("ext_portao_amarelo", new Color32(196, 168, 60, 255), null, .35f); var tela = M("ext_tela_escura", new Color32(70, 66, 58, 255), null, .2f);
                var cg = c - along * (W * .5f - .8f) - back * .02f;
                Box(g, "portao_tela", tela, cg, along, .1f, 1.9f, 1.4f, .03f, false);
                foreach (float u in new[] { -.7f, .7f }) Box(g, "portao_quadro", ama, cg + along * u, along, .1f, 1.95f, .06f, .06f, false);
                Box(g, "portao_topo", ama, cg, along, 1.98f, .06f, 1.46f, .06f, false); Box(g, "portao_base", ama, cg, along, .1f, .06f, 1.46f, .06f, false);
                Box(g, "vao", M("ext_vao_escuro", new Color32(30, 28, 25, 255), null, .05f), c - along * (W * .5f - 2.15f) - back * .015f, along, 0, 1.95f, 1.1f, .02f, false);
                g.transform.position += Vector3.up * y0;
            }
            // (p025_dir, abrigo verde do Araçá: já está no gerador desde a r54t — caixa_verde54, BT-23, cantos medidos; não duplicar)
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc);
            Debug.Log($"[FreeBala] extras VJ: {Root.GetComponentsInChildren<MeshRenderer>().Length} peças em 3 estruturas · cena salva");
        }

        // r63: tinta 'musgo' do Angelim clara/escura demais? razão térreo/creme no MESMO quadro nublado do vídeo B (b_0597/0612/0626/0642)
        // × creme do modelo (223,215,201) = (109,122,95) em média; a textura tem (84,94,67) → fator ×1,30/1,30/1,42 na cor do material
        public static void CoresBatch()
        {
            int code = 0;
            try
            {
                var sc = EditorSceneManager.OpenScene($"{FBImport.SceneDir}/Map_vila_jardim.unity");
                var ms = sc.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).SelectMany(r => r.sharedMaterials).Where(m => m).Distinct()
                           .Where(m => m.name == "VJ_acc_musgo" || m.name == "VJ_band_musgo").ToList();
                var k = new Color(1.30f, 1.30f, 1.42f, 1);
                foreach (var m in ms)
                {
                    string cp = m.HasProperty("baseColorFactor") ? "baseColorFactor" : "_BaseColor";
                    m.SetColor(cp, k); EditorUtility.SetDirty(m);
                    Debug.Log($"[FreeBala] cor r63: {m.name} {cp} = {k} · arquivo {AssetDatabase.GetAssetPath(m)}");
                }
                AssetDatabase.SaveAssets();
                if (ms.Count == 0) Debug.LogWarning("[FreeBala] cor r63: materiais musgo não achados");
            }
            catch (System.Exception e) { Debug.LogError("[FreeBala] cor r63 falhou: " + e); code = 1; }
            EditorApplication.Exit(code);
        }

        static Vector3 W(Vector2 b) => P.TransformPoint(new Vector3(-GenScale * b.x, 0, -GenScale * b.y));                // quadro do gerador → Unity (y = cota da raiz)
        static float Ground(Vector2 b)
        {
            var w = W(b); int mask = ~(1 << FBProjectSetup.LHitbox | 1 << FBProjectSetup.LTrigger);
            if (Physics.Raycast(w + Vector3.up * 60, Vector3.down, out var h, 140, mask, QueryTriggerInteraction.Ignore)) return h.point.y - w.y;
            Debug.LogWarning($"[FreeBala] extras VJ: sem chão em {b}"); return 0;
        }
        static GameObject Part(string nome) { var g = new GameObject(nome); g.transform.SetParent(Root, false); g.layer = Layer; return g; }
        // caixa: centro no chão (quadro do gerador), 'along' = eixo comprido do conjunto; z0 = base acima do chão; tamanhos em metros reais
        static void Box(GameObject parent, string nome, Material m, Vector2 c, Vector2 along, float z0, float h, float len, float dep, bool col)
        {
            var w = W(c); var fw = P.TransformDirection(new Vector3(-along.x, 0, -along.y)); fw.y = 0; fw.Normalize();
            var go = new GameObject(nome) { layer = Layer }; go.transform.SetParent(parent.transform, false);
            go.transform.SetPositionAndRotation(w + Vector3.up * ((z0 + h * .5f) * S), Quaternion.LookRotation(fw, Vector3.up));
            go.transform.localScale = new Vector3(dep * S, h * S, len * S);
            go.AddComponent<MeshFilter>().sharedMesh = cube; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = m;
            if (m.renderQueue >= 2450) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (col) go.AddComponent<BoxCollider>();
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }
    }
}
