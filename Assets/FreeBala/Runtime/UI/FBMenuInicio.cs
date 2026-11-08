// r61 (07/11/2026) TELA INICIAL "TRIPLO A" (pedido: "ta muito feia e sem organização e muito amadora ... retrabalhar a pose inicial do
// nosso player, menus etc. mudaremos a logo do nosso game para a logo oficial"). Substitui o INÍCIO do r47 e o palco antigo.
//
//  - PALCO: fundo azul-noite (Resources/UI/Lobby/lobby_bg2, gerado por unity/tools/menu_aaa/bake_bg.py), brilho quente (buriti) e azul
//    (bandeira de RR) que seguem o personagem na tela, dois fachos de luz que respiram, poeira subindo e luz de chão (FBLobbyStage).
//    Luzes 3D: chave branca pela esquerda, recorte azul atrás-esquerda e laranja atrás-direita (seguem o personagem).
//  - INÍCIO: personagem no terço direito, de 3/4, câmera mais perto e respirando; arrastar gira o personagem.
//    Barra de topo: logo oficial + abas (JOGAR, ARSENAL, PERSONAGEM, EMOTES, PASSE) + dinheiro, amigos/controles/ajustes e perfil.
//    Esquerda embaixo: modo em destaque, mapa (miniatura + descrição), condições, ALTERAR PARTIDA, AMIGOS e JOGAR.
//    Direita: NOVIDADES (carrossel que troca sozinho e leva à página/escolha) e o cartão do PASSE. Nome do personagem aos pés dele.
//  - TÍTULO: só na primeira abertura do jogo — logo grande, "BOA VISTA · RORAIMA" e "PRESSIONE QUALQUER TECLA" (teclado, mouse,
//    toque ou controle). Nunca aparece nos testes (-fb*, -batchmode).
//  - PÁGINAS: mesma barra de topo (voltar com ESC, título com acento, dinheiro); ESC volta ao INÍCIO.
//  - POSE: FBLobbyDirector.Pose (padrão escolhido pelo laboratório -fbposelab; -fbpose <clipe> testa outra sem recompilar).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FreeBala
{
    public partial class FBMenu
    {
        // paleta da logo oficial: branco, amarelo da estrela, azul e verde da bandeira de Roraima, laranja do buriti
        static readonly Color Yel = new(.98f, .77f, .1f), YelDark = new(.12f, .07f, 0), Ink = new(.95f, .95f, .96f), InkDim = new(.95f, .95f, .96f, .6f),
            InkFaint = new(.95f, .95f, .96f, .34f), RRBlue = new(.2f, .47f, .88f), RRGreen = new(.13f, .55f, .22f), Buriti = new(.9f, .48f, .16f),
            GlassC = new(.035f, .045f, .06f, .74f), Stroke = new(1, 1, 1, .1f);
        static readonly Vector3 CamHome = new(0, 1.18f, -6.3f);      // páginas com o personagem no centro (emotes): enquadramento antigo
        static readonly Vector3 CamHero = new(0, .93f, -5.2f);       // INÍCIO: mais perto, o personagem enche ~70 % da altura
        static readonly Vector3 HeroPos = new(.72f, 0, 0);           // terço direito da tela
        const float HeroYaw = 214, HeroFov = 27, PageFov = 30;
        bool atHome; float userYaw; FBLobbyStage stageFx; Transform lightRig; CanvasGroup homeGroup; static bool titleShown;

        // ------------------------------------------------------------ palco
        void BuildStage()
        {
            FBWeather.ResetGlobals(); FBVolumetric.Clear();   // nada do clima da partida no palco (voltar da partida deixava contorno nos personagens)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight; RenderSettings.ambientSkyColor = new Color(.4f, .43f, .5f); RenderSettings.ambientEquatorColor = new Color(.27f, .25f, .25f); RenderSettings.ambientGroundColor = new Color(.13f, .1f, .07f); RenderSettings.fog = false;
            if (cam) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.02f, .025f, .035f); }
            var bgc = new GameObject("LobbyFundo"); var c = bgc.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 60; c.sortingOrder = -100;
            var sc = bgc.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1600, 900); sc.matchWidthOrHeight = .5f;
            var bg = new GameObject("foto", typeof(RectTransform), typeof(RawImage)); bg.transform.SetParent(bgc.transform, false); var bi = bg.GetComponent<RawImage>(); bi.texture = T("lobby_bg2") ?? T("lobby_bg"); bi.raycastTarget = false;
            var fit = bg.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio = 16f / 9;
            stageFx = bgc.AddComponent<FBLobbyStage>(); stageFx.cam = cam; stageFx.Build(T("glow"), T("beam"), T("floor_glow"));
            var key = new GameObject("LuzChave").AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.15f; key.color = new Color(1, .95f, .9f); key.transform.rotation = Quaternion.Euler(22, 32, 0); key.shadows = LightShadows.None;
            lightRig = new GameObject("PalcoLuzes").transform;
            Light Spot(string n, Vector3 at, Color col, float inten)
            {
                var l = new GameObject(n).AddComponent<Light>(); l.type = LightType.Spot; l.range = 12; l.spotAngle = 55; l.intensity = inten; l.color = col;
                l.transform.SetParent(lightRig, false); l.transform.localPosition = at; l.transform.localRotation = Quaternion.LookRotation(new Vector3(0, 1.1f, 0) - at); return l;
            }
            Spot("RecorteAzul", new Vector3(-2.0f, 3.0f, 2.3f), new Color(.38f, .58f, 1f), 30);
            Spot("RecorteQuente", new Vector3(2.1f, 2.7f, 2.0f), new Color(1, .56f, .22f), 26);
            var fill = new GameObject("Preenchimento").AddComponent<Light>(); fill.type = LightType.Point; fill.range = 9; fill.intensity = 2.6f; fill.color = new Color(1, .86f, .7f);
            fill.transform.SetParent(lightRig, false); fill.transform.localPosition = new Vector3(-.4f, .6f, -2.2f);
        }
        void StageCam(bool hero)
        {
            if (!cam) return; cam.fieldOfView = hero ? HeroFov : PageFov;
            cam.transform.SetPositionAndRotation(hero ? CamHero : CamHome, hero ? Quaternion.Euler(-1, 0, 0) : Quaternion.identity);
        }
        void Preview(bool withWeapon = true, string dance = null)
        {
            if (preview) Destroy(preview); var prefab = FBRegistry.I ? FBRegistry.I.Char(FBSession.skin) : null; if (!prefab) return;
            preview = Instantiate(prefab, atHome ? HeroPos : Vector3.zero, Quaternion.Euler(0, atHome ? HeroYaw : 180, 0)); preview.name = "Previa";
            FBWardrobe.ApplyTo(preview, FBSession.look);
            var ch = preview.GetComponent<FBCharacter>(); ch.relaxed = true; ch.isPlayer = false; ch.skinId = FBSession.skin;   // r43: sem o id o IsFemale dava sempre "homem" (dança/pose por sexo)
            if (withWeapon && GameData.Weapons.TryGetValue(FBSession.primary, out var d)) { ch.SetWeapon(d, FBRegistry.I.Weapon(d.id)); }   // vitrine: só a arma na mão
            var ld = preview.AddComponent<FBLobbyDirector>(); ld.cam = cam;
            if (dance != null) { ld.home = false; ch.PlayDance(dance); }
        }

        // ------------------------------------------------------------ ajudantes de layout (âncora + pivô explícitos)
        static RectTransform R(Transform p, string n, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        { var r = FBUI.Rect(p, n, anchor, anchor, pos, size); r.pivot = pivot; r.anchoredPosition = pos; return r; }
        static Text Tx(Transform p, string s, int size, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box, TextAnchor al, bool title, Color c)
        {
            var t = FBUI.Label(p, s, size, anchor, anchor, pos, box, al, title, c); t.rectTransform.pivot = pivot; t.rectTransform.anchoredPosition = pos;
            if (!title) t.font = FBKit.BodyFont; return t;
        }
        static Image Bx(Transform p, string n, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color c, int rad = 0)
        { var im = R(p, n, anchor, pivot, pos, size).gameObject.AddComponent<Image>(); im.color = c; im.raycastTarget = false; if (rad > 0) FBKit.Round(im, rad); return im; }
        static RawImage Pic(Transform p, string n, Texture tex, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color? c = null)
        { var ri = R(p, n, anchor, pivot, pos, size).gameObject.AddComponent<RawImage>(); ri.texture = tex; ri.raycastTarget = false; ri.color = c ?? Color.white; return ri; }
        static Image Glass(Transform p, string n, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, int rad = 10, Color? c = null)
        { var im = Bx(p, n, anchor, pivot, pos, size, c ?? GlassC, rad); var o = im.gameObject.AddComponent<Outline>(); o.effectColor = Stroke; o.effectDistance = new Vector2(1, -1); return im; }
        static Button Click(Graphic g, System.Action a, float hover = .06f, float grow = 1.02f)
        {
            g.raycastTarget = true; var b = g.gameObject.AddComponent<Button>(); b.targetGraphic = g; b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { FBAudio.Synth("hover"); a?.Invoke(); }); FBHover.Add(g, hover, grow); return b;
        }
        static void Cover(RawImage ri, float w, float h)   // preenche sem distorcer
        {
            var tex = ri.texture; if (!tex) { ri.color = new Color(.1f, .12f, .15f); return; }
            float ar = (float)tex.width / tex.height, want = w / h; ri.uvRect = ar > want ? new Rect((1 - want / ar) / 2, 0, want / ar, 1) : new Rect(0, (1 - ar / want) / 2, 1, ar / want);
        }
        static void Shade(Transform p, Vector2 aMin, Vector2 aMax, float alpha, bool flip = false, bool horizontal = false)
        {
            var g = FBUI.Rect(p, "degrade", aMin, aMax, Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>(); g.raycastTarget = false; g.color = new Color(1, 1, 1, alpha);
            g.texture = horizontal ? FBLobbyStage.GradH : FBKit.GradTex; g.uvRect = flip ? (horizontal ? new Rect(1, 0, -1, 1) : new Rect(0, 1, 1, -1)) : new Rect(0, 0, 1, 1);
            g.rectTransform.offsetMin = g.rectTransform.offsetMax = Vector2.zero;
        }
        // faixa com as cores da bandeira de Roraima (azul · branco · verde) — assinatura visual sob a logo e no topo das páginas
        static void FlagTick(Transform p, Vector2 anchor, Vector2 pivot, Vector2 pos, float w, float h = 2, float a = .9f)
        {
            var r = R(p, "bandeira", anchor, pivot, pos, new Vector2(w, h)); float s = w / 3;
            Bx(r, "azul", new Vector2(0, .5f), new Vector2(0, .5f), Vector2.zero, new Vector2(s, h), new Color(RRBlue.r, RRBlue.g, RRBlue.b, a));
            Bx(r, "branco", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(s, 0), new Vector2(s, h), new Color(1, 1, 1, a));
            Bx(r, "verde", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(2 * s, 0), new Vector2(s, h), new Color(RRGreen.r, RRGreen.g, RRGreen.b, a));
        }
        static string Money => FBSession.money.ToString("N0", new System.Globalization.CultureInfo("pt-BR"));
        static int Level(out float k, out int into, out int need)
        {
            int lvl = 1 + (int)Mathf.Sqrt(FBSession.xp / 120f); float x0 = 120f * (lvl - 1) * (lvl - 1), x1 = 120f * lvl * lvl;
            k = Mathf.Clamp01((FBSession.xp - x0) / Mathf.Max(1, x1 - x0)); into = FBSession.xp - (int)x0; need = (int)(x1 - x0); return lvl;
        }
        void MoneyPill(Transform p, Vector2 anchor, Vector2 pos)   // pivô à direita
        {
            var cur = Glass(p, "dinheiro", anchor, new Vector2(1, .5f), pos, new Vector2(156, 42), 21);
            Pic(cur.transform, "cash", T("cash"), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(10, 0), new Vector2(28, 28));
            moneyT = Tx(cur.transform, Money, 19, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(46, 1), new Vector2(102, 30), TextAnchor.MiddleLeft, true, Ink);
        }

        // ------------------------------------------------------------ páginas: barra de topo comum
        void Clear() { foreach (Transform t in root) Destroy(t.gameObject); if (wpPreview) Destroy(wpPreview); }
        Transform Page(string title, bool dimStage = false)
        {
            Clear(); atHome = false; StageCam(false); userYaw = 0;
            if (preview) { preview.transform.position = Vector3.zero; preview.SetActive(true); }   // páginas que não usam o personagem escondem depois
            var p = FBUI.Panel(root, "pagina", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, dimStage ? FBKit.Bg : new Color(0, 0, 0, 0)).transform; FBFadeIn.Add(p, 0, 10);
            var top = FBUI.Panel(p, "topo", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -32), new Vector2(0, 64), new Color(.015f, .02f, .03f, .94f)).transform;
            Bx(top, "fio", new Vector2(.5f, 0), new Vector2(.5f, 0), Vector2.zero, new Vector2(4000, 1), Stroke);
            FlagTick(top, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), 240, 2, .85f);
            var bk = Glass(top, "voltar", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(24, 0), new Vector2(132, 38), 19, new Color(1, 1, 1, .05f)); Click(bk, Home, .08f, 1.03f);
            Tx(bk.transform, "‹  VOLTAR", 14, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(16, 0), new Vector2(80, 30), TextAnchor.MiddleLeft, true, Ink);
            var esc = Bx(bk.transform, "esc", new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-8, 0), new Vector2(30, 18), new Color(1, 1, 1, .1f), 4);
            Tx(esc.transform, "ESC", 9, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(30, 18), TextAnchor.MiddleCenter, true, InkDim);
            Bx(top, "acento", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(182, 0), new Vector2(4, 22), Yel, 2);
            Tx(top, title, 22, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(196, 1), new Vector2(700, 40), TextAnchor.MiddleLeft, true, Ink);
            MoneyPill(top, new Vector2(1, .5f), new Vector2(-24, 0));
            Pic(top, "logo", T("logo"), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-24 - 156 - 22, 0), new Vector2(150, 26), new Color(1, 1, 1, .85f));
            return p;
        }

        // ------------------------------------------------------------ INÍCIO
        void Home()
        {
            Clear(); atHome = true; userYaw = 0; StageCam(true); Preview();
            var p = (Transform)FBUI.Rect(root, "inicio", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); FBFadeIn.Add(p, 0, 8);
            homeGroup = p.GetComponent<CanvasGroup>(); if (!homeGroup) homeGroup = p.gameObject.AddComponent<CanvasGroup>();
            // girar o personagem: arrastar em qualquer área livre (fica atrás de todo o resto)
            {
                var drag = FBUI.Panel(p, "girar", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0)); drag.raycastTarget = true;
                var et = drag.gameObject.AddComponent<EventTrigger>(); var de = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
                de.callback.AddListener(e => userYaw -= ((PointerEventData)e).delta.x * .35f); et.triggers.Add(de);
            }
            // leitura: escurece a esquerda (bloco do modo), o topo e a base — o centro/direita fica limpo para o personagem
            Shade(p, Vector2.zero, new Vector2(.55f, 1), .78f, false, true);
            Shade(p, new Vector2(0, .8f), Vector2.one, .7f, true);
            Shade(p, Vector2.zero, new Vector2(1, .3f), .55f);
            HomeTopBar(p); HomeHero(p); HomeRight(p); HomeCharTag(p);
            // rodapé: versão
            bool dev = Debug.isDebugBuild;
            Tx(p, $"BURITIRO  v{Application.version}{(dev ? "  ·  BUILD DE DESENVOLVIMENTO" : "  ·  BETA")}", 10, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36, 30), new Vector2(520, 16), TextAnchor.MiddleRight, true, InkFaint);
            if (!FBSession.nameChosen && !askedName && !TestRun) { askedName = true; NameDialog(); }
        }
        static bool TestRun => System.Environment.GetCommandLineArgs().Any(a => a == "-batchmode" || a.StartsWith("-fb"));

        void HomeTopBar(Transform p)
        {
            Pic(p, "logo", T("logo"), new Vector2(0, 1), new Vector2(0, .5f), new Vector2(36, -44), new Vector2(288, 50));
            FlagTick(p, new Vector2(0, 1), new Vector2(0, .5f), new Vector2(40, -80), 96, 3);
            // abas
            var tabs = new (string l, System.Action a)[] { ("JOGAR", null), ("ARSENAL", Arsenal), ("PERSONAGEM", Characters), ("EMOTES", Emotes), ("PASSE", Pass) };
            float x = 36 + 288 + 44; int pend = FBPass.Claimable();
            foreach (var (l, a) in tabs)
            {
                bool on = a == null; var t = Tx(p, l, 16, new Vector2(0, 1), new Vector2(0, .5f), new Vector2(x, -44), new Vector2(200, 30), TextAnchor.MiddleLeft, true, on ? Ink : InkDim);
                float w = t.preferredWidth; t.rectTransform.sizeDelta = new Vector2(w + 2, 30);
                var hit = Bx(p, "aba_" + l, new Vector2(0, 1), new Vector2(0, .5f), new Vector2(x - 12, -44), new Vector2(w + 24, 48), new Color(1, 1, 1, 0), 8);
                hit.transform.SetSiblingIndex(t.transform.GetSiblingIndex());
                var ul = Bx(p, "sublinhado", new Vector2(0, 1), new Vector2(0, .5f), new Vector2(x, -64), new Vector2(w, 3), Yel, 1); ul.color = on ? Yel : new Color(Yel.r, Yel.g, Yel.b, 0);
                if (a != null) { var aa = a; var hv = Click(hit, aa, .04f, 1).GetComponent<FBHover>(); hv.onHover = h => { t.color = h ? Ink : InkDim; ul.color = new Color(Yel.r, Yel.g, Yel.b, h ? .6f : 0); }; }
                if (l == "PASSE" && pend > 0)
                {
                    var b = Bx(p, "selo", new Vector2(0, 1), new Vector2(0, .5f), new Vector2(x + w + 4, -34), new Vector2(18, 18), FBKit.Red, 9);
                    Tx(b.transform, pend.ToString(), 10, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(18, 18), TextAnchor.MiddleCenter, true, Color.white);
                }
                x += w + 40;
            }
            // direita: perfil, atalhos e dinheiro
            int lvl = Level(out float k, out int into, out int need);
            var prof = Glass(p, "perfil", new Vector2(1, 1), new Vector2(1, .5f), new Vector2(-36, -44), new Vector2(232, 52), 12);
            var ring = Bx(prof.transform, "aro", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(5, 0), new Vector2(42, 42), Yel, 10);
            var mask = Bx(ring.transform, "recorte", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(38, 38), Color.white, 8); mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var avt = FBCustomizeThumbs.Get(FBSession.skin) ?? T("avatar");
            var av = Pic(mask.transform, "avatar", avt, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(38, 38)); av.uvRect = avt == T("avatar") ? new Rect(0, .25f, 1, .75f) : new Rect(0, 0, 1, 1);
            nameT = Tx(prof.transform, (FBSession.name ?? "JOGADOR").ToUpper(), 14, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(56, 9), new Vector2(150, 20), TextAnchor.MiddleLeft, true, Ink);
            Tx(prof.transform, $"NÍVEL {lvl}", 10, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(56, -7), new Vector2(60, 14), TextAnchor.MiddleLeft, true, Yel);
            var xb = Bx(prof.transform, "xp", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(112, -7), new Vector2(104, 3), new Color(1, 1, 1, .14f), 1);
            Bx(xb.transform, "f", new Vector2(0, .5f), new Vector2(0, .5f), Vector2.zero, new Vector2(104 * k, 3), Yel, 1);
            Pic(prof.transform, "editar", T("ic_edit"), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-10, 8), new Vector2(13, 13), InkFaint);
            Click(prof, NameDialog, .05f, 1.02f);
            float ix = -36 - 232 - 12;
            foreach (var (ic, a) in new (string, System.Action)[] { ("ic_gear", () => FBSettings.Open(root)), ("ic_keyboard", Controls), ("ic_users", Online) })
            {
                var b = Glass(p, "atalho_" + ic, new Vector2(1, 1), new Vector2(1, .5f), new Vector2(ix, -44), new Vector2(44, 44), 12);
                var icon = Pic(b.transform, "ic", T(ic), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(20, 20), InkDim);
                var hv = Click(b, a, .08f, 1.06f).GetComponent<FBHover>(); hv.onHover = h => icon.color = h ? Yel : InkDim;
                ix -= 44 + 8;
            }
            MoneyPill(p, new Vector2(1, 1), new Vector2(ix - 4, -44));
        }

        void HomeHero(Transform p)
        {
            var M = MODES.FirstOrDefault(m => m.id == FBSession.mode); if (M.id == null) M = MODES[0];
            var mp = GameData.Maps.TryGetValue(FBSession.map, out var mm) ? mm.name : FBSession.map; var wx = FBSession.WeatherFor(FBSession.map);
            var a = new Vector2(0, 0); const float X = 48, W = 420;
            // JOGAR
            var play = Bx(p, "jogar", a, a, new Vector2(X, 44), new Vector2(W, 84), Yel, 8); play.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            { var sh = play.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(1, .7f, 0, .28f); sh.effectDistance = new Vector2(0, -5); }
            var arrowBox = Bx(play.transform, "seta_fundo", new Vector2(1, .5f), new Vector2(1, .5f), Vector2.zero, new Vector2(84, 84), new Color(0, 0, 0, .12f));
            var arrow = R(arrowBox.transform, "seta", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(3, 0), new Vector2(30, 30)).gameObject.AddComponent<Image>(); arrow.sprite = FBUI.ArrowSprite; arrow.color = YelDark; arrow.raycastTarget = false; arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -90);
            var pt = Tx(play.transform, "JOGAR", 40, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(26, 9), new Vector2(300, 48), TextAnchor.MiddleLeft, true, YelDark); pt.fontStyle = FontStyle.Italic;
            Tx(play.transform, $"{M.name}  ·  {mp.ToUpper()}", 11, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(28, -22), new Vector2(300, 16), TextAnchor.MiddleLeft, true, new Color(YelDark.r, YelDark.g, YelDark.b, .72f));
            { var band = FBUI.Rect(play.transform, "brilho", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(60, 220)); band.localRotation = Quaternion.Euler(0, 0, -22); var bi = band.gameObject.AddComponent<RawImage>(); bi.texture = GlintTex; bi.color = new Color(1, 1, .92f, .55f); bi.raycastTarget = false;
              var gl = play.gameObject.AddComponent<FBGlint>(); gl.band = band; gl.width = W; }
            Click(play, Play, .12f, 1.025f);
            // ALTERAR PARTIDA e AMIGOS
            var chg = Glass(p, "alterar", a, a, new Vector2(X, 140), new Vector2(W - 56 - 8, 46), 10);
            Tx(chg.transform, "ALTERAR PARTIDA", 14, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(18, 0), new Vector2(240, 26), TextAnchor.MiddleLeft, true, Ink);
            Tx(chg.transform, "MODO  ·  MAPA  ·  CLIMA  ›", 10, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-16, 0), new Vector2(170, 20), TextAnchor.MiddleRight, true, Yel);
            Click(chg, MatchPicker, .07f, 1.015f);
            var fr = Glass(p, "amigos", a, a, new Vector2(X + W - 56, 140), new Vector2(56, 46), 10);
            var fi = Pic(fr.transform, "ic", T("ic_users"), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(20, 20), InkDim);
            Click(fr, Online, .08f, 1.05f).GetComponent<FBHover>().onHover = h => fi.color = h ? Yel : InkDim;
            // condições
            float cx = X; foreach (var s in new[] { DIFFS[Mathf.Clamp(FBSession.diff, 0, 2)], wx.time == "noite" ? "NOITE" : wx.time == "anoitecer" ? "ANOITECER" : "DIA", wx.weather == "chuva" ? "CHUVA" : "TEMPO LIMPO" })
            {
                var c = Bx(p, "cond", a, a, new Vector2(cx, 206), new Vector2(10, 24), new Color(1, 1, 1, .06f), 12); var o = c.gameObject.AddComponent<Outline>(); o.effectColor = Stroke; o.effectDistance = new Vector2(1, -1);
                var ct = Tx(c.transform, s, 10, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(12, 0), new Vector2(160, 18), TextAnchor.MiddleLeft, true, InkDim);
                float w = ct.preferredWidth + 24; c.rectTransform.sizeDelta = new Vector2(w, 24); cx += w + 6;
            }
            // mapa: miniatura + nome + descrição
            var th = Bx(p, "mapa_mini", a, a, new Vector2(X, 244), new Vector2(112, 63), new Color(1, 1, 1, .1f), 6); th.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var shot = Pic(th.transform, "foto", Resources.Load<Texture2D>("MapShots/" + FBSession.map), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(112, 63)); Cover(shot, 112, 63);
            mapT = Tx(p, (mp ?? "").ToUpper(), 20, a, a, new Vector2(X + 126, 280), new Vector2(W - 126, 26), TextAnchor.MiddleLeft, true, Ink);
            Tx(p, MAP_INFO.TryGetValue(FBSession.map, out var info) ? info : "", 12, a, a, new Vector2(X + 126, 248), new Vector2(W - 100, 34), TextAnchor.UpperLeft, false, InkDim);
            // modo em destaque
            modeT = Tx(p, M.name, 76, a, a, new Vector2(X - 4, 316), new Vector2(760, 92), TextAnchor.LowerLeft, true, Ink); modeT.fontStyle = FontStyle.Italic;
            { var s = modeT.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0, 0, 0, .5f); s.effectDistance = new Vector2(0, -4); }
            Bx(p, "acento", a, a, new Vector2(X, 414), new Vector2(4, 16), Yel, 2);
            Tx(p, M.tag, 14, a, a, new Vector2(X + 14, 412), new Vector2(400, 20), TextAnchor.MiddleLeft, true, Yel);
            Tx(p, "MODO DE JOGO", 11, a, a, new Vector2(X, 440), new Vector2(300, 16), TextAnchor.MiddleLeft, true, InkFaint);
            // o bloco inteiro (modo + mapa) abre o seletor
            var hit = Bx(p, "bloco_modo", a, a, new Vector2(X - 12, 236), new Vector2(W + 24, 228), new Color(1, 1, 1, 0), 12); hit.transform.SetSiblingIndex(th.transform.GetSiblingIndex()); Click(hit, MatchPicker, .035f, 1);
        }

        void HomeRight(Transform p)
        {
            var a = new Vector2(1, 1); const float X = -36, W = 340;
            // NOVIDADES
            var card = Glass(p, "novidades", a, a, new Vector2(X, -96), new Vector2(W, 196), 12, new Color(.03f, .035f, .045f, .9f)); card.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var news = card.gameObject.AddComponent<FBNews>();
            var slides = new List<FBNews.Slide>();
            int tier = FBPass.Tier, pend = FBPass.Claimable();
            slides.Add(new FBNews.Slide { tex = Resources.Load<Texture2D>("Modes/squad"), tag = "PASSE DE BATALHA", title = $"TEMPORADA 1 · NÍVEL {tier}", sub = pend > 0 ? $"{pend} prêmio{(pend > 1 ? "s" : "")} esperando para resgatar" : "Jogue partidas para liberar os 30 níveis", act = Pass });
            if (maps.Any(m => m.id == "morro_sacola")) slides.Add(new FBNews.Slide { tex = Resources.Load<Texture2D>("MapShots/morro_sacola"), tag = "NOVO MAPA", title = "MORRO DA SACOLA", sub = "Ruas de terra, muro pré-moldado e quintais", act = () => { FBSession.map = "morro_sacola"; FBSession.Save(); Home(); } });
            if (MODES.Any(m => m.id == "10x10")) slides.Add(new FBNews.Slide { tex = Resources.Load<Texture2D>("MapShots/praca_aguas"), tag = "MODO EM DESTAQUE", title = "10X10 POR RODADAS", sub = "Compre no começo da rodada · primeiro a 7 vitórias", act = () => { FBSession.mode = "10x10"; FBSession.Save(); Home(); } });
            slides.Add(new FBNews.Slide { tex = Resources.Load<Texture2D>("Modes/role"), tag = "MODO LIVRE", title = "DE ROLÊ", sub = "Carros, paredão de som e a cidade inteira", act = () => { FBSession.mode = "role"; FBSession.Save(); Home(); } });
            slides.Add(new FBNews.Slide { tex = Resources.Load<Texture2D>("Modes/classico"), tag = "ARSENAL", title = "MONTE SUA ARMA", sub = "Miras, canos, carregadores, lasers e empunhaduras", act = Arsenal });
            news.Build(slides, W, 196);
            Tx(p, "NOVIDADES", 11, a, new Vector2(1, 0), new Vector2(X, -92), new Vector2(200, 16), TextAnchor.MiddleRight, true, InkFaint);
            // PASSE
            var pc = Glass(p, "passe", a, a, new Vector2(X, -304), new Vector2(W, 78), 12);
            int into = FBSession.passXp % FBPass.PER_TIER;
            Tx(pc.transform, "PASSE DE BATALHA", 10, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -12), new Vector2(200, 14), TextAnchor.MiddleLeft, true, InkFaint);
            Tx(pc.transform, $"NÍVEL {tier}", 22, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -26), new Vector2(200, 28), TextAnchor.MiddleLeft, true, Ink);
            Tx(pc.transform, $"{into} / {FBPass.PER_TIER} XP", 10, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -34), new Vector2(160, 14), TextAnchor.MiddleRight, true, InkDim);
            if (pend > 0) { var tg = Bx(pc.transform, "premios", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -10), new Vector2(92, 18), FBKit.Red, 9); Tx(tg.transform, $"{pend} PRÊMIO{(pend > 1 ? "S" : "")}", 9, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(92, 18), TextAnchor.MiddleCenter, true, Color.white); }
            var bar = Bx(pc.transform, "barra", new Vector2(0, 0), new Vector2(0, 0), new Vector2(16, 12), new Vector2(W - 32, 4), new Color(1, 1, 1, .1f), 2);
            Bx(bar.transform, "f", new Vector2(0, .5f), new Vector2(0, .5f), Vector2.zero, new Vector2((W - 32) * into / (float)FBPass.PER_TIER, 4), Yel, 2);
            Click(pc, Pass, .06f, 1.015f);
        }

        void HomeCharTag(Transform p)
        {
            float vx = .66f; if (cam) { var v = cam.WorldToViewportPoint(HeroPos); if (v.z > 0) vx = v.x; }
            var sk = FBCustomizeData.Find(FBSession.skin)?.def; var sex = sk?.sex == "f" ? "FEMININO" : "MASCULINO";
            var box = FBUI.Rect(p, "personagem", new Vector2(vx, 0), new Vector2(vx, 0), new Vector2(0, 62), new Vector2(380, 74));
            var nm = Tx(box, (sk?.name ?? FBSession.skin).ToUpper(), 30, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, 0), new Vector2(380, 38), TextAnchor.MiddleCenter, true, Ink);
            { var s = nm.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0, 0, 0, .6f); s.effectDistance = new Vector2(0, -3); }
            var chip = Bx(box, "trocar", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 4), new Vector2(10, 26), new Color(0, 0, 0, .45f), 13);
            { var o = chip.gameObject.AddComponent<Outline>(); o.effectColor = new Color(Yel.r, Yel.g, Yel.b, .45f); o.effectDistance = new Vector2(1, -1); }
            var ct = Tx(chip.transform, (FBSession.pickRandom ? $"ALEATÓRIO · {sex}" : "PERSONAGEM FIXO") + "   ·   TROCAR  ›", 11, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(360, 20), TextAnchor.MiddleCenter, true, Yel);
            chip.rectTransform.sizeDelta = new Vector2(ct.preferredWidth + 36, 26);
            Click(chip, Characters, .08f, 1.04f);
            Tx(p, "ARRASTE PARA GIRAR", 9, new Vector2(vx, 0), new Vector2(.5f, 0), new Vector2(0, 8), new Vector2(200, 14), TextAnchor.MiddleCenter, true, InkFaint);
        }

        // ------------------------------------------------------------ TÍTULO (primeira abertura)
        bool WantTitle() { if (titleShown || TestRun) return false; titleShown = true; return true; }
        void Title()
        {
            if (homeGroup) { var ff = homeGroup.GetComponent<FBFadeIn>(); if (ff) DestroyImmediate(ff); homeGroup.alpha = 0; homeGroup.blocksRaycasts = false; }
            var ov = FBUI.Rect(root, "titulo", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); var g = ov.gameObject.AddComponent<CanvasGroup>();
            var veil = FBUI.Panel(ov, "veu", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, .25f)); veil.raycastTarget = true;
            Shade(ov, Vector2.zero, new Vector2(.62f, 1), .9f, false, true); Shade(ov, Vector2.zero, new Vector2(1, .35f), .8f);
            var lg = Pic(ov, "logo", T("logo"), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(84, 70), new Vector2(760, 132)); FBFadeIn.Add(lg, .15f, 24).dur = .6f;
            var tl = Tx(ov, "B O A   V I S T A    ·    R O R A I M A", 15, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(96, -24), new Vector2(700, 22), TextAnchor.MiddleLeft, true, InkDim); FBFadeIn.Add(tl, .45f, 10).dur = .5f;
            FlagTick(ov, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(98, -50), 150, 3);
            var press = Tx(ov, "PRESSIONE QUALQUER TECLA", 17, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 92), new Vector2(600, 26), TextAnchor.MiddleCenter, true, Ink);
            press.gameObject.AddComponent<FBPulse>();
            Tx(ov, $"v{Application.version}", 10, new Vector2(0, 0), new Vector2(0, 0), new Vector2(36, 30), new Vector2(200, 16), TextAnchor.MiddleLeft, true, InkFaint);
            Tx(ov, "© 2026 BURITIRO", 10, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36, 30), new Vector2(200, 16), TextAnchor.MiddleRight, true, InkFaint);
            var w = ov.gameObject.AddComponent<FBTitleWait>(); var hg = homeGroup;
            w.done = () => { FBAudio.Synth("hover"); if (hg) { hg.blocksRaycasts = true; var f = hg.gameObject.AddComponent<FBFadeIn>(); f.dur = .45f; f.dy = 10; } };
        }

        // ------------------------------------------------------------ laboratório de pose (-fbposelab) → <build>/autoteste/poses/<skin>__<clipe>.png
        System.Collections.IEnumerator PoseShots()
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste", "poses"); System.IO.Directory.CreateDirectory(dir);
            yield return new WaitForSeconds(2.5f); FBLobbyDirector.Quiet = true; var keepSkin = FBSession.skin; var keepPose = FBLobbyDirector.Pose;
            var clips = new[] { "ANIM-Rifle_Idle", "Rifle Idle", "IE_Rifle_Idle", "Rifle_Idle_GunDown", "NPC_Hold_Gun_Idle", "X171_PosePolicial_root_Pose3", "X171_PosePolicial_root_Pose5", "X171_PosePolicial_root_Pose7", "IE_Rifle_AimPose", "NPC_MuzDn_End_Pose", "X171_Idle_Male", "X171_PoseBaseHom" };
            foreach (var skin in new[] { "r_tutu", "r_elaine" })
            {
                if (!FBRegistry.I.HasChar(skin)) continue; FBSession.skin = skin;
                foreach (var c in clips)
                {
                    if (c != "Rifle Idle" && !FBRegistry.I.Clip(c)) { Debug.Log($"[FreeBala][pose] {c}: fora do registro"); continue; }
                    FBLobbyDirector.Pose = c; Home(); yield return new WaitForSeconds(3.2f);
                    yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{skin}__{c}.png")); yield return null; yield return null;
                    var ch = preview ? preview.GetComponent<FBCharacter>() : null; Debug.Log($"[FreeBala][pose] {skin} {c}: override={(ch ? ch.OverrideOf("Rifle Idle") : "-")}");
                }
            }
            FBSession.skin = keepSkin; FBLobbyDirector.Pose = keepPose; FBLobbyDirector.Quiet = false; Debug.Log("[FreeBala][pose] fim");
            yield return new WaitForSeconds(.5f); Application.Quit(0);
        }

        // ------------------------------------------------------------ quadro a quadro
        static readonly int __fbt_Update = FBTempo.Id("FBMenu.Update");   // [parceiro 01/11] FBTempo: mede o custo do Update sem o Profiler
        void Update() { if (!FBTempo.On) { Update__fbt(); return; } long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0); }
        void Update__fbt()
        {
            bool show = !Customize && preview && preview.activeSelf;
            if (show) preview.transform.rotation = Quaternion.Euler(0, (atHome ? HeroYaw + userYaw : 180) + Mathf.Sin(Time.time * .4f) * (atHome ? 3 : 8), 0);
            if (atHome && cam && !Customize) { float t = Time.time; cam.transform.position = CamHero + new Vector3(Mathf.Sin(t * .31f) * .03f, Mathf.Sin(t * .23f) * .018f, 0); }
            if (lightRig) lightRig.position = show ? preview.transform.position : Vector3.zero;
            if (stageFx) stageFx.target = show ? preview.transform : null;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !atHome && !Customize && !FBLoading.Busy && !root.GetComponentInChildren<FBTitleWait>()) { if (nameDlg) Destroy(nameDlg); else { if (preview) preview.SetActive(true); Home(); } }
        }
    }

    // efeitos do palco no canvas de fundo (atrás do personagem): brilhos que seguem o personagem na tela, fachos, poeira e luz de chão
    public class FBLobbyStage : MonoBehaviour
    {
        public Camera cam; public Transform target;
        RectTransform warm, cool, floorR, beams; RawImage warmI, coolI, floorI; RawImage[] beamI; CanvasGroup fx; Vector2 tgt = new(.66f, .5f);
        readonly List<(RectTransform r, RawImage i, float sp, float ph, float a)> dust = new();
        static Texture2D gradH;
        public static Texture2D GradH
        {
            get
            {
                if (gradH) return gradH; gradH = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int i = 0; i < 64; i++) gradH.SetPixel(i, 0, new Color(0, 0, 0, Mathf.Pow(1 - i / 63f, 1.4f) * .9f)); gradH.Apply(); return gradH;
            }
        }
        RawImage Img(Transform p, string n, Texture t, Vector2 size, Color c)
        {
            var r = FBUI.Rect(p, n, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, size); var i = r.gameObject.AddComponent<RawImage>(); i.texture = t; i.color = c; i.raycastTarget = false; return i;
        }
        public void Build(Texture glow, Texture beam, Texture floorGlow)
        {
            var layer = FBUI.Rect(transform, "efeitos", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); fx = layer.gameObject.AddComponent<CanvasGroup>(); fx.blocksRaycasts = false;
            coolI = Img(layer, "brilho_azul", glow, new Vector2(1000, 1000), new Color(.2f, .42f, .95f, .2f)); cool = coolI.rectTransform;
            warmI = Img(layer, "brilho_quente", glow, new Vector2(1150, 1050), new Color(.95f, .5f, .16f, .3f)); warm = warmI.rectTransform;
            beams = FBUI.Rect(layer, "fachos", new Vector2(.5f, 1), new Vector2(.5f, 1), Vector2.zero, new Vector2(10, 10));
            beamI = new RawImage[3];
            var cols = new[] { new Color(1, .78f, .5f, .11f), new Color(.55f, .7f, 1f, .08f), new Color(1, .9f, .75f, .06f) };
            var rots = new[] { 14f, -17f, 4f }; var offs = new[] { -170f, 210f, 40f };
            for (int i = 0; i < 3; i++)
            {
                var b = Img(beams, "facho", beam, new Vector2(i == 2 ? 300 : 420, 1150), cols[i]); var r = b.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(.5f, 1); r.pivot = new Vector2(.5f, 1); r.anchoredPosition = new Vector2(offs[i], 40); r.localRotation = Quaternion.Euler(0, 0, rots[i]); beamI[i] = b;
            }
            floorI = Img(layer, "luz_chao", floorGlow ? floorGlow : glow, new Vector2(760, 140), new Color(1, .82f, .6f, .85f)); floorR = floorI.rectTransform;
            var rnd = new System.Random(11);
            for (int i = 0; i < 46; i++)
            {
                float s = 2 + (float)rnd.NextDouble() * 4, a = .1f + (float)rnd.NextDouble() * .3f;
                var d = Img(layer, "poeira", glow, new Vector2(s, s), new Color(1, .9f, .75f, a)); var r = d.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2((float)rnd.NextDouble(), (float)rnd.NextDouble());
                dust.Add((r, d, .006f + (float)rnd.NextDouble() * .018f, (float)rnd.NextDouble() * 6.28f, a));
            }
        }
        void Update()
        {
            float dt = Time.unscaledDeltaTime, t = Time.unscaledTime;
            bool on = target && cam; fx.alpha = Mathf.MoveTowards(fx.alpha, on ? 1 : .35f, dt * 2.5f);
            if (on)
            {
                var vb = cam.WorldToViewportPoint(target.position + Vector3.up * 1.05f); var vf = cam.WorldToViewportPoint(target.position);
                if (vb.z > 0) tgt = Vector2.Lerp(tgt, new Vector2(vb.x, vb.y), 1 - Mathf.Exp(-8 * dt));
                if (vf.z > 0) { floorR.anchorMin = floorR.anchorMax = new Vector2(Mathf.Lerp(floorR.anchorMin.x, vf.x, 1 - Mathf.Exp(-8 * dt)), vf.y); floorR.anchoredPosition = new Vector2(0, 6); }
            }
            warm.anchorMin = warm.anchorMax = tgt + new Vector2(.03f, .02f); cool.anchorMin = cool.anchorMax = tgt + new Vector2(-.13f, .09f);
            beams.anchorMin = beams.anchorMax = new Vector2(tgt.x, 1); beams.anchoredPosition = Vector2.zero;
            warmI.color = new Color(.95f, .5f, .16f, .27f + Mathf.Sin(t * .7f) * .03f); coolI.color = new Color(.2f, .42f, .95f, .19f + Mathf.Sin(t * .5f + 1) * .03f);
            for (int i = 0; i < beamI.Length; i++) { var c = beamI[i].color; c.a = (i == 0 ? .11f : i == 1 ? .08f : .06f) * (.75f + .25f * Mathf.Sin(t * (.35f + i * .17f) + i * 2)); beamI[i].color = c; }
            foreach (var d in dust)
            {
                var a = d.r.anchorMin; a.y += d.sp * dt; a.x += Mathf.Sin(t * .5f + d.ph) * .004f * dt; if (a.y > 1.02f) { a.y = -.02f; a.x = Random.value; }
                d.r.anchorMin = d.r.anchorMax = a; var c = d.i.color; c.a = d.a * (.6f + .4f * Mathf.Sin(t * 1.3f + d.ph)) * Mathf.Clamp01(Mathf.Min(a.y, 1 - a.y) * 6); d.i.color = c;
            }
        }
    }

    // NOVIDADES: carrossel de cartões com foto (troca a cada 6 s, toque/clique leva ao destino, pontos escolhem o cartão)
    public class FBNews : MonoBehaviour
    {
        public class Slide { public Texture2D tex; public string tag, title, sub; public System.Action act; }
        List<Slide> slides; int cur; float t, w, h; RawImage img; Text tagT, titleT, subT; Image tagB; CanvasGroup content; Image[] dots; float fade = 1; int pending = -1;
        public void Build(List<Slide> s, float width, float height)
        {
            slides = s; w = width; h = height; var p = transform;
            img = FBUI.Rect(p, "foto", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>(); img.raycastTarget = false;
            var sh = FBUI.Rect(p, "sombra", Vector2.zero, new Vector2(1, .75f), Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>(); sh.texture = FBKit.GradTex; sh.raycastTarget = false; sh.rectTransform.offsetMin = sh.rectTransform.offsetMax = Vector2.zero;
            var cg = FBUI.Rect(p, "texto", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); content = cg.gameObject.AddComponent<CanvasGroup>(); content.blocksRaycasts = false;
            tagB = FBUI.Panel(cg, "tag", new Vector2(0, 1), new Vector2(0, 1), new Vector2(14 + 60, -14 - 10), new Vector2(120, 20), new Color(.98f, .77f, .1f)); FBKit.Pill(tagB); tagB.raycastTarget = false;
            tagB.rectTransform.pivot = new Vector2(0, .5f); tagB.rectTransform.anchoredPosition = new Vector2(14, -24);
            tagT = FBUI.Label(tagB.transform, "", 9, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, true, new Color(.12f, .07f, 0));
            titleT = FBUI.Label(cg, "", 20, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 48), new Vector2(-28, 28), TextAnchor.MiddleLeft, true, Color.white);
            subT = FBUI.Label(cg, "", 12, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 24), new Vector2(-28, 18), TextAnchor.MiddleLeft, false, new Color(1, 1, 1, .72f)); subT.font = FBKit.BodyFont;
            dots = new Image[slides.Count];
            for (int i = 0; i < slides.Count; i++)
            {
                int k = i; var d = FBUI.Panel(p, "ponto", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14 - (slides.Count - 1 - i) * 14 - 4, -24), new Vector2(8, 8), new Color(1, 1, 1, .3f)); FBKit.Pill(d); dots[i] = d;
                var b = d.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.onClick.AddListener(() => Go(k));
            }
            var bt = gameObject.AddComponent<Button>(); bt.transition = Selectable.Transition.None; bt.onClick.AddListener(() => { FBAudio.Synth("hover"); slides[cur].act?.Invoke(); });
            GetComponent<Image>().raycastTarget = true; FBHover.Add(GetComponent<Image>(), .05f, 1.01f);
            Show(0);
        }
        void Go(int i) { if (i == cur) return; pending = i; t = 0; }
        void Show(int i)
        {
            cur = i; var s = slides[i]; img.texture = s.tex; img.color = s.tex ? Color.white : new Color(.1f, .12f, .15f);
            if (s.tex) { float ar = (float)s.tex.width / s.tex.height, want = w / h; img.uvRect = ar > want ? new Rect((1 - want / ar) / 2, 0, want / ar, 1) : new Rect(0, (1 - ar / want) / 2, 1, ar / want); }
            tagT.text = s.tag; tagB.rectTransform.sizeDelta = new Vector2(tagT.preferredWidth + 20, 20); titleT.text = s.title; subT.text = s.sub;
            for (int k = 0; k < dots.Length; k++) { dots[k].color = k == i ? new Color(.98f, .77f, .1f) : new Color(1, 1, 1, .3f); dots[k].rectTransform.sizeDelta = new Vector2(k == i ? 18 : 8, 8); }
        }
        void Update()
        {
            float dt = Time.unscaledDeltaTime; t += dt;
            if (pending < 0 && t > 6 && slides.Count > 1) { pending = (cur + 1) % slides.Count; }
            if (pending >= 0) { fade -= dt * 5; if (fade <= 0) { Show(pending); pending = -1; fade = 0; t = 0; } }
            else if (fade < 1) fade = Mathf.Min(1, fade + dt * 4);
            float e = fade * fade * (3 - 2 * fade); content.alpha = e; img.color = new Color(img.color.r, img.color.g, img.color.b, .35f + .65f * e);
        }
    }

    // texto que pulsa (PRESSIONE QUALQUER TECLA)
    public class FBPulse : MonoBehaviour
    {
        Graphic g; void Awake() => g = GetComponent<Graphic>();
        void Update() { if (g) { var c = g.color; c.a = .45f + .55f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 3f)); g.color = c; } }
    }

    // tela de título: espera qualquer tecla/clique/toque/botão do controle, some com fade e devolve o INÍCIO
    public class FBTitleWait : MonoBehaviour
    {
        public System.Action done; float t; bool going; CanvasGroup g;
        void Awake() => g = GetComponent<CanvasGroup>();
        void Update()
        {
            t += Time.unscaledDeltaTime;
            if (going) { g.alpha = Mathf.Max(0, 1 - (t / .4f)); if (t >= .4f) Destroy(gameObject); return; }
            if (t < .6f) return;
            var kb = Keyboard.current; var ms = Mouse.current; var ts = Touchscreen.current; var gp = Gamepad.current;
            bool hit = (kb != null && kb.anyKey.wasPressedThisFrame) || (ms != null && (ms.leftButton.wasPressedThisFrame || ms.rightButton.wasPressedThisFrame))
                || (ts != null && ts.primaryTouch.press.wasPressedThisFrame) || (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame));
            if (!hit) return; going = true; t = 0; g.blocksRaycasts = false; done?.Invoke();
        }
    }
}
