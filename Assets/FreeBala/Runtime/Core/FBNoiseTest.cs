// Diagnóstico de ruído/granulação (Etapa 4, Agente 9): FreeBala.exe -fbnoise [mapa:modo] [-fbq 0..3]
// Abre o mapa, congela o tempo (timeScale 0: bots, vento e céu param — dois quadros seguidos só diferem pelo que o próprio render
// sorteia) e fotografa a MESMA cena variando UM fator por vez (SSAO, MSAA, AA da câmera, pós, bloom, vinheta, tonemapping, sombras,
// névoa de altura, escala de render, mip streaming, HDR, cross-fade de LOD) em duas vistas: a do jogo e uma parede a ~1 m da câmera.
// Saída em <build>/autoteste: noise_<vista>_<fator>.png (tela inteira) + noise_<vista>_<fator>_x4.png (centro ampliado 4×, sem filtro)
// e noise.csv com ruído espacial (média do |laplaciano| da luminância no recorte) e temporal (diferença média entre dois quadros
// parados). No fim, resoluções/proporções (1280×720, 1920×1080, 2560×1440, 21:9 2560×1080, retrato 1080×1920) com a escala de render real.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBNoiseTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, "-fbnoise"); if (i < 0) return;
            var g = new GameObject("FB_Ruido"); DontDestroyOnLoad(g); var t = g.AddComponent<FBNoiseTest>();
            if (i + 1 < a.Length && !a[i + 1].StartsWith("-")) { var kv = a[i + 1].Split(':'); t.map = kv[0]; if (kv.Length > 1) t.mode = kv[1]; }
            int qi = Array.IndexOf(a, "-fbq"); if (qi >= 0 && qi + 1 < a.Length && int.TryParse(a[qi + 1], out var q)) t.level = q;
        }
        string map = "vila_jardim", mode = "tatico", dir; int level = -1;
        readonly System.Text.StringBuilder csv = new();

        IEnumerator Start()
        {
            dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"); System.IO.Directory.CreateDirectory(dir);
            csv.AppendLine("vista;fator;ruido_espacial;ruido_temporal;tela;render_px;render_scale;msaa;hdr;ssao;cam_aa;nivel;asset_urp");
            yield return new WaitForSeconds(3);
            if (level >= 0) FBQuality.Set(level);
            GameData.Load(); FBSession.Load(); FBSession.map = map; FBSession.mode = mode; FBSession.online = false; FBSession.view = "tps"; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_" + map); if (op == null) { Debug.Log("[FreeBala][ruido] cena não existe: " + map); Application.Quit(1); yield break; }
            while (!op.isDone) yield return null;
            FBMatch.God = true; for (float w = 0; w < 6; w += Time.unscaledDeltaTime) yield return null;   // aquecimento (shaders, mip streaming)
            Time.timeScale = 0;

            var cam = Camera.main; var pose = gameObject.AddComponent<FBCamPose>();
            // vista "perto": parede/objeto à frente da câmera do jogo, a 1 m, olhando de frente
            Pose? near = null;
            if (cam && Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 60, FBWorld.SolidMask))
                near = new Pose(hit.point + hit.normal * 1.0f, Quaternion.LookRotation(-hit.normal, Vector3.up));
            var views = new List<(string name, Pose? p)> { ("jogo", null) }; if (near.HasValue) views.Add(("perto", near));

            foreach (var (vn, vp) in views)
            {
                pose.pose = vp;
                foreach (var f in Factors()) { f.on(); yield return Shot(vn, f.name); f.off(); }
            }
            pose.pose = null;
            yield return Resolutions();
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "noise.csv"), csv.ToString());
            Time.timeScale = 1; FBMatch.God = false;
            Debug.Log("[FreeBala][ruido] fim → " + dir); Application.Quit(0);
        }

        // ---------- fatores (cada um liga a variação e desfaz depois) ----------
        static UniversalRenderPipelineAsset URP => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        static (string name, Action on, Action off) Gfx(string name, Func<FBGfxFlags, FBGfxFlags> mod)
        {
            Func<FBGfxTier, FBGfxFlags> keep = null;
            return (name, () => { keep = FBGfx.FlagsOverride; var k = keep; FBGfx.FlagsOverride = t => mod(k != null ? k(t) : FBGfx.Defaults(t)); FBGfx.Refresh(); },
                          () => { FBGfx.FlagsOverride = keep; FBGfx.Refresh(); });
        }
        static (string name, Action on, Action off) Prop<T>(string name, Func<T> get, Action<T> set, T value)
        {
            T old = default; return (name, () => { old = get(); set(value); }, () => set(old));
        }
        static (string name, Action on, Action off) Post<T>(string name) where T : VolumeComponent
        {
            var comps = new List<VolumeComponent>();
            return (name, () => { comps.Clear(); foreach (var vp in Profiles()) if (vp.TryGet<T>(out var c) && c.active) { c.active = false; comps.Add(c); } },
                          () => { foreach (var c in comps) c.active = true; });
        }
        static IEnumerable<VolumeProfile> Profiles()
        {
            var u = URP; if (u && u.volumeProfile) yield return u.volumeProfile;
            foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None)) if (v && v.isGlobal && v.sharedProfile) yield return v.sharedProfile;
        }
        IEnumerable<(string name, Action on, Action off)> Factors()
        {
            var u = URP; var sun = RenderSettings.sun;
            yield return ("base", () => { }, () => { });
            yield return Gfx("sem_ssao", f => { f.ssao = false; return f; });
            if (u) yield return Prop("sem_msaa", () => u.msaaSampleCount, v => u.msaaSampleCount = v, 1);
            yield return Gfx("aa_nenhum", f => { f.camAA = AntialiasingMode.None; return f; });
            yield return Gfx("aa_fxaa", f => { f.camAA = AntialiasingMode.FastApproximateAntialiasing; return f; });
            yield return Gfx("aa_smaa", f => { f.camAA = AntialiasingMode.SubpixelMorphologicalAntiAliasing; return f; });
            var allPost = new[] { Post<Bloom>("x"), Post<Vignette>("x"), Post<Tonemapping>("x"), Post<ColorAdjustments>("x"), Post<WhiteBalance>("x"), Post<FilmGrain>("x"), Post<ChromaticAberration>("x") };
            yield return ("sem_pos", () => { foreach (var p in allPost) p.on(); }, () => { foreach (var p in allPost) p.off(); });
            yield return Post<Bloom>("sem_bloom"); yield return Post<Vignette>("sem_vinheta"); yield return Post<Tonemapping>("sem_tonemap");
            yield return Post<FilmGrain>("sem_grao"); yield return Post<ChromaticAberration>("sem_aberracao");
            if (u) yield return Prop("sem_sombra", () => u.shadowDistance, v => u.shadowDistance = v, 0f);
            if (sun) yield return Prop("sombra_dura", () => sun.shadows, v => sun.shadows = v, LightShadows.Hard);
            yield return Gfx("sem_nevoa_altura", f => { f.heightFog = false; return f; });
            yield return Gfx("sem_nuvens_vol", f => { f.volClouds = false; return f; });
            if (u) { yield return Prop("escala_100", () => u.renderScale, v => u.renderScale = v, 1f); yield return Prop("escala_085", () => u.renderScale, v => u.renderScale = v, .85f); yield return Prop("escala_070", () => u.renderScale, v => u.renderScale = v, .7f); }
            yield return Prop("sem_mip_streaming", () => QualitySettings.streamingMipmapsActive, v => QualitySettings.streamingMipmapsActive = v, false);
            yield return Prop("mip_meia", () => QualitySettings.globalTextureMipmapLimit, v => QualitySettings.globalTextureMipmapLimit = v, 1);
            yield return Prop("aniso_desligado", () => QualitySettings.anisotropicFiltering, v => QualitySettings.anisotropicFiltering = v, AnisotropicFiltering.Disable);
            if (u) yield return Prop("sem_hdr", () => u.supportsHDR, v => u.supportsHDR = v, false);
            yield return Prop("sem_crossfade", () => QualitySettings.enableLODCrossFade, v => QualitySettings.enableLODCrossFade = v, false);
        }

        // ---------- captura + métricas ----------
        IEnumerator Shot(string view, string factor, string extra = null)
        {
            for (int k = 0; k < 6; k++) yield return null;                                           // render estabiliza (MSAA/RT recriados)
            yield return new WaitForEndOfFrame(); var a = ScreenCapture.CaptureScreenshotAsTexture();
            yield return null; yield return new WaitForEndOfFrame(); var b = ScreenCapture.CaptureScreenshotAsTexture();
            int w = a.width, h = a.height, cw = Mathf.Min(320, w), ch = Mathf.Min(180, h), x0 = (w - cw) / 2, y0 = (h - ch) / 2;
            var pa = a.GetPixels32(); var pb = b.GetPixels32();
            double lap = 0, tmp = 0; int n = 0;
            float L(Color32[] p, int x, int y) { var c = p[y * w + x]; return .2126f * c.r + .7152f * c.g + .0722f * c.b; }
            for (int y = y0 + 1; y < y0 + ch - 1; y++) for (int x = x0 + 1; x < x0 + cw - 1; x++)
            { lap += Mathf.Abs(4 * L(pa, x, y) - L(pa, x - 1, y) - L(pa, x + 1, y) - L(pa, x, y - 1) - L(pa, x, y + 1)); n++; }
            for (int y = 0; y < h; y += 2) for (int x = 0; x < w; x += 2) tmp += Mathf.Abs(L(pa, x, y) - L(pb, x, y));
            tmp /= (w / 2) * (h / 2); lap /= Math.Max(1, n);
            var name = $"noise_{view}_{factor}"; System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".png"), a.EncodeToPNG());
            var crop = new Texture2D(cw * 4, ch * 4, TextureFormat.RGB24, false); var cp = new Color32[cw * 4 * ch * 4];
            for (int y = 0; y < ch * 4; y++) for (int x = 0; x < cw * 4; x++) cp[y * cw * 4 + x] = pa[(y0 + y / 4) * w + x0 + x / 4];
            crop.SetPixels32(cp); crop.Apply(); System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + "_x4.png"), crop.EncodeToPNG());
            Destroy(a); Destroy(b); Destroy(crop);
            var cam = Camera.main; var u = URP; var cd = cam ? cam.GetUniversalAdditionalCameraData() : null;
            float rs = u ? u.renderScale : 1; string rpx = cam ? $"{Mathf.RoundToInt(cam.pixelWidth * rs)}x{Mathf.RoundToInt(cam.pixelHeight * rs)}" : "-";
            var line = string.Join(";", view, factor, lap.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), tmp.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture),
                $"{Screen.width}x{Screen.height}", rpx, rs.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), u ? u.msaaSampleCount : 0, u && u.supportsHDR ? 1 : 0, FBGfx.F.ssao ? 1 : 0,
                cd ? cd.antialiasing.ToString() : "-", FBQuality.Names[(int)FBQuality.Level], u ? u.name : "-") + (extra != null ? ";" + extra : "");
            csv.AppendLine(line); Debug.Log("[FreeBala][ruido] " + line);
        }

        IEnumerator Resolutions()
        {
            int w0 = Screen.width, h0 = Screen.height; var fs = Screen.fullScreenMode;
            foreach (var (w, h, tag) in new[] { (1280, 720, "720p"), (1920, 1080, "1080p"), (2560, 1440, "1440p"), (2560, 1080, "21x9"), (1080, 1920, "retrato") })
            {
                Screen.SetResolution(w, h, FullScreenMode.Windowed); for (int k = 0; k < 30; k++) yield return null;
                var cs = FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).Select(c => c.scaleFactor).DefaultIfEmpty(1).Max();
                yield return Shot("res_" + tag, $"{w}x{h}", $"pedido {w}x{h} · obtido {Screen.width}x{Screen.height} · escala UI {cs:0.00}");
            }
            Screen.SetResolution(w0, h0, fs); for (int k = 0; k < 10; k++) yield return null;
        }
    }

    // segura a câmera principal numa pose fixa (vista "perto") depois de todo o resto do quadro
    [DefaultExecutionOrder(32000)]
    public class FBCamPose : MonoBehaviour
    {
        public Pose? pose;
        // [parceiro] v1 (thread de física): tempo de CPU medido pelo FBTempo (desligado = só um teste de bool)
        static readonly int __fbt_LateUpdate = FBTempo.Id("FBCamPose.LateUpdate");
        void LateUpdate()
        {
            if (!FBTempo.On) { LateUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
        }
        void LateUpdate__fbt() { var c = Camera.main; if (c && pose.HasValue) c.transform.SetPositionAndRotation(pose.Value.position, pose.Value.rotation); }
    }
}
