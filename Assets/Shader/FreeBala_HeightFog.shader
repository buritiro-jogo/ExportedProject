// BURITIRO: névoa de altura em tela cheia (FBHeightFogFeature / FullScreenPass). O original veio vazio.
// Globais do FBWeather: _FBFogParams (x densidade, y queda com a altura, z altura do chão, w opacidade máxima),
// _FBFogColor, _FBFogSunColor, _FBFogSun (xyz direção para o sol, w força do brilho do sol).
Shader "FreeBala/HeightFog" {
	Properties { }
	SubShader {
		Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
		ZWrite Off ZTest Always Cull Off Blend Off
		Pass {
			Name "FBHeightFog"
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

			float4 _FBFogParams, _FBFogColor, _FBFogSunColor, _FBFogSun;

			half4 Frag(Varyings i) : SV_Target {
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
				float2 uv = i.texcoord;
				half4 src = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
				float dens = _FBFogParams.x;
				if (dens <= 0.0) return src;

				float raw = SampleSceneDepth(uv);
				float3 posWS = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
				float3 cam = GetCameraPositionWS();
				float3 ray = posWS - cam;
				float dist = length(ray);
				#if UNITY_REVERSED_Z
					bool sky = raw <= 0.000001;
				#else
					bool sky = raw >= 0.999999;
				#endif
				if (sky) { dist = 900.0; ray = normalize(ray) * dist; }
				float3 rd = ray / max(dist, 1e-4);

				// integral analítica da névoa exponencial em altura ao longo do raio
				float k = max(_FBFogParams.y, 1e-4);
				float h0 = cam.y - _FBFogParams.z;
				float dy = rd.y * dist;
				float amount;
				if (abs(dy) > 0.01)
					amount = dens * exp(-k * h0) * (1.0 - exp(-k * dy)) / (k * rd.y);
				else
					amount = dens * exp(-k * h0) * dist;
				float fogK = saturate(1.0 - exp(-max(amount, 0.0)));
				fogK = min(fogK, _FBFogParams.w);
				if (sky) fogK *= saturate(1.0 - rd.y * 3.0); // o céu alto continua aparecendo

				float sunAmt = pow(saturate(dot(rd, normalize(_FBFogSun.xyz + 1e-5))), 8.0) * _FBFogSun.w;
				float3 fogCol = lerp(_FBFogColor.rgb, _FBFogSunColor.rgb, saturate(sunAmt));
				return half4(lerp(src.rgb, fogCol, fogK), src.a);
			}
			ENDHLSL
		}
	}
	Fallback Off
}
