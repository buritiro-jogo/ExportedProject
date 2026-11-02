// BURITIRO: poças da chuva (FBRain.cs). O original veio vazio.
// uv0 0..1 no quadrado da poça, uv1 = (semente, hash). Globais: _FBPuddleFill (0..1 enchendo), _FBRainK (chovendo).
Shader "FreeBala/Puddle" {
	Properties {
		_Color ("Agua", Vector) = (0.018,0.021,0.024,1)
		_EdgeColor ("Borda molhada", Vector) = (0.05,0.05,0.05,1)
		_Smooth ("Lisura", Range(0, 1)) = 0.94
	}
	SubShader {
		Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline" }
		Pass {
			Tags { "LightMode" = "UniversalForward" }
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Offset -1, -1
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
			#pragma multi_compile_fog
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			CBUFFER_START(UnityPerMaterial)
				float4 _Color, _EdgeColor; float _Smooth;
			CBUFFER_END
			float _FBPuddleFill, _FBRainK;
			struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; };
			struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float2 uv : TEXCOORD1; float2 seed : TEXCOORD2; float fog : TEXCOORD3; };
			float h2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
			float vn(float2 p) { float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
				return lerp(lerp(h2(i), h2(i + float2(1, 0)), f.x), lerp(h2(i + float2(0, 1)), h2(i + 1), f.x), f.y); }
			V vert(A v) { V o; VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
				o.positionCS = p.positionCS; o.positionWS = p.positionWS; o.uv = v.uv; o.seed = v.uv2; o.fog = ComputeFogFactor(p.positionCS.z); return o; }
			half4 frag(V i) : SV_Target {
				float2 c = i.uv * 2 - 1;
				float n = vn(i.uv * 4.0 + i.seed * 37.0) * 0.6 + vn(i.uv * 9.0 + i.seed * 11.0) * 0.4;
				float r = length(c) + (n - 0.5) * 0.55;
				float fill = saturate(_FBPuddleFill);
				float water = 1 - smoothstep(fill * 0.75 - 0.05, fill * 0.75 + 0.05, r);
				float wet = 1 - smoothstep(fill * 0.95, fill * 0.95 + 0.25, r);
				if (wet <= 0.001) discard;

				// gotas batendo: anéis que se espalham
				float3 N = float3(0, 1, 0);
				if (_FBRainK > 0.01) {
					float2 g = i.positionWS.xz * 3.0;
					float2 cell = floor(g); float2 f = frac(g) - 0.5;
					float ph = frac(_Time.y * 1.3 + h2(cell));
					float d = length(f - (float2(h2(cell + 7), h2(cell + 3)) - 0.5) * 0.5);
					float ring = sin((d - ph * 0.45) * 60.0) * saturate(1 - abs(d - ph * 0.45) * 18.0) * (1 - ph);
					N = normalize(float3(f.x * ring * 0.6, 1, f.y * ring * 0.6) * float3(_FBRainK, 1, _FBRainK));
				}
				float3 Vd = normalize(GetWorldSpaceViewDir(i.positionWS));
				float fres = 0.04 + 0.96 * pow(1 - saturate(dot(N, Vd)), 5);
				float3 R = reflect(-Vd, N);
				float3 refl = GlossyEnvironmentReflection(R, i.positionWS, 1 - _Smooth, 1.0, float2(0, 0));
				Light L = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
				float spec = pow(saturate(dot(N, normalize(L.direction + Vd))), 400.0) * 6.0 * L.shadowAttenuation;
				float3 col = lerp(_EdgeColor.rgb, _Color.rgb, water);
				col = lerp(col, refl, fres * lerp(0.35, 1.0, water)) + L.color * spec * water;
				float a = max(water * lerp(0.75, 0.95, fres), wet * 0.45);
				return half4(MixFog(col, i.fog), a);
			}
			ENDHLSL
		}
	}
	Fallback Off
}
