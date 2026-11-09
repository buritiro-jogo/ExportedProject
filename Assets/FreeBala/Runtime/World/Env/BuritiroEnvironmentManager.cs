// BURITIRO ENVIRONMENT MANAGER (rodada 64) — horário do dia, sol e lua reais de Boa Vista, céu físico e luz coerente com ele.
//
// Como funciona (uma passada por mudança de horário, ~0,3 ms):
//  1. FBSolar: posição do sol e da lua para a data/hora/lugar do preset (Boa Vista, 2,82° N) — rumo e elevação reais.
//  2. FBAtmosphere (Rayleigh + Mie + ozônio): cor/força do sol que chega ao chão, brilho do céu em 48 direções → luz ambiente (céu,
//     horizonte, chão rebatido) e cor da neblina. O céu do skybox (FBSky.shader) usa o MESMO modelo por pixel.
//  3. ADAPTAÇÃO: o olho/câmera se ajusta à luz total da cena (expoente 'adaptation' do preset, suavizado no tempo): o pôr do sol fica
//     quente e mais escuro que o meio-dia sem virar breu, a blue hour fica azul e a noite é dos postes (com piso de luz para o combate).
//  4. Curvas ARTÍSTICAS do preset pela elevação do sol (exposição, contraste, saturação, balanço de branco, bloom, neblina, volumétrica,
//     força das sombras, "noite" que acende postes/faróis/janelas) — FBEnvPreset, editáveis num ScriptableObject.
//  5. Aplica pelo FBWeather.ApplyEnv (sol/lua, ambiente, sonda SH, neblina, céu, pós, volumétrica, reflexo) — chuva continua por cima.
//
// Calibração: o MEIO-DIA físico é casado com o dia aprovado do mapa (força do sol e luz ambiente da cena); o resto do dia segue a física.
// Liga sozinho em toda partida (FBWeather.Apply). '-fbluzantiga' volta o sistema antigo (comparação antes × depois).
// Linha de comando: -fbhora 18.5 (hora fixa) · -fbciclo 2 (minutos de jogo por segundo) · -fbcinematico (perfil de captura).
// Editor: BURITIRO > Ambiente (janela com horários prontos e controles em tempo real).
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public struct FBEnvState
    {
        public float hour, sunEl, sunAz, moonEl, moonAz, moonPhase, moonIllum, night, skyNight, windows, adapt;
        public Vector3 sunDir, moonDir, lightDir; public bool moonLight;
        public Color lightColor; public float lightIntensity, shadowStrength;
        public Color ambSky, ambEq, ambGround; public float ambIntensity;
        public Color fogColor; public float fogMul;
        public float exposure, contrast, saturation, whiteBalance, tint, bloom, volumetric;
        public float skyExposure; public Color sunTint; public float lightPollution, bakedProbeK;
        public override string ToString() => $"{FBEnv.HourText(hour)} sol {sunEl:0.0}°/{sunAz:0}° lua {moonEl:0.0}°/{moonAz:0}° ({moonIllum * 100:0}%) n {night:0.00} adapt {adapt:0.0} luz {(moonLight ? "LUA" : "SOL")} {lightIntensity:0.000} amb {ambIntensity:0.00} céu×{skyExposure:0.00} exp {exposure:+0.00;-0.00} cont {contrast:0} sat {saturation:0} wb {whiteBalance:0}";
    }

    public static class FBEnv
    {
        public static string HourText(float h) { h = Mathf.Repeat(h, 24); int hh = (int)h, mm = Mathf.RoundToInt((h - hh) * 60); if (mm == 60) { hh = (hh + 1) % 24; mm = 0; } return $"{hh:00}:{mm:00}"; }
        // horários prontos (pedidos para a rodada 64; ajustáveis pelo preset/data)
        public static readonly (string nome, float hora)[] Presets =
        {
            ("Meio-dia", 12f), ("Golden hour", 17.5f), ("Pôr do sol", 18f), ("Blue hour", 18.5f), ("Noite", 20f), ("Madrugada", 0f), ("Amanhecer", 5.5f),
        };
        static string[] args; static string Arg(string k) { args ??= System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(args, k); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        static bool Has(string k) { args ??= System.Environment.GetCommandLineArgs(); return System.Array.IndexOf(args, k) >= 0; }
        public static bool Legacy => Has("-fbluzantiga");
        public static float CmdHour { get { var s = Arg("-fbhora"); return s != null && float.TryParse(s.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var h) ? h : -1; } }
        public static float CmdCycle { get { var s = Arg("-fbciclo"); return s != null && float.TryParse(s.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var h) ? h : 0; } }
        public static bool CmdCinematic => Has("-fbcinematico");
    }

    [DefaultExecutionOrder(-50)]
    public class BuritiroEnvironmentManager : MonoBehaviour
    {
        public static BuritiroEnvironmentManager I { get; private set; }
        public static bool Active => I && I.enabled;

        [Header("Horário")]
        [Range(0, 24)] public float hour = 15.5f;
        [Tooltip("Minutos de jogo por segundo real (0 = horário parado)")] public float cycleSpeed;
        public FBEnvPreset preset;
        [Header("Qualidade")] public bool cinematic;
        [Header("Estado (só leitura)")] public FBEnvState state;

        public string MapId { get; private set; }
        // ANOITECER do menu: 17:15 → 19:00 em 7 min de partida (o mesmo tempo do sistema antigo)
        public const float DuskFrom = 17.25f, DuskTo = 19f, DuskSecs = 420f, DayHour = 15.5f, NightHour = 20f;
        bool dusk; float duskT; float lastHour = -99; bool dirty = true; float adaptSm = -1; FBEnvPreset.MapOverride ov;
        // calibração (meio-dia físico ↔ dia aprovado do mapa)
        float sunBase = 1, ambBaseLum = .5f, fogBaseLum = .6f; Color ambSkyBase, ambEqBase, ambGrBase, fogBase; float calSun, calSky, eRef, sunRefLum, lumSkyL, lumEqL, lumGrL, lumFogL;
        readonly Dictionary<string, float> cache = new();

        public static BuritiroEnvironmentManager Begin(GameObject host, string mapId, string menuTime, Light sun)
        {
            var m = host.AddComponent<BuritiroEnvironmentManager>(); I = m; m.MapId = mapId;
            m.preset = Resources.Load<FBEnvPreset>("Env/BuritiroEnv_Padrao"); if (!m.preset) m.preset = FBEnvPreset.Padrao();
            m.ov = m.preset.For(mapId);
            m.sunBase = sun ? sun.intensity : 1; m.ambSkyBase = RenderSettings.ambientSkyColor; m.ambEqBase = RenderSettings.ambientEquatorColor; m.ambGrBase = RenderSettings.ambientGroundColor; m.fogBase = RenderSettings.fogColor;
            m.Calibrate();
            float h = menuTime == "noite" ? NightHour : DayHour; m.dusk = menuTime == "anoitecer"; if (m.dusk) h = DuskFrom;
            if (m.ov != null && m.ov.fixedHour >= 0) { h = m.ov.fixedHour; m.dusk = false; }
            if (FBEnv.CmdHour >= 0) { h = FBEnv.CmdHour; m.dusk = false; }
            m.hour = h; m.cycleSpeed = FBEnv.CmdCycle; m.cinematic = FBEnv.CmdCinematic;
            if (m.cinematic) FBEnvQuality.ApplyCinematic(true);
            m.Evaluate(true); Debug.Log($"[FreeBala][ambiente] {mapId}: {m.state} · {m.calInfo} · preset {m.preset.name} · data {m.preset.year}-{m.preset.month:00}-{m.preset.day:00}");
            return m;
        }
        void OnDestroy() { if (I == this) I = null; if (cinematic) FBEnvQuality.ApplyCinematic(false); }

        // ---- controle (Editor, testes, menu)
        public void SetHour(float h, bool instant = false) { hour = Mathf.Repeat(h, 24); dusk = false; dirty = true; if (instant) adaptSm = -1; }
        public void SetDuskSeconds(float secs) { dusk = true; duskT = Mathf.Clamp(secs, 0, DuskSecs); hour = Mathf.Lerp(DuskFrom, DuskTo, duskT / DuskSecs); dirty = true; adaptSm = -1; }
        public void MarkDirty() { dirty = true; cache.Clear(); Calibrate(); }
        public bool IsDusk => dusk;

        void Update()
        {
            float dt = Time.deltaTime;
            if (dusk && duskT < DuskSecs) { duskT += dt; hour = Mathf.Lerp(DuskFrom, DuskTo, Mathf.Clamp01(duskT / DuskSecs)); }
            else if (cycleSpeed > 0) hour = Mathf.Repeat(hour + cycleSpeed * dt / 60f, 24);
            bool changed = dirty || Mathf.Abs(Mathf.DeltaAngle(hour * 15, lastHour * 15)) > .02f;   // ~5 s de jogo no ciclo de 7 min
            // adaptação suave: mesmo sem mudar o horário, a exposição continua indo para o alvo
            if (changed || (adaptSm > 0 && Mathf.Abs(adaptSm - targetAdapt) > targetAdapt * .005f)) Evaluate(false);
        }

        float targetAdapt = 1; string calInfo = "";
        void Calibrate()
        {
            FBAtmosphere.Turbidity = preset.turbidity; FBAtmosphere.MieG = preset.mieG;
            // meio-dia de referência: sol a 65° (o "dia" aprovado de cada mapa foi calibrado com sol alto)
            var Tn = FBAtmosphere.Transmittance(65); var sd = FBSolar.Direction(180, 65);
            FBAtmosphere.SkyAmbient(sd, Tn, preset.groundAlbedo, out var zn, out var hz, out var gr, out var skyIrr);
            calSun = sunBase / Mathf.Max(1e-5f, FBAtmosphere.Lum(Tn)); sunRefLum = Mathf.Max(1e-5f, FBAtmosphere.Lum(Tn));
            // r64b (lab95 lavado/estourado): a INTENSIDADE do ambiente e da neblina vem do dia aprovado do mapa × curva skyAmbient (relativa ao
            // meio-dia); da física sai só a COR. Tudo em espaço LINEAR (as cores do RenderSettings são gama). Antes: escala única tirada do
            // zênite (×68 na VJ) aplicada ao horizonte e ao chão → fachadas ~12× o ambiente aprovado, e a adaptação (×5 no pôr do sol, ×220
            // na blue hour) multiplicava tudo de novo.
            lumSkyL = Mathf.Max(.02f, FBAtmosphere.Lum(Vec(ambSkyBase.linear))); lumEqL = Mathf.Max(.015f, FBAtmosphere.Lum(Vec(ambEqBase.linear)));
            lumGrL = Mathf.Max(.008f, FBAtmosphere.Lum(Vec(ambGrBase.linear))); lumFogL = Mathf.Max(.02f, FBAtmosphere.Lum(Vec(fogBase.linear)));
            calSky = .123f / Mathf.Max(1e-6f, FBAtmosphere.Lum(FBAtmosphere.Radiance(Vector3.up, sd)));   // zênite do céu antigo ao meio-dia (linear)
            eRef = SceneIllum(65, FBAtmosphere.Lum(Tn), FBAtmosphere.Lum(skyIrr));
            calInfo = $"base linear: céu {lumSkyL:0.000} equador {lumEqL:0.000} chão {lumGrL:0.000} neblina {lumFogL:0.000} · sol {sunBase:0.00} · céu×{calSky:0.0}";
        }
        // luz "média" que a cena recebe (chão + fachadas): é o que a adaptação compara
        static float SceneIllum(float el, float sunLum, float skyLum) { float s = Mathf.Sin(Mathf.Max(0, el) * Mathf.Deg2Rad), c = Mathf.Cos(Mathf.Max(0, el) * Mathf.Deg2Rad); return sunLum * (.55f * s + .18f * c) + skyLum + 1e-7f; }
        static Vector3 Vec(Color c) => new(c.r, c.g, c.b);
        static Color Col(Vector3 v) => new(Mathf.Max(0, v.x), Mathf.Max(0, v.y), Mathf.Max(0, v.z), 1);
        static Color Gam(Vector3 v) => Col(v).gamma;                                                // linear → gama (RenderSettings)
        static Vector3 Unit(Vector3 v) { float l = FBAtmosphere.Lum(v); return l > 1e-9f ? v / l : Vector3.one; }   // só a cor (luminância 1)

        public void Evaluate(bool instant)
        {
            if (!preset) return; dirty = false; lastHour = hour;
            var p = preset; FBAtmosphere.Turbidity = p.turbidity; FBAtmosphere.MieG = p.mieG;
            int y = p.year, mo = p.month, d = p.day; if (y <= 0) { var now = System.DateTime.Now; y = now.Year; mo = now.Month; d = now.Day; }
            double jd = FBSolar.Julian(y, mo, d, hour, p.utcOffset); float ny = ov?.northYaw ?? 0;
            var sun = FBSolar.Sun(jd, p.latitude, p.longitude, ny); var moon = FBSolar.Moon(jd, p.latitude, p.longitude, out float phase, out float illum, ny);
            var s = new FBEnvState { hour = hour, sunEl = sun.elevation, sunAz = sun.azimuth, moonEl = moon.elevation, moonAz = moon.azimuth, moonPhase = phase, moonIllum = illum, sunDir = sun.dir, moonDir = moon.dir };
            float el = sun.elevation;

            // física: sol que chega, céu, chão rebatido
            var T = FBAtmosphere.Transmittance(el);
            FBAtmosphere.SkyAmbient(sun.dir, T, p.groundAlbedo, out var zn, out var hz, out var gr, out var skyIrr);
            float sunLum = FBAtmosphere.Lum(T), skyLum = FBAtmosphere.Lum(skyIrr);
            // adaptação (exposição automática), suavizada
            float E = SceneIllum(el, sunLum, skyLum);
            targetAdapt = Mathf.Clamp(Mathf.Pow(eRef / E, p.adaptation), 1, p.maxAdaptation);
            if (instant || adaptSm < 0) adaptSm = targetAdapt;
            else { float k = 1 - Mathf.Exp(-Time.deltaTime / Mathf.Max(.05f, p.adaptationSeconds)); adaptSm = Mathf.Exp(Mathf.Lerp(Mathf.Log(adaptSm), Mathf.Log(targetAdapt), k)); }
            float X = adaptSm; s.adapt = X;

            // curvas artísticas
            float night = Mathf.Clamp01(p.night.Evaluate(el));
            s.night = night; s.skyNight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-4, -15, el));
            s.windows = Mathf.Clamp01(p.windowsOn.Evaluate(Mathf.Repeat(hour, 24)));
            s.exposure = p.exposure.Evaluate(el) + (ov?.exposureBias ?? 0); s.contrast = p.contrast.Evaluate(el); s.saturation = p.saturation.Evaluate(el);
            s.whiteBalance = p.whiteBalance.Evaluate(el); s.tint = p.tint.Evaluate(el); s.bloom = p.bloom.Evaluate(el);
            s.fogMul = p.fogDensity.Evaluate(el) * (ov?.fogMul ?? 1); s.volumetric = p.volumetric.Evaluate(el) * (ov?.volumetricMul ?? 1);
            s.lightPollution = ov != null && ov.lightPollution >= 0 ? ov.lightPollution : p.lightPollution;
            s.bakedProbeK = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(6, 22, el));

            // sol (cor física, força física × adaptação × curva; teto para fachadas viradas ao sol não estourarem no pôr do sol)
            float tmax = Mathf.Max(1e-6f, Mathf.Max(T.x, Mathf.Max(T.y, T.z)));
            var sunCol = new Color(T.x / tmax, T.y / tmax, T.z / tmax);
            // força: a do dia aprovado × curva × (sol físico / sol de referência)^0,4 — cai no pôr do sol sem sumir (a adaptação do olho)
            float sunI = sunBase * p.sunIntensity.Evaluate(el) * Mathf.Pow(sunLum / sunRefLum, .4f) * (ov?.sunMul ?? 1); sunI = Mathf.Min(sunI, sunBase * 1.05f);
            // lua: força pela fase e pela altura; cor fria
            float moonUp = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-1, 14, moon.elevation));
            float moonI = p.moonIntensity * Mathf.Pow(illum, 1.4f) * moonUp * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-2, -10, el));
            if (sunI >= moonI || sunI > .02f) { s.moonLight = false; s.lightDir = sun.dir; s.lightColor = sunCol; s.lightIntensity = sunI; }
            else { s.moonLight = true; s.lightDir = moon.dir; s.lightColor = p.moonColor; s.lightIntensity = moonI; }
            s.shadowStrength = s.moonLight ? .55f : p.shadowStrength.Evaluate(el);

            // ambiente: COR física (um pouco dessaturada) × luminância do dia aprovado × curva skyAmbient; + piso da noite. Linear → gama.
            float ambK = Mathf.Max(0, p.skyAmbient.Evaluate(el)) * (ov?.ambientMul ?? 1);
            Vector3 Desat(Vector3 v, float k) { float l = FBAtmosphere.Lum(v); return Vector3.Lerp(new Vector3(l, l, l), v, k); }
            float gK = Mathf.Lerp(.45f, 1, Mathf.Sin(Mathf.Clamp(el, 0, 90) * Mathf.Deg2Rad));                 // rebatido do chão cai com o sol baixo
            var aSky = Unit(Desat(zn, .62f)) * (lumSkyL * ambK); var aEq = Unit(Desat(hz, .7f)) * (lumEqL * ambK); var aGr = Unit(gr) * (lumGrL * ambK * gK);
            float fl = night * p.nightAmbientIntensity;                                              // piso da noite (visibilidade do combate)
            s.ambSky = Gam(aSky + Vec(p.nightAmbientSky.linear) * fl); s.ambEq = Gam(aEq + Vec(p.nightAmbientEquator.linear) * fl); s.ambGround = Gam(aGr + Vec(p.nightAmbientGround.linear) * fl);
            s.ambIntensity = 1;
            // neblina: cor do horizonte (média com o lado do sol), luminância da neblina aprovada × curva; à noite vai para o azul-escuro urbano
            var hzSun = FBAtmosphere.Radiance(new Vector3(sun.dir.x, .04f, sun.dir.z).normalized, sun.dir, 8, 4);
            var fogV = Unit(Desat(Vector3.Lerp(hz, hzSun, .3f), .75f)) * (lumFogL * Mathf.Pow(ambK, .8f));
            s.fogColor = Color.Lerp(Gam(fogV), new Color(.035f, .045f, .075f), Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-3, -14, el)));
            // céu (skybox físico): adaptação, com teto no horizonte do lado do sol (≤ 1,3 linear; o shader ainda tem ombro suave)
            s.skyExposure = Mathf.Min(calSky * X, 1.3f / Mathf.Max(1e-6f, FBAtmosphere.Lum(hzSun))) * p.skyBrightness.Evaluate(el);
            s.sunTint = sunCol * Mathf.Clamp01(sunLum * 4);
            state = s;
            Shader.SetGlobalFloat("_FBTurb", p.turbidity);
            FBWeather.ApplyEnv(s);
        }
    }

    // perfil CINEMATIC (opcional, para capturas e PCs fortes): sombra 4096 com 4 cascatas até 220 m, volumétrica com mais passos e maior
    // resolução. URP não tem ray tracing — o "RTX" não existe nesta pipeline (documentado no relatório da rodada 64).
    public static class FBEnvQuality
    {
        // a resolução da sombra principal tem 'set' interno no URP 17: grava no campo serializado (só valores do enum: 256…8192)
        static void SetRes(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset a, int px)
        {
            var f = typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset).GetField("m_MainLightShadowmapResolution", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(a, System.Enum.ToObject(f.FieldType, px));
        }
        static bool on; static float d0; static int c0, r0; static int st0; static float vs0;
        public static bool Cinematic => on;
        public static void ApplyCinematic(bool enable)
        {
            if (enable == on) return;
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset; if (!urp) return;
            if (enable)
            {
                d0 = urp.shadowDistance; c0 = urp.shadowCascadeCount; r0 = urp.mainLightShadowmapResolution; st0 = FBVolumetric.Steps; vs0 = FBVolumetric.ResolutionScale;
                urp.shadowDistance = 220; if (c0 > 1) urp.shadowCascadeCount = 4;          // não troca 1 → 4 (keyword de cascata: compilação no meio do jogo)
                SetRes(urp, 4096); FBVolumetric.Steps = Mathf.Max(st0, 40); FBVolumetric.ResolutionScale = Mathf.Max(vs0, .75f);
            }
            else { urp.shadowDistance = d0; urp.shadowCascadeCount = c0; SetRes(urp, r0); FBVolumetric.Steps = st0; FBVolumetric.ResolutionScale = vs0; }
            on = enable; FBVolumetric.Steps = Mathf.Max(8, FBVolumetric.Steps); Debug.Log($"[FreeBala][ambiente] perfil CINEMATIC {(on ? "ligado" : "desligado")}: sombra {urp.shadowDistance} m · {urp.shadowCascadeCount} cascatas · {urp.mainLightShadowmapResolution} px");
        }
    }
}
