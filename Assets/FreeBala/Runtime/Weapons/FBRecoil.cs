// Recuo + dispersão do jogador — porte direto do recoil.js (mesmos números por classe, vindos do fb_data.json).
// Cada tiro empurra a MIRA num padrão próprio da arma (aprendível); o que o jogador não compensa volta sozinho depois de parar de atirar.
using UnityEngine;

namespace FreeBala
{
    public class FBRecoil
    {
        const float KICK = .75f, BLOOM = .85f;
        public float p, y, qp, qy, n, idle = 9, delay = .12f, rec = 6, bloom, bloomDecay = 2.2f;
        public float lastKick;                                                  // subida (graus) do último tiro — o coice visual da câmera usa (tpsKick do main.js)
        public static bool TestNoSpread;                                        // testes/vitrine: cone zero para medir câmera × retículo × impacto
        public void Reset() { p = y = qp = qy = 0; n = 0; idle = 9; bloom = 0; }
        static float Hash(int seed, int k) { float x = Mathf.Sin(seed * 12.9898f + k * 78.233f) * 43758.5453f; return (x - Mathf.Floor(x)) * 2 - 1; }
        static int Seed(string id) { int h = 7; foreach (var c in id) h = (h * 31 + c) % 9973; return h; }
        static RecoilClass C(WeaponDef w) => GameData.Recoil.TryGetValue(w.cls, out var c) ? c : GameData.Recoil.TryGetValue("fuzil", out var f) ? f : new RecoilClass { v = .58f, h = .42f, ramp = 8, spread = 1.25f, ads = .26f, bloom = .1f, bloomMax = 3, rec = 6.5f };

        public struct State { public bool aiming, crouch, moving, air; public float zoom, grip, recV, recH, spreadHip, spreadAds; }   // r45: acessórios (0 = sem efeito)

        public float Spread(WeaponDef w, State st)
        {
            if (TestNoSpread) return 0;
            var c = C(w); float s = c.spread * (w.spread / .25f);
            if (st.aiming) s *= c.ads * Json.F(FBWeaponModel.Scope(w), "multiplierSpread", .1f) / .1f;   // a luneta escolhida também fecha o cone (recoil.js)
            s *= st.aiming ? (st.spreadAds > 0 ? st.spreadAds : 1) : (st.spreadHip > 0 ? st.spreadHip : 1);   // r45: empunhadura/laser/bipé
            if (st.crouch) s *= .7f;
            s += st.moving ? (st.aiming ? .18f : .75f) : 0;
            if (st.air) s += 2.5f;
            s += bloom * (st.aiming ? .6f : 1) * (st.crouch ? .8f : 1);
            return s;
        }

        public float Shot(WeaponDef w, State st)
        {
            var c = C(w); int k = (int)n++; int sd = Seed(w.id);
            idle = 0; delay = Mathf.Clamp(w.Interval * 1.6f, .1f, .45f); rec = c.rec;
            float dmg0 = w.Damage(0, false), power = Mathf.Clamp(Mathf.Pow(Mathf.Max(1, dmg0) / 25f, .3f), .85f, 1.4f), rf = w.auto ? Mathf.Clamp(Mathf.Sqrt(600f / Mathf.Max(1, w.rpm)), .8f, 1.5f) : 1;
            float m = 1; if (st.aiming) m *= .72f; if (st.crouch) m *= .8f; if (st.moving) m *= 1.15f; if (st.air) m *= 1.5f;
            m /= Mathf.Sqrt(Mathf.Max(1, st.zoom)); m *= st.grip > 0 ? st.grip : 1; if (w.IsSniper && w.auto) m *= .5f;
            float wf = w.recoilScale * KICK;
            float v = c.v * wf * power * rf * (.8f + .45f * Mathf.Min(1, k / Mathf.Max(1, c.ramp))) * m * (1 + (Random.value - .5f) * .1f) * (st.recV > 0 ? st.recV : 1);
            float freq = .5f + .4f * Mathf.Abs(Hash(sd, 1)), ph = Hash(sd, 2) * Mathf.PI, bias = Hash(sd, 3) > 0 ? 1 : -1;
            float h = c.h * wf * m * (bias * .32f * Mathf.Min(1, k / 6f) + .65f * Mathf.Sin(k * freq + ph) + .35f * Hash(sd, 10 + k) + (Random.value - .5f) * .25f) * (st.recH > 0 ? st.recH : 1);
            qp += v; qy += h;                                                   // graus (no web: rad); positivo = sobe / direita
            bloom = Mathf.Min(c.bloomMax, bloom + c.bloom * BLOOM * (st.aiming ? .7f : 1));
            lastKick = v; return v;
        }

        // por quadro: quanto somar em pitch (para cima) e yaw (graus)
        public Vector2 Step(float dt)
        {
            float k = 1 - Mathf.Exp(-dt * 45), dp = qp * k, dy = qy * k;
            qp -= dp; qy -= dy; p += dp; y += dy; idle += dt;
            if (idle > delay)
            {
                float r = 1 - Mathf.Exp(-dt * rec), mnd = .02f * Mathf.Rad2Deg * dt;
                float rp = p * r + Mathf.Sign(p) * Mathf.Min(Mathf.Abs(p), mnd), ry = y * r + Mathf.Sign(y) * Mathf.Min(Mathf.Abs(y), mnd);
                p -= rp; y -= ry; dp -= rp; dy -= ry; n = Mathf.Max(0, n - dt * 9); bloom = Mathf.Max(0, bloom - dt * bloomDecay);
            }
            return new Vector2(dp, dy);
        }

        // o jogador puxando o mouse contra o recuo "paga" o que acumulou
        public void Look(float dyaw, float dpitch)
        {
            if (dpitch < 0 && p > 0) p = Mathf.Max(0, p + dpitch);
            if (y != 0 && dyaw * y < 0) y = Mathf.Abs(dyaw) >= Mathf.Abs(y) ? 0 : y + dyaw;
        }
    }
}
