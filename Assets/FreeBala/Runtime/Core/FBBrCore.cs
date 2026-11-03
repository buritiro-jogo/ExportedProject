// Porte 1:1 do src/brcore.js — regras do BATTLE ROYALE e da "capa" (headshot) recuperadas do APK Sigma 1.0.113 (código do Free Fire,
// docs/re/SIGMA_BR.md). As tabelas (WeaponTable/EquipmentTable/GameVarDef) vêm do fb_data.json (exportadas do próprio brcore.js).
// Testado em Tests/EditMode/FBBrCoreTests.cs com os casos de tests/brcore.test.mjs.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public static class FBBrCore
    {
        public class FFW { public string key; public float dmg, iv, bite, range, full, minD, minDmg, head, body, limb, mag, move; public float? hsFull, hsMin, hsMinRatio; public int pellets = 1; }
        public class Armor { public string key, kind; public int lv; public float max, red, dur; }
        public const float HEAD = 5.5f, BODY = 1f, LIMB = .75f, NO_HEAD_AFTER_POSE = .7f, HP = 200;
        static Dictionary<string, FFW> weapons; static Dictionary<string, object> equip, byId, byClass;
        static void Load()
        {
            if (weapons != null) return; GameData.Load(); weapons = new Dictionary<string, FFW>();
            var ff = Json.O(GameData.Root, "ff");
            foreach (var kv in Json.O(ff, "weapons") ?? new Dictionary<string, object>())
            {
                var o = kv.Value; float? N(string k) => Json.Get(o, k) is double d ? (float)d : (float?)null;
                weapons[kv.Key] = new FFW { key = kv.Key, dmg = Json.F(o, "dmg"), iv = Json.F(o, "iv"), bite = Json.F(o, "bite"), range = Json.F(o, "range"), full = Json.F(o, "full"), minD = Json.F(o, "minD"),
                    minDmg = Json.F(o, "minDmg"), head = Json.F(o, "head"), body = Json.F(o, "body"), limb = Json.F(o, "limb"), mag = Json.F(o, "mag"), move = Json.F(o, "move", 1), pellets = Json.I(o, "pellets", 1),
                    hsFull = N("hsFull"), hsMin = N("hsMin"), hsMinRatio = N("hsMinRatio") };
            }
            equip = Json.O(ff, "equip"); byId = Json.O(ff, "byId"); byClass = Json.O(ff, "byClass");
        }
        public static FFW Weapon(string key) { Load(); return key != null && weapons.TryGetValue(key, out var w) ? w : null; }
        // Free Bala -> linha da WeaponTable do FF (por id, senão por classe, senão M4A1)
        public static FFW FFWeapon(string id, string cls)
        {
            Load(); var k = Json.S(byId, id ?? ""); if (k != null && weapons.TryGetValue(k, out var w)) return w;
            k = Json.S(byClass, cls ?? ""); if (k != null && weapons.TryGetValue(k, out w)) return w; return weapons.TryGetValue("M4A1", out w) ? w : new FFW { dmg = 29, full = 40, minD = 78, minDmg = 11 };
        }
        public static float BaseDamage(FFW w, float dist) { if (dist <= w.full) return w.dmg; if (dist >= w.minD) return w.minDmg; return w.dmg + (w.minDmg - w.dmg) * (dist - w.full) / (w.minD - w.full); }
        public static float PartRatio(FFW w, string part, float dist)
        {
            if (part == "head")
            {
                float full = HEAD + w.head; if (w.hsFull == null || w.hsMin == null || w.hsMinRatio == null) return full;
                float min = full + w.hsMinRatio.Value; if (dist <= w.hsFull) return full; if (dist >= w.hsMin) return min;
                return full + (min - full) * (dist - w.hsFull.Value) / (w.hsMin.Value - w.hsFull.Value);
            }
            if (part == "limb") return LIMB + w.limb;
            return BODY + w.body;
        }
        public static float ArmorFactor(Armor item, float bite = 0) { if (item == null || item.dur <= 0) return 1; float k = Mathf.Clamp01(item.dur / item.max); return 1 - k * item.red * (1 - Mathf.Clamp01(bite)); }
        public static Armor NewArmor(string key) { Load(); var e = Json.O(equip, key); return e == null ? null : new Armor { key = key, kind = Json.S(e, "kind"), lv = Json.I(e, "lv"), max = Json.F(e, "max"), red = Json.F(e, "red"), dur = Json.F(e, "max") }; }
        public struct HitResult { public float dmg, absorbed, raw; public string part; }
        public static HitResult Hit(FFW w, string part, float dist, Armor vest = null, Armor helmet = null, float poseSwitchAgo = 9)
        {
            if (part == "head" && poseSwitchAgo < NO_HEAD_AFTER_POSE) part = "body";
            float raw = BaseDamage(w, dist) * PartRatio(w, part, dist); var item = part == "head" ? helmet : vest;
            float f = ArmorFactor(item, w.bite), dmg = raw * f, abs = raw - dmg; if (item != null && abs > 0) item.dur = Mathf.Max(0, item.dur - abs);
            return new HitResult { dmg = dmg, part = part, absorbed = abs, raw = raw };
        }

        // ---- queda livre / mergulho / paraquedas (GameVarDef)
        public struct SkyP { public float vMin, vMax, hMin, hMax, delta, rot, turnR; }
        public static readonly SkyP Dive = new() { vMin = -9, vMax = -15, hMin = 0, hMax = 5, delta = 2, rot = 45 }, Surf = new() { vMin = -6, vMax = -12, hMin = 0, hMax = 20, delta = 10, rot = 45 },
            Para = new() { vMin = -4, vMax = -8, hMin = 1.5f, hMax = 7, turnR = 9 };
        public const float AutoOpenT = 10, SurfMinH = 160, SafeOpenH = 35, AirlineStart = 500;
        public const float CamDiveUp = 2.8f, CamDiveBack = .7f, CamParaUp = 2.5f, CamParaBack = 6, LandShake = 1.5f, LandShakeT = 1;
        static SkyP Of(string st) => st == "para" ? Para : st == "surf" ? Surf : Dive;
        public static Vector2 SkyVelocity(string st, float fwd, Vector2 cur, float dt)        // (h, v)
        {
            var P = Of(st); float tH, tV;
            if (st == "para") { float t = (Mathf.Clamp(fwd, -1, 1) + 1) / 2; tH = P.hMin + (P.hMax - P.hMin) * t; tV = P.vMin + (P.vMax - P.vMin) * t; }
            else { float t = Mathf.Clamp01(fwd); tH = P.hMin + (P.hMax - P.hMin) * t; tV = P.vMin + (P.vMax - P.vMin) * t; }
            float k = 1 - Mathf.Exp(-(st == "para" ? 3 : P.delta) * dt);
            return new Vector2(cur.x + (tH - cur.x) * k, cur.y + (tV - cur.y) * k);
        }
        public static float SkyTurnRate(string st, float h) => st == "para" ? Mathf.Max(.15f, h / Para.turnR) : Of(st).rot * Mathf.Deg2Rad;
        public static bool ShouldOpen(float fallT, float hAboveGround) => fallT >= AutoOpenT || hAboveGround <= SafeOpenH;
        public static bool CanSurf(float hAboveGround) => hAboveGround > SurfMinH;
        public struct Route { public Vector2 a, b, dir; public float len; }
        public static Route Airline(float cx, float cz, float r, Func<float> rand = null, float scale = 1)
        {
            rand ??= () => UnityEngine.Random.value;
            float an = rand() * Mathf.PI * 2, off = (rand() - .5f) * r * .6f, L = r + AirlineStart * scale, dx = Mathf.Cos(an), dz = Mathf.Sin(an), px = -dz * off, pz = dx * off;
            return new Route { a = new Vector2(cx + px - dx * L, cz + pz - dz * L), b = new Vector2(cx + px + dx * L, cz + pz + dz * L), dir = new Vector2(dx, dz), len = 2 * L };
        }
        public static float JumpPointFor(Route r, float tx, float tz) { float vx = tx - r.a.x, vz = tz - r.a.y; return Mathf.Clamp(vx * r.dir.x + vz * r.dir.y, 0, r.len); }
        // zona do BR: 5 fases proporcionais ao mapa, dano crescente fora da zona
        public struct Phase { public float wait, shrink, k, dmg; }
        public static readonly Phase[] PHASES = { new() { wait = 45, shrink = 35, k = .62f, dmg = 2 }, new() { wait = 35, shrink = 30, k = .55f, dmg = 4 }, new() { wait = 30, shrink = 25, k = .5f, dmg = 7 },
            new() { wait = 25, shrink = 20, k = .45f, dmg = 11 }, new() { wait = 20, shrink = 18, k = .05f, dmg = 16 } };
    }
}
