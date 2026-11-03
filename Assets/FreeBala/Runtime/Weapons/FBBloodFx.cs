// Sangue a partir do barramento de dano (FBDamageEvents): usa o acerto REAL (ponto na hitbox, direção do projétil, região, arma,
// dano aplicado). Puramente visual — só assina o evento, nunca muda o dano.
// Composição de um acerto de bala (referência: Blood Strike / Free Fire / IE — jato curto de saída no sentido da bala, névoa fina
// rápida, poucas gotas; nada de "tinta" nem partícula grande):
//   • saída: gotas pequenas (6–20 mm) em cone estreito no sentido da bala, esticadas pela velocidade, com gravidade;
//   • borrifo fino: muitas gotículas (3–8 mm) mais rápidas e curtas;
//   • respingo de entrada: 2–4 gotas voltando para o atirador;
//   • névoa: 1–3 nuvens finas que abrem e somem em ~0,3 s;
//   • cenário: mancha na parede atrás (direcional se o ângulo for rasante, escorrido em parede vertical) e no chão à frente;
//   • corpo: ferimento preso ao osso (FBWoundFx) + escorrido nos acertos fortes.
// Perfis: região (cabeça mais intensa, membros menores), classe da arma (escopeta = por bago, fraco; sniper = jato longo) e dano.
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public static class FBBloodFx
    {
        public static bool BodyWounds = false;   // marcas de tiro presas no corpo desligadas (pedido: estilo FF, sangue só espirra)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook() { FBDamageEvents.OnDamage -= On; FBDamageEvents.OnDamage += On; }

        // perfil = multiplicadores de quantidade, velocidade de saída, névoa, tamanho do ferimento e alcance/tamanho da mancha no cenário
        public struct Profile { public float amount, speed, mist, wound, reach, splat; public Profile(float a, float s, float m, float w, float r, float sp) { amount = a; speed = s; mist = m; wound = w; reach = r; splat = sp; } }
        public static Profile Region(string part) => part switch
        {
            "head" => new Profile(1.5f, 1.15f, 1.7f, 1.05f, 1.25f, 1.15f),
            "arm" or "leg" or "limb" => new Profile(.6f, .9f, .6f, .8f, .7f, .7f),
            _ => new Profile(1, 1, 1, 1, 1, 1),
        };
        public static Profile Weapon(string cls) => cls switch
        {
            "pistola" => new Profile(.8f, .85f, .85f, .85f, .8f, .85f),
            "submetralhadora" => new Profile(.75f, .9f, .8f, .8f, .85f, .8f),
            "escopeta" => new Profile(.4f, .75f, .55f, .7f, .6f, .6f),           // por bago (vários eventos no mesmo quadro)
            "sniper" => new Profile(1.9f, 1.6f, 1.8f, 1.25f, 1.9f, 1.4f),
            "explosivo" => new Profile(1.2f, .7f, 1.4f, 1, .8f, 1.1f),
            _ => new Profile(1, 1, 1, 1, 1, 1),
        };
        public static string ClassOf(string weaponId) => weaponId != null && GameData.Weapons.TryGetValue(weaponId, out var w) ? w.cls : "fuzil";

        // cores: sangue arterial fresco é vermelho-escuro; partícula unlit, então valores baixos (não "tinta" saturada)
        static readonly Color cDrop = new(.36f, .015f, .015f, .95f), cFine = new(.42f, .03f, .025f, .85f), cMist = new(.40f, .04f, .035f, .38f);

        static ParticleSystem psDrop, psFine, psMist; static bool ready;
        public static void Warm()
        {
            if (ready) return; ready = true; FBFxQuality.Ensure(); bool hi = FBFxQuality.PoolDecals > 100;
            var drop = FBFxLib.Mat("Particles_Alpha", FBFxLib.Tex("droplet"), Color.white, "SangueGota");
            psDrop = FBFxLib.PS("SangueGotas", drop, hi ? 500 : 180, new FBFxLib.Opt { gravity = 1, mode = ParticleSystemRenderMode.Stretch, stretch = .022f, color = FBFxLib.Fade(0, .75f, new Color(.7f, .6f, .6f)) });
            psFine = FBFxLib.PS("SangueBorrifo", drop, hi ? 400 : 140, new FBFxLib.Opt { gravity = .55f, drag = .18f, mode = ParticleSystemRenderMode.Stretch, stretch = .03f, color = FBFxLib.Fade(0, .5f) });
            psMist = FBFxLib.PS("SangueNevoa", FBFxLib.Mat("Particles_Alpha", FBFxLib.Tex("mist"), Color.white, "SangueNevoa"), hi ? 80 : 30,
                new FBFxLib.Opt { gravity = .04f, drag = .35f, size = FBFxLib.Curve(0, .55f, .35f, 1.15f, 1, 1.6f), color = FBFxLib.Fade(.08f, .25f), spin = true });
        }

        // eventos do mesmo personagem no mesmo quadro (bagos da escopeta, rajada): o 1º faz tudo, os seguintes ficam mais leves
        static readonly Dictionary<FBCharacter, (int frame, int n)> burst = new();

        static void On(FBDamage d)
        {
            if (!d.victim || !FBFx.BloodOn) return;
            switch (d.type)
            {
                case FBDamageType.Zone: case FBDamageType.Fall: return;
                case FBDamageType.Bleed: BleedDrip(d.victim); return;
            }
            FBFx.Warm(); var v = d.victim;
            var ip = d.hit.point; var dir = d.hit.dir.sqrMagnitude > .01f ? d.hit.dir.normalized : -v.transform.forward;
            string part = d.head ? "head" : d.hit.part ?? "body";
            var reg = Region(part); var wpn = d.type == FBDamageType.Explosion ? Weapon("explosivo") : Weapon(ClassOf(d.hit.weaponId));
            float dmgK = Mathf.Clamp(d.applied > 0 ? d.applied / 28f : d.amount / 28f, .45f, 1.6f);
            int k = burst.TryGetValue(v, out var b) && b.frame == Time.frameCount ? b.n + 1 : 0; burst[v] = (Time.frameCount, k);
            float heavy = k == 0 ? 1 : 1f / (1 + k * .8f);

            if (d.type == FBDamageType.Explosion || !d.hit.valid && d.type != FBDamageType.Bullet)
            {   // explosão/atropelo/finalização: vários jatos em direções sorteadas, sem ferimento de bala
                var c = v.Chest ? v.Chest.position : v.Pos + Vector3.up;
                for (int i = 0; i < 3; i++) Spray(c + Random.insideUnitSphere * .2f, FBFxLib.Cone(d.type == FBDamageType.Explosion ? (c - (d.source ? d.source.Pos : c - dir)).normalized + Vector3.up * .4f : dir, .9f), 1.1f * heavy, 1, 1.2f);
                SceneSplat(c, Vector3.down * .5f + dir * .5f, 1, 1.2f); return;
            }
            float amt = reg.amount * wpn.amount * dmgK * heavy;
            Spray(ip, dir, amt, reg.speed * wpn.speed, reg.mist * wpn.mist * (k == 0 ? 1 : .5f));
            float wsize = Random.Range(.034f, .05f) * reg.wound * wpn.wound * (part == "head" ? 1 : 1);
            if (BodyWounds) FBWoundFx.Attach(v, ip, dir, wsize, false, part);
            if (BodyWounds && k == 0 && (part == "head" || d.applied > 30 || ClassOf(d.hit.weaponId) == "sniper") && Random.value < .6f) FBWoundFx.Attach(v, ip, dir, wsize * 2.4f, true, part);
            if (k <= 1) SceneSplat(ip, dir, reg.reach * wpn.reach, reg.splat * wpn.splat * Mathf.Lerp(.8f, 1.2f, (dmgK - .45f) / 1.15f));
            if (k == 0) FBAudio.PlayAt("thud", ip, .6f);
        }

        /// <summary>Borrifo de sangue em p, no sentido da bala dir. amount ~1 = tiro de fuzil no tronco.</summary>
        public static void Spray(Vector3 p, Vector3 dir, float amount = 1, float speed = 1, float mist = 1)
        {
            if (!FBFx.BloodOn) return; Warm(); float lod = FBFxQuality.Lod(p); if (lod <= 0) return;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
            int nd = FBFxQuality.Count(9 * amount, lod), nf = FBFxQuality.Count(14 * amount, lod), nb = FBFxQuality.Count(3 * Mathf.Min(1.5f, amount), lod), nm = FBFxQuality.Count(Mathf.Clamp(1.6f * mist, 1, 4), Mathf.Max(lod, .5f));
            var exit = p + dir * .03f;
            for (int i = 0; i < nd; i++)                                       // gotas de saída
                FBFxLib.Emit(psDrop, exit + Random.insideUnitSphere * .02f, FBFxLib.Cone(dir, .32f) * Random.Range(2.2f, 5.5f) * speed + Vector3.up * Random.Range(0, .7f),
                    Random.Range(.006f, .02f), Random.Range(.28f, .62f), FBFxLib.Jit(cDrop, .25f));
            for (int i = 0; i < nf; i++)                                       // borrifo fino (rápido, curto)
                FBFxLib.Emit(psFine, exit, FBFxLib.Cone(dir, .5f) * Random.Range(3.5f, 8.5f) * speed, Random.Range(.003f, .008f), Random.Range(.12f, .3f), FBFxLib.Jit(cFine, .2f));
            for (int i = 0; i < nb; i++)                                       // respingo de entrada (volta para o atirador)
                FBFxLib.Emit(psDrop, p - dir * .02f, FBFxLib.Cone(-dir, .6f) * Random.Range(.8f, 2.2f) + Vector3.up * .4f, Random.Range(.005f, .012f), Random.Range(.2f, .4f), FBFxLib.Jit(cDrop, .2f));
            for (int i = 0; i < nm; i++)                                       // névoa fina
                FBFxLib.Emit(psMist, p + dir * Random.Range(.04f, .18f) * speed, dir * Random.Range(.4f, 1.3f) * speed + Random.insideUnitSphere * .25f,
                    Random.Range(.12f, .26f) * Mathf.Sqrt(mist), Random.Range(.18f, .38f), FBFxLib.Jit(cMist, .15f, Random.Range(.7f, 1.2f)), Random.Range(0, 360f));
        }

        /// <summary>Manchas no cenário: parede/objeto atrás da vítima no sentido da bala e/ou chão à frente.</summary>
        public static void SceneSplat(Vector3 p, Vector3 dir, float reach = 1, float size = 1)
        {
            if (!FBFx.BloodOn) return; dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.down;
            float far = 2.6f * reach;
            if (FBWorld.RayHit(p + dir * .35f, dir, far, out var h, .03f))
            {
                var n = h.normal; float cos = Mathf.Abs(Vector3.Dot(n, dir)), near = 1 - Mathf.Clamp01(h.distance / far) * .45f;
                float s = Random.Range(.22f, .42f) * size * near; var along = Vector3.ProjectOnPlane(dir, n);
                bool wall = Mathf.Abs(n.y) < .5f;
                if (cos < .55f && along.sqrMagnitude > 1e-4f)                // rasante: respingo alongado no sentido do voo
                    FBDecalFx.Place(FBDecalFx.Atlas.Blood, FBDecalFx.CellDir + Random.Range(0, 4), h.point, n, s * .8f, Vector3.Cross(along.normalized, n), Mathf.Clamp(1 / Mathf.Max(cos, .35f), 1.3f, 2.4f), Mover(h.collider));
                else if (wall && s > .26f && Random.value < .45f)            // parede: escorre para baixo
                    FBDecalFx.Place(FBDecalFx.Atlas.Blood, FBDecalFx.CellDrip + Random.Range(0, 2), h.point, n, s * 1.25f, Vector3.up, 1, Mover(h.collider));
                else FBDecalFx.Place(FBDecalFx.Atlas.Blood, FBDecalFx.CellRound + Random.Range(0, 6), h.point, n, s, null, 1, Mover(h.collider));
            }
            if (Random.value < .65f)                                         // chão à frente da vítima
            {
                var q = p + new Vector3(dir.x, 0, dir.z) * Random.Range(.35f, 1.1f) * reach;
                if (FBWorld.GroundY(q.x, p.y, q.z, .2f, 2.6f) is float gy)
                    FBDecalFx.Place(FBDecalFx.Atlas.Blood, Random.value < .5f ? FBDecalFx.CellDrops + Random.Range(0, 2) : FBDecalFx.CellRound + Random.Range(0, 6), new Vector3(q.x, gy, q.z), Vector3.up, Random.Range(.16f, .32f) * size);
            }
        }
        // colisor que se mexe (veículo, porta): o decal vira filho dele
        public static Transform Mover(Collider c) => c && (c.attachedRigidbody || c.gameObject.layer == FBWorld.LVehicle) ? c.transform : null;

        static void BleedDrip(FBCharacter v)
        {
            var p = (v.Chest ? v.Chest.position : v.Pos + Vector3.up) + Random.insideUnitSphere * .1f;
            if (FBFxQuality.Lod(p) > 0 && FBWorld.GroundY(p.x, p.y, p.z, .3f, 2.5f) is float gy)
                FBDecalFx.Place(FBDecalFx.Atlas.Blood, FBDecalFx.CellDrops + Random.Range(0, 2), new Vector3(p.x, gy, p.z), Vector3.up, Random.Range(.14f, .26f), null, 1, null, FBFxQuality.DecalLife * .6f);
        }

        /// <summary>Poça sob o corpo caído (cresce em ~5 s).</summary>
        public static void Pool(Vector3 p) { if (FBFx.BloodOn) FBDecalFx.Pool(p + Vector3.up * .01f, Random.Range(1.1f, 1.7f), Random.Range(4, 6.5f)); }
    }
}
