// Água das fontes calculada na GPU: uma malha única (FBWaterFountains) com 4 vértices por elemento e os parâmetros nos UVs; o
// vertex shader faz a física — sem custo de CPU por quadro e sem ParticleSystem (funciona igual no PC, celular e WebGL).
//   uv0 = (canto x, canto y, tipo, semente)   uv1 = (direção xyz, velocidade m/s)   uv2 = (vida s, tamanho m, matiz, 0)
//   tipo 0 = gota do jato: sobe e cai com gravidade, esticada pela velocidade (vira filete), some ao voltar à lâmina;
//   tipo 1 = anel na superfície onde a água cai: abre e apaga;
//   tipo 2 = névoa: bolo largo e quase transparente subindo devagar sobre a queda.
// À noite (_FBNight) as gotas pegam a cor dos refletores (mesma paleta do FBWaterJet).
Shader "FreeBala/WaterDrops"
{
    Properties
    {
        _Tint("Cor", Color) = (0.85, 0.93, 1, 1)
        _Alpha("Opacidade", Range(0, 1)) = 0.8
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+5" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
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
                half4 _Tint; half _Alpha;
            CBUFFER_END
            #define GRAV 9.8

            struct Attributes { float4 positionOS : POSITION; float4 uv0 : TEXCOORD0; float4 uv1 : TEXCOORD1; float4 uv2 : TEXCOORD2; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 c : TEXCOORD0; half4 col : TEXCOORD1; half kind : TEXCOORD2; half fogFactor : TEXCOORD3; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 base = TransformObjectToWorld(v.positionOS.xyz);
                float2 c = v.uv0.xy; float kind = v.uv0.z, seed = v.uv0.w;
                float3 dir = v.uv1.xyz; float speed = v.uv1.w; float life = max(0.05, v.uv2.x), size = v.uv2.y, hue = v.uv2.z;
                float t = frac(_Time.y / life + seed) * life, k = t / life;
                float3 camR = UNITY_MATRIX_V[0].xyz, camU = UNITY_MATRIX_V[1].xyz;
                float3 p; half a;
                if (kind < 0.5)
                {   // gota: balística; filete alinhado à velocidade na tela
                    float3 vel = dir * speed - float3(0, GRAV * t, 0);
                    float3 center = base + dir * speed * t - float3(0, 0.5 * GRAV * t * t, 0);
                    float3 toCam = normalize(_WorldSpaceCameraPos - center);
                    float vl = length(vel); float3 along = vl > 1e-3 ? vel / vl : camU;
                    float3 side = cross(along, toCam); float sl = length(side); side = sl > 1e-3 ? side / sl : camR;
                    float len = size + vl * 0.028;
                    p = center + side * c.x * size + along * c.y * len;
                    a = smoothstep(0, 0.06, k) * (1 - smoothstep(0.82, 1, k));
                }
                else if (kind < 1.5)
                {   // anel: plano na lâmina, abrindo
                    float r = size * (0.15 + 0.85 * sqrt(k));
                    p = base + float3(c.x * r * 2, 0.012, c.y * r * 2);
                    a = (1 - k) * (1 - k) * 0.7;
                }
                else
                {   // névoa: sobe, abre, quase transparente
                    float s = size * (0.7 + 0.8 * k);
                    float3 center = base + float3(0, 0.25 + k * 0.7, 0) + dir * k * 0.4;
                    p = center + camR * c.x * s + camU * c.y * s;
                    a = sin(k * 3.14159) * 0.16;
                }
                o.positionCS = TransformWorldToHClip(p); o.c = c * 2; o.kind = kind;
                Light ml = GetMainLight(); half3 amb = SampleSH(half3(0, 1, 0));
                half3 day = _Tint.rgb * (amb * 1.15 + ml.color * 0.55);
                half3 lc = FountainPalette(floor(base.x / 6) * 0.37 + floor(base.z / 6) * 0.23 + _Time.y * 0.025 + hue * 0.0) * (1.5 - k * 0.6);
                o.col = half4(lerp(day, lc, _FBNight * 0.9), a * _Alpha * (1 + _FBNight * 0.6));
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half d = length(i.c); half m;
                if (i.kind < 0.5) m = (1 - smoothstep(0.35, 1, d)) * (1 + smoothstep(0.4, 0, d) * 0.6);   // gota com miolo brilhante
                else if (i.kind < 1.5) m = smoothstep(0.62, 0.8, d) * (1 - smoothstep(0.86, 1, d));    // anel fino
                else m = (1 - smoothstep(0, 1, d)) * (1 - smoothstep(0, 1, d));                        // névoa macia
                if (d > 1) discard;
                half3 col = MixFog(i.col.rgb, i.fogFactor);
                return half4(col, saturate(i.col.a * m));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
