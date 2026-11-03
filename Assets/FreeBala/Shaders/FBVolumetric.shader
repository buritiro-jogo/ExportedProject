// LUZ VOLUMÉTRICA do Free Bala (PC ALTO/ULTRA e PC MÉDIO — FBVolumetricFeature no renderer do PC; mobile/web usam os cones de malha).
// O ar do mapa espalha a luz de verdade: fachos dos postes na névoa, raios do sol entre árvores/prédios (sombra real do sol/lua pelo
// shadow map em cascata), halo das luminárias. Duas passadas:
//   0 "Raymarch" (meia resolução): para cada pixel anda pelo raio da câmera até a superfície (máx. _FBVolParams.w metros) somando a luz
//     espalhada pelo sol/lua com sombra (N passos, deslocados por ruído intercalado) e, para cada luminária, só no trecho do raio que cruza
//     a esfera de alcance dela (6 passos, sem custo onde o raio não passa perto). Densidade exponencial pela altura (bruma baixa).
//     Saída: rgb = luz espalhada, a = transmitância (quanto da cena atrás ainda passa).
//   1 "Composicao" (resolução cheia): cena × transmitância + luz espalhada; o resultado da meia resolução é filtrado em cruz com peso pela
//     diferença de profundidade (não vaza luz na silhueta de poste/árvore e apaga o padrão do ruído).
// Globais (FBVolumetric.cs): _FBVolParams (x densidade/m no chão, y queda por metro de altura, z altura do chão, w distância máx.),
// _FBVolMain (rgb luz do sol/lua já com intensidade, w anisotropia g), _FBVolMainDir (xyz direção PARA a luz), _FBVolAmbient (rgb céu
// espalhado), _FBVolSteps (x passos, y quadro p/ variar o ruído), _FBVolLightCount, _FBVolLightPos[i] (xyz, w alcance),
// _FBVolLightDir[i] (xyz eixo do cone, w cos externo; -2 = luz pontual), _FBVolLightCol[i] (rgb cor×intensidade, w cos interno).
Shader "FreeBala/Volumetric"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        HLSLINCLUDE
        // sombra do sol/lua em cascata direto do shadow map (PC tem 2–4 cascatas); amostra simples (a soma dos passos já suaviza)
        #define _MAIN_LIGHT_SHADOWS_CASCADE 1
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        #define FB_VOL_MAX_LIGHTS 32
        float4 _FBVolParams, _FBVolMain, _FBVolMainDir, _FBVolAmbient, _FBVolSteps;
        float _FBVolLightCount;
        float4 _FBVolLightPos[FB_VOL_MAX_LIGHTS], _FBVolLightDir[FB_VOL_MAX_LIGHTS], _FBVolLightCol[FB_VOL_MAX_LIGHTS];

        float Density(float y) { return _FBVolParams.x * exp(-max(y - _FBVolParams.z, -4.0) * _FBVolParams.y); }
        // Henyey-Greenstein normalizada
        float Phase(float c, float g) { float g2 = g * g; return (1.0 - g2) / (12.5663706 * pow(max(1.0 + g2 - 2.0 * g * c, 1e-3), 1.5)); }
        float IGN(float2 p) { return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715)))); }

        float3 WorldPos(float2 uv, out float rawDepth, out bool sky)
        {
            rawDepth = SampleSceneDepth(uv);
            #if UNITY_REVERSED_Z
            sky = rawDepth <= 1e-6;
            float d = rawDepth;
            #else
            float d = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
            sky = d >= 0.999999;
            #endif
            return ComputeWorldSpacePosition(uv, d, UNITY_MATRIX_I_VP);
        }
        ENDHLSL

        Pass
        {
            Name "Raymarch"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            float4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.texcoord; float raw; bool sky;
                float3 wp = WorldPos(uv, raw, sky);
                float3 ro = _WorldSpaceCameraPos; float3 v = wp - ro; float dist = length(v); float3 rd = v / max(dist, 1e-4);
                float L = sky ? _FBVolParams.w : min(dist, _FBVolParams.w);
                float jit = IGN(i.positionCS.xy + _FBVolSteps.y * 5.588238);

                // --- sol / lua com sombra: densidade × fase × visibilidade, transmitância acumulada (Beer)
                int N = (int)_FBVolSteps.x; float dt = L / N;
                float3 scat = 0; float T = 1;
                float phM = Phase(dot(rd, _FBVolMainDir.xyz), _FBVolMain.w);
                float3 amb = _FBVolAmbient.rgb * 0.0795775;                                    // céu: espalhamento isotrópico (1/4π)
                [loop] for (int s = 0; s < N; s++)
                {
                    float t = (s + jit) * dt; float3 p = ro + rd * t;
                    float sig = Density(p.y);
                    float sh = MainLightRealtimeShadow(TransformWorldToShadowCoord(p));
                    float3 Li = _FBVolMain.rgb * (sh * phM) + amb;
                    float st = exp(-sig * dt);
                    scat += T * Li * (1.0 - st);                                               // integral exata no passo (densidade constante)
                    T *= st;
                }

                // --- luminárias: só o trecho do raio dentro do alcance de cada uma
                int nl = (int)_FBVolLightCount;
                [loop] for (int k = 0; k < nl; k++)
                {
                    float4 P = _FBVolLightPos[k]; float3 oc = ro - P.xyz; float b = dot(oc, rd); float c = dot(oc, oc) - P.w * P.w; float h = b * b - c;
                    if (h <= 0) continue;
                    h = sqrt(h); float t0 = max(-b - h, 0.0), t1 = min(-b + h, L); if (t1 <= t0) continue;
                    float4 D = _FBVolLightDir[k]; float4 C = _FBVolLightCol[k];
                    float seg = (t1 - t0) / 6.0; float3 acc = 0;
                    [unroll] for (int s2 = 0; s2 < 6; s2++)
                    {
                        float t = t0 + (s2 + jit) * seg; float3 p = ro + rd * t; float3 l = P.xyz - p; float d2 = dot(l, l); float3 ln = l * rsqrt(max(d2, 1e-4));
                        float rf = saturate(1.0 - d2 / (P.w * P.w)); rf *= rf;                   // some suave no alcance (como o URP)
                        float cone = D.w < -1.5 ? 1.0 : smoothstep(D.w, C.w, dot(-ln, D.xyz));
                        float ph = Phase(dot(rd, ln), 0.35);
                        acc += C.rgb * (cone * rf * ph / max(d2, 0.35)) * Density(p.y);
                    }
                    // transmitância até o meio do trecho (aproximação: a névoa entre a câmera e o poste)
                    float tm = 0.5 * (t0 + t1);
                    scat += acc * seg * exp(-Density(ro.y + rd.y * tm * 0.5) * tm);
                }
                return float4(scat, T);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Composicao"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            TEXTURE2D(_FBVolTex); float4 _FBVolTex_TexelSize;

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
                float d0 = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                // filtro em cruz (9 amostras da meia resolução) com peso pela profundidade: tira o ruído sem borrar a silhueta
                float2 ts = _FBVolTex_TexelSize.xy; float4 sum = 0; float wsum = 0;
                const float2 O[9] = { float2(0, 0), float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1), float2(2, 0), float2(-2, 0), float2(0, 2), float2(0, -2) };
                const float K[9] = { 0.24, 0.12, 0.12, 0.12, 0.12, 0.07, 0.07, 0.07, 0.07 };
                [unroll] for (int k = 0; k < 9; k++)
                {
                    float2 u = uv + O[k] * ts;
                    float dk = LinearEyeDepth(SampleSceneDepth(u), _ZBufferParams);
                    float w = K[k] * exp(-abs(dk - d0) / max(d0 * 0.04, 0.05));
                    sum += SAMPLE_TEXTURE2D_LOD(_FBVolTex, sampler_LinearClamp, u, 0) * w; wsum += w;
                }
                float4 vol = sum / max(wsum, 1e-4);
                col.rgb = col.rgb * vol.a + vol.rgb;
                return col;
            }
            ENDHLSL
        }
    }
}
