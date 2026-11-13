// POÇAS DA CHUVA (FBRain, 20/10) — ideias do Dyna-Wet (RayznGames; sem licença publicada, então nada copiado: reimplementado aqui):
// poça que ENCHE com o tempo de chuva (_FBPuddleFill 0..1: a forma por ruído vai crescendo do centro), água escura e lisa que reflete
// e ANÉIS de gota animados (células de 0,3 m no mundo, cada uma com o seu tempo; dois níveis desencontrados), borda mais rasa = menos lisa.
// Uma malha só com todas as poças do mapa (FBRain.Puddles). r63: _Fixo > 0 = poça permanente do mapa (Vila Jardim).
// r65: fila TRANSPARENTE (depois da cópia do opaco da câmera), mistura pela cobertura da forma. Dois caminhos:
//   · _FBPuddleSSR = 1 (PC com a textura opaca — FBVisual): REFLEXO em espaço de tela (raio refletido marchado no depth da câmera e
//     refinado por bisseção; volta para a sonda do céu do horário fora da tela) — prédios, postes, árvores e luzes acesas aparecem na
//     água; REFRAÇÃO do chão embaixo (mais escuro no fundo); água barrenta (_Metal) espalha a cor do barro (_Tinta) iluminada, em vez de
//     tingir o reflexo (que deixava a poça uma mancha chapada); Fresnel de água (F0 0,02), brilho GGX do sol e dos postes, película de
//     sujeira (aspereza variando, nunca espelho perfeito) e ondulação de vento leve, além dos anéis de chuva.
//   · _FBPuddleSSR = 0 (mobile / -fbvisualoff): o PBR antigo (só sonda), mesma forma.
Shader "FreeBala/Puddle"
{
    Properties
    {
        _Color("Agua", Color) = (0.018, 0.021, 0.024, 1)
        _EdgeColor("Borda molhada", Color) = (0.05, 0.05, 0.05, 1)
        _Smooth("Lisura", Range(0, 1)) = 0.94
        _Fixo("Enchimento fixo (0 = segue a chuva)", Range(0, 1)) = 0
        _Metal("Tinge o reflexo (água barrenta)", Range(0, 1)) = 0
        _Tinta("Cor da água barrenta vista (foto)", Color) = (1, 1, 1, 1)
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    CBUFFER_START(UnityPerMaterial)
        half4 _Color, _EdgeColor, _Tinta; half _Smooth; half _Fixo; half _Metal;
    CBUFFER_END
    #define FB_PUDDLE_GLOBALS_DECLARED
    float _FBPuddleFill, _FBRainK, _FBPuddleSSR; float4 _FBPuddleP;   // r65: P = (aspereza, vento, distância máx. do reflexo, -)

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
        float fill = (_Fixo > 0 ? _Fixo : saturate(_FBPuddleFill)) * lerp(0.75, 1.0, seed.y);   // r63: poça fixa do mapa (Vila Jardim) não depende da chuva
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
    // r65: ondulação de vento (duas oitavas de ruído rolando; inclinação por diferença finita)
    float WindH(float2 q, float t) { return PNoise(q * 2.3 + float2(t * 0.31, t * 0.17)) * 0.6 + PNoise(q * 6.1 + float2(-t * 0.43, t * 0.37)) * 0.4; }
    float2 WindSlope(float2 p, float t, float amp)
    {
        const float e = 0.03;
        float hx = WindH(p + float2(e, 0), t) - WindH(p - float2(e, 0), t), hz = WindH(p + float2(0, e), t) - WindH(p - float2(0, e), t);
        return float2(hx, hz) / (2.0 * e) * amp;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma target 3.5
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
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "FBScreenRefl.hlsl"                                  // r65: SSR/cena compartilhados com a água (FBWater)
            #define PuddleSSRTrace FBSSRTrace

            half3 PuddleNew(VOut i, float2 sh)
            {
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS); float2 suv = GetNormalizedScreenSpaceUV(i.positionCS);
                float3 nr = Ripples(i.positionWS.xz, _Time.y); float2 ws = WindSlope(i.positionWS.xz, _Time.y, _FBPuddleP.y * 0.012 * sh.y);
                float3 n = normalize(float3(nr.x - ws.x, 1.0, nr.z - ws.y)); n = normalize(lerp(float3(0, 1, 0), n, sh.y * 0.85 + 0.15));
                float NoV = saturate(dot(n, V)); half F = 0.02 + 0.98 * pow(1.0 - NoV, 5.0);
                float film = PNoise(i.positionWS.xz * 1.7 + i.seed * 9.0);
                float rough = saturate(_FBPuddleP.x + film * 0.07 + (1.0 - sh.y) * 0.28);                 // borda rasa e película = mais fosco

                // corpo: chão refratado (mais escuro no fundo) ou barro em suspensão (_Metal = turbidez) iluminado pelo céu e pelo sol
                Light L = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 lightIn = SampleSH(float3(0, 1, 0)) + L.color * (L.shadowAttenuation * saturate(L.direction.y));
                half3 ground = SampleSceneColor(suv + n.xz * 0.02 * sh.y) * lerp(0.8, 0.55, sh.y);
                half3 mud = _Tinta.rgb * lightIn * 0.55;
                half3 body = lerp(ground, mud, saturate(_Metal * 1.15) * smoothstep(0.0, 0.6, sh.y));

                // reflexo: tela → sonda do céu do horário
                float3 R = reflect(-V, n);
                half3 env = GlossyEnvironmentReflection(R, i.positionWS, rough, 1, suv);
                half4 ssr = PuddleSSRTrace(i.positionWS, R, _FBPuddleP.z);
                half3 refl = lerp(env, ssr.rgb, ssr.a * saturate(1.0 - rough * 2.2));

                // especular do sol e dos postes (GGX)
                float a2 = max(rough * rough, 0.0025); a2 *= a2;
                float3 H = normalize(L.direction + V); float dd = saturate(dot(n, H)); dd = dd * dd * (a2 - 1.0) + 1.0;
                half3 spec = L.color * (L.shadowAttenuation * L.distanceAttenuation) * (a2 / (PI * dd * dd)) * F * saturate(dot(n, L.direction)) * 0.25;
                InputData inputData = (InputData)0; inputData.positionWS = i.positionWS; inputData.normalizedScreenSpaceUV = suv;
                uint cnt = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(cnt)
                    Light al = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                    float3 H2 = normalize(al.direction + V); float d2 = saturate(dot(n, H2)); d2 = d2 * d2 * (a2 - 1.0) + 1.0;
                    spec += al.color * (al.distanceAttenuation * al.shadowAttenuation) * (a2 / (PI * d2 * d2)) * F * saturate(dot(n, al.direction)) * 0.25;
                    body += _Tinta.rgb * al.color * (al.distanceAttenuation * saturate(al.direction.y)) * 0.35 * saturate(_Metal);
                LIGHT_LOOP_END
                return lerp(body, refl, F) + min(spec, 40.0);
            }

            half4 PuddleFrag(VOut i) : SV_Target
            {
                float2 sh = PuddleShape(i.uv, i.seed); clip(sh.x - 0.02);
                if (_FBPuddleSSR > 0.5) return half4(MixFog(PuddleNew(i, sh), i.fog), sh.x);
                // caminho antigo (mobile / -fbvisualoff)
                float3 n = Ripples(i.positionWS.xz, _Time.y);
                n = normalize(lerp(float3(0, 1, 0), n, sh.y * 0.85 + 0.15));
                InputData d = (InputData)0;
                d.positionWS = i.positionWS; d.positionCS = i.positionCS; d.normalWS = n; d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS); d.fogCoord = i.fog; d.bakedGI = SampleSH(n);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS); d.shadowMask = half4(1, 1, 1, 1);
                SurfaceData s = (SurfaceData)0;
                s.albedo = lerp(_EdgeColor.rgb, _Color.rgb, sh.y); s.smoothness = lerp(0.55, _Smooth, sh.y); s.metallic = _Metal * sh.y; s.occlusion = 1;
                s.specular = half3(0, 0, 0);
                half4 c = UniversalFragmentPBR(d, s);
                // r63: água BARRENTA — o reflexo do céu sai tingido de laranja (vídeo b_0642: 179,123,64), não branco-azulado
                half3 W3 = half3(0.299, 0.587, 0.114); half lt = max(dot(_Tinta.rgb, W3), 1e-3);
                c.rgb = lerp(c.rgb, dot(c.rgb, W3) * _Tinta.rgb / lt, _Metal * sh.y);
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
