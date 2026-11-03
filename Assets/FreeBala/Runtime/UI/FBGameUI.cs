// Telas de partida (porte das cartas do webui.js): LOJA da fase de compra / ARSENAL do treino (categorias, cartas com preço, posse e
// barrinhas de atributos, recomendação, dinheiro e tempo), MOCHILA (armas e kit médico), RODA DE EMOTES (B segurado, 8 fatias com os
// emotes equipados), AJUSTES (sensibilidade, FOV, mira, volumes, qualidade, sangue), MARCAÇÃO (Y), GPS (De Rolê), PASSE (XP).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace FreeBala
{
    // A classe FBShop agora fica em seu próprio arquivo dedicado (FBShop.cs) com o novo layout completo.


    public static class FBBag
    {
        public static bool Open => go && go.activeSelf; static GameObject go;
        public static void Toggle()
        {
            if (Open) { go.SetActive(false); if (!FBInput.Touch) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } return; }
            var p = FBMatch.Player; if (!p || !p.alive) return; if (go) Object.Destroy(go);
            var c = FBUI.Canvas("Mochila", 30); go = c.gameObject; var bg = FBUI.Panel(c.transform, "fundo", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, .7f));
            var pn = FBUI.Panel(bg.transform, "painel", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(700, 440), FBUI.Dark).transform;
            FBUI.Label(pn, "MOCHILA", 36, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -40), new Vector2(400, 50), TextAnchor.MiddleCenter, true, FBUI.Gold);
            var wu = p.GetComponent<FBWeaponUser>(); string[] SL = { "FUZIL", "SMG", "PISTOLA" };
            for (int i = 0; i < 3; i++)
            {
                int k = i; var d = wu.slots[i]; var card = FBUI.Panel(pn, "slot", new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-280 + i * 280, -170), new Vector2(260, 140), wu.cur == i ? new Color(.96f, .67f, .09f, .25f) : new Color(1, 1, 1, .06f));
                FBUI.Label(card.transform, SL[i], 12, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -16), new Vector2(-16, 18), TextAnchor.MiddleLeft, false, FBUI.Mut);
                FBUI.Label(card.transform, d != null ? d.name.ToUpper() : "—", 20, new Vector2(0, .5f), new Vector2(1, .5f), Vector2.zero, new Vector2(-16, 30), TextAnchor.MiddleLeft, true);
                if (d != null) { FBUI.Label(card.transform, $"{wu.ammo[i]} / {wu.reserve[i]}", 14, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 18), new Vector2(-16, 20), TextAnchor.MiddleLeft, false, FBUI.Mut); var b = card.gameObject.AddComponent<Button>(); b.onClick.AddListener(() => { wu.SwitchTo(k); Toggle(); }); }
            }
            FBUI.Button(pn, $"KIT MÉDICO ({(FBMed.inf ? "∞" : FBMed.kits.ToString())}) · USAR", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 130), new Vector2(380, 56), () => { Toggle(); FBPlayer.I?.SendMessage("TryHeal", SendMessageOptions.DontRequireReceiver); }, true);
            FBUI.Button(pn, "FECHAR (I)", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 50), new Vector2(240, 48), Toggle);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
    }

    // roda de emotes (B segurado): 8 fatias com os emotes equipados; solta B em cima de uma fatia = dança
    public static class FBEmoteWheel
    {
        static GameObject go; static List<string> ids; static int sel = -1; static Text[] labels;
        public static bool IsOpen => go && go.activeSelf;
        public static void Open()
        {
            var p = FBMatch.Player; if (!p || !p.alive || p.special || (FBMatch.I && FBMatch.I.state != "play")) return;
            ids = FBEmotes.Equipped(); if (ids.Count == 0) return; if (go) Object.Destroy(go);
            var c = FBUI.Canvas("RodaEmotes", 25); go = c.gameObject; labels = new Text[ids.Count];
            var ring = FBUI.Panel(c.transform, "anel", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(520, 520), new Color(0, 0, 0, .45f)); ring.sprite = FBUI.CircleSprite;
            for (int i = 0; i < ids.Count; i++) { float a = i / (float)ids.Count * Mathf.PI * 2; labels[i] = FBUI.Label(c.transform, FBEmotes.NameOf(ids[i]).ToUpper(), 14, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 190 - new Vector2(0, 46), new Vector2(170, 30), TextAnchor.MiddleCenter, true);
                var tex = FBIcons.Emote(ids[i], FBSession.skin, 128);   // pose do emote (FBIcons)
                if (tex) { var ri = new GameObject("icone", typeof(RectTransform), typeof(RawImage)); var rr = (RectTransform)ri.transform; rr.SetParent(c.transform, false); rr.anchorMin = rr.anchorMax = new Vector2(.5f, .5f); rr.anchoredPosition = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 190 + new Vector2(0, 8); rr.sizeDelta = new Vector2(96, 96); var rw = ri.GetComponent<RawImage>(); rw.texture = tex; rw.raycastTarget = false; } }
            sel = -1; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; go.AddComponent<FBEmoteWheelTicker>();
        }
        public static void Tick()
        {
            if (!IsOpen) return; var d = (Vector2)UnityEngine.InputSystem.Mouse.current.position.ReadValue() - new Vector2(Screen.width / 2f, Screen.height / 2f);
            if (d.magnitude > 60) { float a = Mathf.Atan2(d.x, d.y); if (a < 0) a += Mathf.PI * 2; sel = Mathf.RoundToInt(a / (Mathf.PI * 2) * ids.Count) % ids.Count; } else sel = -1;
            for (int i = 0; i < labels.Length; i++) labels[i].color = i == sel ? FBUI.Gold : Color.white;
        }
        public static void Close(bool apply)
        {
            if (!IsOpen) return; if (apply && sel >= 0) { var p = FBMatch.Player; if (p && p.PlayDance(ids[sel])) { FBPlayer.I?.ForceThirdPerson(); FBNet.I?.SendEvent("dance:" + ids[sel]); } }
            Object.Destroy(go); go = null; if (!FBInput.Touch && FBMatch.I && FBMatch.I.state == "play") { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }
    }
    public class FBEmoteWheelTicker : MonoBehaviour
    {
        // [parceiro] v1 (thread de física): tempo do Update medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_Update = FBTempo.Id("FBEmoteWheelTicker.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt() { FBEmoteWheel.Tick(); if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame) FBEmoteWheel.Close(true); }
    }

    // ajustes (Ajustes > Jogo / Controles / Áudio / Gráficos), salvos na sessão
    public static class FBSettings
    {
        // créditos de terceiros (licenças que exigem atribuição, ex. CC BY): texto em Resources/creditos.txt
        static void Credits(Transform parent)
        {
            var ta = Resources.Load<TextAsset>("creditos"); var bg = FBUI.Panel(parent, "creditos", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(.02f, .03f, .04f, .96f));
            FBUI.Label(bg.transform, ta ? ta.text : "", 14, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 40), new Vector2(900, 560), TextAnchor.UpperLeft, false, FBKit.Txt);
            FBUI.Button(bg.transform, "VOLTAR", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 50), new Vector2(240, 48), () => Object.Destroy(bg.gameObject), true);
        }
        // r47 (pedido 30/10: "todos os controles devem ter efeito real ... aplicação imediata, salvamento, carregamento, persistência"):
        // abas CONTROLES | ÁUDIO | VÍDEO; seis volumes por categoria (GERAL = AudioListener, EFEITOS, MÚSICA, VOZES, VEÍCULOS, AMBIENTE —
        // FBAudio.SetVolumes reaplica até nas fontes que já estão tocando); arrastar aplica na hora (FBSession.Apply) e grava em disco ao
        // soltar / ao fechar o painel (antes: PlayerPrefs.Save() a cada quadro do arrasto); ‹ volta e › avança (antes só avançava).
        static int lastTab;
        static readonly string[] TABS = { "CONTROLES", "ÁUDIO", "VÍDEO" };
        public static void Open(Transform parent) => Open(parent, lastTab);
        public static void Open(Transform parent, int tab)
        {
            var bg = FBUI.Panel(parent, "ajustes", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, .8f));
            bg.gameObject.AddComponent<FBSettingsSaver>();
            const float W = 980, H = 720; lastTab = tab = Mathf.Clamp(tab, 0, TABS.Length - 1);
            var p = FBKit.Box(bg.transform, "painel", 0, 0, W, H, FBKit.Bg, false); var pr = p.rectTransform; pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(.5f, .5f); pr.anchoredPosition = Vector2.zero; p.raycastTarget = true; FBKit.Frame(p);
            var o = p.transform;
            FBKit.T(o, "AJUSTES", 24, 32, 22, 400, 34, TextAnchor.MiddleLeft, true, Color.white);
            FBKit.Box(o, "fio", 32, 66, W - 64, 1, FBKit.Line, false);
            for (int i = 0; i < TABS.Length; i++) { int k = i; FBKit.TextTile(o, TABS[i], 32 + i * 176, 82, 168, 40, i == tab, () => { if (k == lastTab) return; Object.Destroy(bg.gameObject); Open(parent, k); }, 14); }
            float y = 144; const float LX = 48, CX = 420, CW = 512, RH = 52;
            void Label(string label, string hint)
            {
                FBKit.T(o, label, 14, LX, y + (hint != null ? 3 : 8), 360, 24, TextAnchor.MiddleLeft, true, FBKit.Txt);
                if (hint != null) FBKit.T(o, hint, 11, LX, y + 26, 360, 18, TextAnchor.MiddleLeft, false, FBKit.Dim);
            }
            void Slider(string label, float v, System.Action<float> set, string hint = null, System.Action released = null)
            {
                Label(label, hint);
                var val = FBKit.T(o, Mathf.RoundToInt(v * 100) + "%", 13, CX + CW - 56, y + 8, 56, 24, TextAnchor.MiddleRight, true, FBKit.Gold);
                var s = new GameObject("s_" + label, typeof(RectTransform), typeof(Slider)).GetComponent<Slider>(); var rt = (RectTransform)s.transform; rt.SetParent(o, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, .5f); rt.anchoredPosition = new Vector2(CX + 8, -y - 20); rt.sizeDelta = new Vector2(CW - 84, 8);
                var tb = FBUI.Panel(rt, "bg", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1, 1, 1, .12f)); FBKit.Pill(tb);
                var fa = FBUI.Rect(rt, "fa", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); var fill = FBUI.Panel(fa, "f", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, FBKit.Gold); FBKit.Pill(fill);
                var ha = FBUI.Rect(rt, "ha", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-20, 0)); var hd = FBUI.Panel(ha, "h", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(20, 20), Color.white); hd.sprite = FBUI.CircleSprite;
                s.fillRect = fill.rectTransform; s.handleRect = hd.rectTransform; s.targetGraphic = hd; s.value = v;
                var cb = s.colors; cb.highlightedColor = new Color(1, .93f, .75f); cb.pressedColor = FBKit.Gold; s.colors = cb;
                s.onValueChanged.AddListener(x2 => { set(x2); val.text = Mathf.RoundToInt(x2 * 100) + "%"; FBSession.Apply(); FBSettingsSaver.dirty = true; });
                var tr = s.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>(); var up = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
                up.callback.AddListener(_ => { if (FBSettingsSaver.dirty) { FBSettingsSaver.dirty = false; FBSession.Save(); } released?.Invoke(); }); tr.triggers.Add(up);
                y += RH;
            }
            void Choice(string label, string[] opts, int cur, System.Action<int> set, string[] hints = null)
            {
                int c = Mathf.Clamp(cur, 0, opts.Length - 1); Text hintT = null;
                FBKit.T(o, label, 14, LX, y + (hints != null ? 3 : 8), 360, 24, TextAnchor.MiddleLeft, true, FBKit.Txt);
                if (hints != null) hintT = FBKit.T(o, hints[c], 11, LX, y + 26, 360, 18, TextAnchor.MiddleLeft, false, FBKit.Dim);
                var b = FBKit.Tile(o, "op_" + label, CX, y + 4, CW, 36, false, null);
                var t = FBKit.T(b.transform, opts[c], 13, 48, 0, CW - 96, 36, TextAnchor.MiddleCenter, true, Color.white);
                // pontos de posição (quantas opções e qual está ativa)
                var dots = new Image[opts.Length]; for (int i = 0; i < opts.Length; i++) dots[i] = FBKit.Box(b.transform, "pt", CW / 2 - opts.Length * 6 + i * 12 + 3, 30, 6, 3, i == c ? FBKit.Gold : new Color(1, 1, 1, .2f), false);
                void Go(int d) { c = (c + d + opts.Length) % opts.Length; t.text = opts[c]; if (hintT) hintT.text = hints[c]; for (int i = 0; i < dots.Length; i++) dots[i].color = i == c ? FBKit.Gold : new Color(1, 1, 1, .2f); set(c); FBSession.Save(); }
                b.onClick.AddListener(() => Go(1));
                FBKit.Arrow(b.transform, false, 0, 44, 36, () => Go(-1)); FBKit.Arrow(b.transform, true, CW - 44, 44, 36, () => Go(1));
                y += RH;
            }
            if (tab == 0)
            {
                Slider("SENSIBILIDADE", FBSession.sens, v => FBSession.sens = v, "velocidade da câmera (mouse e analógico)");
                Slider("SENSIBILIDADE MIRANDO", FBSession.adsSens, v => FBSession.adsSens = v, "câmera com a mira aberta / luneta");
                Slider("CAMPO DE VISÃO", FBSession.fov, v => FBSession.fov = v, "55° a 95°");
                Choice("ESTILO DE MIRA", new[] { "PADRÃO", "ALTERNATIVA", "MANUAL" }, FBSession.aimAssist == "ff" ? 1 : FBSession.aimAssist == "manual" ? 2 : 0, i => FBSession.aimAssist = new[] { "assist", "ff", "manual" }[i],
                    new[] { "assistência suave que acompanha o alvo", "puxada mais forte para o alvo ao mirar", "sem assistência de mira" });
                Choice("BOTÃO DE MIRA", new[] { "SEGURAR", "ALTERNAR" }, FBSession.aimToggle ? 1 : 0, i => FBSession.aimToggle = i == 1, new[] { "mira aberta enquanto segura o botão direito", "um clique abre, outro fecha" });
                Choice("CORRIDA", new[] { "AUTOMÁTICA (SHIFT)", "SEGURAR SHIFT" }, FBSession.holdRun ? 1 : 0, i => FBSession.holdRun = i == 1);
                Choice("INVERTER EIXO Y", new[] { "NÃO", "SIM" }, FBSession.invertY ? 1 : 0, i => FBSession.invertY = i == 1);
                Choice("HUD MOBILE NA TELA", new[] { "DESLIGADO", "LIGADO" }, FBSession.mobileHUD ? 1 : 0, i => { FBSession.mobileHUD = i == 1; FBSession.Save(); }, new[] { "controles por teclado e mouse", "botões de toque virtuais ativos na tela" });
                Label("LAYOUT DOS BOTÕES MOBILE", "mover, dimensionar, transparência e visibilidade");
                FBKit.Primary(o, "PERSONALIZAR HUD MOBILE", CX, y + 4, CW, 36, () => FBMobileHudEditor.Open());
                y += RH;
            }
            else if (tab == 1)
            {
                Slider("VOLUME GERAL", FBSession.master, v => FBSession.master = v, "todo o som do jogo", () => FBAudio.Synth("hover"));
                Slider("EFEITOS", FBSession.sfx, v => FBSession.sfx = v, "tiros, impactos, passos, interface", () => FBAudio.Synth("hover"));
                Slider("MÚSICA", FBSession.music, v => FBSession.music = v, "música do menu e paredões");
                Slider("VOZES", FBSession.voice, v => FBSession.voice = v, "falas, rádio, reações de dor");
                Slider("VEÍCULOS", FBSession.vehicles, v => FBSession.vehicles = v, "motores, buzinas, sirenes, freios");
                Slider("AMBIENTE", FBSession.ambient, v => FBSession.ambient = v, "cidade, chuva, pássaros, obras");
            }
            else
            {
                Choice("QUALIDADE GRÁFICA", System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Concat(new[] { "AUTOMÁTICA" }, FBQuality.Names)), FBQuality.Choice + 1, i => FBQuality.Set(i - 1),
                    new[] { "detectado neste aparelho: " + FBQuality.Names[(int)FBQuality.Detected], "sombras e efeitos mínimos", "equilíbrio", "sombras, reflexos e névoa", "tudo no máximo" });
                Choice("SANGUE", new[] { "LIGADO", "DESLIGADO" }, FBSession.blood ? 0 : 1, i => FBSession.blood = i == 0);
            }
            FBKit.Box(o, "fio2", 32, H - 80, W - 64, 1, FBKit.Line, false);
            FBKit.TextTile(o, "CRÉDITOS", 32, H - 64, 170, 44, false, () => Credits(bg.transform), 13);
            FBKit.TextTile(o, "RESTAURAR PADRÕES", 214, H - 64, 230, 44, false, () => { Defaults(tab); FBSession.Save(); Object.Destroy(bg.gameObject); Open(parent, tab); }, 13);
            FBKit.Primary(o, "VOLTAR", W - 252, H - 64, 220, 44, () => Object.Destroy(bg.gameObject));
        }
        // padrões do SET do main.js, por aba
        static void Defaults(int tab)
        {
            if (tab == 0) { FBSession.sens = .5f; FBSession.adsSens = .5f; FBSession.fov = .375f; FBSession.aimAssist = "assist"; FBSession.aimToggle = false; FBSession.holdRun = false; FBSession.invertY = false; FBSession.freeLookToggle = false; }
            else if (tab == 1) { FBSession.master = .9f; FBSession.sfx = 1; FBSession.music = .5f; FBSession.voice = 1; FBSession.vehicles = 1; FBSession.ambient = 1; }
            else { FBQuality.Set(-1); FBSession.blood = true; }
        }
    }
    // grava os ajustes quando o painel fecha (VOLTAR, troca de aba, pausa fechada) se algum slider mudou sem soltar o botão em cima dele
    public class FBSettingsSaver : MonoBehaviour
    {
        public static bool dirty;
        void OnDestroy() { if (dirty) { dirty = false; FBSession.Save(); } }
    }

    // marcação (Y / botão do meio): inimigo na mira = alerta para o time; senão marca o local; some em 8 s
    public static class FBMarks
    {
        static readonly List<(GameObject g, float t)> marks = new();
        public static void Mark(Camera cam)
        {
            if (!cam) return; Vector3 p; bool enemy = false;
            if (Physics.Raycast(cam.transform.position, cam.transform.forward, out var h, 300, FBWorld.ShotMask, QueryTriggerInteraction.Collide)) { p = h.point; var hb = h.collider.GetComponent<FBHitbox>(); enemy = hb && FBMatch.Hostile(hb.owner, FBMatch.Player); if (enemy) hb.owner.tagT = 8; } else return;
            var g = FBMarker.Make(p, enemy ? new Color(1, .25f, .2f) : FBUI.Gold, .8f, 30).gameObject; marks.Add((g, Time.time)); FBAudio.Synth(enemy ? "mark_enemy" : "mark");
            for (int i = marks.Count - 1; i >= 0; i--) if (!marks[i].g || Time.time - marks[i].t > 8 || marks.Count > 4) { if (marks[i].g) Object.Destroy(marks[i].g); marks.RemoveAt(i); }
        }
        public static void Clear() { foreach (var m in marks) if (m.g) Object.Destroy(m.g); marks.Clear(); }
    }

    // GPS do De Rolê (G): mapa grande com a sua posição e a oficina/garagem
    public static class FBGps
    {
        static GameObject big; public static bool BigOn => big;
        public static void ToggleBig()
        {
            if (big) { Object.Destroy(big); big = null; if (!FBInput.Touch) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } return; }
            var c = FBUI.Canvas("GPS", 28); big = c.gameObject; var bg = FBUI.Panel(c.transform, "fundo", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, .8f));
            if (FBMinimap.Tex) { var im = FBUI.Panel(bg.transform, "mapa", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(620, 620), Color.white); im.sprite = Sprite.Create(FBMinimap.Tex, new Rect(0, 0, FBMinimap.Tex.width, FBMinimap.Tex.height), Vector2.one * .5f);
                var me = FBMatch.Player; if (me) { var uv = FBMinimap.UV(me.Pos); var dot = FBUI.Panel(im.transform, "eu", Vector2.zero, Vector2.zero, new Vector2(uv.x * 760, uv.y * 760), new Vector2(16, 16), FBUI.Gold); dot.sprite = FBUI.ArrowSprite; dot.rectTransform.localRotation = Quaternion.Euler(0, 0, -FBPlayer.CamYawRad * Mathf.Rad2Deg); } }
            FBUI.Label(bg.transform, "GPS · G FECHA", 18, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 40), new Vector2(400, 30), TextAnchor.MiddleCenter, true, FBUI.Gold);
        }
    }

    // passe de batalha (30 níveis): XP por abate/headshot/partida
    public static class FBPass
    {
        public const int PER_TIER = 1000, TIERS = 30;
        public static int Tier => Mathf.Min(TIERS, FBSession.passXp / PER_TIER);
        // prêmios liberados e ainda não resgatados (selo "N PRÊMIOS" do lobby, passPending do web): o nível 1 já vem liberado
        public static int Claimed { get => PlayerPrefs.GetInt("fb_passClaimed", 0); set => PlayerPrefs.SetInt("fb_passClaimed", value); }
        public static int Claimable() => Mathf.Max(0, Mathf.Min(TIERS, Tier + 1) - Claimed);
        public static void AddXP(int v, string why) { int before = Tier; FBSession.passXp += v; if (Tier > before) FBHUD.I?.Toast("PASSE DE BATALHA", $"NÍVEL {Tier}", "RECOMPENSA LIBERADA NO MENU", new Color(.96f, .67f, .09f)); }
    }
}
