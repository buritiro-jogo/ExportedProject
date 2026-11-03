// Proteção de spawn (rodada 45 — pedido: "substituir a esfera azul por um efeito mais sutil e profissional, que funcione em 1ª e 3ª pessoa e
// no celular"). Em vez da cúpula, uma AURA justa ao corpo: segunda malha skinned do próprio personagem (mesmo esqueleto, FBTactics) com este
// shader aditivo — contorno fresnel fino, uma faixa de varredura subindo devagar, linhas horizontais quase invisíveis só no contorno,
// materialização de baixo para cima (_Intro) e dissolução em pontos no fim (_End). Sem textura, sem profundidade: barato no celular (GLES3).
// Aquecido na tela de carregamento (FBTactics põe uma aura invisível no jogador enquanto carrega).
Shader "FreeBala/SpawnAura"
{
    Properties
    {
        [HDR] _Color ("Cor", Color) = (0.35, 0.85, 1.6, 1)
        _Alpha ("Intensidade", Range(0, 2)) = 1
        _Intro ("Entrada 0..1", Range(0, 1)) = 1
        _End ("Dissolver 0..1", Range(0, 1)) = 0
        _Feet ("Altura dos pés (mundo)", Float) = 0
        _Height ("Altura do corpo", Float) = 1.8
        _Push ("Afasta da pele (m)", Float) = 0.008
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+5" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Aura"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color; float _Alpha, _Intro, _End, _Feet, _Height, _Push;
            CBUFFER_END

            struct Attr { float4 pos : POSITION; float3 n : NORMAL; };
            struct Vary { float4 cs : SV_POSITION; float3 wn : TEXCOORD0; float3 wp : TEXCOORD1; };

            Vary vert(Attr i)
            {
                Vary o; float3 wp = TransformObjectToWorld(i.pos.xyz); float3 wn = TransformObjectToWorldNormal(i.n);
                wp += wn * _Push; o.wp = wp; o.wn = wn; o.cs = TransformWorldToHClip(wp); return o;
            }
            float G(float x) { return exp(-x * x); }
            float Hash(float3 p) { p = frac(p * float3(.1031, .1030, .0973)); p += dot(p, p.yxz + 33.33); return frac((p.x + p.y) * p.z); }

            half4 frag(Vary i) : SV_Target
            {
                float t = _Time.y;
                float3 N = normalize(i.wn); float3 V = normalize(_WorldSpaceCameraPos - i.wp);
                float ndv = saturate(dot(N, V)); float rim = pow(1 - ndv, 3.2);
                float h = (i.wp.y - _Feet) / max(.3, _Height);                                        // 0 nos pés, 1 no topo da cabeça
                float band = G((h - (frac(t * .45) * 1.4 - .2)) / .045);                              // varredura subindo a cada ~2,2 s
                float lines = smoothstep(.92, 1, .5 + .5 * sin(h * 110 - t * 5)) * .35;               // linhas finas, só pesam no contorno
                float front = _Intro * 1.15 - .05;                                                     // entrada: aparece de baixo para cima
                float shown = smoothstep(front, front - .06, h); float edge = G((h - front) / .025) * step(_Intro, .999);
                float dis = step(_End, Hash(floor(i.wp * 38)) * .999);                                // fim: some em pontos
                float I = rim * 1.15 + band * (.18 + rim * .9) + lines * rim + edge * 1.4;
                I *= shown * dis * _Alpha * (1 - _End * .6);
                return half4(_Color.rgb * I, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
