// Física de pneus dos carros do JOGADOR no estilo do FGear do 171 (rodada 43).
// Antes: modelo cinemático — o carro girava exatamente para onde as rodas apontavam, até um teto de giro. Preciso, mas duro e sem derrapagem.
// Agora: modelo de bicicleta com estado lateral (vl, + = esquerda) e de giro (yr, + = esquerda), pneus que saturam (Pacejka simplificado,
// pico em 7°), carga que vai para a frente na freada e para trás na arrancada, círculo de atrito (acelerar/frear gasta aderência lateral do
// eixo), freio de mão que solta a traseira, contraesterço automático (auto-alinhamento do pneu dianteiro) e assistência que segura a rodada
// (sem freio de mão o carro não roda sozinho). Abaixo de ~8 km/h e de ré: cinemático puro, como antes (manobra precisa).
// Do FGear do 171 (docs/re/171_FGEAR_valores.txt): esterço máximo 55°, limite pela velocidade (steerLimitCurve: 100 % parado → 1 % a
// 300 km/h), velocidade do volante (steerSpeedCurve 1,35 → 0 a 400 km/h), retorno ao centro 4, traseira com mais aderência lateral que a
// frente (mLateralFriction 2,0 atrás), auto-alinhamento 0,8 na frente. O resto (pico do pneu, alturas, molas da lataria) é ajuste nosso.
// Lataria: arfagem/rolagem/subida com mola-amortecedor (afunda na freada, levanta na arrancada, rola para fora na curva, quica no pouso);
// só a carroceria e o motorista (L2W) — as rodas ficam no chão.
// Comparar com o modelo antigo: FreeBala.exe -fbfisicaantiga
using UnityEngine;

namespace FreeBala
{
    public static class FBCarPhys
    {
        public static bool On = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbfisicaantiga") < 0;
        const float G = 9.8f * 1.2f, STEER_MAX = 55 * Mathf.Deg2Rad, A_PEAK = 7 * Mathf.Deg2Rad, TB = 2.65f / A_PEAK, TC = 1.3f, SELF_ALIGN = .8f;
        public static readonly Vector3 Pivot = new(0, .25f, 0);                                                        // centro de rolagem (local do carro)
        public static float SteerLimit(float kmh) => Mathf.Lerp(1, .01f, Mathf.Clamp01(kmh / 300));                 // steerLimitCurve_car
        public static float SteerSpeed(float kmh) => Mathf.Lerp(1.35f, 0, Mathf.Clamp01(kmh / 400));               // steerSpeedCurve_car
        static float Tire(float a) => Mathf.Sin(TC * Mathf.Atan(TB * a));                                            // -1..1, cai ~11 % depois do pico
        static float Sq(float x) => x * x;
        public static string Last;                                                                                     // diagnóstico (FBDriveLab)

        // volante: vai para o lado pedido na velocidade do 171 (×1,6 para o teclado) e volta ao centro com "gravity" 4
        public static float SteerIn(FBCar car, float st, float kmh, float dt)
        {
            float cur = car.stIn, target = st, rate;
            if (cur != 0 && st * cur < 0) { target = 0; rate = 4; }                                   // trocou de lado: passa pelo centro
            else if (Mathf.Abs(st) < Mathf.Abs(cur)) rate = 4;                                         // soltou
            else rate = Mathf.Max(.9f, SteerSpeed(kmh) * 1.6f) * (FBInput.Touch ? 1.3f : 1);
            return Mathf.MoveTowards(cur, target, rate * dt);
        }

        // um quadro no chão: integra vl, yr, o acoplamento em v e o yaw em subpassos de ~4 ms. ax = aceleração do motor/freio neste quadro
        public static void Lateral(FBCar car, FBDrive.K Kd, bool hb, float ax, float dt)
        {
            float wb = Mathf.Max(1.2f, car.wheelbase), hw = wb / 2, kmh = Mathf.Abs(car.v) * 3.6f / 1.2f, lim = STEER_MAX * SteerLimit(kmh);
            if (car.air) { car.yaw += car.yr * dt; car.yr *= Mathf.Exp(-dt * .6f); car.steer = car.stIn * lim; car.lim = lim; car.slipF = car.slipR = 0; return; }
            car.hbG = Mathf.Clamp(car.hbG + (hb ? -6 : 2.2f) * dt, .32f, 1);                                  // freio de mão solta a traseira; ao soltar ela volta em ~0,3 s
            float mu = 1.08f * Kd.grip, muR = mu * 1.12f * car.hbG, vAbs = Mathf.Max(Mathf.Abs(car.v), .1f);
            float lim2 = Mathf.Min(lim, Mathf.Atan(wb * mu * G / (vAbs * vAbs)) + A_PEAK * 1.1f);     // volante todo = no limite da aderência, não além
            string tr = car.tracao ?? ""; bool fDrive = tr == "FWD" || tr == "4x4", rDrive = tr != "FWD";
            float lonD = ax > 0 ? Mathf.Clamp01(ax / (mu * G)) * .8f : 0, lonB = ax < 0 ? Mathf.Min(.75f, -ax / (mu * G) * .5f) : 0;
            float capF = Mathf.Sqrt(1 - Sq(Mathf.Max(fDrive ? lonD : 0, lonB))), capR = Mathf.Sqrt(1 - Sq(Mathf.Max(rDrive ? lonD : 0, lonB * .6f))) * (car.burn ? .5f : 1);   // freio puxado para a frente
            float nF = Mathf.Clamp(.5f - .3f * Mathf.Clamp(ax, -G, .8f * G) / (wb * G), .3f, .8f);          // carga na frente (equilíbrio: traseira 12 % mais aderente = sai de frente)
            int n = Mathf.Clamp(Mathf.CeilToInt(dt / .004f), 1, 12); float h = dt / n;
            float k = car.v > 0 ? Mathf.Clamp01((car.v - 2.5f) / 4.5f) : 0;                             // ~8 → 21 km/h: entra o modelo de pneu
            float sF = 0, sR = 0, delta = 0, beta = 0, spin = hb ? 1.4f : 20 * Mathf.Deg2Rad;
            for (int i = 0; i < n; i++)
            {
                float vx = car.v, vxs = Mathf.Max(vx, 3); beta = Mathf.Atan2(car.vl - hw * car.yr, Mathf.Max(Mathf.Abs(vx), 1));   // deriva do eixo TRASEIRO
                delta = Mathf.Clamp(car.stIn * lim2 + k * SELF_ALIGN * beta, -STEER_MAX, STEER_MAX);    // β < 0 (traseira saindo p/ direita) → contraesterço
                float aF = delta - Mathf.Atan2(car.vl + hw * car.yr, vxs), aR = -Mathf.Atan2(car.vl - hw * car.yr, vxs);
                float Ff = mu * nF * G * capF * Tire(aF), Fr = muR * (1 - nF) * G * capR * Tire(aR), cd = Mathf.Cos(delta);
                float dvl = Ff * cd + Fr - vxs * car.yr, dyr = (Ff * cd - Fr) / hw, dvx = -Ff * Mathf.Sin(delta) + car.vl * car.yr;
                float bA = Mathf.Abs(beta); if (bA > spin) dyr += Mathf.Sign(beta) * (bA - spin) * 14;  // assistência: segura a rodada
                float yrK = vx / wb * Mathf.Tan(delta), vlK = yrK * hw;                                  // cinemático (eixo traseiro sem deslizar)
                car.vl = Mathf.Lerp(vlK, car.vl + dvl * h, k); car.yr = Mathf.Clamp(Mathf.Lerp(yrK, car.yr + dyr * h, k), -4, 4);
                car.v += dvx * h * k; car.yaw += car.yr * h;
                sF = Mathf.Max(sF, Mathf.Abs(aF)); sR = Mathf.Max(sR, Mathf.Abs(aR));
            }
            float Sl(float a) => k * Mathf.Clamp01((a - A_PEAK * .8f) / (A_PEAK * 2.5f));               // FGear mLatSlipThreshold 0,8
            car.slipF = Sl(sF); car.slipR = Sl(sR); car.steer = delta; car.lim = lim2; car.beta = beta * k;
            Last = $"{kmh:0} km/h · volante {car.stIn:+0.00;-0.00} · roda {delta * Mathf.Rad2Deg:+0.0;-0.0}° (lim {lim2 * Mathf.Rad2Deg:0.0}°) · β {beta * Mathf.Rad2Deg:+0.0;-0.0}° · giro {car.yr * Mathf.Rad2Deg:0}°/s · lat {car.v * car.yr / G:0.00} g · deriva F/T {car.slipF:0.00}/{car.slipR:0.00}";
        }
        // batida: corta deslize e giro
        public static void OnHit(FBCar car, float k = .4f) { car.vl *= k; car.yr *= k; }

        // ---- lataria (mola-amortecedor). ax = aceleração longitudinal medida, ay = lateral (+ esquerda)
        public static void Body(FBCar car, float ax, float ay, float dt)
        {
            if (!car.shell) return;
            float kh = car.heavy ? 1.5f : 1;
            float tP = Mathf.Clamp(-ax * .0022f * kh, -.06f, .06f), tR = Mathf.Clamp(-ay * .0048f * kh, -.08f, .08f);   // freada: nariz desce; curva à esq.: lado direito desce
            Spring(ref car.bPit, ref car.bPitV, tP, 8.5f, .45f, dt); Spring(ref car.bRol, ref car.bRolV, tR, 7.5f, .4f, dt); Spring(ref car.bHv, ref car.bHvV, 0, 10, .35f, dt);
            car.bHv = Mathf.Clamp(car.bHv, -.1f, .06f);
            car.bodyQ = Quaternion.Euler(car.bPit * Mathf.Rad2Deg, 0, car.bRol * Mathf.Rad2Deg); car.bodyHv = car.bHv; car.bodyOn = true;
            var T = car.transform; var sp = car.shell.parent; if (!sp) return;
            var cpos = T.InverseTransformPoint(sp.TransformPoint(car.shellP0)); var crot = Quaternion.Inverse(T.rotation) * sp.rotation * car.shellR0;
            car.shell.SetPositionAndRotation(T.TransformPoint(Pivot + car.bodyQ * (cpos - Pivot) + Vector3.up * car.bHv), T.rotation * car.bodyQ * crot);
        }
        static void Spring(ref float x, ref float v, float t, float w, float z, float dt)
        {
            int n = dt > .02f ? 3 : 1; float h = dt / n;
            for (int i = 0; i < n; i++) { v += (w * w * (t - x) - 2 * z * w * v) * h; x += v * h; }
        }
        // saiu do carro / garagem: lataria de volta ao lugar, estado lateral zerado
        public static void Reset(FBCar car)
        {
            if (!car) return; car.vl = car.yr = car.slipF = car.slipR = car.beta = 0; car.hbG = 1;
            car.bPit = car.bPitV = car.bRol = car.bRolV = car.bHv = car.bHvV = 0; car.bodyQ = Quaternion.identity; car.bodyHv = 0;
            if (car.bodyOn && car.shell) { car.shell.localPosition = car.shellP0; car.shell.localRotation = car.shellR0; }
            car.bodyOn = false;
        }
    }
}
