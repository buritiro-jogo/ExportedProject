// Configuração do projeto (roda antes de qualquer importação — o glTFast escolhe o shader dos materiais pelo pipeline ativo):
//  - URP com quatro perfis (PC Alto, PC Médio, Mobile, Web: renderer, AA, sombras, SSAO, névoa de altura, pós-processamento) + níveis
//    de qualidade "Mobile", "PC Médio", "PC Alto", "Web" (detalhes em Pipeline()).
//  - Player Settings das 4 plataformas: IL2CPP, ARM64, Linear, entrada nova + antiga, paisagem, WebGL com descompressão embutida.
//  - Camadas de física do jogo (Mapa, Hitbox, Personagem, Veículo, Vidro) e a matriz de colisão.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FreeBala.EditorTools
{
    public static class FBProjectSetup
    {
        const string Dir = "Assets/FreeBala/Settings";
        public const int LMap = 8, LHitbox = 9, LChar = 10, LVehicle = 11, LGlass = 12, LTrigger = 13;

        [MenuItem("FreeBala/Configurar projeto (URP, plataformas, camadas)", priority = 0)]
        public static void Run() { Ensure(true); }
        public static void RunBatch() { Ensure(true); EditorApplication.Exit(0); }

        // shaders usados por materiais criados em código (FBShaders): referência direta num asset de Resources -> entram no build
        static void ShaderRefs()
        {
            const string path = "Assets/FreeBala/Resources/FBShaderRefs.asset"; Directory.CreateDirectory("Assets/FreeBala/Resources");
            var r = AssetDatabase.LoadAssetAtPath<FreeBala.FBShaderRefs>(path); bool make = !r; if (make) r = ScriptableObject.CreateInstance<FreeBala.FBShaderRefs>();
            r.lit = Shader.Find("Universal Render Pipeline/Lit"); r.unlit = Shader.Find("Universal Render Pipeline/Unlit"); r.particlesUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit"); r.sprites = Shader.Find("Sprites/Default"); r.sky = Shader.Find("FreeBala/Sky"); r.foliage = Shader.Find("FreeBala/Foliage"); r.heightFog = Shader.Find("FreeBala/HeightFog"); r.volumetric = Shader.Find("FreeBala/Volumetric"); r.contrail = Shader.Find("FreeBala/Contrail"); r.puddle = Shader.Find("FreeBala/Puddle"); r.grass = Shader.Find("FreeBala/GrassCarpet");
            if (make) AssetDatabase.CreateAsset(r, path); else EditorUtility.SetDirty(r);
            VariantTemplates(r);
        }
        // materiais-modelo salvos em Resources/FBMats: o build só leva as variações de shader usadas por algum material do projeto. Os
        // materiais de FX/UI/grafite/nuvem são criados por código com _SURFACE_TYPE_TRANSPARENT / _ALPHATEST_ON — sem estes modelos o
        // URP descartava essas variações no build e tudo saía opaco (fundo preto/branco), embora funcionasse no editor.
        static void VariantTemplates(FreeBala.FBShaderRefs r)
        {
            const string dir = "Assets/FreeBala/Resources/FBMats"; Directory.CreateDirectory(dir);
            void Make(string name, Shader sh, bool transp, bool additive, bool clip)
            {
                if (!sh) return; var path = $"{dir}/{name}.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path); bool mk = !m; if (mk) m = new Material(sh); else m.shader = sh;
                m.name = name; m.SetFloat("_Surface", transp ? 1 : 0); m.SetFloat("_Blend", additive ? 2 : 0); m.SetFloat("_AlphaClip", clip ? 1 : 0); m.SetFloat("_Cull", 0);
                if (transp) FreeBala.FBShaders.TransparentInPlace(m, additive); else { m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetFloat("_ZWrite", 1); m.renderQueue = clip ? 2450 : 2000; }
                if (clip) { m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cutoff", .03f); } else m.DisableKeyword("_ALPHATEST_ON");
                if (mk) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
            }
            foreach (var (tag, sh) in new[] { ("Unlit", r.unlit), ("Particles", r.particlesUnlit), ("Lit", r.lit) })
            {
                Make(tag + "_Alpha", sh, true, false, false); Make(tag + "_Add", sh, true, true, false); Make(tag + "_Clip", sh, false, false, true); Make(tag + "_AlphaClip", sh, true, false, true);
            }
        }
        public static void Ensure(bool force = false)
        {
            Directory.CreateDirectory(Dir);
            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = false;
            ShaderRefs();
            var pc = Pipeline("FB_URP_PC", Prof.PCAlto); var med = Pipeline("FB_URP_PCMedio", Prof.PCMedio);
            var mob = Pipeline("FB_URP_Mobile", Prof.Mobile); var web = Pipeline("FB_URP_Web", Prof.Web);
            GraphicsSettings.defaultRenderPipeline = pc;
            Quality(pc, med, mob, web);
            Player();
            Layers();
            AssetDatabase.SaveAssets();
            Debug.Log("[FreeBala] projeto configurado (URP PC Alto/PC Médio/Mobile/Web, plataformas, camadas)");
        }

        // ---- perfis gráficos (decisão URP × HDRP em unity/equipe/graficos.md: URP — único que roda em Android/iOS/WebGL)
        //  PC Alto : Forward+ (muitas luzes: tiros, postes, sirene), HDR, MSAA 4x (+SMAA na câmera, FBGfx), sombras 4 cascatas 150 m 4096 suaves
        //            altas, SSAO (normais), névoa de altura, nuvens volumétricas no céu.
        //  PC Médio: Forward+, HDR, MSAA 4x, 2 cascatas 110 m 2048 suaves médias, SSAO 8 amostras (normais, blur bilateral, resolução cheia), névoa de altura.
        //  Mobile  : Forward, LDR, MSAA 2x (barato em GPU de tile), escala 0,85, 2 cascatas 60 m 1024 suaves baixas, sem SSAO/névoa extra.
        //  Web     : Forward, LDR, sem MSAA (FXAA na câmera), 2 cascatas 70 m 1024 suaves baixas, sem SSAO/névoa extra.
        enum Prof { PCAlto, PCMedio, Mobile, Web }
        static UniversalRenderPipelineAsset Pipeline(string name, Prof p)
        {
            bool pc = p == Prof.PCAlto || p == Prof.PCMedio;
            var path = $"{Dir}/{name}.asset"; var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path); UniversalRendererData rd = null;
            if (a) rd = new SerializedObject(a).FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue as UniversalRendererData;
            if (!rd)
            {
                rd = ScriptableObject.CreateInstance<UniversalRendererData>(); rd.name = name + "_Renderer";
                AssetDatabase.CreateAsset(rd, $"{Dir}/{name}_Renderer.asset");
                rd.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            }
            if (!a) { a = UniversalRenderPipelineAsset.Create(rd); AssetDatabase.CreateAsset(a, path); }
            rd.renderingMode = pc ? RenderingMode.ForwardPlus : RenderingMode.Forward;
            if (!rd.postProcessData) rd.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            // renderer features: SSAO e névoa de altura só no PC (o FBGfx liga/desliga por nível em tempo de execução)
            var ssao = Feature<ScreenSpaceAmbientOcclusion>(rd, "SSAO", pc);
            if (ssao)
            {
                var fs = new SerializedObject(ssao);
                void F(string n, float v) { var x = fs.FindProperty("m_Settings." + n); if (x != null) x.floatValue = v; }
                void I(string n, int v) { var x = fs.FindProperty("m_Settings." + n); if (x == null) return; if (x.propertyType == SerializedPropertyType.Enum) x.enumValueIndex = v; else x.intValue = v; }
                void B(string n, bool v) { var x = fs.FindProperty("m_Settings." + n); if (x != null) x.boolValue = v; }
                bool hi = p == Prof.PCAlto;
                // Etapa 4 (ruído): no PC Médio (nível ALTO do FBQuality) eram 4 amostras + blur gaussiano + meia resolução + normais reconstruídas
                // da profundidade → granulação/pontos claros em toda superfície (pior perto da câmera). Agora: normais do pré-passo, 8/12 amostras,
                // blur bilateral (respeita bordas) e resolução cheia nos dois perfis de PC. Ruído azul (sem TAA o gradiente intercalado fica padronizado).
                I("AOMethod", 0); I("Source", 1); I("NormalSamples", hi ? 2 : 1); I("Samples", hi ? 0 : 1); I("BlurQuality", 0); B("Downsample", false); B("AfterOpaque", false);
                F("Intensity", hi ? .9f : .75f); F("Radius", .35f); F("Falloff", 110); F("DirectLightingStrength", .25f);
                fs.ApplyModifiedPropertiesWithoutUndo();
            }
            var fog = Feature<FBHeightFogFeature>(rd, "NevoaAltura", pc);
            if (fog)
            {
                fog.passMaterial = FogMaterial(); fog.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents;
                fog.fetchColorBuffer = true; fog.requirements = ScriptableRenderPassInput.Depth; fog.passIndex = 0; fog.bindDepthStencilAttachment = false;
                EditorUtility.SetDirty(fog);
            }
            // luz volumétrica (fachos dos postes/faróis na névoa, raios do sol entre árvores) — PC Alto e PC Médio; o FBGfx liga por nível
            // 20/10: o raymarch próprio (FBVolumetricFeature, sem sombra das luzes adicionais) deu lugar ao pacote URP Volumetric Fog de Cristian
            // Qiu (MIT, Packages/com.cqf.urpvolumetricfog): sombra do sol E dos postes/faróis, desfoque e upsample pela profundidade, Forward+.
            // Não roda em WebGL (o pacote não suporta) — lá e no celular continua desligado. Os shaders ficam gravados no asset (vão para o build).
            Feature<FBVolumetricFeature>(rd, "LuzVolumetrica", false);
            var cqf = Feature<VolumetricFogRendererFeature>(rd, "NevoaVolumetrica", pc);
            if (cqf)
            {
                var so2 = new SerializedObject(cqf);
                var a1 = so2.FindProperty("downsampleDepthShader"); if (a1 != null) a1.objectReferenceValue = Shader.Find("Hidden/DownsampleDepth");
                var a2 = so2.FindProperty("volumetricFogShader"); if (a2 != null) a2.objectReferenceValue = Shader.Find("Hidden/VolumetricFog");
                so2.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(cqf);
            }
            // decais URP (pedido do Agente 6: sangue/impactos nas paredes) — Screen Space, reconstrução de normal baixa (1 amostra), só no PC
            var dec = Feature<DecalRendererFeature>(rd, "Decais", pc);
            if (dec)
            {
                var ds = new SerializedObject(dec);
                var tq = ds.FindProperty("m_Settings.technique"); if (tq != null) tq.enumValueIndex = 2;                // ScreenSpace
                var dd = ds.FindProperty("m_Settings.maxDrawDistance"); if (dd != null) dd.floatValue = 120;
                var nb = ds.FindProperty("m_Settings.screenSpaceSettings.normalBlend"); if (nb != null) nb.enumValueIndex = 0;   // Low
                var dl = ds.FindProperty("m_Settings.decalLayers"); if (dl != null) dl.boolValue = false;
                ds.ApplyModifiedPropertiesWithoutUndo();
            }
            SyncFeatureMap(rd); EditorUtility.SetDirty(rd);

            a.supportsHDR = pc; a.msaaSampleCount = pc ? 4 : p == Prof.Web ? 1 : 2;   // PC Médio 2x → 4x (Etapa 4: serrilhado/cintilação em grades, fios e folhas; o web usa MSAA nativo 4x)
            a.renderScale = p == Prof.Mobile ? .85f : 1;
            a.shadowDistance = p switch { Prof.PCAlto => 150, Prof.PCMedio => 110, Prof.Mobile => 60, _ => 70 };
            a.shadowCascadeCount = p == Prof.PCAlto ? 4 : 2; a.useSRPBatcher = true;
            if (p == Prof.PCAlto) a.cascade4Split = new Vector3(.07f, .2f, .48f); else a.cascade2Split = .28f;
            a.supportsCameraDepthTexture = true; a.supportsCameraOpaqueTexture = false;
            a.volumeProfile = PostProfile(pc);
            var so = new SerializedObject(a);
            void SetI(string n, int v) { var x = so.FindProperty(n); if (x != null) x.intValue = v; }
            void SetB(string n, bool v) { var x = so.FindProperty(n); if (x != null) x.boolValue = v; }
            void SetF(string n, float v) { var x = so.FindProperty(n); if (x != null) x.floatValue = v; }
            SetI("m_MainLightShadowmapResolution", p switch { Prof.PCAlto => 4096, Prof.PCMedio => 2048, _ => 1024 });
            SetI("m_AdditionalLightsRenderingMode", 1);                                   // por pixel (pool fixo de luzes do jogo: tiros, postes, sirene)
            SetI("m_AdditionalLightsPerObjectLimit", pc ? 8 : 4);
            SetB("m_AdditionalLightShadowsSupported", pc);   // PC: postes mais próximos/faróis com sombra real (FBStreetLights); antes desligado — a sombra dos spots nunca aparecia SetB("m_SoftShadowsSupported", true); SetB("m_MainLightShadowsSupported", true);
            SetI("m_SoftShadowQuality", p == Prof.PCAlto ? 3 : p == Prof.PCMedio ? 2 : 1);  // alta / média / baixa
            SetI("m_AdditionalLightsShadowmapResolution", p == Prof.PCAlto ? 4096 : pc ? 2048 : 512);
            SetI("m_AdditionalLightsShadowResolutionTierLow", 256); SetI("m_AdditionalLightsShadowResolutionTierMedium", 512); SetI("m_AdditionalLightsShadowResolutionTierHigh", 1024);
            SetF("m_ShadowDepthBias", .6f); SetF("m_ShadowNormalBias", .5f);
            SetI("m_ColorGradingMode", pc ? 1 : 0);                                      // HDR no PC, LDR no mobile/web
            SetI("m_ColorGradingLutSize", pc ? 32 : 16);
            SetB("m_SupportsLightCookies", false);
            SetB("m_ReflectionProbeBlending", pc); SetB("m_ReflectionProbeBoxProjection", false);
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(a);
            return a;
        }

        // SSR desligado durante a importação/bake (as câmeras do editor — sonda de reflexo, ícones — não têm textura de normais e o SSR do
        // URP 17.6 lança NullReference); devolvido ligado no fim
        public static System.Collections.Generic.List<ScriptableRendererFeature> SuspendSSR()
        {
            var off = new System.Collections.Generic.List<ScriptableRendererFeature>();
            return off;
        }
        public static void ResumeSSR(System.Collections.Generic.List<ScriptableRendererFeature> off)
        {
            foreach (var f in off) if (f) { f.SetActive(true); EditorUtility.SetDirty(f); }
            if (off.Count > 0) AssetDatabase.SaveAssets();
        }
        static T Feature<T>(UniversalRendererData rd, string name, bool want) where T : ScriptableRendererFeature
        {
            var f = rd.rendererFeatures.FirstOrDefault(x => x && x.GetType() == typeof(T)) as T;
            if (!want) { if (f) { rd.rendererFeatures.Remove(f); AssetDatabase.RemoveObjectFromAsset(f); Object.DestroyImmediate(f, true); } return null; }
            if (!f) { f = ScriptableObject.CreateInstance<T>(); f.name = name; AssetDatabase.AddObjectToAsset(f, rd); rd.rendererFeatures.Add(f); }
            f.SetActive(true); return f;
        }
        static void SyncFeatureMap(ScriptableRendererData rd)
        {
            var so = new SerializedObject(rd); var map = so.FindProperty("m_RendererFeatureMap"); var list = so.FindProperty("m_RendererFeatures"); if (map == null || list == null) return;
            map.arraySize = list.arraySize;
            for (int i = 0; i < list.arraySize; i++) { var o = list.GetArrayElementAtIndex(i).objectReferenceValue; map.GetArrayElementAtIndex(i).longValue = o && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out _, out long id) ? id : 0; }
            so.ApplyModifiedPropertiesWithoutUndo(); rd.SetDirty();
        }
        static Material VolMaterial()
        {
            var path = $"{Dir}/FB_LuzVolumetrica.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path); var sh = Shader.Find("FreeBala/Volumetric"); if (!sh) return m;
            if (!m) { m = new Material(sh) { name = "FB_LuzVolumetrica" }; AssetDatabase.CreateAsset(m, path); } else m.shader = sh;
            return m;
        }
        static Material FogMaterial()
        {
            var path = $"{Dir}/FB_NevoaAltura.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path); var sh = Shader.Find("FreeBala/HeightFog"); if (!sh) return m;
            if (!m) { m = new Material(sh) { name = "FB_NevoaAltura" }; AssetDatabase.CreateAsset(m, path); } else m.shader = sh;
            return m;
        }

        // ---- pós-processamento por perfil (Volume padrão do pipeline: vale em todas as cenas, sem objeto Volume)
        // Feedback antigo: um "grade" global que ESCURECEU tudo foi rejeitado -> aqui nada escurece: o tonemapping Neutral (só no PC/HDR)
        // vem com exposição compensada (+0,25), contraste e saturação sobem pouco, balanço de branco levemente quente (sol de Roraima),
        // bloom só no que passa do branco (sol, lua, lâmpadas, emissivos), vinheta leve.
        static VolumeProfile PostProfile(bool pc)
        {
            var path = $"{Dir}/FB_Pos_{(pc ? "PC" : "Mobile")}.asset"; var vp = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (!vp) { vp = ScriptableObject.CreateInstance<VolumeProfile>(); vp.name = Path.GetFileNameWithoutExtension(path); AssetDatabase.CreateAsset(vp, path); }
            T C<T>() where T : VolumeComponent
            {
                if (!vp.TryGet<T>(out var c)) { c = vp.Add<T>(true); c.name = typeof(T).Name; AssetDatabase.AddObjectToAsset(c, vp); }
                c.active = true; return c;
            }
            var tm = C<Tonemapping>(); tm.mode.Override(pc ? TonemappingMode.Neutral : TonemappingMode.None);
            var ca = C<ColorAdjustments>(); ca.postExposure.Override(pc ? .25f : 0); ca.contrast.Override(pc ? 7 : 5); ca.saturation.Override(pc ? 9 : 8); ca.colorFilter.Override(Color.white); ca.hueShift.Override(0);
            var wb = C<WhiteBalance>(); wb.temperature.Override(4); wb.tint.Override(0);
            var bl = C<Bloom>(); bl.threshold.Override(pc ? 1.05f : .92f); bl.intensity.Override(pc ? .35f : .22f); bl.scatter.Override(.62f); bl.highQualityFiltering.Override(false);
            bl.downscale.Override(pc ? BloomDownscaleMode.Half : BloomDownscaleMode.Quarter); bl.maxIterations.Override(pc ? 6 : 4); bl.tint.Override(Color.white);
            var vg = C<Vignette>(); vg.intensity.Override(pc ? .18f : .15f); vg.smoothness.Override(.42f); vg.color.Override(Color.black); vg.rounded.Override(false);
            EditorUtility.SetDirty(vp); foreach (var c in vp.components) EditorUtility.SetDirty(c);
            return vp;
        }

        static void Quality(RenderPipelineAsset pc, RenderPipelineAsset med, RenderPipelineAsset mob, RenderPipelineAsset web)
        {
            var qs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]; var so = new SerializedObject(qs);
            var levels = so.FindProperty("m_QualitySettings");
            levels.arraySize = 4;                                                        // índices 0–2 mantidos; "Web" entra no fim (FBGfx/FBSession usam o nome/índice)
            string[] names = { "Mobile", "PC Médio", "PC Alto", "Web" }; RenderPipelineAsset[] rp = { mob, med, pc, web };
            for (int i = 0; i < 4; i++)
            {
                var L = levels.GetArrayElementAtIndex(i);
                L.FindPropertyRelative("name").stringValue = names[i];
                L.FindPropertyRelative("customRenderPipeline").objectReferenceValue = rp[i];
                L.FindPropertyRelative("vSyncCount").intValue = 0;
                L.FindPropertyRelative("antiAliasing").intValue = 0;
                L.FindPropertyRelative("anisotropicTextures").intValue = i == 0 ? 1 : 2;
                L.FindPropertyRelative("lodBias").floatValue = i == 0 ? .8f : i == 1 ? 1.2f : i == 2 ? 2 : 1;
                L.FindPropertyRelative("skinWeights").intValue = i == 0 || i == 3 ? 2 : 4;
                L.FindPropertyRelative("globalTextureMipmapLimit").intValue = i == 0 ? 1 : 0;
                var sp = L.FindPropertyRelative("softParticles"); if (sp != null) sp.boolValue = i == 2;
                var rp2 = L.FindPropertyRelative("realtimeReflectionProbes"); if (rp2 != null) rp2.boolValue = false;
            }
            so.FindProperty("m_CurrentQuality").intValue = 2;
            var per = so.FindProperty("m_PerPlatformDefaultQuality");
            if (per != null)
                for (int i = 0; i < per.arraySize; i++)
                {
                    var e = per.GetArrayElementAtIndex(i); var k = e.FindPropertyRelative("first").stringValue;
                    e.FindPropertyRelative("second").intValue = k == "Android" || k == "iPhone" ? 0 : k == "WebGL" ? 3 : 2;
                }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Player()
        {
            PlayerSettings.companyName = "FreeBala"; PlayerSettings.productName = "BURITIRO";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.buritiro.jogo");   // [mobile 09/11] nome definitivo na Play Store (não dá para trocar depois de publicar)
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.buritiro.jogo");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.freebala.game");
            PlayerSettings.bundleVersion = "2.0.0"; PlayerSettings.Android.bundleVersionCode = 1; PlayerSettings.iOS.buildNumber = "1";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false; PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true; PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow; PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900; PlayerSettings.resizableWindow = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip; PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.initialMemorySize = 512; PlayerSettings.WebGL.maximumMemorySize = 4096; PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric; PlayerSettings.WebGL.dataCaching = true;
            // recursos experimentais do URP 17.6 só no PC: reflexo em espaço de tela (o pacote compila a feature com este define)
            { var defs = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone); if (!defs.Split(';').Contains("URP_SCREEN_SPACE_REFLECTION")) PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, string.IsNullOrEmpty(defs) ? "URP_SCREEN_SPACE_REFLECTION" : defs + ";URP_SCREEN_SPACE_REFLECTION"); }
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Minimal);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Minimal);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Minimal);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Minimal);
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;         // servidor local ws:// / http:// (server.mjs)
            PlayerSettings.Android.forceInternetPermission = true;
            // entrada: Input System novo + o antigo (Input.GetKey nos atalhos do jogo)
            var ps = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"); var so = new SerializedObject(ps);
            var ih = so.FindProperty("activeInputHandler"); if (ih != null) ih.intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Layers()
        {
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            void L(int i, string n) { var p = layers.GetArrayElementAtIndex(i); if (string.IsNullOrEmpty(p.stringValue) || p.stringValue == n) p.stringValue = n; }
            L(LMap, "Mapa"); L(LHitbox, "Hitbox"); L(LChar, "Personagem"); L(LVehicle, "Veiculo"); L(LGlass, "Vidro"); L(LTrigger, "Gatilho"); L(14, "PrimeiraPessoa");
            var tags = tm.FindProperty("tags"); foreach (var t in new[] { "Arma", "Assento", "Spawn" })
            { bool has = false; for (int i = 0; i < tags.arraySize; i++) if (tags.GetArrayElementAtIndex(i).stringValue == t) has = true; if (!has) { tags.InsertArrayElementAtIndex(tags.arraySize); tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = t; } }
            tm.ApplyModifiedPropertiesWithoutUndo();
            // hitboxes não empurram ninguém (são gatilhos de tiro); personagens colidem com mapa/veículos
            Physics.IgnoreLayerCollision(LHitbox, LHitbox); Physics.IgnoreLayerCollision(LHitbox, LChar); Physics.IgnoreLayerCollision(LHitbox, LMap);
            Physics.IgnoreLayerCollision(LHitbox, LVehicle); Physics.IgnoreLayerCollision(LHitbox, 0); Physics.IgnoreLayerCollision(LTrigger, LTrigger);
        }
    }
}
