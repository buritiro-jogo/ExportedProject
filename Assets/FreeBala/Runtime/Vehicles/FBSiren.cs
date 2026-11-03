// SIRENE DAS VIATURAS (r45). Antes tocava pela acústica do paredão (feita para o som ser ouvido no bairro inteiro: reverb da rua 2D que
// domina de longe -> "a sirene soa do meu lado mesmo longe"). Agora cada viatura tem UMA fonte 3D própria no teto:
//  · sons do 171 (Carros/SireneLoop = padrão, SireneRapidaLoop = perseguição, Sirene_Wail = lenta; Veiculos/Sirene_Yelp/Pierce = toque curto),
//    loops com a emenda cruzada (tools/re/audio171.py LOOPS);
//  · atenuação 1/r de verdade a partir de 6 m (-20 dB a 60 m, -30 dB a 190 m), some aos 320 m; o ar come os agudos com a distância;
//  · prédio entre a viatura e o ouvido abafa (passa-baixa + -6 dB), dentro da própria viatura a cabine abafa;
//  · Doppler leve (0,25: perceptível na passagem sem "deslizar" o tom quando a câmera gira);
//  · uma fonte por viatura (ligar de novo só troca o modo), e desligar para na hora (com 60 ms de rampa, sem estalo);
//  · o giroflex acompanha: sirene ligada = giroflex ligado (o giroflex pode ficar sozinho, sem sirene).
// Jogador numa viatura: B troca o modo (desligada > padrão > rápida > lenta > desligada), SHIFT+B liga/desliga só o giroflex.
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public static class FBSiren
    {
        public static readonly string[] MODES = { null, "police_siren_loop", "police_siren_rapida", "police_siren_wail" };
        static readonly string[] NAMES = { "DESLIGADA", "PADRÃO", "RÁPIDA", "LENTA" };
        public const float NEAR = 6, FAR = 320;
        class S { public FBCar car; public AudioSource src; public AudioLowPassFilter lp; public int mode; public float vol = 1, gain, occK, ot; public bool occ, stopping; public FBGiroflex.Bar bar; public bool ownBar; }
        static readonly Dictionary<FBCar, S> all = new();
        static AnimationCurve curve;

        public static bool IsPolice(FBCar c) => c && c.kind != "moto" && c.id != null && (c.id.Contains("Viatura") || c.id.Contains("Caveirao") || c.id.Contains("Caveirão"));
        public static int Mode(FBCar c) => c && all.TryGetValue(c, out var s) && !s.stopping ? s.mode : 0;
        public static bool Lights(FBCar c) => c && all.TryGetValue(c, out var s) && FBGiroflex.Alive(s.bar) && s.bar.on;

        // liga (mode 1..3) / desliga (0). vol: 0..1 (o baculejo usa para sumir aos poucos na saída)
        public static void Set(FBCar car, int mode, float vol = 1)
        {
            if (!car) return; _ = R;
            if (!all.TryGetValue(car, out var s)) { if (mode <= 0) return; all[car] = s = new S { car = car }; }
            s.vol = Mathf.Clamp01(vol);
            if (mode <= 0) { s.stopping = true; return; }
            mode = Mathf.Clamp(mode, 1, MODES.Length - 1); var clip = FBAudio.Clip(MODES[mode]); if (!clip) return;
            if (!s.src) Make(s);
            if (s.mode != mode || !s.src.isPlaying || s.stopping) { s.src.clip = clip; s.src.time = Random.value * clip.length * .5f; s.src.Play(); }
            s.mode = mode; s.stopping = false; Giro(s, true);
        }
        public static void Volume(FBCar car, float vol) { if (car && all.TryGetValue(car, out var s)) s.vol = Mathf.Clamp01(vol); }
        // giroflex sem sirene (viatura parada na abordagem, por exemplo)
        public static void SetLights(FBCar car, bool on)
        {
            if (!car) return; _ = R;
            if (!all.TryGetValue(car, out var s)) { if (!on) return; all[car] = s = new S { car = car, stopping = true }; }
            Giro(s, on);
        }
        // toque curto ("uóp") — chegada, pedido de passagem
        public static void Burst(FBCar car, bool pierce = false)
        {
            if (!car) return; var c = FBAudio.Clip(pierce ? "police_siren_pierce" : "police_siren_yelp") ?? FBAudio.Clip("police_siren_yelp"); if (!c) return;
            var g = new GameObject("sirene_toque"); g.transform.SetParent(car.transform, false); g.transform.position = Roof(car);   // fonte própria (o pool do FBAudio não leva o Doppler/curva da sirene)
            var a = g.AddComponent<AudioSource>(); a.playOnAwake = false; a.priority = 8; a.clip = c; a.volume = .9f * FBAudio.VehVol; Shape(a); a.Play(); Object.Destroy(g, c.length + .1f);
        }
        public static void Stop(FBCar car) { if (car && all.TryGetValue(car, out var s)) Kill(s); }
        public static void Reset() { foreach (var s in new List<S>(all.Values)) Kill(s); all.Clear(); }

        static Vector3 Roof(FBCar car) { var b = car.lbox; return car.transform.TransformPoint(new Vector3(b.center.x, b.max.y + .1f, b.center.z + b.size.z * .05f)); }
        static void Shape(AudioSource a)
        {
            if (curve == null)
            {
                curve = new AnimationCurve();
                for (int i = 0; i <= 48; i++)
                {
                    float d = FAR * i * i / (48f * 48f), g = d <= NEAR ? 1 : NEAR / d, tail = Mathf.Clamp01((FAR - d) / (FAR * .25f));
                    curve.AddKey(new Keyframe(d / FAR, g * tail * tail));
                }
                for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0);
            }
            a.spatialBlend = 1; a.rolloffMode = AudioRolloffMode.Custom; a.maxDistance = FAR; a.minDistance = NEAR; a.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curve);
            a.dopplerLevel = .25f; a.spread = 25; a.outputAudioMixerGroup = FBAudio.Group(false);
        }
        static void Make(S s)
        {
            var g = new GameObject("sirene"); g.transform.SetParent(s.car.transform, false);
            var b = s.car.lbox; g.transform.localPosition = new Vector3(b.center.x, b.max.y + .1f, b.center.z + b.size.z * .05f);
            var a = g.AddComponent<AudioSource>(); a.playOnAwake = false; a.loop = true; a.priority = 8; a.volume = 0; Shape(a);
            s.lp = g.AddComponent<AudioLowPassFilter>(); s.lp.cutoffFrequency = 20000; s.lp.lowpassResonanceQ = 1; s.src = a; s.gain = 0;
        }
        static void Giro(S s, bool on)
        {
            if (s.bar != null && !FBGiroflex.Alive(s.bar)) s.bar = null;   // FBStreetEvents.OnDestroy zera os giroflex
            if (on && s.bar == null) { s.bar = FBGiroflex.Find(s.car); s.ownBar = s.bar == null; if (s.ownBar) s.bar = FBGiroflex.Attach(s.car); }
            if (s.bar != null) s.bar.on = on;
        }
        static void Kill(S s)
        {
            if (s.src) Object.Destroy(s.src.gameObject); s.src = null;
            if (s.bar != null) { if (s.ownBar) FBGiroflex.Detach(s.bar); else s.bar.on = false; } s.bar = null; all.Remove(s.car);
        }

        static Runner runner; static Runner R { get { if (!runner) { var g = new GameObject("FBSiren"); Object.DontDestroyOnLoad(g); runner = g.AddComponent<Runner>(); } return runner; } }
        class Runner : MonoBehaviour
        {
            // [parceiro] v1 (thread de física): tempo do LateUpdate medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
            static readonly int __fbt_LateUpdate = FBTempo.Id("FBSiren.Runner.LateUpdate");
            void LateUpdate()
            {
                if (!FBTempo.On) { LateUpdate__fbt(); return; }
                long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
            }
            void LateUpdate__fbt() { Tick(Time.deltaTime); }
        }
        static readonly List<S> dead = new();
        static void Tick(float dt)
        {
            var ear = FBAudio.Ear; Vector3 cam = ear ? ear.position : Vector3.zero; dead.Clear();
            foreach (var s in all.Values)
            {
                if (!s.car) { dead.Add(s); continue; }
                if (!s.src) { if (s.stopping && (!FBGiroflex.Alive(s.bar) || !s.bar.on)) dead.Add(s); continue; }
                float want = s.stopping ? 0 : s.vol;
                s.gain = Mathf.MoveTowards(s.gain, want, dt / .06f);                     // rampa de 60 ms: liga/desliga sem estalo
                if (s.stopping && s.gain <= 0) { Object.Destroy(s.src.gameObject); s.src = null; s.mode = 0; if (!FBGiroflex.Alive(s.bar) || !s.bar.on) dead.Add(s); continue; }
                var p = s.src.transform.position; float d = Vector3.Distance(p, cam);
                bool inside = FBDrive.Cur == s.car && s.car.lbox.Contains(s.car.transform.InverseTransformPoint(cam));
                if ((s.ot += dt) > .25f) { s.ot = 0; s.occ = !inside && d > 5 && d < FAR && FBParedao.Occluded(cam, p); }
                s.occK = Mathf.MoveTowards(s.occK, s.occ ? 1 : 0, dt * 3);
                float air = Mathf.Max(1800, 20000 * Mathf.Exp(-d / 140)), cut = inside ? 4200 : Mathf.Lerp(air, Mathf.Min(air, 900), s.occK);
                s.lp.cutoffFrequency = Mathf.Lerp(s.lp.cutoffFrequency, cut, Mathf.Min(1, dt * 8));
                s.src.volume = s.gain * (inside ? .5f : 1) * (1 - .5f * s.occK) * FBAudio.VehVol;
                s.src.spatialBlend = inside ? .35f : 1;                                    // dentro: vem "do teto", sem pular de lado ao virar a cabeça
            }
            foreach (var s in dead) { if (s.bar != null && s.ownBar && !s.car) FBGiroflex.Detach(s.bar); all.Remove(s.car); }
            FBGiroflex.Tick(dt);
        }

        // jogador numa viatura
        public static void PlayerControl(FBCar car)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current; if (kb == null || !IsPolice(car) || !kb.bKey.wasPressedThisFrame) return;
            if (kb.shiftKey.isPressed) { bool on = !Lights(car); SetLights(car, on); FBHUD.I?.Center(on ? "GIROFLEX LIGADO" : "GIROFLEX DESLIGADO", 1); return; }
            int m = (Mode(car) + 1) % MODES.Length; Set(car, m);
            if (m == 0) SetLights(car, false);
            FBHUD.I?.Center("SIRENE: " + NAMES[m], 1);
        }
        public static string Diag(FBCar car)
        {
            if (!car || !all.TryGetValue(car, out var s)) return "sem sirene"; var ear = FBAudio.Ear; float d = ear && s.src ? Vector3.Distance(ear.position, s.src.transform.position) : -1;
            float g = s.src ? s.src.volume * (d <= NEAR ? 1 : curve.Evaluate(Mathf.Clamp01(d / FAR))) : 0;
            return $"modo {s.mode} · tocando {(s.src && s.src.isPlaying)} · d {d:0.0} m · ganho efetivo {g:0.000} ({(g > 0 ? 20 * Mathf.Log10(g) : -99):0.0} dB) · corte {(s.lp ? s.lp.cutoffFrequency : 0):0} Hz · prédio {s.occK:0.0} · giroflex {(s.bar != null && s.bar.on)}";
        }
    }
}
