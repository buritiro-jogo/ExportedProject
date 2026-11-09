// Céu do Free Bala como skybox (evolução do CLOUD_F do sky.js, mesma paleta de dia do web):
//  · ATMOSFERA guiada pela elevação do sol (_SunDir): zênite/horizonte pela massa de ar, halo de Mie (Henyey-Greenstein) e disco do sol,
//    horizonte laranja do lado do sol + faixa rosada no pôr do sol, bruma abaixo do horizonte. Dia -> pôr do sol -> noite é contínuo:
//    o FBWeather só desce o sol (e sobe a lua); nada troca de uma vez.
//  · NUVENS em camadas: cúmulos 2D procedurais (fbm com distorção de domínio, iluminados por uma amostra na direção do sol, borda
//    prateada contra a luz) + cirros altos esticados pelo vento; tudo deriva com _WindSky. PC alto (_FB_VOLCLOUDS): os cúmulos viram
//    nuvens VOLUMÉTRICAS por raymarch numa camada de 1,6–3 km (ruído 3D _FBCloudVol gerado pelo FBLivingSky). Mobile/Web
//    (_FB_SKY_LITE): menos oitavas, sem amostra de luz, sem cirros.
//  · NOITE: gradiente azul-marinho, poluição luminosa alaranjada no horizonte (Boa Vista), Via Láctea com poeira escura, estrelas em
//    3 camadas giradas entre si (sem grade visível), magnitude de cauda pesada (poucas fortes, muitas fracas), cor por temperatura
//    (maioria branca/amarelada, poucas azuis/vermelhas), tamanho mínimo de 1 pixel com energia conservada (não pisca ao girar a câmera),
//    cintilação mais forte perto do horizonte, extinção pela atmosfera, pela chuva e pelo luar; LUA com fase, mares, crateras, escurecimento
//    do limbo, luz cinérea no lado escuro e halo/coroa proporcionais à fase. Nuvens passam na frente das estrelas e da lua.
//  · CHUVA: céu fechado cinza.
Shader "FreeBala/Sky"
{
    Properties
    {
        _Night ("Noite", Range(0,1)) = 0
        _Dusk ("Por do sol", Range(0,1)) = 0
        _Rain ("Chuva", Range(0,1)) = 0
        _MoonPhase ("Fase da lua", Range(0,1)) = 0.82
        _SunDir ("Sol", Vector) = (-0.5, 0.75, 0.5, 0)
        _MoonDir ("Lua", Vector) = (-0.52, 0.63, 0.57, 0)
        _SkyTime ("Tempo", Float) = 0
        _Cover ("Cobertura de nuvens", Range(0,1)) = 0.45
        _WindSky ("Vento das nuvens (xz)", Vector) = (1, 0.35, 0, 0)
        _FBCloudVol ("Ruido 3D das nuvens", 3D) = "" {}
        _FBPhys ("Ceu fisico (r64)", Range(0,1)) = 0
        _FBSkyExposure ("Exposicao do ceu", Float) = 8
        _FBSunTint ("Tinta do sol", Color) = (1,1,1,1)
        _FBLightPol ("Poluicao luminosa", Range(0,1)) = 0.55
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ _FB_SKY_LITE _FB_VOLCLOUDS
            #include "UnityCG.cginc"
            float _Night, _Dusk, _Rain, _MoonPhase, _SkyTime, _Cover; float4 _SunDir, _MoonDir, _WindSky;
            float _FBPhys, _FBSkyExposure, _FBLightPol; float4 _FBSunTint;
            sampler3D _FBCloudVol;
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.dir = v.vertex.xyz; o.dir.x = -o.dir.x; return o; }   // espelha X: mesmo céu do web (glTF/three.js)

            // ---------- ruído
            float IGN(float2 p) { return frac(52.9829189 * frac(dot(p, float2(.06711056, .00583715)))); }   // ruído de gradiente intercalado (Jimenez), fixo na tela
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float hash3(float3 p) { p = frac(p * .3183099 + .1); p *= 17.; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float3 hash33(float3 p) { return float3(hash3(p), hash3(p + 19.19), hash3(p + 47.3)); }
            float noise(float2 p) { float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f); return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y); }
            float noise3(float3 p)
            {
                float3 i = floor(p), f = frac(p); f = f * f * (3. - 2. * f);
                return lerp(lerp(lerp(hash3(i), hash3(i + float3(1, 0, 0)), f.x), lerp(hash3(i + float3(0, 1, 0)), hash3(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(hash3(i + float3(0, 0, 1)), hash3(i + float3(1, 0, 1)), f.x), lerp(hash3(i + float3(0, 1, 1)), hash3(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }
            float fbm3(float3 p) { float a = .5, s = 0.; for (int i = 0; i < 4; i++) { s += a * noise3(p); p *= 2.03; a *= .5; } return s; }
            float fbm2(float2 p, int oct)
            {
                float a = .5, s = 0., n = 0.; float2x2 R = float2x2(.8, .6, -.6, .8);
                for (int i = 0; i < 6; i++) { if (i >= oct) break; s += a * noise(p); n += a; p = mul(R, p) * 2.02 + 17.3; a *= .5; }
                return s / n;
            }
            float phaseHG(float mu, float g) { float g2 = g * g; return (1. - g2) / (12.566 * pow(max(1. + g2 - 2. * g * mu, 1e-4), 1.5)); }

            // ---------- atmosfera (dia e pôr do sol)
            float3 SunColor(float sh) { return lerp(float3(1.0, .40, .14), float3(1.0, .96, .88), smoothstep(-.02, .38, sh)); }
            float3 Atmosphere(float3 d, float3 sd, float3 sunC)
            {
                float sh = sd.y, up = max(d.y, 0.), mu = dot(d, sd);
                float dayK = smoothstep(-.12, .22, sh);
                float3 zen = lerp(float3(.035, .06, .14), float3(.16, .39, .65), dayK);
                float3 hor = lerp(float3(.30, .31, .44), float3(.68, .76, .79), smoothstep(-.05, .30, sh));
                float lowSun = (1. - smoothstep(.0, .32, sh)) * smoothstep(-.18, -.01, sh);
                float toward = pow(saturate(dot(normalize(float3(d.x, 0, d.z) + 1e-5), normalize(float3(sd.x, 0, sd.z) + 1e-5)) * .5 + .5), 3.);
                hor = lerp(hor, float3(1.0, .53, .27), lowSun * (.22 + .78 * toward));
                float3 sky = lerp(zen, hor, pow(1. - up, 2.6));
                sky = lerp(sky, float3(.64, .43, .56), lowSun * exp(-up * 8.) * (1. - toward) * .35);   // cinturão rosado do lado oposto
                if (d.y < 0.) sky = lerp(hor, hor * .8, saturate(-d.y * 4.));
                sky *= .93 + .1 * mu * mu;                                                             // Rayleigh
                float vis = smoothstep(-.10, .04, sh);
                sky += sunC * (.035 * phaseHG(mu, .72) + .010 * phaseHG(mu, .95)) * vis;               // Mie: halo + auréola
                sky += sunC * smoothstep(.99983, .99991, mu) * 5. * vis * smoothstep(-.02, .02, d.y);  // disco do sol (HDR -> bloom)
                return sky;
            }

            // ---------- r64: atmosfera FÍSICA (espalhamento simples Rayleigh + Mie + ozônio, terra esférica) — o mesmo modelo do
            // FBAtmosphere.cs que acende a cena; resultado LINEAR em unidades do sol (= 1), multiplicado por _FBSkyExposure (adaptação).
            // 10 passos na visada (quadráticos: mais perto do chão) × 4 passos até o sol. Mobile/Web (_FB_SKY_LITE) seguem no modelo antigo.
            static const float PRg = 6360e3, PRt = 6460e3, PHR = 8000., PHM = 1200.;
            static const float3 PBR = float3(5.802e-6, 13.558e-6, 33.1e-6), PBO = float3(0.650e-6, 1.881e-6, 0.085e-6);
            float _FBTurb; float PTurb() { return _FBTurb > 0. ? _FBTurb : 1.5; }
            float2 RaySphere(float3 o, float3 d, float R) { float b = dot(o, d), c = dot(o, o) - R * R, h = b * b - c; if (h < 0.) return float2(-1, -1); h = sqrt(h); return float2(-b - h, -b + h); }
            float3 PDepth(float3 p, float3 s, int n)
            {
                float2 ts = RaySphere(p, s, PRt); float L = max(ts.y, 0.);
                float2 tg = RaySphere(p, s, PRg); if (tg.x > 0.) return float3(1e9, 1e9, 1e9);       // atrás da terra
                float ds = L / n; float3 acc = 0;
                [loop] for (int i = 0; i < n; i++) { float3 q = p + s * ((i + .5) * ds); float hh = length(q) - PRg; acc += float3(exp(-hh / PHR), exp(-hh / PHM), max(0., 1. - abs(hh - 25000.) / 15000.)) * ds; }
                return acc;
            }
            float3 PhysSky(float3 d, float3 s)
            {
                float3 o = float3(0, PRg + 2., 0);
                float L = RaySphere(o, d, PRt).y; float2 tg = RaySphere(o, d, PRg); if (tg.x > 0.) L = tg.x; L = min(L, 300e3);
                float nu = dot(d, s), pR = 3. / (16. * 3.14159) * (1. + nu * nu), g = .8, g2 = g * g;
                float pM = 3. / (8. * 3.14159) * ((1. - g2) * (1. + nu * nu)) / ((2. + g2) * pow(max(1e-4, 1. + g2 - 2. * g * nu), 1.5));
                float tb = PTurb(); float3 accR = 0, accM = 0, od = 0; float prev = 0.; const int NV = 10;
                [loop] for (int i = 1; i <= NV; i++)
                {
                    float t = L * (i / (float)NV) * (i / (float)NV), ds = t - prev, tm = (t + prev) * .5; prev = t;
                    float3 p = o + d * tm; float hh = length(p) - PRg; if (hh < 0.) break;
                    float3 e = float3(exp(-hh / PHR), exp(-hh / PHM), max(0., 1. - abs(hh - 25000.) / 15000.)) * ds; od += e;
                    float3 ld = PDepth(p, s, 4); if (ld.x > 1e8) continue;
                    float3 tau = PBR * (od.x + ld.x) + 4.40e-6 * tb * (od.y + ld.y) + PBO * (od.z + ld.z);
                    float3 T = exp(-tau); accR += T * e.x; accM += T * e.y;
                }
                float3 col = accR * PBR * pR + accM * (3.996e-6 * tb * pM);
                // disco do sol (transmitância na visada) — HDR para o bloom; some atrás da terra
                float3 Ts = exp(-(PBR * od.x + 4.40e-6 * tb * od.y + PBO * od.z));
                col += Ts * smoothstep(.99985, .99993, nu) * 900. * step(0., d.y + .002);
                return col;
            }

            // ---------- nuvens 2D (cúmulos + cirros); alpha = cobertura
            float3 CloudLight(float3 d, float3 sd, float3 sunC, float dens, float shade, float night, float halo)
            {
                float3 lit = lerp(float3(.98, .98, .96), sunC * 1.05, .35), dark = lerp(float3(.50, .56, .65), sunC * .45, .22);
                float3 col = lerp(dark, lit, shade);
                col += sunC * pow(saturate(dot(d, sd)), 6.) * (1. - dens) * .55 * smoothstep(-.05, .1, sd.y);   // borda prateada contra o sol
                float up = max(d.y, 0.);
                float3 nl = lerp(float3(.035, .045, .065), float3(.11, .115, .14), shade) + float3(.42, .52, .70) * halo * .35;
                nl += float3(.20, .11, .05) * exp(-up * 6.) * .5;                                         // cidade iluminando a base das nuvens
                return lerp(col, nl, night);
            }
            float4 Cumulus(float3 d, float3 sd, float3 sunC, float night, float halo)
            {
                float2 wind = _WindSky.xy * _SkyTime * .0035;
                float2 uv = d.xz / (d.y + .14) * .55 + wind;
                float cov = lerp(.60, .36, _Cover) - _Rain * .25;
                #if defined(_FB_SKY_LITE)
                float n = fbm2(uv * 2., 3);
                float dens = smoothstep(cov, cov + .24, n);
                float shade = lerp(.95, .45, dens);
                #else
                float2 q = float2(fbm2(uv * 1.3, 3), fbm2(uv * 1.3 + 5.2, 3));
                float n = fbm2(uv * 2. + q * .8 + wind * .5, 5);
                float dens = smoothstep(cov, cov + .22, n);
                float n2 = fbm2((uv + normalize(sd.xz + 1e-4) * .05) * 2. + q * .8 + wind * .5, 4);
                float shade = saturate(1.05 - (n2 - n) * 3.4 - dens * .35);
                #endif
                float a = dens * smoothstep(-.02, .14, d.y);
                return float4(CloudLight(d, sd, sunC, dens, shade, night, halo), a);
            }
            float4 Cirrus(float3 d, float3 sd, float3 sunC, float night)
            {
                float2 w = normalize(_WindSky.xy + 1e-4); float2x2 R = float2x2(w.x, w.y, -w.y, w.x);
                float2 cu = mul(R, d.xz / (d.y + .05) * .2) + float2(_SkyTime * .006, 0);
                float c = fbm2(cu * float2(.7, 4.2), 4);
                float a = smoothstep(.52, .82, c) * .38 * smoothstep(.0, .2, d.y) * (1. - _Rain * .7);
                float3 col = lerp(lerp(float3(1, 1, 1), sunC, .45), float3(.09, .10, .13), night);
                return float4(col, a);
            }

            #if defined(_FB_VOLCLOUDS)
            // ---------- nuvens volumétricas (PC alto): raymarch numa camada 1,6–3 km, 22 passos + 2 de luz, ruído 3D em textura
            float VolDensity(float3 p, float3 wo, float cov)
            {
                float hgt = saturate((p.y - 1600.) / 1400.);
                float prof = saturate(hgt * 4.) * saturate((1. - hgt) * 2.2);
                float base = tex3Dlod(_FBCloudVol, float4((p + wo) * .00011, 0)).r;
                float det = tex3Dlod(_FBCloudVol, float4((p + wo * 1.7) * .00047, 0)).r;
                float dn = saturate((base * prof - cov) * 3.2);
                return saturate(dn - det * .32 * (1. - dn));
            }
            float4 VolClouds(float3 d, float3 sd, float3 sunC, float night, float halo, float jit)
            {
                if (d.y < .015) return float4(0, 0, 0, 0);
                float t0 = 1600. / d.y, t1 = min(3000. / d.y, 60000.); const int N = 28;   // jitter FIXO por pixel (antes mudava a cada quadro com _SkyTime: sem TAA virava granulação animada no céu)
                float dt = (t1 - t0) / N;   // (estava dentro do comentário acima: a variante _FB_VOLCLOUDS não compilava)
                float3 wo = float3(_WindSky.x, 0, _WindSky.y) * _SkyTime * 7.;
                float cov = lerp(.62, .30, _Cover) - _Rain * .25;
                float3 amb = lerp(lerp(float3(.55, .63, .74), sunC * .5, .25), float3(.04, .05, .07), night);
                float3 sunL = lerp(sunC * 1.25 * smoothstep(-.06, .08, sd.y), float3(.30, .36, .50) * halo * 2., night);
                float3 lightDir = night > .5 ? normalize(_MoonDir.xyz) : sd;
                float T = 1.; float3 acc = 0;
                [loop] for (int i = 0; i < N; i++)
                {
                    float3 p = d * (t0 + (i + jit) * dt);
                    float den = VolDensity(p, wo, cov);
                    if (den > .01)
                    {
                        float l = VolDensity(p + lightDir * 160., wo, cov) + VolDensity(p + lightDir * 420., wo, cov);
                        float Tl = exp(-l * 1.7), powder = 1. - exp(-den * 4.);
                        float hgt = saturate((p.y - 1600.) / 1400.);
                        float3 S = sunL * Tl * powder + amb * (.45 + .4 * hgt);
                        float a = 1. - exp(-den * dt * .0032);
                        acc += T * a * S; T *= 1. - a;
                        if (T < .03) break;
                    }
                }
                float fade = smoothstep(.015, .12, d.y);
                return float4(acc * fade, (1. - T) * fade);
            }
            #endif

            // ---------- estrelas
            float3 RotY(float3 v, float a) { float c = cos(a), s = sin(a); return float3(c * v.x + s * v.z, v.y, -s * v.x + c * v.z); }
            float3 RotX(float3 v, float a) { float c = cos(a), s = sin(a); return float3(v.x, c * v.y - s * v.z, s * v.y + c * v.z); }
            float3 StarLayer(float3 d, float sc, float dens, float bright, float twAmp, float seed, float pixAng)
            {
                float3 p = d * sc + seed, c = floor(p), h = hash33(c + seed);
                if (h.x > dens) return float3(0, 0, 0);
                float3 sp = c + .15 + .7 * hash33(c * 1.7 + 3.1 + seed); float3 dv = p - sp; float r2 = dot(dv, dv);
                float m = pow(h.y, 10.) * .95 + .05 * h.y;                                     // cauda pesada: poucas fortes
                float pix = pixAng * sc;                                                         // tamanho do pixel em células (derivada fora dos ramos)
                float s0 = .03 + .05 * m, s = max(s0, pix * .8);
                float core = exp(-r2 / (s * s)) * (s0 * s0) / (s * s);                          // >= 1 pixel, energia conservada
                float t = h.z;
                float3 col = t < .12 ? float3(.66, .78, 1.) : t < .70 ? float3(1., .97, .92) : t < .93 ? float3(1., .86, .66) : float3(1., .70, .52);
                float tw = noise(float2(_SkyTime * (2.3 + 3. * h.x), dot(c, float3(1.3, 7.1, 3.7))));
                float amp = twAmp * (.25 + .75 * (1. - saturate(d.y * 1.6)));                   // mais atmosfera perto do horizonte
                return col * core * (m * bright + .03) * (1. + amp * (tw * 2. - 1.));
            }

            // ---------- lua (textura procedural fixa no disco: mares + crateras)
            float MoonAlbedo(float3 n)
            {
                float maria = smoothstep(.47, .63, fbm3(n * 2.1 + 4.1));
                float alb = lerp(.97, .60, maria);
                float3 p = n * 5.5, c = floor(p), h = hash33(c), cc = c + .25 + .5 * h; float r = length(p - cc), R = .16 + .22 * h.x;
                if (h.y > .35) alb += -smoothstep(R, R * .55, r) * .16 + exp(-pow((r - R) / (R * .2), 2.)) * .10;
                p = n * 13.; c = floor(p); h = hash33(c + 9.); cc = c + .25 + .5 * h; r = length(p - cc); R = .12 + .2 * h.x;
                if (h.y > .45) alb += -smoothstep(R, R * .5, r) * .10 + exp(-pow((r - R) / (R * .22), 2.)) * .07;
                return alb * (.92 + .08 * noise3(n * 34.));
            }

            half4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir); float h = saturate(d.y * .5 + .5), up = max(d.y, 0.);
                float pixAng = length(fwidth(d)) * .5;                                            // derivadas só aqui, fora de qualquer ramo
                float3 sunDir = normalize(_SunDir.xyz), moonDir = normalize(_MoonDir.xyz);
                float night = _Night;
                float3 sunC = SunColor(sunDir.y);
                float3 sky;
                #if !defined(_FB_SKY_LITE)
                if (_FBPhys > .5)
                {
                    // céu físico em linear → espaço "gamma" do resto do shader (nuvens/noite misturam lá; o fim converte de volta)
                    float3 dd = d; if (dd.y < 0.) dd = normalize(float3(d.x, max(d.y, -.04) * .25, d.z));   // abaixo do horizonte: bruma clara (o chão do mapa cobre)
                    float3 lin = PhysSky(dd, sunDir) * _FBSkyExposure;
                    lin = lin / (1. + lin * .04);                                                     // ombro suave só no disco do sol
                    sky = pow(max(lin, 0.), 1. / 2.2);
                    sunC = pow(max(_FBSunTint.rgb, 1e-4), 1. / 2.2);
                }
                else
                #endif
                {
                    sky = Atmosphere(d, sunDir, sunC);
                    // compatibilidade: _Dusk ainda puxa um pouco de laranja para o horizonte (ANOITECER)
                    sky = lerp(sky, sky * float3(1.05, .93, .86), _Dusk * (1. - night) * exp(-up * 4.) * .5);
                }

                float moonUp = smoothstep(-.05, .1, moonDir.y), md = dot(d, moonDir);
                float phaseB = .25 + .75 * abs(_MoonPhase);
                float halo = (pow(max(md, 0.), 22.) * .18 + pow(max(md, 0.), 420.) * .45) * moonUp * phaseB;
                if (night > .001)
                {
                    float3 nsky = lerp(float3(.040, .060, .105), float3(.010, .018, .050), smoothstep(.0, .75, h));
                    float lpK = _FBPhys > .5 ? _FBLightPol / .55 : 1.;
                    nsky += float3(.30, .17, .08) * exp(-up * 9.) * .42 * lpK;                   // poluição luminosa da cidade (r64: pelo preset)
                    nsky += float3(.05, .10, .07) * exp(-up * 14.) * .25;
                    nsky += float3(.42, .52, .70) * halo;
                    float ext = smoothstep(.02, .30, d.y) * (1. - _Rain) * (1. - .55 * saturate(halo * 3.)) * (1. - .55 * saturate(lpK * .55 - .25) * smoothstep(.6, .0, d.y));   // r64: cidade apaga as estrelas baixas
                    float3 mwN = normalize(float3(.42, .36, .83)); float bd = dot(d, mwN);
                    float band = exp(-bd * bd * 16.); float cl = fbm3(d * 6.) * 1.4, dust = smoothstep(.45, .75, fbm3(d * 11. + 3.));
                    float mw = band * saturate(cl - .25) * (1. - dust * .75); float3 mwc = lerp(float3(.55, .62, .85), float3(.95, .86, .70), saturate(cl - .5));
                    float moonWash = 1. - .45 * moonUp * abs(_MoonPhase);                              // luar apaga as fracas
                    nsky += (mwc * mw * .42 + float3(.30, .34, .48) * band * .035) * ext * moonWash;
                    float3 st = StarLayer(d, 70., .50 + band * .15, 2.4, .55, 0., pixAng)
                              + StarLayer(RotX(RotY(d, 1.3), .7), 150., .62 + band * .2, 1.0, .35, 31.7, pixAng) * moonWash
                              + StarLayer(RotY(RotX(d, 2.1), 2.6), 310., .70 + band * .25, .5, 0., 77.1, pixAng) * (.45 + band * 1.6) * moonWash;
                    float ma = acos(clamp(md, -1., 1.)), mr = .026; float mask = 0.; float3 mc = 0;
                    if (ma < mr * 1.3 && moonUp > 0.)
                    {
                        float3 ax = normalize(cross(moonDir, float3(0., 1., 0.))), ay = cross(ax, moonDir); float2 q = float2(dot(d, ax), dot(d, ay)) / mr;
                        float rr = dot(q, q), aa = max(pixAng / mr * 3., .02);
                        mask = 1. - smoothstep(1. - aa, 1., rr);
                        float3 nrm = float3(q, sqrt(saturate(1. - rr))); float ph = _MoonPhase * 3.14159; float3 L = normalize(float3(sin(ph), 0., -cos(ph)));
                        float lit = smoothstep(-.05, .10, dot(nrm, L)), limb = .80 + .20 * nrm.z;
                        mc = float3(.97, .95, .89) * MoonAlbedo(nrm) * (lit * 1.45 * limb + .028);  // + luz cinérea
                    }
                    nsky += st * ext * (1. - mask);
                    nsky = lerp(nsky, mc, mask * moonUp);
                    sky = lerp(sky, nsky, night);
                }

                // nuvens: cúmulos (2D ou volumétricos) e cirros por cima do céu, estrelas e lua
                float4 ci = float4(0, 0, 0, 0);
                #if !defined(_FB_SKY_LITE)
                ci = Cirrus(d, sunDir, sunC, night); sky = lerp(sky, ci.rgb, ci.a);
                #endif
                #if defined(_FB_VOLCLOUDS)
                float4 vc = VolClouds(d, sunDir, sunC, night, halo, .5 * IGN(i.pos.xy) + .25);
                sky = sky * (1. - vc.a) + vc.rgb;
                float cloudA = vc.a;
                #else
                float4 cu = Cumulus(d, sunDir, sunC, night, halo);
                sky = lerp(sky, cu.rgb, cu.a * .9);
                float cloudA = cu.a;
                #endif
                sky += float3(.42, .52, .70) * halo * cloudA * .35 * night;                          // luar atravessando nuvem fina

                float3 rsky = lerp(float3(.46, .49, .52), float3(.33, .36, .40), smoothstep(.0, .7, h)) * (1. - night * .86);
                rsky = lerp(rsky, rsky * .8, cloudA);
                sky = lerp(sky, rsky, _Rain * .92);
                #if !defined(UNITY_COLORSPACE_GAMMA)
                sky = GammaToLinearSpace(max(sky, 0.));
                #endif
                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
}
