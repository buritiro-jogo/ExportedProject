// Aura de protecao ao renascer (FBTactics.ShowAura).
// Desenhada sobre uma copia do SkinnedMeshRenderer do personagem (mesmos ossos), empurrada _Push m pela normal.
// Valores chegam por MaterialPropertyBlock: _Alpha, _Intro, _End, _Color, _Feet (y mundo), _Height (1.8 * escala).
// Aditiva HDR: _Intro sobe uma frente dos pes a cabeca; _End dissolve por ruido.
Shader "FreeBala/SpawnAura"
{
    Properties
    {
        [HDR] _Color ("Cor", Color) = (0.35,0.85,1.6,1)
        _Alpha ("Intensidade", Range(0, 2)) = 1
        _Intro ("Entrada 0..1", Range(0, 1)) = 1
        _End ("Dissolver 0..1", Range(0, 1)) = 0
        _Feet ("Altura dos pes (mundo)", Float) = 0
        _Height ("Altura do corpo", Float) = 1.8
        _Push ("Afasta da pele (m)", Float) = 0.008
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "SpawnAura"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Alpha;
                half _Intro;
                half _End;
                float _Feet;
                float _Height;
                float _Push;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);
                float a = lerp(Hash31(i),                      Hash31(i + float3(1, 0, 0)), u.x);
                float b = lerp(Hash31(i + float3(0, 1, 0)),    Hash31(i + float3(1, 1, 0)), u.x);
                float c = lerp(Hash31(i + float3(0, 0, 1)),    Hash31(i + float3(1, 0, 1)), u.x);
                float d = lerp(Hash31(i + float3(0, 1, 1)),    Hash31(i + float3(1, 1, 1)), u.x);
                return lerp(lerp(a, b, u.y), lerp(c, d, u.y), u.z);
            }

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 nWS = TransformObjectToWorldNormal(input.normalOS);
                // Empurra em metros no mundo (independe da escala do objeto).
                float3 pWS = TransformObjectToWorld(input.positionOS.xyz) + nWS * _Push;

                o.positionCS = TransformWorldToHClip(pWS);
                o.positionWS = pWS;
                o.normalWS = nWS;
                o.positionOS = input.positionOS.xyz;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float time = _Time.y;
                float h = (input.positionWS.y - _Feet) / max(_Height, 0.01);

                // Fresnel (borda brilhante)
                float3 n = normalize(input.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float ndv = saturate(abs(dot(n, v)));
                float fres = pow(1.0 - ndv, 2.5);

                // Linhas de energia subindo
                float lines = 0.7 + 0.3 * sin(h * 55.0 - time * 5.0);

                // Entrada: frente sobe dos pes (h=0) ate acima da cabeca
                float front = _Intro * 1.15;
                float reveal = saturate((front - h) / 0.06);
                float band = exp(-abs(h - front) * 28.0) * saturate((1.0 - _Intro) * 5.0);

                // Dissolver: limiar sobe com _End; borda do corte brilha
                float nz = ValueNoise(input.positionOS * 9.0 + float3(0.0, time * 0.6, 0.0));
                float thr = _End * 1.2 - 0.1;
                float keep = saturate((nz - thr) / 0.08);
                float edge = (1.0 - saturate(abs(nz - thr) / 0.07)) * step(0.001, _End);

                float a = (0.15 + fres * 1.2) * lines * reveal * keep;
                a += band * 1.5;
                a += edge * 1.5 * reveal;

                // Leve queda nas extremidades (pes e topo da cabeca)
                a *= saturate(h * 8.0 + 0.3) * saturate((1.2 - h) * 6.0);

                half3 col = _Color.rgb * (half)(a * _Alpha);
                return half4(max(col, 0.0), 0.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
