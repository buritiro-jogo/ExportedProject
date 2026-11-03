// GRAMA (versão final, 20/10): tapete UNIFORME de lâminas baixas em volta da câmera, montado inteiro na GPU (FBGrassCarpet.cs).
// Um draw por anel (Graphics.RenderMeshPrimitives) só com os BLOCOS de 4 m que têm gramado e estão na tela (a CPU escolhe, _GC_Tiles);
// cada instância = uma touceira numa célula de uma grade presa ao MUNDO (mesmo sorteio por célula em qualquer quadro/cliente). O vertex shader:
//  · bloco + célula -> posição no mundo (passo _GC_Grid.z, _GC_Grid.w células por lado do bloco) + jitter da célula;
//  · máscara do gramado (_GC_Mask, gerada na importação pelo FBVegBake: A = onde o raio de cima acha grama e nada por cima; RGB = cor do
//    chão ali) -> cobertura contínua, borda pontilhada, lâmina no TOM do gramado de baixo (raiz mais escura, ponta mais clara);
//    altura do chão (_GC_HeightTex, RHalf) -> a lâmina nasce no piso de verdade;
//  · altura 6–16 cm com manchas (aparada × mais alta), vento global _FBWind (mesma direção/rajadas das árvores), inclinação própria;
//  · encolhe até sumir no fim do anel (_GC_Ring) e cruza com o anel de fora sem degrau; fora do frustum / sem grama -> triângulo nulo.
// Luz: Blinn-Phong do URP (sol + luzes adicionais/Forward+ dos postes + sombras + SSAO + névoa), normal puxada para cima (a grama
// acende como o chão, sem lâmina escura), oclusão na raiz e translucidez contra o sol. DepthOnly/DepthNormals para SSAO/SSR.
Shader "FreeBala/GrassCarpet"
{
    Properties
    {
        _RootK("Raiz (x cor do chao)", Float) = 0.55
        _TipK("Ponta (x cor do chao)", Float) = 1.25
        _ColorDry("Ponta seca", Color) = (0.55, 0.52, 0.26, 1)
        _Height("Altura media (m)", Float) = 0.12
        _Width("Largura da lamina (m)", Float) = 0.014
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    CBUFFER_START(UnityPerMaterial)
        half4 _ColorDry; float _RootK, _TipK, _Height, _Width;
    CBUFFER_END
    float4 _GC_Grid;          // -, -, passo, células por lado do bloco
    float4 _GC_Tiles[256];    // canto (x, z) de cada bloco desenhado neste anel
    float4 _GC_Ring;          // r0 (raio interno: entra entre r0-2 e r0), r1 começo do sumiço, r2 fim, escala da touceira
    float4 _GC_Rect;          // máscara: x0, z0, 1/largura, 1/profundidade (mundo)
    float4 _GC_Misc;          // x: alturaMin da textura, y: densidade (0..1), z: largura extra (anel de fora), w: 1 = anel de fora
    float4 _FBWind;
    TEXTURE2D(_GC_Mask); SAMPLER(sampler_GC_Mask);
    TEXTURE2D(_GC_HeightTex); SAMPLER(sampler_GC_HeightTex);

    struct Attributes
    {
        float4 positionOS : POSITION;     // xz = base da lâmina na touceira (-0,5..0,5); y = 0
        float3 normalOS : NORMAL;         // xz = direção da face da lâmina
        float2 uv0 : TEXCOORD0;           // x = lado (-1/1, 0 = ponta), y = altura relativa (0 raiz .. 1 ponta)
        float2 uv1 : TEXCOORD1;           // x = sorteio da lâmina, y = inclinação própria
        uint iid : SV_InstanceID;
    };
    struct VOut { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float4 col : TEXCOORD2; float fog : TEXCOORD3; };

    float GCHash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
    float GCNoise(float2 p) { float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f); return lerp(lerp(GCHash(i), GCHash(i + float2(1, 0)), f.x), lerp(GCHash(i + float2(0, 1)), GCHash(i + float2(1, 1)), f.x), f.y); }

    VOut GrassVert(Attributes v)
    {
        VOut o = (VOut)0;
        uint N = (uint)_GC_Grid.w; uint tile = v.iid / (N * N), l = v.iid % (N * N); uint ix = l % N, iz = l / N;
        float2 cellW = floor(_GC_Tiles[tile].xy / _GC_Grid.z + 0.5) + float2(ix, iz);     // índice da célula NO MUNDO (estável)
        float h1 = GCHash(cellW), h2 = GCHash(cellW + 17.31), h3 = GCHash(cellW + 41.7), h4 = GCHash(cellW + 7.9);
        float2 c = (cellW + float2(h1, h2) * 0.8 + 0.1) * _GC_Grid.z;                        // centro da touceira (XZ)
        float d = distance(c, _WorldSpaceCameraPos.xz);
        // anel: entra pontilhado entre r0-2 e r0 (o de dentro some pontilhado no mesmo trecho), encolhe de r1 a r2
        float inner = _GC_Misc.w > 0.5 ? saturate((d - (_GC_Ring.x - 2.0)) / 2.0) : 1.0;
        float outerCut = _GC_Misc.w > 0.5 ? 1.0 : 1.0 - saturate((d - (_GC_Ring.y - 2.0)) / 2.0);
        float shrink = _GC_Misc.w > 0.5 ? saturate((_GC_Ring.z - d) / max(_GC_Ring.z - _GC_Ring.y, 1e-3)) : 1.0;
        float2 muv = (c - _GC_Rect.xy) * _GC_Rect.zw;
        float4 mk = SAMPLE_TEXTURE2D_LOD(_GC_Mask, sampler_GC_Mask, muv, 0);
        float m = mk.a * step(0.0, muv.x) * step(muv.x, 1.0) * step(0.0, muv.y) * step(muv.y, 1.0);
        float3 ground = pow(saturate(mk.rgb), 2.2);                                        // cor do chão (sRGB -> linear)
        bool keep = m > h3 * 0.92 + 0.04 && h4 < inner && h4 < outerCut + 0.001 && h4 < _GC_Misc.y && shrink > 0.02;
        float gy = SAMPLE_TEXTURE2D_LOD(_GC_HeightTex, sampler_GC_HeightTex, muv, 0).r + _GC_Misc.x;
        float3 org = float3(c.x, gy, c.y);
        // fora do frustum (com folga) -> descarta a touceira inteira
        float4 pc = TransformWorldToHClip(org + float3(0, 0.08, 0));
        float lim = pc.w * 1.15 + 0.6;
        keep = keep && pc.w > -0.6 && abs(pc.x) < lim && abs(pc.y) < lim;
        if (!keep) { o.positionCS = float4(0, 0, -2, 1); return o; }

        // touceira: giro e tamanho por célula; lâmina: altura, inclinação, vento
        float ang = h1 * 6.2832, ca = cos(ang), sa = sin(ang);
        float2 bxz = float2(v.positionOS.x * ca - v.positionOS.z * sa, v.positionOS.x * sa + v.positionOS.z * ca) * _GC_Ring.w;
        float2 fxz = float2(v.normalOS.x * ca - v.normalOS.z * sa, v.normalOS.x * sa + v.normalOS.z * ca);
        float2 side = float2(-fxz.y, fxz.x);
        float patch = GCNoise(c * 0.18) * 0.6 + GCNoise(c * 0.9 + 3.0) * 0.4;             // manchas: aparada x mais alta
        float bh = _Height * lerp(0.55, 1.25, patch) * lerp(0.75, 1.15, v.uv1.x) * lerp(0.55, 1.0, saturate(m * 1.4)) * shrink;
        float t = v.uv0.y;
        float wdt = (_Width + _GC_Misc.z) * (1.0 - t * 0.9) * lerp(0.8, 1.2, v.uv1.x);
        float tm = _Time.y; float ph = dot(c, float2(0.31, 0.23));
        float gust = 0.55 + 0.45 * sin(tm * 0.43 + dot(c, float2(0.045, 0.031)) * 2.0);
        float sway = (sin(tm * 2.1 + ph + v.uv1.x * 3.0) * 0.55 + sin(tm * 3.7 + ph * 1.7) * 0.2 + 0.35) * gust * _FBWind.z;
        float2 bend = fxz * v.uv1.y * 0.35 + _FBWind.xy * sway * 0.55;                    // inclinação própria + vento
        float3 p = org + float3(bxz.x, 0, bxz.y);
        p.xz += side * v.uv0.x * wdt;
        p.xz += bend * bh * t * t;
        p.y += bh * t * (1.0 - 0.25 * saturate(length(bend)) * t);
        o.positionWS = p;
        o.positionCS = TransformWorldToHClip(p); o.fog = ComputeFogFactor(o.positionCS.z);
        float3 fn = float3(fxz.x, 0, fxz.y);
        o.normalWS = normalize(float3(0, 1, 0) * 1.6 + fn * 0.55 + float3(bend.x, 0, bend.y) * 0.3);
        // cor: raiz escura -> ponta clara, manchas secas, variação por lâmina; a = oclusão na raiz
        float dry = smoothstep(0.6, 0.9, GCNoise(c * 0.07 + 11.0)) * 0.35 + v.uv1.x * 0.08;
        half3 tip = lerp(ground * _TipK, _ColorDry.rgb * dot(ground, float3(0.3, 0.6, 0.1)) * 3.0, saturate(dry)) * lerp(0.88, 1.1, v.uv1.x);
        o.col = float4(lerp(ground * _RootK, tip, smoothstep(0.0, 1.0, t)), lerp(0.6, 1.0, t));
        return o;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GrassVert
            #pragma fragment GrassFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            half4 GrassFrag(VOut i) : SV_Target
            {
                InputData d = (InputData)0;
                d.positionWS = i.positionWS; d.positionCS = i.positionCS; d.normalWS = normalize(i.normalWS);
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.fogCoord = i.fog;
                d.bakedGI = SampleSH(d.normalWS);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                d.shadowMask = half4(1, 1, 1, 1);
                SurfaceData s = (SurfaceData)0;
                s.albedo = i.col.rgb; s.occlusion = i.col.a; s.alpha = 1; s.smoothness = 0.1; s.specular = half3(0, 0, 0);
                half4 c = UniversalFragmentBlinnPhong(d, s);
                Light ml = GetMainLight(d.shadowCoord);                                   // translucidez: sol atrás da lâmina
                half back = pow(saturate(dot(-d.viewDirectionWS, ml.direction)), 4.0) * ml.shadowAttenuation * ml.distanceAttenuation;
                c.rgb += i.col.rgb * ml.color * back * 0.35 * saturate(i.col.a * 1.6 - 0.5);
                c.rgb = MixFog(c.rgb, d.fogCoord);
                return half4(c.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GrassVert
            #pragma fragment DepthFrag
            half DepthFrag(VOut i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GrassVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalsFrag(VOut i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = PackNormalOctQuadEncode(n); return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0.0);
            #else
                return half4(n, 0.0);
            #endif
            }
            ENDHLSL
        }
    }
}
