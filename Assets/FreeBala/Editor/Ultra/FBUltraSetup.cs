// [mirante-ultra 03/11/2026] Cria o preset gráfico ULTRA do PC sem mexer nos outros perfis:
//  · Assets/FreeBala/Settings/FB_URP_Ultra.asset + FB_URP_Ultra_Renderer.asset + FB_Pos_Ultra.asset (cópias do PC Alto, depois ajustadas);
//  · nível de qualidade "Ultra" no FIM da lista do QualitySettings (os índices 0–3 Mobile/PC Médio/PC Alto/Web não mudam) e
//    padrão do Standalone (PC). Android, iPhone e WebGL continuam no Mobile e no Web.
// Não chame o FBProjectSetup depois disto esperando o mesmo resultado: ele só conhece os 4 perfis antigos (o Ultra fica intacto,
// porque o Quality() dele reduz a lista para 4 — rode este de novo depois dele).
// Rodar: menu FreeBala/Gráficos/Criar preset Ultra, ou pelo laboratório: lab_run.ps1 -Pedido "metodo FBUltraSetup.Criar".
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class FBUltraSetup
{
    const string Dir = "Assets/FreeBala/Settings";
    const string PC = Dir + "/FB_URP_PC.asset", PCR = Dir + "/FB_URP_PC_Renderer.asset", PCPos = Dir + "/FB_Pos_PC.asset";
    const string U = Dir + "/FB_URP_Ultra.asset", UR = Dir + "/FB_URP_Ultra_Renderer.asset", UPos = Dir + "/FB_Pos_Ultra.asset";
    static readonly StringBuilder log = new();
    static void L(string s) { log.AppendLine(s); Debug.Log("[FreeBala][ultra] " + s); }

    [MenuItem("FreeBala/Gráficos/Criar preset Ultra")]
    public static void Criar()
    {
        log.Clear();
        if (!File.Exists(U)) { if (!AssetDatabase.CopyAsset(PC, U)) throw new Exception("não copiou " + PC); L("criado " + U); }
        if (!File.Exists(UR)) { if (!AssetDatabase.CopyAsset(PCR, UR)) throw new Exception("não copiou " + PCR); L("criado " + UR); }
        if (!File.Exists(UPos)) { if (!AssetDatabase.CopyAsset(PCPos, UPos)) throw new Exception("não copiou " + PCPos); L("criado " + UPos); }
        AssetDatabase.Refresh();
        var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(U);
        var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(UR);
        var pos = AssetDatabase.LoadAssetAtPath<VolumeProfile>(UPos);
        if (!a || !rd || !pos) throw new Exception("assets do Ultra não carregaram");
        Renderer(rd);
        Pipeline(a, rd, pos);
        Pos(pos);
        Quality(a);
        BrgKeepAll();
        AssetDatabase.SaveAssets();
        var diag = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../_diag"));
        try { if (Directory.Exists(diag)) File.WriteAllText(Path.Combine(diag, "ultra_setup.txt"), log.ToString()); } catch { }
        L("pronto");
    }

    static SerializedProperty P(SerializedObject so, string n) { var p = so.FindProperty(n); if (p == null) L("AVISO campo não achado: " + n); return p; }
    static void I(SerializedObject so, string n, int v) { var p = P(so, n); if (p != null) p.intValue = v; }   // valor bruto (enums do URP: MSAA 1/2/4/8, sombra 4096/8192…)
    static void B(SerializedObject so, string n, bool v) { var p = P(so, n); if (p != null) p.boolValue = v; }
    static void F(SerializedObject so, string n, float v) { var p = P(so, n); if (p != null) p.floatValue = v; }

    // ---- renderer: Forward+, decals em DBuffer (Albedo+Normal+MAOS), SSAO cheio, SSR nativo
    static void Renderer(UniversalRendererData rd)
    {
        rd.renderingMode = RenderingMode.ForwardPlus;            // Deferred+ fica para o comparativo (vidro/água transparentes vão no forward de qualquer jeito)
        foreach (var f in rd.rendererFeatures.Where(x => x))
        {
            var so = new SerializedObject(f);
            if (f is DecalRendererFeature)
            {
                I(so, "m_Settings.technique", 1);                  // DBuffer
                I(so, "m_Settings.dBufferSettings.surfaceData", 2); // Albedo Normal MAOS
                F(so, "m_Settings.maxDrawDistance", 250);
                so.ApplyModifiedPropertiesWithoutUndo(); L("decais: DBuffer, Albedo+Normal+MAOS, 250 m");
            }
            else if (f is ScreenSpaceAmbientOcclusion)
            {
                I(so, "m_Settings.Source", 1); I(so, "m_Settings.NormalSamples", 2); I(so, "m_Settings.Samples", 0); I(so, "m_Settings.BlurQuality", 0);
                B(so, "m_Settings.Downsample", false); F(so, "m_Settings.Intensity", .9f); F(so, "m_Settings.Radius", .4f);
                so.ApplyModifiedPropertiesWithoutUndo(); L("SSAO: normais, 12 amostras, blur alto, resolução cheia");
            }
        }
        // SSR nativo do URP 17.6 (o volume do FB_Pos_PC existia, mas o renderer não tinha a feature: não fazia nada)
        var ssrT = AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetType("UnityEngine.Rendering.Universal.ScreenSpaceReflectionRendererFeature")).FirstOrDefault(x => x != null);
        if (ssrT == null) L("AVISO SSR: tipo não encontrado (falta o define URP_SCREEN_SPACE_REFLECTION no Standalone?)");
        else if (!rd.rendererFeatures.Any(x => x && x.GetType() == ssrT))
        {
            var f = (ScriptableRendererFeature)ScriptableObject.CreateInstance(ssrT); f.name = "SSR"; AssetDatabase.AddObjectToAsset(f, rd); rd.rendererFeatures.Add(f); f.SetActive(true);
            L("SSR: feature adicionada");
        }
        SyncFeatureMap(rd); EditorUtility.SetDirty(rd);
    }
    static void SyncFeatureMap(ScriptableRendererData rd)
    {
        var so = new SerializedObject(rd); var map = so.FindProperty("m_RendererFeatureMap"); var list = so.FindProperty("m_RendererFeatures"); if (map == null || list == null) return;
        map.arraySize = list.arraySize;
        for (int i = 0; i < list.arraySize; i++) { var o = list.GetArrayElementAtIndex(i).objectReferenceValue; map.GetArrayElementAtIndex(i).longValue = o && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out _, out long id) ? id : 0; }
        so.ApplyModifiedPropertiesWithoutUndo(); rd.SetDirty();
    }

    // ---- pipeline: HDR 64 bits, sombras 8192 / 300 m / 4 cascatas, Opaque Texture, probes com box projection, GPU Resident Drawer
    static void Pipeline(UniversalRenderPipelineAsset a, UniversalRendererData rd, VolumeProfile pos)
    {
        var so = new SerializedObject(a);
        var list = P(so, "m_RendererDataList"); if (list != null) { list.arraySize = 1; list.GetArrayElementAtIndex(0).objectReferenceValue = rd; }
        B(so, "m_SupportsHDR", true); I(so, "m_HDRColorBufferPrecision", 1);   // 64 bits
        I(so, "m_MSAA", 1); F(so, "m_RenderScale", 1);                          // MSAA desligado: DLSS/STP/TAA (o FBUltra liga 8x se o jogador escolher)
        B(so, "m_RequireDepthTexture", true); B(so, "m_RequireOpaqueTexture", true); I(so, "m_OpaqueDownsampling", 0);
        B(so, "m_MainLightShadowsSupported", true); I(so, "m_MainLightShadowmapResolution", 8192);
        B(so, "m_AdditionalLightShadowsSupported", true); I(so, "m_AdditionalLightsShadowmapResolution", 8192);
        I(so, "m_AdditionalLightsShadowResolutionTierLow", 512); I(so, "m_AdditionalLightsShadowResolutionTierMedium", 1024); I(so, "m_AdditionalLightsShadowResolutionTierHigh", 2048);
        I(so, "m_AdditionalLightsPerObjectLimit", 8);
        F(so, "m_ShadowDistance", 300); I(so, "m_ShadowCascadeCount", 4);
        B(so, "m_SoftShadowsSupported", true); I(so, "m_SoftShadowQuality", 3);
        B(so, "m_ReflectionProbeBlending", true); B(so, "m_ReflectionProbeBoxProjection", true);
        I(so, "m_ColorGradingMode", 1); I(so, "m_ColorGradingLutSize", 65);
        B(so, "m_UseSRPBatcher", true);
        I(so, "m_GPUResidentDrawerMode", 1); B(so, "m_GPUResidentDrawerEnableOcclusionCullingInCameras", true);
        var vp = P(so, "m_VolumeProfile"); if (vp != null) vp.objectReferenceValue = pos;
        so.ApplyModifiedPropertiesWithoutUndo();
        // divisões das 4 cascatas para 300 m: 12 m · 36 m · 105 m · 300 m (o personagem e a mira ficam na 1ª, a torre inteira na 3ª/4ª)
        a.cascade4Split = new Vector3(.04f, .12f, .35f); a.cascadeBorder = .2f;
#if ENABLE_UPSCALER_FRAMEWORK
        a.upscalerName = "Deep Learning Super Sampling 4";
        L("upscaler padrão: " + a.upscalerName + " · opções registradas: " + string.Join(", ", a.upscalerOptions.Where(o => o != null).Select(o => o.GetType().Name)));
#else
        L("AVISO: ENABLE_UPSCALER_FRAMEWORK não definido: DLSS indisponível neste editor");
#endif
        EditorUtility.SetDirty(a);
        L("pipeline: HDR 64 bits, sombra 8192/300 m/4 cascatas, Opaque Texture, box projection, GPU Resident Drawer + oclusão");
    }

    // ---- pós (FB_Pos_Ultra): ACES do PC + bloom de alta qualidade, granulado leve, sem motion blur e sem aberração
    static void Pos(VolumeProfile vp)
    {
        T C<T>() where T : VolumeComponent
        {
            if (!vp.TryGet<T>(out var c)) { c = vp.Add<T>(true); c.name = typeof(T).Name; AssetDatabase.AddObjectToAsset(c, vp); }
            c.active = true; return c;
        }
        var tm = C<Tonemapping>(); tm.mode.Override(TonemappingMode.ACES);
        var bl = C<Bloom>(); bl.highQualityFiltering.Override(true); bl.downscale.Override(BloomDownscaleMode.Half); bl.maxIterations.Override(8); bl.intensity.Override(.3f); bl.threshold.Override(1.05f);
        var vg = C<Vignette>(); vg.intensity.Override(.15f);
        var fg = C<FilmGrain>(); fg.type.Override(FilmGrainLookup.Thin1); fg.intensity.Override(.08f); fg.response.Override(.8f);
        var mb = C<MotionBlur>(); mb.intensity.Override(0); mb.active = false;
        var ca = C<ChromaticAberration>(); ca.intensity.Override(0); ca.active = false;
        var wb = C<WhiteBalance>(); wb.temperature.Override(6); wb.tint.Override(0);   // calor de Roraima, sem cara de filtro
        EditorUtility.SetDirty(vp); foreach (var c in vp.components) EditorUtility.SetDirty(c);
        L("pós: ACES, bloom alta qualidade, granulado 0,08, sem motion blur/aberração, WB +6");
    }

    // ---- QualitySettings: "Ultra" no fim (copiado do PC Alto) e padrão do Standalone
    static void Quality(UniversalRenderPipelineAsset ultra)
    {
        var qs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]; var so = new SerializedObject(qs);
        var levels = so.FindProperty("m_QualitySettings");
        int idx = -1, alto = -1;
        for (int i = 0; i < levels.arraySize; i++) { var n = levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue; if (n == "Ultra") idx = i; if (n == "PC Alto") alto = i; }
        if (idx < 0)
        {
            if (alto < 0) throw new Exception("nível 'PC Alto' não encontrado");
            levels.InsertArrayElementAtIndex(alto); levels.MoveArrayElement(alto + 1, levels.arraySize - 1); idx = levels.arraySize - 1;   // cópia do PC Alto vai para o fim
            L("nível de qualidade 'Ultra' criado no índice " + idx);
        }
        var Lv = levels.GetArrayElementAtIndex(idx);
        Lv.FindPropertyRelative("name").stringValue = "Ultra";
        Lv.FindPropertyRelative("customRenderPipeline").objectReferenceValue = ultra;
        Lv.FindPropertyRelative("vSyncCount").intValue = 0;
        Lv.FindPropertyRelative("antiAliasing").intValue = 0;
        Lv.FindPropertyRelative("anisotropicTextures").intValue = 2;       // forçado (16x)
        Lv.FindPropertyRelative("lodBias").floatValue = 2.4f;
        Lv.FindPropertyRelative("skinWeights").intValue = 4;
        Lv.FindPropertyRelative("globalTextureMipmapLimit").intValue = 0;
        var tpe = Lv.FindPropertyRelative("terrainPixelError"); if (tpe != null) tpe.floatValue = 1;
        var sp = Lv.FindPropertyRelative("softParticles"); if (sp != null) sp.boolValue = true;
        var per = so.FindProperty("m_PerPlatformDefaultQuality");
        if (per != null)
            for (int i = 0; i < per.arraySize; i++)
            {
                var e = per.GetArrayElementAtIndex(i);
                if (e.FindPropertyRelative("first").stringValue == "Standalone") { e.FindPropertyRelative("second").intValue = idx; L("Standalone (PC) passa a abrir no Ultra"); }
            }
        so.ApplyModifiedPropertiesWithoutUndo();
        L("níveis: " + string.Join(" · ", QualitySettings.names));
    }

    // GPU Resident Drawer precisa das variantes BatchRendererGroup no build
    static void BrgKeepAll()
    {
        var gs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]; var so = new SerializedObject(gs);
        var p = so.FindProperty("m_BrgStripping"); if (p == null) { L("AVISO m_BrgStripping não achado"); return; }
        var et = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => { try { return x.GetTypes(); } catch { return Type.EmptyTypes; } }).FirstOrDefault(t => t.IsEnum && t.Name == "BatchRendererGroupStrippingMode");
        if (et == null || !Enum.IsDefined(et, "KeepAll")) { L("AVISO enum BatchRendererGroupStrippingMode.KeepAll não achado"); return; }
        p.intValue = Convert.ToInt32(Enum.Parse(et, "KeepAll")); so.ApplyModifiedPropertiesWithoutUndo(); L("variantes BatchRendererGroup: manter todas");
    }
}
