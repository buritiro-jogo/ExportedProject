// MORRO DA SACOLA - conferência da cena montada (só editor, não muda a cena):
//  · NavMesh: spawns/bots/waypoints/pontos em cima do NavMesh, caminho completo de cada base até a outra, rotas (lanes) e pares de bases.
//  · Fotos: câmeras nas vistas de um json (referencial do mapa: x leste, y norte, z cima) renderizadas pelo próprio URP da cena
//    (neblina, sol e LOD do jogo; sem o FBWeather, que só roda em Play).
// Lote: Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBMorroSacolaTeste.Batch
//        [-fbvistas <vistas.json>] [-fbsaida <pasta>]       (sem -nographics: as fotos precisam da GPU)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace FreeBala.EditorTools
{
    public static class FBMorroSacolaTeste
    {
        const string CENA = "Assets/FreeBala/Scenes/Map_morro_sacola.unity";
        [Serializable] class Vista { public string nome; public float[] pos, alvo; public float fov = 60; public int w = 1280, h = 720; }
        [Serializable] class Lista { public Vista[] v; }

        static string Arg(string k, string padrao)
        {
            var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, k);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : padrao;
        }

        // mapa (x leste, y norte, z cima) -> Unity: eixos do GLB conferidos no montador (leste = -X, norte = -Z)
        static Vector3 U(float[] p) => new Vector3(-p[0], p[2], -p[1]);

        // monta a cena (montador do parceiro + fb_data) e já confere; a cópia .bak do fb_data sai do StreamingAssets (iria no build)
        public static void MontaETesta()
        {
            BuritiroEditor.BuritiroMontaMapa.MontaMorroSacolaAuto();
            string bk = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../_backup/morro_sacola_20261103"));
            Directory.CreateDirectory(bk);
            foreach (var f in Directory.GetFiles(Application.streamingAssetsPath, "fb_data.json.*.bak*"))
            {
                if (f.EndsWith(".meta")) File.Delete(f);
                else File.Move(f, Path.Combine(bk, Path.GetFileName(f)));
            }
            AssetDatabase.Refresh();
            Batch();
        }

        [MenuItem("BURITIRO/Mapas/Morro da Sacola: conferir NavMesh e tirar fotos")]
        public static void Batch()
        {
            string saida = Arg("-fbsaida", Path.GetFullPath(Path.Combine(Application.dataPath, "../../../_diag/morro_sacola")));
            Directory.CreateDirectory(saida);
            var log = new StringBuilder();
            void L(string s) { log.AppendLine(s); Debug.Log("[MorroSacola] " + s); }
            int falhas = 0;
            try
            {
                var cena = EditorSceneManager.OpenScene(CENA, OpenSceneMode.Single);
                var raiz = cena.GetRootGameObjects();
                // NavMesh assada (o FBNavData só registra em Play)
                NavMeshDataInstance h = default;
                foreach (var go in raiz)
                    foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (mb == null || mb.GetType().Name != "FBNavData") continue;
                        var nd = mb.GetType().GetField("data")?.GetValue(mb) as NavMeshData;
                        if (nd) h = NavMesh.AddNavMeshData(nd);
                    }
                L("NavMesh registrada: " + h.valid);
                var tr = raiz.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
                List<Transform> Grupo(string k) => tr.Where(t => t.name.StartsWith(k + "_")).OrderBy(t => t.name.Length).ThenBy(t => t.name).ToList();

                // pontos soltos: precisam estar a menos de 1,5 m do NavMesh
                foreach (var k in new[] { "spawnA", "spawnB", "spawnPolice", "bots", "waypoints", "points" })
                {
                    var g = Grupo(k); int ok = 0; var ruins = new List<string>();
                    foreach (var t in g)
                        if (NavMesh.SamplePosition(t.position, out var hit, 1.5f, NavMesh.AllAreas)) ok++; else ruins.Add(t.name);
                    L($"{k}: {ok}/{g.Count} no NavMesh" + (ruins.Count > 0 ? "  fora: " + string.Join(", ", ruins) : ""));
                    falhas += ruins.Count;
                }
                float Caminho(Vector3 a, Vector3 b, out NavMeshPathStatus st)
                {
                    var p = new NavMeshPath(); st = NavMeshPathStatus.PathInvalid;
                    if (!NavMesh.SamplePosition(a, out var ha, 2f, NavMesh.AllAreas) || !NavMesh.SamplePosition(b, out var hb, 2f, NavMesh.AllAreas)) return -1;
                    NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, p); st = p.status;
                    float d = 0; for (int i = 1; i < p.corners.Length; i++) d += Vector3.Distance(p.corners[i - 1], p.corners[i]);
                    return d;
                }
                var sa = Grupo("spawnA"); var sb = Grupo("spawnB");
                if (sa.Count > 0 && sb.Count > 0)
                {
                    float d = Caminho(sa[0].position, sb[0].position, out var st);
                    L($"base A -> base B: {st}, {d:0} m a pé (reta {Vector3.Distance(sa[0].position, sb[0].position):0} m)");
                    if (st != NavMeshPathStatus.PathComplete) falhas++;
                }
                // rotas: cada trecho entre pontos consecutivos
                for (int r = 0; r < 8; r++)
                {
                    var g = Grupo("lane_" + r).Where(t => t.name.Split('_').Length == 3).OrderBy(t => int.Parse(t.name.Split('_')[2])).ToList();
                    if (g.Count == 0) continue;
                    int ruins = 0; float tot = 0;
                    for (int i = 1; i < g.Count; i++) { tot += Caminho(g[i - 1].position, g[i].position, out var st); if (st != NavMeshPathStatus.PathComplete) ruins++; }
                    L($"rota {r}: {g.Count} pontos, {tot:0} m, trechos sem caminho completo: {ruins}");
                    falhas += ruins;
                }
                for (int r = 0; r < 8; r++)
                {
                    var g = Grupo("basePair_" + r).Where(t => t.name.Split('_').Length == 3).OrderBy(t => t.name).ToList();
                    if (g.Count < 2) continue;
                    float d = Caminho(g[0].position, g[1].position, out var st);
                    L($"par de bases {r}: {st}, {d:0} m a pé");
                    if (st != NavMeshPathStatus.PathComplete) falhas++;
                }

                // fotos
                string vj = Arg("-fbvistas", null);
                if (vj != null && File.Exists(vj))
                {
                    var lista = JsonUtility.FromJson<Lista>("{\"v\":" + File.ReadAllText(vj) + "}");
                    var go = new GameObject("_camera_foto"); var cam = go.AddComponent<Camera>();
                    cam.nearClipPlane = 0.1f; cam.farClipPlane = 2500f; cam.clearFlags = CameraClearFlags.Skybox;
                    bool neblina = RenderSettings.fog;
                    foreach (var v in lista.v)
                    {
                        // vista aérea (acima de 80 m) é só documentação: a névoa do mapa (90-440 m) apagaria o chão inteiro
                        RenderSettings.fog = neblina && v.pos[2] < 80f;
                        var rt = new RenderTexture(v.w, v.h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                        go.transform.position = U(v.pos); go.transform.LookAt(U(v.alvo));
                        cam.fieldOfView = v.fov; cam.aspect = v.w / (float)v.h; cam.targetTexture = rt;
                        for (int k = 0; k < 2; k++) cam.Render();          // a 1ª passada escolhe os LOD
                        RenderTexture.active = rt;
                        var tex = new Texture2D(v.w, v.h, TextureFormat.RGB24, false);
                        tex.ReadPixels(new Rect(0, 0, v.w, v.h), 0, 0); tex.Apply();
                        File.WriteAllBytes(Path.Combine(saida, v.nome + ".jpg"), tex.EncodeToJPG(90));
                        RenderTexture.active = null; cam.targetTexture = null;
                        UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
                        L("foto " + v.nome + (RenderSettings.fog == neblina ? "" : " (sem névoa)"));
                    }
                    RenderSettings.fog = neblina;
                    UnityEngine.Object.DestroyImmediate(go);
                }
                if (h.valid) h.Remove();
            }
            catch (Exception e) { L("ERRO " + e); falhas++; }
            L(falhas == 0 ? "FIM sem falhas" : "FIM com " + falhas + " falha(s)");
            File.WriteAllText(Path.Combine(saida, "teste.txt"), log.ToString(), new UTF8Encoding(false));
            if (Application.isBatchMode) EditorApplication.Exit(falhas == 0 ? 0 : 1);
        }
    }
}
