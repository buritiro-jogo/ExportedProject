// TELA DE CARREGAMENTO da partida (substitui o "CARREGANDO…" por cima do lobby): arte do mapa (MapShots) em tela cheia com movimento
// lento (Ken Burns), escurecida embaixo, logo FREE BALA, modo e nome do mapa no canto, dicas rotativas e barra de progresso REAL:
//   cena (AsyncOperation.progress, 0–55 %) → partida montada (FBMatch saiu de "loading") → efeitos (Warm dos FX) → shaders/vegetação
//   (a cena é renderizada em 6 direções numa câmera fora da tela: compila tudo antes de o jogador ver) → áudio (clipes em carregamento)
//   → quadros estáveis. Só então a tela some com fade; durante o aquecimento o tempo do jogo fica parado (ninguém age com a tela coberta).
// Responsiva (16:9, 21:9, retrato). Prints: FreeBala.exe -fbload (prévia sem partida, troca a resolução e fecha).
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FreeBala
{
    public class FBLoading : MonoBehaviour
    {
        public static FBLoading I { get; private set; }
        public static bool Busy => I;
        public float Progress => shown; public string Stage => stage; public float Cover => group ? group.alpha : 0;   // teste
        static readonly string[] TIPS = {
            "Segure SHIFT para correr; na corrida automática, basta empurrar o analógico até a borda.",
            "C agacha e Z deita: menos silhueta e mira mais firme.",
            "Q e E inclinam o corpo para espiar quinas sem se expor inteiro.",
            "Na frente de uma janela ou murinho, ESPAÇO pula por cima.",
            "H usa o kit médico. Procure cobertura antes de se curar.",
            "Caído? Um aliado segura F (ou ↑ no controle) para te levantar.",
            "Y marca um ponto no mapa para o seu time.",
            "T inspeciona a arma; dirigindo, V muda a distância da câmera.",
            "No 4V4 ZONA, quem morre perde o equipamento: compre com calma.",
            "No FARINHA × BURITI, leve o saco ou o cacho até a SUA base (verde).",
            "No DE ROLÊ, K abre a central multimídia e o paredão toca para todo mundo perto.",
            "Segure B para abrir a roda de emotes. Monte a sua em EMOTES no lobby.",
            "Headshot rende mais XP no passe do que o abate comum.",
            "Personalize pele, cabelo e roupas na tela PERSONAGEM.",
            "No Battle Royale, comece sem arma: ache uma antes de entrar em briga.",
        };
        static readonly Color Txt = new(.925f, .91f, .863f), Dim = new(.925f, .91f, .863f, .6f), Gold2 = new(1, .77f, 0);

        string scene, mapId, modeId; bool preview, simulate; float shown, target; string stage = "";
        Canvas canvas; CanvasGroup group; RawImage bg; Image fill; Text pctT, stageT, tipT; RectTransform tipBox; float t0, tipT0; int tipI;
        Vector2Int screen; Rect kb0, kb1;

        // ---------------------------------------------------------------- entrada
        /// <summary>Cobre a tela e carrega a cena da partida; some só com tudo pronto.</summary>
        public static void Load(string scene, string mapId, string modeId)
        {
            if (I) return; var g = new GameObject("FB_Carregamento"); DontDestroyOnLoad(g);
            I = g.AddComponent<FBLoading>(); I.scene = scene; I.mapId = mapId; I.modeId = modeId;
        }
        /// <summary>Só a tela (prints do -fbload): progresso simulado, sem trocar de cena.</summary>
        public static FBLoading Preview(string mapId, string modeId)
        {
            if (I) Destroy(I.gameObject); var g = new GameObject("FB_Carregamento"); DontDestroyOnLoad(g);
            I = g.AddComponent<FBLoading>(); I.mapId = mapId; I.modeId = modeId; I.preview = true; I.simulate = true; return I;
        }
        void OnDestroy() { Boost(false); if (I == this) I = null; }

        void Start()
        {
            tipI = Random.Range(0, TIPS.Length); Build();
            if (!preview) StartCoroutine(Run());
        }

        // ---------------------------------------------------------------- montagem
        static Texture2D Grad(bool vertical)
        {
            var t = new Texture2D(vertical ? 1 : 64, vertical ? 64 : 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 64; i++) { float a = Mathf.SmoothStep(0, 1, i / 63f); t.SetPixel(vertical ? 0 : i, vertical ? i : 0, new Color(0, 0, 0, a)); }
            t.Apply(); return t;
        }
        static Texture2D gradV, gradH;
        void Build()
        {
            if (canvas) Destroy(canvas.gameObject);
            screen = new Vector2Int(Screen.width, Screen.height); bool portrait = Screen.height > Screen.width;
            canvas = FBUI.Canvas("Carregamento", 1000); canvas.transform.SetParent(transform, false);
            var sc = canvas.GetComponent<CanvasScaler>(); sc.referenceResolution = portrait ? new Vector2(900, 1600) : new Vector2(1600, 900); sc.matchWidthOrHeight = .5f;
            group = canvas.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = true; if (shown <= 0 && t0 <= 0) group.alpha = 0;
            var root = (RectTransform)canvas.transform;
            // fundo opaco + arte do mapa (cobre a tela; a cena montando nunca aparece)
            FBUI.Panel(root, "fundo", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(.02f, .025f, .03f, 1));
            bg = FBUI.Rect(root, "arte", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>(); bg.raycastTarget = false;
            bg.texture = FBMapShot.Big(mapId) ?? Resources.Load<Texture2D>("UI/Lobby/lobby_bg"); bg.color = new Color(.82f, .82f, .82f);
            KenBurnsSetup();
            gradV ??= Grad(true); gradH ??= Grad(false);
            var gb = FBUI.Rect(root, "escuro_baixo", Vector2.zero, new Vector2(1, portrait ? .62f : .55f), Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>(); gb.texture = gradV; gb.uvRect = new Rect(0, 1, 1, -1); gb.raycastTarget = false;
            var gt = FBUI.Rect(root, "escuro_topo", new Vector2(0, .8f), Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>(); gt.texture = gradV; gt.color = new Color(1, 1, 1, .7f); gt.raycastTarget = false;
            if (!portrait) { var gl = FBUI.Rect(root, "escuro_esq", Vector2.zero, new Vector2(.5f, 1), Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>(); gl.texture = gradH; gl.uvRect = new Rect(1, 0, -1, 1); gl.color = new Color(1, 1, 1, .55f); gl.raycastTarget = false; }
            // área segura (entalhe do celular)
            var sa = Screen.safeArea; float W = Mathf.Max(1, Screen.width), H = Mathf.Max(1, Screen.height);
            var safe = FBUI.Rect(root, "seguro", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            if (sa.width > 10) { safe.anchorMin = new Vector2(sa.x / W, sa.y / H); safe.anchorMax = new Vector2(sa.xMax / W, sa.yMax / H); safe.offsetMin = safe.offsetMax = Vector2.zero; }
            // logo
            var logo = Resources.Load<Texture2D>("UI/Lobby/logo");
            if (logo) { var li = FBUI.Rect(safe, "logo", new Vector2(0, 1), new Vector2(0, 1), new Vector2(40 + 170, -30 - 30), new Vector2(340, 59)).gameObject.AddComponent<RawImage>(); li.texture = logo; li.raycastTarget = false; }
            // modo + mapa (canto inferior esquerdo; no retrato, embaixo centralizado)
            var M = FBMenu.MODES.FirstOrDefault(m => m.id == modeId); string modeName = M.name ?? (modeId ?? "").ToUpper(), modeTag = M.tag ?? "";
            string mapName = GameData.Maps.TryGetValue(mapId ?? "", out var md) ? md.name : mapId;
            var wx = FBSession.WeatherFor(mapId ?? ""); string[] diffs = { "FÁCIL", "NORMAL", "REALISTA" };
            string sub = $"{modeName}{(modeTag != "" ? " · " + modeTag : "")} · {diffs[Mathf.Clamp(FBSession.diff, 0, 2)]}{(wx.time == "noite" ? " · NOITE" : wx.time == "anoitecer" ? " · ANOITECER" : "")}{(wx.weather == "chuva" ? " · CHUVA" : "")}";
            var al = portrait ? TextAnchor.LowerCenter : TextAnchor.LowerLeft; Vector2 aMin = portrait ? new Vector2(0, 0) : new Vector2(0, 0), aMax = portrait ? new Vector2(1, 0) : new Vector2(.62f, 0);
            float baseY = portrait ? 300 : 150;
            var tag = FBUI.Label(safe, "MODO · " + modeName, 16, aMin, aMax, new Vector2(portrait ? 0 : 0, baseY + 92), new Vector2(portrait ? -80 : -100, 22), al, true, Gold2); Off(tag.rectTransform, portrait);
            var mt = FBUI.Label(safe, (mapName ?? "").ToUpper(), portrait ? 52 : 64, aMin, aMax, new Vector2(0, baseY + 40), new Vector2(portrait ? -80 : -100, 80), al, true, Txt);
            mt.resizeTextForBestFit = true; mt.resizeTextMinSize = 28; mt.resizeTextMaxSize = portrait ? 52 : 64; mt.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0, -3); Off(mt.rectTransform, portrait);
            var st = FBUI.Label(safe, sub, 15, aMin, aMax, new Vector2(0, baseY - 6), new Vector2(portrait ? -80 : -100, 22), al, false, Dim); Off(st.rectTransform, portrait);
            // dica
            tipBox = FBUI.Rect(safe, "dica", portrait ? new Vector2(.5f, 0) : new Vector2(1, 0), portrait ? new Vector2(.5f, 0) : new Vector2(1, 0), portrait ? new Vector2(0, 165) : new Vector2(-40 - 260, 150), new Vector2(520, 84));
            var tbg = tipBox.gameObject.AddComponent<Image>(); tbg.color = new Color(.03f, .035f, .04f, .55f); tbg.raycastTarget = false;
            var bar = FBUI.Panel(tipBox, "borda", new Vector2(0, 0), new Vector2(0, 1), new Vector2(2, 0), new Vector2(4, 0), Gold2); bar.raycastTarget = false;
            FBUI.Label(tipBox, "DICA", 12, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -16), new Vector2(-36, 18), TextAnchor.MiddleLeft, true, Gold2);
            tipT = FBUI.Label(tipBox, TIPS[tipI], 15, Vector2.zero, Vector2.one, new Vector2(0, -8), new Vector2(-36, -32), TextAnchor.UpperLeft, false, Txt);
            // barra de progresso real
            var track = FBUI.Panel(safe, "barra", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, portrait ? 70 : 64), new Vector2(-80, 6), new Color(1, 1, 1, .12f)); track.raycastTarget = false;
            fill = FBUI.Panel(track.transform, "progresso", Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero, Gold2); fill.raycastTarget = false; fill.rectTransform.pivot = new Vector2(0, .5f);
            var glow = fill.gameObject.AddComponent<Shadow>(); glow.effectColor = new Color(1, .6f, .1f, .5f); glow.effectDistance = new Vector2(0, -2);
            stageT = FBUI.Label(safe, stage, 14, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, portrait ? 92 : 86), new Vector2(-80, 20), TextAnchor.MiddleLeft, true, Dim);
            pctT = FBUI.Label(safe, "0%", 18, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, portrait ? 92 : 88), new Vector2(-80, 24), TextAnchor.MiddleRight, true, Txt);
            FBUI.Label(safe, "BURITIRO · BETA", 11, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, portrait ? 40 : 34), new Vector2(-80, 16), TextAnchor.MiddleLeft, false, new Color(1, 1, 1, .45f));
            Paint();
        }
        static void Off(RectTransform r, bool portrait) { if (!portrait) r.anchoredPosition = new Vector2(r.anchoredPosition.x + 50, r.anchoredPosition.y); }
        // Ken Burns: começa num enquadramento e desliza devagar até outro (zoom 4–9 %, direção sorteada), sem bordas
        void KenBurnsSetup()
        {
            if (!bg || !bg.texture) return; float ar = (float)bg.texture.width / bg.texture.height, sr = (float)Screen.width / Mathf.Max(1, Screen.height);
            Rect Fit(float zoom, Vector2 c)
            {
                float w = 1, h = 1; if (ar > sr) w = sr / ar; else h = ar / sr; w /= zoom; h /= zoom;
                float x = Mathf.Clamp(c.x - w / 2, 0, 1 - w), y = Mathf.Clamp(c.y - h / 2, 0, 1 - h); return new Rect(x, y, w, h);
            }
            var a = new Vector2(.5f + Random.Range(-.06f, .06f), .5f + Random.Range(-.04f, .04f));
            kb0 = Fit(1.04f, a); kb1 = Fit(1.09f, a + new Vector2(Random.Range(-.05f, .05f), Random.Range(-.03f, .03f))); bg.uvRect = kb0;
        }

        // ---------------------------------------------------------------- quadro
        // [parceiro 01/11] medidor de tempo por sistema (FBTempo): com FBTempo.On soma o custo do Update da tela de carregamento
        // (o local se chama ini porque t0 já é o relógio da animação desta tela)
        static readonly int __fbt_Update = FBTempo.Id("FBLoading.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long ini = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, ini);
        }
        void Update__fbt()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f); t0 += dt;
            if (Screen.width != screen.x || Screen.height != screen.y) Build();
            if (bg && bg.texture) { float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t0 / 16f)); bg.uvRect = new Rect(Vector2.Lerp(kb0.position, kb1.position, k), Vector2.Lerp(kb0.size, kb1.size, k)); }
            if (simulate) target = Mathf.Repeat(t0 / 9f, 1.02f);
            shown = Mathf.Max(shown, Mathf.MoveTowards(shown, target, dt * .9f));     // nunca volta; anda rápido mas sem saltos
            Paint();
            if (t0 - tipT0 > 5.5f && tipT) { tipT0 = t0; tipI = (tipI + 1) % TIPS.Length; tipT.text = TIPS[tipI]; FBCustomizeFx.Fade(tipT, 0, 1, .4f); FBCustomizeFx.SlideIn(tipT.rectTransform, new Vector2(0, -8), .4f); }
            if (group && !fading && group.alpha < 1) group.alpha = Mathf.MoveTowards(group.alpha, 1, dt * 4);
        }
        bool fading;
        void Paint()
        {
            if (fill) fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(shown), 1);
            if (pctT) pctT.text = Mathf.RoundToInt(Mathf.Clamp01(shown) * 100) + "%";
            if (stageT) stageT.text = stage + new string('.', 1 + (int)(t0 * 2.5f) % 3);
        }
        public void SetPreviewStage(string s, float p) { stage = s; target = p; shown = p; preview = true; simulate = false; t0 = Mathf.Max(t0, 6); Paint(); }

        // ---------------------------------------------------------------- carregamento real
        // r43 — tempos de cada fase (usuário: "o carregamento das partidas demora muito"): [FreeBala][carregamento] no fim; LastTimes p/ o teste
        public static string LastTimes; public static float LastTotal;
        readonly System.Diagnostics.Stopwatch clk = new(); readonly System.Text.StringBuilder laps = new(); double lapT;
        void Lap(string n) { double t = clk.Elapsed.TotalSeconds; laps.Append($"{n} {t - lapT:0.00}s · "); lapT = t; }
        // r43: a cena era integrada com a prioridade padrão de carregamento em segundo plano (BelowNormal = ~4 ms por quadro no thread
        // principal) e o envio de texturas/malhas para a GPU com a fatia do jogo (8 ms/quadro no PC, 4 no celular). Com a tela coberta não há
        // jogo para proteger: prioridade High (~50 ms/quadro) e fatia de upload maior até a tela sumir. -fbcarregalento = como era (comparação).
        public static bool Old => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbcarregalento") >= 0;
        ThreadPriority prio0; int up0, buf0; bool boosted;
        void Boost(bool on)
        {
            if (on == boosted || Old) return; boosted = on;
            if (on)
            {
                prio0 = Application.backgroundLoadingPriority; up0 = QualitySettings.asyncUploadTimeSlice; buf0 = QualitySettings.asyncUploadBufferSize;
                Application.backgroundLoadingPriority = ThreadPriority.High;
                QualitySettings.asyncUploadTimeSlice = Application.isMobilePlatform ? 24 : 33; QualitySettings.asyncUploadBufferSize = Mathf.Max(buf0, Application.isMobilePlatform ? 64 : 128);
            }
            else { Application.backgroundLoadingPriority = prio0; QualitySettings.asyncUploadTimeSlice = up0; QualitySettings.asyncUploadBufferSize = buf0; }
        }
        IEnumerator Run()
        {
            clk.Start(); stage = "Preparando"; target = .02f; Boost(true);
            while (group.alpha < .99f) yield return null;                               // cobre o lobby antes de qualquer trabalho
            foreach (var r in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) if (r != canvas && r.renderMode == RenderMode.ScreenSpaceOverlay) r.enabled = false;
            stage = "Carregando o mapa";
            var op = SceneManager.LoadSceneAsync(scene); if (op == null) { Debug.LogError("[FreeBala] cena não existe: " + scene); Destroy(gameObject); yield break; }
            op.allowSceneActivation = false; Lap("cobrir a tela");
            while (op.progress < .9f) { target = .05f + op.progress / .9f * .5f; yield return null; }
            Lap("ler a cena"); target = .56f; op.allowSceneActivation = true; while (!op.isDone) yield return null; Lap("ativar a cena");
            // partida montada (o FBMatch.Start faz tudo de uma vez)
            stage = "Montando a partida"; float w = 0;
            while ((!FBMatch.I || FBMatch.I.state == "loading" || !FBMatch.Player) && w < 20) { w += Time.unscaledDeltaTime; target = Mathf.Min(.68f, .56f + w * .02f); yield return null; }
            Lap("montar a partida"); float ts = Time.timeScale; Time.timeScale = 0;     // ninguém age com a tela coberta
            HideOtherUI(true);
            // efeitos
            stage = "Preparando efeitos"; target = .72f; yield return null;
            try { FBBloodFx.Warm(); FBDecalFx.Warm(); FBImpactFx.Warm(); FBShotFx.Warm(); FBExplosionFx.Warm(); FBWoundFx.Warm(); } catch (System.Exception e) { Debug.LogWarning("[FreeBala] aquecimento de FX: " + e.Message); }
            yield return null; Lap("efeitos");
            // shaders e vegetação: a cena renderizada em 6 direções numa câmera fora da tela (compila as variantes antes de o jogador ver)
            stage = "Aquecendo gráficos"; yield return WarmViews(.74f, .92f); Lap("aquecer gráficos");
            // áudio: espera os clipes que estão carregando em segundo plano
            stage = "Carregando áudio"; target = .94f; w = 0;
            while (w < 4 && FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(a => a && a.clip && a.clip.loadState == AudioDataLoadState.Loading)) { w += Time.unscaledDeltaTime; yield return null; }
            // quadros estáveis (sem picos de compilação) por ~10 quadros seguidos, no máximo 4 s
            Lap("áudio"); stage = "Quase lá"; int ok = 0; w = 0;
            while (ok < 10 && w < 4) { yield return null; w += Time.unscaledDeltaTime; ok = Time.unscaledDeltaTime < .05f ? ok + 1 : 0; target = Mathf.Min(.99f, .95f + w * .01f + ok * .004f); }
            Lap("quadros estáveis"); stage = "Pronto"; target = 1; while (shown < .995f) yield return null;
            yield return new WaitForSecondsRealtime(.25f); Lap("barra até 100%");
            LastTotal = (float)clk.Elapsed.TotalSeconds; LastTimes = laps.ToString();
            Debug.Log($"[FreeBala][carregamento] {mapId}:{modeId} · {(Old ? "MODO ANTIGO" : "prioridade alta")} · total {LastTotal:0.0} s · {LastTimes}{FBMatch.BuildTimes}");
            // entra na partida
            Boost(false); Time.timeScale = ts <= 0 ? 1 : ts; HideOtherUI(false); fading = true;
            for (float f = 0; f < .55f; f += Time.unscaledDeltaTime) { group.alpha = 1 - Mathf.SmoothStep(0, 1, f / .55f); yield return null; }
            Destroy(gameObject);
        }
        readonly List<Canvas> hidden = new();
        void HideOtherUI(bool hide)
        {
            if (hide) { foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) if (c != canvas && c.enabled && c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.sortingOrder >= 1000) { c.enabled = false; hidden.Add(c); } }
            else { foreach (var c in hidden) if (c) c.enabled = true; hidden.Clear(); }
        }
        IEnumerator WarmViews(float p0, float p1)
        {
            var main = Camera.main; if (!main) { target = p1; yield break; }
            var go = new GameObject("cam_aquecimento"); var cam = go.AddComponent<Camera>(); cam.CopyFrom(main); cam.enabled = false;
            var rt = new RenderTexture(Application.isMobilePlatform ? 256 : 384, Application.isMobilePlatform ? 144 : 216, 24) { name = "rt_aquecimento" }; cam.targetTexture = rt;
            var pos = FBMatch.Player ? FBMatch.Player.transform.position + Vector3.up * 1.6f : main.transform.position;
            var dirs = new[] { Quaternion.Euler(8, 0, 0), Quaternion.Euler(8, 90, 0), Quaternion.Euler(8, 180, 0), Quaternion.Euler(8, 270, 0), Quaternion.Euler(45, 45, 0), Quaternion.Euler(-20, 225, 0) };
            for (int i = 0; i < dirs.Length; i++)
            {
                cam.transform.SetPositionAndRotation(pos, dirs[i]);
                try { var req = new RenderPipeline.StandardRequest { destination = rt }; if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req); else cam.Render(); }
                catch (System.Exception e) { Debug.LogWarning("[FreeBala] aquecimento de shaders: " + e.Message); }
                target = Mathf.Lerp(p0, p1, (i + 1f) / dirs.Length); yield return null;
            }
            // variantes da transição pontilhada de LOD (LOD_FADE_CROSSFADE): só são usadas DURANTE um cruzamento — sem isto a primeira
            // transição de cada material compilava no meio da partida (engasgo)
            Shader.EnableKeyword("LOD_FADE_CROSSFADE");
            for (int i = 0; i < dirs.Length; i += 2)
            {
                cam.transform.SetPositionAndRotation(pos, dirs[i]);
                try { var req = new RenderPipeline.StandardRequest { destination = rt }; if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req); else cam.Render(); }
                catch (System.Exception e) { Debug.LogWarning("[FreeBala] aquecimento de shaders (LOD): " + e.Message); }
                yield return null;
            }
            Shader.DisableKeyword("LOD_FADE_CROSSFADE");
            cam.targetTexture = null; Destroy(go); rt.Release(); Destroy(rt);
        }

        // ---------------------------------------------------------------- prints (-fbload)
        /// <summary>Prévia da tela em 3 etapas e 3 proporções; grava &lt;build&gt;/autoteste/carregamento_*.png.</summary>
        public static IEnumerator Shots(string dir, string mapId, string modeId)
        {
            System.IO.Directory.CreateDirectory(dir); var L = Preview(mapId, modeId); yield return null;
            IEnumerator Shot(string n) { for (int i = 0; i < 8; i++) yield return null; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "carregamento_" + n + ".png")); yield return null; yield return null; Debug.Log("[FreeBala][carregamento] print " + n); }
            L.SetPreviewStage("Carregando o mapa", .32f); yield return Shot("16x9_mapa");
            L.SetPreviewStage("Aquecendo gráficos", .81f); yield return Shot("16x9_graficos");
            if (!Application.isMobilePlatform)
            {
                int w0 = Screen.width, h0 = Screen.height; var fs = Screen.fullScreenMode;
                Screen.SetResolution(2560, 1080, FullScreenMode.Windowed); for (int i = 0; i < 20; i++) yield return null; L.SetPreviewStage("Montando a partida", .63f); yield return Shot("21x9");
                Screen.SetResolution(720, 1280, FullScreenMode.Windowed); for (int i = 0; i < 20; i++) yield return null; L.SetPreviewStage("Quase lá", .97f); yield return Shot("retrato");
                Screen.SetResolution(w0, h0, fs); for (int i = 0; i < 10; i++) yield return null;
            }
            if (L) Destroy(L.gameObject);
        }
    }
}
