// Áudio (porte do audio.js): sons 2D e posicionais com pool fixo de AudioSources (sem criar fonte no meio do tiroteio), variação de
// pitch (jitter), distância de referência por som (ref), loops com volume suave (passos, ambiente, motor), barramentos SFX/música
// (volume por categoria; se existir Resources/Audio/FBMixer com grupos "SFX" e "Voz", as fontes saem nesses grupos), e os efeitos SINTETIZADOS do jogo (contagem do 4V4, rodada, compra, kit
// médico, vidro, hitmarker de corpo/cabeça, zona, vitória/derrota) gerados em PCM com os mesmos osciladores/ruídos/envelopes.
// Clipes: Resources/Audio/sfx (assets/audio), Resources/Audio/wsnd (assets/wsnd), Resources/Audio/<pasta> (veículos, Sigma...).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public static class FBAudio
    {
        // r47 (pedido 30/10: "vários botões de áudio não respondem"). Causa medida no código: o volume geral só entrava no som na hora do
        // Play (loop já tocando e música do menu ficavam com o valor antigo), a música do menu saía pela categoria de efeitos, e moto MX,
        // helicóptero, passos, aviões e obras nem passavam por ele. Agora: GERAL = AudioListener.volume (vale para TODA fonte do jogo, na
        // hora); categorias multiplicam o volume de cada fonte e são reaplicadas na hora nas fontes do pool, nos loops e nas registradas (Track).
        public enum Cat { Sfx, Music, Voice, Vehicle, Ambient }
        public static float Master = .9f, SfxVol = 1, MusicVol = .5f, VoiceVol = 1, VehVol = 1, AmbVol = 1;
        public static float Vol(Cat c) => c switch { Cat.Music => MusicVol, Cat.Voice => VoiceVol, Cat.Vehicle => VehVol, Cat.Ambient => AmbVol, _ => SfxVol };
        public static int Applied;                                                                     // laboratório (-fbsomlab): quantas vezes os volumes foram reaplicados
        public static void SetVolumes(float master, float sfx, float music, float voice, float veh, float amb)
        {
            Master = Mathf.Clamp01(master); SfxVol = Mathf.Clamp01(sfx); MusicVol = Mathf.Clamp01(music); VoiceVol = Mathf.Clamp01(voice); VehVol = Mathf.Clamp01(veh); AmbVol = Mathf.Clamp01(amb);
            AudioListener.volume = Master; Applied++;
            for (int i = 0; i < pool2.Count; i++) if (pool2[i] && pool2[i].isPlaying) pool2[i].volume = Mathf.Clamp01(base2[i] * Vol(cat2[i]));
            for (int i = 0; i < pool3.Count; i++) if (pool3[i] && pool3[i].isPlaying) pool3[i].volume = Mathf.Clamp01(base3[i] * Vol(cat3[i]));
            foreach (var l in loops.Values) l.target = l.raw * Vol(l.cat);
            for (int i = tracked.Count - 1; i >= 0; i--) { var (s, b, c) = tracked[i]; if (!s) { tracked.RemoveAt(i); continue; } s.volume = Mathf.Clamp01(b * Vol(c)); }
        }
        // fonte própria de outro sistema com volume fixo (ambiente das obras, avião): o volume base fica guardado e acompanha a categoria
        static readonly List<(AudioSource s, float b, Cat c)> tracked = new();
        public static void Track(AudioSource s, float baseVol, Cat c) { if (!s) return; tracked.RemoveAll(x => !x.s || x.s == s); tracked.Add((s, baseVol, c)); s.volume = Mathf.Clamp01(baseVol * Vol(c)); }
        static float[] base2 = new float[12], base3 = new float[32]; static Cat[] cat2 = new Cat[12], cat3 = new Cat[32];
        // mixer opcional (o coordenador cria Resources/Audio/FBMixer.mixer com grupos "SFX" e "Voz"); sem ele tudo sai direto no ouvinte
        static bool mixTried; static UnityEngine.Audio.AudioMixerGroup sfxG, voiceG;
        public static UnityEngine.Audio.AudioMixerGroup Group(bool voice)
        {
            if (!mixTried) { mixTried = true; var m = Resources.Load<UnityEngine.Audio.AudioMixer>("Audio/FBMixer"); if (m) { sfxG = m.FindMatchingGroups("SFX") is { Length: > 0 } a ? a[0] : null; voiceG = m.FindMatchingGroups("Voz") is { Length: > 0 } b ? b[0] : sfxG; } }
            return voice ? voiceG : sfxG;
        }
        static readonly Dictionary<string, AudioClip> cache = new(); static readonly HashSet<string> missing = new();
        static GameObject root; static readonly List<AudioSource> pool3 = new(), pool2 = new(); static int i3, i2;
        class Loop { public AudioSource src; public float target, cur, raw; public string name; public Cat cat; }   // cur: volume já aplicado na fonte (nasce 0)
        static readonly Dictionary<string, Loop> loops = new();
        static readonly string[] Dirs = { "User/", "", "sfx/", "Weapons/", "wsnd/", "veh/", "sg/", "amb/", "vo/" };

        // SONS DO 171 (rodada 39, autorizado pelo usuário — sujeito à autorização da Betagames): Resources/Audio/171/aliases.json troca o som
        // pedido pelo jogo por variações do 171 (sorteia uma a cada toque) e traz a tabela por arma. Apagar a pasta volta aos sons antigos.
        static Dictionary<string, string[]> alias; static Dictionary<string, object> pack171; static bool aliasTried;
        static void LoadPack171()
        {
            if (aliasTried) return; aliasTried = true; var ta = Resources.Load<TextAsset>("Audio/171/aliases"); if (!ta) return;
            pack171 = Json.Parse(ta.text) as Dictionary<string, object>; alias = new Dictionary<string, string[]>();
            foreach (var kv in Json.O(pack171, "aliases") ?? new Dictionary<string, object>()) { var l = (kv.Value as List<object>)?.Select(o => o as string).Where(s => s != null).ToArray(); if (l != null && l.Length > 0) alias[kv.Key] = l; }
        }
        static AudioClip Clip171(string n) { if (cache.TryGetValue(n, out var c)) return c; c = Resources.Load<AudioClip>("Audio/171/" + n); if (c) cache[n] = c; return c; }
        /// <summary>Som do 171 para a arma (slot: fire, far, reload, empty) — por id, senão pela classe; null = usa o som antigo.</summary>
        public static string Weapon171(string id, string cls, string slot)
        {
            LoadPack171(); var A = Json.O(pack171, "armas"); if (A == null) return null;
            var n = Json.S(Json.O(A, id ?? ""), slot) ?? Json.S(Json.O(A, "class:" + (cls ?? "")), slot);
            return n != null && Clip171(n) ? n : null;
        }
        public static AudioClip Clip(string name)
        {
            if (string.IsNullOrEmpty(name)) return null; LoadPack171();
            if (alias != null && alias.TryGetValue(name, out var al)) { var a = Clip171(al[Random.Range(0, al.Length)]); if (a) return a; }
            if (name.StartsWith("x171_")) return Clip171(name);
            if (cache.TryGetValue(name, out var c)) return c; if (missing.Contains(name)) return null;
            foreach (var d in Dirs) { c = Resources.Load<AudioClip>("Audio/" + d + name); if (c) { cache[name] = c; return c; } }
            missing.Add(name); return null;
        }
        public static bool Has(string name) => Clip(name);
        static void Ensure()
        {
            if (root) return;
            // [parceiro] Correção: se o FB_Audio foi destruído, as listas ainda guardavam as fontes mortas e metade dos sons
            //  dava MissingReferenceException (e o erro interrompia quem chamou, como a saída do carro).
            pool3.Clear(); pool2.Clear();
            root = new GameObject("FB_Audio"); Object.DontDestroyOnLoad(root); root.AddComponent<FBAudioTicker>();
            for (int i = 0; i < 32; i++) pool3.Add(New3D(i));
            for (int i = 0; i < 12; i++) pool2.Add(New2D());
        }
        static AudioSource New3D(int i)
        {
            var s = new GameObject("3D" + i).AddComponent<AudioSource>(); s.transform.SetParent(root.transform); s.playOnAwake = false; s.spatialBlend = 1; s.rolloffMode = AudioRolloffMode.Custom; s.dopplerLevel = 0; s.maxDistance = 400; s.priority = 64; s.outputAudioMixerGroup = Group(false);
            var lp = s.gameObject.AddComponent<AudioLowPassFilter>(); lp.cutoffFrequency = 22000; lp.lowpassResonanceQ = 1; return s;
        }
        static AudioSource New2D() { var s = root.AddComponent<AudioSource>(); s.playOnAwake = false; s.spatialBlend = 0; s.priority = 32; s.outputAudioMixerGroup = Group(false); return s; }
        // [parceiro] Correção: uma fonte do grupo que foi destruída é trocada por uma nova em vez de quebrar o som.
        //  (k = índice usado, para o volume base/categoria guardados em base2/base3)
        static AudioSource Next(List<AudioSource> pool, ref int idx, bool is3D, out int k)
        {
            k = idx++ % pool.Count; if (!pool[k]) pool[k] = is3D ? New3D(k) : New2D(); return pool[k];
        }
        // curva "inverse" do WebAudio: g = ref / (ref + rolloff·(d − ref))
        static AnimationCurve Inverse(float refD, float roll, float max)
        {
            var k = new List<Keyframe>(); for (int i = 0; i <= 24; i++) { float d = Mathf.Lerp(0, max, Mathf.Pow(i / 24f, 2)); float g = d <= refD ? 1 : refD / (refD + roll * (d - refD)); k.Add(new Keyframe(d / max, g)); }
            return new AnimationCurve(k.ToArray());
        }
        public static AnimationCurve RolloffCurve(float refD, float roll, float max) => Inverse(refD, roll, max);   // fonte grande (helicóptero)
        static readonly Dictionary<int, AnimationCurve> curves = new();
        public static AnimationCurve Rolloff(float refD, float rolloff = 1.1f) { int key = Mathf.RoundToInt(refD * 10) * 100 + Mathf.RoundToInt(rolloff * 10); if (!curves.TryGetValue(key, out var cv)) curves[key] = cv = Inverse(refD, rolloff, 400); return cv; }

        public static AudioSource Play2D(string name, float vol = 1, float jitter = .08f, float? rate = null, bool music = false) { var c = Clip(name); return c ? Play2D(c, vol, jitter, rate, music) : null; }
        public static AudioSource Play2D(AudioClip c, float vol = 1, float jitter = .08f, float? rate = null, bool music = false)
        {
            if (!c || !float.IsFinite(vol)) return null; Ensure(); var s = Next(pool2, ref i2, false, out int k); var cat = music ? Cat.Music : Cat.Sfx; base2[k] = vol; cat2[k] = cat;
            s.clip = c; s.loop = false; s.volume = Mathf.Clamp01(vol * Vol(cat)); s.pitch = rate ?? 1 + (Random.value - .5f) * jitter; s.Play(); return s;
        }
        public static System.Action<string, Vector3, float> Played;                                   // laboratórios (-fbsomlab): quem tocou o quê
        public static AudioSource PlayAt(string name, Vector3 pos, float vol = 1, float refD = 4, float jitter = .08f, float dur = 0, float rolloff = 1.1f, Cat cat = Cat.Sfx) { Played?.Invoke(name, pos, vol); return PlayAt(Clip(name), pos, vol, refD, jitter, dur, rolloff, cat); }
        public static float LoopLevel(string key) => loops.TryGetValue(key, out var l) && l.src ? l.target : 0;
        public static string LoopClip(string key) => loops.TryGetValue(key, out var l) && l.src && l.src.clip ? l.src.clip.name : null;
        public static AudioSource PlayAt(AudioClip c, Vector3 pos, float vol = 1, float refD = 4, float jitter = .08f, float dur = 0, float rolloff = 1.1f, Cat cat = Cat.Sfx)   // clipe gerado em código (revoada dos pássaros)
        {
            if (!c || !float.IsFinite(pos.x + pos.y + pos.z)) return null; Ensure();
            var s = Next(pool3, ref i3, true, out int k); base3[k] = vol; cat3[k] = cat; s.transform.position = pos; s.clip = c; s.loop = false; s.volume = Mathf.Clamp01(vol * Vol(cat)); s.pitch = 1 + (Random.value - .5f) * jitter;
            var lpf = s.GetComponent<AudioLowPassFilter>(); if (lpf) lpf.cutoffFrequency = 22000;                 // abafado só quando pedido (Muffle)
            s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Rolloff(refD, rolloff)); s.Play(); if (dur > 0) s.SetScheduledEndTime(AudioSettings.dspTime + dur); return s;
        }
        // tiro/explosão 3D com o ar e a parede no caminho (rodada 37): longe perde agudos (absorção do ar), atrás de parede fica abafado e
        // mais baixo — dá para saber se o atirador está à vista ou escondido, e de que lado
        // prioridade (rodada 40): o Unity tem 64 vozes reais; tiros 16, sons pontuais 64, loops 3D (motor do trânsito, paredão) 170 — antes
        // tudo 128 e os tiros disputavam (e perdiam) vozes com os 6–7 loops de motor de cada carro do trânsito e as 8 fontes de cada paredão
        public static AudioSource PlayShotAt(string name, Vector3 pos, float vol, float refD, float rolloff = 1.5f)
        {
            var l = Listener(); float d = l ? Vector3.Distance(l.position, pos) : 0; bool occ = false;
            if (l && d > 1.5f) { var dir = (pos - l.position) / d; occ = Physics.Raycast(l.position, dir, d - .6f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore); }
            var s = PlayAt(name, pos, vol * (occ ? .62f : 1), refD, .08f, 0, rolloff); if (!s) return null; s.priority = 16;
            var lpf = s.GetComponent<AudioLowPassFilter>(); if (lpf) lpf.cutoffFrequency = Mathf.Lerp(occ ? 2600 : 22000, occ ? 900 : 5200, Mathf.InverseLerp(10, 180, d));
            return s;
        }
        static Transform listener; static Transform Listener() { if (!listener) { var al = Object.FindAnyObjectByType<AudioListener>(); listener = al ? al.transform : Camera.main ? Camera.main.transform : null; } return listener; }
        // loop contínuo com volume controlável (passos, ambiente, motor, paredão) — pos = null: 2D
        public static void SetLoop(string key, string name, float vol, Vector3? pos = null, float? rate = null, float refD = 6, float rolloff = 1.1f)
        {
            if (!float.IsFinite(vol)) vol = 0; Ensure();
            if (loops.TryGetValue(key, out var l) && (l.name != name || !l.src)) { StopLoop(key); l = null; }   // [parceiro 01/11] fonte do loop destruída (FB_Audio recriado): recria em vez de quebrar
            if (l == null)
            {
                var c = Clip(name); if (!c) return;
                var g = new GameObject("Loop_" + key); g.transform.SetParent(root.transform); var s = g.AddComponent<AudioSource>(); s.clip = c; s.loop = true; s.volume = 0; s.playOnAwake = false; s.priority = LoopPriority(key, pos.HasValue);
                s.spatialBlend = pos.HasValue ? 1 : 0; s.rolloffMode = AudioRolloffMode.Custom; s.maxDistance = 400; s.dopplerLevel = 0; s.outputAudioMixerGroup = Group(false); if (pos.HasValue) s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Inverse(refD, rolloff, 400));
                s.time = Random.value * c.length * .9f; s.Play(); loops[key] = l = new Loop { src = s, name = name, cat = LoopCat(key) };
            }
            l.raw = vol; l.target = vol * Vol(l.cat); if (rate.HasValue && float.IsFinite(rate.Value)) l.src.pitch = Mathf.Lerp(l.src.pitch, rate.Value, .15f); if (pos.HasValue) l.src.transform.position = pos.Value;
        }
        // categoria do loop pela chave: motor/ronco/pneu/buzina = VEÍCULOS; amb*/água = AMBIENTE; música do menu = MÚSICA; resto = EFEITOS
        static Cat LoopCat(string key) => key == "menu_music" ? Cat.Music : key.StartsWith("veh_") || key.StartsWith("lab_horn") ? Cat.Vehicle : key.StartsWith("amb") || key.StartsWith("agua_") ? Cat.Ambient : Cat.Sfx;
        // (r41) prioridade 170 em TODO loop 3D (r40) fazia o Unity calar o motor do PRÓPRIO carro em marcha lenta: 64 vozes reais, 37 carros do
        // trânsito × 6 camadas de motor competindo. Carro do jogador (veh_*, menos veh_remote_/veh_npc_) e paredão na frente; trânsito atrás.
        static int LoopPriority(string key, bool is3D)
        {
            if (key.StartsWith("veh_") && !key.StartsWith("veh_remote_") && !key.StartsWith("veh_npc_")) return 12;
            if (key.StartsWith("som_") || key.StartsWith("paredao")) return 40;
            return is3D ? 170 : 110;
        }
        public static void StopLoop(string key) { if (loops.TryGetValue(key, out var l)) { loops.Remove(key); if (l.src) Object.Destroy(l.src.gameObject); } }
        public static void StopAllLoops() { foreach (var k in new List<string>(loops.Keys)) StopLoop(k); }
        // (r41) só mexe no que ainda está indo para o alvo (o setter de volume é chamada nativa; com centenas de loops custava 0,6–2,6 ms)
        internal static void Tick(float dt)
        {
            float k = 1 - Mathf.Exp(-dt / .08f);
            foreach (var l in loops.Values)
            {
                if (!l.src || l.cur == l.target) continue;
                l.cur = Mathf.Abs(l.target - l.cur) < 1e-4f ? l.target : Mathf.Lerp(l.cur, l.target, k); l.src.volume = l.cur;
            }
        }

        // ---------------- sintetizados (PCM gerado uma vez por tipo e guardado)
        static readonly Dictionary<string, AudioClip> synth = new();
        static readonly Dictionary<string, string> SG = new() { ["tick"] = "sg_tick", ["count5"] = "sg_count5", ["beep"] = "", ["riser"] = "", ["round"] = "sg_cs_newround", ["lastround"] = "sg_cs_lastround", ["go"] = "sg_cs_round", ["buy"] = "sg_cs_buy", ["win"] = "sg_cs_victory", ["lose"] = "sg_match_defeat" };
        public static void Synth(string kind, int n = 0)
        {
            try
            {
                if (SG.TryGetValue(kind, out var sg) && Has("sg_count5")) { if (sg != "") Play2D(sg, kind == "tick" ? .55f : .8f, 0); return; }
                if (kind == "count5") return; if (kind == "lastround") kind = "round";
                var key = kind + n; if (!synth.TryGetValue(key, out var c)) synth[key] = c = Make(kind, n);
                if (c) Play2D(c, .85f, 0);
            }
            catch { }
        }
        const int SR = 44100;
        class Buf { public float[] d; public Buf(float s) { d = new float[Mathf.CeilToInt(SR * s)]; } }
        static float Env(float t, float vol, float atk, float dur) { if (t < 0 || t > dur) return 0; if (t < atk) return Mathf.Lerp(.0001f, vol, t / atk); return vol * Mathf.Pow(.0001f / Mathf.Max(.0002f, vol), (t - atk) / Mathf.Max(1e-3f, dur - atk)); }
        static void Osc(Buf b, string type, float f0, float f1, float t0, float dur, float vol, float atk = .004f)
        {
            double ph = 0; int s0 = (int)(t0 * SR), n = (int)((dur + .05f) * SR);
            for (int i = 0; i < n && s0 + i < b.d.Length; i++)
            {
                float t = i / (float)SR, f = f0 == f1 ? f0 : f0 * Mathf.Pow(f1 / f0, Mathf.Min(1, t / dur)); ph += f / SR; float p = (float)(ph - System.Math.Floor(ph));
                float w = type == "sine" ? Mathf.Sin(p * Mathf.PI * 2) : type == "square" ? (p < .5f ? 1 : -1) : type == "triangle" ? 1 - 4 * Mathf.Abs(p - .5f) : 2 * p - 1;
                b.d[s0 + i] += w * Env(t, vol, atk, dur);
            }
        }
        static void Noise(Buf b, float t0, float dur, float vol, string type, float f0, float f1, float q = 1, float atk = .003f)
        {
            int s0 = (int)(t0 * SR), n = (int)((dur + .05f) * SR); float y1 = 0, y2 = 0, x1 = 0, x2 = 0; var rnd = new System.Random(s0 + n);
            for (int i = 0; i < n && s0 + i < b.d.Length; i++)
            {
                float t = i / (float)SR, f = f0 == f1 ? f0 : f0 * Mathf.Pow(f1 / f0, Mathf.Min(1, t / dur)), x = (float)rnd.NextDouble() * 2 - 1;
                float w0 = 2 * Mathf.PI * Mathf.Min(f, SR * .45f) / SR, al = Mathf.Sin(w0) / (2 * q), cs = Mathf.Cos(w0); float b0, b1, b2, a0 = 1 + al, a1 = -2 * cs, a2 = 1 - al;
                if (type == "lowpass") { b0 = (1 - cs) / 2; b1 = 1 - cs; b2 = b0; } else if (type == "highpass") { b0 = (1 + cs) / 2; b1 = -(1 + cs); b2 = b0; } else { b0 = al; b1 = 0; b2 = -al; }
                float y = (b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0; x2 = x1; x1 = x; y2 = y1; y1 = y;
                b.d[s0 + i] += y * Env(t, vol, atk, dur);
            }
        }
        static AudioClip Make(string kind, int n)
        {
            var b = new Buf(kind == "riser" ? (n > 0 ? n : 3) + .3f : kind == "go" ? 1.5f : 1.1f);
            switch (kind)
            {
                case "mark_enemy": Osc(b, "square", 1320, 1320, 0, .07f, .07f); Osc(b, "square", 1760, 1760, .09f, .09f, .07f); Osc(b, "sine", 880, 880, 0, .2f, .12f); break;
                case "mark": Osc(b, "sine", 980, 1180, 0, .16f, .13f); Osc(b, "triangle", 1960, 2360, 0, .08f, .04f); break;
                case "tick": Osc(b, "sine", 1650, 1400, 0, .06f, .16f); Osc(b, "triangle", 3300, 3000, 0, .025f, .05f); Noise(b, 0, .025f, .1f, "highpass", 5000, 5000); break;
                case "beep": { float f = n == 3 ? 622 : n == 2 ? 740 : n == 1 ? 880 : 700; Osc(b, "sine", f, f, 0, .42f, .26f); Osc(b, "square", f, f, 0, .16f, .05f); Osc(b, "sine", f * 2, f * 2, 0, .18f, .06f); Osc(b, "sine", f / 2, f / 2, 0, .5f, .16f); Osc(b, "sine", 110, 46, 0, .32f, .55f, .002f); Noise(b, 0, .03f, .18f, "bandpass", 2600, 2600, 1.5f); break; }
                case "riser": { float d = n > 0 ? n : 3; Osc(b, "sawtooth", 110, 440, 0, d, .09f, d * .95f); Noise(b, 0, d, .16f, "bandpass", 400, 7500, 2.5f, d * .97f); break; }
                case "go": Osc(b, "sine", 130, 34, 0, 1.1f, .95f, .002f); Osc(b, "triangle", 65, 30, 0, .8f, .4f, .002f); Noise(b, 0, .9f, .55f, "lowpass", 9000, 380, .7f, .001f); Noise(b, 0, .08f, .4f, "highpass", 2500, 2500);
                    foreach (var (f, v) in new[] { (146.8f, .11f), (220f, .09f), (293.7f, .08f), (440f, .05f) }) Osc(b, "sawtooth", f, f, 0, 1.2f, v); Osc(b, "sine", 1760, 1760, .02f, .6f, .05f); Osc(b, "sine", 2637, 2637, .05f, .5f, .03f); break;
                case "round": Noise(b, 0, .55f, .22f, "bandpass", 350, 3600, 1.4f, .25f); Osc(b, "sine", 85, 40, .45f, .55f, .7f, .002f); Noise(b, .45f, .25f, .25f, "lowpass", 4000, 300); break;
                case "buy": Osc(b, "sine", 1318.5f, 1318.5f, 0, .16f, .16f); Osc(b, "sine", 1975.5f, 1975.5f, .07f, .3f, .14f); Noise(b, 0, .04f, .12f, "bandpass", 4200, 4200, 3); break;
                case "medopen": Noise(b, 0, .22f, .32f, "bandpass", 1800, 5200, 1.2f, .01f); Noise(b, .24f, .4f, .16f, "highpass", 3000, 6500, 2); Osc(b, "square", 2400, 2300, .7f, .02f, .05f); Noise(b, .7f, .03f, .2f, "bandpass", 3800, 3800, 4); break;
                case "heal": Noise(b, 0, .16f, .2f, "bandpass", 700, 3000, 1.5f, .01f); Noise(b, .18f, .34f, .12f, "highpass", 2600, 5400, 1.7f, .015f); Osc(b, "sine", 120, 48, .25f, .25f, .24f, .002f);
                    for (int i = 0; i < 3; i++) { float f = new[] { 392f, 523.3f, 659.3f }[i]; Osc(b, "triangle", f, f * 1.04f, .28f + i * .08f, .32f, .055f); } break;
                case "medcancel": Noise(b, 0, .12f, .14f, "bandpass", 2600, 900, 1.4f); Osc(b, "sine", 520, 330, 0, .16f, .07f); break;
                case "glass": { float v = n > 0 ? n : 1; Noise(b, 0, .06f, .55f * v, "highpass", 2500, 1800, 1, .001f); Noise(b, .01f, .45f, .32f * v, "bandpass", 6500, 3500, 1.6f, .002f); for (int i = 0; i < 9; i++) { float f = 2600 + Random.value * 4200, d = .04f + Random.value * .5f; Osc(b, "sine", f, f * .98f, d, .09f, .05f * v); } break; }
                case "deny": Osc(b, "square", 155, 140, 0, .2f, .07f); Osc(b, "square", 117, 110, .11f, .22f, .07f); break;
                case "hover": Osc(b, "sine", 2400, 2600, 0, .035f, .03f); break;
                case "hit_body": Noise(b, 0, .075f, .22f, "bandpass", 280, 1350, 1.4f, .001f); Osc(b, "triangle", 118, 63, 0, .11f, .22f, .001f); break;
                case "hit_head": Noise(b, 0, .065f, .28f, "bandpass", 1100, 4200, 2.2f, .001f); Osc(b, "sine", 980, 620, 0, .12f, .12f, .001f); Osc(b, "triangle", 220, 110, 0, .1f, .12f, .001f); break;
                case "zone": Osc(b, "sine", 540, 540, 0, .16f, .11f, .003f); Osc(b, "sine", 410, 410, .19f, .2f, .11f, .003f); Noise(b, .37f, .22f, .08f, "bandpass", 650, 1500, 1.2f, .04f); break;
                case "win": case "lose":
                    { var notes = kind == "win" ? new[] { 523.3f, 659.3f, 784f, 1046.5f } : new[] { 440f, 392f, 349.2f, 261.6f }; for (int i = 0; i < 4; i++) { Osc(b, "triangle", notes[i], notes[i], i * .11f, .5f, .16f); Osc(b, "sine", notes[i] / 2, notes[i] / 2, i * .11f, .5f, .08f); } Osc(b, "sine", 90, 45, 0, .5f, .4f, .002f); break; }
                default: return null;
            }
            // compressor simples (limite suave)
            for (int i = 0; i < b.d.Length; i++) b.d[i] = (float)System.Math.Tanh(b.d[i] * 1.1f);
            var c = AudioClip.Create("synth_" + kind + n, b.d.Length, 1, SR, false); c.SetData(b.d, 0); return c;
        }

        // ouvido: o AudioListener do jogo (segue a câmera ativa); o paredão calcula distância/direção a partir dele
        static AudioListener ear; static int earFrame = -1;
        public static Transform Ear
        {
            get
            {
                if (earFrame != Time.frameCount || !ear || !ear.isActiveAndEnabled)
                {
                    earFrame = Time.frameCount; ear = null;
                    foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude)) if (l.isActiveAndEnabled) { ear = l; break; }
                }
                return ear ? ear.transform : Camera.main ? Camera.main.transform : null;
            }
        }
        // ouvinte segue a câmera ativa
        public static void Listener(Transform cam) { Ensure(); var l = root.GetComponent<AudioListener>(); if (!Object.FindAnyObjectByType<AudioListener>()) root.AddComponent<AudioListener>(); if (cam && root.GetComponent<AudioListener>()) root.transform.SetPositionAndRotation(cam.position, cam.rotation); }
    }
    public class FBAudioTicker : MonoBehaviour
    {
        static readonly int __fbt_Update = FBTempo.Id("FBAudioTicker.Update");                          // [parceiro] tempo por script (FBTempo, desligado no jogo)
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt() { FBAudio.Tick(Time.deltaTime); var c = Camera.main; if (c && GetComponent<AudioListener>()) transform.SetPositionAndRotation(c.transform.position, c.transform.rotation); }
    }
}
