// Postes fora do lugar: na Praça das Águas (dados do OSM) alguns postes caíram no MEIO da rua. No início da partida cada poste olha o
// material do triângulo do chão embaixo do pé dele (fora do próprio poste); se for pista (asfalto/rua/faixa) o poste é escondido junto
// com o colisor. Postes em calçada, canteiro, praça etc. ficam. Chamado pelo FBMatch junto com os destrutíveis.
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
    public static class FBStreetPoles
    {
        static readonly Regex POLE = new(@"poste", RegexOptions.IgnoreCase), ROAD = new(@"asphalt|asfalto|road|rua|pista|faixa|lane", RegexOptions.IgnoreCase);
        public static int Clean(GameObject mapRoot)
        {
            int n = 0; if (!mapRoot) return 0;   // postes DENTRO de malhas agrupadas: cortados na importação (FBStreetPolesImport) — em jogo o static batching impede
            foreach (Transform t in mapRoot.transform)
            {
                if (!POLE.IsMatch(t.name) || t.name.Contains("Postes_")) continue;   // malha agrupada (VJ_Postes_Fios) fica
                var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue; var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                if (b.size.y < 3) continue;
                var own = new HashSet<Collider>(t.GetComponentsInChildren<Collider>());
                bool road = false; var o = new Vector3(b.center.x, b.min.y + 1.5f, b.center.z);
                foreach (var h in Physics.RaycastAll(o, Vector3.down, 4, 1 << FBWorld.LMap, QueryTriggerInteraction.Ignore))
                {
                    if (own.Contains(h.collider)) continue;
                    var name = MatName(h); road = name != null && ROAD.IsMatch(name); break;
                }
                if (!road) continue;
                foreach (var r in rs) r.enabled = false; foreach (var c in own) c.enabled = false; n++;
            }
            return n;
        }
        // lista medida fora do jogo (topo do chão embaixo da coluna = asfalto/faixa), também para postes DENTRO de malhas agrupadas
        // (PA_Mobiliario, PA_Vida_Detalhes): Resources/postes_na_rua.json { mapa: [[objeto, x, z] (coordenadas do glTF)] }.
        // Remove da malha (e do colisor) só os triângulos daquela coluna (raio 0,9 m, acima de 5 cm do pé).
        static readonly HashSet<GameObject> done = new();
        static int FromList(GameObject mapRoot)
        {
            if (done.Contains(mapRoot)) return 0; done.Add(mapRoot);
            var ta = Resources.Load<TextAsset>("postes_na_rua"); if (!ta) return 0; var map = mapRoot.name.Replace("Map_", "");
            if (!(Json.Parse(ta.text) is Dictionary<string, object> all) || !all.TryGetValue(map, out var lo) || !(lo is List<object> L)) return 0;
            int n = 0;
            foreach (var o in L)
            {
                if (!(o is List<object> e) || e.Count < 3) continue; string name = e[0] as string; float gx = System.Convert.ToSingle(e[1]), gz = System.Convert.ToSingle(e[2]);
                var t = FindDeep(mapRoot.transform, name); if (!t) continue;
                foreach (var cand in new[] { new Vector3(-gx, 0, gz) })   // glTF (coordenadas do web) → Unity: X espelhado (spawn -59,79 vira 59,79)
                {
                    var wp = mapRoot.transform.TransformPoint(cand); if (Cut(t, wp)) { n++; break; }
                }
            }
            return n;
        }
        static Transform FindDeep(Transform r, string n) { foreach (var t in r.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t; return null; }
        static bool Cut(Transform t, Vector3 wp)
        {
            bool any = false;
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh; if (!m || !m.isReadable) continue; var M = mf.transform.localToWorldMatrix; var V = m.vertices; var W = new Vector3[V.Length]; float lo = float.MaxValue;
                for (int i = 0; i < V.Length; i++) { W[i] = M.MultiplyPoint3x4(V[i]); if ((new Vector2(W[i].x - wp.x, W[i].z - wp.z)).sqrMagnitude < .81f) lo = Mathf.Min(lo, W[i].y); }
                if (lo == float.MaxValue) continue;
                bool Near(int i) => (new Vector2(W[i].x - wp.x, W[i].z - wp.z)).sqrMagnitude < .81f && W[i].y > lo + .05f - .2f;
                Mesh nm = null;
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    var T = m.GetTriangles(s); var keep = new List<int>(T.Length); bool cut = false;
                    for (int k = 0; k < T.Length; k += 3) { if (Near(T[k]) && Near(T[k + 1]) && Near(T[k + 2])) { cut = true; continue; } keep.Add(T[k]); keep.Add(T[k + 1]); keep.Add(T[k + 2]); }
                    if (!cut) continue; if (!nm) { nm = Object.Instantiate(m); nm.name = m.name + "_semposte"; } nm.SetTriangles(keep, s);
                }
                if (!nm) continue; mf.sharedMesh = nm; any = true;
                foreach (var mc in mf.GetComponents<MeshCollider>()) if (mc.sharedMesh == m) { mc.sharedMesh = null; mc.sharedMesh = nm; }
            }
            return any;
        }
        static string MatName(RaycastHit h)
        {
            var r = h.collider.GetComponent<Renderer>(); if (!r) return null; var mats = r.sharedMaterials;
            var mc = h.collider as MeshCollider; var m = mc ? mc.sharedMesh : null;
            if (!m || m.subMeshCount <= 1 || h.triangleIndex < 0) return mats.Length > 0 && mats[0] ? mats[0].name : null;
            int acc = 0; for (int i = 0; i < m.subMeshCount; i++) { var d = m.GetSubMesh(i); acc = (d.indexStart + d.indexCount) / 3; if (h.triangleIndex < acc) return i < mats.Length && mats[i] ? mats[i].name : null; }
            return null;
        }
    }
}
