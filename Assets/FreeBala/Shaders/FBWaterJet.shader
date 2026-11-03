// Jato d'água das fontes (malhas "_jet" do mapa: colunas e arcos). Translúcido, dos dois lados, com estrias de água correndo ao longo
// do jato (textura de espuma tileável no espaço do mundo, rolando para cima na coluna), borda mais opaca pelo fresnel e brilho do sol.
// À noite (_FBNight global, 0..1, do FBWater) ganha a luz colorida dos refletores da Praça das Águas, trocando devagar por grupo.
Shader "FreeBala/WaterJet"
{
    Properties
    {
        _Tint("Cor", Color) = (0.86, 0.94, 1, 1)
        [NoScaleOffset] _FoamTex("Estrias (tileável)", 2D) = "white" {}
        _Alpha("Opacidade", Range(0, 1)) = 0.55
        _StreakSpeed("Velocidade das estrias (m/s)", Float) = 2.4
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "FBWaterCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint; half _Alpha, _StreakSpeed;
            CBUFFER_END
            TEXTURE2D(_FoamTex); SAMPLER(sampler_FoamTex);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half fogFactor : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o; o.positionWS = TransformObjectToWorld(v.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS); o.fogFactor = ComputeFogFactor(o.positionCS.z); return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float t = _Time.y; float3 p = i.positionWS;
                half3 N = normalize(i.normalWS) * IS_FRONT_VFACE(facing, 1, -1); half3 V = GetWorldSpaceNormalizeViewDir(p);
                half edge = 1 - abs(dot(N, V));
                // estrias: coordenada "em volta" do jato × altura; a água sobe rápido no centro (vale para coluna e arco)
                float2 uv = float2((p.x + p.z) * 2.3 + N.x * 0.6, p.y * 0.9 - t * _StreakSpeed);
                half s = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, uv).r;
                half s2 = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, uv * float2(0.5, 1.7) + float2(0.3, -t * 0.7)).r;
                half k = saturate(s * 0.7 + s2 * 0.6);
                Light ml = GetMainLight(); half3 amb = SampleSH(half3(0, 1, 0));
                half3 col = _Tint.rgb * (amb * 1.1 + ml.color * 0.5) * (0.75 + 0.5 * k);
                half sp = pow(saturate(dot(reflect(-V, N), ml.direction)), 48); col += ml.color * sp * 0.9;
                half night = _FBNight;
                half3 lc = FountainPalette(floor(p.x / 6) * 0.37 + floor(p.z / 6) * 0.23 + t * 0.025);
                col = lerp(col, lc * (1.2 + k * 0.8), night * 0.85);
                half a = _Alpha * (0.35 + 0.65 * k) * (0.45 + 0.55 * edge) * (1 + night * 0.6);
                col = MixFog(col, i.fogFactor);
                return half4(col, saturate(a));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
