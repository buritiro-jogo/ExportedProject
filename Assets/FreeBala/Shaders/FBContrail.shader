// Rastro de condensação dos aviões (FBSkyPlanes): fita voltada para a câmera com ruído de "algodão" que se ERODE com a idade (o rastro
// nasce contínuo e fino, alarga e se desfaz em flocos), perfil gaussiano na largura, lado de baixo mais escuro e cor pela luz do sol/lua.
// Vértice: uv0.x = distância ao longo do rastro (m/escala, presa ao ponto: o ruído não escorrega), uv0.y = 0..1 na largura,
// color.r = idade 0..1, color.g = semente, color.a = opacidade (nascimento/fim).
Shader "FreeBala/Contrail"
{
    Properties { _NoiseTex ("Ruido", 2D) = "gray" {} _SunCol ("Luz", Color) = (1,1,1,1) _ShadeCol ("Sombra", Color) = (.6,.65,.75,1) _MaxAlpha ("Opacidade", Float) = .8 }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
        Pass
        {
            Name "Rastro"
            HLSLPROGRAM
            #pragma vertex V
            #pragma fragment F
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial) float4 _NoiseTex_ST; half4 _SunCol, _ShadeCol; half _MaxAlpha; CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; half4 col : COLOR; };
            struct Vv { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 col : COLOR; };
            Vv V(A i) { Vv o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = i.uv; o.col = i.col; return o; }
            half4 F(Vv i) : SV_Target
            {
                float age = i.col.r, seed = i.col.g * 17.0;
                float x = i.uv.x + seed, y = i.uv.y;
                float across = abs(y * 2.0 - 1.0);
                // ruído em duas escalas, a de baixo "enrolada" pela de cima; a largura efetiva encolhe nas pontas dos flocos
                float n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, float2(x * 0.35, y * 0.55 + 0.13)).r;
                float n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, float2(x * 1.30 + n1 * 0.4, y * 1.6 - 0.31)).r;
                float n = n1 * 0.62 + n2 * 0.38;
                float prof = exp(-across * across * lerp(5.0, 2.2, age));                       // perfil: fino e firme -> largo e difuso
                float th = lerp(0.18, 0.62, age * age);                                          // erosão: os flocos se separam com a idade
                float dens = saturate((n * prof - th * 0.55) / lerp(0.12, 0.3, age));
                half a = dens * i.col.a * _MaxAlpha;
                if (a < 0.003) discard;
                half3 c = lerp(_ShadeCol.rgb, _SunCol.rgb, saturate(0.35 + n * 0.75 - (1.0 - y) * 0.25));   // y=1 é o lado de cima
                return half4(c, a);
            }
            ENDHLSL
        }
    }
}
