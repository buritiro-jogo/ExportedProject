// Troca de props dos mapas no import (chamado pelo FBImport, DEPOIS do FBMapLook e ANTES da NavMesh): hoje a Praça das Águas troca os
// food trucks antigos (PA_food_truck_*, caminhãozinho genérico do jogo web) pela Kombi food truck (Art/Props/FoodTruck, preparada pelo
// Tools~/prep_food_truck.py: cm → m, pivô no chão, LOD0 ~24k / LOD1 ~11k / LOD2 ~6k tris, paleta para as cores sólidas).
// O "carrinho de sorvete" pedido não existe com esse nome no mapa: os candidatos são os PA_food_truck_* (mesma classe de tamanho e vagas
// já reservadas no build do web) e os PA_carrinho_hotdog_* (2 m — a Kombi de 5,6 m bloquearia calçada); troque em SWAPS se for o outro.
// Cada troca: desliga o antigo (some colisão e obstáculo da NavMesh junto), põe a Kombi no mesmo ponto alinhada ao eixo comprido do antigo,
// com o balcão virado para o lado mais livre (raycast), no chão por raycast, afastando de parede se a caixa colidir; LODGroup, estática,
// camada do mapa, BoxCollider do corpo. O prefab (URP/Lit, mesmo shader do mapa = nenhuma variante nova) é refeito a cada import.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBMapProps
    {
        public const string Dir = "Assets/FreeBala/Art/Props/FoodTruck", Gltf = Dir + "/food_truck.gltf", PrefabPath = Dir + "/FoodTruck.prefab", MatDir = Dir + "/Materiais";
        /// <summary>(mapa, prefixo do nó antigo, prefab novo). Alternativa: ("praca_aguas", "PA_carrinho_hotdog_", ...).</summary>
        public static readonly (string map, string oldPrefix, string prefab)[] SWAPS = { ("praca_aguas", "PA_food_truck_", PrefabPath) };
        public const string RootName = "FB_PropsTrocados";

        public static void Apply(string mapId, GameObject mapRoot)
        {
            var swaps = SWAPS.Where(s => s.map == mapId).ToList(); if (swaps.Count == 0 || !mapRoot) return;
            var old = GameObject.Find(RootName); if (old) Object.DestroyImmediate(old);
            var root = new GameObject(RootName) { layer = FBProjectSetup.LMap }; root.isStatic = true;
            foreach (var sw in swaps)
            {
                var prefab = sw.prefab == PrefabPath ? FoodTruckPrefab() : AssetDatabase.LoadAssetAtPath<GameObject>(sw.prefab);
                if (!prefab) { Debug.LogWarning($"[FreeBala] props {mapId}: prefab {sw.prefab} não existe"); continue; }
                var box = prefab.GetComponent<BoxCollider>(); int lado = Meta().balcao;
                var olds = mapRoot.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith(sw.oldPrefix)).ToList();
                foreach (var t in olds) t.gameObject.SetActive(false);                        // antes dos raycasts: o antigo não atrapalha
                Physics.SyncTransforms(); int n = 0;
                foreach (var t in olds)
                {
                    // eixo comprido do antigo (malha local) → eixo da Kombi; balcão para o lado com mais espaço livre
                    var mf = t.GetComponentInChildren<MeshFilter>(true); var sz = mf && mf.sharedMesh ? Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale) : Vector3.one;
                    var axis = (sz.x >= sz.z ? t.right : t.forward); axis.y = 0; axis = axis.sqrMagnitude > 1e-4f ? axis.normalized : Vector3.forward;
                    var pos0 = t.position; var side = Vector3.Cross(Vector3.up, axis);             // perpendicular ao eixo
                    // espaço livre de cada lado, ignorando a mobília solta (mesas/churrasqueira em volta do truck antigo vão ser arrumadas)
                    float Free(Vector3 from, Vector3 d) { foreach (var h in Physics.RaycastAll(from + Vector3.up * 1.1f, d, 30, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance)) if (!Movable(h.collider)) return h.distance; return 30; }
                    var counterDir = Free(pos0, side) >= Free(pos0, -side) ? side : -side;
                    // o balcão fica no −X local (lado = −1) ou +X local: escolhe o forward que põe o balcão em counterDir
                    var fwd = Vector3.Cross(counterDir * -lado, Vector3.up); var rot = Quaternion.LookRotation(fwd, Vector3.up);
                    // ENCAIXE dentro da vaga: desliza até ±1,2 m no eixo e até 0,6 m para o lado do balcão (nunca para o lado da rua/calçada
                    // de trás), menor deslocamento primeiro; só obstáculo FIXO impede (poste, árvore, banco, muro…)
                    var pos = Ground(pos0); List<Collider> fixedHits = null, bestFixed = null; var best = pos;
                    if (box)
                    {
                        var cands = new List<Vector3>();
                        foreach (var da in new[] { 0f, .3f, -.3f, .6f, -.6f, .9f, -.9f, 1.2f, -1.2f }) foreach (var ds in new[] { 0f, .3f, .6f }) cands.Add(axis * da + counterDir * ds);
                        foreach (var d in cands.OrderBy(d => d.magnitude))
                        {
                            var p = Ground(pos0 + d); fixedHits = Hits(p, rot, box).Where(c => !Movable(c)).ToList();
                            if (fixedHits.Count == 0) { best = p; bestFixed = fixedHits; break; }
                            if (bestFixed == null || fixedHits.Count < bestFixed.Count) { best = p; bestFixed = fixedHits; }
                        }
                        pos = best;
                    }
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform); go.name = "FoodTruck_" + t.name.Substring(sw.oldPrefix.Length);
                    go.transform.SetPositionAndRotation(pos, rot); n++; Physics.SyncTransforms();
                    // mobília solta encostando na Kombi: vai para a frente do balcão (é onde as mesas fazem sentido), no chão; sem espaço, some
                    var moved = new List<string>();
                    if (box) foreach (var c in Hits(pos, rot, box).Where(Movable).Select(PropRoot).Distinct().ToList())
                    {
                        if (!c) continue; var ok = false; var start = c.position;
                        for (float k = 1; k <= 6 && !ok; k += .5f)
                        {
                            var q = Ground(pos + counterDir * (box.size.x * .5f + k) + axis * Vector3.Dot(start - pos, axis) * .6f); c.position = q; Physics.SyncTransforms();
                            ok = !Hits(pos, rot, box).Any(h => h.transform.IsChildOf(c)) && !PropBlocked(c);
                        }
                        if (!ok) { c.position = start; c.gameObject.SetActive(false); }
                        moved.Add(c.name + (ok ? $" → {c.position}" : " (desligado: sem espaço)")); Physics.SyncTransforms();
                    }
                    var still = box ? Hits(pos, rot, box).Where(c => !Movable(c)).Select(c => PropRoot(c).name).Distinct().ToList() : new List<string>();
                    Debug.Log($"[FreeBala] props {mapId}: {t.name} → {go.name} em {pos} yaw {rot.eulerAngles.y:0} (deslocado {(pos - Ground(pos0)).magnitude:0.00} m da vaga; balcão para {counterDir}, livre {Free(pos, counterDir):0.0} m)"
                        + (moved.Count > 0 ? " · mobília arrumada: " + string.Join(", ", moved) : "") + (still.Count > 0 ? " — AINDA ENCOSTA EM: " + string.Join(", ", still) : ""));
                }
                Debug.Log($"[FreeBala] props {mapId}: {n} × {prefab.name} no lugar de {sw.oldPrefix}*");
            }
            // SEM static batching: com LODGroup ele juntaria LOD0+LOD2 (mesmo material de paleta) num mesh combinado e duplicaria ~24k tris por
            // Kombi na memória; o SRP Batcher já agrupa as draw calls. Ficam oclusão, sondas e NavMesh.
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) { t.gameObject.layer = FBProjectSetup.LMap; GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.NavigationStatic); }
            Physics.SyncTransforms();
        }
        // colisores dentro da caixa do corpo (sobe 15 cm: chão e meio-fio não contam); as Kombis já postas não contam
        static List<Collider> Hits(Vector3 pos, Quaternion rot, BoxCollider b)
        {
            var c = pos + rot * b.center + Vector3.up * .15f; var half = b.size * .5f - new Vector3(.05f, .2f, .05f);
            var mine = GameObject.Find(RootName);
            return Physics.OverlapBox(c, half, rot, ~0, QueryTriggerInteraction.Ignore).Where(h => !(mine && h.transform.IsChildOf(mine.transform))).ToList();
        }
        // mobília solta que o build do web espalha em volta dos food trucks (pode ser arrumada); o resto é fixo
        static readonly System.Text.RegularExpressions.Regex MovableRx = new(@"^PA_(mesas_|churrasqueira|sacos_lixo|tambor)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        static Transform PropRoot(Collider c) { var t = c ? c.transform : null; while (t && !MovableRx.IsMatch(t.name) && !t.name.StartsWith("PA_") && t.parent) t = t.parent; return t; }
        static bool Movable(Collider c) { var t = PropRoot(c); return t && MovableRx.IsMatch(t.name); }
        static bool PropBlocked(Transform prop)
        {
            foreach (var col in prop.GetComponentsInChildren<Collider>())
            {
                var b = col.bounds; var hs = Physics.OverlapBox(b.center + Vector3.up * .1f, b.extents - new Vector3(.03f, .12f, .03f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                if (hs.Any(h => !h.transform.IsChildOf(prop))) return true;
            }
            return false;
        }
        static Vector3 Ground(Vector3 p)
        {
            foreach (var h in Physics.RaycastAll(p + Vector3.up * 4, Vector3.down, 12, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            { var mine = GameObject.Find(RootName); if (Movable(h.collider) || mine && h.transform.IsChildOf(mine.transform)) continue; p.y = h.point.y; break; }
            return p;
        }

        // ---- metadados do preparo (food_truck.json)
        struct FTMeta { public Vector3 colC, colS; public int balcao; }
        static FTMeta Meta()
        {
            var m = new FTMeta { colC = new Vector3(0, 1.05f, 0), colS = new Vector3(2.5f, 2.1f, 5.6f), balcao = -1 };
            var p = Dir + "/food_truck.json"; if (!File.Exists(p)) return m;
            var o = Json.Parse(File.ReadAllText(p)); var col = Json.O(o, "colisor");
            Vector3 V(List<object> l) => l != null && l.Count >= 3 ? new Vector3(Json.Fv(l[0]), Json.Fv(l[1]), Json.Fv(l[2])) : Vector3.zero;
            if (col != null) { m.colC = V(Json.L(col, "centro")); m.colS = V(Json.L(col, "tamanho")); }
            m.balcao = Json.I(o, "balcao_lado_x", -1); return m;
        }

        // ---- prefab da Kombi: materiais URP/Lit, LODGroup, colisor, sombras
        public static GameObject FoodTruckPrefab()
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Gltf); if (!src) { Debug.LogWarning("[FreeBala] food truck: " + Gltf + " não importado"); return null; }
            PaletteImport();
            Directory.CreateDirectory(MatDir);
            var inst = (GameObject)Object.Instantiate(src); inst.name = "FoodTruck";
            var conv = new Dictionary<Material, Material>();
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) if (ms[i]) { if (!conv.TryGetValue(ms[i], out var c)) conv[ms[i]] = c = Convert(ms[i]); ms[i] = c; }
                r.sharedMaterials = ms; r.receiveShadows = true;
                bool glass = ms.All(m => m && m.renderQueue >= 3000);
                r.shadowCastingMode = glass ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            }
            // LODGroup pelos nomes FoodTruck_LOD0/1/2 (alturas na tela: 30 % / 10 % / 2,5 %, some abaixo de 1 %)
            var lods = new List<LOD>(); float[] h = { .3f, .1f, .025f };
            for (int k = 0; k < 3; k++)
            {
                var rs = inst.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("FoodTruck_LOD" + k) || r.transform.parent && r.transform.parent.name.StartsWith("FoodTruck_LOD" + k)).ToArray();
                if (rs.Length > 0) lods.Add(new LOD(h[Mathf.Min(k, 2)], rs));
            }
            if (lods.Count > 0) { var lg = inst.AddComponent<LODGroup>(); var arr = lods.ToArray(); arr[^1].screenRelativeTransitionHeight = .01f; lg.SetLODs(arr); lg.RecalculateBounds(); }
            var meta = Meta(); var bc = inst.AddComponent<BoxCollider>(); bc.center = meta.colC; bc.size = meta.colS;
            foreach (var t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = FBProjectSetup.LMap;
            var pf = PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath); Object.DestroyImmediate(inst); return pf;
        }
        // paleta de cores sólidas: sem mipmap e com filtro ponto (as células de 8 px não podem se misturar)
        static void PaletteImport()
        {
            var p = Dir + "/food_truck_tex/ft_paleta.png"; var ti = AssetImporter.GetAtPath(p) as TextureImporter; if (!ti) return;
            if (ti.mipmapEnabled || ti.filterMode != FilterMode.Point || ti.textureCompression != TextureImporterCompression.Uncompressed)
            { ti.mipmapEnabled = false; ti.filterMode = FilterMode.Point; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.SaveAndReimport(); }
        }
        // glTF (glTFast) → URP/Lit, salvo como asset (nome igual: ft_paleta, ft_vidro, ft_led_*, ft_<original>)
        static Material Convert(Material m)
        {
            var path = $"{MatDir}/{Safe(m.name)}.mat"; var o = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool fresh = !o; if (fresh) o = new Material(FBShaders.Lit) { name = m.name }; else o.shader = FBShaders.Lit;
            var bt = m.HasProperty("baseColorTexture") ? m.GetTexture("baseColorTexture") : null;
            o.SetTexture("_BaseMap", bt); if (bt) { o.SetTextureScale("_BaseMap", m.GetTextureScale("baseColorTexture")); o.SetTextureOffset("_BaseMap", m.GetTextureOffset("baseColorTexture")); }
            var col = m.HasProperty("baseColorFactor") ? m.GetColor("baseColorFactor") : Color.white; o.SetColor("_BaseColor", col);
            float metal = m.HasProperty("metallicFactor") ? m.GetFloat("metallicFactor") : 0, rough = m.HasProperty("roughnessFactor") ? m.GetFloat("roughnessFactor") : .6f;
            o.SetFloat("_WorkflowMode", 1); o.SetFloat("_Metallic", metal); o.SetFloat("_Smoothness", Mathf.Clamp(1 - rough, .05f, .85f));
            var ec = m.HasProperty("emissiveFactor") ? m.GetColor("emissiveFactor") : Color.black;
            if (m.name.StartsWith("ft_led") || ec.maxColorComponent > .002f)
            { o.SetColor("_EmissionColor", (ec.maxColorComponent > .002f ? ec : col) * 2.2f); o.EnableKeyword("_EMISSION"); o.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
            else { o.DisableKeyword("_EMISSION"); o.SetColor("_EmissionColor", Color.black); }
            bool transp = m.renderQueue >= 3000 || col.a < .99f || m.name.StartsWith("ft_vidro");
            if (transp) { FBShaders.TransparentInPlace(o); o.SetFloat("_Smoothness", .9f); }
            else { o.SetOverrideTag("RenderType", "Opaque"); o.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry; o.SetFloat("_Surface", 0); }
            o.SetFloat("_Cull", 0);                                                    // painéis finos da lataria/toldo: as duas faces
            if (fresh) AssetDatabase.CreateAsset(o, path); else EditorUtility.SetDirty(o);
            return o;
        }
        static string Safe(string n) { foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_'); return n.Replace(" (Instance)", ""); }

        [MenuItem("FreeBala/Props/Refazer prefab do food truck")]
        static void MenuPrefab() { var p = FoodTruckPrefab(); AssetDatabase.SaveAssets(); Debug.Log("[FreeBala] food truck: " + (p ? PrefabPath : "falhou")); }
    }
}
