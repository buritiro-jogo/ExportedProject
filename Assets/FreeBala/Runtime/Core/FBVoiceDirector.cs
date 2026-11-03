// Diretor de reações de voz (lógica pura, sem áudio: testável em EditMode). Decide SE e O QUE cada personagem fala ao levar dano:
//   Damage Event → Damage Type → Voice Profile → **Variation (aqui)** → Playback Controller (FBVoice).
// Regras:
//  • Rajada: tiros/dano que chegam numa janela curta (burstWindow) são SOMADOS e geram UMA reação proporcional ao total
//    (leve/média/grave/cabeça/membro); dano simultâneo de origens diferentes (bala + explosão + queda) também vira uma só, a do tipo
//    de maior prioridade.
//  • Prioridade: morte (100) > caído (80) > explosão/atropelo (70) > queda grave > cabeça > grave > média > leve > gás. Uma voz de
//    prioridade MAIOR interrompe a que o personagem está falando; igual/menor espera.
//  • Cooldowns: por personagem (intervalo mínimo entre falas) e por banco (FBVoiceRule.cd); bots/NPC ainda sorteiam npcChance nos
//    bancos leves para o tiroteio não virar coral.
//  • Limite global de vozes simultâneas (maxVoices): cheio → rouba a de menor prioridade, se a nova for mais importante.
//  • Variação: sorteada sem repetir a última do mesmo banco para o mesmo personagem.
//  • Zona/gás: banco separado com estado por personagem (tempo de exposição, vida). Os ticks da zona (1/s) NÃO tocam nada sozinhos;
//    o diretor agenda tosse/irritação/respiração/esforço/fraqueza em intervalos que encurtam com a exposição e a vida baixa. Ao sair,
//    a respiração pesada decai e para. Eliminação no gás tem som próprio (GasMorte).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    // entrada mínima (o FBDamageVoice converte o FBDamage do barramento)
    public struct FBVoiceHit
    {
        public int key; public FBDamageType type; public float applied, hpAfter, maxHp; public bool head, limb, killed, knocked, down, isPlayer, fire;
    }
    // saída: o que tocar (o dono decide; a rede repete bank/variation/intensity nos outros clientes)
    public struct FBVoiceCue
    {
        public int key; public FBVoiceBank bank; public int variation, prio; public float intensity; public bool interrupt; public int steal;   // steal: chave da voz roubada (-1 nenhuma)
        public override string ToString() => $"{key}:{bank}#{variation} i={intensity:0.00}{(interrupt ? " int" : "")}";
    }

    public class FBVoiceDirector
    {
        public float burstWindow = .16f;                 // janela de acúmulo da rajada (s)
        public float gasHold = 1.6f;                     // sem tick de zona por esse tempo = saiu da zona (a zona bate 1x/s)
        public float playerGap = .35f, npcGap = .6f;     // intervalo mínimo entre falas do mesmo personagem
        public int maxVoices = 8;                        // limite global de vozes simultâneas
        public float breathDecay = 2.5f;                 // constante de tempo da respiração depois de sair do gás (s)
        public Func<int, FBVoiceBank, int> countOf = (k, b) => 3;              // nº de variações do banco para o personagem (perfil/sexo)
        public Func<int, FBVoiceBank, int, float> durationOf = (k, b, v) => .8f; // duração real do clipe escolhido
        public Action<FBVoiceCue> onCue;
        public Action<int, FBVoiceBank, string> onSuppress;                       // diagnóstico (autoteste -fbaudio): reação descartada e o motivo
        public readonly FBVoiceProfiles prof;
        readonly System.Random rnd;

        class Gas { public bool inZone, first; public float exposure, lastZoneT = -99, nextT, exitT, breath; }
        class Actor
        {
            public bool isPlayer, dead; public float lastT = -99, lastEnd = -99, hpFrac = 1; public int lastPrio;
            public readonly float[] bankT = new float[NB]; public readonly int[] lastVar = new int[NB];
            public bool pending; public float t0, dmg, maxHp = 100, hpAfter; public bool head, limb, body, fire, down; public int types;
            public Gas gas = new();
            public Actor() { for (int i = 0; i < NB; i++) { bankT[i] = -99; lastVar[i] = -1; } }
        }
        static readonly int NB = Enum.GetValues(typeof(FBVoiceBank)).Length;
        readonly Dictionary<int, Actor> actors = new();
        readonly List<(int key, int prio, float end)> active = new();
        float lastTick = -1;

        public FBVoiceDirector(FBVoiceProfiles p = null, int seed = 12345) { prof = p ?? new FBVoiceProfiles(); rnd = new System.Random(seed); }
        float R => (float)rnd.NextDouble();
        Actor A(int key) { if (!actors.TryGetValue(key, out var a)) actors[key] = a = new Actor(); return a; }
        public void Forget(int key) { actors.Remove(key); active.RemoveAll(v => v.key == key); }
        public int ActiveVoices(float now) { active.RemoveAll(v => v.end <= now); return active.Count; }
        public bool InGas(int key) => actors.TryGetValue(key, out var a) && a.gas.inZone;
        public bool Breathing(int key) => actors.TryGetValue(key, out var a) && !a.gas.inZone && a.gas.breath > 0;

        // ---------------- entrada: um evento de dano do barramento
        public void Hit(in FBVoiceHit h, float now)
        {
            var a = A(h.key); a.isPlayer = h.isPlayer; a.maxHp = Mathf.Max(1, h.maxHp); a.hpFrac = Mathf.Clamp01(h.hpAfter / a.maxHp);
            if (h.killed)
            {
                a.pending = false; bool gas = h.type == FBDamageType.Zone; a.gas = Reset(a.gas);
                Play(a, h.key, gas ? FBVoiceBank.GasMorte : FBVoiceBank.Morte, 1, now, true); a.dead = true; return;
            }
            if (a.dead && h.hpAfter > 0) a.dead = false;                                  // reviveu / renasceu
            if (a.dead) return;
            if (h.knocked) { a.pending = false; Play(a, h.key, FBVoiceBank.Caido, 1, now, true); return; }
            if (h.type == FBDamageType.Zone)
            {
                var g = a.gas; if (!g.inZone) { g.inZone = true; g.first = true; g.breath = 0; g.nextT = now + .45f + R * .3f; }
                g.lastZoneT = now; return;
            }
            if (h.type == FBDamageType.Bleed) { Play(a, h.key, FBVoiceBank.Sangrando, .4f, now); return; }
            if (h.applied <= 0 && h.type == FBDamageType.Bullet) return;                 // colete/capacete absorveu tudo: sem grito
            if (!a.pending) { a.pending = true; a.t0 = now; a.dmg = 0; a.types = 0; a.head = a.limb = a.body = a.fire = false; }
            a.dmg += Mathf.Max(0, h.applied); a.types |= 1 << (int)h.type; a.hpAfter = h.hpAfter; a.down = h.down;
            if (h.head) a.head = true; else if (h.limb) a.limb = true; else a.body = true; if (h.fire) a.fire = true;
        }

        // reação escolhida pelo dono em outro cliente (rede): só passa pelo limite global/interrupção, sem sortear de novo
        public void External(int key, FBVoiceBank bank, int variation, float intensity, float now)
        {
            var a = A(key); a.isPlayer = false; Play(a, key, bank, intensity, now, true, variation);
        }

        // ---------------- relógio: fecha janelas de rajada e conduz o gás
        public void Tick(float now)
        {
            float dt = lastTick < 0 ? 0 : Mathf.Clamp(now - lastTick, 0, .5f); lastTick = now;
            foreach (var kv in actors)
            {
                var a = kv.Value; if (a.dead) continue;
                if (a.pending && now - a.t0 >= burstWindow) { a.pending = false; Flush(a, kv.Key, now); }
                GasTick(a, kv.Key, now, dt);
            }
        }

        void Flush(Actor a, int key, float now)
        {
            float frac = a.dmg / a.maxHp; var best = FBVoiceBank.DorLeve; int bp = -1;
            void Cand(FBVoiceBank b) { int p = prof.Rule(b).prio; if (p > bp) { bp = p; best = b; } }
            bool T(FBDamageType t) => (a.types & (1 << (int)t)) != 0;
            if (T(FBDamageType.Explosion)) Cand(FBVoiceBank.Explosao);
            if (T(FBDamageType.Vehicle)) Cand(FBVoiceBank.Atropelo);
            if (T(FBDamageType.Fall)) Cand(frac >= .25f ? FBVoiceBank.QuedaGrave : FBVoiceBank.Queda);
            if (a.fire) Cand(FBVoiceBank.Fogo);
            if (T(FBDamageType.Environment) || T(FBDamageType.Other)) Cand(a.fire ? FBVoiceBank.Fogo : frac >= .3f ? FBVoiceBank.DorGrave : FBVoiceBank.Ambiente);
            if (T(FBDamageType.Takedown)) Cand(FBVoiceBank.DorGrave);
            if (T(FBDamageType.Bullet))
            {
                if (a.down) Cand(FBVoiceBank.Sangrando);                                // já caído: gemido, não grito
                else if (a.head) Cand(FBVoiceBank.Cabeca);
                else if (frac >= .25f) Cand(FBVoiceBank.DorGrave);
                else if (frac >= .11f) Cand(FBVoiceBank.DorMedia);              // FF: dano ≥ 11 já é "forte" (switch Above11)
                else if (a.limb && !a.body) Cand(FBVoiceBank.Membro);
                else Cand(FBVoiceBank.DorLeve);
            }
            if (bp < 0) return;
            // pouca vida: dor leve vira gemido arrastado
            if (a.hpFrac < .3f && bp <= prof.Rule(FBVoiceBank.DorMedia).prio && R < .65f) best = FBVoiceBank.PoucaVida;
            float inten = best == FBVoiceBank.Explosao || best == FBVoiceBank.Atropelo ? 1 : Mathf.Clamp(.25f + frac * 2.2f, .25f, 1);
            Play(a, key, best, inten, now);
        }

        static Gas Reset(Gas g) => new Gas { exposure = 0 };
        float Sev(Actor a) => Mathf.Clamp01(.55f * Mathf.Clamp01(a.gas.exposure / 18f) + .6f * (1 - a.hpFrac));
        void GasTick(Actor a, int key, float now, float dt)
        {
            var g = a.gas;
            if (g.inZone && now - g.lastZoneT > gasHold)
            {   // saiu da zona: respiração pesada que decai (proporcional ao quanto sofreu)
                g.inZone = false; g.exitT = now; g.breath = g.exposure > 1.5f ? Mathf.Max(.35f, Sev(a)) : 0; g.nextT = now + .7f + R * .4f;
            }
            if (g.inZone)
            {
                g.exposure += dt; if (now < g.nextT) return; float s = Sev(a); FBVoiceBank b;
                if (g.first) b = FBVoiceBank.GasIrrita;                                  // primeiro contato: pigarro/irritação
                else if (a.hpFrac < .3f && R < .5f) b = FBVoiceBank.GasFraca;
                else { float r = R; b = r < .42f + .3f * s ? FBVoiceBank.GasTosse : r < .78f ? FBVoiceBank.GasResp : r < .9f && s < .5f ? FBVoiceBank.GasIrrita : r < .95f ? FBVoiceBank.GasEsforco : FBVoiceBank.GasTosse; }
                bool ok = Play(a, key, b, .4f + .6f * s, now); if (ok) g.first = false;
                g.nextT = now + (ok ? Mathf.Lerp(4.2f, 1.7f, s) * (.8f + .4f * R) : .5f);   // bloqueado (falando/limite): tenta logo
            }
            else
            {
                if (g.exposure > 0) g.exposure = Mathf.Max(0, g.exposure - dt * .5f);   // voltar logo pro gás retoma de onde parou
                if (g.breath <= 0 || now < g.nextT) return;
                float lvl = g.breath * Mathf.Exp(-(now - g.exitT) / breathDecay);
                if (lvl < .15f) { g.breath = 0; return; }                                // respiração voltou ao normal: para
                Play(a, key, FBVoiceBank.GasResp, lvl, now); g.nextT = now + Mathf.Lerp(1.5f, 2.4f, 1 - lvl);
            }
        }

        bool No(int key, FBVoiceBank b, string why) { onSuppress?.Invoke(key, b, why); return false; }

        // ---------------- portão de prioridade/cooldown/limite e escolha da variação
        bool Play(Actor a, int key, FBVoiceBank bank, float inten, float now, bool force = false, int variation = -1)
        {
            var rule = prof.Rule(bank); int prio = rule.prio, ib = (int)bank;
            if (!force)
            {
                if (!a.isPlayer && rule.npcChance < 1 && R > rule.npcChance) { a.bankT[ib] = now; return No(key, bank, "sorteio npc"); }   // bot/NPC: nem toda dor leve vira voz
                if (now - a.bankT[ib] < rule.cd) return No(key, bank, "cooldown do banco");
            }
            bool speaking = now < a.lastEnd, interrupt = false;
            if (speaking) { if (prio > a.lastPrio) interrupt = true; else if (!force) return No(key, bank, "falando (prioridade igual/maior)"); else interrupt = true; }
            else if (!force && now - a.lastT < (a.isPlayer ? playerGap : npcGap) && prio <= a.lastPrio) return No(key, bank, "intervalo do personagem");
            int steal = -1; active.RemoveAll(v => v.end <= now);
            if (interrupt) active.RemoveAll(v => v.key == key);
            if (active.Count >= maxVoices)
            {
                int lo = 0; for (int i = 1; i < active.Count; i++) if (active[i].prio < active[lo].prio || (active[i].prio == active[lo].prio && active[i].end < active[lo].end)) lo = i;
                if (active[lo].prio >= prio) return No(key, bank, "limite global de vozes");   // todas as vozes tocando são mais importantes
                steal = active[lo].key; active.RemoveAt(lo); if (actors.TryGetValue(steal, out var sa)) sa.lastEnd = now;
            }
            int n = Mathf.Max(1, countOf(key, bank)), v = variation;
            if (v < 0) { v = rnd.Next(n); if (n > 1 && v == a.lastVar[ib]) v = (v + 1 + rnd.Next(n - 1)) % n; }   // nunca a mesma variação seguida
            a.lastVar[ib] = v; a.lastT = now; a.lastPrio = prio; a.bankT[ib] = now;
            float dur = Mathf.Max(.1f, durationOf(key, bank, v)); a.lastEnd = now + dur; active.Add((key, prio, now + dur));
            onCue?.Invoke(new FBVoiceCue { key = key, bank = bank, variation = v, prio = prio, intensity = Mathf.Clamp01(inten), interrupt = interrupt, steal = steal });
            return true;
        }
    }
}
