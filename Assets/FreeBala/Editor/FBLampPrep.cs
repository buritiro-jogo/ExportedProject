// Prepara as LUMINÁRIAS dos mapas para apagar uma a uma (rodada 37): a submalha de lâmpada (material *lamp*/*lampada*/*luminaria*,
// a mesma regra do FBLampData) de cada luminária vira um objeto filho próprio com FBLampHead — inclusive quando várias cabeças moram na
// mesma malha (grade de 2,5 m, como a medição das luminárias). A submalha sai da malha original (fica vazia, os índices de material
// não mudam). Filho do próprio renderizador: entorta/cai junto com o poste (FBPole). Idempotente (malha já sem lâmpada é pulada).
// Roda junto do FBPolePrep (FBPolePrep.Batch) ou sozinho:
//   Unity -batchmode -executeMethod FreeBala.EditorTools.FBLampPrep.Batch [-fbmap praca_aguas,vila_jardim,praca_pt]
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBLampPrep
    {
        static readonly Regex HEAD = new(@"(^|_)(lamp|lampada|luminaria)(_on|_sodio|_sodium)?$|street_?light", RegexOptions.IgnoreCase);
        public static void Batch()
        {
            var args = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(args, "-fbmap");
            var maps = (i >= 0 && i + 1 < args.Length ? args[i + 1] : "praca_aguas,vila_jardim,praca_pt").Split(',');
            int code = 0;
            foreach (var id in maps)
            {
                try
                {
                    var path = $"{FBImport.SceneDir}/Map_{id}.unity"; if (!File.Exists(path)) { Debug.LogWarning("[FreeBala][lamp] sem cena " + path); continue; }
                    var sc = EditorSceneManager.OpenScene(path); var rep = Prep(sc);
                    EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc); AssetDatabase.SaveAssets();
                    Debug.Log($"[FreeBala][lamp] {id}: {rep}");
                }
                catch (System.Exception e) { Debug.LogError($"[FreeBala][lamp] {id} falhou: {e}"); code = 1; }
            }
            EditorApplication.Exit(code);
        }
        public static string Prep(UnityEngine.SceneManagement.Scene sc)
        {
            var root = sc.GetRootGameObjects().FirstOrDefault(g => g.name.StartsWith("Map_")); if (!root) return "sem raiz Map_";
            int heads = 0, meshes = 0, skipped = 0; var dir = $"Assets/FreeBala/Optimized/{sc.name}/luminarias"; Directory.CreateDirectory(dir);
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!mr || mr.GetComponent<FBLampHead>() || mr.gameObject.layer != FBWorld.LMap) continue;
                var mf = mr.GetComponent<MeshFilter>(); var m = mf ? mf.sharedMesh : null; if (!m) continue;
                var mats = mr.sharedMaterials; var subs = new List<int>();
                for (int s = 0; s < Mathf.Min(m.subMeshCount, mats.Length); s++) if (mats[s] && HEAD.IsMatch(mats[s].name.Replace(" (Instance)", "")) && m.GetIndexCount(s) > 0) subs.Add(s);
                if (subs.Count == 0) continue;
                if (!m.isReadable) { skipped++; continue; }
                var V = m.vertices; var M = mr.transform.localToWorldMatrix; int n = 0;
                foreach (var s in subs)
                {
                    // células de 2,5 m (mundo) = uma luminária cada
                    var cells = new Dictionary<Vector2Int, List<int>>(); var T = m.GetTriangles(s);
                    for (int k = 0; k + 2 < T.Length; k += 3)
                    {
                        var c = (M.MultiplyPoint3x4(V[T[k]]) + M.MultiplyPoint3x4(V[T[k + 1]]) + M.MultiplyPoint3x4(V[T[k + 2]])) / 3;
                        var key = new Vector2Int(Mathf.FloorToInt(c.x / 2.5f), Mathf.FloorToInt(c.z / 2.5f)); if (!cells.TryGetValue(key, out var L)) cells[key] = L = new List<int>(); L.Add(T[k]); L.Add(T[k + 1]); L.Add(T[k + 2]);
                    }
                    foreach (var kv in cells)
                    {
                        var hm = Compact(m, kv.Value); hm.name = $"{mr.name}_lamp{s}_{n}"; var ap = $"{dir}/{FBRegistry.Safe(hm.name)}.asset"; AssetDatabase.DeleteAsset(ap); AssetDatabase.CreateAsset(hm, ap);
                        var g = new GameObject($"LuminariaCabeca_{n}"); g.layer = mr.gameObject.layer; g.transform.SetParent(mr.transform, false);
                        g.AddComponent<MeshFilter>().sharedMesh = hm; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mats[s];
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = mr.receiveShadows; r.lightProbeUsage = mr.lightProbeUsage; r.reflectionProbeUsage = mr.reflectionProbeUsage;
                        GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);   // fora do static batching: bloco de propriedades por cabeça
                        g.AddComponent<FBLampHead>(); n++; heads++;
                    }
                }
                // malha original sem as lâmpadas (submalhas vazias; mesmos índices de material)
                var nm = Object.Instantiate(m); nm.name = m.name + "_semLampada";
                for (int s = 0; s < nm.subMeshCount; s++) if (subs.Contains(s)) nm.SetTriangles(new int[0], s);
                var np = $"{dir}/{FBRegistry.Safe(mr.name)}_{FBRegistry.Safe(m.name)}_semLampada.asset"; AssetDatabase.DeleteAsset(np); AssetDatabase.CreateAsset(nm, np);
                var mc = mr.GetComponent<MeshCollider>(); bool colSame = mc && mc.sharedMesh == m;
                mf.sharedMesh = nm; if (colSame) mc.sharedMesh = nm; meshes++;
            }
            return $"{heads} cabeças de luminária em {meshes} malhas ({skipped} malhas ilegíveis puladas)";
        }
        static Mesh Compact(Mesh m, List<int> tri)
        {
            var map = new Dictionary<int, int>(); var idx = new List<int>(tri.Count);
            foreach (var v in tri) { if (!map.TryGetValue(v, out var n)) map[v] = n = map.Count; idx.Add(n); }
            var order = new int[map.Count]; foreach (var kv in map) order[kv.Value] = kv.Key;
            var o = new Mesh(); var V0 = m.vertices; o.vertices = order.Select(i => V0[i]).ToArray();
            var N = m.normals; if (N != null && N.Length == m.vertexCount) o.normals = order.Select(i => N[i]).ToArray();
            var Tg = m.tangents; if (Tg != null && Tg.Length == m.vertexCount) o.tangents = order.Select(i => Tg[i]).ToArray();
            var C = m.colors32; if (C != null && C.Length == m.vertexCount) o.colors32 = order.Select(i => C[i]).ToArray();
            for (int ch = 0; ch < 4; ch++) { var U = new List<Vector4>(); m.GetUVs(ch, U); if (U.Count == m.vertexCount) o.SetUVs(ch, order.Select(i => U[i]).ToList()); }
            o.SetTriangles(idx, 0); o.RecalculateBounds(); return o;
        }
    }
}
