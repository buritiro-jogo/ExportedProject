// HUD de armas e vida no estilo Free Fire (referência enviada pelo usuário), preto e branco:
//  - canto SUPERIOR direito: cartão grande da arma em uso (desarmado: o SOCO) (munição "6 /20" grande à esquerda, silhueta branca da arma à direita, acessório/
//    habilidade escrito embaixo e um fio branco do pente); abaixo, cartões pequenos das outras armas + mãos, com a silhueta e a reserva (∞).
//  - centro inferior: barras finas de COLETE (quando houver) e HP, com o texto "HP 200/200".
// Some no veículo e no rolê. Substitui o bloco antigo de silhueta e a vida do canto esquerdo do FBHUD.
using UnityEngine;
using UnityEngine.UI;

namespace FreeBala
{
    public class FBHudWeapons : MonoBehaviour
    {
        class Small { public RectTransform rt; public RawImage icon; public Text n, key; public Image bg; public string id; }
        RectTransform big, row, life; RawImage bigIcon; Text ammo, maxT, perk; Image magBar, magBg; Small[] smalls; string bigId;
        Image hpFill, hpLag, arFill, arBg; Text hpT, arT, kitT; float lag = 1;
        static readonly Color Panel = new(0, 0, 0, .55f), Panel2 = new(0, 0, 0, .42f), W = new(1, 1, 1, .95f), M = new(1, 1, 1, .6f);
        static Texture2D fist;

        public static FBHudWeapons Attach(Transform canvasRoot) { var g = new GameObject("ArmasHUD", typeof(RectTransform)); g.transform.SetParent(canvasRoot, false); var rt = (RectTransform)g.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; return g.AddComponent<FBHudWeapons>(); }
        static RectTransform R(Transform p, string n, Vector2 a, Vector2 pv, Vector2 pos, Vector2 size) { var rt = FBUI.Rect(p, n, a, a, pos, size); rt.pivot = pv; rt.anchoredPosition = pos; return rt; }
        static Text T(Transform p, string s, int sz, Vector2 a, Vector2 pv, Vector2 pos, Vector2 size, TextAnchor al, Color c)
        { var t = R(p, "t", a, pv, pos, size).gameObject.AddComponent<Text>(); t.font = FBUI.Title; t.fontSize = sz; t.alignment = al; t.color = c; t.text = s; t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.supportRichText = true; var sh = t.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .85f); sh.effectDistance = new Vector2(1.2f, -1.2f); return t; }
        static Image I(Transform p, string n, Vector2 a, Vector2 pv, Vector2 pos, Vector2 size, Color c) { var im = R(p, n, a, pv, pos, size).gameObject.AddComponent<Image>(); im.color = c; im.raycastTarget = false; return im; }
        static Image Fill(Transform p, Color c) { var im = FBUI.Panel(p, "fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, c); im.sprite = FBUI.WhiteSprite; im.type = Image.Type.Filled; im.fillMethod = Image.FillMethod.Horizontal; im.raycastTarget = false; return im; }
        static Image Grad(Transform p, Vector2 a, Vector2 pv, Vector2 pos, Vector2 size, float a0, float a1)
        {   // painel com degradê horizontal (mais escuro à esquerda), como os cartões do FF
            var tx = new Texture2D(64, 1) { wrapMode = TextureWrapMode.Clamp }; for (int x = 0; x < 64; x++) tx.SetPixel(x, 0, new Color(1, 1, 1, Mathf.Lerp(a0, a1, x / 63f))); tx.Apply();
            var im = I(p, "painel", a, pv, pos, size, Color.white); im.sprite = Sprite.Create(tx, new Rect(0, 0, 64, 1), Vector2.one * .5f); return im;
        }

        void Awake()
        {
            // canto SUPERIOR DIREITO, compacto como a referência: painel cinza translúcido em degradê; munição grande "7" + "/30" menor,
            // silhueta branca da arma à direita, perk embaixo, fio branco do pente; fileira de cartões menores (outras armas + mãos) com "∞"
            var TR = new Vector2(1, 1); const float W0 = 264;
            big = R(transform, "armaAtual", TR, TR, new Vector2(-18, -18), new Vector2(W0, 82)); var g = Grad(big, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, .22f, .55f); g.rectTransform.anchorMax = Vector2.one; g.color = new Color(.55f, .62f, .7f, 1);
            ammo = T(big, "", 32, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -6), new Vector2(70, 40), TextAnchor.UpperLeft, W);
            maxT = T(big, "", 15, new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, -20), new Vector2(60, 22), TextAnchor.UpperLeft, M);
            var bi = R(big, "silhueta", TR, TR, new Vector2(-8, -6), new Vector2(140, 50)); bigIcon = bi.gameObject.AddComponent<RawImage>(); bigIcon.raycastTarget = false;
            perk = T(big, "", 13, new Vector2(0, 0), new Vector2(0, 0), new Vector2(10, 12), new Vector2(W0 - 20, 18), TextAnchor.LowerLeft, W);
            magBg = I(big, "pente", new Vector2(0, 0), new Vector2(0, 0), new Vector2(10, 5), new Vector2(W0 - 20, 3), new Color(1, 1, 1, .2f)); magBar = Fill(magBg.transform, W);
            row = R(transform, "reservas", TR, TR, new Vector2(-18, -106), new Vector2(W0, 58)); smalls = new Small[4];
            for (int i = 0; i < 4; i++)
            {
                var s = new Small(); s.rt = R(row, "slot" + i, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(62, 58));
                s.bg = Grad(s.rt, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, .5f, .38f); s.bg.rectTransform.anchorMax = Vector2.one; s.bg.color = new Color(.35f, .4f, .46f, 1);
                var ic = R(s.rt, "ic", new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -6), new Vector2(54, 28)); s.icon = ic.gameObject.AddComponent<RawImage>(); s.icon.raycastTarget = false;
                s.n = T(s.rt, "∞", 14, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 3), new Vector2(60, 18), TextAnchor.LowerCenter, W);
                s.key = T(s.rt, "", 9, new Vector2(0, 1), new Vector2(0, 1), new Vector2(3, -2), new Vector2(12, 12), TextAnchor.UpperLeft, M);
                smalls[i] = s;
            }
            // vida no centro inferior
            life = R(transform, "vida", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 26), new Vector2(380, 50));
            arT = T(life, "", 14, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(200, 18), TextAnchor.UpperLeft, new Color(.62f, .8f, 1));
            arBg = I(life, "colete", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -18), new Vector2(380, 5), new Color(1, 1, 1, .15f)); arFill = Fill(arBg.transform, new Color(.55f, .78f, 1));
            hpT = T(life, "", 14, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 10), new Vector2(200, 18), TextAnchor.LowerLeft, W);
            kitT = T(life, "", 14, new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 10), new Vector2(200, 18), TextAnchor.LowerRight, W);
            var hb = I(life, "hp", new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(380, 7), new Color(1, 1, 1, .15f)); hpLag = Fill(hb.transform, new Color(1, 1, 1, .35f)); hpFill = Fill(hb.transform, W);
        }

        static Texture2D Fist()
        {
            if (fist) return fist; fist = Resources.Load<Texture2D>("UI/hud_soco"); if (fist) return fist;   // punho de frente (rodada 38), mesmo formato das silhuetas
            fist = new Texture2D(64, 36, TextureFormat.RGBA32, false);   // reserva: mão fechada simples
            for (int y = 0; y < 36; y++) for (int x = 0; x < 64; x++) { float dx = (x - 32) / 13f, dy = (y - 17) / 11f; float d = dx * dx + dy * dy; bool knuckle = y > 22 && y < 30 && Mathf.Abs(x - 32) < 14 && ((x - 18) % 7) < 6; fist.SetPixel(x, y, new Color(1, 1, 1, d < 1 || knuckle ? .95f : 0)); }
            fist.Apply(); return fist;
        }
        static string Perk(WeaponDef d)
        {
            var ch = FBWeaponModel.Of(d); var raw = Json.O(Json.O(GameData.WeaponsRaw, d.id), "att");
            string N(string kind, int i) => Json.S(FBWeaponModel.Att(d, kind, i), "name");
            if (ch.scope >= 0 && ch.scope != Json.I(raw, "scope", -1)) return N("scopes", ch.scope) ?? "Mira";
            if (ch.magazine > 0) return N("magazines", ch.magazine) ?? "Pente Estendido";
            if (ch.muzzle > 0) return N("muzzles", ch.muzzle) ?? "Silenciador";
            return d.auto ? "Automática" : "Semiautomática";
        }

        // [parceiro] v1 (thread de física): tempo do Update medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_Update = FBTempo.Id("FBHudWeapons.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            // r68: no De Rolê só aparece com arma da loja (FBGunShop.Armed); desarmado o rolê continua sem HUD de combate
            bool roleHide = FBMatch.I && FBMatch.I.mode != null && FBMatch.I.mode.role && !FBGunShop.Armed;
            var pl = FBPlayer.I; var ch = pl ? pl.ch : null; bool show = ch && ch.alive && !pl.Driving && !roleHide;
            big.gameObject.SetActive(show); row.gameObject.SetActive(show); life.gameObject.SetActive(ch && ch.alive && !roleHide); if (!ch) return;
            // vida e colete
            float hp = Mathf.Max(0, ch.hp), mx = Mathf.Max(1, ch.maxHp), k = Mathf.Clamp01(hp / mx); hpFill.fillAmount = k; lag = Mathf.Max(k, Mathf.MoveTowards(lag, k, Time.deltaTime * .5f)); hpLag.fillAmount = lag;
            hpFill.color = ch.knocked ? new Color(1, .5f, .25f) : k < .3f ? new Color(1, .35f, .3f) : W; hpT.text = $"HP {Mathf.CeilToInt(hp)}/{Mathf.RoundToInt(mx)}"; kitT.text = (FBMed.Busy ? "<color=#9be7b1>CURANDO</color>  " : "") + $"KIT ✚ {(FBMed.inf ? "∞" : FBMed.kits.ToString())}  <color=#ffffff99>[H]</color>";
            float ar = ch.vest != null ? ch.vest.dur : ch.armor, arMx = ch.vest != null ? ch.vest.max : 100; bool hasAr = ar > 0;
            arBg.gameObject.SetActive(hasAr); arT.gameObject.SetActive(hasAr); if (hasAr) { arFill.fillAmount = Mathf.Clamp01(ar / Mathf.Max(1, arMx)); arT.text = $"COLETE {Mathf.CeilToInt(ar)}/{Mathf.RoundToInt(arMx)}"; }
            if (!show) return;
            // arma atual
            // rodada 38 (usuário: "quando nenhuma arma está selecionada, o HUD fica vazio"): sem arma na mão o cartão grande mostra o SOCO
            // (mãos = slot 4 do FBWeaponUser, Cur nulo) — antes o cartão era escondido com d == null
            var wu = pl.wu; var d = wu.Cur; big.gameObject.SetActive(true); magBg.gameObject.SetActive(d != null);
            if (d == null)
            {
                if (bigId != "_soco") { bigId = "_soco"; bigIcon.texture = Fist(); perk.text = "SOCO · CORPO A CORPO"; }
                ammo.text = "∞"; ammo.color = W; maxT.text = ""; 
            }
            else
            {
                if (bigId != d.id) { bigId = d.id; bigIcon.texture = FBIcons.Weapon(d.id, 280, 100); perk.text = Perk(d); }
                int a = wu.Ammo; ammo.text = wu.reloadT > 0 ? "··" : a.ToString(); ammo.color = a <= Mathf.Max(1, d.mag / 5) ? new Color(1, .4f, .35f) : W;
                maxT.text = "/" + d.mag; maxT.rectTransform.anchoredPosition = new Vector2(10 + ammo.preferredWidth + 3, -20);
                magBar.fillAmount = wu.reloadT > 0 ? 1 - Mathf.Clamp01(wu.reloadT / Mathf.Max(.1f, 2.2f)) : Mathf.Clamp01(a / (float)Mathf.Max(1, d.mag));
            }
            // reservas: as outras armas (slots 0..2 menos a atual) e as mãos
            int n = 0;
            for (int i = 0; i < 3; i++) { var w = wu.slots[i]; if (w == null || i == wu.cur) continue; var s = smalls[n++]; s.rt.gameObject.SetActive(true); if (s.id != w.id) { s.id = w.id; s.icon.texture = FBIcons.Weapon(w.id, 160, 72); } s.n.text = wu.reserve[i] > 999 ? "∞" : (wu.ammo[i] + wu.reserve[i]).ToString(); s.key.text = (i + 1).ToString(); }
            if (wu.cur != FBWeaponUser.HANDS) { var s = smalls[n++]; s.rt.gameObject.SetActive(true); if (s.id != "_maos") { s.id = "_maos"; s.icon.texture = Fist(); } s.n.text = "∞"; s.key.text = "4"; }
            for (int i = n; i < smalls.Length; i++) smalls[i].rt.gameObject.SetActive(false);
            for (int i = 0; i < n; i++) smalls[i].rt.anchoredPosition = new Vector2(-(n - 1 - i) * 67, 0);   // alinhados à direita, sob o cartão grande
        }
    }
}
