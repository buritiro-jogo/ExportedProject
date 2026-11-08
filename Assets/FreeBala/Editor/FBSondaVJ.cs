// r62 (08/11/2026): SONDA da Vila Jardim — lista os renderers perto de pontos do mapa (x leste, y norte em metros reais) com malha,
// materiais, textura base, cor e níveis de LOD. Serve para achar o que o jogo desenha onde o vídeo mostra outra coisa.
// Lote: Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBSondaVJ.Batch -fbsonda "nome:x:y:raio;..."
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBSondaVJ
    {
        static string Desc(Material z)
        {
            string tp = z.HasProperty("_BaseMap") ? "_BaseMap" : z.HasProperty("baseColorTexture") ? "baseColorTexture" : null; var t = tp != null ? z.GetTexture(tp) : null;
            string cp = z.HasProperty("_BaseColor") ? "_BaseColor" : z.HasProperty("baseColorFactor") ? "baseColorFactor" : null;
            string ti = t ? $"{t.name} {t.width}x{t.height} st {z.GetTextureScale(tp):F2}" : "-";
            return $"{z.name}[{(z.shader ? z.shader.name.Replace("Shader Graphs/", "") : "?")}|tex {ti}|cor {(cp != null ? ColorUtility.ToHtmlStringRGB(z.GetColor(cp)) : "-")}]";
        }
        // r62: grava a máscara de grama ATG da VJ como PNG (norte para cima, leste à direita no MAPA: Unity x = -1,2·leste, z = -1,2·norte)
        public static void Mascara()
        {
            int code = 0;
            try
            {
                var d = AssetDatabase.LoadAssetAtPath<FBVegData>("Assets/FreeBala/Art/Vegetation/vila_jardim_veg.asset"); var mk = d.grassMask;
                var raw = mk.GetRawTextureData<byte>(); int w = mk.width, h = mk.height; var o = new Texture2D(w, h, TextureFormat.RGB24, false);
                var px = new Color32[w * h]; for (int i = 0; i < px.Length && i < raw.Length; i++) { byte v = raw[i]; px[i] = new Color32(v, v, v, 255); }
                o.SetPixels32(px); o.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath("../logs/vj_grama_mascara.png"), o.EncodeToPNG());
                Debug.Log($"[FreeBala][sonda] máscara {w}x{h} {mk.format} rect {d.grassRect} y0 {d.grassY0}");
            }
            catch (System.Exception e) { Debug.LogError("[FreeBala][sonda] " + e); code = 1; }
            EditorApplication.Exit(code);
        }
        // r62: FOTO NO EDITOR das poses do tour (mesma conversão do FBTour sv) — testa conserto de malha/material sem build de 15 min.
        // -fbfoto <tour.json> <pastaSaída> [nomes,separados]
        [System.Serializable] class Pt { public string n; public float x, y, h, z = 2.5f, fov = 60f, p; }
        [System.Serializable] class PtList { public Pt[] pts; }
        // mesmo estado do FBDistanceCull: grupo <bloco>_PERTO/_LONGE pela distância da câmera à caixa do grupo (120 m), interior a 60 m
        static System.Collections.Generic.Dictionary<string, (System.Collections.Generic.List<Renderer> on, System.Collections.Generic.List<Renderer> off, Bounds b, float d)> grupos;
        static void CorteComoJogo(Vector3 p)
        {
            if (grupos == null)
            {
                grupos = new System.Collections.Generic.Dictionary<string, (System.Collections.Generic.List<Renderer>, System.Collections.Generic.List<Renderer>, Bounds, float)>();
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    string tag = null; bool off = false; float d = 120;
                    for (var t = r.transform; t && tag == null; t = t.parent)
                    {
                        var n = t.name; int k;
                        if ((k = n.IndexOf("_PERTO", System.StringComparison.Ordinal)) > 0) tag = n.Substring(0, k);
                        else if ((k = n.IndexOf("_LONGE", System.StringComparison.Ordinal)) > 0) { tag = n.Substring(0, k); off = true; }
                        else if (n.EndsWith("_interior")) { tag = n; d = 60; }
                    }
                    if (tag == null) continue;
                    if (!grupos.TryGetValue(tag, out var g)) g = (new System.Collections.Generic.List<Renderer>(), new System.Collections.Generic.List<Renderer>(), r.bounds, d);
                    (off ? g.off : g.on).Add(r); g.b.Encapsulate(r.bounds); grupos[tag] = g;
                }
            }
            foreach (var g in grupos.Values) { bool near = g.b.SqrDistance(p) < g.d * g.d; foreach (var r in g.on) r.forceRenderingOff = !near; foreach (var r in g.off) r.forceRenderingOff = near; }
        }
        public static void Foto()
        {
            int code = 0;
            try
            {
                var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbfoto"); var json = a[i + 1]; var outDir = a[i + 2];
                var only = i + 3 < a.Length && !a[i + 3].StartsWith("-") ? new System.Collections.Generic.HashSet<string>(a[i + 3].Split(',')) : null;
                System.IO.Directory.CreateDirectory(outDir);
                EditorSceneManager.OpenScene("Assets/FreeBala/Scenes/Map_vila_jardim.unity", OpenSceneMode.Single);
                FBSondaFix.Aplicar();
                var go = new GameObject("FotoCam"); var cam = go.AddComponent<Camera>(); cam.nearClipPlane = .1f; cam.farClipPlane = 2500;
                var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); cam.targetTexture = rt;
                foreach (var p in JsonUtility.FromJson<PtList>(System.IO.File.ReadAllText(json)).pts)
                {
                    if (only != null && !only.Contains(p.n)) continue;
                    var flat = new Vector3(-1.2f * p.x, 0, -1.2f * p.y);
                    float gy = Physics.Raycast(flat + Vector3.up * 80, Vector3.down, out var hit, 160, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : 0;
                    float hr = p.h * Mathf.Deg2Rad; var dirv = new Vector3(-Mathf.Cos(hr), 0, -Mathf.Sin(hr)).normalized;
                    var olhar = Quaternion.AngleAxis(p.p, Vector3.Cross(Vector3.up, dirv)) * dirv;
                    cam.fieldOfView = p.fov; cam.transform.SetPositionAndRotation(new Vector3(flat.x, gy + p.z, flat.z), Quaternion.LookRotation(olhar));
                    CorteComoJogo(cam.transform.position);
                    cam.Render(); var prev = RenderTexture.active; RenderTexture.active = rt;
                    var t = new Texture2D(1280, 720, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); t.Apply(); RenderTexture.active = prev;
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(outDir, p.n + ".png"), t.EncodeToPNG()); Object.DestroyImmediate(t);
                    Debug.Log($"[FreeBala][sonda] foto {p.n} chão {gy:0.00}");
                }
            }
            catch (System.Exception e) { Debug.LogError("[FreeBala][sonda] " + e); code = 1; }
            EditorApplication.Exit(code);
        }
        // r62: submalhas × materiais de objetos da cena e da malha original do glTF (-fbsub "VJ10_Quadra_07,VJ54_Estruturas_Soltas_Video")
        public static void Sub()
        {
            int code = 0;
            try
            {
                var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbsub"); var names = a[i + 1].Split(',');
                EditorSceneManager.OpenScene("Assets/FreeBala/Scenes/Map_vila_jardim.unity", OpenSceneMode.Single);
                var gl = AssetDatabase.LoadAllAssetsAtPath("Assets/FreeBala/Resources/FBImported/Maps/vila_jardim.gltf");
                foreach (var nm in names)
                {
                    var go = GameObject.Find("Map_vila_jardim/" + nm); if (!go) { Debug.Log($"[FreeBala][sonda] sub {nm}: não achado"); continue; }
                    var r = go.GetComponent<MeshRenderer>(); var mf = go.GetComponent<MeshFilter>(); var m = mf.sharedMesh;
                    Debug.Log($"[FreeBala][sonda] sub {nm}: malha {m.name} @ {AssetDatabase.GetAssetPath(m)} · submalhas {m.subMeshCount} · mats {r.sharedMaterials.Length} · lod {m.lodCount} · vért {m.vertexCount}");
                    for (int k = 0; k < m.subMeshCount; k++) { var d = m.GetSubMesh(k); Debug.Log($"[FreeBala][sonda]   [{k}] {(k < r.sharedMaterials.Length && r.sharedMaterials[k] ? r.sharedMaterials[k].name : "-")} · índices {d.indexStart}+{d.indexCount} · caixa {d.bounds.center:F1} tam {d.bounds.size:F1}"); }
                    foreach (var o in gl.OfType<Mesh>().Where(x => x.name.StartsWith(nm)))
                    {
                        Debug.Log($"[FreeBala][sonda]   glTF {o.name} · submalhas {o.subMeshCount} · vért {o.vertexCount}");
                        for (int k = 0; k < o.subMeshCount; k++) { var d = o.GetSubMesh(k); Debug.Log($"[FreeBala][sonda]     [{k}] índices {d.indexStart}+{d.indexCount} · caixa {d.bounds.center:F1} tam {d.bounds.size:F1}"); }
                    }
                }
            }
            catch (System.Exception e) { Debug.LogError("[FreeBala][sonda] " + e); code = 1; }
            EditorApplication.Exit(code);
        }
        public static void Batch()
        {
            int code = 0;
            try
            {
                var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbsonda"); var spec = i >= 0 ? a[i + 1] : "";
                EditorSceneManager.OpenScene("Assets/FreeBala/Scenes/Map_vila_jardim.unity", OpenSceneMode.Single);
                var rends = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var p in spec.Split(';'))
                {
                    var f = p.Split(':'); if (f.Length < 4) continue;
                    float x = float.Parse(f[1], System.Globalization.CultureInfo.InvariantCulture), y = float.Parse(f[2], System.Globalization.CultureInfo.InvariantCulture), r = float.Parse(f[3], System.Globalization.CultureInfo.InvariantCulture);
                    var w = new Vector3(-1.2f * x, 0, -1.2f * y);
                    var near = rends.Where(q => { var b = q.bounds; var c = b.ClosestPoint(new Vector3(w.x, b.center.y, w.z)); return (new Vector2(c.x - w.x, c.z - w.z)).magnitude < r && b.size.x < float.Parse(f.Length > 4 ? f[4] : "80", System.Globalization.CultureInfo.InvariantCulture); })
                        .OrderBy(q => (q.bounds.center - w).sqrMagnitude).Take(30).ToList();
                    Debug.Log($"[FreeBala][sonda] {f[0]} ({x};{y}) unity {w} · {near.Count} renderers");
                    foreach (var q in near)
                    {
                        var mf = q.GetComponent<MeshFilter>(); var m = mf ? mf.sharedMesh : null; string path = ""; for (var t = q.transform; t && path.Length < 140; t = t.parent) path = t.name + "/" + path;
                        var mats = string.Join(", ", q.sharedMaterials.Where(z => z).Select(z => Desc(z)));
                        Debug.Log($"[FreeBala][sonda]   {path} · ativo {q.gameObject.activeInHierarchy}/{q.enabled} · caixa {q.bounds.center:F1} tam {q.bounds.size:F1} · malha {(m ? m.name : "-")} lod {(m ? m.lodCount : 0)} · {mats}");
                    }
                }
            }
            catch (System.Exception e) { Debug.LogError("[FreeBala][sonda] " + e); code = 1; }
            EditorApplication.Exit(code);
        }
    }
    // conserto em teste, aplicado antes da foto (o modo é escolhido por -fbfix)
    public static class FBSondaFix
    {
        public static void Aplicar()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbfix"); var modo = i >= 0 ? a[i + 1] : "nada";
            int n = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var mf = r.GetComponent<MeshFilter>(); var m = mf ? mf.sharedMesh : null; if (!m) continue;
                if (modo == "lod0" && m.lodCount > 1) { r.forceMeshLod = 0; n++; }
            }
            Debug.Log($"[FreeBala][sonda] conserto {modo}: {n} renderers");
        }
    }
}
