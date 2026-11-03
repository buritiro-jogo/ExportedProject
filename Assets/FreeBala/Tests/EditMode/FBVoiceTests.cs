// Testes da voz de dano (agente de áudio): perfis masc/fem, tiro × gás, rajada (1 reação por janela), dano simultâneo de origens diferentes,
// morte no gás, cooldowns/prioridade, saída da zona, limite global e variação sem repetição. Lógica pura (FBVoiceDirector) — sem áudio.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace FreeBala.Tests
{
    public class FBVoiceTests
    {
        static string Dir => Path.Combine(Application.dataPath, "FreeBala/Resources/Audio/Voice");
        static FBVoiceProfiles P => FBVoiceProfiles.Parse(File.ReadAllText(Path.Combine(Dir, "voice_profiles.json")));

        // diretor com relógio manual; registra todas as falas
        class Rig
        {
            public FBVoiceDirector d; public List<(float t, FBVoiceCue c)> cues = new(); public float t;
            public Rig(int maxVoices = 8, float dur = .8f)
            {
                var p = P; d = new FBVoiceDirector(p, 7) { maxVoices = maxVoices };
                d.countOf = (k, b) => p.ProfileOf(k % 2 == 0 ? "f" : "m").Count(b); d.durationOf = (k, b, v) => dur;
                d.onCue = c => cues.Add((t, c));
            }
            public void Hit(int key, FBDamageType type, float dmg, float hpAfter, bool head = false, bool killed = false, bool player = true, bool limb = false)
                => d.Hit(new FBVoiceHit { key = key, type = type, applied = dmg, hpAfter = hpAfter, maxHp = 100, head = head, killed = killed, isPlayer = player, limb = limb }, t);
            public void Run(float until, System.Action<float> each = null) { while (t < until - 1e-4f) { t += .02f; each?.Invoke(t); d.Tick(t); } }
        }

        [Test]
        public void Perfil_SexoPorPersonagem()
        {
            var p = P;
            foreach (var f in new[] { "c_mia", "c_mia2", "c_clara", "p_bianca", "c_helena2", "n_jennifer", "c_mia9" }) Assert.AreEqual("f", p.SexOf(f), f);
            foreach (var m in new[] { "c_felipe", "ie_bope", "n_chad", "p_tiozin", "c_vs2" }) Assert.AreEqual("m", p.SexOf(m), m);
            Assert.AreEqual("f", p.SexOf("npc_desconhecido", "f")); Assert.AreEqual("m", p.SexOf(null));
            // timbre: fixo por personagem (igual em todos os clientes) e dentro da faixa do perfil
            var pf = p.ProfileOf("f"); float a = p.PitchOf("c_mia", pf);
            Assert.AreEqual(a, p.PitchOf("c_mia", pf)); Assert.That(a, Is.InRange(pf.pitch.x, pf.pitch.y));
            Assert.AreEqual(.88f, p.PitchOf("ie_gordao", p.ProfileOf("m")), 1e-4f);
        }

        [Test]
        public void Perfil_BancosCompletosESemTroca()
        {
            var p = P;
            foreach (var sx in new[] { "m", "f" })
                foreach (FBVoiceBank b in System.Enum.GetValues(typeof(FBVoiceBank)))
                {
                    var prof = p.ProfileOf(sx); Assert.Greater(prof.Count(b), 0, $"{sx}.{b} vazio");
                    for (int v = 0; v < prof.Count(b); v++)
                    {
                        var n = prof.Clip(b, v); Assert.IsTrue(File.Exists(Path.Combine(Dir, n + ".ogg")), n);
                        Assert.IsTrue(n.StartsWith(sx + "_"), $"{sx}.{b} usa clipe do outro sexo: {n}");   // nunca voz trocada
                    }
                }
        }

        [Test]
        public void Tiro_LeveGraveCabecaMembro_MascEFem()
        {
            foreach (int key in new[] { 1, 2 })   // 1 = masc, 2 = fem (Rig.countOf)
            {
                var r = new Rig(); r.Hit(key, FBDamageType.Bullet, 8, 92); r.Run(.3f);
                Assert.AreEqual(FBVoiceBank.DorLeve, r.cues.Single().c.bank);
                r = new Rig(); r.Hit(key, FBDamageType.Bullet, 40, 60); r.Run(.3f); Assert.AreEqual(FBVoiceBank.DorGrave, r.cues.Single().c.bank);
                r = new Rig(); r.Hit(key, FBDamageType.Bullet, 15, 85); r.Run(.3f); Assert.AreEqual(FBVoiceBank.DorMedia, r.cues.Single().c.bank);
                r = new Rig(); r.Hit(key, FBDamageType.Bullet, 20, 80, head: true); r.Run(.3f); Assert.AreEqual(FBVoiceBank.Cabeca, r.cues.Single().c.bank);
                r = new Rig(); r.Hit(key, FBDamageType.Bullet, 6, 94, limb: true); r.Run(.3f); Assert.AreEqual(FBVoiceBank.Membro, r.cues.Single().c.bank);
                r = new Rig(); r.Hit(key, FBDamageType.Bullet, 30, 0, killed: true); r.Run(.3f); Assert.AreEqual(FBVoiceBank.Morte, r.cues.Single().c.bank);
            }
        }

        [Test]
        public void Rajada_UmaReacaoPorJanela()
        {
            var r = new Rig(); float hp = 100, next = 0;
            r.Run(1.2f, t => { if (t >= next && hp > 20) { next = t + .1f; hp -= 7; r.Hit(1, FBDamageType.Bullet, 7, hp); } });   // ~600 tiros/min
            Assert.AreEqual(1, r.cues.Count(x => x.t <= r.d.burstWindow + .05f), "primeira janela");
            Assert.LessOrEqual(r.cues.Count, 3, "sem um grito por bala: " + string.Join(", ", r.cues.Select(x => x.c)));
            for (int i = 1; i < r.cues.Count; i++) Assert.Greater(r.cues[i].t - r.cues[i - 1].t, r.d.burstWindow);
        }

        [Test]
        public void DanoSimultaneo_OrigensDiferentes_UmaSoReacaoDeMaiorPrioridade()
        {
            var r = new Rig();
            r.Hit(1, FBDamageType.Bullet, 10, 90); r.Hit(1, FBDamageType.Explosion, 30, 60); r.Hit(1, FBDamageType.Fall, 5, 55);
            r.Run(.5f);
            Assert.AreEqual(1, r.cues.Count); Assert.AreEqual(FBVoiceBank.Explosao, r.cues[0].c.bank);
            // personagens diferentes no mesmo instante falam cada um a sua
            r = new Rig(); r.Hit(1, FBDamageType.Bullet, 10, 90); r.Hit(2, FBDamageType.Vehicle, 40, 60); r.Run(.5f);
            Assert.AreEqual(2, r.cues.Count); Assert.AreEqual(FBVoiceBank.Atropelo, r.cues.First(x => x.c.key == 2).c.bank);
        }

        [Test]
        public void Gas_BancoSeparado_IntervalosEExposicao()
        {
            var r = new Rig(); float hp = 100, tick = 0; int ticks = 0;
            r.Run(30, t => { if (t >= tick) { tick = t + 1; ticks++; hp -= 2; r.Hit(2, FBDamageType.Zone, 2, hp); } });
            var gasBanks = new[] { FBVoiceBank.GasTosse, FBVoiceBank.GasIrrita, FBVoiceBank.GasResp, FBVoiceBank.GasEsforco, FBVoiceBank.GasFraca };
            Assert.IsTrue(r.cues.All(x => gasBanks.Contains(x.c.bank)), "só banco de gás: " + string.Join(", ", r.cues.Select(x => x.c)));
            Assert.AreEqual(FBVoiceBank.GasIrrita, r.cues[0].c.bank, "primeiro contato = irritação");
            Assert.That(r.cues.Count, Is.InRange(5, ticks / 2), "não toca a cada tick");
            // intervalos encurtam com a exposição/vida baixa
            float early = r.cues[2].t - r.cues[1].t, late = r.cues[^1].t - r.cues[^2].t;
            Assert.Less(late, early + .5f);
            Assert.Greater(r.cues[^1].c.intensity, r.cues[1].c.intensity);
        }

        [Test]
        public void Gas_SaidaDaZona_RespiracaoDecaiEPara()
        {
            var r = new Rig(); float hp = 100, tick = 0, lastTick = 0;
            r.Run(15, t => { if (t >= tick) { tick = t + 1; lastTick = t; hp -= 3; r.Hit(1, FBDamageType.Zone, 3, hp); } });
            r.Run(40);
            float exit = lastTick + r.d.gasHold;                                     // sem tick da zona por gasHold = saiu
            var after = r.cues.Where(x => x.t > exit).ToList();
            Assert.IsFalse(r.d.InGas(1)); Assert.IsFalse(r.d.Breathing(1), "respiração parou");
            Assert.GreaterOrEqual(after.Count, 1);
            Assert.IsTrue(after.All(x => x.c.bank == FBVoiceBank.GasResp), "fora da zona só respiração");
            for (int i = 1; i < after.Count; i++) Assert.Less(after[i].c.intensity, after[i - 1].c.intensity, "decai");
            Assert.Less(after[^1].t - exit, 12, "para depois de alguns segundos");
        }

        [Test]
        public void Gas_MorteNoGasTemSomProprio_ESilencia()
        {
            var r = new Rig(); float hp = 10, tick = 0;
            r.Run(6, t => { if (t >= tick && hp > 0) { tick = t + 1; hp -= 2; r.Hit(2, FBDamageType.Zone, 2, hp, killed: hp <= 0); } });
            Assert.AreEqual(FBVoiceBank.GasMorte, r.cues[^1].c.bank);
            int n = r.cues.Count; r.Run(20); Assert.AreEqual(n, r.cues.Count, "morto não tosse nem respira");
        }

        [Test]
        public void Cooldown_EPrioridade()
        {
            var r = new Rig();
            r.Hit(1, FBDamageType.Bullet, 5, 95); r.Run(.2f);
            r.Hit(1, FBDamageType.Bullet, 5, 90); r.Run(.45f);                       // falando ainda, mesma prioridade → descarta
            Assert.AreEqual(1, r.cues.Count);
            r.Hit(1, FBDamageType.Bullet, 40, 50); r.Run(.7f);                       // grave interrompe a leve
            Assert.AreEqual(2, r.cues.Count); Assert.IsTrue(r.cues[1].c.interrupt);
            r.Hit(1, FBDamageType.Bullet, 50, 0, killed: true); r.Run(.75f);        // morte passa por cima de tudo
            Assert.AreEqual(FBVoiceBank.Morte, r.cues[^1].c.bank);
            // cooldown do banco: dor leve não repete antes de rule.cd, mesmo calado
            r = new Rig(dur: .1f); r.Hit(3, FBDamageType.Bullet, 5, 95); r.Run(.2f); r.Hit(3, FBDamageType.Bullet, 5, 90); r.Run(.45f);
            Assert.AreEqual(1, r.cues.Count);
            r.Run(.8f); r.Hit(3, FBDamageType.Bullet, 5, 85); r.Run(1.2f); Assert.AreEqual(2, r.cues.Count);   // passou o cd (0,55 s)
        }

        [Test]
        public void LimiteGlobalDeVozes()
        {
            var r = new Rig(maxVoices: 2, dur: 2);
            for (int k = 1; k <= 4; k++) r.Hit(k, FBDamageType.Bullet, 8, 92);
            r.Run(.3f);
            Assert.AreEqual(2, r.cues.Count); Assert.LessOrEqual(r.d.ActiveVoices(r.t), 2);
            r.Hit(5, FBDamageType.Bullet, 60, 0, killed: true); r.Run(.4f);       // morte rouba a vaga de uma dor leve
            Assert.AreEqual(FBVoiceBank.Morte, r.cues[^1].c.bank); Assert.GreaterOrEqual(r.cues[^1].c.steal, 1);
            Assert.LessOrEqual(r.d.ActiveVoices(r.t), 2);
        }

        [Test]
        public void Variacao_NuncaRepeteSeguida()
        {
            var r = new Rig(dur: .1f); int last = -1;
            for (int i = 0; i < 30; i++)
            {
                r.Hit(1, FBDamageType.Bullet, 50, 50); r.Run(r.t + 1.5f);
                var c = r.cues[^1].c; Assert.AreEqual(FBVoiceBank.DorGrave, c.bank); if (P.ProfileOf("m").Count(FBVoiceBank.DorGrave) > 1) Assert.AreNotEqual(last, c.variation);   // banco de 1 clipe (voz do usuário) não tem como variar last = c.variation;
            }
            Assert.AreEqual(30, r.cues.Count);
        }

        [Test]
        public void Rede_ReacaoDoDonoTocaIgualNoRemoto()
        {
            var r = new Rig(); r.d.External(9, FBVoiceBank.GasTosse, 2, .7f, 0); r.Run(.1f);
            var c = r.cues.Single().c; Assert.AreEqual(FBVoiceBank.GasTosse, c.bank); Assert.AreEqual(2, c.variation); Assert.AreEqual(.7f, c.intensity, 1e-4f);
        }
    }
}
