// PORTAS, CAPÔ E PORTA-MALAS (engenharia reversa do REB, 20/10/2026 — tools/reb/export_doors.py).
// No REB a carroceria é uma malha esquelética: portaL/portaR/portaL02/portaR02/mala/capo são ossos do Avatar e o Animator do carro tem
// os clipes BaseCarro|AbrirTudo / AbrirCapo / MalaAbrir. O exportador grava, por carro, as posições (pose fechada) dos vértices de cada
// peça e o movimento de abrir tirado da pose final do clipe: eixo, ângulo e pivô (dobradiça) no espaço do carro
// (Resources/FBDoors/<carro>.json). Em jogo nada muda até alguém abrir: aí a malha LOD0 do carro é separada (cópia própria) — os
// triângulos de cada peça viram um objeto filho girando na dobradiça real; enquanto houver peça aberta o carro fica no LOD0.
//   Teclas: U portas · J capô · P porta-malas (dentro do carro ou a pé a até 3,5 m dele); garagem do rolê: botões.
//   Rede: máscara das peças no estado do veículo (FBDrive.VehicleState, índice 12).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
    public static class FBDoors
    {
        public static readonly string[] NAMES = { "portaL", "portaR", "portaL02", "portaR02", "mala", "capo" };
        public const int DOORS = 1 | 2 | 4 | 8, TRUNK = 16, HOOD = 32;
        class PartDef { public Vector3 axis, pivot, t; public float ang; public HashSet<long> keys; }
        class CarDef { public PartDef[] p = new PartDef[6]; public HashSet<long> shared = new(); }
        class Rig
        {
            public FBCar car; public bool split, failed; public readonly Transform[] piv = new Transform[6]; public readonly float[] k = new float[6]; public int target;
            public readonly List<Renderer> extra = new(); public LODGroup lg; public bool forced; public readonly float[] bent = new float[6]; public readonly bool[] gone = new bool[6]; public float detachV; public int detachPart = -1; public Vector3 detachVel;
        }
        static readonly Dictionary<string, CarDef> defs = new(); static readonly HashSet<string> missing = new(); static readonly Dictionary<FBCar, Rig> rigs = new();
        static Runner runner;

        static long Pack(int x, int y, int z) => ((long)(x & 0x1FFFF) << 34) | ((long)(y & 0x1FFFF) << 17) | (long)(z & 0x1FFFF);
        static long Key(Vector3 v) => Pack(Mathf.RoundToInt(v.x * 1000), Mathf.RoundToInt(v.y * 1000), Mathf.RoundToInt(v.z * 1000));
        static void AddDilated(HashSet<long> s, int x, int y, int z)
        {
            s.Add(Pack(x, y, z)); s.Add(Pack(x + 1, y, z)); s.Add(Pack(x - 1, y, z)); s.Add(Pack(x, y + 1, z)); s.Add(Pack(x, y - 1, z)); s.Add(Pack(x, y, z + 1)); s.Add(Pack(x, y, z - 1));
        }
        static void Decode(string b64, HashSet<long> into)
        {
            if (string.IsNullOrEmpty(b64)) return; var b = System.Convert.FromBase64String(b64);
            for (int i = 0; i + 5 < b.Length; i += 6) AddDilated(into, (short)(b[i] | b[i + 1] << 8), (short)(b[i + 2] | b[i + 3] << 8), (short)(b[i + 4] | b[i + 5] << 8));
        }
        static CarDef Def(string id)
        {
            if (string.IsNullOrEmpty(id) || missing.Contains(id)) return null; if (defs.TryGetValue(id, out var d)) return d;
            var ta = Resources.Load<TextAsset>("FBDoors/" + id); if (!ta) { missing.Add(id); return null; }
            var J = Json.Parse(ta.text) as Dictionary<string, object>; var P = Json.O(J, "parts"); d = new CarDef();
            if (P == null) { missing.Add(id); return null; }
            for (int i = 0; i < NAMES.Length; i++)
            {
                var o = Json.O(P, NAMES[i]); if (o == null) continue;
                Vector3 V(string k) { var l = Json.L(o, k); return l != null && l.Count >= 3 ? new Vector3(Json.Fv(l[0]), Json.Fv(l[1]), Json.Fv(l[2])) : Vector3.zero; }
                var pd = new PartDef { axis = V("axis").normalized, pivot = V("pivot"), t = V("t"), ang = Json.F(o, "ang"), keys = new HashSet<long>() };
                Decode(Json.S(o, "p"), pd.keys); if (pd.keys.Count > 0 && pd.axis.sqrMagnitude > .5f) d.p[i] = pd;
            }
            Decode(Json.S(J, "shared"), d.shared); defs[id] = d; return d;
        }

        public static bool Has(FBCar car) => car && Def(car.id) != null;
        public static bool HasPart(FBCar car, int bits) { var d = car ? Def(car.id) : null; if (d == null) return false; for (int i = 0; i < 6; i++) if ((bits & (1 << i)) != 0 && d.p[i] != null) return true; return false; }
        public static int Mask(FBCar car) => car && rigs.TryGetValue(car, out var r) ? r.target : 0;
        // abre/fecha um grupo (portas = as laterais juntas, como o "Abrir tudo" do REB; capô; porta-malas)
        public static void Toggle(FBCar car, int bits)
        {
            if (!HasPart(car, bits)) return; int cur = Mask(car); bool open = (cur & bits) == 0;
            SetMask(car, open ? cur | bits : cur & ~bits, true);
        }
        public static void SetMask(FBCar car, int mask, bool sound = false)
        {
            if (!car) return; var d = Def(car.id); if (d == null) return;
            if (!rigs.TryGetValue(car, out var r)) { if (mask == 0) return; rigs[car] = r = new Rig { car = car }; }
            int was = r.target; r.target = mask; Ensure();
            if (sound && was != mask)
            {
                bool opening = (mask & ~was) != 0; var pos = car.transform.position;
                if ((((mask ^ was) & DOORS) != 0)) FBAudio.PlayAt(opening ? "veh_door_open" : "veh_door_close", pos, .55f, 6);
                if ((((mask ^ was) & (TRUNK | HOOD)) != 0)) FBAudio.PlayAt(opening ? "veh_door_open" : "veh_door_close", pos + car.transform.forward * (((mask ^ was) & HOOD) != 0 ? 1.8f : -1.8f), .45f, 6);
            }
        }
        static void Ensure() { if (!runner) { var g = new GameObject("FB_Portas"); Object.DontDestroyOnLoad(g); runner = g.AddComponent<Runner>(); } }

        // ---- separa a malha LOD0 em carroceria + peças (uma vez por carro, só quando alguém abre)
        static bool Split(Rig r)
        {
            var car = r.car; var d = Def(car.id); if (d == null || !car.shell) return false;
            Transform lod0 = null; foreach (Transform c in car.shell) { var n = c.name; if (n.StartsWith("body") && !n.StartsWith("body_lod")) { lod0 = c; break; } }
            if (!lod0) return false; int made = 0;
            foreach (var mf in lod0.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>(); var m = mf.sharedMesh; if (!mr || !m || !m.isReadable) continue;
                var V = FBCarDamage.RestOf(m) ?? m.vertices; var cls = new int[V.Length];                // amassada: classifica pela forma original var M = car.shell.worldToLocalMatrix * mf.transform.localToWorldMatrix;   // espaço do carro (o mesmo do REB)
                var toLod = lod0.worldToLocalMatrix * mf.transform.localToWorldMatrix; bool same = toLod.isIdentity;
                for (int i = 0; i < V.Length; i++)
                {
                    var p = same ? V[i] : (Vector3)(toLod.MultiplyPoint3x4(V[i])); long k = Key(p); int c = -1;
                    for (int j = 0; j < 6; j++) if (d.p[j] != null && d.p[j].keys.Contains(k)) { c = j; break; }
                    if (c >= 0 && d.shared.Contains(k)) c = -2;                                       // costura (posição dentro e fora): neutra
                    cls[i] = c;
                }
                int sub = m.subMeshCount; var bodyTris = new List<int>[sub]; var partTris = new List<int>[6, sub]; bool any = false;
                for (int s = 0; s < sub; s++)
                {
                    bodyTris[s] = new List<int>(); var T = m.GetTriangles(s);
                    for (int t = 0; t + 2 < T.Length; t += 3)
                    {
                        int a = cls[T[t]], b = cls[T[t + 1]], c = cls[T[t + 2]]; int part = -1;
                        if (a != -1 && b != -1 && c != -1)
                        {
                            int best = -1, bn = 0; for (int j = 0; j < 6; j++) { int n = (a == j ? 1 : 0) + (b == j ? 1 : 0) + (c == j ? 1 : 0); if (n > bn) { bn = n; best = j; } }
                            part = best;
                        }
                        if (part < 0) { bodyTris[s].Add(T[t]); bodyTris[s].Add(T[t + 1]); bodyTris[s].Add(T[t + 2]); continue; }
                        (partTris[part, s] ??= new List<int>()).AddRange(new[] { T[t], T[t + 1], T[t + 2] }); any = true;
                    }
                }
                if (!any) continue;
                var bm = Object.Instantiate(m); bm.name = m.name + "_portas"; for (int s = 0; s < sub; s++) bm.SetTriangles(bodyTris[s], s, false); bm.RecalculateBounds(); mf.sharedMesh = bm;
                for (int j = 0; j < 6; j++)
                {
                    bool has = false; for (int s = 0; s < sub; s++) has |= partTris[j, s] != null && partTris[j, s].Count > 0; if (!has) continue;
                    var pd = d.p[j]; var pm = Compact(m, j, partTris, sub);
                    if (!r.piv[j]) { var pg = new GameObject("peca_" + NAMES[j]).transform; pg.SetParent(lod0, false); pg.localPosition = pd.pivot; r.piv[j] = pg; AttachLining(car, j, pg); }
                    var go = new GameObject("malha"); go.layer = mf.gameObject.layer; go.transform.SetParent(r.piv[j], false);
                    go.transform.localPosition = -pd.pivot; if (!same) { go.transform.localPosition = -pd.pivot; }
                    // malha no espaço do lod0: vértices já convertidos no Compact quando o filtro não é o próprio lod0
                    go.AddComponent<MeshFilter>().sharedMesh = same ? pm : Transformed(pm, toLod);
                    var pr = go.AddComponent<MeshRenderer>(); pr.sharedMaterials = mr.sharedMaterials; pr.shadowCastingMode = mr.shadowCastingMode; pr.receiveShadows = mr.receiveShadows;
                    pr.lightProbeUsage = mr.lightProbeUsage; pr.reflectionProbeUsage = mr.reflectionProbeUsage; pr.motionVectorGenerationMode = mr.motionVectorGenerationMode;
                    var mpb = new MaterialPropertyBlock(); if (mr.HasPropertyBlock()) { mr.GetPropertyBlock(mpb); pr.SetPropertyBlock(mpb); }
                    r.extra.Add(pr); made++;
                }
            }
            if (made == 0) return false;
            // as peças entram no LOD0 do grupo (somem junto com ele de longe)
            r.lg = car.shell.GetComponent<LODGroup>();
            if (r.lg) { var L = r.lg.GetLODs(); if (L.Length > 0) { var l0 = new List<Renderer>(L[0].renderers); l0.AddRange(r.extra); L[0].renderers = l0.ToArray(); r.lg.SetLODs(L); } }
            return true;
        }
        // forro da porta (interior do REB: forro_L/R/L02/R02, e os modelos da oficina forro_<lado>_<k>) vai junto com a porta — antes ficava
        // parado no lugar e a porta aberta mostrava só o avesso da chapa ("parte de dentro da porta sem textura")
        static void AttachLining(FBCar car, int j, Transform piv)
        {
            if (j > 3 || !car.shell) return; var side = NAMES[j].Substring(5);                      // portaL -> L, portaR02 -> R02
            var list = new List<Transform>(); var intr = car.shell.Find("interior"); if (intr) { var f = intr.Find("forro_" + side); if (f) list.Add(f); }
            var parts = car.shell.Find("reb_parts"); if (parts) foreach (var t in parts.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("forro_" + side + "_")) list.Add(t);
            foreach (var t in list) t.SetParent(piv, true);
        }
        static Mesh Compact(Mesh m, int j, List<int>[,] partTris, int sub)
        {
            var map = new Dictionary<int, int>(); var idx = new List<int>[sub];
            for (int s = 0; s < sub; s++) { idx[s] = new List<int>(); var L = partTris[j, s]; if (L == null) continue; foreach (var v in L) { if (!map.TryGetValue(v, out var n)) map[v] = n = map.Count; idx[s].Add(n); } }
            var order = new int[map.Count]; foreach (var kv in map) order[kv.Value] = kv.Key;
            var o = new Mesh { name = m.name + "_" + NAMES[j] }; if (order.Length > 65000) o.indexFormat = IndexFormat.UInt32;
            var V = m.vertices; var nv = new Vector3[order.Length]; for (int i = 0; i < order.Length; i++) nv[i] = V[order[i]]; o.vertices = nv;
            if (m.HasVertexAttribute(VertexAttribute.Normal)) { var N = m.normals; var nn = new Vector3[order.Length]; for (int i = 0; i < order.Length; i++) nn[i] = N[order[i]]; o.normals = nn; }
            if (m.HasVertexAttribute(VertexAttribute.Tangent)) { var Tg = m.tangents; var nt = new Vector4[order.Length]; for (int i = 0; i < order.Length; i++) nt[i] = Tg[order[i]]; o.tangents = nt; }
            if (m.HasVertexAttribute(VertexAttribute.Color)) { var C = m.colors32; var nc = new Color32[order.Length]; for (int i = 0; i < order.Length; i++) nc[i] = C[order[i]]; o.colors32 = nc; }
            for (int ch = 0; ch < 4; ch++)
            {
                if (!m.HasVertexAttribute((VertexAttribute)((int)VertexAttribute.TexCoord0 + ch))) continue;
                var U = new List<Vector4>(); m.GetUVs(ch, U); var nu = new List<Vector4>(order.Length); for (int i = 0; i < order.Length; i++) nu.Add(U[order[i]]); o.SetUVs(ch, nu);
            }
            o.subMeshCount = sub; for (int s = 0; s < sub; s++) o.SetTriangles(idx[s], s, false); o.RecalculateBounds(); return o;
        }
        static Mesh Transformed(Mesh pm, Matrix4x4 M)
        {
            var V = pm.vertices; for (int i = 0; i < V.Length; i++) V[i] = M.MultiplyPoint3x4(V[i]); pm.vertices = V;
            if (pm.HasVertexAttribute(VertexAttribute.Normal)) { var N = pm.normals; for (int i = 0; i < N.Length; i++) N[i] = M.MultiplyVector(N[i]).normalized; pm.normals = N; }
            pm.RecalculateBounds(); return pm;
        }

        // ---- animação (0,7 s portas, 0,9 s capô/mala; saída suave)
        static float Ease(float x) => x * x * (3 - 2 * x);
        static void Tick(float dt)
        {
            List<FBCar> dead = null;
            foreach (var kv in rigs)
            {
                var r = kv.Value; if (!kv.Key) { (dead ??= new List<FBCar>()).Add(kv.Key); continue; }
                bool moving = false, anyOpen = false;
                for (int j = 0; j < 6; j++) { float tg = (r.target & (1 << j)) != 0 ? 1 : 0; if (r.k[j] != tg) moving = true; if (r.k[j] > 0 || tg > 0 || r.bent[j] > 0 || r.gone[j]) anyOpen = true; }
                if (r.detachPart >= 0) anyOpen = true;
                if (!moving && !anyOpen) { if (r.forced && r.lg) { r.lg.ForceLOD(-1); r.forced = false; } continue; }
                if (!r.split && !r.failed) { r.split = Split(r); r.failed = !r.split; }
                if (!r.split) continue;
                if (r.detachPart >= 0 && r.piv[r.detachPart]) { int dp = r.detachPart; r.detachPart = -1; Detach(r.car, dp, r.detachVel); }
                if (r.lg && anyOpen && !r.forced) { r.lg.ForceLOD(0); r.forced = true; }
                var d = Def(r.car.id);
                for (int j = 0; j < 6; j++)
                {
                    if (!r.piv[j] || d.p[j] == null) continue; float tg = (r.target & (1 << j)) != 0 ? 1 : 0;
                    float dur = j >= 4 ? .9f : .7f; r.k[j] = Mathf.MoveTowards(r.k[j], tg, dt / dur); float e = Mathf.Max(Ease(r.k[j]), r.bent[j]); var pd = d.p[j];
                    r.piv[j].localRotation = Quaternion.AngleAxis(pd.ang * e, pd.axis); r.piv[j].localPosition = pd.pivot + pd.t * e;
                }
            }
            if (dead != null) foreach (var c in dead) rigs.Remove(c);
        }

        // ---- teclas: dentro do carro (FBDrive.PlayerInput) ou a pé perto de um carro
        public static void Keys(FBCar car)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current; if (kb == null || !car) return;
            if (kb.uKey.wasPressedThisFrame) { if (HasPart(car, DOORS)) Toggle(car, DOORS); else FBHUD.I?.Center("ESTE VEÍCULO NÃO ABRE AS PORTAS", 1.2f); }
            if (kb.jKey.wasPressedThisFrame && HasPart(car, HOOD)) Toggle(car, HOOD);
            if (kb.pKey.wasPressedThisFrame && HasPart(car, TRUNK)) Toggle(car, TRUNK);
        }
        static FBCar NearCar(Vector3 p, float maxD)
        {
            FBCar best = null; float bd = maxD;
            foreach (var c in FBVehicles.list)
            {
                if (!c || !Has(c) || (c.driver && !c.driver.isPlayer) || c.remoteDriver >= 0) continue;
                var L = c.lbox; var lp = c.transform.InverseTransformPoint(p); var q = new Vector3(Mathf.Clamp(lp.x, L.min.x, L.max.x), Mathf.Clamp(lp.y, L.min.y, L.max.y), Mathf.Clamp(lp.z, L.min.z, L.max.z));
                float dd = Vector3.Distance(c.transform.TransformPoint(q), p); if (dd < bd) { bd = dd; best = c; }
            }
            return best;
        }
        public static void Reset() { rigs.Clear(); }
        public static string DebugInfo(FBCar car)
        {
            if (!car) return "sem carro"; var d = Def(car.id); if (d == null) return "sem dados de portas";
            if (!rigs.TryGetValue(car, out var r)) return $"{d.p.Count(x => x != null)} peças no arquivo · não separado";
            int piv = r.piv.Count(x => x); var ang = string.Join(" ", Enumerable.Range(0, 6).Where(j => r.piv[j]).Select(j => $"{NAMES[j]}={Quaternion.Angle(Quaternion.identity, r.piv[j].localRotation):0}°"));
            return $"{d.p.Count(x => x != null)} peças · separado {r.split} (falhou {r.failed}) · {piv} dobradiças · {r.extra.Count} malhas · máscara {r.target} · empeno capô {r.bent[5]:0.00} · ângulos {ang}";
        }
        // batida: a peça empena e fica entreaberta (fração do curso) até a oficina
        public static void Bend(FBCar car, int part, float k)
        {
            var d = car ? Def(car.id) : null; if (d == null || part < 0 || part > 5 || d.p[part] == null) return;
            if (!rigs.TryGetValue(car, out var r)) rigs[car] = r = new Rig { car = car };
            r.bent[part] = Mathf.Clamp01(Mathf.Max(r.bent[part], k)); Ensure();
        }
        public static void Unbend(FBCar car)
        {
            if (!car || !rigs.TryGetValue(car, out var r)) return;
            for (int j = 0; j < 6; j++) { r.bent[j] = 0; if (r.gone[j] && r.piv[j]) foreach (var x in r.piv[j].GetComponentsInChildren<Renderer>(true)) x.enabled = true; r.gone[j] = false; }   // oficina: peça nova no lugar
        }
        public static float BentOf(FBCar car, int part) => car && rigs.TryGetValue(car, out var r) && part >= 0 && part < 6 ? r.bent[part] : 0;
        public static int GoneCount(FBCar car) => car && rigs.TryGetValue(car, out var r) ? r.gone.Count(g => g) : 0;
        // r45: batida grave numa peça já empenada — a peça SOLTA: uma cópia vira corpo físico (caixa do tamanho da peça, massa de chapa) com o
        // impulso da batida e some fora de vista (FBFisica.Debris); a original fica escondida até a oficina (Unbend). Peça ainda não separada
        // (o Split roda no Tick): pede e solta no próximo quadro.
        public static bool Detach(FBCar car, int part, Vector3 vel)
        {
            var d = car ? Def(car.id) : null; if (d == null || part < 0 || part > 5 || d.p[part] == null) return false;
            if (!rigs.TryGetValue(car, out var r)) rigs[car] = r = new Rig { car = car }; if (r.gone[part]) return false;
            if (!r.split || !r.piv[part]) { r.detachPart = part; r.detachVel = vel; Ensure(); return true; }
            var src = r.piv[part]; var rs = src.GetComponentsInChildren<MeshRenderer>(true).Where(x => x.enabled && x.GetComponent<MeshFilter>()?.sharedMesh).ToArray(); if (rs.Length == 0) return false;
            var deb = new GameObject("peca_solta_" + NAMES[part]); deb.layer = FBWorld.LMap; deb.transform.SetPositionAndRotation(src.position, src.rotation);
            var bb = new Bounds(); bool first = true;
            foreach (var x in rs)
            {
                var g = new GameObject("malha"); g.layer = deb.layer; g.transform.SetParent(deb.transform, false); g.transform.SetPositionAndRotation(x.transform.position, x.transform.rotation); g.transform.localScale = x.transform.lossyScale;
                var mf = x.GetComponent<MeshFilter>(); g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh; var nr = g.AddComponent<MeshRenderer>(); nr.sharedMaterials = x.sharedMaterials;
                var mb = mf.sharedMesh.bounds; foreach (var c in new[] { mb.min, mb.max, new Vector3(mb.min.x, mb.max.y, mb.min.z), new Vector3(mb.max.x, mb.min.y, mb.max.z) })
                { var lp = deb.transform.InverseTransformPoint(g.transform.TransformPoint(c)); if (first) { bb = new Bounds(lp, Vector3.zero); first = false; } else bb.Encapsulate(lp); }
                x.enabled = false;
            }
            var bc = deb.AddComponent<BoxCollider>(); bc.center = bb.center; bc.size = Vector3.Max(bb.size, Vector3.one * .04f);
            foreach (var cc in car.cols) if (cc) Physics.IgnoreCollision(bc, cc);
            var pb = FBFisica.Make(deb, "metal", part >= 4 ? 14 : 22); pb.rb.WakeUp(); pb.rb.linearVelocity = vel; pb.rb.angularVelocity = Random.insideUnitSphere * 5;
            FBFisica.Debris(deb, 45); FBFisica.Sound("metal_pesado", src.position, .8f, 7); FBFx.Sparks(src.position, Vector3.up);
            r.gone[part] = true; r.bent[part] = 0; Detached++; return true;
        }
        public static int Detached;

        class Runner : MonoBehaviour
        {
            // [parceiro] v1 (thread de física): tempo do LateUpdate medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
            static readonly int __fbt_LateUpdate = FBTempo.Id("FBDoors.Runner.LateUpdate");
            void LateUpdate()
            {
                if (!FBTempo.On) { LateUpdate__fbt(); return; }
                long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
            }
            void LateUpdate__fbt()
            {
                Tick(Time.deltaTime);
                var me = FBMatch.Player; if (!me || !me.alive || FBVehicles.CarOf(me) || FBGarage.Open_) return;
                if (!FBVehicles.Allowed) return;
                var kb = UnityEngine.InputSystem.Keyboard.current; if (kb == null || !(kb.uKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) return;
                var c = NearCar(me.Pos, 3.5f); if (c) Keys(c);
            }
        }
        // garante o executor (para o a pé funcionar sem ninguém ter aberto nada ainda)
        public static void Boot() => Ensure();
    }
}
