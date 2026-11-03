// Prepara os POSTES dos mapas para entortar/cair em jogo (FBPole):
//  · poste individual (*_poste_br): sai do static batching (objeto estático não se move em jogo) e ganha FBPole (pé + altura medidos);
//  · grupo de postes numa malha só (PA_Postes_LED_Praca, PA_Postes_Ponto, PA_Postes_Solares, VJ_Postes_Fios): partido em um objeto por
//    poste — ilhas conexas da malha (posição de vértice) juntadas quando se encostam no XZ; rede com fios ligando tudo (ilha > 12 m) vai
//    por células de 3 m. Cada pedaço vira malha própria (Assets/FreeBala/Optimized/<cena>/postes), mesmo material/colisor/superfície.
//   Unity -batchmode -executeMethod FreeBala.EditorTools.FBPolePrep.Batch [-fbmap praca_aguas,vila_jardim,praca_pt]
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBPolePrep
    {
        static readonly Regex SINGLE = new(@"(^|_)poste_br", RegexOptions.IgnoreCase), GROUP = new(@"_Postes_", RegexOptions.IgnoreCase), PIECE = new(@"_p\d+$");
        public static void Batch()
        {
            var args = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(args, "-fbmap");
            var maps = (i >= 0 && i + 1 < args.Length ? args[i + 1] : "praca_aguas,vila_jardim,praca_pt").Split(',');
            int code = 0;
            foreach (var id in maps)
            {
                try
                {
                    var path = $"{FBImport.SceneDir}/Map_{id}.unity"; if (!File.Exists(path)) { Debug.LogWarning("[FreeBala][postes] sem cena " + path); continue; }
                    var sc = EditorSceneManager.OpenScene(path); var rep = Prep(sc, id) + " · " + FBLampPrep.Prep(sc);   // + cabeças de luminária apagáveis (rodada 37)
                    EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc); AssetDatabase.SaveAssets();
                    Debug.Log($"[FreeBala][postes] {id}: {rep}");
                }
                catch (System.Exception e) { Debug.LogError($"[FreeBala][postes] {id} falhou: {e}"); code = 1; }
            }
            EditorApplication.Exit(code);
        }
        static string Prep(UnityEngine.SceneManagement.Scene sc, string id)
        {
            var root = sc.GetRootGameObjects().FirstOrDefault(g => g.name.StartsWith("Map_")); if (!root) return "sem raiz Map_";
            int singles = 0, pieces = 0, groups = 0;
            var all = root.GetComponentsInChildren<Transform>(true).ToList();
            foreach (var t in all)
            {
                if (!t || t.GetComponent<FBPole>()) continue; var mf = t.GetComponent<MeshFilter>(); var mr = t.GetComponent<MeshRenderer>(); if (!mf || !mr || !mf.sharedMesh) continue;
                if (SINGLE.IsMatch(t.name)) { if (MakePole(t.gameObject, mr.bounds)) singles++; }
                else if (GROUP.IsMatch(t.name) && !PIECE.IsMatch(t.name) && mf.sharedMesh.isReadable) { pieces += Split(t, mf, mr, sc.name); groups++; }   // pedaço já partido (_pN): não parte de novo (rodar 2× multiplicava objetos)
            }
            return $"{singles} postes individuais, {groups} grupos -> {pieces} pedaços";
        }
        static bool MakePole(GameObject g, Bounds b)
        {
            if (b.size.y < 3) return false;
            foreach (var t in g.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            var p = g.GetComponent<FBPole>() ?? g.AddComponent<FBPole>(); p.basePos = new Vector3(b.center.x, b.min.y, b.center.z); p.height = b.size.y; return true;
        }
        static int Split(Transform t, MeshFilter mf, MeshRenderer mr, string scene)
        {
            var m = mf.sharedMesh; var V = m.vertices; var M = t.localToWorldMatrix; var W = V.Select(v => M.MultiplyPoint3x4(v)).ToArray();
            // ilhas por posição de vértice (todas as submalhas)
            var pid = new Dictionary<Vector3Int, int>(); var par = new List<int>(); int[] vid = new int[V.Length];
            for (int i = 0; i < V.Length; i++) { var k = Vector3Int.RoundToInt(V[i] * 1000); if (!pid.TryGetValue(k, out var id)) { pid[k] = id = par.Count; par.Add(id); } vid[i] = id; }
            int F(int x) { while (par[x] != x) x = par[x] = par[par[x]]; return x; }
            var subs = Enumerable.Range(0, m.subMeshCount).Select(s => m.GetTriangles(s)).ToArray();
            foreach (var T in subs) for (int k = 0; k + 2 < T.Length; k += 3) { int a = F(vid[T[k]]); par[F(vid[T[k + 1]])] = a; par[F(vid[T[k + 2]])] = a; }
            var isl = new Dictionary<int, Bounds>();
            foreach (var T in subs) for (int k = 0; k + 2 < T.Length; k += 3) { int r = F(vid[T[k]]); var c = (W[T[k]] + W[T[k + 1]] + W[T[k + 2]]) / 3; if (isl.TryGetValue(r, out var b)) { b.Encapsulate(c); isl[r] = b; } else isl[r] = new Bounds(c, Vector3.zero); }
            // ilhas que se encostam no XZ (até 0,5 m) viram o mesmo poste; ilha enorme (rede com fios) cai na grade de 3 m
            var roots = isl.Keys.ToList(); var gpar = roots.ToDictionary(r => r, r => r); int G(int x) { while (gpar[x] != x) x = gpar[x] = gpar[gpar[x]]; return x; }
            for (int a = 0; a < roots.Count; a++) for (int b2 = a + 1; b2 < roots.Count; b2++)
                {
                    var A = isl[roots[a]]; var B = isl[roots[b2]]; if (A.size.x > 12 || A.size.z > 12 || B.size.x > 12 || B.size.z > 12) continue;
                    if (A.min.x - .5f <= B.max.x && B.min.x - .5f <= A.max.x && A.min.z - .5f <= B.max.z && B.min.z - .5f <= A.max.z) gpar[G(roots[a])] = G(roots[b2]);
                }
            string Key(int t0, int s)
            {
                var T = subs[s]; int r = F(vid[T[t0]]); var b = isl[r];
                if (b.size.x > 12 || b.size.z > 12) { var c = (W[T[t0]] + W[T[t0 + 1]] + W[T[t0 + 2]]) / 3; return $"g{Mathf.FloorToInt(c.x / 3)}_{Mathf.FloorToInt(c.z / 3)}"; }
                return "i" + G(r);
            }
            var parts = new Dictionary<string, List<int>[]>();
            for (int s = 0; s < subs.Length; s++) for (int k = 0; k + 2 < subs[s].Length; k += 3) { var key = Key(k, s); if (!parts.TryGetValue(key, out var L)) parts[key] = L = Enumerable.Range(0, subs.Length).Select(_ => new List<int>()).ToArray(); L[s].Add(subs[s][k]); L[s].Add(subs[s][k + 1]); L[s].Add(subs[s][k + 2]); }
            if (parts.Count < 2) return 0;
            var dir = $"Assets/FreeBala/Optimized/{scene}/postes"; Directory.CreateDirectory(dir);
            var mc0 = t.GetComponent<MeshCollider>(); var surf = t.GetComponent<FBSurface>(); int n = 0;
            foreach (var kv in parts)
            {
                var pm = Compact(m, kv.Value); pm.name = $"{t.name}_{n}"; var ap = $"{dir}/{FBRegistry.Safe(pm.name)}.asset"; AssetDatabase.DeleteAsset(ap); AssetDatabase.CreateAsset(pm, ap);
                var g = new GameObject($"{t.name}_p{n}"); g.layer = t.gameObject.layer; g.transform.SetParent(t.parent, false); g.transform.localPosition = t.localPosition; g.transform.localRotation = t.localRotation; g.transform.localScale = t.localScale;
                g.AddComponent<MeshFilter>().sharedMesh = pm; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterials = mr.sharedMaterials; r.shadowCastingMode = mr.shadowCastingMode; r.receiveShadows = mr.receiveShadows;
                r.lightProbeUsage = mr.lightProbeUsage; r.reflectionProbeUsage = mr.reflectionProbeUsage;
                if (mc0) g.AddComponent<MeshCollider>().sharedMesh = pm; if (surf) g.AddComponent<FBSurface>().kind = surf.kind;
                GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
                if (!MakePole(g, r.bounds)) { }                                                          // pedaço baixo (fio, base): objeto comum, sem queda
                n++;
            }
            // o grupo original sai (malha e colisor), o objeto fica (filhos / referências)
            Object.DestroyImmediate(mr); var mf0 = t.GetComponent<MeshFilter>(); if (mf0) Object.DestroyImmediate(mf0); if (mc0) Object.DestroyImmediate(mc0);
            return n;
        }
        static Mesh Compact(Mesh m, List<int>[] tri)
        {
            var map = new Dictionary<int, int>(); var idx = tri.Select(_ => new List<int>()).ToArray();
            for (int s = 0; s < tri.Length; s++) foreach (var v in tri[s]) { if (!map.TryGetValue(v, out var n)) map[v] = n = map.Count; idx[s].Add(n); }
            var order = new int[map.Count]; foreach (var kv in map) order[kv.Value] = kv.Key;
            var o = new Mesh(); if (order.Length > 65000) o.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            var V0 = m.vertices; o.vertices = order.Select(i => V0[i]).ToArray();
            var N = m.normals; if (N != null && N.Length == m.vertexCount) o.normals = order.Select(i => N[i]).ToArray();
            var Tg = m.tangents; if (Tg != null && Tg.Length == m.vertexCount) o.tangents = order.Select(i => Tg[i]).ToArray();
            var C = m.colors32; if (C != null && C.Length == m.vertexCount) o.colors32 = order.Select(i => C[i]).ToArray();
            for (int ch = 0; ch < 4; ch++) { var U = new List<Vector4>(); m.GetUVs(ch, U); if (U.Count == m.vertexCount) o.SetUVs(ch, order.Select(i => U[i]).ToList()); }
            o.subMeshCount = tri.Length; for (int s = 0; s < tri.Length; s++) o.SetTriangles(idx[s], s); o.RecalculateBounds(); return o;
        }
    }
}
