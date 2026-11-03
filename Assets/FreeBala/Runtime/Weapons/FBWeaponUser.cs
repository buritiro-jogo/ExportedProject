// Armas de um personagem (porte do INV do main.js): 3 slots (fuzil/longa, submetralhadora, pistola) + mãos livres, pente + reserva,
// recarga (tempo por classe; espingarda/bolt "cycled" bala a bala), troca com guardar/sacar do IE (a mão leva a arma às costas/coldre),
// armas guardadas no corpo, cadência e bolt-action, tiro sem munição (clique), tiro com recuo/dispersão (FBRecoil) e balas (FBCombat).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public class FBWeaponUser : MonoBehaviour
    {
        public const int HANDS = 3;
        [System.NonSerialized] public FBCharacter ch; public readonly WeaponDef[] slots = new WeaponDef[3]; public readonly int[] ammo = new int[3], reserve = new int[3];
        public int cur = HANDS, pending = -1; public float switching, cool, reloadT, boltT; [System.NonSerialized] public FBRecoil recoil = new(); int shotsBurst;
        public WeaponDef Cur => cur < HANDS ? slots[cur] : null;
        public int Ammo => cur < HANDS ? ammo[cur] : 0; public int Reserve => cur < HANDS ? reserve[cur] : 0;
        public bool Unarmed => Cur == null; public bool HasAny => slots[0] != null || slots[1] != null || slots[2] != null;
        public System.Action onEquip;

        void Awake() { ch = GetComponent<FBCharacter>(); }
        public bool Has(string id) { foreach (var s in slots) if (s != null && s.id == id) return true; return false; }
        public int SlotOfId(string id) { for (int i = 0; i < 3; i++) if (slots[i] != null && slots[i].id == id) return i; return -1; }
        static GameObject Model(WeaponDef d) => d != null && FBRegistry.I ? FBRegistry.I.Weapon(d.id) : null;

        // dá as armas (cada uma no slot da classe) e equipa a principal; sem nada = mãos livres
        public void Give(params string[] ids)
        {
            int last = -1;
            foreach (var id in ids) if (id != null && GameData.Weapons.TryGetValue(id, out var d) && Model(d))
                {
                    int s = Mathf.Clamp(d.slot, 0, 2); slots[s] = d; ammo[s] = d.mag; reserve[s] = d.mag * (d.cls == "explosivo" ? 3 : 5); last = s;
                }
            int first = ids.Length > 1 ? (slots[0] != null ? 0 : slots[1] != null ? 1 : slots[2] != null ? 2 : HANDS) : last >= 0 ? last : (slots[0] != null ? 0 : slots[1] != null ? 1 : slots[2] != null ? 2 : HANDS);
            Equip(first, ids.Length != 1 || ch.isBot);
        }
        public void Strip() { for (int i = 0; i < 3; i++) { slots[i] = null; ammo[i] = reserve[i] = 0; } Equip(HANDS, true); }
        public void RefillAll(int mags) { for (int i = 0; i < 3; i++) if (slots[i] != null) { ammo[i] = slots[i].mag; reserve[i] = Mathf.Max(reserve[i], slots[i].mag * mags); } reloadT = 0; }

        public void Equip(int i, bool instant = false)
        {
            if (i < HANDS && slots[i] == null) i = HANDS;
            pending = -1; switching = 0; cur = i; recoil.Reset(); reloadT = 0;
            var d = Cur; ch.SetWeapon(d, Model(d));
            // [parceiro] Correção: troca de arma que termina já dentro do carro ou na moto não aparece na mão do piloto
            // (esconde a arma, pula a animação/som de sacar e não entra em postura de combate no banco)
            if (ch.vehiclePose) { ch.SetWeaponVisible(false); instant = true; }
            ch.combat = d != null && !ch.vehiclePose;
            // armas de reserva no corpo (HOLST do web / coldre do IE): fuzil/sniper/escopeta/explosivo nas costas, SMG na bandoleira, pistola na
            // coxa — as dos OUTROS slots (a da mão nunca duplica). Equip roda no spawn (Give) e em cada troca, então aparecem desde o início
            var rest = new List<(WeaponDef, GameObject)>(); for (int s = 0; s < 3; s++) if (s != cur && slots[s] != null) rest.Add((slots[s], Model(slots[s])));
            ch.SetHolsters(rest);
            if (!instant && d != null) { ch.DrawAnim(); PlayAt(FBWeaponSound.Draw(d) ?? S(d, "Unholster") ?? "unholster", .75f); }
            onEquip?.Invoke();
        }
        public void SwitchTo(int i)
        {
            if (i < 0 || i > HANDS || (i != HANDS && slots[i] == null) || i == cur || switching > 0 || !ch.alive) return;
            if (ch.isPlayer && FBMed.Busy) { FBMed.Cancel(); FBAudio.Synth("medcancel"); }
            reloadT = 0; pending = i; float ht = Cur != null ? ch.HolsterAnim() : 0;
            switching = ch.isPlayer && FBPlayer.I && FBPlayer.I.firstPerson ? .26f : Mathf.Max(.14f, ht);
            if (Cur != null) PlayAt(FBWeaponSound.Holster(Cur) ?? S(Cur, "Holster") ?? "unholster", .6f);
        }
        public void Cycle(int dir)
        {
            var order = new List<int>(); for (int s = 0; s < 3; s++) if (slots[s] != null) order.Add(s); order.Add(HANDS);
            int k = Mathf.Max(0, order.IndexOf(cur)); SwitchTo(order[(k + (dir > 0 ? 1 : -1) + order.Count) % order.Count]);
        }

        public bool Reload()
        {
            var d = Cur; if (d == null || reloadT > 0 || switching > 0 || ammo[cur] >= d.mag || reserve[cur] <= 0 || !ch.alive) return false;
            bool empty = ammo[cur] == 0;
            reloadT = d.cycled ? .5f + .45f * Mathf.Min(d.mag - ammo[cur], reserve[cur]) : d.IsPistol ? 1.6f : d.IsSniper ? 2.6f : d.cls == "escopeta" ? 2.4f : 2.2f;
            if (empty && !d.cycled) reloadT += .35f;
            ch.Trigger("reload"); shellT = -1; shellsLeft = 0;
            // recarga PRÓPRIA da arma (o 171 por classe vira só reserva); escopeta = um cartucho por vez + a bomba (sons no Update)
            if (d.cycled && FBWeaponSound.Insert(d) != null) { shellsLeft = Mathf.Min(d.mag - ammo[cur], reserve[cur]); shellT = .3f; }
            else PlayAt(FBWeaponSound.ReloadOf(d) ?? S(d, empty ? "ReloadEmpty" : "Reload") ?? FBWeaponSound.Reload(d) ?? (empty ? "reload_empty" : "reload"), .7f);
            if (ch.isPlayer && FBMed.Busy) { FBMed.Cancel(); FBAudio.Synth("medcancel"); }
            return true;
        }

        // [parceiro] v1 (thread de física): tempo do Update medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_Update = FBTempo.Id("FBWeaponUser.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            float dt = Time.deltaTime; cool -= dt; if (boltT > 0) boltT -= dt;
            if (switching > 0) { switching -= dt; if (switching <= 0 && pending >= 0) Equip(pending); }
            if (reloadT > 0)
            {
                reloadT -= dt;
                if (shellT > 0 && Cur != null && (shellT -= dt) <= 0)
                {   // escopeta: um cartucho a cada 0,45 s (ritmo do reloadT do cycled) e a bomba no fim
                    if (shellsLeft-- > 0) { PlayAt(FBWeaponSound.Insert(Cur), .65f); shellT = .45f; } else { PlayAt(FBWeaponSound.Close(Cur), .7f); shellT = -1; }
                }
                if (reloadT <= 0 && Cur != null) { int need = Cur.mag - ammo[cur], take = ch.isBot ? need : Mathf.Min(need, reserve[cur]); ammo[cur] += take; if (!ch.isBot) reserve[cur] -= take; }
            }
            if (ch.isBot && Cur != null && reserve[cur] < Cur.mag) reserve[cur] = Cur.mag * 3;           // bots: munição de reserva infinita
        }

        // dispara do ponto/direção da mira (câmera do jogador, cano do bot); devolve se saiu o tiro.
        // 3ª pessoa (aimTo): a câmera só escolhe o ponto mirado P de cada bago; a bala sai de shotFrom (cano, ou a cabeça com o cano enfiado
        // na parede — shotOrigin do main.js) direto até P, então um obstáculo entre o cano e P pega o tiro.
        public bool Fire(Vector3 origin, Vector3 dir, FBRecoil.State st, out Vector3 hitPoint, float extraSpread = 0, Vector3? muzzleFrom = null, float near = 0, System.Func<Vector3, Vector3, Vector3> aimTo = null, Vector3? shotFrom = null)
        {
            hitPoint = origin + dir * 200; var d = Cur;
            if (d == null || cool > 0 || switching > 0 || reloadT > 0 || !ch.alive || (d.bolt && boltT > 0) || ch.DrawBusy) return false;
            if (ammo[cur] <= 0) { if (cool <= 0) { PlayAt(FBWeaponSound.Empty(d) ?? S(d, "FireEmpty") ?? "empty", .8f); cool = .3f; } if (ch.isBot || reserve[cur] > 0) Reload(); return false; }
            ammo[cur]--; cool = d.Interval; shotsBurst++; if (d.bolt) boltT = d.Interval; ch.lastShot = 0; ch.shooting = true; ch.protUntil = 0;
            if (ch.isPlayer) FBMatch.Stats.shots++;
            bool silenced = FBWeaponSound.Silenced(d); var mz = muzzleFrom ?? ch.MuzzlePos;
            bool isFpShooter = ch.isPlayer && FBPlayer.I && (FBPlayer.I.firstPerson || FBPlayer.I.OpticE >= .5f);
            if (isFpShooter) FBFx.MuzzleWorldLight(mz, d.cls, silenced);
            else FBFx.Muzzle(mz, ch.isPlayer ? .25f : .55f, d.cls, silenced);
            if (!silenced) FBBotBrain.Alert(ch.Pos, ch.isPlayer ? 60 : 45, ch.team);              // tiro é ouvido de longe: quem ouve vai investigar (ponto aproximado)
            FBBotBrain.Noise(ch.Pos);
            float spreadDeg = extraSpread <= -50 ? 0 : recoil.Spread(d, st) + extraSpread;            // -99: tiro já resolvido pelo bot (sem cone extra)
            var right = Vector3.Cross(dir, Mathf.Abs(dir.y) > .95f ? Vector3.right : Vector3.up).normalized; var up = Vector3.Cross(right, dir);
            var tracerFrom = muzzleFrom ?? mz;
            for (int k = 0; k < Mathf.Max(1, d.pellets); k++)
            {
                float a = Random.value * Mathf.PI * 2, r = Mathf.Sqrt(Random.value) * Mathf.Tan(spreadDeg * Mathf.Deg2Rad);
                var dd = (dir + right * Mathf.Cos(a) * r + up * Mathf.Sin(a) * r).normalized;
                if (aimTo != null) { var P = aimTo(origin, dd); var from = shotFrom ?? mz; var d2 = P - from; float L = d2.magnitude; if (L > .05f) { var s1 = FBCombat.BulletFrom(from, d2 / L, tracerFrom, ch, d, 0, k < 3, L + .05f, d.pellets); if (k == 0) hitPoint = s1.end; continue; } }
                var s = FBCombat.BulletFrom(origin, dd, tracerFrom, ch, d, near, k < 3, 300, d.pellets); if (k == 0) hitPoint = s.end;
            }
            recoil.Shot(d, st);
            var fire = FBWeaponSound.Fire(d, silenced); var tail = silenced ? null : FBWeaponSound.Tail(d);
            if (ch.isPlayer) { FBAudio.Play2D(fire, silenced ? .6f : .85f, .06f); if (tail != null) FBAudio.Play2D(tail, .35f, .06f); }
            else
            {   // tiro dos outros (rodada 40: "quase não conseguimos ouvir os disparos dos inimigos"): antes ref 6 m e queda 1,5 (a 20 m ~1/5
                // do próprio tiro, a 40 m ~1/10) e troca seca para o "longe" aos 60 m. Agora tiro é FONTE ALTA: ref 15 m, queda 1 (lei do
                // inverso), e o perto cede para o eco de longe aos poucos entre 30 e 110 m; posição pelo 3D + abafado atrás de parede
                float dp = Camera.main ? Vector3.Distance(Camera.main.transform.position, mz) : 0;
                if (silenced) { if (dp < 45) FBAudio.PlayShotAt(fire, mz, .55f, 5, 1.2f); }
                else
                {
                    float nearW = Mathf.Clamp01((110 - dp) / 60), farW = Mathf.Clamp01((dp - 30) / 50);
                    if (nearW > .02f) FBAudio.PlayShotAt(fire, mz, .95f * nearW, 15, 1f);
                    if (farW > .02f) FBAudio.PlayShotAt(FBWeaponSound.Far(d) ?? "shot_far", mz, .95f * farW, 40, 1f);
                }
            }
            FBCasings.Eject(ch, d);
            return true;
        }

        float shellT = -1; int shellsLeft;
        static string S(WeaponDef d, string k) => Json.S(d.sounds, k);
        void PlayAt(string name, float vol) { if (string.IsNullOrEmpty(name)) return; if (ch.isPlayer) FBAudio.Play2D(name, vol); else FBAudio.PlayAt(name, ch.Pos + Vector3.up, vol, 4); }
    }

    // som do disparo (24/10, retorno do usuário: "mesmo sem silenciador o áudio sai com barulho de silenciador"): no fb_data.json 14 armas
    // (P90, Uzi, Skorpion, G36, ACR, Beretta, Pistola 308, SMG caseira…) têm como ÚNICO som "S_WEP_Fire_Silencer" (e garrucha/M31/SIG um
    // "Suppressed"), herdado do pacote de origem onde vinham com silenciador. Agora o som segue o acessório MONTADO (FBWeaponModel.Of):
    //  · sem silenciador: os sons "silenciados" dos dados são ignorados e entra um disparo aberto da classe (também quando o clipe não existe);
    //  · com silenciador: som abafado (S_WEP_Fire_Silencer / boom suprimido na escopeta), sem cauda, flash menor e sem alertar bots de longe.
    public static class FBWeaponSound
    {
        static readonly System.Text.RegularExpressions.Regex SUPP = new("silenc|suppress", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        static readonly Dictionary<string, string[]> OPEN = new()
        {
            ["pistola"] = new[] { "A_Pistol_Short_1", "A_Pistol_Short_2", "A_Pistol_Short_3", "S_WEP_Fire_002" },
            ["submetralhadora"] = new[] { "A_MachineGun_F_1", "A_MachineGun_F_2", "A_MachineGun_F_3", "A_MachineGun_F_4" },
            ["fuzil"] = new[] { "AK_ONE_S_HOT", "AKM_Single_Shot2", "AR_Single_Shot_2", "S_WEP_Fire_003" },
            ["escopeta"] = new[] { "S_WEP_Fire_008" },
            ["sniper"] = new[] { "A_Sniper_B_1", "A_Sniper_B_2", "A_Sniper_B_3", "A_Sniper_B_4", "A_Sniper_B_5" },
            ["explosivo"] = new[] { "S_WEP_Fire_Launcher" },
        };
        static readonly Dictionary<string, (string[] open, string[] supp, string[] tail)> cache = new();
        // SOM PRÓPRIO POR ARMA (rodada 40, 27/10: "praticamente todas as armas estão utilizando o mesmo som"): a camada do 171 trocava o
        // disparo de qualquer arma pelo da CLASSE (todo fuzil = RifleAssalto). Agora Resources/Audio/Weapons/weapon_sounds.json
        // (tools/re/weapon_audio.py) dá a cada uma das 54 armas o seu conjunto: disparos (variações de um tiro), eco de longe, sacar,
        // guardar, cartucho/bomba. O 171 fica só como reserva de "longe/sem bala" para arma sem tabela.
        static Dictionary<string, object> table; static bool tableTried;
        static Dictionary<string, object> T(WeaponDef d)
        {
            if (!tableTried) { tableTried = true; var ta = Resources.Load<TextAsset>("Audio/Weapons/weapon_sounds"); if (ta) table = Json.O(Json.Parse(ta.text) as Dictionary<string, object>, "armas"); }
            return d == null ? null : Json.O(table, d.id);
        }
        static string[] L(WeaponDef d, string k) => (Json.L(T(d), k) ?? new List<object>()).Select(o => o as string).Where(n => n != null && FBAudio.Has(n)).ToArray();
        static string One(WeaponDef d, string k) { var n = Json.S(T(d), k); return n != null && FBAudio.Has(n) ? n : null; }
        static string Pick(string[] l) => l.Length > 0 ? l[Random.Range(0, l.Length)] : null;
        public static string Draw(WeaponDef d) => One(d, "draw");
        public static string Holster(WeaponDef d) => One(d, "holster");
        public static string ReloadOf(WeaponDef d) => One(d, "reload");
        public static string Insert(WeaponDef d) => Pick(L(d, "insert"));
        public static string Close(WeaponDef d) => One(d, "close");
        public static int FireCount(WeaponDef d) => Sets(d).open.Length;
        static (string[] open, string[] supp, string[] tail) Sets(WeaponDef d)
        {
            if (cache.TryGetValue(d.id, out var s)) return s;
            var open = new List<string>(); var supp = new List<string>(); var tail = new List<string>();
            var own = L(d, "fire");
            if (own.Length > 0)
            {   // disparo próprio da arma; a cauda (reverb) dos dados entra só no tiro do jogador
                open.AddRange(own); var sp = d.cls == "escopeta" ? "Shotgun_Boom_103__Suppressed_" : "S_WEP_Fire_Silencer"; if (FBAudio.Has(sp)) supp.Add(sp);
                if (d.tail != null) foreach (var o in d.tail) if (o is string n && FBAudio.Has(n)) tail.Add(n);
                return cache[d.id] = (open.ToArray(), supp.ToArray(), tail.ToArray());
            }
            if (d.fire != null) foreach (var o in d.fire) if (o is string n && FBAudio.Has(n)) (SUPP.IsMatch(n) ? supp : open).Add(n);
            if (d.tail != null) foreach (var o in d.tail) if (o is string n && FBAudio.Has(n)) tail.Add(n);
            if (open.Count == 0 && OPEN.TryGetValue(d.cls ?? "", out var pool)) foreach (var n in pool) if (FBAudio.Has(n)) open.Add(n);
            if (open.Count == 0) foreach (var n in new[] { "disparo1", "disparo2", "disparo3" }) if (FBAudio.Has(n)) open.Add(n);
            if (supp.Count == 0) { var n = d.cls == "escopeta" ? "Shotgun_Boom_103__Suppressed_" : "S_WEP_Fire_Silencer"; if (FBAudio.Has(n)) supp.Add(n); }
            return cache[d.id] = (open.ToArray(), supp.ToArray(), tail.ToArray());
        }
        /// <summary>Silenciador montado = o cano escolhido (FBWeaponModel.Of) é um nó "Silencer" do GLB.</summary>
        public static bool Silenced(WeaponDef d)
        {
            if (d == null) return false; var c = FBWeaponModel.Of(d); if (c == null || c.muzzle < 0) return false;
            var node = Json.S(FBWeaponModel.Att(d, "muzzles", c.muzzle), "node") ?? ""; return SUPP.IsMatch(node);
        }
        public static string Fire(WeaponDef d, bool silenced)
        {
            var s = Sets(d); var l = silenced && s.supp.Length > 0 ? s.supp : s.open.Length > 0 ? s.open : s.supp;
            return l.Length > 0 ? l[Random.Range(0, l.Length)] : null;
        }
        public static string Tail(WeaponDef d) { var t = Sets(d).tail; return t.Length > 0 ? t[Random.Range(0, t.Length)] : null; }
        // tiro ouvido de longe, recarga e "sem bala" do 171 (null = os de antes)
        public static string Far(WeaponDef d) => d == null ? null : Pick(L(d, "far")) ?? FBAudio.Weapon171(d.id, d.cls, "far");
        public static string Reload(WeaponDef d) => d == null ? null : FBAudio.Weapon171(d.id, d.cls, "reload");   // reserva (arma sem recarga própria)
        public static string Empty(WeaponDef d) => d == null ? null : FBAudio.Weapon171(d.id, d.cls, "empty");
    }

    // cápsulas ejetadas (ejectCasing do main.js): pool fixo de cápsulas com física simples e som ao bater no chão
    public static class FBCasings
    {
        static readonly List<(Transform t, Vector3 v, float life, bool snd)> live = new(); static Mesh mesh; static Material mat; static int idx; static readonly List<Transform> pool = new();
        public static void Eject(FBCharacter ch, WeaponDef d)
        {
            if (!ch || d == null || d.cls == "explosivo" || !Camera.main || Vector3.Distance(Camera.main.transform.position, ch.Pos) > 25) return;
            if (pool.Count == 0)
            {
                var root = new GameObject("FB_Capsulas"); Object.DontDestroyOnLoad(root); mesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx") ?? MakeMesh();
                mat = new Material(FBShaders.Lit); mat.SetColor("_BaseColor", new Color(.83f, .66f, .28f)); mat.SetFloat("_Metallic", .9f); mat.SetFloat("_Smoothness", .6f);
                for (int i = 0; i < 40; i++) { var g = new GameObject("c"); g.transform.SetParent(root.transform); g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterial = mat; g.transform.localScale = new Vector3(.012f, .022f, .012f); g.SetActive(false); pool.Add(g.transform); }
                root.AddComponent<FBCasingTicker>();
            }
            var t = pool[idx++ % pool.Count]; t.gameObject.SetActive(true); var mz = ch.Muzzle ? ch.Muzzle : ch.transform; t.position = (ch.RightHand ? ch.RightHand.position : mz.position) + Vector3.up * .05f;
            var v = ch.transform.right * Random.Range(1.5f, 2.6f) + Vector3.up * Random.Range(1.2f, 2.2f) - ch.transform.forward * .4f; live.RemoveAll(x => x.t == t); live.Add((t, v, 0, false));
        }
        static Mesh MakeMesh() { var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); var m = g.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(g); return m; }
        public static void Tick(float dt)
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var (t, v, life, snd) = live[i]; life += dt; if (!t || life > 4) { if (t) t.gameObject.SetActive(false); live.RemoveAt(i); continue; }
                v.y -= 9.8f * dt; var np = t.position + v * dt; t.Rotate(720 * dt, 360 * dt, 0);
                if (FBWorld.GroundY(np.x, np.y + .05f, np.z, .05f, .2f) is float gy && np.y <= gy + .01f) { np.y = gy + .01f; v = new Vector3(v.x * .4f, Mathf.Abs(v.y) * .25f, v.z * .4f); if (!snd) { snd = true; FBAudio.PlayAt("casing" + Random.Range(1, 4), np, .35f, 2); } }
                t.position = np; live[i] = (t, v, life, snd);
            }
        }
    }
    public class FBCasingTicker : MonoBehaviour
    {
        static readonly int __fbt_Update = FBTempo.Id("FBCasingTicker.Update");                         // [parceiro] tempo por script (FBTempo, desligado no jogo)
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt() => FBCasings.Tick(Time.deltaTime);
    }
}
