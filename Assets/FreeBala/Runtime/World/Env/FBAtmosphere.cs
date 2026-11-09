// ATMOSFERA FÍSICA na CPU (rodada 64): o mesmo modelo de espalhamento simples do FBSky.shader (Rayleigh + Mie + absorção do ozônio,
// terra esférica de 6360 km, atmosfera de 100 km), usado para que o CÉU ILUMINE a cena de verdade:
//  · Transmittance(elev): cor e força do sol que chega ao chão (pôr do sol alaranjado/vermelho sai da conta, não de uma tabela);
//  · Radiance(dir, sol): brilho do céu numa direção — amostrado em 48 direções vira a sonda de luz ambiente (SH) e a cor da neblina;
//  · tudo em unidades relativas (sol fora da atmosfera = 1). A calibração para as intensidades do jogo fica no BuritiroEnvironmentManager.
// Coeficientes: Bruneton 2017 / Hillaire 2020 (valores de referência da Terra, "clear sky"). Boa Vista: ar seco e com fumaça de
// queimada na estiagem → turbidez (Mie) configurável no preset (1 = céu limpo de referência).
using UnityEngine;

namespace FreeBala
{
    public static class FBAtmosphere
    {
        const float Rg = 6360e3f, Rt = 6460e3f, HR = 8000f, HM = 1200f;
        static readonly Vector3 BetaR = new(5.802e-6f, 13.558e-6f, 33.1e-6f);
        static readonly Vector3 BetaOz = new(0.650e-6f, 1.881e-6f, 0.085e-6f);
        const float BetaMs = 3.996e-6f, BetaMe = 4.40e-6f;
        public static float Turbidity = 1f, MieG = .8f;

        static Vector3 Exp(Vector3 v) => new(Mathf.Exp(v.x), Mathf.Exp(v.y), Mathf.Exp(v.z));
        static Vector3 Mul(Vector3 a, Vector3 b) => new(a.x * b.x, a.y * b.y, a.z * b.z);

        // profundidade óptica (Rayleigh, Mie, ozônio) de um ponto na altura h até o topo, na direção com cosseno mu em relação à vertical
        static void Depth(float h, float mu, int n, out float dR, out float dM, out float dO)
        {
            dR = dM = dO = 0; float r = Rg + h;
            float disc = r * r * (mu * mu - 1) + Rt * Rt; if (disc < 0) disc = 0;
            float L = -r * mu + Mathf.Sqrt(disc);
            // atrás da terra: raio que cruza o chão fica opaco
            float dg = r * r * (mu * mu - 1) + Rg * Rg;
            if (mu < 0 && dg >= 0) { dR = dM = dO = 1e9f; return; }
            float ds = L / n;
            for (int i = 0; i < n; i++)
            {
                float t = (i + .5f) * ds; float hh = Mathf.Sqrt(r * r + t * t + 2 * r * mu * t) - Rg;
                float eR = Mathf.Exp(-hh / HR), eM = Mathf.Exp(-hh / HM), eO = Mathf.Max(0, 1 - Mathf.Abs(hh - 25000f) / 15000f);
                dR += eR * ds; dM += eM * ds; dO += eO * ds;
            }
        }

        // transmitância do sol até um observador no chão (altura h) com o sol na elevação el (graus)
        public static Vector3 Transmittance(float elevDeg, float h = 2f)
        {
            float mu = Mathf.Sin(elevDeg * Mathf.Deg2Rad);
            Depth(h, mu, 24, out var dR, out var dM, out var dO);
            if (dR > 1e8f) return Vector3.zero;
            return Exp(-(BetaR * dR + Vector3.one * (BetaMe * Turbidity * dM) + BetaOz * dO));
        }

        // brilho do céu (espalhamento simples) na direção d (Unity, y = cima) com o sol na direção s
        public static Vector3 Radiance(Vector3 d, Vector3 s, int nv = 12, int nl = 6)
        {
            d.Normalize(); s.Normalize(); float h0 = 2f, r0 = Rg + h0;
            float mu = d.y, disc = r0 * r0 * (mu * mu - 1) + Rt * Rt;
            float L = -r0 * mu + Mathf.Sqrt(Mathf.Max(0, disc));
            float dg = r0 * r0 * (mu * mu - 1) + Rg * Rg;
            if (mu < 0 && dg >= 0) L = Mathf.Min(L, -r0 * mu - Mathf.Sqrt(dg));            // até o chão
            L = Mathf.Min(L, 300e3f);
            float nu = Vector3.Dot(d, s), pR = 3f / (16f * Mathf.PI) * (1 + nu * nu);
            float g = MieG, g2 = g * g, pM = 3f / (8f * Mathf.PI) * ((1 - g2) * (1 + nu * nu)) / ((2 + g2) * Mathf.Pow(Mathf.Max(1e-4f, 1 + g2 - 2 * g * nu), 1.5f));
            Vector3 accR = Vector3.zero, accM = Vector3.zero; float oR = 0, oM = 0, oO = 0;
            // passos crescentes (mais perto do observador, onde o ar é denso)
            float prev = 0;
            for (int i = 1; i <= nv; i++)
            {
                float t = L * Mathf.Pow(i / (float)nv, 2f), ds = t - prev, tm = (t + prev) * .5f; prev = t;
                var p = new Vector3(0, r0, 0) + d * tm; float r = p.magnitude, hh = r - Rg; if (hh < 0) break;
                float eR = Mathf.Exp(-hh / HR) * ds, eM = Mathf.Exp(-hh / HM) * ds, eO = Mathf.Max(0, 1 - Mathf.Abs(hh - 25000f) / 15000f) * ds;
                oR += eR; oM += eM; oO += eO;
                float muS = Vector3.Dot(p / r, s);
                Depth(hh, muS, nl, out var lR, out var lM, out var lO); if (lR > 1e8f) continue;
                var tau = BetaR * (oR + lR) + Vector3.one * (BetaMe * Turbidity * (oM + lM)) + BetaOz * (oO + lO);
                var T = Exp(-tau);
                accR += T * eR; accM += T * eM;
            }
            return Mul(accR, BetaR) * pR + accM * (BetaMs * Turbidity * pM);
        }

        // iluminação ambiente do céu: SH de 2ª ordem (projeção de 48 direções da esfera de cima) + chão refletindo (albedo)
        static readonly Vector3[] dirs = Fib(48);
        static Vector3[] Fib(int n)
        {
            var r = new Vector3[n]; float ga = Mathf.PI * (3 - Mathf.Sqrt(5));
            for (int i = 0; i < n; i++) { float y = 1 - (i + .5f) / n; float rr = Mathf.Sqrt(1 - y * y); r[i] = new Vector3(Mathf.Cos(ga * i) * rr, y, Mathf.Sin(ga * i) * rr); }   // só o hemisfério de cima (y > 0)
            return r;
        }
        // devolve: céu (zênite), horizonte (média em volta), chão (rebatido) e a irradiância do céu num plano horizontal
        public static void SkyAmbient(Vector3 sun, Vector3 sunIrr, float groundAlbedo, out Vector3 zenith, out Vector3 horizon, out Vector3 ground, out Vector3 skyIrr)
        {
            zenith = horizon = Vector3.zero; skyIrr = Vector3.zero; int nz = 0, nh = 0;
            foreach (var d in dirs)
            {
                var L = Radiance(d, sun, 8, 4);
                skyIrr += L * d.y;                                                                  // cosseno
                if (d.y > .5f) { zenith += L; nz++; } else if (d.y < .25f) { horizon += L; nh++; }
            }
            skyIrr *= 2 * Mathf.PI / dirs.Length;
            zenith /= Mathf.Max(1, nz); horizon /= Mathf.Max(1, nh);
            float sunUp = Mathf.Max(0, sun.y);
            var Eg = sunIrr * sunUp + skyIrr;                                                       // irradiância no chão
            ground = Eg * (groundAlbedo / Mathf.PI);                                                // chão lambertiano rebatendo luz para cima
        }
        public static float Lum(Vector3 c) => .2126f * c.x + .7152f * c.y + .0722f * c.z;
    }
}
