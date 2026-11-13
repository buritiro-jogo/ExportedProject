// Jogador remoto: estados 'st' guardados com horário de chegada e mostrados no passado (interpolação, como o net.js), com atraso
// ADAPTATIVO (intervalo médio entre pacotes + jitter, 80–300 ms), extrapolação curta pela velocidade quando o pacote atrasa
// (até 250 ms, depois segura) e correção suave ao voltar, teleporte (renascer) sem deslizar, veículo interpolado como no web.
// Campos opcionais: o Unity só reenvia arma/dança quando mudam (e tudo a cada 1 s) — aqui vale o último valor recebido.
// Também replica: mira (ay/pi: guinada e arfagem, para o tronco), inclinação (ln), dança (dn), vida e flags.
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public class FBRemote : MonoBehaviour
    {
        public int netId; public FBCharacter ch; public string displayName;
        [System.NonSerialized] public float aimYaw, aimPitch;                  // graus (Unity); arfagem positiva = olhando para cima — para o rig (Agente 2)
        struct Snap { public float at, yaw, ayaw, pitch, lean, hp; public Vector3 p, v; public int f; public string w, dn; public List<object> ve; }
        readonly List<Snap> buf = new(); string curW = "\u0001", curDn = "", lastW, lastDn = ""; bool hadVe, extrap; float lastAt, gapAvg = .05f, gapJit = .01f, blend;
        public float Delay => Mathf.Clamp(gapAvg + 2 * gapJit + .04f, .08f, .3f);
        public int Buffered => buf.Count;

        public void Push(Dictionary<string, object> m)
        {
            float now = Time.time;
            if (lastAt > 0) { float g = now - lastAt; if (g < .15f) { gapAvg = Mathf.Lerp(gapAvg, g, .1f); gapJit = Mathf.Lerp(gapJit, Mathf.Abs(g - gapAvg), .1f); } }   // pacotes de "parado" (5 Hz) não contam
            lastAt = now;
            if (m.ContainsKey("w")) lastW = Json.S(m, "w"); if (m.ContainsKey("dn")) lastDn = Json.S(m, "dn") ?? "";
            var s = new Snap { at = now, p = Coords.FromWeb(Json.L(m, "p")), v = Coords.FromWeb(Json.L(m, "v")), yaw = Coords.YawFromWeb(Json.F(m, "y")), f = Json.I(m, "f"), w = lastW, dn = lastDn, hp = Json.F(m, "hp", 100), ve = Json.L(m, "ve") };
            s.ayaw = m.ContainsKey("ay") ? Coords.YawFromWeb(Json.F(m, "ay")) : s.yaw; s.pitch = Json.F(m, "pi") * Mathf.Rad2Deg; s.lean = Json.F(m, "ln");
            buf.Add(s); if (buf.Count > 30) buf.RemoveAt(0);
        }
        public static int ShotsShown;   // r68e: tiros de outros jogadores mostrados aqui (som + clarão); o autoteste de LAN (FBLanTeste) confere
        public void Shot() { if (ch.weapon != null) { ShotsShown++; ch.lastShot = 0; FBAudio.PlayAt(ch.weapon.fire != null && ch.weapon.fire.Count > 0 ? ch.weapon.fire[Random.Range(0, ch.weapon.fire.Count)] as string : "disparo1", ch.MuzzlePos, .8f, 6); FBFx.Muzzle(ch.MuzzlePos, .55f, ch.weapon.cls); FBBotBrain.Noise(ch.Pos); } }
        public void Teleport(Vector3 p) { buf.Clear(); transform.position = p; extrap = false; blend = 0; }   // renasceu: sem interpolar do lugar da morte

        // [parceiro] v1 (thread de física): tempo de CPU do Update medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_Update = FBTempo.Id("FBRemote.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            if (buf.Count == 0) return; float dt = Time.deltaTime, t = Time.time - Delay;
            while (buf.Count > 2 && buf[1].at <= t) buf.RemoveAt(0);
            var a = buf[0]; var b = buf.Count > 1 ? buf[1] : a; float k = b.at > a.at ? Mathf.Clamp01((t - a.at) / (b.at - a.at)) : 1;
            // veículo: o carro manda e a pose vai no banco (FBDriverPose); estado interpolado entre os dois pacotes como o web
            var ve = b.ve; if (a.ve != null && b.ve != null && a.ve.Count == b.ve.Count && a.ve.Count >= 12 && Equals(a.ve[0]?.ToString(), b.ve[0]?.ToString())) ve = LerpVe(a.ve, b.ve, k);
            if (ve != null) { hadVe = FBDrive.ApplyRemote(ch, ve, dt); if (hadVe) return; }
            else if (hadVe) { hadVe = false; FBDrive.Release(ch); }

            Vector3 pos; bool late = buf.Count == 1 || t > b.at;
            if (!late) pos = (b.p - a.p).sqrMagnitude > 36 ? (k < .5f ? a.p : b.p) : Vector3.Lerp(a.p, b.p, k);   // > 6 m entre pacotes = teleporte
            else
            {
                float over = Mathf.Clamp(t - b.at, 0, .25f); var v = b.v; if ((b.f & 16) != 0) v.y = 0;            // no chão: extrapola só no plano
                pos = b.p + v * over;
            }
            if (extrap && !late) blend = .15f; extrap = late;
            if (blend > 0) { blend -= dt; pos = Vector3.Lerp(transform.position, pos, 1 - Mathf.Exp(-dt * 18)); }  // volta suave depois de extrapolar
            transform.position = pos; transform.rotation = Quaternion.Euler(0, late ? b.yaw : Mathf.LerpAngle(a.yaw, b.yaw, k), 0);
            aimYaw = Mathf.LerpAngle(a.ayaw, b.ayaw, k); aimPitch = Mathf.Lerp(a.pitch, b.pitch, k);
            ch.velocity = late ? b.v : Vector3.Lerp(a.v, b.v, k); var f = b.f; ch.lean = b.lean;
            ch.sprint = (f & 1) != 0; ch.crouch = (f & 2) != 0; ch.aiming = (f & 4) != 0; ch.shooting = (f & 8) != 0; ch.grounded = (f & 16) != 0;
            bool alive = (f & 32) != 0; if (alive && !ch.alive) ch.Revive(b.hp); ch.hp = b.hp; ch.combat = ch.aiming || ch.shooting;
            if (b.w != curW) { curW = b.w; GameData.Weapons.TryGetValue(curW ?? "", out var d); var wu = ch.GetComponent<FBWeaponUser>(); if (d != null) wu.Give(d.id); else wu.Strip(); }
            if (b.dn != curDn && (alive || b.dn == "")) { curDn = b.dn; if (curDn == "") ch.StopDance(true); else ch.PlayDance(curDn); }
        }

        // ve = [id, x, y, z, yaw, pitch, roll, steer, spin, v, paredão, roda] — índice 4 é ângulo (radianos), 0 e 10 não interpolam
        public static List<object> LerpVe(List<object> a, List<object> b, float k)
        {
            var o = new List<object>(b.Count);
            for (int i = 0; i < b.Count; i++)
            {
                if (i == 0 || i == 10) { o.Add(b[i]); continue; }
                double x = Num(a[i]), y = Num(b[i]);                                     // sai como double: é o que o Json.Fv (FBDrive) lê
                if (i == 4) { double d = Mathf.Repeat((float)(y - x) + Mathf.PI, 2 * Mathf.PI) - Mathf.PI; o.Add(x + d * k); } else o.Add(x + (y - x) * k);
            }
            return o;
        }
        static double Num(object v) => v switch { double d => d, float f => f, int n => n, long l => l, _ => 0 };
    }
}
