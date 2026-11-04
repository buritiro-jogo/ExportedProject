// [mirante-ultra 03/11/2026] Preset gráfico ULTRA (só PC). Usa o tier "Ultra" do QualitySettings (FB_URP_Ultra, criado pelo
// Editor/FBUltraSetup) e guarda as opções avançadas do menu de gráficos em PlayerPrefs:
//  · AA: DLSS 4 em DLAA (resolução cheia, qualidade máxima) · DLSS Qualidade (renderiza a 67 % e o DLSS reconstrói) · STP · TAA · MSAA 8x.
//    DLSS só em placa NVIDIA RTX; nas outras o jogo usa STP no lugar.
//  · Escala de render 1,0 / 1,5 / 2,0 (supersampling; só com TAA e MSAA, porque DLSS e STP já decidem a resolução).
//  · Sombras ALTA (4096, 200 m) ou ULTRA (8192, 300 m: cobre a torre do Mirante, 120 m, e a arena de 600 m).
//  · SSAO, névoa volumétrica e distância da vegetação.
// Nada aqui vale fora do nível ULTRA no PC: Mobile, Web, PC Médio e PC Alto continuam como estavam.
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FreeBala
{
    public static class FBUltra
    {
        public const string TierName = "Ultra";
        public enum AA { DLAA = 0, DLSSQualidade = 1, STP = 2, TAA = 3, MSAA8x = 4 }
        public static readonly string[] AANomes = { "DLSS (DLAA)", "DLSS QUALIDADE", "STP", "TAA", "MSAA 8X" };
        public static readonly string[] AADicas = { "NVIDIA RTX: resolução cheia + IA, o mais limpo", "NVIDIA RTX: renderiza menor e o DLSS reconstrói (mais FPS)", "upscaler temporal da Unity (qualquer placa)", "antisserrilhado temporal da Unity", "multiamostragem clássica (serrilha mais em folhas)" };
        public static readonly float[] Escalas = { 1f, 1.5f, 2f };
        public static readonly string[] EscalaNomes = { "100%", "150%", "200%" };
        public static readonly float[] VegDist = { 160f, 250f, 400f };
        public static readonly string[] VegNomes = { "PADRÃO (160 M)", "LONGE (250 M)", "MÁXIMA (400 M)" };

        const string DLSSName = "Deep Learning Super Sampling 4", STPName = "Spatial-Temporal Post-Processing";

        static int GetI(string k, int d) { try { return PlayerPrefs.GetInt(k, d); } catch { return d; } }
        static void SetI(string k, int v) { PlayerPrefs.SetInt(k, v); PlayerPrefs.Save(); }
        public static AA Modo { get => (AA)Mathf.Clamp(GetI("fb_ultra_aa", (int)AA.DLAA), 0, 4); set { SetI("fb_ultra_aa", (int)value); Reaplica(); } }
        public static int Escala { get => Mathf.Clamp(GetI("fb_ultra_escala", 0), 0, 2); set { SetI("fb_ultra_escala", value); Reaplica(); } }
        public static bool SombraUltra { get => GetI("fb_ultra_sombra", 1) == 1; set { SetI("fb_ultra_sombra", value ? 1 : 0); Reaplica(); } }
        public static bool SSAO { get => GetI("fb_ultra_ssao", 1) == 1; set { SetI("fb_ultra_ssao", value ? 1 : 0); FBGfx.Refresh(); } }
        public static bool NevoaVol { get => GetI("fb_ultra_nevoa", 1) == 1; set { SetI("fb_ultra_nevoa", value ? 1 : 0); FBGfx.Refresh(); } }
        public static int Vegetacao { get => Mathf.Clamp(GetI("fb_ultra_veg", 0), 0, 2); set { SetI("fb_ultra_veg", value); Reaplica(); } }

        public static int TierIndex => Array.IndexOf(QualitySettings.names, TierName);
        // nível ULTRA no PC com o tier "Ultra" presente no projeto
        public static bool Ativo => FBQuality.Level == FBQLevel.Ultra && !FBQuality.Mobile && FBQuality.Device.plat == FBPlat.PC && TierIndex >= 0;

        // DLSS: placa NVIDIA RTX (série 20 em diante) no Direct3D 11/12 ou Vulkan
        public static bool TemDLSS
        {
            get
            {
                var n = SystemInfo.graphicsDeviceName ?? ""; var api = SystemInfo.graphicsDeviceType;
                bool rtx = n.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0 && n.IndexOf("RTX", StringComparison.OrdinalIgnoreCase) >= 0;
                return rtx && (api == GraphicsDeviceType.Direct3D11 || api == GraphicsDeviceType.Direct3D12 || api == GraphicsDeviceType.Vulkan);
            }
        }
        // modo que vale de fato (DLSS sem placa RTX vira STP)
        public static AA ModoEfetivo { get { var m = Modo; return (m == AA.DLAA || m == AA.DLSSQualidade) && !TemDLSS ? AA.STP : m; } }

        static void Reaplica() { if (FBQuality.Level == FBQLevel.Ultra) FBQuality.Apply(FBQLevel.Ultra); }

        // ajusta a tabela de flags do FBQuality para o Ultra do PC (chamado no Apply, logo depois do Build)
        public static void AjustaPerfil(FBQProfile p)
        {
            bool su = SombraUltra;
            p.shadows = true; p.shadowDistance = su ? 300 : 200; p.shadowRes = su ? 8192 : 4096; p.shadowCascades = 4; p.softShadows = true;
            p.smallCasterSize = .15f; p.maxPixelLights = 8; p.realtimeReflections = true;
            p.vegDrawDistance = VegDist[Vegetacao]; p.vegLodDistance = Mathf.Max(55, VegDist[Vegetacao] * .35f); p.vegMaxInstances = Vegetacao == 2 ? 160000 : 120000;
            p.lodBias = 2.4f; p.mipLimit = 0; p.skinWeights = 4; p.animCullDistance = 300; p.drawDistance = 1600;
            var m = ModoEfetivo;
            p.renderScale = m == AA.DLSSQualidade ? .667f : (m == AA.TAA || m == AA.MSAA8x) ? Escalas[Escala] : 1f;
            p.minRenderScale = p.renderScale; p.dynamicResolution = false;
            // FPS alvo = taxa do monitor (60 Hz no PC do Ruan): sem quadro sobrando que o monitor não mostra; o teste -fbtesteexe destrava sozinho
            int hz = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value); p.targetFps = hz >= 50 ? hz : 60;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-fbtesteexe") >= 0) p.targetFps = -1;   // medição de desempenho: sem trava
        }

        // ajusta o asset FB_URP_Ultra (chamado no Apply depois que o FBQuality gravou escala/sombra)
        public static void AplicaAsset(UniversalRenderPipelineAsset a)
        {
            if (!a) return;
            var m = ModoEfetivo;
            a.msaaSampleCount = m == AA.MSAA8x ? 8 : 1;            // TAA, STP e DLSS exigem MSAA desligado
#if ENABLE_UPSCALER_FRAMEWORK
            string up = m == AA.DLAA || m == AA.DLSSQualidade ? DLSSName : m == AA.STP ? STPName : "Bilinear";
            try { a.upscalerName = up; } catch (Exception e) { Debug.LogWarning("[FreeBala][ultra] upscaler: " + e.Message); }
            if (up == DLSSName) DLSSQualidade(a, m == AA.DLAA ? "DLAA" : "MaximumQuality");
#endif
            Debug.Log($"[FreeBala][ultra] AA {AANomes[(int)m]}{(m != Modo ? " (DLSS indisponível nesta placa)" : "")} · escala {a.renderScale:0.##} · sombra {a.mainLightShadowmapResolution} {a.shadowDistance:0} m · upscaler {a.upscalerName}");
        }

#if ENABLE_UPSCALER_FRAMEWORK
        // DLSSOptions fica no asset (lista de opções dos upscalers); o tipo só existe com o módulo NVIDIA: acesso por reflexão
        static void DLSSQualidade(UniversalRenderPipelineAsset a, string modo)
        {
            try
            {
                foreach (var o in a.upscalerOptions)
                {
                    if (o == null || o.GetType().Name != "DLSSOptions") continue;
                    var pr = o.GetType().GetProperty("DLSSQualityMode", BindingFlags.Public | BindingFlags.Instance);
                    if (pr != null && pr.CanWrite && pr.PropertyType.IsEnum && Enum.IsDefined(pr.PropertyType, modo)) { pr.SetValue(o, Enum.Parse(pr.PropertyType, modo)); continue; }
                    var f = o.GetType().GetField("m_DLSSQualityMode", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null && f.FieldType.IsEnum && Enum.IsDefined(f.FieldType, modo)) f.SetValue(o, Enum.Parse(f.FieldType, modo));
                }
            }
            catch (Exception e) { Debug.LogWarning("[FreeBala][ultra] DLSS: " + e.Message); }
        }
#endif

        // AA da câmera principal no Ultra: só o TAA é por câmera; DLSS/STP vêm do asset; MSAA 8x ganha SMAA por cima
        public static AntialiasingMode CamAA => ModoEfetivo switch { AA.TAA => AntialiasingMode.TemporalAntiAliasing, AA.MSAA8x => AntialiasingMode.SubpixelMorphologicalAntiAliasing, _ => AntialiasingMode.None };
    }
}
