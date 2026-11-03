// Economia do 4V4 / Farinha × Buriti (estilo Free Fire): dinheiro inicial, loja na fase de compra (SHOP do main.js com os mesmos preços),
// renda por rodada crescente + vitória/sequência de derrotas, bônus por abate/headshot, bots compram (melhor categoria que cabe no bolso,
// às vezes economizam) + colete/capacete/kit. Quem sobrevive mantém o equipamento, quem morre volta desarmado. + kit médico (medkit.js)
// e armas/acessórios/coletes caídos no chão (saque: tecla X).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public static class FBEconomy
    {
        public const int START = 1000, BASE = 1500, STEP = 250, BASE_MAX = 3000, WIN = 500, LOSE = 250, KILL = 250, HEAD = 100, MAX = 9999; public const string BOT_PISTOL = "g18";
        public static bool EconMode(string m) => m == "squad" || m == "4v4" || m == "pvl" || m == "10x10";   // [parceiro 31/10] o 10x10 também tem compra/economia
        public class Item { public string id, name, sub, icon, cls; public int price; public bool gear, own, rebuy = true, best; public string note; public (string, float)[] stats; }
        public class Cat { public string cat, icon; public List<(string id, int price)> items; }
        public static readonly List<Cat> SHOP = new()
        {
            new Cat { cat = "PISTOLAS", icon = "pistol", items = new() { ("g18", 300), ("pistola_308", 400), ("beretta_gold", 450), ("1911_gold", 500), ("desert_eagle", 700), ("desert_eagle_gold", 800) } },
            new Cat { cat = "SUBMETRALHADORAS", icon = "smg", items = new() { ("uzi", 800), ("czskorpion", 900), ("mp5", 1100), ("ump", 1200), ("p90", 1500) } },
            new Cat { cat = "ESCOPETAS", icon = "shotgun", items = new() { ("remington_870", 1000), ("m31", 1200) } },
            new Cat { cat = "FUZIS", icon = "rifle", items = new() { ("ar15", 1900), ("aks74", 2000), ("ak", 2100), ("m4", 2100), ("m16", 2200), ("galil_ace", 2300), ("g36", 2300), ("lr_300", 2300), ("sig", 2400), ("parafal_1", 2400), ("acr", 2500), ("scar", 2500), ("fal", 2600), ("ak12", 2700), ("ar10", 2800), ("rpk", 3000) } },
            new Cat { cat = "SNIPERS", icon = "scope", items = new() { ("parafal", 2600), ("sniper_hunting", 2800), ("barret_50", 4500) } },
            new Cat { cat = "EQUIPAMENTO", icon = "vest", items = new() { ("kit", 1000), ("colete", 650), ("capacete", 450), ("medkit", FBMed.Price) } },
        };
        static readonly Dictionary<string, (string name, string sub)> GEAR = new() { ["colete"] = ("COLETE", "BLINDAGEM · ABSORVE 45%"), ["capacete"] = ("CAPACETE", "HEADSHOT −40%"), ["kit"] = ("KIT COLETE + CAPACETE", "PROTEÇÃO COMPLETA · ECONOMIZE $100"), ["medkit"] = ("KIT MÉDICO", $"+{FBMed.Heal} DE VIDA · TECLA H") };
        static readonly Dictionary<string, int> CLS_PRICE = new() { ["pistola"] = 400, ["submetralhadora"] = 1000, ["escopeta"] = 1000, ["fuzil"] = 2200, ["sniper"] = 2800, ["explosivo"] = 5000 };
        static readonly Dictionary<string, int> TIER = new() { ["pistola"] = 0, ["submetralhadora"] = 1, ["escopeta"] = 1, ["fuzil"] = 2, ["sniper"] = 2, ["explosivo"] = 0 };
        public static int PriceOf(string id) { foreach (var c in SHOP) foreach (var (i, p) in c.items) if (i == id) return p; return GameData.Weapons.TryGetValue(id, out var d) && CLS_PRICE.TryGetValue(d.cls, out var pr) ? pr : 2000; }
        public static readonly Dictionary<FBCharacter, int> money = new(); static readonly int[] streakL = new int[2];
        public static bool buying; public static float buyT, buyTotal;
        // r51 (pedido 31/10: "amplie o tempo de compra no modo 4v4"): 4V4 15 → 30 s, Farinha × Buriti 12 → 25 s; -fbcompracurta = antes
        public static bool LongBuy = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbcompracurta") < 0;
        static readonly string[][] BOT_GUNS = { new[] { "m4", "ar15", "g36", "scar", "mp5", "ump", "m16", "lr_300", "sig", "acr", "remington_870" }, new[] { "ak", "ak12", "fal", "rpk", "aks74", "parafal_1", "galil_ace", "g3", "uzi", "m31" } };
        static readonly Dictionary<string, float> GUN_MUL = new() { ["pistola"] = .6f, ["submetralhadora"] = .85f, ["escopeta"] = 1, ["fuzil"] = 1, ["sniper"] = 1.25f, ["explosivo"] = 1 };
        static List<string>[] botGuns;

        public static int Money(FBCharacter a) => a && money.TryGetValue(a, out var m) ? m : 0;
        public static bool Econ => FBMatch.I && FBMatch.I.mode != null && FBMatch.I.mode.econ;
        public static void Reset() { foreach (var a in FBMatch.Actors()) { money[a] = START; a.armor = 0; a.helmet = false; if (!a.isPlayer && !a.isRemote && a.team < 2) UnarmBot(a); } streakL[0] = streakL[1] = 0; Hud(); }
        public static void Give(FBCharacter a, int v, string why)
        {
            if (!a || !money.ContainsKey(a)) return; money[a] = Mathf.Min(MAX, money[a] + v);
            if (a.isPlayer) { Hud($"+${v}{(why != null ? " " + why : "")}"); if (v > 0 && FBMatch.I && FBMatch.I.state == "play") FBAudio.Play2D("sg_coin", .45f, 0); }
        }
        public static void Round(int winner, int round)
        {
            int b = Mathf.Min(BASE_MAX, BASE + STEP * (round - 1)); for (int t = 0; t < 2; t++) streakL[t] = t == winner ? 0 : Mathf.Min(3, streakL[t] + 1);
            foreach (var a in FBMatch.Actors().ToList()) { if (!money.ContainsKey(a) || a.team > 1) continue; Give(a, b + (a.team == winner ? WIN : LOSE * streakL[a.team]), a.team == winner ? "VITÓRIA" : "RODADA"); }
        }
        public static void Hud(string plus = null) { var p = FBMatch.Player; if (!Econ || !p) { FBHUD.I?.SetCash(-1, null, 0, false); return; } FBHUD.I?.SetCash(Money(p), plus, Mathf.RoundToInt(p.armor), p.helmet); if (FBShop.Open) FBShop.Refresh(); }
        public static void ShopTime(float t) { buyT = t; FBShop.SetTime(t); }
        public static void BuyPhase(bool on, float total = 0)
        {
            if (on && total > 0) { buyTotal = total; buyT = total; }                                // a barra da loja mede a fase inteira (reabrir com B não a reinicia)
            buying = on; FBHUD.I?.BuyHint(on);
            if (on) { foreach (var b in FBMatch.Bots.ToList()) if (b && b.alive && b.team < 2) BotBuy(b); if (FBMatch.Player.alive) FBShop.Show(true); }
            else FBShop.Show(false);
            Hud();
        }

        // ---- armas dos bots (visíveis na mão) = o que derrubam ao morrer
        static void EnsureBotGuns()
        {
            if (botGuns != null) return; bool econ = Econ || FBSession.mode == "br"; botGuns = new[] { new List<string>(), new List<string>() };
            for (int t = 0; t < 2; t++)
            {
                var sh = BOT_GUNS[t].Where(id => GameData.Weapons.ContainsKey(id) && FBRegistry.I.Weapon(id)).OrderBy(_ => Random.value).ToList(); var smg = sh.Where(id => GameData.Weapons[id].cls == "submetralhadora").ToList();
                botGuns[t].AddRange(econ ? smg.Take(1).Concat(sh.Where(id => !smg.Contains(id)).Take(2)) : sh.Take(3));
                if (botGuns[t].Count == 0) botGuns[t].AddRange(GameData.Weapons.Keys.Where(id => FBRegistry.I.Weapon(id) && GameData.Weapons[id].slot == 0).Take(3));
            }
        }
        public static void ResetMatch() { botGuns = null; money.Clear(); }
        public static void ArmBot(FBCharacter b, string forced = null)
        {
            EnsureBotGuns(); var id = forced ?? (botGuns[Mathf.Clamp(b.team, 0, 1)].Count > 0 ? FBUtil.Pick(botGuns[Mathf.Clamp(b.team, 0, 1)]) : null);
            if (id == null || !GameData.Weapons.TryGetValue(id, out var def)) return;
            if (Econ && forced == null) { if (!b.isPlayer) { UnarmBot(b); return; } }
            var wu = b.GetComponent<FBWeaponUser>(); wu.Give(id); var bot = b.GetComponent<FBBot>(); if (bot) bot.gunMul = GUN_MUL.TryGetValue(def.cls, out var m) ? m : 1;
        }
        public static void UnarmBot(FBCharacter b) { var wu = b.GetComponent<FBWeaponUser>(); wu.Strip(); var bot = b.GetComponent<FBBot>(); if (bot) bot.gunMul = .6f; }
        public static void BotBuy(FBCharacter b)
        {
            if (!money.ContainsKey(b)) return; EnsureBotGuns(); var wu = b.GetComponent<FBWeaponUser>(); bool armed = wu.Cur != null; int curT = armed ? (TIER.TryGetValue(wu.Cur.cls, out var tt) ? tt : 0) : -1;
            var all = GameData.Weapons.Keys.Where(id => FBRegistry.I.Weapon(id) && GameData.Weapons[id].cls != "explosivo").ToList(); var pool = all.Where(id => GameData.Weapons[id].cls != "pistola").ToList();
            var mine = pool.Where(id => botGuns[Mathf.Clamp(b.team, 0, 1)].Contains(id)).ToList();
            var cand = (mine.Count > 0 ? mine : pool).Where(id => PriceOf(id) <= money[b] && TIER[GameData.Weapons[id].cls] > curT).ToList();
            if (!armed && cand.Count == 0) cand = all.Where(id => GameData.Weapons[id].cls == "pistola" && PriceOf(id) <= money[b]).ToList();
            if (cand.Count > 0 && !(armed && money[b] < 2000 && Random.value < .4f)) { int best = cand.Max(id => TIER[GameData.Weapons[id].cls]); var id = FBUtil.Pick(cand.Where(x => TIER[GameData.Weapons[x].cls] == best).ToList()); money[b] -= PriceOf(id); ArmBot(b, id); }
            if (wu.Cur == null && GameData.Weapons.ContainsKey(BOT_PISTOL)) ArmBot(b, BOT_PISTOL);
            if (b.armor < 60 && money[b] >= 650) { money[b] -= 650; b.armor = 100; }
            if (!b.helmet && money[b] >= 450 && Random.value < .6f) { money[b] -= 450; b.helmet = true; }
            if (b.kits < 2 && money[b] >= FBMed.Price + 400) { money[b] -= FBMed.Price; b.kits++; }
        }

        // ---- loja do jogador
        public static List<(string cat, string icon, List<Item> items)> ShopData()
        {
            var p = FBMatch.Player; var wu = p.GetComponent<FBWeaponUser>(); var res = new List<(string, string, List<Item>)>();
            foreach (var c in SHOP)
            {
                var items = new List<Item>();
                foreach (var (id, price) in c.items)
                {
                    if (GEAR.TryGetValue(id, out var g))
                    {
                        var it = new Item { id = id, price = price, name = g.name, sub = g.sub, gear = true };
                        if (id == "colete") { it.own = p.armor > 0; it.rebuy = p.armor < 100; it.note = $"{Mathf.RoundToInt(p.armor)}/100"; }
                        if (id == "capacete") it.own = p.helmet; if (id == "kit") { it.own = p.armor >= 100 && p.helmet; it.rebuy = false; it.best = true; }
                        if (id == "medkit") { it.own = FBMed.kits >= FBMed.Max; it.rebuy = FBMed.kits < FBMed.Max; it.note = $"{FBMed.kits}/{FBMed.Max}"; }
                        items.Add(it);
                    }
                    else if (GameData.Weapons.TryGetValue(id, out var d) && FBRegistry.I.Weapon(id))
                        items.Add(new Item { id = id, price = price, name = d.name.ToUpper(), cls = d.cls, sub = d.cls.ToUpper(), own = wu.Has(id), rebuy = false, stats = WStats(id) });
                }
                if (items.Count > 0) res.Add((c.cat, c.icon, items));
            }
            return res;
        }
        static (string, float)[] WStats(string id)
        {
            // r51: as barras da loja vêm do MESMO modelo que calcula o dano (antes: números do pacote LPSP, sem relação com o tiro)
            if (GameData.Weapons.TryGetValue(id, out var wd) && FBDamageModel.Of(wd) is FBDamageModel.Stat ms)
                return new[] { ("DANO", Mathf.Min(1, ms.dmg * ms.pellets / 130f)), ("CADÊNCIA", Mathf.Min(1, ms.rpm / 900)), ("PRECISÃO", Mathf.Clamp(1.25f - ms.acc * .55f - (ms.pellets > 1 ? .35f : 0), .05f, 1)), ("ALCANCE", Mathf.Clamp01((ms.rm - .65f) / .35f)), ("MOBILIDADE", Mathf.Min(1, ms.move / .98f)) };
            var st = Json.O(Json.O(GameData.WeaponsRaw, id), "stats"); float rpm = Json.F(st, "roundsPerMinutes", 60 / Mathf.Max(.01f, Json.F(st, "secondsBetweenShots", .1f))), dmg = Json.F(st, "baseDamage", 25) * Mathf.Max(1, Json.F(st, "shotCount", 1));
            return new[] { ("DANO", Mathf.Min(1, dmg / 130)), ("CADÊNCIA", Mathf.Min(1, rpm / 1000)), ("PRECISÃO", Mathf.Clamp((100 - Json.F(st, "spread", .25f) * 60) / 105, .05f, 1)), ("MOBILIDADE", Mathf.Min(1, Json.F(st, "multiplierMovementSpeed", 1) * 100 / 115)) };
        }
        public static void Buy(Item it)
        {
            var p = FBMatch.Player; if (!p.alive) return; var wu = p.GetComponent<FBWeaponUser>();
            if (FBMatch.I.mode.arsenal) { if (!it.gear && GameData.Weapons.ContainsKey(it.id)) { wu.Give(it.id); FBAudio.Play2D("reload", .5f); FBHUD.I?.Toast("ARSENAL", it.name, new[] { "FUZIL", "SMG", "PISTOLA" }[Mathf.Clamp(GameData.Weapons[it.id].slot, 0, 2)], new Color(.96f, .67f, .09f)); } FBShop.Refresh(); return; }
            if (!buying) return;
            if (it.own && !it.rebuy) { if (!it.gear) wu.SwitchTo(wu.SlotOfId(it.id)); return; }
            if (Money(p) < it.price) { FBAudio.Synth("deny"); return; }
            if (it.gear)
            {
                if (it.id == "medkit") { if (!FBMed.Add()) { FBAudio.Synth("deny"); return; } }
                else if (it.id == "colete") p.armor = 100; else if (it.id == "capacete") p.helmet = true; else if (it.id == "kit") { p.armor = 100; p.helmet = true; }
                money[p] -= it.price; FBAudio.Synth("buy"); FBAudio.Play2D("reload", .4f); Hud(); return;
            }
            money[p] -= it.price; wu.Give(it.id); FBAudio.Synth("buy"); FBAudio.Play2D("reload", .5f);
            FBHUD.I?.Toast("COMPRADO", it.name, $"−${it.price} · {new[] { "FUZIL", "SMG", "PISTOLA" }[Mathf.Clamp(GameData.Weapons[it.id].slot, 0, 2)]}", new Color(.21f, .76f, .35f)); Hud();
        }
        public static IEnumerable<Item> ArsenalItems() => GameData.Weapons.Values.Where(d => FBRegistry.I.Weapon(d.id)).OrderBy(d => d.cls).ThenBy(d => d.name).Select(d => new Item { id = d.id, name = d.name.ToUpper(), cls = d.cls, sub = d.cls.ToUpper(), price = 0 });
    }

    // ---- kit médico do jogador. r45 (pedido: "só poderá se curar enquanto estiver PARADO"): 0,5 s de início (Heal_Start, kit ainda não
    // gasto) e a vida sobe DURANTE a animação nos 2,5 s seguintes (75 no total, kit gasto ao começar a subir). Qualquer movimento/ação cancela
    // na hora (FBPlayer.HealGuard): para a animação e a recuperação. Antes: canalizava 3 s andando e curava depois, já com a arma na mão
    public static partial class FBMed
    {
        public static int kits; public static bool inf, spent; public static string state; public static float t, hotLeft;
        public const float Wind = Use - Hot;                                                     // 0,5 s de início
        public static bool Busy => state == "use"; public static bool Healing => Busy && t > Wind;
        public static void ResetPlayer(int n, bool infinite) { state = null; t = 0; hotLeft = 0; kits = n; inf = infinite; }
        public static bool Add(int n = 1) { if (kits >= Max) return false; kits = Mathf.Min(Max, kits + n); return true; }
        public static string Why(FBCharacter p) { if (!p || !p.alive) return "morto"; if (state != null) return "em uso"; if (!inf && kits <= 0) return "SEM KIT MÉDICO"; if (p.hp >= p.maxHp) return "VIDA CHEIA"; return null; }
        public static void Begin() { state = "use"; t = 0; spent = false; hotLeft = 0; }
        public static bool Cancel() { if (state != "use") return false; state = null; t = 0; hotLeft = 0; return true; }
        public static string Update(float dt, System.Action<float> heal)
        {
            string ev = null;
            if (state == "use")
            {
                float t0 = t; t += dt;
                if (!spent && t >= Wind) { spent = true; if (!inf) kits--; }
                if (t > Wind) heal(Heal * (Mathf.Min(t, Use) - Mathf.Max(t0, Wind)) / Hot);
                if (t >= Use) { state = null; t = 0; ev = "done"; }
            }
            return ev;
        }
        public static float Progress => state == "use" ? Mathf.Min(1, t / Use) : 0;
    }

    // ---- armas, acessórios e coletes caídos (saque)
    public static class FBLoot
    {
        public class L { public string id; public GameObject obj; public Transform ring; public Vector3 pos; public int ammo, reserve; public float t; public string armor; }
        public static readonly List<L> loots = new(); public static L near;
        public static void Clear() { foreach (var l in loots) if (l.obj) Object.Destroy(l.obj); loots.Clear(); near = null; }
        static Material ringMat;
        public static void DropWeapon(string id, Vector3 pos, int ammo = -1, int reserve = -1)
        {
            if (!GameData.Weapons.TryGetValue(id, out var def)) return; var model = FBRegistry.I.Weapon(id); if (!model) return;
            var obj = new GameObject("Saque_" + id); var w = FBWeaponModel.Make(def, obj.transform); foreach (var c in w.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            // deita a arma de lado: cano na horizontal, dimensão mais fina na vertical
            w.transform.localRotation = Quaternion.Euler(0, 0, 90); w.transform.localScale = Vector3.one * FBCharacter.WeaponWorld * FBCharacter.WeaponModelFix(id);   // r50: do tamanho da arma na mão (r49: 1 = AK de 0,65 m de gente; antes 0,6175)
            var gy = FBWorld.GroundY(pos.x, pos.y + 1, pos.z, .3f, 4) ?? pos.y; obj.transform.position = new Vector3(pos.x, gy, pos.z); obj.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            var bb = new Bounds(obj.transform.position, Vector3.zero); foreach (var r in w.GetComponentsInChildren<Renderer>()) bb.Encapsulate(r.bounds); w.transform.position += Vector3.up * (gy + .03f - bb.min.y);
            ringMat ??= RingMat(); var ring = FBZone.Ring(32, .42f, .5f, ringMat); ring.transform.SetParent(obj.transform, false); ring.transform.localPosition = Vector3.up * .03f;
            loots.Add(new L { id = id, obj = obj, ring = ring.transform, pos = obj.transform.position, ammo = ammo < 0 ? def.mag : ammo, reserve = reserve < 0 ? def.mag * 2 : reserve });
            int max = FBMatch.I && FBMatch.I.mode != null && FBMatch.I.mode.br ? 700 : 24; while (loots.Count > max) { if (loots[0].obj) Object.Destroy(loots[0].obj); loots.RemoveAt(0); }
        }
        public static void DropArmor(string key, Vector3 pos)
        {
            var a = FBBrCore.NewArmor(key); if (a == null) return; var obj = new GameObject("Saque_" + key); var box = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.Destroy(box.GetComponent<Collider>()); box.transform.SetParent(obj.transform, false);
            box.transform.localScale = a.kind == "vest" ? new Vector3(.5f, .12f, .4f) : new Vector3(.3f, .2f, .3f); box.transform.localPosition = Vector3.up * .08f;
            var m = new Material(FBShaders.Lit); m.SetColor("_BaseColor", a.lv == 3 ? new Color(.55f, .3f, .7f) : a.lv == 2 ? new Color(.2f, .45f, .8f) : new Color(.45f, .45f, .4f)); box.GetComponent<Renderer>().sharedMaterial = m;
            var gy = FBWorld.GroundY(pos.x, pos.y + 1, pos.z, .3f, 4) ?? pos.y; obj.transform.position = new Vector3(pos.x, gy, pos.z);
            ringMat ??= RingMat(); var ring = FBZone.Ring(32, .42f, .5f, ringMat); ring.transform.SetParent(obj.transform, false); ring.transform.localPosition = Vector3.up * .03f;
            loots.Add(new L { armor = key, obj = obj, ring = ring.transform, pos = obj.transform.position });
        }
        public static void DropRandomAtt(WeaponDef w, Vector3 pos) { }          // acessórios avulsos: a mochila usa os encaixes da arma (FBBag)
        static Material RingMat() { var m = new Material(FBShaders.Unlit); m.SetColor("_BaseColor", new Color(1, .77f, 0, .7f)); m.SetFloat("_Surface", 1); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); if (m.renderQueue < 3000) m.renderQueue = 3000; m.SetFloat("_Cull", 0); m.SetFloat("_ZWrite", 0); m.SetInt("_SrcBlend", 5); m.SetInt("_DstBlend", 10); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3050; return m; }
        public static void Update(float dt, FBCharacter player)
        {
            near = null; float nd = 2.3f; bool br = FBMatch.I && FBMatch.I.mode != null && FBMatch.I.mode.br;
            for (int i = loots.Count - 1; i >= 0; i--)
            {
                var l = loots[i]; l.t += dt; if (!l.obj) { loots.RemoveAt(i); continue; }
                if (!br && l.t > 120) { Object.Destroy(l.obj); loots.RemoveAt(i); continue; }
                float d = (l.pos - player.Pos).Flat().magnitude; if (d < nd && Mathf.Abs(l.pos.y - player.Pos.y) < 2) { nd = d; near = l; }
                l.ring.localScale = Vector3.one * (1 + .05f * Mathf.Sin(l.t * 4));
            }
            if (near == null || !player.alive) FBHUD.I?.Prompt(null, null);
            else if (near.armor != null) { var a = FBBrCore.NewArmor(near.armor); FBHUD.I?.Prompt("X", $"PEGAR {ArmorLabel(near.armor)}", $"EQUIPAMENTO · REDUZ O DANO {Mathf.RoundToInt(a.red * 100)}%"); }
            else FBHUD.I?.Prompt("X", "PEGAR " + GameData.Weapons[near.id].name.ToUpper(), $"{near.ammo} / {near.reserve}");
        }
        public static string ArmorLabel(string key) { var a = FBBrCore.NewArmor(key); return a == null ? key : $"{(a.kind == "vest" ? "COLETE" : "CAPACETE")} NV.{a.lv}"; }
        public static void Pickup(FBCharacter p)
        {
            var l = near; if (l == null) return; var wu = p.GetComponent<FBWeaponUser>(); if (wu.switching > 0) return;
            if (l.armor != null)
            {
                var it = FBBrCore.NewArmor(l.armor); var old = it.kind == "vest" ? p.vest : p.helmetFF;
                if (old != null && old.dur > 0 && old.lv > it.lv) { FBHUD.I?.Center("VOCÊ JÁ TEM " + ArmorLabel(old.key) + " MELHOR", 1.4f); FBAudio.Synth("deny"); return; }
                Object.Destroy(l.obj); loots.Remove(l); near = null; if (it.kind == "vest") p.vest = it; else p.helmetFF = it; FBAudio.Play2D("ie_crouch", .6f); FBAudio.Play2D("click", .6f);
                if (old != null && old.dur > 0) DropArmor(old.key, p.Pos);
                FBHUD.I?.Toast("EQUIPADO", ArmorLabel(it.key), $"REDUZ {Mathf.RoundToInt(it.red * 100)}% · DURABILIDADE {it.max}", new Color(.93f, .91f, .86f)); return;
            }
            var def = GameData.Weapons[l.id]; int slot = Mathf.Clamp(def.slot, 0, 2); var old2 = wu.slots[slot];
            Object.Destroy(l.obj); loots.Remove(l); near = null;
            if (old2 != null) DropWeapon(old2.id, p.Pos, wu.ammo[slot], wu.reserve[slot]);
            wu.Give(l.id); wu.ammo[slot] = l.ammo; wu.reserve[slot] = l.reserve; wu.switching = .25f;
            FBHUD.I?.Toast("ARMA COLETADA", def.name.ToUpper(), $"{l.ammo} / {l.reserve} · {new[] { "FUZIL", "SMG", "PISTOLA" }[slot]}", new Color(.96f, .67f, .09f));
        }
    }
}
