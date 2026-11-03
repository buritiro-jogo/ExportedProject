// Vento da vegetação (FBFoliage.shader): incluir DEPOIS do LitInput.hlsl (usa as funções de transformação do URP).
#ifndef FB_WIND_INCLUDED
#define FB_WIND_INCLUDED
// vento: deslocamento em espaço de mundo (ver cabeçalho)
float4 _FBWind; float _FBWindGround;   // altura do chão do mapa (FBWeather)
float3 FBWindOffset(float3 posWS)
{
    float h = max(posWS.y - _FBWindGround - 0.15, 0.0);
    float bend = saturate(h * 0.11); bend *= bend;                                   // ~0 no chão, 1 a ~9 m
    float t = _Time.y;
    float ph = dot(posWS.xz, float2(0.071, 0.053));                                   // rajada que atravessa o mapa
    float gust = 0.6 + 0.4 * sin(t * 0.37 + ph * 0.6) * sin(t * 0.11 + ph * 1.3);
    float sway = sin(t * 1.25 + ph) * 0.65 + sin(t * 2.3 + ph * 1.9) * 0.22 + 0.45;  // leve viés a favor do vento
    float2 dir = _FBWind.xy;
    float3 off = float3(dir.x, 0, dir.y) * (sway * gust * bend * _FBWind.z * 0.22);
    float fl = sin(t * 7.1 + dot(posWS, float3(2.3, 1.7, 2.9))) * cos(t * 4.3 + dot(posWS.zx, float2(3.1, 1.3)));
    off += float3(fl, fl * 0.6, -fl) * (0.022 * _FBWind.w * saturate(h - 1.0));
    return off;
}
#if defined(_FB_VEG)
// tufos instanciados (FBVegetation): balanço pela altura acima da base do próprio tufo, encolhe até sumir no fim do alcance
// (_FBVegFade: x início, y fim, em metros da câmera) e manchas de tom no mundo (grama mais seca/mais verde por região)
float4 _FBVegFade;
float3 FBVegOS(float3 posOS)
{
    float3 org = TransformObjectToWorld(float3(0, 0, 0));
    float d = distance(org, _WorldSpaceCameraPos);
    posOS *= saturate((_FBVegFade.y - d) / max(_FBVegFade.y - _FBVegFade.x, 1e-3));
    float3 ws = TransformObjectToWorld(posOS);
    float hl = max(ws.y - org.y, 0.0), t = _Time.y, ph = dot(org.xz, float2(0.21, 0.17));
    float s = (sin(t * 1.9 + ph) * 0.6 + sin(t * 3.3 + ph * 1.7) * 0.3 + 0.35) * (0.55 + 0.45 * sin(t * 0.4 + dot(org.xz, float2(0.05, 0.04))));
    float3 off = float3(_FBWind.x, 0, _FBWind.y) * (s * hl * hl * 0.45 * _FBWind.z);
    off.y = -length(off) * 0.3;                                                       // a ponta desce um pouco ao dobrar
    return posOS + mul((float3x3)GetWorldToObjectMatrix(), off);
}
float FBVegHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
float FBVegNoise(float2 p) { float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f); return lerp(lerp(FBVegHash(i), FBVegHash(i + float2(1, 0)), f.x), lerp(FBVegHash(i + float2(0, 1)), FBVegHash(i + float2(1, 1)), f.x), f.y); }
half3 FBVegTint(float3 posWS)
{
    float n = FBVegNoise(posWS.xz * 0.09) * 0.65 + FBVegNoise(posWS.xz * 0.37 + 11.0) * 0.35;
    return lerp(half3(1.10, 1.02, 0.74), half3(0.84, 1.0, 0.88), smoothstep(0.25, 0.75, n));   // seco/amarelado <-> verde escuro
}
#endif
float3 FBWindOS(float3 posOS)
{
#if defined(_FB_VEG)
    return FBVegOS(posOS);
#else
    float3 ws = TransformObjectToWorld(posOS);
    return posOS + mul((float3x3)GetWorldToObjectMatrix(), FBWindOffset(ws));
#endif
}
#endif
