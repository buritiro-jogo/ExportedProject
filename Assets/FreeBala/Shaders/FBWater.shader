// Água do Free Bala (espelhos d'água das fontes, Rio Branco, brejo/lagoa) — URP, transparente, sem textura de cor: tudo procedural.
//  · normais em 2 camadas do mesmo normal map tileável, deslizando em direções/escala diferentes; no rio as duas seguem o FLUXO
//    (_Flow.xy = direção no mundo XZ, z = velocidade m/s, w = 0 lago … 1 correnteza) e a espuma corre junto;
//  · 3ª camada pequena e rápida só onde há jatos (_Agitation) — a lâmina da fonte treme;
//  · reflexo: reflection probe/céu do URP (GlossyEnvironmentReflection) com fresnel de Schlick; sem probe usa _SkyTint;
//  · sol/lua: especular forte e estreito + brilho largo (luz principal do URP — à noite o FBWeather põe a lua nela);
//  · cor raso → fundo e espuma de margem pela PROFUNDIDADE real (_CameraDepthTexture) quando FB_WATER_DEPTH está ligado (PC/
//    celular com depth texture); sem ela, a cor sai do ângulo de visão (rasante = mais fundo) e não há espuma de borda;
//  · FB_WATER_LITE (qualidade baixa): 1 amostra de normal, sem agitação, sem espuma por textura.
// Keywords globais (multi_compile: as 4 variantes vão no build): o FBWater liga FB_WATER_DEPTH/FB_WATER_LITE conforme a qualidade.
// r65 (PC, _FBPuddleSSR = 1 — textura opaca da câmera): água FÍSICA em vez de cor chapada + alfa:
//  · REFRAÇÃO: o fundo/margem visto através da água (textura opaca deslocada pela normal);
//  · ABSORÇÃO de Beer-Lambert pela coluna d'água real (depth): o azul some primeiro → rio barrento de Boa Vista cor de chá/barro,
//    nunca piscina cristalina; ESPALHAMENTO da turbidez (cor de fundo do material) iluminado pelo céu e pelo sol;
//  · REFLEXO em espaço de tela (pontes, prédios, árvores, margem, luzes) com volta para a sonda do céu do horário;
//  · brilho especular GGX do sol/lua E dos postes/faróis (luzes adicionais do Forward+) — à noite a água pega as luzes da orla.
Shader "FreeBala/Water"
{
    Properties
    {
        _ShallowColor("Raso (rgb, alfa)", Color) = (0.30, 0.62, 0.70, 0.42)
        _DeepColor("Fundo (rgb, alfa)", Color) = (0.04, 0.22, 0.36, 0.90)
        _SkyTint("Céu sem probe", Color) = (0.58, 0.72, 0.85, 1)
        [NoScaleOffset][Normal] _NormalMap("Normais (tileável)", 2D) = "bump" {}
        _NormalScale("Força das normais", Range(0, 2)) = 0.7
        _Tiling("Tamanho das ondas (m) camada 1, 2", Vector) = (2.6, 7, 0, 0)
        _Speed("Deriva das camadas (m/s) 1, 2", Vector) = (0.07, 0.045, 0, 0)
        _Flow("Fluxo xy=dir XZ, z=vel m/s, w=0 lago 1 rio", Vector) = (0, 1, 0, 0)
        _Agitation("Agitação (jatos)", Range(0, 1)) = 0
        _DepthRange("Profundidade até a cor de fundo (m)", Float) = 0.9
        _Murk("Turbidez", Range(0, 1)) = 0.15
        [NoScaleOffset] _FoamTex("Espuma (tileável)", 2D) = "black" {}
        _FoamColor("Cor da espuma", Color) = (0.93, 0.95, 0.95, 1)
        _FoamDist("Espuma da margem (m)", Float) = 0.3
        _FoamAmount("Quantidade de espuma", Range(0, 1)) = 0.55
        _Smoothness("Lisura", Range(0, 1)) = 0.94
        _SpecBoost("Brilho do sol/lua", Float) = 3
        _ReflStrength("Força do reflexo", Range(0, 1)) = 0.8
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ FB_WATER_LITE
            #pragma multi_compile _ FB_WATER_DEPTH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "FBScreenRefl.hlsl"
            float _FBWaterPbr;                                             // r65: FBVisual (waterPbr) — 0 = água da r64

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _SkyTint, _FoamColor;
                float4 _Tiling, _Speed, _Flow;
                half _NormalScale, _Agitation, _DepthRange, _Murk, _FoamDist, _FoamAmount, _Smoothness, _SpecBoost, _ReflStrength;
            CBUFFER_END
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_FoamTex); SAMPLER(sampler_FoamTex);

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float2 Rot(float2 d, float a) { float s = sin(a), c = cos(a); return float2(d.x * c - d.y * s, d.x * s + d.y * c); }
            half3 SampleN(float2 uv, half scale) { return UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv), scale); }

            half4 WaterNew(Varyings i, half3 N, half3 V, half fres, float2 uv1, float2 uv2, half river);
            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y; float2 xz = i.positionWS.xz;
                // ---- direções das camadas: lago = cruzadas fixas; rio = ao longo do fluxo (±20°)
                float2 fdir = normalize(_Flow.xy + float2(1e-5, 0)); half river = saturate(_Flow.w);
                float2 d1 = normalize(lerp(float2(0.8, 0.6), Rot(fdir, 0.35), river));
                float2 d2 = normalize(lerp(float2(-0.55, 0.83), Rot(fdir, -0.3), river));
                float s1 = _Speed.x + _Flow.z * river, s2 = _Speed.y + _Flow.z * 0.7 * river;
                float2 uv1 = (xz + d1 * t * s1) / max(0.05, _Tiling.x);
                float2 uv2 = (xz + d2 * t * s2) / max(0.05, _Tiling.y) + 0.37;
                half3 n1 = SampleN(uv1, _NormalScale);
            #if defined(FB_WATER_LITE)
                half3 nT = n1;
            #else
                half3 n2 = SampleN(uv2, _NormalScale * 0.8);
                half3 nT = half3(n1.xy + n2.xy, n1.z * n2.z);
                if (_Agitation > 0.01)                                     // tremor dos jatos: ondinhas curtas e rápidas
                {
                    half3 n3 = SampleN(xz / 0.55 + float2(t * 0.41, -t * 0.37), _NormalScale * 1.4 * _Agitation);
                    half3 n4 = SampleN(xz / 0.83 + float2(-t * 0.33, t * 0.29) + 0.5, _NormalScale * 1.1 * _Agitation);
                    nT.xy += n3.xy + n4.xy;
                }
            #endif
                half3 N = normalize(half3(nT.x, max(0.2, nT.z), nT.y));     // espaço tangente (z para cima) → mundo (y para cima)
                half3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half NdV = saturate(dot(N, V));
                half fres = 0.02 + 0.98 * pow(1 - NdV, 5);
            #if !defined(FB_WATER_LITE)
                if (_FBPuddleSSR > 0.5 && _FBWaterPbr > 0.5) return WaterNew(i, N, V, fres, uv1, uv2, river);
            #endif

                // ---- profundidade (raso → fundo) e espuma da margem
                half deepK, edgeFoam = 0, soft = 1;
            #if defined(FB_WATER_DEPTH) && !defined(FB_WATER_LITE)
                float2 suv = i.screenPos.xy / i.screenPos.w;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float thick = max(0, sceneEye - i.screenPos.w);            // água atravessada pelo olhar até o fundo
                float column = thick * saturate(abs(V.y) + 0.15);              // aproximação da coluna vertical
                deepK = saturate(column / max(0.05, _DepthRange));
                edgeFoam = 1 - saturate(column / max(0.02, _FoamDist));
                soft = saturate(thick / 0.06);                               // borda sem serrilhado onde a água encosta
            #else
                deepK = saturate(0.45 + 0.5 * (1 - abs(V.y)));               // sem depth: rasante parece mais fundo
            #endif
                deepK = saturate(deepK + _Murk * 0.35);
                half4 body = lerp(_ShallowColor, _DeepColor, deepK);

                // ---- luz: corpo d'água pela luz ambiente + sol/lua; reflexo do céu/probe; especular
                Light ml = GetMainLight();
                half3 amb = SampleSH(half3(0, 1, 0));
                half3 lit = body.rgb * (amb * 0.9 + ml.color * saturate(ml.direction.y + 0.15) * 0.55);
                half3 R = reflect(-V, N);
                half3 env = GlossyEnvironmentReflection(R, 1 - _Smoothness, 1);
                half envL = dot(env, half3(0.3, 0.59, 0.11));
                env = lerp(_SkyTint.rgb * (amb * 1.2 + 0.25), env, saturate(envL * 6));     // sem probe: tom de céu iluminado pela cena
                half3 col = lerp(lit, env * _ReflStrength, fres);
                half rl = saturate(dot(R, ml.direction));
                col += ml.color * (pow(rl, 900 * _Smoothness * _Smoothness + 40) * _SpecBoost * 6 + pow(rl, 60) * 0.12 * _SpecBoost) * saturate(ml.direction.y * 4 + 0.2);

                // ---- espuma: margem (depth) + filetes na correnteza do rio, carregados pelo fluxo
                half foam = 0;
            #if !defined(FB_WATER_LITE)
                half fA = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, uv1 * 1.7 + float2(0.13, 0.71)).r;
                half fB = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, uv2 * 2.3).r;
                half fm = fA * fB;
                foam = saturate(edgeFoam * 1.6 - (1 - fm) * 1.1) * _FoamAmount;
                foam += smoothstep(0.55, 0.9, fm) * 0.35 * river * _FoamAmount;   // espuma solta descendo o rio
                foam += smoothstep(0.6, 0.95, fA) * 0.25 * _Agitation * _FoamAmount;  // bolhas onde os jatos caem
                foam = saturate(foam);
            #endif
                col = lerp(col, _FoamColor.rgb * (amb + ml.color * 0.6), foam);

                half a = lerp(body.a, 1, fres * 0.7);
                a = saturate(max(a, foam)) * soft;
                col = MixFog(col, i.fogFactor);
                return half4(col, a);
            }
            // r65: caminho físico (ver cabeçalho)
            half4 WaterNew(Varyings i, half3 N, half3 V, half fres, float2 uv1, float2 uv2, half river)
            {
                float2 suv = i.screenPos.xy / i.screenPos.w;
                float sceneEye = FBSceneEye(suv); float thick = max(0, sceneEye - i.screenPos.w);
                float column = thick * saturate(abs(V.y) + 0.15);
                half soft = saturate(thick / 0.06);
                // refração: o deslocamento some no raso (sem "vazar" a margem por cima da água)
                float2 ruv = suv + N.xz * 0.035 * saturate(column / 0.5);
                if (FBSceneEye(ruv) < i.screenPos.w) ruv = suv;                      // amostra caiu em algo NA FRENTE da água: sem refração
                half3 bed = FBSceneColor(ruv);
                // absorção: coeficiente por canal a partir da cor rasa (o que ela não tem é o que a água come) × turbidez
                half3 sigma = (1.15 - _ShallowColor.rgb) * (0.6 + 2.4 * _Murk) / max(0.05, _DepthRange);
                half3 T = exp(-sigma * (thick * 0.5 + column * 0.5));
                Light ml = GetMainLight(); half3 amb = SampleSH(half3(0, 1, 0));
                half3 litIn = amb * 0.9 + ml.color * saturate(ml.direction.y + 0.15) * 0.55;
                half3 scatter = _DeepColor.rgb * litIn;                                // turbidez iluminada (barro em suspensão)
                half3 body = bed * T + scatter * (1 - T);
                // reflexo: tela → sonda/céu
                float rough = saturate(1 - _Smoothness) * 0.8 + 0.02;
                half3 R = reflect(-V, N);
                half3 env = GlossyEnvironmentReflection(R, i.positionWS, rough, 1, suv);
                half envL = dot(env, half3(0.3, 0.59, 0.11)); env = lerp(_SkyTint.rgb * (amb * 1.2 + 0.25), env, saturate(envL * 6));
                half4 ssr = FBSSRTrace(i.positionWS, R, 90);
                half3 refl = lerp(env, ssr.rgb, ssr.a) * _ReflStrength;
                half3 col = lerp(body, refl, fres);
                // brilho: sol/lua (o mesmo de antes, estreito + largo) e luzes adicionais (GGX)
                half rl = saturate(dot(R, ml.direction));
                col += ml.color * (pow(rl, 900 * _Smoothness * _Smoothness + 40) * _SpecBoost * 6 + pow(rl, 60) * 0.12 * _SpecBoost) * saturate(ml.direction.y * 4 + 0.2);
                float a2 = max(rough * rough, 0.0016); a2 *= a2;
            #if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)
                InputData inputData = (InputData)0; inputData.positionWS = i.positionWS; inputData.normalizedScreenSpaceUV = suv;
                uint cnt = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(cnt)
                    Light al = GetAdditionalLight(lightIndex, i.positionWS);
                    col += min(FBSpecGGX(al.color * al.distanceAttenuation, al.direction, V, N, a2, fres), 30.0);
                    col += _DeepColor.rgb * al.color * al.distanceAttenuation * saturate(al.direction.y) * 0.08 * (1 - T.g);   // luz entrando na água turva
                LIGHT_LOOP_END
            #endif
                // espuma (igual ao caminho antigo, sem a da margem rasa por textura de profundidade inexistente)
                half fA = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, uv1 * 1.7 + float2(0.13, 0.71)).r;
                half fB = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, uv2 * 2.3).r; half fm = fA * fB;
                half edgeFoam = 1 - saturate(column / max(0.02, _FoamDist));
                half foam = saturate(edgeFoam * 1.6 - (1 - fm) * 1.1) * _FoamAmount;
                foam += smoothstep(0.55, 0.9, fm) * 0.35 * river * _FoamAmount;
                foam += smoothstep(0.6, 0.95, fA) * 0.25 * _Agitation * _FoamAmount;
                col = lerp(col, _FoamColor.rgb * (amb + ml.color * 0.6), saturate(foam));
                col = MixFog(col, i.fogFactor);
                return half4(col, soft);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
