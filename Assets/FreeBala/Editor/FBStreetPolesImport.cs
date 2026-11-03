// Postes no meio da pista (Praça das Águas): na IMPORTAÇÃO do mapa corta da malha (e do colisor) as colunas listadas em
// Resources/postes_na_rua.json ([objeto, x, z] em coordenadas do glTF; medidas pelo topo do chão = asfalto). Em tempo de jogo não dá:
// os objetos do mapa são estáticos e o build junta tudo numa malha combinada (static batching). A malha cortada vira asset em Art/Colliders.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBStreetPolesImport
    {
        public static int Apply(string id, GameObject map)
        {
            var ta = Resources.Load<TextAsset>("postes_na_rua"); if (!ta || !map) return 0;
            if (!(Json.Parse(ta.text) is Dictionary<string, object> all) || !all.TryGetValue(id, out var lo) || !(lo is List<object> L)) return 0;
            int n = 0; var cut = new Dictionary<Mesh, Mesh>();
            foreach (var o in L)
            {
                if (!(o is List<object> e) || e.Count < 3) continue; string name = e[0] as string;
                var wp = map.transform.TransformPoint(new Vector3(-System.Convert.ToSingle(e[1]), 0, System.Convert.ToSingle(e[2])));   // glTF (web) → Unity: X espelhado
                Transform t = null; foreach (var x in map.GetComponentsInChildren<Transform>(true)) if (x.name == name) { t = x; break; }
                if (!t) { Debug.LogWarning($"[FreeBala][postes] {id}: {name} não encontrado"); continue; }
                bool any = false;
                foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
                {
                    var m = mf.sharedMesh; if (!m) continue; var M = mf.transform.localToWorldMatrix; var V = m.vertices; var W = new Vector3[V.Length]; float low = float.MaxValue;
                    for (int i = 0; i < V.Length; i++) { W[i] = M.MultiplyPoint3x4(V[i]); if (new Vector2(W[i].x - wp.x, W[i].z - wp.z).sqrMagnitude < .81f) low = Mathf.Min(low, W[i].y); }
                    if (low == float.MaxValue) continue;
                    bool Near(int i) => new Vector2(W[i].x - wp.x, W[i].z - wp.z).sqrMagnitude < .81f;
                    Mesh nm = null;
                    for (int s = 0; s < m.subMeshCount; s++)
                    {
                        var T = m.GetTriangles(s); var keep = new List<int>(T.Length); bool c = false;
                        for (int k = 0; k < T.Length; k += 3) { if (Near(T[k]) && Near(T[k + 1]) && Near(T[k + 2])) { c = true; continue; } keep.Add(T[k]); keep.Add(T[k + 1]); keep.Add(T[k + 2]); }
                        if (!c) continue;
                        if (!nm) { if (!cut.TryGetValue(m, out nm)) { nm = Object.Instantiate(m); nm.name = m.name + "_semposte"; cut[m] = nm; } }
                        nm.SetTriangles(keep, s);
                    }
                    if (!nm) continue; mf.sharedMesh = nm; any = true;
                    foreach (var mc in mf.GetComponents<MeshCollider>()) if (mc.sharedMesh == m) mc.sharedMesh = nm;
                }
                if (any) n++; else Debug.LogWarning($"[FreeBala][postes] {id}: nada cortado em {name} ({wp.x:0.0}, {wp.z:0.0})");
            }
            var dir = "Assets/FreeBala/Art/Colliders"; System.IO.Directory.CreateDirectory(dir);
            foreach (var kv in cut) { var p = $"{dir}/{id}_{kv.Value.name}.asset"; AssetDatabase.DeleteAsset(p); AssetDatabase.CreateAsset(kv.Value, p); }
            Debug.Log($"[FreeBala][postes] {id}: {n} colunas tiradas da pista");
            return n;
        }
    }
}
