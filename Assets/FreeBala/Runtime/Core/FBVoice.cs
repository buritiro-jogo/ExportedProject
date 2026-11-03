// Voz de dano — ligação com o jogo e Playback Controller:
//   Damage Event (FBDamageEvents) → Damage Type → Voice Profile (FBVoiceProfiles) → Variation (FBVoiceDirector) → **Playback (aqui)**.
// • Cada personagem (jogador, bots, NPCs, remotos) é registrado pela instância; o perfil (sexo + timbre fixo) sai do skinId.
// • Reprodução: pool próprio de AudioSources de voz (não disputa com tiros no pool do FBAudio), 3D com rolloff "inverse" por banco e a
//   fonte acompanhando a cabeça do personagem enquanto fala; a própria voz do jogador local sai 2D. Altura = timbre do personagem ×
//   banco × jitter ±3 %. Grupo "Voz" do mixer quando existir (FBAudio.Group).
// • Rede: só o DONO do personagem decide (FBDamage.Local). O jogador local manda {banco, variação, intensidade} (FBNet "vo"); os outros
//   clientes tocam a mesma variação no remoto (FBVoiceDirector.External), sem decidir de novo → sem voz duplicada cliente/servidor.
//   Bots/NPCs são locais a cada cliente e usam o mesmo diretor.
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public static class FBVoice
    {
        public static FBVoiceDirector D { get; private set; }
        class Who { public FBCharacter ch; public FBVoiceProfile prof; public float pitch; public string skin; }
        static readonly Dictionary<int, Who> who = new();
        class Slot { public AudioSource src; public int key = -1; public Transform follow; public float lift; }
        static readonly List<Slot> slots = new(); static GameObject root;
        static float bodyHitT;
        // diagnóstico (FBVoiceAutoTest / -fbaudio): uma linha por evento de dano, voz tocada ou voz suprimida; null = custo zero
        public struct Trace { public float t; public FBCharacter ch; public string sex, kind, type, bank, decision, clip; public float amount, hp, vol, pitch, intensity, dist; public int voices; }
        public static System.Action<Trace> trace;
        static readonly Dictionary<int, string> lastType = new();
        static float DistToListener(Vector3 p) { var l = Object.FindAnyObjectByType<AudioListener>(); return l ? Vector3.Distance(l.transform.position, p) : -1; }

        static void Ensure()
        {
            if (D != null) return;
            var p = FBVoiceProfiles.I;
            D = new FBVoiceDirector(p, System.Environment.TickCount) { maxVoices = Application.isMobilePlatform ? 5 : 8 };
            D.countOf = (k, b) => who.TryGetValue(k, out var w) ? w.prof.Count(b) : 0;
            D.durationOf = (k, b, v) => { var c = ClipOf(k, b, v); return c ? c.length / Mathf.Max(.5f, PitchOf(k, b)) : .8f; };
            D.onCue = Perform;
            D.onSuppress = (k, b, why) =>
            {
                if (trace == null || !who.TryGetValue(k, out var w) || !w.ch) return;
                trace(new Trace { t = Time.time, ch = w.ch, sex = w.prof.sex, kind = "voz", type = lastType.TryGetValue(k, out var ty) ? ty : "", bank = b.ToString(), decision = "suprimida: " + why, hp = w.ch.hp, dist = DistToListener(w.ch.Pos), voices = D.ActiveVoices(Time.time) });
            };
            root = new GameObject("FB_Voice"); Object.DontDestroyOnLoad(root); root.AddComponent<FBVoiceRunner>();
            for (int i = 0; i < D.maxVoices + 2; i++)
            {
                var s = new GameObject("Voz" + i).AddComponent<AudioSource>(); s.transform.SetParent(root.transform);
                s.playOnAwake = false; s.loop = false; s.spatialBlend = 1; s.rolloffMode = AudioRolloffMode.Custom; s.dopplerLevel = 0; s.maxDistance = 400;
                s.spread = 30; s.priority = 64; s.outputAudioMixerGroup = FBAudio.Group(true); slots.Add(new Slot { src = s });
            }
        }

        static readonly Dictionary<FBCharacter, int> keys = new(); static int nextKey = 1;
        public static int Key(FBCharacter a) { if (!keys.TryGetValue(a, out var k)) keys[a] = k = nextKey++; return k; }
        static Who Reg(FBCharacter a)
        {
            int k = Key(a);
            if (!who.TryGetValue(k, out var w) || w.skin != a.skinId)   // troca de skin (guarda-roupa / respawn) refaz o perfil
            {
                var p = FBVoiceProfiles.I; var prof = p.ProfileOf(p.SexOf(a.skinId, a.npcSet?.sex));
                who[k] = w = new Who { ch = a, prof = prof, pitch = p.PitchOf(a.skinId, prof), skin = a.skinId };
            }
            return w;
        }
        public static string SexOf(FBCharacter a) { Ensure(); return Reg(a).prof.sex; }
        static AudioClip ClipOf(int k, FBVoiceBank b, int v) => who.TryGetValue(k, out var w) ? FBAudio.Clip("Voice/" + w.prof.Clip(b, v)) : null;
        static float PitchOf(int k, FBVoiceBank b) => (who.TryGetValue(k, out var w) ? w.pitch : 1) * FBVoiceProfiles.I.Rule(b).pitch;

        // ---------------- entrada: barramento de dano (FBDamageVoice só chama isto para d.Local)
        public static void OnDamage(in FBDamage d)
        {
            var a = d.victim; if (!a) return; Ensure(); Reg(a);
            var part = d.hit.part ?? ""; var why = d.why ?? "";
            // tiro que chega pela rede ("hit") ou sem raio vem sem hit.valid → o barramento marca Other; com atirador é bala
            var type = d.type == FBDamageType.Other && d.source && d.source != a ? FBDamageType.Bullet : d.type;
            lastType[Key(a)] = type.ToString();
            if (trace != null) trace(new Trace { t = Time.time, ch = a, sex = Reg(a).prof.sex, kind = "dano", type = type + (d.head ? "/cabeça" : part != "" ? "/" + part : "") + (d.killed ? "/morto" : d.knocked ? "/caído" : ""), amount = d.applied, hp = d.hpAfter, dist = DistToListener(a.Pos), voices = D.ActiveVoices(Time.time) });
            var h = new FBVoiceHit
            {
                key = Key(a), type = type, applied = d.applied, hpAfter = d.hpAfter, maxHp = a.maxHp, head = d.head || part == "head",
                limb = part.Contains("arm") || part.Contains("leg") || part.Contains("hand") || part.Contains("foot"),
                killed = d.killed, knocked = d.knocked, down = a.knocked && !d.knocked, isPlayer = a.isPlayer,
                fire = why.Contains("fire") || why.Contains("burn") || why.Contains("fogo") || why.Contains("molotov")
            };
            D.Hit(h, Time.time);
            // o jogador sente o baque do tiro no corpo (independe da voz, que pode estar em cooldown)
            if (a.isPlayer && (d.type == FBDamageType.Bullet || d.type == FBDamageType.Explosion) && d.applied > 0 && Time.time - bodyHitT > .12f) { bodyHitT = Time.time; FBAudio.Play2D("ie_bodyhit", .5f, 0, Random.Range(.9f, 1.1f)); }
        }
        // compatibilidade com chamadas antigas (FBVoice.Hurt)
        public static void Hurt(FBCharacter a, float dmg, string why)
        {
            if (!a) return; var t = FBDamageEvents.TypeOf(why, default);
            OnDamage(new FBDamage { victim = a, type = t, amount = dmg, applied = dmg, hpBefore = a.hp + dmg, hpAfter = a.hp, why = why });
        }
        // rede: reação decidida pelo dono do personagem remoto
        public static void OnNet(FBCharacter a, int bank, int variation, float intensity)
        {
            if (!a || bank < 0 || bank >= System.Enum.GetValues(typeof(FBVoiceBank)).Length) return; Ensure(); Reg(a);
            D.External(Key(a), (FBVoiceBank)bank, variation, intensity, Time.time);
        }

        // ---------------- Playback Controller
        static void Perform(FBVoiceCue c)
        {
            if (!who.TryGetValue(c.key, out var w) || !w.ch) return;
            if (c.interrupt || c.steal >= 0) foreach (var s in slots) if (s.key == c.key || s.key == c.steal) { s.src.Stop(); s.key = -1; s.follow = null; }
            var clip = ClipOf(c.key, c.bank, c.variation); if (!clip) return;
            var rule = FBVoiceProfiles.I.Rule(c.bank); var a = w.ch; bool own = a.isPlayer && !a.isRemote;
            Slot sl = null; foreach (var s in slots) if (!s.src.isPlaying) { sl = s; break; }
            if (sl == null) { sl = slots[0]; foreach (var s in slots) if (s.src.time > sl.src.time) sl = s; sl.src.Stop(); }   // pool cheio: a mais adiantada cede
            var src = sl.src; sl.key = c.key; sl.follow = a.Head ? a.Head : a.transform; sl.lift = a.Head ? .05f : 1.5f * a.ScaleK;
            src.transform.position = sl.follow.position + Vector3.up * sl.lift;
            src.clip = clip; src.pitch = PitchOf(c.key, c.bank) * (1 + (Random.value - .5f) * .06f);
            src.volume = Mathf.Clamp01(rule.vol * Mathf.Lerp(.55f, 1, c.intensity) * (own ? .75f : 1) * FBAudio.VoiceVol);
            src.spatialBlend = own ? 0 : 1; if (!own) src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, FBAudio.Rolloff(rule.refD, 1.15f));
            src.Play();
            if (trace != null) trace(new Trace { t = Time.time, ch = a, sex = w.prof.sex, kind = "voz", type = lastType.TryGetValue(c.key, out var ty) ? ty : "", bank = c.bank.ToString(), decision = c.steal >= 0 ? "tocou (roubou vaga)" : c.interrupt ? "tocou (interrompeu)" : "tocou", clip = clip.name, vol = src.volume, pitch = src.pitch, intensity = c.intensity, hp = a.hp, dist = own ? 0 : DistToListener(src.transform.position), voices = D.ActiveVoices(Time.time) });
            if (own && FBNet.I) FBNet.I.SendVoice((int)c.bank, c.variation, c.intensity);   // dono → outros clientes
        }

        internal static void Tick()
        {
            if (D == null) return; D.Tick(Time.time);
            foreach (var s in slots)
            {
                if (s.key < 0) continue;
                if (!s.src.isPlaying) { s.key = -1; s.follow = null; continue; }
                if (s.follow) s.src.transform.position = s.follow.position + Vector3.up * s.lift; else s.src.Stop();   // personagem sumiu: cala
            }
            if (Time.frameCount % 120 == 0)
            {   // limpa personagens destruídos
                List<int> dead = null; foreach (var kv in who) if (!kv.Value.ch) (dead ??= new()).Add(kv.Key);
                if (dead != null) foreach (var k in dead) { who.Remove(k); D.Forget(k); }
                List<FBCharacter> gone = null; foreach (var kv in keys) if (!kv.Key) (gone ??= new()).Add(kv.Key); if (gone != null) foreach (var c in gone) keys.Remove(c);
            }
        }
    }
    public class FBVoiceRunner : MonoBehaviour
    {
        // [parceiro 01/11] medidor de tempo por sistema (FBTempo): com FBTempo.On soma o custo do Tick das vozes
        static readonly int __fbt_Update = FBTempo.Id("FBVoiceRunner.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt() => FBVoice.Tick();
    }
}
