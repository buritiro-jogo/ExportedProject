// LOJA DE ARMAS DO DE ROLÊ (r68, pedido de 10/11/2026: "todo mapa que tenha o modo De Rolê precisa de pelo menos uma loja de armas
// funcional e acessível ... todas as armas existentes, sem modelos duplicados ... moeda virtual ... sem armas duplicadas, sem compras
// repetidas por falha de sincronização e sem perda de inventário").
//  · ONDE: uma loja perto de cada base do mapa (1º spawnA do fb_data e, a 150 m+ dele, o 1º spawnB ou o ponto do mapa mais longe; r68d:
//    sempre os pontos do fb_data como vieram, ver Bases) achada por busca DETERMINÍSTICA em
//    espiral: chão plano do mapa dentro da área jogável, fora do asfalto, nada do mapa no volume, céu aberto em cima, de costas para uma
//    parede quando dá (puxadinho encostado no prédio), frente do balcão andável e CAMINHO PELA NAVMESH até a base. Mesmo mapa = mesmo
//    lugar em todos os PCs da rede local (não depende de sorteio, dos carros nem de onde cada jogador nasceu).
//  · O QUÊ: catálogo = toda arma do fb_data com modelo no registro e fora da lista banida, SEM REPETIR MODELO (mesma assinatura de malhas
//    e materiais = uma entrada) nem nome; categorias reais por classe + EQUIPAMENTO (munição, colete, capacete, kit, kit médico).
//    Preço = o da loja do 4V4 (FBEconomy.PriceOf); a mesma tabela vai para o servidor (PriceTable / loja_precos.json).
//  · DINHEIRO: os CRÉDITOS do perfil (FBSession.money, os mesmos que a partida paga no fim). Arma comprada é sua para sempre (PlayerPrefs
//    fb_role_owned): na loja vira EQUIPAR, grátis. Munição, colete, capacete e kit médico se compram de novo.
//  · REDE: com servidor que entende a loja (caps "loja": FBLanServer e server.mjs r68) a compra é um pedido {buy, rid, item}; o Host confere
//    preço (tabela dele), saldo e posse e responde UMA vez por rid (reenvio = mesma resposta, nunca cobra duas vezes); saldo e lista de armas
//    do jogador vêm da resposta. Sem resposta em 6 s o pedido fica "atrasado": se o Host confirmar depois, o item chega sozinho.
//    Offline aplica direto no perfil.
//  · r68b (revisão de 11/11): clicar de novo no MESMO item com pedido atrasado reusa o rid (o Host devolve o resultado, não cobra duas
//    vezes); conexão caiu = pedido em andamento vira atrasado e, quando a carteira volta, cada atrasado é CONSULTADO ('bchk', que nunca
//    cobra): comprado = o item chega; não chegou = avisa que nada foi cobrado. Saldo vindo do Host só vale se for >= 0. Sem sala ainda
//    (conectando / entrando) a compra espera, em vez de cair na compra local.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace FreeBala
{
    public static class FBGunShop
    {
        public const string PREF = "fb_role_owned";
        public const int AMMO_PRICE = 100, AMMO_MAGS = 3, AMMO_MAX_MAGS = 9;
        public const float W = 4.4f, D = 3.4f, H = 3.4f, COUNTER = 1.1f;        // loja: largura × fundo × pé-direito (m), altura do balcão
        static readonly string[] ORDER = { "pistola", "submetralhadora", "escopeta", "fuzil", "sniper", "explosivo" };
        static readonly Dictionary<string, string> CAT = new() { ["pistola"] = "PISTOLAS", ["submetralhadora"] = "SUBMETRALHADORAS", ["escopeta"] = "ESCOPETAS", ["fuzil"] = "FUZIS", ["sniper"] = "SNIPERS", ["explosivo"] = "EXPLOSIVOS" };
        public static readonly string[] GEAR_IDS = { "ammo", "kit", "colete", "capacete", "medkit" };
        public static int GearPrice(string id) => id switch { "ammo" => AMMO_PRICE, "kit" => 1000, "colete" => 650, "capacete" => 450, "medkit" => FBMed.Price, _ => -1 };
        static (string name, string sub) GearName(string id) => id switch
        {
            "ammo" => ("MUNIÇÃO", $"PENTE CHEIO + {AMMO_MAGS} PENTES EM CADA ARMA"),
            "kit" => ("KIT COLETE + CAPACETE", "PROTEÇÃO COMPLETA · ECONOMIZE $100"),
            "colete" => ("COLETE", "BLINDAGEM DO TRONCO"),
            "capacete" => ("CAPACETE", "PROTEGE A CABEÇA"),
            "medkit" => ("KIT MÉDICO", $"+{FBMed.Heal:0} DE VIDA · TECLA H"),
            _ => (id.ToUpperInvariant(), ""),
        };
        public static int PriceOf(string id) { int g = GearPrice(id); return g >= 0 ? g : FBEconomy.PriceOf(id); }
        public static int Money => FBSession.money;

        // ------------------------------------------------------------------ catálogo (sem modelo nem nome repetido)
        static List<WeaponDef> catalog; public static readonly List<string> Duplicates = new();
        static int Ord(string cls) { int i = System.Array.IndexOf(ORDER, cls); return i < 0 ? ORDER.Length : i; }
        public static List<WeaponDef> Catalog()
        {
            if (catalog != null) return catalog;
            var list = new List<WeaponDef>(); if (!FBRegistry.I) return list;            // registro ainda não carregou: não guarda lista vazia
            Duplicates.Clear(); var bySig = new Dictionary<string, string>(); var byName = new Dictionary<string, string>();
            foreach (var d in GameData.Weapons.Values.OrderBy(d => Ord(d.cls)).ThenBy(d => FBEconomy.PriceOf(d.id)).ThenBy(d => d.id, System.StringComparer.Ordinal))
            {
                if (GameData.BannedWeapons.Contains(d.id)) continue;
                var go = FBRegistry.I.Weapon(d.id); if (!go) continue;
                string sig = Sig(go), nk = (d.name ?? d.id).Trim().ToUpperInvariant();
                if (bySig.TryGetValue(sig, out var twin)) { Duplicates.Add($"{d.id} = {twin} (mesmo modelo)"); continue; }
                if (byName.TryGetValue(nk, out var twin2)) { Duplicates.Add($"{d.id} = {twin2} (mesmo nome)"); continue; }
                bySig[sig] = d.id; byName[nk] = d.id; list.Add(d);
            }
            catalog = list;
            Debug.Log($"[FreeBala][loja] catálogo do De Rolê: {list.Count} armas ({string.Join(", ", ORDER.Select(c => $"{c} {list.Count(w => w.cls == c)}"))})"
                + $" · fora por modelo/nome repetido: {(Duplicates.Count == 0 ? "nenhuma" : string.Join("; ", Duplicates))}");
            return list;
        }
        // assinatura do modelo: malha (nome + vértices) e materiais de cada renderer, na ordem da hierarquia
        static string Sig(GameObject go)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                Mesh m = r is SkinnedMeshRenderer sm ? sm.sharedMesh : r.TryGetComponent<MeshFilter>(out var mf) ? mf.sharedMesh : null; if (!m) continue;
                sb.Append(m.name).Append('#').Append(m.vertexCount).Append('@');
                foreach (var mt in r.sharedMaterials) sb.Append(mt ? mt.name : "-").Append(',');
                sb.Append(';');
            }
            return sb.Length > 0 ? sb.ToString() : "go:" + go.name;
        }
        public static Dictionary<string, int> PriceTable()
        {
            var t = new Dictionary<string, int>();
            foreach (var d in Catalog()) t[d.id] = FBEconomy.PriceOf(d.id);
            foreach (var g in GEAR_IDS) t[g] = GearPrice(g);
            return t;
        }
        // loja_precos.json do server.mjs (mesmo formato que ele lê: {"precos":{id:preço}})
        public static string PriceJson() => "{\"versao\":\"r68\",\"precos\":{" + string.Join(",", PriceTable().OrderBy(k => k.Key, System.StringComparer.Ordinal).Select(kv => $"\"{kv.Key}\":{kv.Value}")) + "}}";

        // ------------------------------------------------------------------ armas do jogador (para sempre)
        static HashSet<string> owned;
        static HashSet<string> Owned => owned ??= new HashSet<string>((PlayerPrefs.GetString(PREF, "") ?? "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
        public static bool Owns(string id) => id != null && Owned.Contains(id);
        public static List<object> OwnedList() => Owned.OrderBy(x => x, System.StringComparer.Ordinal).Cast<object>().ToList();
        static void SaveOwned() { PlayerPrefs.SetString(PREF, string.Join(",", Owned.OrderBy(x => x, System.StringComparer.Ordinal))); PlayerPrefs.Save(); }
        public static void ReloadOwned() => owned = null;                              // teste: depois de restaurar o PlayerPrefs
        // munição de cada arma guardada nesta sessão (trocar de arma na loja não dá munição de graça)
        static readonly Dictionary<string, (int ammo, int reserve)> stash = new();

        // ------------------------------------------------------------------ rede (autoridade do Host)
        // late: pedido sem resposta -> (item, quando). Atravessa a queda e a volta à sala; some depois de 5 min ou quando o Host responde.
        static string pendRid, pendItem; static float pendT; static readonly Dictionary<string, (string item, float t)> late = new(); static bool walletOk; static int ridN;
        static readonly string sess = System.Guid.NewGuid().ToString("N").Substring(0, 6);   // rid único também entre aberturas do jogo
        public static string Pending => pendRid != null ? pendItem : null;
        static bool Authority => FBNet.I && FBNet.I.ShopAuthority;
        public static bool WalletSynced => walletOk;
        public static string Status =>
            !FBSession.online ? "DE ROLÊ · COMPRA NO SEU PERFIL (OFFLINE)" :
            Authority ? (walletOk ? "DE ROLÊ ONLINE · COMPRA CONFERIDA PELO HOST" : "DE ROLÊ ONLINE · SINCRONIZANDO A CARTEIRA…") :
            FBNet.I && FBNet.I.open && !FBNet.I.InRoom ? "DE ROLÊ ONLINE · ENTRANDO NA SALA…" :
            FBNet.I && FBNet.I.open ? "DE ROLÊ ONLINE · SERVIDOR ANTIGO, SEM LOJA (COMPRA SÓ NO SEU PERFIL)" : "DE ROLÊ ONLINE · SEM CONEXÃO COM O HOST (RECONECTANDO)";
        public static void OnRoomJoined() { ToLate(); walletOk = false; }
        // conexão caiu: o pedido em andamento vira atrasado (consultado na volta) e a carteira precisa ser confirmada de novo
        public static void OnDisconnect() { ToLate(); walletOk = false; Refresh(); }
        static void ToLate() { if (pendRid != null) late[pendRid] = (pendItem, Time.unscaledTime); pendRid = null; pendItem = null; }
        public static void OnWallet(Dictionary<string, object> m)
        {
            SyncWallet(m, "v"); walletOk = true; Debug.Log($"[FreeBala][loja] carteira sincronizada com o Host: $ {FBSession.money} · {Owned.Count} armas suas");
            foreach (var kv in late.ToList())
            {
                if (Time.unscaledTime - kv.Value.t > 300) { late.Remove(kv.Key); continue; }
                FBNet.I?.Send(new Dictionary<string, object> { ["t"] = "bchk", ["rid"] = kv.Key, ["item"] = kv.Value.item });   // "chegou?" (nunca cobra)
                Debug.Log($"[FreeBala][loja] consultando o Host sobre o pedido atrasado {kv.Value.item} (rid {kv.Key})");
            }
            Refresh();
        }
        public static void OnPenalty(int v) { if (Authority && v > 0) FBNet.I.Send(new Dictionary<string, object> { ["t"] = "wdec", ["v"] = v }); }
        public static void OnBuy(Dictionary<string, object> m)
        {
            string rid = Json.S(m, "rid") ?? "", item = Json.S(m, "item") ?? ""; bool ok = Json.B(m, "ok"), chk = Json.B(m, "chk"); int paid = Mathf.RoundToInt(Json.F(m, "paid"));
            bool mine = pendRid != null && rid == pendRid, wasLate = late.Remove(rid);
            if (mine) { pendRid = null; pendItem = null; }
            SyncWallet(m, "wallet");
            Debug.Log($"[FreeBala][loja] resposta do Host: {item} {(ok ? "OK" : "RECUSADA")} pago $ {paid} · saldo $ {FBSession.money}{(chk ? " (consulta)" : "")}{(wasLate ? " (pedido atrasado)" : mine ? "" : " (resposta repetida, só o saldo)")}{(ok ? "" : " · " + Json.S(m, "msg"))}");
            if (!mine && !wasLate) { Refresh(); return; }                                 // resposta repetida de pedido já tratado: só o saldo
            if (ok) Apply(FBMatch.Player, item, paid, true);
            else if (chk && !mine) { FBAudio.Synth("deny"); Say("PEDIDO NÃO CHEGOU AO HOST", "NADA FOI COBRADO · PODE COMPRAR DE NOVO", null, new Color(.96f, .67f, .09f)); }
            else { FBAudio.Synth("deny"); Say("COMPRA RECUSADA PELO HOST", (Json.S(m, "msg") ?? "").ToUpperInvariant()); }
            Refresh();
        }
        // r68b: só aceita saldo >= 0 (servidor sem carteira mandava -1 e o jogo gravava 0 no perfil)
        static void SyncWallet(Dictionary<string, object> m, string key)
        {
            if (m.TryGetValue(key, out var v) && v != null) { int w = Mathf.RoundToInt(Json.Fv(v)); if (w >= 0) { FBSession.money = w; FBSession.Save(); } else Debug.LogWarning($"[FreeBala][loja] saldo inválido do Host ignorado: {w}"); }
            var ow = Json.L(m, "owned"); if (ow == null) return; bool ch = false;
            foreach (var o in ow) if (o is string s && GearPrice(s) < 0 && Owned.Add(s)) ch = true;
            if (ch) SaveOwned();
        }

        // ------------------------------------------------------------------ comprar / equipar
        public static string WhyNot(FBCharacter p, string id)
        {
            if (!p) return "INDISPONÍVEL"; var wu = p.GetComponent<FBWeaponUser>();
            switch (id)
            {
                case "ammo": return !wu.HasAny ? "VOCÊ NÃO TEM ARMA" : AmmoFull(wu) ? "MUNIÇÃO CHEIA" : null;
                case "colete": return p.armor >= 100 ? "COLETE INTEIRO" : null;
                case "capacete": return p.helmet ? "JÁ TEM CAPACETE" : null;
                case "kit": return p.armor >= 100 && p.helmet ? "JÁ EQUIPADO" : null;
                case "medkit": return FBMed.inf ? "KITS INFINITOS" : FBMed.kits >= FBMed.Max ? $"LIMITE DE {FBMed.Max} KITS" : null;
            }
            return GameData.Weapons.ContainsKey(id) && FBRegistry.I && FBRegistry.I.Weapon(id) ? null : "ARMA INDISPONÍVEL";
        }
        static bool AmmoFull(FBWeaponUser wu) { for (int i = 0; i < 3; i++) { var d = wu.slots[i]; if (d != null && (wu.ammo[i] < d.mag || wu.reserve[i] < d.mag * AMMO_MAX_MAGS)) return false; } return true; }
        static string AmmoNote(FBWeaponUser wu) { int n = 0, f = 0; for (int i = 0; i < 3; i++) { var d = wu.slots[i]; if (d == null) continue; n++; if (wu.ammo[i] >= d.mag && wu.reserve[i] >= d.mag * AMMO_MAX_MAGS) f++; } return n == 0 ? "SEM ARMA" : $"{f}/{n} CHEIAS"; }

        public static void Buy(FBEconomy.Item it)
        {
            var p = FBMatch.Player; if (it == null || !p || !p.alive) { FBAudio.Synth("deny"); return; }
            if (pendRid != null) { FBAudio.Synth("deny"); Say("AGUARDE", "A COMPRA ANTERIOR AINDA ESTÁ COM O HOST"); return; }
            bool weapon = !it.gear;
            if (weapon && Owns(it.id)) { Equip(p, it.id, false); return; }             // arma já é sua: equipar não muda dono nem saldo
            var why = WhyNot(p, it.id); if (why != null) { FBAudio.Synth("deny"); Say("INDISPONÍVEL", why); return; }
            int price = PriceOf(it.id); if (Money < price) { FBAudio.Synth("deny"); Say("SALDO INSUFICIENTE", $"FALTAM $ {price - Money}"); return; }
            if (FBSession.online)
            {
                var n = FBNet.I;
                if (!n || !n.open) { FBAudio.Synth("deny"); Say("SEM CONEXÃO", "A COMPRA PRECISA DO HOST · RECONECTANDO, TENTE DE NOVO"); return; }
                if (!n.InRoom) { FBAudio.Synth("deny"); Say("AGUARDE", "ENTRANDO NA SALA DO HOST"); return; }   // r68b: antes caía na compra local sem ninguém conferir
                if (n.ShopAuthority)
                {
                    if (!walletOk) { FBAudio.Synth("deny"); Say("AGUARDE", "SINCRONIZANDO A CARTEIRA COM O HOST"); return; }
                    // mesmo item com pedido atrasado: reusa o rid (se o Host já comprou, só devolve o resultado; nunca cobra duas vezes)
                    var again = late.Where(kv => kv.Value.item == it.id).Select(kv => kv.Key).FirstOrDefault();
                    if (again != null) late.Remove(again);
                    pendRid = again ?? $"{FBLobbyNet.Pid}-{sess}-{++ridN}"; pendItem = it.id; pendT = 6;
                    n.Send(new Dictionary<string, object> { ["t"] = "buy", ["rid"] = pendRid, ["item"] = it.id });
                    Debug.Log($"[FreeBala][loja] pedido ao Host: {it.id} $ {price} (rid {pendRid}{(again != null ? ", reenvio do atrasado" : "")})");
                    FBAudio.Synth("hover"); Refresh(); return;
                }
                // servidor antigo (sem "loja" nas caps): ninguém confere — vale só no seu perfil
            }
            FBSession.money = Mathf.Max(0, FBSession.money - price); FBSession.Save();
            Apply(p, it.id, price, false);
            Debug.Log($"[FreeBala][loja] compra local: {it.id} $ {price} · saldo $ {FBSession.money}");
            Refresh();
        }
        static void Apply(FBCharacter p, string id, int paid, bool viaHost)
        {
            var wu = p ? p.GetComponent<FBWeaponUser>() : null; string head = "COMPRADO", tail = paid > 0 ? $"−$ {paid} · SALDO $ {Money}" : $"SALDO $ {Money}";
            switch (id)
            {
                case "ammo":
                    if (wu) for (int i = 0; i < 3; i++) { var d = wu.slots[i]; if (d == null) continue; wu.ammo[i] = d.mag; wu.reserve[i] = Mathf.Max(wu.reserve[i], Mathf.Min(d.mag * AMMO_MAX_MAGS, wu.reserve[i] + d.mag * AMMO_MAGS)); }
                    break;
                case "colete": if (p) p.armor = 100; break;
                case "capacete": if (p) p.helmet = true; break;
                case "kit": if (p) { p.armor = 100; p.helmet = true; } break;
                case "medkit": FBMed.Add(); break;
                default:
                    if (!GameData.Weapons.ContainsKey(id)) return;
                    if (Owned.Add(id)) SaveOwned();
                    if (p && p.alive) Equip(p, id, true); else head = "COMPRADO · NA LOJA PARA EQUIPAR";
                    break;
            }
            FBAudio.Synth("buy"); FBAudio.Play2D("reload", .45f);
            var nm = GameData.Weapons.TryGetValue(id, out var wd) ? wd.name.ToUpperInvariant() : GearName(id).name;
            Say(head, nm, tail + (viaHost ? " · CONFERIDO PELO HOST" : ""), new Color(.21f, .76f, .35f));
        }
        static void Equip(FBCharacter p, string id, bool bought)
        {
            var wu = p.GetComponent<FBWeaponUser>(); if (!wu || !GameData.Weapons.TryGetValue(id, out var d)) return;
            int s = wu.SlotOfId(id);
            if (s >= 0) { if (wu.cur != s) wu.SwitchTo(s); Refresh(); return; }
            int slot = Mathf.Clamp(d.slot, 0, 2); var old = wu.slots[slot];
            if (old != null) stash[old.id] = (wu.ammo[slot], wu.reserve[slot]);          // a que sai do slot continua sua, com a munição que tinha
            wu.Give(id);
            if (!bought) { if (stash.TryGetValue(id, out var am)) { wu.ammo[slot] = am.ammo; wu.reserve[slot] = am.reserve; } else { wu.ammo[slot] = d.mag; wu.reserve[slot] = d.mag * 2; } }
            if (!bought) { FBAudio.Play2D("reload", .4f); Say("EQUIPADO", d.name.ToUpperInvariant(), $"{wu.ammo[slot]} / {wu.reserve[slot]} · SLOT {slot + 1}", new Color(.96f, .67f, .09f)); }
            Refresh();
        }
        static void Refresh() { if (FBShop.Open && FBShop.Store) FBShop.Refresh(); }

        // ------------------------------------------------------------------ vitrine (dados da tela FBShop no modo loja)
        public static List<(string cat, string icon, List<FBEconomy.Item> items)> ShopData(FBCharacter p)
        {
            var wu = p.GetComponent<FBWeaponUser>(); var res = new List<(string, string, List<FBEconomy.Item>)>(); var cat = Catalog();
            foreach (var cls in ORDER.Concat(cat.Select(c => c.cls).Where(c => !ORDER.Contains(c)).Distinct()))
            {
                var items = cat.Where(d => d.cls == cls).Select(d => new FBEconomy.Item { id = d.id, name = d.name.ToUpperInvariant(), cls = d.cls, sub = (d.cls ?? "").ToUpperInvariant(), price = PriceOf(d.id), own = Owns(d.id), rebuy = false }).ToList();
                if (items.Count > 0) res.Add((CAT.TryGetValue(cls ?? "", out var cn) ? cn : (cls ?? "OUTRAS").ToUpperInvariant(), "", items));
            }
            var gear = new List<FBEconomy.Item>();
            foreach (var g in GEAR_IDS)
            {
                var (n, sub) = GearName(g); var it = new FBEconomy.Item { id = g, name = n, sub = sub, price = GearPrice(g), gear = true };
                switch (g)
                {
                    case "ammo": it.own = wu.HasAny && AmmoFull(wu); it.note = AmmoNote(wu); break;
                    case "colete": it.own = p.armor >= 100; it.note = $"{Mathf.RoundToInt(p.armor)}/100"; break;
                    case "capacete": it.own = p.helmet; break;
                    case "kit": it.own = p.armor >= 100 && p.helmet; it.best = true; break;
                    case "medkit": it.own = FBMed.inf || FBMed.kits >= FBMed.Max; it.note = FBMed.inf ? "∞" : $"{FBMed.kits}/{FBMed.Max}"; break;
                }
                it.rebuy = !it.own; gear.Add(it);
            }
            res.Add(("EQUIPAMENTO", "", gear));
            return res;
        }

        // ------------------------------------------------------------------ a loja no mapa
        public class Spot { public Vector3 pos, front; public float yaw; public bool wall; public float path; public GameObject go; public FBMarker mk; }
        public static readonly List<Spot> shops = new(); static Spot near; static bool prompt; static float flashT;
        public static bool Near => near != null && near.go && FBMatch.Player && FBMatch.Player.alive && FBPlayer.I && !FBPlayer.I.Driving;
        public static Spot Nearest => near;
        public static bool Armed { get { var pl = FBPlayer.I; return pl && pl.ch && pl.ch.alive && pl.wu && pl.wu.HasAny && !pl.Driving; } }
        // HUD do De Rolê (FBHUD/FBHudWeapons ficam desligados no rolê): liga a mira/munição/vida quando o jogador está armado,
        // e o aviso da loja / mensagens da compra por alguns segundos
        public static bool HudOn => Armed || (prompt && Near) || Time.unscaledTime < flashT;
        public static void Flash(float s) => flashT = Mathf.Max(flashT, Time.unscaledTime + s);
        static void Say(string a, string b, string c = null, Color? col = null)
        {
            FBHUD.I?.Toast(a, b ?? "", c ?? "", col ?? new Color(.95f, .33f, .3f)); Flash(3.2f);
        }

        public static void Clear() { foreach (var s in shops) { if (s.go) Object.Destroy(s.go); if (s.mk) Object.Destroy(s.mk.gameObject); } shops.Clear(); near = null; prompt = false; }
        public static void Setup(FBMatch g)
        {
            // a carteira (walletOk) é da conexão: quem zera é o FBNet ao entrar na sala (o "joined" pode chegar antes deste Setup).
            // r68b: os pedidos atrasados ficam (até 5 min): se o Host cobrou, o item chega quando a carteira for confirmada
            Clear(); ToLate(); stash.Clear(); hinted = false; var m = g ? g.Map : null; if (m == null) return;
            var bases = Bases(g.mapId, m);
            if (bases.Count == 0 && FBMatch.Player) bases.Add(FBMatch.Player.Pos);
            var t0 = Time.realtimeSinceStartup;
            foreach (var b in bases)
            {
                var s = Find(b, true) ?? Find(b, false);
                if (s == null) { Debug.LogWarning($"[FreeBala][loja] {FBSession.map}: nenhum lugar livre para a loja perto da base {b} (até 90 m)"); continue; }
                Build(s); shops.Add(s);
                Debug.Log($"[FreeBala][loja] {FBSession.map}: loja {shops.Count} em {s.pos.x:0.0} {s.pos.y:0.00} {s.pos.z:0.0} (rumo {s.yaw:0}°) · {(s.front - b).Flat().magnitude:0} m da base, caminho a pé {s.path:0} m{(s.wall ? " · encostada na parede" : "")}");
            }
            if (shops.Count > 0) { Physics.SyncTransforms(); ClearCars(); }
            Catalog();
            Debug.Log($"[FreeBala][loja] {FBSession.map}: {shops.Count} loja(s) prontas em {(Time.realtimeSinceStartup - t0) * 1000:0} ms");
        }
        static bool hinted;
        // r68d: as bases saem dos pontos do fb_data COMO VIERAM (MapDef.dataA/dataB/dataP), nunca das listas da partida. O EnsureLevel filtra
        // essas listas pelo caminho até a âncora e, quando uma esvazia, SORTEIA pontos novos: na Orla (todo spawnB fora da área jogável) e no
        // BV City (spawnB a 930 m da âncora: a busca de caminho desiste no meio) a 2ª loja mudava de lugar a cada partida e de um PC para outro.
        // 1ª base = 1º spawnA dentro da área e na NavMesh; 2ª = 1º spawnB a 150 m+ dela; sem esse spawnB, o ponto do mapa (points, waypoints,
        // bots) mais longe dela, a 150 m+, com caminho a pé completo até ela. Tudo na mesma ordem do arquivo: o mesmo resultado em todo PC.
        static List<Vector3> Bases(string mapId, MapDef m)
        {
            var pb = FBWorld.GetPlayableBounds(mapId); var bases = new List<Vector3>();
            bool Ok(Vector3 p) => pb.Contains(p) && NavMesh.SamplePosition(p, out _, 3f, NavMesh.AllAreas);
            var A = m.dataA.Where(Ok).ToList(); var B = m.dataB.Where(Ok).ToList(); var P = m.dataP.Where(Ok).ToList();
            var l1 = A.Count > 0 ? A : B.Count > 0 ? B : P; if (l1.Count == 0) return bases;
            var a = l1[0]; bases.Add(a); float Dist(Vector3 p) => (p - a).Flat().magnitude;
            foreach (var p in B) if (Dist(p) > 150) { bases.Add(p); return bases; }
            var path = new NavMeshPath(); NavMesh.SamplePosition(a, out var ah, 3f, NavMesh.AllAreas);
            foreach (var p in P.Where(p => Dist(p) > 150).OrderByDescending(Dist))
                if (NavMesh.SamplePosition(p, out var ph, 3f, NavMesh.AllAreas) && NavMesh.CalculatePath(ah.position, ph.position, NavMesh.AllAreas, path)
                    && path.status == NavMeshPathStatus.PathComplete) { bases.Add(p); break; }
            if (bases.Count < 2) Debug.Log($"[FreeBala][loja] {mapId}: só uma base (nenhum spawnB nem ponto do mapa a 150 m+ com caminho a pé)");
            return bases;
        }
        // ---- busca do lugar: anéis de 2,5 m a partir de 8 m da base; ângulos sempre na mesma ordem (determinística)
        static Spot Find(Vector3 c, bool needWall)
        {
            const int M = 1 << FBWorld.LMap; var path = new NavMeshPath();
            for (float r = 8; r <= 90; r += 2.5f)
            {
                int n = Mathf.Max(8, Mathf.RoundToInt(2 * Mathf.PI * r / 3f));
                for (int k = 0; k < n; k++)
                {
                    float a = k * Mathf.PI * 2 / n; var p = c + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * r;
                    if (shops.Any(o => (o.pos - p).Flat().magnitude < 40)) continue;
                    if (!NavMesh.SamplePosition(p, out var h, 2f, NavMesh.AllAreas) || Mathf.Abs(h.position.y - c.y) > 6) continue;
                    // "do lado de dentro do muro" = caminho pela NavMesh até a base (abaixo). O FBNav.Inside não serve aqui: o cache dele depende
                    // de onde cada PC nasceu (mudaria o lugar da loja de um PC para outro) e o quadrado da arena (SetLimite) é do 10x10 e fica
                    // ligado depois dele
                    p = h.position;
                    var cands = new List<(Vector3 p, float yaw, bool wall)>();
                    // de costas para a parede: a parede mais perto (8 direções a 1,2 m de altura) vira o fundo da loja
                    float bestD = 9; Vector3 wn = Vector3.zero, wp = Vector3.zero;
                    for (int j = 0; j < 8; j++)
                    {
                        float b = j * Mathf.PI / 4; var dir = new Vector3(Mathf.Sin(b), 0, Mathf.Cos(b));
                        if (Physics.Raycast(p + Vector3.up * 1.2f, dir, out var wh, D * .5f + 2f, M, QueryTriggerInteraction.Ignore) && Mathf.Abs(wh.normal.y) < .3f && wh.distance < bestD) { bestD = wh.distance; wn = wh.normal.Flat().normalized; wp = wh.point; }
                    }
                    if (bestD < 9 && wn.sqrMagnitude > .5f) { var q = wp + wn * (D * .5f + .32f); q.y = p.y; cands.Add((q, Mathf.Atan2(wn.x, wn.z) * Mathf.Rad2Deg, true)); }
                    if (!needWall) { float yaw0 = Mathf.Atan2(c.x - p.x, c.z - p.z) * Mathf.Rad2Deg; for (int t = 0; t < 4; t++) cands.Add((p, yaw0 + t * 90, false)); }
                    foreach (var (cp, yaw, wall) in cands)
                    {
                        if (!Fits(cp, yaw, out var gy)) continue;
                        var front = new Vector3(cp.x, gy, cp.z) + Quaternion.Euler(0, yaw, 0) * Vector3.forward * (D * .5f + 1.1f);
                        if (!NavMesh.SamplePosition(front, out var fh, 1f, NavMesh.AllAreas) || !NavMesh.SamplePosition(c, out var ch, 3f, NavMesh.AllAreas)) continue;
                        if (!NavMesh.CalculatePath(ch.position, fh.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                        float len = 0; for (int i = 1; i < path.corners.Length; i++) len += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                        if (len > (fh.position - ch.position).magnitude * 2.2f + 25) continue;   // acessível de verdade: sem volta enorme
                        return new Spot { pos = new Vector3(cp.x, gy, cp.z), yaw = yaw, wall = wall, front = fh.position, path = len };
                    }
                }
            }
            return null;
        }
        static bool Fits(Vector3 p, float yaw, out float gy)
        {
            const int M = 1 << FBWorld.LMap; gy = p.y; var q = Quaternion.Euler(0, yaw, 0); var R = q * Vector3.right; var F = q * Vector3.forward;
            float lo = 1e9f, hi = -1e9f;
            for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++)
                {
                    var o = p + R * (i * W * .5f) + F * (j * D * .5f) + Vector3.up * 2.5f;
                    if (!Physics.Raycast(o, Vector3.down, out var hit, 5f, M, QueryTriggerInteraction.Ignore)) return false;
                    lo = Mathf.Min(lo, hit.point.y); hi = Mathf.Max(hi, hit.point.y);
                }
            if (hi - lo > .35f) return false; gy = lo;
            for (int i = -1; i <= 1; i++) for (int j = -1; j <= 2; j++) if (FBRoads.IsRoad(p + R * (i * (W * .5f + 1)) + F * (j * (D * .5f + 1)))) return false;   // fora do asfalto, com folga
            var ctr = new Vector3(p.x, hi + .12f + H * .5f, p.z);
            if (Physics.CheckBox(ctr, new Vector3(W * .5f + .3f, H * .5f, D * .5f + .3f), q, M, QueryTriggerInteraction.Ignore)) return false;
            if (Physics.Raycast(ctr, Vector3.up, 14f, M, QueryTriggerInteraction.Ignore)) return false;           // céu aberto (não fica embaixo de marquise/viaduto)
            for (int k = 1; k <= 2; k++)
            {
                var fp = p + F * (D * .5f + k * 1.15f);
                if (!NavMesh.SamplePosition(fp, out var fh, .7f, NavMesh.AllAreas) || Mathf.Abs(fh.position.y - lo) > .5f) return false;
                if (Physics.CheckCapsule(fh.position + Vector3.up * .45f, fh.position + Vector3.up * 1.6f, .35f, M, QueryTriggerInteraction.Ignore)) return false;
            }
            return true;
        }
        // carro estacionado em cima da loja ou na frente do balcão sai (os estacionados são os mesmos em todos os PCs)
        static void ClearCars()
        {
            int n = 0;
            foreach (var car in FBVehicles.list.ToList())
            {
                if (!car || car.driver || car.netOwner >= 0 || car == FBRole.roleCar) continue; var cp = car.transform.position;
                foreach (var s in shops)
                {
                    var l = Quaternion.Inverse(Quaternion.Euler(0, s.yaw, 0)) * (cp - s.pos);
                    if (Mathf.Abs(l.x) < W * .5f + 2.6f && l.z > -D * .5f - 2.6f && l.z < D * .5f + 4.5f && Mathf.Abs(l.y) < 4) { FBVehicles.list.Remove(car); Object.Destroy(car.gameObject); n++; break; }
                }
            }
            if (n > 0) Debug.Log($"[FreeBala][loja] {n} carro(s) estacionado(s) em cima da loja tirado(s)");
        }

        // ---- o prédio: modelo do Blender (Resources/Lojas/loja_armas.glb) ou, sem ele, a mesma planta em blocos
        static void Build(Spot s)
        {
            var root = new GameObject("FB_LojaDeArmas"); root.transform.SetPositionAndRotation(s.pos, Quaternion.Euler(0, s.yaw, 0)); s.go = root;
            var pf = Resources.Load<GameObject>("Lojas/loja_armas");
            if (pf)
            {
                var v = Object.Instantiate(pf, root.transform, false); v.name = "modelo"; v.transform.localPosition = Vector3.zero; v.transform.localRotation = Quaternion.identity;
                foreach (var c in v.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
                foreach (var r in v.GetComponentsInChildren<Renderer>(true)) { r.gameObject.layer = FBWorld.LMap; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
            }
            else Blocks(root.transform);
            // colisão: paredes, balcão, teto e letreiro (o jogador fica do lado de fora, no balcão)
            void Box(string n, Vector3 c, Vector3 sz) { var g = new GameObject("col_" + n); g.layer = FBWorld.LMap; g.transform.SetParent(root.transform, false); g.transform.localPosition = c; var b = g.AddComponent<BoxCollider>(); b.size = sz; }
            const float T = .15f, OPEN = 3.4f, TOP = 2.45f;
            Box("fundo", new Vector3(0, H * .5f, -D * .5f + T * .5f), new Vector3(W, H, T));
            Box("esq", new Vector3(-W * .5f + T * .5f, H * .5f, 0), new Vector3(T, H, D)); Box("dir", new Vector3(W * .5f - T * .5f, H * .5f, 0), new Vector3(T, H, D));
            Box("balcao", new Vector3(0, COUNTER * .5f, D * .5f - .3f), new Vector3(W, COUNTER, .6f));
            Box("verga", new Vector3(0, (TOP + H) * .5f, D * .5f - T * .5f), new Vector3(W, H - TOP, T));
            float pw = (W - OPEN) * .5f; Box("pilarE", new Vector3(-W * .5f + pw * .5f, H * .5f, D * .5f - T * .5f), new Vector3(pw, H, T)); Box("pilarD", new Vector3(W * .5f - pw * .5f, H * .5f, D * .5f - T * .5f), new Vector3(pw, H, T));
            Box("teto", new Vector3(0, H + .08f, 0), new Vector3(W + .2f, .16f, D + .2f));
            Box("letreiro", new Vector3(0, H + .55f, D * .5f), new Vector3(3.9f, .75f, .14f));
            var ob = root.AddComponent<NavMeshObstacle>(); ob.shape = NavMeshObstacleShape.Box; ob.center = new Vector3(0, H * .5f, 0); ob.size = new Vector3(W + .2f, H, D + .2f); ob.carving = true; ob.carveOnlyStationary = true;
            FBPedNav.Block(s.pos, W * .5f + .6f, D * .5f + .6f, s.yaw);                     // pedestres do rolê contornam a loja
            s.mk = FBMarker.Make(s.front, new Color(.96f, .67f, .09f), 1.1f, 32); s.mk.Label("LOJA");
        }
        // a mesma planta sem o .glb (alvenaria cinza, faixa e letreiro dourados): a loja nunca some por falta do modelo
        static void Blocks(Transform root)
        {
            Material Mt(Color c) { var m = new Material(FBShaders.Lit); m.SetColor("_BaseColor", c); return m; }
            var wall = Mt(new Color(.2f, .21f, .22f)); var gold = Mt(new Color(.96f, .67f, .09f)); var floor = Mt(new Color(.5f, .5f, .48f)); var dark = Mt(new Color(.07f, .07f, .08f));
            void Cube(Vector3 c, Vector3 sz, Material m) { var g = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.Destroy(g.GetComponent<Collider>()); g.layer = FBWorld.LMap; g.transform.SetParent(root, false); g.transform.localPosition = c; g.transform.localScale = sz; g.GetComponent<Renderer>().sharedMaterial = m; }
            const float T = .15f, OPEN = 3.4f, TOP = 2.45f; float pw = (W - OPEN) * .5f;
            Cube(new Vector3(0, .06f, 0), new Vector3(W, .12f, D), floor);
            Cube(new Vector3(0, H * .5f, -D * .5f + T * .5f), new Vector3(W, H, T), wall);
            Cube(new Vector3(-W * .5f + T * .5f, H * .5f, 0), new Vector3(T, H, D), wall); Cube(new Vector3(W * .5f - T * .5f, H * .5f, 0), new Vector3(T, H, D), wall);
            Cube(new Vector3(0, COUNTER * .5f, D * .5f - .3f), new Vector3(OPEN, COUNTER, .6f), dark);
            Cube(new Vector3(0, (TOP + H) * .5f, D * .5f - T * .5f), new Vector3(W, H - TOP, T), wall);
            Cube(new Vector3(-W * .5f + pw * .5f, H * .5f, D * .5f - T * .5f), new Vector3(pw, H, T), wall); Cube(new Vector3(W * .5f - pw * .5f, H * .5f, D * .5f - T * .5f), new Vector3(pw, H, T), wall);
            Cube(new Vector3(0, H + .08f, 0), new Vector3(W + .2f, .16f, D + .2f), wall);
            Cube(new Vector3(0, H + .55f, D * .5f), new Vector3(3.9f, .75f, .14f), gold);
            Cube(new Vector3(0, TOP - .1f, D * .5f + .02f), new Vector3(W, .08f, .06f), gold);
        }

        // ------------------------------------------------------------------ por quadro (FBRole.Tick)
        public static void Tick(float dt)
        {
            if (pendRid != null && (pendT -= Time.unscaledDeltaTime) <= 0)
            {
                Debug.LogWarning($"[FreeBala][loja] o Host não respondeu o pedido {pendItem} (rid {pendRid}) em 6 s: fica como atrasado");
                ToLate(); FBAudio.Synth("deny");
                Say("SEM RESPOSTA DO HOST", "SE ELE CONFIRMAR DEPOIS, O ITEM CHEGA SOZINHO · NADA É COBRADO DUAS VEZES"); Refresh();
            }
            var me = FBMatch.Player; near = null; float best = 2.8f;
            if (me)
                foreach (var s in shops)
                {
                    if (!s.go) continue; float d = (s.front - me.Pos).Flat().magnitude;
                    if (d < best && Mathf.Abs(s.front.y - me.Pos.y) < 2) { best = d; near = s; }
                    if (s.mk) { bool show = d > 22 && !(FBPlayer.I && FBPlayer.I.Driving && d < 60); if (s.mk.gameObject.activeSelf != show) s.mk.gameObject.SetActive(show); if (show) s.mk.Spin(dt * .6f); }
                }
            // aviso único depois da garagem: onde fica a loja
            if (!hinted && shops.Count > 0 && me && me.alive && !FBGarage.Open_)
            {
                hinted = true; var s0 = shops.Where(s => s.go).OrderBy(s => (s.front - me.Pos).sqrMagnitude).FirstOrDefault();
                if (s0 != null) Say("LOJA DE ARMAS", "SIGA A LUZ DOURADA · G MOSTRA NO GPS", $"{(s0.front - me.Pos).Flat().magnitude:0} m DAQUI · APERTE B NO BALCÃO", new Color(.96f, .67f, .09f));
            }
            if (prompt && !Near) { prompt = false; FBHUD.I?.Prompt(null, null); }
            if (FBShop.Open && FBShop.Store && !Near) FBShop.Show(false);                  // morreu / foi empurrado: a loja fecha
        }
        // aviso do balcão (chamado pelo FBLoot.Update quando não há saque perto); true = o aviso é da loja
        public static bool Prompt(FBCharacter p)
        {
            if (!Near || FBShop.Open) { prompt = false; return false; }
            prompt = true; FBHUD.I?.Prompt("B", "LOJA DE ARMAS", $"ARMAS, MUNIÇÃO E EQUIPAMENTO · SALDO $ {Money}"); return true;
        }
        public static void OpenStore()
        {
            if (!Near) return; Catalog(); FBShop.ShowStore(true);
            Debug.Log($"[FreeBala][loja] loja aberta · saldo $ {Money} · {Owned.Count} armas suas · {Status}");
        }
    }
}
