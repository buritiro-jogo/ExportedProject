// Rig dentro dos veículos (FBDriverPose dono único dos ossos): para alguns personagens (masc/fem, rigs IE / APK / NPC CC) × veículos
// (carro do REB com rig de mãos/pés, carro do IE, moto), entra, dirige 2,4 s com FBDrive.Simulate (acelera, esterça de um lado ao outro, freia)
// e mede, a 60 q/s fixos, depois da entrada (0,35 s):
//  - mãos × alvo (aro do volante girando / guidão) e pés × pedal/pedaleira;
//  - tremor: 2ª diferença por quadro das posições de mãos, pés, cabeça e quadril no espaço do veículo (corpo não pode "pular" no banco);
//  - penetração: cabeça dentro do colisor do próprio veículo.
// Falha fora dos limites; loga cada combinação ([FreeBala][veiculo]) e grava um print de lado em unity/logs/print_veiculo_<pers>_<veic>.png.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FreeBala.Tests
{
    public class FBVehicleRigTests
    {
        const float HandMax = .05f, FootMax = .08f, JitterMax = .012f, PenMax = .1f;   // m, m, m/q², fração de quadros

        static float P(List<float> l, float q) { if (l.Count == 0) return 0; var c = l.OrderBy(x => x).ToList(); return c[Mathf.Clamp(Mathf.RoundToInt(q * (c.Count - 1)), 0, c.Count - 1)]; }

        static List<string> Skins()
        {
            var ids = FBRegistry.I.charIds; var pick = new List<string>();
            foreach (var pre in new[] { "ie_", "c_", "n_", "p_" })
            {
                foreach (var sex in new[] { "m", "f" })
                {
                    var id = ids.FirstOrDefault(i => i.StartsWith(pre) && !pick.Contains(i) && (GameData.Skins.Find(s => s.id == i)?.sex ?? "m") == sex);
                    if (id != null) pick.Add(id);
                }
            }
            return pick.Take(6).ToList();
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Motorista_CarrosEMotos()
        {
            if (!FBRegistry.I || FBRegistry.I.charIds.Count == 0) Assert.Ignore("sem personagens importados");
            FBSession.Load(); FBSession.map = "praca_aguas"; FBSession.mode = "role"; FBSession.online = false; FBSession.view = "tps"; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_praca_aguas"); while (!op.isDone) yield return null;
            for (float t = 0; t < 3; t += Time.unscaledDeltaTime) yield return null;
            Assert.IsNotNull(FBMatch.I, "partida não montou"); var me = FBMatch.Player; Assert.IsNotNull(me, "sem jogador");
            Time.captureFramerate = 60;
            // um veículo de cada tipo, num ponto livre perto do jogador (vira o mesmo lugar a cada combinação)
            var spot = me.Pos + me.Fwd * 12; spot.y = FBWorld.GroundY(spot.x, spot.y + 3, spot.z, 2, 8) ?? spot.y;
            var kinds = new List<(string tag, FBCar car)>();
            FBCar Try(string id) { var c = FBVehicles.Spawn(id, spot + Vector3.right * 40, me.Yaw); return c; }
            foreach (var id in GameData.Vehicles.Keys) { var c = Try(id); if (c && c.rigHands != null && c.kind == "car") { kinds.Add(("reb_" + id, c)); break; } if (c) Object.Destroy(c.gameObject); }
            if (GameData.IE != null)
            {
                foreach (var id in GameData.IE.Keys) { var c = Try(id); if (c && c.kind == "car") { kinds.Add(("ie_" + id, c)); break; } if (c) Object.Destroy(c.gameObject); }
                foreach (var id in GameData.IE.Keys) { var c = Try(id); if (c && c.kind == "moto") { kinds.Add(("moto_" + id, c)); break; } if (c) Object.Destroy(c.gameObject); }
            }
            if (!kinds.Exists(k => k.car.kind == "moto")) foreach (var id in GameData.Vehicles.Keys) { var c = Try(id); if (c && c.kind == "moto") { kinds.Add(("moto_" + id, c)); break; } if (c) Object.Destroy(c.gameObject); }
            foreach (var k in kinds) k.car.gameObject.SetActive(false);
            Assert.Greater(kinds.Count, 0, "nenhum veículo montou");
            var bad = new List<string>(); var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../logs")); System.IO.Directory.CreateDirectory(dir);
            var cam = Camera.main;
            foreach (var skin in Skins())
            {
                foreach (var (tag, car) in kinds)
                {
                    car.gameObject.SetActive(true); car.transform.SetPositionAndRotation(spot + Vector3.up * .3f, Quaternion.Euler(0, me.Yaw, 0)); yield return null;
                    var p = FBMatch.I.Spawn(skin, null, 0, spot + Vector3.left * 4, false); var an = p.an; if (an) an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    yield return null; yield return null;
                    if (!FBDrive.Enter(p, car)) { bad.Add($"{skin} × {tag}: não entrou"); Object.Destroy(p.gameObject); car.gameObject.SetActive(false); continue; }
                    var dp = p.GetComponent<FBDriverPose>();
                    Transform B(HumanBodyBones b) => an.GetBoneTransform(b);
                    var bones = new[] { B(HumanBodyBones.LeftHand), B(HumanBodyBones.RightHand), B(HumanBodyBones.LeftFoot), B(HumanBodyBones.RightFoot), B(HumanBodyBones.Head), B(HumanBodyBones.Hips) };
                    var hand = new List<float>(); var foot = new List<float>(); var jit = new List<float>(); int pen = 0, n = 0; var h1 = new Vector3[bones.Length]; var h2 = new Vector3[bones.Length]; int hn = 0;
                    for (int f = 0; f < 165; f++)
                    {
                        float t = f / 60f, thr = t < 1.4f ? 1 : -1, st = Mathf.Sin(t * 3.2f);
                        FBDrive.Simulate(car, p, thr, st, false, 1 / 60f); yield return null;
                        if (t < .35f || !dp) continue; var L = dp.Last; n++;
                        if (L.hands) { hand.Add((bones[0].position - L.hL).magnitude); hand.Add((bones[1].position - L.hR).magnitude); }
                        if (L.feet) { foot.Add((bones[2].position - L.fL).magnitude); foot.Add((bones[3].position - L.fR).magnitude); }
                        for (int i = 0; i < bones.Length; i++)
                        {
                            if (!bones[i]) continue; var lp = car.transform.InverseTransformPoint(bones[i].position);
                            if (hn >= 2) jit.Add((lp - 2 * h1[i] + h2[i]).magnitude); h2[i] = h1[i]; h1[i] = lp;
                        }
                        hn++;
                        if (bones[4] && Physics.OverlapSphere(bones[4].position, .07f, 1 << FBWorld.LVehicle, QueryTriggerInteraction.Ignore).Any(c => c.GetComponentInParent<FBCar>() == car)) pen++;
                    }
                    string rep = $"{skin} × {tag}: mãos p95 {P(hand, .95f) * 100:0.0} cm (n {hand.Count}) · pés p95 {P(foot, .95f) * 100:0.0} cm (n {foot.Count}) · tremor p95 {P(jit, .95f) * 1000:0.0} mm/q² máx {P(jit, 1) * 1000:0.0} · cabeça no teto {pen}/{n}";
                    Debug.Log("[FreeBala][veiculo] " + rep);
                    if (P(hand, .95f) > HandMax) bad.Add(rep + " → mãos fora do alvo"); if (P(foot, .95f) > FootMax) bad.Add(rep + " → pés fora do pedal");
                    if (P(jit, .95f) > JitterMax) bad.Add(rep + " → tremor"); if (n > 0 && pen > PenMax * n) bad.Add(rep + " → cabeça atravessa o teto");
                    if (cam) { var c = car.transform; cam.transform.position = c.position + c.right * -3.2f + c.up * 1.3f; cam.transform.LookAt(c.position + c.up * .9f); }
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"print_veiculo_{skin}_{tag}.png")); yield return null; yield return null;
                    FBDrive.Exit(p, true); yield return null;
                    if (FBVehicles.Driving(p)) bad.Add($"{skin} × {tag}: não saiu");
                    Object.Destroy(p.gameObject); car.v = 0; car.gameObject.SetActive(false); yield return null;
                }
            }
            Time.captureFramerate = 0;
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }
    }
}
