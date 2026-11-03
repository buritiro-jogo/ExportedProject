// PEÇAS DA OFICINA POR CARRO, como o Rebaixados Elite Brasil 3.9.71 (pedido 20/10/2026: "garagens modificadoras exatamente como o apk REB,
// com peças corretas para cada veículo, som específico para cada tipo de veículo"). Dados: tools/reb/export_reb_parts.py ->
// assets/vehicles/reb_oficina.json (fb_data "rebOficina") + assets/vehicles/parts/<carro>.glb (registro Parts):
//  · BODYKIT (BodykitsCombiner do REB, 5 zonas: aerofólio, capô, frente, lateral, traseira): a peça de fábrica de cada zona já está na
//    carroceria LOD0 — ao trocar, os triângulos dela saem da malha (mesmas posições do nó bk_<zona>_base) e entra a variante bk_<zona>_<k>;
//  · FORROS DE PORTA (Carro/Portas.forros*: um modelo por filho), ENGATE (CarroConfig.engate), BANDEIRAS (pano + mastro na janela da
//    frente, atlas BandeirasCopa: Brasil, Rumo ao Hexa, Free Bala (célula repintada na r38), REB), FORRO DO PORTA-MALAS do som (Sons.forroPortaMala);
//  · de fábrica: ronco (Carro/Ronco.roncoInicial -> os 37 pacotes Realistic Engine Sound), buzina (componente Buzina), tração
//    (CarroConfig.numTracao: 0 FWD, 1 RWD, 2 4x4) e os 13 tamanhos de pneu (Carro/TamanhoRodas).
// Carros portados (Clio, Kadett, viaturas) usam os dados de fábrica do carro do REB mais parecido (ALIAS) e não têm peças próprias.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public static class FBReb
    {
        public static readonly string[] ZONAS = { "aerofolio", "capo", "frente", "lateral", "traseira" };
        public static readonly string[] ZONA_NOMES = { "AEROFÓLIO", "CAPÔ", "PARA-CHOQUE DIANTEIRO", "SAIAS LATERAIS", "PARA-CHOQUE TRASEIRO" };
        public static readonly string[] BANDEIRAS = { "BRASIL", "RUMO AO HEXA", "FREE BALA", "REB" };
        static readonly Vector2[] FLAG_OFS = { new(0, 0), new(.5f, 0), new(0, -.35f), new(.5f, -.35f) };   // células do atlas (2 colunas × 3 linhas)
        static readonly Dictionary<string, string> ALIAS = new() { ["Clio2003"] = "GolG2", ["Kadett"] = "Chevett", ["ViaturaPMAzul"] = "S10_2017", ["ViaturaBM"] = "S10_2017", ["ViaturaPM"] = "S10_2017" };
        static Dictionary<string, object> Carros => Json.O(GameData.RebOficina, "carros");
        public static Dictionary<string, object> Own(string id) => Json.O(Carros, id);
        public static Dictionary<string, object> Car(string id) => Own(id) ?? (id != null && ALIAS.TryGetValue(id, out var a) ? Own(a) : null);
        public static List<string> Roncos => (Json.L(GameData.RebOficina, "roncos") ?? new List<object>()).Select(o => o as string).Where(x => x != null && Json.O(Json.O(GameData.RES, "profiles"), x) != null).ToList();
        public static string RoncoNome(string k) => (k ?? "").Replace('_', ' ');
        public static string Ronco(string id) { var r = Json.S(Car(id), "ronco"); return r != null && Json.O(Json.O(GameData.RES, "profiles"), r) != null ? r : null; }
        public static string Buzina(string id) { var b = Json.S(Car(id), "buzina"); return b == null ? null : "reb_horn_" + (b.StartsWith("B_") ? b.Substring(2) : b).ToLowerInvariant().Replace('-', '_'); }
        public static string Tracao(string id) { var c = Car(id); if (c == null || !c.ContainsKey("tracao")) return null; int t = Json.I(c, "tracao"); return t == 1 ? "RWD" : t == 2 ? "4x4" : "FWD"; }
        public static float[] Pneus(string id) { var l = Json.L(Car(id), "pneus"); return l == null ? new[] { .8f, .85f, .9f, .95f, 1, 1.05f, 1.1f, 1.15f, 1.2f, 1.25f, 1.3f, 1.4f, 1.5f } : l.Select(o => Json.Fv(o)).ToArray(); }
        public static bool HasZone(FBCar car, string z) => Json.O(Json.O(Own(car.id), "bodykit"), z) != null && Parts(car);
        public static List<int> ZoneVariants(FBCar car, string z) => (Json.L(Json.O(Json.O(Own(car.id), "bodykit"), z), "var") ?? new List<object>()).Select(o => (int)Json.Fv(o)).ToList();
        public static int Forros(FBCar car, string side) => (Json.L(Json.O(Own(car.id), "forros"), side) ?? new List<object>()).Count;
        public static bool HasEngate(FBCar car) => Own(car.id)?.ContainsKey("engate") == true && Parts(car);
        public static bool HasBandeira(FBCar car) => Json.O(Own(car.id), "bandeiras") != null && Parts(car);
        public static bool HasBodykit(FBCar car) => ZONAS.Any(z => HasZone(car, z));

        // ---- peças instanciadas no carro (filho do shell: acompanha a suspensão a ar)
        class Rig { public Transform root; public readonly Dictionary<string, Transform> node = new(); public readonly Dictionary<MeshFilter, int[][]> orig = new(); public readonly Dictionary<string, HashSet<Vector3Int>> zoneKeys = new(); public Material flag; }
        static readonly Dictionary<FBCar, Rig> rigs = new();
        static bool Parts(FBCar car) => car && FBRegistry.I.Parts(car.id);
        static Rig RigOf(FBCar car)
        {
            if (rigs.TryGetValue(car, out var r) && r.root) return r;
            var src = FBRegistry.I.Parts(car.id); if (!src || !car.shell) return null;
            var go = Object.Instantiate(src, car.shell, false); go.name = "reb_parts"; r = new Rig { root = go.transform };
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) { t.gameObject.layer = FBWorld.LVehicle; if (t != go.transform && !r.node.ContainsKey(t.name)) r.node[t.name] = t; }
            FBVehicles.DressParts(go, car);
            FBPlates.Split(go.transform, FBPlates.Of(car.paint));                                // para-choque do kit traz a placa dele (sem isso: pedaço do atlas no lugar da placa)
            foreach (var mr in go.GetComponentsInChildren<Renderer>(true)) { mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
            foreach (var kv in r.node) if (kv.Key != "parts") kv.Value.gameObject.SetActive(false);   // nada aparece até a oficina ligar
            rigs[car] = r; return r;
        }
        static void Show(Rig r, string n, bool on) { if (r != null && r.node.TryGetValue(n, out var t) && t) t.gameObject.SetActive(on); }

        public static void Apply(FBCar car, FBTuneData T)
        {
            if (!car || car.ie || T == null) return;
            if (!Parts(car)) return;
            var r = RigOf(car); if (r == null) return;
            // bodykit: zona por zona
            T.bk ??= new int[0]; if (T.bk.Length < ZONAS.Length) { var b = Enumerable.Repeat(-1, ZONAS.Length).ToArray(); for (int i = 0; i < T.bk.Length; i++) b[i] = T.bk[i]; T.bk = b; }
            var cut = new List<string>();
            for (int zi = 0; zi < ZONAS.Length; zi++)
            {
                var z = ZONAS[zi]; var vs = ZoneVariants(car, z); int k = vs.Contains(T.bk[zi]) ? T.bk[zi] : -1;
                foreach (var v in vs) Show(r, $"bk_{z}_{v}", v == k);
                if (k >= 0) cut.Add(z);
            }
            CutBody(car, r, cut);
            // forros das portas (frente = L/R, trás = L02/R02): esconde o forro original do interior e mostra o modelo escolhido
            foreach (var (side, sel) in new[] { ("L", T.forroF), ("R", T.forroF), ("L02", T.forroT), ("R02", T.forroT) })
            {
                int n = Forros(car, side); if (n == 0) continue; int k = Mathf.Clamp(sel, 0, n - 1);
                for (int i = 0; i < n; i++) Show(r, $"forro_{side}_{i}", i == k);
                foreach (var t in car.shell.GetComponentsInChildren<Transform>(true)) if (t.name == "forro_" + side && !t.IsChildOf(r.root)) t.gameObject.SetActive(false);   // o original (pode estar preso na porta: FBDoors)
            }
            Show(r, "engate", T.engate && HasEngate(car));
            bool bL = T.bandL && HasBandeira(car), bR = T.bandR && HasBandeira(car);
            Show(r, "bandeira_L", bL); Show(r, "mastro_L", bL); Show(r, "bandeira_R", bR); Show(r, "mastro_R", bR);
            if (bL || bR) Flags(car, r, T.bandeira);
            bool som = T.somSet && T.som != null && T.som.Count > 0 && Json.O(Json.O(car.def, "som"), "bed") == null;
            Show(r, "forro_som", som && Own(car.id)?.ContainsKey("forroSom") == true);
            AddToLod0(car, r);
        }

        // triângulos da peça de fábrica de cada zona escolhida saem da carroceria LOD0 (vértices iguais aos do nó bk_<zona>_base, 2 mm)
        static Vector3Int Key(Vector3 p) => new(Mathf.RoundToInt(p.x * 500), Mathf.RoundToInt(p.y * 500), Mathf.RoundToInt(p.z * 500));
        static HashSet<Vector3Int> ZoneKeys(FBCar car, Rig r, string z)
        {
            if (r.zoneKeys.TryGetValue(z, out var ks)) return ks; ks = new HashSet<Vector3Int>(); r.zoneKeys[z] = ks;
            if (!r.node.TryGetValue($"bk_{z}_base", out var bt) || !bt) return ks;
            foreach (var mf in bt.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh || !mf.sharedMesh.isReadable) continue; var m = car.shell.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices) ks.Add(Key(m.MultiplyPoint3x4(v)));
            }
            return ks;
        }
        static IEnumerable<MeshFilter> BodyLod0(FBCar car)
        {
            foreach (Transform c in car.shell) if (c.name.StartsWith("body") && !c.name.Contains("lod")) foreach (var mf in c.GetComponentsInChildren<MeshFilter>(true)) yield return mf;
        }
        static void CutBody(FBCar car, Rig r, List<string> zones)
        {
            if (zones.Count == 0 && r.orig.Count == 0) return;
            var keys = new HashSet<Vector3Int>(); foreach (var z in zones) keys.UnionWith(ZoneKeys(car, r, z));
            foreach (var mf in BodyLod0(car))
            {
                var m = mf.sharedMesh; if (!m || !m.isReadable) continue;
                if (!r.orig.ContainsKey(mf))
                {
                    if (zones.Count == 0) continue;
                    if (!m.name.EndsWith("_kit")) { var cp = Object.Instantiate(m); cp.name = m.name + "_kit"; mf.sharedMesh = cp; m = cp; }
                    var o = new int[m.subMeshCount][]; for (int s = 0; s < o.Length; s++) o[s] = m.GetTriangles(s); r.orig[mf] = o;
                }
                var orig = r.orig[mf]; var M = car.shell.worldToLocalMatrix * mf.transform.localToWorldMatrix; var vs = m.vertices; var rest = FBCarDamage.RestOf(m); if (rest != null && rest.Length == vs.Length) vs = rest;
                var inZone = new bool[vs.Length]; if (keys.Count > 0) for (int i = 0; i < vs.Length; i++) inZone[i] = keys.Contains(Key(M.MultiplyPoint3x4(vs[i])));
                for (int s = 0; s < orig.Length && s < m.subMeshCount; s++)
                {
                    var t = orig[s]; if (keys.Count == 0) { m.SetTriangles(t, s, false); continue; }
                    var keep = new List<int>(t.Length);
                    for (int i = 0; i + 2 < t.Length; i += 3) if (!(inZone[t[i]] && inZone[t[i + 1]] && inZone[t[i + 2]])) { keep.Add(t[i]); keep.Add(t[i + 1]); keep.Add(t[i + 2]); }
                    m.SetTriangles(keep, s, false);
                }
                m.RecalculateBounds();
            }
        }
        // peças ativas cortadas junto com o LOD0 (somem longe com a carroceria detalhada)
        static void AddToLod0(FBCar car, Rig r)
        {
            var lg = car.shell.GetComponent<LODGroup>(); if (!lg) return; var lods = lg.GetLODs(); if (lods.Length == 0) return;
            var mine = r.root.GetComponentsInChildren<Renderer>(true);
            lods[0].renderers = lods[0].renderers.Where(x => x && !x.transform.IsChildOf(r.root)).Concat(mine).ToArray(); lg.SetLODs(lods);
        }
        // bandeira: atlas do REB (Resources/CarFX/reb_bandeiras) com a célula escolhida; o pano balança no mastro (mais rápido andando)
        static void Flags(FBCar car, Rig r, int idx)
        {
            if (!r.flag)
            {
                var tex = Resources.Load<Texture2D>("CarFX/reb_bandeiras"); if (!tex) return;
                r.flag = new Material(FBShaders.Lit) { name = "bandeira" }; r.flag.SetTexture("_BaseMap", tex); r.flag.SetFloat("_Smoothness", .15f); r.flag.SetFloat("_Cull", 0);
                var pole = new Material(FBShaders.Lit) { name = "mastro" }; pole.SetColor("_BaseColor", new Color(.12f, .12f, .13f)); pole.SetFloat("_Metallic", .6f); pole.SetFloat("_Smoothness", .6f);
                foreach (var side in new[] { "L", "R" })
                {
                    if (r.node.TryGetValue("bandeira_" + side, out var b) && b) foreach (var mr in b.GetComponentsInChildren<Renderer>(true)) mr.sharedMaterials = Enumerable.Repeat(r.flag, mr.sharedMaterials.Length).ToArray();
                    if (r.node.TryGetValue("mastro_" + side, out var m) && m) foreach (var mr in m.GetComponentsInChildren<Renderer>(true)) mr.sharedMaterials = Enumerable.Repeat(pole, mr.sharedMaterials.Length).ToArray();
                    var bp = Json.L(Json.O(Own(car.id), "bandeiras"), side);
                    if (b && bp != null && !b.GetComponent<FBRebFlag>()) { var f = b.gameObject.AddComponent<FBRebFlag>(); f.car = car; f.pivotLocal = new Vector3(-Json.Fv(bp[0]), Json.Fv(bp[1]), Json.Fv(bp[2])); }
                }
            }
            var o = FLAG_OFS[Mathf.Clamp(idx, 0, FLAG_OFS.Length - 1)]; r.flag.SetTextureOffset("_BaseMap", o); r.flag.mainTextureOffset = o;
        }
        public static void Forget(FBCar car) { rigs.Remove(car); }
    }

    // pano da bandeira balançando em torno do mastro (o REB usa Cloth; aqui um giro leve que cresce com a velocidade)
    public class FBRebFlag : MonoBehaviour
    {
        public FBCar car; public Vector3 pivotLocal; Transform pivot; float ph;
        void Start()
        {
            pivot = new GameObject("piv_bandeira").transform; pivot.SetParent(transform.parent, false); pivot.localPosition = pivotLocal; pivot.gameObject.layer = gameObject.layer;
            transform.SetParent(pivot, true); ph = Random.value * 10;
        }
        // [parceiro 01/11] medidor de tempo por sistema (FBTempo): com FBTempo.On soma o custo do Update de todas as bandeiras
        static readonly int __fbt_Update = FBTempo.Id("FBRebFlag.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            if (!pivot) return; float v = car ? Mathf.Abs(car.v) : 0; ph += Time.deltaTime * (2.5f + v * .25f);
            float a = Mathf.Sin(ph) * (4 + Mathf.Min(10, v * .4f)) + Mathf.Sin(ph * 2.3f) * 2;
            pivot.localRotation = Quaternion.Euler(0, a, 0);
        }
    }
}
