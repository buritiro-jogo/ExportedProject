// BURITIRO: gotas, respingos e névoa dos chafarizes (FBWater.BuildFountains). O original veio vazio.
// uv0 = (canto x, canto y, tipo 0 gota / 1 anel no chão / 2 névoa, aleatório); uv1 = (direção xyz, velocidade); uv2 = (vida, tamanho).
Shader "FreeBala/WaterDrops" {
	Properties {
		_Tint ("Cor", Vector) = (0.85,0.93,1,1)
		_Alpha ("Opacidade", Range(0, 1)) = 0.8
	}
	SubShader {
		Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
		Pass {
			Tags { "LightMode" = "UniversalForward" }
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off Cull Off
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			CBUFFER_START(UnityPerMaterial)
				float4 _Tint; float _Alpha;
			CBUFFER_END
			struct A { float4 positionOS : POSITION; float4 u0 : TEXCOORD0; float4 u1 : TEXCOORD1; float4 u2 : TEXCOORD2; };
			struct V { float4 positionCS : SV_POSITION; float2 corner : TEXCOORD0; float2 k : TEXCOORD1; float fog : TEXCOORD2; float3 light : TEXCOORD3; };
			V vert(A v) {
				V o;
				float kind = v.u0.z, rnd = v.u0.w;
				float life = max(v.u2.x, 0.05), size = v.u2.y;
				float tau = frac(_Time.y / life + rnd);   // 0..1 da vida
				float t = tau * life;
				float3 basePos = TransformObjectToWorld(v.positionOS.xyz);
				float3 p = basePos;
				float alpha = 1, s = size;
				float3 camR = UNITY_MATRIX_V[0].xyz, camU = UNITY_MATRIX_V[1].xyz;
				float3 offs;
				if (kind < 0.5) {
					p += v.u1.xyz * v.u1.w * t + float3(0, -4.9, 0) * t * t;
					alpha = smoothstep(0.0, 0.08, tau) * (1 - smoothstep(0.8, 1.0, tau));
					// gota esticada na direção do movimento
					float3 vel = v.u1.xyz * v.u1.w + float3(0, -9.8, 0) * t;
					float3 velV = normalize(vel + 1e-4);
					float3 side = normalize(cross(velV, GetWorldSpaceViewDir(p)) + 1e-4);
					offs = side * v.u0.x * s * 0.5 + velV * v.u0.y * s * (1.0 + min(length(vel) * 0.06, 1.6));
				} else if (kind < 1.5) {
					s = size * lerp(0.4, 1.0, tau);
					alpha = (1 - tau) * 0.6;
					offs = float3(v.u0.x, 0, v.u0.y) * s;
					p.y += 0.015;
				} else {
					s = size * lerp(0.7, 1.3, tau);
					p += v.u1.xyz * t * 0.5 + float3(0, 0.25, 0) * t;
					alpha = sin(tau * 3.14159) * 0.22;
					offs = (camR * v.u0.x + camU * v.u0.y) * s;
				}
				p += offs;
				o.positionCS = TransformWorldToHClip(p);
				o.corner = v.u0.xy * 2;
				o.k = float2(kind, alpha * _Alpha);
				o.fog = ComputeFogFactor(o.positionCS.z);
				Light L = GetMainLight();
				o.light = L.color * 0.5 + SampleSH(float3(0, 1, 0));
				return o;
			}
			half4 frag(V i) : SV_Target {
				float r = length(i.corner);
				float a;
				if (i.k.x < 0.5) a = saturate(1 - r);
				else if (i.k.x < 1.5) a = saturate(1 - abs(r - 0.8) * 6) ;
				else a = saturate(1 - r) * saturate(1 - r);
				a *= i.k.y;
				if (a <= 0.003) discard;
				float3 col = _Tint.rgb * i.light;
				return half4(MixFog(col, i.fog), a);
			}
			ENDHLSL
		}
	}
	Fallback Off
}
