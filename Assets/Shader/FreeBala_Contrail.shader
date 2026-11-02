// BURITIRO: rastro de avião no céu (FBSkyPlanes.cs). O original veio vazio.
// cor do vértice: r = idade (0 novo, 1 velho), g = semente, a = opacidade; uv.x = distância, uv.y = 0..1 através do rastro.
Shader "FreeBala/Contrail" {
	Properties {
		_NoiseTex ("Ruido", 2D) = "gray" {}
		_SunCol ("Luz", Vector) = (1,1,1,1)
		_ShadeCol ("Sombra", Vector) = (0.6,0.65,0.75,1)
		_MaxAlpha ("Opacidade", Float) = 0.8
	}
	SubShader {
		Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
		Pass {
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off Cull Off
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			CBUFFER_START(UnityPerMaterial)
				float4 _NoiseTex_ST, _SunCol, _ShadeCol; float _MaxAlpha;
			CBUFFER_END
			TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
			struct A { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
			struct V { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
			V vert(A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.color = v.color; o.uv = v.uv; return o; }
			half4 frag(V i) : SV_Target {
				float age = i.color.r, seed = i.color.g;
				float across = abs(i.uv.y * 2 - 1);
				float n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, float2(i.uv.x * 0.004 + seed, i.uv.y * (1 - age * 0.5) + seed * 3)).r;
				float width = lerp(0.35, 1.0, age);
				float core = saturate(1 - across / width);
				core = core * core * (3 - 2 * core);
				float a = core * lerp(0.6, 1.2, n) * (1 - age) * i.color.a * _MaxAlpha;
				if (a <= 0.003) discard;
				float3 col = lerp(_ShadeCol.rgb, _SunCol.rgb, saturate(n * 0.6 + (1 - across) * 0.5));
				return half4(col, saturate(a));
			}
			ENDHLSL
		}
	}
	Fallback Off
}
