// FreeBala/Volumetric - luz volumetrica (material de FBVolumetricFeature, BeforeRenderingTransparents)
// Pass 0 "Raymarch": Blitter.BlitTexture(scaleBias 1,1,0,0) num RT RGBA16F de meia resolucao.
//         Saida rgb = luz espalhada, a = transmitancia.
// Pass 1 "Composite": DrawProcedural (3 vertices) com MPB: _BlitTexture (copia da cor),
//         _FBVolTex (raymarch), _BlitScaleBias, _FBVolTex_TexelSize. cor = cena * a + rgb.
// Globais (FBVolumetric.Tick):
//   _FBVolParams    x densidade (1/m), y queda por altura (1/m), z Y do chao, w distancia maxima
//   _FBVolMain      rgb cor*intensidade da luz principal, w = G (anisotropia Henyey-Greenstein)
//   _FBVolMainDir   xyz direcao PARA a luz principal
//   _FBVolAmbient   rgb ambiente
//   _FBVolSteps     x passos, y frame % 64 (dither)
//   _FBVolLightCount, _FBVolLightPos[32] (xyz, w alcance), _FBVolLightDir[32] (xyz forward,
//   w = cos ang. externo ou -2 se ponto), _FBVolLightCol[32] (rgb, w = cos ang. interno)
Shader "FreeBala/Volumetric"
{
    Properties
    {
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off
        Blend Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        // Blit.hlsl: Attributes, Varyings, Vert, _BlitTexture, _BlitScaleBias (nao redeclarar)
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        #define FB_MAX_LIGHTS 32

        float4 _FBVolParams;
        float4 _FBVolMain;
        float4 _FBVolMainDir;
        float4 _FBVolAmbient;
        float4 _FBVolSteps;
        float  _FBVolLightCount;
        float4 _FBVolLightPos[FB_MAX_LIGHTS];
        float4 _FBVolLightDir[FB_MAX_LIGHTS];
        float4 _FBVolLightCol[FB_MAX_LIGHTS];
        ENDHLSL

        // ---------------------------------------------------------------- Pass 0
        Pass
        {
            Name "Raymarch"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragMarch
            // _MAIN_LIGHT_SHADOWS_SCREEN fica de fora: usa coord. de tela, nao serve para raymarch
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // Ruido de gradiente intercalado (dither do inicio do raio)
            float FB_IGN(float2 p)
            {
                return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
            }

            // Fase Henyey-Greenstein normalizada (1/sr)
            float FB_HG(float c, float g)
            {
                float g2 = g * g;
                float d = max(1.0 + g2 - 2.0 * g * c, 1e-4);
                return (1.0 - g2) / (4.0 * PI * d * sqrt(d));
            }

            // Densidade (extincao) na altura y; abaixo do chao fica constante
            float FB_Sigma(float y)
            {
                return _FBVolParams.x * exp(-_FBVolParams.y * max(y - _FBVolParams.z, 0.0));
            }

            // Profundidade optica analitica de 0 a t (para as luzes locais)
            float FB_OD(float oy, float dy, float t)
            {
                float k = max(_FBVolParams.y, 1e-4);
                float base = _FBVolParams.x * exp(-k * max(oy - _FBVolParams.z, 0.0));
                float kd = k * dy;
                float e = min(-kd * t, 20.0);
                return (abs(kd) > 1e-5) ? base * (1.0 - exp(e)) / kd : base * t;
            }

            float FB_Shadow(float3 p)
            {
            #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                return MainLightRealtimeShadow(TransformWorldToShadowCoord(p));
            #else
                return 1.0;
            #endif
            }

            // Luzes pontuais/spot: integra so o trecho do raio dentro da esfera de alcance
            float3 FB_LocalLights(float3 o, float3 rd, float len, float jit, int lsteps)
            {
                float3 acc = 0.0;
                int n = min((int)_FBVolLightCount, FB_MAX_LIGHTS);
                const float g = 0.2; // mesma anisotropia usada nas luzes do CQ

                [loop]
                for (int i = 0; i < n; i++)
                {
                    float4 lp = _FBVolLightPos[i];
                    float r = lp.w;
                    if (r <= 0.0)
                        continue;

                    // Interseccao raio x esfera
                    float3 oc = o - lp.xyz;
                    float b = dot(oc, rd);
                    float c = dot(oc, oc) - r * r;
                    float h = b * b - c;
                    if (h <= 0.0)
                        continue;
                    h = sqrt(h);
                    float t0 = max(-b - h, 0.0);
                    float t1 = min(-b + h, len);
                    if (t1 <= t0)
                        continue;

                    float4 ld = _FBVolLightDir[i];
                    float4 lc = _FBVolLightCol[i];
                    float r2 = r * r;
                    float segDs = (t1 - t0) / lsteps;
                    float sum = 0.0;

                    [loop]
                    for (int j = 0; j < lsteps; j++)
                    {
                        float t = t0 + (j + jit) * segDs;
                        float3 p = o + rd * t;
                        float3 toL = lp.xyz - p;
                        float d2 = dot(toL, toL);
                        float3 l = toL * rsqrt(max(d2, 1e-6));

                        float win = saturate(1.0 - d2 / r2);
                        float att = (win * win) / (1.0 + d2);
                        if (ld.w > -1.5) // spot
                        {
                            float cd = dot(-l, ld.xyz);
                            att *= smoothstep(ld.w, max(lc.w, ld.w + 1e-3), cd);
                        }

                        float ph = FB_HG(dot(rd, l), g);
                        float tr = exp(-FB_OD(o.y, rd.y, t));
                        sum += att * ph * FB_Sigma(p.y) * tr;
                    }
                    acc += lc.rgb * (sum * segDs);
                }
                return acc;
            }

            half4 FragMarch(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (!(_FBVolParams.x > 0.0))
                    return half4(0.0, 0.0, 0.0, 1.0);

                // scaleBias = (1,1,0,0): texcoord ja e o uv de tela 0..1
                float2 uv = input.texcoord;
                float raw = SampleSceneDepth(uv);
                bool sky = Linear01Depth(raw, _ZBufferParams) > 0.9999;
            #if UNITY_REVERSED_Z
                float dev = raw;
            #else
                float dev = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, raw);
            #endif
                float3 posWS = ComputeWorldSpacePosition(uv, dev, UNITY_MATRIX_I_VP);

                float3 cam = _WorldSpaceCameraPos.xyz;
                float3 v = posWS - cam;
                float dist = length(v);
                float3 rd = v / max(dist, 1e-4);
                float maxD = max(_FBVolParams.w, 1.0);
                float len = sky ? maxD : min(dist, maxD);

                int steps = clamp((int)_FBVolSteps.x, 4, 64);
                float ds = len / steps;
                float jit = FB_IGN(input.positionCS.xy + 5.588238 * _FBVolSteps.y);

                float md2 = dot(_FBVolMainDir.xyz, _FBVolMainDir.xyz);
                float3 L = (md2 > 1e-6) ? _FBVolMainDir.xyz * rsqrt(md2) : float3(0.0, 1.0, 0.0);
                float g = clamp(_FBVolMain.w, -0.95, 0.95);
                float3 sunRad = _FBVolMain.rgb * FB_HG(dot(rd, L), g);
                float3 amb = _FBVolAmbient.rgb;

                // Luz principal + ambiente (integracao com conservacao de energia)
                float3 scat = 0.0;
                float T = 1.0;
                [loop]
                for (int i = 0; i < steps; i++)
                {
                    float t = (i + jit) * ds;
                    float3 p = cam + rd * t;
                    float st = exp(-FB_Sigma(p.y) * ds);
                    float3 Ls = sunRad * FB_Shadow(p) + amb;
                    scat += T * Ls * (1.0 - st);
                    T *= st;
                }

                int lsteps = clamp(steps / 4, 3, 8);
                scat += FB_LocalLights(cam, rd, len, jit, lsteps);

                scat = min(scat, 1000.0); // protege o RGBA16F
                return half4(scat, saturate(T));
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------- Pass 1
        Pass
        {
            Name "Composite"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragComp

            TEXTURE2D(_FBVolTex);
            float4 _FBVolTex_TexelSize; // x 1/w, y 1/h, z w, w h

            // Upsample bilateral 2x2: pesa os 4 texels pela diferenca de profundidade
            float4 FB_Upsample(float2 uv)
            {
                float4 ts = _FBVolTex_TexelSize;
                if (ts.z < 1.0)
                    return SAMPLE_TEXTURE2D_LOD(_FBVolTex, sampler_LinearClamp, uv, 0);

                float zf = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float2 pos = uv * ts.zw - 0.5;
                float2 b = floor(pos);
                float2 f = pos - b;
                float2 lo = 0.5 * ts.xy;
                float2 hi = 1.0 - 0.5 * ts.xy;

                float4 acc = 0.0;
                float wsum = 0.0;
                UNITY_UNROLL
                for (int k = 0; k < 4; k++)
                {
                    float2 o = float2(k % 2, k / 2);
                    float2 tuv = clamp((b + o + 0.5) * ts.xy, lo, hi);
                    float zl = LinearEyeDepth(SampleSceneDepth(tuv), _ZBufferParams);
                    float2 wb2 = lerp(1.0 - f, f, o);
                    float wd = 1.0 / (1e-3 + abs(zf - zl) / max(zf, 1e-3));
                    float w = wb2.x * wb2.y * wd;
                    acc += SAMPLE_TEXTURE2D_LOD(_FBVolTex, sampler_PointClamp, tuv, 0) * w;
                    wsum += w;
                }
                return acc / max(wsum, 1e-6);
            }

            half4 FragComp(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
                if (!(_FBVolParams.x > 0.0))
                    return scene;

                float4 vol = FB_Upsample(uv);
                // NaN/valores ruins viram "sem efeito"
                float tr = (vol.a >= 0.0 && vol.a <= 1.0) ? vol.a : 1.0;
                float3 add = all(vol.rgb >= 0.0) ? vol.rgb : 0.0;
                scene.rgb = (half3)(scene.rgb * tr + add);
                return scene;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
