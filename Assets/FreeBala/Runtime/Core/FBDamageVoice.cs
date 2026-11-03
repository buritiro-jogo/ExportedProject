// Voz de dor a partir do barramento de dano (FBDamageEvents). Puramente sonoro.
// Damage Event → Damage Type → Voice Profile → Variation → Playback Controller (FBVoice / FBVoiceDirector / FBVoiceProfiles).
// Só o DONO do personagem reage (d.Local): o tiro num remoto é aplicado no cliente dele, que decide a voz e a manda pela rede ("vo").
using UnityEngine;

namespace FreeBala
{
    public static class FBDamageVoice
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook() { FBDamageEvents.OnDamage -= On; FBDamageEvents.OnDamage += On; }
        // rodada 40 (27/10: "garanta que os sons funcionem tanto para o jogador quanto para aliados e inimigos"): todo personagem reage —
        // jogador, aliados, inimigos, bots, civis. O dono decide (d.Local) e o FBVoiceDirector limita vozes simultâneas/cooldown por
        // personagem; o 3D de cada voz (rolloff do banco) faz o gemido de quem está longe sumir sozinho
        static void On(FBDamage d) { if (d.Local && d.victim) FBVoice.OnDamage(d); }
    }
}
