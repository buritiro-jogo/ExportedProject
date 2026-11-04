// Horário e clima por mapa (porte do weather.js) + ambiente sonoro (ambience.js):
//  · DIA / NOITE / ANOITECER (pôr do sol escurecendo durante a partida) × LIMPO / CHUVA, escolhidos no menu e salvos por mapa;
//  · o sol ANDA de verdade: dia a 48°; ANOITECER começa na hora dourada (13°) e desce até -14° — luz alaranjada e rasante, sombras longas,
//    céu (FBSky) pela elevação do sol; no crepúsculo só a luz ambiente; depois a LUA (luz azulada, outra direção) sobe devagar. Tudo
//    contínuo: luz, ambiente, neblina, névoa de altura, céu e reflexos acompanham o mesmo parâmetro (sem troca brusca);
//  · noite: céu escuro, neblina escura, postes acesos (cabeça dos *_poste_br achada pela geometria: brilho + poça de luz no chão; um pool
//    FIXO de 6 luzes reais vai para os postes mais perto da câmera — regra anti-travada);
//  · chuva: céu fechado, neblina mais perto, gotas com colisão/respingos/poças (FBRain), chão MOLHADO (asfalto/calçada/concreto mais escuros e brilhantes,
//    restaurados ao sair), vento mais forte nas árvores, som da chuva (abafado sob laje).
//  · névoa de altura (FBHeightFog, PC) e partículas ambientes (FBAmbientFx: insetos nos postes, poeira no sol, fumaça) acompanham o clima;
//    pássaros (FBBirds: pombos no chão/telhados que revoam, urubus planando) — à noite empoleirados, na chuva quase sem voo.
//  · ambiente: camadas por mapa (pássaros/cidade/rua/vento/assobio/noite/chuva) + sons pontuais (cachorros, galo, crianças, pombos, avião,
//    sino, sirene longe) nas bordas do mapa — rojão removido a pedido (26/10); tiros calam os pássaros por ~14 s; coberto (laje/casa) abafa.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public static class FBWeather
    {
        public static bool Night, Rain, Dusk; static float duskT; static Light sun; static readonly List<Light> heads = new(); static List<Vector3> lamps;
        static Color sunCol, ambSky, ambEq, ambGr, fogCol; static float sunI, fogNear, fogFar, groundY, cover; static Vector2 wind;
        static ReflectionProbe[] probes; static float[] probeI;
        public static IReadOnlyList<Vector3> Lamps => lamps;                                   // FBAmbientFx (insetos)
        public static float NightK => curN;
        public static void Apply((string time, string weather) w)
        {
            Night = w.time == "noite"; Dusk = w.time == "anoitecer"; Rain = w.weather == "chuva"; duskT = 0;
            lamps = null; cones = null; RestoreWet(); wet.Clear();                    // cena nova: o que era da partida anterior foi destruído
            sun = RenderSettings.sun ? RenderSettings.sun : Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            if (!sun) return; sunCol = sun.color; sunI = sun.intensity; ambSky = RenderSettings.ambientSkyColor; ambEq = RenderSettings.ambientEquatorColor; ambGr = RenderSettings.ambientGroundColor;
            fogCol = RenderSettings.fogColor; fogNear = RenderSettings.fogStartDistance; fogFar = RenderSettings.fogEndDistance;
            // [mirante-ultra] ULTRA no PC: a névoa linear das arenas (70–512 m) cobria a cidade vista do alto do Mirante ("sopa cinza").
            // O horizonte das arenas tem 3 km: no Ultra a névoa começa a 150 m e fecha em ~1,1 km (o resto do clima segue igual)
            if (FBUltra.Ativo) { fogNear = Mathf.Max(fogNear, 70) * 2.2f; fogFar = Mathf.Max(fogFar, 512) * 2.6f; }
            probes = Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None); probeI = probes.Select(p => p.intensity).ToArray();
            cover = Random.Range(.3f, .62f); float wa = Random.Range(0, Mathf.PI * 2); wind = new Vector2(Mathf.Cos(wa), Mathf.Sin(wa));
            var go = new GameObject("FB_Clima"); go.AddComponent<FBWeatherTicker>();
            Bounds? mb = null;
            { foreach (var c in Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None)) { if (c.gameObject.layer != FBWorld.LMap) continue; if (mb == null) mb = c.bounds; else { var b = mb.Value; b.Encapsulate(c.bounds); mb = b; } } FBLivingSky.Start(mb); }
            groundY = FBMatch.Player ? FBMatch.Player.Pos.y : mb?.min.y ?? 0;
            FBGfx.SetWind(wind, Rain ? 1.7f : Random.Range(.8f, 1.15f), groundY);
            if (Rain) FBRain.Begin(go.transform, groundY);                                        // gotas com colisão, respingos, poças (FBRain)
            UnityEngine.Rendering.GraphicsSettings.lightsUseColorTemperature = true;
            if (sun) { sun.useColorTemperature = true; sun.colorTemperature = 5400; sun.shadows = LightShadows.Soft; }   // sol de tarde: branco-quente, sombra suave (vídeo 2 / referências)
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) if (root.name.StartsWith("Map_")) FBStreetPoles.Clean(root);   // antes de achar as luminárias: poste na pista não acende
            string mapId = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Replace("Map_", ""); lampCct = mapId.Contains("praca_aguas") ? 5200 : 3000;
            FBVolumetric.Clear(); FBVolumetric.InScene = true; FBVolumetric.GroundY = groundY;
            FindWet(); lamps = FindLamps(); FindLampMats();
            FBStreetLights.Build(lamps, go.transform, mapId); FBAccentLights.Build(go.transform, mapId);   // luz real em cada luminária + fontes/portal/palmeiras/letreiro (FBLightRig)
            BuildCones();
            heads.Clear(); FBCarLights.Reset(); FBWindowLights.Build();   // janelas acesas à noite (interior + luz vazando)
            LightVolume();
            Set(Night ? 1 : 0);
            FBAmbientFx.Start(go.transform, mb);
            FBBirds.Begin(go.transform, groundY, Night, Rain);                                           // pombos (chão/telhados, revoada) e urubus planando
        }

        // n: 0 dia … 1 noite
        static void Set(float n)
        {
            if (!sun) return; float rainK = Rain ? 1 : 0;
            // sol (elevação em graus) e lua
            float el = Mathf.Lerp(Dusk ? 13 : 38, -14, Mathf.SmoothStep(0, 1, Mathf.Clamp01(n / .62f)));
            var sunRot = Quaternion.Euler(Mathf.Max(el, 3.5f), -35, 0); var moonRot = Quaternion.Euler(40, 150, 0);
            float sunUp = Mathf.Clamp01(Mathf.InverseLerp(-1.5f, 9f, el)), lowSun = 1 - Mathf.InverseLerp(3, 28, el);
            float moonK = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.5f, .85f, n));
            var dayC = Color.Lerp(sunCol, new Color(.75f, .75f, .78f), rainK * .6f);
            if (sunUp > .001f || moonK <= 0)
            {
                sun.transform.rotation = sunRot; sun.color = Color.Lerp(dayC, new Color(1, .6f, .36f), lowSun * (1 - rainK * .6f) * .85f);
                sun.intensity = sunI * (1 - rainK * .55f) * Mathf.Pow(sunUp, .8f) * Mathf.Lerp(1, .78f, lowSun); sun.shadowStrength = Rain ? .35f : .9f;
            }
            else
            {
                sun.transform.rotation = moonRot; sun.color = new Color(.58f, .68f, 1f);                       // luar azulado
                sun.intensity = sunI * .13f * moonK * (1 - rainK * .6f); sun.shadowStrength = .55f;
            }
            float dk = Mathf.Clamp01(Mathf.Max(n, (1 - sunUp) * .8f));                                     // escuridão do ambiente
            var warm = lowSun * (1 - dk) * (1 - rainK);
            RenderSettings.ambientSkyColor = Color.Lerp(Color.Lerp(Color.Lerp(ambSky, new Color(.55f, .58f, .62f), rainK * .5f), new Color(.70f, .60f, .62f), warm * .35f), new Color(.24f, .29f, .42f), dk);   // céu da noite: azul visível (jogável)
            RenderSettings.ambientEquatorColor = Color.Lerp(Color.Lerp(Color.Lerp(ambEq, new Color(.45f, .46f, .48f), rainK * .5f), new Color(.72f, .55f, .45f), warm * .3f), new Color(.17f, .19f, .27f), dk);
            RenderSettings.ambientGroundColor = Color.Lerp(ambGr, new Color(.07f, .07f, .09f), dk);
            var fc = Color.Lerp(Color.Lerp(Color.Lerp(fogCol, new Color(.55f, .58f, .62f), rainK * .7f), new Color(.86f, .62f, .48f), warm * .45f), new Color(.02f, .03f, .06f), dk);
            RenderSettings.fog = true; RenderSettings.fogColor = fc;
            RenderSettings.fogStartDistance = Mathf.Lerp(fogNear * .75f, fogNear * .4f, Mathf.Max(n * .6f, rainK * .7f)); RenderSettings.fogEndDistance = Mathf.Lerp(fogFar * .85f, fogFar * .45f, Mathf.Max(n * .5f, rainK * .6f));   // dia com névoa atmosférica (referência)
            // névoa de altura (FBHeightFog, PC): bruma baixa de dia, mais densa à noite (fumaça da cidade) e na chuva
            Shader.SetGlobalColor("_FBFogColor", Color.Lerp(fc, Color.white, .08f * (1 - dk)));
            Shader.SetGlobalColor("_FBFogSunColor", sunUp > .001f ? Color.Lerp(fc, sun.color, .55f) : Color.Lerp(fc, new Color(.35f, .42f, .6f), .35f * moonK));
            Shader.SetGlobalVector("_FBFogParams", new Vector4(Mathf.Lerp(Mathf.Lerp(.0026f, .0034f, dk), .0065f, rainK), .045f, groundY, Mathf.Lerp(.5f, .78f, Mathf.Max(dk, rainK))));
            Shader.SetGlobalVector("_FBFogSun", new Vector4(-sun.transform.forward.x, -sun.transform.forward.y, -sun.transform.forward.z, (1 - rainK * .7f) * (sunUp > .001f ? .9f : .35f)));
            // reflexos (probe assada de dia): apagam junto com o céu
            if (probes != null) for (int i = 0; i < probes.Length; i++) if (probes[i]) probes[i].intensity = probeI[i] * Mathf.Lerp(1, .12f, dk) * (1 - rainK * .3f);
            // reflexo do ambiente (skybox) e luz ambiente também apagam à noite — antes ficavam no nível do dia e carros/contêineres/grama brilhavam demais
            RenderSettings.reflectionIntensity = Mathf.Lerp(1, .02f, dk) * (1 - rainK * .2f); RenderSettings.ambientIntensity = Mathf.Lerp(1, .4f, dk);   // noite ESCURA: quem ilumina são os postes
            NightGrade(dk); ApplyWet(Mathf.Max(rainK, Mathf.SmoothStep(0, .7f, Mathf.InverseLerp(.3f, 1, n))));
            // a SONDA de luz ambiente (o que de fato ilumina os objetos) não é recalculada no player quando as cores mudam: os prédios
            // ficavam com a luz ambiente do DIA à noite. Monta a sonda direto das 3 cores (céu/horizonte/chão) × intensidade
            { var k = RenderSettings.ambientIntensity; var sh = new UnityEngine.Rendering.SphericalHarmonicsL2(); var eq = RenderSettings.ambientEquatorColor * k;
              sh.AddAmbientLight(eq); sh.AddDirectionalLight(Vector3.up, (RenderSettings.ambientSkyColor * k - eq) * .9f, 1); sh.AddDirectionalLight(Vector3.down, (RenderSettings.ambientGroundColor * k - eq) * .9f, 1);
              RenderSettings.ambientProbe = sh; DynamicGI.UpdateEnvironment(); }
            if (RenderSettings.skybox && RenderSettings.skybox.HasProperty("_Exposure")) RenderSettings.skybox.SetFloat("_Exposure", Mathf.Lerp(Rain ? .55f : 1.15f, .12f, n));
            if (RenderSettings.skybox && RenderSettings.skybox.HasProperty("_Tint")) RenderSettings.skybox.SetColor("_Tint", Color.Lerp(Rain ? new Color(.6f, .62f, .66f) : new Color(.5f, .5f, .5f), new Color(.2f, .25f, .45f), n));
            // luz volumétrica (PC): ar mais limpo de dia (raios do sol só entre árvores/prédios), bruma urbana à noite (halo e facho dos postes),
            // chuva enche o ar; o sol/lua espalham com a cor e a força que estão iluminando a cena
            FBVolumetric.Density = Mathf.Lerp(Mathf.Lerp(.0016f, .0062f, dk), .0105f, rainK); FBVolumetric.Falloff = Mathf.Lerp(.05f, .035f, dk);
            FBVolumetric.G = Mathf.Lerp(.62f, .4f, dk); FBVolumetric.MaxDist = Mathf.Lerp(160, 120, dk); FBVolumetric.LampK = Rain ? 3.6f : 2.6f;
            FBVolumetric.MainColor = sun.color.linear * sun.intensity * (sunUp > .001f ? Mathf.Lerp(.55f, .9f, lowSun) : 1.4f) * (1 - rainK * .5f);
            FBVolumetric.MainK = (sunUp > .001f ? Mathf.Lerp(.55f, .9f, lowSun) : 1.4f) * (1 - rainK * .5f);
            FBVolumetric.MainDir = -sun.transform.forward; FBVolumetric.Ambient = RenderSettings.ambientEquatorColor.linear * RenderSettings.ambientIntensity * .55f;
            curN = n;
            if (FBLivingSky.I) FBLivingSky.I.SetWeather(n, Rain, Dusk ? 1 : 0, Quaternion.Euler(el, -35, 0) * Vector3.back, moonRot * Vector3.back, cover, wind);   // céu vivo (sky.js)
        }
        static float curN, duskLt, lampCct = 3000; static GameObject cones; static Material coneMat;
        // cone de luz dos postes na névoa (referência: facho visível do poste até o chão): uma malha só com um cone aberto por luminária,
        // aditiva, degradê forte em cima e sumindo no chão; acende com a noite (mais forte na chuva/neblina)
        static void BuildCones()
        {
            cones = null; if (lamps == null || lamps.Count == 0) return;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>(); const int SEG = 18;
            var col = new List<Color>();
            foreach (var p in lamps)
            {
                var lc = FBStreetLights.GlowColorAt(p);
                float gy = Physics.Raycast(p + Vector3.down * .3f, Vector3.down, out var h, 30, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) ? h.point.y : p.y - 8;
                float H = Mathf.Max(2, p.y - gy), R0 = .18f, R1 = Mathf.Clamp(H * .42f, 2, 5.5f); int b = v.Count;
                for (int i = 0; i <= SEG; i++)
                {
                    float a = i / (float)SEG * Mathf.PI * 2; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    v.Add(p + d * R0 + Vector3.down * .05f); uv.Add(new Vector2(i / (float)SEG, 1)); v.Add(new Vector3(p.x, gy + .05f, p.z) + d * R1); uv.Add(new Vector2(i / (float)SEG, 0)); col.Add(lc); col.Add(lc);
                }
                for (int i = 0; i < SEG; i++) { int a0 = b + i * 2; t.AddRange(new[] { a0, a0 + 1, a0 + 2, a0 + 2, a0 + 1, a0 + 3 }); }
            }
            var m = new Mesh { name = "cones_postes", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; m.SetVertices(v); m.SetUVs(0, uv); m.SetColors(col); m.SetTriangles(t, 0); m.RecalculateBounds();
            var tex = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) { float k = y / 63f; float a = Mathf.Pow(k, 2.2f) * .9f + .1f * k; for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(1, 1, 1, a)); }
            tex.Apply(); coneMat = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit), true); coneMat.SetTexture("_BaseMap", tex); coneMat.mainTexture = tex; coneMat.SetFloat("_Cull", 0); coneMat.renderQueue = 3010;
            cones = new GameObject("FB_ConesPostes"); cones.AddComponent<MeshFilter>().sharedMesh = m; var mr = cones.AddComponent<MeshRenderer>(); mr.sharedMaterial = coneMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; cones.SetActive(false);
        }
        // volume global de iluminação da partida (não havia nenhum Volume na cena: só o perfil padrão do URP, com tonemapping DESLIGADO e
        // bloom 0 — imagem lavada e a noite nunca escurecia). Vídeos de referência: tonemapping, contraste, bloom nas luzes; aqui Neutral
        // (o ACES escurecia o dia — rejeitado antes). A noite (NightGrade) mexe neste perfil: exposição, saturação, luar, bloom.
        static void LightVolume()
        {
            var go = new GameObject("FB_Iluminacao"); var v = go.AddComponent<UnityEngine.Rendering.Volume>(); v.isGlobal = true; v.priority = 50;
            var p = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>(); p.name = "FB_Iluminacao"; v.profile = p;
            var tm = p.Add<UnityEngine.Rendering.Universal.Tonemapping>(true); tm.mode.value = UnityEngine.Rendering.Universal.TonemappingMode.Neutral;
            // [parceiro 31/10] exposição base 0,25 -> 0 (o parceiro tirou o ganho de exposição do volume; a noite continua somando o -0,45 do NightGrade)
            var ca = p.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true); ca.postExposure.value = 0; ca.contrast.value = 8; ca.saturation.value = 6; ca.colorFilter.value = Color.white;
            var bl = p.Add<UnityEngine.Rendering.Universal.Bloom>(true); bl.threshold.value = 1.05f; bl.intensity.value = FBQuality.Mobile ? .15f : .3f; bl.scatter.value = .65f; bl.highQualityFiltering.value = !FBQuality.Mobile;
            var vg = p.Add<UnityEngine.Rendering.Universal.Vignette>(true); vg.intensity.value = .18f; vg.smoothness.value = .45f;
            gradeBase.Clear(); bloomBase.Clear();
            FBVolumetric.AttachProfile(p);                                                           // névoa volumétrica (pacote URP Volumetric Fog) no mesmo volume
        }
        // números reais da iluminação (vitrine -fbgfx): sol/lua, ambiente, sonda, exposição, luzes acesas
        public static string Diag()
        {
            var sh = RenderSettings.ambientProbe; string post = "";
            foreach (var v in Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None)) if (v && v.isGlobal && v.profile && v.profile.TryGet<UnityEngine.Rendering.Universal.ColorAdjustments>(out var ca)) post += $" vol {v.name} w{v.weight:0.##} exp {(ca.postExposure.overrideState ? ca.postExposure.value : 0):0.##} ativo {ca.active}";
            var ls = Object.FindObjectsByType<Light>(FindObjectsSortMode.None); int on = ls.Count(l => l.enabled && l.intensity > .01f && l.type != LightType.Directional);
            var dirs = string.Join(" ", ls.Where(l => l.type == LightType.Directional).Select(l => $"{l.name}:{(l.enabled ? l.intensity : 0):0.###}"));
            return $"n {curN:0.##} · dir {dirs} · amb {RenderSettings.ambientMode} k {RenderSettings.ambientIntensity:0.##} céu {RenderSettings.ambientSkyColor} sonda {sh[0, 0]:0.###}/{sh[1, 0]:0.###}/{sh[2, 0]:0.###} · refl {RenderSettings.reflectionIntensity:0.##} · luzes {on} postes {lamps?.Count ?? 0} (reais {FBStreetLights.Lit}; {FBStreetLights.Diag()}) · volumétrica {(FBVolumetric.Active && FBVolumetric.InScene ? $"sim dens {FBVolumetric.Density:0.0000} fontes {FBVolumetric.Sources.Count}" : "não")} · {post}";
        }
        // luminárias do mapa (cabeças dos postes, lâmpadas, refletores): emissão quente que acende com a noite — com o bloom vira o
        // brilho da lâmpada; a luz que ilumina o chão vem dos spots dos postes (Tick). Restaurado ao trocar de cena.
        static readonly Dictionary<Material, (bool on, Color c)> lampMats = new();
        static void FindLampMats()
        {
            RestoreLampMats(); var rx = new System.Text.RegularExpressions.Regex("lamp|lampada|luminaria|refletor|light_on|_on$|farol_poste|_light$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);   // _light$: luzes coloridas das fontes (PA_blue_light…)
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r.gameObject.layer != FBWorld.LMap) continue;
                foreach (var m in r.sharedMaterials) if (m && !lampMats.ContainsKey(m) && rx.IsMatch(m.name) && m.HasProperty("_EmissionColor")) lampMats[m] = (m.IsKeywordEnabled("_EMISSION"), m.GetColor("_EmissionColor"));
            }
        }
        public static void RestoreLampMats() { foreach (var kv in lampMats) if (kv.Key) { kv.Key.SetColor("_EmissionColor", kv.Value.c); if (!kv.Value.on) kv.Key.DisableKeyword("_EMISSION"); } lampMats.Clear(); }
        static void LampGlow(float k)
        {
            var c = Mathf.CorrelatedColorTemperatureToRGB(lampCct) * (3.5f * k);
            foreach (var kv in lampMats) { if (!kv.Key) continue; if (k > .01f) { kv.Key.EnableKeyword("_EMISSION"); kv.Key.SetColor("_EmissionColor", kv.Value.c.maxColorComponent > .05f && !kv.Key.name.ToLower().Contains("lamp") ? Accent(kv.Value.c, k) : kv.Value.c + c); }   /* luz colorida (fonte): realça a própria cor */ else { kv.Key.SetColor("_EmissionColor", kv.Value.c); if (!kv.Value.on) kv.Key.DisableKeyword("_EMISSION"); } }
        }
        // luz colorida das fontes/refletores (*_light): a cor normalizada e um teto de brilho — antes ×6 sobre a emissão do glTF: as muretas e
        // bordas das fontes estouravam em branco à noite (retorno do usuário). Branco (pouca saturação) brilha menos ainda.
        static Color Accent(Color c0, float k)
        {
            float m = Mathf.Max(1e-3f, c0.maxColorComponent); var n = c0 / m; float sat = 1 - Mathf.Min(n.r, Mathf.Min(n.g, n.b));
            return n * ((.35f + 1.05f * k) * Mathf.Lerp(.45f, 1f, sat));
        }
        // noite de verdade: exposição e saturação caem no pós-processamento (o tonemapping clareava a cena noturna)
        static readonly System.Collections.Generic.Dictionary<UnityEngine.Rendering.VolumeProfile, (float exp, float sat, Color f)> gradeBase = new();
        static readonly Dictionary<UnityEngine.Rendering.VolumeProfile, (float i, float t)> bloomBase = new();
        static void NightGrade(float dk)
        {
            foreach (var v in Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None))
            {
                if (!v || !v.isGlobal) continue; var prof = v.profile; if (!prof) continue;
                if (!prof.TryGet<UnityEngine.Rendering.Universal.ColorAdjustments>(out var ca)) ca = prof.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
                if (!gradeBase.TryGetValue(prof, out var b0)) gradeBase[prof] = b0 = (ca.postExposure.overrideState ? ca.postExposure.value : 0, ca.saturation.overrideState ? ca.saturation.value : 0, ca.colorFilter.overrideState ? ca.colorFilter.value : Color.white);
                ca.postExposure.overrideState = true; ca.postExposure.value = b0.exp + Mathf.Lerp(0, -.45f, dk);       // ajuste do dia (FBGfx) preservado
                ca.saturation.overrideState = true; ca.saturation.value = b0.sat + Mathf.Lerp(0, -30, dk);
                ca.colorFilter.overrideState = true; ca.colorFilter.value = b0.f * Color.Lerp(Color.white, new Color(.78f, .85f, 1f), dk);   // luar azulado
                if (prof.TryGet<UnityEngine.Rendering.Universal.Bloom>(out var bl))            // noite: lâmpadas e janelas "estouram" no bloom
                { if (!bloomBase.TryGetValue(prof, out var bb)) bloomBase[prof] = bb = (bl.intensity.value, bl.threshold.value); bl.intensity.overrideState = bl.threshold.overrideState = true; bl.intensity.value = Mathf.Lerp(bb.i, Mathf.Max(bb.i, .95f), dk); bl.threshold.value = Mathf.Lerp(bb.t, Mathf.Min(bb.t, .75f), dk); }
            }
        }

        // chão molhado na chuva: materiais de piso do mapa ficam mais escuros e lisos (restaurados ao trocar de cena/sair do Play)
        static readonly Dictionary<Material, (Color c, float s)> wet = new(); static readonly Dictionary<Material, bool> soilM = new(); static float wetK = -1;
        // piso molhado com força k (chuva = 1; noite ≈ 0,7: reflexo das luzes no chão, como na referência)
        static void ApplyWet(float k)
        {
            if (Mathf.Abs(k - wetK) < .02f) return; wetK = k;
            foreach (var kv in wet) { var m = kv.Key; if (!m) continue; var (c, s) = kv.Value; bool soil = soilM.TryGetValue(m, out var so) && so;
                m.SetColor("_BaseColor", Color.Lerp(c, new Color(c.r * .74f, c.g * .74f, c.b * .76f, c.a), k)); m.SetFloat("_Smoothness", Mathf.Lerp(s, Mathf.Max(s, soil ? .32f : .62f), k)); }
        }
        static readonly System.Text.RegularExpressions.Regex WetRx = new("asphalt|asfalto|road|sidewalk|calcada|paver|pav_|piso|concrete|concreto|conc|curb|meiofio|slab|laje|dirt|terra|sand|areia|granite|tile_floor|quadra|court", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        static void FindWet()
        {
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r.gameObject.layer != FBWorld.LMap) continue;
                foreach (var m in r.sharedMaterials)
                {
                    if (!m || wet.ContainsKey(m) || !WetRx.IsMatch(m.name) || !m.HasProperty("_Smoothness") || !m.HasProperty("_BaseColor") || m.renderQueue >= 2450) continue;
                    var c = m.GetColor("_BaseColor"); float s = m.GetFloat("_Smoothness"); wet[m] = (c, s);
                    bool soil = System.Text.RegularExpressions.Regex.IsMatch(m.name, "dirt|terra|sand|areia", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    soilM[m] = soil;
                }
            }
        }
        // globais de shader da partida (névoa de altura) sobrevivem à troca de cena: no menu o passe de tela cheia continuava ligado com a névoa
        // do mapa — o fundo (UI, sem profundidade) virava "céu" e a borda do personagem (profundidade resolvida do MSAA) ganhava contorno
        public static void ResetGlobals() { Shader.SetGlobalVector("_FBFogParams", Vector4.zero); Shader.SetGlobalVector("_FBFogSun", Vector4.zero); }
        public static void RestoreWet() { foreach (var kv in wet) if (kv.Key) { kv.Key.SetColor("_BaseColor", kv.Value.c); kv.Key.SetFloat("_Smoothness", kv.Value.s); } wet.Clear(); soilM.Clear(); wetK = -1; }

        // luminárias: posição da CABEÇA de cada poste (vértices do topo, no fim do braço — não o centro do poste). Malha que junta vários
        // postes (VJ_Postes_Fios) é separada por grade de 3 m: cada coluna com mais de 3 m de altura é um poste; fios (baixos) ficam de fora
        static List<Vector3> FindLamps()
        {
            // medidas na importação (FBLampData): em jogo as malhas não são legíveis (o caminho abaixo pegava o centro de malhas agrupadas)
            var ld = Object.FindObjectsByType<FBLampData>(FindObjectsSortMode.None); var pre = ld.SelectMany(d => d.pos).ToList();
            var sod = new HashSet<Vector3>(ld.SelectMany(d => d.pos.Where((_, i) => d.sodium != null && i < d.sodium.Length && d.sodium[i])));
            FBStreetLights.SodiumAt = p => sod.Contains(p);
            if (pre.Count > 0) return pre;
            var res = new List<Vector3>(); var rx = new System.Text.RegularExpressions.Regex("poste|lamp(_on|ada)|lumin|street_?light", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            void Add(Vector3 h) { if (!res.Any(p => (p - h).sqrMagnitude < 2.5f * 2.5f)) res.Add(h); }
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r.gameObject.layer != FBWorld.LMap || !r.enabled) continue;
                string n = r.name + " " + (r.transform.parent ? r.transform.parent.name : "") + " " + (r.sharedMaterial ? r.sharedMaterial.name : "");
                if (!rx.IsMatch(n)) continue; var b = r.bounds; bool lampHead = n.ToLower().Contains("lamp");
                var mf = r.GetComponent<MeshFilter>(); var mesh = mf ? mf.sharedMesh : null;
                if (mesh && mesh.isReadable)
                {
                    var V = mesh.vertices; var M = r.transform.localToWorldMatrix; var cells = new Dictionary<Vector2Int, (float lo, float hi, List<Vector3> v)>();
                    foreach (var v0 in V) { var v = M.MultiplyPoint3x4(v0); var k = new Vector2Int(Mathf.FloorToInt(v.x / 3), Mathf.FloorToInt(v.z / 3)); if (!cells.TryGetValue(k, out var c)) c = (v.y, v.y, new List<Vector3>()); c.lo = Mathf.Min(c.lo, v.y); c.hi = Mathf.Max(c.hi, v.y); c.v.Add(v); cells[k] = c; }
                    foreach (var c in cells.Values)
                    {
                        if (c.hi - c.lo < 3 && !lampHead) continue;
                        Vector3 s = Vector3.zero; int cnt = 0; foreach (var v in c.v) if (v.y > c.hi - .5f) { s += v; cnt++; }
                        // a luminária fica na ponta do braço: o vértice do topo mais longe do eixo (mais baixo) do poste
                        var top = s / Mathf.Max(1, cnt); Add(new Vector3(top.x, c.hi - .9f, top.z));   // ABAIXO da luminária: dentro dela a sombra do próprio poste apagava o cone
                    }
                    continue;
                }
                if (b.size.y < 3 && !lampHead) continue;
                Add(new Vector3(b.center.x, b.max.y - .9f, b.center.z));
            }
            return res;
        }
        // pula o ANOITECER para 'secs' segundos de partida (vitrine gráfica -fbgfx)
        public static void ForceNight(bool on) { Night = on; Dusk = false; Set(on ? 1 : 0); }   // autoteste (-fbr37): noite/dia na hora
        public static void SkipDusk(float secs) { if (!Dusk) return; duskT = Mathf.Max(0, secs - .01f); duskLt = 0; Tick(.02f); }
        public static void Tick(float dt)
        {
            if (Dusk && duskT < 420 && (duskT += dt) > 0 && (duskLt -= dt) <= 0)               // ANOITECER (weather.js): pôr do sol -> crepúsculo -> noite em 7 min
            { duskLt = .25f; float u = duskT / 420f; Set(Mathf.SmoothStep(0, 1, Mathf.Clamp01((u - .08f) / .84f))); }
            var cam = Camera.main; if (!cam) return;
            if (cones) { float ck = Mathf.InverseLerp(.4f, 1, curN); cones.SetActive(ck > .01f && !(FBVolumetric.Active && FBVolumetric.InScene)); if (coneMat) coneMat.SetColor("_BaseColor", new Color(1, .78f, .5f, 1) * (.09f * ck * (Rain ? 1.6f : 1))); }
            FBCarLights.Tick(cam, curN, Rain, Time.deltaTime); FBWindowLights.Tick(cam, curN, Time.deltaTime);   // farol/lanterna/freio/ré nas posições reais de cada carro (FBCarLights)
            LampGlow(Mathf.InverseLerp(.45f, 1, curN));                   // lâmpadas acesas (a chamada estava dentro do comentário acima: as luminárias nunca acendiam)
            FBStreetLights.Tick(cam, curN, Time.deltaTime); FBAccentLights.Tick(curN); FBVolumetric.Tick(cam);
        }
    }
    public class FBWeatherTicker : MonoBehaviour
    {
        static readonly int __fbt_Update = FBTempo.Id("FBWeatherTicker.Update");   // [parceiro 01/11] medidor FBTempo
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt() { FBWeather.Tick(Time.deltaTime); FBAmbience.Tick(Time.deltaTime); }
        void OnDestroy() { FBWeather.RestoreWet(); FBWeather.RestoreLampMats(); FBVolumetric.Clear(); FBWeather.ResetGlobals(); } }

    public static class FBAmbience
    {
        class P { public float birds, birds2, city, street, wind; public Dictionary<string, float> rate = new(); }
        static readonly Dictionary<string, P> PROFILES = new()
        {
            ["favela"] = new P { birds = .11f, birds2 = .05f, city = .035f, street = .02f, wind = .05f, rate = new() { ["dogs"] = 5, ["rooster"] = 1.2f, ["kids"] = 1.4f, ["pigeon"] = 1.2f, ["plane"] = .35f, ["sino"] = .2f, ["sirene"] = .3f } },
            ["centro"] = new P { birds = .06f, birds2 = .03f, city = .09f, street = .06f, wind = .045f, rate = new() { ["dogs"] = 2, ["rooster"] = .3f, ["kids"] = .9f, ["pigeon"] = 2, ["plane"] = .5f, ["sino"] = .25f, ["sirene"] = .45f } },
            ["parque"] = new P { birds = .13f, birds2 = .08f, city = .04f, street = .03f, wind = .055f, rate = new() { ["dogs"] = 3, ["rooster"] = .4f, ["kids"] = 2, ["pigeon"] = 1.5f, ["plane"] = .4f, ["sino"] = .2f, ["sirene"] = .25f } },
            ["estande"] = new P { birds = .07f, birds2 = .04f, city = .02f, street = 0, wind = .07f, rate = new() { ["dogs"] = 1.2f, ["rooster"] = .3f, ["kids"] = 0, ["pigeon"] = .6f, ["plane"] = .3f } },
        };
        static readonly Dictionary<string, (string[] clips, Vector2 dist, Vector2 h, float vol, float refD)> SHOTS = new()
        {
            ["dogs"] = (Enumerable.Range(0, 10).Select(i => "amb_dog" + i).ToArray(), new(18, 75), new(-2, 2), .9f, 9), ["rooster"] = (new[] { "amb_rooster0", "amb_rooster1" }, new(35, 90), new(0, 8), .8f, 14),
            ["kids"] = (new[] { "amb_kids0", "amb_kids1" }, new(28, 65), new(-1, 4), .55f, 10), ["pigeon"] = (new[] { "amb_pigeon0" }, new(5, 16), new(3, 9), .7f, 4),
            ["plane"] = (new[] { "amb_plane" }, new(60, 160), new(140, 220), .8f, 160),
            // do 171: sirene de viatura passando longe (fica fora do alcance: só o eco do bairro) — sino de igreja removido a pedido (r41)
            ["sirene"] = (new[] { "amb_sirene" }, new(160, 300), new(0, 6), .5f, 90),
        };
        static bool on; static P prof; static readonly Dictionary<string, float> next = new(); static float cover, coverT, cov, hush, baseY; static Bounds bounds;
        public static void Start(string mapId)
        {
            prof = PROFILES[mapId.Contains("praca_aguas") ? "centro" : mapId.Contains("praca_pt") ? "parque" : mapId.Contains("estande") ? "estande" : "favela"]; on = true; hush = 0; cover = 0;
            baseY = FBMatch.Player ? FBMatch.Player.Pos.y : 0; var cols = Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None); if (cols.Length > 0) { bounds = cols[0].bounds; foreach (var c in cols) bounds.Encapsulate(c.bounds); }
            foreach (var s in SHOTS.Keys) next[s] = Gap(s) * Random.Range(.15f, .8f);
            if (!Object.FindAnyObjectByType<FBWeatherTicker>()) new GameObject("FB_Ambiente").AddComponent<FBWeatherTicker>();
        }
        public static void Stop() { on = false; foreach (var k in new[] { "birds", "birds2", "city", "street", "wind", "whistle", "night", "rain", "people" }) FBAudio.StopLoop("amb_" + k); }
        public static void Shot() { if (!on) return; float was = hush; hush = Mathf.Min(1, hush + .35f); }
        static float Gap(string s) { float per = prof.rate.TryGetValue(s, out var r) ? r : 0; return per > 0 ? 60 / per * Random.Range(.5f, 1.5f) : 1e9f; }
        public static void Tick(float dt)
        {
            if (!on || !Camera.main) return; var pos = Camera.main.transform.position;
            if ((coverT -= dt) <= 0) { coverT = .4f; cov = FBWorld.RayHit(pos + Vector3.up * .3f, Vector3.up, 14) ? 1 : 0; }
            cover += (cov - cover) * Mathf.Min(1, dt * 2.5f); if (Time.time - FBBotBrain.lastNoiseT < .1f) Shot(); hush = Mathf.Max(0, hush - dt / 14);
            float outK = 1 - cover * .65f, h = Mathf.Clamp01((pos.y - baseY - 2) / 14); bool N = FBWeather.Night, R = FBWeather.Rain; float birds = (1 - hush * .85f) * outK * (N ? 0 : R ? .15f : 1);
            FBAudio.SetLoop("amb_birds", "amb_bed_birds", prof.birds * birds); FBAudio.SetLoop("amb_birds2", "amb_bed_birds2", prof.birds2 * birds);
            FBAudio.SetLoop("amb_city", "amb_bed_city", prof.city * (1 - cover * .4f)); FBAudio.SetLoop("amb_street", "amb_bed_street", prof.street * (1 - cover * .5f) * (1 - h * .5f));
            FBAudio.SetLoop("amb_wind", "amb_bed_wind", prof.wind * (1 + h * 2.2f) * (1 - cover * .8f)); FBAudio.SetLoop("amb_whistle", "amb_bed_whistle", .05f * h * h * (1 - cover));
            FBAudio.SetLoop("amb_people", "amb_pessoas", (prof == PROFILES["centro"] || prof == PROFILES["parque"]) && !N && !R ? prof.city * 1.6f * (1 - hush * .8f) * outK : 0); FBAudio.SetLoop("amb_night", "amb_bed_night", N && !R ? .3f * (1 - cover * .6f) : 0); FBAudio.SetLoop("amb_rain", "amb_bed_rain", R ? .55f * (1 - cover * .45f) : 0);
            foreach (var s in SHOTS.Keys.ToList())
            {
                if ((next[s] -= dt) > 0) continue; next[s] = Gap(s);
                if (s == "pigeon" && cover > .5f) continue; if ((s == "kids" || s == "rooster" || s == "pigeon") && (hush > .3f || N || R)) continue; if (s == "sino" && N) continue;
                if (s == "plane") { FBSkyPlanes.Flyover(pos, baseY); continue; }   // avião de verdade passando no céu, com o som junto (skyplanes.js)
                var S = SHOTS[s]; Vector3 p;
                if (s == "dogs" || s == "kids" || s == "rooster") { int side = Random.Range(0, 4); float x = Random.Range(bounds.min.x + 5, bounds.max.x - 5), z = Random.Range(bounds.min.z + 5, bounds.max.z - 5); p = new Vector3(side == 0 ? bounds.min.x + 5 : side == 1 ? bounds.max.x - 5 : x, pos.y + Random.Range(S.h.x, S.h.y), side == 2 ? bounds.min.z + 5 : side == 3 ? bounds.max.z - 5 : z); }
                else { float a = Random.value * Mathf.PI * 2, d = Random.Range(S.dist.x, S.dist.y); p = new Vector3(pos.x + Mathf.Cos(a) * d, pos.y + Random.Range(S.h.x, S.h.y), pos.z + Mathf.Sin(a) * d); }
                var src = FBAudio.PlayAt(S.clips[Random.Range(0, S.clips.Length)], p, S.vol * (1 - cover * .45f), S.refD, .12f, 0, 1);
            }
        }
    }
}
