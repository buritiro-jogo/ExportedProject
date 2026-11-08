// r63 (08/11/2026): POÇAS PERMANENTES do mapa (Vila Jardim), assadas na cena — pedido do usuário: "poças de água, estradas barrentas…
// 100 % fiéis". Evidência: vídeos do usuário (F:/CRIAÇÃO DE GAMES ASSETS/VILA IMAGENS, 08/11; caminhada r54 b_0570/0597/0612/0626/0642):
//  · na LATERITA entre os blocos e nos pátios de terra a água fica PARADA em manchas grandes e compridas, cor de barro alaranjado
//    (mediana do b_0642: 179,123,64 com o reflexo do céu nublado), borda de terra molhada mais escura (129,96,73);
//  · no ASFALTO (ruas internas, estacionamentos, avenida) as poças são escuras e ficam encostadas no meio-fio/sarjeta (vídeo da moto, 08/11).
// Como: amostra o chão do mapa numa grade de 2,5 m (raio para baixo + material do TRIÂNGULO atingido, como o FBVegBake), escolhe manchas por
// ruído de baixa frequência (zonas de alagamento ~20 m) e só onde o chão é plano e descoberto; cada poça = um quadrado com a forma por ruído
// do shader FreeBala/Puddle (_Fixo = enchimento permanente). Malhas por célula de 64 m (culling), dois materiais (barro / asfalto).
//   Unity -batchmode -executeMethod FreeBala.EditorTools.FBMapLook.PocasBatch -fbmap vila_jardim [-fbseco]
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static partial class FBMapLook
    {
        const string PocaDir = "Assets/FreeBala/Art/Pocas";
        static readonly Regex PocaSolo = new("laterita|terra|dirt|barro|mud|lama|rua_terra|yard", RegexOptions.IgnoreCase);
        static readonly Regex PocaAsf = new("asfalto|asphalt|road_worn|road|gasto54|bloquete", RegexOptions.IgnoreCase);
        static readonly Regex PocaNao = new("quadra|court|paint|pintura|faixa|linha|zebra|grass|grama|telha|roof|laje", RegexOptions.IgnoreCase);

        public static void PocasBatch()
        {
            var args = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(args, "-fbmap"); string id = i >= 0 && i + 1 < args.Length ? args[i + 1] : "vila_jardim";
            bool seco = System.Array.IndexOf(args, "-fbseco") >= 0; int code = 0;
            try { Pocas(id, seco); } catch (System.Exception e) { Debug.LogError("[FreeBala] poças falharam: " + e); code = 1; }
            EditorApplication.Exit(code);
        }

        // r63: grama refeita (terra pelada) + poças, numa sessão só do Unity:  -executeMethod FreeBala.EditorTools.FBMapLook.R63Batch -fbmap vila_jardim
        public static void R63Batch()
        {
            var args = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(args, "-fbmap"); string id = i >= 0 && i + 1 < args.Length ? args[i + 1] : "vila_jardim";
            int code = 0;
            try { if (!VegRebake(id)) code = 1; Pocas(id, false); } catch (System.Exception e) { Debug.LogError("[FreeBala] r63 falhou: " + e); code = 1; }
            EditorApplication.Exit(code);
        }

        public static void Pocas(string id, bool seco)
        {
            var sc = EditorSceneManager.OpenScene($"{FBImport.SceneDir}/Map_{id}.unity");
            var map = sc.GetRootGameObjects().FirstOrDefault(g => g.name == "Map_" + id) ?? sc.GetRootGameObjects().FirstOrDefault(g => g.name.StartsWith("Map_"));
            if (!map) throw new System.Exception("raiz Map_ não achada");
            foreach (var g in sc.GetRootGameObjects()) if (g.name == "Pocas") Object.DestroyImmediate(g);
            // o MeshFilter tem a cópia com Mesh LOD; o índice de triângulo do raio é do colisor (malha original): troca durante a amostragem
            var swap = new List<(MeshFilter mf, Mesh m)>();
            foreach (var mf in map.GetComponentsInChildren<MeshFilter>(true)) { var mc = mf.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh && mf.sharedMesh != mc.sharedMesh) { swap.Add((mf, mf.sharedMesh)); mf.sharedMesh = mc.sharedMesh; } }
            var got = new List<(Vector3 p, float sx, float sz, float yaw, float seed, int kind)>();
            var nMat = new Dictionary<string, int>();
            try
            {
                Physics.SyncTransforms();
                // caixa: chão de terra/asfalto do mapa (renderers com esses materiais)
                var rends = map.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.sharedMaterials.Any(m => m && (PocaSolo.IsMatch(m.name) || PocaAsf.IsMatch(m.name)) && !PocaNao.IsMatch(m.name))).ToList();
                if (rends.Count == 0) throw new System.Exception("nenhum chão de terra/asfalto");
                var B = rends[0].bounds; foreach (var r in rends) B.Encapsulate(r.bounds);
                var play = PlayArea(id);                                                         // área do jogo + 120 m (o resto do bairro é só paisagem)
                if (play.HasValue) { var pr = play.Value; B.SetMinMax(new Vector3(Mathf.Max(B.min.x, pr.xMin - 120), B.min.y, Mathf.Max(B.min.z, pr.yMin - 120)), new Vector3(Mathf.Min(B.max.x, pr.xMax + 120), B.max.y, Mathf.Min(B.max.z, pr.yMax + 120))); }
                int mask = 1 << FBProjectSetup.LMap, solid = ~(1 << FBProjectSetup.LHitbox | 1 << FBProjectSetup.LTrigger);
                var cache = new Dictionary<Mesh, int[]>(); const float sp = 2.5f;
                int nx = Mathf.CeilToInt(B.size.x / sp), nz = Mathf.CeilToInt(B.size.z / sp), amostras = 0;
                bool Encostado(Vector3 p, float r)
                {
                    var o = p + Vector3.up * .08f;                                               // meio-fio tem ~0,15 m: raio baixo
                    for (int k = 0; k < 8; k++) if (Physics.Raycast(o, Quaternion.Euler(0, k * 45, 0) * Vector3.forward, r, solid, QueryTriggerInteraction.Ignore)) return true;
                    return false;
                }
                // plano: 8 pontos em volta dentro de 4 cm (senão a poça fura o chão ou flutua no meio-fio); devolve a cota de cima
                bool Plano(Vector3 c, float rx, float rz, float yaw, out float top)
                {
                    top = c.y; var q = Quaternion.Euler(0, yaw, 0);
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * Mathf.PI / 4; var o = c + q * new Vector3(Mathf.Cos(a) * rx * .42f, 0, Mathf.Sin(a) * rz * .42f);
                        if (!Physics.Raycast(o + Vector3.up * .6f, Vector3.down, out var h, 1.4f, mask, QueryTriggerInteraction.Ignore)) return false;
                        if (Mathf.Abs(h.point.y - c.y) > .04f) return false; top = Mathf.Max(top, h.point.y);
                    }
                    return true;
                }
                for (int x = 0; x < nx; x++) for (int z = 0; z < nz; z++)
                    {
                        float jx = Hash(x, z, 31) - .5f, jz = Hash(x, z, 32) - .5f;
                        var o = new Vector3(B.min.x + (x + .5f + jx * .8f) * sp, B.max.y + 30, B.min.z + (z + .5f + jz * .8f) * sp);
                        if (!Physics.Raycast(o, Vector3.down, out var h, B.size.y + 80, mask, QueryTriggerInteraction.Ignore) || h.normal.y < .985f) continue;
                        var m = MatAt(h, cache); if (!m || PocaNao.IsMatch(m.name)) continue;
                        int kind = PocaSolo.IsMatch(m.name) ? 0 : PocaAsf.IsMatch(m.name) ? 1 : -1; if (kind < 0) continue;
                        amostras++;
                        var p = h.point;
                        if (Physics.Raycast(p + Vector3.up * .3f, Vector3.up, 30, solid, QueryTriggerInteraction.Ignore)) continue;   // coberto (carport, quiosque): seco
                        bool lama = m.name.ToLowerInvariant().Contains("lama");
                        // zonas de alagamento: ruído de ~22 m; a terra alaga muito mais que o asfalto
                        float zn = WN(p.x + 113, p.z - 71, 22, 3), r = Hash(x, z, 33);
                        float sx, sz, yaw = Hash(x, z, 34) * 360;
                        if (kind == 0)
                        {
                            // ruas de terra / becos entre as fileiras (rua_terra54, chao_terra54) alagam em faixas compridas (b_0626, b_0642);
                            // quintal (dirt_yard) e terra solta, bem menos
                            var mn = m.name.ToLowerInvariant(); bool via = mn.Contains("rua_terra") || mn.Contains("chao_terra") || mn.Contains("laterita");
                            if (!lama && (zn < (via ? .45f : .56f) || r > (via ? .72f : .45f))) continue;
                            sx = Mathf.Lerp(2.4f, lama ? 8f : via ? 9f : 5.0f, Hash(x, z, 35)); sz = sx * Mathf.Lerp(.32f, .8f, Hash(x, z, 36));
                        }
                        else
                        {
                            // asfalto: só junto do meio-fio/sarjeta (1,6 m) e em poucas zonas
                            if (zn < .6f || r > .35f || !Encostado(p, 1.6f)) continue;
                            sx = Mathf.Lerp(1.6f, 4.2f, Hash(x, z, 35)); sz = sx * Mathf.Lerp(.3f, .6f, Hash(x, z, 36));
                        }
                        if (!Plano(p, sx, sz, yaw, out float top)) { sx *= .55f; sz *= .55f; if (!Plano(p, sx, sz, yaw, out top)) continue; }
                        p.y = top;
                        if (got.Any(g => (new Vector2(g.p.x - p.x, g.p.z - p.z)).magnitude < Mathf.Max(g.sx, sx) * .45f)) continue;   // sobreposição forte
                        got.Add((p, sx, sz, yaw, Hash(x, z, 37), kind)); nMat[m.name] = nMat.TryGetValue(m.name, out var c0) ? c0 + 1 : 1;
                    }
                Debug.Log($"[FreeBala] poças {id}: {got.Count} ({got.Count(g => g.kind == 0)} barro, {got.Count(g => g.kind == 1)} asfalto) de {amostras} amostras de chão · por material: {string.Join(", ", nMat.OrderByDescending(k => k.Value).Select(k => $"{k.Key} {k.Value}"))}");
            }
            finally { foreach (var (mf, m) in swap) if (mf) mf.sharedMesh = m; }
            if (seco || got.Count == 0) return;

            // ---- materiais (shader das poças da chuva, já no build pelo FBShaderRefs) e malhas por célula
            System.IO.Directory.CreateDirectory(PocaDir);
            var sh = Shader.Find("FreeBala/Puddle"); if (!sh) throw new System.Exception("shader FreeBala/Puddle não achado");
            Material Mat(string nome, Color agua, Color borda, float liso, float metal)
            {
                var path = $"{PocaDir}/{nome}.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m) { m = new Material(sh) { name = nome }; AssetDatabase.CreateAsset(m, path); } else m.shader = sh;
                m.SetColor("_Color", agua); m.SetColor("_EdgeColor", borda); m.SetFloat("_Smooth", liso); m.SetFloat("_Fixo", 1f); m.SetFloat("_Metal", metal); m.SetColor("_Tinta", new Color32(179, 123, 64, 255));   m.enableInstancing = false;   // _Tinta = mediana da poça no b_0642
                EditorUtility.SetDirty(m); return m;
            }
            // barro: água opaca de laterita (sRGB ~150,95,45 → linear) e borda de terra molhada (sRGB ~110,78,55); asfalto: água escura
            var mats = new[] { Mat("poca_barro", new Color(.305f, .115f, .026f), new Color(.155f, .077f, .038f), .9f, .85f),
                               Mat("poca_asfalto", new Color(.022f, .024f, .027f), new Color(.045f, .045f, .047f), .94f, 0f) };
            foreach (var old in AssetDatabase.FindAssets("t:Mesh", new[] { PocaDir })) AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(old));
            var root = new GameObject("Pocas") { layer = FBProjectSetup.LMap };
            const float C = 64; int nm = 0;
            foreach (var cell in got.GroupBy(g => (g.kind, Mathf.FloorToInt(g.p.x / C), Mathf.FloorToInt(g.p.z / C))))
            {
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var uv2 = new List<Vector2>(); var tri = new List<int>();
                var ctr = new Vector3((cell.Key.Item2 + .5f) * C, cell.Average(g => g.p.y), (cell.Key.Item3 + .5f) * C);
                foreach (var (p, sx, sz, yaw, seed, _) in cell)
                {
                    var rot = Quaternion.Euler(0, yaw, 0); int b = v.Count;
                    foreach (var (u, w) in new[] { (0f, 0f), (1f, 0f), (1f, 1f), (0f, 1f) })
                    { v.Add(p - ctr + Vector3.up * .014f + rot * new Vector3((u - .5f) * sx, 0, (w - .5f) * sz)); uv.Add(new Vector2(u, w)); uv2.Add(new Vector2(seed, Hash(Mathf.RoundToInt(p.x * 10), Mathf.RoundToInt(p.z * 10), 38))); }
                    tri.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
                }
                var mesh = new Mesh { name = $"poca_{cell.Key.kind}_{cell.Key.Item2}_{cell.Key.Item3}" }; mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetUVs(1, uv2); mesh.SetTriangles(tri, 0);
                mesh.SetNormals(v.ConvertAll(_ => Vector3.up)); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, $"{PocaDir}/{mesh.name}.asset");
                var go = new GameObject(mesh.name) { layer = FBProjectSetup.LMap }; go.transform.SetParent(root.transform, false); go.transform.position = ctr;
                go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mats[cell.Key.kind];
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = true; mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic); nm++;
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc);
            Debug.Log($"[FreeBala] poças {id}: {nm} malhas em {PocaDir}, cena salva");
        }
    }
}
