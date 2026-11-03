// SOM DOS VEÍCULOS (porte do carsound.js) — como no Rebaixados Elite Brasil 3.9.71:
//  · motor: Realistic Engine Sound mobile alimentado pelo RPM do RCC: 6 camadas em loop (idle, low, med, high, very_high, maxRPM), cada uma
//    com curva de VOLUME e de PITCH (AnimationCurve do APK, Hermite) sobre rpm/7000 — curvas em fb_data.json "res" (tools/reb/res_curves.py);
//  · câmbio do RCC_CarControllerV3: 5 marchas, sobe a 6900, desce a 3000, troca 0,35 s, 1000–7000 rpm, vmax da marcha = vmax·marcha/5;
//  · TurboCharger_mobile (loop + blow-off) nos esportivos ou com turbo da oficina.
// MOTO (porte do moto.js): esterço e inclinação do Grau Brasil, grau como pêndulo invertido no eixo traseiro, motor de 5 marchas com curva de torque.
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public static class FBCarSound
    {
        public const float MIN_RPM = 1000, MAX_RPM = 7000, UP_RPM = 6900, DOWN_RPM = 3000, SHIFT_DELAY = .35f, INERTIA = .1f; public const int GEARS = 5;
        public static readonly string[] LAYERS = { "idle", "low_on", "med_on", "high_on", "very_high_on", "maxRPM" };
        static readonly Dictionary<string, string[]> P = new()
        {
            ["boxer"] = new[] { "Fusca", "Kombi" },
            ["i4vw"] = new[] { "GolG2", "GolQuadrado", "GolG5", "Up", "SaveiroG4", "SaveiroQuadrada", "SaveiroCross", "Santana2002", "ParatiSurf1994", "CrossFox", "Golf", "Jetta2012", "Nivus" },
            ["i4sport"] = new[] { "CivicLXR_2014", "Corolla2018", "CivicG10", "i30" },
            ["i6"] = new[] { "OmegaCD1997", "OpalaDiplomata85" },
            ["diesel"] = new[] { "S10_2017", "Hilux2023Conquest", "L200", "F250", "RAM3500", "Amarok", "Sw4", "Toro" },
        };
        public static readonly Dictionary<string, string> HORN = new() { ["boxer"] = "reb_horn_fusca", ["diesel"] = "reb_horn_caminhonetes", ["i6"] = "reb_horn_chevette", ["i4"] = "reb_horn_palio", ["i4vw"] = "reb_horn_gol_saveiro", ["i4sport"] = "reb_horn_gol_saveiro" };
        // BUZINAS (pedido 26/10: "precisam ser trocadas e ter mais variedades"): cada carro tem a SUA, sorteada no conjunto da categoria e
        // fixa pelo syncId (todo mundo ouve a mesma). De segurar = loop (as de fábrica do REB, as do 171 com o loop fechado no período do
        // tom e variações de ±2–3 semitons — tools/re/horns.py); o trânsito toca padrões curtos com a mesma buzina do carro (Honk)
        // r45: "horn_popular*" e "horn_toque3" eram a CAMPAINHA DE BICICLETA do 171 (FMOD Veiculos/Bicicleta/buzina_normalizado, pico em 8,3 kHz) — removidas
        static readonly string[] H_CARRO = { "reb_horn_gol_saveiro", "reb_horn_palio", "horn_sedan", "horn_sedan_grave", "horn_palio_grave", "horn_gol_agudo", "reb_horn_chevette" },
            H_VELHO = { "reb_horn_fusca", "reb_horn_chevette", "horn_palio_grave" }, H_PICAPE = { "reb_horn_caminhonetes", "reb_horn_caminhonetesv2", "horn_caminhonete_grave", "horn_sedan_grave" },
            H_GRANDE = { "horn_navio", "horn_caminhonete_grave", "reb_horn_caminhonetesv2" }, H_POLICIA = { "horn_policia1", "horn_policia2", "horn_policia3" }, H_MOTO = { "horn_moto", "horn_moto_agudo", "horn_moto_grave" };   // buzina de MOTO do 171 (o MX Grau não tem buzina)
        public static string HornFor(FBCar u)
        {
            if (!u) return "reb_horn_palio"; if (u.horn != null) return u.horn; string id = u.id ?? "";
            var pool = u.kind == "moto" ? H_MOTO : id.Contains("Viatura") ? H_POLICIA : id == "F250" || id == "RAM3500" ? H_GRANDE
                : System.Text.RegularExpressions.Regex.IsMatch(id, "Fusca|Kombi|Chevet|Opala|Brasilia|Variant|Kadett") ? H_VELHO : u.heavy || System.Array.IndexOf(P["diesel"], id) >= 0 ? H_PICAPE : H_CARRO;
            uint h = 2166136261; foreach (char ch in (u.syncId ?? "") + "|" + id) h = (h ^ ch) * 16777619;
            var ok = System.Array.FindAll(pool, FBAudio.Has); return u.horn = ok.Length > 0 ? ok[h % (uint)ok.Length] : "reb_horn_palio";
        }
        // toque do trânsito: "bi", "bi-bi" ou "biiii" (mais nervoso = mais longo/repetido) com a buzina do próprio carro, no carro
        public static void Honk(FBCar u, float aggr = .5f)
        {
            if (!u) return; var c = FBAudio.Clip(HornFor(u)); if (!c) return; float r = Random.value + aggr * .5f;
            var pat = r < .55f ? new[] { (0f, .16f) } : r < 1.05f ? new[] { (0f, .14f), (.24f, .16f) } : new[] { (0f, .75f + aggr * .5f) };
            double t0 = AudioSettings.dspTime + .02;
            foreach (var (at, dur) in pat)
            {
                var s = FBAudio.PlayAt(c, u.transform.position + Vector3.up, .85f, 10, .02f); if (!s) continue;
                s.Stop(); s.loop = true; s.PlayScheduled(t0 + at); s.SetScheduledEndTime(t0 + at + dur);
            }
        }
        public static readonly HashSet<string> TURBO = new() { "i4sport", "i4_Evo_Sport", "i4_240X_Sport", "i6_GTR", "V6_GTR", "i6_2JX_Sport", "i4_Ricer_1" };
        public static bool Sporty(string prof) => prof != null && System.Text.RegularExpressions.Regex.IsMatch(prof, "^(v8|i4sport)$|V8|v8_|V10|v10|V12|v12|Rotary|rotary|Evo|GTR|2JX|240X|Ricer|Flat6");
        // ronco de FÁBRICA do REB (Carro/Ronco.roncoInicial, tools/reb/peek_roncos.py) — antes era um dos 8 perfis por família de carro
        public static string ProfileFor(string id, FBCar u)
        {
            if (u != null && u.ie) return u.kind == "moto" ? null : "diesel";
            var reb = FBReb.Ronco(id); if (reb != null) return reb;
            foreach (var kv in P) if (System.Array.IndexOf(kv.Value, id) >= 0) return kv.Key;
            return u != null && u.heavy ? "diesel" : "i4";
        }
        // curva do Unity serializada [t, v, tanIn, tanOut] (Hermite); fora do intervalo fica na ponta
        public static float EvalCurve(List<object> K, float t)
        {
            if (K == null || K.Count == 0) return 0;
            float[] A(int i) { var k = K[i] as List<object>; return new[] { Json.Fv(k[0]), Json.Fv(k[1]), Json.Fv(k[2]), Json.Fv(k[3]) }; }
            var f = A(0); if (t <= f[0]) return f[1]; var L = A(K.Count - 1); if (t >= L[0]) return L[1];
            for (int i = 0; i < K.Count - 1; i++)
            {
                var a = A(i); var b = A(i + 1); if (t > b[0]) continue; float dt = b[0] - a[0]; if (dt <= 0) return b[1];
                float s = (t - a[0]) / dt, s2 = s * s, s3 = s2 * s, m0 = float.IsFinite(a[3]) ? a[3] * dt : 0, m1 = float.IsFinite(b[2]) ? b[2] * dt : 0;
                return (2 * s3 - 3 * s2 + 1) * a[1] + (s3 - 2 * s2 + s) * m0 + (-2 * s3 + 3 * s2) * b[1] + (s3 - s2) * m1;
            }
            return L[1];
        }
        static float MaxLimit => Json.F(GameData.RES, "maxRPMLimit", 7000);
        // CÂMBIO (revisto 26/10: "soem corretamente com passar de marchas, aceleração, freios, parado"). Antes: marchas em degraus iguais
        // (1ª curtíssima, 4ª→5ª quase sem queda), subia sempre a 6900 e o giro pulava na hora. Agora:
        //  · relações escalonadas (1ª curta, 5ª longa) sobre a máxima DO CARRO; em 5ª na máxima fica a ~6600 rpm (sem bater no corte);
        //  · troca pelo pé: acelerando fundo sobe perto do corte, acelerando pouco sobe cedo (2600–6900); solto/freando desce em 1700–3200;
        //  · na troca o giro cai pela inércia (não pula), e quem chama corta a força/ronco por SHIFT_DELAY (shiftT > 0);
        //  · parado: marcha lenta com leve oscilação; acelerando parado sobe o giro (ponto morto).
        static readonly float[] RATIO = { .30f, .50f, .69f, .87f, 1.06f };
        public class Gearbox
        {
            public float vmax, rpm = MIN_RPM, shiftT, idleT; public int gear = 1;
            public Gearbox(float vmax) { this.vmax = Mathf.Max(5, vmax); }
            public float GearTop(int g) => vmax * RATIO[Mathf.Clamp(g, 1, GEARS) - 1];
            public float RpmAt(float sp, int g) => sp / GearTop(g) * MAX_RPM;
            public bool Shifting => shiftT > 0;
            public (float rpm, int gear, int shifted) Step(float dt, float v, float throttle)
            {
                float sp = Mathf.Abs(v); int shifted = 0; if (v < -.5f) gear = 1; throttle = Mathf.Clamp01(throttle);
                float target = RpmAt(sp, gear);
                if (sp < 1.2f) { idleT += dt; target = Mathf.Max(target, MIN_RPM * (1 + .025f * Mathf.Sin(idleT * 5.3f)) + throttle * MAX_RPM * .42f); }   // marcha lenta / acelerando parado
                if (shiftT > 0) { shiftT -= dt; target = Mathf.Min(target, rpm); }                                                  // embreagem: o giro não sobe durante a troca
                target = Mathf.Clamp(target, MIN_RPM, MAX_RPM);
                float inertia = shiftT > 0 ? .07f : target > rpm ? INERTIA * (1.4f - throttle * .6f) : INERTIA * 1.6f;              // sobe com o pé, cai mais devagar
                rpm += (target - rpm) * (1 - Mathf.Exp(-dt / Mathf.Max(.02f, inertia)));
                if (shiftT <= 0 && v > .5f)
                {
                    float upAt = Mathf.Lerp(2600, UP_RPM, Mathf.Pow(throttle, 1.5f)), downAt = Mathf.Lerp(1700, 3200, throttle);
                    if (gear < GEARS && throttle > .05f && rpm >= upAt && RpmAt(sp, gear + 1) > 1500) { gear++; shifted = 1; shiftT = SHIFT_DELAY; }
                    else if (gear > 1 && RpmAt(sp, gear) < downAt && RpmAt(sp, gear - 1) < UP_RPM * .9f) { gear--; shifted = -1; shiftT = SHIFT_DELAY * .6f; }
                }
                if (rpm > MAX_RPM * .985f && throttle > 0) rpm -= MAX_RPM * .03f * Random.value;                                   // corte de giro
                return (rpm, gear, shifted);
            }
        }
        public static void EngineAudio(string key, string profile, float rpm, float throttle, Vector3 pos, float vol = .42f, float refD = 4, float rolloff = .8f, bool turbo = false)
        {
            var pr = Json.O(Json.O(GameData.RES, "profiles"), profile) ?? Json.O(Json.O(GameData.RES, "profiles"), "i4"); if (pr == null) return;
            float n = Mathf.Min(1, rpm / MaxLimit), g = vol * (.6f + .4f * Mathf.Min(1, throttle));
            foreach (var l in LAYERS)
            {
                var c = Json.L(pr, l); if (c == null) continue;
                float lv = Mathf.Clamp(EvalCurve(c[0] as List<object>, n), 0, 1.2f), pt = Mathf.Max(.05f, EvalCurve(c[1] as List<object>, n));
                FBAudio.SetLoop(key + l, $"reb_eng_{profile}_{l}", lv * g, pos, pt, refD, rolloff);
            }
            if (TURBO.Contains(profile) || turbo)
            {
                var T = Json.O(GameData.RES, "turbo");
                FBAudio.SetLoop(key + "turbo", "reb_turbo_loop", EvalCurve(Json.L(T, "loopVol"), n) * Mathf.Min(1, throttle) * vol, pos, EvalCurve(Json.L(T, "loopPitch"), n), refD, rolloff);
            }
        }
        public static void StopEngine(string key) { foreach (var l in LAYERS) FBAudio.StopLoop(key + l); FBAudio.StopLoop(key + "turbo"); }
        public static void TurboBlow(string profile, float rpm, Vector3 pos, bool turbo = false)
        {
            if (!TURBO.Contains(profile ?? "") && !turbo) return; var T = Json.O(GameData.RES, "turbo"); float n = rpm / MaxLimit, v = EvalCurve(Json.L(T, "blowVol"), n);
            if (v > .05f) { var s = FBAudio.PlayAt(n > .75f ? "reb_turbo_blow_long" : "reb_turbo_blow_short", pos, .5f * v, 4); if (s) s.pitch = EvalCurve(Json.L(T, "blowPitch"), n); }
        }
    }

    public static class FBMoto
    {
        public const float BALANCE = 40 * Mathf.Deg2Rad, FAIL = 62 * Mathf.Deg2Rad, LEAN_MAX = 45 * Mathf.Deg2Rad, LEAN_SPEED = 8, STEER_SPEED = 20, G = 3.4f, UP = 3.3f, DOWN = 5, DRAG = 4.4f, SOFT = 2.2f, POP = .9f,
            MIN_SPEED = 1.5f, FULL_SPEED = 5.5f, THR_RATE = 4, SCORE_MIN = 17 * Mathf.Deg2Rad, LOW_MIN = 5 * Mathf.Deg2Rad, SIDE_LEAN = 25 * Mathf.Deg2Rad, STEER_IN_WHEELIE = .55f, FALL_TIME = 1.6f;
        public static readonly float[] STEER_DEG = { 45, 18 }, TRICK = { 25, 30 };
        public class Wheelie { public float a, w, thr, fall, dist, best, last, peak, t, trick; public bool key; }
        public class Engine { public int gear = 1; public float shift, rpm = .2f; }
        public struct Ev { public string kind; public float power, dist, time, peak; }
        public static Wheelie NewWheelie() => new();
        public static Engine NewEngine() => new();
        public static float SteerAngle(float vReal) { float t = Mathf.Clamp01(Mathf.Abs(vReal) / STEER_SPEED); return (STEER_DEG[0] + (STEER_DEG[1] - STEER_DEG[0]) * t) * Mathf.Deg2Rad; }
        public static float LeanTarget(float steerN, float vReal, bool inWheelie = false)
        {
            float l = Mathf.Clamp(steerN, -1, 1) * LEAN_MAX * Mathf.Clamp01(Mathf.Abs(vReal) / LEAN_SPEED);
            return Mathf.Clamp(l, -(inWheelie ? SIDE_LEAN : LEAN_MAX), inWheelie ? SIDE_LEAN : LEAN_MAX);
        }
        public static List<Ev> StepWheelie(Wheelie S, bool key, float thr, float brake, float vReal, float dt)
        {
            var ev = new List<Ev>(); float v = Mathf.Abs(vReal);
            if (S.fall > 0) { S.fall -= dt; S.a += (0 - S.a) * Mathf.Min(1, dt * 3); S.w = 0; S.thr = 0; S.key = false; S.trick = 0; if (S.fall <= 0) { S.fall = 0; S.a = 0; } return ev; }
            float speedF = Mathf.Clamp01((v - MIN_SPEED) / (FULL_SPEED - MIN_SPEED));
            S.thr += (Mathf.Clamp01(thr) - S.thr) * Mathf.Min(1, dt * THR_RATE);
            bool edge = key && !S.key; S.key = key;
            if (S.a < .02f && edge && thr > .5f && speedF > .3f) { S.w += POP; ev.Add(new Ev { kind = "start" }); }
            float up = key ? UP * S.thr * speedF : 0, down = DOWN * Mathf.Clamp01(brake), grav = S.a > .001f || S.w > 0 ? -G * Mathf.Sin(BALANCE - S.a) : 0;
            S.w += (up - down + grav - DRAG * S.w - (key ? 0 : SOFT * S.w)) * dt;
            float was = S.a; S.a += S.w * dt;
            if (S.a <= 0) { if (S.w < -.8f && was > .12f) ev.Add(new Ev { kind = "land", power = -S.w }); S.a = 0; S.w = 0; }
            if (S.a > S.peak) S.peak = S.a;
            if (S.a > FAIL) { ev.Add(new Ev { kind = "loop", dist = S.dist }); S.fall = FALL_TIME; S.w = 0; S.dist = 0; S.peak = 0; S.t = 0; return ev; }
            if (S.a > SCORE_MIN) { S.dist += v * dt; S.t += dt; }
            else if (S.a < LOW_MIN && (S.dist > 0 || S.t > 0))
            {
                if (S.dist > 4) { S.last = S.dist; S.best = Mathf.Max(S.best, S.dist); ev.Add(new Ev { kind = "end", dist = S.dist, time = S.t, peak = S.peak }); }
                S.dist = 0; S.t = 0; S.peak = 0;
            }
            float deg = S.a * Mathf.Rad2Deg; S.trick = deg >= TRICK[0] ? Mathf.Clamp01((deg - TRICK[0]) / 15) : 0;
            return ev;
        }
        static readonly float[][] CURVE = { new[] { 0f, 300 }, new[] { .17f, 449 }, new[] { 1f, 350 } }; static readonly float[] RATIO = { 1.55f, 1.2f, 1, .86f, .74f };
        public static float TorqueAt(float t)
        {
            t = Mathf.Clamp01(t); for (int i = 1; i < CURVE.Length; i++) if (t <= CURVE[i][0]) { var a = CURVE[i - 1]; var b = CURVE[i]; float k = (t - a[0]) / (b[0] - a[0]); return (a[1] + (b[1] - a[1]) * k) / 449; }
            return CURVE[^1][1] / 449;
        }
        public static float EngineStep(Engine E, float v, float thr, float dt, float vmax, float acc)
        {
            float rel = Mathf.Clamp01(v / vmax), span = 1f / 5;
            E.rpm = Mathf.Clamp01((rel - (E.gear - 1) * span) / span) * .8f + .2f + thr * .05f;
            if (E.shift > 0) { E.shift -= dt; E.rpm = Mathf.Max(.2f, E.rpm - .25f); }
            else if (E.gear < 5 && E.rpm > .93f) { E.gear++; E.shift = .16f; }
            else if (E.gear > 1 && E.rpm < .12f + .2f && (rel - (E.gear - 1) * span) / span < .12f) E.gear--;
            return E.shift > 0 ? 0 : acc * TorqueAt(rel) * RATIO[E.gear - 1] * thr * (1 - rel * .35f);
        }
        public static float YawRate(float vWorld, float steer, float lean, float wheelbase)
        {
            float v = Mathf.Abs(vWorld) / 1.2f, kin = vWorld / wheelbase * Mathf.Tan(steer), g = 9.8f * 1.2f;
            float byLean = -Mathf.Sign(vWorld) * g * Mathf.Tan(Mathf.Clamp(lean, -.78f, .78f)) / Mathf.Max(1, Mathf.Abs(vWorld));
            float k = Mathf.Clamp01((v - 2.5f) / 5); return kin * (1 - k) + byLean * k;
        }
    }
}
