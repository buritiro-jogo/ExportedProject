// BURITIRO: vento e sumiço por distância da vegetação (FreeBala/Foliage).
// Globais vindos do jogo: _FBWind (xy direção, z força, w ritmo) e _FBVegFade (x começo, y fim em metros).
#ifndef FB_FOLIAGE_WIND_INCLUDED
#define FB_FOLIAGE_WIND_INCLUDED

float4 _FBWind;
float4 _FBVegFade;

float3 FBFoliageOS(float3 positionOS)
{
    float3 originWS = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
    float3 posWS = TransformObjectToWorld(positionOS);

    // some aos poucos encolhendo para a base, sem estalo (entre x e y metros da câmera)
    float fadeK = 1.0;
    if (_FBVegFade.y > _FBVegFade.x + 0.01)
    {
        float dist = distance(originWS, GetCameraPositionWS());
        fadeK = 1.0 - smoothstep(_FBVegFade.x, _FBVegFade.y, dist);
        posWS = lerp(originWS, posWS, fadeK);
    }

    float strength = _FBWind.z;
    if (strength > 0.0)
    {
        float h = max(0.0, posWS.y - originWS.y);
        float bend = min(h * h * 0.012, 0.45) * strength;
        float t = _Time.y * max(_FBWind.w, 0.3);
        float phase = dot(originWS.xz, float2(0.37, 0.71));
        float sway = sin(t * 1.3 + phase) * 0.55 + sin(t * 2.9 + phase * 1.7) * 0.2 + 0.3;
        float2 dir = _FBWind.xy;
        posWS.xz += dir * (bend * sway);
        // folhas tremendo um pouco
        float flutter = sin(t * 7.0 + dot(posWS, float3(3.1, 2.3, 4.7))) * 0.012 * strength * saturate(h * 0.5);
        posWS += float3(flutter, flutter * 0.5, -flutter);
    }
    return TransformWorldToObject(posWS);
}

#endif
