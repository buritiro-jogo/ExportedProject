// Escudo de protecao ao renascer (FBTactics.Shields).
// _Mode 1: anel hexagonal no chao sobre um Quad (deitado, Euler(90, t*12, 0), escala ~1.45 m) -> usa UV.
// _Mode 0: cupula (esfera/hemisferio com UV de esfera) com fresnel e brilho na intersecao com a cena.
// Aditivo HDR. Valores por MaterialPropertyBlock: _Alpha, _Intro, _End, _Color.
Shader "FreeBala/SpawnShield"
{
    Properties
    {
        [HDR] _Color ("Cor", Color) = (0.35,0.85,1.6,1)
        _HexScale ("Escala dos hexagonos", Float) = 7
        _Alpha ("Opacidade", Range(0, 1)) = 1
        _Intro ("Entrada 0..1", Range(0, 1)) = 1
        _End ("Dissolver 0..1", Range(0, 1)) = 0
        _Mode ("0 cupula, 1 anel no chao", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+5"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "SpawnShield"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _HexScale;
                half _Alpha;
                half _Intro;
                half _End;
                float _Mode;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float2 Mod2(float2 a, float2 b)
            {
                return a - b * floor(a / b);
            }

            // Grade hexagonal: edge = 0 na borda da celula, 0.5 no centro; id = centro da celula.
            void HexGrid(float2 p, out float edge, out float2 id)
            {
                const float2 r = float2(1.0, 1.7320508);
                const float2 hr = r * 0.5;
                float2 a = Mod2(p, r) - hr;
                float2 b = Mod2(p - hr, r) - hr;
                float2 gv = dot(a, a) < dot(b, b) ? a : b;
                id = p - gv;
                float2 ag = abs(gv);
                edge = 0.5 - max(dot(ag, float2(0.5, 0.8660254)), ag.x);
            }

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                VertexPositionInputs vp = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = vp.positionCS;
                o.positionWS = vp.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = input.uv;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float time = _Time.y;
                float isRing = step(0.5, _Mode);
                float2 uv = input.uv;

                // Coordenadas da grade: anel = disco centrado; cupula = UV de esfera (u da a volta, 2:1)
                float2 q = uv * 2.0 - 1.0;
                float r = length(q);
                float2 pRing = q * (_HexScale * 0.5);
                float2 pDome = float2(uv.x * 2.0, uv.y) * _HexScale;
                float2 p = lerp(pDome, pRing, isRing);

                float edge;
                float2 id;
                HexGrid(p, edge, id);
                float aa = max(length(fwidth(p)), 1e-4) * 1.2;
                float hexLine = 1.0 - smoothstep(0.0, 0.03 + aa, edge);
                float rnd = Hash21(id);

                // Celulas pulsando de leve
                float pulse = 0.5 + 0.5 * sin(time * 2.5 + rnd * 6.2831853);
                float cellFill = 0.06 + 0.10 * pulse;

                // Dissolver por celula
                float keep = saturate((rnd - (_End * 1.1 - 0.08)) / 0.08) * (1.0 - _End);

                // Entrada: anel cresce do centro; cupula sobe da base
                float revealRing = saturate((_Intro * 1.1 - r) / 0.1);
                float revealDome = saturate((_Intro * 1.15 - uv.y) / 0.08);

                // ---- anel no chao ----
                float disc = 1.0 - smoothstep(0.96, 1.0, r);
                float rim = exp(-abs(r - 0.93) * 38.0);
                float inner = smoothstep(0.25, 0.92, r);
                float ring = (hexLine * 0.9 + cellFill) * inner * keep * disc + rim * 1.4 * (1.0 - _End);
                ring *= revealRing;

                // ---- cupula ----
                float3 n = normalize(input.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float fres = pow(1.0 - saturate(abs(dot(n, v))), 3.0);

                float2 suv = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float fragEye = -TransformWorldToView(input.positionWS).z;
                float diff = sceneEye - fragEye;
                // So brilha quando a cena esta logo atras da superficie (sem depth texture nao acende).
                float inter = diff >= 0.0 ? 1.0 - saturate(diff / 0.35) : 0.0;

                float dome = (hexLine * 0.7 + cellFill) * (0.25 + fres) * keep + fres * 0.6 * (1.0 - _End) + inter * 1.5;
                dome *= revealDome;

                float a = lerp(dome, ring, isRing) * _Alpha;
                half3 col = _Color.rgb * (half)a;
                return half4(max(col, 0.0), 0.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
