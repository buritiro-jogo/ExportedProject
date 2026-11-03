// MOTOS DO MX GRAU (rodada 44 — pedido 28/10/2026: "reformular completamente as motocicletas utilizando o MX Grau ... a mesma sensação de
// pilotagem ... sons originais de cada moto ... rigs dos motociclistas sincronizados com guidão e pedaleiras").
// Fonte: MX Grau 3.9 (com.mapinha.mxgrau, Unity 2022.3 IL2CPP). Engenharia reversa em docs/re/MX_GRAU.md; dados em
// Resources/FBMx/mx_bikes.json (tools/mxgrau/export_bikes.py: parâmetros do MXController, Rigidbody/ConfigurableJoint/colisores/PhysicMaterial
// de cada nó do prefab, poses do piloto do RigData, sons do MXBikeSound).
//
// O que é PORTE (lido do binário, mesmo cálculo):
//  • FISICA — corpo rígido no chassi + suspensão dianteira (junta prismática com mola 4500/amortecedor 500 e alvo 0,08 m) + braço traseiro
//    (junta de giro com mola) + duas rodas-esfera (PhysicMaterial 0,9 "Maximum") — MXController.FixedUpdate: torque na roda traseira e força no
//    chassi pela curva de aceleração (acelcv) × fator de velocidade, embreagem 0,2 s na troca de marcha (sem torque), freio motor e freio por
//    VelocityChange, roda dianteira orientada à força pela direção (steerAngle 50°→18° até 20 m/s), aderência lateral por roda (força
//    ±40/±80 e amortecimento por passo 1/(grip.x+grip.y)), endireitamento sem esterço (giro y e deriva ×0,1), inclinação IMPOSTA em torno
//    da roda traseira (leanCurve ±50° × velocidade/30 km/h, metade no grau), grau por torque (wheeliePowerB, dobra com a frente no chão,
//    arrasto angular = wheelieDrag), equilíbrio (balanceForce acima de 30°), freio no grau (wbrakePower), raspar a rabeta acima de
//    scrapeAngle e queda em fallAngle / batida forte (impulso 8000, 800 empinado) / empinada parada.
//  • PASSO FIXO 0,012 s e gravidade 10 m/s² do APK: enquanto alguém pilota uma moto MX o passo fixo do jogo vai a 0,012 s (volta ao sair).
//  • ESCALA: o mundo do jogo é 1,2 unidade por metro (as motos do APK entram ×1,2). Para a mesma dinâmica em metros: forças ×1,2, torques
//    ×1,44 (inércia escala com o quadrado), VelocityChange ×1,2, gravidade extra até 12 u/s², raios lidos do colisor ×1,2, velocidades ÷1,2.
//  • SOM — MXBikeSound.Update: 6 camadas por moto (marcha lenta, baixa, alta, giro parado, corte/desaceleração, subida de giro) com as
//    curvas fadeIN/fadeOUT, multiplicadores e tempos de transição de cada moto; volume/pitch pela rotação (rpmN) e pelo acelerador.
//  • PILOTO — MXPlayer + RigData: 8 estados (parado, ré, pilotando, em pé, grau, grau em pé, manobra A, manobra B) com quadril, mãos (no
//    punho do guidão, que gira com a direção), cotovelos, pés e joelhos; os clipes humanoides do PlayerMX (MX_*) dão o resto do corpo;
//    balanço do corpo pela inércia (o "PM" do APK: mola 250/20 lateral e vertical, 120/25 longitudinal) e o tronco acompanha o grau.
// O que é ADAPTAÇÃO: o personagem é o nosso (rig humanoide do jogo) — as poses do RigData entram por IK, não por Animation Rigging; subir/
// descer continua com os clipes do 171 (o MX não tem); a queda solta o piloto (personagem do jogo) e a moto tomba sozinha.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public class FBMxBike : MonoBehaviour
    {
        // ---------------------------------------------------------------- dados
        static Dictionary<string, object> db;
        public static Dictionary<string, object> DB { get { if (db == null) { var ta = Resources.Load<TextAsset>("FBMx/mx_bikes"); db = ta ? Json.Parse(ta.text) as Dictionary<string, object> ?? new() : new(); } return db; } }
        public static readonly string[] IDS = { "pop110", "titan160", "cg150", "moby", "xre300", "xt660", "gs310", "xj6", "mt09", "at1100", "gs1250" };
        public static bool Has(string vid) => DB.ContainsKey(vid) && FBRegistry.I && FBRegistry.I.Vehicle("mx_" + vid);
        public const float S = 1.2f, APK_DT = .012f, APK_G = 10f;
        static int activeCount; static float savedDt = -1;

        public FBCar car; public string vid; Dictionary<string, object> M, P;
        // nós (papéis do MXController)
        Transform t, tfw, trw, tchassis, fwmesh, rwmesh, handlemesh, pivothandle; public Transform gripL, gripR;
        Rigidbody rb, fwrb, rwrb; ConfigurableJoint fwjoint; readonly List<Rigidbody> bodies = new(); readonly List<Collider> cols = new();
        readonly List<(Transform tr, Vector3 p, Quaternion r)> rest = new();
        // parâmetros (APK)
        float maxspeed, torque, reverseSpeed, brakePower, wbrakePower, balanceForce, wheeliePowerB, wheelieDrag, scrapeAngle, fallAngle, lookAngle;
        Vector2 fwgrip, rwgrip, fwgrip_def, rwgrip_def; AnimationCurve steerAngle, leanCurve, acelcv; int gearcount; float[] gears;
        float fwheelRadius, rwheelRadius, distWheelHandle, weight = 120, steervel; Vector3 handleLocPos;
        // estado (MXController)
        public float in_steering, in_acceleration, in_revving, smtThrottle, speedKmh, rpmN, bikeSpeed, gearRatio, leanAngle, wheelieAngle, wheelieDir = 1, lookDir = 1, targetLeanWheelie, leanWheelieAngle,
            wheelieMod45, wheelieMod90, wheelieMod4590, wheelieMod3060, speedMod10, speedMod40, pickUpSpeed, smtHandleRot, wheeliePowerA;
        public bool in_wheelie, onWheelie, isReversing, crashed, active, changeGear = true; public int currentGear, currGear, prevGear, visualGear;
        public float vmag; Vector3 lastVelocity; float gearWaitT; public float crashT;
        public bool rabeta;                                                                              // faíscas da rabeta (FXRabeta)
        public bool standing, trickA, trickB;                                                            // botões do MX (em pé / manobras)
        // Grau com física manual realista: controle direto do acelerador, ponto de equilíbrio e freio traseiro ("pé no freio")
        public static bool GrauAssist = false, AntiHop = true, SomPiso = true;
        float prevWA; public float hopVy, spinF, spinR, spinExp; float angF, angR, remT = -9; Quaternion fwRest, rwRest, rwRestC, lastF, lastR; bool restOk;
        public static float SpinCapDeg = 16; public string WheelDiag => $"rodas: visível D {spinF:0} T {spinR:0} °/s (teto {SpinCapDeg:0}°/quadro) × real {spinExp:0} °/s · traseira filha da física {(rwmesh && trw ? rwmesh.IsChildOf(trw) : false)}";

        // ---------------------------------------------------------------- montagem
        static Transform Find(Transform r, string n) { if (string.IsNullOrEmpty(n)) return null; foreach (var x in r.GetComponentsInChildren<Transform>(true)) if (x.name == n) return x; return null; }
        static Vector3 V3(object o) { var l = o as List<object>; if (l != null && l.Count >= 3) return new Vector3(Json.Fv(l[0]), Json.Fv(l[1]), Json.Fv(l[2])); var d = o as Dictionary<string, object>; return d != null ? new Vector3(Json.F(d, "x"), Json.F(d, "y"), Json.F(d, "z")) : Vector3.zero; }
        static Quaternion Q4(object o) { var d = o as Dictionary<string, object>; if (d != null) return new Quaternion(Json.F(d, "x"), Json.F(d, "y"), Json.F(d, "z"), Json.F(d, "w", 1)); var l = o as List<object>; return l != null && l.Count >= 4 ? new Quaternion(Json.Fv(l[0]), Json.Fv(l[1]), Json.Fv(l[2]), Json.Fv(l[3])) : Quaternion.identity; }
        static Vector2 V2(object o) { var d = o as Dictionary<string, object>; return d != null ? new Vector2(Json.F(d, "x"), Json.F(d, "y")) : Vector2.zero; }
        public static AnimationCurve Curve(object o)
        {
            var ks = Json.L(o, "m_Curve"); var c = new AnimationCurve(); if (ks == null) return c;
            foreach (var k in ks) c.AddKey(new Keyframe(Json.F(k, "time"), Json.F(k, "value"), Json.F(k, "inSlope"), Json.F(k, "outSlope")));
            return c;                                                                                    // m_PreInfinity/m_PostInfinity 2 = segura nas pontas (padrão)
        }

        /// <summary>Monta física/som/pontos de uma moto MX já instanciada (modelo filho de car.transform na pose de fábrica).</summary>
        public static FBMxBike Attach(FBCar car, string vid, Transform model)
        {
            var m = car.gameObject.AddComponent<FBMxBike>(); m.car = car; m.vid = vid; m.M = Json.O(DB, vid); m.P = Json.O(m.M, "params"); m.Build(model); return m;
        }
        void Build(Transform model)
        {
            if (FBRegistry.I) foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true)) mf.sharedMesh = FBRegistry.I.LodOf(mf.sharedMesh);   // r44: Mesh LOD (FBLod3.Veiculos)
            t = transform; var R = Json.O(M, "roles");
            tfw = Find(model, Json.S(R, "tfw")); trw = Find(model, Json.S(R, "trw")); tchassis = Find(model, Json.S(R, "tchassis")) ?? t; fwmesh = Find(model, Json.S(R, "fwmesh")); rwmesh = Find(model, Json.S(R, "rwmesh"));
            handlemesh = Find(model, Json.S(R, "handlemesh")); pivothandle = Find(model, Json.S(R, "pivothandle")); gripL = Find(model, "gripL"); gripR = Find(model, "gripR");
            maxspeed = Json.F(P, "maxspeed", 120); weight = Json.F(P, "weight", 120); torque = Json.F(P, "torque", 10); reverseSpeed = Json.F(P, "reverseSpeed", 1); brakePower = Json.F(P, "brakePower", 20); wbrakePower = Json.F(P, "wbrakePower", 25);
            balanceForce = Json.F(P, "balanceForce", 5); wheeliePowerB = Json.F(P, "wheeliePowerB", 40); wheelieDrag = Json.F(P, "wheelieDrag", 8); scrapeAngle = Json.F(P, "scrapeAngle", 80); fallAngle = Json.F(P, "fallAngle", 96);
            lookAngle = Json.F(P, "lookAngle", 30); fwgrip = fwgrip_def = V2(Json.Get(P, "fwgrip")); rwgrip = rwgrip_def = V2(Json.Get(P, "rwgrip"));
            steerAngle = Curve(Json.Get(P, "steerAngle")); leanCurve = Curve(Json.Get(P, "leanCurve")); acelcv = Curve(Json.Get(P, "acelcv")); gearcount = Mathf.Max(1, Json.I(P, "gearcount", 5));
            gears = new float[gearcount]; for (int i = 0; i < gearcount; i++) gears[i] = Mathf.Floor((i + 1) / (float)gearcount * maxspeed);   // MXController.Awake
            // ---- corpos, juntas e colisores (valores do prefab; o nó raiz do APK é o próprio grupo do FBCar)
            string apk = Json.S(M, "apk"); var nodeRb = new Dictionary<string, Rigidbody>();
            foreach (var pn in Json.L(M, "physics") ?? new List<object>())
            {
                if (!Json.B(pn, "active", true)) continue; var name = Json.S(pn, "name"); var tr = name == apk ? t : Find(model, name); if (!tr) continue;
                if (tr != t) rest.Add((tr, tr.localPosition, tr.localRotation));
                foreach (var c in Json.L(pn, "comps") ?? new List<object>())
                {
                    var ty = Json.S(c, "type");
                    if (ty == "Rigidbody")
                    {
                        var b = tr.GetComponent<Rigidbody>(); if (!b) b = tr.gameObject.AddComponent<Rigidbody>();
                        if (!b) { Debug.LogWarning($"[FreeBala][MxBike] Rigidbody não pôde ser adicionado em '{tr.name}'"); continue; }
                        b.mass = Json.F(c, "Mass", 1); b.linearDamping = Json.F(c, "Drag"); b.angularDamping = Json.F(c, "AngularDrag", .05f);
                        b.useGravity = Json.B(c, "UseGravity", true); b.isKinematic = true; b.interpolation = RigidbodyInterpolation.Interpolate; b.collisionDetectionMode = CollisionDetectionMode.Discrete;
                        nodeRb[name] = b; bodies.Add(b);
                    }
                    else if (ty == "SphereCollider") { var s = tr.gameObject.AddComponent<SphereCollider>(); s.radius = Json.F(c, "Radius", .3f); s.center = V3(Json.Get(c, "Center")); Mat(s, Json.O(c, "mat")); cols.Add(s); }
                    else if (ty == "CapsuleCollider")
                    {
                        var s = tr.gameObject.AddComponent<CapsuleCollider>(); s.radius = Json.F(c, "Radius", .2f); s.height = Json.F(c, "Height", 1); s.direction = Json.I(c, "Direction", 1); s.center = V3(Json.Get(c, "Center"));
                        s.isTrigger = Json.B(c, "IsTrigger"); Mat(s, Json.O(c, "mat")); cols.Add(s);
                    }
                    else if (ty == "BoxCollider") { var s = tr.gameObject.AddComponent<BoxCollider>(); s.size = V3(Json.Get(c, "Size")); s.center = V3(Json.Get(c, "Center")); Mat(s, Json.O(c, "mat")); cols.Add(s); }
                }
            }
            rb = nodeRb.TryGetValue(apk, out var r0) ? r0 : t.GetComponent<Rigidbody>();
            fwrb = tfw ? tfw.GetComponent<Rigidbody>() : null; rwrb = trw ? trw.GetComponent<Rigidbody>() : null;
            // juntas depois de todos os corpos (o ConnectedBody aponta para o nó com o Rigidbody)
            foreach (var pn in Json.L(M, "physics") ?? new List<object>())
            {
                if (!Json.B(pn, "active", true)) continue; var name = Json.S(pn, "name"); var tr = name == apk ? t : Find(model, name); if (!tr) continue;
                foreach (var c in Json.L(pn, "comps") ?? new List<object>()) if (Json.S(c, "type") == "ConfigurableJoint") Joint(tr, c, nodeRb);
            }
            fwjoint = tfw ? tfw.GetComponent<ConfigurableJoint>() : null;
            // MXController.Start: limites de giro e iterações do resolvedor; a raiz não colide com as rodas
            if (rb) { rb.maxAngularVelocity = 10; rb.solverVelocityIterations = 10; }
            foreach (var w in new[] { fwrb, rwrb }) if (w) { w.maxAngularVelocity = 300; w.solverIterations = 20; }
            foreach (var j in GetComponentsInChildren<ConfigurableJoint>(true)) if (j.connectedBody && j.connectedBody != rb) j.connectedBody.solverIterations = 20;
            foreach (var a in t.GetComponents<Collider>()) foreach (var w in new[] { tfw, trw }) if (w) foreach (var wc in w.GetComponents<Collider>()) Physics.IgnoreCollision(a, wc);
            foreach (var c in cols) c.contactOffset = .001f * S;
            foreach (var b in bodies) b.maxDepenetrationVelocity = 1.5f * S;                              // r45d: colisor da roda que afunda na emenda da calçada não 'cospe' a moto
            if (fwmesh && rwmesh) { fwRest = lastF = fwmesh.localRotation; rwRest = rwmesh.localRotation; rwRestC = lastR = Quaternion.Inverse(t.rotation) * rwmesh.rotation; restOk = true; }
            fwheelRadius = tfw && tfw.GetComponent<SphereCollider>() ? tfw.GetComponent<SphereCollider>().radius : .3f; rwheelRadius = trw && trw.GetComponent<SphereCollider>() ? trw.GetComponent<SphereCollider>().radius : .3f;
            // MXController.Start: "handle_pivot" na pose do guidão (com a inclinação do garfo) e o guidão dentro dele — o esterço gira em volta do garfo
            if (handlemesh) { var hp = new GameObject("handle_pivot").transform; hp.SetParent(handlemesh.parent, false); hp.SetPositionAndRotation(handlemesh.position, handlemesh.rotation); handlemesh.SetParent(hp, true); handlemesh.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); hp.gameObject.layer = handlemesh.gameObject.layer; }
            if (pivothandle) handleLocPos = pivothandle.localPosition; if (handlemesh && tfw) distWheelHandle = Vector3.Distance(handlemesh.position, tfw.position);
            foreach (var c in cols) car.cols.Add(c);
            sound = new MxSound(this);
            MeasureLevel();
        }
        static readonly Dictionary<string, PhysicsMaterial> mats = new();
        static void Mat(Collider c, Dictionary<string, object> m)
        {
            if (m == null) return; var key = Json.S(m, "name") + Json.F(m, "dyn"); if (!mats.TryGetValue(key, out var pm))
            {
                pm = new PhysicsMaterial(Json.S(m, "name")) { dynamicFriction = Json.F(m, "dyn", .6f), staticFriction = Json.F(m, "sta", .6f), bounciness = Json.F(m, "bounce"),
                    frictionCombine = (PhysicsMaterialCombine)Json.I(m, "fcomb"), bounceCombine = (PhysicsMaterialCombine)Json.I(m, "bcomb") };
                mats[key] = pm;
            }
            c.sharedMaterial = pm;
        }
        static JointDrive Drive(object o, float springK = 1) => new JointDrive { positionSpring = Json.F(o, "positionSpring") * springK, positionDamper = Json.F(o, "positionDamper"), maximumForce = Mathf.Min(3.4e38f, Json.F(o, "maximumForce", 3.4e38f)) };
        static SoftJointLimit Lim(object o, float k = 1) => new SoftJointLimit { limit = Json.F(o, "limit") * k, bounciness = Json.F(o, "bounciness"), contactDistance = Json.F(o, "contactDistance") * k };
        void Joint(Transform tr, object c, Dictionary<string, Rigidbody> nodeRb)
        {
            var j = tr.gameObject.AddComponent<ConfigurableJoint>(); var cb = Json.S(c, "ConnectedBody"); if (cb != null && nodeRb.TryGetValue(cb, out var b)) j.connectedBody = b;
            var ax = V3(Json.Get(c, "Axis")); j.axis = ax.sqrMagnitude > 1e-6f ? ax : Vector3.right; var sa = V3(Json.Get(c, "SecondaryAxis")); if (sa.sqrMagnitude > 1e-6f) j.secondaryAxis = sa;
            j.anchor = V3(Json.Get(c, "Anchor")); j.autoConfigureConnectedAnchor = Json.B(c, "AutoConfigureConnectedAnchor", true); if (!j.autoConfigureConnectedAnchor) j.connectedAnchor = V3(Json.Get(c, "ConnectedAnchor"));
            j.xMotion = (ConfigurableJointMotion)Json.I(c, "XMotion"); j.yMotion = (ConfigurableJointMotion)Json.I(c, "YMotion"); j.zMotion = (ConfigurableJointMotion)Json.I(c, "ZMotion");
            j.angularXMotion = (ConfigurableJointMotion)Json.I(c, "AngularXMotion"); j.angularYMotion = (ConfigurableJointMotion)Json.I(c, "AngularYMotion"); j.angularZMotion = (ConfigurableJointMotion)Json.I(c, "AngularZMotion");
            j.linearLimit = Lim(Json.Get(c, "LinearLimit"), S); var ls = Json.Get(c, "LinearLimitSpring"); j.linearLimitSpring = new SoftJointLimitSpring { spring = Json.F(ls, "spring"), damper = Json.F(ls, "damper") };
            j.lowAngularXLimit = Lim(Json.Get(c, "LowAngularXLimit")); j.highAngularXLimit = Lim(Json.Get(c, "HighAngularXLimit")); j.angularYLimit = Lim(Json.Get(c, "AngularYLimit")); j.angularZLimit = Lim(Json.Get(c, "AngularZLimit"));
            j.xDrive = Drive(Json.Get(c, "XDrive")); j.yDrive = Drive(Json.Get(c, "YDrive")); j.zDrive = Drive(Json.Get(c, "ZDrive"));
            j.angularXDrive = Drive(Json.Get(c, "AngularXDrive")); j.angularYZDrive = Drive(Json.Get(c, "AngularYZDrive")); j.slerpDrive = Drive(Json.Get(c, "SlerpDrive"));
            j.rotationDriveMode = (RotationDriveMode)Json.I(c, "RotationDriveMode"); j.targetPosition = V3(Json.Get(c, "TargetPosition")) * S;
            j.enablePreprocessing = Json.B(c, "EnablePreprocessing", true); j.massScale = Json.F(c, "MassScale", 1); j.connectedMassScale = Json.F(c, "ConnectedMassScale", 1);
            j.configuredInWorldSpace = Json.B(c, "ConfiguredInWorldSpace"); j.swapBodies = Json.B(c, "SwapBodies"); j.enableCollision = Json.B(c, "EnableCollision");
        }

        // ---------------------------------------------------------------- CÂMERA (base: MXGrau.MXCam.LateUpdate — atrás e baixa, 2,55 m,
        // altura 1,4, olha a 0,9; rumo por SmoothDampAngle). r47 (pedido 30/10: "a câmera não deve herdar a inclinação lateral da moto nem
        // girar excessivamente nas curvas ... terceira pessoa estável ... acompanhar a direção do movimento de forma suave ... liberdade de
        // controle manual"). Causa medida no código: a origem era o ponto 1,2 m ACIMA da moto no referencial dela (TransformPoint) — com
        // 35° de inclinação ela andava ~0,7 m para o lado a cada curva e a câmera ia junto; somava-se o balanço pelo esterço (até 0,25 m)
        // e o rumo seguia a frente do chassi (que guina no grau e no contra-esterço). Agora:
        //  · origem = meio das duas rodas no plano do chão (a linha de contato quase não se move com a inclinação), suavizada (sem tremer
        //    na suspensão), altura pela roda traseira; olhar sempre com o "para cima" do mundo (horizonte nivelado, sem rolagem);
        //  · rumo = direção do MOVIMENTO (velocidade) acima de 3 m/s, com amortecimento 0,7 s → 0,35 s na velocidade;
        //  · mouse orbita (lado) e inclina (camPitch: até 10° para cima e 35° para baixo); a câmera só volta ao centro 1,6 s depois do último movimento, em ~1 s;
        //  · balanço lateral 1/4 do original e tremida pela velocidade pela metade.
        static float camAng, camAngV, orbit, orbitV, orbitT, sway, lastCamYaw = float.NaN, pitchS; static Vector3 orgS; static FBMxBike camBike;
        public static float CamRollDbg, CamLatDbg;                                         // laboratório: rolagem da câmera (°) e deslocamento lateral da origem (m)
        public void Camera(Camera cam, float dt, float camPitch)
        {
            if (camBike != this) { camBike = this; camAng = transform.eulerAngles.y; camAngV = 0; orbit = 0; orbitV = 0; lastCamYaw = float.NaN; pitchS = 0; orgS = Origin(); }
            var pl = FBPlayer.I; float cy = pl ? pl.camYaw : camAng;
            if (!float.IsNaN(lastCamYaw) && Mathf.Abs(Mathf.DeltaAngle(lastCamYaw, cy)) > .01f) { orbit += Mathf.DeltaAngle(lastCamYaw, cy); orbitT = 0; }   // mouse mexeu: orbita
            else if ((orbitT += dt) > 1.6f) orbit = Mathf.SmoothDampAngle(orbit, 0, ref orbitV, 1f, Mathf.Infinity, dt);
            var fwdF = Vector3.ProjectOnPlane(transform.forward, Vector3.up); if (fwdF.sqrMagnitude < 1e-4f) fwdF = Vector3.forward; fwdF.Normalize();
            var vel = rb ? rb.linearVelocity.Flat() : Vector3.zero; float vs = vel.magnitude;
            if (crashed && vs > .5f) fwdF = vel / vs;
            else if (vs > 3 && Vector3.Dot(vel, fwdF) > 0) fwdF = Vector3.Slerp(fwdF, vel / vs, Mathf.Clamp01((vs - 3) / 5));
            float yaw = Quaternion.LookRotation(fwdF).eulerAngles.y;
            float k = Mathf.Clamp01((Mathf.Abs(bikeSpeed) - 30) / 50f); k = k * k * (3 - 2 * k); float smt = k * .35f + (1 - k) * .7f;
            camAng = Mathf.SmoothDampAngle(camAng, yaw, ref camAngV, smt, Mathf.Infinity, dt);
            float heading = camAng + orbit; if (pl) pl.camYaw = heading; lastCamYaw = heading;
            var org = Origin(); orgS = Vector3.Lerp(orgS, org, 1 - Mathf.Exp(-dt * 14)); var origin = orgS + fwdF * (-.2f * S);
            CamLatDbg = Vector3.Dot(transform.TransformPoint(Vector3.up * 1.2f) - org, Quaternion.Euler(0, heading, 0) * Vector3.right);   // quanto a origem ANTIGA andaria de lado
            pitchS = Mathf.Lerp(pitchS, Mathf.Clamp(camPitch, -35, 10), 1 - Mathf.Exp(-dt * 10));   // camPitch > 0 = olhar para cima (câmera desce)
            var dir = Quaternion.Euler(-pitchS, heading, 0) * Vector3.forward; float zoom = Mathf.Clamp(FBDrive.CamZoom / .82f, .6f, 1.6f);
            var look = origin + Vector3.up * .9f * S; var campos = look + Vector3.up * .5f * S - dir * 2.55f * S * zoom;
            sway = Mathf.Lerp(sway, in_steering, Mathf.Clamp01(dt * 3));
            // obstáculo atrás (parede): encurta
            var ray = campos - look; if (FBWorld.RayHit(look, ray.normalized, ray.magnitude + .2f, out var h)) campos = look + ray.normalized * Mathf.Max(.6f, h.distance - .25f);
            cam.transform.position = campos; cam.transform.rotation = Quaternion.LookRotation(look - campos, Vector3.up);
            cam.transform.position += cam.transform.right * sway * (1 - wheelieMod45) * speedMod40 * .06f * S;
            float sk = Mathf.Clamp01((Mathf.Abs(bikeSpeed) - 10) / 190f), fr = sk * 6 + 2, amp = sk * .025f + .0005f;
            cam.transform.Rotate((Mathf.PerlinNoise(Time.time * fr, 0) * 2 - 1) * amp * 2, (Mathf.PerlinNoise(0, Time.time * fr) * 2 - 1) * amp * 2, 0, Space.Self);
            CamRollDbg = Mathf.DeltaAngle(0, cam.transform.eulerAngles.z);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 62 + Mathf.Min(10, Mathf.Abs(bikeSpeed) * .06f), 1 - Mathf.Exp(-dt * 4)); cam.nearClipPlane = .08f;
        }
        // meio das rodas no plano do chão (XZ) + altura da roda traseira; sem rodas, a raiz da moto
        Vector3 Origin()
        {
            Vector3 a = rwrb ? rwrb.position : transform.position, b = fwrb ? fwrb.position : a; var m = (a + b) * .5f;
            return new Vector3(m.x, rwrb ? rwrb.position.y : transform.position.y + .3f * S, m.z);
        }

        // ---------------------------------------------------------------- PINTURA (BikePaint do MX: textura 1x3 base/meio/topo, filtro Point,
        // nos materiais listados de cada peça; o padrão de fábrica é cinza — aqui cada moto ganha cores de rua, fixas pela posição de nascimento)
        static readonly Color[] TINTAS = { new(.62f, .04f, .04f), new(.03f, .03f, .035f), new(.86f, .86f, .84f), new(.05f, .14f, .5f), new(.55f, .56f, .58f), new(.85f, .62f, .04f),
            new(.16f, .17f, .18f), new(.45f, .02f, .02f), new(.08f, .3f, .12f), new(.9f, .32f, .03f) };
        static readonly float[] PESO = { 3.2f, 3, 2.4f, 1.6f, 1.6f, .7f, 1.4f, 1, .5f, .4f };
        public void Paint(uint seed)
        {
            var PT = Json.O(M, "paint"); var parts = Json.L(PT, "parts"); if (parts == null || parts.Count == 0) return;
            var rnd = FBVehicles.Seeded(seed); Color PickC() { float tot = PESO.Sum(), r = rnd() * tot; for (int i = 0; i < TINTAS.Length; i++) { r -= PESO[i]; if (r <= 0) return TINTAS[i]; } return TINTAS[0]; }
            var on = Json.L(PT, "p"); var def = Json.L(PT, "def"); var c = new Color[3];
            for (int i = 0; i < 3; i++) { var d = def != null && i < def.Count ? def[i] as List<object> : null; c[i] = d != null && d.Count >= 3 ? new Color(Json.Fv(d[0]), Json.Fv(d[1]), Json.Fv(d[2])) : new Color(.33f, .33f, .33f); }
            c[0] = PickC(); var dark = c[0].grayscale < .3f;
            if (on != null && on.Count > 1 && on[1] is bool b1 && b1) c[1] = rnd() < .6f ? (dark ? new Color(.85f, .85f, .84f) : new Color(.03f, .03f, .035f)) : PickC();
            if (on != null && on.Count > 2 && on[2] is bool b2 && b2) c[2] = rnd() < .7f ? new Color(.05f, .05f, .055f) : c[1];
            var tex = new Texture2D(1, 3, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "mx_paint" };
            tex.SetPixels(new[] { c[0], c[1], c[2] }); tex.Apply(false, true);
            var want = new HashSet<string>(); foreach (var pp in parts) foreach (var m in Json.L(pp, "mats") ?? new List<object>()) if (m is string ms) want.Add(ms);
            int n = 0;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials; bool any = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i]; if (!m || !want.Contains(m.name.Replace(" (Instance)", ""))) continue;
                    var nm = new Material(m) { name = m.name };
                    foreach (var prop in new[] { "_BaseMap", "baseColorTexture", "_MainTex" }) if (nm.HasProperty(prop)) nm.SetTexture(prop, tex);
                    foreach (var prop in new[] { "_BaseColor", "baseColorFactor", "_Color" }) if (nm.HasProperty(prop)) nm.SetColor(prop, Color.white);
                    mats[i] = nm; any = true; n++;
                }
                if (any) r.sharedMaterials = mats;
            }
            Painted = n; PaintColor = c[0];
        }
        public int Painted; public Color PaintColor;

        // ---------------------------------------------------------------- r49i: moto parada nivelada no chão
        // Pedido (31/10): "As motos estão tendo spawn com uma leve inclinação para cima". Causa (medida no glTF das 11 motos): na pose de
        // fábrica o fundo da roda dianteira fica 10–13 cm ACIMA da raiz e o da traseira 10–14 cm ABAIXO — nariz ~10° para cima. No APK o nó
        // raiz do prefab tinha rotação X de +10° (xf do mx.json; +8,7° na XRE) que o glTF não trouxe. Pilotando, a física gira a raiz até as
        // duas rodas tocarem o chão (por isso andando estava certo), mas parada — nascer, descanso, descer, levantar caída, trânsito
        // cinemático, NPC montado pelo estado — a raiz ficava só com o rumo: traseira ~14 cm enterrada, dianteira ~16 cm no ar, e ao subir a
        // física "pulava" para os 10°. Agora a raiz parada leva a arfagem que nivela as rodas (LevelPitch, dos pontos da física) mais a rampa
        // do chão embaixo de cada roda, com as duas rodas no chão (LevelBottom = fundo das rodas acima da raiz nivelada).
        public float LevelPitch, LevelBottom; Vector3 cF, cR; public static float ParkErr; public static int Parked;
        Vector3 parkFromP, parkToP; Quaternion parkFromQ, parkToQ; float parkT, parkDur;
        void MeasureLevel()
        {
            if (!tfw || !trw) return; cF = t.InverseTransformPoint(tfw.position); cR = t.InverseTransformPoint(trw.position);
            float bF = cF.y - fwheelRadius, bR = cR.y - rwheelRadius, wb = Mathf.Max(.3f, cF.z - cR.z);
            LevelPitch = Mathf.Atan2(bF - bR, wb) * Mathf.Rad2Deg; var q = Quaternion.Euler(LevelPitch, 0, 0);
            LevelBottom = ((q * cF).y - fwheelRadius + (q * cR).y - rwheelRadius) * .5f;
        }
        /// <summary>r49i: moto parada com as duas rodas no chão (arfagem de nível + rampa do chão). roll em rad na convenção do CarRot
        /// (descanso = 0,1); yawDeg nulo = rumo atual; blend &gt; 0 = vai até a pose em blend s (descer: deita no descanso sem estalo).</summary>
        public void Park(float roll, float? yawDeg = null, float blend = 0)
        {
            if (!tfw || !trw) return; var f = Vector3.ProjectOnPlane(t.forward, Vector3.up);
            float yaw = yawDeg ?? (f.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(f).eulerAngles.y : t.eulerAngles.y), s = Mathf.Abs(t.lossyScale.y);
            var p = t.position; var yq = Quaternion.Euler(0, yaw, 0); var pF = p + yq * new Vector3(0, 0, cF.z * s); var pR = p + yq * new Vector3(0, 0, cR.z * s);
            float? gF, gR; car.SetColliders(false);
            try { gF = FBWorld.GroundY(pF.x, p.y, pF.z, .8f, 1.6f); gR = FBWorld.GroundY(pR.x, p.y, pR.z, .8f, 1.6f); }
            finally { car.SetColliders(true); }
            float slope = gF != null && gR != null ? Mathf.Clamp(Mathf.Atan2(gF.Value - gR.Value, (cF.z - cR.z) * s) * Mathf.Rad2Deg, -15, 15) : 0;
            var q = Quaternion.Euler(LevelPitch - slope, yaw, -roll * Mathf.Rad2Deg); float g0 = p.y + LevelBottom * s;
            float yF = (gF ?? gR ?? g0) - ((q * cF).y - fwheelRadius) * s, yR = (gR ?? gF ?? g0) - ((q * cR).y - rwheelRadius) * s;
            var pos = new Vector3(p.x, Mathf.Max(yF, yR), p.z);                                          // nenhuma roda enterrada (a outra fica no ar só além da rampa de 15°)
            if (blend > 0 && !active) { parkFromP = p; parkFromQ = t.rotation; parkToP = pos; parkToQ = q; parkT = 0; parkDur = blend; }
            else { t.SetPositionAndRotation(pos, q); parkDur = 0; Physics.SyncTransforms(); }
            car.yaw = -yaw * Mathf.Deg2Rad; ParkErr = Mathf.Max(ParkErr, Mathf.Abs(yF - yR)); Parked++;
        }
        /// <summary>laboratório: fundo de cada roda (esfera da física) acima do chão embaixo dela e a arfagem da raiz.</summary>
        public string ParkDiag()
        {
            if (!tfw || !trw) return "sem rodas"; float s = Mathf.Abs(t.lossyScale.y); float? gF, gR; car.SetColliders(false);
            try { gF = FBWorld.GroundY(tfw.position.x, tfw.position.y, tfw.position.z, .5f, 2); gR = FBWorld.GroundY(trw.position.x, trw.position.y, trw.position.z, .5f, 2); }
            finally { car.SetColliders(true); }
            string H(Transform w, float r, float? g) => g == null ? "sem chão" : $"{(w.position.y - r * s - g.Value) * 100:+0.0;-0.0} cm";
            var e = t.eulerAngles; float X(float d) => d > 180 ? d - 360 : d;
            return $"roda D {H(tfw, fwheelRadius, gF)} · T {H(trw, rwheelRadius, gR)} acima do chão · raiz arfagem {X(e.x):0.0}° (nível {LevelPitch:0.0}°) rolagem {X(e.z):0.0}° · sem a correção: D +{((cF.y - fwheelRadius) * s) * 100:0} cm, T {((cR.y - rwheelRadius) * s) * 100:0} cm";
        }

        // ---------------------------------------------------------------- liga/desliga a física (pilotando × estacionada)
        public void SetActive(bool on)
        {
            if (on == active) return; active = on; parkDur = 0;
            if (on)
            {
                crashed = false; crashT = 0; smtThrottle = in_acceleration = in_steering = 0; wheelieDir = 1; onWheelie = in_wheelie = false; changeGear = true; currentGear = currGear = prevGear = 0;
                pmInit = false; steervel = 0;                                                              // [parceiro 31/10] mola do piloto e esterço recomeçam do zero a cada montada
                foreach (var b in bodies) { b.isKinematic = false; b.linearVelocity = Vector3.zero; b.angularVelocity = Vector3.zero; b.WakeUp(); }
                FineStep(true);
                sound.Start();
            }
            else
            {
                foreach (var b in bodies) { if (!b.isKinematic) { b.linearVelocity = Vector3.zero; b.angularVelocity = Vector3.zero; } b.isKinematic = true; }
                foreach (var (tr, p, r) in rest) if (tr) tr.SetLocalPositionAndRotation(p, r);
                if (pivothandle) pivothandle.localPosition = handleLocPos; if (handlemesh) handlemesh.localRotation = Quaternion.identity;
                FineStep(false);
                sound.Stop();
            }
        }
        void OnDestroy() { if (active) { active = false; FineStep(false); } sound?.Stop(); }
        // r46f: passo fino de física (12 ms) compartilhado — moto MX ativa ou ragdoll de queda viva (FBRagdoll.GoLive); volta ao passo do projeto quando o último solta
        public static void FineStep(bool on)
        {
            if (on) { if (activeCount++ == 0) { savedDt = Time.fixedDeltaTime; Time.fixedDeltaTime = APK_DT; } }
            else if (--activeCount <= 0) { activeCount = 0; if (savedDt > 0) Time.fixedDeltaTime = savedDt; }
        }

        /// <summary>Entrada do piloto (MXController.Update com InputData): esterço, acelerador suavizado (4/s), grau, giro em ponto morto.</summary>
        public void Input(float steer, float thr, bool wheelie, bool revving, bool stand, bool tA, bool tB, float dt)
        {
            // MXGrau.InputHUB.Update: esterço por SmoothDamp, tempo 0,30 s + 0,15 s × clamp01((peso − 60)/190) (moto pesada vira mais devagar)
            float st = .3f + .15f * Mathf.Clamp01((weight - 60) / 190f); in_steering = Mathf.Clamp(Mathf.SmoothDamp(in_steering, Mathf.Clamp(steer, -1, 1), ref steervel, st, Mathf.Infinity, dt), -1, 1); in_revving = revving ? 1 : 0; in_wheelie = wheelie; standing = stand; trickA = tA && onWheelie; trickB = tB && onWheelie;
            smtThrottle = Mathf.MoveTowards(smtThrottle, Mathf.Clamp(thr, -1, 1), dt * 4); in_acceleration = smtThrottle;
        }

        // ---------------------------------------------------------------- física (MXController.FixedUpdate)
        // [parceiro] v1 (thread de física): tempo de CPU por script medido pelo FBTempo (desligado no jogo: custa um teste de bool)
        static readonly int __fbt_FixedUpdate = FBTempo.Id("FBMxBike.FixedUpdate");
        void FixedUpdate()
        {
            if (!FBTempo.On) { FixedUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); FixedUpdate__fbt(); FBTempo.Soma(__fbt_FixedUpdate, t0);
        }
        void FixedUpdate__fbt()
        {
            if (!active || !rb || rb.isKinematic) return;
            float dt = Time.fixedDeltaTime;
            foreach (var b in bodies) if (b && !b.isKinematic && b.useGravity) b.AddForce(Vector3.down * (APK_G * S + Physics.gravity.y), ForceMode.Acceleration);   // g do APK (10 m/s²) na escala do mundo
            if (crashed)
            {
                crashT += dt;
                /* r45e: moto caída sem piloto arrasta no chão (antes rolava 170 m sozinha na pista do teste) */
                if (crashT > .4f) foreach (var b in bodies) if (b && !b.isKinematic) { var hv = b.linearVelocity; float k = Mathf.Clamp01(1 - dt * 1.6f); b.linearVelocity = new Vector3(hv.x * k, hv.y, hv.z * k); b.angularVelocity *= Mathf.Clamp01(1 - dt * 1.2f); }
                if (crashT > 4 && rb.linearVelocity.sqrMagnitude < .09f && !car.driver) { var keep = (t.position, t.rotation); SetActive(false); t.SetPositionAndRotation(keep.position, keep.rotation); }
                return;
            }
            if (onWheelie) in_wheelie = true;
            float brakeIn = in_acceleration <= 0 ? Mathf.Min(-in_acceleration, 1) : 0;
            wheelieDir += ((in_wheelie ? -1 : 1) - wheelieDir) * Mathf.Clamp01(dt * 2);
            // garfo: o pivô do guidão acompanha a compressão da suspensão dianteira
            if (pivothandle && handlemesh && tfw) { float dh = Vector3.Distance(handlemesh.position, tfw.position); pivothandle.localPosition = new Vector3(handleLocPos.x, (distWheelHandle - dh) / S, handleLocPos.z); }
            float rwSpin = rwrb ? trw.InverseTransformDirection(rwrb.angularVelocity).x : 0;
            var rightF = Vector3.ProjectOnPlane(t.right, Vector3.up).normalized; var fwdF = Vector3.Cross(rightF, Vector3.up).normalized;
            var vel = rb.linearVelocity; speedKmh = bikeSpeed = Vector3.Dot(vel, fwdF) * 3.6f / S;
            speedMod10 = Mathf.Clamp01(speedKmh / 10); speedMod40 = Mathf.Clamp01(speedKmh / 40);
            float gb = 1 - Mathf.Clamp01((bikeSpeed - 10) / 30); fwgrip = fwgrip_def * (1 + .25f * gb); rwgrip = rwgrip_def * (1 + .25f * gb);
            visualGear = 0; for (int i = 0; i < gears.Length; i++) if (bikeSpeed > gears[i] - 2) visualGear = i + 1 < gears.Length ? i + 1 : gears.Length - 1;
            float g0 = gears[visualGear], baseRPM = g0 == 0 || bikeSpeed / g0 < 0 ? 1 : Mathf.Clamp(bikeSpeed / g0, .1f, 1);
            rpmN = Mathf.Clamp01(Mathf.MoveTowards(rpmN, baseRPM, dt * 4));
            // roda dianteira: alinhada ao chassi no plano + ângulo de direção pela velocidade
            var rightC = Vector3.ProjectOnPlane(tchassis.right, Vector3.up).normalized; var fwdC = Vector3.Cross(rightC, Vector3.up).normalized;
            vmag = vel.magnitude / S;
            if (tfw && fwdC.sqrMagnitude > 1e-6f) tfw.rotation = Quaternion.AngleAxis(in_steering * steerAngle.Evaluate(vmag), Vector3.up) * Quaternion.LookRotation(fwdC, Vector3.up);
            if (fwjoint) fwjoint.axis = Vector3.right;
            rb.centerOfMass = Vector3.zero;                                                                // centerOfMassOffset (0 em todas as motos)
            float speedN = maxspeed != 0 && bikeSpeed / maxspeed >= 0 ? Mathf.Min(bikeSpeed / maxspeed, 1) : 0;
            currGear = Mathf.Min(Mathf.FloorToInt(speedN * gearcount), gearcount);
            if (currGear != prevGear) { changeGear = false; currentGear = 0; gearWaitT = .2f; }               // ChangeCurrentGear: embreagem 0,2 s
            if (gearWaitT > 0) { gearWaitT -= dt; if (gearWaitT <= 0) changeGear = true; }
            prevGear = currGear; if (changeGear) currentGear = currGear;
            gearRatio = Mathf.Clamp01((vmag + gearcount) / gearcount - currentGear);
            float pw = bikeSpeed < maxspeed ? (1 - speedN) * .4f + .6f : 0; if (rwrb && Mathf.Abs(rwrb.angularVelocity.x) > 500) pw = 0;
            if (in_acceleration > 0 && rwrb)
            {
                float gf = currentGear >= 0 ? Mathf.Min(currentGear, 1) : 0;
                rwrb.AddTorque(t.right * (in_acceleration * torque * gf * pw) * (S * S));
                rb.AddForce(tchassis.forward * acelcv.Evaluate(gearRatio) * pw * S);
            }
            isReversing = rwSpin < 0;
            if (vmag < reverseSpeed && in_acceleration < 0) rb.AddForce(-t.forward * acelcv.Evaluate(in_acceleration) * .5f * S);
            var vdir = vel.sqrMagnitude > 1e-10f ? vel.normalized : Vector3.zero;
            // [parceiro] fisica 31/10: raycasts das rodas movidos para cá (antes ficavam mais abaixo) para o freio saber se há roda no chão
            var mask = ~(1 << FBWorld.LVehicle) & ~(1 << FBWorld.LChar) & ~(1 << FBWorld.LHitbox) & ~(1 << FBWorld.LTrigger);
            bool fHit = tfw && Physics.Raycast(tfw.position, Vector3.down, fwheelRadius * S * 1.4f, mask, QueryTriggerInteraction.Ignore);
            bool rHit = trw && Physics.Raycast(trw.position, Vector3.down, rwheelRadius * S * 1.4f, mask, QueryTriggerInteraction.Ignore);
            // [parceiro] fisica 31/10: freio (S) e freio-motor só agem com alguma roda no chão. No ar eles freavam a velocidade 3D
            //  (inclusive a queda) e a moto ficava quase parada no ar segurando S.
            bool noChao = fHit || rHit;
            // [parceiro] Correção: sem acelerar, a moto também freia quando está indo para trás (antes rodava de ré sozinha sem parar)
            //  e o freio não passa do zero (não fica invertendo o sentido a cada passo): força limitada a |v|.
            if (noChao && in_acceleration == 0 && !in_wheelie) rb.AddForce(-Vector3.ClampMagnitude(vdir * gearRatio * brakePower * .2f * dt * S, vel.magnitude), ForceMode.VelocityChange);   // freio motor
            if (noChao && in_acceleration < 0 && !isReversing) rb.AddForce(-Vector3.ClampMagnitude(vdir * brakeIn * brakePower * dt * S, vel.magnitude), ForceMode.VelocityChange);
            pickUpSpeed = Mathf.Clamp01((bikeSpeed - 2) * .125f);
            float wk = onWheelie ? 1.2f : 1;
            smtHandleRot = Mathf.MoveTowardsAngle(smtHandleRot, -lookDir, dt * 4);
            if (handlemesh) handlemesh.localRotation = Quaternion.Euler(0, in_steering * wheelieDir * steerAngle.Evaluate(vmag) * wk, 0);
            // ângulo do grau (frente acima do plano) e efeitos
            float a = Vector3.SignedAngle(tchassis.forward, fwdC, rightC);
            rabeta = false;
            if (a > 50)
            {
                rabeta = a > 64 && a < 80;
                if (a >= 78 && brakeIn < 0.18f) Crash("loop");
                if (bikeSpeed < 1) Crash("parado");
            }
            if (a > 10) rb.linearVelocity *= .999f;
            prevWA = wheelieAngle; wheelieAngle = a;
            wheelieMod45 = Mathf.Clamp01(a / 45); wheelieMod90 = Mathf.Clamp01(a / 90); wheelieMod4590 = Mathf.Clamp01((a - 45) / 45); wheelieMod3060 = Mathf.Clamp01((a - 30) / 30);
            // capotou (> 90°): gira no rumo da velocidade
            float flip = Mathf.Clamp01((a - 90) / 10);
            if (flip > 0 && vel.sqrMagnitude > .01f) { var vf = Vector3.ProjectOnPlane(vel, Vector3.up).normalized; rb.AddTorque(Vector3.up * 50 * Vector3.SignedAngle(fwdC, vf, Vector3.up) * flip * S * S); }
            float baseLean = Mathf.Clamp01(bikeSpeed / 30) * -leanCurve.Evaluate(in_steering);
            leanAngle = baseLean + Mathf.Clamp01(wheelieMod45) * (baseLean * .5f - baseLean);
            // aderência lateral de cada roda (força pela deriva local, limitada); fHit/rHit vêm dos raycasts lá de cima (antes do freio)
            if (fHit && fwrb)
            {
                float x = fwrb.transform.InverseTransformDirection(rb.GetPointVelocity(fwrb.position)).x / S;
                fwrb.AddRelativeForce(Vector3.right * (x >= -10 ? -40 * Mathf.Min(x, 10) : 400) * S);
            }
            if (rHit && rwrb)
            {
                float x = rwrb.transform.InverseTransformDirection(rb.GetPointVelocity(rwrb.position)).x / S;
                rb.AddForceAtPosition(rwrb.transform.right * (x >= -10 ? -80 * Mathf.Min(x, 10) : 800) * S, rwrb.position);

                // Firmeza e aderência dos pneus (elimina o efeito de deslizar como sabão / lisa):
                var latDir = t.right;
                float latVel = Vector3.Dot(rb.linearVelocity, latDir);
                float gripDecel = (onWheelie ? 45f : 85f) * S;
                float newLatVel = Mathf.MoveTowards(latVel, 0f, gripDecel * dt);
                rb.linearVelocity += latDir * (newLatVel - latVel);

                // Corta deriva parasita das próprias rodas
                var rlv = trw.InverseTransformDirection(rwrb.linearVelocity);
                rlv.x = Mathf.MoveTowards(rlv.x, 0, 80f * S * dt);
                rwrb.linearVelocity = trw.TransformDirection(rlv);

                if (flip < .9f && fwrb && fHit)
                {
                    var flv = tfw.InverseTransformDirection(fwrb.linearVelocity);
                    flv.x = Mathf.MoveTowards(flv.x, 0, 80f * S * dt);
                    fwrb.linearVelocity = tfw.TransformDirection(flv);
                }

                // Redirecionamento preciso da velocidade física nas curvas (sem deslizar de lado)
                if (!onWheelie && fHit && Mathf.Abs(in_steering) > 0.02f && Mathf.Abs(bikeSpeed) > 1.2f)
                {
                    float curSteer = in_steering * steerAngle.Evaluate(vmag);
                    float wb = Mathf.Max(distWheelHandle * S, 1.35f);
                    float yawRate = (bikeSpeed / 3.6f) * Mathf.Tan(curSteer * Mathf.Deg2Rad) / wb;
                    rb.linearVelocity = Quaternion.AngleAxis(yawRate * Mathf.Rad2Deg * dt, Vector3.up) * rb.linearVelocity;
                    var av = rb.angularVelocity;
                    av.y = Mathf.MoveTowards(av.y, yawRate, 14f * dt);
                    rb.angularVelocity = av;
                }
                else if (Mathf.Abs(in_steering) < .05f)
                {
                    var av = t.InverseTransformDirection(rb.angularVelocity); av.y *= .1f; rb.angularVelocity = t.TransformDirection(av);
                }
            }
            else
            {   // roda traseira fora do chão
                var av = t.InverseTransformDirection(rb.angularVelocity); av.y *= .1f; av.z *= .1f; rb.angularVelocity = t.TransformDirection(av);
                var lv2 = t.InverseTransformDirection(rb.linearVelocity); lv2.x *= .1f; rb.linearVelocity = t.TransformDirection(lv2);
            }
            if (AntiHop && fHit && rHit && !onWheelie && !in_wheelie && rb.linearVelocity.y > .9f * S)
            {   // r45d: as duas rodas no chão e a moto subindo = quique de emenda/meio-fio (no APK o chão é liso): segura a subida
                float lim = .9f * S; hopVy = Mathf.Max(hopVy, rb.linearVelocity.y / S);
                foreach (var b in bodies) if (b && !b.isKinematic && b.linearVelocity.y > lim) { var v0 = b.linearVelocity; v0.y = Mathf.Lerp(v0.y, lim, .6f); b.linearVelocity = v0; }
            }
            if (fwrb) lastVelocity = fwrb.linearVelocity;
            // grau com física manual realista e controle refinado
            bool steered = false;
            if (in_wheelie)
            {
                // 1. FREIO TRASEIRO ("Pé no freio" - S / brakeIn)
                // Resposta imediata: puxa a frente para baixo para segurar no ponto de equilíbrio
                if (brakeIn > 0.04f)
                {
                    float brkTorque = brakeIn * wbrakePower * 2.5f * Mathf.Clamp01(bikeSpeed / 2f);
                    if (a > 45) brkTorque *= 1.4f;
                    rb.AddTorque(t.right * brkTorque, ForceMode.Acceleration);
                }

                // 2. ACELERAÇÃO E PUXADA (W / in_acceleration)
                if (in_acceleration > 0.04f && bikeSpeed > 2f)
                {
                    float sk = (1 - (maxspeed != 0 ? Mathf.Clamp01(speedKmh / maxspeed) : 0)) * .4f + .6f;
                    wheeliePowerA = in_acceleration * wheeliePowerB * sk;
                    
                    // Frente ainda no chão ou rente ao asfalto: impulso para levantar
                    if (tfw && Physics.Raycast(tfw.position, Vector3.down, fwheelRadius * S * 1.3f, mask, QueryTriggerInteraction.Ignore))
                    {
                        wheeliePowerA *= 2.5f;
                    }
                    else
                    {
                        // No ar: física do ponto de equilíbrio (sweet spot 46° - 58°)
                        if (a >= 46 && a <= 58)
                        {
                            wheeliePowerA *= (0.6f + 0.4f * in_acceleration);
                        }
                        else if (a > 58)
                        {
                            // Acima de 58°: acelerador forte sobe em direção à rabeta
                            wheeliePowerA *= (0.8f * in_acceleration);
                        }
                    }

                    rb.angularDamping = wheelieDrag;
                    rb.AddTorque(-t.right * wheeliePowerA, ForceMode.Acceleration);

                    // Controle lateral no grau (A / D)
                    float steerP = in_steering * steerAngle.Evaluate(vmag) * 0.7f;
                    rb.AddTorque(Vector3.up * steerP, ForceMode.Acceleration);
                    rb.linearVelocity = Quaternion.AngleAxis(in_steering * 0.35f, Vector3.up) * rb.linearVelocity;
                    steered = true;
                }

                // Gravidade natural trazendo a frente lentamente se soltar o acelerador sem frear
                if (onWheelie && in_acceleration <= 0.04f && brakeIn <= 0.04f)
                {
                    if (a < 54) rb.AddTorque(t.right * (14f * Mathf.Cos(a * Mathf.Deg2Rad)), ForceMode.Acceleration);
                }
            }
            if (!steered) rb.angularDamping = .2f;
            // inclinação imposta em volta do eixo traseiro
            var fwdAx = t.forward; var upRef = Vector3.ProjectOnPlane(Vector3.up, fwdAx); var upNow = Vector3.ProjectOnPlane(t.up, fwdAx);
            if (upRef.sqrMagnitude > 1e-6f && upNow.sqrMagnitude > 1e-6f && rwrb)
            {
                float roll = Vector3.SignedAngle(upRef, upNow, fwdAx);
                t.RotateAround(rwrb.position, fwdAx, -(roll - leanAngle));
            }
            // entra/sai do grau pelo ângulo real da moto
            if (a > 12 && in_wheelie) onWheelie = true;
            if (a < 6) { onWheelie = false; in_wheelie = false; }
            if (Mathf.Abs(in_steering) > .25f) lookDir = Mathf.Sign(in_steering);
            targetLeanWheelie = Mathf.MoveTowards(targetLeanWheelie, lookDir, dt * 4); leanWheelieAngle = -targetLeanWheelie * lookAngle * wheelieMod4590;
        }

        // ---------------------------------------------------------------- queda (CrashBike / OnCollisionEnter)
        public event System.Action<string> OnCrash;
        void OnCollisionEnter(Collision c)
        {
            if (!active || crashed) return; float thr = wheelieAngle > 60 ? 800 : 8000;
            float imp = c.impulse.sqrMagnitude / (S * S);                                                  // impulso²: o APK compara o quadrado com o limite
            if (imp > thr) { if (in_wheelie) Crash("batida"); else if (speedKmh > 50) Crash("batida"); }
        }
        public void Crash(string why)
        {
            if (crashed || !active) return; crashed = true; crashT = 0; in_acceleration = smtThrottle = 0;
            foreach (var b in bodies) if (b) b.angularDamping = .2f;
            OnCrash?.Invoke(why);
        }
        // [parceiro] fisica 31/10: piloto saiu à força (morreu) com a moto andando ou no ar: a moto fica na física e cai/para sozinha,
        //  como no caminho de queda, mas sem disparar OnCrash (MxCrash -> Exit de novo, recursivo)
        public void Abandon()
        {
            if (crashed || !active) return; crashed = true; crashT = 0; in_acceleration = smtThrottle = 0;
            foreach (var b in bodies) if (b) b.angularDamping = .2f;
        }

        // ---------------------------------------------------------------- visual (Update) + som
        MxSound sound; public float Rpm => rpmN;
        static readonly int __fbt_Update = FBTempo.Id("FBMxBike.Update");                               // [parceiro] tempo por script (FBTempo)
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            if (active && fwmesh && trw) fwmesh.localRotation = trw.localRotation;                       // roda dianteira visível gira com a traseira (como no APK)
            if (active) sound?.Update(Time.deltaTime);
        }
        // r45d: mede o giro das rodas visíveis contra o esperado pela velocidade; roda que não gira (nó fora do corpo da roda, ou o
        // giro do APK que não chega no modelo) passa a girar por aqui pelo chão percorrido
        // r45e: o giro de verdade passa de 40°/quadro a 50 km/h e a roda raiada parece parada/girando para trás (efeito estroboscópico) — as
        //  duas rodas visíveis giram aqui pelo chão percorrido com teto por quadro; a traseira medida/posta em relação ao chassi (ela é filha
        //  da roda da física, o localRotation dela não muda)
        void SpinVisual(float dt)
        {
            if (!restOk || dt <= 0 || !fwmesh || !rwmesh) return; bool moving = active || Time.time - remT < .3f;
            float v = moving ? bikeSpeed / 3.6f * S : 0; spinExp = Mathf.Abs(v) / Mathf.Max(.05f, rwheelRadius * S) * Mathf.Rad2Deg;
            float cap = SpinCapDeg; float dF = Mathf.Clamp(v / Mathf.Max(.05f, fwheelRadius * S) * Mathf.Rad2Deg * dt, -cap, cap), dR = Mathf.Clamp(v / Mathf.Max(.05f, rwheelRadius * S) * Mathf.Rad2Deg * dt, -cap, cap);
            if (active && rwrb && Mathf.Abs(bikeSpeed) < 8 && in_acceleration > .5f) dR = Mathf.Clamp(Mathf.Abs(trw.InverseTransformDirection(rwrb.angularVelocity).x) * Mathf.Rad2Deg * dt, -cap, cap);   /* cantando pneu parado */
            angF = (angF + dF) % 360; angR = (angR + dR) % 360;
            fwmesh.localRotation = fwRest * Quaternion.Euler(angF, 0, 0); rwmesh.rotation = t.rotation * rwRestC * Quaternion.Euler(angR, 0, 0);
            var rc = Quaternion.Inverse(t.rotation) * rwmesh.rotation; float mf = Quaternion.Angle(lastF, fwmesh.localRotation) / dt, mr = Quaternion.Angle(lastR, rc) / dt; lastF = fwmesh.localRotation; lastR = rc;
            spinF = Mathf.Lerp(spinF, mf, Mathf.Clamp01(dt * 6)); spinR = Mathf.Lerp(spinR, mr, Mathf.Clamp01(dt * 6));
        }
        static readonly int __fbt_LateUpdate = FBTempo.Id("FBMxBike.LateUpdate");                       // [parceiro] tempo por script (FBTempo)
        void LateUpdate()
        {
            if (!FBTempo.On) { LateUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
        }
        void LateUpdate__fbt()
        {
            SpinVisual(Time.deltaTime);
            if (!active && parkDur > 0)                                                                    // r49i: deita no descanso depois de descer
            {
                parkT += Time.deltaTime; float k = Mathf.SmoothStep(0, 1, parkT / parkDur);
                t.SetPositionAndRotation(Vector3.Lerp(parkFromP, parkToP, k), Quaternion.Slerp(parkFromQ, parkToQ, k)); if (parkT >= parkDur) { parkDur = 0; Physics.SyncTransforms(); }
            }
            if (!active) return;
            car.v = bikeSpeed * S / 3.6f; car.lean = leanAngle * Mathf.Deg2Rad; car.steerN = in_steering; car.wheelie = Mathf.Max(0, wheelieAngle) * Mathf.Deg2Rad; car.rpm = rpmN;
            car.gear = visualGear + 1; car.yaw = -t.eulerAngles.y * Mathf.Deg2Rad; car.pitch = 0; car.roll = 0;
        }
        public string GearLabel => bikeSpeed < -1 ? "R" : (visualGear + 1).ToString();
        public float MaxLeanDeg => leanCurve != null ? Mathf.Abs(leanCurve.Evaluate(1)) : 45;                    // r45: trânsito (moto NPC na faixa)
        public float SteerDeg(float vmag) => steerAngle != null ? steerAngle.Evaluate(vmag) : 30;
        public float Wheelbase => tfw && trw ? Vector3.Distance(tfw.position, trw.position) : 1.4f * S;

        // remoto/NPC: sem física (cinemático) — guidão, rodas, piloto e som pelo estado recebido
        Vector3 remPrev; bool remInit;
        public void RemoteState(float v, float steer, float thr, float wheelieRad, float dt)
        {
            bikeSpeed = speedKmh = v * 3.6f / S; in_steering = Mathf.Clamp(steer, -1, 1); in_acceleration = thr; wheelieAngle = wheelieRad * Mathf.Rad2Deg; onWheelie = wheelieAngle > 8; isReversing = bikeSpeed < -.3f;
            visualGear = 0; for (int i = 0; i < gears.Length; i++) if (Mathf.Abs(bikeSpeed) > gears[i] - 2) visualGear = Mathf.Min(i + 1, gears.Length - 1);
            float g0 = Mathf.Max(1, gears[visualGear]); rpmN = Mathf.MoveTowards(rpmN, Mathf.Clamp(Mathf.Abs(bikeSpeed) / g0, .1f, 1), dt * 4);
            if (handlemesh) handlemesh.localRotation = Quaternion.Euler(0, in_steering * steerAngle.Evaluate(Mathf.Abs(v) / S), 0);
            var pos = t.position; if (!remInit) { remPrev = pos; remInit = true; } float d = Vector3.Dot(pos - remPrev, t.forward) / S; remPrev = pos;
            remT = Time.time; /* r45e: rodas giram no SpinVisual */ _ = d;
            if (!sound.On) sound.Start(); sound.Update(dt);
        }
        public void RemoteStop() { remInit = false; if (!active) sound.Stop(); }
        // r45b: liga a física já andando (moto NPC do trânsito que passa do cinemático para o corpo rígido): velocidade em todos os corpos e
        // rodas girando na mesma velocidade (rolando sem deslizar, eixo +right = para a frente)
        public void Launch(Vector3 vel)
        {
            if (!active) return; foreach (var b in bodies) if (b && !b.isKinematic) b.linearVelocity = vel;
            float v = Vector3.Dot(vel, t.forward);
            if (rwrb && !rwrb.isKinematic) rwrb.angularVelocity = t.right * (v / Mathf.Max(.05f, rwheelRadius * S));
            if (fwrb && !fwrb.isKinematic) fwrb.angularVelocity = t.right * (v / Mathf.Max(.05f, fwheelRadius * S));
        }

        // ---- MXBikeSound: 6 camadas próprias de cada moto
        class MxSound
        {
            readonly FBMxBike m; AudioSource low, high, idle, off, rpmup, rev; AudioClip cOff, cUp; public bool On; public float OffMix, HighMix;
            float lowVol, highVol, idleVol, offtolow, rpmupToHigh, loopstorevving; readonly float mUp, mOff, mLow, mIdle, pUp, pDown, tHigh, tOff; readonly AnimationCurve fadeIN, fadeOUT;
            public MxSound(FBMxBike m)
            {
                this.m = m; var Sd = Json.O(m.M, "sound");
                mUp = Json.F(Sd, "rpmupVolumeMultiplier", 1); mOff = Json.F(Sd, "offVolumeMultiplier", 1); mLow = Json.F(Sd, "lowVolumeMultiplier", 1); mIdle = Json.F(Sd, "idleVolumeMultiplier", 1);
                pUp = Json.F(Sd, "low_pitch_up", 1); pDown = Json.F(Sd, "low_pitch_down", 1); tHigh = Json.F(Sd, "rpmhightransitiontime", 1); tOff = Json.F(Sd, "offtolowtransitiontime", 1);
                fadeIN = Curve(Json.Get(Sd, "fadeIN")); fadeOUT = Curve(Json.Get(Sd, "fadeOUT"));
                if (fadeIN.length == 0) fadeIN = AnimationCurve.Linear(0, 0, 1, 1); if (fadeOUT.length == 0) fadeOUT = AnimationCurve.Linear(0, 1, 1, 0);
            }
            AudioSource Src(string role)
            {
                var clip = FBAudio.Clip($"mx_{m.vid}_{role}"); var go = new GameObject("som_" + role); go.transform.SetParent(m.transform, false); go.transform.localPosition = new Vector3(0, .5f, -.3f);
                var s = go.AddComponent<AudioSource>(); s.clip = clip; s.loop = true; s.playOnAwake = false; s.spatialBlend = 1; s.dopplerLevel = 0; s.volume = 0; s.priority = 40;
                s.rolloffMode = AudioRolloffMode.Custom; s.maxDistance = 400; s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, FBAudio.Rolloff(5 * S, 1.05f)); s.outputAudioMixerGroup = FBAudio.Group(false);
                return s;
            }
            public void Start()
            {
                if (On) return; On = true;
                if (!low) { low = Src("low"); high = Src("high"); idle = Src("idle"); off = Src("off"); rpmup = Src("rpmup"); rev = Src("revving"); cOff = off.clip; cUp = rpmup.clip; }
                foreach (var s in new[] { low, high, idle, off, rpmup, rev }) if (s && s.clip) { s.volume = 0; s.Play(); }
                lowVol = 1; highVol = 0; offtolow = cOff ? cOff.length : 1; rpmupToHigh = 0; loopstorevving = 0;
            }
            public void Stop() { On = false; foreach (var s in new[] { low, high, idle, off, rpmup, rev }) if (s) s.Stop(); }
            float VOL = .8f * FBAudio.VehVol;                                                                         // mixagem do jogo (o APK toca a 1)
            public void Update(float dt)
            {
                if (!On || !low) return;
                if (m.in_revving > .5f)
                {
                    loopstorevving = Mathf.Clamp01(loopstorevving + dt * 4); rev.volume = loopstorevving * VOL;
                    float k = (1 - loopstorevving) * VOL; rpmup.volume = k; low.volume = k; high.volume = k; idle.volume = k; off.volume = k; return;
                }
                loopstorevving = Mathf.Clamp01(loopstorevving - dt * 4); rev.volume = loopstorevving * VOL; if (loopstorevving <= 0) rev.time = 0;
                float rpm = m.rpmN; idleVol = Mathf.Clamp01((m.bikeSpeed - 5) / -5);
                float offLen = cOff ? cOff.length : 1, upLen = cUp ? cUp.length : 1;
                if (m.in_acceleration > .05f)
                {
                    highVol = Mathf.Clamp01(highVol + dt * 16); lowVol = Mathf.Clamp01(lowVol - dt * 16); offtolow = offLen;
                    rpmupToHigh = Mathf.Clamp(rpmupToHigh + dt * tHigh, 0, upLen); off.time = 0;
                }
                else
                {
                    rpmup.time = 0; highVol = Mathf.Clamp01(highVol - dt * 16); lowVol = Mathf.Clamp01(lowVol + dt * 16);
                    offtolow = Mathf.Clamp(offtolow - dt * tOff, 0, offLen); rpmupToHigh = 0;
                }
                float k8 = Mathf.Clamp01(rpmupToHigh / upLen), s9 = Mathf.Clamp01((rpm - .6f) / .35f), s10 = s9 * .8f + .2f;
                high.volume = highVol * s10 * fadeIN.Evaluate(k8) * VOL; high.pitch = s9 * .2f + .8f;
                rpmup.volume = s10 * highVol * fadeOUT.Evaluate(k8) * mUp * VOL; rpmup.pitch = high.pitch;
                float ko = Mathf.Clamp01(offtolow / offLen);
                low.volume = lowVol * fadeOUT.Evaluate(ko) * (1 - idleVol) * mLow * VOL; low.pitch = pDown + Mathf.Clamp01(rpm) * (pUp - pDown);
                idle.volume = mIdle * idleVol * VOL;
                off.volume = lowVol * fadeIN.Evaluate(ko) * (1 - idleVol) * mOff * VOL; off.pitch = rpm >= 0 ? Mathf.Min(rpm, 1) * .2f + 1 : 1;
                if (SomPiso)
                {   // r45d: soltou o acelerador andando = motor segurando (freio motor), não silêncio: os multiplicadores do APK (xt660 0,04)
                    //  deixavam o 'off'+'low' 3–5× abaixo do acelerando; piso de 45 % do volume cheio, e marcha lenta audível parado
                    float sum = low.volume + off.volume, want = (1 - idleVol) * lowVol * .38f * VOL;
                    if (sum < want) { if (sum > 1e-3f) { float kk = Mathf.Min(want / sum, 8); low.volume *= kk; off.volume *= kk; } else low.volume = want; }
                    idle.volume = Mathf.Max(mIdle, .45f) * idleVol * VOL;
                }
                OffMix = low.volume + off.volume + idle.volume; HighMix = high.volume + rpmup.volume;
            }
            public void Blip() { if (rev && rev.clip) { loopstorevving = 1; rev.time = 0; } }
        }
        public void StartBlip() => sound?.Blip();
        public string SoundDiag => sound == null ? "-" : $"som: sem acelerar {sound.OffMix:0.00} · acelerando {sound.HighMix:0.00}";

        // ---------------------------------------------------------------- PILOTO (MXPlayer + RigData)
        public enum St { Idle, Reversing, Riding, RidingStanding, Wheelie, WheelieStanding, TrickA, TrickB }
        public struct Pose { public Vector3 hips, lhand, rhand, lhh, rhh, lfoot, rfoot, lfh, rfh; public float pitch; public Quaternion lrot, rrot; }
        Pose[] poses; Pose cur; bool curInit; public St state; float stateT;
        Vector3 pmPos, pmVel, pmAnchor; bool pmInit;                                                     // "PM": massa do piloto presa por molas (balanço do corpo)
        public static int PmResets;                                                                       // laboratório: vezes que a massa passou de 0,3 m e foi zerada
        Pose PoseOf(int i)
        {
            var r = Json.O(M, "rig"); Vector3 G(string k) { var l = Json.L(r, k); return l != null && i < l.Count ? V3(l[i]) : Vector3.zero; }
            float pitchOf(int j) { var l = Json.L(r, "hipsRot"); if (l == null || j >= l.Count) return 0; var q = Q4(l[j]); return q.eulerAngles.x > 180 ? q.eulerAngles.x - 360 : q.eulerAngles.x; }
            Quaternion GQ(string k) { var l = Json.L(r, k); if (l == null || i >= l.Count) return Quaternion.identity; hasHandRot = true; return Q4(l[i]).normalized; }
            return new Pose { hips = G("hips"), lhand = G("lhand"), rhand = G("rhand"), lhh = G("lhandhint"), rhh = G("rhandhint"), lfoot = G("lfoot"), rfoot = G("rfoot"), lfh = G("lfoothint"), rfh = G("rfoothint"), pitch = pitchOf(i) - pitchOf(2), lrot = GQ("lhandRot"), rrot = GQ("rhandRot") };
        }
        public Pose RidingPose => poses != null ? poses[2] : PoseOf(2);
        // r45e: quadril da pose que o piloto está usando AGORA (parada = Idle): o subir/descer mira aqui (antes mirava a de andar e ficava 0,34 m fora)
        public Vector3 CurHipsW => transform.TransformPoint(curInit ? cur.hips : (poses != null ? poses[0] : PoseOf(0)).hips);
        // r45e: levanta a moto caída/tombada antes de alguém subir (o SetActive(true) não reiniciava uma moto que ainda estava ativa e caída)
        public static int Recovered;
        public void Recover()
        {
            if (!crashed && Vector3.Angle(t.up, Vector3.up) < 25) return;
            float yaw = t.eulerAngles.y; var p = t.position; SetActive(false); crashed = false; crashT = 0;
            if (Physics.Raycast(p + Vector3.up * 2 * S, Vector3.down, out var h, 6 * S, ~((1 << FBWorld.LVehicle) | (1 << FBWorld.LChar) | (1 << FBWorld.LHitbox) | (1 << FBWorld.LTrigger)), QueryTriggerInteraction.Ignore)) p.y = h.point.y;
            t.SetPositionAndRotation(p, Quaternion.Euler(LevelPitch, yaw, 0)); Park(0, yaw); curInit = false; Recovered++;   // r49i: de pé, nivelada, rodas no chão (antes 0,35 m acima e de nariz para cima)
        }
        static Pose Lerp(Pose a, Pose b, float k) => new Pose
        {
            hips = Vector3.Lerp(a.hips, b.hips, k), lhand = Vector3.Lerp(a.lhand, b.lhand, k), rhand = Vector3.Lerp(a.rhand, b.rhand, k), lhh = Vector3.Lerp(a.lhh, b.lhh, k), rhh = Vector3.Lerp(a.rhh, b.rhh, k),
            lfoot = Vector3.Lerp(a.lfoot, b.lfoot, k), rfoot = Vector3.Lerp(a.rfoot, b.rfoot, k), lfh = Vector3.Lerp(a.lfh, b.lfh, k), rfh = Vector3.Lerp(a.rfh, b.rfh, k), pitch = Mathf.Lerp(a.pitch, b.pitch, k),
            lrot = Quaternion.Slerp(a.lrot, b.lrot, k), rrot = Quaternion.Slerp(a.rrot, b.rrot, k)
        };
        // r49i (pedido de 31/10: "as animações em cima da moto precisam de leveza, e não deve mudar de uma para outra de forma bruta").
        // Causas (código): (1) estado do piloto sem histerese — parado/andando trocava no mesmo limiar (2 km/h e acelerador 0,05) e a curva em
        // |esterço| 0,35 a 8 km/h: perto do limiar o clipe alternava várias vezes por segundo; (2) o clipe base troca por CrossFade entre DOIS
        // estados (A/B do FBCharacter.PlayFull) — trocar de novo antes de a mistura de 0,3 s acabar regrava o clipe que ainda estava saindo
        // (estalo); (3) os alvos do IK (quadril, mãos, pés) iam ao novo estado em ~0,25 s e o pé esquerdo pulava da pedaleira para o chão num
        // quadro. Agora: histerese (para abaixo de 1,2 km/h sem acelerador por 0,2 s; sai acima de 3 km/h ou com acelerador; curva entra em
        // |esterço| > 0,42 a 10 km/h e sai abaixo de 0,22 ou de 7 km/h), clipe mantido no mínimo 0,5 s (0,3 s nas manobras), mistura do clipe
        // 0,45 s (FBDrive → sysFade), alvos do IK com constante de 0,16 s (manobra 0,12) e o pé vai ao chão / volta à pedaleira em 0,35 s num arco.
        float idleT, clipT = 9, footDown; string clipOut; bool leanOn; int leanSide, footFrame = -1;
        public static int ClipSwaps; public static float ClipGapMin = 9;                                  // laboratório: trocas de clipe e o menor intervalo entre duas
        public float FootDownDbg => footDown;                                                             // laboratório
        public static bool SmoothRider = true;                                                            // false = piloto como até a r49h (laboratório: antes × depois)
        /// <summary>Estado do piloto (MXPlayer.SetNewState) e clipe base humanoide do PlayerMX.</summary>
        public string RiderClip(float dt)
        {
            poses ??= Enumerable.Range(0, 8).Select(PoseOf).ToArray();
            if (!SmoothRider)
            {
                St w0 = trickA ? St.TrickA : trickB ? St.TrickB : onWheelie ? (standing ? St.WheelieStanding : St.Wheelie) : Mathf.Abs(bikeSpeed) < 2 && in_acceleration <= 0.05f ? (isReversing && bikeSpeed < -.3f ? St.Reversing : St.Idle) : standing ? St.RidingStanding : St.Riding;
                if (w0 != state) { state = w0; stateT = 0; } stateT += dt;
                var t0 = poses[(int)state]; float d0 = state == St.TrickA || state == St.TrickB ? .4f : .25f;
                if (!curInit) { cur = t0; curInit = true; } else cur = Lerp(cur, t0, Mathf.Clamp01(dt / d0 * 3));
                var c0 = state switch { St.Idle => "MX_Idle", St.Reversing => "MX_Reverse", St.TrickA => "MX_BancoJoelho", St.TrickB => "MX_ChaoMao", _ => Mathf.Abs(in_steering) > .35f && bikeSpeed > 8 ? (in_steering > 0 ? "MX_LeanR" : "MX_LeanL") : "MX_Ride" };
                clipT += dt; if (c0 != clipOut) { if (clipOut != null) { ClipGapMin = Mathf.Min(ClipGapMin, clipT); ClipSwaps++; } clipOut = c0; clipT = 0; }
                return c0;
            }
            float spd = Mathf.Abs(bikeSpeed); bool wasIdle = state == St.Idle || state == St.Reversing;
            bool idleNow = wasIdle ? !(spd > 3 || in_acceleration > .1f) : spd < 1.2f && in_acceleration <= .05f;
            idleT = idleNow ? idleT + dt : 0; bool idle = wasIdle ? idleNow : idleT >= .2f || !curInit && idleNow;
            St want = trickA ? St.TrickA : trickB ? St.TrickB : onWheelie ? (standing ? St.WheelieStanding : St.Wheelie) : idle ? (isReversing && bikeSpeed < -.3f ? St.Reversing : St.Idle) : standing ? St.RidingStanding : St.Riding;
            if (want != state) { state = want; stateT = 0; } stateT += dt;
            var tgt = poses[(int)state]; float tau = state == St.TrickA || state == St.TrickB ? .12f : .16f;                    // MXPlayer.ChangePose (0,4/0,25 s), mais macio
            if (!curInit) { cur = tgt; curInit = true; } else cur = Lerp(cur, tgt, 1 - Mathf.Exp(-dt / tau));
            if (leanOn) { if (Mathf.Abs(in_steering) < .22f || bikeSpeed < 7 || (in_steering > 0 ? 1 : -1) != leanSide) leanOn = false; }
            else if (Mathf.Abs(in_steering) > .42f && bikeSpeed > 10) { leanOn = true; leanSide = in_steering > 0 ? 1 : -1; }
            string want2 = state switch
            {
                St.Idle => "MX_Idle", St.Reversing => "MX_Reverse", St.TrickA => "MX_BancoJoelho", St.TrickB => "MX_ChaoMao",
                _ => leanOn ? (leanSide > 0 ? "MX_LeanR" : "MX_LeanL") : "MX_Ride"
            };
            clipT += dt; bool trick = state == St.TrickA || state == St.TrickB || clipOut == "MX_BancoJoelho" || clipOut == "MX_ChaoMao";
            if (clipOut == null) { clipOut = want2; clipT = 0; }
            else if (want2 != clipOut && clipT >= (trick ? .3f : .5f)) { ClipGapMin = Mathf.Min(ClipGapMin, clipT); clipOut = want2; clipT = 0; ClipSwaps++; }
            return clipOut;
        }
        // r46d: alvos do piloto (mãos nos punhos, pés nas pedaleiras / pé esquerdo no chão parado) e os pontos de dica de cotovelo/joelho —
        // os MESMOS no pilotar (PoseRider) e nas transições (FBDrive.PoseActor): antes subir/descer usava o ponto médio do guidão e polos
        // genéricos, e o 1º quadro de descer (e o último do blend de subir) trocava de alvo de uma vez (sonda r46d: braço a 10.800°/s)
        public Vector3 HandTarget(int i) { if (!curInit) RiderClip(0); var g = GripOf(i); return g ? g.TransformPoint(i == 0 ? cur.lhand : cur.rhand) : transform.TransformPoint(i == 0 ? cur.lhand : cur.rhand); }
        // r49m (pedido de 31/10: "na moto, o braço esquerdo e a mão esquerda ainda não estão alinhados ao guidão"). Causa (MXPlayer.Update
        // desmontado, 0x12a5390–0x12a54fc): o APK gira o PAI do punho (LeftHandGrip/RightHandGrip = rotação base × giro do acelerador), põe o
        // nó gripL/gripR em localPosition/localRotation = lhand/lhandRot do RigData e o alvo do TwoBoneIK = posição E rotação desse nó. O glTF
        // das motos guardou gripL/gripR já com a última pose salva no prefab (gripL da XRE: (-0,030; 0,014; -0,086) × lhand (-0,026; 0,026;
        // -0,076)) e o HandTarget fazia gripL.TransformPoint(lhand): o deslocamento entrava DUAS vezes e o punho ficava ~8 cm fora do lugar —
        // e a rotação da mão do RigData nunca era aplicada (ficava a do clipe, torcida pelo IK do braço). Agora o ponto é do PAI do nó, a
        // rotação (pai × lhandRot) é convertida do osso da mão do piloto do APK (prefab: dedos em +Y, indicador→mínimo em ±X) para o osso de
        // cada personagem pelo referencial anatômico (dedo médio + linha dos nós dos dedos), e quem vai ao ponto do APK é a PALMA (não o
        // punho): mão maior/menor que a do piloto do MX continua com a palma no punho do guidão. Metade da torção vai para o antebraço
        // (pronação), o resto no pulso; dobra do pulso limitada a 70°.
        public static bool HandFix = true;                                                                // false = como até a r49l (laboratório: antes × depois)
        public static int HandClamps; public static float HandTwistMax, HandBendMax;                    // laboratório
        bool hasHandRot;
        Transform GripOf(int i) { var g = i == 0 ? gripL : gripR; return HandFix && g && g.parent ? g.parent : g; }
        public const float PalmK = .55f;                                                                  // palma = 55 % do punho aos nós do dedo médio
        static readonly Vector3[] ApkIdx = { new(-.0317f, .1015f, -.0253f), new(.0317f, .1015f, -.0253f) }, ApkMid = { new(-.0094f, .1076f, -.0268f), new(.0094f, .1076f, -.0268f) },
                                  ApkLit = { new(.0319f, .0833f, -.041f), new(-.0319f, .0833f, -.041f) };   // LeftHand/RightHand do piloto do APK (23 prefabs iguais)
        static Quaternion HandBasis(Vector3 idx, Vector3 mid, Vector3 lit)
        { var f = mid.normalized; var lat = Vector3.ProjectOnPlane(lit - idx, f); if (lat.sqrMagnitude < 1e-10f) lat = Vector3.right; return Quaternion.LookRotation(f, Vector3.Cross(f, lat.normalized)); }
        static readonly Quaternion[] apkB = { HandBasis(ApkIdx[0], ApkMid[0], ApkLit[0]), HandBasis(ApkIdx[1], ApkMid[1], ApkLit[1]) };
        static readonly Dictionary<Transform, (Transform idx, Transform mid, Transform lit)> fingerBones = new();
        public static bool HandBones(Transform hand, Animator an, int i, out Transform idx, out Transform mid, out Transform lit)
        {
            idx = mid = lit = null; if (!hand) return false;
            if (!fingerBones.TryGetValue(hand, out var fb))
            {
                Transform H(HumanBodyBones b) => an && an.isHuman ? an.GetBoneTransform(b) : null;
                Transform C(params string[] keys) { foreach (Transform c in hand) foreach (var k in keys) if (c.name.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0) return c; return null; }
                fb = (H(i == 0 ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal) ?? C("index"), H(i == 0 ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal) ?? C("middle"),
                      H(i == 0 ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal) ?? C("pinky", "little"));
                if (fingerBones.Count > 2000) fingerBones.Clear(); fingerBones[hand] = fb;
            }
            (idx, mid, lit) = fb; return idx && mid && lit;
        }
        /// <summary>Referencial anatômico do osso da mão (no espaço de rotação do osso) e o vetor punho→palma (comprimento de mundo).</summary>
        static bool HandFrame(Transform hand, Animator an, int i, out Quaternion basis, out Vector3 palm)
        {
            basis = Quaternion.identity; palm = Vector3.zero; if (!HandBones(hand, an, i, out var idx, out var mid, out var lit)) return false;
            var iq = Quaternion.Inverse(hand.rotation); var p = hand.position; var m = iq * (mid.position - p); if (m.sqrMagnitude < 1e-8f) return false;
            basis = HandBasis(iq * (idx.position - p), m, iq * (lit.position - p)); palm = m * PalmK; return true;
        }
        /// <summary>Ponto do APK onde fica a palma (mundo).</summary>
        public Vector3 ApkPalmW(int i) { if (!curInit) RiderClip(0); var g = GripOf(i); var l = (i == 0 ? cur.lhand : cur.rhand) + (i == 0 ? cur.lrot : cur.rrot) * (ApkMid[i] * PalmK); return g ? g.TransformPoint(l) : transform.TransformPoint(l); }
        /// <summary>r49m: alvo do punho do personagem (a palma no ponto do APK) e a rotação de mundo da mão dele.</summary>
        public Vector3 HandGoal(int i, Animator an, Transform hand, out Quaternion rot, out bool ok)
        {
            var wr = HandTarget(i); rot = hand ? hand.rotation : Quaternion.identity; ok = false; var g = GripOf(i);
            if (!HandFix || !hasHandRot || !hand || !g || !HandFrame(hand, an, i, out var ob, out var palm)) return wr;
            rot = g.rotation * (i == 0 ? cur.lrot : cur.rrot) * apkB[i] * Quaternion.Inverse(ob); ok = true;
            return ApkPalmW(i) - rot * palm;
        }
        /// <summary>r49m: aplica a rotação da mão DEPOIS do IK do braço: metade da torção no antebraço, dobra do pulso até 70°.</summary>
        public static void SetHand(Animator an, int i, Transform fore, Transform hand, Quaternion want, bool ok, float w)
        {
            if (!ok || !hand || w <= 1e-3f) return; w = Mathf.Min(1, w);
            if (fore)
            {
                var ax = hand.position - fore.position;
                if (ax.sqrMagnitude > 1e-8f)
                {
                    ax.Normalize();
                    if (HandFrame(hand, an, i, out var ob, out _))
                    {   // dobra: dedos (rotação pedida) × eixo do antebraço
                        var fw = want * (ob * Vector3.forward); float bend = Vector3.Angle(ax, fw); HandBendMax = Mathf.Max(HandBendMax, Mathf.Min(bend, 70));
                        if (bend > 70) { want = Quaternion.FromToRotation(fw, Vector3.Slerp(ax, fw, 70 / bend)) * want; HandClamps++; }
                    }
                    var d = want * Quaternion.Inverse(hand.rotation); if (d.w < 0) d = new Quaternion(-d.x, -d.y, -d.z, -d.w);
                    float tw = 2 * Mathf.Atan2(Vector3.Dot(new Vector3(d.x, d.y, d.z), ax), d.w) * Mathf.Rad2Deg;
                    HandTwistMax = Mathf.Max(HandTwistMax, Mathf.Abs(tw * .5f));
                    fore.rotation = Quaternion.AngleAxis(tw * .5f * w, ax) * fore.rotation;          // gira no próprio eixo: o punho não sai do lugar
                }
            }
            hand.rotation = Quaternion.Slerp(hand.rotation, want, w);
        }
        public Vector3 ElbowHint(int i) { if (!curInit) RiderClip(0); return transform.TransformPoint(i == 0 ? cur.lhh : cur.rhh); }
        public Vector3 KneeHint(int i) { if (!curInit) RiderClip(0); return transform.TransformPoint(i == 0 ? cur.lfh : cur.rfh); }
        // r49 (foto do usuário, 30/10: moto pequena com os joelhos lá em cima e os pés fora das pedaleiras): na pop/moby/cg a pedaleira fica a
        // 0,55–0,65 m do quadril e a perna dos nossos personagens tem ~1 m — com a dica do RigData (à frente, quase na altura do banco) a dobra
        // do joelho subia até a altura do quadril/peito (laboratório r49a: moby joelho D 0,82 = quadril 0,82). Quanto mais dobrada a perna, mais
        // a dica vai para FORA e um pouco para baixo: o joelho abre para o lado, como quem pilota moto baixa, e a canela desce para a pedaleira.
        public static float KneeOutMax;                                                                   // laboratório
        public Vector3 KneePole(int i, Transform ul, Transform ll, Transform ft, Vector3 tg)
        {
            var p = KneeHint(i); if (!ul || !ll || !ft) return p;
            float L = (ll.position - ul.position).magnitude + (ft.position - ll.position).magnitude, d = (tg - ul.position).magnitude;
            float fold = Mathf.InverseLerp(.72f, .58f, d / Mathf.Max(.1f, L)); if (fold <= 0) return p;   // r49e: .82 abria o joelho em toda moto (fold 1, joelho 0,42–0,44 para o lado)
            var bt = transform; var side = bt.right * (i == 0 ? -1 : 1); var mid = Vector3.Lerp(ul.position, tg, .5f);
            var outP = mid + side * .45f * L + bt.forward * .35f * L - bt.up * .25f * L;
            KneeOutMax = Mathf.Max(KneeOutMax, fold);
            return Vector3.Lerp(p, outP, fold * .6f);
        }
        public Vector3 FootTarget(int i) => FootTarget(i, out _);
        public Vector3 FootTarget(int i, out float? groundY)
        {
            if (!curInit) RiderClip(0); var bt = transform; var fp = i == 0 ? cur.lfoot : cur.rfoot; var tg = bt.TransformPoint(fp); groundY = null;
            if (i != 0) return tg;
            // parado: pé esquerdo no chão ao lado da moto (como os clipes de parado do 171). r49i: desce/sobe em 0,35 s num arco (antes num quadro)
            bool down = state == St.Idle && Mathf.Abs(bikeSpeed) < 1;
            if (!SmoothRider) footDown = down ? 1 : 0; else if (footFrame != Time.frameCount) { footFrame = Time.frameCount; footDown = Mathf.MoveTowards(footDown, down ? 1 : 0, Time.deltaTime / .35f); }
            if (footDown > 0)
            {
                var g = bt.TransformPoint(new Vector3(fp.x - .12f, fp.y, fp.z + .1f));
                if (Physics.Raycast(g + Vector3.up * .6f, Vector3.down, out var h, 2.5f, FBWorld.SolidMask & ~(1 << FBWorld.LVehicle), QueryTriggerInteraction.Ignore))
                { float k = SmoothRider ? Mathf.SmoothStep(0, 1, footDown) : footDown; tg = Vector3.Lerp(tg, h.point + Vector3.up * .09f, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * .05f * S); if (k > .6f || SmoothRider) groundY = h.point.y; }   // r49i: ponta do pé corrigida o arco todo (cortar em 60 % virava o pé 21° num quadro)
            }
            return tg;
        }
        // r46l: pé no chão parado — o IK põe o TORNOZELO a 9 cm e a rotação do pé fica a do clipe; a ponta afundava até 15 cm (MX_Idle, sonda
        // r46j/l; e o começo de descer, que agora mistura a partir desta pose, herdava). Gira o pé no tornozelo só o que falta para a ponta ficar
        // no chão (o calcanhar sobe: pé apoiado na ponta, como quem segura a moto)
        public static int ToeFixes; public static float ToeFixMax;
        // r46m: a subida entra em rampa (1 m/s) — inteira no 1º quadro do parado, o pé girava 1.860°/s (sonda r46m, fim do blend de subir)
        // r49m: no arranque o pé sobe da rua à pedaleira a até ~2 m/s e a correção caía junto (need = mín(need0, toeSm)): o pé desvirava
        //  ~720°/s (laboratório -fbva_pilotar: LeftFoot 716–1262 °/s a 0,2 s). Agora a correção se desfaz a no máximo 0,45 m/s e continua
        //  até zerar mesmo depois que o pé deixou o chão (antes sumia no quadro em que o arco acabava).
        float toeSm, toeGround; int toeFrame;
        void ToeAboveGround(Animator an, Transform ft, HumanBodyBones toeB, float groundY, float k)
        {
            var toe = an ? an.GetBoneTransform(toeB) : null; if (!ft || !toe) return; float need0 = Mathf.Max(0, groundY + .015f * k - toe.position.y);
            if (toeFrame != Time.frameCount - 1) toeSm = 0; toeFrame = Time.frameCount;
            toeSm = Mathf.MoveTowards(toeSm, need0, Time.deltaTime * (need0 > toeSm || !SmoothRider ? 1f : .45f)); float need = SmoothRider ? toeSm : Mathf.Min(need0, toeSm); if (need <= 1e-3f) return;
            var v = toe.position - ft.position; float L = v.magnitude; if (L < 1e-4f) return; float hz = new Vector2(v.x, v.z).magnitude; if (hz < 1e-4f) return;
            float wy = Mathf.Min(v.y + need, L * .95f), wh = Mathf.Sqrt(Mathf.Max(0, L * L - wy * wy)); var want = new Vector3(v.x / hz * wh, wy, v.z / hz * wh);
            ft.rotation = Quaternion.FromToRotation(v, want) * ft.rotation; ToeFixes++; ToeFixMax = Mathf.Max(ToeFixMax, need);
        }
        // r49 (foto do usuário, 30/10: "BMW GS — piloto curvado sobre o tanque, braços pressionados contra ele"). Causa (laboratório r49c):
        // os estilos de piloto do 171 (FBDrive.RiderStyle: MOTOIdle01/02, Pose_on_bike_Fwd…) foram feitos para moto baixa de guidão longe —
        // deitam o tronco 26–32° e põem os OMBROS praticamente em cima dos punhos (ombro→punho 5–11 cm à frente; no MX_Idle são 26–34 cm).
        // Na GS 1250 o guidão é alto e perto: o peito ia para o painel/tanque e os antebraços ficavam espremidos contra ele (o Reach ainda
        // escorregava o corpo à frente quando o braço não chegava). Agora, depois do Reach, se os ombros estão a menos de 26 cm (× escala)
        // atrás dos punhos, a coluna volta para trás (coluna/peito/peito alto, até 35°) só o que falta — o estilo do clipe continua (cabeça,
        // braços, pernas), sem o peito no tanque.
        public static int ChestFixes; public static float ChestFixMax;                                  // laboratório
        public static void ChestClear(FBCharacter a, Transform bt, Vector3 gL, Vector3 gR, float w)
        {
            var an = a ? a.an : null; if (!an || !an.isHuman || w <= .01f) return;
            Transform B(HumanBodyBones b) => an.GetBoneTransform(b);
            var lA = B(HumanBodyBones.LeftUpperArm); var rA = B(HumanBodyBones.RightUpperArm); var sp = B(HumanBodyBones.Spine); if (!lA || !rA || !sp) return;
            float want = .26f * a.ScaleK, done = 0; var fwd = bt.forward; var right = bt.right; var g = (gL + gR) * .5f;
            for (int it = 0; it < 3; it++)                                                                 // a cadeia gira em 3 ossos: 2–3 passadas convergem
            {
                var sh = (lA.position + rA.position) * .5f; float dz = Vector3.Dot(g - sh, fwd); if (dz >= want - .005f) break;
                if (it == 0) w *= Mathf.InverseLerp(.55f, .35f, Vector3.Dot(sh - g, bt.up) / Mathf.Max(.3f, a.ScaleK)); if (w <= .01f) break;   // em pé na pedaleira o peito passa longe do guidão
                var v = Vector3.ProjectOnPlane(sh - sp.position, right); float L = v.magnitude; if (L < .1f) break;
                float phi = Vector3.SignedAngle(bt.up, v, right), zs = Vector3.Dot(v, fwd) - (want - dz);
                float phi2 = Mathf.Max(-12, Mathf.Asin(Mathf.Clamp(zs / L, -1, 1)) * Mathf.Rad2Deg), th = Mathf.Min(phi - phi2, 35 - done) * w; if (th < .2f) break;
                foreach (var (bb, k) in new[] { (HumanBodyBones.Spine, .4f), (HumanBodyBones.Chest, .35f), (HumanBodyBones.UpperChest, .25f) })
                { var b = B(bb); if (b) b.rotation = Quaternion.AngleAxis(-th * k, right) * b.rotation; }
                done += th / Mathf.Max(.01f, w);
            }
            if (done > .2f) { ChestFixes++; ChestFixMax = Mathf.Max(ChestFixMax, done); }
        }
        // r50 (lab r49n, fotos do r_tutu parado na XRE: a perna esquerda descia RETA pela lateral da moto; sonda: coxa E × chassi 22 cm em 38
        // quadros sentado). O pé de apoio vai ao chão 12 cm para fora da pedaleira do RigData — medida do piloto do APK; nossos personagens
        // são mais altos que a moto (1,43 un/m de gente × 1,2 da MX) e a perna, quase vertical, atravessava banco/tampa lateral. O FBLegClear
        // (r48) só age nas transições e se desliga com o pé no chão. Aqui: com o pé no chão, coxa (do 30 % em diante — perto do quadril ela
        // encosta no banco de verdade) e canela são testadas contra a MALHA da moto e o pé vai para fora em passos de 3,5 cm (até ~25 cm) até
        // livrar; o afastamento é suave (sobe 1,2 un/s, volta 0,4). Jogador sempre; NPC a até 25 m da câmera. GroundLegOn = false = como antes.
        public static bool GroundLegOn = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbsemapoio") < 0;
        public static int GroundPushes; public static float GroundPushMax;
        float legPush; int legPushFrame;
        Vector3 GroundLegClear(FBCharacter a, Transform ul, Transform ll, Transform ft, Vector3 tg, float? gy, float w, Quaternion fr, ref float? gyOut)
        {
            if (!GroundLegOn || !car || !ul || !ll || !ft) return tg;
            float dtl = legPushFrame == Time.frameCount - 1 ? Time.deltaTime : 0; legPushFrame = Time.frameCount;
            float need = 0, k = Mathf.Max(.5f, a.ScaleK), step = .035f * k; var cam = UnityEngine.Camera.main;
            var outDir = Vector3.ProjectOnPlane(-transform.right, Vector3.up).normalized; Vector3? lastG = null;
            Vector3 At(float push)
            {
                var t2 = tg + outDir * push;
                if (gy != null && Physics.Raycast(t2 + Vector3.up * .6f, Vector3.down, out var h, 1.5f, FBWorld.SolidMask & ~(1 << FBWorld.LVehicle), QueryTriggerInteraction.Ignore)) { t2.y = tg.y + (h.point.y - gy.Value); lastG = h.point; }
                return t2;
            }
            void Solve(Vector3 t2) { FBIK.TwoBonePole(ul, ll, ft, t2, KneePole(0, ul, ll, ft, t2), w); ft.rotation = fr; }
            bool near = a.isPlayer || !cam || (cam.transform.position - transform.position).sqrMagnitude < 25 * 25;   // NPC longe: sem custo (a malha vira colisor uma vez por moto)
            if (gy != null && near && FBLegClear.Arm(car, true))
            {
                try
                {
                    bool Hit() => FBLegClear.Hits(Vector3.Lerp(ul.position, ll.position, .3f), ll.position) || FBLegClear.Hits(ll.position, ft.position);
                    for (int it = 0; it < 7 && Hit(); it++) { need += step; Solve(At(need)); }
                    if (need > 0 && Hit()) need = Mathf.Max(need, legPush);                      // não livrou: fica no máximo achado
                }
                finally { FBLegClear.Arm(car, false); }
            }
            legPush = dtl <= 0 ? need : Mathf.MoveTowards(legPush, need, dtl * (need > legPush ? 1.2f : .4f));
            if (legPush > .002f) { GroundPushes++; GroundPushMax = Mathf.Max(GroundPushMax, legPush); }
            var fin = At(legPush); Solve(fin); if (gy != null && lastG is Vector3 lg) gyOut = lg.y; return fin;
        }
        /// <summary>IK do piloto nos pontos do RigData (depois da animação). Devolve os alvos para diagnóstico.</summary>
        public FBDrive.PoseTargets PoseRider(FBCharacter a, float w)
        { bool was = FBIK.Anat; FBIK.Anat = true; try { return PoseRiderCore(a, w); } finally { FBIK.Anat = was; if (!was) FBIK.Where = "-"; } }   // r46g: torção anatômica limitada (FBIK)
        FBDrive.PoseTargets PoseRiderCore(FBCharacter a, float w)
        {
            var T = new FBDrive.PoseTargets(); var an = a.an; if (!an || !an.isHuman) return T; var hips = an.GetBoneTransform(HumanBodyBones.Hips); if (!hips) return T;
            if (!curInit) RiderClip(0); FBIK.Where = "pilotando/mx";
            var bt = transform; var p = cur;
            // massa do piloto (PM): mola atrás do quadril — acelerar joga o corpo para trás, frear para a frente, curva para fora
            var anchor = bt.TransformPoint(p.hips) + bt.up * .25f * S; float dt = Mathf.Max(1e-3f, Time.deltaTime);
            // r49i: o amortecimento era pela velocidade ABSOLUTA da massa — andando a v constante o equilíbrio fica 25·v/120 ≈ 0,21·v atrás
            //  do quadril (0,35 m a 6 km/h): passava do limite de 0,3 m, zerava (quadril pulando 8 cm, braço 19° num quadro no laboratório
            //  -fbva_pilotar) e recomeçava a cada ~6 quadros acima de ~5 km/h. Agora amortece a velocidade RELATIVA ao ponto de apoio: parado ou
            //  em velocidade constante a massa fica no lugar; acelerar/frear/curvar desloca a/k (3 m/s² ≈ 2,5 cm), como a mola foi pensada.
            // [parceiro] (r45 → 31/10) achou o mesmo tremor ("o piloto tremia para frente e para trás entre ~6 e 45 km/h") e refez a mola
            //  como deslocamento relativo à moto empurrado só pela aceleração do apoio (2ª derivada, limitada a 40 m/s²). A dinâmica relativa
            //  é a mesma desta (x'' = −k·x − c·x' − a_apoio); fica a nossa r49i, validada no -fbva_pilotar, que deriva o apoio uma vez só.
            //  Do parceiro entrou o pmInit = false no SetActive (já zerava aqui quando o apoio pula mais de 1 m).
            if (!SmoothRider) { if (!pmInit) { pmPos = anchor; pmVel = Vector3.zero; pmInit = true; } }
            else if (!pmInit || (anchor - pmAnchor).sqrMagnitude > 1) { pmPos = pmAnchor = anchor; pmVel = Vector3.zero; pmInit = true; }
            var av = SmoothRider ? (anchor - pmAnchor) / dt : Vector3.zero; pmAnchor = anchor;
            var lo = bt.InverseTransformVector(pmPos - anchor); var lv = bt.InverseTransformVector(pmVel - av);
            var acc = new Vector3(-250 * lo.x - 20 * lv.x, -250 * lo.y - 20 * lv.y, -120 * lo.z - 25 * lv.z); pmVel += bt.TransformVector(acc) * dt; pmPos += pmVel * dt;
            if ((pmPos - anchor).sqrMagnitude > .09f) { pmPos = anchor; pmVel = av; PmResets++; }
            var sway = bt.InverseTransformVector(pmPos - anchor); sway = new Vector3(Mathf.Clamp(sway.x, -.06f, .06f), Mathf.Clamp(sway.y, -.04f, .04f), Mathf.Clamp(sway.z, -.08f, .08f)) * w;
            a.transform.rotation = bt.rotation;
            a.transform.position += bt.TransformPoint(p.hips + new Vector3(sway.x, sway.y, sway.z) / S) - hips.position;
            // tronco: arfagem da pose (em pé / grau) + grau real + olhar para o lado da curva no grau
            float pitch = p.pitch * w + Mathf.Clamp(wheelieAngle, 0, 60) * -.25f * w; var right = bt.right; var fwd = bt.forward;
            foreach (var (bb, k) in new[] { (HumanBodyBones.Spine, .35f), (HumanBodyBones.Chest, .35f), (HumanBodyBones.UpperChest, .3f) })
            { var b = an.GetBoneTransform(bb); if (b) b.rotation = Quaternion.AngleAxis(pitch * k, right) * Quaternion.AngleAxis(-leanWheelieAngle * .3f * k * w, bt.up) * b.rotation; }
            var head = an.GetBoneTransform(HumanBodyBones.Head); if (head) head.rotation = Quaternion.AngleAxis(-pitch * .6f, right) * Quaternion.AngleAxis(leanAngle * .35f * w, fwd) * head.rotation;
            Transform B(HumanBodyBones b) => an.GetBoneTransform(b);
            // mãos nos punhos do guidão (que giram com a direção); manobra B solta a esquerda
            if (gripL && gripR)
            {
                var lA = B(HumanBodyBones.LeftUpperArm); var lF = B(HumanBodyBones.LeftLowerArm); var lH = B(HumanBodyBones.LeftHand); var rA = B(HumanBodyBones.RightUpperArm); var rF = B(HumanBodyBones.RightLowerArm); var rH = B(HumanBodyBones.RightHand);
                var hL = HandGoal(0, an, lH, out var qL, out var okL); var hR = HandGoal(1, an, rH, out var qR, out var okR);   // r49m: palma no punho do guidão + rotação do RigData
                if (state != St.TrickB) FBDrive.Reach(a, car, a.GetComponent<FBDriverPose>(), lA, lF, lH, rA, rF, rH, hL, hR, w);   // braço do nosso personagem mais curto que o do piloto do MX: o tronco vai à frente
                ChestClear(a, bt, hL, hR, w);                                                               // r49: peito fora do tanque/painel
                FBIK.TwoBonePole(lA, lF, lH, hL, bt.TransformPoint(p.lhh), w); FBIK.TwoBonePole(rA, rF, rH, hR, bt.TransformPoint(p.rhh), w);
                SetHand(an, 0, lF, lH, qL, okL, w); SetHand(an, 1, rF, rH, qR, okR, w);
                T.hands = true; T.hL = hL; T.hR = hR;
            }
            // pés nas pedaleiras (joelhos pelos pontos de dica); a rotação do pé fica a do clipe
            var legs = new[] { (B(HumanBodyBones.LeftUpperLeg), B(HumanBodyBones.LeftLowerLeg), B(HumanBodyBones.LeftFoot), p.lfoot, p.lfh), (B(HumanBodyBones.RightUpperLeg), B(HumanBodyBones.RightLowerLeg), B(HumanBodyBones.RightFoot), p.rfoot, p.rfh) };
            for (int i = 0; i < 2; i++)
            {
                var (ul, ll, ft, fp, fh) = legs[i]; if (!ft) continue; var tg = FootTarget(i, out var gy);
                var fr = ft.rotation; FBIK.TwoBonePole(ul, ll, ft, tg, KneePole(i, ul, ll, ft, tg), w); ft.rotation = fr;
                if (i == 0) tg = GroundLegClear(a, ul, ll, ft, tg, gy, w, fr, ref gy);
                if (gy is float g0) { if (i == 0) toeGround = g0; ToeAboveGround(an, ft, i == 0 ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes, g0, Mathf.Max(.5f, a.ScaleK)); }
                else if (i == 0 && SmoothRider && toeSm > 1e-3f && toeFrame == Time.frameCount - 1) ToeAboveGround(an, ft, HumanBodyBones.LeftToes, toeGround, Mathf.Max(.5f, a.ScaleK));   // r49m: termina de desvirar
                if (i == 0) T.fL = tg; else T.fR = tg; T.feet = true;
            }
            return T;
        }
    }
}
