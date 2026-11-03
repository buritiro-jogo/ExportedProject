// POÇAS DA CHUVA (FBRain, 20/10) — ideias do Dyna-Wet (RayznGames; sem licença publicada, então nada copiado: reimplementado aqui):
// poça que ENCHE com o tempo de chuva (_FBPuddleFill 0..1: a forma por ruído vai crescendo do centro), água escura e lisa que reflete
// (sonda de reflexo + SSR do URP pelo DepthNormals com suavidade) e ANÉIS de gota animados (células de 0,3 m no mundo, cada uma com o
// seu tempo; dois níveis desencontrados), borda mais rasa = menos lisa. Uma malha só com todas as poças do mapa (FBRain.Puddles).
// Opaco com alpha-to-coverage na borda (MSAA) — entra no SSR e no SSAO como o piso.
Shader "FreeBala/Puddle"
{
    Properties
    {
        _Color("Agua", Color) = (0.018, 0.021, 0.024, 1)
        _EdgeColor("Borda molhada", Color) = (0.05, 0.05, 0.05, 1)
        _Smooth("Lisura", Range(0, 1)) = 0.94
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    CBUFFER_START(UnityPerMaterial)
        half4 _Color, _EdgeColor; half _Smooth;
    CBUFFER_END
    float _FBPuddleFill, _FBRainK;

    struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; };
    struct VOut { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float2 uv : TEXCOORD1; float2 seed : TEXCOORD2; float fog : TEXCOORD3; };

    float PHash(float2 p) { p = frac(p * float2(233.34, 851.73)); p += dot(p, p + 23.45); return frac(p.x * p.y); }
    float PNoise(float2 p) { float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f); return lerp(lerp(PHash(i), PHash(i + float2(1, 0)), f.x), lerp(PHash(i + float2(0, 1)), PHash(i + float2(1, 1)), f.x), f.y); }

    VOut PuddleVert(Attributes v)
    {
        VOut o; o.positionWS = TransformObjectToWorld(v.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS);
        o.uv = v.uv; o.seed = v.uv1; o.fog = ComputeFogFactor(o.positionCS.z); return o;
    }
    // forma: blob por ruído que cresce com _FBPuddleFill; devolve (cobertura 0..1, profundidade 0 borda .. 1 meio)
    float2 PuddleShape(float2 uv, float2 seed)
    {
        float2 c = uv - 0.5; float r = length(c) * 2.0;
        float n = PNoise(uv * 3.1 + seed * 37.0) * 0.6 + PNoise(uv * 7.3 + seed * 11.0) * 0.4;
        float fill = saturate(_FBPuddleFill) * lerp(0.75, 1.0, seed.y);
        float f = (1.0 - r) + (n - 0.5) * 0.85 - (1.0 - fill) * 0.95;
        return float2(saturate(f * 9.0), saturate(f * 2.2));
    }
    // anéis de gota (normal em espaço de mundo): por célula de 0,3 m um impacto a cada ~0,8 s, anel abrindo até 0,14 m e sumindo
    float3 Ripples(float2 p, float t)
    {
        float2 g = float2(0, 0);
        [unroll] for (int layer = 0; layer < 2; layer++)
        {
            float2 q = p / 0.3 + layer * float2(0.37, 0.71); float2 ci = floor(q);
            [unroll] for (int dx = -1; dx <= 1; dx++) [unroll] for (int dy = -1; dy <= 1; dy++)
            {
                float2 cell = ci + float2(dx, dy); float h = PHash(cell + layer * 13.1);
                float2 ctr = cell + float2(PHash(cell + 3.3), PHash(cell + 7.7)) * 0.8 + 0.1;
                float ph = frac(t * 1.25 + h); float rad = ph * 0.48; float2 d = q - ctr; float dist = length(d);
                float x = (dist - rad) * 28.0; float ring = exp(-x * x) * (1.0 - ph) * (1.0 - ph);
                g += (dist > 1e-4 ? d / dist : float2(0, 0)) * ring * cos(x * 1.4) * step(h, _FBRainK * 0.9 + 0.1);
            }
        }
        return normalize(float3(-g.x * 0.35, 1.0, -g.y * 0.35));
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest+5" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            AlphaToMask On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex PuddleVert
            #pragma fragment PuddleFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            half4 PuddleFrag(VOut i) : SV_Target
            {
                float2 sh = PuddleShape(i.uv, i.seed); clip(sh.x - 0.02);
                float3 n = Ripples(i.positionWS.xz, _Time.y);
                n = normalize(lerp(float3(0, 1, 0), n, sh.y * 0.85 + 0.15));
                InputData d = (InputData)0;
                d.positionWS = i.positionWS; d.positionCS = i.positionCS; d.normalWS = n; d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS); d.fogCoord = i.fog; d.bakedGI = SampleSH(n);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS); d.shadowMask = half4(1, 1, 1, 1);
                SurfaceData s = (SurfaceData)0;
                s.albedo = lerp(_EdgeColor.rgb, _Color.rgb, sh.y); s.smoothness = lerp(0.55, _Smooth, sh.y); s.metallic = 0; s.occlusion = 1; s.alpha = sh.x;
                s.specular = half3(0, 0, 0);
                half4 c = UniversalFragmentPBR(d, s);
                c.rgb = MixFog(c.rgb, d.fogCoord);
                return half4(c.rgb, sh.x);
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
            #pragma target 3.0
            #pragma vertex PuddleVert
            #pragma fragment DepthFrag
            half DepthFrag(VOut i) : SV_Target { clip(PuddleShape(i.uv, i.seed).x - 0.5); return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex PuddleVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _WRITE_SMOOTHNESS
            half4 NormalsFrag(VOut i) : SV_Target
            {
                float2 sh = PuddleShape(i.uv, i.seed); clip(sh.x - 0.5);
                float3 n = normalize(lerp(float3(0, 1, 0), Ripples(i.positionWS.xz, _Time.y), sh.y * 0.85 + 0.15));
                half a = 0;
            #if defined(_WRITE_SMOOTHNESS)
                a = lerp(0.55, _Smooth, sh.y);
            #endif
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = PackNormalOctQuadEncode(n); return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), a);
            #else
                return half4(n, a);
            #endif
            }
            ENDHLSL
        }
    }
}
