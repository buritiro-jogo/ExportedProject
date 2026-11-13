// Mapa tático da partida em tela cheia (FBMatchMap):

// Abre e fecha pela tecla [M], pelo clique no minimapa ou pelo botão na HUD.

// Exibe em tempo real a foto ortográfica do mapa, limites da área jogável,

// zona de dano (vermelho/perigo), zona segura atual (ciano/azul),

// próxima zona segura (âmbar/dourado pulsante com contagem), posição e orientação do jogador,

// aliados, pontos de conquista e legenda explicativa.

using System.Collections.Generic;

using System.Linq;

using UnityEngine;

using UnityEngine.UI;



namespace FreeBala

{

    public class FBMatchMap : MonoBehaviour

    {

        public static FBMatchMap I { get; private set; }

        public static bool Open => I != null && I.gameObject.activeSelf;



        Canvas canvas;

        RectTransform root, mapFrame, mapContainer;

        RawImage mapImg;

        Text titleText, zoneStatusText, subText, hintText;

        Image playerArrow, zoneCurrentRing, zoneNextRing, arenaRing, playBoundsBox;

        Text playerLbl;



        readonly List<Image> allyDots = new();

        readonly List<Text> allyLabels = new();

        readonly List<Image> tacDots = new();

        readonly List<Text> tacLabels = new();

        readonly List<Image> shotPings = new();
        Image heliScanZone, heliScanRing;
        readonly List<Image> taggedDots = new();
        readonly List<Text> taggedLabels = new();



        static Sprite discSpr, ringSpr, arrowSpr, boxSpr;

        static Sprite DiscSpr => discSpr ? discSpr : discSpr = FBUI.CircleSprite;

        static Sprite RingSpr => ringSpr ? ringSpr : ringSpr = GenRing();

        static Sprite ArrowSpr => arrowSpr ? arrowSpr : arrowSpr = FBUI.ArrowSprite;

        static Sprite BoxSpr => boxSpr ? boxSpr : boxSpr = FBUI.WhiteSprite;



        static Sprite GenRing()

        {

            int n = 128;

            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

            var px = new Color[n * n];

            for (int y = 0; y < n; y++)

            {

                for (int x = 0; x < n; x++)

                {

                    float nx = (x + 0.5f) / n * 2 - 1, ny = (y + 0.5f) / n * 2 - 1;

                    float d = Mathf.Sqrt(nx * nx + ny * ny);

                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.92f) * 16f);

                    px[y * n + x] = new Color(1, 1, 1, a);

                }

            }

            t.SetPixels(px); t.Apply();

            return Sprite.Create(t, new Rect(0, 0, n, n), Vector2.one * 0.5f);

        }



        public static void Ensure()

        {

            if (I) return;

            var g = new GameObject("FBMatchMap");

            DontDestroyOnLoad(g);

            I = g.AddComponent<FBMatchMap>();

            I.Build();

            g.SetActive(false);

        }



        static int lastToggleFrame = -1;

        public static void Toggle()

        {

            if (Time.frameCount == lastToggleFrame) return;

            lastToggleFrame = Time.frameCount;

            Ensure();

            if (Open) Hide();

            else Show();

        }



        public static void Show()

        {

            Ensure();

            if (!I) return;

            I.gameObject.SetActive(true);

            if (!FBMinimap.Tex) FBMinimap.Capture();

            Cursor.lockState = CursorLockMode.None;

            Cursor.visible = true;

            Canvas.ForceUpdateCanvases();

            I.Refresh(true);

        }



        public static void Close() => Hide();

        public static void Hide()

        {

            if (!I || !I.gameObject.activeSelf) return;

            I.gameObject.SetActive(false);

            if (FBMatch.I && FBMatch.I.state == "play" && !FBMatch.Paused && !FBHUD.CardOpen && !FBShop.Open && !FBBag.Open && !FBGarage.Open_ && !FBInput.Touch)

            {

                Cursor.lockState = CursorLockMode.Locked;

                Cursor.visible = false;

            }

        }



        void Build()

        {

            canvas = FBUI.Canvas("Canvas_MatchMap", 45);

            canvas.transform.SetParent(transform, false);



            // Fundo escuro translúcido com clique para fechar

            var bgImg = FBUI.Panel(canvas.transform, "bg_overlay", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.02f, 0.03f, 0.04f, 0.88f));

            var bgBtn = bgImg.gameObject.AddComponent<Button>();

            bgBtn.onClick.AddListener(Hide);



            // Janela central responsiva (720px no canvas padrão 1600x900)

            const float sz = 720f;

            root = FBUI.Rect(bgImg.transform, "janela_mapa", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(sz + 40, sz + 110));

            var rootPanel = root.gameObject.AddComponent<Image>();

            rootPanel.color = new Color(0.06f, 0.08f, 0.1f, 0.96f);

            rootPanel.sprite = FBUI.Round(12);

            rootPanel.type = Image.Type.Sliced;



            // Cabeçalho

            var header = FBUI.Rect(root, "cabecalho", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -32), new Vector2(-30, 54));

            titleText = FBUI.Label(header, "MAPA TÁTICO", 22, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 8), new Vector2(400, 26), TextAnchor.MiddleLeft, true, FBUI.Gold);

            subText = FBUI.Label(header, "", 13, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, -14), new Vector2(400, 20), TextAnchor.MiddleLeft, false, FBUI.Mut);



            zoneStatusText = FBUI.Label(header, "ZONA SEGURA ATIVA", 15, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-60, 0), new Vector2(360, 28), TextAnchor.MiddleRight, true, new Color(0.35f, 0.85f, 1f));



            // Botão Fechar (X)

            var closeBtn = FBUI.Button(header, "✕", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(36, 36), Hide);

            var cb = closeBtn.colors;

            cb.normalColor = new Color(1, 1, 1, 0.12f);

            cb.highlightedColor = new Color(0.9f, 0.2f, 0.2f, 0.8f);

            closeBtn.colors = cb;



            // Moldura do Mapa

            mapFrame = FBUI.Rect(root, "moldura_mapa", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -4), new Vector2(sz, sz));

            var frameImg = mapFrame.gameObject.AddComponent<Image>();

            frameImg.color = new Color(0.08f, 0.12f, 0.16f, 1f);

            frameImg.sprite = FBUI.Round(8);

            frameImg.type = Image.Type.Sliced;



            // Container com máscara para clipping

            var maskGo = new GameObject("map_mask", typeof(RectTransform));

            mapContainer = (RectTransform)maskGo.transform;

            mapContainer.SetParent(mapFrame, false);

            mapContainer.anchorMin = Vector2.zero;

            mapContainer.anchorMax = Vector2.one;

            mapContainer.sizeDelta = new Vector2(-6, -6);

            var maskImg = maskGo.AddComponent<Image>();

            maskImg.sprite = FBUI.Round(6);

            maskImg.type = Image.Type.Sliced;

            var mask = maskGo.AddComponent<Mask>();

            mask.showMaskGraphic = false;



            // Imagem do Mapa Ortográfico

            var imgGo = new GameObject("map_img", typeof(RectTransform));

            var imgRt = (RectTransform)imgGo.transform;

            imgRt.SetParent(mapContainer, false);

            imgRt.anchorMin = Vector2.zero;

            imgRt.anchorMax = Vector2.one;

            imgRt.sizeDelta = Vector2.zero;

            mapImg = imgGo.AddComponent<RawImage>();

            mapImg.color = Color.white;



            // Retângulo de limites jogáveis reais

            var pbGo = new GameObject("playable_bounds", typeof(RectTransform));

            var pbRt = (RectTransform)pbGo.transform;

            pbRt.SetParent(mapContainer, false);

            playBoundsBox = pbGo.AddComponent<Image>();

            playBoundsBox.color = new Color(0.2f, 0.8f, 1f, 0.35f);

            playBoundsBox.sprite = RingSpr;

            playBoundsBox.type = Image.Type.Sliced;

            playBoundsBox.raycastTarget = false;



            // Círculo da Arena (quando aplicável)

            var arGo = new GameObject("arena_ring", typeof(RectTransform));

            arenaRing = arGo.AddComponent<Image>();

            arenaRing.transform.SetParent(mapContainer, false);

            arenaRing.sprite = RingSpr;

            arenaRing.color = new Color(1f, 0.7f, 0.2f, 0.45f);

            arenaRing.raycastTarget = false;

            arenaRing.gameObject.SetActive(false);



            // Anel da Próxima Zona (âmbar / dourado)

            var znGo = new GameObject("zone_next_ring", typeof(RectTransform));

            zoneNextRing = znGo.AddComponent<Image>();

            zoneNextRing.transform.SetParent(mapContainer, false);

            zoneNextRing.sprite = RingSpr;

            zoneNextRing.color = new Color(1f, 0.82f, 0.25f, 0.95f);

            zoneNextRing.raycastTarget = false;

            zoneNextRing.gameObject.SetActive(false);



            // Anel da Zona Segura Atual (ciano / neon)

            var zcGo = new GameObject("zone_current_ring", typeof(RectTransform));

            zoneCurrentRing = zcGo.AddComponent<Image>();

            zoneCurrentRing.transform.SetParent(mapContainer, false);

            zoneCurrentRing.sprite = RingSpr;

            zoneCurrentRing.color = new Color(0.25f, 0.85f, 1f, 0.95f);

            zoneCurrentRing.raycastTarget = false;

            zoneCurrentRing.gameObject.SetActive(false);

            // Varredura Aérea do Helicóptero (Reconhecimento / Danger-Zone de Radar)
            var hzGo = new GameObject("heli_scan_zone", typeof(RectTransform));
            heliScanZone = hzGo.AddComponent<Image>();
            heliScanZone.transform.SetParent(mapContainer, false);
            heliScanZone.sprite = DiscSpr;
            heliScanZone.color = new Color(1f, 0.45f, 0.1f, 0.22f);
            heliScanZone.raycastTarget = false;
            heliScanZone.gameObject.SetActive(false);

            var hrGo = new GameObject("heli_scan_ring", typeof(RectTransform));
            heliScanRing = hrGo.AddComponent<Image>();
            heliScanRing.transform.SetParent(mapContainer, false);
            heliScanRing.sprite = RingSpr;
            heliScanRing.color = new Color(1f, 0.55f, 0.1f, 0.88f);
            heliScanRing.raycastTarget = false;
            heliScanRing.gameObject.SetActive(false);



            // Seta do Jogador

            var plGo = new GameObject("player_marker", typeof(RectTransform));

            var plRt = (RectTransform)plGo.transform;

            plRt.SetParent(mapContainer, false);

            plRt.sizeDelta = new Vector2(24, 24);

            playerArrow = plGo.AddComponent<Image>();

            playerArrow.sprite = ArrowSpr;

            playerArrow.color = FBUI.Gold;

            playerArrow.raycastTarget = false;

            var ol = plGo.AddComponent<Outline>();

            ol.effectColor = new Color(0, 0, 0, 0.9f);

            ol.effectDistance = new Vector2(1.5f, -1.5f);



            var plLblGo = new GameObject("player_lbl", typeof(RectTransform));

            var plLblRt = (RectTransform)plLblGo.transform;

            plLblRt.SetParent(plGo.transform, false);

            plLblRt.anchoredPosition = new Vector2(0, -18);

            plLblRt.sizeDelta = new Vector2(80, 16);

            playerLbl = plLblGo.AddComponent<Text>();

            playerLbl.font = FBUI.Font;

            playerLbl.fontSize = 11;

            playerLbl.alignment = TextAnchor.MiddleCenter;

            playerLbl.color = FBUI.Gold;

            playerLbl.text = "VOCÊ";

            var olLbl = plLblGo.AddComponent<Outline>();

            olLbl.effectColor = Color.black;

            olLbl.effectDistance = new Vector2(1, -1);



            // Rodapé com Legenda

            var footer = FBUI.Rect(root, "rodape", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 24), new Vector2(-20, 40));

            BuildLegend(footer);

        }



        void BuildLegend(RectTransform parent)

        {

            var row = FBUI.Rect(parent, "legend_row", new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(0, 30));



            void AddItem(string label, Color c, Sprite sp, bool isArrow = false)

            {

                var item = new GameObject("leg_" + label, typeof(RectTransform));

                var ir = (RectTransform)item.transform;

                ir.SetParent(row, false);

                ir.sizeDelta = new Vector2(130, 24);



                var icon = new GameObject("icon", typeof(RectTransform)).AddComponent<Image>();

                icon.transform.SetParent(item.transform, false);

                ((RectTransform)icon.transform).anchoredPosition = new Vector2(-48, 0);

                ((RectTransform)icon.transform).sizeDelta = new Vector2(14, 14);

                icon.sprite = sp;

                icon.color = c;

                icon.raycastTarget = false;



                var txt = FBUI.Label(item.transform, label, 12, new Vector2(0, 0), new Vector2(1, 1), new Vector2(10, 0), new Vector2(0, 0), TextAnchor.MiddleLeft, false, Color.white);

            }



            var hLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();

            hLayout.childAlignment = TextAnchor.MiddleCenter;

            hLayout.childForceExpandWidth = false;

            hLayout.childForceExpandHeight = false;

            hLayout.spacing = 16;



            AddItem("Zona Atual", new Color(0.25f, 0.85f, 1f), RingSpr);

            AddItem("Próxima Zona", new Color(1f, 0.82f, 0.25f), RingSpr);

            AddItem("Zona de Dano", new Color(0.95f, 0.25f, 0.25f), BoxSpr);

            AddItem("Você", FBUI.Gold, ArrowSpr, true);

            AddItem("Aliados", new Color(0.3f, 0.65f, 1f), DiscSpr);
            AddItem("Varredura Aérea", new Color(1f, 0.55f, 0.1f), RingSpr);
            AddItem("Alvo Revelado", new Color(1f, 0.2f, 0.2f), DiscSpr);



            hintText = FBUI.Label(parent, "[M] ou [ESC] para fechar", 11, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-10, -10), new Vector2(200, 16), TextAnchor.MiddleRight, false, FBUI.Mut);

        }



        const float szFallback = 714f;

        Vector2 WorldToMap(Vector3 worldPos)

        {

            if (mapContainer == null) return Vector2.zero;

            var uv = FBMinimap.UV(worldPos);

            var sz = mapContainer.rect.size;

            if (sz.x < 10 || sz.y < 10) sz = new Vector2(szFallback, szFallback);

            return new Vector2((uv.x - 0.5f) * sz.x, (uv.y - 0.5f) * sz.y);

        }



        float WorldRadiusToMap(float radius)

        {

            if (mapContainer == null) return 10f;

            float mapWorldSize = Mathf.Max(10f, FBMinimap.Size);

            float w = mapContainer.rect.size.x;

            if (w < 10) w = szFallback;

            return (radius * 2f / mapWorldSize) * w;

        }



        void Update()

        {

            if (!Open) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;

            if ((kb != null && (kb.mKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) || Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.Escape))

            {

                Hide();

                return;

            }

            Refresh(false);

        }



        public void Refresh(bool initial)

        {

            var match = FBMatch.I;

            var pl = FBMatch.Player;



            // Textura do minimapa (ortográfica) com fallback garantido

            if (!FBMinimap.Tex) FBMinimap.Capture();

            if (FBMinimap.Tex && mapImg.texture != FBMinimap.Tex)

            {

                mapImg.texture = FBMinimap.Tex;

            }

            if (!mapImg.texture)

            {

                var fallbackShot = FBMapShot.Get((FBMatch.I ? FBMatch.I.mapId : FBSession.map));

                if (fallbackShot) mapImg.texture = fallbackShot;

            }



            // Título e modo

            string mapName = match != null ? match.MapName.ToUpperInvariant() : "MAPA";

            string modeName = match?.mode != null ? match.mode.name.ToUpperInvariant() : "PARTIDA";

            if (match?.mode is FBModes.Squad sq)

            {

                modeName = $"4V4 ZONA · RODADA {sq.Round}";

            }

            titleText.text = $"{mapName} · {modeName}";



            // Limites jogáveis

            var pb = FBWorld.GetPlayableBounds(match ? match.mapId : null);

            var pbCenter = WorldToMap(pb.center);

            float pbW = (pb.size.x / Mathf.Max(10f, FBMinimap.Size)) * mapContainer.rect.size.x;

            float pbH = (pb.size.z / Mathf.Max(10f, FBMinimap.Size)) * mapContainer.rect.size.y;

            playBoundsBox.rectTransform.anchoredPosition = pbCenter;

            playBoundsBox.rectTransform.sizeDelta = new Vector2(pbW, pbH);



            // Zonas

            var zone = match?.mode?.Zone;

            var arena = match?.arena;



            // Arena circular fixa (quando existir)

            if (arena != null && zone == null)

            {

                arenaRing.gameObject.SetActive(true);

                arenaRing.rectTransform.anchoredPosition = WorldToMap(arena.c);

                float ad = WorldRadiusToMap(arena.r);

                arenaRing.rectTransform.sizeDelta = Vector2.one * ad;

            }

            else

            {

                arenaRing.gameObject.SetActive(false);

            }



            if (zone != null)

            {

                // Zona segura atual (Ciano)

                zoneCurrentRing.gameObject.SetActive(true);

                zoneCurrentRing.rectTransform.anchoredPosition = WorldToMap(zone.c);

                float cd = WorldRadiusToMap(zone.r);

                zoneCurrentRing.rectTransform.sizeDelta = Vector2.one * Mathf.Max(6f, cd);



                // Próxima zona (Âmbar pulsante)

                if (zone.to != null)

                {

                    zoneNextRing.gameObject.SetActive(true);

                    var (nextCenter, nextRadius) = zone.to.Value;

                    zoneNextRing.rectTransform.anchoredPosition = WorldToMap(nextCenter);

                    float nd = WorldRadiusToMap(nextRadius);

                    zoneNextRing.rectTransform.sizeDelta = Vector2.one * Mathf.Max(4f, nd);



                    float pulse = Mathf.PingPong(Time.unscaledTime * 3f, 0.4f) + 0.6f;

                    zoneNextRing.color = new Color(1f, 0.82f, 0.25f, pulse);

                }

                else

                {

                    zoneNextRing.gameObject.SetActive(false);

                }



                // Banner de status da zona

                if (zone.st == "shrink")

                {

                    int sec = Mathf.CeilToInt(Mathf.Max(0, zone.t));

                    zoneStatusText.text = $"⚠️ ZONA FECHANDO EM {sec}s";

                    zoneStatusText.color = new Color(1f, 0.5f, 0.2f);

                }

                else if (zone.to != null)

                {

                    int sec = Mathf.CeilToInt(Mathf.Max(0, zone.t));

                    zoneStatusText.text = $"⏱️ PRÓXIMA ZONA EM {sec}s";

                    zoneStatusText.color = new Color(0.35f, 0.85f, 1f);

                }

                else

                {

                    zoneStatusText.text = "ZONA FINAL";

                    zoneStatusText.color = new Color(1f, 0.3f, 0.3f);

                }



                subText.text = $"Dano fora: {Mathf.RoundToInt(zone.dmg)}/s · Raio seguro: {Mathf.RoundToInt(zone.r)}m";

            }

            else if (arena != null)

            {

                zoneCurrentRing.gameObject.SetActive(true);

                zoneCurrentRing.rectTransform.anchoredPosition = WorldToMap(arena.c);

                float cd = WorldRadiusToMap(arena.r);

                zoneCurrentRing.rectTransform.sizeDelta = Vector2.one * Mathf.Max(6f, cd);

                zoneNextRing.gameObject.SetActive(false);

                zoneStatusText.text = "ÁREA DE COMBATE DELIMITADA";

                zoneStatusText.color = new Color(0.35f, 0.85f, 1f);

                subText.text = $"Raio da área: {Mathf.RoundToInt(arena.r)}m";

            }

            else

            {

                zoneCurrentRing.gameObject.SetActive(false);

                zoneNextRing.gameObject.SetActive(false);

                zoneStatusText.text = "SEM RESTRIÇÃO DE ZONA";

                zoneStatusText.color = FBUI.Mut;

                subText.text = "Área aberta";

            }



            // Posição e rotação do jogador

            if (pl)

            {

                playerArrow.gameObject.SetActive(true);

                playerArrow.rectTransform.anchoredPosition = WorldToMap(pl.Pos);

                float yaw = FBPlayer.I != null ? FBPlayer.I.ViewYaw : pl.transform.eulerAngles.y;

                playerArrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -yaw);

            }

            else

            {

                playerArrow.gameObject.SetActive(false);

            }



            // Aliados

            int aIdx = 0;

            foreach (var actor in FBMatch.Actors())

            {

                if (actor.isPlayer || !actor.alive || actor.team != FBSession.team) continue;



                if (aIdx >= allyDots.Count)

                {

                    var dotGo = new GameObject("ally_" + aIdx, typeof(RectTransform));

                    var dotRt = (RectTransform)dotGo.transform;

                    dotRt.SetParent(mapContainer, false);

                    dotRt.sizeDelta = new Vector2(16, 16);

                    var img = dotGo.AddComponent<Image>();

                    img.sprite = ArrowSpr;

                    img.color = new Color(0.25f, 0.65f, 1f);

                    img.raycastTarget = false;

                    var aol = dotGo.AddComponent<Outline>();

                    aol.effectColor = Color.black;

                    aol.effectDistance = new Vector2(1, -1);

                    allyDots.Add(img);



                    var lblGo = new GameObject("lbl", typeof(RectTransform));

                    var lRt = (RectTransform)lblGo.transform;

                    lRt.SetParent(dotGo.transform, false);

                    lRt.anchoredPosition = new Vector2(0, -14);

                    lRt.sizeDelta = new Vector2(60, 14);

                    var txt = lblGo.AddComponent<Text>();

                    txt.font = FBUI.Font;

                    txt.fontSize = 10;

                    txt.alignment = TextAnchor.MiddleCenter;

                    txt.color = new Color(0.7f, 0.85f, 1f);

                    var lol = lblGo.AddComponent<Outline>();

                    lol.effectColor = Color.black;

                    lol.effectDistance = new Vector2(1, -1);

                    allyLabels.Add(txt);

                }



                var dot = allyDots[aIdx];

                var lbl = allyLabels[aIdx];

                dot.gameObject.SetActive(true);

                dot.rectTransform.anchoredPosition = WorldToMap(actor.Pos);

                dot.rectTransform.localRotation = Quaternion.Euler(0, 0, -actor.Yaw);

                lbl.text = actor.Name;

                aIdx++;

            }

            for (int i = aIdx; i < allyDots.Count; i++) allyDots[i].gameObject.SetActive(false);



            // Pontos de Conquista (Modo Tático)

            int pIdx = 0;

            var points = match?.mode?.Points;

            if (points != null && points.Count > 0)

            {

                for (int i = 0; i < points.Count; i++)

                {

                    var pt = points[i];

                    if (pIdx >= tacDots.Count)

                    {

                        var tGo = new GameObject("tac_" + pIdx, typeof(RectTransform));

                        var tRt = (RectTransform)tGo.transform;

                        tRt.SetParent(mapContainer, false);

                        tRt.sizeDelta = new Vector2(22, 22);

                        var img = tGo.AddComponent<Image>();

                        img.sprite = DiscSpr;

                        img.raycastTarget = false;

                        var tol = tGo.AddComponent<Outline>();

                        tol.effectColor = Color.black;

                        tol.effectDistance = new Vector2(1.2f, -1.2f);

                        tacDots.Add(img);



                        var lGo = new GameObject("txt", typeof(RectTransform));

                        var lRt = (RectTransform)lGo.transform;

                        lRt.SetParent(tGo.transform, false);

                        lRt.anchoredPosition = Vector2.zero;

                        lRt.sizeDelta = new Vector2(22, 22);

                        var txt = lGo.AddComponent<Text>();

                        txt.font = FBUI.Title;

                        txt.fontSize = 13;

                        txt.alignment = TextAnchor.MiddleCenter;

                        txt.color = Color.white;

                        tacLabels.Add(txt);

                    }



                    var dot = tacDots[pIdx];

                    var lbl = tacLabels[pIdx];

                    dot.gameObject.SetActive(true);

                    dot.rectTransform.anchoredPosition = WorldToMap(new Vector3(pt.x, pt.y, pt.z));

                    // 0: neutro, 1: nosso, 2: deles, 3: disputado

                    Color c = pt.w < 0.5f ? new Color(0.7f, 0.7f, 0.7f) : pt.w < 1.5f ? new Color(0.2f, 0.5f, 1f) : pt.w < 2.5f ? new Color(0.95f, 0.25f, 0.25f) : FBUI.Gold;

                    dot.color = c;

                    string[] IDS = { "A", "B", "C", "D", "E" };

                    lbl.text = i < IDS.Length ? IDS[i] : (i + 1).ToString();

                    pIdx++;

                }

            }

            for (int i = pIdx; i < tacDots.Count; i++) tacDots[i].gameObject.SetActive(false);



            // Tiros recentes de inimigos (pings vermelhos)

            int sIdx = 0;

            foreach (var actor in FBMatch.Actors())

            {

                if (actor.isPlayer || !actor.alive || actor.team == FBSession.team || actor.lastShot >= 2.5f) continue;

                if (sIdx >= shotPings.Count)

                {

                    var spGo = new GameObject("ping_" + sIdx, typeof(RectTransform));

                    var spRt = (RectTransform)spGo.transform;

                    spRt.SetParent(mapContainer, false);

                    spRt.sizeDelta = new Vector2(18, 18);

                    var img = spGo.AddComponent<Image>();

                    img.sprite = DiscSpr;

                    img.color = new Color(1f, 0.25f, 0.2f, 0.85f);

                    img.raycastTarget = false;

                    shotPings.Add(img);

                }

                var ping = shotPings[sIdx];

                ping.gameObject.SetActive(true);

                ping.rectTransform.anchoredPosition = WorldToMap(actor.Pos);

                float alpha = (1f - actor.lastShot / 2.5f);

                ping.color = new Color(1f, 0.25f, 0.2f, alpha * 0.9f);

                sIdx++;

            }

            for (int i = sIdx; i < shotPings.Count; i++) shotPings[i].gameObject.SetActive(false);

            // Helicóptero - Reconhecimento Aéreo (Radar / Danger Zone)
            bool scanning = FBHeliRecon.IsScanning;
            if (heliScanZone) heliScanZone.gameObject.SetActive(scanning);
            if (heliScanRing) heliScanRing.gameObject.SetActive(scanning);
            if (scanning && heliScanZone && heliScanRing)
            {
                Vector2 scanPos = WorldToMap(FBHeliRecon.ScanPosition);
                heliScanZone.rectTransform.anchoredPosition = scanPos;
                heliScanRing.rectTransform.anchoredPosition = scanPos;

                float scanDiam = WorldRadiusToMap(FBHeliRecon.ScanRadius);
                heliScanZone.rectTransform.sizeDelta = new Vector2(scanDiam, scanDiam);
                heliScanRing.rectTransform.sizeDelta = new Vector2(scanDiam, scanDiam);

                float pulse = Mathf.PingPong(Time.time * 2.5f, 0.4f) + 0.6f;
                heliScanRing.color = new Color(1f, 0.55f, 0.1f, pulse * 0.9f);
                heliScanZone.color = new Color(1f, 0.4f, 0.1f, 0.22f * pulse);
            }

            // Inimigos ou combatentes revelados pelo helicóptero (visíveis por 60s com contagem)
            int tIdx = 0;
            foreach (var actor in FBMatch.Actors())
            {
                if (actor.isPlayer || !actor.alive || !FBHeliRecon.IsTagged(actor)) continue;
                float rem = FBHeliRecon.RemainingSeconds(actor);
                if (rem <= 0) continue;

                if (tIdx >= taggedDots.Count)
                {
                    var tGo = new GameObject("tagged_" + tIdx, typeof(RectTransform));
                    var tRt = (RectTransform)tGo.transform;
                    tRt.SetParent(mapContainer, false);
                    tRt.sizeDelta = new Vector2(20, 20);
                    var img = tGo.AddComponent<Image>();
                    img.sprite = DiscSpr;
                    img.color = new Color(1f, 0.15f, 0.15f, 0.95f);
                    img.raycastTarget = false;
                    var tol = tGo.AddComponent<Outline>();
                    tol.effectColor = Color.black;
                    tol.effectDistance = new Vector2(1.2f, -1.2f);
                    taggedDots.Add(img);

                    var lGo = new GameObject("lbl", typeof(RectTransform));
                    var lRt = (RectTransform)lGo.transform;
                    lRt.SetParent(tGo.transform, false);
                    lRt.anchoredPosition = new Vector2(0, -15);
                    lRt.sizeDelta = new Vector2(70, 16);
                    var txt = lGo.AddComponent<Text>();
                    txt.font = FBUI.Font;
                    txt.fontSize = 10;
                    txt.alignment = TextAnchor.MiddleCenter;
                    txt.color = new Color(1f, 0.45f, 0.45f);
                    var lol = lGo.AddComponent<Outline>();
                    lol.effectColor = Color.black;
                    lol.effectDistance = new Vector2(1, -1);
                    taggedLabels.Add(txt);
                }

                var dot = taggedDots[tIdx];
                var lbl = taggedLabels[tIdx];
                dot.gameObject.SetActive(true);
                dot.rectTransform.anchoredPosition = WorldToMap(actor.Pos);
                lbl.text = $"⚠️ {actor.Name} ({Mathf.CeilToInt(rem)}s)";
                tIdx++;
            }
            for (int i = tIdx; i < taggedDots.Count; i++) taggedDots[i].gameObject.SetActive(false);

        }

    }

}

