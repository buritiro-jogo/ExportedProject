// r46: LABORATÓRIO DAS TRANSIÇÕES DE VEÍCULO — FreeBala.exe -fbveicanim [mapa:modo]   (padrão vila_jardim:role)
//   opcionais: -fbva_chars r_a,x171_b · -fbva_carros Fusca,S10_2017 · -fbva_motos pop110,xj6 · -fbva_quedas pop110,xj6 · -fbva_fotos 0|1|2
//   r47: -fbva_emotes [padrao|todos|NPC_a,NPC_b] — no lugar dos veículos, cada personagem dança cada emote na pista (ver Emotes)
// Pedido de 29/10: "esqueletos deformados, ossos deslocados, articulações quebradas" ao entrar/sair do carro, subir/descer da moto e
// nas quedas. Personagens de 4 famílias (regional homem/mulher, 171 homem/mulher) × carros de tamanhos diferentes × motos MX: NPC entra e
// sai do carro, sobe e desce da moto e cai da moto andando (frente, esquerda, direita, de costas); o jogador também (carro, moto, queda).
// A FBPoseProbe mede o esqueleto quadro a quadro (LateUpdate depois de tudo que mexe em osso). Fotos de perto em 6 tempos (lado e
// frente-diagonal) para o 1º personagem de cada família (-fbva_fotos 2 = todos, 0 = nenhum) e o quadro do primeiro defeito grave de cada
// transição. Uma linha "T ..." por transição e a tabela família × transição no fim.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    [DefaultExecutionOrder(5000)]
    public class FBVeicAnimLab : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbveicanim"); if (i < 0) return;
            var g = new GameObject("FB_VeicAnimLab"); DontDestroyOnLoad(g); var t = g.AddComponent<FBVeicAnimLab>(); if (i + 1 < a.Length && !a[i + 1].StartsWith("-")) t.run = a[i + 1];
        }
        static string Arg(string k) { var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, k); return i >= 0 && i + 1 < a.Length && !a[i + 1].StartsWith("-") ? a[i + 1] : null; }
        string run = "vila_jardim:role", dir; Camera cam; Vector3 spot; int fotos = 1;
        // r48: -fbva_semveiculo 1 esconde a lataria/interior do veículo SÓ no quadro da foto (a lataria cobria o corpo nas fotos de perto) e
        // -fbva_nfotos N tira N tempos por transição, com mais uma vista de trás (como a câmera do jogo); com fotos, o tempo do jogo anda em
        // passos fixos de 1/30 s (Time.captureFramerate) para cada foto cair no mesmo instante do clipe em toda execução
        bool semVeic; int nFotos = 6;
        static void Log(string s) => Debug.Log("[FreeBala][veicanim] " + s);
        IEnumerator Wait(float s) { for (float w = 0; w < s; w += Time.unscaledDeltaTime) yield return null; }
        IEnumerator Shot(string n) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png")); yield return null; }
        void Look(Vector3 from, Vector3 at, float fov = 42) { cam.transform.position = from; cam.transform.LookAt(at); cam.fieldOfView = fov; }
        // r48: piloto sentado na MX — NPC atravessava o chassi até 25 cm (coxa D, sentado) e o jogador 0: giro, quadril, joelhos e pés no
        // referencial da MX, FBCar × MX e se a física da MX está ligada
        static string V3(Vector3 v) => $"({v.x:0.00};{v.y:0.00};{v.z:0.00})";
        static string SeatDiag(FBCharacter a, FBCar v)
        {
            var an = a ? a.an : null; var h = an ? an.GetBoneTransform(HumanBodyBones.Hips) : null; if (!h || !v) return "-";
            var M = v.mx ? v.mx.transform : v.transform; string L(HumanBodyBones b) { var x = an.GetBoneTransform(b); return x ? V3(M.InverseTransformPoint(x.position)) : "-"; }
            var lU = an.GetBoneTransform(HumanBodyBones.LeftUpperLeg); var rU = an.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            float pel = lU && rU ? Vector3.SignedAngle(M.forward.Flat(), Vector3.Cross(rU.position - lU.position, Vector3.up).Flat(), Vector3.up) : 0;
            // r49: tronco (quadril→pescoço) contra o "em pé" da moto, no plano dela (+ = à frente), inclinação que o Reach somou e quanto faltou
            var nk = an.GetBoneTransform(HumanBodyBones.Neck) ?? an.GetBoneTransform(HumanBodyBones.Head); var dpz = a.GetComponent<FBDriverPose>();
            float torso = nk ? Vector3.SignedAngle(M.up, Vector3.ProjectOnPlane(nk.position - h.position, M.right), M.right) : 0;
            var lHd = an.GetBoneTransform(HumanBodyBones.LeftHand); var lSh = an.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            string reach = $"tronco {torso:0}° · alcance {(dpz ? dpz.reachLean : 0):0}° falta {FBDrive.LastNeed * 100:0} cm resto {FBDrive.LastRest * 100:0} cm · ombro E {(lSh ? V3(M.InverseTransformPoint(lSh.position)) : "-")} mão E {(lHd ? V3(M.InverseTransformPoint(lHd.position)) : "-")} alvo E {(v.mx ? V3(M.InverseTransformPoint(v.mx.HandTarget(0))) : "-")} · joelho aberto {FBMxBike.KneeOutMax:0.00} · peito recuado {FBMxBike.ChestFixMax:0}° ({FBMxBike.ChestFixes} quadros) · ombro→punho {(lSh && v.mx ? Vector3.Dot(v.mx.HandTarget(0) - lSh.position, M.forward) * 100 : 0):0} cm à frente"; FBMxBike.KneeOutMax = 0; FBMxBike.ChestFixMax = 0; FBMxBike.ChestFixes = 0;
            return reach + $" · raiz×MX {Mathf.DeltaAngle(M.eulerAngles.y, a.transform.eulerAngles.y):0}° · bacia×MX {pel:0}° · FBCar×MX giro {Mathf.DeltaAngle(M.eulerAngles.y, v.transform.eulerAngles.y):0}° pos {V3(M.InverseTransformPoint(v.transform.position))}" +
                   $" · MX tombada {Vector3.SignedAngle(Vector3.up, M.up, M.forward):0.0}° ativa {(v.mx ? v.mx.active.ToString() : "-")} · quadril {V3(M.InverseTransformPoint(h.position))} banco {V3(M.InverseTransformPoint(FBDrive.SeatHips(v)))}" +
                   $" · coxa D {L(HumanBodyBones.RightUpperLeg)} joelho D {L(HumanBodyBones.RightLowerLeg)} pé D {L(HumanBodyBones.RightFoot)} · coxa E {L(HumanBodyBones.LeftUpperLeg)} joelho E {L(HumanBodyBones.LeftLowerLeg)} pé E {L(HumanBodyBones.LeftFoot)} · clipe {a.sys ?? "-"} danW {a.DanW:0.00}";
        }
        // r49m: mão no punho do guidão — palma (55 % do punho aos nós do dedo médio) contra o nó do punho do guidão (LeftHandGrip/RightHandGrip,
        // posto pelo autor da moto no punho de borracha) e contra o ponto do APK, linha dos nós dos dedos × eixo do guidão (punho E → punho D;
        // segurando o guidão ficam quase paralelos) e a dobra do pulso (antebraço × dedo médio)
        static string HandDiag(FBCharacter a, FBCar v)
        {
            var an = a ? a.an : null; var mx = v ? v.mx : null; if (!an || !mx || !mx.gripL || !mx.gripR) return "-"; var sb = new System.Text.StringBuilder();
            Transform Gp(Transform g) => g.parent ? g.parent : g;
            for (int i = 0; i < 2; i++)
            {
                var hand = an.GetBoneTransform(i == 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand); var fore = an.GetBoneTransform(i == 0 ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                var gp = Gp(i == 0 ? mx.gripL : mx.gripR); var bar = Gp(mx.gripR).position - Gp(mx.gripL).position;
                if (!hand || !FBMxBike.HandBones(hand, an, i, out var idx, out var mid, out var lit)) { sb.Append(i == 0 ? "E: sem dedos · " : "D: sem dedos · "); continue; }
                var palm = hand.position + (mid.position - hand.position) * FBMxBike.PalmK; float kn = Vector3.Angle(lit.position - idx.position, bar); kn = Mathf.Min(kn, 180 - kn);
                float bend = fore ? Vector3.Angle(hand.position - fore.position, mid.position - hand.position) : 0;
                sb.Append($"{(i == 0 ? "E" : "D")}: palma→punho do guidão {(palm - gp.position).magnitude * 100:0.0} cm · palma→ponto do APK {(palm - mx.ApkPalmW(i)).magnitude * 100:0.0} cm · nós×guidão {kn:0}° · dobra do pulso {bend:0}° · ");
            }
            sb.Append($"torção no antebraço até {FBMxBike.HandTwistMax:0}° · pulso limitado {FBMxBike.HandClamps} quadros");
            return sb.ToString();
        }
        IEnumerator Maos(FBCharacter a, FBCar v, string n)
        {
            if (!v || !v.mx || !a) yield break;
            foreach (bool novo in new[] { false, true })
            {
                FBMxBike.HandFix = novo; FBMxBike.HandClamps = 0; FBMxBike.HandTwistMax = FBMxBike.HandBendMax = 0;
                for (int k = 0; k < 5; k++) yield return null; yield return new WaitForEndOfFrame();
                Log($"  mãos {(novo ? "DEPOIS" : "antes")} {a.skinId} × {v.id}: {HandDiag(a, v)}");
                if (fotos > 0)
                {
                    var an = a.an; var lh = an ? an.GetBoneTransform(HumanBodyBones.LeftHand) : null; var rh = an ? an.GetBoneTransform(HumanBodyBones.RightHand) : null; var T = v.mx.transform;
                    var f = T.forward.Flat().normalized; var r = Vector3.Cross(Vector3.up, f); cam.enabled = true;
                    if (lh) { var c = lh.position; Look(c - r * .62f + f * .38f + Vector3.up * .3f, c, 34); yield return Shot($"{n}_{(novo ? "depois" : "antes")}_E"); }
                    if (rh) { var c = rh.position; Look(c + r * .62f + f * .38f + Vector3.up * .3f, c, 34); yield return Shot($"{n}_{(novo ? "depois" : "antes")}_D"); }
                    cam.enabled = false;
                }
            }
            FBMxBike.HandFix = true;
        }
        // r49: carro, sentado — topo da cabeça (vértice mais alto da malha do personagem) contra o teto VISÍVEL (malha LOD 0, por cima e por
        // dentro), pés dentro da cabine (x local contra a lateral da carroceria na altura do banco) e quadril × banco. Pedido 30/10 (fotos da
        // Saveiro com paredão): cabeça atravessando o teto, pernas para fora da porta.
        static float MeshMaxY(SkinnedMeshRenderer[] rs, out Vector3 at)
        {
            if (!meshBake) meshBake = new Mesh(); float m = float.MinValue; at = Vector3.zero;
            foreach (var r in rs) { if (!r || !r.enabled || !r.gameObject.activeInHierarchy) continue; r.BakeMesh(meshBake, true); meshBake.GetVertices(meshV); var t = r.transform; var p = t.position; var q = t.rotation; for (int i = 0; i < meshV.Count; i++) { var w = p + q * meshV[i]; if (w.y > m) { m = w.y; at = w; } } }
            return m;
        }
        public static float LastRoofGap = 9;
        static string CarSeatDiag(FBCharacter a, FBCar v)
        {
            var an = a ? a.an : null; var h = an ? an.GetBoneTransform(HumanBodyBones.Hips) : null; var hd = an ? an.GetBoneTransform(HumanBodyBones.Head) : null; if (!h || !hd || !v) return "-";
            var T = v.transform; float top = MeshMaxY(a.GetComponentsInChildren<SkinnedMeshRenderer>(), out var topAt);
            var cols = FBPoseProbe.AddVehicleColliders(v, out _); string roof = "sem teto"; LastRoofGap = 9;
            bool bf = Physics.queriesHitBackfaces; Physics.queriesHitBackfaces = true;
            try
            {
                FBPoseProbe.Arm(cols, true);
                var o = new Vector3(hd.position.x, top + 1.5f, hd.position.z); var hits = Physics.RaycastAll(o, Vector3.down, 3f, 1 << FBPoseProbe.Layer, QueryTriggerInteraction.Collide).OrderBy(x => x.distance).ToList();
                // de cima para baixo: 1º = teto por fora; o 1º com a normal para baixo = teto por dentro
                if (hits.Count > 0) { var ext = hits[0]; var inn = hits.FirstOrDefault(x => Vector3.Dot(x.normal, Vector3.down) > .3f && x.point.y < ext.point.y + .01f && ext.point.y - x.point.y < .2f);
                    float gIn = (inn.collider ? inn.point.y : ext.point.y - .03f) - top; LastRoofGap = gIn;
                    roof = $"teto por fora {ext.point.y - top:+0.00;-0.00} m · por dentro {(inn.collider ? (inn.point.y - top).ToString("+0.00;-0.00") : "?")} m ({ext.collider.name.Replace("sonda:", "")})"; }
            }
            finally { FBPoseProbe.Arm(cols, false); FBPoseProbe.RemoveColliders(cols); Physics.queriesHitBackfaces = bf; }
            string F(HumanBodyBones b) { var x = an.GetBoneTransform(b); if (!x) return "-"; var l = T.InverseTransformPoint(x.position); return $"x {l.x:0.00} y {l.y:0.00} z {l.z:0.00}"; }
            float half = Mathf.Abs(v.lbox.min.x); var lf = an.GetBoneTransform(HumanBodyBones.LeftFoot); float lfx = lf ? T.InverseTransformPoint(lf.position).x : 0;
            return $"topo da cabeça {roof} · cabeça×raiz {T.InverseTransformPoint(topAt).y:0.00} · quadril {V3(T.InverseTransformPoint(h.position))} banco {V3(T.InverseTransformPoint(FBDrive.SeatHips(v)))} · seatDrop {a.seatDrop:0.00}" +
                   $" · pé E {F(HumanBodyBones.LeftFoot)} pé D {F(HumanBodyBones.RightFoot)} · lateral {-half:0.00}{(lfx < -half ? " PÉ FORA" : "")} · escala {a.ScaleK:0.00} · {v.id} {(v.shell ? "carroceria y " + v.shell.localPosition.y.ToString("0.00") : "")}";
        }
        // r48: o NPC esperava fora da tela ("Cull Update Transforms" = ossos congelados, na pose T do FBX) e a mistura do começo de subir/entrar
        // partia dela (braços abertos nas fotos r48c/r48g). No jogo quem vê a subida já via o NPC antes: câmera nele 3 quadros, o Animator escreve.
        IEnumerator SeeIt(FBCharacter a)
        {
            var h = a.an ? a.an.GetBoneTransform(HumanBodyBones.Hips) : null; var hp = h ? h.position : a.Pos + Vector3.up;
            cam.enabled = true; Look(hp - a.transform.right * 3f + Vector3.up * .4f, hp); yield return null; yield return null; yield return null; cam.enabled = false;
        }
        readonly List<(string fam, string tr, string ch, string veh, List<string> fails)> table = new();

        // sonda ativa: amostra no LateUpdate (ordem 5000, depois de FBCharacter/FBDriverPose/FBMxBike/FBGarupa/FBFall)
        FBPoseProbe probe; FBCharacter subj; System.Func<string> phaseOf;
        void LateUpdate()
        {
            if (FBMatch.Paused) { FBMatch.Paused = false; FBHUD.I?.Pause(false, null); }   // r48: Esc/foco na janela abria o "PAUSADO" por cima das fotos (r48i)
            if (probe != null && subj) probe.Sample(Time.deltaTime, phaseOf != null ? phaseOf() : "-");
        }
        string Ph(FBCharacter a) { var dp = a ? a.GetComponent<FBDriverPose>() : null; return $"{dp?.trans ?? (FBFall.Busy(a) ? "queda" : "-")}/{a?.sys ?? "-"}"; }

        static List<object> State(FBCar c) { var w = Coords.ToWeb(c.transform.position); return new List<object> { c.syncId, (double)w[0], (double)w[1], (double)w[2], (double)c.yaw, 0d, 0d, 0d, 0d, 0d, 0d, 0d }; }
        FBCar Put(string vid, Vector3 at, float yaw) { if (FBWorld.GroundY(at.x, at.y + 3, at.z, .4f, 8) is float g) at.y = g; var c = FBVehicles.Spawn(vid, at + Vector3.up * .3f, yaw); if (c) c.syncId = "va:" + vid + ":" + Time.frameCount;
            // r49: -fbva_rebaixa CM = carro rebaixado na oficina (cm, positivo = mais baixo; o Arg recusa valor com "-") nos carros — o carro do jogador do pedido (paredão) é da garagem
            if (c && c.kind != "moto" && !c.mx && float.TryParse(Arg("-fbva_rebaixa"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rb)) FBTune.Apply(c, new FBTuneData { fh = -rb / 100f, rh = -rb / 100f });
            return c; }
        void Kill(FBCar c) { if (!c) return; if (c.mx) c.mx.SetActive(false); FBVehicles.list.Remove(c); Destroy(c.gameObject); }

        // ---- uma transição medida: começa a sonda, roda 'go' (que devolve quando acabou), tira as fotos nos tempos e a do 1º defeito
        IEnumerator Measure(string fam, string tr, FBCharacter a, FBCar veh, bool fall, bool shots, System.Func<bool> started, System.Func<bool> done, System.Func<float> expectF, float tail)
        {
            var cols = FBPoseProbe.AddVehicleColliders(fall ? null : veh, out int skipped);
            probe = new FBPoseProbe(a, fall ? null : veh, fall, cols); subj = a; phaseOf = () => Ph(a); if (fall) probe.MarkPreFall();
            if (shots && semVeic) Time.captureFramerate = 30;
            bool ok = started(); float expect = Mathf.Max(.5f, expectF()); string name = $"{tr}_{a.skinId}_{veh?.id ?? "-"}"; int defShots = 0; float t0 = Time.time; int ph = 0;
            float[] at = nFotos == 6 ? new[] { .08f, .25f, .42f, .58f, .75f, .92f } : Enumerable.Range(0, nFotos).Select(k => (k + .5f) / nFotos).ToArray();
            if (ok)
            {
                while (Time.time - t0 < expect + 3 && !done())
                {
                    if (shots && ph < at.Length && Time.time - t0 >= expect * at[ph]) { yield return Photo(a, veh, $"{name}_{ph}", fall); ph++; continue; }
                    if (defShots < 2 && probe.TakeFlag(out var why)) { defShots++; yield return Photo(a, veh, $"{name}_defeito{defShots}", fall); Log($"  foto do defeito {defShots}: {why} t={probe.t:0.00}"); continue; }
                    yield return null;
                }
                for (float w = 0; w < tail; w += Time.deltaTime) yield return null;
            }
            Time.captureFramerate = 0;
            var p = probe; probe = null; subj = null; FBPoseProbe.RemoveColliders(cols);
            var fails = ok ? p.Fails() : new List<string> { "não começou" }; p.Dispose(); table.Add((fam, tr, a.skinId, veh?.id ?? "-", fails));
            Log($"T {tr} · {a.skinId} ({fam}) × {veh?.id ?? "-"} · {(ok ? "começou" : "NÃO COMEÇOU")} · {(fails.Count == 0 ? "OK" : "FALHAS: " + string.Join(", ", fails))} · {p.Report()}" + (skipped > 0 ? $" · malhas sem leitura {skipped}" : ""));
        }
        // r46n: quadril × raiz (m, escala do personagem), Animator e visibilidade — o começo de subir da moto guardava o quadril no céu
        static string HipDiag(FBCharacter a)
        {
            var an = a ? a.an : null; var h = an ? an.GetBoneTransform(HumanBodyBones.Hips) : null; if (!h) return "-";
            var l = a.transform.InverseTransformPoint(h.position); bool vis = a.GetComponentsInChildren<Renderer>().Any(r => r.isVisible);
            return $"({l.x:0.00};{l.y:0.00};{l.z:0.00}) {(an.enabled ? "lig" : "desl")} {an.cullingMode} {(vis ? "visível" : "fora")}";
        }
        IEnumerator Photo(FBCharacter a, FBCar veh, string n, bool fall)
        {
            var hips = a.an ? a.an.GetBoneTransform(HumanBodyBones.Hips) : null; var hp = hips ? hips.position : a.Pos + Vector3.up;
            Vector3 f, r; if (veh && !fall) { var T = veh.mx ? veh.mx.transform : veh.transform; f = T.forward.Flat().normalized; } else f = a.transform.forward.Flat().normalized; r = Vector3.Cross(Vector3.up, f);
            cam.enabled = true;
            List<Renderer> hid = null;
            if (semVeic && veh) { hid = (veh.mx ? veh.mx.GetComponentsInChildren<Renderer>() : veh.GetComponentsInChildren<Renderer>()).Where(x => x.enabled && !x.transform.IsChildOf(a.transform)).ToList(); foreach (var x in hid) x.enabled = false; }
            Look(hp - r * 3.1f + f * .3f + Vector3.up * .45f, hp + Vector3.up * .05f); yield return Shot(n + "_lado");
            if (fotos > 0) { Look(hp + f * 2.4f - r * 2.1f + Vector3.up * .9f, hp); yield return Shot(n + "_frente"); }
            if (semVeic) { Look(hp - f * 3.4f - r * .6f + Vector3.up * 1.3f, hp + Vector3.up * .2f); yield return Shot(n + "_tras"); }
            if (fotos > 2) { Look(hp + r * 3.1f + f * .3f + Vector3.up * .45f, hp + Vector3.up * .05f); yield return Shot(n + "_direita"); }   // r48: perna D do piloto (do outro lado da moto)
            if (hid != null) foreach (var x in hid) if (x) x.enabled = true;
            cam.enabled = false;
        }

        // ---- r47: EMOTES. Pedido de 30/10: "pés, tornozelos, pernas, retarget, foot IK e contato com o chão, root motion e transições durante
        // os emotes". A sonda mede a forma do corpo (esticado, joelho de lado/ao contrário, músculo fora do limite, tranco); aqui se mede o
        // CONTATO: altura de cada pé acima do piso descontada a do próprio pé parado em pé (calcanhar = osso do pé, ponta = dedos), pé
        // flutuando (os dois acima de 5 cm por mais de 0,35 s), abaixo do piso, deslize do pé apoiado (< 2 cm do piso) e o quadril se afastando
        // da raiz (root motion que não acompanha: a dança anda e volta de uma vez ao repetir). Uma linha "E ..." por emote e a tabela no fim.
        // r47d: régua independente dos ossos — vértice mais baixo da malha (BakeMesh) contra o chão. A régua por osso compara cada pé com o
        // repouso DELE, e a pose de espera feminina tem um calcanhar levantado (referência alta para aquele pé)
        static readonly List<Vector3> meshV = new(); static Mesh meshBake;
        static float MeshMinY(SkinnedMeshRenderer[] rs)
        {
            if (!meshBake) meshBake = new Mesh(); float m = float.MaxValue;
            foreach (var r in rs) { if (!r || !r.enabled || !r.gameObject.activeInHierarchy) continue; r.BakeMesh(meshBake, true); meshBake.GetVertices(meshV); var t = r.transform; var p = t.position; var q = t.rotation; for (int i = 0; i < meshV.Count; i++) { float y = p.y + (q * meshV[i]).y; if (y < m) m = y; } }
            return m;
        }
        IEnumerator Emotes(List<string> chars, System.Func<string, string> famOf, FBCharacter me, HashSet<string> firstOfFam)
        {
            var arg = Arg("-fbva_emotes"); var list = arg == null || arg == "padrao" ? FBEmotes.DEFAULT.ToList() : arg == "todos" ? FBEmotes.All.Select(e => e.id).ToList() : arg.Split(',').ToList();
            list = list.Where(e => FBRegistry.I.Clip(e)).Distinct().ToList(); Log($"emotes {list.Count}: {string.Join(", ", list)}");
            var tab = new List<(string fam, string e, List<string> fails)>();
            foreach (var id in chars.Append("@jogador"))
            {
                bool pl = id == "@jogador";                                                    // o jogador também (é quem mais dança: lobby, roda de emotes, vitória)
                var a = pl ? me : FBStreetEvents.Spawn(id, spot, 0, "va"); if (!a) { Log($"{id} não nasceu"); continue; }
                // AlwaysAnimate enquanto mede (r47b: com "Cull Update Transforms" a NPC no canto da tela ficava com os ossos congelados e a
                // linha saía toda zerada — sem dança medida); 3 s parado em pé: a medida de repouso dos pés (RestSample) fecha
                a.HoldAnimate(this, true); if (pl) FBMatch.Teleport(me, spot, 0); yield return Wait(3); Log($"{id}: tornozelo em repouso {a.RestFootH:0.000} m"); yield return FBPoseProbe.Calibrate(a); string fam = pl ? "jogador " + famOf(me.skinId) : famOf(id);
                foreach (var e in list)
                {
                    FBMatch.Teleport(a, spot, 0); yield return Wait(.8f);
                    var an = a.an; Transform B(HumanBodyBones b) => an.GetBoneTransform(b);
                    Transform[] ft = { B(HumanBodyBones.LeftFoot), B(HumanBodyBones.RightFoot) }, to = { B(HumanBodyBones.LeftToes), B(HumanBodyBones.RightToes) }; var hips = B(HumanBodyBones.Hips);
                    if (!ft[0] || !ft[1] || !hips) { Log($"{id} sem ossos do pé"); break; }
                    float G() => Physics.Raycast(a.Pos + Vector3.up * 1.5f, Vector3.down, out var gh, 4, 1 << FBWorld.LMap, QueryTriggerInteraction.Ignore) ? gh.point.y : a.Pos.y;
                    // pé parado em pé (pose de espera do NPC, foot IK no chão): altura de repouso do calcanhar e da ponta
                    float g0 = G(); var h0F = new float[2]; var h0T = new float[2]; for (int i = 0; i < 2; i++) { h0F[i] = ft[i].position.y - g0; h0T[i] = to[i] ? to[i].position.y - g0 : h0F[i]; }
                    var hipRest = a.transform.InverseTransformPoint(hips.position).Flat();
                    var smrs = a.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r => r.enabled && r.sharedMesh).ToArray(); float m0 = smrs.Length > 0 ? MeshMinY(smrs) - g0 : 0, meshBelow = 0, meshAir = 0, meshAirMax = 0; string meshW = "-";
                    probe = new FBPoseProbe(a, null, false, null); subj = a; phaseOf = () => $"emote/{a.sys ?? e}";
                    bool ok = a.PlayDance(e); var clip = FBRegistry.I.Clip(e); float dur = Mathf.Clamp(clip.length, 3, 9), t0 = Time.time, floatRun = 0, floatMax = 0, below = 0, slide = 0, slideD = 0, hipDrift = 0, liftMax = 0, liftMin = 0;
                    string floatW = "-", belowW = "-", slideW = "-", hipW = "-"; int frames = 0, floatF = 0; var last = new Vector3[2]; var r0 = a.transform.position; bool has = false;
                    while (ok && Time.time - t0 < dur)
                    {
                        yield return null; float dt = Time.deltaTime, t = Time.time - t0; if (dt <= 0) continue;
                        if (a.DanW < .9f) { has = false; continue; }                                  // ainda misturando com a pose de antes
                        frames++; float g = G(); var cl = new float[2]; var p = new Vector3[2];
                        for (int i = 0; i < 2; i++) { float hf = ft[i].position.y - g - h0F[i], ht = to[i] ? to[i].position.y - g - h0T[i] : hf; cl[i] = Mathf.Min(hf, ht); p[i] = (to[i] ? to[i] : ft[i]).position; }
                        float low = Mathf.Min(cl[0], cl[1]); liftMax = Mathf.Max(liftMax, a.GroundLift); liftMin = Mathf.Min(liftMin, a.GroundLift);
                        if (low > .05f) { floatRun += dt; floatF++; if (floatRun > floatMax) { floatMax = floatRun; floatW = $"{low:0.00} m t={t:0.00}"; } } else floatRun = 0;
                        if (-low > below) { below = -low; belowW = $"{(cl[0] < cl[1] ? "pé E" : "pé D")} {-low:0.00} m t={t:0.00}"; }
                        if (has) for (int i = 0; i < 2; i++) if (cl[i] < .02f) { float v = (p[i] - last[i]).Flat().magnitude / dt; slideD += (p[i] - last[i]).Flat().magnitude; if (v > slide) { slide = v; slideW = $"{(i == 0 ? "pé E" : "pé D")} {v:0.00} m/s t={t:0.00}"; } }
                        float hd = (a.transform.InverseTransformPoint(hips.position).Flat() - hipRest).magnitude; if (hd > hipDrift) { hipDrift = hd; hipW = $"{hd:0.00} m t={t:0.00}"; }
                        if (smrs.Length > 0) { float mh = MeshMinY(smrs) - g; if (m0 - mh > meshBelow) { meshBelow = m0 - mh; meshW = $"t={t:0.00}"; } if (mh - m0 > .05f) { meshAir += dt; meshAirMax = Mathf.Max(meshAirMax, meshAir); } else meshAir = 0; }
                        last = p; has = true;
                    }
                    var pr = probe; probe = null; subj = null; var fails = ok ? pr.Fails() : new List<string> { "não tocou" };
                    if (floatMax > .35f) fails.Add("pé flutuando"); if (below > .03f && !fails.Contains("abaixo do chão")) fails.Add("abaixo do chão"); if (slide > .9f) fails.Add("pé deslizando"); if (hipDrift > .6f) fails.Add("quadril longe da raiz"); if (meshBelow > .03f) fails.Add("malha abaixo do chão"); if (meshAirMax > .35f) fails.Add("malha no ar");
                    float rootMove = (a.transform.position - r0).Flat().magnitude; tab.Add((fam, e, fails));
                    Log($"E {e} · {(pl ? me.skinId : id)} ({fam}) · {(fails.Count == 0 ? "OK" : "FALHAS: " + string.Join(", ", fails))} · {frames} quadros · flutuando máx {floatMax:0.00} s ({floatW}) {floatF * 100f / Mathf.Max(1, frames):0}% · abaixo {below:0.00} ({belowW}) · pés em repouso E {h0F[0]:0.000} D {h0F[1]:0.000} · malha: repouso {m0:+0.00;-0.00} abaixo máx {meshBelow:0.00} ({meshW}) no ar máx {meshAirMax:0.00} s · deslize máx {slideW} total {slideD:0.00} m · quadril×raiz {hipW} · raiz andou {rootMove:0.00} m · corpo subiu máx {liftMax:0.00} desceu máx {-liftMin:0.00} (tornozelo em repouso {a.RestFootH:0.000}) · {pr.Report()}");
                    if (fotos > 0 && (pl || firstOfFam.Contains(id)) && fails.Count > 0) { a.PlayDance(e); for (float w = 0; w < dur * .45f; w += Time.deltaTime) yield return null; yield return Photo(a, null, $"emote_{e}_{id}", false); }
                    pr.Dispose(); a.StopDance(true); yield return Wait(.6f);
                }
                a.HoldAnimate(this, false); if (!pl) FBStreetEvents.Despawn(a); yield return Wait(.4f);
            }
            foreach (var g in tab.GroupBy(x => x.e)) Log($"TABELA {g.Key}: {g.Count(x => x.fails.Count == 0)}/{g.Count()} sem defeito · {string.Join(", ", g.SelectMany(x => x.fails).GroupBy(s => s).OrderByDescending(k => k.Count()).Select(k => $"{k.Key} {k.Count()}"))}");
            Log($"RESUMO: {tab.Count(x => x.fails.Count == 0)}/{tab.Count} emotes sem defeito grave");
        }

        IEnumerator Start()
        {
            dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste", "veicanim"); System.IO.Directory.CreateDirectory(dir);
            foreach (var old in System.IO.Directory.GetFiles(dir, "*.png")) System.IO.File.Delete(old);
            if (int.TryParse(Arg("-fbva_fotos"), out var fo)) fotos = fo;
            semVeic = Arg("-fbva_semveiculo") == "1"; if (int.TryParse(Arg("-fbva_nfotos"), out var nf)) nFotos = Mathf.Clamp(nf, 2, 30);
            yield return Wait(3); var mm = run.Split(':'); GameData.Load(); FBSession.Load(); var keep = (FBSession.map, FBSession.mode, FBSession.view);
            FBSession.map = mm[0]; FBSession.mode = mm.Length > 1 ? mm[1] : "role"; FBSession.online = false; FBSession.view = "tps"; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_" + mm[0]); while (op != null && !op.isDone) yield return null; yield return Wait(8);
            var me = FBMatch.Player; if (!me) { Log("sem jogador"); yield break; } FBMatch.God = true;
            if (FBGarage.Open_) { FBGarage.AutoPick(); yield return Wait(1.5f); }
            if (FBVehicles.CarOf(me)) { FBDrive.Exit(me, true); yield return Wait(1); }
            cam = new GameObject("VeicAnimCam").AddComponent<Camera>(); cam.depth = 100; cam.cullingMask = Camera.main ? Camera.main.cullingMask : ~0; cam.enabled = false; cam.nearClipPlane = .05f;
            // r49i: -fbva_chao mapa = no chão do mapa de verdade (vaga de rua livre mais perto do jogador: meio-fio, calçada, luz do rolê) —
            //  o pedido de 31/10 veio do jogo, não da pista; sem a opção, a pista plana de sempre
            bool chaoMapa = Arg("-fbva_chao") == "mapa";
            if (chaoMapa)
            {
                if (!FBRoads.Ready) FBRoads.Load(FBSession.map);
                var sp = FBRoads.Ready ? FBRoads.spots.Where(x => !FBVehicles.list.Any(c => c && (c.transform.position - x.p).sqrMagnitude < 36)).OrderBy(x => (x.p - me.Pos).sqrMagnitude).FirstOrDefault() : null;
                spot = sp != null ? sp.p : me.Pos + me.transform.forward * 5; Log($"chão do mapa: {(sp != null ? "vaga " + sp.kind : "frente do jogador")} a {(spot - me.Pos).magnitude:0} m do jogador");
                FBMatch.Teleport(me, spot + new Vector3(-6, 0, -4), 0); yield return Wait(1);
            }
            else {
            // pista plana isolada no alto (como o carrolab): nada de trânsito, rampa ou poste no caminho
            var p0 = me.Pos + Vector3.up * 300; var pad = new GameObject("FB_VeicAnimPad").AddComponent<BoxCollider>(); pad.gameObject.layer = FBWorld.LMap; pad.size = new Vector3(600, 2, 600); pad.transform.position = p0 - Vector3.up;
            var padVis = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(padVis.GetComponent<Collider>()); padVis.transform.localScale = new Vector3(600, .02f, 600); padVis.transform.position = p0 - Vector3.up * .01f;
            { var pm = new Material(FBShaders.Lit); pm.SetColor("_BaseColor", new Color(.46f, .47f, .45f)); padVis.GetComponent<Renderer>().sharedMaterial = pm; }   // r49f: o material padrão do primitivo não vai no build (chão rosa nas fotos)
            Physics.SyncTransforms(); spot = p0 + new Vector3(30, 0, 30);
            FBMatch.Teleport(me, spot + new Vector3(-14, 0, -10), 0); yield return Wait(1);
            }

            // ---- elenco: 2 de cada família (ordem fixa por id) ou -fbva_chars
            bool Has(string id) => FBRegistry.I && FBRegistry.I.HasChar(id);
            List<string> Fam(System.Func<SkinDef, bool> f, int n) => GameData.Skins.Where(s => Has(s.id) && !FBNpcAnims.IsPolice(s.id) && f(s)).Select(s => s.id).OrderBy(x => x).Take(n).ToList();
            var fam = new Dictionary<string, string>(); void AddF(string f, IEnumerable<string> ids) { foreach (var id in ids) fam[id] = f; }
            AddF("regional H", Fam(s => s.id.StartsWith("r_") && !s.role && s.sex != "f", 2)); AddF("regional M", Fam(s => s.id.StartsWith("r_") && !s.role && s.sex == "f", 2));
            AddF("NPC H", Fam(s => s.role && s.sex != "f", 2)); AddF("NPC M", Fam(s => s.role && s.sex == "f", 2));   // r52: NPCs próprios no lugar dos do 171 (que saíram)
            var chars = Arg("-fbva_chars")?.Split(',').Where(Has).ToList() ?? fam.Keys.ToList();
            string FamOf(string id) => fam.TryGetValue(id, out var f) ? f : id.StartsWith("r_") ? "regional" : "outro";
            var firstOfFam = chars.GroupBy(FamOf).Select(g => g.First()).ToHashSet();
            // ---- veículos: carros de tamanhos diferentes e motos MX (pequena, trail, rua)
            bool IsCar(string v) => FBRegistry.I.Vehicle(v) && GameData.Vehicles.ContainsKey(v) && Json.S(GameData.Vehicles[v], "kind") != "moto";
            var carPref = new[] { "Fusca", "GolG5", "S10_2017", "Hilux2023Conquest", "CivicG10", "SaveiroG4", "ViaturaPMAzul" };
            var ca = Arg("-fbva_carros"); var cars = ca == "nenhum" ? new List<string>() : ca?.Split(',').Where(IsCar).ToList() ?? carPref.Where(IsCar).Take(5).ToList();   // r48: "nenhum" = só motos
            if (cars.Count == 0 && ca != "nenhum") cars = GameData.Vehicles.Keys.Where(IsCar).Take(4).ToList();
            var motos = (Arg("-fbva_motos")?.Split(',') ?? new[] { "pop110", "titan160", "xre300", "xj6", "mt09" }).Where(FBMxBike.Has).Select(v => "mx_" + v).ToList();
            var quedas = (Arg("-fbva_quedas")?.Split(',') ?? new[] { "pop110", "xj6" }).Where(FBMxBike.Has).Select(v => "mx_" + v).ToList();
            Log($"elenco {chars.Count}: {string.Join(", ", chars.Select(c => c + " (" + FamOf(c) + ")"))} · carros {string.Join(", ", cars)} · motos {string.Join(", ", motos)} · quedas em {string.Join(", ", quedas)} · fotos {fotos}");

            if (Arg("-fbva_emotes") != null || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbva_emotes") >= 0)
            { yield return Emotes(chars, FamOf, me, firstOfFam); (FBSession.map, FBSession.mode, FBSession.view) = keep; FBSession.Save(); Log("fim"); yield return Wait(1); Application.Quit(); yield break; }
            // ---- NPCs
            foreach (var id in chars)
            {
                string fm = FamOf(id); bool shots = fotos >= 2 || (fotos == 1 && firstOfFam.Contains(id));
                foreach (var vid in cars.Concat(motos))
                {
                    var vc = Put(vid, spot, 0); if (!vc) { Log($"{vid} não nasceu"); continue; } yield return Wait(1.2f); bool moto = vc.kind == "moto" || vc.mx;
                    if (vc.mx && id == chars[0]) Log($"  {vid} estacionada: {vc.mx.ParkDiag()}");                 // r49i: nariz/rodas da moto parada
                    var a = FBStreetEvents.Spawn(id, FBDrive.DoorOut(vc, 1), vc.transform.eulerAngles.y + 90, "va"); if (!a) { Log($"{id} não nasceu"); Kill(vc); continue; }
                    yield return Wait(.8f); string hd = "pronto " + HipDiag(a); yield return FBPoseProbe.Calibrate(a); hd += " · calibrado " + HipDiag(a); if (vid == cars.FirstOrDefault()) Log($"calibração {id}: {FBPoseProbe.CalOf(a)?.diag}");
                    FBMatch.Teleport(a, FBDrive.DoorOut(vc, a.ScaleK), vc.transform.eulerAngles.y + 90); hd += " · teleporte " + HipDiag(a); yield return Wait(.6f); hd += " · 0,6 s " + HipDiag(a);
                    yield return SeeIt(a); hd += " · na câmera " + HipDiag(a);
                    FBDrive.ApplyRemote(a, State(vc), 0, true); hd += " · ApplyRemote " + HipDiag(a); if (moto) Log($"  quadril antes de subir: {hd}"); var dp = a.GetComponent<FBDriverPose>(); float len = 2.5f;
                    yield return Measure(fm, moto ? "sobe_moto" : "entra_carro", a, vc, false, shots,
                        () => { bool ok; if (moto) { FBDrive.StartMount(a, vc); ok = dp && dp.trans == "mount"; } else ok = FBDrive.StartCarIn(a, vc); if (dp) len = dp.transLen; return ok; },
                        () => dp == null || dp.trans == null, () => len, .8f);
                    if (moto) { Log($"  sentado {id}: {SeatDiag(a, vc)}"); yield return Maos(a, vc, $"mao_{id}_{vid}"); } else Log($"  sentado {id}: {CarSeatDiag(a, vc)} · banco desceu {a.seatDrop * 100:0} cm (alvo {a.seatDropT * 100:0}) · cabeça abaixou entrando até {FBDrive.DuckMax * 100:0} cm em {FBDrive.Ducks} quadros (topo ainda acima do limite até {FBDrive.DuckLeft * 100:0} cm)");
                    FBDrive.DuckMax = 0; FBDrive.Ducks = 0; FBDrive.DuckLeft = 0;
                    bool outDone = false;
                    yield return Measure(fm, moto ? "desce_moto" : "sai_carro", a, vc, false, shots,
                        () => { bool ex = moto ? FBDrive.StartDismount(a, vc) : FBDrive.StartCarOut(a, vc, false);
                                if (dp) { len = dp.transLen; dp.onOut = (ch, cr) => { FBDrive.OutPoint(ch, cr, out var at, out var yw); FBDrive.Release(ch); FBMatch.Teleport(ch, at, yw); var c2 = ch.GetComponent<CharacterController>(); if (c2) c2.enabled = true; outDone = true; }; }
                                return ex; },
                        () => outDone || dp == null || (dp.trans == null && !FBVehicles.CarOf(a)), () => len, .6f);
                    FBStreetEvents.Despawn(a); Kill(vc); yield return Wait(.4f);
                }
                // quedas: monta, a moto "anda" e o piloto cai (o mesmo FBDrive.Release + FBFall do trânsito/racha), com a direção forçada
                foreach (var vid in quedas) foreach (var d in new[] { "Fwd", "Left", "Right", "Back" })
                {
                    var vc = Put(vid, spot, 0); if (!vc) continue; yield return Wait(1.2f);
                    var a = FBStreetEvents.Spawn(id, FBDrive.DoorOut(vc, 1), vc.transform.eulerAngles.y + 90, "va"); if (!a) { Kill(vc); continue; }
                    yield return Wait(.8f); yield return FBPoseProbe.Calibrate(a);
                    FBDrive.ApplyRemote(a, State(vc), 0, true); var dp = a.GetComponent<FBDriverPose>(); FBDrive.StartMount(a, vc);
                    for (float w = 0; w < 5 && dp && dp.trans != null; w += Time.deltaTime) yield return null; yield return Wait(.8f);
                    var T = vc.mx ? vc.mx.transform : vc.transform; var vel = Vector3.ProjectOnPlane(T.forward, Vector3.up).normalized * (d == "Back" ? 12 : 35) / 3.6f * 1.17f;   // r49i: raiz nivelada tem arfagem
                    yield return Measure(fm, "queda_" + d, a, vc, true, shots,
                        () => { FBFall.Grab(a, vc); FBDrive.Release(a); if (!a.gameObject.activeSelf) a.gameObject.SetActive(true); return FBFall.Begin(a, FBFall.FallPoint(vc, d), T.eulerAngles.y, vel, d); },
                        () => !FBFall.Busy(a), () => 5f, 1.0f);
                    FBStreetEvents.Despawn(a); Kill(vc); yield return Wait(.4f);
                }
            }

            // ---- jogador: entra/sai do carro, sobe/desce da moto e a queda de frente pelo FBDrive.MxCrash de verdade
            {
                string fm = "jogador " + FamOf(me.skinId); yield return FBPoseProbe.Calibrate(me); Log($"calibração jogador {me.skinId}: {FBPoseProbe.CalOf(me)?.diag} · arma {(me.weaponGO && me.weaponGO.activeSelf ? me.weaponGO.name : "nenhuma")}");
                foreach (var vid in cars.Take(2).Concat(motos.Take(2)))
                {
                    var vc = Put(vid, spot, 0); if (!vc) continue; yield return Wait(1.2f); bool moto = vc.kind == "moto" || vc.mx;
                    FBMatch.Teleport(me, FBDrive.DoorOut(vc, me.ScaleK), vc.transform.eulerAngles.y + 90); yield return Wait(1);
                    FBDriverPose dp = null; float len = 2.5f;
                    yield return Measure(fm, moto ? "sobe_moto" : "entra_carro", me, vc, false, true,
                        () => { bool ok = FBDrive.Enter(me, vc); dp = me.GetComponent<FBDriverPose>(); if (dp) len = Mathf.Max(.5f, dp.transLen); return ok; },
                        () => dp == null || dp.trans == null, () => len, .8f);
                    if (moto) { Log($"  sentado jogador {me.skinId}: {SeatDiag(me, vc)}"); yield return Maos(me, vc, $"mao_jog_{me.skinId}_{vid}"); } else Log($"  sentado jogador {me.skinId}: {CarSeatDiag(me, vc)} · banco desceu {me.seatDrop * 100:0} cm (alvo {me.seatDropT * 100:0}) · cabeça abaixou entrando até {FBDrive.DuckMax * 100:0} cm em {FBDrive.Ducks} quadros (topo ainda acima do limite até {FBDrive.DuckLeft * 100:0} cm)");
                    FBDrive.DuckMax = 0; FBDrive.Ducks = 0; FBDrive.DuckLeft = 0;
                    yield return Measure(fm, moto ? "desce_moto" : "sai_carro", me, vc, false, true,
                        () => { bool ex = FBDrive.Exit(me, false); dp = me.GetComponent<FBDriverPose>(); if (dp) len = Mathf.Max(.5f, dp.transLen); return ex; },
                        () => !FBVehicles.CarOf(me) && (dp == null || dp.trans == null), () => len, .8f);
                    if (FBVehicles.CarOf(me)) FBDrive.Exit(me, true); Kill(vc); yield return Wait(.5f);
                }
                foreach (var vid in quedas.Take(1))
                {
                    var bike = Put(vid, spot, 0); if (!bike) continue; yield return Wait(1.2f);
                    FBMatch.Teleport(me, FBDrive.DoorOut(bike, me.ScaleK), bike.transform.eulerAngles.y + 90); yield return Wait(.8f);
                    if (!FBDrive.Enter(me, bike)) { Log("jogador não montou para a queda"); Kill(bike); continue; }
                    var dp = me.GetComponent<FBDriverPose>(); for (float w = 0; w < 5 && dp && dp.trans != null; w += Time.deltaTime) yield return null; yield return Wait(1);
                    bike.mx.Launch(bike.mx.transform.forward * 40 / 3.6f * FBMxBike.S); yield return Wait(.35f);
                    yield return Measure(fm, "queda_batida", me, bike, true, true, () => { FBDrive.MxCrash(bike, "batida"); return FBFall.Busy(me); }, () => !FBFall.Busy(me), () => 5f, 1.0f);
                    if (FBVehicles.CarOf(me)) FBDrive.Exit(me, true); Kill(bike); yield return Wait(.5f);
                }
            }

            // ---- r49i: pilotar (jogador) — a MESMA sequência com o piloto como até a r49h e com o novo (histerese, clipe mínimo 0,5 s, mistura
            //  0,45 s, alvos do IK 0,16 s, pé ao chão em arco): trocas do clipe base, menor intervalo entre trocas e velocidade dos ossos em
            //  relação à moto (o "tranco" que o usuário via). Sequência: arranca até 18 km/h, segura com o guidão oscilando em volta dos limiares
            //  de curva, freia até parar, anda "a passo" (0 ↔ 3,5 km/h) em volta do limiar de parado, para.
            if (motos.Count > 0 && Arg("-fbva_pilotar") != "0" && !chaoMapa)
                foreach (bool novo in new[] { false, true })
                {
                    FBMxBike.SmoothRider = novo; string nm = novo ? "novo" : "antigo"; var bike = Put(motos[0], spot + new Vector3(0, 0, 40), 0); if (!bike) break; yield return Wait(1.2f);
                    FBMatch.Teleport(me, FBDrive.DoorOut(bike, me.ScaleK), bike.transform.eulerAngles.y + 90); yield return Wait(.8f);
                    if (!FBDrive.Enter(me, bike)) { Log($"pilotar ({nm}): jogador não montou"); Kill(bike); break; }
                    var dp = me.GetComponent<FBDriverPose>(); for (float w = 0; w < 5 && dp && dp.trans != null; w += Time.deltaTime) yield return null; yield return Wait(.6f);
                    var mx = bike.mx; float t0 = Time.time; FBMxBike.ClipSwaps = 0; FBMxBike.ClipGapMin = 9; FBMxBike.PmResets = 0; int sysSw = 0; string lastSys = me.sys; float lastSw = Time.time, sysGap = 9;
                    float VT(float t) => t < 2 ? 9 * t : t < 7 ? 18 : t < 9 ? 0 : t < 12 ? ((int)((t - 9) / .6f) % 2 == 0 ? 3.5f : 0) : 0;
                    FBDrive.LabDrive = () => { float t = Time.time - t0; float thr = Mathf.Clamp((VT(t) - mx.speedKmh) * .25f, -1, 1); if (VT(t) <= 0 && mx.speedKmh < 1) thr = 0;
                                               float st = t >= 2 && t < 7 ? .25f + .2f * Mathf.Sin((t - 2) * 2 * Mathf.PI / 1.3f) : 0; return new Vector2(thr, st); };
                    var an = me.an; var ids = new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Head, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm };
                    var bs = ids.Select(b => an ? an.GetBoneTransform(b) : null).ToArray(); var prev = new Quaternion[bs.Length]; bool init = false; float maxW = 0, sumW = 0, footV = 0; int nW = 0, over = 0; string maxWho = "-"; Vector3 pf = default;
                    var lf = an ? an.GetBoneTransform(HumanBodyBones.LeftFoot) : null; float vMax = 0; int frames = 0;
                    var evF = new List<(float j, string s)>(); var evB = new List<(float j, string s)>(); float degMax = 0; int degOver = 0;
                    string Ctx(float dt) => $"t={Time.time - t0:0.00} s dt={dt * 1000:0} ms sys={me.sys} estado={mx.state} péNoChão={mx.FootDownDbg:0.00} vel={mx.speedKmh:0.0} acel={mx.in_acceleration:0.00} esterço={mx.in_steering:0.00}";
                    while (Time.time - t0 < 13.5f && FBVehicles.CarOf(me) == bike && !mx.crashed)
                    {
                        yield return new WaitForEndOfFrame(); float dt = Mathf.Max(1e-3f, Time.deltaTime); var bt = mx.transform; vMax = Mathf.Max(vMax, mx.speedKmh); frames++;
                        if (me.sys != lastSys) { sysGap = Mathf.Min(sysGap, Time.time - lastSw); lastSw = Time.time; lastSys = me.sys; sysSw++; }
                        for (int i = 0; i < bs.Length; i++)
                        {
                            if (!bs[i]) continue; var q = Quaternion.Inverse(bt.rotation) * bs[i].rotation;
                            if (init) { float dg = Quaternion.Angle(prev[i], q), w = dg / dt; sumW += w; nW++; if (w > 360) over++; if (w > maxW) { maxW = w; maxWho = $"{ids[i]} a {Time.time - t0:0.0} s ({me.sys})"; }
                                        if (dg > 8) { degOver++; evB.Add((dg, $"{ids[i]} {dg:0.0}° num quadro · {Ctx(dt)}")); } degMax = Mathf.Max(degMax, dg); }
                            prev[i] = q;
                        }
                        if (lf) { var lp = bt.InverseTransformPoint(lf.position); if (init) { float jm = (lp - pf).magnitude * FBMxBike.S; footV = Mathf.Max(footV, jm / dt); if (jm > .04f) evF.Add((jm, $"pé E pulou {jm * 100:0} cm · {Ctx(dt)}")); } pf = lp; }
                        init = true;
                    }
                    FBDrive.LabDrive = null;
                    Log($"  pilotar {nm}: {frames} quadros em {Time.time - t0:0.0} s ({frames / Mathf.Max(.1f, Time.time - t0):0} qps) · osso > 8° num quadro: {degOver} (máx {degMax:0}°) · pé E > 4 cm num quadro: {evF.Count} · massa do piloto zerada {FBMxBike.PmResets} vezes");
                    foreach (var e in evF.OrderByDescending(x => x.j).Take(6)) Log($"    {e.s}");
                    foreach (var e in evB.OrderByDescending(x => x.j).Take(6)) Log($"    {e.s}");
                    Log($"PILOTAR {nm} ({motos[0]}, {me.skinId}): {(mx.crashed ? "CAIU · " : "")}vel. máx {vMax:0} km/h · clipe base trocou {FBMxBike.ClipSwaps} vezes (menor intervalo {(FBMxBike.ClipGapMin < 9 ? FBMxBike.ClipGapMin : 0):0.00} s) · sys do personagem {sysSw} trocas (menor {(sysGap < 9 ? sysGap : 0):0.00} s) · ossos em relação à moto: média {(nW > 0 ? sumW / nW : 0):0} °/s, máx {maxW:0} °/s em {maxWho}, quadros×osso acima de 360 °/s: {over} · pé E até {footV:0.0} m/s");
                    if (FBVehicles.CarOf(me)) FBDrive.Exit(me, true); Kill(bike); yield return Wait(.6f);
                }
            FBMxBike.SmoothRider = true;

            // ---- tabela: falhas por família × transição
            foreach (var g in table.GroupBy(x => (x.fam, x.tr)).OrderBy(g => g.Key.fam).ThenBy(g => g.Key.tr))
            {
                int n = g.Count(), bad = g.Count(x => x.fails.Count > 0); var kinds = g.SelectMany(x => x.fails).GroupBy(s => s).OrderByDescending(k => k.Count()).Select(k => $"{k.Key} {k.Count()}");
                Log($"TABELA {g.Key.fam} · {g.Key.tr}: {n - bad}/{n} sem defeito · {string.Join(", ", kinds)}");
            }
            Log($"RESUMO: {table.Count(x => x.fails.Count == 0)}/{table.Count} transições sem defeito grave");
            Log($"r48: perna por cima da moto {FBLegClear.Lifts} giros (máx {FBLegClear.LiftMax:0.0}°, {FBLegClear.Built} motos com colisor de malha)");
            Log($"contadores r46: pés subidos {FBDrive.FeetLifts} (máx {FBDrive.FeetLiftMax:0.00} m) · pé por IK {FBDrive.FootFixes} (máx {FBDrive.FootFixMax:0.00} m) · ponta do pé no chão (MX parada) {FBMxBike.ToeFixes} (máx {FBMxBike.ToeFixMax:0.00} m) · torção IK {FBIK.Twists} (máx {FBIK.TwistMax:0}°) · misturas de pose {FBPoseBlend.Blends} · quedas {FBFall.Started}/{FBFall.Finished} (de costas {FBFall.GotUpBack}, de bruços {FBFall.GotUpFront}) · anatomia {FBRagdoll.AnatomyFixes} · eixo anatômico no ragdoll {FBRagdoll.AnatAxes} · junta reencaixada {FBRagdoll.ProjFixes} (máx {FBRagdoll.ProjMax:0.00} m) · no chão até levantar méd {(FBFall.RestN > 0 ? FBFall.RestSum / FBFall.RestN : 0):0.00} s ({FBFall.RestTimeouts} de {FBFall.RestN} pelo limite de 5 s) · apoio no levantar {FBFall.Lifts} (máx {FBFall.LiftMax:0.00} m; depois da mistura {FBFall.PostFixes} quadros, máx {FBFall.PostMax:0.00} m) · raiz mexida por fora no levantar {FBFall.OffDrift:0.00} m · polo limitado {FBIK.PoleClamps} (lado mantido {FBIK.SideHolds}) · IK sobre pose velha {FBIK.StaleSkips} · subida do pé pulada com Animator parado {FBCharacter.CulledSkips} · torções >90° {FBIK.BigTwists} [{string.Join(", ", FBIK.BigBy.OrderByDescending(kv => kv.Value).Take(8).Select(kv => kv.Key + " " + kv.Value))}]");
            (FBSession.map, FBSession.mode, FBSession.view) = keep; FBSession.Save(); Log("fim"); yield return Wait(1); Application.Quit();
        }
    }
}
