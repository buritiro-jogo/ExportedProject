// r45d: GARUPA (pedido: "pegar diferentes poses rig etc para as motos, alguns com passageiras femininas como é no game original").
// Passageira sentada atrás do piloto nas motos do trânsito, com o clipe de garupa do 171 (Anim_MB_Idle_Passenger) e as mãos na cintura
// de quem pilota (IK). Mesmo esquema do carona da viatura (FBPassengerSeat): locomoção zerada, camadas de cima em 0, clipe de corpo
// inteiro em 1 — depois do FBCharacter e do FBDriverPose do piloto (ordem 1002). Na queda da moto ela cai junto (FBFall).
using UnityEngine;
using System.Collections.Generic;

namespace FreeBala
{
    [DefaultExecutionOrder(1002)]
    public class FBGarupa : MonoBehaviour
    {
        public static int Seated, Dismounted; public static readonly string[] CLIPS = { "X171_Anim_MB_Idle_Passenger", "X171_Anim_MB_Idle_Passenger_Base", "X171_bike_ar_garupa" };
        FBCharacter c; public FBCar car; CharacterController cc; Animator an; int LU = -1, LE = -1, LEU = -1; readonly List<int> zeroF = new(), offB = new(); public bool owned; float noDrv;
        static readonly string[] ZERO = { "VelX", "VelZ", "Speed", "VerticalVelocity", "CrouchAlpha" }, OFF = { "IsRunning", "IsAiming", "IsShooting", "InCombat", "InAir", "IsCrouching", "Tactical Sprint" };
        public static string Clip { get { if (!FBRegistry.I) return null; foreach (var n in CLIPS) if (FBRegistry.I.Clip(n)) return n; return null; } }
        public static FBGarupa Sit(FBCharacter ch, FBCar u, bool owned)
        {
            if (!ch || !u || Clip == null) return null; var g = ch.gameObject.GetOrAdd<FBGarupa>(); g.c = ch; g.car = u; g.owned = owned; g.cc = ch.GetComponent<CharacterController>();
            if (g.cc) g.cc.enabled = false; ch.aiming = ch.hasAim = false; ch.SetWeaponVisible(false); ch.vehiclePose = true; ch.velocity = Vector3.zero; ch.animVel = Vector2.zero; ch.StopDance(true); ch.sys = Clip;
            foreach (var cl in ch.GetComponentsInChildren<Collider>()) foreach (var bc in u.cols) if (cl && bc) Physics.IgnoreCollision(cl, bc);
            g.an = ch.an; g.zeroF.Clear(); g.offB.Clear(); Seated++;
            ch.HoldAnimate(g, true);                                                                   // r46i: ver FBCharacter.HoldAnimate
            if (g.an)
            {
                g.LU = g.an.GetLayerIndex("Upper Body"); g.LE = g.an.GetLayerIndex("Emote Full Body"); g.LEU = g.an.GetLayerIndex("Emote Upper Body");
                foreach (var p in g.an.parameters)
                {
                    string n = p.name.Trim();
                    if (p.type == AnimatorControllerParameterType.Float && System.Array.IndexOf(ZERO, n) >= 0) g.zeroF.Add(p.nameHash);
                    else if (p.type == AnimatorControllerParameterType.Bool && System.Array.IndexOf(OFF, n) >= 0) g.offB.Add(p.nameHash);
                }
            }
            return g;
        }
        void OnDestroy() { if (c) c.HoldAnimate(this, false); }
        public void Leave() { if (c) { c.HoldAnimate(this, false); c.vehiclePose = false; if (c.sys == Clip) c.sys = null; if (cc) cc.enabled = true; } car = null; Destroy(this); }
        // banco de trás: atrás do quadril do piloto (MX: pose de pilotar do RigData), um pouco mais alto
        public static Vector3 SeatOf(FBCar u, float k) { var T = u.mx ? u.mx.transform : u.transform; return FBDrive.SeatHips(u) - T.forward * .42f * k + T.up * .06f * k; }
        // [parceiro 01/11] medidor de tempo por sistema (FBTempo): com FBTempo.On soma o custo do Update/LateUpdate da garupa
        static readonly int __fbt_Update = FBTempo.Id("FBGarupa.Update");
        static readonly int __fbt_LateUpdate = FBTempo.Id("FBGarupa.LateUpdate");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void LateUpdate()
        {
            if (!FBTempo.On) { LateUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
        }
        void Update__fbt()
        {
            if (!car || !c || !an || !an.runtimeAnimatorController || !c.alive) return;
            foreach (var h in zeroF) an.SetFloat(h, 0); foreach (var h in offB) an.SetBool(h, false);
            if (LU >= 0) an.SetLayerWeight(LU, 0); if (LEU >= 0) an.SetLayerWeight(LEU, 0); if (LE >= 0 && c.sys != null) an.SetLayerWeight(LE, 1); an.speed = 1;
            if (c.sys == null) c.sys = Clip;
        }
        void LateUpdate__fbt()
        {
            if (!car || !c) { if (c) { if (owned && !car) { Destroy(c.gameObject); return; } c.vehiclePose = false; c.sys = null; if (cc) cc.enabled = true; } Destroy(this); return; }
            if (!c.alive) { Leave(); return; }
            // r45e: piloto não está mais na moto (estacionou e desceu, foi derrubado): a garupa desce pela direita e sai andando
            var drv = car.driver; if (drv && FBVehicles.CarOf(drv) == car && !FBFall.Busy(drv)) noDrv = 0; else if ((noDrv += Time.deltaTime) > .6f)
            {
                var T0 = car.mx ? car.mx.transform : car.transform; var at = SeatOf(car, c.ScaleK) + T0.right * .75f * c.ScaleK; if (FBWorld.GroundY(at.x, at.y + .5f, at.z, .4f, 3) is float gy) at.y = gy;
                var ch = c; bool own = owned; Leave(); FBMatch.Teleport(ch, at, T0.eulerAngles.y + 90); if (own) FBTransito.GarupaDesce(ch); Dismounted++; return;
            }
            var hips = an ? an.GetBoneTransform(HumanBodyBones.Hips) : null; if (!hips) return; var T = car.mx ? car.mx.transform : car.transform; float k = c.ScaleK;
            transform.rotation = T.rotation; transform.position += SeatOf(car, k) - hips.position;
            // mãos na cintura de quem pilota
            var d = car.driver; var dh = d && d.an ? d.an.GetBoneTransform(HumanBodyBones.Hips) : null; if (!dh) return;
            Transform B(HumanBodyBones x) => an.GetBoneTransform(x);
            Transform lA = B(HumanBodyBones.LeftUpperArm), lF = B(HumanBodyBones.LeftLowerArm), lH = B(HumanBodyBones.LeftHand), rA = B(HumanBodyBones.RightUpperArm), rF = B(HumanBodyBones.RightLowerArm), rH = B(HumanBodyBones.RightHand);
            var up = T.up; var rt = T.right; var fw = T.forward; var w0 = dh.position + up * .1f * k + fw * .03f * k;
            FBIK.Anat = true; FBIK.Where = "garupa";   /* r46g: torção anatômica limitada */
            if (lA && lF && lH) FBIK.TwoBonePole(lA, lF, lH, w0 - rt * .17f * k, lF.position - rt * .25f - up * .15f, .85f);
            if (rA && rF && rH) FBIK.TwoBonePole(rA, rF, rH, w0 + rt * .17f * k, rF.position + rt * .25f - up * .15f, .85f);
            FBIK.Anat = false; FBIK.Where = "-";
        }
    }
}
