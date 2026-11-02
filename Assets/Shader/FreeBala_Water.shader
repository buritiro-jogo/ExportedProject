// BURITIRO: água reescrita (o FreeBala/Water original veio vazio). Mesmas propriedades dos materiais FBWater_*.
Shader "FreeBala/Water" {
	Properties {
		_ShallowColor ("Raso (rgb, alfa)", Vector) = (0.3,0.62,0.7,0.42)
		_DeepColor ("Fundo (rgb, alfa)", Vector) = (0.04,0.22,0.36,0.9)
		_SkyTint ("Céu sem probe", Vector) = (0.58,0.72,0.85,1)
		[NoScaleOffset] [Normal] _NormalMap ("Normais (tileável)", 2D) = "bump" {}
		_NormalScale ("Força das normais", Range(0, 2)) = 0.7
		_Tiling ("Tamanho das ondas (m) camada 1, 2", Vector) = (2.6,7,0,0)
		_Speed ("Deriva das camadas (m/s) 1, 2", Vector) = (0.07,0.045,0,0)
		_Flow ("Fluxo xy=dir XZ, z=vel m/s, w=0 lago 1 rio", Vector) = (0,1,0,0)
		_Agitation ("Agitação (jatos)", Range(0, 1)) = 0
		_DepthRange ("Profundidade até a cor de fundo (m)", Float) = 0.9
		_Murk ("Turbidez", Range(0, 1)) = 0.15
		[NoScaleOffset] _FoamTex ("Espuma (tileável)", 2D) = "black" {}
		_FoamColor ("Cor da espuma", Vector) = (0.93,0.95,0.95,1)
		_FoamDist ("Espuma da margem (m)", Float) = 0.3
		_FoamAmount ("Quantidade de espuma", Range(0, 1)) = 0.55
		_Smoothness ("Lisura", Range(0, 1)) = 0.94
		_SpecBoost ("Brilho do sol/lua", Float) = 3
		_ReflStrength ("Força do reflexo", Range(0, 1)) = 0.8
	}

	SubShader {
		Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
		Pass {
			Name "ForwardWater"
			Tags { "LightMode" = "UniversalForward" }
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Cull Back

			HLSLPROGRAM
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ FB_WATER_LITE
			#pragma multi_compile _ FB_WATER_DEPTH
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
			#pragma multi_compile_fog
			#pragma multi_compile_instancing

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

			CBUFFER_START(UnityPerMaterial)
				float4 _ShallowColor, _DeepColor, _SkyTint, _Tiling, _Speed, _Flow, _FoamColor;
				float _NormalScale, _Agitation, _DepthRange, _Murk, _FoamDist, _FoamAmount, _Smoothness, _SpecBoost, _ReflStrength;
			CBUFFER_END
			TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
			TEXTURE2D(_FoamTex); SAMPLER(sampler_FoamTex);
			float _FBNight;

			struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
			struct Varyings {
				float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float3 normalWS : TEXCOORD1;
				float4 screenPos : TEXCOORD2;
				float fog : TEXCOORD3;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			Varyings vert(Attributes v) {
				Varyings o = (Varyings)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
				o.positionCS = p.positionCS;
				o.positionWS = p.positionWS;
				o.normalWS = TransformObjectToWorldNormal(v.normalOS);
				o.screenPos = ComputeScreenPos(p.positionCS);
				o.fog = ComputeFogFactor(p.positionCS.z);
				return o;
			}

			float3 SampleN(float2 uv) {
				return UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv), _NormalScale);
			}

			half4 frag(Varyings i) : SV_Target {
				UNITY_SETUP_INSTANCE_ID(i);
				float t = _Time.y;
				float2 xz = i.positionWS.xz;
				float2 flowDir = normalize(_Flow.xy + 1e-4);
				float river = saturate(_Flow.w);
				float flowSpeed = _Flow.z;
				float2 tile = max(_Tiling.xy, 0.05);
				float agit = 1.0 + _Agitation * 2.5;

				// duas camadas de normal: rio corre na direção do fluxo; lago só deriva
				float2 d1 = lerp(float2(_Speed.x, _Speed.x * 0.6), flowDir * flowSpeed, river) * t * agit;
				float2 d2 = lerp(float2(-_Speed.y * 0.7, _Speed.y), flowDir * flowSpeed * 0.62 + float2(-flowDir.y, flowDir.x) * 0.03, river) * t * agit;
				float3 n1 = SampleN(xz / tile.x + d1 / tile.x);
			#if defined(FB_WATER_LITE)
				float3 nTS = n1;
			#else
				float3 n2 = SampleN(xz / tile.y + d2 / tile.y + 0.37);
				float3 nTS = normalize(float3(n1.xy + n2.xy, n1.z * n2.z));
			#endif
				nTS.xy *= agit;
				// a malha da água é praticamente plana: tangente = X do mundo
				float3 N = normalize(float3(nTS.x, nTS.z, nTS.y));
				N = normalize(lerp(normalize(i.normalWS), N, 0.85));

				float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
				float NoV = saturate(dot(N, V));
				float fresnel = 0.02 + 0.98 * pow(1.0 - NoV, 5.0);
				fresnel = saturate(fresnel * _ReflStrength + 0.04);

				// profundidade até o fundo (só com Depth Texture)
				float depthK = 0.55;
				float edge = 0.0;
				float2 suv = i.screenPos.xy / i.screenPos.w;
			#if defined(FB_WATER_DEPTH)
				float rawD = SampleSceneDepth(suv);
				float sceneEye = LinearEyeDepth(rawD, _ZBufferParams);
				float waterEye = i.screenPos.w;
				float thick = max(0.0, sceneEye - waterEye);
				// converte a espessura na vista para profundidade aproximada na vertical
				float vert = thick * saturate(abs(V.y) + 0.15);
				depthK = saturate(vert / max(_DepthRange, 0.05));
				edge = 1.0 - saturate(vert / max(_FoamDist, 0.01));
			#endif
				depthK = saturate(depthK + _Murk * 0.5);

				float3 waterCol = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthK);
				float alpha = lerp(_ShallowColor.a, _DeepColor.a, depthK);

				Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
				float night = saturate(_FBNight);
				float3 ambient = SampleSH(N);
				float3 diffuse = waterCol * (mainLight.color * saturate(dot(float3(0, 1, 0), mainLight.direction)) * 0.35 + ambient);

				// reflexo: probe da cena; sem probe usa a cor do céu
				float3 R = reflect(-V, N);
				float perceptualRough = 1.0 - _Smoothness;
				float3 refl = GlossyEnvironmentReflection(R, i.positionWS, perceptualRough, 1.0, suv);
				float probeLum = dot(refl, float3(0.3, 0.59, 0.11));
				refl = (probeLum < 0.002) ? _SkyTint.rgb * lerp(1.0, 0.06, night) : refl;

				// brilho do sol/lua
				float3 H = normalize(mainLight.direction + V);
				float specPow = exp2(10.0 * _Smoothness + 1.0);
				float spec = pow(saturate(dot(N, H)), specPow) * _SpecBoost * mainLight.shadowAttenuation;
				float3 specCol = mainLight.color * spec;

				float3 col = lerp(diffuse, refl, fresnel) + specCol;
				alpha = saturate(max(alpha, fresnel) + spec * 0.5);

				// espuma da margem e dos jatos
				float foamTex = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, xz * 0.45 + d1 * 0.5).r;
				float foam = saturate(edge * 1.4 * foamTex + _Agitation * foamTex * 0.6) * _FoamAmount;
				float3 foamLit = _FoamColor.rgb * (mainLight.color * 0.6 + ambient);
				col = lerp(col, foamLit, foam);
				alpha = max(alpha, foam);
			#if defined(FB_WATER_DEPTH)
				// some suave encostando na margem, sem serrilhado
				alpha *= saturate((1.0 - edge) * 6.0 + 0.15);
			#endif

				col = MixFog(col, i.fog);
				return half4(col, saturate(alpha));
			}
			ENDHLSL
		}
	}
	Fallback Off
}
