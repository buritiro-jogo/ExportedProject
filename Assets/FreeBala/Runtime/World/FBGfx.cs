// PERFIL GRÁFICO em tempo de execução (Agente 5 — gráficos). Decide o nível visual e liga/desliga os recursos caros por plataforma:
//  · Nível (FBGfxTier): Mobile, Web, PCMedio, PCAlto. Vem do nível de qualidade do Unity (nomes do FBProjectSetup: "Mobile", "PC Médio",
//    "PC Alto", "Web") e da plataforma; o FBQuality (Agente 9) pode decidir por fora com FBGfx.TierOverride / FBGfx.FlagsOverride.
//  · Flags (FBGfxFlags): SSAO, névoa de altura, nuvens volumétricas, céu leve, detail maps das fachadas/ruas, vento na vegetação,
//    partículas ambientes (insetos nos postes, poeira no sol, fumaça), suavização da câmera principal.
//  · Aplica: antialiasing da câmera principal (MSAA vem do pipeline; FXAA/SMAA aqui), SSAO/névoa de altura nos renderers, keyword do céu,
//    e o vento global da vegetação (_FBWind). Detail maps ficam ligados em todos os níveis (texturas pequenas; desligar keyword em
//    tempo de execução exigiria variantes que o build não leva).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public enum FBGfxTier { Mobile = 0, Web = 1, PCMedio = 2, PCAlto = 3 }
    public struct FBGfxFlags
    {
        public bool ssao, heightFog, volClouds, skyLite, detailMaps, wind, cloudSprites;
        public bool volumetric, ssr; public int volSteps; public float volScale;              // luz volumétrica (FBVolumetric) e reflexo em espaço de tela (URP SSR)
        public int lampInsects, dustMotes, smokePlumes;                                   // limites das partículas ambientes (FBAmbientFx)
        public bool grass; public float grassNear, grassFar, grassNearSpacing, grassFarSpacing, grassDensity;   // tapete de grama (FBGrassCarpet): raios/passos dos 2 anéis
        public AntialiasingMode camAA;
    }

    public static class FBGfx
    {
        public static System.Func<FBGfxTier> TierOverride;                               // FBQuality pode impor o nível
        public static System.Func<FBGfxTier, FBGfxFlags> FlagsOverride;                  // … ou as flags inteiras
        public static FBGfxTier Tier { get; private set; } = FBGfxTier.PCAlto;
        public static FBGfxFlags F { get; private set; } = Defaults(FBGfxTier.PCAlto);
        public static event System.Action Changed;
        static bool hooked;

        public static FBGfxFlags Defaults(FBGfxTier t) => t switch
        {
            FBGfxTier.Mobile => new FBGfxFlags { skyLite = true, wind = true, lampInsects = 2, dustMotes = 0, smokePlumes = 1, camAA = AntialiasingMode.None,
                grass = true, grassNear = 8, grassFar = 17, grassNearSpacing = .36f, grassFarSpacing = .7f, grassDensity = .85f },
            FBGfxTier.Web => new FBGfxFlags { skyLite = true, wind = true, cloudSprites = true, lampInsects = 3, dustMotes = 40, smokePlumes = 1, camAA = AntialiasingMode.FastApproximateAntialiasing,
                grass = true, grassNear = 8, grassFar = 18, grassNearSpacing = .36f, grassFarSpacing = .7f, grassDensity = .85f },
            FBGfxTier.PCMedio => new FBGfxFlags { ssao = true, heightFog = true, detailMaps = true, wind = true, cloudSprites = true, volumetric = true, volSteps = 14, volScale = .4f, lampInsects = 6, dustMotes = 90, smokePlumes = 3, camAA = AntialiasingMode.None,
                grass = true, grassNear = 11, grassFar = 26, grassNearSpacing = .3f, grassFarSpacing = .6f, grassDensity = 1 },
            _ => new FBGfxFlags { ssao = true, heightFog = true, volClouds = false, detailMaps = true,   // volClouds: a variante só passou a compilar em 20/10 (dt dentro de comentário) e sai em 'panquecas' cinza — fica desligada até ser refeita; céu 2D aprovado
                       wind = true, volumetric = true, volSteps = 26, volScale = .5f, ssr = true, lampInsects = 8, dustMotes = 140, smokePlumes = 4, camAA = AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                       grass = true, grassNear = 14, grassFar = 34, grassNearSpacing = .27f, grassFarSpacing = .55f, grassDensity = 1 },
        };

        static FBGfxTier Detect()
        {
            if (TierOverride != null) return TierOverride();
            if (Application.platform == RuntimePlatform.WebGLPlayer) return FBGfxTier.Web;      // WebGL2: nunca SSAO/volumétrico
            int q = QualitySettings.GetQualityLevel(); var names = QualitySettings.names; string n = q >= 0 && q < names.Length ? names[q].ToLowerInvariant() : "";
            if (n.Contains("web")) return FBGfxTier.Web;
            if (n.Contains("mobile") || n.Contains("baixo")) return FBGfxTier.Mobile;
            if (n.Contains("médio") || n.Contains("medio")) return FBGfxTier.PCMedio;
            if (n.Contains("alto")) return FBGfxTier.PCAlto;
            return Application.isMobilePlatform ? FBGfxTier.Mobile : FBGfxTier.PCAlto;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!hooked)
            {
                hooked = true;
                QualitySettings.activeQualityLevelChanged += (_, _) => Refresh();
                SceneManager.sceneLoaded += (_, _) => Refresh();
                RenderPipelineManager.beginCameraRendering += OnCam;
            }
            SetWind(new Vector2(.8f, .6f), 1);
            Refresh();
        }

        // QUAL névoa volumétrica fica ligada. Linha de comando: -fbvol casa|cq|ambas|nenhuma
        //  · "" (padrão) e "casa": só o FBVolumetricFeature, o raymarch da casa — é o que o jogo usa.
        //  · "cq": só o pacote URP Volumetric Fog.  · "ambas": os dois.  · "nenhuma": sem névoa volumétrica.
        // Medido em 06/11 na Vila Jardim (pose vj5, 4 corridas de cada), fração de pixels azulados: com o CQ ligado 46/42/17/47 %,
        // só com o raymarch da casa 12/13/13/12 %, sem nenhuma das duas 13/12/13/13 %. Ou seja, quem lavava a cena de azul era o
        // pacote CQ sozinho, não a soma das duas; e o raymarch da casa não cobra nada por isso (FPS igual nos quatro modos).
        static string volMode;
        public static string VolMode
        {
            get
            {
                if (volMode != null) return volMode;
                volMode = "";
                var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbvol");
                if (i >= 0 && i + 1 < a.Length) volMode = a[i + 1].ToLowerInvariant();
                return volMode;
            }
        }

        public static void Refresh()
        {
            Tier = Detect(); var f = FlagsOverride != null ? FlagsOverride(Tier) : Defaults(Tier); F = f;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                foreach (var rd in Renderers(urp))
                    foreach (var feat in rd.rendererFeatures)
                    {
                        if (!feat) continue;
                        if (feat is ScreenSpaceAmbientOcclusion) feat.SetActive(f.ssao);
                        else if (feat is FBHeightFogFeature) feat.SetActive(f.heightFog);
                        else if (feat is FBVolumetricFeature) feat.SetActive(f.volumetric && VolMode != "cq" && VolMode != "nenhuma");
                        // o pacote CQ fica DESLIGADO por padrão: medido em 06/11 na Vila Jardim (pose vj5, 4 corridas de cada), com ele a
                        // cena distante era lavada de azul em 46/42/17/47 % dos pixels, contra 12 % sem ele — o mesmo valor de desligar as
                        // duas. A luz volumétrica dos postes continua, pelo raymarch da casa. Para ligá-lo de novo: -fbvol cq (ou ambas)
                        else if (feat is VolumetricFogRendererFeature) feat.SetActive(f.volumetric && (VolMode == "cq" || VolMode == "ambas"));
#if URP_SCREEN_SPACE_REFLECTION
                        else if (feat is ScreenSpaceReflectionRendererFeature) feat.SetActive(f.ssr);
#endif
                    }
            FBVolumetric.Active = f.volumetric && VolMode != "nenhuma"; FBVolumetric.Steps = Mathf.Max(8, f.volSteps); FBVolumetric.ResolutionScale = f.volScale > 0 ? f.volScale : .5f;
            Changed?.Invoke();
        }
        static IEnumerable<ScriptableRendererData> Renderers(UniversalRenderPipelineAsset a)
        {
            // rendererDataList é interno no URP 17: lê pelo campo serializado (só leitura)
            var fi = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fi?.GetValue(a) is ScriptableRendererData[] list) foreach (var r in list) if (r) yield return r;
        }

        // câmera principal: AA pós (FXAA/SMAA) por nível; o MSAA vem do pipeline. Não mexe nas câmeras overlay/radar.
        static void OnCam(ScriptableRenderContext ctx, Camera cam)
        {
            if (!cam || cam.cameraType != CameraType.Game || cam != Camera.main) return;
            var d = cam.GetUniversalAdditionalCameraData(); if (!d || d.renderType != CameraRenderType.Base) return;
            if (d.antialiasing != F.camAA) { d.antialiasing = F.camAA; d.antialiasingQuality = AntialiasingQuality.High; }
            if (!d.renderPostProcessing) d.renderPostProcessing = true;
            FBVisual.PrepareCamera(cam);                                                   // r65: textura opaca para as poças com reflexo em tela
        }

        // vento global da vegetação (FBFoliage.shader): direção XZ (Unity), força 0..2 (chuva/temporal = mais)
        public static void SetWind(Vector2 dir, float strength, float groundY = 0)
        {
            Shader.SetGlobalFloat("_FBWindGround", groundY);
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector2.right; float s = F.wind ? strength : 0;
            Shader.SetGlobalVector("_FBWind", new Vector4(dir.x, dir.y, s, s > 0 ? Mathf.Lerp(.6f, 1.4f, Mathf.Clamp01(strength - .5f)) : 0));
        }

    }
}
