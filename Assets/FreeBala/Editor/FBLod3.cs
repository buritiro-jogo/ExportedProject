// LOD EM 3 NÍVEIS (rodada 42/43, pedido do usuário 28/10: "clonando o mesmo elemento 3 vezes, com detalhes diferentes: máxima qualidade
// para perto, média para média distância, baixa para longas distâncias — a otimização que as grandes empresas fazem").
// No Unity 6 isso é o Mesh LOD: a MESMA malha guarda os três níveis (100 % / ~50 % / ~15 % dos triângulos, gerados por simplificação
// que preserva bordas e UV — MeshLodUtility) e o renderizador escolhe o nível pelo tamanho na tela, sem trocar objeto nem duplicar
// memória de vértices. Aplicado a:
//  · mapas (Map_*.unity): toda malha estática >= MinTris sob "Map_*" (prédios, casca longe, interiores, mato/árvores juntados, props);
//  · personagens (Resources/FBPrefabs/Chars): a malha do corpo (SkinnedMeshRenderer) — cada um tem ~42 mil triângulos.
// O gerador do Unity já derrubou o editor (rodada 41, modelo completo dos blocos): o lote anota "INICIO <malha>" / "FIM" num diário
// (logs/lod3_diario.txt) ANTES de cada geração; se o editor cair, a próxima rodada lê o diário, põe a malha sem "FIM" na lista negra
// (logs/lod3_negadas.txt) e segue — todas as outras ganham os 3 níveis e a culpada fica registrada.
// Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBLod3.Batch [-fblod3maps vila_jardim,praca_pt] [-fblod3semchars]
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBLod3
    {
        public const int Levels = 3, MinTris = 600;
        const string Dir = "Assets/FreeBala/Optimized/Lod3";
        static string Diario => Path.GetFullPath("../logs/lod3_diario.txt");
        static string Negadas => Path.GetFullPath("../logs/lod3_negadas.txt");
        static HashSet<string> deny;
        static int made, reused, skipped, failed, emptySub, foliage;

        static string Arg(string k) { var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, k); return i >= 0 && i + 1 < a.Length ? a[i + 1] : null; }
        public static void Batch()
        {
            int code = 0;
            try
            {
                Recover(); Directory.CreateDirectory(Dir);
                var only = Arg("-fblod3maps")?.Split(',').Select(x => "Map_" + x).ToHashSet();
                foreach (var f in Directory.GetFiles(FBImport.SceneDir, "Map_*.unity").Select(f => f.Replace('\\', '/')))
                {
                    var n = Path.GetFileNameWithoutExtension(f); if (only != null && !only.Contains(n)) continue;
                    var sc = EditorSceneManager.OpenScene(f, OpenSceneMode.Single); var rep = Scene(sc);
                    EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc); AssetDatabase.SaveAssets();
                    Debug.Log($"[FreeBala][lod3] {n}: {rep}");
                }
                if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fblod3semchars") < 0) Debug.Log("[FreeBala][lod3] personagens: " + Chars());
                Debug.Log("[FreeBala][lod3] veículos: " + Veiculos());
                AssetDatabase.SaveAssets();
                Debug.Log($"[FreeBala][lod3] fim · malhas geradas {made} · reaproveitadas {reused} · antigas descartadas {stale} · puladas {skipped} (submalha vazia {emptySub}, folhagem {foliage}) · falharam {failed} · lista negra {deny.Count}");
            }
            catch (System.Exception e) { Debug.LogError("[FreeBala][lod3] " + e); code = 1; }
            EditorApplication.Exit(code);
        }
        // diário: malha com INICIO sem FIM = derrubou o editor na rodada anterior
        static void Recover()
        {
            deny = File.Exists(Negadas) ? File.ReadAllLines(Negadas).Where(l => l.Trim().Length > 0).ToHashSet() : new HashSet<string>();
            if (File.Exists(Diario))
            {
                string open = null; foreach (var l in File.ReadAllLines(Diario)) { if (l.StartsWith("INICIO ")) open = l.Substring(7); else if (l.StartsWith("FIM ")) open = null; }
                if (open != null && deny.Add(open)) { File.AppendAllText(Negadas, open + "\n"); Debug.LogWarning("[FreeBala][lod3] lista negra (derrubou o editor): " + open); }
            }
            File.WriteAllText(Diario, "");
        }
        static long Tris(Mesh m) { long n = 0; for (int i = 0; i < m.subMeshCount; i++) n += m.GetIndexCount(i) / 3; return n; }
        static uint Fnv(string s) { uint h = 2166136261; foreach (var c in s) h = (h ^ c) * 16777619; return h; }
        static readonly Dictionary<Mesh, Mesh> cache = new();
        static int stale;
        // assinatura do conteúdo: vértices, submalhas e caixa (cm) — a cópia com LOD guarda os mesmos vértices e submalhas da original
        public static string Sig(Mesh m) { var b = m.bounds; return $"{m.vertexCount}|{m.subMeshCount}|{b.center.x:0.00},{b.center.y:0.00},{b.center.z:0.00}|{b.size.x:0.00},{b.size.y:0.00},{b.size.z:0.00}"; }
        static bool InDir(Mesh m) => m && AssetDatabase.GetAssetPath(m).StartsWith(Dir + "/");

        // r62: REPARO da cena sem reinstalar o mapa: cada renderer com malha de LOD (Optimized/Lod3) volta à malha ORIGINAL (o MeshCollider
        // do próprio objeto, ou a malha de mesmo nome no glTF/Optimized) e o LOD é refeito só se a cópia não bate com a original.
        // Unity -batchmode -executeMethod FreeBala.EditorTools.FBLod3.ReparoBatch [-fblod3maps vila_jardim]
        public static void ReparoBatch()
        {
            int code = 0;
            try
            {
                Recover(); var only = Arg("-fblod3maps") ?? "vila_jardim";
                foreach (var id in only.Split(','))
                {
                    var f = $"{FBImport.SceneDir}/Map_{id}.unity"; var sc = EditorSceneManager.OpenScene(f, OpenSceneMode.Single);
                    // índice por nome das malhas que podem ser a original (glTF do mapa + Optimized fora do Lod3 + Art)
                    var idx = new Dictionary<string, List<Mesh>>();
                    void Add(Mesh x) { if (!x || InDir(x)) return; if (!idx.TryGetValue(x.name, out var l)) idx[x.name] = l = new List<Mesh>(); if (!l.Contains(x)) l.Add(x); }
                    foreach (var g in Directory.GetFiles($"{FBImport.Imp}/Maps", id + "*.gltf")) foreach (var x in AssetDatabase.LoadAllAssetsAtPath(g.Replace(Path.DirectorySeparatorChar, '/')).OfType<Mesh>()) Add(x);
                    foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/FreeBala/Optimized", "Assets/FreeBala/Art" }))
                    { var ap = AssetDatabase.GUIDToAssetPath(guid); if (ap.StartsWith(Dir + "/")) continue; foreach (var x in AssetDatabase.LoadAllAssetsAtPath(ap).OfType<Mesh>()) Add(x); }
                    int ok = 0, fixd = 0, back = 0, amb = 0, none = 0;
                    foreach (var root in sc.GetRootGameObjects())
                        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                        {
                            var cur = mf.sharedMesh; if (!InDir(cur)) continue;
                            var nm = System.Text.RegularExpressions.Regex.Replace(cur.name, "(_lod3)?(_[0-9a-f]{8})?$", "");   // o asset principal leva o nome do arquivo (nome_hash)
                            var mc = mf.GetComponent<MeshCollider>(); Mesh src = mc && mc.sharedMesh && !InDir(mc.sharedMesh) && mc.sharedMesh.name == nm ? mc.sharedMesh : null;
                            if (!src && idx.TryGetValue(nm, out var cands))
                            {
                                var r = mf.GetComponent<MeshRenderer>(); int nmat = r ? r.sharedMaterials.Length : -1;
                                var fit = cands.Where(x => x.subMeshCount == nmat).ToList(); if (fit.Count == 0) fit = cands;
                                if (fit.Count == 1) src = fit[0]; else { amb++; Debug.LogWarning($"[FreeBala][lod3] reparo: {nm} tem {fit.Count} originais possíveis — fica como está"); continue; }
                            }
                            if (!src) { none++; Debug.LogWarning($"[FreeBala][lod3] reparo: original de {nm} não achada"); continue; }
                            if (Sig(src) == Sig(cur)) { ok++; continue; }
                            bool seco = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbseco") >= 0;
                            if (seco) { fixd++; Debug.Log($"[FreeBala][lod3] reparo (seco): {nm} · antiga {Sig(cur)} · original {Sig(src)}"); continue; }
                            var nl = Lod(src); mf.sharedMesh = nl ? nl : src; if (nl) fixd++; else back++;
                            var rr = mf.GetComponent<MeshRenderer>();
                            Debug.Log($"[FreeBala][lod3] reparo: {nm} · antiga {Sig(cur)} · original {Sig(src)} · mats {(rr ? rr.sharedMaterials.Length : 0)} → {(nl ? "LOD refeito" : "malha original")}");
                        }
                    if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbseco") < 0) { EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc); AssetDatabase.SaveAssets(); }
                    Debug.Log($"[FreeBala][lod3] reparo Map_{id}: {ok} certas · {fixd} LOD refeito · {back} voltaram à original · {amb} ambíguas · {none} sem original · geradas {made} · antigas descartadas {stale}");
                }
            }
            catch (System.Exception e) { Debug.LogError("[FreeBala][lod3] " + e); code = 1; }
            EditorApplication.Exit(code);
        }
        /// <summary>Cópia da malha com os 3 níveis (asset em Optimized/Lod3, reaproveitada entre rodadas); null = não deu/não vale.</summary>
        public static Mesh Lod(Mesh src)
        {
            if (!src || src.name.EndsWith("_lod3") || Tris(src) < MinTris) { skipped++; return null; }
            if (cache.TryGetValue(src, out var c)) return c;
            var sp = AssetDatabase.GetAssetPath(src);
            if (sp.StartsWith(Dir + "/")) { reused++; return cache[src] = src; }                 // já é a cópia com 3 níveis (rodada anterior)
            // submalha VAZIA derruba o gerador (segfault no editor): FBGlassSplit/FBLampPrep zeram a submalha do vidro/lâmpada
            // para manter os slots de material ("_semvidro", "_semLampada") — essas ficam de fora (são poucas e pequenas).
            for (int i = 0; i < src.subMeshCount; i++) if (src.GetIndexCount(i) == 0) { emptySub++; skipped++; return cache[src] = null; }
            var key = sp + "|" + src.name; if (deny.Contains(key)) { skipped++; return cache[src] = null; }
            // r62 (08/11): o reaproveitamento era SÓ pelo nome — a VJ r54z (04/11) manteve os nomes e herdou 194 malhas de LOD de versões
            // antigas (quadra com 5 submalhas para 6 materiais = piso com a tinta branca; quiosque virou caixa). Agora o conteúdo entra na chave.
            var sig = Sig(src); var path = $"{Dir}/{FBRegistry.Safe(src.name)}_{Fnv(key + "|" + sig):x8}.asset";
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (m && Sig(m) == sig) { reused++; return cache[src] = m; }
            var old = AssetDatabase.LoadAssetAtPath<Mesh>($"{Dir}/{FBRegistry.Safe(src.name)}_{Fnv(key):x8}.asset");   // nome antigo (sem conteúdo na chave)
            if (old && Sig(old) == sig) { reused++; return cache[src] = old; }
            if (old) stale++;
            File.AppendAllText(Diario, "INICIO " + key + "\n");
            try
            {
                m = Object.Instantiate(src); m.name = src.name + "_lod3";
                MeshLodUtility.GenerateMeshLods(m, Levels);
                AssetDatabase.CreateAsset(m, path); made++;
                File.AppendAllText(Diario, "FIM " + key + "\n");
                return cache[src] = m;
            }
            catch (System.Exception e) { File.AppendAllText(Diario, "FIM " + key + "\n"); failed++; Debug.LogWarning($"[FreeBala][lod3] {src.name}: {e.Message}"); return cache[src] = null; }
        }
        static string Scene(UnityEngine.SceneManagement.Scene sc)
        {
            int n = 0; long before = 0;
            foreach (var root in sc.GetRootGameObjects())
            {
                if (!root.name.StartsWith("Map_")) continue;
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var r = mf.GetComponent<MeshRenderer>(); if (!r || !r.enabled || !mf.sharedMesh) continue;           // só colisão: fica
                    if (mf.GetComponent<FBGlass>() || mf.GetComponentInParent<FBPole>()) continue;                       // vidro/poste dinâmicos
                    // r43c: FOLHAGEM fica de fora — copa feita de cartões com recorte (alfa) não aguenta simplificação: os cartões viravam
                    // polígonos grandes caindo na casca/fundo do atlas (tour: árvores do 171 como placas cinzas facetadas). Folhagem pede impostor.
                    if (r.sharedMaterials.Any(mt => mt && (mt.renderQueue >= 2450 && mt.renderQueue < 3000 || mt.shader && mt.shader.name.Contains("Foliage")))
                        || mf.name.StartsWith("VJ11_Arvore") || mf.name.StartsWith("VJ11_Mato")) { foliage++; continue; }
                    var src = mf.sharedMesh; var m = Lod(src); if (!m) continue;
                    before += Tris(src); if (m == src) { n++; continue; }
                    mf.sharedMesh = m; n++;                                                                               // o MeshCollider continua com a original
                    GameObjectUtility.SetStaticEditorFlags(mf.gameObject, GameObjectUtility.GetStaticEditorFlags(mf.gameObject) & ~StaticEditorFlags.BatchingStatic);   // Mesh LOD não combina com static batching
                }
            }
            return $"{n} renderizadores com 3 níveis ({before / 1000}k tri no nível 0)";
        }
        // r44: motos do MX Grau (70–120 mil triângulos cada): cópia de cada malha com 3 níveis; o FBMxBike troca na hora de montar (FBRegistry.LodOf)
        static string Veiculos()
        {
            var reg = AssetDatabase.LoadAssetAtPath<FBRegistry>("Assets/FreeBala/Resources/FBRegistry.asset"); if (!reg) return "sem registro"; int n = 0;
            reg.lodSrc.Clear(); reg.lodDst.Clear();
            foreach (var f in Directory.GetFiles($"{FBImport.Imp}/Vehicles", "mx_*.gltf").Select(f => f.Replace('\\', '/')))
                foreach (var m in AssetDatabase.LoadAllAssetsAtPath(f).OfType<Mesh>()) { var l = Lod(m); if (l && l != m) { reg.lodSrc.Add(m); reg.lodDst.Add(l); n++; } }
            EditorUtility.SetDirty(reg); AssetDatabase.SaveAssets(); return $"{n} malhas das motos MX";
        }
        static string Chars()
        {
            if (!Directory.Exists(FBCharImport.PrefabDir)) return "sem pasta"; int n = 0, p = 0;
            foreach (var f in Directory.GetFiles(FBCharImport.PrefabDir, "*.prefab").Select(f => f.Replace('\\', '/')))
            {
                var root = PrefabUtility.LoadPrefabContents(f); bool dirty = false;
                try
                {
                    foreach (var s in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    { var m = Lod(s.sharedMesh); if (m && m != s.sharedMesh) { s.sharedMesh = m; dirty = true; } if (m) n++; }
                    if (dirty) { PrefabUtility.SaveAsPrefabAsset(root, f); p++; }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            return $"{n} malhas em {p} prefabs";
        }
    }
}
