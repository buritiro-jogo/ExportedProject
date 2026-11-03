// VEGETAÇÃO DOS MAPAS na importação (Agente 5; parte do FBMapLook, chamada depois da conversão dos materiais):
//  1. acha TODAS as superfícies verdes do mapa: nome do material (grama/grass/lawn/canteiro…) ou cor média verde da textura × cor, só em
//     triângulos quase horizontais e com área relevante (fachada/placa/quadra pintada de verde ficam de fora);
//  2. amostra o chão por RAIOS de cima numa grade com jitter estratificado (blue-noise) só onde há triângulo verde: o raio pega a superfície
//     de cima de verdade — telhado, rua, calçada, banco ou prop por cima do verde descartam o ponto; a normal entra na inclinação do tufo;
//  3. distância até a borda do gramado (transformada de distância na grade) + espaço livre (cápsula sem bater em parede/poste/grade, nada
//     por cima até 3 m) -> composição: tapete de tufos baixos e densos com manchas de densidade por ruído (fbm), borda aparada perto de
//     calçada/rua, touceiras/capim alto em faixas de borda e manchas; escala/rotação/inclinação variadas, tom variando no shader;
//  4. troca a textura do chão verde (o "tapete") por terra + grama rala procedural (mesmo nome de material: passos/superfície continuam);
//  5. grava Art/Vegetation/<mapa>_veg.asset (células de 24 m, instâncias ordenadas pelo sorteio fixo) + objeto "Vegetacao" com FBVegetation.
// Assets por mapa (tools/unity/veg_prep.py): Praça das Águas = grama_pa (tufos em cartões do grass_patches), Vila Jardim = capim_vj (real_bush).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala.EditorTools
{
    public static partial class FBMapLook
    {
        const string VegDir = "Assets/FreeBala/Art/Vegetation";
        static readonly Regex GreenName = R("grass|grama|lawn|gramado|canteiro|jardim|turf|relva");
        static readonly Regex NotGround = R("court|quadra|tuft|paint|pintura|acc_|band_|pillar|iron|ferro|let_|sign|placa|plastic|plastico|tank|wall|parede|muro|leaf|folha|mf_|plant|flower|petal|stem|brejo|water|agua|roof|telha|car|bag|saco|tronco|trunk|washingtonia|palm");
        // TETO RÍGIDO no bake (orçamento do Agente 9, perf.md: Ultra PC desenha até 90 mil visíveis; o asset guarda no máximo 1,35× isso)
        const int MaxInstances = 120000, MaxBig = 7000;                                   // total por mapa / arbustos médios+altos (touceiras)
        const float PlayMargin = 22, RingWidth = 35;                                      // área jogável + margem; anel esparso em volta; fora: nada
        static readonly Regex DryName = R("dry|seca|seco|terreno|terrain|far|distant|campo|fundo");

        class VegSet { public string glb; public (string mesh, bool shadows)[] kinds; public bool carpet; }   // carpet = gramado vira tapete de lâminas (FBGrassCarpet)
        static VegSet VegFor(string id) =>
            id.Contains("praca_aguas") ? new VegSet { glb = "grama_pa", carpet = true, kinds = new[] { ("tufo_0", false), ("tufo_1", false), ("tufo_2", false), ("tufo_3", false), ("tufo_0", true), ("tufo_2", true) } } :
            id.Contains("vila_jardim") ? new VegSet { glb = "capim_vj", kinds = new[] { ("Small_bush_1", false), ("Small_bush_2", false), ("Medium_bush_1", true), ("Medium_bush_2", true), ("Medium_bush_3", true), ("tall_bush_1", true), ("tall_bush_2", true), ("tall_bush_3", true), ("tall_bush_4", true) } } :
            null;

        static void VegBake(GameObject map, string id, List<Material> mats)
        {
            var set = VegFor(id); Directory.CreateDirectory(VegDir);
            // ---- 1. superfícies verdes
            var rends = map.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled && r.GetComponent<MeshFilter>()?.sharedMesh).ToList();
            var area = new Dictionary<Material, float>(); var greenTris = new List<(Vector3 a, Vector3 b, Vector3 c)>();
            foreach (var r in rends)
            {
                var mesh = r.GetComponent<MeshFilter>().sharedMesh; var ms = r.sharedMaterials; Vector3[] v;
                try { v = mesh.vertices; } catch { continue; }
                var M = r.transform.localToWorldMatrix;
                for (int s = 0; s < ms.Length && s < mesh.subMeshCount; s++)
                {
                    var m = ms[s]; if (!m || !IsGreenCandidate(m)) continue; var tri = mesh.GetTriangles(s);
                    for (int i = 0; i + 2 < tri.Length; i += 3)
                    {
                        Vector3 a = M.MultiplyPoint3x4(v[tri[i]]), b = M.MultiplyPoint3x4(v[tri[i + 1]]), c = M.MultiplyPoint3x4(v[tri[i + 2]]);
                        var n = Vector3.Cross(b - a, c - a); float ar = n.magnitude * .5f; if (ar < 1e-4f || Mathf.Abs(n.normalized.y) < .75f) continue;
                        area[m] = area.TryGetValue(m, out var x) ? x + ar : ar;
                    }
                }
            }
            var green = new HashSet<Material>(area.Where(kv => kv.Value > 25).Select(kv => kv.Key));
            foreach (var r in rends)
            {
                var mesh = r.GetComponent<MeshFilter>().sharedMesh; var ms = r.sharedMaterials; if (!ms.Any(green.Contains)) continue; var v = mesh.vertices; var M = r.transform.localToWorldMatrix;
                for (int s = 0; s < ms.Length && s < mesh.subMeshCount; s++)
                {
                    if (!green.Contains(ms[s])) continue; var tri = mesh.GetTriangles(s);
                    for (int i = 0; i + 2 < tri.Length; i += 3) greenTris.Add((M.MultiplyPoint3x4(v[tri[i]]), M.MultiplyPoint3x4(v[tri[i + 1]]), M.MultiplyPoint3x4(v[tri[i + 2]])));
                }
            }
            Debug.Log($"[FreeBala] vegetação {id}: superfícies verdes = {string.Join(", ", green.Select(m => $"{m.name} ({area[m]:0} m²)"))}");
            // ---- 4. chão verde -> terra + grama rala (vale também nos mapas sem asset de vegetação? não: só onde a vegetação cobre)
            if (set == null || green.Count == 0) return;
            var groundTex = set.carpet ? GroundTexCarpet() : GroundTex();
            foreach (var m in green)
            {
                if (m.shader != FBShaders.Lit && m.shader != FBShaders.Foliage) continue;
                float mpu = MetersPerUV(m, rends.Cast<Renderer>().ToList(), 4); var st = m.GetTextureScale("_BaseMap"); float t = mpu / 12f / Mathf.Max(.01f, Mathf.Abs(st.x));   // repete a cada 12 m
                var c0 = m.GetColor("_BaseColor"); m.SetTexture("_BaseMap", groundTex); m.SetTextureScale("_BaseMap", new Vector2(t, t)); m.SetTextureOffset("_BaseMap", Vector2.zero);
                m.SetColor("_BaseColor", Color.Lerp(Color.white, new Color(c0.r, c0.g, c0.b) * 1.6f, .15f)); m.SetFloat("_Smoothness", .06f);
                m.DisableKeyword("_DETAIL_MULX2"); m.SetTexture("_DetailAlbedoMap", null); m.SetTexture("_DetailNormalMap", null); m.DisableKeyword("_OCCLUSIONMAP"); m.SetTexture("_OcclusionMap", null);
                EditorUtility.SetDirty(m);
            }

            // ---- 2. amostragem por raios onde há triângulo verde
            var meshes = AssetDatabase.LoadAllAssetsAtPath($"{FBImport.Imp}/Vegetation/{set.glb}.gltf").OfType<Mesh>().ToList();
            var srcMat = AssetDatabase.LoadAllAssetsAtPath($"{FBImport.Imp}/Vegetation/{set.glb}.gltf").OfType<Material>().FirstOrDefault();
            if (meshes.Count == 0) { Debug.LogWarning($"[FreeBala] vegetação {id}: {set.glb}.gltf não importou (rode tools/unity/veg_prep.py e importe de novo)"); return; }
            Physics.SyncTransforms();
            float sp = set.glb == "capim_vj" ? .4f : .33f;                                    // espaçamento do tapete
            var bmin = greenTris.Aggregate(new Vector3(1e9f, 1e9f, 1e9f), (q, t) => Vector3.Min(q, Vector3.Min(t.a, Vector3.Min(t.b, t.c))));
            var bmax = greenTris.Aggregate(new Vector3(-1e9f, -1e9f, -1e9f), (q, t) => Vector3.Max(q, Vector3.Max(t.a, Vector3.Max(t.b, t.c))));
            // área jogável (pontos do level.json: spawns, bots, pontos, waypoints, becos, carros) + margem; o terreno em volta do mapa inteiro
            // (ex.: VJ_grass_dry, 4,2 km²) fica fora — antes ele gerava 4,19 milhões de instâncias na Vila Jardim
            var play = PlayArea(id); float ringOut = PlayMargin + RingWidth;
            if (play.HasValue) { var pr = play.Value; bmin.x = Mathf.Max(bmin.x, pr.xMin - ringOut); bmin.z = Mathf.Max(bmin.z, pr.yMin - ringOut); bmax.x = Mathf.Min(bmax.x, pr.xMax + ringOut); bmax.z = Mathf.Min(bmax.z, pr.yMax + ringOut); }
            if (bmax.x <= bmin.x || bmax.z <= bmin.z) { Debug.LogWarning($"[FreeBala] vegetação {id}: nenhum verde na área jogável"); return; }
            var dry = new HashSet<Material>(green.Where(m => DryName.IsMatch(m.name) || area[m] > 150000));   // terreno seco/enorme: só tufos esparsos
            float Zone(Vector3 p) { if (!play.HasValue) return 0; var pr = play.Value; float dx = Mathf.Max(pr.xMin - PlayMargin - p.x, 0, p.x - pr.xMax - PlayMargin), dz = Mathf.Max(pr.yMin - PlayMargin - p.z, 0, p.z - pr.yMax - PlayMargin); return Mathf.Sqrt(dx * dx + dz * dz); }   // 0 = jogável
            int nx = Mathf.CeilToInt((bmax.x - bmin.x) / sp) + 1, nz = Mathf.CeilToInt((bmax.z - bmin.z) / sp) + 1;
            var cover = new bool[nx, nz];                                                     // células da grade cobertas por triângulo verde
            foreach (var (a, b, c) in greenTris)
            {
                int x0 = Mathf.Max(0, (int)((Mathf.Min(a.x, b.x, c.x) - bmin.x) / sp)), x1 = Mathf.Min(nx - 1, (int)((Mathf.Max(a.x, b.x, c.x) - bmin.x) / sp) + 1);
                int z0 = Mathf.Max(0, (int)((Mathf.Min(a.z, b.z, c.z) - bmin.z) / sp)), z1 = Mathf.Min(nz - 1, (int)((Mathf.Max(a.z, b.z, c.z) - bmin.z) / sp) + 1);
                for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++) if (!cover[x, z] && InTri2D(bmin.x + (x + .5f) * sp, bmin.z + (z + .5f) * sp, a, b, c, sp * .75f)) cover[x, z] = true;
            }
            var hitP = new Vector3[nx, nz]; var hitN = new Vector3[nx, nz]; var gcol = new Color[nx, nz]; var colOf = new Dictionary<Material, Color>(); var ok = new bool[nx, nz]; var isDry = new bool[nx, nz]; int mask = 1 << FBProjectSetup.LMap, nOk = 0;
            var subCache = new Dictionary<Mesh, int[]>();
            for (int x = 0; x < nx; x++) for (int z = 0; z < nz; z++)
                {
                    if (!cover[x, z]) continue;
                    float jx = Hash(x, z, 1) - .5f, jz = Hash(x, z, 2) - .5f;                   // jitter estratificado
                    var o = new Vector3(bmin.x + (x + .5f + jx * .9f) * sp, bmax.y + 30, bmin.z + (z + .5f + jz * .9f) * sp);
                    if (!Physics.Raycast(o, Vector3.down, out var h, bmax.y - bmin.y + 80, mask, QueryTriggerInteraction.Ignore) || h.normal.y < .75f) continue;
                    if (!green.Contains(MatAt(h, subCache))) continue;
                    hitP[x, z] = h.point; hitN[x, z] = h.normal; ok[x, z] = true; nOk++; var hm = MatAt(h, subCache); isDry[x, z] = dry.Contains(hm);
                    if (set.carpet && hm) { if (!colOf.TryGetValue(hm, out var hc)) colOf[hm] = hc = AvgColor(hm); gcol[x, z] = hc; }   // cor do chão (o tapete puxa o tom dele)
                }
            // ---- 3. distância até a borda (em células) e espaço livre
            var grassOk = new bool[nx, nz];                                                   // onde o tapete de lâminas pode nascer
            var dist = new int[nx, nz]; var q = new Queue<(int, int)>();
            for (int x = 0; x < nx; x++) for (int z = 0; z < nz; z++) { dist[x, z] = ok[x, z] ? int.MaxValue : 0; if (!ok[x, z]) q.Enqueue((x, z)); }
            while (q.Count > 0) { var (x, z) = q.Dequeue(); foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) { int a = x + dx, b = z + dz; if (a < 0 || b < 0 || a >= nx || b >= nz || dist[a, b] <= dist[x, z] + 1) continue; dist[a, b] = dist[x, z] + 1; q.Enqueue((a, b)); } }
            float keep = Mathf.Min(1, MaxInstances / Mathf.Max(1f, nOk * 1.08f));            // mapa enorme: afina o tapete para caber no teto
            var inst = new List<(int kind, Matrix4x4 m, float key)>(); var opens = new List<(Vector3 p, int d)>(); var edges = new List<Vector3>();
            int kinds = set.kinds.Length; var rnd = new System.Random(id.GetHashCode());
            for (int x = 0; x < nx; x++) for (int z = 0; z < nz; z++)
                {
                    if (!ok[x, z]) continue; var p = hitP[x, z]; var n = hitN[x, z]; float ed = dist[x, z] * sp;     // metros até sair do verde
                    if (Physics.Raycast(p + Vector3.up * .1f, Vector3.up, 3, ~0, QueryTriggerInteraction.Ignore)) continue;   // banco/marquise por cima
                    if (ed < 2.5f && Physics.CheckCapsule(p + Vector3.up * .22f, p + Vector3.up * .5f, .1f, ~(1 << FBProjectSetup.LHitbox | 1 << FBProjectSetup.LTrigger), QueryTriggerInteraction.Ignore)) continue;   // parede/poste/grade
                    if (Zone(p) <= RingWidth) grassOk[x, z] = true;
                    float n1 = WN(p.x, p.z, 14, 3), n2 = WN(p.x + 37, p.z - 11, 5, 2);           // manchas grandes (~14 m) / médias (~5 m)
                    if (ed > 3 && Hash(x, z, 8) < .01f && Zone(p) <= 0) opens.Add((p, dist[x, z])); if (ed > .25f && ed < .7f && Hash(x, z, 9) < .02f) edges.Add(p);
                    float r = (float)rnd.NextDouble(); int kind; float hs, ws;
                    float zone = Zone(p); if (zone > RingWidth) continue;                                      // fora da área jogável + anel: nada
                    bool sparse = isDry[x, z] || zone > 0;                                                    // terreno seco / anel: só tufos baixos esparsos
                    float clump = sparse ? 0 : Mathf.InverseLerp(.62f, .8f, n2); bool border = !sparse && ed > .2f && ed < (set.glb == "capim_vj" ? 1.3f : 1.6f);
                    // médios/altos SÓ em borda de canteiro ou mancha; a cobertura contínua é de tufos baixos
                    // ALTURAS REAIS (medidas das malhas convertidas): capim pequeno 0,30–0,33 m, médio 0,61–0,69 m, alto 1,10–1,45 m; tufo PA 0,30 m.
                    // grama 10–35 cm · arbusto baixo 40–80 cm · alto ≤ 1,5 m e raro
                    if (set.glb == "capim_vj" && (border && r < .025f || r < .015f * clump)) { kind = 5 + (int)(Hash(x, z, 4) * 3.999f); hs = Rng(.65f, 1f); ws = Rng(.7f, 1f); }      // alto (raro): 0,7–1,45 m
                    else if (set.glb == "capim_vj" && (border && r < .09f || r < .08f * clump)) { kind = 2 + (int)(Hash(x, z, 5) * 2.999f); hs = Rng(.62f, 1.12f); ws = Rng(.65f, 1f); }  // baixo: 0,38–0,77 m
                    else if (set.glb != "capim_vj" && (set.carpet ? border && r < .022f : border && r < .04f || r < .035f * clump)) { kind = 4 + (int)(Hash(x, z, 4) * 1.999f); hs = Rng(1.4f, 2.5f) * (set.carpet ? .75f : 1); ws = Rng(1.2f, 1.7f); }  // touceira: 0,42–0,75 m (com tapete: só na borda, poucas e mais baixas)
                    else
                    {
                        if (set.carpet && !sparse) continue;                                   // gramado: quem cobre agora é o tapete de lâminas (GPU)
                        float pk = sparse ? (zone > 0 ? .12f : .22f) * Mathf.Lerp(.5f, 1.3f, n1) : Mathf.Lerp(set.glb == "capim_vj" ? .55f : .6f, set.glb == "capim_vj" ? .98f : 1f, n1);
                        if (Hash(x, z, 6) > pk * keep) continue;
                        if (set.glb == "capim_vj") { kind = (int)(Hash(x, z, 7) * 1.999f); hs = Rng(.4f, 1f) * (ed < .5f ? .7f : 1); ws = Rng(.6f, 1f); }                           // 0,12–0,33 m
                        else { kind = (int)(Hash(x, z, 7) * 3.999f); hs = Rng(.5f, 1.1f) * (ed < .45f ? .7f : 1) * Mathf.Lerp(.9f, 1.08f, n2); ws = Rng(.8f, 1.2f); }               // 0,10–0,35 m
                        if (sparse) hs *= .8f;
                    }
                    var up = Vector3.Slerp(Vector3.up, n, .6f); var rot = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(Rng(-6, 6), (float)rnd.NextDouble() * 360, Rng(-6, 6));
                    inst.Add((kind, Matrix4x4.TRS(p - up * .02f, rot, new Vector3(ws, hs, ws)), Hash(Mathf.RoundToInt(p.x * 50), Mathf.RoundToInt(p.z * 50), 13)));
                }
            float Rng(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            // teto rígido: arbustos grandes primeiro limitados a MaxBig, depois o total a MaxInstances (descarte pelo sorteio fixo -> uniforme)
            int firstBig = set.glb == "capim_vj" ? 2 : 4, before = inst.Count;
            var big = inst.Where(i => i.kind >= firstBig).OrderBy(i => i.key).ToList(); var low = inst.Where(i => i.kind < firstBig).OrderBy(i => i.key).ToList();
            if (big.Count > MaxBig) big = big.Take(MaxBig).ToList();
            if (big.Count + low.Count > MaxInstances) low = low.Take(MaxInstances - big.Count).ToList();
            inst = big.Concat(low).ToList();

            // ---- 5. dados: tipos + células de 24 m
            var mat = VegMaterial(set.glb, srcMat);
            var data = ScriptableObject.CreateInstance<FBVegData>(); data.name = id + "_veg";
            data.kinds = set.kinds.Select(k => new FBVegData.Kind { name = k.mesh, mesh = meshes.FirstOrDefault(m => m.name == k.mesh) ?? meshes.FirstOrDefault(m => m.name.StartsWith(k.mesh)) ?? meshes[0], mat = mat, shadows = k.shadows }).ToArray();
            const float C = 24;
            foreach (var g in inst.GroupBy(i => (i.kind, Mathf.FloorToInt(i.m.m03 / C), Mathf.FloorToInt(i.m.m23 / C))))
            {
                var arr = g.OrderBy(i => i.key).Select(i => i.m).ToArray(); var b = new Bounds(arr[0].GetColumn(3), Vector3.one);
                foreach (var m in arr) b.Encapsulate((Vector3)m.GetColumn(3));
                data.cells.Add(new FBVegData.Cell { center = b.center, radius = b.extents.magnitude + 1.5f, kind = g.Key.kind, m = arr });
            }
            data.total = inst.Count;
            if (set.carpet) CarpetMask(data, cover, grassOk, hitP, gcol, nx, nz, sp, bmin);
            data.openSpots = opens.OrderByDescending(o => o.d).Select(o => o.p).Take(8).ToList(); data.edgeSpots = edges.Take(8).ToList();
            var path = $"{VegDir}/{id}_veg.asset"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(data, path);
            if (data.grassMask) { AssetDatabase.AddObjectToAsset(data.grassMask, data); AssetDatabase.AddObjectToAsset(data.grassHeight, data); EditorUtility.SetDirty(data); AssetDatabase.SaveAssets(); }
            var go = new GameObject("Vegetacao"); go.AddComponent<FBVegetation>().data = data;
            Debug.Log($"[FreeBala] vegetação {id}: {inst.Count} instâncias (antes do teto {before}; área jogável {(play.HasValue ? $"{play.Value.width:0}×{play.Value.height:0} m" : "não achada")}; secos {string.Join("/", dry.Select(m => m.name))}) ({string.Join(", ", data.kinds.Select((k, i) => $"{k.name}:{inst.Count(t => t.kind == i)}"))}) em {data.cells.Count} células · {nOk} pontos verdes · afinamento {keep:0.00}");
        }

        // TAPETE DE GRAMA (FBGrassCarpet): máscara do gramado a 0,5 m (A = fração das células de amostragem com grama livre — borda suave no
        // bilinear; RGB = cor média do chão naquele ponto, sRGB: as lâminas puxam o tom do gramado de baixo) + altura do chão (RHalf, acima de grassY0); material FreeBala/GrassCarpet salvo como asset (leva o shader e as variantes)
        static void CarpetMask(FBVegData data, bool[,] cover, bool[,] okG, Vector3[,] hitP, Color[,] gcol, int nx, int nz, float sp, Vector3 bmin)
        {
            const float GS = .5f; int mx = Mathf.CeilToInt(nx * sp / GS) + 1, mz = Mathf.CeilToInt(nz * sp / GS) + 1;
            var tot = new float[mx, mz]; var acc = new float[mx, mz]; var hy = new float[mx, mz]; var hn = new int[mx, mz]; var hc = new Color[mx, mz]; float y0 = float.MaxValue; int nG = 0;
            for (int x = 0; x < nx; x++) for (int z = 0; z < nz; z++)
                {
                    if (!cover[x, z]) continue; int gx = Mathf.Min(mx - 1, (int)((x + .5f) * sp / GS)), gz = Mathf.Min(mz - 1, (int)((z + .5f) * sp / GS)); tot[gx, gz]++;
                    if (!okG[x, z]) continue; acc[gx, gz]++; hy[gx, gz] += hitP[x, z].y; hc[gx, gz] += gcol[x, z]; hn[gx, gz]++; y0 = Mathf.Min(y0, hitP[x, z].y); nG++;
                }
            if (nG == 0) return;
            var hfill = new float[mx, mz]; var has = new bool[mx, mz];
            for (int x = 0; x < mx; x++) for (int z = 0; z < mz; z++) if (hn[x, z] > 0) { hfill[x, z] = hy[x, z] / hn[x, z] - y0; has[x, z] = true; }
            for (int it = 0; it < 3; it++)                                                   // altura vaza 1,5 m para fora do gramado (bilinear da borda)
            {
                var nf = (float[,])hfill.Clone(); var nh = (bool[,])has.Clone();
                for (int x = 0; x < mx; x++) for (int z = 0; z < mz; z++)
                    {
                        if (has[x, z]) continue; float sum = 0; int k = 0;
                        for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++) { int a = x + dx, b = z + dz; if (a >= 0 && b >= 0 && a < mx && b < mz && has[a, b]) { sum += hfill[a, b]; k++; } }
                        if (k > 0) { nf[x, z] = sum / k; nh[x, z] = true; }
                    }
                hfill = nf; has = nh;
            }
            // cor do chão: média das células com grama; vaza para as vizinhas sem cor (borda bilinear não puxa preto)
            var col = new Color[mx, mz]; var hasC = new bool[mx, mz]; var avgAll = Color.black; int nAll = 0;
            for (int x = 0; x < mx; x++) for (int z = 0; z < mz; z++) if (hn[x, z] > 0) { col[x, z] = hc[x, z] / hn[x, z]; hasC[x, z] = true; avgAll += col[x, z]; nAll++; }
            avgAll = nAll > 0 ? avgAll / nAll : new Color(.35f, .45f, .2f);
            if (avgAll.grayscale > .62f) { Debug.LogWarning($"[FreeBala] tapete de grama {data.name}: cor média do chão {avgAll} clara demais (medição falhou?) -> verde padrão"); for (int x = 0; x < mx; x++) for (int z = 0; z < mz; z++) if (hasC[x, z]) col[x, z] = Color.Lerp(col[x, z], new Color(.30f, .38f, .14f), .85f); avgAll = new Color(.30f, .38f, .14f); }
            for (int x = 0; x < mx; x++) for (int z = 0; z < mz; z++) if (!hasC[x, z]) col[x, z] = avgAll;
            var mp = new Color32[mx * mz]; var hp = new ushort[mx * mz];
            for (int z = 0; z < mz; z++) for (int x = 0; x < mx; x++)
                {
                    float m = tot[x, z] > 0 ? acc[x, z] / tot[x, z] : 0; var c = col[x, z];
                    mp[z * mx + x] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255), (byte)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255), (byte)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255), (byte)Mathf.RoundToInt(Mathf.Clamp01(m) * 255));
                    hp[z * mx + x] = Mathf.FloatToHalf(hfill[x, z]);
                }
            var mk = new Texture2D(mx, mz, TextureFormat.RGBA32, false, true) { name = data.name + "_grama_mascara", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var ht = new Texture2D(mx, mz, TextureFormat.RHalf, false, true) { name = data.name + "_grama_altura", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            mk.SetPixels32(mp); mk.Apply(false, false); ht.SetPixelData(hp, 0); ht.Apply(false, false);
            data.grassMask = mk; data.grassHeight = ht; data.grassY0 = y0; data.grassRect = new Vector4(bmin.x, bmin.z, mx * GS, mz * GS);
            var mpath = $"{VegDir}/grama_tapete.mat"; var gm = AssetDatabase.LoadAssetAtPath<Material>(mpath); var sh = Shader.Find("FreeBala/GrassCarpet");
            if (!gm) { gm = new Material(sh) { name = "grama_tapete" }; AssetDatabase.CreateAsset(gm, mpath); } else if (gm.shader != sh) gm.shader = sh;
            gm.enableInstancing = true; EditorUtility.SetDirty(gm); data.grassMat = gm;
            Debug.Log($"[FreeBala] tapete de grama {data.name}: máscara {mx}×{mz} (0,5 m), {nG} pontos de grama, chão {y0:0.00} m");
        }
        // chão sob o tapete: gramado denso (verde médio com manchas secas leves, quase sem terra) no tom das lâminas — de longe, onde as
        // lâminas já sumiram, o gramado continua com cara de grama (o GroundTex antigo era terra + grama rala, feito para os tufos)
        static Texture2D GroundTexCarpet()
        {
            string p = $"{GfxDir}/chao_grama_v3.png";
            if (!File.Exists(p))
            {
                const int S = 512; var t = new Texture2D(S, S, TextureFormat.RGB24, false);
                for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                    {
                        float u = x / (float)S, v = y / (float)S;
                        float g = Fbm(u, v, 5, 4), d = Fbm(u, v, 3, 3, 3), f = Fbm(u, v, 96, 2), sp = Hash(x, y, 31), st = Fbm(u, v, 64, 2, 24);
                        var c = Color.Lerp(new Color(.17f, .27f, .075f), new Color(.27f, .35f, .11f), Mathf.SmoothStep(.25f, .8f, g));
                        c = Color.Lerp(c, new Color(.40f, .39f, .19f), Mathf.SmoothStep(.62f, .85f, d) * .55f);                                    // manchas secas
                        c = Color.Lerp(c, new Color(.30f, .24f, .15f), Mathf.SmoothStep(.86f, .95f, 1 - g) * .5f);                                  // terra rara
                        c *= .82f + f * .26f + (st - .5f) * .08f;                                                                                   // grão das folhas
                        if (sp > .992f) c = Color.Lerp(c, new Color(.5f, .52f, .26f), .5f); else if (sp < .01f) c *= .72f;
                        t.SetPixel(x, y, c);
                    }
                File.WriteAllBytes(p, t.EncodeToPNG()); Object.DestroyImmediate(t); AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceSynchronousImport);
                var ti = AssetImporter.GetAtPath(p) as TextureImporter; if (ti) { ti.wrapMode = TextureWrapMode.Repeat; ti.anisoLevel = 8; ti.mipmapEnabled = true; ti.SaveAndReimport(); }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }
        // área jogável no XZ: todos os pontos do level.json (qualquer lista [x,y,z] em spawns, bots, pontos, waypoints, becos, carros…)
        static Rect? PlayArea(string id)
        {
            var f = Path.Combine(FBImport.Src, "maps", id, "level.json"); if (!File.Exists(f)) return null;
            var pts = new List<Vector3>();
            void Walk(object o)
            {
                if (o is List<object> l)
                {
                    if (l.Count >= 3 && l.Count <= 4 && l.Take(3).All(v => v is double || v is long || v is int || v is float)) { pts.Add(Coords.FromWeb(l)); return; }
                    foreach (var v in l) Walk(v);
                }
                else if (o is Dictionary<string, object> d) foreach (var kv in d) if (kv.Key != "fog" && kv.Key != "walls") Walk(kv.Value);
            }
            Walk(Json.Parse(File.ReadAllText(f)));
            if (pts.Count < 3) return null;
            float x0 = pts.Min(p => p.x), x1 = pts.Max(p => p.x), z0 = pts.Min(p => p.z), z1 = pts.Max(p => p.z);
            return Rect.MinMaxRect(x0, z0, x1, z1);
        }
        // ruído de valor em coordenadas de mundo: 'scale' = tamanho típico da mancha em metros
        static float WN(float x, float z, float scale, int oct) => Fbm(x / (scale * 64) + .5f, z / (scale * 64) + .5f, 64, oct);
        static bool IsGreenCandidate(Material m)
        {
            string n = m.name; if (NotGround.IsMatch(n) || m.renderQueue >= 2450) return false;
            if (GreenName.IsMatch(n)) return true;
            var c = AvgColor(m); float mx = Mathf.Max(c.r, c.g, c.b), mn = Mathf.Min(c.r, c.g, c.b);
            return c.g > c.r * 1.1f && c.g > c.b * 1.15f && mx - mn > .07f && mx > .08f;
        }
        static readonly Dictionary<Texture, Color> avgCache = new();
        // cor média do chão de um material (o tapete de grama pinta as lâminas com ela). Antes lia o arquivo da textura com LoadImage: textura
        // dentro do .gltf / .asset / formato que o LoadImage não abre caía no BRANCO -> grama toda branca na Praça das Águas (93 % da máscara).
        // Agora: média na GPU (Blit da textura para 32x32 e ReadPixels), qualquer textura/formato; nomes de propriedade do URP e do glTFast.
        static Color AvgColor(Material m)
        {
            Color bc = Color.white; foreach (var n in new[] { "_BaseColor", "_Color", "baseColorFactor" }) if (m.HasProperty(n)) { bc = m.GetColor(n); break; }
            Texture t = null; foreach (var n in new[] { "_BaseMap", "_MainTex", "baseColorTexture", "_BaseColorMap" }) if (m.HasProperty(n) && m.GetTexture(n)) { t = m.GetTexture(n); break; }
            if (!t) return new Color(bc.r, bc.g, bc.b, 1);
            if (!avgCache.TryGetValue(t, out var a))
            {
                var g = GpuAvg(t);
                if (g == null) { Debug.LogWarning($"[FreeBala] grama: não consegui medir a cor de {t.name} ({m.name}); usando verde padrão"); g = new Color(.30f, .38f, .14f); }
                a = g.Value; avgCache[t] = a;
            }
            var c = a * bc; c.a = 1; return c;
        }
        static Color? GpuAvg(Texture t)
        {
            RenderTexture rt = null; Texture2D tx = null; var prev = RenderTexture.active;
            try
            {
                rt = RenderTexture.GetTemporary(32, 32, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); Graphics.Blit(t, rt);
                RenderTexture.active = rt; tx = new Texture2D(32, 32, TextureFormat.RGBA32, false, false); tx.ReadPixels(new Rect(0, 0, 32, 32), 0, 0); tx.Apply(false);
                var px = tx.GetPixels32(); double r = 0, gg = 0, b = 0; foreach (var q in px) { r += q.r; gg += q.g; b += q.b; }
                int k = px.Length; return new Color((float)(r / k / 255), (float)(gg / k / 255), (float)(b / k / 255), 1);
            }
            catch (System.Exception e) { Debug.LogWarning($"[FreeBala] grama: média na GPU falhou em {t.name}: {e.Message}"); return null; }
            finally { RenderTexture.active = prev; if (rt) RenderTexture.ReleaseTemporary(rt); if (tx) Object.DestroyImmediate(tx); }
        }
        static Material MatAt(RaycastHit h, Dictionary<Mesh, int[]> cache)
        {
            var r = h.collider.GetComponent<MeshRenderer>(); var mf = h.collider.GetComponent<MeshFilter>(); if (!r || !mf || !mf.sharedMesh) return null; var ms = r.sharedMaterials;
            var mesh = mf.sharedMesh; if (mesh.subMeshCount <= 1 || h.triangleIndex < 0) return ms.Length > 0 ? ms[0] : null;
            if (!cache.TryGetValue(mesh, out var starts)) { starts = Enumerable.Range(0, mesh.subMeshCount).Select(s => mesh.GetSubMesh(s).indexStart).ToArray(); cache[mesh] = starts; }
            int ti = h.triangleIndex * 3, sub = 0; for (int s = 0; s < starts.Length; s++) if (ti >= starts[s]) sub = s;
            return sub < ms.Length ? ms[sub] : null;
        }
        static bool InTri2D(float px, float pz, Vector3 a, Vector3 b, Vector3 c, float pad)
        {
            float S(Vector3 p, Vector3 q, float x, float z) => (q.x - p.x) * (z - p.z) - (q.z - p.z) * (x - p.x);
            float d1 = S(a, b, px, pz), d2 = S(b, c, px, pz), d3 = S(c, a, px, pz);
            if (!((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0))) return true;
            // perto da aresta (célula maior que o triângulo): aceita se o centro do triângulo está a menos de 'pad'
            var cc = (a + b + c) / 3; return (new Vector2(cc.x - px, cc.z - pz)).magnitude < pad;
        }
        static Material VegMaterial(string glb, Material src)
        {
            var path = $"{VegDir}/veg_{glb}.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path); bool mk = !m;
            if (mk) m = new Material(FBShaders.Foliage); else m.shader = FBShaders.Foliage; m.name = "veg_" + glb;
            var bt = src && src.HasProperty("baseColorTexture") ? src.GetTexture("baseColorTexture") : null; var nt = src && src.HasProperty("normalTexture") ? src.GetTexture("normalTexture") : null;
            if (bt) { m.SetTexture("_BaseMap", bt); m.SetTextureScale("_BaseMap", src.GetTextureScale("baseColorTexture")); m.SetTextureOffset("_BaseMap", src.GetTextureOffset("baseColorTexture")); }
            if (nt) { m.SetTexture("_BumpMap", nt); m.EnableKeyword("_NORMALMAP"); } else m.DisableKeyword("_NORMALMAP");
            m.SetColor("_BaseColor", glb == "capim_vj" ? new Color(.92f, .95f, .85f) : new Color(.95f, 1, .9f)); m.SetFloat("_Smoothness", .12f); m.SetFloat("_Metallic", 0);
            m.SetFloat("_Cull", 0); m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cutoff", .5f); m.EnableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_FB_VEG");
            m.SetFloat("_AlphaToMask", 1);                                                    // alpha-to-coverage com MSAA (borda suave, sem halo)
            FixAlphaTex(bt);
            m.SetOverrideTag("RenderType", "TransparentCutout"); m.renderQueue = (int)RenderQueue.AlphaTest; m.enableInstancing = true;
            if (mk) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
            return m;
        }
        // textura de cartão com alfa: alfa é transparência (o Unity dilata a cor), mipmaps preservam a cobertura do recorte em 0,5
        static void FixAlphaTex(Texture t)
        {
            var ti = t ? AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t)) as TextureImporter : null; if (!ti) return;
            if (ti.alphaIsTransparency && ti.mipMapsPreserveCoverage && Mathf.Approximately(ti.alphaTestReferenceValue, .5f)) return;
            ti.alphaIsTransparency = true; ti.mipMapsPreserveCoverage = true; ti.alphaTestReferenceValue = .5f; ti.mipmapEnabled = true; ti.SaveAndReimport();
        }
        // chão sob a vegetação: terra avermelhada de Roraima + grama rala/seca (tileável, 512²) — some o "tapete verde chapado"
        static Texture2D GroundTex()
        {
            string p = $"{GfxDir}/chao_grama_v2.png";
            if (!File.Exists(p))
            {
                const int S = 512; var t = new Texture2D(S, S, TextureFormat.RGB24, false);
                Color soil = new(.42f, .32f, .22f), soil2 = new(.33f, .25f, .18f), grass = new(.30f, .38f, .16f), dry = new(.52f, .48f, .27f);
                for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                    {
                        float u = x / (float)S, v = y / (float)S;
                        float g = Fbm(u, v, 6, 4), d = Fbm(u, v, 3, 3, 3), f = Fbm(u, v, 64, 2), sp = Hash(x, y, 21);
                        // grama dominante (verde ↔ seca em manchas grandes e suaves), terra só em falhas pequenas — sem grade visível
                        var c = Color.Lerp(new Color(.25f, .37f, .12f), new Color(.40f, .42f, .18f), Mathf.SmoothStep(.3f, .75f, d));
                        c = Color.Lerp(c, Color.Lerp(soil, soil2, Fbm(u, v, 12, 2)), Mathf.SmoothStep(.62f, .8f, 1 - g) * .75f);
                        if (sp > .985f) c = Color.Lerp(c, dry * 1.15f, .6f); else if (sp < .012f) c *= .7f;                                             // fiapos e pedrinhas
                        c *= .9f + f * .2f; t.SetPixel(x, y, c);
                    }
                File.WriteAllBytes(p, t.EncodeToPNG()); Object.DestroyImmediate(t); AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceSynchronousImport);
                var ti = AssetImporter.GetAtPath(p) as TextureImporter; if (ti) { ti.wrapMode = TextureWrapMode.Repeat; ti.anisoLevel = 4; ti.mipmapEnabled = true; ti.SaveAndReimport(); }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }
    }
}
