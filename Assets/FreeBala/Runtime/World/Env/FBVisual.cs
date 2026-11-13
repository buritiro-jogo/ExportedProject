// RODADA 65 — AJUSTES VISUAIS EM ARQUIVO: StreamingAssets/fb_visual.json (ou -fbvisual <arquivo>). Calibra luz do dia, pós, SSAO, postes,
// poças e minimapa NO EXE, sem build nova a cada tentativa. Os padrões deste C# são NEUTROS (= rodada 64); -fbvisualoff ignora o arquivo
// (comparação ANTES × DEPOIS no mesmo exe). Lido uma vez, no primeiro acesso.
//   · DIA: peso dk = sol de -2° a 12° (nada muda à noite). sunMul/ambMul mexem na razão sol:sombra — a r64 calibrou o dia IGUAL ao aprovado,
//     por isso "não mudou nada"; aqui o sol ganha força contra o ambiente (sombra mais densa, volume), com a exposição compensando.
//   · SSAO: os valores do recurso do renderer (intensidade, raio, força na luz direta) trocados em tempo de jogo, só no PC.
//   · POSTES: inclinação do facho para LONGE do poste/parede (achado por raios curtos), abertura, força e alcance.
//   · POÇAS: modo novo do FreeBala/Puddle (reflexo em espaço de tela + refração do chão + Fresnel), só com a textura opaca da câmera (PC).
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FreeBala
{
    [System.Serializable]
    public class FBVisualCfg
    {
        // dia
        public float sunMul = 1, ambMul = 1, groundMul = 1, fogDayMul = 1, dayExposure = 0, dayContrast = 0, daySaturation = 0, sunShadow = -1;
        public string tonemap = "";                                                    // "" = mantém (Neutral); "aces" | "neutral"
        public Color splitShadows = new(.5f, .5f, .5f), splitHighlights = new(.5f, .5f, .5f); public float splitBalance = 0;
        public float shmShadows = 0, shmHighlights = 0;                                 // ShadowsMidtonesHighlights: luminância (-1..1) das sombras/altas
        // SSAO (-1 = mantém o do renderer)
        public float ssaoIntensity = -1, ssaoRadius = -1, ssaoDirect = -1;
        // postes
        public float lampTilt = 0, lampAngle = 0, lampInner = 0, lampIntensityMul = 1, lampRangeMul = 1, lampHeadGlow = 1, lampGlowSize = 1;
        // poças (0 = shader antigo; 1 = reflexo em tela + refração)
        public float puddleSSR = 0, puddleRough = .06f, puddleWind = .5f, puddleSsrDist = 40;
        // minimapa: "" = foto (antigo) | "neutro"
        public string minimap = "";
        // r65 2ª parte (iluminação global AAA + veículos). 0 = comportamento da r64.
        public float lampCookie = 0;          // cookie de luminária de LED nos postes (distribuição de rua, sem círculo)
        public float wetPbr = 0;              // 1 = chão molhado SÓ com chuva, por porosidade (albedo↓, brilho↑) e mapas _msw; 0 = antigo (molhado toda noite)
        public float waterPbr = 0;            // 1 = rio/lagoa com refração, absorção (Beer-Lambert), turbidez e reflexo em tela
        public float carLights = 0;           // 1 = faróis sem "bolas" (corona só de longe e pequena), lentes calibradas, cookies baixo/alto, transições
        public float carHighBeam = 1;         // farol alto disponível (tecla J no carro do jogador)
        public float giroflex = 0;            // 1 = giroflex com sequência real, sombra no par mais perto (não atravessa o próprio carro)
        public float carShadowDist = 0;       // distância das sombras dos carros (0 = antigo: 45 m PC / 25 m celular)
        public float matSanity = 0;           // 1 = corrige metálico/lisura impossíveis em materiais não metálicos do mapa (log)

    }

    public static class FBVisual
    {
        static FBVisualCfg c; public static bool Off { get; private set; }
        public static string Source { get; private set; } = "padrão";
        public static FBVisualCfg C
        {
            get
            {
                if (c != null) return c;
                var a = System.Environment.GetCommandLineArgs(); Off = System.Array.IndexOf(a, "-fbvisualoff") >= 0;
                c = new FBVisualCfg(); if (Off) { Source = "desligado (-fbvisualoff)"; return c; }
                int i = System.Array.IndexOf(a, "-fbvisual"); string p = i >= 0 && i + 1 < a.Length ? a[i + 1] : System.IO.Path.Combine(Application.streamingAssetsPath, "fb_visual.json");
                try { if (System.IO.File.Exists(p)) { JsonUtility.FromJsonOverwrite(System.IO.File.ReadAllText(p), c); Source = p; } }
                catch (System.Exception e) { Debug.LogWarning($"[FreeBala][visual] {p}: {e.Message}"); c = new FBVisualCfg(); Source = "erro → padrão"; }
                Debug.Log($"[FreeBala][visual] {Source}: sol×{c.sunMul} amb×{c.ambMul} tm '{c.tonemap}' exp {c.dayExposure:+0.00;-0.00} ssao {c.ssaoIntensity}/{c.ssaoRadius}/{c.ssaoDirect} postes tilt {c.lampTilt}° poças {c.puddleSSR} minimapa '{c.minimap}'");
                return c;
            }
        }
        // peso do dia pela elevação do sol
        public static float DayK(float el) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-2, 12, el));

        // SSAO do renderer (PC): campos internos do recurso (m_Settings) por reflexão; volta ao original na troca de cena não é preciso (o
        // asset não é salvo no player)
        static float[] ssao0;
        public static void ApplyRenderer()
        {
            var v = C; if (FBQuality.Mobile) return;
            if (!(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)) return;
            var fl = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            if (!(fl?.GetValue(urp) is ScriptableRendererData[] list)) return;
            foreach (var rd in list)
            {
                if (!rd) continue;
                foreach (var f in rd.rendererFeatures)
                {
                    if (!(f is ScreenSpaceAmbientOcclusion)) continue;
                    var sf = f.GetType().GetField("m_Settings", BindingFlags.NonPublic | BindingFlags.Instance); var s = sf?.GetValue(f); if (s == null) continue;
                    var t = s.GetType(); var fi = t.GetField("Intensity", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); var fr = t.GetField("Radius", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var fd = t.GetField("DirectLightingStrength", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fi == null || fr == null || fd == null) continue;
                    ssao0 ??= new[] { (float)fi.GetValue(s), (float)fr.GetValue(s), (float)fd.GetValue(s) };
                    fi.SetValue(s, v.ssaoIntensity >= 0 ? v.ssaoIntensity : ssao0[0]); fr.SetValue(s, v.ssaoRadius >= 0 ? v.ssaoRadius : ssao0[1]); fd.SetValue(s, v.ssaoDirect >= 0 ? v.ssaoDirect : ssao0[2]);
                    if (t.IsValueType) sf.SetValue(f, s);
                }
            }
        }

        // poças: modo novo só com a textura opaca da câmera principal (PC); o shader lê _FBPuddleSSR
        public static bool PuddleSSR => !Off && C.puddleSSR > .5f && !FBQuality.Mobile && Application.platform != RuntimePlatform.WebGLPlayer;
        public static void ApplyGlobals()
        {
            var v = C; bool ssr = PuddleSSR;
            Shader.SetGlobalFloat("_FBPuddleSSR", ssr ? 1 : 0);
            Shader.SetGlobalFloat("_FBWaterPbr", ssr && v.waterPbr > .5f ? 1 : 0);
            Shader.SetGlobalVector("_FBPuddleP", new Vector4(Mathf.Clamp(v.puddleRough, .01f, .5f), Mathf.Max(0, v.puddleWind), Mathf.Clamp(v.puddleSsrDist, 5, 120), 0));
        }
        public static void PrepareCamera(Camera cam)
        {
            CookieKeeper(cam);
            if (!cam || !PuddleSSR) return; var d = cam.GetUniversalAdditionalCameraData(); if (d && d.requiresColorOption != CameraOverrideOption.On) d.requiresColorOption = CameraOverrideOption.On;
            if (d && d.requiresDepthOption != CameraOverrideOption.On) d.requiresDepthOption = CameraOverrideOption.On;   // reflexo/refração precisam do depth
        }
        // r65: o URP liga a variante _LIGHT_COOKIES de TODOS os shaders só quando há luz com cookie visível. Postes (LED) e faróis acendem no
        // anoitecer → a primeira luz com cookie no meio da partida obrigaria a compilar as variantes (engasgo). Uma luz minúscula com cookie
        // presa à câmera (1 m à frente, 5 cm de alcance, intensidade 0,001) mantém a variante ativa desde o carregamento — sem efeito visível.
        static void CookieKeeper(Camera cam)
        {
            if (!cam || Off || FBQuality.Mobile || (C.lampCookie <= 0 && C.carLights <= .5f) || cam.transform.Find("FBCookieKeeper")) return;
            var l = new GameObject("FBCookieKeeper").AddComponent<Light>(); l.transform.SetParent(cam.transform, false); l.transform.localPosition = new Vector3(0, 0, 1);
            l.type = LightType.Spot; l.range = .05f; l.spotAngle = 20; l.intensity = .001f; l.shadows = LightShadows.None; l.cookie = Texture2D.whiteTexture;
        }
    }
}
