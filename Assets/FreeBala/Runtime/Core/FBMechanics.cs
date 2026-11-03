// Porte 1:1 do src/mechanics.js (regras recuperadas por engenharia reversa dos APKs — docs/re/MECANICAS.md):
//   IE = Invasão Elite 1.1.13 (MFPS 2.0)     CF = O Complexo / Complexo Favela 16066 (base do Free Bala)
// Puro (sem Unity além de Mathf): testado em Tests/EditMode/FBMechanicsTests.cs com os mesmos casos de tests/mechanics.test.mjs.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    // estado de knockdown/revive que as regras manipulam (FBCharacter implementa)
    public interface IKnockable { bool Knocked { get; set; } float Hp { get; set; } float MaxHp { get; } float KnockInvT { get; set; } float BleedAcc { get; set; } object RevivedBy { get; set; } float ReviveP { get; set; } bool Civ { get; } }

    public static class FBMechanics
    {
        // ---- KNOCKDOWN / REVIVE (IE: bl_KnockedReviveSettings + bl_PlayerHealthManager)
        public static class KNOCK
        {
            public const float moveSpeed = 1.5f, bleedDamage = 4, bleedTime = 3, invuln = .8f, reviveHealth = 50, reviveTime = 5, detectDistance = 5, detectAngle = 40;
        }
        // squad = BR do IE (revive 10 s); pvl = ELIM; tatico = CP. Clássico/FFA/treino = morte direta (instantDeathModes)
        static readonly Dictionary<string, float> KNOCK_MODE = new() { ["squad"] = 10, ["pvl"] = KNOCK.reviveTime, ["tatico"] = KNOCK.reviveTime };
        public static bool KnockMode(string id) => id != null && KNOCK_MODE.ContainsKey(id);
        public static float ReviveTime(string id) => id != null && KNOCK_MODE.TryGetValue(id, out var t) ? t : KNOCK.reviveTime;

        public static bool CanKnock(IKnockable a, string modeId, bool hasReviver) { if (!KnockMode(modeId) || a == null || a.Knocked || a.Civ) return false; return hasReviver; }
        public static void KnockDown(IKnockable a, float now) { a.Knocked = true; a.Hp = a.MaxHp > 0 ? a.MaxHp : 100; a.KnockInvT = now + KNOCK.invuln; a.BleedAcc = 0; a.RevivedBy = null; a.ReviveP = 0; }
        public static float BleedStep(IKnockable a, float dt)
        {
            if (!a.Knocked) return 0; a.BleedAcc += dt; float d = 0;
            while (a.BleedAcc >= KNOCK.bleedTime - 1e-4f) { a.BleedAcc -= KNOCK.bleedTime; d += KNOCK.bleedDamage; }   // folga: o web acumula em double
            return d;
        }
        public static bool KnockInvulnerable(IKnockable a, float now) => a.Knocked && now < a.KnockInvT;
        // bl_ReviveDetector.DetectTarget(): até 5 m e dentro de 40° do olhar (yaw do web: atan2(dx, dz))
        public static bool CanReviveTarget(float dx, float dz, float dist, float lookYaw)
        {
            if (dist > KNOCK.detectDistance) return false; if (dist < .9f) return true;
            float yaw = Mathf.Atan2(dx, dz), d = yaw - lookYaw; d = Mathf.Atan2(Mathf.Sin(d), Mathf.Cos(d));
            return Mathf.Abs(d) * Mathf.Rad2Deg <= KNOCK.detectAngle;
        }
        public static bool ReviveStep(IKnockable a, float dt, string modeId) { a.ReviveP = Mathf.Min(1, a.ReviveP + dt / ReviveTime(modeId)); return a.ReviveP >= 1; }
        public static void ReviveDone(IKnockable a) { a.Knocked = false; a.Hp = KNOCK.reviveHealth; a.ReviveP = 0; a.BleedAcc = 0; a.RevivedBy = null; }

        // ---- DANO DE QUEDA (IE: bl_FirstPersonController.CalculateFall)
        public const float FallSafe = 4.5f, FallDeath = 15, FallMinAir = .4f;
        public static float FallDamage(float dist, float airTime) { if (!(airTime > FallMinAir) || !(dist > FallSafe)) return 0; return Mathf.Floor(dist / FallDeath * 100); }
        public static float LandVolume(float dist) => Mathf.Clamp01(dist / FallDeath);

        // ---- PROTEÇÃO DE SPAWN (CF: SpawnProtectionRules)
        public const float ProtMin = 5, ProtMax = 8; public const bool ProtBreakOnFire = true; public const float VoidY = -70;
        public static float RollProtection(Func<float> rand = null) => ProtMin + (rand ?? (() => UnityEngine.Random.value))() * (ProtMax - ProtMin);

        // ---- PULO (coyote / buffer)
        public const float Coyote = .12f, JumpBuffer = .12f;
        public static bool CanCoyoteJump(float offGroundT, bool jumpedSinceGround) => !jumpedSinceGround && offGroundT <= Coyote;
        public static bool BufferedJump(float pressedT) => pressedT <= JumpBuffer;

        // ---- BRILHO DA LUNETA (IE: bl_SniperGlint)
        public const float GlintMaxDist = 900, GlintMinDist = 5, GlintBaseDist = 10, GlintScale = .52f, GlintArea = 12, GlintMaxScale = 3;
        public static float GlintScaleFor(float dist, float fov, float defFov) { if (dist <= GlintMinDist || dist > GlintMaxDist) return 0; return Mathf.Min(fov / defFov * (dist / GlintBaseDist) * GlintScale, GlintMaxScale); }
        public static float RayPointDist(Vector3 o, Vector3 d, Vector3 p) { var v = p - o; float t = Vector3.Dot(v, d); if (t < 0) return float.PositiveInfinity; return (v - d * t).magnitude; }
    }

    // ---- KILLSTREAK (CF: SantaStudio.KillStreak.KillStreakSettings + HeliGunnerDirector)
    public class FBKillstreak
    {
        public class Reward { public int kills; public float run, dmgMul, cooldown; public string name; }
        public static readonly Dictionary<string, Reward> STREAK = new()
        {
            ["caveirao"] = new Reward { kills = 10, run = 45, dmgMul = .5f, cooldown = 6, name = "CAVEIRÃO" },
            ["heli"] = new Reward { kills = 15, run = 40, dmgMul = .4f, cooldown = 6, name = "HELICÓPTERO" },
        };
        public const float RevealDelay = 2, RevealMaxWait = 5, HoldToExit = .6f; public const bool ResetOnDeath = true, CountWhileGunner = false;
        public int kills; public string reward, pending; public float revealAt, firstKillAt, cooldownUntil;
        public FBKillstreak() { ResetState(); }
        public void ResetState() { kills = 0; reward = null; pending = null; revealAt = 0; firstKillAt = 0; }
        public string Kill(float now, bool gunner = false)
        {
            if (gunner && !CountWhileGunner) return null;
            kills++; string earned = null;
            if (kills >= STREAK["heli"].kills && reward != "heli") earned = "heli";
            else if (kills >= STREAK["caveirao"].kills && reward == null) earned = "caveirao";
            if (earned != null)
            {
                reward = earned; pending = earned; if (firstKillAt == 0) firstKillAt = now;
                revealAt = Mathf.Min(now + RevealDelay, firstKillAt + RevealMaxWait);
            }
            return earned;
        }
        public string Tick(float now) { if (pending != null && now >= revealAt) { var r = pending; pending = null; firstKillAt = 0; return r; } return null; }
        public int? Next() => reward == "heli" ? (int?)null : reward == "caveirao" ? STREAK["heli"].kills : STREAK["caveirao"].kills;
        public bool Ready(float now) => reward != null && pending == null && now >= cooldownUntil;
        public string Consume(float now) { var r = reward; if (r == null) return null; ResetState(); cooldownUntil = now + STREAK[r].run + STREAK[r].cooldown; return r; }
        public void Died() { if (ResetOnDeath) ResetState(); }
    }

    // ---- KIT MÉDICO (src/medkit.js): canalização 3 s, cura 75 em 2,5 s, máx. 4, 2 por vida, regeneração passiva até 40
    public static partial class FBMed
    {
        public const float Use = 3, Heal = 75, Hot = 2.5f, PassiveCap = 40, MoveMul = .6f; public const int Max = 4, Start = 2, Price = 300;
    }
}
