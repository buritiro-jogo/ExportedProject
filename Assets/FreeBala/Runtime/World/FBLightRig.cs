// ILUMINAÇÃO DA CIDADE À NOITE (referências do usuário: capturas do jogo retrabalhadas + fotos reais da Praça das Águas):
//  · FBStreetLights — cada luminária do mapa é uma fonte de luz REAL (spot para baixo), não mais um pool de 6–16 luzes: o Forward+ do PC
//    aguenta dezenas; as mais perto da câmera (prioridade para as que estão na frente) acendem com fade (sem "pipocar"), as mais próximas
//    projetam sombra (sombra de luz adicional ligada no pipeline do PC). Tipo por mapa: Praça das Águas = LED branco frio (postes de
//    refletores da praça, fotos noturnas); Vila Jardim = mistura de LED e vapor de sódio laranja (bairro, referência 2); demais = quente.
//    Mobile continua com um pool pequeno + cones de malha.
//  · FBAccentLights — luzes de destaque que existem na praça real: refletores coloridos das fontes (mesma paleta e mesmo ciclo das gotas do
//    FBWaterDrops/FBWaterJet), refletores no pé do Portal do Milênio, palmeiras iluminadas de baixo, e o letreiro "EU AMO BOA VISTA"
//    iluminado DE FORA por dois refletores (o letreiro em si não é tocado: nem malha, nem material).
//  · FBVolumetric — parâmetros da luz volumétrica (PC): densidade do ar, sol/lua, céu e as luzes que espalham na névoa (postes, faróis,
//    fontes). Desde 20/10 quem desenha é o pacote "URP Volumetric Fog" de Cristian Qiu (MIT, Packages/com.cqf.urpvolumetricfog): raymarch
//    em meia resolução com SOMBRA do sol e das luzes adicionais (Forward+), desfoque/upsample pela profundidade — o FBVolumetricFeature
//    próprio (sem sombra das luzes adicionais) saiu dos renderers. Aqui só se traduz o clima para o Volume dele (DriveCQ).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FreeBala
{
    public static class FBVolumetric
    {
        public const int MaxLights = 32;
        public static bool Active;                                                       // flag do nível gráfico (FBGfx) E cena de partida com clima
        public static bool InScene;
        public static float ResolutionScale = .5f; public static int Steps = 24;
        public static readonly List<Light> Sources = new();                              // quem quer espalhar na névoa (postes, faróis, fontes)
        // atmosfera (FBWeather.Set)
        public static float Density = .002f, Falloff = .045f, GroundY, MaxDist = 150, G = .6f, LampK = 3f;
        public static Color MainColor, Ambient; public static Vector3 MainDir = Vector3.up;
        static readonly Vector4[] P = new Vector4[MaxLights], D = new Vector4[MaxLights], C = new Vector4[MaxLights];
        static readonly List<(float d, Light l)> tmp = new();
        static readonly int idParams = Shader.PropertyToID("_FBVolParams"), idMain = Shader.PropertyToID("_FBVolMain"), idMainDir = Shader.PropertyToID("_FBVolMainDir"),
            idAmb = Shader.PropertyToID("_FBVolAmbient"), idSteps = Shader.PropertyToID("_FBVolSteps"), idCount = Shader.PropertyToID("_FBVolLightCount"),
            idPos = Shader.PropertyToID("_FBVolLightPos"), idDir = Shader.PropertyToID("_FBVolLightDir"), idCol = Shader.PropertyToID("_FBVolLightCol");

        public static void Clear() { Sources.Clear(); InScene = false; }
        public static Color LightColor(Light l)
        {
            var c = l.color.linear * l.intensity; if (l.useColorTemperature) c *= Mathf.CorrelatedColorTemperatureToRGB(l.colorTemperature); return c;
        }
        public static void Tick(Camera cam)
        {
            if (!Active || !InScene || !cam) return;
            var cp = cam.transform.position; tmp.Clear();
            for (int i = Sources.Count - 1; i >= 0; i--)
            {
                var l = Sources[i]; if (!l) { Sources.RemoveAt(i); continue; }
                if (!l.isActiveAndEnabled || l.intensity < .01f || l.type == LightType.Directional) continue;
                float d = (l.transform.position - cp).sqrMagnitude; if (d > (MaxDist + l.range) * (MaxDist + l.range)) continue;
                tmp.Add((d, l));
            }
            tmp.Sort((a, b) => a.d.CompareTo(b.d)); int n = Mathf.Min(MaxLights, tmp.Count);
            for (int i = 0; i < n; i++)
            {
                var l = tmp[i].l; var t = l.transform; float range = Mathf.Min(l.range, 26f);   // halo/facho só perto (o alcance todo deixaria a névoa leitosa)
                P[i] = new Vector4(t.position.x, t.position.y, t.position.z, range);
                bool spot = l.type == LightType.Spot;
                float co = spot ? Mathf.Cos(l.spotAngle * .5f * Mathf.Deg2Rad) : -2, ci = spot ? Mathf.Cos(l.innerSpotAngle * .5f * Mathf.Deg2Rad) : 1;
                D[i] = new Vector4(t.forward.x, t.forward.y, t.forward.z, co);
                var c = LightColor(l) * LampK; C[i] = new Vector4(c.r, c.g, c.b, ci);
            }
            Shader.SetGlobalVector(idParams, new Vector4(Density, Falloff, GroundY, MaxDist));
            Shader.SetGlobalVector(idMain, new Vector4(MainColor.r, MainColor.g, MainColor.b, G));
            Shader.SetGlobalVector(idMainDir, MainDir);
            Shader.SetGlobalVector(idAmb, Ambient);
            Shader.SetGlobalVector(idSteps, new Vector4(Steps, Time.frameCount % 64, 0, 0));
            Shader.SetGlobalFloat(idCount, n);
            Shader.SetGlobalVectorArray(idPos, P); Shader.SetGlobalVectorArray(idDir, D); Shader.SetGlobalVectorArray(idCol, C);
            DriveCQ(n);
        }

        // ---- pacote URP Volumetric Fog (Cristian Qiu): mesma física do nosso raymarch -> parâmetros do Volume dele
        // densidade do pacote fixa (CQDensity) e a extinção real (Density, por metro) vira attenuationDistance; espalhamento do sol = Density × MainK
        // (o fator de cor que o FBWeather já aplicava: sol baixo/lua/chuva); postes/faróis/fontes ganham VolumetricAdditionalLight com Density × LampK
        public static float MainK = 1; const float CQDensity = .6f;
        static VolumetricFogVolumeComponent cq; static readonly HashSet<Light> cqLights = new();
        public static void AttachProfile(VolumeProfile p)
        {
            cqLights.Clear(); cq = null; if (!p) return;
            if (!p.TryGet(out cq)) cq = p.Add<VolumetricFogVolumeComponent>(true);
            cq.enabled.Override(false);
        }
        static void DriveCQ(int nLights)
        {
            if (!cq) return; bool on = Active && InScene; cq.enabled.Override(on); if (!on) return;
            bool hi = FBGfx.Tier == FBGfxTier.PCAlto;
            cq.density.Override(CQDensity); cq.attenuationDistance.Override(Mathf.Max(.05f, CQDensity / Mathf.Max(1e-4f, Density) * 1.4f));   // menos leitoso (retorno da rodada 31)
            cq.distance.Override(Mathf.Min(MaxDist, hi ? 90 : 70)); cq.baseHeight.Override(GroundY); cq.maximumHeight.Override(GroundY + 2.5f / Mathf.Max(.01f, Falloff));
            cq.enableMainLightContribution.Override(true); cq.anisotropy.Override(Mathf.Clamp(G, -.95f, .95f));
            cq.scattering.Override(Mathf.Clamp01(Density / CQDensity * MainK * 1.1f)); cq.tint.Override(Color.white);
            cq.enableAdditionalLightsContribution.Override(Sources.Count > 0); cq.maxSteps.Override(Mathf.Clamp(Steps + 14, 20, 44)); cq.blurIterations.Override(hi ? 2 : 1);   // 40 passos no PC Alto (56 custava ~1/3 do quadro)
            float ls = Mathf.Clamp(Density / CQDensity * LampK * 1.3f, 0, 16);
            foreach (var l in Sources)
            {
                if (!l || l.type == LightType.Directional) continue;
                if (!cqLights.Contains(l)) { if (!l.TryGetComponent<VolumetricAdditionalLight>(out _)) l.gameObject.AddComponent<VolumetricAdditionalLight>(); cqLights.Add(l); }
                var va = l.GetComponent<VolumetricAdditionalLight>(); va.Scattering = ls; va.Anisotropy = .2f; va.Radius = .25f;
            }
        }
    }

    public static class FBStreetLights
    {
        public struct Lamp { public Vector3 p; public float cct, intensity, range, angle, inner; public bool sodium; public Quaternion rot; public float tilt; }
        static readonly List<Lamp> lamps = new(); static float[] w; static readonly List<Light> slots = new(); static readonly Dictionary<int, int> slotOf = new();
        static readonly HashSet<int> sel = new(); static float selT; static int K, maxShadow; static float maxDist;
        public static IReadOnlyList<Lamp> Lamps => lamps;
        public static int Lit { get; private set; }

        static uint Hash(Vector3 p) => (uint)(Mathf.RoundToInt(p.x * 3) * 73856093 ^ Mathf.RoundToInt(p.z * 3) * 83492791);
        // tipo da luminária pelo mapa: LED branco (praça, avenida) ou vapor de sódio (rua de bairro)
        static Lamp Classify(Vector3 p, string map)
        {
            bool pa = map.Contains("praca_aguas"), vj = map.Contains("vila_jardim");
            // luminária baixa (teto de galpão/cobertura, até 6 m do chão): luz de teto fria e curta, não de poste
            float hg = Physics.Raycast(p + Vector3.down * .2f, Vector3.down, out var gh, 30, 1 << FBWorld.LMap, QueryTriggerInteraction.Ignore) ? p.y - gh.point.y : 99;
            if (hg < 6) return new Lamp { p = p, cct = 4300, intensity = 18, range = 11, angle = 150, inner = 100 };
            bool sodium = SodiumAt != null && SodiumAt(p) || (vj ? Hash(p) % 100 < 45 : !pa && Hash(p) % 100 < 30);
            if (sodium) return new Lamp { p = p, cct = 2100, intensity = 85, range = 32, angle = 128, inner = 60, sodium = true };
            return new Lamp { p = p, cct = pa ? 4700 : 4100, intensity = pa ? 125 : 100, range = pa ? 40 : 34, angle = pa ? 150 : 135, inner = pa ? 80 : 65 };
        }
        public static System.Func<Vector3, bool> SodiumAt;                              // medido na importação (FBLampData.sodium)
        public static Color GlowColor(Lamp l) => l.sodium ? new Color(1f, .62f, .26f) : l.cct > 4400 ? new Color(.93f, .96f, 1f) : new Color(1f, .9f, .74f);
        public static Color GlowColorAt(Vector3 p) { foreach (var l in lamps) if ((l.p - p).sqrMagnitude < .01f) return GlowColor(l); return new Color(1, .86f, .6f); }

        // ---- luminária quebrada (poste derrubado/entortado, tiro): curto (pisca irregular + faísca) e apaga de vez
        static bool[] dead = new bool[0], flkOn = new bool[0]; static float[] flk = new float[0], flkNext = new float[0];
        static ParticleSystem glowPs; static ParticleSystem.Particle[] glowP;
        public static bool IsDead(int i) => i >= 0 && i < dead.Length && dead[i];
        // r44 — CORTE DE ENERGIA (FBEletricista): apaga sem quebrar (pisca e some; volta com a energia)
        static bool[] cut = new bool[0];
        public static bool IsCut(int i) => i >= 0 && i < cut.Length && cut[i];
        public static void Cut(int i, bool off)
        {
            if (i < 0 || i >= lamps.Count) return; if (cut.Length != lamps.Count) System.Array.Resize(ref cut, lamps.Count);
            if (cut[i] == off) return; cut[i] = off; if (dead[i]) return;
            SetGlow(i, !off); SetHead(i, !off); if (off) FBFx.Sparks(lamps[i].p, Vector3.down);
        }
        public static int Nearest(Vector3 p, float maxD) { int bi = -1; float bd = maxD * maxD; for (int i = 0; i < lamps.Count; i++) { if (IsDead(i)) continue; float d = (lamps[i].p - p).sqrMagnitude; if (d < bd) { bd = d; bi = i; } } return bi; }
        public static void Kill(int i, float flicker, bool hideGlowNow, bool shot = false)
        {
            if (i < 0 || i >= lamps.Count || dead[i]) return; dead[i] = true; flk[i] = flicker; flkNext[i] = 0; flkOn[i] = true;
            var p = lamps[i].p; FBFx.Sparks(p, Vector3.down); FBAudio.PlayAt("glass_break", p, shot ? .75f : .45f, 9);
            if (shot) FBImpactFx.Glass(p, Vector3.down, Vector3.down);
            if (hideGlowNow) { SetGlow(i, false); FBLampHead.SetLamp(i, false, p, lamps); }
        }
        // (rodada 37) a CABEÇA da luminária (material emissivo compartilhado que o FBWeather acende à noite) também apaga/pisca — antes só a
        // luz e o brilho somado apagavam e a lâmpada continuava branca e estourando no bloom
        static void SetHead(int i, bool on) => FBLampHead.SetLamp(i, on, lamps[i].p, lamps);
        static void SetGlow(int i, bool on)
        {
            if (!glowPs) return;
            if (glowP == null || glowP.Length != glowPs.particleCount) { glowP = new ParticleSystem.Particle[glowPs.particleCount]; glowPs.GetParticles(glowP); }
            if (i >= glowP.Length) return; var c = GlowColor(lamps[i]); glowP[i].startColor = on ? c : new Color(0, 0, 0, 0); glowPs.SetParticles(glowP, glowP.Length);
        }
        public static void Build(IReadOnlyList<Vector3> pos, Transform parent, string map)
        {
            lamps.Clear(); slots.Clear(); slotOf.Clear(); sel.Clear(); selT = 0; Lit = 0; glowPs = null; glowP = null;
            if (pos == null) { w = new float[0]; dead = new bool[0]; flkOn = new bool[0]; flk = new float[0]; flkNext = new float[0]; return; }
            // r65: facho do poste. Antes todo spot apontava RETO para baixo com 135°: o poste (a 1–1,5 m da cabeça) caía dentro do cone e
            // virava um risco aceso (retorno do usuário, estacionamento do Colégio). Luminária de LED de rua joga a luz para a pista (óptica
            // assimétrica): o facho inclina para LONGE do poste/parede achado por raios curtos abaixo da cabeça, e a abertura fecha um pouco.
            var V = FBVisual.C;
            foreach (var p in pos)
            {
                var lp = Classify(p, map);
                lp.intensity *= V.lampIntensityMul; lp.range *= V.lampRangeMul;
                if (V.lampAngle > 0) { lp.angle = Mathf.Min(lp.angle, V.lampAngle); lp.inner = Mathf.Min(lp.inner, V.lampInner > 0 ? V.lampInner : lp.angle * .5f); }
                lp.rot = Aim(lp.p, V.lampTilt, out lp.tilt); lamps.Add(lp);
            }
            w = new float[lamps.Count]; dead = new bool[lamps.Count]; flkOn = new bool[lamps.Count]; flk = new float[lamps.Count]; flkNext = new float[lamps.Count];
            FBLampHead.ResetScene(); FBLampHead.Bind(lamps);                                // cabeças separadas pelo FBLampPrep (uma por luminária)
            bool mob = FBQuality.Mobile; var L = FBQuality.Level;
            K = mob ? 6 : L == FBQLevel.Ultra ? 56 : L == FBQLevel.Alto ? 40 : L == FBQLevel.Medio ? 24 : 10;
            // r65: mais postes com sombra (luz que atravessava prédio = poste sem sombra): os 6 mais perto em 512 px, o resto em 256 px
            maxShadow = mob ? 0 : L == FBQLevel.Ultra ? 22 : L == FBQLevel.Alto ? 14 : L == FBQLevel.Medio ? 5 : 0;   // retorno do usuário: postes/luz de teto sem sombra nos objetos
            sw = new float[lamps.Count];
            maxDist = mob ? 70 : L >= FBQLevel.Alto ? 190 : 140;
            K = Mathf.Min(K, lamps.Count);
            for (int i = 0; i < K + Mathf.Min(6, lamps.Count - K); i++)
            {
                var l = new GameObject("Poste" + i).AddComponent<Light>(); l.transform.SetParent(parent); l.type = LightType.Spot; l.transform.rotation = Quaternion.Euler(90, 0, 0);
                l.useColorTemperature = true; l.color = Color.white; l.intensity = 0; l.enabled = false; l.shadows = LightShadows.None;
                l.shadowStrength = .9f; l.shadowBias = .04f; l.shadowNormalBias = .3f; l.shadowNearPlane = .7f; FBLightUtil.MediumShadows(l);
                if (V.lampCookie > 0 && !mob) l.cookie = LedCookie();                           // r65: distribuição de luminária de rua (não círculo)
                slots.Add(l); FBVolumetric.Sources.Add(l);
            }
            // brilho das lâmpadas (sprite somado, cor pelo tipo) — acende com a noite (FBWeather.Tick)
            glow = null;
            if (lamps.Count > 0)
            {
                glow = new GameObject("PostesBrilho"); glow.transform.SetParent(parent); var tex = new Texture2D(64, 64);
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) { float d = new Vector2(x - 32, y - 32).magnitude / 32; float a = Mathf.Clamp01(1 - d); tex.SetPixel(x, y, new Color(1, 1, 1, a * a * (.55f + .45f * Mathf.Pow(Mathf.Clamp01(1 - d * 3), 2)))); }
                tex.Apply(); var m = new Material(FBShaders.ParticlesUnlit); m.SetTexture("_BaseMap", tex); m = FBShaders.Transparent(m, true); m.renderQueue = 3100;
                var ps = glow.AddComponent<ParticleSystem>(); ps.Stop(); var mm = ps.main; mm.loop = false; mm.startLifetime = 1e6f; mm.startSpeed = 0; mm.maxParticles = lamps.Count + 1; mm.playOnAwake = false; mm.simulationSpace = ParticleSystemSimulationSpace.World;
                var e = ps.emission; e.enabled = false; var pr = glow.GetComponent<ParticleSystemRenderer>(); pr.material = m; pr.shadowCastingMode = ShadowCastingMode.Off; ps.Play();
                foreach (var l in lamps) ps.Emit(new ParticleSystem.EmitParams { position = l.p + Vector3.up * .55f, startSize = (l.sodium ? 4.2f : 3.6f) * V.lampGlowSize, startLifetime = 1e6f, startColor = GlowColor(l) }, 1);
                glowPs = ps;
                glow.SetActive(false);
            }
            Debug.Log($"[FreeBala][luz] postes: {lamps.Count} luminárias ({lamps.Count(x => x.sodium)} sódio), {K} luzes reais, {maxShadow} com sombra, alcance {maxDist:0} m · facho inclinado em {Aimed} (tilt {V.lampTilt}°)");
        }
        static GameObject glow;

        public static void Tick(Camera cam, float nightK, float dt)
        {
            bool lit = nightK > .45f; if (glow) glow.SetActive(lit);
            if (lamps.Count == 0 || w == null) return;
            float k = Mathf.InverseLerp(.45f, 1, nightK); var cp = cam.transform.position; var cf = cam.transform.forward;
            if ((selT -= dt) <= 0)
            {
                selT = .2f; sel.Clear();
                if (lit)
                {
                    var cand = new List<(float s, int i)>();
                    for (int i = 0; i < lamps.Count; i++)
                    {
                        if ((dead[i] && flk[i] <= 0) || IsCut(i)) continue;                                  // apagada de vez / sem energia
                        var v = lamps[i].p - cp; float d = v.magnitude; if (d > maxDist) continue;
                        // na frente da câmera primeiro, mas pouco (girar a câmera não pode trocar as luzes = "pipocar"); acesa fica (histerese)
                        bool behind = Vector3.Dot(v, cf) < -.2f * d && d > lamps[i].range * .6f; cand.Add((d * (behind ? 1.45f : 1) * (w[i] > 0 ? .8f : 1), i));
                    }
                    cand.Sort((a, b) => a.s.CompareTo(b.s)); for (int j = 0; j < Mathf.Min(K, cand.Count); j++) sel.Add(cand[j].i);
                }
            }
            // curto das quebradas: pisca irregular (o brilho acompanha) até apagar
            for (int i = 0; i < lamps.Count; i++)
            {
                if (!dead[i] || flk[i] <= 0) continue; flk[i] -= dt;
                if (Time.time >= flkNext[i]) { flkOn[i] = !flkOn[i]; flkNext[i] = Time.time + (flkOn[i] ? Random.Range(.02f, .12f) : Random.Range(.04f, .3f)); if (lit) SetGlow(i, flkOn[i] && flk[i] > 0); SetHead(i, flkOn[i] && flk[i] > 0); if (flkOn[i] && Random.value < .35f) FBFx.Sparks(lamps[i].p, Vector3.down); }
                if (flk[i] <= 0) { flkOn[i] = false; SetGlow(i, false); SetHead(i, false); }
            }
            // fade de entrada/saída e alocação estável dos slots
            for (int i = 0; i < lamps.Count; i++)
            {
                float target = sel.Contains(i) ? 1 : 0; if (w[i] == target && target == 0) continue;
                w[i] = Mathf.MoveTowards(w[i], target, dt * 1.6f);
                if (w[i] > 0 && !slotOf.ContainsKey(i)) { int s = FreeSlot(); if (s < 0) { w[i] = 0; continue; } slotOf[i] = s; var l = slots[s]; l.transform.SetPositionAndRotation(lamps[i].p, lamps[i].rot); l.colorTemperature = lamps[i].cct; l.range = lamps[i].range; l.spotAngle = lamps[i].angle; l.innerSpotAngle = lamps[i].inner; }
                if (w[i] <= 0 && slotOf.TryGetValue(i, out var s2)) { slots[s2].enabled = false; slots[s2].intensity = 0; slots[s2].shadows = LightShadows.None; slotOf.Remove(i); }
            }
            // sombra nas mais próximas (já acesas por inteiro). r65: a sombra ENTRA e SAI devagar (força 0→0,9 em ~0,7 s) e quem já tem
            // sombra fica com vantagem (histerese) — antes ligava/desligava de uma vez e a sombra "brotava" na frente do jogador
            var near = slotOf.Keys.Where(i => w[i] > .95f).OrderBy(i => (lamps[i].p - cp).sqrMagnitude * (sw[i] > 0 ? .7f : 1)).Take(maxShadow).ToList();
            var nearSet = near.ToHashSet(); var hi = near.Take(6).ToHashSet();
            Lit = 0;
            foreach (var kv in slotOf)
            {
                float life = IsCut(kv.Key) ? 0 : dead[kv.Key] ? (flk[kv.Key] > 0 && flkOn[kv.Key] ? Random.Range(.6f, 1.1f) : 0) : 1;
                var l = slots[kv.Value]; l.intensity = lamps[kv.Key].intensity * k * w[kv.Key] * life; l.enabled = l.intensity > .01f; if (l.enabled) Lit++;
                int i = kv.Key; sw[i] = Mathf.MoveTowards(sw[i], nearSet.Contains(i) ? 1 : 0, dt * 1.4f);
                var sh = sw[i] > 0 ? LightShadows.Soft : LightShadows.None; if (l.shadows != sh) l.shadows = sh;
                l.shadowStrength = .9f * Mathf.SmoothStep(0, 1, sw[i]);
                var ad = l.GetUniversalAdditionalLightData(); int tier = hi.Contains(i) ? UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium : UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow;
                if (ad && ad.additionalLightsShadowResolutionTier != tier) ad.additionalLightsShadowResolutionTier = tier;
            }
            for (int i = 0; i < sw.Length; i++) if (!slotOf.ContainsKey(i)) sw[i] = 0;
        }
        // diagnóstico (vitrine -fbgfx): seleção, slots, peso máximo, luminária mais perto
        public static string Diag()
        {
            var cam = Camera.main; float nd = 1e9f; if (cam) foreach (var l in lamps) nd = Mathf.Min(nd, (l.p - cam.transform.position).magnitude);
            float wmax = 0; if (w != null) foreach (var x in w) wmax = Mathf.Max(wmax, x);
            return $"postes sel {sel.Count} slots {slotOf.Count}/{slots.Count} vivos {slots.Count(x => x)} wmax {wmax:0.00} mais perto {nd:0} m alcance {maxDist:0} K {K}";
        }
        static float[] sw = new float[0];
        // r65: COOKIE de luminária pública de LED (sem IES no URP — o perfil vira textura): distribuição de rua tipo II/III — larga ao longo
        // da via (eixo x do spot), lançada para o lado da rua (+y, o 'away' do Aim) e cortada do lado da casa/poste; "asa de morcego"
        // (borda mais forte que o centro, compensa a queda cos³ no chão → piso iluminado por igual em vez de bola no pé do poste).
        static Texture2D ledCookie;
        static Texture2D LedCookie()
        {
            if (ledCookie) return ledCookie; const int N = 128; ledCookie = new Texture2D(N, N, TextureFormat.R8, false, true) { wrapMode = TextureWrapMode.Clamp, name = "cookie_led_rua" };
            var px = new byte[N * N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float u = (x + .5f) / N * 2 - 1, v = (y + .5f) / N * 2 - 1;                // -1..1 no cone
                float vs = v > -.05f ? (v + .05f) / 1.05f : (v + .05f) / .5f;                    // lado da casa (v<0) encolhido
                float e = Mathf.Sqrt(u * u / (1f * 1f) + vs * vs / (.92f * .92f));
                float edge = 1 - Mathf.SmoothStep(.78f, 1f, e);
                float bat = .62f + .38f * Mathf.Clamp01(e * e * 1.25f);
                px[y * N + x] = (byte)Mathf.RoundToInt(255 * Mathf.Clamp01(edge * bat));
            }
            ledCookie.SetPixelData(px, 0); ledCookie.Apply(false, true); return ledCookie;
        }
        static int FreeSlot() { var used = new HashSet<int>(slotOf.Values); for (int s = 0; s < slots.Count; s++) if (!used.Contains(s)) return s; return -1; }
        // direção do facho: 16 raios horizontais curtos (2,8 m) a 0,5 e 1,4 m abaixo da luminária acham o poste/parede; o facho inclina
        // tilt graus para o lado oposto. Sem nada perto (luminária solta) = reto para baixo. tilt 0 = comportamento antigo.
        static Quaternion Aim(Vector3 p, float tilt, out float used)
        {
            used = 0; var down = Quaternion.Euler(90, 0, 0); if (tilt <= 0) return down;
            Vector3 acc = Vector3.zero; int hits = 0;
            foreach (float dy in new[] { .5f, 1.4f })
                for (int k = 0; k < 16; k++)
                {
                    float a = k * Mathf.PI / 8; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    if (Physics.Raycast(p + Vector3.down * dy, d, out var h, 2.8f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)) { acc += d * (1 - h.distance / 2.8f); hits++; }
                }
            if (hits == 0 || acc.sqrMagnitude < 1e-4f) return down;
            var away = -acc.normalized; float t = tilt * Mathf.Deg2Rad; used = tilt;
            var dir = (Vector3.down * Mathf.Cos(t) + away * Mathf.Sin(t)).normalized;
            return Quaternion.LookRotation(dir, away);
        }
        public static int Aimed => lamps.Count(l => l.tilt > 0);
    }

    public static class FBAccentLights
    {
        enum Kind { Fonte, Portal, PortalVerde, Palmeira, Letreiro }
        struct A { public Light l; public Kind k; public float i; public Vector3 cell; }
        static readonly List<A> list = new();

        static Light Make(Transform parent, string name, LightType t, Vector3 p, Quaternion r, float range, float angle = 60, float inner = 30)
        {
            var l = new GameObject(name).AddComponent<Light>(); l.transform.SetParent(parent); l.transform.SetPositionAndRotation(p, r);
            l.type = t; l.range = range; if (t == LightType.Spot) { l.spotAngle = angle; l.innerSpotAngle = inner; }
            l.shadows = LightShadows.None; l.intensity = 0; l.enabled = false; l.renderMode = LightRenderMode.ForcePixel; return l;
        }
        static Renderer[] MapRenderers() => Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.gameObject.layer == FBWorld.LMap && r.enabled).ToArray();

        static Transform root; static bool fontes;
        // fontes: um refletor colorido por grupo de 6 m de jatos (mesmas células da paleta do shader das gotas). Os jatos vêm do FBWater
        // (level.fountains); se a água ainda não montou nesta cena, tenta de novo no Tick.
        static void BuildFountains()
        {
            if (fontes || !root || FBWater.JetsScene != UnityEngine.SceneManagement.SceneManager.GetActiveScene() || FBWater.LastJets.Count == 0) return; fontes = true;
            bool hi = FBQuality.Level >= FBQLevel.Alto;
            var cells = new Dictionary<Vector2Int, (Vector3 s, int n, float lo)>();
            foreach (var j in FBWater.LastJets) { var c = new Vector2Int(Mathf.FloorToInt(j.p.x / 6), Mathf.FloorToInt(j.p.z / 6)); cells.TryGetValue(c, out var v); cells[c] = (v.s + j.p, v.n + 1, v.n == 0 ? j.p.y : Mathf.Min(v.lo, j.p.y)); }
            foreach (var kv in cells.Take(hi ? 40 : 16))
            {
                var c = kv.Value.s / kv.Value.n; var l = Make(root, "FonteRefletor", LightType.Point, new Vector3(c.x, kv.Value.lo + .9f, c.z), Quaternion.identity, 8f);
                list.Add(new A { l = l, k = Kind.Fonte, i = 6, cell = new Vector3(kv.Key.x, 0, kv.Key.y) }); FBVolumetric.Sources.Add(l);
            }
            Debug.Log($"[FreeBala][luz] fontes: {cells.Count} grupos de jatos, {list.Count(a => a.k == Kind.Fonte)} refletores coloridos");
        }

        public static void Build(Transform parent, string map)
        {
            list.Clear(); root = null; if (FBQuality.Mobile) return;                                     // mobile: só o brilho emissivo das fontes/lâmpadas
            bool hi = FBQuality.Level >= FBQLevel.Alto; var rs = MapRenderers();
            root = parent; fontes = false; BuildFountains();
            // Portal do Milênio: dois refletores quentes em cada pé, para cima, + verde rasante (fotos noturnas)
            foreach (var r in rs.Where(r => r.name.Contains("Portal_Milenio")))
            {
                var b = r.bounds; bool alongX = b.size.x > b.size.z; var ax = alongX ? Vector3.right : Vector3.forward; float half = (alongX ? b.extents.x : b.extents.z) - .9f;
                foreach (var sgn in new[] { -1f, 1f })
                {
                    var foot = new Vector3(b.center.x, b.min.y + .25f, b.center.z) + ax * (half * sgn);
                    var side = Vector3.Cross(Vector3.up, ax);
                    foreach (var s2 in new[] { -1f, 1f })
                    {
                        var l = Make(parent, "PortalRefletor", LightType.Spot, foot + side * (1.6f * s2) - ax * (.6f * sgn), Quaternion.LookRotation((foot + Vector3.up * b.size.y * .9f - (foot + side * (1.6f * s2))).normalized), b.size.y * 1.4f, 34, 16);
                        l.useColorTemperature = true; l.colorTemperature = 3200; if (hi) { l.shadows = LightShadows.Soft; l.shadowNearPlane = .3f; FBLightUtil.MediumShadows(l); } list.Add(new A { l = l, k = Kind.Portal, i = 70 });
                    }
                    var g = Make(parent, "PortalVerde", LightType.Point, foot + Vector3.up * .3f - ax * (1.2f * sgn), Quaternion.identity, 5); g.color = new Color(.2f, 1f, .45f); list.Add(new A { l = g, k = Kind.PortalVerde, i = 5 });
                }
                break;
            }
            // palmeiras iluminadas de baixo (verde-claro, como na praça real)
            if (hi)
                foreach (var r in rs.Where(r => r.name.Contains("Palmeira")))
                {
                    var b = r.bounds; if (b.size.y < 3) continue; var p = new Vector3(b.center.x, b.min.y + .2f, b.center.z) + new Vector3(.7f, 0, .4f);
                    var l = Make(parent, "PalmeiraRefletor", LightType.Spot, p, Quaternion.LookRotation((new Vector3(b.center.x, b.max.y - 1, b.center.z) - p).normalized), b.size.y * 1.15f, 46, 18);
                    l.color = new Color(.78f, 1f, .82f); list.Add(new A { l = l, k = Kind.Palmeira, i = 16 });
                }
            // letreiro "EU AMO BOA VISTA": dois refletores à frente (lado SE = -X no Unity), sem tocar no letreiro
            foreach (var r in rs.Where(r => r.name.StartsWith("PA_Letreiro_Base")))
            {
                var b = r.bounds; var face = Vector3.left; var along = b.size.z > b.size.x ? Vector3.forward : Vector3.right; var tgt = b.center + Vector3.up * 2.9f;
                foreach (var s in new[] { -1f, 1f })
                {
                    var p = new Vector3(b.center.x, b.min.y + 1.6f, b.center.z) + face * 10f + along * (s * Mathf.Max(2.5f, (along == Vector3.forward ? b.extents.z : b.extents.x) * .45f));
                    var l = Make(parent, "LetreiroRefletor", LightType.Spot, p, Quaternion.LookRotation((tgt - p).normalized), 22, 55, 30);
                    l.useColorTemperature = true; l.colorTemperature = 4800; list.Add(new A { l = l, k = Kind.Letreiro, i = 24 });
                }
                break;
            }
            Debug.Log($"[FreeBala][luz] destaque: {list.Count(a => a.k == Kind.Portal)} portal, {list.Count(a => a.k == Kind.Palmeira)} palmeiras, {list.Count(a => a.k == Kind.Letreiro)} letreiro");
        }

        // mesma paleta/ciclo do FountainPalette (FBWaterCommon.hlsl) — a luz bate com a cor da água
        static readonly Color[] PAL = { new(.15f, .35f, 1f), new(.10f, .95f, .45f), new(1f, .20f, .75f), new(1f, .18f, .12f), new(1f, .62f, .12f), new(.10f, .85f, 1f) };
        public static Color Palette(float h)
        {
            h -= Mathf.Floor(h); float k = h * 6; int i = Mathf.FloorToInt(k) % 6; float f = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.75f, 1, k - Mathf.Floor(k)));
            return Color.Lerp(PAL[i], PAL[(i + 1) % 6], f);
        }
        public static void Tick(float nightK)
        {
            float k = Mathf.InverseLerp(.4f, .95f, nightK); if (!fontes && root) BuildFountains(); float tt = Time.timeSinceLevelLoad;
            foreach (var a in list)
            {
                if (!a.l) continue; a.l.intensity = a.i * k; a.l.enabled = k > .01f;
                if (a.k == Kind.Fonte && a.l.enabled) a.l.color = Palette(a.cell.x * .37f + a.cell.z * .23f + tt * .025f);
            }
        }
    }
}
