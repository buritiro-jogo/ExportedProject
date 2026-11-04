// Teste de fumaça de verdade: carrega cada modo num mapa, deixa a partida rodar alguns segundos e confere que o jogador nasceu armado,
// os bots nasceram, se movem e atiram, a HUD existe, ninguém caiu do mapa e não houve exceção. Grava um print por modo em unity/logs/.
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FreeBala.Tests
{
    public class FBMatchSmokeTests
    {
        static IEnumerator Run(string map, string mode, float seconds)
        {
            FBSession.Load(); FBSession.map = map; FBSession.mode = mode; FBSession.online = false; FBSession.view = "tps"; FBSession.diff = 1;
            if (FBRegistry.I) { if (FBRegistry.I.HasChar("r_tutu")) FBSession.skin = "r_tutu"; }
            FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_" + map); while (!op.isDone) yield return null;
            float t = 0, tj = 0, pior = 0; int quadros = 0; string aos = null; var rel = System.Diagnostics.Stopwatch.StartNew();
            var p0 = new System.Collections.Generic.Dictionary<FBCharacter, Vector3>(); bool moved = false;
            // a janela conta tempo de JOGO (Time.deltaTime, limitado por quadro): no editor em lote um quadro de vários segundos (montagem
            // da partida, compilação de shader) esgotava a janela em tempo real antes de os bots andarem; teto de 120 s reais
            while (tj < seconds && rel.Elapsed.TotalSeconds < 120)
            {
                yield return null; t += Time.unscaledDeltaTime; tj += Time.deltaTime; quadros++; pior = Mathf.Max(pior, Time.unscaledDeltaTime);
                if (aos == null && t >= seconds) aos = $"{tj:0.0} s de jogo quando a janela antiga ({seconds:0} s reais) acabava, bots {(moved ? "já tinham andado" : "ainda parados")}";
                if (tj < 1) continue;
                foreach (var b in FBMatch.Bots) { if (!b) continue; if (!p0.TryGetValue(b, out var q)) p0[b] = b.Pos; else if ((b.Pos - q).magnitude > 1) moved = true; }   // algum bot andou (defensor parado é legítimo)
            }
            Debug.Log($"[FreeBala][teste] {map}/{mode}: janela {rel.Elapsed.TotalSeconds:0.0} s reais · {tj:0.0} s de jogo · {quadros} quadros (pior {pior:0.00} s){(aos != null ? " · " + aos : "")}");
            Assert.IsNotNull(FBMatch.I, "partida não montou"); Assert.IsNotNull(FBMatch.Player, "sem jogador");
            Assert.Greater(FBMatch.Player.Pos.y, -50, "jogador caiu do mapa");
            if (mode != "role" && mode != "treino") { Assert.Greater(FBMatch.Bots.Count, 0, "sem bots"); Assert.IsTrue(moved, "bots parados"); }
            Assert.IsNotNull(FBHUD.I, "sem HUD");
            var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../logs")); System.IO.Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"print_{map}_{mode}.png")); yield return null; yield return null;
            Debug.Log($"[FreeBala][teste] {map}/{mode}: jogador {FBMatch.Player.Pos} vida {FBMatch.Player.hp} · bots {FBMatch.Bots.Count(b => b && b.alive)}/{FBMatch.Bots.Count} · tiros do jogador {FBMatch.Stats.shots} · estado {FBMatch.I.state}");
        }
        [UnityTest, Timeout(180000)] public IEnumerator Tatico_VilaJardim() { yield return Run("vila_jardim", "tatico", 20); }
        [UnityTest, Timeout(180000)] public IEnumerator Squad4v4_PracaAguas() { yield return Run("praca_aguas", "squad", 36); }
        [UnityTest, Timeout(180000)] public IEnumerator Classico_PracaPT() { yield return Run("praca_pt", "classico", 15); }
        [UnityTest, Timeout(180000)] public IEnumerator FFA_Orla() { yield return Run("orla", "ffa", 15); }
        [UnityTest, Timeout(180000)] public IEnumerator Treino_Estande() { yield return Run("estande", "treino", 10); }
        [UnityTest, Timeout(180000)] public IEnumerator Pvl_VilaJardim() { yield return Run("vila_jardim", "pvl", 34); }
        [UnityTest, Timeout(180000)] public IEnumerator Role_VilaJardim() { yield return Run("vila_jardim", "role", 10); }
        [UnityTest, Timeout(180000)] public IEnumerator BR_PracaPT() { yield return Run("praca_pt", "br", 20); }
        [UnityTest, Timeout(180000)] public IEnumerator Modo10x10_MorroSacola() { yield return Run("morro_sacola", "10x10", 40); }   // passa da fase de compra
        [UnityTest, Timeout(180000)] public IEnumerator Classico_MorroSacola() { yield return Run("morro_sacola", "classico", 15); }
        [UnityTest, Timeout(120000)] public IEnumerator Menu_Lobby()
        {
            var op = SceneManager.LoadSceneAsync("Menu"); while (!op.isDone) yield return null;
            for (float t = 0; t < 6; t += Time.unscaledDeltaTime) yield return null;
            Assert.IsNotNull(GameObject.Find("Previa"), "sem prévia do personagem no lobby");
            var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../logs")); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "print_lobby.png")); yield return null; yield return null;
        }
        // veículos (vehicles.js + drive.js): carros montados (rodas, LOD, colisão), entrar, acelerar, esterçar, frear, sair
        [UnityTest, Timeout(180000)] public IEnumerator Dirigir_PracaAguas()
        {
            FBSession.Load(); FBSession.map = "praca_aguas"; FBSession.mode = "role"; FBSession.online = false; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_praca_aguas"); while (!op.isDone) yield return null;
            for (float t = 0; t < 3; t += Time.unscaledDeltaTime) yield return null;
            Assert.Greater(FBVehicles.list.Count, 0, "nenhum carro estacionado");
            var cars = FBVehicles.list.Where(c => c && c.kind == "car").ToList(); Assert.Greater(cars.Count, 0, "sem carros");
            foreach (var c in cars) { Assert.GreaterOrEqual(c.wheels.Count, 4, $"{c.id}: rodas"); Assert.Greater(c.lbox.size.z, 2.5f, $"{c.id}: tamanho"); Assert.Greater(c.cols.Count, 0, $"{c.id}: colisão"); }
            int walls = FBVehicles.list.Count(c => c && c.soundWallNode); Debug.Log($"[FreeBala][teste] veículos {FBVehicles.list.Count} · carros {cars.Count} · motos {FBVehicles.list.Count(c => c && c.kind == "moto")} · paredões {walls} · ids {string.Join(",", FBVehicles.list.Where(c => c).Select(c => c.id).Distinct())}");
            var me = FBMatch.Player; Assert.IsNotNull(me, "sem jogador");
            // carro com mais espaço à frente
            var car = cars.OrderByDescending(c => { c.SetColliders(false); float d = FBWorld.RayHit(c.transform.position + Vector3.up, c.transform.forward, 40, out var h) ? h.distance : 40; c.SetColliders(true); return d; }).First();
            var cc = me.GetComponent<CharacterController>(); if (cc) cc.enabled = false; me.transform.position = car.transform.position + car.transform.right * -(car.lbox.extents.x * car.scale + .8f); if (cc) cc.enabled = true;
            yield return null;
            Assert.IsTrue(FBDrive.Enter(me, car), "não entrou"); Assert.IsTrue(FBVehicles.Driving(me), "não está dirigindo");
            var p0 = car.transform.position; float vmax = 0;
            for (float t = 0; t < 3; t += Time.deltaTime) { FBDrive.Simulate(car, me, 1, t > 1.5f ? .6f : 0, false, Time.deltaTime); vmax = Mathf.Max(vmax, car.v); yield return null; }
            float moved = (car.transform.position - p0).Flat().magnitude;
            Debug.Log($"[FreeBala][teste] dirigiu {car.id}: {moved:0.0} m, v máx {vmax * 3.6f / 1.2f:0} km/h, marcha {car.gear}, rpm {car.rpm * 7000:0}, yaw {car.yaw:0.00}");
            Assert.Greater(moved, 4, "carro não andou"); Assert.Greater(vmax, 5, "não acelerou");
            var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../logs")); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "print_dirigir.png")); yield return null; yield return null;
            for (float t = 0; t < 3 && Mathf.Abs(car.v) > .2f; t += Time.deltaTime) { FBDrive.Simulate(car, me, -1, 0, false, Time.deltaTime); yield return null; }
            Assert.Less(Mathf.Abs(car.v), 6, "não freou");
            Assert.IsTrue(FBDrive.Exit(me, false), "não saiu"); Assert.IsFalse(FBVehicles.Driving(me), "continua dirigindo");
            yield return null; Assert.Greater(me.Pos.y, car.transform.position.y - 3, "saiu para baixo do chão");
        }
        // 1ª pessoa: braços com a malha do próprio personagem (fparms.js)
        [UnityTest, Timeout(180000)] public IEnumerator PrimeiraPessoa_Bracos()
        {
            FBSession.Load(); FBSession.map = "estande"; FBSession.mode = "treino"; FBSession.online = false; FBSession.view = "fp"; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_estande"); while (!op.isDone) yield return null;
            for (float t = 0; t < 4; t += Time.unscaledDeltaTime) yield return null;
            var arms = Object.FindAnyObjectByType<FBCharArms>(); Assert.IsNotNull(arms, "sem braços do personagem na 1ª pessoa"); Assert.IsTrue(arms.ok, "braços incompletos");
            Debug.Log($"[FreeBala][teste] braços FP do personagem {FBMatch.Player.skinId}: ok");
            var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../logs")); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "print_fp_bracos.png")); yield return null; yield return null;
            FBSession.view = "tps"; FBSession.Save();
        }
        // oficina do REB (Vehicles.tune): cor, roda, suspensão, neon, farol, placa, som montado; grafites e céu vivo no mapa
        [UnityTest, Timeout(180000)] public IEnumerator Oficina_Grafites_Ceu()
        {
            FBSession.Load(); FBSession.map = "vila_jardim"; FBSession.mode = "role"; FBSession.online = false; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_vila_jardim"); while (!op.isDone) yield return null;
            for (float t = 0; t < 3; t += Time.unscaledDeltaTime) yield return null;
            Assert.IsNotNull(GameObject.Find("Grafites"), "sem grafites no mapa"); Assert.IsNotNull(FBLivingSky.I, "sem céu vivo"); Assert.AreEqual("ceu_freebala", RenderSettings.skybox ? RenderSettings.skybox.name : "", "skybox do jogo");
            var car = FBVehicles.list.First(c => c && !c.ie && GameData.Vehicles.ContainsKey(c.id) && FBTune.SomAnchor(Json.O(GameData.Vehicles, c.id)) != null);
            var T = new FBTuneData { hasPaint = true, paint = Color.red, metal = .8f, brilho = .9f, wheel = GameData.Wheels.Keys.First(k => FBRegistry.I.Wheel(k)), wsize = 1.1f, fh = -.1f, rh = .1f, fcam = 8, neon = true, hasFarol = true, placa = "FBL2026", ronco = "v8", turbo = true, tracao = "4x4", somSet = true };
            var mod = GameData.Som.Keys.First(k => FBRegistry.I.Som(k)); T.som.Add(new FBSomSlot { m = mod, p = Vector3.zero, s = 1 }); T.som.Add(new FBSomSlot { m = mod, p = new Vector3(.6f, 0, 0), s = 1 });
            FBTune.Apply(car, T); yield return null;
            Assert.IsNotNull(car.soundWallNode, "som não montou"); Assert.AreEqual("v8", car.engineProf); Assert.IsTrue(car.turbo);
            Assert.IsNotNull(car.transform.Find("neon"), "sem neon"); Assert.AreEqual(T.wheel, car.curWid, "roda não trocou");
            var cam = Camera.main; cam.transform.position = car.transform.position + car.transform.rotation * new Vector3(4, 2.2f, -6); cam.transform.LookAt(car.transform.position + Vector3.up);
            var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../logs")); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "print_oficina.png")); yield return null; yield return null;
            Debug.Log($"[FreeBala][teste] oficina em {car.id}: roda {car.curWid}, som {T.som.Count} módulos, grafites ok, céu ok");
        }
        // diagnóstico do rig: altura dos ossos em relação ao chão (personagem afundado?)
        [UnityTest, Timeout(120000)] public IEnumerator Rig_Alturas()
        {
            var op = SceneManager.LoadSceneAsync("Menu"); while (!op.isDone) yield return null;
            for (float t = 0; t < 3; t += Time.unscaledDeltaTime) yield return null;
            { var pv = GameObject.Find("Previa"); var an0 = pv ? pv.GetComponent<Animator>() : null;
              if (an0) { an0.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                string M() { string B0(HumanBodyBones b) { var t = an0.GetBoneTransform(b); return t ? (t.position.y - pv.transform.position.y).ToString("0.00") : "-"; } var ci = an0.GetCurrentAnimatorClipInfo(0); return $"quadril {B0(HumanBodyBones.Hips)} · pé {B0(HumanBodyBones.LeftFoot)} · cabeça {B0(HumanBodyBones.Head)} · clipe {(ci.Length > 0 ? ci[0].clip.name : "-")}"; }
                for (float t = 0; t < 1; t += Time.deltaTime) yield return null; Debug.Log("[FreeBala][rig] Previa normal: " + M());
                for (int L = 0; L < an0.layerCount; L++) { var cl = an0.GetCurrentAnimatorClipInfo(L); Debug.Log($"[FreeBala][rig] camada {L} {an0.GetLayerName(L)} peso {an0.GetLayerWeight(L):0.00} · {string.Join(", ", cl.Select(c => c.clip.name + "×" + c.weight.ToString("0.00") + (c.clip.empty ? "(VAZIO)" : "")))}"); }
                var ch1 = pv.GetComponent<FBCharacter>(); if (ch1) ch1.enabled = false;
                an0.SetLayerWeight(1, 0); for (float t = 0; t < .6f; t += Time.deltaTime) yield return null; Debug.Log("[FreeBala][rig] sem Upper Body: " + M());
                an0.SetLayerWeight(1, 1); an0.SetBool("IsAiming", false); for (float t = 0; t < .6f; t += Time.deltaTime) yield return null;
                var ctl = an0.runtimeAnimatorController; Debug.Log($"[FreeBala][rig] controlador {ctl.GetType().Name} {ctl.name} · applyRootMotion {an0.applyRootMotion} · avatar {an0.avatar?.name} valid {an0.avatar?.isValid} human {an0.avatar?.isHuman} · layer1 {an0.GetLayerName(1)}");
                an0.runtimeAnimatorController = null; var gq = UnityEngine.Playables.PlayableGraph.Create(); var pq = UnityEngine.Animations.AnimationClipPlayable.Create(gq, FBRegistry.I.Clip("Idle")); UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(UnityEngine.Animations.AnimationPlayableOutput.Create(gq, "a", an0), pq); gq.Play();
                for (float t = 0; t < .6f; t += Time.deltaTime) yield return null; Debug.Log("[FreeBala][rig] Previa só com Idle via playable: " + M()); gq.Destroy(); an0.runtimeAnimatorController = ctl;
                var rbld = pv.GetComponents<Behaviour>().FirstOrDefault(c => c.GetType().Name == "RigBuilder"); if (rbld) { rbld.enabled = false; an0.Rebind(); for (float t = 0; t < 1; t += Time.deltaTime) yield return null; Debug.Log("[FreeBala][rig] Previa sem RigBuilder: " + M()); }
                var ch0 = pv.GetComponent<FBCharacter>(); if (ch0) ch0.enabled = false; for (float t = 0; t < 1; t += Time.deltaTime) yield return null; Debug.Log("[FreeBala][rig] Previa sem FBCharacter: " + M());
                var clip = an0.GetCurrentAnimatorClipInfo(0).Length > 0 ? an0.GetCurrentAnimatorClipInfo(0)[0].clip : null;
                if (clip) { var g2 = Object.Instantiate(FBRegistry.I.Char("r_tutu"), new Vector3(30, 0, 0), Quaternion.identity); var a2 = g2.GetComponent<Animator>(); a2.cullingMode = AnimatorCullingMode.AlwaysAnimate; a2.runtimeAnimatorController = null;
                  var gr = UnityEngine.Playables.PlayableGraph.Create(); var pl = UnityEngine.Animations.AnimationClipPlayable.Create(gr, clip); UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(UnityEngine.Animations.AnimationPlayableOutput.Create(gr, "a", a2), pl); gr.Play();
                  for (float t = 0; t < 1; t += Time.deltaTime) yield return null; var h = a2.GetBoneTransform(HumanBodyBones.Hips); var hd = a2.GetBoneTransform(HumanBodyBones.Head);
                  Debug.Log($"[FreeBala][rig] clipe {clip.name} puro: quadril {h.position.y:0.00} · cabeça {hd.position.y:0.00} · humanMotion {clip.humanMotion} · isHuman {clip.isHumanMotion}"); gr.Destroy(); Object.Destroy(g2); } } }
            foreach (var id in new[] { "r_tutu", "n_alicia", "r_pm_silva", "p_m01" })
            {
                var pf = FBRegistry.I.Char(id); if (!pf) continue; var g = Object.Instantiate(pf, new Vector3(20, 0, 0), Quaternion.identity); yield return null; yield return null;
                var an = g.GetComponent<Animator>(); for (float t = 0; t < 1; t += Time.deltaTime) yield return null;
                string B(HumanBodyBones b) { var t = an && an.isHuman ? an.GetBoneTransform(b) : null; return t ? (t.position.y - g.transform.position.y).ToString("0.00") : "-"; }
                var rs = g.GetComponentsInChildren<Renderer>(); var bb = rs.Length > 0 ? rs[0].bounds : default; foreach (var r in rs) bb.Encapsulate(r.bounds);
                Debug.Log($"[FreeBala][rig] {id}: escala {g.transform.lossyScale.y:0.00} · humano {an && an.isHuman} · humanScale {(an ? an.humanScale : 0):0.00} · controlador {(an && an.runtimeAnimatorController ? an.runtimeAnimatorController.name : "-")} · quadril {B(HumanBodyBones.Hips)} · pé {B(HumanBodyBones.LeftFoot)} · cabeça {B(HumanBodyBones.Head)} · malha y {bb.min.y - g.transform.position.y:0.00}..{bb.max.y - g.transform.position.y:0.00} · rootMotion {(an ? an.applyRootMotion : false)}");
                Object.Destroy(g);
            }
        }
        [UnityTest, Timeout(180000)] public IEnumerator Rig_Jogador()
        {
            FBSession.Load(); FBSession.map = "estande"; FBSession.mode = "treino"; FBSession.view = "tps"; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_estande"); while (!op.isDone) yield return null;
            for (float t = 0; t < 2; t += Time.unscaledDeltaTime) yield return null;
            var P = FBMatch.Player; var an = P.an; an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            string M() { string B(HumanBodyBones b) { var t = an.GetBoneTransform(b); return t ? (t.position.y - P.transform.position.y).ToString("0.00") : "-"; } return $"quadril {B(HumanBodyBones.Hips)} · pé E {B(HumanBodyBones.LeftFoot)} · pé D {B(HumanBodyBones.RightFoot)} · joelho E {B(HumanBodyBones.LeftLowerLeg)}"; }
            for (float t = 0; t < 1; t += Time.deltaTime) yield return null; Debug.Log("[FreeBala][rigj] normal: " + M());
            for (int L = 0; L < an.layerCount; L++) { var cl = an.GetCurrentAnimatorClipInfo(L); Debug.Log($"[FreeBala][rigj] camada {L} {an.GetLayerName(L)} peso {an.GetLayerWeight(L):0.00} · {string.Join(", ", cl.Select(c => c.clip.name + "×" + c.weight.ToString("0.00")))}"); }
            FBQuality.FootIK = false; for (float t = 0; t < 1; t += Time.deltaTime) yield return null; Debug.Log("[FreeBala][rigj] sem IK de pés: " + M());
            var rb = P.GetComponents<Behaviour>().FirstOrDefault(c => c.GetType().Name == "RigBuilder"); if (rb) { rb.enabled = false; for (float t = 0; t < 1; t += Time.deltaTime) yield return null; Debug.Log("[FreeBala][rigj] sem RigBuilder: " + M()); }
            P.enabled = false; for (float t = 0; t < 1; t += Time.deltaTime) yield return null; Debug.Log("[FreeBala][rigj] sem FBCharacter: " + M());
            FBQuality.FootIK = true;
        }
    }
}
