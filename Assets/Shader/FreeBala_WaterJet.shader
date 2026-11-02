// BURITIRO: jato d'água com estrias correndo (materiais FBWater_Jato). O original veio vazio.
Shader "FreeBala/WaterJet" {
	Properties {
		_Tint ("Cor", Vector) = (0.86,0.94,1,1)
		[NoScaleOffset] _FoamTex ("Estrias (tileável)", 2D) = "white" {}
		_Alpha ("Opacidade", Range(0, 1)) = 0.55
		_StreakSpeed ("Velocidade das estrias (m/s)", Float) = 2.4
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
			#pragma multi_compile_instancing
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			CBUFFER_START(UnityPerMaterial)
				float4 _Tint; float _Alpha, _StreakSpeed;
			CBUFFER_END
			TEXTURE2D(_FoamTex); SAMPLER(sampler_FoamTex);
			struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
			struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 normalWS : TEXCOORD2; float fog : TEXCOORD3; };
			V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
				o.positionCS = p.positionCS; o.positionWS = p.positionWS; o.normalWS = TransformObjectToWorldNormal(v.normalOS); o.uv = v.uv; o.fog = ComputeFogFactor(p.positionCS.z); return o; }
			half4 frag(V i) : SV_Target {
				float t = _Time.y * _StreakSpeed;
				float s1 = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, float2(i.uv.x, i.uv.y * 0.5 - t * 0.5)).r;
				float s2 = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, float2(i.uv.x * 1.7 + 0.3, i.uv.y * 0.9 - t * 0.8)).r;
				float streak = saturate(s1 * 0.6 + s2 * 0.5);
				float3 N = normalize(i.normalWS);
				float3 Vd = normalize(GetWorldSpaceViewDir(i.positionWS));
				float rim = pow(1 - saturate(abs(dot(N, Vd))), 2);
				Light L = GetMainLight();
				float3 col = _Tint.rgb * (L.color * 0.55 + SampleSH(N)) * (0.75 + streak * 0.5);
				float a = saturate(_Alpha * (0.45 + streak * 0.7) + rim * 0.25);
				return half4(MixFog(col, i.fog), a);
			}
			ENDHLSL
		}
	}
	Fallback Off
}
