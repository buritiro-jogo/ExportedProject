// Vidro das janelas dentro das malhas dos prédios (Vila Jardim: o vidro é um dos materiais da malha de cada bloco). A importação só marcava
// como vidro quebrável o objeto cujo material PRINCIPAL era vidro → nenhuma janela dos prédios quebrava nem acendia. Aqui:
//  1) Split: cada submalha de vidro vira um objeto filho "vidro" (a malha original fica sem ela) — o FBImport depois põe camada Glass,
//     colisor e FBGlass nesse filho (quebra por painel);
//  2) Measure: mede cada painel (componente conexo) e grava FBWindowData na raiz do mapa (centro, normal para fora, tamanho) para as
//     janelas acesas à noite (FBWindowLights).
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBGlassSplit
    {
        static readonly Regex GLASS = new(@"^(VJ_|PA_|PT_|OR_)?glass$", RegexOptions.IgnoreCase);
        public static int Split(string id, GameObject map)
        {
            int n = 0; var dir = "Assets/FreeBala/Art/Glass"; System.IO.Directory.CreateDirectory(dir);
            foreach (var mf in map.GetComponentsInChildren<MeshFilter>(true).ToList())
            {
                var r = mf.GetComponent<MeshRenderer>(); var m = mf.sharedMesh; if (!r || !m || m.subMeshCount < 2) continue; var mats = r.sharedMaterials;
                var gi = Enumerable.Range(0, Mathf.Min(mats.Length, m.subMeshCount)).Where(i => mats[i] && GLASS.IsMatch(mats[i].name)).ToList(); if (gi.Count == 0) continue;
                // filho só com o vidro
                var gm = new Mesh { name = m.name + "_vidro", indexFormat = m.indexFormat }; gm.SetVertices(m.vertices); if (m.normals.Length == m.vertexCount) gm.SetNormals(m.normals); if (m.uv.Length == m.vertexCount) gm.SetUVs(0, m.uv);
                var gt = new List<int>(); foreach (var i in gi) gt.AddRange(m.GetTriangles(i)); gm.SetTriangles(gt, 0); gm.RecalculateBounds();
                // original sem o vidro (mesmos submeshes/materiais, vidro vazio)
                var om = Object.Instantiate(m); om.name = m.name + "_semvidro"; foreach (var i in gi) om.SetTriangles(new int[0], i);
                var p1 = $"{dir}/{id}_{mf.name}_vidro.asset"; var p2 = $"{dir}/{id}_{mf.name}_semvidro.asset";
                AssetDatabase.DeleteAsset(p1); AssetDatabase.DeleteAsset(p2); AssetDatabase.CreateAsset(gm, p1); AssetDatabase.CreateAsset(om, p2);
                mf.sharedMesh = om; var mc = mf.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh == m) mc.sharedMesh = om;
                var g = new GameObject("vidro"); g.transform.SetParent(mf.transform, false); g.layer = mf.gameObject.layer;
                g.AddComponent<MeshFilter>().sharedMesh = gm; var gr = g.AddComponent<MeshRenderer>(); gr.sharedMaterial = mats[gi[0]]; gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                n++;
            }
            Debug.Log($"[FreeBala][vidro] {id}: {n} malhas com vidro separadas");
            return n;
        }

        public static int Measure(GameObject map)
        {
            var C = new List<Vector3>(); var N = new List<Vector3>(); var S = new List<Vector2>();
            foreach (var g in map.GetComponentsInChildren<FBGlass>(true))
            {
                var mf = g.GetComponent<MeshFilter>(); var m = mf ? mf.sharedMesh : null; if (!m) continue;
                var V = m.vertices; var T = m.triangles; var M = mf.transform.localToWorldMatrix; var host = g.transform.parent ? g.transform.parent.GetComponent<Renderer>() : null;
                var bc = host ? host.bounds.center : g.GetComponent<Renderer>().bounds.center;
                var pid = new Dictionary<Vector3Int, int>(); var vid = new int[V.Length]; var W = new Vector3[V.Length];
                for (int i = 0; i < V.Length; i++) { W[i] = M.MultiplyPoint3x4(V[i]); var k = Vector3Int.RoundToInt(W[i] * 200); if (!pid.TryGetValue(k, out var id)) pid[k] = id = pid.Count; vid[i] = id; }
                var par = Enumerable.Range(0, pid.Count).ToArray(); int F(int x) { while (par[x] != x) x = par[x] = par[par[x]]; return x; }
                for (int t = 0; t < T.Length; t += 3) { int a = F(vid[T[t]]); par[F(vid[T[t + 1]])] = a; par[F(vid[T[t + 2]])] = a; }
                var groups = new Dictionary<int, Bounds>();
                foreach (var t in T) { int r = F(vid[t]); if (groups.TryGetValue(r, out var b)) { b.Encapsulate(W[t]); groups[r] = b; } else groups[r] = new Bounds(W[t], Vector3.zero); }
                foreach (var b in groups.Values)
                {
                    float sx = b.size.x, sz = b.size.z, h = b.size.y; if (h < .35f || h > 3.5f) continue;
                    Vector3 n = sx < sz ? Vector3.right : Vector3.forward; float w = Mathf.Max(sx, sz); if (w < .3f || w > 4) continue;
                    if (Vector3.Dot(b.center - bc, n) < 0) n = -n;
                    C.Add(b.center); N.Add(n); S.Add(new Vector2(w, h));
                }
            }
            var d = map.GetComponent<FBWindowData>(); if (!d) d = map.AddComponent<FBWindowData>(); d.center = C.ToArray(); d.normal = N.ToArray(); d.size = S.ToArray();
            Debug.Log($"[FreeBala][vidro] janelas medidas: {C.Count}");
            return C.Count;
        }
    }
}
