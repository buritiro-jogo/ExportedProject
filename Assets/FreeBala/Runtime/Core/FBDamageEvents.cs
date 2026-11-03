// Barramento de eventos de dano: FBCombat.DamageActor calcula o dano (autoridade) e publica UM evento por dano aplicado.
// Efeitos são assinantes puramente visuais/sonoros e nunca alteram o cálculo:
//   FBBloodFx (sangue/impacto no corpo e no cenário), FBDamageVoice (voz de dor por tipo de dano e perfil de voz),
//   HUD/rede/outros podem assinar sem tocar no FBCombat.
// Damage Event → Damage Type → (assinantes: sangue, voz, HUD...)
using System;
using UnityEngine;

namespace FreeBala
{
    public enum FBDamageType { Bullet, Zone, Fall, Bleed, Explosion, Vehicle, Takedown, Environment, Other }

    // dados reais do acerto (hit detection), quando existem
    public struct FBHitInfo
    {
        public Vector3 point, normal, dir;   // ponto na hitbox, normal da superfície atingida, direção do projétil
        public string part;                  // "head" | "body" | "arm" | "leg" | ... (FBHitbox.part)
        public string weaponId; public float dist;
        public bool valid;
        public bool armorDone; public float ap;   // r51: colete já descontado pelo FBDamageModel · penetração de colete da arma (rede)
    }

    public struct FBDamage
    {
        public FBCharacter victim, source;
        public FBDamageType type;
        public float amount;                 // dano pedido (antes de colete etc. já descontado)
        public float applied;                // vida efetivamente removida (0..hpBefore)
        public float hpBefore, hpAfter;
        public bool head, killed, knocked;
        public FBHitInfo hit;                // hit.valid = false para dano sem projétil (zona, queda...)
        public string why;                   // motivo original (compatibilidade)
        public bool Local => victim && !victim.isRemote;
    }

    public static class FBDamageEvents
    {
        public static event Action<FBDamage> OnDamage;
        public static FBDamageType TypeOf(string why, FBHitInfo hit) => why switch
        {
            "zone" => FBDamageType.Zone, "fall" => FBDamageType.Fall, "bleed" => FBDamageType.Bleed, "car" => FBDamageType.Vehicle,
            "takedown" => FBDamageType.Takedown, "explosion" or "gunner" or "heli" => FBDamageType.Explosion,
            null => hit.valid ? FBDamageType.Bullet : FBDamageType.Other, _ => FBDamageType.Environment
        };
        public static void Raise(in FBDamage d)
        {
            var h = OnDamage; if (h == null) return;
            foreach (Action<FBDamage> f in h.GetInvocationList()) { try { f(d); } catch (Exception e) { Debug.LogException(e); } }   // um assinante com erro não derruba os outros
        }
    }
}
