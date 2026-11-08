// LOBBY (porte do webui.js, estilo Free Fire): palco 3D com o personagem escolhido segurando a arma principal e o diretor de animação

// (FBLobbyDirector: parado longo do IE, inspeção, recarga, guardar/sacar, olhar a arma/câmera, emote curto), perfil e créditos, barra

// lateral (PASSE, ARMAS, PERSONAGEM, EMOTES, CONTROLES, AJUSTES, AMIGOS/ONLINE), cartão do modo à direita (abre o seletor de partida:

// modo, mapa, dificuldade, horário e clima por mapa, lado no Farinha × Buriti) e COMEÇAR. Páginas: ARSENAL (arma por slot + mira/cano/

// carregador/laser/empunhadura com prévia girando), PERSONAGEM (tela própria FBCustomize: vitrine 3D, grade, guarda-roupa),

// EMOTES (8 equipados + dança da vitória, prévia no palco), PASSE (30 níveis), CONTROLES, AJUSTES. Tudo salvo (FBSession).

using System.Collections.Generic;

using System.Linq;

using UnityEngine;

using UnityEngine.SceneManagement;

using UnityEngine.UI;



namespace FreeBala

{

    public partial class FBMenu : MonoBehaviour

    {

        public static readonly (string id, string name, string tag, string desc)[] MODES = {

            ("role", "DE ROLÊ", "ROLE PLAY", "Modo livre: sem armas e sem inimigos. Carros liberados, paredão de som (K) e NPCs que curtem perto do som."),

            ("classico", "CLÁSSICO", "4 × 4", "Mata-mata 4×4 numa área delimitada do mapa. Primeiro time a 40 abates ou quem liderar após 8 minutos."),

            ("squad", "4X4", "RODADAS", "Rodadas com compra no início, zona que fecha dentro da área da partida, primeiro a vencer 4 rodadas. Morreu, perde o equipamento."),

            // [parceiro 31/10] modo 10x10 por rodadas com compra (FBModo10x10)

            ("10x10", "10X10", "RODADAS · COMPRA", "Dois times de 10 nascem juntos, cada um do seu lado. Compre no começo da rodada; abates e rodadas dão dinheiro. Primeiro a vencer 7 rodadas. Morreu, perde o equipamento."),

        };

        static readonly Dictionary<string, string> MAP_INFO = new() { ["vila_jardim"] = "Favela · becos, lajes e telhados · combate de perto", ["praca_aguas"] = "Centro de Boa Vista · praça aberta e prédios · longa distância", ["praca_pt"] = "Parque Germano Sampaio · lagoa, mata, quadras · média distância", ["orla"] = "Parque do Rio Branco · Mirante, praia, calçadão · média/longa", ["morro_sacola"] = "Boa Vista · ruas de terra, muro pré-moldado, quintais · média distância", ["estande"] = "Estande de tiro · alvos de 10 a 100 m" };

        static readonly string[] DIFFS = { "FÁCIL", "NORMAL", "REALISTA" };

        List<MapDef> maps; List<SkinDef> skins; Transform root, page; GameObject preview, wpPreview; Camera cam; Text moneyT, nameT, modeT, mapT, charT; RawImage modeArt;



        void Start()

        {

            GameData.Load(); FBSession.Load(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; Time.timeScale = 1;

            // [parceiro 31/10] mapas da lista liberada em GameData.AllowedMaps (antes fixo em vila_jardim/praca_aguas), desde que a cena esteja no build

            maps = GameData.Maps.Values.Where(m => !m.disabled && GameData.AllowedMaps.Contains(m.id) && Application.CanStreamedLevelBeLoaded("Map_" + m.id)).ToList(); if (maps.Count == 0) maps = GameData.Maps.Values.Where(m => !m.disabled && (m.id == "vila_jardim" || m.id == "praca_aguas")).ToList();

            skins = GameData.Skins.Where(s => !s.role && FBRegistry.I && FBRegistry.I.HasChar(s.id)).ToList();

            if (FBSession.mode == "4v4") FBSession.mode = "squad";

            if (MODES.All(m => m.id != FBSession.mode)) FBSession.mode = "classico";

            if (!maps.Any(m => m.id == FBSession.map)) FBSession.map = "vila_jardim";   // [parceiro 31/10] mapa salvo fora da lista do menu volta para a Vila Jardim

            if (FBSession.pickRandom)   // sorteio do mesmo sexo entre TODOS os personagens da tela de personagem; o visual vai junto (salvo por personagem)

            { bool fem = FBCustomizeData.Find(FBSession.skin)?.fem ?? false; var pool = FBCustomizeData.Roster.Where(e => e.fem == fem).ToList(); if (pool.Count > 0) { FBSession.skin = pool[Random.Range(0, pool.Count)].id; FBSession.look = FBCustomizeData.LookFor(FBSession.skin); } }

            cam = Camera.main; if (cam) { cam.transform.SetPositionAndRotation(CamHome, Quaternion.identity); cam.fieldOfView = 30; }

            BuildStage();

            { var cv = FBUI.Canvas("Lobby", 5).transform; var sa = FBUI.Rect(cv, "area_segura", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); sa.gameObject.AddComponent<FBSafeArea>(); root = sa; }   // r47: entalhe/cantos do celular

            Home(); if (WantTitle()) Title();   // r61: tela de título só na primeira abertura

            FBAudio.SetLoop("menu_music", "music", .9f);   /* r47: categoria MÚSICA (o volume do ajuste entra na hora; antes saía pelos efeitos e só valia ao reabrir o menu) */

            I = this;

            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbcustom") >= 0) StartCoroutine(CustomShots());

            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fblobby") >= 0) StartCoroutine(LobbyShots());

            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbchars") >= 0) StartCoroutine(CharShots());

            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbload") >= 0) StartCoroutine(LoadShots());

            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbmenu") >= 0) StartCoroutine(MenuShots());

            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbposelab") >= 0) StartCoroutine(PoseShots());   // r61: comparação das poses do palco

        }

        public static FBMenu I { get; private set; }

        void OnDestroy() { FBAudio.StopLoop("menu_music"); }


        // r61 (07/11/2026, "tela inicial triplo A"): palco, INÍCIO, tela de título, páginas e pose do personagem em FBMenuInicio.cs

        static Texture2D T(string n) => Resources.Load<Texture2D>("UI/Lobby/" + n);




        // faixa do brilho do COMEÇAR (alfa em sino na horizontal)

        static Texture2D glintTex;

        static Texture2D GlintTex

        {

            get

            {

                if (glintTex) return glintTex; glintTex = new Texture2D(32, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

                for (int i = 0; i < 32; i++) { float s = Mathf.Sin(Mathf.PI * (i + .5f) / 32); glintTex.SetPixel(i, 0, new Color(1, 1, 1, s * s)); }

                glintTex.Apply(); return glintTex;

            }

        }

        static string WName(string id) => GameData.Weapons.TryGetValue(id ?? "", out var w) ? w.name : "—";



        // ---------------------------------------------------------------- SELETOR DE PARTIDA

        // esquerda: modos (cartões com arte) e mapas (cartões com foto, recorte que preenche — sem faixa cinza); direita: resumo do modo

        // escolhido e as opções da partida; CONFIRMAR embaixo à direita

        void MatchPicker()

        {

            var p = Page("ESCOLHER PARTIDA", true); if (preview) preview.SetActive(false); var b = FBKit.Body(p, 64); const float CW = 186, G = 12, RX = 812, RW = 428;

            FBKit.Section(b, "MODO DE JOGO", 0, 18, 400);

            for (int i = 0; i < MODES.Length; i++)

            {

                var m = MODES[i]; float x = (i % 4) * (CW + G), y = 46 + (i / 4) * (100 + G); bool sel = FBSession.mode == m.id;

                var t = FBKit.Tile(b, "modo_" + m.id, x, y, CW, 100, sel, () => { FBSession.mode = m.id; FBSession.Save(); MatchPicker(); });

                var tex = Resources.Load<Texture2D>("Modes/" + m.id); if (!tex) tex = Resources.Load<Texture2D>("MapShots/" + FBSession.map);

                var art = FBKit.Cover(t.transform, tex, 1, 1, CW - 2, 98); art.color = sel ? Color.white : new Color(.62f, .62f, .62f);

                FBKit.Shade(t.transform, 1, 30, CW - 2, 69);

                FBKit.T(t.transform, m.name, 15, 12, 54, CW - 20, 22, TextAnchor.MiddleLeft, true, Color.white);

                FBKit.T(t.transform, m.tag, 10, 12, 76, CW - 20, 16, TextAnchor.MiddleLeft, true, sel ? FBKit.Gold : FBKit.Dim);

            }

            FBKit.Section(b, "MAPA", 0, 176, 400);

            for (int i = 0; i < maps.Count; i++)

            {

                var m = maps[i]; float x = (i % 4) * (CW + G), y = 204 + (i / 4) * (124 + G); bool sel = FBSession.map == m.id;

                var t = FBKit.Tile(b, "mapa_" + m.id, x, y, CW, 124, sel, () => { FBSession.map = m.id; FBSession.Save(); MatchPicker(); });

                var img = FBKit.Cover(t.transform, Resources.Load<Texture2D>("MapShots/" + m.id), 1, 1, CW - 2, 122); img.color = sel ? Color.white : new Color(.62f, .62f, .62f);

                FBKit.Shade(t.transform, 1, 64, CW - 2, 59);

                FBKit.T(t.transform, m.name.ToUpper(), 14, 12, 96, CW - 20, 22, TextAnchor.MiddleLeft, true, Color.white);

            }

            // resumo à direita

            var M = MODES.FirstOrDefault(m => m.id == FBSession.mode); if (M.id == null) M = MODES[0];

            var card = FBKit.Box(b, "resumo", RX, 18, RW, 236).transform; card.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            var shot = Resources.Load<Texture2D>("MapShots/" + FBSession.map); var cardTex = shot ? shot : Resources.Load<Texture2D>("Modes/" + M.id); FBKit.Cover(card, cardTex, 1, 1, RW - 2, 234); FBKit.Shade(card, 1, 50, RW - 2, 184);

            FBKit.T(card, M.tag, 11, 16, 86, RW - 32, 16, TextAnchor.MiddleLeft, true, FBKit.Gold);

            FBKit.T(card, M.name, 24, 16, 98, RW - 32, 30, TextAnchor.MiddleLeft, true, Color.white);

            FBKit.T(card, M.desc, 13, 16, 132, RW - 32, 72, TextAnchor.UpperLeft, false, FBKit.Dim);

            var mp = GameData.Maps.TryGetValue(FBSession.map, out var mm) ? mm.name : FBSession.map;

            FBKit.T(card, mp.ToUpper() + (MAP_INFO.TryGetValue(FBSession.map, out var info) ? "  ·  " + info : ""), 11, 16, 206, RW - 32, 20, TextAnchor.MiddleLeft, true, FBKit.Txt);

            float y0 = 272; var wx = FBSession.WeatherFor(FBSession.map);

            void Seg(string title, string[] opts, int cur, System.Action<int> set)

            {

                FBKit.Section(b, title, RX, y0, RW); float w = (RW - (opts.Length - 1) * 8) / opts.Length;

                for (int i = 0; i < opts.Length; i++) { int k = i; FBKit.TextTile(b, opts[i], RX + i * (w + 8), y0 + 26, w, 38, i == cur, () => { set(k); FBSession.Save(); MatchPicker(); }, 13); }

                y0 += 80;

            }

            Seg("DIFICULDADE DOS BOTS", DIFFS, FBSession.diff, i => FBSession.diff = i);

            Seg("HORÁRIO", new[] { "DIA", "ANOITECER", "NOITE" }, wx.time == "anoitecer" ? 1 : wx.time == "noite" ? 2 : 0, i => FBSession.SetWeather(FBSession.map, new[] { "dia", "anoitecer", "noite" }[i], wx.weather));

            Seg("CLIMA", new[] { "LIMPO", "CHUVA" }, wx.weather == "chuva" ? 1 : 0, i => FBSession.SetWeather(FBSession.map, wx.time, i == 1 ? "chuva" : "limpo"));

            if (FBSession.mode == "pvl") Seg("SEU LADO", new[] { "FARINHA · POLÍCIA", "BURITI · LADRÃO" }, FBSession.team, i => FBSession.team = i);

            FBKit.Primary(b, "CONFIRMAR", RX, Mathf.Max(y0 + 12, 600), RW, 54, () => { if (preview) preview.SetActive(true); Home(); });

        }



        // ---------------------------------------------------------------- ARSENAL (porte do pageArsenal do webui.js — "gunsmith")

        // esquerda: EQUIPAMENTO (3 slots) + CATEGORIAS; centro: arma grande girando (arraste gira) com classe/nome/EQUIPADA e, embaixo, as

        // peças (MIRA, CANO, CARREGADOR, LASER, EMPUNHADURA) que abrem a bandeja de opções; direita: ATRIBUTOS (barras), ficha e EQUIPAR;

        // rodapé: carrossel de cartões das armas da categoria (páginas ‹ ›)

        static readonly string[] CLASS_ORDER = { "fuzil", "submetralhadora", "sniper", "escopeta", "pistola" };

        static readonly Dictionary<string, string> CLASS_N = new() { ["fuzil"] = "Fuzis", ["submetralhadora"] = "Submetralhadoras", ["sniper"] = "Snipers", ["escopeta"] = "Escopetas", ["pistola"] = "Pistolas" };

        static readonly (string k, string n)[] LOADOUT = { ("primary", "PRINCIPAL"), ("smg", "SMG"), ("secondary", "PISTOLA") };

        static readonly (string kind, string l)[] PARTS = { ("scopes", "MIRA"), ("muzzles", "CANO"), ("magazines", "CARREGADOR"), ("lasers", "LASER"), ("grips", "EMPUNHADURA") };

        string arCls, arSel, arOpen; int arPage = -1; float arYaw;

        static string Slot(string k) => k == "primary" ? FBSession.primary : k == "smg" ? FBSession.smg : FBSession.secondary;

        static void SetSlot(string k, string id) { if (k == "primary") FBSession.primary = id; else if (k == "smg") FBSession.smg = id; else FBSession.secondary = id; }

        static string SlotOf(WeaponDef d) => Mathf.Clamp(d.slot, 0, 2) == 0 ? "primary" : d.slot == 1 ? "smg" : "secondary";

        static string ClsOf(WeaponDef d) => System.Array.IndexOf(CLASS_ORDER, d.cls) >= 0 ? d.cls : "fuzil";

        static int AttSel(FBWeaponModel.Choice c, string kind) => kind == "scopes" ? c.scope : kind == "muzzles" ? c.muzzle : kind == "magazines" ? c.magazine : kind == "lasers" ? c.laser : c.grip;

        static void AttSet(FBWeaponModel.Choice c, string kind, int i) { if (kind == "scopes") c.scope = i; else if (kind == "muzzles") c.muzzle = i; else if (kind == "magazines") c.magazine = i; else if (kind == "lasers") c.laser = i; else c.grip = i; }



        void Arsenal()

        {

            var W = GameData.Weapons; var all = W.Values.Where(w => FBRegistry.I && FBRegistry.I.Weapon(w.id)).ToList();

            if (arCls == null && W.TryGetValue(FBSession.primary, out var p0)) arCls = ClsOf(p0); arCls ??= "fuzil";

            var ids = all.Where(w => ClsOf(w) == arCls).OrderBy(w => w.name).ToList(); if (ids.Count == 0) { arCls = "fuzil"; ids = all.Where(w => ClsOf(w) == arCls).OrderBy(w => w.name).ToList(); }

            if (!ids.Any(w => w.id == arSel)) { var d0 = ids.FirstOrDefault(); arSel = d0 != null && ids.Any(w => w.id == Slot(SlotOf(d0))) ? Slot(SlotOf(d0)) : d0?.id; }

            if (arSel == null || !W.TryGetValue(arSel, out var d)) return;

            var p = Page("ARSENAL", false); if (preview) preview.SetActive(false);   // sem véu escuro: a arma 3D fica ATRÁS da tela (véu apagava a arma)

            var b = FBUI.Rect(p, "corpo", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); b.offsetMin = new Vector2(26, 18); b.offsetMax = new Vector2(-26, -74);

            const float LW = 220, RW = 290, G = 16, H = 808, CAR = 120; float MW = 1548 - LW - RW - 2 * G, MX = LW + G, RX = MX + MW + G;

            var c = FBWeaponModel.Of(d); var raw = Json.O(Json.O(GameData.WeaponsRaw, d.id), "att"); string slotK = SlotOf(d); bool eq = Slot(slotK) == d.id;

            // ---- esquerda: equipamento + categorias

            FBKit.Box(b, "fundoL", -10, -8, LW + 20, H - CAR - G + 12, new Color(.02f, .025f, .03f, .82f), false); FBKit.Box(b, "fundoR", RX - 10, -8, RW + 20, H - CAR - G + 12, new Color(.02f, .025f, .03f, .82f), false); FBKit.Box(b, "fundoB", -10, H - CAR - 8, 1568, CAR + 22, new Color(.02f, .025f, .03f, .82f), false);

            Hdr(b, "EQUIPAMENTO", 0, 0, LW);

            for (int i = 0; i < 3; i++)

            {

                var (k, n) = LOADOUT[i]; var id = Slot(k); bool on = id == arSel; var t = FBKit.Tile(b, "slot_" + k, 0, 24 + i * 76, LW, 68, false, () => { if (W.TryGetValue(id ?? "", out var dd)) { arCls = ClsOf(dd); arSel = id; arPage = -1; arOpen = null; Arsenal(); } });

                t.GetComponent<Image>().color = on ? new Color(1, .74f, .12f, .14f) : new Color(1, 1, 1, .04f);

                FBKit.Box(t.transform, "borda", 0, 0, 3, 68, on ? FBKit.Gold : new Color(.33f, .33f, .33f), false);

                FBKit.T(t.transform, n, 9, 12, 8, 120, 14, TextAnchor.MiddleLeft, true, FBKit.Dim);

                FBKit.T(t.transform, WName(id).ToUpper(), 14, 12, 24, LW - 20, 20, TextAnchor.MiddleLeft, true, Color.white);

                var ic = FBIcons.Weapon(id, 200, 70); if (ic) { var ri = FBKit.At(t.transform, "ic", LW - 104, 30, 96, 34).gameObject.AddComponent<RawImage>(); ri.texture = ic; ri.raycastTarget = false; ri.color = new Color(1, 1, 1, .85f); }

            }

            Hdr(b, "CATEGORIAS", 0, 262, LW);

            for (int i = 0; i < CLASS_ORDER.Length; i++)

            {

                var cl = CLASS_ORDER[i]; bool on = cl == arCls; int cnt = all.Count(w => ClsOf(w) == cl);

                var t = FBKit.Tile(b, "cat_" + cl, 0, 286 + i * 40, LW, 36, false, () => { arCls = cl; arSel = null; arPage = 0; arOpen = null; Arsenal(); });

                t.GetComponent<Image>().color = on ? new Color(1, .74f, .12f, .16f) : new Color(0, 0, 0, 0); t.GetComponent<Outline>().enabled = false;

                if (on) FBKit.Box(t.transform, "borda", 0, 0, 2, 36, FBKit.Gold, false);

                FBKit.T(t.transform, CLASS_N[cl], 13, 12, 0, 160, 36, TextAnchor.MiddleLeft, true, on ? Color.white : FBKit.Dim);

                FBKit.T(t.transform, cnt.ToString(), 11, LW - 40, 0, 28, 36, TextAnchor.MiddleRight, true, FBKit.Dim);

            }

            // ---- centro: vitrine + peças (+ bandeja)

            var parts = PARTS.Where(x => { var l = Json.L(raw, x.kind); return l != null && l.Any(o => o != null); }).ToList();

            bool tray = arOpen != null && parts.Any(x => x.kind == arOpen); float slotsY = H - CAR - G - 70, trayH = 104, viewH = slotsY - G - (tray ? trayH + 8 : 0);

            var view = FBKit.Box(b, "vitrine", MX, 0, MW, viewH, new Color(0, 0, 0, 0)); view.raycastTarget = true;

            FBKit.T(view.transform, CLASS_N[ClsOf(d)].ToUpper(), 11, 18, 14, 400, 16, TextAnchor.MiddleLeft, true, FBKit.Gold);

            FBKit.T(view.transform, d.name, 42, 18, 30, MW - 36, 50, TextAnchor.MiddleLeft, true, Color.white);

            if (eq) { var tg = FBKit.Box(view.transform, "eq", 18, 86, 104, 20, new Color(0, 0, 0, .5f)); tg.GetComponent<Outline>().effectColor = new Color(.21f, .76f, .35f); FBKit.T(tg.transform, "✓ EQUIPADA", 10, 0, 0, 104, 20, TextAnchor.MiddleCenter, true, new Color(.48f, .89f, .6f)); }

            FBKit.T(view.transform, "⟲ ARRASTE PARA GIRAR", 10, MW - 200, viewH - 26, 186, 16, TextAnchor.MiddleRight, true, new Color(1, 1, 1, .35f));

            var drag = view.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>(); var de = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.Drag };

            de.callback.AddListener(e => { arYaw -= ((UnityEngine.EventSystems.PointerEventData)e).delta.x * .5f; if (wpPreview) wpPreview.transform.rotation = Quaternion.Euler(0, arYaw, 0); }); drag.triggers.Add(de);

            if (tray)

            {

                var part = parts.First(x => x.kind == arOpen); var opts = Json.L(raw, arOpen); var ol = new List<int>(); if (arOpen == "scopes" || arOpen == "lasers" || arOpen == "grips") ol.Add(-1); for (int i = 0; i < opts.Count; i++) if (opts[i] != null) ol.Add(i);

                var tb = FBKit.Box(b, "bandeja", MX, viewH + 8, MW, trayH, new Color(.04f, .05f, .055f, .92f)); tb.GetComponent<Outline>().effectColor = new Color(1, .74f, .12f, .35f);

                FBKit.T(tb.transform, part.l + "   <color=#9aa3ab>escolha uma opção</color>", 10, 10, 6, 400, 16, TextAnchor.MiddleLeft, true, FBKit.Gold).supportRichText = true;

                int sel = AttSel(c, arOpen); float ow = Mathf.Min(150, (MW - 20 - 5 * 7) / 8f);

                for (int j = 0; j < ol.Count && j < 16; j++)

                {

                    int idx = ol[j]; string lab = idx < 0 ? (arOpen == "scopes" ? "Mira de ferro" : "Nenhum") : AttLabel(arOpen, opts[idx] as Dictionary<string, object>, idx); string kind = arOpen;

                    FBKit.TextTile(tb.transform, lab, 10 + (j % 8) * (ow + 5), 26 + (j / 8) * 38, ow, 34, idx == sel, () => { AttSet(c, kind, idx); FBWeaponModel.Set(d, c); Arsenal(); }, 11);

                }

            }

            if (parts.Count == 0) FBKit.T(b, "Esta arma não aceita acessórios", 12, MX + 16, slotsY, MW, 70, TextAnchor.MiddleLeft, false, FBKit.Dim);

            float pw = parts.Count > 0 ? (MW - (parts.Count - 1) * 8) / parts.Count : 0;

            for (int i = 0; i < parts.Count; i++)

            {

                var (kind, l) = parts[i]; var opts = Json.L(raw, kind); int s = AttSel(c, kind); bool on = arOpen == kind;

                string name = s < 0 || s >= opts.Count || opts[s] == null ? (kind == "scopes" ? "Mira de ferro" : "Nenhum") : AttLabel(kind, opts[s] as Dictionary<string, object>, s);

                var t = FBKit.Tile(b, "peca_" + kind, MX + i * (pw + 8), slotsY, pw, 70, false, () => { arOpen = arOpen == kind ? null : kind; Arsenal(); });

                t.GetComponent<Image>().color = on ? new Color(1, .74f, .12f, .16f) : new Color(.055f, .067f, .08f, .85f);

                FBKit.Box(t.transform, "topo", 0, 0, pw, 2, on ? FBKit.Gold : new Color(1, 1, 1, .12f), false);

                FBKit.T(t.transform, l, 9, 10, 12, pw - 20, 14, TextAnchor.MiddleLeft, true, FBKit.Dim);

                FBKit.T(t.transform, name, 13, 10, 30, pw - 20, 22, TextAnchor.MiddleLeft, true, Color.white);

            }

            // ---- direita: atributos, ficha, equipar

            Hdr(b, "ATRIBUTOS", RX, 0, RW);

            var mg = Json.L(raw, "magazines"); int ms = c.magazine; int ammo = mg != null && ms >= 0 && ms < mg.Count && mg[ms] != null ? Json.I(mg[ms] as Dictionary<string, object>, "ammo", d.mag) : d.mag;

            var mz = Json.L(raw, "muzzles"); bool sil = mz != null && c.muzzle >= 0 && c.muzzle < mz.Count && (Json.S(mz[c.muzzle] as Dictionary<string, object>, "node") ?? "").Contains("Silencer");

            float spread = d.spread > 0 ? d.spread : .25f, dmg = d.Damage(0, false) * Mathf.Max(1, d.pellets) * (sil ? .92f : 1);

            var md = FBWeaponModel.ModsOf(d, c); var mdB = FBWeaponModel.ModsOf(d, c, true);

            static float PrecOf(float sp, float k) => Mathf.Clamp(100 - sp * 60 * k, 5, 110);

            var stats = new (string l, float v, float m, string f)[] {

                ("DANO", dmg, 150, dmg.ToString("0")), ("CADÊNCIA", d.rpm, 1000, d.rpm.ToString("0") + " rpm"), ("ALCANCE", 100 / spread, 900, (100 / spread).ToString("0") + " m"),

                // r45: o que o tiro usa de verdade (FBWeaponModel.ModsOf -> FBRecoil): dispersão sem mirar/mirando, recuo vertical/lateral, mobilidade

                ("PRECISÃO", PrecOf(spread, md.spreadHip), 110, PrecOf(spread, md.spreadHip).ToString("0")),

                ("PRECISÃO MIRANDO", PrecOf(spread * .45f, md.spreadAds), 110, PrecOf(spread * .45f, md.spreadAds).ToString("0") + (md.laser ? " · laser" : "")),

                ("CONTROLE DE RECUO", 100 / (md.recV * .7f + md.recH * .3f), 200, (100 / (md.recV * .7f + md.recH * .3f)).ToString("0") + (md.bipod ? " · " + (100 / (mdB.recV * .7f + mdB.recH * .3f)).ToString("0") + " apoiado" : "")),

                ("MOBILIDADE", (d.move > 0 ? d.move : 1) * 100 * md.move - (ammo > 60 ? 6 : 0), 120, ((d.move > 0 ? d.move : 1) * 100 * md.move - (ammo > 60 ? 6 : 0)).ToString("0") + "%"),

                ("MUNIÇÃO", ammo, 200, ammo.ToString()), ("SILÊNCIO", sil ? 90 : 20, 100, sil ? "silenciada" : "alta") };

            var sb = FBKit.Box(b, "atributos", RX, 24, RW, 22 + stats.Length * 34, new Color(.04f, .05f, .055f, .6f)).transform;

            for (int i = 0; i < stats.Length; i++)

            {

                var s = stats[i]; float y = 12 + i * 34; FBKit.T(sb, s.l, 10, 12, y, 140, 14, TextAnchor.MiddleLeft, true, FBKit.Dim); FBKit.T(sb, s.f, 12, RW - 132, y, 120, 14, TextAnchor.MiddleRight, true, FBKit.Txt);

                var bg = FBKit.Box(sb, "barra", 12, y + 18, RW - 24, 5, new Color(1, 1, 1, .08f), false); FBKit.Box(bg.transform, "f", 0, 0, (RW - 24) * Mathf.Clamp(s.v / s.m, .03f, 1), 5, new Color(.93f, .91f, .86f), false);

            }

            float iy = 24 + 22 + stats.Length * 34 + 10; float cw = (RW - 6) / 2;

            var info = new[] { ("TIPO", CLASS_N[ClsOf(d)]), ("PENTE", ammo + " balas"), ("DISPARO", d.pellets > 1 ? $"Chumbo ({d.pellets})" : d.auto ? "Automático" : d.bolt ? "Ferrolho" : "Semi"), ("SLOT", LOADOUT.First(x => x.k == slotK).n) };

            for (int i = 0; i < 4; i++) { var t = FBKit.Box(b, "info", RX + (i % 2) * (cw + 6), iy + (i / 2) * 50, cw, 44, new Color(1, 1, 1, .035f)).transform; FBKit.T(t, info[i].Item1, 9, 9, 6, cw - 18, 12, TextAnchor.MiddleLeft, true, FBKit.Dim); FBKit.T(t, info[i].Item2, 13, 9, 20, cw - 18, 18, TextAnchor.MiddleLeft, true, Color.white); }

            float ey = H - CAR - G - 64;

            if (eq) { var t = FBKit.Box(b, "equipada", RX, ey, RW, 64, new Color(.21f, .76f, .35f, .16f)); t.GetComponent<Outline>().effectColor = new Color(.21f, .76f, .35f, .5f); FBKit.T(t.transform, "✓ EQUIPADA", 20, 0, 10, RW, 28, TextAnchor.MiddleCenter, true, new Color(.48f, .89f, .6f)); FBKit.T(t.transform, "SLOT " + LOADOUT.First(x => x.k == slotK).n, 10, 0, 40, RW, 14, TextAnchor.MiddleCenter, true, new Color(.48f, .89f, .6f, .7f)); }

            else { var bt = FBKit.Primary(b, "EQUIPAR", RX, ey, RW, 64, () => { SetSlot(slotK, d.id); FBSession.Save(); Arsenal(); }); var tx = bt.GetComponentInChildren<Text>(); tx.fontSize = 22; tx.rectTransform.anchoredPosition += new Vector2(0, 7); FBKit.T(bt.transform, "NO SLOT " + LOADOUT.First(x => x.k == slotK).n, 10, 0, 42, RW, 14, TextAnchor.MiddleCenter, true, new Color(.16f, .1f, 0, .7f)); }

            // ---- rodapé: carrossel

            float cy = H - CAR; const float CWD = 156, CG = 10; float inner = 1548 - 2 * 34 - 2 * 8; int per = Mathf.Max(1, Mathf.FloorToInt((inner + CG) / (CWD + CG))), pages = Mathf.CeilToInt(ids.Count / (float)per);

            if (arPage < 0) arPage = Mathf.Max(0, ids.FindIndex(w => w.id == arSel)) / per; arPage = Mathf.Clamp(arPage, 0, Mathf.Max(0, pages - 1));

            if (pages > 1) { FBKit.TextTile(b, "‹", 0, cy, 34, CAR, false, () => { arPage--; Arsenal(); }, 22); FBKit.TextTile(b, "›", 1548 - 34, cy, 34, CAR, false, () => { arPage++; Arsenal(); }, 22); }

            for (int i = 0; i < per && arPage * per + i < ids.Count; i++)

            {

                var w = ids[arPage * per + i]; bool on = w.id == arSel, isEq = FBSession.primary == w.id || FBSession.smg == w.id || FBSession.secondary == w.id;

                var t = FBKit.Tile(b, "card_" + w.id, 42 + i * (CWD + CG), cy, CWD, CAR, false, () => { arSel = w.id; arOpen = null; Arsenal(); });

                t.GetComponent<Image>().color = on ? new Color(.16f, .14f, .07f, .95f) : new Color(.08f, .095f, .11f, .95f); if (on) t.GetComponent<Outline>().effectColor = new Color(1, .74f, .12f, .6f);

                FBKit.Box(t.transform, "base", 0, CAR - 3, CWD, 3, on ? FBKit.Gold : new Color(.2f, .2f, .2f), false);

                var ic = FBIcons.Weapon(w.id, 280, 110); if (ic) { var ri = FBKit.At(t.transform, "ic", 10, 20, CWD - 20, 58).gameObject.AddComponent<RawImage>(); ri.texture = ic; ri.raycastTarget = false; }

                FBKit.T(t.transform, w.name, 13, 8, CAR - 30, CWD - 16, 20, TextAnchor.MiddleLeft, true, Color.white);

                if (isEq) { var e = FBKit.Box(t.transform, "eq", CWD - 64, 6, 58, 14, new Color(.21f, .76f, .35f), false); FBKit.T(e.transform, "EQUIPADA", 8, 0, 0, 58, 14, TextAnchor.MiddleCenter, true, new Color(0, .27f, .07f)); }

            }

            if (pages > 1) for (int i = 0; i < pages; i++) FBKit.Box(b, "pg", 1548 / 2f - pages * 10.5f + i * 21, H + 6, 16, 3, i == arPage ? FBKit.Gold : new Color(1, 1, 1, .18f), false);

            StartCoroutine(PlaceWeapon(d, view.rectTransform));

        }

        static void Hdr(Transform p, string s, float x, float y, float w) { FBKit.T(p, s, 11, x, y, w, 16, TextAnchor.MiddleLeft, true, FBKit.Dim); var ln = FBKit.Box(p, "fio", x + s.Length * 9 + 10, y + 8, Mathf.Max(0, w - s.Length * 9 - 10), 1, FBKit.Line, false); }

        // a arma 3D fica no centro da vitrine (posição de tela → mundo pela câmera do lobby), escalada para ocupar ~60 % da largura dela

        System.Collections.IEnumerator PlaceWeapon(WeaponDef d, RectTransform view)

        {

            yield return null; if (!view || !cam) yield break; if (wpPreview) Destroy(wpPreview);

            wpPreview = FBWeaponModel.Make(d); if (!wpPreview) yield break;

            var cs = new Vector3[4]; view.GetWorldCorners(cs); var center = (cs[0] + cs[2]) / 2 + new Vector3(0, -(cs[2].y - cs[0].y) * .06f, 0); float vw = cs[2].x - cs[0].x;

            const float dist = 3.2f; var pos = cam.ScreenPointToRay(center).GetPoint(dist);

            wpPreview.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, arYaw, 0)); wpPreview.transform.localScale = Vector3.one;

            var rs = wpPreview.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray(); if (rs.Length == 0) yield break; var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);   // só peças visíveis (acessórios desligados inflavam a caixa)

            float fw = 2 * dist * Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) * cam.aspect, want = fw * vw / Screen.width * .7f, size = Mathf.Max(bb.size.x, bb.size.z, .01f);

            wpPreview.transform.localScale = Vector3.one * (want / size); wpPreview.transform.position += pos - Center(wpPreview);

            if (!wpPreview.GetComponent<FBSpin>()) wpPreview.AddComponent<FBSpin>().speed = 12;

        }

        static Vector3 Center(GameObject g) { var rs = g.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray(); var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds); return bb.center; }

        static string AttLabel(string kind, Dictionary<string, object> a, int i)

        {

            if (a == null) return "—";

            if (kind == "scopes") { float f = Json.F(a, "fieldOfViewMultiplierAim", .9f); return f >= .745f ? $"Red dot {i + 1}" : f >= .645f ? $"Holográfica {i + 1}" : f >= .595f ? $"ACOG 2x ({i + 1})" : f >= .495f ? $"Luneta 4x ({i + 1})" : $"Luneta 6x ({i + 1})"; }

            var node = Json.S(a, "node") ?? ""; if (kind == "muzzles") return node.Contains("Silencer") ? "Silenciador" : "Cano padrão"; if (kind == "magazines") return $"{Json.I(a, "ammo")} balas";

            if (kind == "lasers") return node.Contains("Flash") ? "Lanterna" : "Laser"; return node.Contains("Bipod") ? "Bipé" : "Empunhadura";

        }

        // ---------------------------------------------------------------- PERSONAGEM

        // tela própria (FBCustomize): vitrine 3D, grade com miniaturas, filtros/busca/favoritos, guarda-roupa por categoria; volta ao INÍCIO

        public FBCustomize Customize { get; private set; }

        public void Characters()

        {

            Clear(); if (preview) { Destroy(preview); preview = null; }

            Customize = FBCustomize.Open(cam, () => { Customize = null; Home(); });

        }

        // -fbcustom: abre a tela de personagem e grava os prints em <build>/autoteste/personagem_*.png (sem -fbauto, fecha no fim)

        // -fbload: prévia da tela de carregamento em 16:9, 21:9 e retrato → <build>/autoteste/carregamento_*.png (sem -fbauto, fecha no fim)

        System.Collections.IEnumerator LoadShots()

        {

            yield return new WaitForSeconds(1.5f);

            yield return FBLoading.Shots(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"), FBSession.map, FBSession.mode);

            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbauto") < 0) Application.Quit(0);

        }

        // -fbmenu: abre cada página do lobby e grava <build>/autoteste/menu_<página>.png (sem -fbauto, fecha no fim)

        System.Collections.IEnumerator MenuShots()

        {

            var dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"); System.IO.Directory.CreateDirectory(dir);

            yield return new WaitForSeconds(2.5f);

            var pages = new (string n, System.Action a)[] { ("titulo", () => { Home(); Title(); }), ("inicio", Home), ("partida", MatchPicker), ("armas", Arsenal), ("armas_mira", () => { arOpen = "scopes"; Arsenal(); }), ("armas_pistola", () => { arOpen = null; arCls = "pistola"; arSel = null; Arsenal(); }), ("emotes", Emotes), ("passe", Pass), ("controles", Controls), ("ajustes", () => { Home(); FBSettings.Open(root, 0); }), ("ajustes_audio", () => { Home(); FBSettings.Open(root, 1); }), ("ajustes_video", () => { Home(); FBSettings.Open(root, 2); }), ("online", Online) };

            foreach (var (n, a) in pages) { a(); yield return new WaitForSeconds(1.5f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "menu_" + n + ".png")); yield return new WaitForSeconds(.6f); }

            Home(); if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbauto") < 0) Application.Quit(0);

        }

        // -fbchars: print de cada jogável na vitrine (corpo e rosto) em <build>/autoteste/char_*.png e rosto_*.png (sem -fbauto, fecha no fim)

        System.Collections.IEnumerator CharShots()

        {

            yield return new WaitForSeconds(2); Characters(); yield return null;

            yield return Customize.CharShots(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"));

            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbauto") < 0) Application.Quit(0);

        }

        // r42: autoteste do LOBBY e da SELEÇÃO (usuário: "a tela inicial apresenta inúmeros problemas envolvendo animações, esqueletos

        // quebrados ... ao selecionar um personagem, executamos uma animação feminina em todos"). Por personagem: início (diretor do

        // lobby com a arma: parado longo, inspecionar/recarregar/guardar) em 3 momentos e a seleção com a pose de confirmar

        // -> autoteste/lobby/<id>_*.png e [FreeBala][lobby] com o clipe que tocou

        System.Collections.IEnumerator LobbyShots()

        {

            var dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste", "lobby"); System.IO.Directory.CreateDirectory(dir);

            var keep = FBSession.skin; yield return new WaitForSeconds(2);

            System.Collections.IEnumerator Shot(string n) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png")); yield return null; yield return null; }

            Home(); yield return new WaitForSeconds(1f); NameDialog(); yield return new WaitForSeconds(.6f); yield return Shot("nome_usuario"); if (nameDlg) Destroy(nameDlg);

            { var t1 = FBSession.CleanName("  zé_da#vila!!  ", out var w1); var t2 = FBSession.CleanName("ab", out var w2); Debug.Log($"[FreeBala][lobby] nome: 'zé_da#vila!!' -> '{t1}' ({w1}) · 'ab' -> {(t2 ?? "recusado")} ({w2})"); }

            foreach (var id in new[] { "r_tutu", "r_elaine", "r_giga", "r_vivi", "r_miguel", "r_pm_silva", "r_aka_junin", "r_faria" })

            {

                if (!FBRegistry.I.HasChar(id)) continue; FBSession.skin = id; Home();

                var ch = preview ? preview.GetComponent<FBCharacter>() : null; var ld = preview ? preview.GetComponent<FBLobbyDirector>() : null;

                for (int k = 0; k < 3; k++) { yield return new WaitForSeconds(k == 0 ? 2.5f : 5.5f); yield return Shot($"{id}_inicio_{k}"); Debug.Log($"[FreeBala][lobby] {id} início {k}: clipe {(ch && ch.dance != null ? ch.dance.clip : ch ? ch.OverrideOf("Rifle Idle") : "-")}"); }

                Characters(); yield return null; Customize.Select(id); yield return new WaitForSeconds(1.6f); yield return Shot($"{id}_selecao");

                Customize.stage.PlayConfirm(); yield return new WaitForSeconds(1.4f); yield return Shot($"{id}_confirmar");

                var sc = Customize.stage.ch; Debug.Log($"[FreeBala][lobby] {id} ({(sc && sc.IsFemale ? "f" : "m")}) confirmar: {(sc && sc.dance != null ? sc.dance.clip : "-")}");

            }

            FBSession.skin = keep; Debug.Log("[FreeBala][lobby] fim"); yield return new WaitForSeconds(.5f); Application.Quit(0);

        }

        System.Collections.IEnumerator CustomShots()

        {

            yield return new WaitForSeconds(2); Characters(); yield return null;

            var dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste");

            yield return Customize.Shots(dir, true);

            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbauto") < 0) Application.Quit(0);

        }



        // ---------------------------------------------------------------- EMOTES

        // esquerda: catálogo com miniatura da pose; palco no centro mostra o emote; direita: os 8 da roda e a dança da vitória

        void Emotes()

        {

            var p = Page("EMOTES"); var eq = FBEmotes.Equipped(); var b = FBKit.Body(p, 64);

            FBKit.Box(b, "fundoL", 0, 16, 380, 740, new Color(0, 0, 0, .55f)); FBKit.Section(b, "CATÁLOGO · CLIQUE PARA EQUIPAR", 14, 30, 360);

            var list = FBKit.Scroll(b, 8, 58, 364, 690);

            foreach (var e in FBEmotes.All.Where(x => FBRegistry.I.Clip(x.id)).OrderBy(x => x.cat).ThenBy(x => x.name))

            {

                var ee = e; bool on = eq.Contains(e.id);

                var r = FBKit.Row(list, "          " + e.name.ToUpper(), on ? "NA RODA ✓" : e.cat.ToUpper(), on, () =>

                {

                    var l = FBEmotes.Equipped(); if (l.Contains(ee.id)) l.Remove(ee.id); else { if (l.Count >= 8) l.RemoveAt(0); l.Add(ee.id); }

                    FBSession.emoteSlots = string.Join("|", l); FBSession.Save(); Preview(false, ee.id); Emotes();

                }, 48);

                var ic = FBIcons.Emote(e.id, FBSession.skin, 96); if (ic) { var ri = FBUI.Rect(r.transform, "ic", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(34, 0), new Vector2(42, 42)).gameObject.AddComponent<RawImage>(); ri.texture = ic; ri.raycastTarget = false; }

            }

            const float RX = 900, RW = 340;

            var box = FBKit.Box(b, "roda", RX, 16, RW, 470, new Color(0, 0, 0, .55f)).transform; FBKit.Section(box, $"NA RODA ({eq.Count}/8)", 14, 14, 300);

            for (int i = 0; i < 8; i++)

            {

                float x = 14 + (i % 2) * 160, y = 44 + (i / 2) * 104; var id = i < eq.Count ? eq[i] : null; var t = FBKit.Box(box, "slot", x, y, 152, 96, FBKit.Card).transform;

                if (id == null) { FBKit.T(t, "VAZIO", 11, 0, 0, 152, 96, TextAnchor.MiddleCenter, true, FBKit.Dim); continue; }

                var ic = FBIcons.Emote(id, FBSession.skin, 96); if (ic) { var ri = FBKit.At(t, "ic", 46, 6, 60, 60).gameObject.AddComponent<RawImage>(); ri.texture = ic; ri.raycastTarget = false; }

                FBKit.T(t, FBEmotes.NameOf(id).ToUpper(), 10, 6, 70, 140, 20, TextAnchor.MiddleCenter, true, FBKit.Txt);

            }

            FBKit.Section(b, "DANÇA DA VITÓRIA", RX, 504, RW);

            FBKit.TextTile(b, FBSession.victory != null ? FBEmotes.NameOf(FBSession.victory).ToUpper() + "  ›" : "ALEATÓRIA  ›", RX, 530, RW, 44, true, () => { var l = FBEmotes.Equipped(); int i = l.IndexOf(FBSession.victory ?? ""); FBSession.victory = i + 1 < l.Count ? l[i + 1] : null; FBSession.Save(); Emotes(); }, 14);

            FBKit.T(b, "Na partida: segure B para abrir a roda de emotes.", 12, RX, 590, RW, 40, TextAnchor.UpperLeft, false, FBKit.Dim);

        }



        // ---------------------------------------------------------------- PASSE / CONTROLES / ONLINE

        void Pass()

        {

            var p = Page("PASSE DE BATALHA", true); var b = FBKit.Body(p, 64); int tier = FBPass.Tier; FBPass.Claimed = Mathf.Min(FBPass.TIERS, tier + 1);

            int into = FBSession.passXp % FBPass.PER_TIER;

            FBKit.T(b, "TEMPORADA 1", 12, 0, 22, 400, 18, TextAnchor.MiddleLeft, true, FBKit.Gold);

            FBKit.T(b, $"NÍVEL {tier}", 34, 0, 40, 400, 44, TextAnchor.MiddleLeft, true, Color.white);

            var bar = FBKit.Box(b, "barra", 0, 96, FBKit.MaxW, 8, new Color(1, 1, 1, .1f), false); FBKit.Box(bar.transform, "f", 0, 0, FBKit.MaxW * into / (float)FBPass.PER_TIER, 8, FBKit.Gold, false);

            FBKit.T(b, $"{into} / {FBPass.PER_TIER} XP PARA O PRÓXIMO NÍVEL", 12, FBKit.MaxW - 500, 70, 500, 18, TextAnchor.MiddleRight, true, FBKit.Dim);

            const float S = 112, G = 12;

            for (int i = 0; i < FBPass.TIERS; i++)

            {

                bool got = i < tier, next = i == tier; float x = (i % 10) * (S + G), y = 136 + (i / 10) * (S + 34);

                var t = FBKit.Box(b, "nivel", x, y, S, S, got ? new Color(1, .74f, .12f, .2f) : next ? new Color(1, 1, 1, .09f) : FBKit.Card).transform;

                if (got || next) t.GetComponent<Outline>().effectColor = got ? new Color(1, .74f, .12f, .8f) : new Color(1, 1, 1, .4f);

                FBKit.T(t, (i + 1).ToString(), 30, 0, 18, S, 50, TextAnchor.MiddleCenter, true, got ? FBKit.Gold : FBKit.Txt);

                FBKit.T(t, got ? "LIBERADO" : next ? "PRÓXIMO" : "BLOQUEADO", 10, 0, 76, S, 18, TextAnchor.MiddleCenter, true, got ? FBKit.Gold : FBKit.Dim);

                FBKit.T(b, i % 5 == 4 ? "★ PRÊMIO" : "$ " + (100 + i * 20), 11, x, y + S + 6, S, 16, TextAnchor.MiddleCenter, true, FBKit.Dim);

            }

            FBKit.T(b, "Abate +100 XP  ·  Tiro na cabeça +150  ·  Partida +200  ·  Vitória +500", 13, 0, 590, FBKit.MaxW, 20, TextAnchor.MiddleCenter, false, FBKit.Dim);

        }

        void Controls()
        {
            var p = Page("CONTROLES", true); var b = FBKit.Body(p, 64); const float W = 400, G = 20;
            (string t, string[] l)[] cols = {
                ("TECLADO E MOUSE", new[] { "W A S D|mover", "SHIFT|corrida automática", "ESPAÇO|pular / pular janela", "C / Z|agachar / deitar", "Q / E|inclinar", "R|recarregar", "H|kit médico (C alterna em pé/agachado)", "X|pegar do chão", "F|veículo · segurar: reanimar", "1–4 · RODA|trocar arma", "I|mochila", "B|emotes (segure) / loja", "T|inspecionar", "V|distância da câmera (no veículo)", "ALT|olhar livre", "Y|marcar", "G|GPS / killstreak", "K|central multimídia", "TAB|placar", "ENTER|chat", "ESC|pausa" }),
                ("CONTROLE", new[] { "ANALÓGICOS|mover / olhar", "RT|atirar", "LT|mirar", "A|pular", "B|agachar", "X|recarregar", "Y|trocar arma", "LB / RB|inclinar", "L3|correr", "R3|câmera", "D-PAD ↑|veículo / reanimar", "D-PAD ↓|pegar", "D-PAD ←|kit médico", "D-PAD →|loja / emotes", "START|pausa", "SELECT|placar" }),
                ("TOQUE (MOBILE)", new[] { "ANDAR|analógico virtual", "ATIRAR|disparo + mira livre", "TIRO ESQ.|disparo secundário", "MIRA|abrir mira (ADS)", "PULO|pular / janelas", "AGACHAR|agachar / abaixar", "DEITAR|deitar no chão", "CORRER|travar corrida", "RECARREGAR|trocar munição", "TROCAR ARMA|alternar armas", "EMOTES|abrir roda de danças", "CURAR|kit médico rápido", "PEGAR|coletar loot / armas", "AÇÃO|veículo / reanimar", "MAPA|mapa tático e zonas", "CÂMERA|olhar livre / visão" }) };

            for (int c = 0; c < cols.Length; c++)
            {
                float x = c * (W + G); var box = FBKit.Box(b, "col", x, 18, W, 700, new Color(0, 0, 0, .45f)).transform; FBKit.Section(box, cols[c].t, 16, 16, W);

                if (c == 2)
                {
                    FBKit.Primary(box, "EDITAR HUD MOBILE", 16, 48, W - 32, 36, () => FBMobileHudEditor.Open());
                    FBKit.TextTile(box, FBSession.mobileHUD ? "HUD NA TELA: ATIVADO" : "HUD NA TELA: DESATIVADO", 16, 90, W - 32, 28, FBSession.mobileHUD, () => { FBSession.mobileHUD = !FBSession.mobileHUD; FBSession.Save(); Controls(); }, 12);

                    for (int i = 0; i < cols[c].l.Length; i++)
                    {
                        var kv = cols[c].l[i].Split('|'); float y = 126 + i * 26;
                        var k = FBKit.Box(box, "k", 16, y, 140, 22, new Color(1, 1, 1, .08f)); FBKit.T(k.transform, kv[0], 10, 0, 0, 140, 22, TextAnchor.MiddleCenter, true, FBKit.Txt);
                        FBKit.T(box, kv[1], 12, 166, y, W - 182, 22, TextAnchor.MiddleLeft, false, FBKit.Dim);
                    }
                }
                else
                {
                    for (int i = 0; i < cols[c].l.Length; i++)
                    {
                        var kv = cols[c].l[i].Split('|'); float y = 50 + i * 30;
                        var k = FBKit.Box(box, "k", 16, y, 150, 24, new Color(1, 1, 1, .08f)); FBKit.T(k.transform, kv[0], 11, 0, 0, 150, 24, TextAnchor.MiddleCenter, true, FBKit.Txt);
                        FBKit.T(box, kv[1], 13, 180, y, W - 196, 24, TextAnchor.MiddleLeft, false, FBKit.Dim);
                    }
                }
            }
        }

        // r43: NOME DE USUÁRIO (pedido 28/10: "quero que nossos users possam escolher seu próprio user name, para não ter que usar o nome do
        // personagem"): aparece no placar, no feed de abates e online. 3–16 letras/números/_ . -; primeira vez o jogo pergunta sozinho.
        bool askedName; GameObject nameDlg;

        void NameDialog()

        {

            if (nameDlg) Destroy(nameDlg);

            var ov = FBUI.Panel(root, "nome_usuario", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, .72f)); nameDlg = ov.gameObject; ov.raycastTarget = true;

            const float W = 520, H = 300; var box = FBUI.Panel(ov.transform, "caixa", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(W, H), new Color(.06f, .065f, .075f, .97f)).transform;

            FBKit.T(box, "SEU NOME DE JOGADOR", 22, 28, 22, W - 56, 30, TextAnchor.MiddleLeft, true, Color.white);

            FBKit.T(box, "Aparece no placar, no feed de abates e no online — não precisa ser o nome do personagem.", 13, 28, 56, W - 56, 36, TextAnchor.UpperLeft, false, FBKit.Dim);

            var bg = FBKit.Box(box, "campo", 28, 100, W - 56, 48, new Color(1, 1, 1, .07f)); bg.raycastTarget = true;

            var t = FBKit.T(bg.transform, "", 20, 14, 0, W - 84, 48, TextAnchor.MiddleLeft, true, Color.white);

            var f = bg.gameObject.AddComponent<InputField>(); f.textComponent = t; f.characterLimit = 16; f.text = FBSession.nameChosen ? FBSession.name : "";

            var msg = FBKit.T(box, "", 12, 28, 152, W - 56, 22, TextAnchor.MiddleLeft, false, new Color(1, .55f, .4f));

            void Save()

            {

                var n = FBSession.CleanName(f.text, out var why); if (n == null) { msg.text = why; return; }

                FBSession.SetName(n.ToUpper()); if (nameT) nameT.text = FBSession.name; Destroy(nameDlg);

            }

            FBKit.Primary(box, "SALVAR", 28, 196, W - 56 - 150, 52, Save);

            FBKit.TextTile(box, "CANCELAR", W - 28 - 140, 196, 140, 52, false, () => Destroy(nameDlg), 14);

            f.onEndEdit.AddListener(_ => { var kb = UnityEngine.InputSystem.Keyboard.current; if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) Save(); });

            f.ActivateInputField();

        }

        void Online()

        {

            var p = Page("JOGAR ONLINE", true); if (preview) preview.SetActive(false); var b = FBKit.Body(p, 64); const float W = 560; float x = (FBKit.MaxW - W) / 2;

            var box = FBKit.Box(b, "online", x, 90, W, 420, new Color(0, 0, 0, .55f)).transform;

            FBKit.T(box, "SALA ONLINE", 22, 28, 24, W - 56, 30, TextAnchor.MiddleLeft, true, Color.white);

            FBKit.T(box, "Mesmo servidor do jogo web: navegador e app entram na mesma sala.", 13, 28, 58, W - 56, 36, TextAnchor.UpperLeft, false, FBKit.Dim);

            InputField F(string label, string val, float y) { FBKit.Section(box, label, 28, y, W); var bg = FBKit.Box(box, "campo", 28, y + 26, W - 56, 44, new Color(1, 1, 1, .06f)); bg.raycastTarget = true; var t = FBKit.T(bg.transform, val, 17, 14, 0, W - 84, 44, TextAnchor.MiddleLeft, false, Color.white); var f = bg.gameObject.AddComponent<InputField>(); f.textComponent = t; f.text = val; return f; }

            var nameF = F("SEU NOME", FBSession.name, 112); var serverF = F("SERVIDOR (ws://IP:8123/ws)", FBSession.server, 200);

            FBKit.Primary(box, "ENTRAR NA SALA DO MAPA", 28, 320, W - 56, 56, () => { { var nn = FBSession.CleanName(nameF.text, out _); if (nn != null) FBSession.SetName(nn.ToUpper()); } FBSession.server = serverF.text.Trim(); FBSession.online = true; FBSession.Save(); Go(); });

        }



        void Play() { FBSession.online = false; FBSession.Save(); Go(); }

        void Go()

        {

            var scene = "Map_" + (FBSession.mode == "treino" && Application.CanStreamedLevelBeLoaded("Map_estande") && FBSession.map == "estande" ? "estande" : FBSession.map);

            if (!Application.CanStreamedLevelBeLoaded(scene)) { scene = "Map_" + maps[0].id; FBSession.map = maps[0].id; }

            if (FBLoading.Busy) return; Clear(); if (preview) preview.SetActive(false);

            FBLoading.Load(scene, FBSession.map, FBSession.mode);                    // tela de carregamento (arte do mapa, progresso real, aquecimento)

        }


    }

    public class FBSpin : MonoBehaviour

    {

        public float speed = 25;

        static readonly int __fbt_Update = FBTempo.Id("FBSpin.Update");   // [parceiro 01/11] medição do FBTempo (ver FBMenu.Update)

        void Update() { if (!FBTempo.On) { Update__fbt(); return; } long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0); }

        void Update__fbt() => transform.Rotate(0, speed * Time.deltaTime, 0, Space.World);

    }

}

