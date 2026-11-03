// Costuras entre áreas (coordenador): liga o FBQuality (níveis por plataforma) às flags de gráficos (FBGfx), efeitos (FBFxQuality)
// e foot IK (FBFootIK.Q), para que cada sistema leia o mesmo nível sem depender diretamente dos outros.
using UnityEngine;

namespace FreeBala
{
    public static class FBIntegration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            FBGfx.FlagsOverride = t => { var f = FBGfx.Defaults(t); f.ssao &= FBQuality.SSAO; f.volClouds &= FBQuality.VolumetricClouds; f.heightFog &= FBQuality.PostFX && FBQuality.HeightFog; f.detailMaps &= FBQuality.DetailMaps; f.volumetric &= FBQuality.PostFX; f.ssr &= FBQuality.RealtimeReflections;
                float k = FBQuality.AmbientParticles; f.lampInsects = Mathf.RoundToInt(f.lampInsects * k); f.dustMotes = Mathf.RoundToInt(f.dustMotes * k); f.smokePlumes = Mathf.RoundToInt(f.smokePlumes * k); return f; };
            FBQuality.Changed -= Sync; FBQuality.Changed += Sync; Sync();
        }
        static void Sync()
        {
            FBGfx.Refresh();
            FBFxQuality.Apply(Mathf.Clamp((int)FBQuality.Level - 1, 0, 2));                 // Baixo/Médio → 0, Alto → 1, Ultra → 2
            FBFootIK.Q.FarDist = Mathf.Max(8, FBQuality.FootIKDistance);                    // distância de ativação do IK dos pés
            FBFootIK.Q.NearDist = Mathf.Min(FBFootIK.Q.FarDist * .5f, 18);
            if (FBVoice.D != null) FBVoice.D.maxVoices = FBQuality.MaxVoices;                // vozes simultâneas (o pool do FBVoice tem folga de +2)
        }
    }
}
