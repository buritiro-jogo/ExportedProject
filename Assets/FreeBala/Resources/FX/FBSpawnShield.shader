// Bolha de proteção de spawn (rodada 39 — pedido "a bola de proteção redonda está muito feia e simples, deixar bem mais profissional").
// Escudo de energia: grade de hexágonos (triplanar no espaço do objeto), borda fresnel, faixa de varredura subindo, células que pulsam,
// materialização de baixo para cima (_Intro), dissolução célula a célula no fim (_End) e brilho no contato com o chão/corpo (profundidade).
// _Mode 1 = anel no chão (quad deitado): anel duplo com traços girando, varredura de radar e preenchimento suave.
// Em Resources/ para entrar no build; aquecido na tela de carregamento (FBTactics mantém uma bolha invisível enquanto carrega).
Shader "FreeBala/SpawnShield"
{
    Properties
    {
        [HDR] _Color ("Cor", Color) = (0.35, 0.85, 1.6, 1)
        _HexScale ("Escala dos hexágonos", Float) = 7
        _Alpha ("Opacidade", Range(0, 1)) = 1
        _Intro ("Entrada 0..1", Range(0, 1)) = 1
        _End ("Dissolver 0..1", Range(0, 1)) = 0
        _Mode ("0 cúpula, 1 anel no chão", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Escudo"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color; float _HexScale, _Alpha, _Intro, _End, _Mode;
            CBUFFER_END

            struct Attr { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
            struct Vary { float4 cs : SV_POSITION; float3 op : TEXCOORD0; float3 wn : TEXCOORD1; float3 wp : TEXCOORD2; float4 sp : TEXCOORD3; float2 uv : TEXCOORD4; };

            Vary vert(Attr i)
            {
                Vary o; VertexPositionInputs p = GetVertexPositionInputs(i.pos.xyz);
                o.cs = p.positionCS; o.wp = p.positionWS; o.op = i.pos.xyz; o.wn = TransformObjectToWorldNormal(i.n); o.sp = ComputeScreenPos(p.positionCS); o.uv = i.uv;
                return o;
            }
            float G(float x) { return exp(-x * x); }                                              // gaussiana (pow de base negativa é indefinido no HLSL)
            float Hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            // célula hexagonal mais próxima: xy = posição dentro da célula, zw = id da célula
            float4 HexCell(float2 p)
            {
                const float2 s = float2(1, 1.7320508);
                float4 c = floor(float4(p, p - float2(.5, 1)) / s.xyxy) + .5;
                float4 h = float4(p - c.xy * s, p - (c.zw + .5) * s);
                return dot(h.xy, h.xy) < dot(h.zw, h.zw) ? float4(h.xy, c.xy) : float4(h.zw, c.zw + 9.43);
            }
            // distância até a borda do hexágono (0 na borda, 0,5 no centro)
            float HexEdge(float2 h) { h = abs(h); return .5 - max(dot(h, float2(.5, .8660254)), h.x); }
            void Plane(float2 p, float w, inout float line_, inout float rnd)
            {
                float4 c = HexCell(p); line_ += w * (1 - smoothstep(.0, .045, HexEdge(c.xy))); rnd += w * Hash(c.zw);
            }

            half4 Ring(Vary i)
            {
                float2 p = i.uv * 2 - 1; float r = length(p); float ang = atan2(p.y, p.x); float t = _Time.y;
                float outer = G((r - .88) / .022);
                float dash = step(.45, frac(ang / 6.2831853 * 28 + t * .35));
                float inner = G((r - .76) / .012) * dash;
                float fill = smoothstep(.9, .15, r) * .1;
                float beam = pow(saturate(cos(ang - t * 2.6)), 28) * smoothstep(.9, .2, r) * .45;
                float pulse = G((r - frac(t * .7) * .9) / .03) * .35 * (1 - r);
                float I = outer * 1.25 + inner * .8 + fill + beam + pulse;
                float a = saturate(I) * _Alpha * (1 - _End) * step(r, 1);
                return half4(_Color.rgb * (.55 + I), a);
            }

            half4 frag(Vary i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                if (_Mode > .5) return Ring(i);
                float t = _Time.y;
                float3 N = normalize(i.wn); float3 Vd = normalize(_WorldSpaceCameraPos - i.wp);
                float ndv = abs(dot(N, Vd)); float fres = pow(1 - ndv, 2.6);
                bool front = IS_FRONT_VFACE(face, true, false);
                float y01 = saturate(i.op.y + .5);                                                  // esfera do Unity: raio 0,5
                // hexágonos triplanares (pesos agudos: sem borrão entre os planos)
                float3 n0 = normalize(i.op); float3 w = pow(abs(n0), 6); w /= (w.x + w.y + w.z);
                float3 q = i.op * _HexScale; float line_ = 0, rnd = 0;
                Plane(q.zy, w.x, line_, rnd); Plane(q.xz, w.y, line_, rnd); Plane(q.xy + 3.7, w.z, line_, rnd);
                // faixa de varredura subindo + pulso por célula
                float band = G((y01 - frac(t * .55)) / .05);
                float pulse = pow(saturate(sin(t * 2.2 + rnd * 40) * .5 + .5), 6) * .5;
                // entrada: materializa do chão para cima com uma frente brilhante
                float f = _Intro * 1.25 - .08; float introMask = smoothstep(f + .02, f - .04, y01); float introEdge = G((y01 - f) / .035) * (1 - _Intro);
                // fim: as células somem em ordem aleatória, com a borda acesa antes de sumir
                float alive = step(_End, rnd * .999 + .001); float endEdge = (1 - saturate((rnd - _End) / .08)) * step(.001, _End);
                // contato com o chão/corpo (profundidade da cena)
                float2 suv = i.sp.xy / i.sp.w; float sceneZ = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams); float inter = 1 - saturate((sceneZ - i.sp.w) / .12);
                float I = .05 + fres * 1.35 + line_ * (.3 + fres * 1.1) + band * (.25 + line_ * .6) + pulse * line_ * .6 + inter * 1.1 + introEdge * 2.2 + endEdge * 1.6;
                float a = saturate(I) * _Alpha * introMask * alive * (front ? 1 : .4);
                return half4(_Color.rgb * (.5 + I), a);
            }
            ENDHLSL
        }
    }
}
