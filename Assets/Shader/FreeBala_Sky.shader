// BURITIRO: céu procedural reescrito (o original veio vazio na reconstrução).
// Mesmas propriedades que o FBLivingSky.cs alimenta. Saída em HDR linear, sem estourar:
// o céu de dia fica entre 0,1 e 1,2 e só o disco do sol passa disso (para o bloom).
Shader "FreeBala/Sky" {
	Properties {
		_Night ("Noite", Range(0, 1)) = 0
		_Dusk ("Por do sol", Range(0, 1)) = 0
		_Rain ("Chuva", Range(0, 1)) = 0
		_MoonPhase ("Fase da lua", Range(0, 1)) = 0.82
		_SunDir ("Sol", Vector) = (-0.5,0.75,0.5,0)
		_MoonDir ("Lua", Vector) = (-0.52,0.63,0.57,0)
		_SkyTime ("Tempo", Float) = 0
		_Cover ("Cobertura de nuvens", Range(0, 1)) = 0.45
		_WindSky ("Vento das nuvens (xz)", Vector) = (1,0.35,0,0)
		_FBCloudVol ("Ruido 3D das nuvens", 3D) = "" {}
		_SkyBrightness ("Brilho geral do céu", Range(0.3, 2)) = 1
	}
	SubShader {
		Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
		Cull Off ZWrite Off

		Pass {
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#pragma multi_compile_local _ _FB_SKY_LITE _FB_VOLCLOUDS
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			CBUFFER_START(UnityPerMaterial)
				float _Night, _Dusk, _Rain, _MoonPhase, _SkyTime, _Cover, _SkyBrightness;
				float4 _SunDir, _MoonDir, _WindSky;
			CBUFFER_END
			TEXTURE3D(_FBCloudVol); SAMPLER(sampler_FBCloudVol);

			struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
			struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

			Varyings vert(Attributes v) {
				Varyings o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
				o.dir = v.positionOS.xyz;
				return o;
			}

			float hash3(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
			float hash2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
			float vnoise(float2 p) {
				float2 i = floor(p), f = frac(p);
				f = f * f * (3.0 - 2.0 * f);
				float a = hash2(i), b = hash2(i + float2(1, 0)), c = hash2(i + float2(0, 1)), d = hash2(i + float2(1, 1));
				return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
			}
			float fbm(float2 p, int oct) {
				float s = 0, a = 0.5, n = 0;
				for (int i = 0; i < oct; i++) { s += a * vnoise(p); n += a; p = p * 2.03 + 17.1; a *= 0.5; }
				return s / n;
			}

			// O FBLivingSky espelha o x das direções antes de mandar; desfaz aqui.
			float3 Unmirror(float4 d) { return normalize(float3(-d.x, d.y, d.z) + 1e-5); }

			float CloudDensity(float2 uv, float cover) {
				float n;
			#if defined(_FB_VOLCLOUDS)
				float t = _SkyTime * 0.0035;
				n = SAMPLE_TEXTURE3D(_FBCloudVol, sampler_FBCloudVol, float3(uv * 0.55, t)).r * 0.65
				  + SAMPLE_TEXTURE3D(_FBCloudVol, sampler_FBCloudVol, float3(uv * 1.7 + 0.37, t * 1.7)).r * 0.35;
			#elif defined(_FB_SKY_LITE)
				n = fbm(uv * 3.0, 3);
			#else
				n = fbm(uv * 3.0, 5);
			#endif
				float thr = lerp(0.78, 0.18, cover);
				return saturate((n - thr) / 0.22);
			}

			half4 frag(Varyings i) : SV_Target {
				float3 d = normalize(i.dir);
				float3 sun = Unmirror(_SunDir);
				float3 moon = Unmirror(_MoonDir);
				float night = saturate(_Night), dusk = saturate(_Dusk) * (1 - night), rain = saturate(_Rain);
				float up = d.y;
				float h = saturate(up);
				float cosSun = dot(d, sun);

				// --- gradiente do céu limpo (cores lineares, céu tropical meio enevoado no horizonte)
				float3 zenDay = float3(0.05, 0.16, 0.48);
				float3 horDay = float3(0.42, 0.56, 0.70);
				float3 zenDusk = float3(0.10, 0.12, 0.26);
				float3 horDusk = float3(0.95, 0.42, 0.16);
				float3 zenNight = float3(0.004, 0.007, 0.018);
				float3 horNight = float3(0.018, 0.026, 0.05);
				float3 zenRain = float3(0.20, 0.22, 0.25);
				float3 horRain = float3(0.34, 0.36, 0.38);

				float g = pow(1.0 - h, 3.0);
				float3 day = lerp(zenDay, horDay, g);
				// o laranja do por do sol fica concentrado do lado do sol
				float sunSide = saturate(cosSun * 0.5 + 0.5);
				float3 duskC = lerp(zenDusk, lerp(float3(0.45, 0.30, 0.34), horDusk, sunSide * sunSide), g);
				float3 nightC = lerp(zenNight, horNight, g);
				float3 rainC = lerp(zenRain, horRain, g) * lerp(1, 0.18, night);

				float3 col = lerp(day, duskC, dusk);
				col = lerp(col, nightC, night);
				col = lerp(col, rainC, rain);

				// brilho em volta do sol (Mie) e o disco
				float sunUp = saturate(sun.y * 4 + 0.3);
				float clearK = (1 - rain) * (1 - night);
				float mie = pow(saturate(cosSun), 8.0) * 0.35 + pow(saturate(cosSun), 64.0) * 0.8;
				float3 sunTint = lerp(float3(1.0, 0.93, 0.82), float3(1.0, 0.55, 0.25), dusk);
				col += sunTint * mie * clearK * sunUp;
				float disc = smoothstep(0.99955, 0.99975, cosSun);
				col += sunTint * disc * 18.0 * clearK * sunUp;

				// estrelas e lua
				float3 sd = d * 420.0;
				float star = hash3(floor(sd));
				star = step(0.9975, star) * (0.6 + 0.4 * sin(_SkyTime * 2.0 + star * 90.0));
				col += star * night * (1 - rain) * h * 0.9;
				float cosMoon = dot(d, moon);
				float moonDisc = smoothstep(0.99935, 0.9996, cosMoon);
				float moonGlow = pow(saturate(cosMoon), 120.0) * 0.12;
				float moonLit = lerp(0.25, 1.0, saturate(_MoonPhase));
				col += float3(0.85, 0.9, 1.0) * (moonDisc * 2.2 * moonLit + moonGlow) * night * (1 - rain * 0.85);

				// --- nuvens num plano alto
				float cover = saturate(max(_Cover, rain));
				if (up > 0.0) {
					float t = 1.0 / max(up, 0.06);
					float2 uv = d.xz * t * 0.35 + _WindSky.xy * _SkyTime * 0.0025;
					float dens = CloudDensity(uv, cover);
					// nuvens somem no horizonte para não serrilhar
					dens *= saturate(up * 6.0);
					float lit = saturate(dot(normalize(float3(d.x, 0.35, d.z)), sun) * 0.5 + 0.5);
					float3 cloudLit = lerp(float3(0.62, 0.64, 0.68), float3(1.0, 0.97, 0.92), lit) * 0.95;
					cloudLit = lerp(cloudLit, float3(1.0, 0.62, 0.42) * lerp(0.5, 1.0, lit), dusk);
					cloudLit = lerp(cloudLit, float3(0.03, 0.035, 0.05), night);
					cloudLit = lerp(cloudLit, float3(0.30, 0.31, 0.33) * lerp(1, 0.15, night), rain);
					cloudLit += sunTint * pow(saturate(cosSun), 12.0) * 0.6 * clearK * (1 - dens);
					col = lerp(col, cloudLit, dens * lerp(0.85, 1.0, rain));
				} else {
					// abaixo do horizonte: tom de chão/névoa distante, nada de preto chapado
					float3 ground = lerp(horDay * 0.5, float3(0.10, 0.08, 0.07), dusk);
					ground = lerp(ground, horNight * 0.6, night);
					ground = lerp(ground, horRain * 0.7, rain);
					col = lerp(col, ground, saturate(-up * 8.0));
				}

				col = max(col * _SkyBrightness, 0);
			#if defined(UNITY_COLORSPACE_GAMMA)
				// o projeto está em espaço de cor Gamma: as cores acima são lineares
				// conversão sRGB feita aqui mesmo: o Core.hlsl do URP não inclui o Color.hlsl (LinearToSRGB não existe e o céu ficava magenta)
				float3 srgbHi = 1.055 * pow(max(col, 1e-6), 1.0 / 2.4) - 0.055;
				col = lerp(srgbHi, col * 12.92, step(col, 0.0031308));
			#endif
				return half4(col, 1);
			}
			ENDHLSL
		}
	}
	Fallback Off
}
