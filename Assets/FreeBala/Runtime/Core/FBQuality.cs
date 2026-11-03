// Perfis de qualidade do jogo (Agente 9): Baixo / Médio / Alto / Ultra, com detecção automática por plataforma, GPU, memória e núcleos.
// Os outros sistemas só LEEM as flags (FBQuality.Shadows, FBQuality.MaxParticles…) e assinam FBQuality.Changed para reaplicar.
// Aplica sozinho o que é do Unity/URP (LOD bias, sombra, escala de render, pesos de skin, mip, FPS alvo) e, a cada cena carregada,
// tira a sombra de props pequenos (sombra = maior custo medido na Vila Jardim: até 6 mil shadow casters).
// Trocar o nível liga/desliga keywords de sombra/pós no URP: faça isso no menu ou no carregamento, nunca no meio da partida
// (compilação de shader = travada). Escolha salva em PlayerPrefs "fb_qlevel" (-1 = automático). Tabela das flags: unity/equipe/perf.md.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public enum FBQLevel { Baixo = 0, Medio = 1, Alto = 2, Ultra = 3 }
    public enum FBPlat { PC, Android, iOS, WebGL }

    // Dados do aparelho usados na detecção (separado para dar para testar sem hardware: FBQuality.Detect(info))
    public struct FBDeviceInfo
    {
        public FBPlat plat; public bool mobileBrowser; public int memMB, gpuMB, cores, shaderLevel; public string gpu; public GraphicsDeviceType api;
        public static FBDeviceInfo Current()
        {
            var p = Application.platform;
            var plat = p == RuntimePlatform.Android ? FBPlat.Android : p == RuntimePlatform.IPhonePlayer ? FBPlat.iOS : p == RuntimePlatform.WebGLPlayer ? FBPlat.WebGL : FBPlat.PC;
#if UNITY_EDITOR
            // no editor simula a plataforma alvo (Android/iOS/WebGL) para os testes verem o perfil certo
            var t = UnityEditor.EditorUserBuildSettings.activeBuildTarget;
            if (t == UnityEditor.BuildTarget.Android) plat = FBPlat.Android; else if (t == UnityEditor.BuildTarget.iOS) plat = FBPlat.iOS; else if (t == UnityEditor.BuildTarget.WebGL) plat = FBPlat.WebGL;
#endif
            return new FBDeviceInfo { plat = plat, mobileBrowser = plat == FBPlat.WebGL && Application.isMobilePlatform, memMB = SystemInfo.systemMemorySize, gpuMB = SystemInfo.graphicsMemorySize, cores = SystemInfo.processorCount, shaderLevel = SystemInfo.graphicsShaderLevel, gpu = SystemInfo.graphicsDeviceName ?? "", api = SystemInfo.graphicsDeviceType };
        }
        public bool Mobile => plat == FBPlat.Android || plat == FBPlat.iOS || mobileBrowser;
    }

    // Todas as flags de um nível (valores da tabela do perf.md)
    [Serializable]
    public class FBQProfile
    {
        public FBQLevel level; public FBPlat plat; public bool mobile;
        // sombras
        public bool shadows; public float shadowDistance; public int shadowRes, shadowCascades; public bool softShadows; public float smallCasterSize;
        // pós e céu (Agente 5)
        public bool postFX, bloom, ssao, volumetricClouds, realtimeReflections, heightFog, detailMaps; public int maxPixelLights; public float vegetationDensity, ambientParticles;
        // áudio (Agente 7)
        public int maxVoices;
        // efeitos (Agente 6)
        public bool decals; public int maxDecals, maxParticles; public float particleScale, rainDensity, impactChance; public bool hdOnlyLocal;
        // personagens (Agentes 2 e 3)
        public float footIKDistance; public int footIKInterval; public float animCullDistance; public int skinWeights;
        // cena/render
        public float lodBias; public int maxLodLevel, mipLimit, targetFps, texBudgetMB; public float renderScale, minRenderScale, drawDistance; public bool dynamicResolution;
        // mundo vivo (coordenador: trânsito/NPCs do rolê)
        public int maxTraffic, maxNpcs;
        // vegetação instanciada (Agente 5: tufos/arbustos nas áreas verdes) — orçamento por nível
        public float vegDrawDistance, vegLodDistance; public int vegMaxInstances; public bool vegShadows, vegCrossFade, vegGpuCulling;
    }

    public static class FBQuality
    {
        public static readonly string[] Names = { "BAIXO", "MÉDIO", "ALTO", "ULTRA" };
        public static FBQProfile P { get; private set; } = Build(FBQLevel.Alto, new FBDeviceInfo { plat = FBPlat.PC });
        public static FBQLevel Level => P.level;
        public static bool Auto { get; private set; } = true;
        public static FBQLevel Detected { get; private set; } = FBQLevel.Alto;
        public static FBDeviceInfo Device { get; private set; }
        public static event Action Changed;   // disparado depois de aplicar um nível novo

        // IK dos pés ligado/desligado (chave geral; os testes desligam). Por nível: FootIKDistance/FootIKInterval.
        public static bool FootIK = true;

        // atalhos de leitura (os outros agentes usam estes)
        public static bool Shadows => P.shadows; public static float ShadowDistance => P.shadowDistance; public static float SmallCasterSize => P.smallCasterSize;
        public static bool PostFX => P.postFX; public static bool Bloom => P.bloom; public static bool SSAO => P.ssao; public static bool VolumetricClouds => P.volumetricClouds;
        public static bool RealtimeReflections => P.realtimeReflections; public static bool HeightFog => P.heightFog; public static bool DetailMaps => P.detailMaps; public static float AmbientParticles => P.ambientParticles; public static int MaxVoices => P.maxVoices; public static int MaxPixelLights => P.maxPixelLights; public static float VegetationDensity => P.vegetationDensity;
        public static bool Decals => P.decals; public static int MaxDecals => P.maxDecals; public static int MaxParticles => P.maxParticles; public static float ParticleScale => P.particleScale; public static float ImpactChance => P.impactChance; public static bool HdOnlyLocal => P.hdOnlyLocal; public static float RainDensity => P.rainDensity;
        public static float FootIKDistance => P.footIKDistance; public static int FootIKInterval => P.footIKInterval; public static float AnimCullDistance => P.animCullDistance;
        public static float LodBias => P.lodBias; public static float RenderScale => P.renderScale; public static float DrawDistance => P.drawDistance; public static int MaxTraffic => P.maxTraffic; public static int MaxNpcs => P.maxNpcs;
        public static bool Mobile => P.mobile;
        public static float VegDrawDistance => P.vegDrawDistance; public static float VegLodDistance => P.vegLodDistance; public static int VegMaxInstances => P.vegMaxInstances;
        public static bool VegShadows => P.vegShadows; public static bool VegCrossFade => P.vegCrossFade; public static bool VegGpuCulling => P.vegGpuCulling;
        // contadores que o sistema de vegetação ESCREVE a cada quadro (o autoteste/benchmark leem): instâncias desenhadas e total carregado
        public static int VegVisibleInstances, VegTotalInstances;

        // IK dos pés deste personagem neste quadro? (distância da câmera + intervalo de quadros escalonado pelo id, para não pesar tudo no mesmo quadro)
        public static bool FootIKFor(Vector3 pos, bool isPlayer, int id)
        {
            if (!FootIK) return false; if (isPlayer) return true;
            var cam = Camera.main; if (cam && (cam.transform.position - pos).sqrMagnitude > P.footIKDistance * P.footIKDistance) return false;
            return P.footIKInterval <= 1 || (Time.frameCount + (id & 0xff)) % P.footIKInterval == 0;
        }

        // ---------- detecção ----------
        static bool Has(string s, params string[] k) { foreach (var x in k) if (s.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0) return true; return false; }
        public static FBQLevel Detect(FBDeviceInfo d)
        {
            var g = d.gpu ?? "";
            switch (d.plat)
            {
                case FBPlat.Android:
                    {
                        // Adreno 6xx+/7xx, Mali-G7x/G6xx/Immortalis, Xclipse: topo; Mali-T, Mali-G5x/G3x, Adreno 3xx–5xx, PowerVR: fracos
                        bool weak = Has(g, "Mali-T", "Mali-G3", "Mali-G5", "Mali-400", "Mali-450", "PowerVR", "Adreno (TM) 3", "Adreno (TM) 4", "Adreno (TM) 5");
                        bool strong = Has(g, "Adreno (TM) 7", "Adreno (TM) 8", "Adreno (TM) 66", "Adreno (TM) 69", "Immortalis", "Mali-G71", "Mali-G72", "Mali-G76", "Mali-G77", "Mali-G78", "Mali-G710", "Mali-G715", "Mali-G720", "Mali-G610", "Mali-G615", "Xclipse");
                        if (weak || d.memMB < 3500 || d.cores < 6 || d.shaderLevel < 35) return FBQLevel.Baixo;
                        if (strong && d.memMB >= 7000 && d.cores >= 8) return FBQLevel.Alto;
                        return FBQLevel.Medio;
                    }
                case FBPlat.iOS:   // memória separa bem as gerações (A12/3 GB, A14/4–6 GB, A15+ 6–8 GB)
                    return d.memMB >= 5500 ? FBQLevel.Alto : d.memMB >= 2900 ? FBQLevel.Medio : FBQLevel.Baixo;
                case FBPlat.WebGL:
                    {
                        // navegador: memória/GPU quase sempre "0/desconhecido"; o nome da GPU vem mascarado em alguns navegadores
                        if (d.mobileBrowser) return FBQLevel.Baixo;
                        if (Has(g, "RTX", "RX 6", "RX 7", "RX 9", "Apple M", "Arc A7")) return FBQLevel.Alto;
                        if (Has(g, "Intel", "SwiftShader", "llvmpipe", "Basic Render", "Mali", "Adreno")) return FBQLevel.Baixo;
                        return FBQLevel.Medio;
                    }
                default:
                    {
                        bool soft = Has(g, "Basic Render", "SwiftShader", "llvmpipe", "Microsoft Basic");
                        bool integrated = Has(g, "Intel(R) HD", "Intel(R) UHD", "Intel HD", "Intel UHD", "Iris", "Radeon(TM) Graphics", "Radeon Graphics", "Radeon(TM) Vega", "Vega 8", "Vega 6", "Vega 3",
                            "Radeon 6", "Radeon 7", "Radeon 8", "Radeon(TM) 6", "Radeon(TM) 7", "Radeon(TM) 8");   // APUs 660M/680M/760M/780M/880M (as discretas são "Radeon RX …")
                        if (soft || d.memMB < 4000) return FBQLevel.Baixo;
                        if (integrated) return d.memMB >= 12000 && Has(g, "Xe Graphics", "Iris Xe", "Iris(R) Xe", "680M", "780M", "880M", "890M", "Arc") ? FBQLevel.Medio : FBQLevel.Baixo;
                        if (d.gpuMB >= 7500 && d.memMB >= 15000 && d.cores >= 8) return FBQLevel.Ultra;
                        if (d.gpuMB >= 3500 && d.memMB >= 7500) return FBQLevel.Alto;
                        return d.gpuMB >= 1800 ? FBQLevel.Medio : FBQLevel.Baixo;
                    }
            }
        }

        // ---------- tabela de perfis ----------
        public static FBQProfile Build(FBQLevel L, FBDeviceInfo d)
        {
            int i = (int)L; bool mob = d.Mobile, web = d.plat == FBPlat.WebGL;
            T V<T>(T b, T m, T a, T u) => i == 0 ? b : i == 1 ? m : i == 2 ? a : u;
            var p = new FBQProfile { level = L, plat = d.plat, mobile = mob };
            p.shadows = i >= 1 || !mob;                                  // PC baixo ainda tem sombra curta (o chão sem sombra parece flutuar)
            p.shadowDistance = mob ? V(0f, 25, 40, 55) : V(32f, 60, 85, 120);
            p.shadowRes = mob ? V(512, 1024, 1024, 2048) : V(1024, 2048, 2048, 4096);
            p.shadowCascades = mob || web ? 1 : V(1, 2, 3, 4);
            p.softShadows = i >= 2;
            p.smallCasterSize = V(2.5f, 1.4f, .7f, .3f);                // meia-diagonal (m) abaixo da qual o renderer não projeta sombra
            p.postFX = i >= 1 || !mob; p.bloom = i >= (mob ? 2 : 1); p.ssao = !mob && !web && i >= 2; p.volumetricClouds = mob ? i >= 3 : i >= 2;
            p.realtimeReflections = !mob && i >= 3; p.maxPixelLights = mob ? V(1, 2, 4, 4) : V(2, 4, 8, 8);
            p.vegetationDensity = mob ? V(.15f, .35f, .6f, .8f) : web ? V(.25f, .45f, .7f, .85f) : V(.3f, .6f, .85f, 1f);   // fração das instâncias geradas
            p.vegDrawDistance = mob ? V(25f, 40, 60, 80) : web ? V(30f, 50, 75, 100) : V(40f, 70, 110, 160);   // além disso não desenha (m)
            p.vegLodDistance = mob ? V(8f, 14, 22, 30) : V(14f, 22, 35, 55);                                 // a partir daqui usa o LOD baixo/cruzado (m)
            p.vegMaxInstances = mob ? V(2000, 5000, 10000, 16000) : web ? V(4000, 8000, 16000, 26000) : V(8000, 20000, 45000, 90000);   // teto de instâncias visíveis por quadro
            p.vegShadows = !mob && !web && i >= 2;                                                           // sombra da vegetação só no PC ALTO/ULTRA
            p.vegCrossFade = !mob && i >= 2;                                                                 // dither entre LODs
            p.vegGpuCulling = !web && i >= 1 && SystemInfo.supportsComputeShaders;                           // culling por compute (indireto); senão CPU por célula
            p.heightFog = mob || web ? i >= 2 : i >= 1; p.detailMaps = mob ? i >= 2 : i >= 1;   // névoa de altura e detail maps do Agente 5
            p.ambientParticles = mob ? V(.25f, .5f, .8f, 1f) : V(.4f, .7f, 1f, 1.3f);           // multiplicador dos limites do FBAmbientFx (insetos, poeira, fumaça)
            p.maxVoices = mob ? V(3, 4, 5, 6) : V(4, 6, 8, 10);                                // vozes simultâneas do FBVoiceDirector
            p.decals = i >= 1; p.maxDecals = V(0, 32, 96, 256); p.maxParticles = mob ? V(120, 300, 600, 1000) : V(200, 500, 1200, 2500);
            p.particleScale = V(.4f, .7f, 1f, 1f); p.impactChance = V(.3f, .65f, 1f, 1f);   // CF ImpactFX: chance de spawn por qualidade
            p.hdOnlyLocal = i <= 2;                                       // FF: só o jogador local com textura/malha HD (bots/remotos na versão leve)
            p.rainDensity = V(.3f, .6f, 1f, 1f);
            p.footIKDistance = V(8f, 18, 35, 60);   // IE: IK de pés até 18 m (baixo) / 35 m (alto)
            p.footIKInterval = V(3, 2, 1, 1); p.animCullDistance = V(35f, 60, 110, 200); p.skinWeights = mob ? V(1, 2, 2, 4) : V(2, 2, 4, 4);   // (estavam dentro do comentário acima: distância 0 = todo bot/remoto era 'longe')
            p.lodBias = mob ? V(.6f, .8f, 1.2f, 1.8f) : V(.8f, 1.2f, 1.8f, 2.4f);   // matriz do Free Fire: 0,8 / 1,2 / 1,8
            p.maxLodLevel = i == 0 && mob ? 1 : 0;
            p.mipLimit = mob ? V(1, 1, 0, 0) : V(1, 0, 0, 0);
            p.texBudgetMB = mob || web ? V(256, 384, 512, 768) : Mathf.Clamp(Mathf.Max(V(768, 1536, 2560, 3584), d.gpuMB * 6 / 10), 512, Mathf.Max(768, d.gpuMB * 8 / 10));   // mip streaming (FBOptimize liga streamingMipmaps nas texturas 3D)
            p.renderScale = d.plat == FBPlat.Android ? V(.6f, .75f, .85f, 1f) : d.plat == FBPlat.iOS ? V(.65f, .8f, .9f, 1f) : web ? V(.7f, .85f, 1f, 1f) : V(.85f, 1f, 1f, 1f);
            p.minRenderScale = Mathf.Max(.5f, p.renderScale - .2f); p.dynamicResolution = mob || web;
            p.targetFps = mob ? V(30, 45, 60, 60) : -1;
            p.drawDistance = V(250f, 400, 700, 1200);
            p.maxTraffic = V(3, 6, 12, 18); p.maxNpcs = V(4, 10, 18, 28);
            return p;
        }

        // ---------- aplicar ----------
        static bool booted, applying; static Snapshot snap;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (booted) return; booted = true;
            Device = FBDeviceInfo.Current(); Detected = Detect(Device);
            var args = System.Environment.GetCommandLineArgs(); int pi = System.Array.IndexOf(args, "-fbplat");   // -fbplat android|ios|webgl: simula o perfil no PC
            if (pi >= 0 && pi + 1 < args.Length && System.Enum.TryParse<FBPlat>(args[pi + 1], true, out var sim)) { var d = Device; d.plat = sim; Device = d; Detected = Detect(Device); Simulated = true; }
            int saved = PlayerPrefs.GetInt("fb_qlevel", -1);
            if (saved < 0) { int old = PlayerPrefs.GetInt("fb_quality", -1); if (old >= 0) saved = Mathf.Clamp(old, 0, 2); }   // escolha antiga (3 níveis do Unity)
            Auto = saved < 0; Apply(Auto ? Detected : (FBQLevel)Mathf.Clamp(saved, 0, 3));
            SceneManager.sceneLoaded += (s, m) => OnScene(s);
            QualitySettings.activeQualityLevelChanged += (a, b) => { if (!applying) Apply(P.level); };   // menu antigo mudou o nível do Unity: reaplica o nosso por cima
            Debug.Log($"[FreeBala][qualidade] {Device.plat} · GPU {Device.gpu} {Device.gpuMB} MB · RAM {Device.memMB} MB · {Device.cores} núcleos · detectado {Names[(int)Detected]} → usando {Names[(int)Level]}{(Auto ? " (auto)" : "")}");
#if UNITY_EDITOR
            Application.quitting += () => snap?.Restore();                   // no editor o asset do URP é arquivo do projeto: devolve os valores originais
#endif
        }

        // Escolha do jogador: -1 = automático, 0..3 = nível fixo. Salva e aplica.
        public static void Set(int level)
        {
            PlayerPrefs.SetInt("fb_qlevel", level); PlayerPrefs.Save();
            Auto = level < 0; Apply(Auto ? Detected : (FBQLevel)Mathf.Clamp(level, 0, 3));
        }
        public static int Choice => Auto ? -1 : (int)Level;
        // Perfil de outra plataforma no aparelho atual (benchmark "Android BAIXO no PC"): troca só a tabela de flags e o asset URP (Mobile),
        // não as texturas (ASTC só existe no build Android). null = volta à plataforma real.
        public static bool Simulated { get; private set; }
        public static void SimulatePlatform(FBPlat? plat)
        {
            if (!booted) Boot();
            var d = FBDeviceInfo.Current(); if (plat.HasValue) d.plat = plat.Value; Device = d; Simulated = plat.HasValue; Detected = Detect(Device);
            Apply(Auto ? Detected : P.level);
        }

        public static void Apply(FBQLevel L)
        {
            if (!booted) { booted = true; Device = FBDeviceInfo.Current(); Detected = Detect(Device); }
            P = Build(L, Device); applying = true;
            try
            {
                // nível do Unity que carrega o asset URP certo (Mobile / PC Médio / PC Alto do FBProjectSetup)
                int n = QualitySettings.names.Length, web = System.Array.IndexOf(QualitySettings.names, "Web"), q = Device.plat == FBPlat.WebGL && web >= 0 ? web : P.mobile ? 0 : Mathf.Clamp((int)L - 1, 0, 2);
                if (n > 0 && QualitySettings.GetQualityLevel() != Mathf.Min(q, n - 1)) QualitySettings.SetQualityLevel(Mathf.Min(q, n - 1), true);
                QualitySettings.vSyncCount = 0; QualitySettings.lodBias = P.lodBias; QualitySettings.maximumLODLevel = P.maxLodLevel;
                // r43: LOD em 3 níveis (Mesh LOD, Editor/FBLod3): quanto MAIOR o limiar, mais cedo cai para o nível médio/baixo. Inverso do
                // lodBias do tier (o mesmo critério do Free Fire: celular troca antes, PC alto guarda o nível cheio até mais longe)
                QualitySettings.meshLodThreshold = Mathf.Clamp(1.2f / Mathf.Max(.3f, P.lodBias), .45f, 2.2f);
                QualitySettings.skinWeights = P.skinWeights switch { 1 => SkinWeights.OneBone, 2 => SkinWeights.TwoBones, _ => SkinWeights.FourBones };
                QualitySettings.globalTextureMipmapLimit = P.mipLimit; QualitySettings.anisotropicFiltering = (int)L == 0 ? AnisotropicFiltering.Disable : (int)L == 1 ? AnisotropicFiltering.Enable : AnisotropicFiltering.ForceEnable;
                QualitySettings.realtimeReflectionProbes = P.realtimeReflections; QualitySettings.particleRaycastBudget = P.mobile ? 64 : 256;
                QualitySettings.asyncUploadTimeSlice = P.mobile ? 4 : 8; QualitySettings.asyncUploadBufferSize = P.mobile ? 32 : 64; QualitySettings.asyncUploadPersistentBuffer = !P.mobile;
                Application.targetFrameRate = P.targetFps;
                QualitySettings.streamingMipmapsActive = true; QualitySettings.streamingMipmapsAddAllCameras = true; QualitySettings.streamingMipmapsMemoryBudget = P.texBudgetMB;
                QualitySettings.streamingMipmapsRenderersPerFrame = P.mobile ? 512 : 2048; QualitySettings.streamingMipmapsMaxLevelReduction = P.mobile ? 3 : 2; QualitySettings.streamingMipmapsMaxFileIORequests = P.mobile ? 256 : 1024;
                if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset a)
                {
                    snap ??= new Snapshot(a);
                    a.renderScale = P.renderScale; a.shadowDistance = P.shadows ? P.shadowDistance : 0; a.shadowCascadeCount = P.shadowCascades;
                    a.mainLightShadowmapResolution = P.shadowRes; a.maxAdditionalLightsCount = P.maxPixelLights;
                    a.cascadeBorder = .3f;                                                         // a sombra some esmaecendo nos últimos 30 % (sem linha)
                }
                FBDynRes.Setup();
            }
            finally { applying = false; }
            var sc = SceneManager.GetActiveScene(); if (sc.isLoaded) OnScene(sc);
            Changed?.Invoke();
        }

        // por cena: câmera principal sem sombra no Baixo-mobile; props pequenos sem sombra (guarda o modo original para voltar se o nível subir)
        static readonly List<(Renderer r, ShadowCastingMode m)> casters = new();
        public static int LastCulledCasters { get; private set; }
        static void OnScene(Scene s)
        {
            var cam = Camera.main; if (cam && cam.TryGetComponent<UniversalAdditionalCameraData>(out var cd)) cd.renderShadows = P.shadows;
            if (cam) cam.useOcclusionCulling = false;   // oclusão Umbra cortava estruturas à vista (Praça das Águas e Vila Jardim — FBRenderAudit, rodada 40)
            foreach (var (r, m) in casters) if (r) r.shadowCastingMode = m; casters.Clear();
            LastCulledCasters = PruneCasters(s, P.smallCasterSize, casters);
            SmoothLods(s);
            AjustaLodArena();
        }

        // [parceiro] v18 (thread de física, arenas v4): as células das arenas 10x10 têm um LODGroup por célula com distâncias
        // calculadas para lodBias 1 (LOD0 até 90 m, LOD1 até 200 m, corte logo depois da neblina). Com o bias do Alto e do
        // Ultra (1,8 e 2,4) o Unity estica tudo: a célula fica no LOD0 até ~220 m, o corte vai a ~1,2 km e o ganho de
        // draw calls some. Nas cenas de arena (objeto raiz "Pontos" com o filho "arena_0", que o BuritiroMontaMapa v4 cria)
        // o bias fica em no máximo 1; nos outros mapas continua o do nível de qualidade. v18c: a cena Map_vila_jardim também.
        public static bool ArenaLod { get; private set; }
        // [parceiro] arena: 1 no Alto/Ultra e nunca abaixo de 0,85 (com 0,6 do Baixo do celular as células sumiam ainda dentro da neblina)
        public static float LodBiasAtual => ArenaLod ? Mathf.Clamp(P.lodBias, .85f, 1f) : P.lodBias;
        static void AjustaLodArena()
        {
            bool arena = false;
            for (int i = 0; i < SceneManager.sceneCount && !arena; i++)
            {
                var sc = SceneManager.GetSceneAt(i); if (!sc.isLoaded) continue;
                if (sc.name == "Map_vila_jardim") { arena = true; break; }
                foreach (var root in sc.GetRootGameObjects())
                {
                    if (root.name == "Pontos" && root.transform.Find("arena_0") != null) { arena = true; break; }
                    // [parceiro] v18c (thread da Vila Jardim): a Vila também fica com o bias em no máximo 1. Os móveis de dentro das
                    // casas ganharam um LODGroup de 2 níveis (o segundo vazio) e com 2,4 eles continuavam desenhados longe.
                    if (root.name == "Map_vila_jardim") { arena = true; break; }
                }
            }
            if (arena != ArenaLod)
                Debug.Log(arena ? $"[FreeBala][qualidade] arena 10x10 ou Vila Jardim: lodBias {Mathf.Clamp(P.lodBias, .85f, 1f):0.0#} (o nível usa {P.lodBias:0.0#})"
                                : $"[FreeBala][qualidade] fora da arena: lodBias {P.lodBias:0.0#}");
            ArenaLod = arena;
            // [parceiro] o limiar do Mesh LOD (r43) segue o bias efetivo, como no Apply
            float bias = LodBiasAtual;
            QualitySettings.lodBias = bias;
            QualitySettings.meshLodThreshold = Mathf.Clamp(1.2f / Mathf.Max(.3f, bias), .45f, 2.2f);
        }
        // POP-IN (retorno do usuário 20/10: "objetos, texturas, luzes e sombras surgindo na frente do jogador"): os LODGroups de corte do
        // FBOptimize (prop pequeno some abaixo de 0,5–1,2 % da tela) e os dos carros eram LODFadeMode.None = aparecem/somem num quadro.
        // Agora: transição pontilhada animada (0,5 s) em todos; no PC o limiar de tela dos grupos de corte cai (×0,7 Médio, ×0,55 Alto/Ultra):
        // o prop só some bem mais longe. O valor original fica guardado (trocar de nível reaplica do original).
        static readonly Dictionary<LODGroup, float> lodOrig = new();
        public static float LodCutK => P.mobile ? 1f : P.level >= FBQLevel.Alto ? .55f : P.level == FBQLevel.Medio ? .7f : .9f;
        public static void SmoothLods(Scene s)
        {
            if (!s.IsValid() || !s.isLoaded) return; float k = LodCutK; LODGroup.crossFadeAnimationDuration = .5f;
            bool pa = s.name.Contains("praca_aguas");
            foreach (var root in s.GetRootGameObjects())
            {
                foreach (var lg in root.GetComponentsInChildren<LODGroup>(true))
                {
                    if (lg && lg.lodCount <= 1 && (lg.transform.root.name.StartsWith("Map_") || lg.gameObject.layer == FBWorld.LMap))
                    {
                        lg.enabled = false;
                        continue;
                    }
                    Smooth(lg, pa ? Mathf.Min(k * .4f, .002f) : k);
                }
            }
        }
        public static void Smooth(LODGroup lg, float k = -1)
        {
            if (!lg) return;
            if (lg.lodCount <= 1 && (lg.transform.root.name.StartsWith("Map_") || lg.gameObject.layer == FBWorld.LMap))
            {
                lg.enabled = false;
                return;
            }
            if (k < 0) k = LodCutK;
            if (lg.fadeMode == LODFadeMode.None) { lg.fadeMode = LODFadeMode.CrossFade; lg.animateCrossFading = true; }
            if (lg.lodCount != 1) return;
            var L = lg.GetLODs(); if (!lodOrig.TryGetValue(lg, out var o)) lodOrig[lg] = o = L[0].screenRelativeTransitionHeight;
            float target = k <= .005f ? Mathf.Min(o, k) : o * k;
            if (Mathf.Abs(L[0].screenRelativeTransitionHeight - target) > 1e-6f) { L[0].screenRelativeTransitionHeight = target; lg.SetLODs(L); }
        }
        public static int PruneCasters(Scene s, float minHalfDiag, List<(Renderer, ShadowCastingMode)> undo = null)
        {
            int n = 0; if (!s.IsValid() || !s.isLoaded) return 0;
            foreach (var root in s.GetRootGameObjects())
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.shadowCastingMode != ShadowCastingMode.On || r.GetComponentInParent<FBCharacter>() || r.GetComponentInParent<FBCar>()) continue;   // personagens e carros mantêm a sombra
                    if (r.bounds.extents.magnitude >= minHalfDiag) continue;
                    undo?.Add((r, r.shadowCastingMode)); r.shadowCastingMode = ShadowCastingMode.Off; n++;
                }
            return n;
        }

        class Snapshot
        {
            readonly UniversalRenderPipelineAsset a; readonly float rs, sd; readonly int sc, res, lights;
            public Snapshot(UniversalRenderPipelineAsset a) { this.a = a; rs = a.renderScale; sd = a.shadowDistance; sc = a.shadowCascadeCount; res = a.mainLightShadowmapResolution; lights = a.maxAdditionalLightsCount; }
            public void Restore() { if (!a) return; a.renderScale = rs; a.shadowDistance = sd; a.shadowCascadeCount = sc; a.mainLightShadowmapResolution = res; a.maxAdditionalLightsCount = lights; }
        }
    }

    // Resolução dinâmica (mobile/WebGL): baixa a escala de render quando o quadro passa do alvo e sobe devagar quando sobra folga.
    // Mudar renderScale não compila shader (só recria o alvo de render), então pode rodar no meio da partida.
    public class FBDynRes : MonoBehaviour
    {
        static FBDynRes I; float avg = 16, cool; public static float Scale { get; private set; } = 1;
        public static void Setup()
        {
            Scale = FBQuality.P.renderScale;
            if (!Application.isPlaying) return;
            if (!FBQuality.P.dynamicResolution) { if (I) Destroy(I.gameObject); return; }
            if (!I) { var g = new GameObject("FB_DynRes"); DontDestroyOnLoad(g); g.hideFlags = HideFlags.HideInHierarchy; I = g.AddComponent<FBDynRes>(); }
        }
        // [parceiro] v1 (thread de física): tempo do Update medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_Update = FBTempo.Id("FBDynRes.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            var p = FBQuality.P; float ms = Time.unscaledDeltaTime * 1000; if (ms > 250) return;   // pulo de carregamento não conta
            avg = Mathf.Lerp(avg, ms, .05f); cool -= Time.unscaledDeltaTime; if (cool > 0) return;
            float target = 1000f / (p.targetFps > 0 ? p.targetFps : 60);
            float s = Scale;
            if (avg > target * 1.15f) s -= .05f; else if (avg < target * .8f) s += .025f; else return;
            s = Mathf.Clamp(s, p.minRenderScale, p.renderScale); if (Mathf.Abs(s - Scale) < .001f) return;
            Scale = s; cool = 1.5f; if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset a) a.renderScale = s;
        }
    }
}
