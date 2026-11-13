// r62 (07/11/2026) PÁGINAS DO LOBBY REFEITAS — pedido: "ainda vejo problemas com as abas de equipamentos, escolha de personagens, emotes,
// passe, arsenal ... não estou gostando desse background cinza nos cards ... tudo preso à tela visível ... botão de sair do jogo ...
// retrabalhar toda a parte de jogar online (lista de amigos, formar squad contra outros ou contra bots) ... cores da bandeira".
//
//  - Todas as páginas montam o conteúdo num quadro fixo de 1548×808 centrado (Box1548) abaixo da barra de 64 px; com o CanvasScaler em
//    EXPAND (FBUI.Canvas) a tela lógica nunca é menor que 1600×900, então nada sai da tela em 21:9, 4:3 ou janela estreita.
//  - SQUAD (Squad): cartões dos 4 lugares do squad (você, amigos, "+ convidar"), escolha CONTRA JOGADORES / CONTRA BOTS, partida
//    (modo/mapa) e botão de iniciar/pronto; à direita o painel SOCIAL: seu código, servidor, abas AMIGOS / ONLINE AGORA / CONVITES,
//    adicionar amigo pelo código, convidar, aceitar/recusar. Atualiza sozinho (FBVersionWatch → FBLobbyNet.version).
//  - EMOTES: catálogo em grade com miniaturas (filtro por categoria), roda com os 8 equipados em círculo, dança da vitória.
//  - PASSE: faixa de 30 níveis com rolagem horizontal centrada no nível atual, cartões de prêmio, como ganhar XP.
//  - SAIR DO JOGO (ConfirmQuit): confirmação → Application.Quit (botão na barra do INÍCIO e ESC no INÍCIO).
//  - Avisos e convites (FBMenuAvisos): canvas próprio por cima de tudo — convite de squad com ACEITAR/RECUSAR e avisos do servidor.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FreeBala
{
    public partial class FBMenu
    {
        const float BW = 1548, BH = 808;
        // quadro fixo centrado logo abaixo da barra de topo das páginas
        static RectTransform Box1548(Transform page)
        {
            var r = FBUI.Rect(page, "quadro", new Vector2(.5f, 1), new Vector2(.5f, 1), Vector2.zero, new Vector2(BW, BH)); r.pivot = new Vector2(.5f, 1); r.anchoredPosition = new Vector2(0, -78); return r;
        }
        // painel de vidro azul-marinho (sem cinza) com fio claro
        static Image Panel(Transform p, string n, float x, float y, float w, float h, float a = .82f)
        { var im = FBKit.Box(p, n, x, y, w, h, new Color(.02f, .055f, .12f, a)); FBKit.Round(im, 12); im.raycastTarget = true; return im; }
        static Text Head(Transform p, string s, float x, float y, float w)
        {
            var a = FBKit.At(p, "acento", x, y + 2, 4, 16).gameObject.AddComponent<Image>(); a.color = FBKit.Blue; a.raycastTarget = false; FBKit.Pill(a);
            return FBKit.T(p, s, 14, x + 14, y, w - 14, 20, TextAnchor.MiddleLeft, true, Color.white);
        }
        static RawImage Icon(Transform p, string tex, float x, float y, float s, Color c)
        { var ri = FBKit.At(p, "ic", x, y, s, s).gameObject.AddComponent<RawImage>(); ri.texture = T(tex); ri.color = c; ri.raycastTarget = false; return ri; }
        // grade com rolagem vertical (células fixas)
        static RectTransform ScrollGrid(Transform p, float x, float y, float w, float h, Vector2 cell, int cols, float gap = 10)
        {
            var bg = FBKit.At(p, "grade", x, y, w, h).gameObject.AddComponent<Image>(); bg.color = new Color(0, 0, 0, 0); bg.raycastTarget = true; bg.gameObject.AddComponent<RectMask2D>();
            var c = new GameObject("c", typeof(RectTransform)); var cr = (RectTransform)c.transform; cr.SetParent(bg.transform, false); cr.anchorMin = new Vector2(0, 1); cr.anchorMax = new Vector2(1, 1); cr.pivot = new Vector2(.5f, 1); cr.sizeDelta = Vector2.zero;
            var g = c.AddComponent<GridLayoutGroup>(); g.cellSize = cell; g.spacing = new Vector2(gap, gap); g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = cols; g.padding = new RectOffset(2, 2, 2, 12);
            c.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var s = bg.gameObject.AddComponent<ScrollRect>(); s.content = cr; s.horizontal = false; s.scrollSensitivity = 40; s.movementType = ScrollRect.MovementType.Clamped; s.viewport = (RectTransform)bg.transform; return cr;
        }
        static RectTransform Cell(Transform grid, string n) { var g = new GameObject(n, typeof(RectTransform)); g.transform.SetParent(grid, false); return (RectTransform)g.transform; }
        static Button Btn(Transform p, string s, float x, float y, float w, float h, System.Action a, Color bg, Color txt, int fs = 14)
        {
            var im = FBKit.Box(p, "b_" + s, x, y, w, h, bg, false); FBKit.Round(im, 8); var b = Click(im, a, .1f, 1.03f);
            FBKit.T(im.transform, s, fs, 0, 0, w, h, TextAnchor.MiddleCenter, true, txt); return b;
        }
        // rosto do personagem (miniatura do próprio modelo; pede a renderização na primeira vez e redesenha quando fica pronta)
        static FBCustomizeThumbs menuThumbs; static readonly HashSet<string> thumbAsked = new();
        static Texture Avatar(string skin)
        {
            if (string.IsNullOrEmpty(skin)) return T("avatar");
            var t = FBCustomizeThumbs.Get(skin); if (t) return t;
            if (FBRegistry.I && FBRegistry.I.HasChar(skin) && thumbAsked.Add(skin))
            { if (!menuThumbs) menuThumbs = FBCustomizeThumbs.Create(null); menuThumbs.Request(skin, _ => { thumbAsked.Remove(skin); if (FBLobbyNet.I) FBLobbyNet.I.version++; }); }
            return T("avatar");
        }
        public void OpenSquad() => Squad();

        // ================================================================ SAIR DO JOGO
        GameObject quitDlg;
        void ConfirmQuit()
        {
            if (quitDlg) { Destroy(quitDlg); return; }
            var ov = FBUI.Panel(root, "sair", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, .01f, .04f, .78f)); ov.raycastTarget = true; quitDlg = ov.gameObject; FBFadeIn.Add(ov, 0, 0).dur = .15f;
            const float W = 520, H = 250; var box = FBKit.Box(ov.transform, "caixa", 0, 0, W, H, new Color(.025f, .06f, .13f, .98f)); var br = box.rectTransform; br.anchorMin = br.anchorMax = br.pivot = new Vector2(.5f, .5f); br.anchoredPosition = Vector2.zero; FBKit.Frame(box, 14); box.raycastTarget = true;
            FlagTick(box.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0), W, 3, .95f);
            Icon(box.transform, "ic_power", 32, 34, 30, new Color(1, .45f, .4f));
            FBKit.T(box.transform, "SAIR DO JOGO?", 26, 76, 30, W - 100, 38, TextAnchor.MiddleLeft, true, Color.white);
            FBKit.T(box.transform, "O BURITIRO vai fechar. Seu personagem, armas e progresso já estão salvos.", 14, 32, 84, W - 64, 44, TextAnchor.UpperLeft, false, FBKit.Dim);
            Btn(box.transform, "CANCELAR", 32, H - 32 - 54, 200, 54, () => Destroy(quitDlg), new Color(.08f, .16f, .3f, .9f), Color.white, 16);
            Btn(box.transform, "SAIR", W - 32 - 240, H - 32 - 54, 240, 54, Quit, new Color(.78f, .16f, .14f), Color.white, 18);
        }
        static void Quit()
        {
            FBSession.Save(); PlayerPrefs.Save(); Debug.Log("[FreeBala] sair do jogo (menu)");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ================================================================ JOGAR (com squad)
        public void GoOnline() { FBSession.online = true; FBSession.Save(); Go(); }
        void PlayOrParty()
        {
            var net = FBLobbyNet.I;
            if (net && net.InParty)
            {
                if (!net.IsLeader) { Squad(); return; }
                if (!net.AllReady) { net.toast = "Aguardando o squad ficar PRONTO"; net.toastT = Time.unscaledTime; net.version++; Squad(); return; }
                net.StartParty(FBSession.map, FBSession.mode); return;
            }
            Play();
        }

        // ================================================================ SQUAD / SOCIAL
        static string sqTab = "amigos"; static bool sqVsBots; System.Action matchBack;
        RectTransform sqParty, sqList, sqStatus, sqAction, sqTabs, sqModes, sqMic; InputField sqFriend;
        // r69: modo online escolhido no squad. DE ROLÊ e ROLÊ ARMADO usam a cidade do mapa (modo "role"); TIMES e 2 × 2 usam o mata-mata.
        // modo online que vai valer de fato (o mesmo cálculo do FBNet.Start): o modo do jogo escolhido no ALTERAR manda no par rolê/times
        static string EffNet() => FBSession.mode == "role" ? (FBSession.netMode == "rolex" ? "rolex" : "role") : (FBSession.netMode == "x2" ? "x2" : "tdm");
        void SetNetMode(string m)
        {
            FBSession.netMode = m;
            if (m == "role" || m == "rolex") FBSession.mode = "role"; else if (FBSession.mode == "role") FBSession.mode = "classico";
            FBSession.Save(); Squad();
        }
        void Squad()
        {
            var net = FBLobbyNet.Ensure();
            var p = Page("SQUAD  ·  JOGAR ONLINE", false); if (preview) preview.SetActive(false);
            Shade(p, Vector2.zero, Vector2.one, .55f); var b = Box1548(p);
            const float LW = 1020, RX = 1050, RW = 498;
            // ---------- esquerda: squad
            Head(b, "SEU SQUAD", 0, 0, 400);
            sqParty = FBKit.At(b, "lugares", 0, 30, LW, 372);
            // partida
            var pc = Panel(b, "partida", 0, 418, LW, 390).transform;
            Head(pc, "COMO JOGAR", 24, 20, 300);
            // r69: cartões mais baixos (64) para caber a fileira dos modos online
            void Vs(bool bots, float x)
            {
                bool on = sqVsBots == bots; var t = FBKit.Tile(pc, bots ? "vs_bots" : "vs_jogadores", x, 52, 478, 64, on, () => { sqVsBots = bots; Squad(); });
                Icon(t.transform, bots ? "ic_bot" : "ic_swords", 18, 14, 36, on ? Color.white : FBKit.Dim);
                FBKit.T(t.transform, bots ? "CONTRA BOTS" : "CONTRA JOGADORES", 19, 70, 8, 390, 26, TextAnchor.MiddleLeft, true, Color.white);
                FBKit.T(t.transform, bots ? "Você contra a inteligência artificial · modo e mapa escolhidos" : "Rolê, rolê armado, times ou 2 × 2 (squad de 4 sorteado)", 12, 70, 36, 400, 20, TextAnchor.MiddleLeft, false, FBKit.Dim);
            }
            Vs(false, 24); Vs(true, 518);
            sqModes = FBKit.At(pc, "modos", 24, 128, 972, 40);
            // modo + mapa
            var M = MODES.FirstOrDefault(m => m.id == FBSession.mode); if (M.id == null) M = MODES[0];
            var mp = GameData.Maps.TryGetValue(FBSession.map, out var mm) ? mm.name : FBSession.map;
            var info = FBKit.Tile(pc, "partida_info", 24, 180, 972, 92, false, () => { matchBack = Squad; MatchPicker(); }); info.GetComponent<Image>().color = new Color(.04f, .1f, .21f, .85f);
            var th = FBKit.At(info.transform, "mini", 10, 10, 128, 72).gameObject.AddComponent<Image>(); th.color = Color.white; FBKit.Round(th, 6); th.gameObject.AddComponent<Mask>().showMaskGraphic = true; th.raycastTarget = false;
            var shot = FBKit.Cover(th.transform, FBMapShot.Get(FBSession.map), 0, 0, 128, 72);
            var nm = EffNet();
            string nmDesc = nm == "rolex" ? "A mesma cidade livre, mas todo mundo entra armado e os jogadores podem se enfrentar. Morreu, volta em 3 s longe dos outros. Loja de armas aberta."
                : nm == "x2" ? "Os 4 do squad são sorteados 2 contra 2 numa área de combate do mapa. Primeiro time a 20 abates (ou 10 min). O microfone só chega na sua dupla."
                : nm == "tdm" ? "Mata-mata em times na sala online do mapa (até 10 jogadores). O squad entra junto e do mesmo lado."
                : "Cidade livre com todo mundo na mesma sala (até 16 jogadores), sem armas: carros, paredão e loja de armas sincronizados.";
            FBKit.T(info.transform, (sqVsBots ? M.name : FBSession.NetModeName(nm) + " ONLINE") + "  ·  " + (mp ?? "").ToUpper(), 20, 156, 16, 640, 28, TextAnchor.MiddleLeft, true, Color.white);
            FBKit.T(info.transform, sqVsBots ? M.desc : nmDesc, 12, 156, 46, 660, 40, TextAnchor.UpperLeft, false, FBKit.Dim);
            FBKit.T(info.transform, sqVsBots ? "ALTERAR  ›" : "MAPA  ›", 13, 972 - 140, 0, 120, 92, TextAnchor.MiddleRight, true, FBKit.Blue);
            sqAction = FBKit.At(pc, "acao", 24, 284, 972, 100);
            sqMic = FBKit.At(b, "mic", LW - 300, 0, 300, 28);
            // ---------- direita: social
            var sc = Panel(b, "social", RX, 0, RW, BH).transform;
            Head(sc, "SOCIAL", 24, 20, 200);
            // código
            var cb = FBKit.Box(sc, "codigo", 24, 54, RW - 48, 64, new Color(.05f, .13f, .27f, .9f)); FBKit.Round(cb, 10);
            FBKit.T(cb.transform, "SEU CÓDIGO DE JOGADOR", 10, 16, 10, 260, 14, TextAnchor.MiddleLeft, true, FBKit.Dim);
            FBKit.T(cb.transform, FBLobbyNet.Pid, 26, 16, 24, 220, 34, TextAnchor.MiddleLeft, true, Color.white);
            var cp = Btn(cb.transform, "COPIAR", RW - 48 - 16 - 120, 14, 120, 36, () => { GUIUtility.systemCopyBuffer = FBLobbyNet.Pid; var n = FBLobbyNet.I; if (n) { n.toast = "Código copiado: mande para seus amigos"; n.toastT = Time.unscaledTime; n.version++; } }, new Color(.12f, .32f, .65f, .95f), Color.white, 12);
            sqStatus = FBKit.At(sc, "status", 24, 128, RW - 48, 40);
            sqTabs = FBKit.At(sc, "abas", 24, 178, RW - 48, 40);
            // adicionar amigo (fica fora do redesenho: o texto digitado não some)
            var ab = FBKit.Box(sc, "campo", 24, 230, RW - 48 - 140, 44, new Color(.01f, .04f, .1f, .9f)); FBKit.Round(ab, 8); ab.raycastTarget = true;
            var tx = FBKit.T(ab.transform, "", 17, 14, 0, RW - 48 - 170, 44, TextAnchor.MiddleLeft, true, Color.white);
            var ph = FBKit.T(ab.transform, "Código do amigo (ex.: K7M2QX)", 14, 14, 0, RW - 48 - 170, 44, TextAnchor.MiddleLeft, false, new Color(1, 1, 1, .32f));
            sqFriend = ab.gameObject.AddComponent<InputField>(); sqFriend.textComponent = tx; sqFriend.placeholder = ph; sqFriend.characterLimit = 10; sqFriend.characterValidation = InputField.CharacterValidation.Alphanumeric;
            void AddF() { var n = FBLobbyNet.I; if (!n) return; var c = sqFriend.text; if (n.AddFriend(c)) { sqFriend.text = ""; n.toast = "Amigo adicionado: " + FBLobbyNet.CleanPid(c); } else n.toast = FBLobbyNet.CleanPid(c) == FBLobbyNet.Pid ? "Esse é o seu próprio código" : FBLobbyNet.IsFriend(FBLobbyNet.CleanPid(c)) ? "Já está na sua lista" : "Código inválido (4 a 10 letras/números)"; n.toastT = Time.unscaledTime; n.version++; }
            Btn(sc, "+ ADICIONAR", RW - 24 - 132, 230, 132, 44, AddF, FBKit.Flag, Color.white, 13);
            sqList = FBKit.At(sc, "lista", 24, 288, RW - 48, BH - 288 - 20);
            var w = p.gameObject.AddComponent<FBVersionWatch>(); w.rebuild = SquadRefresh;
        }
        void SquadRefresh()
        {
            if (!sqParty) return; var net = FBLobbyNet.I; if (!net) return;
            foreach (Transform t in sqParty) Destroy(t.gameObject); foreach (Transform t in sqList) Destroy(t.gameObject); foreach (Transform t in sqStatus) Destroy(t.gameObject);
            foreach (Transform t in sqAction) Destroy(t.gameObject); foreach (Transform t in sqTabs) Destroy(t.gameObject);
            if (sqModes) foreach (Transform t in sqModes) Destroy(t.gameObject); if (sqMic) foreach (Transform t in sqMic) Destroy(t.gameObject);
            bool on = net.status == FBLobbyNet.St.On;
            string nmNow = EffNet(); int nParty = net.InParty ? net.party.Count : 1;
            // ---- r69: modos online (só o líder escolhe; quem não é líder vê o que o líder vai iniciar quando ele mandar)
            if (sqModes && !sqVsBots)
            {
                var ms = new (string id, string l, bool ok)[] { ("role", "DE ROLÊ", true), ("rolex", "ROLÊ ARMADO", !on || net.capRolex), ("tdm", "TIMES", true), ("x2", "2 × 2", (!on || net.capX2) && nParty == 4) };
                for (int i = 0; i < ms.Length; i++)
                {
                    var mi = ms[i]; bool sel = nmNow == mi.id, can = mi.ok && net.IsLeader;
                    var tb = FBKit.TextTile(sqModes, mi.l + (mi.id == "x2" ? $"  ({nParty}/4)" : ""), i * 246, 0, 234, 40, sel, () => { if (can) SetNetMode(mi.id); else if (net.IsLeader) { net.toast = mi.id == "x2" && nParty != 4 ? "2 × 2 precisa de 4 no squad: convide mais amigos" : "O servidor conectado é antigo e não tem esse modo"; net.toastT = Time.unscaledTime; net.version++; } }, 13);
                    if (!mi.ok && !sel) tb.GetComponent<Image>().color = new Color(.03f, .07f, .14f, .5f);
                }
            }
            // ---- r69: microfone (Caps Lock também liga/desliga; a voz vai para o squad no menu e para o time/sala na partida)
            if (sqMic && FBVoiceChat.Supported)
            {
                var vc = FBVoiceChat.Ensure(); bool mon = vc.on;
                string ml = !string.IsNullOrEmpty(vc.erro) ? vc.erro : mon ? "● MIC LIGADO  ·  CAPS" : "○ LIGAR MICROFONE  ·  CAPS";
                Btn(sqMic, ml, 0, 0, 300, 28, () => { vc.Toggle(); SquadRefresh(); }, mon ? FBKit.Flag : new Color(.12f, .32f, .65f, .95f), Color.white, 11);
            }
            // ---- lugares do squad
            var members = net.InParty ? net.party.ToList() : new List<FBLobbyNet.Person> { new() { pid = FBLobbyNet.Pid, name = FBSession.name, skin = FBSession.skin } };
            var me = members.FirstOrDefault(x => x.pid == FBLobbyNet.Pid); if (me != null) { members.Remove(me); members.Insert(0, me); me.skin = FBSession.skin; }
            const float CW = 243, CH = 372, G = 16;
            for (int i = 0; i < 4; i++)
            {
                float x = i * (CW + G);
                if (i < members.Count)
                {
                    var m = members[i]; bool isMe = m.pid == FBLobbyNet.Pid, lead = net.InParty && m.pid == net.leader;
                    var c = FBKit.Box(sqParty, "lugar", x, 0, CW, CH, new Color(.04f, .1f, .22f, .9f)); FBKit.Round(c, 14); c.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                    if (isMe) c.GetComponent<Outline>().effectColor = new Color(.45f, .72f, 1f, .9f);
                    var bgG = FBKit.At(c.transform, "fundo", 0, 0, CW, CH).gameObject.AddComponent<RawImage>(); bgG.texture = T("glow"); bgG.color = new Color(.2f, .5f, 1f, .35f); bgG.raycastTarget = false;
                    var av = FBKit.Cover(c.transform, Avatar(m.skin), 0, 0, CW, CH - 70, .7f);
                    FBKit.Shade(c.transform, 0, CH - 190, CW, 120);
                    var bar = FBKit.Box(c.transform, "base", 0, CH - 70, CW, 70, new Color(.02f, .05f, .11f, .96f), false);
                    FBKit.T(bar.transform, (string.IsNullOrEmpty(m.name) ? m.pid : m.name).ToUpper(), 18, 14, 10, CW - 28, 26, TextAnchor.MiddleLeft, true, Color.white).FitTo(34, 14);   // nome grande no cartão do squad, mas encolhe se for comprido
                    string st = isMe ? (lead ? "VOCÊ · LÍDER" : "VOCÊ") : lead ? "LÍDER" : m.ready ? "PRONTO" : "AGUARDANDO";
                    Color sc = lead ? FBKit.Blue : m.ready ? new Color(.4f, .95f, .55f) : FBKit.Dim;
                    FBKit.T(bar.transform, st, 11, 14, 40, CW - 28, 18, TextAnchor.MiddleLeft, true, sc);
                    if (net.InParty && !lead && m.ready) { var rb = FBKit.Box(c.transform, "pronto", CW - 98, 12, 86, 24, FBKit.Flag, false); FBKit.Pill(rb); FBKit.T(rb.transform, "✓ PRONTO", 11, 0, 0, 86, 24, TextAnchor.MiddleCenter, true, Color.white); }
                    if (lead) { var cr = FBKit.Box(c.transform, "lider", 12, 12, 36, 36, new Color(.08f, .3f, .7f, .95f), false); FBKit.Pill(cr); Icon(cr.transform, "ic_crown", 8, 8, 20, Color.white); }
                    if (net.InParty && net.IsLeader && !isMe) { var pid = m.pid; Btn(c.transform, "REMOVER", CW - 98, CH - 70 - 36, 86, 26, () => net.Kick(pid), new Color(.6f, .12f, .1f, .9f), Color.white, 10); }
                }
                else
                {
                    var t = FBKit.Tile(sqParty, "convidar", x, 0, CW, CH, false, () => { sqTab = "amigos"; SquadRefresh(); if (sqFriend) sqFriend.ActivateInputField(); });
                    t.GetComponent<Image>().color = new Color(.03f, .08f, .17f, .55f); FBKit.Round(t.GetComponent<Image>(), 14);
                    var ring = FBKit.Box(t.transform, "aro", CW / 2 - 40, CH / 2 - 70, 80, 80, new Color(.12f, .3f, .6f, .45f), false); FBKit.Pill(ring); FBKit.Round(ring, 40);
                    Icon(ring.transform, "ic_plus", 22, 22, 36, new Color(1, 1, 1, .85f));
                    FBKit.T(t.transform, "CONVIDAR", 16, 0, CH / 2 + 24, CW, 24, TextAnchor.MiddleCenter, true, Color.white);
                    FBKit.T(t.transform, on ? "Chame um amigo pela lista ao lado" : "Servidor offline", 11, 12, CH / 2 + 50, CW - 24, 34, TextAnchor.UpperCenter, false, FBKit.Dim);
                }
            }
            // ---- status do servidor
            {
                Color dc = on ? new Color(.35f, .95f, .5f) : net.status == FBLobbyNet.St.Connecting ? new Color(1, .8f, .3f) : new Color(1, .4f, .35f);
                var dot = FBKit.Box(sqStatus, "luz", 2, 14, 12, 12, dc, false); FBKit.Pill(dot);
                // r68: mostra onde está ligado de verdade (servidor online, Host da rede ou este PC hospedando)
                string where = FBLanHost.Hosting ? "HOST · " + LanAddr() : FBLanHost.OnLan ? "REDE · " + ShortServer() : ShortServer();
                string s = on ? "ONLINE  ·  " + where : net.status == FBLobbyNet.St.Connecting ? "CONECTANDO…  ·  " + where : (FBLanHost.OnLan ? "HOST FORA DO AR  ·  " : "SERVIDOR OFFLINE  ·  ") + where;
                FBKit.T(sqStatus, s, 11, 22, 0, 214, 40, TextAnchor.MiddleLeft, true, on ? FBKit.Txt : dc).FitTo(11, 8);
                if (FBLanHost.Supported) Btn(sqStatus, "REDE LOCAL", 450 - 104 - 8 - 104, 4, 104, 32, LanDialog, FBLanHost.OnLan ? FBKit.Flag : new Color(.12f, .32f, .65f, .95f), Color.white, 11);
                Btn(sqStatus, "SERVIDOR", 450 - 104, 4, 104, 32, ServerDialog, new Color(.08f, .16f, .3f, .9f), Color.white, 11);
            }
            // ---- abas
            var fr = FBLobbyNet.Friends; int onF = fr.Count(f => net.StatusOf(f.pid) != "offline");
            var tabs = new (string k, string l)[] { ("amigos", $"AMIGOS  {onF}/{fr.Count}"), ("online", "ONLINE AGORA"), ("convites", "CONVITES" + (net.invites.Count > 0 ? $"  ({net.invites.Count})" : "")) };
            for (int i = 0; i < 3; i++) { var k = tabs[i].k; FBKit.TextTile(sqTabs, tabs[i].l, i * 152, 0, 146, 40, sqTab == k, () => { sqTab = k; SquadRefresh(); }, 12); }
            // ---- lista
            var rows = new List<(string pid, string name, string skin, string st, FBLobbyNet.Invite iv)>();
            if (sqTab == "amigos") foreach (var f in fr) { net.seen.TryGetValue(f.pid, out var pp); rows.Add((f.pid, string.IsNullOrEmpty(pp?.name) ? f.name : pp.name, pp?.skin, net.StatusOf(f.pid), null)); }
            else if (sqTab == "online") foreach (var pp in net.seen.Values.Where(x => x.st != "offline" && x.pid != FBLobbyNet.Pid)) rows.Add((pp.pid, pp.name, pp.skin, pp.st, null));
            else foreach (var iv in net.invites) rows.Add((iv.from, iv.name, iv.skin, "menu", iv));
            rows = rows.OrderBy(r => r.st == "menu" ? 0 : r.st == "partida" ? 1 : 2).ThenBy(r => r.name).ToList();
            float lw = 450, lh = sqList.rect.height > 10 ? sqList.rect.height : 500;
            if (rows.Count == 0)
            {
                string e = !on ? (FBLanHost.Supported ? "Sem conexão com o servidor.\nNa mesma rede, sem internet: REDE LOCAL (um PC hospeda, os outros procuram na rede ou digitam o IP)." : "Sem conexão com o servidor.\nAjuste o endereço em SERVIDOR.") :
                    sqTab == "amigos" ? "Nenhum amigo ainda.\nMande o SEU CÓDIGO para um amigo ou digite o código dele acima." : sqTab == "online" ? "Ninguém mais no menu agora." : "Nenhum convite.\nQuando alguém te chamar para o squad, aparece aqui.";
                FBKit.T(sqList, e, 13, 10, 30, lw - 20, 120, TextAnchor.UpperCenter, false, FBKit.Dim);
            }
            var list = ScrollGrid(sqList, 0, 0, lw, lh, new Vector2(lw - 4, 64), 1, 8);
            foreach (var r in rows)
            {
                var row = Cell(list, "linha"); var bg = row.gameObject.AddComponent<Image>(); bg.color = new Color(.05f, .12f, .25f, .7f); FBKit.Round(bg, 10); bg.raycastTarget = false;
                var ring = FBKit.Box(row, "aro", 10, 8, 48, 48, r.st == "menu" ? new Color(.35f, .95f, .5f) : r.st == "partida" ? FBKit.Blue : new Color(.3f, .36f, .45f), false); FBKit.Round(ring, 10);
                var mk = FBKit.Box(ring.transform, "recorte", 2, 2, 44, 44, Color.white, false); FBKit.Round(mk, 9); mk.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                FBKit.Cover(mk.transform, Avatar(r.skin), 0, 0, 44, 44, .9f);
                FBKit.T(row, (string.IsNullOrEmpty(r.name) ? r.pid : r.name).ToUpper(), 15, 70, 10, 190, 22, TextAnchor.MiddleLeft, true, Color.white).FitTo(15);
                string stx = r.iv != null ? "TE CHAMOU PARA O SQUAD" : r.st == "menu" ? "NO MENU" : r.st == "partida" ? "EM PARTIDA" : "OFFLINE";
                FBKit.T(row, stx + "  ·  " + r.pid, 10, 70, 34, 200, 18, TextAnchor.MiddleLeft, true, r.st == "menu" ? new Color(.45f, .95f, .6f) : r.st == "partida" ? FBKit.Blue : FBKit.Dim);
                var pid = r.pid; var iv0 = r.iv; float bx = lw - 4 - 10;
                if (iv0 != null)
                {
                    bx -= 96; Btn(row, "ACEITAR", bx, 14, 96, 36, () => net.Accept(iv0), FBKit.Flag, Color.white, 12);
                    bx -= 86; Btn(row, "RECUSAR", bx, 14, 80, 36, () => net.Decline(iv0), new Color(.1f, .18f, .32f, .95f), Color.white, 11);
                    continue;
                }
                bool inMy = net.party.Any(x => x.pid == pid);
                if (inMy) { bx -= 96; FBKit.T(row, "NO SQUAD", 11, bx, 0, 96, 64, TextAnchor.MiddleCenter, true, FBKit.Blue); }
                else if (r.st == "menu" && on) { bx -= 96; Btn(row, "CONVIDAR", bx, 14, 96, 36, () => net.InviteTo(pid), FBKit.BlueDeep, Color.white, 12); }
                if (sqTab == "amigos") { bx -= 42; Btn(row, "✕", bx, 14, 36, 36, () => net.RemoveFriend(pid), new Color(.1f, .18f, .32f, .95f), FBKit.Dim, 14); }
                else if (!FBLobbyNet.IsFriend(pid)) { bx -= 42; Btn(row, "+", bx, 14, 36, 36, () => net.AddFriend(pid, r.name), new Color(.1f, .18f, .32f, .95f), Color.white, 18); }
            }
            // ---- ação principal
            const float AW = 972;
            if (sqVsBots)
            {
                if (net.InParty)
                {
                    FBKit.T(sqAction, "Squad contra bots ainda não: os bots rodam em cada aparelho e não são sincronizados pela rede. Jogue CONTRA JOGADORES com o squad, ou saia do squad para jogar sozinho contra bots.", 13, 0, 0, AW - 260, 100, TextAnchor.MiddleLeft, false, FBKit.Dim);
                    Btn(sqAction, "SAIR DO SQUAD", AW - 240, 22, 240, 56, () => net.LeaveParty(), new Color(.1f, .18f, .32f, .95f), Color.white, 15);
                }
                else Big(sqAction, "JOGAR CONTRA BOTS", (MODES.FirstOrDefault(m => m.id == FBSession.mode).name ?? "") + "  ·  SOZINHO", true, Play);
                return;
            }
            if (!on)
            {
                if (FBLanHost.Supported && !FBLanHost.OnLan) { Big(sqAction, "JOGAR NA REDE LOCAL", "Servidor online fora do ar · um PC hospeda e os outros entram pela rede (Wi-Fi ou cabo)", true, LanDialog); return; }
                Big(sqAction, FBLanHost.OnLan ? "HOST DA REDE FORA DO AR" : "SERVIDOR OFFLINE", FBLanHost.OnLan ? "Tentando de novo… · confira o Host em REDE LOCAL" : "Tentando conectar de novo… (ou ajuste em SERVIDOR)", false, () => net.Reconnect()); return;
            }
            if (net.InParty && !net.IsLeader)
            {
                bool rd = net.MeReady;
                Big(sqAction, rd ? "PRONTO ✓" : "ESTOU PRONTO", rd ? "Aguardando o líder iniciar · clique para cancelar" : "O líder inicia quando todos estiverem prontos", !rd, () => net.SetReady(!rd), AW - 256);
                Btn(sqAction, "SAIR DO SQUAD", AW - 240, 22, 240, 56, () => net.LeaveParty(), new Color(.1f, .18f, .32f, .95f), Color.white, 15);
                return;
            }
            int notReady = net.InParty ? net.party.Count(x => x.pid != net.leader && !x.ready) : 0;
            // r69: 2 × 2 só com exatamente 4; servidor antigo sem o modo escolhido não deixa iniciar (cairia em outro modo sem avisar)
            string bloqueio = nmNow == "x2" && nParty != 4 ? $"2 × 2 precisa de exatamente 4 no squad (agora {nParty})" : nmNow == "x2" && !net.capX2 ? "Servidor antigo: não tem 2 × 2 (atualize o server.mjs ou use REDE LOCAL)" : nmNow == "rolex" && !net.capRolex ? "Servidor antigo: não tem ROLÊ ARMADO (atualize o server.mjs ou use REDE LOCAL)" : null;
            string lado = nmNow == "x2" ? "os 4 serão sorteados 2 contra 2" : nmNow == "tdm" ? "todos no mesmo time" : "todos na mesma cidade";
            string sub = bloqueio ?? (net.InParty ? (notReady > 0 ? $"Aguardando {notReady} jogador{(notReady > 1 ? "es" : "")} ficar PRONTO" : $"Squad de {net.party.Count} pronto · {FBSession.NetModeName(nmNow)} · {lado}") : $"Sozinho na sala online do mapa · {FBSession.NetModeName(nmNow)} · convide amigos para o squad");
            bool pode = notReady == 0 && bloqueio == null;
            Big(sqAction, "INICIAR PARTIDA", sub, pode, () => { if (pode) { FBSession.netMode = nmNow; net.StartParty(FBSession.map, FBSession.mode); } }, net.InParty ? AW - 256 : AW);
            if (net.InParty) Btn(sqAction, "SAIR DO SQUAD", AW - 240, 22, 240, 56, () => net.LeaveParty(), new Color(.1f, .18f, .32f, .95f), Color.white, 15);
        }
        static string ShortServer() { var s = FBSession.Server ?? ""; s = s.Replace("ws://", "").Replace("wss://", ""); int i = s.IndexOf('/'); return (i > 0 ? s.Substring(0, i) : s).ToUpper(); }
        static string LanAddr() { var ips = FBLanHost.LocalIPs(); return (ips.Count > 0 ? ips[0].ip : "127.0.0.1") + (FBLanHost.Port != FBLanServerPort ? ":" + FBLanHost.Port : ""); }
        const int FBLanServerPort = 8123;   // porta padrão (com ela o IP basta; outra porta aparece junto)
        // botão grande (JOGAR/INICIAR): verde quando dá para clicar, azul-escuro quando está esperando
        static void Big(Transform p, string title, string sub, bool go, System.Action a, float w = 972)
        {
            var im = FBKit.Box(p, "grande", 0, 10, w, 80, go ? FBKit.Flag : new Color(.08f, .18f, .36f, .95f), false); FBKit.Round(im, 10); Click(im, a, .12f, 1.015f);
            var sh = im.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .35f); sh.effectDistance = new Vector2(0, -5);
            var t = FBKit.T(im.transform, title, 30, 26, 8, w - 52, 40, TextAnchor.MiddleLeft, true, Color.white); t.fontStyle = FontStyle.Italic;
            FBKit.T(im.transform, sub, 12, 28, 50, w - 56, 20, TextAnchor.MiddleLeft, true, new Color(1, 1, 1, .78f));
        }
        void ServerDialog()
        {
            var ov = FBUI.Panel(root, "servidor", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, .01f, .04f, .78f)); ov.raycastTarget = true; var dlg = ov.gameObject;
            const float W = 560, H = 290; var box = FBKit.Box(ov.transform, "caixa", 0, 0, W, H, new Color(.025f, .06f, .13f, .98f)); var br = box.rectTransform; br.anchorMin = br.anchorMax = br.pivot = new Vector2(.5f, .5f); br.anchoredPosition = Vector2.zero; FBKit.Frame(box, 14); box.raycastTarget = true;
            FlagTick(box.transform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, W, 3, .95f);
            FBKit.T(box.transform, "SERVIDOR ONLINE", 22, 30, 24, W - 60, 30, TextAnchor.MiddleLeft, true, Color.white);
            FBKit.T(box.transform, "Endereço do PC que roda o INICIAR_FREE_BALA.bat (mesmo servidor do jogo web). Ex.: ws://192.168.0.10:8123/ws", 13, 30, 58, W - 60, 40, TextAnchor.UpperLeft, false, FBKit.Dim);
            var bg = FBKit.Box(box.transform, "campo", 30, 108, W - 60, 48, new Color(.01f, .04f, .1f, .95f)); FBKit.Round(bg, 8); bg.raycastTarget = true;
            var t = FBKit.T(bg.transform, FBSession.server, 16, 14, 0, W - 88, 48, TextAnchor.MiddleLeft, false, Color.white); var f = bg.gameObject.AddComponent<InputField>(); f.textComponent = t; f.text = FBSession.server;
            Btn(box.transform, "CANCELAR", 30, H - 30 - 52, 180, 52, () => Destroy(dlg), new Color(.08f, .16f, .3f, .9f), Color.white, 15);
            Btn(box.transform, "SALVAR E CONECTAR", W - 30 - 290, H - 30 - 52, 290, 52, () =>
            { var s = f.text.Trim(); if (s.StartsWith("ws://") || s.StartsWith("wss://")) { if (FBLanHost.Hosting) FBLanHost.StopHost(); else FBLanHost.LeaveLan(); FBSession.server = s; FBSession.Save(); FBLobbyNet.I?.Reconnect(); } Destroy(dlg); SquadRefresh(); }, FBKit.Flag, Color.white, 15);
        }

        // r68 REDE LOCAL: HOSPEDAR neste PC, PROCURAR NA REDE (descoberta UDP 8125) ou CONECTAR POR IP. Redesenha sozinha quando a conexão,
        // a procura ou o número de jogadores do Host mudam. O campo do IP fica fora do redesenho (o texto digitado não some).
        void LanDialog()
        {
            var ov = FBUI.Panel(root, "rede_local", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, .01f, .04f, .78f)); ov.raycastTarget = true; var dlg = ov.gameObject;
            const float W = 700, H = 640, CW = W - 60; var box = FBKit.Box(ov.transform, "caixa", 0, 0, W, H, new Color(.025f, .06f, .13f, .98f)); var br = box.rectTransform; br.anchorMin = br.anchorMax = br.pivot = new Vector2(.5f, .5f); br.anchoredPosition = Vector2.zero; FBKit.Frame(box, 14); box.raycastTarget = true;
            FlagTick(box.transform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, W, 3, .95f);
            FBKit.T(box.transform, "REDE LOCAL", 22, 30, 24, CW, 30, TextAnchor.MiddleLeft, true, Color.white);
            FBKit.T(box.transform, "Jogue com quem está na mesma rede (Wi-Fi ou cabo), sem servidor na internet. Um PC HOSPEDA; os outros procuram na rede ou digitam o IP dele.", 13, 30, 58, CW, 40, TextAnchor.UpperLeft, false, FBKit.Dim);
            // conectar por IP
            const float IY = H - 30 - 52 - 70;
            FBKit.T(box.transform, "CONECTAR POR IP", 11, 30, IY - 20, 300, 16, TextAnchor.MiddleLeft, true, FBKit.Dim);
            var ipBg = FBKit.Box(box.transform, "campo_ip", 30, IY, CW - 190, 48, new Color(.01f, .04f, .1f, .95f)); FBKit.Round(ipBg, 8); ipBg.raycastTarget = true;
            var ipT = FBKit.T(ipBg.transform, "", 17, 14, 0, CW - 220, 48, TextAnchor.MiddleLeft, true, Color.white);
            var ipPh = FBKit.T(ipBg.transform, "IP do Host (ex.: 192.168.0.10)", 14, 14, 0, CW - 220, 48, TextAnchor.MiddleLeft, false, new Color(1, 1, 1, .32f));
            var ipF = ipBg.gameObject.AddComponent<InputField>(); ipF.textComponent = ipT; ipF.placeholder = ipPh; ipF.characterLimit = 64;
            var msg = FBKit.T(box.transform, "", 12, 30, IY + 52, CW, 18, TextAnchor.MiddleLeft, true, new Color(1, .5f, .45f));
            void Entrar(string addr, string nome)
            {
                // r68b: confere ANTES de parar o Host (endereço errado não derruba mais a sala de quem já está jogando aqui)
                if (!FBLanHost.ValidAddr(addr)) { msg.color = new Color(1, .5f, .45f); msg.text = "ENDEREÇO INVÁLIDO · USE O IP QUE APARECE NA TELA DO HOST (EX.: 192.168.0.10)"; return; }
                if (FBLanHost.IsSelf(addr)) { msg.color = new Color(.45f, .95f, .6f); msg.text = "ESSE É O SEU PRÓPRIO IP: VOCÊ JÁ É O HOST · FECHE E CLIQUE EM INICIAR PARTIDA"; return; }
                if (FBLanHost.Hosting) FBLanHost.StopHost();
                if (!FBLanHost.Join(addr)) { msg.color = new Color(1, .5f, .45f); msg.text = "ENDEREÇO INVÁLIDO · USE O IP QUE APARECE NA TELA DO HOST (EX.: 192.168.0.10)"; return; }
                var n = FBLobbyNet.I; if (n) { n.toast = "Entrando no Host " + nome + " · depois é só INICIAR PARTIDA"; n.toastT = Time.unscaledTime; n.version++; }
                Destroy(dlg); SquadRefresh();
            }
            Btn(box.transform, "CONECTAR", W - 30 - 176, IY, 176, 48, () => Entrar(ipF.text, ipF.text.Trim()), new Color(.12f, .32f, .65f, .95f), Color.white, 14);
            // rodapé
            Btn(box.transform, "FECHAR", 30, H - 30 - 52, 180, 52, () => { Destroy(dlg); SquadRefresh(); }, new Color(.08f, .16f, .3f, .9f), Color.white, 15);
            var holder = FBKit.At(box.transform, "conteudo", 30, 110, CW, IY - 20 - 12 - 110);
            var foot = FBKit.At(box.transform, "rodape", W - 30 - 330, H - 30 - 52, 330, 52);
            void Fill()
            {
                foreach (Transform t in holder) Destroy(t.gameObject); foreach (Transform t in foot) Destroy(t.gameObject);
                var net = FBLobbyNet.I; bool on = net && net.status == FBLobbyNet.St.On;
                // ---- este PC
                var hc = FBKit.Box(holder, "este_pc", 0, 0, CW, 176, new Color(.04f, .1f, .22f, .9f)); FBKit.Round(hc, 12);
                if (FBLanHost.Hosting)
                {
                    var ips = FBLanHost.LocalIPs(); string ip0 = ips.Count > 0 ? ips[0].ip : "127.0.0.1"; string porta = FBLanHost.Port != FBLanServerPort ? ":" + FBLanHost.Port : "";
                    var d = FBKit.Box(hc.transform, "luz", 18, 22, 12, 12, on ? new Color(.35f, .95f, .5f) : new Color(1, .8f, .3f), false); FBKit.Pill(d);
                    FBKit.T(hc.transform, $"VOCÊ É O HOST  ·  {FBLanHost.Clients} CONECTADO{(FBLanHost.Clients == 1 ? "" : "S")} (VOCÊ INCLUSO)", 13, 38, 12, CW - 200, 30, TextAnchor.MiddleLeft, true, Color.white);
                    FBKit.T(hc.transform, "OS OUTROS ENTRAM COM O IP", 10, 18, 46, 300, 14, TextAnchor.MiddleLeft, true, FBKit.Dim);
                    FBKit.T(hc.transform, ip0 + porta, 34, 18, 60, CW - 200, 46, TextAnchor.MiddleLeft, true, FBKit.Blue);
                    string outros = ips.Count > 1 ? "Outros IPs deste PC: " + string.Join(", ", ips.Skip(1).Take(3).Select(x => x.ip)) + "  ·  " : "";
                    FBKit.T(hc.transform, outros + "Se o Windows perguntar, marque REDES PRIVADAS e clique em PERMITIR (senão os outros PCs não acham este Host).", 11, 18, 110, CW - 36, 56, TextAnchor.UpperLeft, false, FBKit.Dim);
                    Btn(hc.transform, "PARAR", CW - 18 - 150, 18, 150, 40, () => { FBLanHost.StopHost(); Fill(); }, new Color(.6f, .12f, .1f, .9f), Color.white, 13);
                    Btn(hc.transform, "COPIAR IP", CW - 18 - 150, 64, 150, 36, () => { GUIUtility.systemCopyBuffer = ip0 + porta; msg.color = new Color(.45f, .95f, .6f); msg.text = "IP COPIADO: " + ip0 + porta; }, new Color(.12f, .32f, .65f, .95f), Color.white, 12);
                }
                else if (FBLanHost.OnLan)
                {
                    var d = FBKit.Box(hc.transform, "luz", 18, 22, 12, 12, on ? new Color(.35f, .95f, .5f) : net && net.status == FBLobbyNet.St.Connecting ? new Color(1, .8f, .3f) : new Color(1, .4f, .35f), false); FBKit.Pill(d);
                    FBKit.T(hc.transform, on ? "CONECTADO AO HOST DA REDE" : net && net.status == FBLobbyNet.St.Connecting ? "CONECTANDO AO HOST…" : "HOST FORA DO AR (TENTANDO DE NOVO)", 13, 38, 12, CW - 200, 30, TextAnchor.MiddleLeft, true, Color.white);
                    FBKit.T(hc.transform, ShortServer(), 30, 18, 52, CW - 200, 44, TextAnchor.MiddleLeft, true, FBKit.Blue);
                    FBKit.T(hc.transform, on ? "Feche esta janela e clique em INICIAR PARTIDA: todos que entrarem no mesmo mapa e modo caem na mesma sala." : "Confira se o Host continua aberto e na mesma rede.", 11, 18, 104, CW - 36, 56, TextAnchor.UpperLeft, false, FBKit.Dim);
                    Btn(hc.transform, "SAIR DA REDE", CW - 18 - 170, 18, 170, 40, () => { FBLanHost.LeaveLan(); Fill(); }, new Color(.1f, .18f, .32f, .95f), Color.white, 13);
                }
                else
                {
                    var hb = FBKit.Box(hc.transform, "hospedar", 18, 18, CW - 36, 80, FBKit.Flag, false); FBKit.Round(hb, 10);
                    Click(hb, () => { if (!FBLanHost.StartHost()) { msg.color = new Color(1, .5f, .45f); msg.text = "NÃO DEU PARA HOSPEDAR: " + (FBLanHost.LastError ?? "").ToUpper(); } Fill(); }, .12f, 1.015f);
                    var ht = FBKit.T(hb.transform, "HOSPEDAR NESTE PC", 26, 22, 8, CW - 80, 40, TextAnchor.MiddleLeft, true, Color.white); ht.fontStyle = FontStyle.Italic;
                    FBKit.T(hb.transform, "Abre a sala na rede: até 16 no De Rolê, 10 no mata-mata · os outros procuram ou digitam o seu IP", 12, 24, 50, CW - 80, 20, TextAnchor.MiddleLeft, true, new Color(1, 1, 1, .8f));
                    FBKit.T(hc.transform, "Quem hospeda precisa deixar o jogo aberto até o fim da partida. Não precisa de internet nem de instalar nada.", 11, 18, 110, CW - 36, 50, TextAnchor.UpperLeft, false, FBKit.Dim);
                }
                // ---- procurar na rede
                float y0 = 192;
                FBKit.T(holder, "HOSTS NA REDE", 13, 0, y0, 300, 32, TextAnchor.MiddleLeft, true, Color.white);
                if (FBLanHost.Scanning) FBKit.T(holder, "PROCURANDO…", 12, CW - 170, y0, 170, 32, TextAnchor.MiddleCenter, true, FBKit.Blue);
                else Btn(holder, "PROCURAR DE NOVO", CW - 170, y0, 170, 32, () => { FBLanHost.Scan(); Fill(); }, new Color(.12f, .32f, .65f, .95f), Color.white, 11);
                var lst = FBLanHost.Found.Where(f => !(FBLanHost.Hosting && f.port == FBLanHost.Port && FBLanHost.LocalIPs().Any(x => x.ip == f.ip))).Take(3).ToList();
                if (lst.Count == 0)
                    FBKit.T(holder, FBLanHost.Scanning ? "Perguntando a todos os PCs da rede…" : FBLanHost.ScanVersion == 0 ? "Clique em PROCURAR DE NOVO." :
                        "Nenhum Host respondeu. Confira se estão na mesma rede e se o Host permitiu o jogo no firewall; ou digite o IP dele abaixo.", 12, 0, y0 + 40, CW, 44, TextAnchor.UpperLeft, false, FBKit.Dim);
                for (int i = 0; i < lst.Count; i++)
                {
                    var f = lst[i]; float ry = y0 + 40 + i * 58;
                    var row = FBKit.Box(holder, "host", 0, ry, CW, 52, new Color(.05f, .12f, .25f, .7f)); FBKit.Round(row, 10);
                    FBKit.T(row.transform, (f.name ?? f.ip).ToUpper(), 15, 16, 6, CW - 220, 22, TextAnchor.MiddleLeft, true, Color.white).FitTo(15);
                    FBKit.T(row.transform, $"{f.ip}{(f.port != FBLanServerPort ? ":" + f.port : "")}  ·  {f.players} NA REDE  ·  {f.rooms} SALA{(f.rooms == 1 ? "" : "S")}", 10, 16, 30, CW - 220, 16, TextAnchor.MiddleLeft, true, FBKit.Dim);
                    bool cur = FBSession.lanServer == f.url && !FBLanHost.Hosting;
                    if (cur) FBKit.T(row.transform, on ? "CONECTADO" : "CONECTANDO…", 12, CW - 160, 0, 150, 52, TextAnchor.MiddleCenter, true, new Color(.45f, .95f, .6f));
                    else { var addr = f.ip + ":" + f.port; var nm = f.name ?? f.ip; Btn(row.transform, "ENTRAR", CW - 150, 8, 140, 36, () => Entrar(addr, nm), FBKit.Flag, Color.white, 13); }
                }
                // ---- rodapé: volta ao servidor online
                if (FBLanHost.OnLan && !FBLanHost.Hosting) Btn(foot, "VOLTAR AO SERVIDOR ONLINE", 0, 0, 330, 52, () => { FBLanHost.LeaveLan(); Fill(); }, new Color(.08f, .16f, .3f, .9f), Color.white, 13);
            }
            var w = dlg.AddComponent<FBKeyWatch>(); w.rebuild = Fill;
            w.key = () => (FBLobbyNet.I ? FBLobbyNet.I.version : 0) * 131 + FBLanHost.ScanVersion * 17 + FBLanHost.Clients * 5 + (FBLanHost.Hosting ? 1 : 0) + (FBLanHost.OnLan ? 2 : 0);
            if (!FBLanHost.Hosting && !FBLanHost.Scanning) FBLanHost.Scan();   // já abre procurando
            Fill();
        }

        // cartão do SQUAD no INÍCIO (embaixo do passe): rostos dos 4 lugares e estado; atualiza sozinho
        void HomeSquadCard(Transform p, Vector2 a, float x, float y, float w)
        {
            var card = Glass(p, "squad", a, a, new Vector2(x, y), new Vector2(w, 96), 12); Click(card, Squad, .06f, 1.015f);
            var holder = FBUI.Rect(card.transform, "conteudo", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            void Fill()
            {
                foreach (Transform t in holder) Destroy(t.gameObject); var net = FBLobbyNet.I;
                var ms = net && net.InParty ? net.party.ToList() : new List<FBLobbyNet.Person> { new() { pid = FBLobbyNet.Pid, skin = FBSession.skin } };
                Tx(holder, "SQUAD", 10, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -10), new Vector2(120, 14), TextAnchor.MiddleLeft, true, InkFaint);
                string st = !net || net.status != FBLobbyNet.St.On ? "OFFLINE · SÓ CONTRA BOTS" : net.InParty ? $"{net.party.Count}/4 · {(net.IsLeader ? "VOCÊ É O LÍDER" : "AGUARDANDO O LÍDER")}" : "SOZINHO · CONVIDE AMIGOS";
                Tx(holder, st, 10, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -10), new Vector2(240, 14), TextAnchor.MiddleRight, true, net && net.status == FBLobbyNet.St.On ? Yel : new Color(1, .5f, .45f));
                for (int i = 0; i < 4; i++)
                {
                    var ring = Bx(holder, "aro", new Vector2(0, 0), new Vector2(0, 0), new Vector2(16 + i * 58, 12), new Vector2(50, 50), i < ms.Count ? (i == 0 ? Yel : FBKit.Flag) : new Color(.1f, .24f, .5f, .55f), 12);
                    if (i < ms.Count)
                    {
                        var mk = Bx(ring.transform, "recorte", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(46, 46), Color.white, 10); mk.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                        var tex = Avatar(ms[i].pid == FBLobbyNet.Pid ? FBSession.skin : ms[i].skin); var av = Pic(mk.transform, "rosto", tex, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(46, 46)); Cover(av, 46, 46);
                    }
                    else Pic(ring.transform, "mais", T("ic_plus"), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(18, 18), InkFaint);
                }
                int inv = net ? net.invites.Count : 0;
                if (inv > 0) { var bd = Bx(holder, "convites", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 18), new Vector2(120, 26), FBKit.Red, 13); Tx(bd.transform, $"{inv} CONVITE{(inv > 1 ? "S" : "")}", 11, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(120, 20), TextAnchor.MiddleCenter, true, Color.white); }
                else Tx(holder, "ABRIR  ›", 11, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 26), new Vector2(120, 16), TextAnchor.MiddleRight, true, Yel);
            }
            var w2 = card.gameObject.AddComponent<FBVersionWatch>(); w2.rebuild = Fill;
        }

        // -fbsquad [-fbserver ws://...]: teste do squad com amigos de mentira (unity/tools/menu_aaa/fake_friends.mjs) →
        // <build>/autoteste/squad_*.png e [FreeBala][squad] no log (sem -fbauto, fecha no fim)
        System.Collections.IEnumerator SquadShots()
        {
            var args = System.Environment.GetCommandLineArgs(); int ai = System.Array.IndexOf(args, "-fbserver");
            var dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"); System.IO.Directory.CreateDirectory(dir);
            System.Collections.IEnumerator Shot(string n) { yield return new WaitForSeconds(1.2f); yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "squad_" + n + ".png")); yield return new WaitForSeconds(.4f); }
            var net = FBLobbyNet.Ensure(); if (ai >= 0 && ai + 1 < args.Length) { FBSession.server = args[ai + 1]; net.Reconnect(); }
            sqVsBots = false; Squad();
            float t0 = Time.unscaledTime; while (net.status != FBLobbyNet.St.On && Time.unscaledTime - t0 < 15) yield return null;
            Debug.Log($"[FreeBala][squad] conexão: {net.status} {net.lastError} · meu código {FBLobbyNet.Pid}");
            yield return Shot("sozinho");
            t0 = Time.unscaledTime; while (net.seen.Values.Count(p => p.st == "menu") < 3 && Time.unscaledTime - t0 < 10) { net.Who(); yield return new WaitForSeconds(1); }
            var others = net.seen.Values.Where(p => p.pid != FBLobbyNet.Pid && p.st == "menu").ToList();
            Debug.Log("[FreeBala][squad] online: " + string.Join(", ", others.Select(p => p.name + "/" + p.pid)));
            foreach (var o in others.Where(o => o.name != "ZEZINHO")) net.AddFriend(o.pid, o.name);
            sqTab = "amigos"; yield return Shot("amigos");
            foreach (var o in others.Where(o => o.name == "VAQUEIRO" || o.name == "MARIA")) { net.InviteTo(o.pid); yield return new WaitForSeconds(.5f); }
            t0 = Time.unscaledTime; while (net.party.Count < 3 && Time.unscaledTime - t0 < 8) yield return null;
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[FreeBala][squad] squad: {net.party.Count} · líder {net.leader} · " + string.Join(", ", net.party.Select(p => p.name + (p.ready ? "(pronto)" : ""))));
            yield return Shot("cheio");
            t0 = Time.unscaledTime; while (net.invites.Count == 0 && Time.unscaledTime - t0 < 30) yield return null;
            Debug.Log($"[FreeBala][squad] convites: {net.invites.Count}");
            yield return Shot("convite");
            sqTab = "convites"; SquadRefresh(); yield return Shot("aba_convites");
            sqTab = "online"; SquadRefresh(); yield return Shot("aba_online");
            Home(); yield return Shot("inicio");
            if (System.Array.IndexOf(args, "-fbauto") < 0) Application.Quit(0);
        }

        // ================================================================ EMOTES
        // -fbmoonwalk: Michael na tela de EMOTES (catálogo com o exclusivo) + quadros do moonwalk no palco → autoteste/moonwalk_*.png; fecha no fim.
        // Não salva o personagem (o do jogador continua o mesmo).
        System.Collections.IEnumerator MoonShots()
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"); System.IO.Directory.CreateDirectory(dir);
            System.Collections.IEnumerator Shot(string n) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "moonwalk_" + n + ".png")); yield return new WaitForSeconds(.15f); }
            yield return new WaitForSeconds(2.5f); var keep = FBSession.skin; FBSession.skin = "r_michael";
            var ex = FBEmotes.ExclusiveFor("r_michael"); Debug.Log($"[FreeBala][moonwalk] exclusivos do Michael: {string.Join(",", ex)} · roda: {string.Join(",", FBEmotes.Equipped())}");
            emCat = null; Emotes(); yield return new WaitForSeconds(2.5f); yield return Shot("emotes");
            if (ex.Count > 0)
            {
                Preview(false, ex[0]); var ch = preview ? preview.GetComponent<FBCharacter>() : null; float t0 = Time.time;
                for (int k = 0; k < 8; k++) { yield return new WaitForSeconds(.28f); yield return Shot("quadro_" + k); }
                Debug.Log($"[FreeBala][moonwalk] tocando: {(ch && ch.dance != null ? ch.dance.clip + " por " + ch.dance.len.ToString("0.0") + " s" : "nada")}");
                float tEnd = Time.time; while (ch && ch.dance != null && Time.time - t0 < 12) yield return null; Debug.Log($"[FreeBala][moonwalk] durou ~{Time.time - t0:0.0} s");
            }
            FBSession.skin = "r_tutu"; Debug.Log($"[FreeBala][moonwalk] outro personagem (r_tutu) vê o exclusivo? {FBEmotes.Allowed("NPC_Moonwalk_Michael", "r_tutu")} · roda: {string.Join(",", FBEmotes.Equipped())}");
            FBSession.skin = keep; yield return new WaitForSeconds(.5f); Application.Quit(0);
        }
        static string emCat = null;
        // r62: categorias e nomes de emote em português/legíveis ("10_DANCE-LIKE SIDESTEP" → "DANCE-LIKE SIDESTEP")
        public static string CatPT(string c) => c switch { "dance" => "DANÇAS", "fight" => "LUTA", "fun" => "DIVERSÃO", "pose" => "POSES", "gun" => "ARMAS", "idle" => "PARADO", "sit" => "SENTADO", "talk" => "CONVERSA", "run" => "CORRIDA", "misc" => "OUTROS", _ => (c ?? "").ToUpper() };
        public static string EmName(string n)
        {
            n = (n ?? "").Replace('_', ' ').Trim(); n = System.Text.RegularExpressions.Regex.Replace(n, @"^\d+\s+", ""); n = System.Text.RegularExpressions.Regex.Replace(n, @"\s+", " ");
            return n.ToUpper();
        }
        void Emotes()
        {
            var p = Page("EMOTES"); var b = Box1548(p); var eq = FBEmotes.Equipped();
            var all = FBEmotes.All.Where(x => FBRegistry.I.Clip(x.id) && FBEmotes.Allowed(x.id, FBSession.skin)).OrderBy(x => string.IsNullOrEmpty(x.only) ? 1 : 0).ThenBy(x => x.cat).ThenBy(x => x.name).ToList();   // r62: exclusivo do personagem primeiro
            var cats = all.Select(x => x.cat).Distinct().ToList();
            // ---- catálogo (esquerda)
            const float LW = 560; var lp = Panel(b, "catalogo", 0, 0, LW, BH).transform;
            Head(lp, "CATÁLOGO", 22, 20, 200); FBKit.T(lp, $"{all.Count} EMOTES  ·  CLIQUE PARA EQUIPAR / TIRAR", 10, 150, 20, LW - 172, 20, TextAnchor.MiddleRight, true, FBKit.Dim);
            var chips = new List<(string k, string l)> { (null, "TODOS") }; chips.AddRange(cats.Take(5).Select(c => (c, CatPT(c))));
            float cx = 22; foreach (var (k, l) in chips) { float w = Mathf.Min(150, 26 + l.Length * 9); var kk = k; FBKit.TextTile(lp, l, cx, 54, w, 32, emCat == k, () => { emCat = kk; Emotes(); }, 11); cx += w + 6; if (cx > LW - 60) break; }
            var grid = ScrollGrid(lp, 18, 98, LW - 36, BH - 98 - 18, new Vector2(122, 150), 4, 10);
            foreach (var e in all.Where(x => emCat == null || x.cat == emCat))
            {
                var ee = e; bool on = eq.Contains(e.id); int slot = eq.IndexOf(e.id);
                var c = Cell(grid, "e_" + e.id); var im = c.gameObject.AddComponent<Image>(); im.color = on ? new Color(.12f, .34f, .72f, .75f) : new Color(.05f, .12f, .25f, .75f); FBKit.Round(im, 10);
                var o = c.gameObject.AddComponent<Outline>(); o.effectColor = on ? new Color(.5f, .76f, 1f, .95f) : FBKit.Line; o.effectDistance = new Vector2(1, -1);
                var ic = FBIcons.Emote(e.id, FBSession.skin, 128); if (ic) { var ri = FBKit.At(c, "ic", 11, 8, 100, 100).gameObject.AddComponent<RawImage>(); ri.texture = ic; ri.raycastTarget = false; }
                var nm = FBKit.T(c, EmName(e.name), 11, 6, 110, 110, 20, TextAnchor.MiddleCenter, true, Color.white); nm.resizeTextForBestFit = true; nm.resizeTextMinSize = 8; nm.resizeTextMaxSize = 11;
                FBKit.T(c, CatPT(e.cat), 9, 6, 128, 110, 14, TextAnchor.MiddleCenter, true, FBKit.Dim);
                if (!string.IsNullOrEmpty(e.only)) { var xb = FBKit.Box(c, "exclusivo", 6, 6, 74, 18, new Color(.95f, .75f, .1f, .95f), false); FBKit.Pill(xb); FBKit.T(xb.transform, "EXCLUSIVO", 9, 0, 0, 74, 18, TextAnchor.MiddleCenter, true, new Color(.1f, .08f, 0)); }
                if (on) { var bd = FBKit.Box(c, "slot", 122 - 30, 6, 24, 24, FBKit.Flag, false); FBKit.Pill(bd); FBKit.T(bd.transform, (slot + 1).ToString(), 11, 0, 0, 24, 24, TextAnchor.MiddleCenter, true, Color.white); }
                Click(im, () =>
                {
                    var l = FBEmotes.Equipped(); if (l.Contains(ee.id)) l.Remove(ee.id); else { if (l.Count >= 8) l.RemoveAt(0); l.Add(ee.id); }
                    FBSession.emoteSlots = string.Join("|", l); FBSession.Save(); Preview(false, ee.id); Emotes();
                }, .1f, 1.03f);
            }
            // ---- roda (direita)
            const float RW = 420, RX = BW - RW; var rp = Panel(b, "roda", RX, 0, RW, BH).transform;
            Head(rp, $"SUA RODA  ({eq.Count}/8)", 22, 20, 300);
            const float CX0 = RW / 2, CY0 = 250, RAD = 138, S = 92;
            var hub = FBKit.Box(rp, "centro", CX0 - 58, CY0 - 58, 116, 116, new Color(.03f, .08f, .18f, .95f)); FBKit.Round(hub, 58);
            FBKit.T(hub.transform, "B", 34, 0, 18, 116, 44, TextAnchor.MiddleCenter, true, Color.white); FBKit.T(hub.transform, "SEGURE NA\nPARTIDA", 9, 0, 62, 116, 30, TextAnchor.UpperCenter, true, FBKit.Dim);
            for (int i = 0; i < 8; i++)
            {
                float ang = Mathf.PI / 2 - i * Mathf.PI / 4; float x = CX0 + Mathf.Cos(ang) * RAD - S / 2, y = CY0 - Mathf.Sin(ang) * RAD - S / 2;
                var id = i < eq.Count ? eq[i] : null; var sl = FBKit.Box(rp, "slot" + i, x, y, S, S, id != null ? new Color(.08f, .24f, .52f, .9f) : new Color(.03f, .08f, .17f, .7f)); FBKit.Round(sl, (int)(S / 2));
                if (id == null) { FBKit.T(sl.transform, (i + 1).ToString(), 18, 0, 0, S, S, TextAnchor.MiddleCenter, true, new Color(1, 1, 1, .2f)); continue; }
                var ic = FBIcons.EmoteWhite(id, FBSession.skin, 128); if (ic) { var ri = FBKit.At(sl.transform, "ic", 10, 6, S - 20, S - 20).gameObject.AddComponent<RawImage>(); ri.texture = ic; ri.raycastTarget = false; }
                var n = FBKit.T(sl.transform, EmName(FBEmotes.NameOf(id)), 8, 4, S - 20, S - 8, 14, TextAnchor.MiddleCenter, true, Color.white); n.resizeTextForBestFit = true; n.resizeTextMinSize = 6; n.resizeTextMaxSize = 9;
                var idd = id; Click(sl, () => Preview(false, idd), .1f, 1.05f);
            }
            float vy = 470;
            Head(rp, "DANÇA DA VITÓRIA", 22, vy, 300);
            var vb = FBKit.Box(rp, "vitoria", 22, vy + 32, RW - 44, 56, new Color(.05f, .13f, .27f, .9f)); FBKit.Round(vb, 10);
            void StepV(int d) { var l = FBEmotes.Equipped(); int i = l.IndexOf(FBSession.victory ?? ""); i += d; FBSession.victory = i >= 0 && i < l.Count ? l[i] : null; if (i < -1) FBSession.victory = l.LastOrDefault(); FBSession.Save(); if (FBSession.victory != null) Preview(false, FBSession.victory); Emotes(); }
            Btn(vb.transform, "‹", 6, 8, 40, 40, () => StepV(-1), new Color(0, 0, 0, 0), Color.white, 24);
            Btn(vb.transform, "›", RW - 44 - 46, 8, 40, 40, () => StepV(1), new Color(0, 0, 0, 0), FBKit.Blue, 24);
            FBKit.T(vb.transform, FBSession.victory != null ? EmName(FBEmotes.NameOf(FBSession.victory)) : "ALEATÓRIA (DA RODA)", 16, 50, 0, RW - 44 - 100, 56, TextAnchor.MiddleCenter, true, Color.white);
            FBKit.T(rp, "Toca no fim da partida quando o seu time vence.\nClique num emote da roda para ver no personagem.", 12, 22, vy + 100, RW - 44, 44, TextAnchor.UpperLeft, false, FBKit.Dim);
            Btn(rp, "RODA PADRÃO", 22, BH - 22 - 48, RW - 44, 48, () => { FBSession.emoteSlots = ""; FBSession.Save(); Emotes(); }, new Color(.08f, .16f, .3f, .9f), Color.white, 13);
            Tx(p, "PRÉVIA  ·  O PERSONAGEM DANÇA O EMOTE ESCOLHIDO", 10, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 26), new Vector2(500, 16), TextAnchor.MiddleCenter, true, InkFaint);
        }

        // ================================================================ PASSE
        void Pass()
        {
            var p = Page("PASSE DE BATALHA", false); if (preview) preview.SetActive(false); Shade(p, Vector2.zero, Vector2.one, .5f);
            var b = Box1548(p); int tier = FBPass.Tier; FBPass.Claimed = Mathf.Min(FBPass.TIERS, tier + 1); int into = FBSession.passXp % FBPass.PER_TIER;
            // ---- faixa do topo: temporada, nível, barra
            var hd = Panel(b, "temporada", 0, 0, BW, 180).transform; hd.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var art = FBKit.Cover(hd, FBMapShot.Get("praca_aguas"), 0, 0, BW, 180, .6f); art.color = new Color(.55f, .7f, 1f, .45f);
            var sh = FBKit.At(hd, "sombra", 0, 0, BW * .7f, 180).gameObject.AddComponent<RawImage>(); sh.texture = FBLobbyStage.GradH; sh.color = new Color(1, 1, 1, .95f); sh.raycastTarget = false;
            FlagTick(hd, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, 300, 3, .95f);
            FBKit.T(hd, "TEMPORADA 1  ·  BOA VISTA", 13, 32, 28, 500, 20, TextAnchor.MiddleLeft, true, FBKit.Blue);
            var lv = FBKit.T(hd, $"NÍVEL {tier}", 52, 30, 50, 420, 64, TextAnchor.MiddleLeft, true, Color.white); lv.fontStyle = FontStyle.Italic;
            FBKit.T(hd, $"DE {FBPass.TIERS}", 16, 30 + lv.preferredWidth + 14, 80, 120, 24, TextAnchor.MiddleLeft, true, FBKit.Dim);
            var bar = FBKit.Box(hd, "barra", 32, 132, 700, 10, new Color(1, 1, 1, .12f), false); FBKit.Pill(bar);
            var fill = FBKit.Box(bar.transform, "f", 0, 0, Mathf.Max(10, 700f * into / FBPass.PER_TIER), 10, FBKit.Flag, false); FBKit.Pill(fill);
            FBKit.T(hd, $"{into} / {FBPass.PER_TIER} XP PARA O NÍVEL {Mathf.Min(FBPass.TIERS, tier + 1)}", 12, 32, 148, 700, 18, TextAnchor.MiddleLeft, true, FBKit.Dim);
            var how = new[] { ("ABATE", "+100 XP"), ("TIRO NA CABEÇA", "+150 XP"), ("PARTIDA", "+200 XP"), ("VITÓRIA", "+500 XP") };
            for (int i = 0; i < how.Length; i++)
            {
                float x = BW - 32 - (how.Length - i) * 158 + 8; var c = FBKit.Box(hd, "xp", x, 44, 150, 92, new Color(.03f, .09f, .2f, .85f)); FBKit.Round(c, 10);
                FBKit.T(c.transform, how[i].Item2, 22, 0, 18, 150, 30, TextAnchor.MiddleCenter, true, Color.white); FBKit.T(c.transform, how[i].Item1, 10, 0, 54, 150, 16, TextAnchor.MiddleCenter, true, FBKit.Dim);
            }
            // ---- faixa de níveis com rolagem horizontal
            Head(b, "RECOMPENSAS", 0, 204, 400);
            FBKit.T(b, "ARRASTE PARA OS LADOS  ·  A CADA 5 NÍVEIS UM PRÊMIO ESPECIAL", 10, BW - 600, 204, 600, 20, TextAnchor.MiddleRight, true, FBKit.Dim);
            const float CW = 190, CH = 430, G = 14; float trackY = 240;   // r62: mais altos, sem o vazio embaixo
            var view = FBKit.At(b, "faixa", 0, trackY, BW, CH + 40).gameObject.AddComponent<Image>(); view.color = new Color(0, 0, 0, 0); view.raycastTarget = true; view.gameObject.AddComponent<RectMask2D>();
            var content = FBUI.Rect(view.transform, "c", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(FBPass.TIERS * (CW + G) + G, CH + 40)); content.pivot = new Vector2(0, 1); content.anchoredPosition = Vector2.zero;
            var sr = view.gameObject.AddComponent<ScrollRect>(); sr.content = content; sr.vertical = false; sr.horizontal = true; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 50; sr.viewport = view.rectTransform;
            for (int i = 0; i < FBPass.TIERS; i++)
            {
                bool got = i < tier, next = i == tier, big = i % 5 == 4; float x = G + i * (CW + G);
                Color bgc = got ? new Color(.1f, .3f, .64f, .9f) : next ? new Color(.06f, .17f, .36f, .95f) : new Color(.03f, .08f, .17f, .8f);
                var c = FBKit.Box(content, "nivel" + (i + 1), x, 14, CW, CH, bgc); FBKit.Round(c, 14); c.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                if (next) { c.GetComponent<Outline>().effectColor = new Color(1, 1, 1, .9f); c.GetComponent<Outline>().effectDistance = new Vector2(2, -2); }
                if (big) { var gl = FBKit.At(c.transform, "brilho", -50, 40, CW + 100, 260).gameObject.AddComponent<RawImage>(); gl.texture = T("glow"); gl.color = new Color(.2f, .85f, .45f, got ? .55f : .3f); gl.raycastTarget = false; }
                FBKit.T(c.transform, (i + 1).ToString(), 13, 14, 12, 60, 20, TextAnchor.MiddleLeft, true, got ? Color.white : FBKit.Dim);
                if (got) { var ck = FBKit.Box(c.transform, "ok", CW - 34, 10, 24, 24, FBKit.Flag, false); FBKit.Pill(ck); FBKit.T(ck.transform, "✓", 13, 0, 0, 24, 24, TextAnchor.MiddleCenter, true, Color.white); }
                // prêmio
                if (big)
                {
                    var ico = FBKit.At(c.transform, "premio", CW / 2 - 62, 96, 124, 124).gameObject.AddComponent<RawImage>(); ico.texture = T("ic_crown"); ico.color = got ? Color.white : new Color(1, 1, 1, .45f); ico.raycastTarget = false;
                    FBKit.T(c.transform, "PRÊMIO", 24, 0, 250, CW, 32, TextAnchor.MiddleCenter, true, Color.white); FBKit.T(c.transform, "ESPECIAL", 12, 0, 284, CW, 18, TextAnchor.MiddleCenter, true, new Color(.45f, 1f, .6f));
                }
                else
                {
                    var ico = FBKit.At(c.transform, "cash", CW / 2 - 58, 100, 116, 116).gameObject.AddComponent<RawImage>(); ico.texture = T("cash"); ico.color = got ? Color.white : new Color(1, 1, 1, .45f); ico.raycastTarget = false;
                    FBKit.T(c.transform, "$ " + (100 + i * 20), 28, 0, 250, CW, 34, TextAnchor.MiddleCenter, true, got ? Color.white : FBKit.Txt); FBKit.T(c.transform, "DINHEIRO", 11, 0, 286, CW, 16, TextAnchor.MiddleCenter, true, FBKit.Dim);
                }
                var foot = FBKit.Box(c.transform, "estado", 0, CH - 52, CW, 52, got ? new Color(.08f, .45f, .22f, .95f) : next ? new Color(.12f, .32f, .66f, .95f) : new Color(.02f, .05f, .11f, .9f), false);
                FBKit.T(foot.transform, got ? "LIBERADO" : next ? "PRÓXIMO" : "BLOQUEADO", 13, 0, 0, CW, 52, TextAnchor.MiddleCenter, true, got || next ? Color.white : FBKit.Dim);
            }
            // abre centrado no nível atual
            float target = Mathf.Clamp(G + tier * (CW + G) - BW / 2 + CW / 2, 0, Mathf.Max(0, content.sizeDelta.x - BW)); content.anchoredPosition = new Vector2(-target, 0);
            Btn(b, "‹", 0, trackY + CH / 2 - 10, 44, 64, () => { content.anchoredPosition = new Vector2(Mathf.Min(0, content.anchoredPosition.x + 5 * (CW + G)), 0); }, new Color(.03f, .08f, .18f, .92f), Color.white, 26);
            Btn(b, "›", BW - 44, trackY + CH / 2 - 10, 44, 64, () => { content.anchoredPosition = new Vector2(Mathf.Max(-(content.sizeDelta.x - BW), content.anchoredPosition.x - 5 * (CW + G)), 0); }, new Color(.03f, .08f, .18f, .92f), FBKit.Blue, 26);
            FBKit.T(b, "O passe sobe sozinho com o XP das partidas: abates, tiros na cabeça, partidas jogadas e vitórias. Os prêmios liberados entram na hora.", 13, 0, BH - 36, BW, 24, TextAnchor.MiddleCenter, false, FBKit.Dim);
        }
    }

    // redesenha um pedaço da tela quando o lobby online muda (squad, amigos, convites, conexão)
    public class FBVersionWatch : MonoBehaviour
    {
        public System.Action rebuild; int v = int.MinValue; float t = 1;
        void Update()
        {
            var n = FBLobbyNet.I; int cur = n ? n.version : -1; t += Time.unscaledDeltaTime;
            if (cur == v || t < .25f) return; v = cur; t = 0; rebuild?.Invoke();
        }
    }

    // r68: redesenha quando a chave muda (tela REDE LOCAL: conexão, procura na rede, jogadores no Host)
    public class FBKeyWatch : MonoBehaviour
    {
        public System.Func<int> key; public System.Action rebuild; int v; float t;
        void Start() { v = key != null ? key() : 0; }   // o primeiro desenho é feito por quem cria
        void Update()
        {
            t += Time.unscaledDeltaTime; if (key == null || t < .25f) return; t = 0;
            int cur = key(); if (cur == v) return; v = cur; rebuild?.Invoke();
        }
    }

    // avisos do lobby por cima de tudo: convite de squad (ACEITAR / RECUSAR, some em 20 s) e mensagens curtas do servidor
    public class FBMenuAvisos : MonoBehaviour
    {
        Transform root; Text toastT; CanvasGroup toastG; GameObject invCard; FBLobbyNet.Invite shown; float toastSeen = -1;
        public static FBMenuAvisos Create()
        {
            var cv = FBUI.Canvas("LobbyAvisos", 60); var a = cv.gameObject.AddComponent<FBMenuAvisos>(); a.root = cv.transform;
            var tb = FBUI.Panel(cv.transform, "aviso", new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -104), new Vector2(620, 44), new Color(.03f, .1f, .24f, .96f)); FBKit.Round(tb, 22); tb.raycastTarget = false;
            a.toastT = FBUI.Label(tb.transform, "", 15, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-30, 0), TextAnchor.MiddleCenter, true, Color.white);
            a.toastG = tb.gameObject.AddComponent<CanvasGroup>(); a.toastG.alpha = 0; a.toastG.blocksRaycasts = false;
            return a;
        }
        void Update()
        {
            var n = FBLobbyNet.I; if (!n) return;
            if (n.toastT != toastSeen && !string.IsNullOrEmpty(n.toast)) { toastSeen = n.toastT; toastT.text = n.toast; }
            float age = Time.unscaledTime - toastSeen; toastG.alpha = toastSeen < 0 ? 0 : Mathf.Clamp01(Mathf.Min(age * 6, (3.2f - age) * 3));
            var iv = n.invites.LastOrDefault(x => Time.unscaledTime - x.t < 20);
            if (iv != shown) { if (invCard) Destroy(invCard); shown = iv; if (iv != null) ShowInvite(n, iv); }
        }
        void ShowInvite(FBLobbyNet n, FBLobbyNet.Invite iv)
        {
            const float W = 470, H = 132;
            var card = FBUI.Panel(root, "convite", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-W / 2 - 36, -H / 2 - 96), new Vector2(W, H), new Color(.025f, .07f, .16f, .97f)); FBKit.Frame(card, 14); invCard = card.gameObject; card.raycastTarget = true;
            FBFadeIn.Add(card, 0, -14);
            var t = card.transform; var fl = FBUI.Rect(t, "bandeira", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -1.5f), new Vector2(0, 3)); var fi = fl.gameObject.AddComponent<Image>(); fi.color = FBKit.Flag; fi.raycastTarget = false;
            var ring = FBKit.Box(t, "aro", 18, 22, 60, 60, FBKit.Blue, false); FBKit.Round(ring, 12); var mk = FBKit.Box(ring.transform, "recorte", 3, 3, 54, 54, Color.white, false); FBKit.Round(mk, 10); mk.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var tex = (string.IsNullOrEmpty(iv.skin) ? null : FBCustomizeThumbs.Get(iv.skin)) ?? Resources.Load<Texture2D>("UI/Lobby/avatar"); FBKit.Cover(mk.transform, tex, 0, 0, 54, 54, .9f);
            FBKit.T(t, "CONVITE DE SQUAD", 11, 92, 20, 300, 16, TextAnchor.MiddleLeft, true, FBKit.Blue);
            FBKit.T(t, (iv.name ?? "").ToUpper() + " te chamou", 18, 92, 38, W - 110, 26, TextAnchor.MiddleLeft, true, Color.white).FitTo(18);
            Button B(string s, float x, float w, Color c, System.Action a) { var im = FBKit.Box(t, "b_" + s, x, H - 16 - 40, w, 40, c, false); FBKit.Round(im, 8); im.raycastTarget = true; var b = im.gameObject.AddComponent<Button>(); b.onClick.AddListener(() => { FBAudio.Synth("hover"); a(); }); FBHover.Add(im, .1f, 1.03f); FBKit.T(im.transform, s, 14, 0, 0, w, 40, TextAnchor.MiddleCenter, true, Color.white); return b; }
            B("RECUSAR", 92, 130, new Color(.1f, .18f, .32f, .95f), () => n.Decline(iv));
            B("ACEITAR", W - 18 - 200, 200, FBKit.Flag, () => { n.Accept(iv); if (FBMenu.I) FBMenu.I.OpenSquad(); });
        }
    }

    // r62: ajuste automático de texto que só ENCOLHE (sem limite o Unity crescia o nome até ~40 px e estourava a caixa)
    public static class FBTextExt { public static Text FitTo(this Text t, int max, int min = 9) { t.resizeTextForBestFit = true; t.resizeTextMaxSize = max; t.resizeTextMinSize = Mathf.Min(min, max); return t; } }
}
