// Rodada 40 (27/10/2026): Vila Jardim inteira. Depois do FBImportMap (-fbmap vila_jardim), otimiza SÓ a cena da Vila Jardim
// (FBOptimize.OptimizeScene: flags estáticas, sombra de prop pequeno) e LIMPA a oclusão Umbra assada (o mapa mudou; a oclusão foi
// desligada no jogo pela FBRenderAudit — dado velho só ocuparia espaço). Os outros mapas não são tocados.
// Uso: Unity -batchmode -executeMethod FreeBala.EditorTools.FBImportR40.OptimizeVJBatch
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBImportR40
    {
        // (r41) vegetação da v11: 4 881 tufos + 1 053 árvores eram ~6 mil renderers (de 9,5 mil no mapa) — culling e envio de desenho por
        // objeto custavam ~10 ms/quadro. No glb ficam como instâncias (arquivo pequeno); aqui viram UMA malha por célula de 64 m e tipo
        // (VJ11_MatoCel_* / VJ11_ArvoreCel_*), uma submalha por material. O FBDistanceCull corta o mato por célula (VJ11_Mato*).
        // ---- r43: LOD de FOLHAGEM por cartões (o pedido do usuário: "clonando o mesmo elemento 3 vezes"). A simplificação automática
        // (Mesh LOD) desmanchava os cartões de folha com recorte alfa (blocos cinzas facetados, sombra em degraus — o "Minecraft"); aqui
        // cada célula de árvores ganha 3 cópias num LODGroup: completa; metade dos cartões de folha (os que ficam crescem ×1,32 para
        // manter a copa cheia); um quarto (×1,74). Tronco e galhos (material opaco) iguais nos 3. Cartão = ilha de triângulos ligados.
        static long[] triL = new long[3];
        static bool Leafy(Material mt) => mt && (mt.renderQueue >= 2450 && mt.renderQueue < 3000 || mt.shader && mt.shader.name.Contains("Foliage") || (mt.HasProperty("_AlphaClip") && mt.GetFloat("_AlphaClip") > .5f));
        // r53: nas árvores do 171, 80–95 % dos triângulos estão no material "_tronco" (galhos e raminhos em 150–700 ilhas de até 60 tri, recorte
        // alfa no atlas) e o desbaste só mexia nas folhas: LOD0 3 491k · LOD1 3 084k · LOD2 2 886k tri (o nível de longe com 83 % do peso).
        // Agora os galhos pequenos também são raleados; peças > 1,2 m (tronco, galho-mestre) ficam. r53b: 60/30 % deixava a copa rala e cinza
        // de longe (no 171 muitos tufos de folha usam o atlas "_tronco") -> 78/42 % e os raminhos que ficam crescem como os cartões (keep^-0,4).
        static bool CardLods(GameObject go, Mesh m0, System.Collections.Generic.List<Material> mats, string dir)
        {
            if (!mats.Exists(Leafy)) return false;
            var r0 = go.GetComponent<MeshRenderer>(); var keep = new[] { 1f, .5f, .25f }; var keepW = new[] { 1f, .78f, .42f }; var lr = new LOD[3]; triL[0] += Tris(m0);
            for (int L = 1; L < 3; L++)
            {
                var m = Thin(m0, mats, keep[L], Mathf.Pow(keep[L], -.4f), keepW[L]); m.name = m0.name + "_L" + L; AssetDatabase.CreateAsset(m, $"{dir}/{m.name}.asset"); triL[L] += Tris(m);
                var c = new GameObject(go.name + "_L" + L); c.transform.SetParent(go.transform, false); c.layer = go.layer;
                GameObjectUtility.SetStaticEditorFlags(c, GameObjectUtility.GetStaticEditorFlags(go));
                c.AddComponent<MeshFilter>().sharedMesh = m; var r = c.AddComponent<MeshRenderer>(); r.sharedMaterials = r0.sharedMaterials; r.shadowCastingMode = r0.shadowCastingMode;
                lr[L] = new LOD(L == 1 ? .12f : .012f, new Renderer[] { r });
            }
            lr[0] = new LOD(.32f, new Renderer[] { r0 });                                    // célula de 24 m: ~60 m -> L1, ~180 m -> L2, ~2 km some
            var g = go.AddComponent<LODGroup>(); g.SetLODs(lr); g.RecalculateBounds(); g.fadeMode = LODFadeMode.None; return true;
        }
        static long Tris(Mesh m) { long t = 0; for (int i = 0; i < m.subMeshCount; i++) t += m.GetIndexCount(i) / 3; return t; }
        static Mesh Thin(Mesh src, System.Collections.Generic.List<Material> mats, float keep, float grow, float keepWood = 1f)
        {
            var V = src.vertices; var N = src.normals; var T = src.tangents; var U = src.uv; var U2 = src.uv2; var C = src.colors;
            bool hn = N.Length == V.Length, ht = T.Length == V.Length, hu = U.Length == V.Length, hu2 = U2.Length == V.Length, hc = C.Length == V.Length;
            var nv = new System.Collections.Generic.List<Vector3>(); var nn = new System.Collections.Generic.List<Vector3>(); var nt = new System.Collections.Generic.List<Vector4>();
            var nu = new System.Collections.Generic.List<Vector2>(); var nu2 = new System.Collections.Generic.List<Vector2>(); var nc = new System.Collections.Generic.List<Color>();
            var subs = new System.Collections.Generic.List<int[]>();
            for (int si = 0; si < src.subMeshCount; si++)
            {
                var I = src.GetIndices(si); bool leaf = si < mats.Count && Leafy(mats[si]), wood = !leaf && keepWood < 1f; var outI = new System.Collections.Generic.List<int>(I.Length);
                // ilhas (cartões): união dos vértices de cada triângulo
                var par = new System.Collections.Generic.Dictionary<int, int>(); int F(int x) { while (par.TryGetValue(x, out var p) && p != x) { par.TryGetValue(p, out var pp); par[x] = pp; x = pp; } return x; }
                void Un(int a, int b) { if (!par.ContainsKey(a)) par[a] = a; if (!par.ContainsKey(b)) par[b] = b; a = F(a); b = F(b); if (a != b) par[a] = b; }
                if (leaf || wood) for (int k = 0; k < I.Length; k += 3) { Un(I[k], I[k + 1]); Un(I[k], I[k + 2]); }
                var big = new System.Collections.Generic.HashSet<int>();   // r53: ilhas de madeira > 1,2 m (tronco) nunca saem
                if (wood)
                {
                    var bb = new System.Collections.Generic.Dictionary<int, Bounds>();
                    for (int k = 0; k < I.Length; k++) { int r = F(I[k]); if (bb.TryGetValue(r, out var b)) { b.Encapsulate(V[I[k]]); bb[r] = b; } else bb[r] = new Bounds(V[I[k]], Vector3.zero); }
                    foreach (var kv in bb) if (kv.Value.size.magnitude > 1.2f) big.Add(kv.Key);
                }
                var ctr = new System.Collections.Generic.Dictionary<int, Vector4>();
                if (leaf || wood) for (int k = 0; k < I.Length; k++) { int r = F(I[k]); ctr.TryGetValue(r, out var a); ctr[r] = a + new Vector4(V[I[k]].x, V[I[k]].y, V[I[k]].z, 1); }
                var map = new System.Collections.Generic.Dictionary<int, int>();
                for (int k = 0; k < I.Length; k += 3)
                {
                    int root = leaf || wood ? F(I[k]) : 0;
                    if (leaf) { uint h = (uint)root * 2654435761u; if ((h >> 8) % 1000 >= keep * 1000) continue; }   // sorteio fixo por cartão
                    else if (wood && !big.Contains(root)) { uint h = (uint)root * 2654435761u; if ((h >> 8) % 1000 >= keepWood * 1000) continue; }
                    for (int e = 0; e < 3; e++)
                    {
                        int vi = I[k + e];
                        if (!map.TryGetValue(vi, out var ni))
                        {
                            ni = nv.Count; map[vi] = ni; var p = V[vi];
                            if (leaf || wood && !big.Contains(root)) { var a = ctr[root]; var cc = new Vector3(a.x, a.y, a.z) / a.w; p = cc + (p - cc) * (leaf ? grow : Mathf.Pow(keepWood, -.4f)); }
                            nv.Add(p); if (hn) nn.Add(N[vi]); if (ht) nt.Add(T[vi]); if (hu) nu.Add(U[vi]); if (hu2) nu2.Add(U2[vi]); if (hc) nc.Add(C[vi]);
                        }
                        outI.Add(ni);
                    }
                }
                subs.Add(outI.ToArray());
            }
            var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; m.SetVertices(nv); if (hn) m.SetNormals(nn); if (ht) m.SetTangents(nt); if (hu) m.SetUVs(0, nu); if (hu2) m.SetUVs(1, nu2); if (hc) m.SetColors(nc);
            m.subMeshCount = subs.Count; for (int i = 0; i < subs.Count; i++) m.SetTriangles(subs[i], i, false); m.RecalculateBounds(); return m;
        }
        static string MergeVegetation(UnityEngine.SceneManagement.Scene sc)
        {
            const float CELL = 64f, CELL_TREE = 24f; var dir = "Assets/FreeBala/Optimized/Map_vila_jardim/veg";   // r43: árvore em célula de 24 m (o LOD escolhe pelo tamanho na tela) System.IO.Directory.CreateDirectory(dir);
            foreach (var f in System.IO.Directory.GetFiles(dir, "*.asset")) AssetDatabase.DeleteAsset(f.Replace(System.IO.Path.DirectorySeparatorChar, '/'));
            int nIn = 0, nOut = 0, lodCells = 0; triL = new long[3];
            foreach (var root in sc.GetRootGameObjects())
            {
                var groups = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<MeshRenderer>>();
                foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var n = mr.name; string kind = n.StartsWith("VJ11_Mato") ? "Mato" : n.StartsWith("VJ11_Arvore") ? "Arvore" : null;
                    if (kind == null || !mr.GetComponent<MeshFilter>() || !mr.GetComponent<MeshFilter>().sharedMesh) continue;
                    var c = mr.bounds.center; float cs = kind == "Arvore" ? CELL_TREE : CELL; var key = $"{kind}Cel_{Mathf.FloorToInt(c.x / cs)}_{Mathf.FloorToInt(c.z / cs)}";
                    if (!groups.TryGetValue(key, out var l)) groups[key] = l = new System.Collections.Generic.List<MeshRenderer>(); l.Add(mr);
                }
                foreach (var kv in groups)
                {
                    var byMat = new System.Collections.Generic.Dictionary<Material, System.Collections.Generic.List<CombineInstance>>(); var any = kv.Value[0];
                    foreach (var mr in kv.Value)
                    {
                        var mesh = mr.GetComponent<MeshFilter>().sharedMesh; var mats = mr.sharedMaterials;
                        for (int i = 0; i < mesh.subMeshCount && i < mats.Length; i++)
                        {
                            if (!mats[i]) continue;
                            if (!byMat.TryGetValue(mats[i], out var ci)) byMat[mats[i]] = ci = new System.Collections.Generic.List<CombineInstance>();
                            ci.Add(new CombineInstance { mesh = mesh, subMeshIndex = i, transform = mr.transform.localToWorldMatrix });
                        }
                    }
                    var parts = new System.Collections.Generic.List<CombineInstance>(); var matList = new System.Collections.Generic.List<Material>();
                    foreach (var mk in byMat)
                    {
                        var sub = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; sub.CombineMeshes(mk.Value.ToArray(), true, true);
                        parts.Add(new CombineInstance { mesh = sub, transform = Matrix4x4.identity }); matList.Add(mk.Key);
                    }
                    var m = new Mesh { name = "VJ11_" + kv.Key, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; m.CombineMeshes(parts.ToArray(), false, false);
                    m.RecalculateBounds(); AssetDatabase.CreateAsset(m, $"{dir}/{m.name}.asset");
                    var go = new GameObject("VJ11_" + kv.Key); go.transform.SetParent(any.transform.root, false); go.layer = any.gameObject.layer;
                    go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity; go.transform.localScale = Vector3.one;
                    go.AddComponent<MeshFilter>().sharedMesh = m; var nr = go.AddComponent<MeshRenderer>(); nr.sharedMaterials = matList.ToArray();
                    nr.shadowCastingMode = kv.Key.StartsWith("Mato") ? UnityEngine.Rendering.ShadowCastingMode.Off : any.shadowCastingMode;   // mato rasteiro sem sombra
                    GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(any.gameObject) & ~StaticEditorFlags.BatchingStatic);
                    foreach (var mr in kv.Value) { var mf0 = mr.GetComponent<MeshFilter>(); Object.DestroyImmediate(mr); if (mf0) Object.DestroyImmediate(mf0); nIn++; }   // como o FBPolePrep: tira os componentes (o objeto é de instância de prefab)
                    foreach (var p in parts) Object.DestroyImmediate(p.mesh);
                    if (kv.Key.StartsWith("Arvore")) lodCells += CardLods(go, m, matList, dir) ? 1 : 0;
                    nOut++;
                }
            }
            return $"vegetação juntada: {nIn} objetos -> {nOut} células · árvores com LOD de cartões: {lodCells} células (tri LOD0 {triL[0] / 1000}k · LOD1 {triL[1] / 1000}k · LOD2 {triL[2] / 1000}k)";
        }
        static bool HasAncestor(Transform t, string prefix) { for (; t; t = t.parent) if (t.name.StartsWith(prefix)) return true; return false; }
        static bool NameHas(Transform t, string part) { for (; t; t = t.parent) if (t.name.Contains(part)) return true; return false; }
        public static void OptimizeVJBatch()
        {
            int code = 0;
            try
            {
                var sc = EditorSceneManager.OpenScene($"{FBImport.SceneDir}/Map_vila_jardim.unity", OpenSceneMode.Single);
                var rep = FBOptimize.OptimizeScene(sc, false);
                StaticOcclusionCulling.Clear();
                // blocos novos (VJ10_Bloco_*): a mesma malha em 174 lugares -> UMA cópia com Mesh LOD (Unity 6, níveis automáticos pelo
                // tamanho na tela); fora do static batching (não combina com Mesh LOD)
                var lodOf = new System.Collections.Generic.Dictionary<Mesh, Mesh>(); int nb = 0;
                System.IO.Directory.CreateDirectory("Assets/FreeBala/Optimized/Map_vila_jardim");
                foreach (var f in System.IO.Directory.GetFiles("Assets/FreeBala/Optimized/Map_vila_jardim", "*_meshlod.asset")) AssetDatabase.DeleteAsset(f.Replace(System.IO.Path.DirectorySeparatorChar, '/'));   // (r41) mapa regerado: sem cache velho
                foreach (var root in sc.GetRootGameObjects())
                    foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (!mf.sharedMesh || !HasAncestor(mf.transform, "VJ10_Bloco") || !NameHas(mf.transform, "_LONGE")) continue;   // (r41) só a casca de longe: o modelo completo (só < 120 m) derrubava o MeshLod::BuildRedirect (crash 139)
                        var src = mf.sharedMesh;
                        if (!lodOf.TryGetValue(src, out var m))
                        {
                            var path = $"Assets/FreeBala/Optimized/Map_vila_jardim/{FBRegistry.Safe(src.name)}_meshlod.asset";
                            m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                            if (!m) { m = Object.Instantiate(src); m.name = src.name + "_lod"; MeshLodUtility.GenerateMeshLods(m, 4); AssetDatabase.CreateAsset(m, path); }
                            lodOf[src] = m;
                        }
                        mf.sharedMesh = m; nb++;
                        GameObjectUtility.SetStaticEditorFlags(mf.gameObject, GameObjectUtility.GetStaticEditorFlags(mf.gameObject) & ~StaticEditorFlags.BatchingStatic);
                    }
                rep += $" · Mesh LOD em {nb} blocos ({lodOf.Count} malha(s))";
                rep += " · " + MergeVegetation(sc);
                EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc); AssetDatabase.SaveAssets();
                Debug.Log($"[FreeBala][r40] Map_vila_jardim otimizado: {rep} · oclusão assada limpa");
            }
            catch (System.Exception e) { Debug.LogException(e); code = 1; }
            finally { EditorApplication.Exit(code); }
        }
    }
}
