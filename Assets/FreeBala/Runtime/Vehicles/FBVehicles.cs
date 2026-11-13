// VEÍCULOS (porte do vehicles.js): carros do REB montados como no web — carroceria em 3 LODs (0 / 28 / 70 m) dentro do "shell" (altura de
// fábrica ou rebaixado, sorteada pela semente da vaga), rodas do próprio APK ou aro de customização (wheels.json) em cada ponto de roda,
// interior do REB (forros + volante), materiais de jogo (pintura com reflexo, vidro escuro, metal), colisão pela carroceria (LOD do meio)
// + caixa por roda, rig do REB (banco, mãos no aro, cotovelos, pés nos pedais, saída) e paredão nas picapes. Veículos do Invasão Elite /
// Grau Brasil (ie.json: Caveirão, motos) com hierarquia original, escala própria (IE_FIX) e pontos de guidão/pedaleira.
// Coordenadas: o glTFast espelha X (web (x,y,z) -> Unity (-x,y,z)); ângulos de guinada/rolagem trocam de sinal, arfagem não.
// A direção (física, som, pose, câmera, rede) fica em FBDrive.cs.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public class FBWheel { public Transform obj; public bool front, noSteer; public float r, rr; public int sgn = 1; public Quaternion bas; public Vector3 webP; }

    public class FBCar : MonoBehaviour
    {
        static int seq; public readonly int uid = ++seq;
        public string id, syncId, soundMod, kind = "car"; public Transform soundWallNode; public FBCharacter driver; public int remoteDriver = -1;
        public int netOwner = -1;   // r68: cópia local do carro de OUTRO jogador (garagem/trânsito dele): só o dono dirige
        public bool ie, heavy, grau, armored, wallOn; public Dictionary<string, object> def, meta;
        public readonly List<FBWheel> wheels = new(); public readonly List<(Transform obj, Quaternion bas)> steerNodes = new(); public Vector3? steerAxis;
        public float wheelbase = 2.6f, track = 1.4f, scale = 1, gripSide, footSide = .22f; public Vector3 seat = new(-.38f, .55f, .1f); public Vector3? feet, grip;
        public Vector3[] rigHands, rigPoles, rigFeet; public Vector3? rigWheel, rigOut; public Transform volante; public Quaternion volBase;
        public Bounds lbox, box; public readonly List<Collider> cols = new(); public string engineProf;
        // estado da direção (drive.js D) — vive no carro: quem dirige, o remoto e a oficina leem daqui
        public float v, steer, yaw, pitch, roll, vy, rpm, spin, rspin, crashT, lean, stIn, launchT, prevThr, gas, sp, revRpm, vPrev, gasEst; public int gear = 1; public bool air, burn;
        public Vector3 shift; public FBMoto.Wheelie wh = FBMoto.NewWheelie(); public FBMoto.Engine eng = FBMoto.NewEngine(); public FBCarSound.Gearbox gb;
        public float steerN, steerAng, trick, wheelie; public FBDrive.Rider rider; public (string txt, string sub, bool bad, float until)? grauMsg;
        public FBDrive.VolSetup vol; public bool volTried; public float hornT;
        // luzes (FBCarLights): o trânsito NPC diz freio/ré/seta/pisca-alerta direto (aiLights); no carro do jogador vêm da direção
        public bool aiLights, lBrake, lRev; public int lBlink; public bool lHeadOff; public Color? headTint;
        public int netLights = -1;                  // r45b: luzes/sirene do carro de outro jogador (bits do estado da rede; -1 = sem)
        // oficina (vehicles.js u.R): peças e valores de fábrica deste carro
        public Transform shell, wheelTpl; public float shellY0, zb, wbL; public Vector3 seat0; public Vector3[] rigHands0, rigPoles0, rigFeet0; public Vector3? rigWheel0;
        public string wid, curWid; public GameObject wsrc; public Color paint = Color.white; public Dictionary<string, Material> ownMats; public bool turbo; public string tracao;
        public float Speed => v;
        public void SetColliders(bool on) { foreach (var c in cols) if (c) c.enabled = on; }
        // física nova (FBCarPhys): deslize lateral, giro, deriva por eixo, lataria (mola) e pose de base da carroceria
        public float riderHoldT, hbG = 1, vl, yr, slipF, slipR, beta, lim, bPit, bPitV, bRol, bRolV, bHv, bHvV, bodyHv; public Quaternion bodyQ = Quaternion.identity, shellR0 = Quaternion.identity; public Vector3 shellP0; public bool bodyOn;
        public Vector3 L2W(Vector3 local) => transform.TransformPoint(bodyOn ? FBCarPhys.Pivot + bodyQ * (local - FBCarPhys.Pivot) + Vector3.up * bodyHv : local);   // banco/volante/pedais acompanham a lataria
        public string horn;   // buzina sorteada deste carro (FBCarSound.HornFor)
        public FBMxBike mx;   // r44: moto do MX Grau (física de corpo rígido portada; ver FBMxBike)
        public void Horn() { if (hornT > Time.time) return; hornT = Time.time + 1.2f; FBCarSound.Honk(this); }   // buzina do próprio carro no REB
    }

    public static class FBVehicles
    {
        public const float SCALE = 1.12f, RIDE = .1f, SEAT_HIP = .3f;
        public static readonly List<FBCar> list = new(); static readonly Dictionary<FBCharacter, FBCar> driving = new(); public static bool fp;
        public static bool Driving(FBCharacter c) => c && driving.ContainsKey(c);
        public static FBCar CarOf(FBCharacter c) { if (!c || !driving.TryGetValue(c, out var car)) return null; if (!car) { driving.Remove(c); return null; } return car; }   // carro destruído com alguém "dentro": limpa o registro
        internal static void SetDriving(FBCharacter c, FBCar car) { if (!c) return; if (car) driving[c] = car; else driving.Remove(c); }
        static readonly (string id, float w)[] CIVIL = { ("GolG2", 5), ("GolQuadrado", 5), ("UnoMille", 5), ("Celta", 4), ("Fusca", 3), ("Chevett", 3), ("Palio2010", 4), ("CorsaClassic", 3), ("GolG5", 4), ("HB20", 4), ("Up", 3), ("UnoWay", 3),
            ("SaveiroG4", 4.5f), ("SaveiroQuadrada", 4.5f), ("Santana2002", 2), ("OmegaCD1997", 1.5f), ("OpalaDiplomata85", 1.5f), ("Kombi", 2), ("Montana", 3), ("Fiorino", 2), ("CrossFox", 2), ("ParatiSurf1994", 2),
            ("Clio2003", 3), ("Kadett", 3), ("S10_2017", 2.5f), ("Hilux2023Conquest", 1.5f), ("Toro", 2.5f), ("Amarok", 2), ("Corolla2018", 1.5f), ("CivicLXR_2014", 1.5f), ("Jetta2012", 1), ("L200", 1.5f), ("F250", 1.5f), ("Sw4", 1), ("Golf", 1.5f), ("i30", 1), ("SaveiroCross", 3), ("RAM3500", 1) };
        static readonly uint[] PAINTS = { 0xc9cbcd, 0xeeeeea, 0x18191b, 0x8e1712, 0x1d3f7a, 0x55595e, 0xc9b98f, 0x2e5a3a, 0xd9b21c, 0xa7aaad, 0xf2f2ee, 0x6d1d1a };
        public static readonly string[] BIKES = { "pop100", "motorizada", "xj6", "falcon400", "start" };
        static readonly Dictionary<string, (float s, float seatY)> IE_FIX = new() { ["hilux"] = (1.16f, .42f), ["caveirao"] = (1.32f, .3f), ["pop100"] = (1.2f, 0), ["motorizada"] = (1.2f, 0), ["xj6"] = (1.2f, 0), ["falcon400"] = (1.2f, 0), ["start"] = (1.2f, 0) };
        static readonly Dictionary<string, string[]> IE_WHEELS = new() { ["moto"] = new[] { "RodaDianteira", "RodaTraseira" }, ["car"] = new[] { "FL", "FR", "RL", "RR" } };
        static readonly Dictionary<string, string[]> IE_STEER = new() { ["moto"] = new[] { "SteeringMain" }, ["car"] = new string[0] };
        static uint HashSeed(string v) { uint h = 2166136261; foreach (var c in v) h = (h ^ c) * 16777619; return h; }
        public static System.Func<float> Seeded(uint seed) { uint s = seed; return () => { s += 0x6d2b79f5; uint t = s; t = (t ^ (t >> 15)) * (t | 1); t ^= t + (t ^ (t >> 7)) * (t | 61); return ((t ^ (t >> 14))) / 4294967296f; }; }
        static string F(float x) => x.ToString(System.Globalization.CultureInfo.InvariantCulture);
        static Vector3 W2U(List<object> p, float dy = 0) => p == null ? Vector3.zero : new Vector3(-Json.Fv(p[0]), Json.Fv(p[1]) + dy, Json.Fv(p[2]));   // web -> Unity (espelha X)
        static Color Hex(uint c) => new Color(((c >> 16) & 255) / 255f, ((c >> 8) & 255) / 255f, (c & 255) / 255f);

        // ---- materiais de jogo (gameMat): pintura com reflexo, vidro escuro, metal; o atlas fica com o material do glTF
        static readonly Dictionary<string, Material> MATS = new();
        static Material GameMat(Material m, Color paint)
        {
            if (!m) return m; var n = m.name.Replace(" (Instance)", "");
            string kind = n.StartsWith("carpaint") ? "paint" : n.StartsWith("carglass") ? "glass" : n.StartsWith("carmetal") ? "metal" : null; if (kind == null) return m;
            var key = kind == "paint" ? $"p|{n}|{ColorUtility.ToHtmlStringRGB(paint)}" : $"{kind}|{n}"; if (MATS.TryGetValue(key, out var r) && r) return r;
            r = new Material(FBShaders.Lit) { name = "veh_" + kind };
            if (kind == "paint") { r.SetColor("_BaseColor", paint); r.SetFloat("_Smoothness", .72f); r.SetFloat("_Metallic", .22f); }
            else if (kind == "glass") { r.SetColor("_BaseColor", new Color(.043f, .055f, .07f, .62f)); r.SetFloat("_Smoothness", .95f); r.SetFloat("_Metallic", .1f); r = FBShaders.Transparent(r); }
            else { var c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("baseColorFactor") ? m.GetColor("baseColorFactor") : Color.gray; r.SetColor("_BaseColor", c); r.SetFloat("_Smoothness", .7f); r.SetFloat("_Metallic", .85f); }
            MATS[key] = r; return r;
        }
        static void Dress(GameObject o, Color paint)
        {
            foreach (var mr in o.GetComponentsInChildren<Renderer>(true))
            {
                var ms = mr.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = GameMat(ms[i], paint); mr.sharedMaterials = ms;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; mr.receiveShadows = true;
            }
        }
        // peças da oficina do REB (FBReb): pintura/vidro/metal pelos materiais de jogo e o atlas pelo material da PRÓPRIA carroceria
        // (o GLB das peças vem com 'atlas_<carro>' sem imagem — uma textura só por carro)
        public static void DressParts(GameObject o, FBCar car)
        {
            Material atlas = null;
            if (car.shell) foreach (var r in car.shell.GetComponentsInChildren<Renderer>(true)) { if (r.transform.IsChildOf(o.transform)) continue; foreach (var m in r.sharedMaterials) if (m && m.name.StartsWith("atlas_")) { atlas = m; break; } if (atlas) break; }
            foreach (var mr in o.GetComponentsInChildren<Renderer>(true))
            {
                var ms = mr.sharedMaterials;
                for (int i = 0; i < ms.Length; i++) { if (!ms[i]) continue; if (ms[i].name.StartsWith("atlas_") && atlas) ms[i] = atlas; else ms[i] = GameMat(ms[i], car.paint); }
                mr.sharedMaterials = ms;
            }
        }
        static Transform Find(Transform r, System.Text.RegularExpressions.Regex rx) { foreach (var t in r.GetComponentsInChildren<Transform>(true)) if (t != r && rx.IsMatch(t.name)) return t; return null; }

        // roda: a de fábrica (APK) ou, nos rebaixados e nos carros sem roda original, um aro de customização sorteado
        // carroceria x rodas dos carros portados que ficavam altos (pedido 20/10: viaturas e Clio "muito altos"): medido em vista lateral
        // no Blender (tools/cars/side_render) — viaturas com a soleira 3 cm abaixo do cubo (a S10 do REB fica 13 cm) e folga de 5–13 cm no
        // paralama; agora o pneu encosta/entra um pouco no paralama como nos rebaixados do REB.
        static readonly Dictionary<string, (float stock, float low)> PORT_RIDE = new() { ["ViaturaPMAzul"] = (-.16f, -.19f), ["ViaturaBM"] = (-.16f, -.19f), ["Clio2003"] = (-.11f, -.14f) };
        static string WheelFor(string id, Dictionary<string, object> d, bool civil, System.Func<float> rnd)
        {
            var W = GameData.Wheels; if (W == null) return null;
            var rims = W.Keys.Where(k => !Json.B(W[k], "stock")).ToList(); var sw = Json.S(d, "stockWheel");
            if (sw != null && W.ContainsKey(sw) && !(civil && rnd() < .25f)) return sw;
            return rims.Count > 0 ? rims[Mathf.Min(rims.Count - 1, (int)(rnd() * rims.Count))] : null;
        }

        // ---- carro do REB (make)
        public static FBCar Make(string id, Vector3 pos, float yawDeg, bool civil, System.Func<float> rnd, bool? stockOpt = null, uint? paintOpt = null, bool? som = null, string somMod = null)
        {
            var prefab = FBRegistry.I.Vehicle(id); var d = Json.O(GameData.Vehicles, id); if (!prefab || d == null) return null; rnd ??= () => Random.value;
            var grp = new GameObject("Carro_" + id); var body = new GameObject("body").transform; body.SetParent(grp.transform, false); var shell = new GameObject("shell").transform; shell.SetParent(body, false);
            float lift = 0; foreach (var w in Json.L(d, "wheels") ?? new List<object>()) lift = Mathf.Max(lift, Json.F(w, "r") - Json.Fv(Json.L(w, "p")[1]));
            body.localPosition = new Vector3(0, lift, 0);
            bool port = Json.B(d, "port"); bool stock = stockOpt ?? (!civil || rnd() < .55f);
            float sy = port ? (stock ? -.02f : -.07f - rnd() * .03f) : RIDE + (stock ? .1f + rnd() * .04f : rnd() * .03f);
            if (PORT_RIDE.TryGetValue(id, out var pr)) sy = stock ? pr.stock : pr.low - rnd() * .02f;
            shell.localPosition = new Vector3(0, sy, 0);
            var paint = Hex(paintOpt ?? PAINTS[Mathf.Min(PAINTS.Length - 1, (int)(rnd() * PAINTS.Length))]);
            var inst = Object.Instantiate(prefab); Transform[] lods = new Transform[3]; Transform wheelTpl = null;
            foreach (Transform c in inst.GetComponentsInChildren<Transform>(true))
            {
                if (c == inst.transform) continue; var n = System.Text.RegularExpressions.Regex.Replace(c.name, "_\\d+$", "");
                if (n.StartsWith("body_lod1")) lods[1] ??= c; else if (n.StartsWith("body_lod2")) lods[2] ??= c; else if (n.StartsWith("body")) lods[0] ??= c; else if (n.StartsWith("wheel")) wheelTpl ??= c;
            }
            var levels = new List<LOD>(); float[] sh = Json.Get(d, "port") != null ? new[] { .40f, .16f, .03f } : new[] { .14f, .056f, .012f };   // portados ("port" é objeto no fb_data: Json.B dá false) — viaturas do GTA: LOD0 130 mil tris/33 materiais — com o lodBias 2,4 ficava em LOD0 até ~100 m (passeio r40); agora LOD0 só a < ~27 m
            for (int i = 0; i < 3; i++) if (lods[i]) { lods[i].SetParent(shell, false); Dress(lods[i].gameObject, paint); if (!port) { FBPlates.Split(lods[i], FBPlates.Of(paint)); FBMirrors.Split(lods[i], shell, id); } else FBPlates.SetText(lods[i], FBPlates.Of(paint)); levels.Add(new LOD(sh[i], lods[i].GetComponentsInChildren<Renderer>())); }
            if (levels.Count > 0) { var lg = shell.gameObject.AddComponent<LODGroup>(); lg.fadeMode = LODFadeMode.CrossFade; lg.animateCrossFading = true; var arr = levels.ToArray(); arr[^1].screenRelativeTransitionHeight = .004f; lg.SetLODs(arr); lg.RecalculateBounds(); }
            if (wheelTpl) { wheelTpl.SetParent(body, false); wheelTpl.gameObject.SetActive(false); wheelTpl.name = "wheel_tpl"; }   // molde da roda de fábrica (oficina volta a ela)
            // interior (forros + volante) no mesmo referencial da carroceria
            Transform vol = null; var intD = Json.O(d, "int");
            if (intD != null && FBRegistry.I.Interior(id) is GameObject ip) { var it = Object.Instantiate(ip, shell, false); it.name = "interior"; Dress(it, paint); vol = Find(it.transform, new System.Text.RegularExpressions.Regex("^volante$")); }
            // acessório (viatura etc.)
            var car = grp.AddComponent<FBCar>(); car.id = id; car.def = d; car.heavy = System.Text.RegularExpressions.Regex.IsMatch(id, "F250|RAM|Kombi"); car.engineProf = FBCarSound.ProfileFor(id, car); car.tracao = FBReb.Tracao(id);
            // paredão: 3 em cada 4 picapes civis — sorteado antes da roda, na mesma ordem do vehicles.js
            string somPick = civil && FBParedao.PICKUPS.Contains(id) && (som ?? rnd() < .75f) ? somMod ?? FBParedao.PickModule(d, rnd) : null;
            // rodas
            var wid = WheelFor(id, d, civil, rnd); GameObject wsrc = wid != null ? FBRegistry.I.Wheel(wid) : null; float wdr = wid != null ? Json.F(Json.O(GameData.Wheels, wid), "r") : 0;
            foreach (var w in Json.L(d, "wheels") ?? new List<object>())
            {
                var p = Json.L(w, "p"); float px = Json.Fv(p[0]), r = Json.F(w, "r"); GameObject wm;
                if (wsrc) { wm = Object.Instantiate(wsrc, body, false); var inner = Find(wm.transform, new System.Text.RegularExpressions.Regex("^wheel")); if (wdr > 0) wm.transform.localScale = Vector3.one * (r / wdr); }
                else if (wheelTpl) wm = Object.Instantiate(wheelTpl.gameObject, body, false); else continue;
                wm.name = "roda_" + Json.S(w, "id"); Dress(wm, paint);
                wm.transform.localPosition = new Vector3(-px, Json.Fv(p[1]), Json.Fv(p[2])); if (px < 0) wm.transform.localRotation = Quaternion.Euler(0, 180, 0);
                wm.SetActive(true); car.wheels.Add(new FBWheel { obj = wm.transform, front = Json.Fv(p[2]) > 0, r = r * SCALE, rr = r, sgn = px < 0 ? -1 : 1, bas = wm.transform.localRotation, webP = new Vector3(px, Json.Fv(p[1]), Json.Fv(p[2])) });
            }
            Object.Destroy(inst); car.wheelTpl = wheelTpl; car.wid = wid; car.curWid = wid; car.wsrc = wsrc; car.paint = paint;
            grp.transform.localScale = Vector3.one * SCALE; car.scale = SCALE;
            // medidas: entre-eixos e bitola (metros do mundo), banco (quadril) e rig do REB
            var ws = Json.L(d, "wheels") ?? new List<object>(); var zs = ws.Select(w => Json.Fv(Json.L(w, "p")[2])).ToList(); var xs = ws.Select(w => Mathf.Abs(Json.Fv(Json.L(w, "p")[0]))).ToList();
            float zf = Mathf.Max(zs.DefaultIfEmpty(1).Max(), 1), zb = Mathf.Min(zs.DefaultIfEmpty(-1).Min(), -1), tx = Mathf.Max(xs.DefaultIfEmpty(.7f).Max(), .7f);
            car.wheelbase = (zf - zb) * SCALE; car.track = 2 * tx * SCALE;
            var R = Json.O(d, "rig"); float oy = body.localPosition.y + shell.localPosition.y;
            Vector3? Rp(string k) { var a = Json.L(R, k); return a == null ? null : W2U(a, oy); }
            if (R != null)
            {
                var s = Json.L(R, "seat"); car.seat = new Vector3(-Json.Fv(s[0]), Json.Fv(s[1]) + oy + SEAT_HIP, Json.Fv(s[2]) + .06f);
                if (Json.L(R, "handL") != null) car.rigHands = new[] { Rp("handL").Value, Rp("handR").Value };
                if (Json.L(R, "poleL") != null) car.rigPoles = new[] { Rp("poleL").Value, Rp("poleR").Value };
                if (Json.L(R, "footL") != null) car.rigFeet = new[] { Rp("footL").Value, Rp("footR").Value };
                car.rigWheel = Rp("wheel"); var o = Json.L(R, "out"); if (o != null) car.rigOut = new Vector3(-Json.Fv(o[0]), 0, Json.Fv(o[2]));
            }
            else car.seat = new Vector3(-tx * .42f, lift + .62f, zb * .12f);
            if (vol) { car.volante = vol; car.volBase = vol.localRotation; }
            car.shell = shell; car.shellY0 = shell.localPosition.y; car.shellP0 = shell.localPosition; car.shellR0 = shell.localRotation; car.zb = zb; car.wbL = zf - zb; car.seat0 = car.seat; car.rigHands0 = car.rigHands?.ToArray(); car.rigPoles0 = car.rigPoles?.ToArray(); car.rigFeet0 = car.rigFeet?.ToArray(); car.rigWheel0 = car.rigWheel;
            Finish(car, grp, pos, yawDeg); LodExtras(car);
            if (somPick != null) { FBParedao.Mount(car, somPick, d); foreach (var t in car.soundWallNode ? car.soundWallNode.GetComponentsInChildren<Transform>(true) : new Transform[0]) t.gameObject.layer = FBWorld.LVehicle; }
            return car;
        }
        // ---- veículo do Invasão Elite / Grau Brasil (makeIE)
        public static FBCar MakeIE(string id, Vector3 pos, float yawDeg)
        {
            var meta = Json.O(GameData.IE, id); var prefab = FBRegistry.I.Vehicle(id); if (meta == null || !prefab) return null;
            var grp = new GameObject("IE_" + id); var model = Object.Instantiate(prefab, grp.transform, false); model.name = "modelo";
            var dead = Find(model.transform, new System.Text.RegularExpressions.Regex("^destroyed$")); if (dead) dead.gameObject.SetActive(false);
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            var kind = Json.S(meta, "kind") == "moto" ? "moto" : "car"; var mw = Json.L(meta, "wheels") ?? new List<object>();
            float lift; var wn = Json.L(meta, "wheelNodes");
            if (wn != null) lift = -mw.Min(w => Json.Fv(Json.L(w, "p")[1]) - Json.F(w, "r"));
            else { var bb = Bounds(model.transform, grp.transform); lift = -bb.min.y; }
            model.transform.localPosition = new Vector3(0, lift, 0);
            var car = grp.AddComponent<FBCar>(); car.id = id; car.ie = true; car.meta = meta; car.kind = kind; car.heavy = id == "caveirao"; car.armored = id == "caveirao"; car.grau = (Json.S(meta, "src") ?? "").ToLowerInvariant().Contains("grau");
            var names = wn != null ? wn.Select(x => (Json.S(x, "name"), Json.B(x, "front"), Json.B(x, "noSteer"))).ToList() : IE_WHEELS[kind].Select(n => (n, System.Text.RegularExpressions.Regex.IsMatch(n, "Dianteira|^F"), false)).ToList();
            foreach (var (n, front, noSteer) in names)
            {
                var o = Find(model.transform, new System.Text.RegularExpressions.Regex("^" + System.Text.RegularExpressions.Regex.Escape(n) + "$")); if (!o) continue;
                bool fz = grp.transform.InverseTransformPoint(o.position).z > 0; float wr = mw.Where(w => (Json.Fv(Json.L(w, "p")[2]) > 0) == fz).Select(w => Json.F(w, "r")).DefaultIfEmpty(.4f).First();
                car.wheels.Add(new FBWheel { obj = o, front = front, noSteer = noSteer, r = wr, sgn = 1, bas = o.localRotation });
            }
            foreach (var n in (Json.L(meta, "steerNodes")?.Select(x => x as string) ?? IE_STEER[kind]))
            { var o = Find(model.transform, new System.Text.RegularExpressions.Regex("^" + System.Text.RegularExpressions.Regex.Escape(n) + "$")); if (o) car.steerNodes.Add((o, o.localRotation)); }
            var zs = mw.Select(w => Json.Fv(Json.L(w, "p")[2])).DefaultIfEmpty(0).ToList(); var seats = Json.L(meta, "seats") ?? new List<object>(); var drv = seats.FirstOrDefault(x => Json.B(x, "driver")) ?? seats.FirstOrDefault();
            car.wheelbase = Mathf.Max(1.2f, zs.Max() - zs.Min()); car.track = kind == "moto" ? .3f : 2 * mw.Select(w => Mathf.Abs(Json.Fv(Json.L(w, "p")[0]))).DefaultIfEmpty(.7f).Max();
            car.seat = drv != null ? W2U(Json.L(drv, "p"), lift) : new Vector3(0, 1, 0);
            var ft = Json.L(drv, "feet"); if (ft != null) car.feet = W2U(ft, lift); car.footSide = Json.F(drv, "footSide", .22f);
            var pts = Json.O(meta, "points"); var spn = Json.L(pts, "steerPoint"); if (spn != null) car.grip = W2U(spn, lift); car.gripSide = Json.F(pts, "gripSide");
            var sa = Json.L(meta, "steerAxis"); if (sa != null) car.steerAxis = new Vector3(-Json.Fv(sa[0]), Json.Fv(sa[1]), Json.Fv(sa[2])).normalized;
            // colisão: caixas simples (motos: a malha tem 30–60 mil triângulos) ou a malha visível
            foreach (var b in Json.L(meta, "simpleCol") ?? new List<object>()) { var bc = grp.AddComponent<BoxCollider>(); var c = Json.L(b, "c"); var s = Json.L(b, "size"); bc.center = new Vector3(-Json.Fv(c[0]), Json.Fv(c[1]) + lift, Json.Fv(c[2])); bc.size = new Vector3(Json.Fv(s[0]), Json.Fv(s[1]), Json.Fv(s[2])); car.cols.Add(bc); }
            if (IE_FIX.TryGetValue(id, out var fx)) { grp.transform.localScale = Vector3.one * fx.s; car.scale = fx.s; car.wheelbase *= fx.s; car.track *= fx.s; foreach (var w in car.wheels) w.r *= fx.s; car.seat.y += fx.seatY; }
            car.engineProf = FBCarSound.ProfileFor(id, car);
            Finish(car, grp, pos, yawDeg, Json.L(meta, "simpleCol") == null); LodExtras(car);
            return car;
        }
        // LOD (Agente 9): interior (forros/volante) só no LOD0 e sem sombra (fica dentro da carroceria); rodas em todos os níveis e cortadas
        // junto com a carroceria. Veículos do IE (motos com 30–60 mil triângulos) ganham um LODGroup de corte por tamanho na tela.
        static void LodExtras(FBCar car)
        {
            var wheelR = car.wheels.Where(w => w.obj).SelectMany(w => w.obj.GetComponentsInChildren<Renderer>(true)).ToArray();
            var lg = car.shell ? car.shell.GetComponent<LODGroup>() : null;
            if (lg)
            {
                var it = car.shell.Find("interior"); var intR = it ? it.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
                foreach (var r in intR) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var lods = lg.GetLODs(); for (int i = 0; i < lods.Length; i++) lods[i].renderers = lods[i].renderers.Concat(wheelR).Concat(i == 0 ? intR : new Renderer[0]).Where(r => r).Distinct().ToArray();
                lg.SetLODs(lods); lg.RecalculateBounds();
            }
            else if (car.ie && !car.GetComponentInChildren<LODGroup>())
            {
                var all = car.GetComponentsInChildren<Renderer>(true); if (all.Length == 0) return;
                lg = car.gameObject.AddComponent<LODGroup>(); lg.fadeMode = LODFadeMode.CrossFade; lg.animateCrossFading = true; lg.SetLODs(new[] { new LOD(car.kind == "moto" ? .01f : .006f, all) }); lg.RecalculateBounds();
            }
        }
        // caixa no espaço 'space' pelas caixas LOCAIS das malhas (r.bounds é a caixa de MUNDO alinhada aos eixos: com o carro girado 45° ela
        // inchava ~40% e a colisão/saída do carro ficavam muito maiores que o veículo)
        static Bounds Bounds(Transform root, Transform space)
        {
            var inv = space.worldToLocalMatrix; Bounds b = default; bool any = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || r.name == "sombra_contato" || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                Bounds lb; Matrix4x4 m;
                if (r is SkinnedMeshRenderer sk) { lb = sk.localBounds; m = inv * sk.transform.localToWorldMatrix; }
                else { var mf = r.GetComponent<MeshFilter>(); if (mf && mf.sharedMesh) { lb = mf.sharedMesh.bounds; m = inv * r.transform.localToWorldMatrix; } else { lb = r.bounds; m = inv; } }
                for (int i = 0; i < 8; i++) { var p = m.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? lb.min.x : lb.max.x, (i & 2) == 0 ? lb.min.y : lb.max.y, (i & 4) == 0 ? lb.min.z : lb.max.z)); if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p); }
            }
            return b;
        }
        // colisão pela carroceria (LOD do meio, como o web) + caixa por roda; camada Veiculo; corpo cinemático
        static void Finish(FBCar car, GameObject grp, Vector3 pos, float yawDeg, bool meshCol = true)
        {
            // chão ANTES de criar a colisão do próprio carro (senão o raio batia no teto dele e o carro nascia flutuando)
            // de todos os pisos sob o ponto, o mais perto da altura pedida (copa/tronco de árvore, toldo ou poste em cima da vaga não levantam o carro)
            { float best = float.MaxValue, gy = pos.y; bool any = false;
              foreach (var h in Physics.RaycastAll(new Vector3(pos.x, pos.y + 2.8f, pos.z), Vector3.down, 8.8f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore))
                  if (h.normal.y > .6f && Mathf.Abs(h.point.y - pos.y) < best) { best = Mathf.Abs(h.point.y - pos.y); gy = h.point.y; any = true; }
              if (any) pos.y = gy; }
            grp.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yawDeg, 0));
            car.lbox = Bounds(grp.transform, grp.transform); car.box = car.lbox;
            if (meshCol)
            {
                var shell = grp.transform.Find("body/shell"); Transform hull = null;
                if (shell) foreach (Transform c in shell) if (c.name.StartsWith("body_lod1")) hull = c;
                if (!hull && shell) foreach (Transform c in shell) if (c.name.StartsWith("body")) { hull = c; break; }
                var src = hull ? hull : grp.transform; bool ok = false;
                foreach (var mf in src.GetComponentsInChildren<MeshFilter>(true)) { if (!mf.sharedMesh || !mf.sharedMesh.isReadable) continue; var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; car.cols.Add(mc); ok = true; }
                if (!ok) { var bc = grp.AddComponent<BoxCollider>(); bc.center = car.lbox.center + Vector3.up * car.lbox.size.y * .05f; bc.size = Vector3.Scale(car.lbox.size, new Vector3(.96f, .9f, .97f)); car.cols.Add(bc); }   // malha não legível: caixa
                foreach (var w in car.wheels) { var wb = Bounds(w.obj, grp.transform); var bc = grp.AddComponent<BoxCollider>(); bc.center = wb.center; bc.size = wb.size; car.cols.Add(bc); }
            }
            ContactShadow(grp.transform, car.lbox);
            foreach (var t in grp.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = FBWorld.LVehicle;
            var rb = grp.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None;
            car.yaw = -yawDeg * Mathf.Deg2Rad;
            list.Add(car);
        }

        // sombra de contato: mancha escura e suave sob o veículo (sem ela o carro parado parecia flutuar — a sombra do sol cai atrás dele)
        static Material shadowMat;
        static void ContactShadow(Transform grp, Bounds lb)
        {
            if (!shadowMat)
            {
                const int N = 64; var tx = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; var px = new Color[N * N];
                for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) { float u = Mathf.Abs((x + .5f) / N * 2 - 1), v = Mathf.Abs((y + .5f) / N * 2 - 1); float d = Mathf.Pow(Mathf.Pow(u, 4) + Mathf.Pow(v, 4), .25f); px[y * N + x] = new Color(0, 0, 0, Mathf.Clamp01((1 - d) * 2.2f) * .62f); }
                tx.SetPixels(px); tx.Apply();
                var tpl = Resources.Load<Material>("FBMats/Unlit_Alpha"); shadowMat = tpl ? new Material(tpl) : FBShaders.Transparent(new Material(FBShaders.Unlit));
                shadowMat.SetTexture("_BaseMap", tx); shadowMat.SetColor("_BaseColor", Color.white); shadowMat.renderQueue = 2990;
            }
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(q.GetComponent<Collider>()); /* r45e: o Destroy adiado deixava o MeshCollider vivo quando a MX punha o Rigidbody (erro vermelho no console) */ q.name = "sombra_contato"; q.transform.SetParent(grp, false);
            q.transform.localPosition = new Vector3(lb.center.x, lb.min.y + .03f, lb.center.z); q.transform.localRotation = Quaternion.Euler(90, 0, 0); q.transform.localScale = new Vector3(lb.size.x, lb.size.z, 1);
            var mr = q.GetComponent<MeshRenderer>(); mr.sharedMaterial = shadowMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        }

        // ---- carros estacionados do nível (spawnParked): mesma semente por vaga do web (o mesmo carro em todo aparelho)
        // VEÍCULOS POR MODO (pedido do usuário 20/10): 4v4 e Todos × Todos SEM veículos (nem estacionados: nada para entrar, sincronizar ou
        // mostrar); Battle Royale, Treino e De Rolê COM; os demais modos como antes. Vale no visual (não nascem), na lógica (Enter recusa)
        // e na rede (estado de veículo de remoto é ignorado; o modo é o mesmo para todos na sala).
        public static bool AllowedIn(string mode) => !(mode == "squad" || mode == "4v4" || mode == "10x10" || mode == "ffa");   // [parceiro 01/11] 10x10 (FBModo10x10) também sem veículos
        public static bool Allowed => AllowedIn(FBSession.mode);
        public static void SpawnParked(FBMatch G, float freeChance = 0)
        {
            Clear(); if (!Allowed) { Debug.Log($"[FreeBala] veículos desativados no modo {FBSession.mode}"); return; }
            var parked = Json.L(G.Level, "parked"); if (parked == null || FBRegistry.I == null) return;
            string mapKey = Json.S(G.Level, "id") ?? Json.S(G.Level, "name") ?? "map"; var sig = string.Join("|", parked.Select(o => string.Join(",", (Json.L(o, "p") ?? new List<object>()).Select(v => F(Json.Fv(v)))) + ":" + F(Json.F(o, "yaw")) + ":" + (Json.S(o, "kind") ?? "")));
            var allRng = Seeded(HashSeed(mapKey + "|" + sig)); var ids = CIVIL.Where(c => FBRegistry.I.Vehicle(c.id) && GameData.Vehicles != null && GameData.Vehicles.ContainsKey(c.id)).ToList(); float tot = ids.Sum(c => c.w);
            string PickCivil() { float r = allRng() * tot; foreach (var (id, w) in ids) { r -= w; if (r <= 0) return id; } return ids.Count > 0 ? ids[0].id : null; }
            var plan = new List<(Dictionary<string, object> s, int i, string id, string ie, System.Func<float> rng, bool skip)>(); int idx = 0;
            foreach (var po in parked)
            {
                var s = po as Dictionary<string, object>; var kind = Json.S(s, "kind");
                var rng = Seeded(HashSeed($"{mapKey}:{idx}:{string.Join(",", (Json.L(s, "p") ?? new List<object>()).Select(v => F(Json.Fv(v))))}:{F(Json.F(s, "yaw"))}"));
                string via = kind == "viatura" ? PickViatura(rng) : null;
                plan.Add((s, idx, via ?? PickCivil(), null, rng, false)); idx++;
            }
            // Caveirão do IE entre as viaturas (4ª viatura ou a última)
            var vi = plan.Where(p => Json.S(p.s, "kind") == "viatura").ToList(); var ci = plan.Where(p => Json.S(p.s, "kind") != "viatura").ToList();
            for (int i = ci.Count - 1; i > 0; i--) { int j = (int)(allRng() * (i + 1)); (ci[i], ci[j]) = (ci[j], ci[i]); }
            if (vi.Count > 0 && GameData.IE != null && GameData.IE.ContainsKey("caveirao") && FBRegistry.I.Vehicle("caveirao")) { var k = plan.IndexOf(vi[Mathf.Min(vi.Count - 1, 3)]); var p = plan[k]; p.ie = "caveirao"; p.id = null; plan[k] = p; }
            if (freeChance > 0) for (int k = 0; k < plan.Count; k++) { var p = plan[k]; if (Json.S(p.s, "kind") != "viatura" && p.ie == null && p.rng() < freeChance) { p.skip = true; plan[k] = p; } }
            foreach (var p in plan)
            {
                if (p.skip) continue; var pos = Coords.FromWeb(Json.L(p.s, "p")); float yaw = Coords.YawFromWeb(Json.F(p.s, "yaw"));
                var car = p.ie != null ? MakeIE(p.ie, pos, yaw) : p.id != null ? Make(p.id, pos, yaw, Json.S(p.s, "kind") != "viatura", p.rng) : null; if (!car) continue;
                car.syncId = $"{mapKey}:{p.i}";
                if (car.kind == "moto") { car.yaw += (p.rng() - .5f) * .6f; Rest(car); }
                else if (!ParkValid(car, out var why)) { ParkLog.Add(why); list.Remove(car); Object.Destroy(car.gameObject); continue; }
            }
            MotoProps();
        }
        // viaturas estacionadas: S10 2016 da PM (azul) e da Brigada Militar (adesivada) — tools/cars/viaturabm.json / viaturapmazul.json;
        // a S10 PM-RR antiga (ViaturaPM) só entra se as novas não estiverem no registro. Sorteio fixo pela vaga (igual em todos os clientes).
        static readonly string[] VIATURAS = { "ViaturaPMAzul", "ViaturaBM" };
        public static string PickViatura(System.Func<float> rng)
        {
            var ok = VIATURAS.Where(v => FBRegistry.I.Vehicle(v) && GameData.Vehicles.ContainsKey(v)).ToList();
            if (ok.Count > 0) return ok[Mathf.Min(ok.Count - 1, (int)(rng() * ok.Count))];
            return FBRegistry.I.Vehicle("ViaturaPM") && GameData.Vehicles.ContainsKey("ViaturaPM") ? "ViaturaPM" : null;
        }
        static void Rest(FBCar car) { if (car.mx) car.mx.Park(.1f, -car.yaw * Mathf.Rad2Deg); else car.transform.rotation = FBDrive.CarRot(0, car.yaw, .1f); }   // moto no descanso, meio de lado (r49i: MX nivelada nas duas rodas)
        // r45 (pedido: "carros estacionados só em locais válidos, nunca no meio da rua, sem sobreposição, altura e rotação corretas"): cada
        // vaga do nível é conferida no jogo antes de ficar —
        //  · NO MEIO DA FAIXA (centro a menos de 55 % da meia largura da faixa mais perto, pela malha FBRoads): vai para a vaga livre mais perto
        //    (até 30 m) ou não nasce;
        //  · SOBREPOSTO a outro veículo ou ENTRANDO em parede/objeto (caixa do carro 85 % acima do meio-fio): tenta afastar 0,6 m para os lados,
        //    depois não nasce;
        //  · ALTURA e INCLINAÇÃO pelo chão embaixo das 4 rodas (rua em ladeira = carro inclinado junto, sem roda enterrada ou no ar).
        public static readonly List<string> ParkLog = new(); public static int ParkMoved, ParkDropped, ParkOk; public static float SettleErr;
        static bool ParkValid(FBCar car, out string why)
        {
            why = null; var T = car.transform; if (!FBRoads.Ready) FBRoads.Load(FBSession.map);
            float hw = car.lbox.extents.x * car.scale, hl = car.lbox.extents.z * car.scale;
            if (FBRoads.Ready)
            {
                var S = FBRoads.NearestLane(T.position, 6, out _, out float lat);
                if (S != null && Mathf.Abs(lat) < S.w / 2 * .55f)
                {
                    FBSpot best = null; float bd = 30;
                    foreach (var sp in FBRoads.spots) { if (sp.car) continue; float d = Vector3.Distance(sp.p, T.position); if (d < bd && !list.Any(o => o && o != car && Vector3.Distance(o.transform.position, sp.p) < 2.6f)) { bd = d; best = sp; } }
                    if (best == null) { ParkDropped++; why = $"{car.id} no meio da faixa (lateral {lat:0.0} m de {S.w / 2:0.0}) sem vaga perto: não nasce"; return false; }
                    T.position = best.p; car.yaw = -best.yaw * Mathf.Deg2Rad; T.rotation = FBDrive.CarRot(0, car.yaw, 0); best.car = car; ParkMoved++;
                    ParkLog.Add($"{car.id} estava no meio da faixa: foi para a vaga a {bd:0} m");
                }
            }
            var r = T.right; Vector3 p0 = T.position;
            foreach (float dx in new[] { 0f, .6f, -.6f, 1.2f, -1.2f })
            {
                T.position = p0 + r * dx; Settle(car);
                if (!Blocked(car, out var what)) { if (dx != 0) { ParkMoved++; ParkLog.Add($"{car.id} afastado {dx:+0.0;-0.0} m ({what})"); } ParkOk++; return true; }
                why = $"{car.id} sobreposto a {what}";
            }
            ParkDropped++; return false;
        }
        static bool Blocked(FBCar car, out string what)
        {
            what = null; var T = car.transform; var L = car.lbox; var ext = Vector3.Scale(L.extents, Vector3.one * car.scale);
            float bot = .35f, top = Mathf.Max(bot + .3f, L.max.y * car.scale * .88f);                       // de 35 cm (acima do meio-fio/calçada) até 88 % da altura (toldo baixo não conta)
            var c = T.TransformPoint(new Vector3(L.center.x, 0, L.center.z)) + T.up * ((bot + top) / 2); var half = new Vector3(ext.x * .85f, (top - bot) / 2, ext.z * .88f);
            car.SetColliders(false);
            try
            {
                foreach (var h in Physics.OverlapBox(c, half, T.rotation, FBWorld.SolidMask | (1 << FBWorld.LVehicle), QueryTriggerInteraction.Ignore))
                {
                    var oc = h.GetComponentInParent<FBCar>(); if (oc == car) continue;
                    if (oc) { what = "veículo " + oc.id; return true; }
                    if (h.gameObject.layer == FBWorld.LMap && !(h is TerrainCollider) && h.bounds.size.y > .5f) { what = h.name; return true; }
                }
            }
            finally { car.SetColliders(true); }
            return false;
        }
        // altura e inclinação pelo chão embaixo das rodas
        public static void Settle(FBCar car)
        {
            var T = car.transform; var L = car.lbox; float s = car.scale; float hx = L.extents.x * s * .8f, hz = Mathf.Max(.8f, car.wheelbase * .5f);
            var fwd = FBDrive.Fwd(car.yaw); var rgt = Vector3.Cross(Vector3.up, fwd).normalized; var o = T.position; float[] y = new float[4]; int n = 0;
            car.SetColliders(false);
            try
            {
                var pts = new[] { o + fwd * hz - rgt * hx, o + fwd * hz + rgt * hx, o - fwd * hz - rgt * hx, o - fwd * hz + rgt * hx };
                for (int i = 0; i < 4; i++) { var g = FBWorld.GroundY(pts[i].x, o.y + 1.5f, pts[i].z, .3f, 4); y[i] = g ?? o.y; if (g != null) n++; }
            }
            finally { car.SetColliders(true); }
            if (n < 3) return;
            float front = (y[0] + y[1]) / 2, back = (y[2] + y[3]) / 2, left = (y[0] + y[2]) / 2, right = (y[1] + y[3]) / 2;
            float pa = Mathf.Clamp(Mathf.Atan2(front - back, hz * 2), -.25f, .25f), ra = Mathf.Clamp(Mathf.Atan2(left - right, hx * 2), -.15f, .15f), cy = (y[0] + y[1] + y[2] + y[3]) / 4;
            // sinal da arfagem/rolagem do CarRot escolhido pela MEDIDA: o que deixa as 4 rodas mais perto do chão de cada uma
            float bestE = 1e9f, bp = 0, br = 0;
            foreach (float sp in new[] { 1f, -1f }) foreach (float sr in new[] { 1f, -1f })
            {
                var q = FBDrive.CarRot(pa * sp, car.yaw, ra * sr); float e = 0;
                var lc = new[] { new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz), new Vector3(-hx, 0, -hz), new Vector3(hx, 0, -hz) };
                for (int i = 0; i < 4; i++) { float wy = cy + (q * lc[i]).y; e += Mathf.Abs(wy - y[i]); }
                if (e < bestE) { bestE = e; bp = pa * sp; br = ra * sr; }
            }
            T.SetPositionAndRotation(new Vector3(o.x, cy, o.z), FBDrive.CarRot(bp, car.yaw, br)); car.pitch = bp; car.roll = br; SettleErr = Mathf.Max(SettleErr, bestE / 4);
        }
        // motos que vieram como enfeite fixo no mapa (…moto_bros/crf/pop/cbr): some o enfeite e nasce uma moto de verdade do Grau, no mesmo rumo
        static void MotoProps()
        {
            var mxIds = FBMxBike.IDS.Where(FBMxBike.Has).ToList();                               // r44: motos do MX Grau no lugar das do Grau Brasil
            var ids = mxIds.Count > 0 ? mxIds : BIKES.Where(b => GameData.IE != null && GameData.IE.ContainsKey(b) && FBRegistry.I.Vehicle(b)).ToList(); if (ids.Count == 0) return;
            var rx = new System.Text.RegularExpressions.Regex("(^|_)moto_(bros|crf|pop|cbr)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var found = new List<Transform>();
            foreach (var r in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) if (r.gameObject.layer == FBWorld.LMap && rx.IsMatch(r.name) && !(r.parent && rx.IsMatch(r.parent.name))) found.Add(r);
            found = found.OrderBy(t => t.GetSiblingIndex()).ThenBy(t => t.position.x).ToList();
            for (int i = 0; i < found.Count; i++)
            {
                var o = found[i]; var rs = o.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue; var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                var ax = (o.rotation * Vector3.right).Flat().normalized; var az = (o.rotation * Vector3.forward).Flat().normalized; var sz = b.size;
                var along = Mathf.Abs(sz.x * ax.x) + Mathf.Abs(sz.z * ax.z) >= Mathf.Abs(sz.x * az.x) + Mathf.Abs(sz.z * az.z) ? ax : az;
                foreach (var r in rs) r.enabled = false; foreach (var c in o.GetComponentsInChildren<Collider>()) c.enabled = false;
                float Room(Vector3 d) { float m = 9; foreach (var hy in new[] { .5f, 1f }) if (FBWorld.RayHit(new Vector3(b.center.x, b.min.y + hy, b.center.z), d, 9, out var h)) m = Mathf.Min(m, h.distance); return m; }
                if (Room(-along) > Room(along) + .5f) along = -along;
                var at = new Vector3(b.center.x, b.min.y, b.center.z); float yw = Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg;
                var car = mxIds.Count > 0 ? MakeMx(ids[i % ids.Count], at, yw) : MakeIE(ids[i % ids.Count], at, yw); if (!car) continue;
                car.syncId = "motoprop:" + i; Rest(car);
            }
        }
        public static void Clear()
        {
            foreach (var c in list) if (c) Object.Destroy(c.gameObject);
            list.Clear(); driving.Clear(); FBParedao.Reset(); FBCarFX.Reset(); FBDrive.ResetState(); FBSkyPlanes.Reset();
        }
        // compatibilidade: um carro avulso (rolê/garagem)
        public static FBCar Spawn(string id, Vector3 p, float yawDeg) => id != null && id.StartsWith("mx_") ? MakeMx(id.Substring(3), p, yawDeg) : GameData.IE != null && GameData.IE.ContainsKey(id) ? MakeIE(id, p, yawDeg) : Make(id, p, yawDeg, true, null);

        // ---- r44: moto do MX Grau — modelo com a hierarquia do APK, física montada pelo FBMxBike (cinemática estacionada, dinâmica pilotando).
        // O grupo do FBCar é o nó raiz do prefab do APK (Rigidbody do chassi + colisores cápsula); escala 1,2 (mundo do jogo).
        public static FBCar MakeMx(string vid, Vector3 pos, float yawDeg)
        {
            GameObject grp = null;
            try { return MakeMx0(vid, pos, yawDeg, out grp); }
            catch (System.Exception ex) { Debug.LogWarning($"[FreeBala][MakeMx] moto '{vid}' falhou: {ex.Message}"); if (grp) Object.Destroy(grp); return null; }
        }
        static FBCar MakeMx0(string vid, Vector3 pos, float yawDeg, out GameObject grp)
        {
            grp = null;
            var prefab = FBRegistry.I.Vehicle("mx_" + vid); var meta = Json.O(FBMxBike.DB, vid); if (!prefab || meta == null) return null;
            grp = new GameObject("MX_" + vid); var model = Object.Instantiate(prefab, grp.transform, false); model.name = "modelo";
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            var car = grp.AddComponent<FBCar>(); car.id = "mx_" + vid; car.ie = true; car.kind = "moto"; car.meta = meta; car.grau = true;
            var mx = FBMxBike.Attach(car, vid, model.transform); car.mx = mx; mx.OnCrash += why => FBDrive.MxCrash(car, why);
            // pontos do piloto (usados no subir/descer do 171 e no remoto): quadril de "pilotando" do RigData, punhos, pedaleiras
            var rp = mx.RidingPose; car.seat = rp.hips - new Vector3(0, .42f, -.02f);
            if (mx.gripL && mx.gripR) { var gl = grp.transform.InverseTransformPoint(mx.gripL.position); var gr = grp.transform.InverseTransformPoint(mx.gripR.position); car.grip = (gl + gr) * .5f; car.gripSide = Mathf.Abs(gr.x - gl.x) * .5f; }
            car.feet = (rp.lfoot + rp.rfoot) * .5f; car.footSide = Mathf.Abs(rp.rfoot.x - rp.lfoot.x) * .5f;
            float zf = 0, zr = 0; foreach (var w in model.GetComponentsInChildren<Transform>(true)) { if (w.name == "FPhysicsWheel") zf = grp.transform.InverseTransformPoint(w.position).z; else if (w.name == "RPhysicsWheel") zr = grp.transform.InverseTransformPoint(w.position).z; }
            grp.transform.localScale = Vector3.one * FBMxBike.S; car.scale = FBMxBike.S; car.wheelbase = Mathf.Max(1, (zf - zr) * FBMxBike.S); car.track = .3f * FBMxBike.S;
            { float best = float.MaxValue, gy = pos.y; bool any = false;
              foreach (var h in Physics.RaycastAll(new Vector3(pos.x, pos.y + 2.8f, pos.z), Vector3.down, 8.8f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore))
                  if (h.normal.y > .6f && h.collider.transform.root != grp.transform && Mathf.Abs(h.point.y - pos.y) < best) { best = Mathf.Abs(h.point.y - pos.y); gy = h.point.y; any = true; }
              if (any) pos.y = gy; }
            grp.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yawDeg, 0)); mx.Park(0, yawDeg);   // r49i: nivelada (a pose de fábrica é ~10° de nariz para cima)
            mx.Paint(HashSeed(vid + "|" + F(Mathf.Round(pos.x)) + "," + F(Mathf.Round(pos.z))));                  // r45: pintura do BikePaint (igual em todos os clientes)
            car.lbox = Bounds(grp.transform, grp.transform); car.box = car.lbox;
            ContactShadow(grp.transform, car.lbox);
            var sh = grp.transform.Find("sombra_contato");                                               // r49i: a raiz nivelada tem arfagem: a sombra fica no plano das rodas
            if (sh) { var lq = Quaternion.Euler(-mx.LevelPitch, 0, 0); sh.localPosition = lq * new Vector3(car.lbox.center.x, mx.LevelBottom + .03f, car.lbox.center.z); sh.localRotation = lq * Quaternion.Euler(90, 0, 0); }
            foreach (var t in grp.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = FBWorld.LVehicle;
            car.yaw = -yawDeg * Mathf.Deg2Rad; car.engineProf = null;
            var lg = grp.AddComponent<LODGroup>(); lg.fadeMode = LODFadeMode.CrossFade; lg.animateCrossFading = true; lg.SetLODs(new[] { new LOD(.01f, grp.GetComponentsInChildren<Renderer>(true).Where(r => r.name != "sombra_contato").ToArray()) }); lg.RecalculateBounds();
            list.Add(car); return car;
        }
        public static FBCar BySyncId(string id) => list.FirstOrDefault(c => c && c.syncId == id);
        // veículo mais próximo (até r m da lataria) que ninguém está usando
        public static FBCar Nearest(Vector3 p, float r)
        {
            FBCar best = null; float bd = r;
            foreach (var c in list)
            {
                if (!c || c.driver || c.remoteDriver >= 0 || c.netOwner >= 0) continue; var lp = c.transform.InverseTransformPoint(p);
                var q = c.lbox.ClosestPoint(lp); float d = Vector3.Distance(c.transform.TransformPoint(q), p); if (d < bd) { bd = d; best = c; }
            }
            return best;
        }
        public static void Toggle(FBCharacter ch)
        {
            if (Driving(ch)) { FBDrive.Exit(ch, false); return; }
            if (FBDrive.Approaching(ch)) { FBDrive.CancelApproach(ch); return; }                  // r50: F de novo enquanto anda até a porta = desiste
            if (!Allowed) return; var car = Nearest(ch.Pos, 2.2f); if (car && !FBDrive.BeginApproach(ch, car)) FBDrive.Enter(ch, car);
            if (!car && ch.isPlayer && FBNet.I)                                                     // r68: carro de outro jogador (só o dono dirige)
                foreach (var o in list) if (o && o.netOwner >= 0 && Vector3.Distance(o.transform.TransformPoint(o.lbox.ClosestPoint(o.transform.InverseTransformPoint(ch.Pos))), ch.Pos) < 2.2f) { FBHUD.I?.Center("CARRO DE " + (FBNet.I.NameOf(o.netOwner) ?? "OUTRO JOGADOR").ToUpper(), 1.6f); break; }
        }
        public static void Exit(FBCharacter ch, bool force) => FBDrive.Exit(ch, force);
        public static void ExitIfDriving(FBCharacter ch) { if (Driving(ch)) FBDrive.Exit(ch, true); }
        public static void PlayerInput(FBCharacter ch, FBInputState inp, float dt) => FBDrive.PlayerInput(ch, inp, dt);
        public static void Tick(float dt) { FBDrive.Tick(dt); FBCarFX.Update(dt); }
        public static void Camera(Camera cam, float dt, float camYaw, float camPitch) => FBDrive.Camera(cam, dt, camYaw, camPitch);
    }
    public class FBVehiclesTicker : MonoBehaviour
    {
        static readonly int __fbt_Update = FBTempo.Id("FBVehiclesTicker.Update");                       // [parceiro] tempo por script (FBTempo, desligado no jogo)
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt() => FBVehicles.Tick(Time.deltaTime);
    }
}
