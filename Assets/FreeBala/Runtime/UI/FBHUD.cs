// HUD da partida (porte do hud de webui.js, montado em uGUI por código): mira dinâmica (abre com a dispersão do recoil.js; some sem arma,

// correndo ou mirando de luneta), marcador de acerto (cabeça em vermelho) + dano flutuante, vida e colete/capacete, munição/reserva e

// arma, placar do modo (rótulo, A × B, tempo, barras e "pips" das rodadas), caixa de objetivo, kill feed, toast de evento, banner de

// abate (sequência), banner de equipe eliminada, contagem 3·2·1/VALENDO, killcam, cartões de morte/vitória, pausa, dinheiro da economia,

// aviso de zona com seta, prompt de ação (X pegar / F veículo), reanimar, kit médico, corrida automática, direção do dano, minimapa que

// gira com a visão (aliados sempre; inimigo só quando atira) e placar do Tab; nomes sobre a cabeça dos aliados.

using System.Collections.Generic;

using System.Linq;

using UnityEngine;

using UnityEngine.UI;



namespace FreeBala

{

    public class FBHUD : MonoBehaviour

    {

        static FBHUD inst; public static FBHUD I { get => inst ? inst : null; set => inst = value; }   // null de verdade depois de destruído (o "?." não enxerga o null falso do Unity)

        public static bool CardOpen;

        Transform root; Canvas canvas;

        Text hpT, armorT, ammoT, resT, wT, eyebrowT, leftT, centerMatchT, rightT, boxT, centerT, toastA, toastB, toastC, bannerA, bannerB, wipeA, wipeB, countT, killcamT, cashT, cashPlus, promptK, promptT, promptS, zoneT, reviveT, medT, runT, boardT;

        Image hpBar, armorBar, vignette, hitMark, barA, barB, toastBar, reviveBar, medBar, zoneArrow, dmgArrow, crossDot; RectTransform[] cross = new RectTransform[4]; Image[] pips = new Image[7];

        RectTransform toastRT, bannerRT, wipeRT, boxRT, promptRT, zoneRT, reviveRT, medRT, mini, miniPlayer, dmgRT, boardRT; Image miniBg;

        readonly List<(Text t, float ttl)> feed = new(); readonly List<(Text t, Vector3 p, float t0)> floats = new(); readonly List<Image> miniDots = new(); readonly List<Text> tags = new();

        float hitT, dmgT, centerTTL, toastT, bannerT, wipeT, countTT, killcamTT, cashPlusT, mtoastT, dmgDirT, scopeKick; Text mtoastT2;

        GameObject card; GameObject pausePanel;

        // HUD v2 do web (webui.js .h-root)

        Image hpLag, ring; Text fmT, medKey, ringT, ringS; RawImage wSil, medIc, miniMap; RectTransform magRT, ringRT, scA, scMid, scB, squadRT, miniClip, miniN, cone; Text squadCt; Image lineA, lineB;

        readonly List<Image> magPips = new(); readonly List<Image> miniRings = new(); readonly List<(RectTransform rt, Image em, Text num, Text nm, Image bar, Text dt, CanvasGroup cg)> sqRows = new();

        readonly List<(RectTransform rt, Image em, Text num, Text nm, RectTransform barBg, Image bar)> tagW = new(); readonly List<Image> miniArrows = new();

        Text blockX; Image impX; bool impOn; Vector2 impPx; Vector2 crossOff; float crossGap; bool crossVis, crossDotVis; float hpLagV = 100; int magN; string wId; Text nT; Image zoneRing, zoneNext, heliScanRing;

        static readonly Dictionary<FBCharacter, int> squadNo = new(); static FBMatch squadMatch;

        static readonly Color[] SQUAD_COL = { Hex(0xf5a623), Hex(0x3aa0ff), Hex(0xe86fb5), Hex(0x9b7bff), Hex(0x35c25a), Hex(0x26c6da), Hex(0xff7043), Hex(0xc0ca33) };

        static Color Hex(uint c, float a = 1) => new Color(((c >> 16) & 255) / 255f, ((c >> 8) & 255) / 255f, (c & 255) / 255f, a);



        void Awake()

        {

            I = this; canvas = FBUI.Canvas("HUD", 10, false); root = canvas.transform; CardOpen = false;

            FBDamageIndicators.Ensure(root);                                                // arcos de direção do dano (rodada 37)

            vignette = Img("dano", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(.6f, 0, 0, 0)); vignette.sprite = HurtEdge; vignette.raycastTarget = false;   // bordas, não a tela toda

            for (int i = 0; i < 4; i++) cross[i] = Img("mira" + i, C, C, Vector2.zero, i < 2 ? new Vector2(2, 11) : new Vector2(11, 2), Color.white).rectTransform;

            crossDot = Img("ponto", C, C, Vector2.zero, new Vector2(3, 3), Color.white);

            hitMark = Img("acerto", C, C, Vector2.zero, new Vector2(20, 20), new Color(1, 1, 1, 0)); hitMark.sprite = FBUI.HitSprite; hitMark.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);

            // vida: número + barra fina, sem painel (canto inferior esquerdo, 28 px / 26 px do web)

            hpT = T(root, "100", 28, Vector2.zero, new Vector2(0, .5f), 28, 40, 60, 32, TextAnchor.MiddleLeft, true, HPC);

            var hb = Box(root, "vidaBarra", Vector2.zero, new Vector2(0, .5f), 88, 40, 150, 4, HL); hpLag = Fill(hb.transform, new Color(1, 1, 1, .35f)); hpBar = Fill(hb.transform, HPC);

            var ab = Box(root, "colete", Vector2.zero, new Vector2(0, .5f), 88, 34, 150, 2, new Color(1, 1, 1, .08f)); armorBar = Fill(ab.transform, new Color(.4f, .7f, 1, .9f));

            armorT = T(root, "", 9, Vector2.zero, new Vector2(0, .5f), 88, 48, 220, 12, TextAnchor.MiddleLeft, true, HM);

            // kit médico: ícone + quantidade + tecla, na linha de cima

            medRT = R(root, "kit", Vector2.zero, new Vector2(0, .5f), 28, 73, 90, 18);

            medIc = Raw(medRT, "ic", new Vector2(0, .5f), new Vector2(0, .5f), 1, 0, 16, 16, Resources.Load<Texture2D>("UI/Hud/ic_med"), HW);

            medT = T(medRT, "2", 15, new Vector2(0, .5f), new Vector2(0, .5f), 24, 0, 20, 16, TextAnchor.MiddleLeft, true, HW);

            var kbx = Box(medRT, "tecla", new Vector2(0, .5f), new Vector2(0, .5f), 46, 0, 16, 16, new Color(1, 1, 1, .04f)); kbx.gameObject.AddComponent<Outline>().effectColor = HL;

            medKey = T(kbx.transform, "H", 9, new Vector2(.5f, .5f), new Vector2(.5f, .5f), 0, 0, 16, 16, TextAnchor.MiddleCenter, true, HM);

            medBar = Fill(Box(medRT, "mbg", new Vector2(0, .5f), new Vector2(0, .5f), 0, -12, 1, 1, Color.clear).transform, Color.clear);

            // anel do kit médico em uso (centro, 62% da altura)

            ringRT = R(root, "anelKit", new Vector2(.5f, .38f), new Vector2(.5f, .5f), 0, 0, 54, 54);

            var rbg = ringRT.gameObject.AddComponent<Image>(); rbg.sprite = DiscSprite; rbg.color = new Color(1, 1, 1, .14f); rbg.raycastTarget = false;

            ring = Box(ringRT, "p", new Vector2(.5f, .5f), new Vector2(.5f, .5f), 0, 0, 54, 54, new Color(1, 1, 1, .95f)); ring.sprite = DiscSprite; ring.type = Image.Type.Filled; ring.fillMethod = Image.FillMethod.Radial360; ring.fillOrigin = 2; ring.fillClockwise = true;

            var rin = Box(ringRT, "miolo", new Vector2(.5f, .5f), new Vector2(.5f, .5f), 0, 0, 46, 46, new Color(.03f, .05f, .055f, .86f)); rin.sprite = DiscSprite;

            Raw(ringRT, "ic", new Vector2(.5f, .5f), new Vector2(.5f, .5f), 0, 13, 11, 11, Resources.Load<Texture2D>("UI/Hud/ic_med"), HW);

            ringT = T(ringRT, "3.0", 15, new Vector2(.5f, .5f), new Vector2(.5f, .5f), 0, -3, 54, 18, TextAnchor.MiddleCenter, true, Color.white);

            T(ringRT, "USANDO KIT MÉDICO", 11, new Vector2(.5f, 0), new Vector2(.5f, 1), 0, -6, 300, 14, TextAnchor.MiddleCenter, true, Color.white);

            ringS = T(ringRT, "ANDE OU AGACHE · CORRER OU ATIRAR CANCELA", 9, new Vector2(.5f, 0), new Vector2(.5f, 1), 0, -23, 400, 12, TextAnchor.MiddleCenter, true, HM); ringRT.gameObject.SetActive(false);

            // arma: silhueta + nome + modo de tiro + munição + pente (canto inferior direito, 28 px / 24 px do web)

            var BR = new Vector2(1, 0);

            ammoT = T(root, "", 32, BR, new Vector2(1, .5f), -72, 47, 160, 34, TextAnchor.MiddleRight, true, HW);

            resT = T(root, "", 15, BR, new Vector2(1, .5f), -28, 43, 90, 16, TextAnchor.MiddleRight, true, HM);

            wT = T(root, "", 11, BR, new Vector2(1, .5f), -29, 84.5f, 220, 13, TextAnchor.MiddleRight, true, HM);

            fmT = T(root, "", 9, BR, new Vector2(1, .5f), -29, 72, 100, 10, TextAnchor.MiddleRight, false, HM);

            wSil = Raw(root, "silhueta", BR, new Vector2(1, .5f), -80, 79, 106, 28, null, new Color(1, 1, 1, .8f)); wSil.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, .8f);

            magRT = R(root, "pente", BR, new Vector2(1, .5f), -28, 25.5f, 150, 3);

            runT = T(root, "", 10, BR, new Vector2(1, .5f), -28, 104, 300, 12, TextAnchor.MiddleRight, true, FBUI.Gold);

            // placar do topo: compacto, cor só no fio de baixo (A · meio · B, 10 px do topo)

            var TC = new Vector2(.5f, 1);

            Image Sc(string n, out RectTransform rt) { var im = Box(root, n, TC, new Vector2(.5f, 1), 0, -10, 54, 51, new Color(0, 0, 0, .45f)); rt = im.rectTransform; return im; }

            Sc("placarA", out scA); Sc("placarMeio", out scMid); Sc("placarB", out scB);

            lineA = Box(scA, "fio", new Vector2(.5f, 0), new Vector2(.5f, 0), 0, 0, 54, 2, new Color(90 / 255f, 160 / 255f, 1, .85f)); lineA.rectTransform.anchorMin = Vector2.zero; lineA.rectTransform.anchorMax = new Vector2(1, 0); lineA.rectTransform.sizeDelta = new Vector2(0, 2 * K);

            lineB = Box(scB, "fio", new Vector2(.5f, 0), new Vector2(.5f, 0), 0, 0, 54, 2, new Color(1, 95 / 255f, 85 / 255f, .85f)); lineB.rectTransform.anchorMin = Vector2.zero; lineB.rectTransform.anchorMax = new Vector2(1, 0); lineB.rectTransform.sizeDelta = new Vector2(0, 2 * K);

            leftT = T(scA, "", 20, new Vector2(.5f, .5f), new Vector2(.5f, .5f), 0, 0, 120, 26, TextAnchor.MiddleCenter, true, Color.white);

            rightT = T(scB, "", 20, new Vector2(.5f, .5f), new Vector2(.5f, .5f), 0, 0, 120, 26, TextAnchor.MiddleCenter, true, Color.white);

            eyebrowT = T(scMid, "", 8, TC, new Vector2(.5f, 1), 0, -3, 300, 10, TextAnchor.MiddleCenter, false, HM);

            centerMatchT = T(scMid, "", 17, TC, new Vector2(.5f, 1), 0, -12, 300, 22, TextAnchor.MiddleCenter, true, Color.white);

            barA = Fill(Box(scMid, "ba", new Vector2(0, 0), new Vector2(0, 0), 0, 0, 1, 2, new Color(1, 1, 1, .08f)).transform, new Color(.18f, .48f, 1));

            barB = Fill(Box(scMid, "bb", new Vector2(1, 0), new Vector2(1, 0), 0, 0, 1, 2, new Color(1, 1, 1, .08f)).transform, new Color(.95f, .23f, .27f)); barB.fillOrigin = 1;

            for (int i = 0; i < 7; i++) pips[i] = Box(scMid, "pip" + i, new Vector2(.5f, 0), new Vector2(.5f, 0), 0, 4, 10, 5, new Color(1, 1, 1, .16f));

            boxRT = Box(root, "objetivo", TC, new Vector2(.5f, 1), 0, -62, 200, 16, new Color(0, 0, 0, .4f)).rectTransform; boxT = T(boxRT, "", 10, new Vector2(.5f, .5f), new Vector2(.5f, .5f), 0, 0, 900, 14, TextAnchor.MiddleCenter, false, new Color(1, 1, 1, .75f)); boxRT.gameObject.SetActive(false);

            // painel do esquadrão (ao lado do minimapa)

            squadRT = R(root, "esquadrao", new Vector2(0, 1), new Vector2(0, 1), 218, -16, 200, 260);

            T(squadRT, "ESQUADRÃO", 10, new Vector2(0, 1), new Vector2(0, 1), 2, 0, 120, 12, TextAnchor.MiddleLeft, true, new Color(1, 1, 1, .7f));

            squadCt = T(squadRT, "", 10, new Vector2(1, 1), new Vector2(1, 1), -2, 0, 160, 12, TextAnchor.MiddleRight, true, new Color(1, 1, 1, .7f)); squadCt.supportRichText = true; squadRT.gameObject.SetActive(false);

            centerT = Lbl(root, "", 30, C, C, new Vector2(0, 170), new Vector2(1100, 50), TextAnchor.MiddleCenter, true, FBUI.Gold);

            mtoastT2 = Lbl(root, "", 26, C, C, new Vector2(0, 220), new Vector2(1200, 44), TextAnchor.MiddleCenter, true, FBUI.Gold);

            // toast de evento (direita)

            toastRT = Img("toast", new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-190, 120), new Vector2(340, 84), new Color(0, 0, 0, .6f)).rectTransform;

            toastBar = FBUI.Panel(toastRT, "barra", new Vector2(0, 0), new Vector2(0, 1), new Vector2(3, 0), new Vector2(6, 0), FBUI.Gold);

            toastA = Lbl(toastRT, "", 13, new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -14), new Vector2(-20, 18), TextAnchor.MiddleLeft, false, FBUI.Gold);

            toastB = Lbl(toastRT, "", 24, new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(10, 2), new Vector2(-20, 30), TextAnchor.MiddleLeft, true);

            toastC = Lbl(toastRT, "", 12, new Vector2(0, 0), new Vector2(1, 0), new Vector2(10, 14), new Vector2(-20, 18), TextAnchor.MiddleLeft, false, FBUI.Mut); toastRT.gameObject.SetActive(false);

            // banner de abate e de equipe eliminada

            bannerRT = FBUI.Rect(root, "banner", C, C, new Vector2(0, -170), new Vector2(700, 80));

            bannerA = Lbl(bannerRT, "", 34, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -20), new Vector2(700, 44), TextAnchor.MiddleCenter, true);

            bannerB = Lbl(bannerRT, "", 15, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 14), new Vector2(700, 22), TextAnchor.MiddleCenter, false, FBUI.Mut); bannerRT.gameObject.SetActive(false);

            wipeRT = FBUI.Rect(root, "wipe", C, C, new Vector2(0, 90), new Vector2(900, 90)); wipeA = Lbl(wipeRT, "", 40, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -24), new Vector2(900, 50), TextAnchor.MiddleCenter, true); wipeB = Lbl(wipeRT, "", 15, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 12), new Vector2(900, 22), TextAnchor.MiddleCenter, false, FBUI.Mut); wipeRT.gameObject.SetActive(false);

            countT = Lbl(root, "", 120, C, C, new Vector2(0, 60), new Vector2(900, 160), TextAnchor.MiddleCenter, true, Color.white);

            killcamT = Lbl(root, "", 22, C, C, new Vector2(0, -60), new Vector2(900, 40), TextAnchor.MiddleCenter, true, FBUI.Red);

            // dinheiro (economia)

            // [parceiro] física 02/11: o dinheiro ficava em (150, -30), embaixo do minimapa (R: centro 141,-139, 237x237, até y -258) e ele

            // cobria o texto. Agora fica logo abaixo da escala "20 m" do minimapa (y -276), alinhado à borda esquerda dele (x 22),

            // sem passar da coluna do esquadrão (começa em x 272).

            cashT = Lbl(root, "", 26, new Vector2(0, 1), new Vector2(0, 1), new Vector2(142, -302), new Vector2(240, 34), TextAnchor.MiddleLeft, true, new Color(.4f, .95f, .5f));

            cashPlus = Lbl(root, "", 16, new Vector2(0, 1), new Vector2(0, 1), new Vector2(142, -330), new Vector2(240, 22), TextAnchor.MiddleLeft, false, new Color(.4f, .95f, .5f));

            // prompts

            promptRT = Img("prompt", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 200), new Vector2(420, 56), new Color(0, 0, 0, .55f)).rectTransform;

            var kb = FBUI.Panel(promptRT, "k", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(30, 0), new Vector2(36, 36), FBUI.Gold); promptK = Lbl(kb.transform, "X", 20, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, true, Color.black);

            promptT = Lbl(promptRT, "", 18, new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(40, 8), new Vector2(-90, 24), TextAnchor.MiddleLeft, true); promptS = Lbl(promptRT, "", 12, new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(40, -12), new Vector2(-90, 18), TextAnchor.MiddleLeft, false, FBUI.Mut); promptRT.gameObject.SetActive(false);

            reviveRT = Img("reviver", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 270), new Vector2(420, 40), new Color(0, 0, 0, .55f)).rectTransform; reviveT = Lbl(reviveRT, "", 16, Vector2.zero, Vector2.one, new Vector2(0, 4), Vector2.zero, TextAnchor.MiddleCenter, true);

            reviveBar = Fill(FBUI.Panel(reviveRT, "rb", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 3), new Vector2(-10, 4), new Color(1, 1, 1, .1f)).transform, new Color(.3f, .9f, .4f)); reviveRT.gameObject.SetActive(false);

            zoneRT = FBUI.Rect(root, "zona", C, C, new Vector2(0, 120), new Vector2(400, 90)); zoneArrow = FBUI.Panel(zoneRT, "seta", new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -24), new Vector2(34, 34), new Color(.35f, .75f, 1)); zoneArrow.sprite = FBUI.ArrowSprite;

            zoneT = Lbl(zoneRT, "", 15, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 16), new Vector2(400, 22), TextAnchor.MiddleCenter, true, new Color(.35f, .75f, 1)); zoneRT.gameObject.SetActive(false);

            dmgRT = FBUI.Rect(root, "dirDano", C, C, Vector2.zero, new Vector2(10, 10)); dmgArrow = FBUI.Panel(dmgRT, "seta", C, C, new Vector2(0, 140), new Vector2(60, 24), new Color(1, .2f, .15f, 0)); dmgArrow.sprite = FBUI.ArrowSprite;

            // minimapa (drawMini do main.js): recorte com cantos cortados, mapa girando com a visão a 60%, anéis de 10 m, cone de visão,

            // aliados = setas azuis, inimigo = ponto vermelho só quando atira, objetivos = losangos, zona, moldura com cantos e o N

            mini = R(root, "minimapa", new Vector2(0, 1), new Vector2(.5f, .5f), 18 + 95, -(16 + 95), 190, 190);

            // minimapa redondo: fundo escuro, foto do mapa (nítida, luz neutra), vinheta nas bordas, anel com borda fina e bússola N/L/S/O girando

            var halo = Box(mini, "sombra", C, C, 2, -2, 194, 194, new Color(0, 0, 0, .4f)); halo.sprite = MiniShape(false);

            var clip = Box(mini, "recorte", C, C, 0, 0, 190, 190, new Color(.05f, .06f, .07f, .92f)); clip.sprite = MiniShape(false); clip.gameObject.AddComponent<Mask>().showMaskGraphic = true; miniClip = clip.rectTransform;

            miniMap = Raw(miniClip, "mapa", C, C, 0, 0, 10, 10, null, new Color(1, 1, 1, .92f)); miniMap.gameObject.SetActive(false);

            foreach (var r in new[] { 15, 30 }) { var rr = Box(miniClip, "anel" + r, C, C, 0, 0, r * 2 * MS, r * 2 * MS, new Color(1, 1, 1, .07f)); rr.sprite = RingSprite; miniRings.Add(rr); }

            var cn = Box(miniClip, "cone", C, C, 0, 0, 190 * 1.1f, 190 * 1.1f, new Color(1, 1, 1, .9f)); cn.sprite = GlowSprite; cn.type = Image.Type.Filled; cn.fillMethod = Image.FillMethod.Radial360; cn.fillOrigin = 2; cn.fillClockwise = true; cone = cn.rectTransform;

            zoneRing = Box(miniClip, "zona", C, C, 0, 0, 10, 10, new Color(80 / 255f, 220 / 255f, 1, .95f)); zoneRing.sprite = RingSprite; zoneRing.gameObject.SetActive(false);

            zoneNext = Box(miniClip, "zonaProx", C, C, 0, 0, 10, 10, new Color(1, 1, 1, .8f)); zoneNext.sprite = RingSprite; zoneNext.gameObject.SetActive(false);

            heliScanRing = Box(miniClip, "heliScan", C, C, 0, 0, 10, 10, new Color(1f, 0.5f, 0.1f, .85f)); heliScanRing.sprite = RingSprite; heliScanRing.gameObject.SetActive(false);

            var vig = Box(miniClip, "vinheta", C, C, 0, 0, 178, 178, new Color(.02f, .03f, .04f, 1)); vig.sprite = VignetteSprite;   // escurece as bordas

            miniBg = clip;

            var fr = Box(mini, "moldura", C, C, 0, 0, 190, 190, new Color(1, 1, 1, .22f)); fr.sprite = MiniShape(true); fr.raycastTarget = true;

            var miniBtn = fr.gameObject.AddComponent<Button>(); miniBtn.onClick.AddListener(() => FBMatchMap.Toggle());

            var mcb = miniBtn.colors; mcb.highlightedColor = new Color(1, 1, 1, .35f); mcb.pressedColor = FBUI.Gold; miniBtn.colors = mcb;

            foreach (var (x, y, w, h) in new[] { (6f, 6f, 2f, 22f), (6f, 6f, 22f, 2f), (184f, 184f, 2f, 22f), (184f, 184f, 22f, 2f) })

            { var c = Box(mini, "canto", new Vector2(0, 0), new Vector2(x < 95 ? 0 : 1, y < 95 ? 0 : 1), x - (x < 95 ? 1 : -1), y - (y < 95 ? 1 : -1), w, h, new Color(1, 1, 1, .6f)); }

            miniPlayer = Box(mini, "eu", C, C, 0, 0, 13.2f, 15.4f, Color.white).rectTransform; var mpI = miniPlayer.GetComponent<Image>(); mpI.sprite = ArrowSpr; var ol = mpI.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(0, 0, 0, .85f); ol.effectDistance = new Vector2(1.4f, -1.4f);

            var nb = Box(mini, "N", C, C, 0, 80, 18, 18, new Color(0, 0, 0, .75f)); nb.sprite = DiscSprite; miniN = nb.rectTransform; nT = T(miniN, "N", 11, C, C, 0, 0, 18, 18, TextAnchor.MiddleCenter, true, new Color(1, .78f, .25f));

            foreach (var l in new[] { "L", "S", "O" }) { var t = T(mini, l, 9, C, C, 0, 0, 14, 14, TextAnchor.MiddleCenter, true, new Color(1, 1, 1, .6f)); compass.Add(t.rectTransform); }

            // escala (barra de 20 m) embaixo do mapa

            var sb = Box(mini, "escala", C, C, 0, -103, 20 * MS, 2, new Color(1, 1, 1, .55f)); T(mini, "20 m · [M] MAPA", 8, C, C, 0, -111, 74, 10, TextAnchor.MiddleCenter, false, new Color(1, .85f, .25f, .85f));

            // placar (Tab)

            boardRT = Img("placarTab", C, C, Vector2.zero, new Vector2(620, 430), new Color(.03f, .04f, .05f, .9f)).rectTransform; boardT = Lbl(boardRT, "", 16, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(-40, -40), TextAnchor.UpperLeft); boardT.supportRichText = true; boardRT.gameObject.SetActive(false);

            for (int i = 0; i < 5; i++) { var t = Lbl(root, "", 15, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-200, -140 - i * 22), new Vector2(380, 22), TextAnchor.MiddleRight); t.supportRichText = true; feed.Add((t, 0)); }

            FBMinimap.Capture();

            FBHudWeapons.Attach(root); FBKillFeed.Attach(root);

            hpT.gameObject.SetActive(false); hpBar.transform.parent.gameObject.SetActive(false); armorBar.transform.parent.gameObject.SetActive(false); armorT.gameObject.SetActive(false);   // vida agora no centro (FBHudWeapons)   // dock de armas/kit (estilo FF) e feed de abates novos

        }

        static readonly Vector2 C = new(.5f, .5f);

        // px do web a 1280x720 -> canvas de referência 1600x900

        const float K = 1.25f, MS = (95 - 6) / 40f;   // escala do minimapa: raio de 40 m no recorte (px do web por metro)

        static readonly Color HW = new(1, 1, 1, .92f), HM = new(1, 1, 1, .5f), HL = new(1, 1, 1, .14f), HPC = new(236 / 255f, 232 / 255f, 220 / 255f);

        RectTransform R(Transform parent, string n, Vector2 anchor, Vector2 pivot, float x, float y, float w, float h)

        { var rt = FBUI.Rect(parent, n, anchor, anchor, new Vector2(x * K, y * K), new Vector2(w * K, h * K)); rt.pivot = pivot; rt.anchoredPosition = new Vector2(x * K, y * K); return rt; }

        Image Box(Transform parent, string n, Vector2 anchor, Vector2 pivot, float x, float y, float w, float h, Color c) { var im = R(parent, n, anchor, pivot, x, y, w, h).gameObject.AddComponent<Image>(); im.color = c; im.raycastTarget = false; return im; }

        RawImage Raw(Transform parent, string n, Vector2 anchor, Vector2 pivot, float x, float y, float w, float h, Texture tex, Color c) { var im = R(parent, n, anchor, pivot, x, y, w, h).gameObject.AddComponent<RawImage>(); im.texture = tex; im.color = c; im.raycastTarget = false; return im; }

        Text T(Transform parent, string s, float px, Vector2 anchor, Vector2 pivot, float x, float y, float w, float h, TextAnchor al, bool title, Color c)

        {

            var rt = R(parent, "txt", anchor, pivot, x, y, w, h); var t = rt.gameObject.AddComponent<Text>(); t.text = s; t.font = title ? FBUI.Title : FBUI.Font; t.fontSize = Mathf.RoundToInt(px * K); t.alignment = al; t.color = c; t.raycastTarget = false;

            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; var sh = rt.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .85f); sh.effectDistance = new Vector2(0, -1.5f); return t;

        }

        static Sprite Gen(int n, System.Func<float, float, float> a, int h = 0)

        {

            h = h > 0 ? h : n; var t = new Texture2D(n, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear }; var px = new Color[n * h];

            for (int y = 0; y < h; y++) for (int x = 0; x < n; x++) px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(a((x + .5f) / n, (y + .5f) / h)));

            t.SetPixels(px); t.Apply(); return Sprite.Create(t, new Rect(0, 0, n, h), Vector2.one * .5f);

        }

        static Sprite disc, ringSp, glow, arrowS, grad;

        static Sprite DiscSprite => disc ? disc : disc = Gen(128, (x, y) => (1 - Mathf.Sqrt((x - .5f) * (x - .5f) + (y - .5f) * (y - .5f)) * 2) * 64);

        static Sprite RingSprite => ringSp ? ringSp : ringSp = Gen(256, (x, y) => { float r = Mathf.Sqrt((x - .5f) * (x - .5f) + (y - .5f) * (y - .5f)) * 2; return 1 - Mathf.Abs(r - .985f) * 128 / 1.6f; });

        static Sprite GlowSprite => glow ? glow : glow = Gen(128, (x, y) => { float r = Mathf.Sqrt((x - .5f) * (x - .5f) + (y - .5f) * (y - .5f)) * 2; return r > 1 ? 0 : .28f * (1 - r); });

        static Sprite ArrowSpr => arrowS ? arrowS : arrowS = Gen(64, (x, y) => { float X = (x - .5f) * 14, Y = (.5f - y) * 16; return InTri(X, Y, 0, -8, 6, 6, 0, 3) || InTri(X, Y, 0, -8, 0, 3, -6, 6) ? 1 : 0; });   // seta do drawMini (0,-8)(6,6)(0,3)(-6,6)

        static Sprite GradSprite => grad ? grad : grad = Gen(64, (x, y) => Mathf.Lerp(.62f, .18f, x), 1);

        static bool InTri(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)

        { float d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by), d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy), d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay); bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0; return !(neg && pos); }

        static Sprite shapeF, shapeO, vigS; readonly List<RectTransform> compass = new();

        static Sprite VignetteSprite => vigS ? vigS : vigS = Gen(128, (x, y) => { float r = Mathf.Max(Mathf.Abs(x - .5f), Mathf.Abs(y - .5f)) * 2; return Mathf.Clamp01((r - .7f) / .3f) * .7f; });

        // recorte do minimapa: quadrado com os cantos superior esquerdo e inferior direito cortados (pad 6, chanfro 14); outline = moldura de 1 px

        // recorte do minimapa: quadrado com os cantos superior esquerdo e inferior direito cortados (pad 6, chanfro 14); outline = moldura de 1 px

        static Sprite MiniShape(bool outline)

        {

            if (!outline && shapeF) return shapeF; if (outline && shapeO) return shapeO;

            const int N = 380; const float W = 190, pad = 6, ch = 14;

            float Inside(float x, float y)   // x,y em px do web (y para baixo); distância com sinal até a borda (positivo = dentro)

            { float d = Mathf.Min(Mathf.Min(x - pad, W - pad - x), Mathf.Min(y - pad, W - pad - y)); d = Mathf.Min(d, ((x - pad) + (y - pad) - ch) / 1.4142f); d = Mathf.Min(d, ((W - pad - x) + (W - pad - y) - ch) / 1.4142f); return d; }

            var sp = Gen(N, (u, v) => { float d = Inside(u * W, (1 - v) * W); return outline ? 1 - Mathf.Abs(d - .5f) * 2 : Mathf.Clamp01(d * 2 + .5f); });

            if (outline) shapeO = sp; else shapeF = sp; return sp;

        }

        Image Img(string n, Vector2 a0, Vector2 a1, Vector2 p, Vector2 s, Color c) { var im = FBUI.Panel(root, n, a0, a1, p, s, c); im.raycastTarget = false; return im; }

        Text Lbl(Transform parent, string t, int size, Vector2 a0, Vector2 a1, Vector2 p, Vector2 s, TextAnchor al, bool title = false, Color? c = null) { var l = FBUI.Label(parent, t, size, a0, a1, p, s, al, title, c); var sh = l.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .6f); sh.effectDistance = new Vector2(1, -1); return l; }

        static Image Fill(Transform parent, Color c) { var im = FBUI.Panel(parent, "fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, c); im.sprite = FBUI.WhiteSprite; im.type = Image.Type.Filled; im.fillMethod = Image.FillMethod.Horizontal; im.raycastTarget = false; return im; }



        // ---- API usada pelo jogo

        public void Feed(string s, bool me) => FBKillFeed.Message(s.Replace(" ✖ ", "  ✖  "), me);

        public void Hit(bool head) { if (killT > 0) return; hitT = .2f; hitBig = 1; hitMark.color = head ? new Color(1, .82f, .2f) : Color.white; FBAudio.Play2D("hitmark", .5f, 0); }

        // abate (vermelho, maior, segura mais) / derrubada (laranja) — rodada 40

        float killT, hitBig = 1;

        public void KillMark(bool knock) { killT = .5f; hitT = .5f; hitBig = 1.8f; hitMark.color = knock ? new Color(1, .55f, .1f) : new Color(1, .16f, .12f); FBAudio.Play2D("hitmark", .7f, 0, knock ? 1.1f : .85f); }

        // vinheta de dano: borda radial (vermelho escuro só nas beiradas, centro limpo para mirar)

        static Sprite hurtS; static Sprite HurtEdge => hurtS ? hurtS : hurtS = Gen(256, (x, y) => { float dx = (x - .5f) * 2, dy = (y - .5f) * 2, r = Mathf.Sqrt(dx * dx * .8f + dy * dy * 1.1f); return Mathf.Pow(Mathf.Clamp01((r - .55f) / .75f), 1.6f); });

        public static int LastFloat = -1, FloatN; int floatFrame = -1; float floatSum; Vector3 floatP; bool floatHead;

        public void FloatDamage(Vector3 p, float dmg, bool head)

        {

            FloatN++;

            if (floatFrame == Time.frameCount && floats.Count > 0 && (p - floatP).sqrMagnitude < 4)

            {   // r51: escopeta — os bagos do mesmo disparo no mesmo alvo viram um número (antes 6–9 números empilhados)

                floatSum += dmg; var (lt, _, _) = floats[floats.Count - 1]; lt.text = Mathf.RoundToInt(floatSum).ToString(); if (head && !floatHead) { lt.color = new Color(1, .3f, .25f); lt.fontSize = Mathf.RoundToInt(lt.fontSize * 17f / 13f); } floatHead |= head;

                LastFloat = Mathf.RoundToInt(floatSum); return;

            }

            floatFrame = Time.frameCount; floatSum = dmg; floatP = p; floatHead = head; LastFloat = Mathf.RoundToInt(dmg);

            var t = Lbl(root, Mathf.RoundToInt(dmg).ToString(), head ? 17 : 13, C, C, Vector2.zero, new Vector2(80, 22), TextAnchor.MiddleCenter, true, head ? new Color(1, .3f, .25f) : Color.white);

            floats.Add((t, p + new Vector3(Random.Range(-.2f, .2f), 0, Random.Range(-.2f, .2f)), Time.time));

        }

        // direção do dano: arcos por atacante que acompanham a câmera (FBDamageIndicators); a seta única antiga ficou só como reserva

        public void DamageDir(Vector3 from) => FBDamageIndicators.Hit(null, from, 20);

        public void Damaged(float k = .55f) { dmgT = Mathf.Max(dmgT, k); }

        public void Damaged() => Damaged(.55f);

        public void Center(string s, float ttl = 2.5f) { centerT.text = s; centerTTL = ttl; }

        public void MatchToast(string s, Color c, float ttl) { mtoastT2.text = s; mtoastT2.color = c; mtoastT = ttl; }

        public void Toast(string a, string b, string c, Color col) { toastA.text = a; toastB.text = b; toastC.text = c; toastA.color = col; toastBar.color = col; toastT = 3; toastRT.gameObject.SetActive(true); }

        public string BoxText => boxT && boxRT && boxRT.gameObject.activeSelf ? boxT.text : "";   // r51: teste da compra no exe

        public void SetBox(string s, bool on) { boxT.text = s; boxRT.gameObject.SetActive(on && !string.IsNullOrEmpty(s)); boxRT.sizeDelta = new Vector2(boxT.preferredWidth + 20 * K, 16 * K); }

        public void SetMatch(string eyebrow, string left, string center, string right, float a, float b, Color[] pp)

        {

            eyebrowT.text = eyebrow; leftT.text = left; centerMatchT.text = center; rightT.text = right;

            float wa = Mathf.Max(40, leftT.preferredWidth / K + 20), wb = Mathf.Max(40, rightT.preferredWidth / K + 20), wm = Mathf.Max(120, Mathf.Max(eyebrowT.preferredWidth, centerMatchT.preferredWidth) / K) + 24, tot = wa + wm + wb + 4;

            scA.sizeDelta = new Vector2(wa * K, 51 * K); scMid.sizeDelta = new Vector2(wm * K, 51 * K); scB.sizeDelta = new Vector2(wb * K, 51 * K);

            scA.anchoredPosition = new Vector2((-tot / 2 + wa / 2) * K, -10 * K); scMid.anchoredPosition = new Vector2((-tot / 2 + wa + 2 + wm / 2) * K, -10 * K); scB.anchoredPosition = new Vector2((tot / 2 - wb / 2) * K, -10 * K);

            var ba = (RectTransform)barA.transform.parent; var bb = (RectTransform)barB.transform.parent; ba.sizeDelta = bb.sizeDelta = new Vector2(wm / 2 * K, 2 * K);

            ba.gameObject.SetActive(a >= 0); bb.gameObject.SetActive(b >= 0); if (a >= 0) barA.fillAmount = Mathf.Clamp01(a); if (b >= 0) barB.fillAmount = Mathf.Clamp01(b);

            int np = pp?.Length ?? 0; for (int i = 0; i < 7; i++) { pips[i].gameObject.SetActive(i < np); if (i < np) { pips[i].color = pp[i]; pips[i].rectTransform.anchoredPosition = new Vector2((i - (np - 1) / 2f) * 14 * K, 4 * K); } }

        }

        public void Countdown(string s) { countT.text = s; countTT = 1; countT.color = s.Length > 2 ? new Color(.3f, .9f, .45f) : Color.white; }

        public void KillBanner(int n, string a, string b, Color c) { bannerA.text = a; bannerA.color = c; bannerB.text = b; bannerT = 2.2f; bannerRT.gameObject.SetActive(true); }

        public void WipeBanner(string a, string b, Color c) { wipeA.text = a; wipeA.color = c; wipeB.text = b; wipeT = 3; wipeRT.gameObject.SetActive(true); }

        public void Killcam(string by, bool on) { killcamT.text = on ? "ABATIDO POR " + by : ""; killcamTT = on ? 3.5f : 0; }

        public void SetCash(int v, string plus, int armor, bool helmet) { if (!this || !cashT) return; cashT.text = v < 0 ? "" : $"$ {v}"; if (!string.IsNullOrEmpty(plus)) { cashPlus.text = plus; cashPlusT = 2; } }

        public void BuyHint(bool on) { if (on) Center("B = LOJA", 2); }

        public void Prompt(string key, string text, string sub = null) { promptRT.gameObject.SetActive(text != null); if (text == null) return; promptK.text = key; promptT.text = text; promptS.text = sub ?? ""; }

        public void ZoneWarning(bool on, float rel, float dmg, float dist, string label = "FORA DA ZONA") { if (!this || !zoneRT) return; zoneRT.gameObject.SetActive(on);   // na troca de mapa a HUD já foi destruída (FBZone.Dispose no OnDestroy da partida)

            if (!on) return; zoneArrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -rel * Mathf.Rad2Deg); zoneT.text = $"{label} · −{Mathf.RoundToInt(dmg)}/s · {Mathf.RoundToInt(dist)} m"; }

        public void SetRun(bool on) { runT.text = on ? "» CORRIDA AUTOMÁTICA" : ""; }

        public void SetMed(int kits, bool inf, float prog, bool healing)

        {

            medT.text = inf ? "∞" : kits.ToString(); var g = medRT.GetOrAdd<CanvasGroup>(); g.alpha = !inf && kits <= 0 ? .35f : 1;

            ringRT.gameObject.SetActive(prog > 0); if (prog > 0) { ring.fillAmount = prog; ringT.text = Mathf.Max(0, (1 - prog) * FBMed.Use).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture); }

            hpBar.color = healing ? new Color(.61f, .9f, .69f) : hpBar.color;

        }

        public void Revive(string text, float p) { reviveRT.gameObject.SetActive(text != null); if (text == null) return; reviveT.text = text; reviveBar.fillAmount = p; }

        public void ScopeKick() { scopeKick = 1; }

        // r60 MIRA ÚNICA. Antes havia DOIS retículos na tela, em lugares diferentes: a mira da arma no centro e, por cima, um ✕ no ponto real

        // do impacto (r52). Lia como imprecisão, e nenhum dos dois dizia sozinho onde a bala ia bater. Agora é um só: quando o cano não

        // alcança o ponto sob o retículo (quina de carro, muro, batente), a PRÓPRIA mira vai até onde a bala para, em vermelho. Um retículo,

        // sempre no ponto de impacto verdadeiro. O FBPlayer chama isto no fim do LateUpdate, com a câmera final do quadro, para a mira não

        // ficar um quadro atrás do que a tela mostra.

        public void PlaceImpact(bool on, Vector3 screen)

        {

            impOn = on && screen.z > 0; impPx = new Vector2(screen.x, screen.y);

            float k = canvas ? Mathf.Max(1e-4f, canvas.scaleFactor) : 1;

            crossOff = impOn ? new Vector2(impPx.x - Screen.width * .5f, impPx.y - Screen.height * .5f) / k : Vector2.zero;

            CrossLayout();

        }

        // UM MARCADOR POR VEZ (r60b, pedido do Ruan: "na mira, tenhamos o ✕ quando o tiro estiver em um muro"):

        //  · tiro livre      -> só o retículo (4 traços + ponto), no centro da tela;

        //  · tiro bloqueado  -> só o ✕, no ponto REAL onde a bala para, em vermelho.

        // Nunca os dois juntos, que era a queixa original (duas miras em lugares diferentes).

        void CrossLayout()

        {

            bool xOn = impOn && crossVis;

            for (int i = 0; i < 4; i++)

            {

                if (!cross[i]) continue;

                cross[i].gameObject.SetActive(crossVis && !xOn);

                cross[i].anchoredPosition = i == 0 ? new Vector2(0, crossGap) : i == 1 ? new Vector2(0, -crossGap) : i == 2 ? new Vector2(crossGap, 0) : new Vector2(-crossGap, 0);

            }

            if (crossDot) { crossDot.gameObject.SetActive(crossDotVis && !xOn); crossDot.rectTransform.anchoredPosition = Vector2.zero; }

            if (impX) { impX.gameObject.SetActive(xOn); if (xOn) impX.rectTransform.anchoredPosition = crossOff; }

        }

        public bool ImpactShown => impOn && crossVis;                    // teste no exe: a mira saiu do centro

        public Vector2 ImpactPx => impPx;



        // ---- cartões (morte / vitória / pausa)

        public void CloseCards() { if (card) Destroy(card); card = null; CardOpen = false; if (FBMatch.I && FBMatch.I.state == "play" && !FBMatch.Paused && !FBInput.Touch) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } }

        GameObject Card(string eyebrow, string title, Color tc, string body)

        {

            CloseCards(); CardOpen = true; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;

            var bg = FBUI.Panel(root, "cartao", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, .55f)); card = bg.gameObject;

            var p = FBUI.Panel(bg.transform, "painel", C, C, Vector2.zero, new Vector2(560, 330), FBUI.Dark).transform;

            FBUI.Label(p, eyebrow, 14, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -34), new Vector2(680, 22), TextAnchor.MiddleCenter, false, FBUI.Mut);

            FBUI.Label(p, title, 64, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -100), new Vector2(680, 80), TextAnchor.MiddleCenter, true, tc);

            var b = FBUI.Label(p, body, 18, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 0), new Vector2(640, 140), TextAnchor.MiddleCenter); b.supportRichText = true;

            return p.gameObject;

        }

        // ---- telas de ABATE e de FIM DE PARTIDA (rodada 38: refeitas do zero — pedido do usuário "refaça toda a tela de renascer, revanche

        // etc, algo completamente diferente e novo"). Antes: o mesmo cartão pequeno no meio da tela para as duas. Agora: tela cheia, em camadas,

        // animada (entra deslizando, números contam), com quem te abateu, arma, distância e vida que sobrou dele, anel de contagem para

        // renascer, placar dos times com barras, destaques da partida (MVP) e recompensas. Teclado: ESPAÇO renasce, R pede revanche.

        class CardFx : MonoBehaviour

        {

            public System.Action<float> tick; float t;

            // [parceiro 01/11] medidor de tempo por sistema (FBTempo): com FBTempo.On soma o custo das animações das telas

            static readonly int __fbt_Update = FBTempo.Id("FBHUD.CardFx.Update");

            void Update()

            {

                if (!FBTempo.On) { Update__fbt(); return; }

                long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);

            }

            void Update__fbt() { t += Time.unscaledDeltaTime; tick?.Invoke(t); }

        }

        static Sprite sideGrad, topGrad;

        static Sprite SideGrad => sideGrad ? sideGrad : sideGrad = Gen(128, (x, y) => Mathf.Pow(1 - x, 1.6f) * .92f, 1);                  // escuro à esquerda → transparente

        static Sprite TopGrad => topGrad ? topGrad : topGrad = Gen(1, (x, y) => Mathf.Pow(y, 1.4f), 128);                                     // forte em cima → nada embaixo

        static readonly string[] DICAS = {

            "Agachado (C) você erra menos e aparece menos atrás de cobertura.", "Recarregue atrás de uma parede — o inimigo ouve a troca de pente.",

            "No Tático, ponto disputado não anda: elimine quem está dentro.", "Kit médico (H) cura até em pé; agachado você fica menor.",

            "Mire na cabeça: headshot vale mais dinheiro e passe.", "Troque de posição depois de abater alguém — o time dele vem atrás do barulho.",

            "Aliado caído? Limpe a área antes de reanimar.", "Pistola saca mais rápido que trocar o pente do fuzil no meio do tiroteio." };

        static string Ptbr(float v, string f) => v.ToString(f, System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));

        Text Tx(Transform p, string s, int size, Vector2 a, Vector2 pos, Vector2 box, TextAnchor al, bool title, Color c, bool shadow = true)

        {

            var t = FBUI.Label(p, s, size, a, a, pos, box, al, title, c); t.supportRichText = true; t.horizontalOverflow = HorizontalWrapMode.Overflow;

            if (shadow) { var sh = t.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .8f); sh.effectDistance = new Vector2(2, -2); }

            return t;

        }

        static float Ease(float t) { t = Mathf.Clamp01(t); return 1 - Mathf.Pow(1 - t, 3); }

        static float EaseBack(float t) { t = Mathf.Clamp01(t); float c = 1.7f; return 1 + (c + 1) * Mathf.Pow(t - 1, 3) + c * Mathf.Pow(t - 1, 2); }

        RectTransform Tile(Transform p, string label, Vector2 a, Vector2 pos, Vector2 size, Color accent, out Text value)

        {

            var im = FBUI.Panel(p, "tile_" + label, a, a, pos, size, new Color(.06f, .07f, .09f, .82f)); var rt = im.rectTransform;

            FBUI.Panel(rt, "acento", new Vector2(0, 0), new Vector2(0, 1), new Vector2(2, 0), new Vector2(4, 0), accent);

            Tx(rt, label, 13, new Vector2(0, 1), new Vector2(16 + 70, -16), new Vector2(160, 18), TextAnchor.MiddleLeft, false, FBUI.Mut, false);

            value = Tx(rt, "0", 38, new Vector2(0, 0), new Vector2(16 + 80, 30), new Vector2(180, 44), TextAnchor.MiddleLeft, true, Color.white);

            return rt;

        }

        GameObject OpenScreen(string name, out CardFx fx)

        {

            CloseCards(); CardOpen = true; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;

            var bg = FBUI.Panel(root, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0)); card = bg.gameObject; bg.raycastTarget = true;

            fx = card.AddComponent<CardFx>(); return card;

        }

        public void DeathCard(bool auto, System.Action respawn, System.Action menu) => DeathScreen(auto, respawn, menu, null, null, false);

        public void DeathScreen(bool auto, System.Action respawn, System.Action menu, FBCharacter by, string why, bool head)

        {

            var root2 = OpenScreen("tela_abate", out var fx).transform; var bgIm = root2.GetComponent<Image>();

            // camadas: escurecido · degradê lateral · vinheta vermelha em cima · faixa diagonal

            var side = FBUI.Panel(root2, "lado", new Vector2(0, 0), new Vector2(.62f, 1), Vector2.zero, Vector2.zero, new Color(.02f, .02f, .03f, 1)); side.sprite = SideGrad;

            var top = FBUI.Panel(root2, "vinheta", new Vector2(0, .72f), new Vector2(1, 1), Vector2.zero, Vector2.zero, new Color(.55f, .03f, .03f, .55f)); top.sprite = TopGrad;

            var slash = FBUI.Panel(root2, "faixa", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(430, 100), new Vector2(860, 6), FBUI.Red); slash.rectTransform.localRotation = Quaternion.Euler(0, 0, 1.2f);   // entre o título e o "POR …"

            var col = FBUI.Rect(root2, "coluna", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(380, 0), new Vector2(620, 600));

            var title = Tx(col, "ELIMINADO", 104, new Vector2(0, .5f), new Vector2(310, 150), new Vector2(620, 120), TextAnchor.MiddleLeft, true, new Color(1, .27f, .22f));

            string killer = by ? by.Name.ToUpper() : why == "zone" ? "A ZONA" : why == "fall" ? "UMA QUEDA" : "—";

            var byT = Tx(col, by && by != FBMatch.Player ? $"<color=#9aa3ad>POR</color> {killer}" : why == "zone" ? "<color=#9aa3ad>PEGO PELA</color> ZONA" : why == "fall" ? "<color=#9aa3ad>MORTE POR</color> QUEDA" : "VOCÊ CAIU", 40, new Vector2(0, .5f), new Vector2(310, 66), new Vector2(620, 50), TextAnchor.MiddleLeft, true, Color.white);

            string wpn = by && by.weapon != null ? by.weapon.name.ToUpper() : by ? "MÃOS" : "";

            float dist = by && FBMatch.Player ? (by.Pos - FBMatch.Player.Pos).magnitude : 0;

            var det = Tx(col, by ? $"{wpn}   ·   {Ptbr(dist, "0")} m{(head ? "   ·   <color=#ff5a4e>HEADSHOT</color>" : "")}" : "", 20, new Vector2(0, .5f), new Vector2(310, 26), new Vector2(620, 26), TextAnchor.MiddleLeft, false, new Color(.85f, .87f, .9f));

            Image hpFill = null;

            if (by && by.alive && by != FBMatch.Player)

            {

                Tx(col, "VIDA QUE SOBROU DELE", 13, new Vector2(0, .5f), new Vector2(310, -8), new Vector2(620, 16), TextAnchor.MiddleLeft, false, FBUI.Mut, false);

                var bar = FBUI.Panel(col, "vida", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(160, -28), new Vector2(320, 10), new Color(1, 1, 1, .12f));

                hpFill = FBUI.Panel(bar.transform, "fill", new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, Vector2.zero, new Color(.35f, .9f, .45f)); hpFill.rectTransform.pivot = new Vector2(0, .5f); hpFill.rectTransform.sizeDelta = new Vector2(0, 0);

                Tx(bar.transform, Ptbr(Mathf.Max(1, by.hp), "0") + " HP", 14, new Vector2(1, .5f), new Vector2(50, 0), new Vector2(80, 18), TextAnchor.MiddleLeft, true, Color.white, false);

            }

            // estatísticas da partida até aqui

            var S = FBMatch.Stats; float kd = S.kills / Mathf.Max(1f, S.deaths);

            var tiles = new List<(RectTransform rt, Text v, float val, string fmt)>();

            string[] L = { "ABATES", "MORTES", "K/D", "HEADSHOTS" }; float[] V = { S.kills, S.deaths, kd, S.heads }; string[] F = { "0", "0", "0.00", "0" };

            for (int i = 0; i < 4; i++) { var rt = Tile(col, L[i], new Vector2(0, .5f), new Vector2(76 + i * 158, -112), new Vector2(150, 84), i == 1 ? FBUI.Red : FBUI.Gold, out var vt); tiles.Add((rt, vt, V[i], F[i])); }

            var tip = Tx(root2, "DICA  ·  " + DICAS[Random.Range(0, DICAS.Length)], 17, new Vector2(.5f, 0), new Vector2(0, 40), new Vector2(1200, 24), TextAnchor.MiddleCenter, false, new Color(.85f, .86f, .88f));

            // anel de contagem + botões (direita)

            var right = FBUI.Rect(root2, "direita", new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-250, -20), new Vector2(380, 460));

            var ringBg = FBUI.Panel(right, "anel_fundo", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 70), new Vector2(210, 210), new Color(1, 1, 1, .1f)); ringBg.sprite = RingSprite;

            var ringIm = FBUI.Panel(right, "anel", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 70), new Vector2(210, 210), FBUI.Gold); ringIm.sprite = RingSprite; ringIm.type = Image.Type.Filled; ringIm.fillMethod = Image.FillMethod.Radial360; ringIm.fillOrigin = 2; ringIm.fillClockwise = false;

            var num = Tx(right, auto ? "" : "✓", 76, new Vector2(.5f, .5f), new Vector2(0, 78), new Vector2(200, 90), TextAnchor.MiddleCenter, true, Color.white);

            var lab = Tx(right, auto ? "RENASCENDO" : "PRONTO PARA VOLTAR", 15, new Vector2(.5f, .5f), new Vector2(0, 30), new Vector2(200, 20), TextAnchor.MiddleCenter, false, FBUI.Mut, false);

            bool done = false; System.Action go = () => { if (done) return; done = true; CloseCards(); respawn(); };

            var bR = FBUI.Button(right, "RENASCER  [ESPAÇO]", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -98), new Vector2(330, 62), go, true);

            var bM = FBUI.Button(right, "MENU PRINCIPAL", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -168), new Vector2(330, 48), () => menu());

            var rightCg = right.gameObject.AddComponent<CanvasGroup>(); var colCg = col.gameObject.AddComponent<CanvasGroup>();

            float total = auto && FBMatch.I ? Mathf.Max(.1f, FBMatch.I.RespawnLeft) : 0;

            fx.tick = t =>

            {

                bgIm.color = new Color(0, 0, 0, .5f * Ease(t / .5f));

                float e = Ease((t - .05f) / .55f); col.anchoredPosition = new Vector2(Mathf.Lerp(-300, 380, e), 0); colCg.alpha = e;

                title.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1, Ease(t / .35f));

                slash.rectTransform.sizeDelta = new Vector2(860 * Ease((t - .1f) / .5f), 6);

                rightCg.alpha = Ease((t - .35f) / .4f); rightCg.interactable = rightCg.blocksRaycasts = t > .6f;

                for (int i = 0; i < tiles.Count; i++) { float k = Ease((t - .45f - i * .08f) / .5f); tiles[i].rt.localScale = Vector3.one * Mathf.Lerp(.85f, 1, EaseBack(k)); tiles[i].v.text = Ptbr(tiles[i].val * k, tiles[i].fmt); }

                if (hpFill && by) hpFill.rectTransform.sizeDelta = new Vector2(320 * Mathf.Clamp01(by.hp / Mathf.Max(1, by.maxHp)) * Ease((t - .6f) / .6f), 0);

                if (auto && FBMatch.I) { float left = Mathf.Max(0, FBMatch.I.RespawnLeft); ringIm.fillAmount = total > 0 ? left / total : 0; num.text = Mathf.CeilToInt(left).ToString(); num.rectTransform.localScale = Vector3.one * (1 + .12f * Mathf.Pow(1 - (left % 1), 6)); }

                else { ringIm.fillAmount = Ease((t - .4f) / .6f); }

                tip.color = new Color(.85f, .86f, .88f, Ease((t - .9f) / .5f));

                var kb = UnityEngine.InputSystem.Keyboard.current; if (t > .6f && kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) go();

            };

        }

        public void VictoryCard(bool win, string modeName, string mapName, int a, int b, int place, int total, int reward, System.Action again, System.Action menu)

        {

            var root2 = OpenScreen("tela_fim", out var fx).transform; var bgIm = root2.GetComponent<Image>(); var S = FBMatch.Stats;

            Color acc = win ? FBUI.Gold : FBUI.Red; int my = FBSession.team;

            var top = FBUI.Panel(root2, "faixa_topo", new Vector2(0, .58f), new Vector2(1, 1), Vector2.zero, Vector2.zero, new Color(acc.r * .55f, acc.g * .4f, acc.b * .3f, .75f)); top.sprite = TopGrad;

            var line = FBUI.Panel(root2, "linha", new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -232), new Vector2(0, 4), acc);

            string titleS = place > 0 ? (win ? "BOOYAH!" : $"#{place}") : win ? "VITÓRIA" : "DERROTA";

            var title = Tx(root2, titleS, 128, new Vector2(.5f, 1), new Vector2(0, -120), new Vector2(1000, 150), TextAnchor.MiddleCenter, true, acc);

            var eyebrow = Tx(root2, $"{modeName} · {mapName}".ToUpper(), 18, new Vector2(.5f, 1), new Vector2(0, -36), new Vector2(1000, 24), TextAnchor.MiddleCenter, false, new Color(.9f, .9f, .92f));

            // placar

            var score = FBUI.Rect(root2, "placar", new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -282), new Vector2(900, 70)); var scoreCg = score.gameObject.AddComponent<CanvasGroup>();

            Text sa = null, sb = null; Image ba = null, bb = null;

            if (place > 0) Tx(score, $"<b>#{place}</b> <size=22><color=#9aa3ad>DE {total}</color></size>", 52, new Vector2(.5f, .5f), Vector2.zero, new Vector2(600, 70), TextAnchor.MiddleCenter, true, Color.white);

            else

            {

                Color ca = new(.3f, .6f, 1), cb = new(1, .36f, .33f);

                Tx(score, $"{FBMatch.TEAM_SHORT[my]} <size=16><color=#9aa3ad>(VOCÊ)</color></size>", 22, new Vector2(.5f, .5f), new Vector2(-370, 18), new Vector2(300, 28), TextAnchor.MiddleRight, true, ca);

                Tx(score, FBMatch.TEAM_SHORT[1 - my], 22, new Vector2(.5f, .5f), new Vector2(370, 18), new Vector2(300, 28), TextAnchor.MiddleLeft, true, cb);

                sa = Tx(score, "0", 54, new Vector2(.5f, .5f), new Vector2(-122, 4), new Vector2(160, 64), TextAnchor.MiddleRight, true, Color.white);   // borda direita a 42 px do "×"

                Tx(score, "×", 30, new Vector2(.5f, .5f), new Vector2(0, 4), new Vector2(40, 40), TextAnchor.MiddleCenter, true, FBUI.Mut);

                sb = Tx(score, "0", 54, new Vector2(.5f, .5f), new Vector2(122, 4), new Vector2(160, 64), TextAnchor.MiddleLeft, true, Color.white);

                var bar = FBUI.Panel(score, "barra", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, -12), new Vector2(700, 8), new Color(1, 1, 1, .08f));

                ba = FBUI.Panel(bar.transform, "a", new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, Vector2.zero, ca); ba.rectTransform.pivot = new Vector2(0, .5f);

                bb = FBUI.Panel(bar.transform, "b", new Vector2(1, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero, cb); bb.rectTransform.pivot = new Vector2(1, .5f);

            }

            // seu desempenho (esquerda)

            var left = FBUI.Rect(root2, "desempenho", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-300, -110), new Vector2(520, 330)); var leftCg = left.gameObject.AddComponent<CanvasGroup>();

            Tx(left, "SEU DESEMPENHO", 18, new Vector2(0, 1), new Vector2(260, -12), new Vector2(520, 24), TextAnchor.MiddleLeft, true, acc);

            int accP = Mathf.RoundToInt(100f * S.hits / Mathf.Max(1, S.shots)); float kd = S.kills / Mathf.Max(1f, S.deaths);

            string[] L = { "ABATES", "MORTES", "K/D", "HEADSHOTS", "PRECISÃO %", "TIROS" }; float[] V = { S.kills, S.deaths, kd, S.heads, accP, S.shots }; string[] F = { "0", "0", "0.00", "0", "0", "0" };

            var tiles = new List<(RectTransform rt, Text v, float val, string fmt)>();

            for (int i = 0; i < 6; i++) { var rt = Tile(left, L[i], new Vector2(0, 1), new Vector2(85 + (i % 3) * 176, -84 - (i / 3) * 100), new Vector2(168, 90), i == 1 ? FBUI.Red : acc, out var vt); tiles.Add((rt, vt, V[i], F[i])); }

            // destaques (direita): MVP + 5 melhores da partida

            var right = FBUI.Rect(root2, "destaques", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(300, -110), new Vector2(520, 330)); var rightCg = right.gameObject.AddComponent<CanvasGroup>();

            Tx(right, "DESTAQUES DA PARTIDA", 18, new Vector2(0, 1), new Vector2(260, -12), new Vector2(520, 24), TextAnchor.MiddleLeft, true, acc);

            var rank = FBMatch.Tally.Select(kv => (name: kv.Key, kv.Value.team, kv.Value.k, kv.Value.d)).OrderByDescending(x => x.k).ThenBy(x => x.d).Take(5).ToList();

            if (rank.Count > 0)

            {

                var mvp = rank[0]; var mv = FBUI.Panel(right, "mvp", new Vector2(0, 1), new Vector2(0, 1), new Vector2(260, -78), new Vector2(520, 76), new Color(acc.r * .3f, acc.g * .25f, acc.b * .15f, .85f));

                Tx(mv.transform, "MVP", 15, new Vector2(0, .5f), new Vector2(46, 14), new Vector2(80, 20), TextAnchor.MiddleCenter, true, acc, false);

                Tx(mv.transform, "★", 34, new Vector2(0, .5f), new Vector2(46, -10), new Vector2(60, 40), TextAnchor.MiddleCenter, true, acc, false);

                Tx(mv.transform, mvp.name.ToUpper(), 30, new Vector2(0, .5f), new Vector2(100 + 170, 8), new Vector2(340, 36), TextAnchor.MiddleLeft, true, Color.white);

                Tx(mv.transform, $"{mvp.k} ABATES · {mvp.d} MORTES · {(mvp.team == my ? "SEU TIME" : "TIME INIMIGO")}", 14, new Vector2(0, .5f), new Vector2(100 + 170, -20), new Vector2(340, 18), TextAnchor.MiddleLeft, false, FBUI.Mut, false);

                for (int i = 1; i < rank.Count; i++)

                {

                    var r = rank[i]; var rowY = -130 - (i - 1) * 40;

                    var row = FBUI.Panel(right, "linha" + i, new Vector2(0, 1), new Vector2(0, 1), new Vector2(260, rowY), new Vector2(520, 34), new Color(1, 1, 1, i % 2 == 0 ? .04f : .07f));

                    Tx(row.transform, $"{i + 1}", 16, new Vector2(0, .5f), new Vector2(22, 0), new Vector2(30, 20), TextAnchor.MiddleCenter, true, FBUI.Mut, false);

                    Tx(row.transform, r.name.ToUpper(), 17, new Vector2(0, .5f), new Vector2(50 + 140, 0), new Vector2(280, 22), TextAnchor.MiddleLeft, true, r.team == my ? new Color(.55f, .78f, 1) : new Color(1, .6f, .58f), false);

                    Tx(row.transform, $"{r.k} / {r.d}", 17, new Vector2(1, .5f), new Vector2(-60, 0), new Vector2(100, 22), TextAnchor.MiddleRight, true, Color.white, false);

                }

            }

            // recompensas + botões (rodapé)

            var foot = FBUI.Rect(root2, "rodape", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 92), new Vector2(1100, 120)); var footCg = foot.gameObject.AddComponent<CanvasGroup>();

            var money = Tx(foot, "", 30, new Vector2(.5f, .5f), new Vector2(-380, 20), new Vector2(320, 40), TextAnchor.MiddleLeft, true, FBUI.Gold);

            Tx(foot, $"PASSE <b>+{(win ? 500 : 200)} XP</b>   ·   CRÉDITOS <b>${FBSession.money}</b>", 16, new Vector2(.5f, .5f), new Vector2(-380, -14), new Vector2(320, 22), TextAnchor.MiddleLeft, false, new Color(.85f, .86f, .88f), false);

            bool done = false; System.Action go = () => { if (done) return; done = true; again(); };

            FBUI.Button(foot, "REVANCHE  [R]", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(170, 10), new Vector2(300, 66), go, true);

            FBUI.Button(foot, "MENU PRINCIPAL", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(440, 10), new Vector2(220, 52), () => menu());

            fx.tick = t =>

            {

                bgIm.color = new Color(.01f, .01f, .02f, .78f * Ease(t / .6f));

                float tk = EaseBack(t / .55f); title.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.8f, 1, tk); title.color = new Color(acc.r, acc.g, acc.b, Ease(t / .3f));

                line.rectTransform.sizeDelta = new Vector2(900 * Ease((t - .2f) / .6f), 4); eyebrow.color = new Color(.9f, .9f, .92f, Ease((t - .3f) / .4f));

                scoreCg.alpha = Ease((t - .45f) / .4f); float sk = Ease((t - .55f) / 1.1f);

                if (sa) { sa.text = Mathf.RoundToInt(a * sk).ToString(); sb.text = Mathf.RoundToInt(b * sk).ToString(); float tot = Mathf.Max(1, a + b); ba.rectTransform.sizeDelta = new Vector2(700 * a / tot * sk, 0); bb.rectTransform.sizeDelta = new Vector2(700 * b / tot * sk, 0); }

                float lk = Ease((t - .7f) / .5f); leftCg.alpha = lk; left.anchoredPosition = new Vector2(Mathf.Lerp(-380, -300, lk), -110);

                float rk = Ease((t - .85f) / .5f); rightCg.alpha = rk; right.anchoredPosition = new Vector2(Mathf.Lerp(380, 300, rk), -110);

                for (int i = 0; i < tiles.Count; i++) { float k = Ease((t - .9f - i * .07f) / .7f); tiles[i].v.text = Ptbr(tiles[i].val * k, tiles[i].fmt); tiles[i].rt.localScale = Vector3.one * Mathf.Lerp(.86f, 1, EaseBack(k)); }

                float fk = Ease((t - 1.2f) / .5f); footCg.alpha = fk; footCg.interactable = footCg.blocksRaycasts = t > 1.3f;

                money.text = $"+${Mathf.RoundToInt(reward * Ease((t - 1.3f) / 1.2f))}";

                var kb = UnityEngine.InputSystem.Keyboard.current; if (t > 1.3f && kb != null && kb.rKey.wasPressedThisFrame) go();

            };

        }

        public void Pause(bool on, System.Action menu)

        {

            if (!on) { if (pausePanel) Destroy(pausePanel); pausePanel = null; return; }

            var bg = FBUI.Panel(root, "pausa", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, .6f)); pausePanel = bg.gameObject;

            FBUI.Label(bg.transform, "PAUSADO", 60, C, C, new Vector2(0, 160), new Vector2(600, 80), TextAnchor.MiddleCenter, true, FBUI.Gold);

            FBUI.Button(bg.transform, "CONTINUAR", C, C, new Vector2(0, 50), new Vector2(320, 56), () => { FBMatch.Paused = false; Pause(false, null); if (!FBInput.Touch) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } }, true);

            FBUI.Button(bg.transform, "AJUSTES", C, C, new Vector2(0, -16), new Vector2(320, 50), () => FBSettings.Open(bg.transform));

            FBUI.Button(bg.transform, "MENU PRINCIPAL", C, C, new Vector2(0, -76), new Vector2(320, 50), () => { FBMatch.Paused = false; menu(); });

        }



        // [parceiro 01/11] medidor de tempo por sistema (FBTempo): com FBTempo.On soma o custo do Update do HUD

        static readonly int __fbt_Update = FBTempo.Id("FBHUD.Update");

        void Update()

        {

            if (!FBTempo.On) { Update__fbt(); return; }

            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);

        }

        void Update__fbt()

        {

            float dt = Time.deltaTime; var pl = FBPlayer.I; var cam = Camera.main;

            canvas.enabled = !(FBMatch.I && FBMatch.I.mode != null && FBMatch.I.mode.role) || FBMatch.Paused || CardOpen;

            if (pl)

            {

                var ch = pl.ch; var w = pl.wu.Cur;

                float hpN = Mathf.Clamp01(ch.hp / ch.maxHp); hpBar.fillAmount = hpN; hpLagV = Mathf.Max(hpN, Mathf.MoveTowards(hpLagV > 1 ? 1 : hpLagV, hpN, dt * .6f)); hpLag.fillAmount = hpLagV;

                hpBar.color = FBMed.Healing ? new Color(.61f, .9f, .69f) : ch.knocked ? new Color(1, .45f, .2f) : ch.hp < 30 ? new Color(1, .29f, .23f) : HPC; hpT.text = Mathf.CeilToInt(Mathf.Max(0, ch.hp)).ToString();

                float ar = ch.vest != null ? ch.vest.dur / ch.vest.max : ch.armor / 100f; armorBar.fillAmount = Mathf.Clamp01(ar);

                armorT.text = (ch.vest != null ? FBLoot.ArmorLabel(ch.vest.key) : "") + (ch.helmetFF != null ? " · " + FBLoot.ArmorLabel(ch.helmetFF.key) : "");

                Weapon(pl, w);

                bool show = w != null && ch.alive && !ch.sprint && !FBScopes.Scoped && !FBScopes.Optic && !pl.Driving && !CardOpen && !pl.FreeLooking && !(pl.firstPerson && ch.aiming);   // r45: mirando em 1ª pessoa quem mira é a mira da arma   // câmera livre: a mira some (a arma não aponta para lá)

                float spread = w != null ? pl.wu.recoil.Spread(w, new FBRecoil.State { aiming = ch.aiming, crouch = ch.crouch, moving = ch.velocity.Flat().sqrMagnitude > .25f, air = !ch.grounded, zoom = 1, grip = 1 }) : 0;

                // setCrosshair/setBlocked do webui.js: abertura em px do web (×K), traços 2×9 px a partir de ±gap; mira bloqueada = vermelho com ✕

                float gap = (pl.CrossGapPx + 4.5f) * K; bool blk = pl.AimBlocked && show; var cc = blk ? Hex(0xff3b30) : Color.white;

                for (int i = 0; i < 4; i++) { var im = cross[i].GetComponent<Image>(); im.color = cc; cross[i].sizeDelta = i < 2 ? new Vector2(2 * K, 9 * K) : new Vector2(9 * K, 2 * K); }

                crossDot.color = cc;

                if (!impX) { impX = Img("impacto", C, C, Vector2.zero, new Vector2(18, 18), Color.white); impX.sprite = FBOptic.ImpactSprite; impX.gameObject.SetActive(false); }

                impX.color = cc;

                // r60b: aqui só cor, tamanho e QUEM pode aparecer. Quem decide se é o retículo ou o ✕, e onde, é o CrossLayout,

                // chamado pelo PlaceImpact no fim do LateUpdate do FBPlayer (câmera final do quadro, sem um quadro de atraso).

                crossGap = gap; crossVis = show; crossDotVis = show || (w == null && ch.alive && !pl.firstPerson);

                CrossLayout();

                Minimap(pl, cam);

            }

            hitT -= dt; killT -= dt; var hc = hitMark.color; hc.a = Mathf.Clamp01(hitT * 6); hitMark.color = hc; hitMark.rectTransform.localScale = Vector3.one * hitBig * (1 + Mathf.Max(0, hitT) * 2);

            // dano: pico pela intensidade e decai; vida crítica (< 30 %) deixa a borda pulsando devagar enquanto durar

            dmgT = Mathf.Max(0, dmgT - dt * .9f); var me0 = FBMatch.Player; float crit = me0 && me0.alive && me0.hp < me0.maxHp * .3f ? Mathf.Lerp(.7f, .38f, me0.hp / (me0.maxHp * .3f)) * (.72f + .28f * Mathf.Sin(Time.time * 4.2f)) : 0;

            vignette.color = new Color(.55f, 0, .02f, Mathf.Clamp01(Mathf.Max(Mathf.Clamp01(dmgT) * .85f, crit)));

            dmgDirT -= dt; dmgArrow.color = new Color(1, .2f, .15f, Mathf.Clamp01(dmgDirT));

            centerTTL -= dt; if (centerTTL <= 0) centerT.text = "";

            mtoastT -= dt; if (mtoastT <= 0) mtoastT2.text = "";

            toastT -= dt; if (toastT <= 0 && toastRT.gameObject.activeSelf) toastRT.gameObject.SetActive(false);

            bannerT -= dt; if (bannerT <= 0 && bannerRT.gameObject.activeSelf) bannerRT.gameObject.SetActive(false);

            wipeT -= dt; if (wipeT <= 0 && wipeRT.gameObject.activeSelf) wipeRT.gameObject.SetActive(false);

            countTT -= dt; countT.color = new Color(countT.color.r, countT.color.g, countT.color.b, Mathf.Clamp01(countTT * 2)); countT.rectTransform.localScale = Vector3.one * (1 + Mathf.Max(0, countTT - .6f));

            killcamTT -= dt; if (killcamTT <= 0) killcamT.text = "";

            cashPlusT -= dt; if (cashPlusT <= 0) cashPlus.text = "";

            for (int i = 0; i < feed.Count; i++) { var (t, ttl) = feed[i]; ttl -= dt; feed[i] = (t, ttl); var c = t.color; c.a = Mathf.Clamp01(ttl); t.color = c; }

            for (int i = floats.Count - 1; i >= 0; i--)

            {

                var (t, p, t0) = floats[i]; float k = (Time.time - t0) / .9f; if (k >= 1 || !cam) { Destroy(t.gameObject); floats.RemoveAt(i); continue; }

                // ao lado do alvo (não na cara): desloca para a direita/cima em tela, sobe devagar, pequeno "pop" no início

                var sp = cam.WorldToScreenPoint(p); t.gameObject.SetActive(sp.z > 0); float u = Screen.height / 1080f;

                if (sp.z > 0) t.rectTransform.position = sp + new Vector3(46 + k * 10, 34 + k * 38, 0) * u;

                t.rectTransform.localScale = Vector3.one * (k < .12f ? Mathf.Lerp(1.35f, 1, k / .12f) : 1); var c = t.color; c.a = 1 - k * k; t.color = c;

            }

            if (FBMatch.I && (FBInput.Read().map || (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.mKey.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.M))) { FBMatchMap.Toggle(); }

            boardRT.gameObject.SetActive(FBInput.Read().scoreboard && FBMatch.I && !CardOpen); if (boardRT.gameObject.activeSelf) Board();

            Squad(); Tags(cam);

        }

        void Board()

        {

            var sb = new System.Text.StringBuilder(); sb.Append($"<size=22><b>{FBMatch.I.MapName} · {(FBMatch.I.mode?.name ?? "")}</b></size>\n\n");

            foreach (var t in FBMatch.FFA ? new[] { -1 } : new[] { 0, 1 })

            {

                if (t >= 0) sb.Append($"<color={(t == 0 ? "#8cc8ff" : "#ff8a80")}><b>{FBMatch.TEAM_NAME[t]}</b></color>\n");

                foreach (var a in FBMatch.Actors().Where(a => t < 0 || a.team == t).OrderByDescending(a => a.isPlayer ? FBMatch.Stats.kills : a.kills))

                    sb.Append($"{(a.isPlayer ? "<color=#f5aa17>" : "")}{(a.alive ? "" : "✖ ")}{a.Name}{(a.isPlayer ? "</color>" : "")}   <b>{(a.isPlayer ? FBMatch.Stats.kills : a.kills)}</b> / {(a.isPlayer ? FBMatch.Stats.deaths : a.deaths)}\n");

                sb.Append('\n');

            }

            boardT.text = sb.ToString();

        }

        // arma na mão: silhueta (sprite do pak "ui", weapons_hud), nome, AUTO/SEMI, munição "N / reserva" e o pente (uma marca por bala, até 40)

        void Weapon(FBPlayer pl, WeaponDef w)

        {

            var ch = pl.ch;

            // o bloco antigo (silhueta/nome/munição/pente) e a linha do kit foram substituídos pelo FBHudWeapons

            wSil.enabled = false; wT.text = fmT.text = resT.text = ammoT.text = ""; magRT.gameObject.SetActive(false); medRT.gameObject.SetActive(false); return;

#pragma warning disable CS0162

            if (pl.Driving) { wSil.enabled = false; wT.text = fmT.text = resT.text = ammoT.text = ""; magRT.gameObject.SetActive(false); wId = null; return; }   // no veículo: velocímetro no lugar

            if (w == null)

            {

                wSil.enabled = false; wT.text = ch.alive ? "DESARMADO" : ""; fmT.text = ""; resT.text = ""; magRT.gameObject.SetActive(false);

                bool econ = FBMatch.I && FBMatch.I.mode != null && FBMatch.I.mode.GetType().Name.Contains("4"); ammoT.text = ""; wId = null; return;

            }

            if (wId != w.id) { wId = w.id; var tex = Resources.Load<Texture2D>("UI/WeaponSil/" + w.id); wSil.texture = tex; wSil.enabled = tex; if (tex) { float h = 28, ww = Mathf.Min(130, h * tex.width / (float)tex.height); wSil.rectTransform.sizeDelta = new Vector2(ww * K, (ww < 130 ? h : 130 * tex.height / (float)tex.width) * K); } wT.text = w.name.ToUpperInvariant(); fmT.text = w.auto ? "AUTO" : "SEMI"; magN = 0; }

            int ammo = pl.wu.Ammo; ammoT.text = pl.wu.reloadT > 0 ? "···" : ammo.ToString(); resT.text = "/ " + pl.wu.Reserve;

            ammoT.rectTransform.anchoredPosition = new Vector2(-(28 * K + resT.preferredWidth + 5 * K), 47 * K);

            float nameW = Mathf.Max(wT.preferredWidth, fmT.preferredWidth); wSil.rectTransform.anchoredPosition = new Vector2(-(29 * K + nameW + 10 * K), 79 * K);

            magN = Mathf.Max(magN, ammo); int n = Mathf.Min(40, magN), f = magN > 0 ? Mathf.RoundToInt(ammo / (float)magN * n) : 0; bool low = magN > 0 && ammo <= Mathf.Max(2, magN * .2f);

            magRT.gameObject.SetActive(n > 0); while (magPips.Count < n) { var im = Box(magRT, "bala", new Vector2(1, .5f), new Vector2(1, .5f), 0, 0, 5, 3, HW); magPips.Add(im); }

            float pw = Mathf.Min(5, (150 - 2 * (n - 1)) / Mathf.Max(1f, n));

            for (int i = 0; i < magPips.Count; i++) { var im = magPips[i]; bool on = i < n; im.gameObject.SetActive(on); if (!on) continue; im.rectTransform.sizeDelta = new Vector2(pw * K, 3 * K); im.rectTransform.anchoredPosition = new Vector2(-i * (pw + 2) * K, 0); im.color = i < f ? (low ? Hex(0xff6b5e) : HW) : HL; }

            ammoT.color = HW;

#pragma warning restore CS0162

        }

        // número/cor do esquadrão (squadSlot do main.js): o jogador é o 1; os aliados ganham número na ordem em que aparecem

        static (int n, Color col) Slot(FBCharacter a)

        {

            if (squadMatch != FBMatch.I) { squadMatch = FBMatch.I; squadNo.Clear(); }

            if (a && a.isPlayer && !squadNo.ContainsKey(a)) squadNo[a] = 1; if (!squadNo.ContainsKey(a)) squadNo[a] = squadNo.Count + 1 + (squadNo.Values.Contains(1) ? 0 : 1);

            int k = squadNo[a]; return (k, SQUAD_COL[(k - 1) % SQUAD_COL.Length]);

        }

        static bool Ally(FBCharacter a) => !FBMatch.FFA && a && a.team == FBSession.team && !a.isPlayer && !FBMatch.Civs.Contains(a);

        // painel do esquadrão: número/cor, nome, vida, distância; caído = riscado

        void Squad()

        {

            var me = FBMatch.Player; if (!me || !FBMatch.I) { squadRT.gameObject.SetActive(false); return; }

            var rows = FBMatch.Actors().Where(Ally).ToList(); if (rows.Count == 0) { squadRT.gameObject.SetActive(false); return; }

            Slot(me); rows.Insert(0, me); rows = rows.OrderBy(a => Slot(a).n).Take(10).ToList(); squadRT.gameObject.SetActive(true);   // [parceiro 01/11] 10 linhas (time inteiro do 10x10; antes 8)

            int alive = rows.Count(a => a.alive); squadCt.text = $"<color=#ffffff>{alive}</color>/{rows.Count} VIVOS" + (alive < rows.Count ? $" · <color=#ff6a5a>{rows.Count - alive} ✖</color>" : "");

            while (sqRows.Count < rows.Count)

            {

                int i = sqRows.Count; var bg = Box(squadRT, "r", new Vector2(0, 1), new Vector2(0, 1), 0, -(17 + i * 29), 200, 26, Color.white); bg.sprite = GradSprite; bg.color = new Color(0, 0, 0, 1);

                var em = Box(bg.transform, "n", new Vector2(0, .5f), new Vector2(0, .5f), 3, 0, 18, 18, Color.white); var num = T(em.transform, "", 12, C, C, 0, 0, 18, 18, TextAnchor.MiddleCenter, true, Color.white);

                var nm = T(bg.transform, "", 12, new Vector2(0, 1), new Vector2(0, 1), 27, -3, 133, 14, TextAnchor.MiddleLeft, true, Color.white); nm.horizontalOverflow = HorizontalWrapMode.Wrap;

                var bb = Box(bg.transform, "vida", new Vector2(0, 1), new Vector2(0, 1), 27, -19, 133, 3, new Color(1, 1, 1, .18f)); var bar = Fill(bb.transform, Hex(0xf2f2f2));

                var dt = T(bg.transform, "", 10, new Vector2(1, .5f), new Vector2(1, .5f), -6, 0, 28, 12, TextAnchor.MiddleRight, true, new Color(1, 1, 1, .75f));

                sqRows.Add((bg.rectTransform, em, num, nm, bar, dt, bg.gameObject.AddComponent<CanvasGroup>()));

            }

            for (int i = 0; i < sqRows.Count; i++)

            {

                var r = sqRows[i]; bool on = i < rows.Count; r.rt.gameObject.SetActive(on); if (!on) continue; var a = rows[i]; var sl = Slot(a);

                r.em.color = a.alive ? sl.col : new Color(.33f, .33f, .33f); r.num.text = sl.n.ToString(); r.nm.text = a.isPlayer ? (FBSession.online || FBSession.nameChosen ? FBSession.name : "Você") : a.Name;

                r.nm.color = !a.alive ? new Color(.73f, .73f, .73f) : a.isPlayer ? Hex(0xffd56a) : Color.white; r.cg.alpha = a.alive ? 1 : .5f;

                r.bar.fillAmount = a.alive ? Mathf.Clamp01(a.hp / Mathf.Max(1, a.maxHp)) : 0; r.bar.color = a.alive && a.hp < 30 ? Hex(0xff4a3a) : Hex(0xf2f2f2);

                r.dt.text = a.isPlayer ? "" : !a.alive ? "✖" : Mathf.RoundToInt(Vector3.Distance(a.Pos, me.Pos)) + "m";

            }

        }

        // nomes em cima dos personagens (updateTags do webui.js): crachá com o número do esquadrão + nome + vida; aliado fora da tela

        // (ou atrás) vira marcador preso na borda com a distância; inimigo só enquanto marcado pelo acerto

        void Tags(Camera cam)

        {

            int n = 0; if (!cam || !FBMatch.I) { foreach (var t in tagW) t.rt.gameObject.SetActive(false); return; }

            var cr = (RectTransform)canvas.transform; Vector2 cs = cr.rect.size;

            // rodada 38 (usuário: "os nomes ficam constantemente visíveis, causando muita poluição visual"): o crachá completo (nome, distância,

            // vida) só aparece no aliado que está NA MIRA / no centro da visão — raio da mira passando pelo corpo dele (tolerância cresce com a

            // distância), sem parede no meio — e fica 0,5 s depois de tirar a mira (não pisca). Fora isso, nada na tela; aliado CAÍDO mantém um

            // marcador discreto "CAÍDO · 12m" (preso na borda se estiver fora da tela) para ser reanimado.

            var ray = cam.ViewportPointToRay(new Vector3(.5f, .5f, 0)); FBCharacter look = null; float lookT = 1e9f;

            foreach (var a in FBMatch.Actors())

            {

                if (a.isPlayer || !a.alive || !a.Head || !Ally(a)) continue;

                Vector3 p0 = a.Pos + Vector3.up * .15f * a.ScaleK, p1 = a.Head.position; float t = Vector3.Dot((p0 + p1) * .5f - ray.origin, ray.direction); if (t < .5f || t > 250) continue;

                float seg = SegRayDist(p0, p1, ray, out float tr); float tol = (.42f + tr * .006f) * a.ScaleK;

                if (seg > tol || tr >= lookT) continue;

                var chest = (p0 + p1) * .5f; var to = chest - ray.origin; if (FBWorld.RayHit(ray.origin, to.normalized, to.magnitude - .6f * a.ScaleK)) continue;   // parede no meio

                look = a; lookT = tr;

            }

            if (look) { tagFocus = look; tagFocusT = Time.unscaledTime; }

            foreach (var a in FBMatch.Actors())

            {

                if (a.isPlayer || !a.alive || !a.Head) continue; bool ally = Ally(a); if (!ally) continue;   // inimigo nunca mostra nome/vida (nem ao levar tiro)

                bool downed = a.knocked;

                var wp = a.Head.position + Vector3.up * .55f * a.ScaleK; float dist = Vector3.Distance(wp, cam.transform.position);

                var vp = cam.WorldToViewportPoint(wp); float x = vp.x * 2 - 1, y = vp.y * 2 - 1; bool behind = vp.z < 0, edge = behind || Mathf.Abs(x) > .96f || Mathf.Abs(y) > .94f;

                if (edge) { if (behind) { x = -x; y = -y; if (Mathf.Abs(x) < .05f && Mathf.Abs(y) < .05f) y = -1; } float k = Mathf.Max(Mathf.Abs(x) / .94f, Mathf.Abs(y) / .86f, 1e-3f); x /= k; y /= k; }

                if (n >= tagW.Count)

                {

                    var rt = R(root, "cracha", C, new Vector2(.5f, 0), 0, 0, 160, 22);

                    var em = Box(rt, "n", new Vector2(.5f, 1), new Vector2(.5f, 1), 0, 0, 15, 15, Color.white); var num = T(em.transform, "", 10, C, C, 0, 0, 15, 15, TextAnchor.MiddleCenter, true, Color.white);

                    var nm = T(rt, "", 11, new Vector2(.5f, 1), new Vector2(0, 1), 0, 0, 200, 15, TextAnchor.MiddleLeft, true, Color.white); nm.supportRichText = true;

                    var bb = Box(rt, "vida", new Vector2(.5f, 0), new Vector2(.5f, 0), 0, 0, 64, 4, new Color(0, 0, 0, .55f)); var bar = Fill(bb.transform, Color.white);

                    tagW.Add((rt, em, num, nm, bb.rectTransform, bar));

                }

                var tg = tagW[n++]; tg.rt.gameObject.SetActive(true); tg.rt.anchoredPosition = new Vector2(x * cs.x / 2, y * cs.y / 2 - (edge ? 11 * K : 0));

                var sl = ally ? Slot(a) : (0, Color.clear); int m = Mathf.RoundToInt(dist);

                tg.em.gameObject.SetActive(ally); tg.em.color = sl.Item2; tg.num.text = sl.Item1.ToString();

                tg.nm.fontSize = Mathf.RoundToInt((edge ? 10 : dist > 40 ? 10 : 11) * K);

                tg.nm.text = edge ? (downed ? $"CAÍDO · {m}m" : $"{m}m") : a.Name + (downed ? " · CAÍDO" : "") + (ally && dist > 15 ? $" <size={Mathf.RoundToInt(10 * K)}><color=#cfe6ffbf>{m}m</color></size>" : "");

                tg.nm.color = downed ? Hex(0xff8a80) : Hex(0xcfe6ff);

                float nw = tg.nm.preferredWidth, ew = ally ? 19 * K : 0, tw = nw + ew; tg.em.rectTransform.anchoredPosition = new Vector2(-tw / 2 + 7.5f * K, 0); tg.nm.rectTransform.anchoredPosition = new Vector2(-tw / 2 + ew, 0);

                tg.barBg.gameObject.SetActive(!edge); tg.bar.fillAmount = Mathf.Clamp01(a.hp / 100f); tg.bar.color = a.hp < 30 ? Hex(0xff4a3a) : sl.Item2;

                var cg = tg.rt.GetOrAdd<CanvasGroup>(); cg.alpha = edge ? .85f : 1f;   // nomes de amigos sempre visíveis

                if (!edge) { tg.em.rectTransform.anchoredPosition += new Vector2(0, 0); }

            }

            for (int i = n; i < tagW.Count; i++) tagW[i].rt.gameObject.SetActive(false);

        }

        FBCharacter tagFocus; float tagFocusT = -9;

        // distância entre o segmento a–b e o raio (e o t do ponto mais próximo ao longo do raio)

        static float SegRayDist(Vector3 a, Vector3 b, Ray r, out float tr)

        {

            Vector3 u = b - a, v = r.direction, w = a - r.origin; float A = Vector3.Dot(u, u), B = Vector3.Dot(u, v), Cc = Vector3.Dot(v, v), D = Vector3.Dot(u, w), E = Vector3.Dot(v, w), den = A * Cc - B * B;

            float sc = den > 1e-6f ? Mathf.Clamp01((B * E - Cc * D) / den) : 0; tr = Mathf.Max(0, (E + B * sc) / Cc); sc = A > 1e-6f ? Mathf.Clamp01(Vector3.Dot(r.origin + v * tr - a, u) / A) : 0;

            return Vector3.Distance(a + u * sc, r.origin + v * tr);

        }

        // minimapa (drawMini do main.js): a frente da câmera aponta para cima; jogador 14 px abaixo do centro

        void Minimap(FBPlayer pl, Camera cam)

        {

            float yaw = pl.camYaw, sc = MS * K; var me = pl.ch.Pos; var c0 = Vector2.zero; var rot = Quaternion.Euler(0, 0, yaw);

            if (FBMinimap.Tex)

            {

                if (!miniMap.gameObject.activeSelf) { miniMap.gameObject.SetActive(true); miniMap.texture = FBMinimap.Tex; miniMap.transform.SetAsFirstSibling(); }

                var uv = FBMinimap.UV(me); miniMap.rectTransform.sizeDelta = Vector2.one * FBMinimap.Size * sc; miniMap.rectTransform.localRotation = rot;

                miniMap.rectTransform.anchoredPosition = c0 + (Vector2)(rot * new Vector2((.5f - uv.x) * FBMinimap.Size * sc, (.5f - uv.y) * FBMinimap.Size * sc));

            }

            if (cam) { float hf = Mathf.Atan(Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) * cam.aspect) * Mathf.Rad2Deg; var ci = cone.GetComponent<Image>(); ci.fillAmount = hf * 2 / 360f; cone.localRotation = Quaternion.Euler(0, 0, hf); }

            Vector2 P(Vector3 p) { var d = (p - me) * sc; return c0 + (Vector2)(rot * new Vector2(d.x, d.z)); }

            Vector2 Edge(Vector2 v, float m = 9) { float lim = (95 - 6 - m) * K; return new Vector2(Mathf.Clamp(v.x, -lim, lim), Mathf.Clamp(v.y, -lim, lim)); }

            int n = 0, na = 0;

            Image Dot(Vector2 v, Color c, float size, bool diamond = false)

            {

                if (n >= miniDots.Count) { var im = Box(miniClip, "p", C, C, 0, 0, 8, 8, Color.white); var ol = im.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(0, 0, 0, .85f); ol.effectDistance = new Vector2(1.2f, -1.2f); miniDots.Add(im); }

                var dd = miniDots[n++]; dd.gameObject.SetActive(true); dd.sprite = diamond ? null : DiscSprite; dd.rectTransform.anchoredPosition = v; dd.color = c; dd.rectTransform.sizeDelta = Vector2.one * size * K; dd.rectTransform.localRotation = Quaternion.Euler(0, 0, diamond ? 45 : 0); return dd;

            }

            var z = FBMatch.I?.mode?.Zone;

            zoneRing.gameObject.SetActive(z != null); if (z != null) { zoneRing.rectTransform.anchoredPosition = P(z.c); zoneRing.rectTransform.sizeDelta = Vector2.one * Mathf.Max(.2f, z.r * 2 * sc); }

            bool hasNext = z != null && z.to != null;

            zoneNext.gameObject.SetActive(hasNext);

            if (hasNext)

            {

                var (nc, nr) = z.to.Value;

                zoneNext.rectTransform.anchoredPosition = P(nc);

                zoneNext.rectTransform.sizeDelta = Vector2.one * Mathf.Max(.2f, nr * 2 * sc);

                float pulse = Mathf.PingPong(Time.time * 2.5f, 0.4f) + 0.6f;

                zoneNext.color = new Color(1f, 0.82f, 0.25f, pulse);

            }

            if (heliScanRing != null)

            {

                bool scanning = FBHeliRecon.IsScanning;

                heliScanRing.gameObject.SetActive(scanning);

                if (scanning)

                {

                    heliScanRing.rectTransform.anchoredPosition = P(FBHeliRecon.ScanPosition);

                    heliScanRing.rectTransform.sizeDelta = Vector2.one * Mathf.Max(.2f, FBHeliRecon.ScanRadius * 2 * sc);

                    float pulse = Mathf.PingPong(Time.time * 2.5f, 0.4f) + 0.6f;

                    heliScanRing.color = new Color(1f, 0.5f, 0.1f, pulse * 0.9f);

                }

            }

            var pts = FBMatch.I?.mode?.Points; if (pts != null) foreach (var p in pts) Dot(P(p), p.w < .5f ? Hex(0xd8d8d8) : p.w < 1.5f ? Hex(0x2e7aff) : p.w < 2.5f ? Hex(0xf23a45) : Hex(0xf5aa17), 10, true);   // Tático: neutro / nosso / deles / disputado

            foreach (var a in FBMatch.Actors())

            {

                if (a.isPlayer || !a.alive) continue;

                if (Ally(a))

                {

                    if (na >= miniArrows.Count) { var im = Box(miniClip, "aliado", C, C, 0, 0, 13.2f * .85f / 1.1f, 15.4f * .85f / 1.1f, Hex(0x3d9bff)); im.sprite = ArrowSpr; var ol = im.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(0, 0, 0, .85f); ol.effectDistance = new Vector2(1.2f, -1.2f); miniArrows.Add(im); }

                    var ar = miniArrows[na++]; ar.gameObject.SetActive(true); ar.rectTransform.anchoredPosition = Edge(P(a.Pos)); ar.rectTransform.localRotation = Quaternion.Euler(0, 0, yaw - a.Yaw); continue;

                }

                if (FBMatch.Civs.Contains(a)) continue;

                bool tagged = FBHeliRecon.IsTagged(a);

                if (a.lastShot >= 3 && !tagged) continue;

                var v = P(a.Pos); bool outE = Mathf.Abs(v.x) > (95 - 14) * K || Mathf.Abs(v.y) > (95 - 14) * K;

                if (tagged)

                {

                    float pulse = Mathf.PingPong(Time.time * 3f, 0.3f) + 0.7f;

                    Dot(Edge(v, 7), new Color(1f, 0.15f, 0.15f, pulse), outE ? 9 : 12, true);

                }

                else

                {

                    Dot(Edge(v, 7), new Color(1, .23f, .19f, 1 - a.lastShot / 3), outE ? 8 : 10);

                }

            }

            for (int i = n; i < miniDots.Count; i++) miniDots[i].gameObject.SetActive(false);

            for (int i = na; i < miniArrows.Count; i++) miniArrows[i].gameObject.SetActive(false);

            // norte: na borda, na direção de -Z (o "z − 500" do web)

            // bússola no anel: N (dourado) e L/S/O girando com a visão

            var nd = (P(me + Vector3.back * 500) - c0).normalized; float lim2 = (95 - 6 - 10) * K;

            Vector2 OnSquare(Vector2 d) => d / Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y), 1e-4f) * lim2;   // direção → ponto na borda quadrada

            miniN.anchoredPosition = OnSquare(nd);

            for (int i = 0; i < compass.Count; i++) compass[i].anchoredPosition = OnSquare(Quaternion.Euler(0, 0, -90 * (i + 1)) * nd);

        }

    }



    // foto do mapa vista de cima para o minimapa (captureMapRadar): câmera ortográfica uma vez no carregamento

    public static class FBMinimap

    {

        public static Texture2D Tex; public static float Size = 400; static Vector3 center; static Bounds mapBounds;

        public static Vector3 Center => center;

        public static Bounds MapBounds => mapBounds;

        public static Vector2 UV(Vector3 p) => new Vector2((p.x - center.x) / Size + .5f, (p.z - center.z) / Size + .5f);

        public static void Capture()

        {

            Tex = null; var cols = Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Where(c => c.gameObject.layer == FBWorld.LMap).ToList(); if (cols.Count == 0) return;

            var b = cols[0].bounds; foreach (var c in cols) b.Encapsulate(c.bounds); center = b.center; Size = Mathf.Min(Mathf.Max(b.size.x, b.size.z), 900); mapBounds = b;

            int R = FBQuality.Mobile ? 1024 : 2048;   // antes 512 px para até 900 m (≈1,8 m por pixel: borrado)

            // luz neutra de dia (a partida pode começar à noite/chuva: o mapa saía escuro), sem neblina e sem sombras

            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None); var saved = lights.Select(l => (l, l.enabled, l.intensity, l.transform.rotation, l.shadows)).ToList();

            var amb = (RenderSettings.ambientMode, RenderSettings.ambientLight, RenderSettings.fog, RenderSettings.ambientIntensity);

            foreach (var l in lights) { if (l.type == LightType.Directional) { l.enabled = true; l.intensity = 1.1f; l.transform.rotation = Quaternion.Euler(70, -30, 0); l.shadows = LightShadows.None; } else l.enabled = false; }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.62f, .64f, .66f); RenderSettings.fog = false;

            var g = new GameObject("CamRadar"); var cam = g.AddComponent<Camera>(); cam.orthographic = true; cam.orthographicSize = Size / 2; cam.transform.SetPositionAndRotation(new Vector3(center.x, b.max.y + 50, center.z), Quaternion.Euler(90, 0, 0));

            cam.farClipPlane = b.size.y + 200; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.1f, .12f, .14f); cam.cullingMask = 1 << FBWorld.LMap | 1; cam.allowHDR = false;

            var ucd = g.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); ucd.renderPostProcessing = false; ucd.renderShadows = false;

            var rt = new RenderTexture(R, R, 24); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; Tex = new Texture2D(R, R, TextureFormat.RGB24, true) { anisoLevel = 4, wrapMode = TextureWrapMode.Clamp };

            Tex.ReadPixels(new Rect(0, 0, R, R), 0, 0); RenderTexture.active = null;

            // tratamento: contraste leve e um pouco menos de saturação (leitura limpa por cima da foto)

            var px = Tex.GetPixels32();

            for (int i = 0; i < px.Length; i++) { var c = px[i]; float l = (c.r * .3f + c.g * .59f + c.b * .11f) / 255f; Color o = Color.Lerp(new Color(l, l, l), new Color(c.r / 255f, c.g / 255f, c.b / 255f), .8f); o = (o - Color.gray * .5f) * 1.18f + Color.gray * .5f; o.a = 1; px[i] = o; }

            Tex.SetPixels32(px); Tex.Apply(true, true);

            cam.targetTexture = null; Object.Destroy(rt); Object.Destroy(g);

            foreach (var (l, en, it, rot, sh) in saved) if (l) { l.enabled = en; l.intensity = it; l.transform.rotation = rot; l.shadows = sh; }

            (RenderSettings.ambientMode, RenderSettings.ambientLight, RenderSettings.fog, RenderSettings.ambientIntensity) = amb;

        }

    }

}

