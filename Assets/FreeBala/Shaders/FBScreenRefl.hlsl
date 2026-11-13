// r65: REFLEXO E REFRAÇÃO EM ESPAÇO DE TELA, comuns às poças (FBPuddle) e à água (FBWater). Precisa da textura opaca e do depth da
// câmera (FBVisual liga a opaca no PC; o global _FBPuddleSSR = 1 diz que estão disponíveis). Sem eles os shaders usam o caminho antigo.
//  · raio refletido marchado NO MUNDO com passos crescentes, acerto = ponto atrás do depth por menos que a espessura, 5 bisseções;
//  · amostras com LOD 0 dentro do laço (sem derivadas — aviso do compilador na lab99);
//  · máscara: some perto da borda da tela e com a distância (sem corte seco quando a câmera gira).
#ifndef FB_SCREEN_REFL
#define FB_SCREEN_REFL
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

#ifndef FB_PUDDLE_GLOBALS_DECLARED
float _FBPuddleSSR; float4 _FBPuddleP;   // P = (aspereza das poças, vento, distância máx. do reflexo, -)
#endif

float2 FBClipToUV(float4 cs) { float2 n = cs.xy / cs.w; n.y *= _ProjectionParams.x; return n * 0.5 + 0.5; }
float FBSceneEye(float2 uv) { return LinearEyeDepth(SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(uv), 0).r, _ZBufferParams); }
half3 FBSceneColor(float2 uv) { return SAMPLE_TEXTURE2D_X_LOD(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, UnityStereoTransformScreenSpaceTex(uv), 0).rgb; }

// devolve (cor refletida, peso 0..1)
half4 FBSSRTrace(float3 posWS, float3 R, float maxDist)
{
    float3 p = posWS + R * 0.05; float stepLen = 0.1; half4 res = 0;
    [loop] for (int it = 0; it < 40; it++)
    {
        float3 q = p + R * stepLen; float4 cs = TransformWorldToHClip(q); if (cs.w <= 0.05) break;
        float2 uv = FBClipToUV(cs); if (uv.x < 0 || uv.y < 0 || uv.x > 1 || uv.y > 1) break;
        float diff = cs.w - FBSceneEye(uv);
        if (diff > 0.03 && diff < max(0.4, stepLen * 2.5))
        {
            float3 a = p, b = q;
            [unroll] for (int k = 0; k < 5; k++)
            {
                float3 m = (a + b) * 0.5; float4 mc = TransformWorldToHClip(m); float2 mu = FBClipToUV(mc);
                if (mc.w > FBSceneEye(mu)) { b = m; uv = mu; } else a = m;
            }
            float2 e = smoothstep(0, 0.07, uv) * smoothstep(0, 0.07, 1 - uv);
            res = half4(FBSceneColor(uv), e.x * e.y * (1 - saturate(length(b - posWS) / maxDist)));
            break;
        }
        p = q; stepLen *= 1.17; if (length(p - posWS) > maxDist) break;
    }
    return res;
}

// GGX (D·F·NdL) de uma luz, para água/poça (sem termo difuso)
half3 FBSpecGGX(half3 lightCol, float3 L, float3 V, float3 n, float a2, half F)
{
    float3 H = normalize(L + V); float d = saturate(dot(n, H)); d = d * d * (a2 - 1.0) + 1.0;
    return lightCol * (a2 / (PI * d * d)) * F * saturate(dot(n, L)) * 0.25;
}
#endif
