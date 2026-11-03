// PERCEPÇÃO E MIRA DOS BOTS (rodada 37 — "os bots atiram de longe demais, levo tiro sem saber de onde vem").
// Causas no FBBot antigo: visão até 220 m e alvo escolhido até 160 m; tiro decidido por SORTEIO (acertou = bala vai exatamente no
// ponto; 16 % de acerto mesmo a 100+ m); olho do bot 22 cm à frente do corpo (encostado na parede, o raio de visão saía do outro lado);
// percepção binária (viu = já sabe e atira em 0,14–0,32 s, de qualquer distância).
// Agora, por bot:
//  · CONSCIÊNCIA por alvo (0..1): sobe com a visibilidade (campo de visão, 3 raios — cabeça, peito, quadril — a partir do olho DENTRO
//    do corpo, postura, movimento, disparo recente, noite, distância) e cai sem contato; só ao chegar em 1 o alvo é "visto". Perto é
//    quase imediato (0,3 s a 8 m); a 80 m leva ~2 s e só se o alvo estiver exposto/em movimento/atirando.
//  · ALCANCE DE COMBATE pela classe da arma (pistola 32 m, SMG 42, escopeta 20, fuzil 68, sniper 130); além disso o bot se aproxima
//    (ou procura cobertura mais perto) em vez de atirar; nunca atira além de 1,15× o alcance.
//  · MIRA com ERRO ANGULAR que converge: grande ao adquirir o alvo e diminui em ~0,6 s; aumenta com o bot andando, o alvo andando de
//    lado (antecipação imperfeita) e a dificuldade. A bala real (FBCombat) decide parede × corpo — longe erra naturalmente.
//  · Ouvir tiro (até 70 m) leva a investigar o ponto aproximado, não a "saber" onde o jogador está.
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public static class FBBotSense
    {
        // dificuldade (FBSession.diff 0 fácil, 1 normal, 2 difícil)
        static int D => Mathf.Clamp(FBSession.diff, 0, 2);
        // calibrado no autoteste -fbr37 (clássico, 120 s, 1.442 tiros): com 2,8/1,85/1,2 os bots acertavam 9 % a 0–15 m e 4 % a 15–35 m
        public static float AimBaseDeg => new[] { 2.3f, 1.45f, 1f }[D];               // erro de mira assentado (graus, 1 desvio)
        public static float ReactK => new[] { 1.35f, 1f, .78f }[D];                    // tempo para perceber/reagir
        public static float RangeK => new[] { .85f, 1f, 1.12f }[D];
        public static float HeadAim => new[] { .02f, .06f, .12f }[D];

        public static float EngageRange(WeaponDef w)
        {
            float r = w == null ? 25 : w.cls switch { "pistola" => 32, "submetralhadora" => 42, "escopeta" => 20, "sniper" => 130, "explosivo" => 45, _ => 68 };
            return r * RangeK;
        }
        // alcance máximo de visão: mapa urbano (becos) e noite
        public static float SightMax { get { float n = FBWeather.NightK; return Mathf.Lerp(120, 65, Mathf.InverseLerp(.4f, .95f, n)); } }

        // olho do bot (dentro do corpo — nunca à frente, para o raio não nascer do outro lado da parede)
        public static Vector3 Eye(FBCharacter ch)
        {
            if (ch.Head) { var h = ch.Head.position; var axis = new Vector3(ch.Pos.x, h.y + .04f * ch.ScaleK, ch.Pos.z); return Vector3.Lerp(axis, h, .35f); }   // 65 % no eixo da cápsula
            return ch.Pos + Vector3.up * (ch.crouch ? 1.15f : 1.62f) * ch.ScaleK;
        }
        // pontos do alvo: cabeça, peito, quadril (postura)
        public static void TargetPoints(FBCharacter t, Vector3[] p)
        {
            float s = t.ScaleK;
            if (t.Head) p[0] = t.Head.position; else p[0] = t.Pos + Vector3.up * (t.prone ? .45f : t.crouch ? 1.25f : 1.65f) * s;
            p[1] = t.Chest ? t.Chest.position : t.Pos + Vector3.up * (t.prone ? .35f : t.crouch ? .9f : 1.3f) * s;
            p[2] = t.Hips ? t.Hips.position : t.Pos + Vector3.up * (t.prone ? .25f : t.crouch ? .6f : .95f) * s;
        }
        static readonly Vector3[] pts = new Vector3[3];
        // visibilidade instantânea 0..1 (0 = fora do campo de visão ou totalmente coberto); vis = fração de pontos à vista
        public static float Visibility(FBCharacter self, float yawRad, bool alert, FBCharacter t, out float dist, out int seenPts, out int mask)
        {
            dist = 9999; seenPts = 0; mask = 0; if (!t || !t.alive) return 0;
            var eye = Eye(self); TargetPoints(t, pts); var c = pts[1]; dist = (c - eye).magnitude; if (dist > SightMax) return 0;
            // campo de visão: frontal 120° (patrulha) / 200° (combate); muito perto (5 m) ouve/percebe em qualquer direção
            var f = new Vector3(Mathf.Sin(yawRad), 0, Mathf.Cos(yawRad)); var d2 = (c - eye).Flat().normalized; float dot = Vector3.Dot(f, d2);
            if (dist > 5 && dot < (alert ? -.17f : .5f)) return 0;
            for (int k = 0; k < 3; k++) { var v = pts[k] - eye; float L = v.magnitude; if (L < .05f || !FBWorld.RayHit(eye, v / L, L - .3f, .04f)) { seenPts++; mask |= 1 << k; } }
            if (seenPts == 0) return 0;
            float vis = seenPts / 3f;
            vis *= t.prone ? .4f : t.crouch ? .7f : 1;
            if (t.velocity.Flat().magnitude > 1) vis *= 1.35f;
            bool firing = t.lastShot < 1.2f; if (firing) vis *= 2.2f;
            float night = FBWeather.NightK; if (night > .5f && !firing && dist > 12) vis *= Mathf.Lerp(1, .5f, Mathf.InverseLerp(.5f, .95f, night));
            vis *= Mathf.Clamp01(1.25f - dist / SightMax);                                // longe some aos poucos
            if (dist > 5 && dot < .5f) vis *= .55f;                                       // canto do olho
            return Mathf.Clamp01(vis);
        }
        // segundos para perceber um alvo totalmente exposto a esta distância
        public static float NoticeTime(float dist) => Mathf.Lerp(.28f, 2.1f, Mathf.Clamp01(dist / 80f)) * ReactK;

        // erro de mira (graus): assentamento desde a aquisição + bot andando + alvo andando de lado
        public static float AimErrorDeg(FBCharacter self, FBCharacter t, float sinceAcquire, bool selfMoving)
        {
            float e = AimBaseDeg * (1 + 2f * Mathf.Exp(-sinceAcquire / .5f));
            if (selfMoving) e *= 1.35f; if (self.crouch) e *= .82f;
            var to = (t.Pos - self.Pos).Flat(); float d = Mathf.Max(4, to.magnitude); var lat = t.velocity.Flat() - Vector3.Project(t.velocity.Flat(), to.normalized);
            e += lat.magnitude / d * Mathf.Rad2Deg * .3f;                                // antecipação imperfeita do movimento lateral
            return e;
        }
        // direção com erro (distribuição ~normal no cone)
        public static Vector3 Scatter(Vector3 dir, float sigmaDeg)
        {
            var r = Vector3.Cross(dir, Mathf.Abs(dir.y) > .95f ? Vector3.right : Vector3.up).normalized; var u = Vector3.Cross(r, dir);
            float g1 = Gauss(), g2 = Gauss(); float a = sigmaDeg * Mathf.Deg2Rad;
            return (dir + r * Mathf.Tan(g1 * a) + u * Mathf.Tan(g2 * a * .8f)).normalized;
        }
        static float Gauss() { float u1 = Mathf.Max(1e-6f, Random.value), u2 = Random.value; return Mathf.Clamp(Mathf.Sqrt(-2 * Mathf.Log(u1)) * Mathf.Cos(2 * Mathf.PI * u2), -2.6f, 2.6f); }
    }
}
