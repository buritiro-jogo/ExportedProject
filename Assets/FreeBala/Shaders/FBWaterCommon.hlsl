// Partes comuns dos shaders de água (FBWaterJet, FBWaterDrops).
#ifndef FB_WATER_COMMON
#define FB_WATER_COMMON

float _FBNight;   // global (FBWater): 0 dia … 1 noite — liga a luz colorida das fontes

// cores dos refletores das fontes da Praça das Águas (fotos noturnas): cada grupo de jatos tem uma cor e todas trocam devagar
half3 FountainPalette(float h)
{
    h = frac(h); float k = h * 6; int i = (int)floor(k); half f = smoothstep(0.75, 1.0, frac(k));
    const half3 c0 = half3(0.15, 0.35, 1.0), c1 = half3(0.10, 0.95, 0.45), c2 = half3(1.0, 0.20, 0.75);
    const half3 c3 = half3(1.0, 0.18, 0.12), c4 = half3(1.0, 0.62, 0.12), c5 = half3(0.10, 0.85, 1.0);
    half3 a = i == 0 ? c0 : i == 1 ? c1 : i == 2 ? c2 : i == 3 ? c3 : i == 4 ? c4 : c5;
    half3 b = i == 0 ? c1 : i == 1 ? c2 : i == 2 ? c3 : i == 3 ? c4 : i == 4 ? c5 : c0;
    return lerp(a, b, f);
}
#endif
