// Sistema de captura de screenshots em alta definição (4K/8K lossless PNG) para Buritiro.
// Atalhos em jogo:
//   - F12 ou PrintScreen: Captura 4K nativa (superSize 2x com anti-aliasing SSAA)
//   - Shift + F12: Captura Ultra-HD 8K (superSize 4x para artes de divulgação e banners)
//   - Ctrl + F12: Captura 4K ocultando o HUD automaticamente durante a foto
//   - F11: Liga/Desliga o HUD (Modo Foto para enquadramentos cinematográficos)
//   - F10: REELS 9:16 em qualidade máxima — 2160×3840 (renderiza 4320×7680 e reduz: SSAA 2×), sem HUD, detalhe/sombra/texturas no
//     máximo só durante a foto; Shift + F10 = 1080×1920. F9 liga as guias do enquadramento 9:16 na tela (o que fica fora sai da foto)
// Salva em: <raiz_do_projeto>/Screenshots/Buritiro_*.png (reels: Screenshots/Reels; no celular, persistentDataPath/Reels)
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace FreeBala
{
    public class FBScreenshot : MonoBehaviour
    {
        static FBScreenshot instance;
        bool hudVisible = true;

        public static string ScreenshotDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Screenshots"));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (instance) return;
            var go = new GameObject("[BURITIRO] ScreenshotSystem");
            instance = go.AddComponent<FBScreenshot>();
            DontDestroyOnLoad(go);
        }

        // [parceiro] tempo de CPU por script medido pelo FBTempo (desligado no jogo: custa um teste de bool)
        static readonly int __fbt_Update = FBTempo.Id("FBScreenshot.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }

        void Update__fbt()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // F11: Alternar HUD para fotos limpas
            if (kb.f11Key != null && kb.f11Key.wasPressedThisFrame)
            {
                ToggleHUD();
            }

            // F10: reels 9:16 (Shift = 1080×1920) · F9: guias do enquadramento
            if (kb.f10Key != null && kb.f10Key.wasPressedThisFrame) CaptureReel(kb.shiftKey.isPressed ? 1080 : 2160);
            if (kb.f9Key != null && kb.f9Key.wasPressedThisFrame) ToggleGuides();

            // F12 ou PrintScreen: Capturar screenshot
            bool f12 = kb.f12Key != null && kb.f12Key.wasPressedThisFrame;
            bool printScreen = kb.printScreenKey != null && kb.printScreenKey.wasPressedThisFrame;

            if (f12 || printScreen)
            {
                bool shift = kb.shiftKey.isPressed;
                bool ctrl = kb.ctrlKey.isPressed;
                int super = shift ? 4 : 2;   // Shift = 8K (4x), Padrão = 4K (2x)
                bool hideUI = ctrl;
                Capture(super, hideUI);
            }
        }

        public void ToggleHUD()
        {
            hudVisible = !hudVisible;
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                if (c) c.enabled = hudVisible;
            }
            Debug.Log($"[BURITIRO] Interface (HUD): {(hudVisible ? "Visível" : "Oculta (Modo Foto)")}");
        }

        public static void Capture(int superSize = 2, bool hideUI = false)
        {
            if (instance)
            {
                instance.StartCoroutine(instance.DoCapture(superSize, hideUI));
            }
            else
            {
                Directory.CreateDirectory(ScreenshotDir);
                string time = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string resName = superSize >= 4 ? "8K" : (superSize == 2 ? "4K" : "FullHD");
                string file = Path.Combine(ScreenshotDir, $"Buritiro_{resName}_{time}.png");
                ScreenCapture.CaptureScreenshot(file, superSize);
                Debug.Log($"[BURITIRO] 📸 Screenshot salva: {file}");
            }
        }

        // ---------------- REELS 9:16 (pedido 26/10: "capturas da melhor qualidade possível, porém para reels, 9:16")
        // A câmera do jogo renderiza direto numa textura vertical (mesmo campo de visão vertical da tela = o miolo do quadro, igual às
        // guias do F9) com 2× a resolução final e reduz pela metade (média de 2×2 pixels = SSAA). A interface é canvas de tela: não entra.
        public static string ReelDir => Application.isMobilePlatform ? Path.Combine(Application.persistentDataPath, "Reels") : Path.Combine(ScreenshotDir, "Reels");
        public static string LastReel { get; private set; }
        public static void CaptureReel(int width = 2160) { if (instance) instance.StartCoroutine(instance.DoReel(width)); }
        IEnumerator DoReel(int width)
        {
            var cam = Camera.main; if (!cam) yield break;
            int W = width, H = width * 16 / 9, ss = Mathf.Min(2, Mathf.Max(1, SystemInfo.maxTextureSize / H));   // 2× se a placa aguenta 7680 px
            // qualidade máxima só durante a foto
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            float lod = QualitySettings.lodBias, sd = urp ? urp.shadowDistance : 0; int maxLod = QualitySettings.maximumLODLevel, mip = QualitySettings.globalTextureMipmapLimit, sres = urp ? urp.mainLightShadowmapResolution : 0, casc = urp ? urp.shadowCascadeCount : 0;
            QualitySettings.lodBias = Mathf.Max(lod, 4); QualitySettings.maximumLODLevel = 0; QualitySettings.globalTextureMipmapLimit = 0;
            if (urp) { urp.shadowDistance = Mathf.Max(sd, 160); urp.mainLightShadowmapResolution = 4096; urp.shadowCascadeCount = 4; }
            yield return null; yield return null; yield return null;                                       // texturas/LODs carregarem
            RenderTexture big = null, small = null; Texture2D tex = null;
            try
            {
                big = new RenderTexture(W * ss, H * ss, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1, name = "reel_ssaa" };
                small = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "reel" };
                big.Create(); small.Create();
                // pedido de render padrão do Unity 6 (câmera + pilha de overlays + pós-processamento) direto na textura; senão Camera.Render
                var req = new RenderPipeline.StandardRequest { destination = big }; var prevT = cam.targetTexture; cam.aspect = W / (float)H;
                try { if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req); else { cam.targetTexture = big; cam.Render(); } }
                finally { cam.targetTexture = prevT; cam.ResetAspect(); }
                big.filterMode = FilterMode.Bilinear; Graphics.Blit(big, small);                         // 2× -> 1×: bilinear no meio de 2×2 = média
                var prevA = RenderTexture.active; RenderTexture.active = small;
                tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(false);
                RenderTexture.active = prevA;
                Directory.CreateDirectory(ReelDir);
                var file = Path.Combine(ReelDir, $"Buritiro_Reels_{W}x{H}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
                File.WriteAllBytes(file, tex.EncodeToPNG()); LastReel = file;
                Debug.Log($"[BURITIRO] 📸 Reels 9:16 {W}×{H} (SSAA {ss}×) salvo em: {file}");
                FBHUD.I?.Center($"FOTO REELS 9:16 SALVA · {W}×{H}", 2.2f);
            }
            finally
            {
                if (big) { big.Release(); Destroy(big); } if (small) { small.Release(); Destroy(small); } if (tex) Destroy(tex);
                QualitySettings.lodBias = lod; QualitySettings.maximumLODLevel = maxLod; QualitySettings.globalTextureMipmapLimit = mip;
                if (urp) { urp.shadowDistance = sd; urp.mainLightShadowmapResolution = sres; urp.shadowCascadeCount = casc; }
            }
        }
        // guias do enquadramento 9:16: escurece as laterais que ficam fora da foto e marca os terços
        GameObject guides;
        public void ToggleGuides()
        {
            if (guides) { guides.SetActive(!guides.activeSelf); return; }
            var cv = FBUI.Canvas("GuiasReels", 900); guides = cv.gameObject; DontDestroyOnLoad(guides);
            var frame = new GameObject("quadro_9x16", typeof(RectTransform), typeof(AspectRatioFitter)); frame.transform.SetParent(cv.transform, false);
            var fr = (RectTransform)frame.transform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;
            var arf = frame.GetComponent<AspectRatioFitter>(); arf.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth; arf.aspectRatio = 9f / 16;
            Color dim = new Color(0, 0, 0, .62f), line = new Color(1, 1, 1, .5f);
            foreach (var (aMin, aMax) in new[] { (new Vector2(-10, 0), new Vector2(0, 1)), (new Vector2(1, 0), new Vector2(11, 1)) })   // faixas de fora (presas ao quadro)
            { var p = FBUI.Panel(fr, "fora", aMin, aMax, Vector2.zero, Vector2.zero, dim); p.rectTransform.offsetMin = p.rectTransform.offsetMax = Vector2.zero; p.raycastTarget = false; }
            for (int i = 1; i <= 2; i++)
            {
                var v = FBUI.Panel(fr, "terco_v", new Vector2(i / 3f, 0), new Vector2(i / 3f, 1), Vector2.zero, new Vector2(1.5f, 0), line); v.raycastTarget = false;
                var h = FBUI.Panel(fr, "terco_h", new Vector2(0, i / 3f), new Vector2(1, i / 3f), Vector2.zero, new Vector2(0, 1.5f), line); h.raycastTarget = false;
            }
            FBUI.Label(fr, "REELS 9:16 · F10 FOTO 4K · SHIFT+F10 FULL HD · F9 GUIAS", 13, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 18), new Vector2(560, 22), TextAnchor.MiddleCenter, true, new Color(1, 1, 1, .85f)).raycastTarget = false;
        }

        IEnumerator DoCapture(int superSize, bool hideUI)
        {
            Canvas[] canvases = null;
            bool[] prevStates = null;

            if (hideUI)
            {
                canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                prevStates = new bool[canvases.Length];
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i])
                    {
                        prevStates[i] = canvases[i].enabled;
                        canvases[i].enabled = false;
                    }
                }
            }

            yield return new WaitForEndOfFrame();

            Directory.CreateDirectory(ScreenshotDir);
            string time = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string resName = superSize >= 4 ? "8K" : (superSize == 2 ? "4K" : "FullHD");
            string file = Path.Combine(ScreenshotDir, $"Buritiro_{resName}_{time}.png");

            ScreenCapture.CaptureScreenshot(file, superSize);

            if (hideUI && canvases != null)
            {
                yield return null;
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i]) canvases[i].enabled = prevStates[i];
                }
            }

            Debug.Log($"[BURITIRO] 📸 Screenshot ({resName}) salva em: {file}");
        }
    }
}
