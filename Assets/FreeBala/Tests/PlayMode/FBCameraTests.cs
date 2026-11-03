// Câmera em 3ª pessoa × retículo × cano × impacto (porte fiel do updateCamera/playerFire/aimPoint/shotOrigin do main.js).
// Numa arena flutuante (chão + parede móvel, longe do mapa do estande), jogador em 3ª pessoa, dispersão zerada (FBRecoil.TestNoSpread):
//  - para cada classe de arma, alvo a 5/20/60 m, mirando (ADS) e do quadril: o impacto fica a < 3 cm do ponto mirado pela câmera e a
//    < 5 cm + 0,15 % da distância do ponto sob o retículo medido antes do tiro (a câmera parada não pode "andar" entre o quadro e o tiro);
//  - obstáculo entre o cano e o alvo (fora da linha da câmera): a bala para no obstáculo e o retículo vira MIRA BLOQUEADA;
//  - parede e carro colados às costas: a câmera não atravessa (nem por um quadro, nem girando);
//  - normal ↔ ADS: transição contínua (sem salto de posição/FOV) e câmera parada depois de assentar;
//  - correr/correr+mirar/correr+atirar/mirar em diagonal e de costas: o corpo não "foge" da câmera (deriva do quadril ≤ 35 cm).
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FreeBala.Tests
{
    public class FBCameraTests
    {
        static readonly Vector3 O = new(0, 400, 0);                      // arena: chão no y = 400, jogador olhando para +Z
        Transform arena; GameObject wall;

        IEnumerator Setup()
        {
            FBSession.Load(); FBSession.map = "estande"; FBSession.mode = "treino"; FBSession.online = false; FBSession.view = "tps"; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_estande"); while (!op.isDone) yield return null;
            for (float t = 0; t < 25 && !(FBMatch.I && FBMatch.Player && FBPlayer.I && FBMatch.I.state == "play"); t += Time.unscaledDeltaTime) yield return null;
            Assert.IsNotNull(FBPlayer.I, "sem jogador"); Assert.AreEqual("play", FBMatch.I.state, "partida não começou");
            FBMatch.God = true; if (FBPlayer.I.FirstPerson) FBPlayer.I.ForceThirdPerson();
            FBCamLab.Install(); FBRecoil.TestNoSpread = true;
            arena = new GameObject("ArenaCameraTeste").transform;
            FBCamLab.Box(arena, "Chao", O + new Vector3(0, -.5f, 50), new Vector3(30, 1, 160));
            wall = FBCamLab.Box(arena, "Parede", O + new Vector3(0, 4, 20), new Vector3(14, 8, .4f), true);
            yield return Place(0);
        }
        void TearDown() { FBCamLab.Remove(); FBMatch.God = false; Time.captureDeltaTime = 0; if (arena) Object.Destroy(arena.gameObject); }

        static IEnumerator Place(float yaw)
        {
            var me = FBMatch.Player; FBCamLab.Teleport(me, O + Vector3.up * .05f, yaw); me.GetComponent<FBWeaponUser>().recoil.Reset();
            for (float t = 0; t < .9f; t += Time.deltaTime) yield return null;
        }
        static IEnumerator Ready(FBWeaponUser wu)
        {
            wu.RefillAll(3);
            for (float t = 0; t < 4 && (wu.cool > 0 || wu.boltT > 0 || wu.reloadT > 0 || wu.switching > 0); t += Time.deltaTime) yield return null;
        }
        void SetWall(float dist) => wall.transform.position = O + new Vector3(0, 4, dist + .2f);   // face da parede em z = dist

        [UnityTest, Timeout(600000)]
        public IEnumerator Tiro_RetículoCanoImpacto_PorClasse()
        {
            yield return Setup();
            var me = FBMatch.Player; var pl = FBPlayer.I; var wu = me.GetComponent<FBWeaponUser>(); var fails = new List<string>(); var rows = new List<string>();
            try
            {
                foreach (var (cls, pref) in FBCamLab.CLASSES)
                {
                    var w = FBCamLab.Pick(cls, pref); if (w == null) { Debug.LogWarning("[FreeBala][cam] sem arma " + cls); continue; }
                    wu.Give(w.id); for (float t = 0; t < 1.2f; t += Time.deltaTime) yield return null;
                    foreach (var dist in new[] { 5f, 20f, 60f })
                        foreach (var aim in new[] { true, false })
                        {
                            SetWall(dist); yield return Place(0); FBCamLab.aim = aim; pl.camPitch = 0;
                            yield return Ready(wu); for (float t = 0; t < .9f; t += Time.deltaTime) yield return null;
                            string who = $"{cls}/{w.id} {dist} m {(aim ? "mirando" : "quadril")}";
                            if (!FBCamLab.CrosshairHit(pl.cam, out var h0) || h0.collider.gameObject != wall) { fails.Add(who + ": retículo não está na parede"); continue; }
                            int n = pl.ShotCount; FBCamLab.Shoot();
                            for (int k = 0; k < 6 && pl.ShotCount == n; k++) yield return null;
                            if (pl.ShotCount == n) { fails.Add(who + ": o tiro não saiu"); continue; }
                            float eAim = Vector3.Distance(pl.LastShotHit, pl.LastShotAim), eX = Vector3.Distance(pl.LastShotHit, h0.point), onWall = Mathf.Abs(pl.LastShotHit.z - (O.z + dist));
                            rows.Add($"{who}: impacto×mirado {eAim * 100:0.0} cm · impacto×retículo {eX * 100:0.0} cm · cano→alvo {Vector3.Distance(me.MuzzlePos, pl.LastShotHit):0.0} m");
                            if (eAim > .03f) fails.Add($"{who}: impacto a {eAim * 100:0.0} cm do ponto mirado");
                            if (eX > .05f + dist * .0015f) fails.Add($"{who}: impacto a {eX * 100:0.0} cm do retículo");
                            if (onWall > .05f) fails.Add($"{who}: bala parou fora da parede ({pl.LastShotHit})");
                            FBCamLab.aim = false;
                        }
                }
            }
            finally { foreach (var r in rows) Debug.Log("[FreeBala][cam] " + r); TearDown(); }
            Assert.IsEmpty(fails, string.Join("\n", fails));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Tiro_ObstaculoEntreCanoEAlvo()
        {
            yield return Setup();
            var me = FBMatch.Player; var pl = FBPlayer.I; var wu = me.GetComponent<FBWeaponUser>();
            try
            {
                var w = FBCamLab.Pick("fuzil", new[] { "ak" }); wu.Give(w.id); for (float t = 0; t < 1.2f; t += Time.deltaTime) yield return null;
                SetWall(20); yield return Place(0); FBCamLab.aim = true; yield return Ready(wu); for (float t = 0; t < .9f; t += Time.deltaTime) yield return null;
                Assert.IsTrue(FBCamLab.CrosshairHit(pl.cam, out var h0), "retículo sem alvo");
                var M = me.MuzzlePos; var dir = (h0.point - M).normalized; var c = M + dir * 1.5f;
                var box = FBCamLab.Box(arena, "Obstaculo", c, Vector3.one * .18f);
                yield return null;
                Assert.IsTrue(FBCamLab.CrosshairHit(pl.cam, out var h1) && h1.collider.gameObject == wall, "o obstáculo tapou a câmera (teste mal montado)");
                for (float t = 0; t < .2f; t += Time.deltaTime) yield return null;
                Assert.IsTrue(pl.AimBlocked, "mira bloqueada não acendeu");
                int n = pl.ShotCount; FBCamLab.Shoot(); for (int k = 0; k < 6 && pl.ShotCount == n; k++) yield return null;
                Assert.Greater(pl.ShotCount, n, "o tiro não saiu");
                Debug.Log($"[FreeBala][cam] obstáculo: impacto a {Vector3.Distance(pl.LastShotHit, c) * 100:0} cm do obstáculo, {Vector3.Distance(pl.LastShotHit, h0.point):0.0} m do retículo");
                Assert.Less(Vector3.Distance(pl.LastShotHit, c), .2f, "a bala atravessou o obstáculo entre o cano e o alvo");
            }
            finally { TearDown(); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Camera_ParedeECarro_NaoAtravessa()
        {
            yield return Setup();
            var me = FBMatch.Player; var pl = FBPlayer.I; var fails = new List<string>();
            try
            {
                var w = FBCamLab.Pick("fuzil", new[] { "ak" }); me.GetComponent<FBWeaponUser>().Give(w.id); for (float t = 0; t < 1f; t += Time.deltaTime) yield return null;
                SetWall(40);
                // parede colada às costas (face a 0,55 m do centro do jogador), girando a câmera ±35° e mirando
                var back = FBCamLab.Box(arena, "ParedeCostas", O + new Vector3(0, 3, -.75f), new Vector3(10, 6, .4f));
                foreach (var yaw in new[] { 0f, 35f, -35f })
                    foreach (var aim in new[] { false, true })
                    {
                        yield return Place(yaw); FBCamLab.aim = aim; float worst = 9;
                        for (float t = 0; t < .8f; t += Time.deltaTime) { yield return null; worst = Mathf.Min(worst, pl.cam.transform.position.z - (O.z - .55f)); }
                        var head = me.Head ? me.Head.position : me.Pos + Vector3.up * 1.6f;
                        if (worst < .04f) fails.Add($"parede yaw {yaw} {(aim ? "mirando" : "normal")}: câmera {worst * 100:0} cm da face (atravessou ou encostou no plano próximo)");
                        if (Physics.Linecast(head, pl.cam.transform.position, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)) fails.Add($"parede yaw {yaw}: parede entre a cabeça e a câmera");
                    }
                Object.Destroy(back); FBCamLab.aim = false; yield return null;
                // carro atrás e ao lado
                var car = FBVehicles.Spawn("S10_2017", O + new Vector3(.6f, .05f, -2.4f), 90);
                Assert.IsNotNull(car, "S10 não nasceu");
                foreach (var yaw in new[] { 0f, 30f, -30f })
                {
                    yield return Place(yaw); bool inside = false;
                    for (float t = 0; t < .8f; t += Time.deltaTime) { yield return null; inside |= Physics.CheckSphere(pl.cam.transform.position, .04f, 1 << FBWorld.LVehicle, QueryTriggerInteraction.Ignore); }
                    if (inside) fails.Add($"carro yaw {yaw}: câmera entrou no veículo");
                }
                FBVehicles.list.Remove(car); Object.Destroy(car.gameObject);
            }
            finally { TearDown(); }
            Assert.IsEmpty(fails, string.Join("\n", fails));
        }

        // correr / correr+mirar / correr+atirar / mirando em diagonal / mirando de costas, com fuzil e pistola: o quadril (o corpo que se vê) não pode
        // se afastar do personagem (pivô da câmera) mais que 35 cm, e a distância câmera→quadril não pode oscilar mais que 60 cm (câmera "ficando para trás")
        [UnityTest, Timeout(300000)]
        public IEnumerator Movimento_CorpoNaoFogeDaCamera()
        {
            yield return Setup();
            var me = FBMatch.Player; var pl = FBPlayer.I; var fails = new List<string>();
            try
            {
                SetWall(140);
                foreach (var cls in new[] { "fuzil", "pistola" })
                {
                    var w = FBCamLab.Pick(cls, new string[0]); me.GetComponent<FBWeaponUser>().Give(w.id); for (float t = 0; t < 1.2f; t += Time.deltaTime) yield return null;
                    foreach (var (nm, mv, run, am, fr) in new[] { ("correndo", new Vector2(0, 1), true, false, false), ("corre_mira", new Vector2(0, 1), true, true, false),
                        ("corre_tiro", new Vector2(0, 1), true, false, true), ("mira_diagonal", new Vector2(.71f, .71f), false, true, false), ("mira_costas", new Vector2(0, -1), false, true, false),
                        ("andando", new Vector2(0, 1), false, false, false), ("andando_lado", new Vector2(1, 0), false, false, false) })
                    {
                        yield return Place(0); me.GetComponent<FBWeaponUser>().RefillAll(5); float drift = 0, swing = 0; float S = me.ScaleK;
                        yield return FBCamLab.Move(me, pl.cam, mv, run, am, fr, 1.8f, (a, b) => { drift = a; swing = b; });
                        Debug.Log($"[FreeBala][cam] {cls} {nm}: quadril até {drift * 100:0} cm do personagem · câmera→quadril variou {swing * 100:0} cm");
                        if (drift > .35f * S) fails.Add($"{cls} {nm}: corpo fugiu {drift * 100:0} cm do personagem/câmera");
                        if (swing > .6f * S) fails.Add($"{cls} {nm}: distância câmera→corpo oscilou {swing * 100:0} cm");
                    }
                }
            }
            finally { TearDown(); }
            Assert.IsEmpty(fails, string.Join("\n", fails));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Camera_NormalAds_SemSalto()
        {
            yield return Setup();
            var me = FBMatch.Player; var pl = FBPlayer.I;
            try
            {
                foreach (var cls in new[] { "fuzil", "pistola" })
                {
                    var w = FBCamLab.Pick(cls, new string[0]); me.GetComponent<FBWeaponUser>().Give(w.id); SetWall(30); yield return Place(0);
                    Time.captureDeltaTime = 1 / 60f; FBCamLab.aim = false; for (int k = 0; k < 40; k++) yield return null;
                    var pos = new List<Vector3>(); var fov = new List<float>();
                    for (int k = 0; k < 150; k++) { FBCamLab.aim = k >= 20 && k < 85; yield return null; pos.Add(pl.cam.transform.position); fov.Add(pl.cam.fieldOfView); }
                    Time.captureDeltaTime = 0; FBCamLab.aim = false;
                    float maxStep = 0, maxFov = 0; for (int k = 1; k < pos.Count; k++) { maxStep = Mathf.Max(maxStep, (pos[k] - pos[k - 1]).magnitude); maxFov = Mathf.Max(maxFov, Mathf.Abs(fov[k] - fov[k - 1])); }
                    float rest = 0; foreach (var r in new[] { (70, 85), (140, 150) }) for (int k = r.Item1; k < r.Item2; k++) rest = Mathf.Max(rest, (pos[k] - pos[k - 1]).magnitude);
                    float swing = (pos[84] - pos[19]).magnitude;
                    Debug.Log($"[FreeBala][cam] {cls} normal↔ADS: deslocamento {swing * 100:0} cm · maior passo {maxStep * 100:0.0} cm/quadro · maior ΔFOV {maxFov:0.00}°/quadro · parado {rest * 1000:0.0} mm/quadro");
                    Assert.Greater(swing, .1f, $"{cls}: câmera não mudou para a mira (ombro/braço)");
                    Assert.Less(maxStep, .2f, $"{cls}: salto de {maxStep * 100:0} cm num quadro");
                    Assert.Less(maxFov, 3f, $"{cls}: FOV saltou {maxFov:0.0}° num quadro");
                    Assert.Less(rest, .004f, $"{cls}: câmera tremendo parada ({rest * 1000:0.0} mm/quadro)");
                }
            }
            finally { TearDown(); }
        }
    }
}
