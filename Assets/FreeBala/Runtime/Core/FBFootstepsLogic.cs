// Passos — lógica pura (sem física/áudio, testável fora do Unity): piso pelo nome/marca e detector de CONTATO DO PÉ.
// O FBFootsteps (Unity) lê os ossos dos pés de cada personagem por fora (Animator.GetBoneTransform), sem mexer no FBCharacter.
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
    // pisos com som próprio (Resources/Audio/Steps/step_<nome minúsculo><n>.ogg)
    public enum FBStepSurf : byte { Concreto, Asfalto, Terra, Grama, Areia, Lama, Metal, Madeira, Agua, Vidro }

    public static class FBStepSurfaces
    {
        static readonly Regex rxAsf = new(@"asphalt|asfalto|road|rodovia|estrada|avenida|street|pista|piche", RegexOptions.IgnoreCase),
            rxGrass = new(@"grass|grama|gramado|lawn|mato|capim|relva|turf", RegexOptions.IgnoreCase),
            rxGlass = new(@"vidro|glass|caco", RegexOptions.IgnoreCase),
            rxWater = new(@"agua|água|water|poça|poca|puddle", RegexOptions.IgnoreCase);
        // kind = FBSurface.kind / FBWorld.SurfOf (0 concreto, 1 terra, 2 areia, 3 lama, 4 metal, 5 madeira); o nome refina grama/asfalto,
        // que o FBWorld junta com terra/concreto
        public static FBStepSurf FromName(string name, int kind)
        {
            name ??= "";
            if (rxWater.IsMatch(name) && !FBWater.NaoEAgua(name)) return FBStepSurf.Agua;   // [parceiro] verde-água, areia do rio, azulejo azul-piscina etc. não são água
            if (rxGlass.IsMatch(name)) return FBStepSurf.Vidro;
            if (kind == 0 || kind == 1) { if (rxGrass.IsMatch(name)) return FBStepSurf.Grama; if (kind == 0 && rxAsf.IsMatch(name)) return FBStepSurf.Asfalto; }
            if (kind == 0) { int k = FBWorld.SurfOf(name); if (k != 0) kind = k; }
            return kind switch { 1 => FBStepSurf.Terra, 2 => FBStepSurf.Areia, 3 => FBStepSurf.Lama, 4 => FBStepSurf.Metal, 5 => FBStepSurf.Madeira, _ => FBStepSurf.Concreto };
        }
        public static string ClipBase(FBStepSurf s) => "Steps/step_" + s.ToString().ToLowerInvariant();
        // ganho por piso (metal/madeira soam mais; grama/areia abafam)
        public static float Gain(FBStepSurf s) => s switch { FBStepSurf.Metal => 1.15f, FBStepSurf.Madeira => 1.05f, FBStepSurf.Vidro => 1.1f, FBStepSurf.Grama => .8f, FBStepSurf.Areia => .85f, FBStepSurf.Agua => 1f, _ => 1 };
    }

    // Detector de contato do pé: alimentado a cada quadro com a altura de cada pé acima da raiz do personagem (m, já sem escala).
    // (r41) Usuário: "o áudio dos passos às vezes está muito rápido, às vezes muito lento, na caminhada e na corrida". Medido (-fbaudio,
    // passos_andar/correr): os limiares FIXOS (sobe 4,5 cm / pisa a 2,2 cm de uma base que subia 6 cm/s) perdiam pisadas — correndo,
    // 7 de 10 vinham da "passada por distância" — e os dois modos se misturavam com ritmos diferentes (0,3 s × 0,65 s andando).
    // Agora o limiar é RELATIVO ao movimento real de cada pé: faixa [mín, máx] da altura nos últimos ~0,8 s (envelope que relaxa), o pé
    // "arma" acima de 55 % da faixa e "pisa" ao descer abaixo de 22 % — vale para qualquer rig, escala, clipe, andar ou correr.
    // A passada por distância só entra se os ossos estão PARADOS (Animator em culling / rig sem humanoide), nunca misturada.
    public class FBFootContact
    {
        public float walkStride = .78f, runStride = 1.55f, minSpeed = .55f;
        const float RELAX = .8f, MINAMP = .025f, ARM = .55f, HIT = .22f, MINGAP = .2f;
        readonly float[] lo = { float.NaN, float.NaN }, hi = new float[2], mean = { float.NaN, float.NaN }, lastT = { -9, -9 }; readonly bool[] armed = new bool[2];
        float t, lastMove = -9, strideAcc; int lastFoot = 1;
        public bool Fallback { get; private set; }

        public void Reset() { lo[0] = lo[1] = mean[0] = mean[1] = float.NaN; armed[0] = armed[1] = false; strideAcc = 0; }

        // retorna -1 (nada), 0 pé esquerdo, 1 pé direito
        public int Update(float dt, bool bones, float hl, float hr, float speed, bool grounded, bool run)
        {
            t += dt;
            if (!grounded) { armed[0] = armed[1] = false; strideAcc = Mathf.Min(strideAcc, .6f); return -1; }
            int ev = -1; bool moving = false;
            if (bones)
            {
                float k = Mathf.Clamp01(dt / RELAX), km = Mathf.Clamp01(dt / .5f);
                for (int f = 0; f < 2; f++)
                {
                    float h0 = f == 0 ? hl : hr; if (float.IsNaN(mean[f])) mean[f] = h0;
                    mean[f] += (h0 - mean[f]) * km; float h = h0 - mean[f];          // sem a tendência (escada/rampa/foot IK subindo)
                    if (float.IsNaN(lo[f])) { lo[f] = hi[f] = h; continue; }
                    lo[f] = h < lo[f] ? h : lo[f] + (h - lo[f]) * k;          // envelope: segue o extremo na hora, relaxa em ~0,8 s
                    hi[f] = h > hi[f] ? h : hi[f] + (h - hi[f]) * k;
                    float amp = hi[f] - lo[f]; if (amp < MINAMP) { armed[f] = false; continue; }
                    moving = true;
                    if (h > lo[f] + amp * ARM) armed[f] = true;
                    else if (armed[f] && h < lo[f] + amp * HIT && t - lastT[f] >= MINGAP && speed >= minSpeed * .6f)
                    {
                        armed[f] = false; lastT[f] = t;
                        if (ev < 0 || f != lastFoot) ev = f;
                    }
                }
                if (moving) lastMove = t;
                if (ev >= 0) { lastFoot = ev; Fallback = false; strideAcc = 0; return ev; }
                if (t - lastMove < .6f) { strideAcc = 0; return -1; }               // ossos se mexendo: só eles pisam
            }
            if (speed < minSpeed) { strideAcc = Mathf.Min(strideAcc, .6f); return -1; }
            // ossos parados (culling) ou ausentes enquanto anda: passadas por distância
            Fallback = true; strideAcc += speed * dt / (run ? runStride : walkStride);
            if (strideAcc >= 1) { strideAcc -= 1; lastFoot = 1 - lastFoot; return lastFoot; }
            return -1;
        }
    }
}
