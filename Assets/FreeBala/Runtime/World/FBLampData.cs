// Luminárias do mapa medidas na IMPORTAÇÃO (FBImport → FBLampData.Measure): em jogo as malhas não são legíveis e o FBWeather caía no
// centro do bounding box da malha — postes agrupados numa malha só (PA_Postes_LED_Praca, VJ_Postes_Fios…) viravam UMA luminária no meio
// do grupo, e a praça ficava sem luz. Aqui cada coluna com mais de 3 m (grade de 3 m) vira uma luminária na ponta, como antes no editor.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public class FBLampData : MonoBehaviour
    {
        public Vector3[] pos = new Vector3[0]; public bool[] sodium = new bool[0];   // luminária de vapor de sódio (material *_sodio: luz laranja)
        static readonly System.Text.RegularExpressions.Regex RX = new("poste|lamp|lumin|street_?light", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            HEAD = new(@"(^|_)(lamp|lampada|luminaria)(_on|_sodio|_sodium)?$|street_?light", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // 1) malha legível com material de LÂMPADA (PA_lamp, VJ_lampada…): cada grupo de triângulos desse material (grade de 2,5 m) é uma
        //    luminária, 0,35 m abaixo dela; 2) poste sem material de lâmpada: cada coluna com mais de 3 m (grade de 3 m) vira uma luminária na
        //    ponta, 0,9 m abaixo do topo (regra antiga do FBWeather); 3) malha ilegível: topo do bounding box (último recurso)
        public static List<Vector3> Compute(IEnumerable<MeshRenderer> rs) => Compute(rs, null);
        public static List<Vector3> Compute(IEnumerable<MeshRenderer> rs, List<bool> sod)
        {
            var res = new List<Vector3>(); bool curSod = false; void Add(Vector3 h) { if (!res.Any(p => (p - h).sqrMagnitude < 2.5f * 2.5f)) { res.Add(h); sod?.Add(curSod); } }
            foreach (var r in rs)
            {
                if (!r) continue; var mats = r.sharedMaterials;
                string n = r.name + " " + (r.transform.parent ? r.transform.parent.name : "") + " " + string.Join(" ", mats.Where(m => m).Select(m => m.name));
                if (!RX.IsMatch(n)) continue;
                var mf = r.GetComponent<MeshFilter>(); var mesh = mf ? mf.sharedMesh : null; var b = r.bounds;
                if (mesh && mesh.isReadable)
                {
                    var V = mesh.vertices; var M = r.transform.localToWorldMatrix; bool any = false;
                    for (int si = 0; si < Mathf.Min(mesh.subMeshCount, mats.Length); si++)
                    {
                        if (!mats[si] || !HEAD.IsMatch(mats[si].name.Replace(" (Instance)", ""))) continue; any = true; curSod = System.Text.RegularExpressions.Regex.IsMatch(mats[si].name, "sodio|sodium", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        var cells = new Dictionary<Vector2Int, (Vector3 s, int n, float lo)>();
                        foreach (var t in mesh.GetTriangles(si)) { var v = M.MultiplyPoint3x4(V[t]); var k = new Vector2Int(Mathf.FloorToInt(v.x / 2.5f), Mathf.FloorToInt(v.z / 2.5f)); cells.TryGetValue(k, out var c); cells[k] = (c.s + v, c.n + 1, c.n == 0 ? v.y : Mathf.Min(c.lo, v.y)); }
                        foreach (var c in cells.Values) { var m = c.s / c.n; Add(new Vector3(m.x, c.lo - .35f, m.z)); }
                    }
                    curSod = false; if (any) continue;
                    var cl = new Dictionary<Vector2Int, (float lo, float hi, List<Vector3> v)>();
                    foreach (var v0 in V) { var v = M.MultiplyPoint3x4(v0); var k = new Vector2Int(Mathf.FloorToInt(v.x / 3), Mathf.FloorToInt(v.z / 3)); if (!cl.TryGetValue(k, out var c)) c = (v.y, v.y, new List<Vector3>()); c.lo = Mathf.Min(c.lo, v.y); c.hi = Mathf.Max(c.hi, v.y); c.v.Add(v); cl[k] = c; }
                    foreach (var c in cl.Values)
                    {
                        if (c.hi - c.lo < 3) continue;
                        Vector3 s = Vector3.zero; int cnt = 0; foreach (var v in c.v) if (v.y > c.hi - .5f) { s += v; cnt++; }
                        var top = s / Mathf.Max(1, cnt); Add(new Vector3(top.x, c.hi - .9f, top.z));
                    }
                    continue;
                }
                if (b.size.y < 3) continue;
                Add(new Vector3(b.center.x, b.max.y - .9f, b.center.z));
            }
            return res;
        }
        public static int Measure(GameObject map)
        {
            var sod = new List<bool>(); var list = Compute(map.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.gameObject.layer == FBWorld.LMap), sod);
            var d = map.GetComponent<FBLampData>(); if (!d) d = map.AddComponent<FBLampData>(); d.pos = list.ToArray(); d.sodium = sod.ToArray();
            Debug.Log($"[FreeBala][luz] luminárias medidas: {list.Count} ({sod.Count(x => x)} de sódio)");
            return list.Count;
        }
    }
}
