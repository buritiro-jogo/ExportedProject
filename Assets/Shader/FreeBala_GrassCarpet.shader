// FreeBala/GrassCarpet — tapete de grama do FBGrassCarpet.cs (reescrito à mão, URP 17.6). ALTERNATIVA.
// Desenho (FBGrassCarpet.Ring): Graphics.RenderMeshPrimitives(mesh "FB_touceira_N", instanceCount = tiles * n * n).
//   SV_InstanceID -> ladrilho = id / (n*n), célula = id % (n*n) (x = cel % n, z = cel / n).
//   _GC_Tiles[i].xy = canto do ladrilho de 4 m; _GC_Grid.z = lado da célula, .w = n.
//   _GC_Ring = (r0, r1, r2, escala da touceira); _GC_Misc = (grassY0, densidade, largura extra, anel externo?).
//   _GC_Rect = (x0, z0, 1/larg, 1/prof) -> uv de _GC_Mask (rgb = cor do chão, a = presença) e _GC_HeightTex (r = altura).
// Malha: 5 vértices por lâmina; POSITION = posição da lâmina na touceira ([-.5,.5] XZ), NORMAL = direção da lâmina,
//   UV0 = (lado -1..1, altura 0/.55/1), UV1 = (aleatório, inclinação -0.8..0.8).
// Sem sombra projetada (o script usa ShadowCastingMode.Off). Lâmina descartada = vértices colapsados na raiz.
Shader "FreeBala/GrassCarpet"
{
    Properties
    {
        _RootK ("Raiz (x cor do chao)", Float) = 0.55
        _TipK ("Ponta (x cor do chao)", Float) = 1.25
        _ColorDry ("Ponta seca", Vector) = (0.55,0.52,0.26,1)
        _Height ("Altura media (m)", Float) = 0.12
        _Width ("Largura da lamina (m)", Float) = 0.014
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Altura: groundY = grassY0 + r * GC_HEIGHT_SCALE (troque se a textura guardar Y absoluto ou 0..1)
        #ifndef GC_HEIGHT_SCALE
        #define GC_HEIGHT_SCALE 1.0
        #endif

        CBUFFER_START(UnityPerMaterial)
            float _RootK;
            float _TipK;
            float4 _ColorDry;
            float _Height;
            float _Width;
        CBUFFER_END

        // Do MaterialPropertyBlock (por draw) e globais do FBGfx
        float4 _GC_Grid;
        float4 _GC_Ring;
        float4 _GC_Rect;
        float4 _GC_Misc;
        float4 _GC_Tiles[256];
        float4 _FBWind;
        TEXTURE2D(_GC_Mask);      SAMPLER(sampler_GC_Mask);
        TEXTURE2D(_GC_HeightTex); SAMPLER(sampler_GC_HeightTex);

        struct GCAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            float2 uv0        : TEXCOORD0;
            float2 uv1        : TEXCOORD1;
            uint instanceID   : SV_InstanceID;
        };

        struct GCBlade
        {
            float3 positionWS;
            float3 facingWS;
            float3 sideWS;
            float  side;
            float  v;
            half3  albedo;
        };

        float2 GCHash22(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.xx + p3.yz) * p3.zy);
        }

        GCBlade GCBuildBlade(GCAttributes a)
        {
            GCBlade b = (GCBlade)0;

            // instância -> ladrilho / célula
            uint n = (uint)max(_GC_Grid.w + 0.5, 1.0);
            uint per = n * n;
            uint tile = a.instanceID / per;
            uint cell = a.instanceID - tile * per;
            uint cz = cell / n;
            uint cx = cell - cz * n;
            float cs = _GC_Grid.z;
            float2 corner = _GC_Tiles[min(tile, 255u)].xy + float2(cx, cz) * cs;

            // centro com jitter e rotação aleatória (estáveis no mundo)
            float2 h = GCHash22(corner * 1.37 + 0.5);
            float2 center = corner + (0.5 + (h - 0.5) * 0.7) * cs;
            float sa, ca;
            sincos((h.x + h.y * 0.5) * 6.2831853, sa, ca);
            float2 o = a.positionOS.xz;
            float2 root = center + float2(o.x * ca - o.y * sa, o.x * sa + o.y * ca) * _GC_Ring.w;
            float2 f = a.normalOS.xz;
            f = float2(f.x * ca - f.y * sa, f.x * sa + f.y * ca);
            f *= rsqrt(max(dot(f, f), 1e-6));

            // máscara / altura do chão na raiz da lâmina
            float2 uv = (root - _GC_Rect.xy) * _GC_Rect.zw;
            float inside = (all(uv >= 0.0) && all(uv <= 1.0)) ? 1.0 : 0.0;
            half4 m = SAMPLE_TEXTURE2D_LOD(_GC_Mask, sampler_GC_Mask, uv, 0);
            float groundY = _GC_Misc.x + SAMPLE_TEXTURE2D_LOD(_GC_HeightTex, sampler_GC_HeightTex, uv, 0).r * GC_HEIGHT_SCALE;

            float rnd = a.uv1.x;
            float bladeR = frac(rnd * 13.17 + h.x * 7.31 + h.y * 3.7);
            float keep = inside * step(bladeR, _GC_Misc.y * m.a);

            // anéis: perto some no fim (r2); longe entra em r0 e some de r1 a r2
            float r0 = _GC_Ring.x, r1 = _GC_Ring.y, r2 = _GC_Ring.z;
            float d = distance(center, _WorldSpaceCameraPos.xz);
            float s = saturate((r2 - d) / max(r2 - r1, 0.15 * r2));
            if (_GC_Misc.w > 0.5)
                s *= saturate((d - 0.85 * r0) / max(0.15 * r0, 0.01));
            float alive = keep * s;

            float v = a.uv0.y;
            float side = a.uv0.x;
            float hgt = _Height * lerp(0.6, 1.4, rnd) * alive;
            float w = (_Width + _GC_Misc.z) * lerp(0.6, 1.0, alive) * (1.0 - 0.7272 * v);

            float3 facing = float3(f.x, 0.0, f.y);
            float3 sideDir = float3(-f.y, 0.0, f.x);
            float3 p = float3(root.x, groundY, root.y);
            p += sideDir * (side * 0.5 * w);
            p.y += v * hgt;
            p += facing * (a.uv1.y * hgt * v * v * 0.5); // inclinação própria

            // vento: onda que corre na direção do vento + tremor
            float t = _Time.y * max(_FBWind.w, 0.3);
            float sway = max(_FBWind.z, 0.0) * (0.28 + 0.18 * sin(t * 1.9 - dot(root, _FBWind.xy) * 0.45 + rnd * 2.0)
                                                      + 0.06 * sin(t * 6.1 + rnd * 23.0));
            p.xz += _FBWind.xy * (sway * hgt * v * v);
            p.y -= 0.5 * sway * sway * hgt * v * v * v;

            if (alive < 0.02)
                p = float3(root.x, groundY, root.y); // lâmina inteira colapsa (triângulos degenerados)

            // cor do chão (rgb da máscara) -> raiz/ponta/ponta seca
            half3 ground = m.rgb;
            half dry = saturate(frac(rnd * 7.7 + h.y) * 1.6 - 0.9);
            half3 tip = lerp(ground * _TipK, _ColorDry.rgb, dry);
            b.albedo = lerp(ground * _RootK, tip, pow(v, 0.8));

            b.positionWS = p;
            b.facingWS = facing;
            b.sideWS = sideDir;
            b.side = side;
            b.v = v;
            return b;
        }

        // normal arredondada: para cima + face da lâmina (virada para a câmera)
        half3 GCNormal(half3 facing, half3 sideDir, half side, bool front)
        {
            half3 fn = front ? facing : -facing;
            return normalize(half3(0, 0.75, 0) + fn * 0.45 + sideDir * side * 0.25);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GCVert
            #pragma fragment GCFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct GCVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  albedo     : TEXCOORD1;
                half3  facing     : TEXCOORD2;
                half3  sideDir    : TEXCOORD3;
                half2  sideV      : TEXCOORD4;
                half3  ambient    : TEXCOORD5;
                half   fogFactor  : TEXCOORD6;
            };

            GCVaryings GCVert(GCAttributes a)
            {
                GCBlade b = GCBuildBlade(a);
                GCVaryings o = (GCVaryings)0;
                o.positionWS = b.positionWS;
                o.positionCS = TransformWorldToHClip(b.positionWS);
                o.albedo = b.albedo;
                o.facing = b.facingWS;
                o.sideDir = b.sideWS;
                o.sideV = half2(b.side, b.v);
                o.ambient = EvaluateAmbientProbeSRGB(half3(0, 1, 0));
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 GCFrag(GCVaryings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half3 n = GCNormal(normalize(i.facing), i.sideDir, i.sideV.x, IS_FRONT_VFACE(face, true, false));

                InputData id = (InputData)0;
                id.positionWS = i.positionWS;
                id.positionCS = i.positionCS;
                id.normalWS = n;
                id.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                id.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                id.fogCoord = InitializeInputDataFog(float4(i.positionWS, 1.0), i.fogFactor);
                id.bakedGI = i.ambient * lerp(0.6, 1.0, i.sideV.y); // AO na raiz
                id.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                id.shadowMask = half4(1, 1, 1, 1);

                SurfaceData sd = (SurfaceData)0;
                sd.albedo = i.albedo;
                sd.smoothness = 0.2;
                sd.normalTS = half3(0, 0, 1);
                sd.occlusion = 1.0;
                sd.alpha = 1.0;

                half4 col = UniversalFragmentPBR(id, sd);

                // contraluz pela lâmina
                Light l = GetMainLight(id.shadowCoord);
                half back = saturate(dot(-n, l.direction)) * 0.5 + pow(saturate(dot(id.viewDirectionWS, -l.direction)), 4.0) * 0.5;
                col.rgb += i.albedo * l.color * l.shadowAttenuation * back * 0.35 * i.sideV.y;

                col.rgb = MixFog(col.rgb, id.fogCoord);
                return half4(col.rgb, 1.0);
            }
            ENDHLSL
        }

        // Prepass de profundidade (necessário com depth priming / textura de profundidade)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GCDepthVert
            #pragma fragment GCDepthFrag

            float4 GCDepthVert(GCAttributes a) : SV_POSITION
            {
                return TransformWorldToHClip(GCBuildBlade(a).positionWS);
            }

            half GCDepthFrag(float4 positionCS : SV_POSITION) : SV_Target
            {
                return positionCS.z;
            }
            ENDHLSL
        }

        // Normais para SSAO
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GCDNVert
            #pragma fragment GCDNFrag

            struct GCDNVaryings
            {
                float4 positionCS : SV_POSITION;
                half3 facing      : TEXCOORD0;
                half3 sideDir     : TEXCOORD1;
                half  side        : TEXCOORD2;
            };

            GCDNVaryings GCDNVert(GCAttributes a)
            {
                GCBlade b = GCBuildBlade(a);
                GCDNVaryings o;
                o.positionCS = TransformWorldToHClip(b.positionWS);
                o.facing = b.facingWS;
                o.sideDir = b.sideWS;
                o.side = b.side;
                return o;
            }

            half4 GCDNFrag(GCDNVaryings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target0
            {
                return half4(GCNormal(normalize(i.facing), i.sideDir, i.side, IS_FRONT_VFACE(face, true, false)), 0.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
