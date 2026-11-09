// POSIÇÃO DO SOL E DA LUA (rodada 64, BuritiroEnvironmentManager).
//  · Sol: algoritmo da NOAA (Meeus simplificado, erro < 0,1°), com refração atmosférica perto do horizonte — Boa Vista fica a 2,82° N,
//    então o sol passa quase a pino; em novembro (declinação −17°) ele nasce a ~107° e se põe a ~253° (oés-sudoeste).
//  · Lua: fórmula de baixa precisão do suncalc/Astronomical Almanac (erro ~1°), fase pela elongação sol–lua.
//  · Rumo (azimute) a partir do NORTE no sentido horário; elevação em graus acima do horizonte.
//  · Direção no Unity: os mapas vêm do OSM/Blender pelo glTF e o glTFast espelha X: no Unity o LESTE é −X e o NORTE é −Z
//    (mesma conta do FBTour: Unity = (−1,2·x_leste, y, −1,2·y_norte)). Mapa com outro norte: FBEnvPreset.MapOverride.northYaw.
using UnityEngine;

namespace FreeBala
{
    public struct FBSkyBody { public float azimuth, elevation; public Vector3 dir; }   // dir = do chão PARA o astro (Unity)

    public static class FBSolar
    {
        public const float BoaVistaLat = 2.8235f, BoaVistaLon = -60.6758f, BoaVistaUtc = -4f;
        const double D2R = System.Math.PI / 180, R2D = 180 / System.Math.PI;

        // dia juliano de uma data/hora LOCAL (fuso em horas)
        public static double Julian(int year, int month, int day, double localHours, double utcOffset)
        {
            double ut = localHours - utcOffset; int y = year, m = month;
            if (m <= 2) { y -= 1; m += 12; }
            int A = y / 100, B = 2 - A + A / 4;
            return System.Math.Floor(365.25 * (y + 4716)) + System.Math.Floor(30.6001 * (m + 1)) + day + B - 1524.5 + ut / 24.0;
        }

        public static FBSkyBody Sun(double jd, float lat, float lon, float northYaw = 0)
        {
            double t = (jd - 2451545.0) / 36525.0;
            double L0 = Mod(280.46646 + t * (36000.76983 + t * 0.0003032), 360);
            double M = 357.52911 + t * (35999.05029 - 0.0001537 * t);
            double e = 0.016708634 - t * (0.000042037 + 0.0000001267 * t);
            double C = System.Math.Sin(M * D2R) * (1.914602 - t * (0.004817 + 0.000014 * t)) + System.Math.Sin(2 * M * D2R) * (0.019993 - 0.000101 * t) + System.Math.Sin(3 * M * D2R) * 0.000289;
            double om = 125.04 - 1934.136 * t, lam = L0 + C - 0.00569 - 0.00478 * System.Math.Sin(om * D2R);
            double eps0 = 23 + (26 + (21.448 - t * (46.815 + t * (0.00059 - t * 0.001813))) / 60) / 60, eps = eps0 + 0.00256 * System.Math.Cos(om * D2R);
            double dec = System.Math.Asin(System.Math.Sin(eps * D2R) * System.Math.Sin(lam * D2R));
            double ra = System.Math.Atan2(System.Math.Cos(eps * D2R) * System.Math.Sin(lam * D2R), System.Math.Cos(lam * D2R));
            // ângulo horário pelo tempo sideral (mesmo caminho da lua: evita a conta da equação do tempo)
            double gmst = Mod(280.46061837 + 360.98564736629 * (jd - 2451545.0), 360);
            double H = (gmst + lon) * D2R - ra;
            return Body(H, dec, lat, northYaw, true);
        }

        // lua: posição + fase (0 = nova, 1 = cheia; sinal negativo = minguante) e fração iluminada
        public static FBSkyBody Moon(double jd, float lat, float lon, out float phase, out float illum, float northYaw = 0)
        {
            double d = jd - 2451545.0;
            double L = (218.316 + 13.176396 * d) * D2R, M = (134.963 + 13.064993 * d) * D2R, F = (93.272 + 13.229350 * d) * D2R;
            double lng = L + 6.289 * D2R * System.Math.Sin(M), bet = 5.128 * D2R * System.Math.Sin(F);
            double ep = 23.4397 * D2R;
            double ra = System.Math.Atan2(System.Math.Sin(lng) * System.Math.Cos(ep) - System.Math.Tan(bet) * System.Math.Sin(ep), System.Math.Cos(lng));
            double dec = System.Math.Asin(System.Math.Sin(bet) * System.Math.Cos(ep) + System.Math.Cos(bet) * System.Math.Sin(ep) * System.Math.Sin(lng));
            double gmst = Mod(280.46061837 + 360.98564736629 * d, 360);
            double H = (gmst + lon) * D2R - ra;
            // fase: elongação sol–lua (longitudes eclípticas)
            double Ms = (357.5291 + 0.98560028 * d) * D2R, Cs = (1.9148 * System.Math.Sin(Ms) + 0.02 * System.Math.Sin(2 * Ms) + 0.0003 * System.Math.Sin(3 * Ms)) * D2R;
            double sunLng = Ms + Cs + 102.9372 * D2R + System.Math.PI;
            double elong = System.Math.Acos(System.Math.Cos(bet) * System.Math.Cos(lng - sunLng));
            double phaseAng = System.Math.PI - elong;                                          // 0 = cheia
            illum = (float)((1 + System.Math.Cos(phaseAng)) * .5);
            bool waxing = Mod((lng - sunLng) * R2D, 360) < 180;
            phase = (float)(1 - phaseAng / System.Math.PI) * (waxing ? 1 : -1);
            return Body(H, dec, lat, northYaw, false);
        }

        static FBSkyBody Body(double H, double dec, float lat, float northYaw, bool refraction)
        {
            double phi = lat * D2R;
            double sinEl = System.Math.Sin(phi) * System.Math.Sin(dec) + System.Math.Cos(phi) * System.Math.Cos(dec) * System.Math.Cos(H);
            double el = System.Math.Asin(System.Math.Clamp(sinEl, -1, 1)) * R2D;
            double az = System.Math.Atan2(System.Math.Sin(H), System.Math.Cos(H) * System.Math.Sin(phi) - System.Math.Tan(dec) * System.Math.Cos(phi)) * R2D + 180;   // a partir do norte, horário
            if (refraction && el > -2) el += 1.02 / System.Math.Tan((el + 10.3 / (el + 5.11)) * D2R) / 60.0;                         // Saemundsson (minutos de arco)
            var b = new FBSkyBody { azimuth = (float)Mod(az, 360), elevation = (float)el };
            b.dir = Direction(b.azimuth, b.elevation, northYaw); return b;
        }

        // rumo/elevação -> vetor no Unity (norte = −Z, leste = −X; northYaw gira o norte do mapa em torno de Y)
        public static Vector3 Direction(float azimuth, float elevation, float northYaw = 0)
        {
            float a = (azimuth + northYaw) * Mathf.Deg2Rad, e = elevation * Mathf.Deg2Rad;
            return new Vector3(-Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), -Mathf.Cos(a) * Mathf.Cos(e)).normalized;
        }
        static double Mod(double a, double b) { a %= b; return a < 0 ? a + b : a; }
    }
}
