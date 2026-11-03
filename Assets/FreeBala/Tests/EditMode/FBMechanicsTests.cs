// Mesmos casos de tests/mechanics.test.mjs (jogo web) contra o porte C# (FBMechanics) — garante que as regras dos APKs não mudaram no porte.
using NUnit.Framework;
using UnityEngine;

namespace FreeBala.Tests
{
    public class K : IKnockable
    {
        public bool Knocked { get; set; } public float Hp { get; set; } public float MaxHp { get; set; } = 100; public float KnockInvT { get; set; }
        public float BleedAcc { get; set; } public object RevivedBy { get; set; } public float ReviveP { get; set; } public bool Civ { get; set; }
    }

    public class FBMechanicsTests
    {
        [Test] public void KnockSoModosSemMorteInstantanea()
        {
            foreach (var m in new[] { "classico", "ffa", "treino", "online" }) Assert.IsFalse(FBMechanics.CanKnock(new K(), m, true), m);
            foreach (var m in new[] { "squad", "pvl", "tatico" }) Assert.IsTrue(FBMechanics.CanKnock(new K(), m, true), m);
            Assert.AreEqual(10, FBMechanics.ReviveTime("squad")); Assert.AreEqual(5, FBMechanics.ReviveTime("pvl"));
        }
        [Test] public void KnockSemCompanheiroMorre()
        {
            Assert.IsFalse(FBMechanics.CanKnock(new K(), "squad", false));
            Assert.IsFalse(FBMechanics.CanKnock(new K { Knocked = true }, "squad", true));
            Assert.IsFalse(FBMechanics.CanKnock(new K { Civ = true }, "squad", true));
        }
        [Test] public void KnockVidaCheiaInvulnESangramento()
        {
            var a = new K { Hp = -12 }; FBMechanics.KnockDown(a, 10);
            Assert.IsTrue(a.Knocked); Assert.AreEqual(100, a.Hp);
            Assert.IsTrue(FBMechanics.KnockInvulnerable(a, 10.5f)); Assert.IsFalse(FBMechanics.KnockInvulnerable(a, 10.81f));
            float total = 0; for (int i = 0; i < 300; i++) total += FBMechanics.BleedStep(a, .1f); Assert.AreEqual(40, total);
            var b = new K(); FBMechanics.KnockDown(b, 0); float t = 0, hp = 100; while (hp > 0) { hp -= FBMechanics.BleedStep(b, .05f); t += .05f; }
            Assert.That(Mathf.Abs(t - 75), Is.LessThan(.1f));
        }
        [Test] public void Revive()
        {
            Assert.IsTrue(FBMechanics.CanReviveTarget(0, 3, 3, 0)); Assert.IsFalse(FBMechanics.CanReviveTarget(0, 6, 6, 0));
            Assert.IsFalse(FBMechanics.CanReviveTarget(3, 3, Mathf.Sqrt(18), 0)); Assert.IsTrue(FBMechanics.CanReviveTarget(1, 3, Mathf.Sqrt(10), 0));
            var a = new K { Knocked = true, Hp = 70 }; bool done = false; float t = 0; while (!done) { done = FBMechanics.ReviveStep(a, .1f, "pvl"); t += .1f; }
            Assert.That(Mathf.Abs(t - FBMechanics.KNOCK.reviveTime), Is.LessThan(.11f));
            FBMechanics.ReviveDone(a); Assert.IsFalse(a.Knocked); Assert.AreEqual(50, a.Hp);
        }
        [Test] public void Queda()
        {
            Assert.AreEqual(0, FBMechanics.FallDamage(4.4f, 2)); Assert.AreEqual(0, FBMechanics.FallDamage(6, .3f));
            Assert.AreEqual(40, FBMechanics.FallDamage(6, 1)); Assert.AreEqual(50, FBMechanics.FallDamage(7.5f, 1)); Assert.AreEqual(100, FBMechanics.FallDamage(15, 2));
            Assert.AreEqual(.5f, FBMechanics.LandVolume(7.5f), 1e-6);
        }
        [Test] public void ProtecaoSpawn()
        {
            Assert.AreEqual(5, FBMechanics.RollProtection(() => 0)); Assert.AreEqual(8, FBMechanics.RollProtection(() => 1));
            for (int i = 0; i < 200; i++) { var d = FBMechanics.RollProtection(); Assert.That(d, Is.InRange(5f, 8f)); }
        }
        [Test] public void Pulo()
        {
            Assert.IsTrue(FBMechanics.CanCoyoteJump(.1f, false)); Assert.IsFalse(FBMechanics.CanCoyoteJump(.13f, false)); Assert.IsFalse(FBMechanics.CanCoyoteJump(.05f, true));
            Assert.IsTrue(FBMechanics.BufferedJump(.12f)); Assert.IsFalse(FBMechanics.BufferedJump(.2f));
        }
        [Test] public void BrilhoLuneta()
        {
            Assert.AreEqual(0, FBMechanics.GlintScaleFor(4, 60, 60)); Assert.AreEqual(1.04f, FBMechanics.GlintScaleFor(20, 60, 60), 1e-5);
            Assert.AreEqual(3, FBMechanics.GlintScaleFor(200, 60, 60)); Assert.AreEqual(.52f, FBMechanics.GlintScaleFor(20, 30, 60), 1e-5);
            Assert.AreEqual(3, FBMechanics.RayPointDist(Vector3.zero, Vector3.forward, new Vector3(3, 0, 10)), 1e-5);
            Assert.IsTrue(float.IsPositiveInfinity(FBMechanics.RayPointDist(Vector3.zero, Vector3.forward, new Vector3(0, 0, -5))));
        }
        [Test] public void Killstreak()
        {
            var k = new FBKillstreak();
            for (int i = 1; i <= 9; i++) Assert.IsNull(k.Kill(i));
            Assert.AreEqual(10, k.Next()); Assert.AreEqual("caveirao", k.Kill(10));
            Assert.IsNull(k.Tick(11)); Assert.AreEqual("caveirao", k.Tick(12)); Assert.IsTrue(k.Ready(12)); Assert.AreEqual(15, k.Next());
            for (int i = 11; i <= 14; i++) k.Kill(i + 10);
            Assert.AreEqual("heli", k.Kill(30)); Assert.AreEqual("heli", k.Tick(32)); Assert.AreEqual("heli", k.Consume(40)); Assert.AreEqual(0, k.kills);
            Assert.IsFalse(k.Ready(50)); k.Kill(41, true); Assert.AreEqual(0, k.kills);
            for (int i = 0; i < 12; i++) k.Kill(100 + i); k.Died(); Assert.AreEqual(0, k.kills); Assert.IsNull(k.reward);
            var k2 = new FBKillstreak { kills = 9, firstKillAt = 0 }; k2.Kill(100); Assert.AreEqual(102, k2.revealAt);
        }
    }
}
