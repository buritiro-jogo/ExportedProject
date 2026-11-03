// VARREDURA DAS RUAS (base do trânsito do De Rolê): lê a colisão REAL de cada cena de mapa numa grade de 0,5 m e grava em
// unity/tools/roads/scan/<id>.json + <id>.bin, que o tools/roads/build_roads.py transforma nas faixas (Resources/FBRoads/<id>.json).
// Por célula: altura do chão (o piso mais baixo com 2,3 m livres em cima — passa por baixo de toldo/marquise e ignora a laje de asfalto
// enterrada sob a calçada), material do topo e o de baixo (pintura/sarjeta/faixa a poucos cm do asfalto continua sendo rua) e se há
// obstáculo na altura do carro (caixa de 0,35 a 1,85 m: poste, tapume, muro fino, banco, árvore). Os contêineres de cobertura ficam
// DESLIGADOS na varredura (no De Rolê eles saem do mapa) — as células deles saem marcadas.
// Lote: Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBRoadScan.Batch [-fbmap id]
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBRoadScan
    {
        public const float CELL = .5f, HEADROOM = 2.3f;
        static string Arg(string k) { var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, k); return i >= 0 && i + 1 < a.Length ? a[i + 1] : null; }
        public static string OutDir => Path.Combine(FBImport.GameRoot, "unity", "tools", "roads", "scan");

        public static void Batch()
        {
            int code = 0;
            try
            {
                var only = Arg("-fbmap");
                foreach (var m in Json.Parse(File.ReadAllText(Path.Combine(FBImport.Src, "maps.json"))) as List<object>)
                {
                    var id = Json.S(m, "id"); if (Json.B(m, "disabled") || (only != null && id != only)) continue;
                    var scene = $"{FBImport.SceneDir}/Map_{id}.unity"; if (!File.Exists(scene)) continue;
                    EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
                    Scan(id);
                }
            }
            catch (Exception e) { Debug.LogError("[FreeBala][ruas] " + e); code = 1; }
            EditorApplication.Exit(code);
        }

        [MenuItem("FreeBala/Trânsito/Varrer ruas da cena aberta", priority = 60)]
        static void Menu() { var s = EditorSceneManager.GetActiveScene(); var id = s.name.StartsWith("Map_") ? s.name.Substring(4) : s.name; Scan(id); }

        class SubCache { public int[] starts; public Material[] mats; }
        static readonly Dictionary<Collider, SubCache> subs = new();
        static string MatOf(RaycastHit h)
        {
            var col = h.collider; if (!col) return "";
            if (!subs.TryGetValue(col, out var sc))
            {
                sc = new SubCache(); var r = col.GetComponent<Renderer>(); sc.mats = r ? r.sharedMaterials : new Material[0];
                if (col is MeshCollider mc && mc.sharedMesh && mc.sharedMesh.subMeshCount > 1) { var m = mc.sharedMesh; sc.starts = new int[m.subMeshCount]; for (int s = 0; s < m.subMeshCount; s++) sc.starts[s] = m.GetSubMesh(s).indexStart; }
                subs[col] = sc;
            }
            int sub = 0;
            if (sc.starts != null && h.triangleIndex >= 0) { int ti = h.triangleIndex * 3; for (int s = sc.starts.Length - 1; s >= 0; s--) if (ti >= sc.starts[s]) { sub = s; break; } }
            var mat = sub < sc.mats.Length ? sc.mats[sub] : sc.mats.Length > 0 ? sc.mats[0] : null;
            return mat ? mat.name.Replace(" (Instance)", "") : col.name;
        }

        static string MatName(Collider c) { var r = c ? c.GetComponent<Renderer>() : null; return r && r.sharedMaterial ? r.sharedMaterial.name : ""; }
        public static void Scan(string id)
        {
            var t0 = DateTime.Now; subs.Clear();
            int mask = 1 << FBWorld.LMap;
            // contêineres de cobertura: fora do De Rolê → fora da varredura (as células ficam marcadas)
            var cont = new List<GameObject>();
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (t.gameObject.layer == FBWorld.LMap && FBRoads.IsCoverContainer(t)) cont.Add(t.gameObject);
            var contBounds = new List<Bounds>();
            foreach (var g in cont) { foreach (var r in g.GetComponentsInChildren<Renderer>()) contBounds.Add(r.bounds); g.SetActive(false); }
            Physics.SyncTransforms();
            try
            {
                // área: tudo que tem material de rua, cortado pela área da NavMesh (o mapa jogável), com folga
                Bounds area = default; bool any = false;
                foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (r.gameObject.layer != FBWorld.LMap || !r.enabled) continue;
                    if (!r.sharedMaterials.Any(m => m && FBRoads.RoadRx.IsMatch(m.name))) continue;
                    if (!any) { area = r.bounds; any = true; } else area.Encapsulate(r.bounds);
                }
                if (!any) { Debug.Log($"[FreeBala][ruas] {id}: sem material de rua — pulando"); return; }
                // NavMesh da cena (o FBNavData só registra em jogo): triângulos para a máscara do mapa jogável
                var nd = UnityEngine.Object.FindAnyObjectByType<FBNavData>(); UnityEngine.AI.NavMeshDataInstance ni = default; UnityEngine.AI.NavMeshTriangulation tri = default;
                if (nd && nd.data) { ni = UnityEngine.AI.NavMesh.AddNavMeshData(nd.data); tri = UnityEngine.AI.NavMesh.CalculateTriangulation(); }
                if (tri.vertices != null && tri.vertices.Length > 0)
                {
                    var nb = new Bounds(tri.vertices[0], Vector3.zero); foreach (var v in tri.vertices) nb.Encapsulate(v);
                    var mn = Vector3.Max(area.min, nb.min); var mx = Vector3.Min(area.max, nb.max); if (mx.x > mn.x && mx.z > mn.z) { area.SetMinMax(new Vector3(mn.x, area.min.y, mn.z), new Vector3(mx.x, area.max.y, mx.z)); }
                }
                Bounds all = area; foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) if (r.gameObject.layer == FBWorld.LMap) all.Encapsulate(r.bounds);
                float x0 = Mathf.Floor((area.min.x - 12) / CELL) * CELL, z0 = Mathf.Floor((area.min.z - 12) / CELL) * CELL;
                int nx = Mathf.CeilToInt((area.max.x + 12 - x0) / CELL), nz = Mathf.CeilToInt((area.max.z + 12 - z0) / CELL);
                float top = all.max.y + 5, bottom = all.min.y - 5;
                var height = new short[nx * nz]; var mTop = new ushort[nx * nz]; var mBase = new ushort[nx * nz]; var flags = new byte[nx * nz]; var block = new ushort[nx * nz];
                var names = new List<string>(); var nameIx = new Dictionary<string, ushort>();
                ushort Ix(string n) { if (!nameIx.TryGetValue(n, out var k)) { k = (ushort)names.Count; names.Add(n); nameIx[n] = k; } return k; }
                var bnames = new List<string> { "" }; var bIx = new Dictionary<string, ushort>();
                ushort BI(string n) { if (!bIx.TryGetValue(n, out var k)) { k = (ushort)bnames.Count; bnames.Add(n); bIx[n] = k; } return k; }
                var hits = new List<RaycastHit>(16); var half = new Vector3(CELL * .48f, .75f, CELL * .48f); var over = new Collider[8];
                int nRoad = 0;
                for (int iz = 0; iz < nz; iz++)
                {
                    if ((iz & 63) == 0) EditorUtility.DisplayProgressBar("Varrendo ruas " + id, $"{iz}/{nz}", iz / (float)nz);
                    for (int ix = 0; ix < nx; ix++)
                    {
                        int c = iz * nx + ix; float x = x0 + (ix + .5f) * CELL, z = z0 + (iz + .5f) * CELL;
                        hits.Clear(); float y = top;
                        for (int k = 0; k < 16; k++)
                        {
                            if (!Physics.Raycast(new Vector3(x, y, z), Vector3.down, out var h, y - bottom, mask, QueryTriggerInteraction.Ignore)) break;
                            hits.Add(h); y = h.point.y - .002f;                                  // 2 mm: a faixa pintada fica 1 cm acima do asfalto
                        }
                        height[c] = short.MinValue; mTop[c] = mBase[c] = ushort.MaxValue;
                        // piso = o MAIS BAIXO com face para cima e 2,3 m livres até o que estiver em cima
                        int gi = -1;
                        for (int i = hits.Count - 1; i >= 0; i--)
                        {
                            if (hits[i].normal.y < .55f) continue;
                            float room = i == 0 ? 99 : hits[i - 1].point.y - hits[i].point.y;
                            if (room >= HEADROOM) { gi = i; break; }
                        }
                        if (gi < 0) continue;
                        var g = hits[gi]; height[c] = (short)Mathf.Clamp(Mathf.RoundToInt(g.point.y * 100), -32000, 32000);
                        mTop[c] = Ix(MatOf(g));
                        // decalque sobre outro piso (pintura, sarjeta, faixa de pedestre): o de baixo a até 8 cm — rua tem preferência
                        for (int j = gi + 1; j < hits.Count && g.point.y - hits[j].point.y < .08f; j++)
                        {
                            var bm = MatOf(hits[j]); if (mBase[c] == ushort.MaxValue || FBRoads.RoadRx.IsMatch(bm)) mBase[c] = Ix(bm);
                            if (FBRoads.RoadRx.IsMatch(bm)) break;
                        }
                        byte f = 0;
                        int no = Physics.OverlapBoxNonAlloc(new Vector3(x, g.point.y + 1.1f, z), half, over, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
                        if (no > 0) { f |= 1; block[c] = BI(over[0].name + "|" + MatName(over[0])); }                                                      // obstáculo na altura do carro
                        if (g.normal.y < .9f) f |= 4;                                                                                                       // rampa/inclinação
                        foreach (var b in contBounds) if (b.Contains(new Vector3(x, b.center.y, z))) { f |= 2; break; }
                        flags[c] = f; if (FBRoads.RoadRx.IsMatch(names[mTop[c]]) || (mBase[c] != ushort.MaxValue && FBRoads.RoadRx.IsMatch(names[mBase[c]]))) nRoad++;
                    }
                }
                // máscara da NavMesh (bit 8): triângulos rasterizados na grade
                if (tri.indices != null)
                    for (int t = 0; t + 2 < tri.indices.Length; t += 3)
                    {
                        Vector3 a = tri.vertices[tri.indices[t]], b = tri.vertices[tri.indices[t + 1]], cc = tri.vertices[tri.indices[t + 2]];
                        int ia0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, Mathf.Min(b.x, cc.x)) - x0) / CELL)), ia1 = Mathf.Min(nx - 1, Mathf.FloorToInt((Mathf.Max(a.x, Mathf.Max(b.x, cc.x)) - x0) / CELL));
                        int ja0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.z, Mathf.Min(b.z, cc.z)) - z0) / CELL)), ja1 = Mathf.Min(nz - 1, Mathf.FloorToInt((Mathf.Max(a.z, Mathf.Max(b.z, cc.z)) - z0) / CELL));
                        float area2 = (b.x - a.x) * (cc.z - a.z) - (b.z - a.z) * (cc.x - a.x); if (Mathf.Abs(area2) < 1e-6f) continue;
                        for (int jz = ja0; jz <= ja1; jz++)
                            for (int ix = ia0; ix <= ia1; ix++)
                            {
                                float px = x0 + (ix + .5f) * CELL, pz = z0 + (jz + .5f) * CELL;
                                float w0 = ((b.x - px) * (cc.z - pz) - (b.z - pz) * (cc.x - px)) / area2, w1 = ((cc.x - px) * (a.z - pz) - (cc.z - pz) * (a.x - px)) / area2, w2 = 1 - w0 - w1;
                                if (w0 >= -1e-4f && w1 >= -1e-4f && w2 >= -1e-4f) flags[jz * nx + ix] |= 8;
                            }
                    }
                if (ni.valid) ni.Remove();
                EditorUtility.ClearProgressBar();
                Directory.CreateDirectory(OutDir);
                using (var bw = new BinaryWriter(File.Create(Path.Combine(OutDir, id + ".bin"))))
                {
                    foreach (var v in height) bw.Write(v); foreach (var v in mTop) bw.Write(v); foreach (var v in mBase) bw.Write(v); bw.Write(flags); foreach (var v in block) bw.Write(v);
                }
                // vagas do nível (estacionados) no espaço do Unity
                var lvl = Json.Parse(File.ReadAllText(Path.Combine(FBImport.Src, "maps", id, "level.json"))) as Dictionary<string, object>;
                var sb = new StringBuilder(); var inv = System.Globalization.CultureInfo.InvariantCulture; string F(float v) => v.ToString("0.###", inv);
                sb.Append("{\"id\":\"").Append(id).Append("\",\"cell\":").Append(F(CELL)).Append(",\"x0\":").Append(F(x0)).Append(",\"z0\":").Append(F(z0))
                  .Append(",\"nx\":").Append(nx).Append(",\"nz\":").Append(nz).Append(",\"headroom\":").Append(F(HEADROOM)).Append(",\"materials\":[")
                  .Append(string.Join(",", names.Select(n => "\"" + n.Replace("\\", "/").Replace("\"", "'") + "\""))).Append("],\"parked\":[");
                bool first = true;
                foreach (var s in Json.L(lvl, "parked") ?? new List<object>())
                {
                    var p = Coords.FromWeb(Json.L(s, "p")); float yaw = Coords.YawFromWeb(Json.F(s, "yaw"));
                    sb.Append(first ? "" : ",").Append("{\"p\":[").Append(F(p.x)).Append(',').Append(F(p.y)).Append(',').Append(F(p.z)).Append("],\"yaw\":").Append(F(yaw)).Append(",\"kind\":\"").Append(Json.S(s, "kind") ?? "").Append("\"}"); first = false;
                }
                sb.Append("],\"containers\":[");
                first = true; foreach (var b in contBounds) { sb.Append(first ? "" : ",").Append('[').Append(F(b.min.x)).Append(',').Append(F(b.min.z)).Append(',').Append(F(b.max.x)).Append(',').Append(F(b.max.z)).Append(']'); first = false; }
                sb.Append("],\"blockers\":[").Append(string.Join(",", bnames.Select(n => "\"" + n.Replace("\\", "/").Replace("\"", "'") + "\""))).Append("],\"points\":[");
                first = true;                                                                     // pontos do nível (área jogável)
                foreach (var key in new[] { "spawnA", "spawnB", "bots", "points", "waypoints", "area" })   // area = limite jogável (Vila Jardim v10)
                    foreach (var po in Json.L(lvl, key) ?? new List<object>()) { if (!(po is List<object> pl) || pl.Count < 3) continue; var p = Coords.FromWeb(pl); sb.Append(first ? "" : ",").Append('[').Append(F(p.x)).Append(',').Append(F(p.y)).Append(',').Append(F(p.z)).Append(']'); first = false; }
                sb.Append("]}");
                File.WriteAllText(Path.Combine(OutDir, id + ".json"), sb.ToString());
                Debug.Log($"[FreeBala][ruas] {id}: grade {nx}×{nz} ({nx * nz} células), {nRoad} de rua, {names.Count} materiais, {cont.Count} contêineres fora — {(DateTime.Now - t0).TotalSeconds:0.0} s");
            }
            finally { foreach (var g in cont) if (g) g.SetActive(true); EditorUtility.ClearProgressBar(); }
        }
    }
}
