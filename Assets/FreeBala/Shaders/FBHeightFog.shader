// Névoa de ALTURA do Free Bala (passe de tela cheia antes dos transparentes; só PC — FBHeightFogFeature no renderer do PC).
// Complementa a neblina linear de distância do Unity (que todos os materiais já aplicam): uma camada exponencial que se acumula perto
// do chão e afina com a altura (bruma da manhã, fumaça da cidade à noite, chuva). Integral analítica da densidade ao longo do raio
// (densidade = c·exp(-b·(y - y0))), cor puxando para o sol/lua na direção da luz (espalhamento para frente).
// Globais (FBWeather): _FBFogColor, _FBFogSunColor, _FBFogParams (x densidade c, y queda b por metro, z altura base y0, w opacidade máx.),
// _FBFogSun (xyz direção da luz, w força do brilho).
Shader "FreeBala/HeightFog"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "NevoaAltura"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 _FBFogColor, _FBFogSunColor, _FBFogParams, _FBFogSun;

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_FBFogParams.w <= 0.0) return col;
                float depth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                bool sky = depth <= 1e-6;
                #else
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                bool sky = depth >= 0.999999;
                #endif
                float3 wp = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float3 ro = _WorldSpaceCameraPos;
                float3 v = wp - ro; float dist = length(v); float3 rd = v / max(dist, 1e-4);
                if (sky) dist = 900.0;                                                   // céu: só a faixa do horizonte
                float b = max(_FBFogParams.y, 1e-4);
                float k = rd.y * b * dist;
                float shape = abs(k) > 1e-3 ? (1.0 - exp(-k)) / k : 1.0 - 0.5 * k;      // integral da exponencial ao longo do raio
                float optical = _FBFogParams.x * exp(-(ro.y - _FBFogParams.z) * b) * dist * shape;
                float a = saturate(1.0 - exp(-max(optical, 0.0))) * _FBFogParams.w;
                if (sky) a *= saturate(1.0 - rd.y * 3.0);
                float sunAmt = pow(saturate(dot(rd, _FBFogSun.xyz)), 8.0) * _FBFogSun.w;
                float3 fc = lerp(_FBFogColor.rgb, _FBFogSunColor.rgb, saturate(sunAmt));
                col.rgb = lerp(col.rgb, fc, a);
                return col;
            }
            ENDHLSL
        }
    }
}
