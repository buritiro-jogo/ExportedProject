// Resolução de sombra por luz no URP (rodada 37): Light.shadowResolution só existe no Built-In — no URP a atribuição era ignorada e
// cada uma gerava um aviso com pilha no log (366 por partida no autoteste). O equivalente é o "tier" do UniversalAdditionalLightData.
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FreeBala
{
    public static class FBLightUtil
    {
        public static void MediumShadows(Light l)
        {
            if (!l) return; var ad = l.GetUniversalAdditionalLightData(); if (ad) ad.additionalLightsShadowResolutionTier = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium;
        }
    }
}
