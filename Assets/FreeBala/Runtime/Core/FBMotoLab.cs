// Autoteste das MOTOS com as poses do 171 (rodada 43): FreeBala.exe -fbmotolab [mapa:modo] [moto]
// Monta o jogador numa moto do Grau numa faixa reta, e passa por: parado, acelerando, curva para cada lado, rápido (vento), grau, freando.
// Em cada estado: clipe escolhido (FBDrive.MotoClipOf), inclinação da moto, velocidade, prints de lado e de trás (autoteste/moto_*.png).
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBMotoLab : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbmotolab"); if (i < 0) return;
            var g = new GameObject("FB_MotoLab"); DontDestroyOnLoad(g); var t = g.AddComponent<FBMotoLab>();
            if (i + 1 < a.Length && !a[i + 1].StartsWith("-")) t.run = a[i + 1]; if (i + 2 < a.Length && !a[i + 2].StartsWith("-")) t.moto = a[i + 2]; else if (FBMxBike.Has("xj6")) t.moto = "mx_xj6";
        }
        string run = "vila_jardim:classico", moto = "xj6", dir; Camera cam; float cThr, cSt; bool cG;
        static void Log(string s) => Debug.Log("[FreeBala][motolab] " + s);
        IEnumerator Wait(float s) { for (float w = 0; w < s; w += Time.unscaledDeltaTime) yield return null; }
        void Shot(string n) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "moto_" + n + ".png"));
        IEnumerator Start()
        {
            dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"); System.IO.Directory.CreateDirectory(dir);
            yield return Wait(3); var mm = run.Split(':'); GameData.Load(); FBSession.Load(); var keepMap = FBSession.map; var keepMode = FBSession.mode;
            FBSession.map = mm[0]; FBSession.mode = mm.Length > 1 ? mm[1] : "classico"; FBSession.online = false; FBSession.view = "tps"; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_" + mm[0]); while (op != null && !op.isDone) yield return null; yield return Wait(5);
            FBMatch.God = true; foreach (var b in FBMatch.Bots.ToList()) if (b) b.gameObject.SetActive(false);
            var me = FBMatch.Player; if (!me) { Log("sem jogador"); yield break; }
            // pista plana isolada 300 m acima do mapa (como o -fbdrivelab): nada no caminho nas manobras
            var p0 = new Vector3(me.Pos.x, me.Pos.y + 300, me.Pos.z);
            var pad = GameObject.CreatePrimitive(PrimitiveType.Plane); pad.name = "FB_MotoPad"; Destroy(pad.GetComponent<Collider>()); pad.layer = FBWorld.LMap; pad.transform.position = p0; pad.transform.localScale = new Vector3(300, 1, 300);
            var bc = new GameObject("FB_MotoPadCol").AddComponent<BoxCollider>(); bc.gameObject.layer = FBWorld.LMap; bc.size = new Vector3(3000, 2, 3000); bc.transform.position = p0 - Vector3.up;
            var tex = new Texture2D(64, 64) { wrapMode = TextureWrapMode.Repeat }; for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) tex.SetPixel(x, y, x % 32 == 0 || y % 32 == 0 ? new Color(.85f, .85f, .8f) : ((x / 32) + (y / 32)) % 2 == 0 ? new Color(.33f, .34f, .35f) : new Color(.27f, .28f, .29f)); tex.Apply();
            var sh = Shader.Find("Universal Render Pipeline/Lit"); var mat = new Material(sh ? sh : pad.GetComponent<Renderer>().sharedMaterial.shader); mat.mainTexture = tex; if (mat.HasProperty("_BaseMap")) { mat.SetTexture("_BaseMap", tex); mat.SetTextureScale("_BaseMap", new Vector2(375, 375)); }
            pad.GetComponent<Renderer>().sharedMaterial = mat; Physics.SyncTransforms();
            var car = FBVehicles.Spawn(moto, p0 + Vector3.up * .3f, 0); yield return Wait(1);
            if (!car) { Log("moto não nasceu: " + moto); yield break; }
            var cc = me.GetComponent<CharacterController>(); if (cc) cc.enabled = false; me.transform.position = car.transform.position - car.transform.right * 1.2f; if (cc) cc.enabled = true; yield return Wait(.3f);
            cam = new GameObject("MotoCam").AddComponent<Camera>(); cam.depth = 100; cam.cullingMask = Camera.main ? Camera.main.cullingMask : ~0; cam.nearClipPlane = .05f;
            void SideShot(string n2) { var T = car.transform; cam.transform.position = T.position - T.right * 3.4f + T.forward * .4f + Vector3.up * 1.2f; cam.transform.LookAt(T.position + Vector3.up * .8f); cam.fieldOfView = 45; Shot(n2); }
            if (!FBDrive.Enter(me, car)) { Log("não montou"); yield break; }
            // subir na moto (clipe do 171): 5 fotos do lado esquerdo
            var dp = me.GetComponent<FBDriverPose>(); float tm = 0;
            foreach (var at in new[] { .2f, .55f, .9f, 1.3f, 2.2f })
            {
                while (tm < at) { tm += Time.deltaTime; yield return null; }
                SideShot($"monta_{at:0.0}".Replace(',', '_').Replace('.', '_')); Log($"subindo t={at:0.0}s fase {dp?.trans ?? "-"} clipe {me.sys} quadril-banco {(me.an ? (me.an.GetBoneTransform(HumanBodyBones.Hips).position - car.L2W(car.seat + new Vector3(0, .42f, -.02f))).magnitude : -1):0.00} m"); yield return null;
            }
            FBPlayer.TestInput = inp => { inp.move = new Vector2(-cSt, cThr); inp.jumpHeld = cG; inp.interact = false; inp.multimidia = false; return inp; };   // antes: Simulate + entrada vazia do jogo = 2 passos por quadro
            Log($"moto {car.id} · clipe de pilotar {FBDrive.SitClip(car)} · lean+ = {FBDrive.MotoLeanPos} · lean− = {FBDrive.MotoLeanNeg}");
            IEnumerator Phase(string n, float secs, float thr, float st, bool grau)
            {
                // a câmera do JOGO (a do laboratório fica parada para as fotos — medir nela dava giro 0 e "fora do eixo" = distância até ela)
                var gcam = Camera.main && Camera.main != cam ? Camera.main : FBPlayer.I ? FBPlayer.I.GetComponentInChildren<Camera>() : null;
                cThr = thr; cSt = st; cG = grau; float rollMax = 0, latMax = 0, yawRate = 0, lastY = gcam ? gcam.transform.eulerAngles.y : 0, offMax = 0, rollCam = 0, stepMax = 0, winA = 0, winT = 0;
                for (float t = 0; t < secs; t += Time.deltaTime)   // um passo por quadro, pelo caminho do teclado
                {
                    yield return null;
                    // r47: câmera de jogo — rolagem (horizonte), quanto a origem antiga andaria de lado e giro máximo (°/s) da câmera
                    rollMax = Mathf.Max(rollMax, Mathf.Abs(FBMxBike.CamRollDbg)); latMax = Mathf.Max(latMax, Mathf.Abs(FBMxBike.CamLatDbg));
                    // r47c: giro em janelas de 0,1 s (dividir pelo deltaTime de UM quadro dava 1.240 °/s num quadro curto depois de foto) + maior salto
                    // de ângulo num quadro (tranco visível)
                    if (!gcam) continue; float cy = gcam.transform.eulerAngles.y, da = Mathf.DeltaAngle(lastY, cy); stepMax = Mathf.Max(stepMax, Mathf.Abs(da)); winA += da; winT += Time.deltaTime;
                    if (winT >= .1f) { yawRate = Mathf.Max(yawRate, Mathf.Abs(winA) / winT); winA = winT = 0; } lastY = cy;
                    rollCam = Mathf.Max(rollCam, Mathf.Abs(Mathf.DeltaAngle(0, gcam.transform.eulerAngles.z)));   // horizonte da câmera de jogo
                    offMax = Mathf.Max(offMax, Mathf.Abs(Vector3.Dot(car.transform.position - gcam.transform.position, Quaternion.Euler(0, cy, 0) * Vector3.right)));
                }
                Shot(n + "_camjogo"); yield return null;
                if (car.mx) Log($"{n} câmera: rolagem máx {rollMax:0.0}° · origem antiga sairia {latMax:0.00} m de lado · moto fora do centro da câmera {offMax:0.00} m · giro máx {yawRate:0} °/s (janela 0,1 s) · maior salto num quadro {stepMax:0.0}° · horizonte da câmera de jogo {rollCam:0.0}° ({(gcam ? gcam.name : "sem câmera")})");
                var T = car.transform; float leanDeg = car.lean * Mathf.Rad2Deg;
                cam.transform.position = T.position + T.right * 3.2f + Vector3.up * 1.2f; cam.transform.LookAt(T.position + Vector3.up * .9f); cam.fieldOfView = 45; yield return null; Shot(n + "_lado");
                cam.transform.position = T.position - T.forward * 3.6f + Vector3.up * 1.5f; cam.transform.LookAt(T.position + Vector3.up * .9f); yield return null; Shot(n + "_tras");
                Log($"{n}: {car.v * 3.6f / 1.2f:0} km/h · inclinação {leanDeg:0}° · grau {car.wheelie:0.00} · clipe {FBDrive.MotoClipOf(car) ?? me.sys} (sys {me.sys})" + MxInfo());
            }
            string MxInfo()
            {
                var mx = car.mx; if (!mx) return ""; var L = dp ? dp.Last : default; var an = me.an; string Er(HumanBodyBones b, Vector3 t, bool on) { var x = an ? an.GetBoneTransform(b) : null; return on && x ? (x.position - t).magnitude.ToString("0.00") : "-"; }
                return $" · MX marcha {mx.GearLabel} rpm {mx.rpmN:0.00} grau {mx.wheelieAngle:0}° lean {mx.leanAngle:0}° estado {mx.state} caiu {mx.crashed} · erro mão E {Er(HumanBodyBones.LeftHand, L.hL, L.hands)} D {Er(HumanBodyBones.RightHand, L.hR, L.hands)} pé E {Er(HumanBodyBones.LeftFoot, L.fL, L.feet)} D {Er(HumanBodyBones.RightFoot, L.fR, L.feet)} m";
            }
            if (car.mx)
            {   // r44: arrancada (0–60 km/h), máxima, grau segurado por acelerador (piloto simples), curva fechada, frenagem de 60
                cThr = 1; cSt = 0; cG = false; float t60 = -1, tt = 0, vmax = 0;
                for (; tt < 12; tt += Time.deltaTime) { float k = car.mx.speedKmh; vmax = Mathf.Max(vmax, k); if (t60 < 0 && k >= 60) t60 = tt; yield return null; }
                Log($"arrancada: 0–60 em {(t60 < 0 ? "não chegou" : t60.ToString("0.0") + " s")} · máxima em 12 s {vmax:0} km/h (APK: {Json.F(Json.O(car.meta, "params"), "maxspeed")} km/h)" + MxInfo());
                cThr = -1; float tb = 0; while (car.mx.speedKmh > 2 && tb < 10) { tb += Time.deltaTime; yield return null; } Log($"frenagem de {vmax:0} km/h até parar: {tb:0.0} s" + MxInfo());
                cThr = 1; for (float w0 = 0; w0 < 3; w0 += Time.deltaTime) yield return null;
                float gT = 0, gMax = 0, gOn = 0; cG = true;
                for (; gT < 7 && !car.mx.crashed; gT += Time.deltaTime) { float ga = car.mx.wheelieAngle; gMax = Mathf.Max(gMax, ga); if (car.mx.onWheelie) gOn += Time.deltaTime; cThr = ga > 50 ? -.7f : ga > 38 ? 0 : ga > 26 ? .45f : 1; /* piloto de teste: freio traseiro acima de 50° (como no MX) */ if (Mathf.Abs(gT - 3) < .02f) { cam.transform.position = car.transform.position + car.transform.right * 3.2f + Vector3.up * 1.2f; cam.transform.LookAt(car.transform.position + Vector3.up * .9f); Shot("mx_grau_lado"); } yield return null; }
                cG = false; Log($"grau segurado 7 s: no grau {gOn:0.0} s · ângulo máx {gMax:0}° · caiu {car.mx.crashed}" + MxInfo());
                if (car.mx.crashed) { yield return Wait(5); FBDrive.Enter(me, car); yield return Wait(3); }
                cThr = 0; for (float w0 = 0; w0 < 4 && car.mx.onWheelie; w0 += Time.deltaTime) yield return null; yield return Wait(.5f);   /* r45: roda da frente no chão antes da curva (antes a curva começava a 83° de grau) */
                cThr = .8f; cSt = 1; for (float w1 = 0; w1 < 4; w1 += Time.deltaTime) yield return null; Log("curva fechada esquerda 4 s" + MxInfo());
                cSt = 0; cThr = 0; for (float w2 = 0; w2 < 3; w2 += Time.deltaTime) yield return null;
                // ---- r45d: grau do JOGADOR (segura o grau e o acelerador cheio, sem modular): APK puro × assistido
                foreach (bool assist in new[] { false, true })
                {
                    FBMxBike.GrauAssist = assist; if (car.mx.crashed) { yield return Wait(4); if (!FBVehicles.CarOf(me)) FBDrive.Enter(me, car); yield return Wait(3); }
                    cG = false; cThr = 1; for (float w0 = 0; w0 < 3; w0 += Time.deltaTime) yield return null;
                    float jMax = 0, jOn = 0, s1 = 0, s2 = 0; int n = 0; cG = true; cThr = 1;
                    for (float gj = 0; gj < 6 && !car.mx.crashed; gj += Time.deltaTime) { float ja = car.mx.wheelieAngle; jMax = Mathf.Max(jMax, ja); if (car.mx.onWheelie) { jOn += Time.deltaTime; if (gj > 1.5f) { s1 += ja; s2 += ja * ja; n++; } } yield return null; }
                    float mean = n > 0 ? s1 / n : 0, sd = n > 0 ? Mathf.Sqrt(Mathf.Max(0, s2 / n - mean * mean)) : 0;
                    { var T = car.transform; cam.transform.position = T.position + T.right * 3.4f + Vector3.up * 1.2f; cam.transform.LookAt(T.position + Vector3.up * .9f); cam.fieldOfView = 45; yield return null; Shot($"grau_jogador_{(assist ? "assistido" : "apk")}"); }
                    Log($"grau do jogador ({(assist ? "ASSISTIDO" : "APK puro")}): no grau {jOn:0.0}/6 s · máx {jMax:0}° · média {mean:0}° ± {sd:0}° · caiu {car.mx.crashed} · motos levantadas {FBMxBike.Recovered}");
                    cG = false; cThr = .5f; for (float w0 = 0; w0 < 3; w0 += Time.deltaTime) yield return null;
                    cThr = .5f; for (float w0 = 0; w0 < 1.5f; w0 += Time.deltaTime) yield return null; Log($"  meia aceleração no grau: {(assist ? "assistido" : "apk")} · " + MxInfo());
                }
                FBMxBike.GrauAssist = true; if (car.mx.crashed) { yield return Wait(4); if (!FBVehicles.CarOf(me)) FBDrive.Enter(me, car); yield return Wait(3); }
                // ---- r45d: som sem acelerar (rolando a 50 km/h) e rodas girando
                cThr = 1; for (float w0 = 0; w0 < 4; w0 += Time.deltaTime) yield return null; Log("acelerando: " + car.mx.SoundDiag + " · " + car.mx.WheelDiag);
                foreach (bool piso in new[] { false, true }) { FBMxBike.SomPiso = piso; cThr = 0; for (float w0 = 0; w0 < 1.2f; w0 += Time.deltaTime) yield return null; Log($"soltou o acelerador ({(piso ? "com piso" : "APK")}): {car.mx.speedKmh:0} km/h · " + car.mx.SoundDiag + " · " + car.mx.WheelDiag); cThr = 1; for (float w0 = 0; w0 < 2.5f; w0 += Time.deltaTime) yield return null; }
                FBMxBike.SomPiso = true; cThr = -1; for (float w0 = 0; w0 < 4; w0 += Time.deltaTime) yield return null; cThr = 0; yield return Wait(1.5f); Log("parado: " + car.mx.SoundDiag);
                // ---- r45d: RUA DE VERDADE (emendas, meio-fio, tampas): quique com e sem o anti-quique
                if (FBRoads.Ready)
                {
                    var road = FBRoads.segs.Where(S => S.kind == 0 && S.len > 90 && !S.ring).OrderBy(S => (FBRoads.At(S, 10, out _) - p0 + Vector3.up * 300).sqrMagnitude).FirstOrDefault();
                    foreach (bool anti in new[] { false, true })
                    {
                        if (road == null) break; FBMxBike.AntiHop = anti; if (car.mx.crashed) { yield return Wait(4); if (!FBVehicles.CarOf(me)) FBDrive.Enter(me, car); yield return Wait(2); }
                        var rp = FBRoads.At(road, 8, out var tan); if (FBWorld.GroundY(rp.x, rp.y + 3, rp.z, .4f, 8) is float gy) rp.y = gy;
                        car.mx.SetActive(false); car.transform.SetPositionAndRotation(rp + Vector3.up * .35f, Quaternion.LookRotation(tan.Flat().normalized)); Physics.SyncTransforms(); car.mx.SetActive(true); car.mx.hopVy = 0; yield return Wait(.6f);
                        float air = 0, tot = 0, maxH = 0, vyMax = 0; int hops = 0; bool inAir = false; bool offRoad = false;
                        for (float rt = 0; rt < 9 && !car.mx.crashed; rt += Time.deltaTime)
                        {
                            var T = car.transform; float s = FBRoads.Project(road, T.position, out _, out _); if (s > road.len - 12) break;
                            var q = FBRoads.At(road, s + 14, out _); var on = FBRoads.At(road, s, out _); if ((on - T.position).Flat().magnitude > 4.5f) { offRoad = true; break; } /* r45e: segura ~50 km/h e para se sair da faixa (antes ia a 140 km/h e caía no mato) */ cThr = car.mx.speedKmh < 50 ? .7f : 0; cSt = -Mathf.Clamp(Vector3.SignedAngle(T.forward.Flat(), (q - T.position).Flat(), Vector3.up) / 15f, -1, 1);
                            float h = FBWorld.GroundY(T.position.x, T.position.y + .5f, T.position.z, .25f, 3) is float g2 ? T.position.y - g2 : 3; maxH = Mathf.Max(maxH, h);
                            var rb = car.GetComponent<Rigidbody>(); float vy = rb ? rb.linearVelocity.y / FBMxBike.S : 0; vyMax = Mathf.Max(vyMax, vy);
                            bool a2 = h > .55f * FBMxBike.S; if (a2) air += Time.deltaTime; if (a2 && !inAir) hops++; inAir = a2; tot += Time.deltaTime; yield return null;
                        }
                        cThr = -1; cSt = 0; { var T = car.transform; cam.transform.position = T.position + T.right * 3.2f + Vector3.up * 1.2f; cam.transform.LookAt(T.position + Vector3.up * .9f); yield return null; Shot($"rua_{(anti ? "antiquique" : "apk")}"); }
                        Log($"rua real ({(anti ? "COM anti-quique" : "sem")}) faixa {road.id}: {tot:0.0} s · no ar {air:0.00} s · pulos {hops} · subida máx {vyMax:0.0} m/s · altura máx {maxH:0.00} · quique segurado {car.mx.hopVy:0.0} m/s · caiu {car.mx.crashed} · saiu da faixa {offRoad} · " + car.mx.WheelDiag);
                        for (float w0 = 0; w0 < 2; w0 += Time.deltaTime) yield return null;
                    }
                    FBMxBike.AntiHop = true;
                    car.mx.SetActive(false); car.transform.SetPositionAndRotation(p0 + Vector3.up * .3f, Quaternion.identity); Physics.SyncTransforms(); car.mx.SetActive(true); cThr = 0; cSt = 0; yield return Wait(1);
                }
                else Log("rua real: sem malha de ruas neste mapa/modo");
            }
            yield return Phase("parado", 1.5f, 0, 0, false);
            yield return Phase("acelerando", 2.5f, 1, 0, false);
            yield return Phase("curva_esq", 1.4f, .7f, 1, false);
            yield return Phase("curva_dir", 1.8f, .7f, -1, false);
            yield return Phase("rapido", 5f, 1, 0, false);
            yield return Phase("grau", 1.2f, 1, 0, true);
            yield return Phase("freando", 2.5f, -1, 0, false);
            yield return Phase("parado_fim", 2f, 0, 0, false);
            FBPlayer.TestInput = null; yield return Wait(.5f);
            // descer da moto (clipe do 171): pede para sair parado e fotografa
            dp = me.GetComponent<FBDriverPose>();                                            // r47c: cada Exit destrói e cada Enter recria o componente (o grau do jogador sobe de novo) — a referência do começo ficava velha e o log dizia "NÃO tocou"
            bool ok = FBDrive.Exit(me, false); Log($"descer: pedido {(ok ? "aceito" : "recusado")} a {Mathf.Abs(car.v) * 3.6f:0} km/h · fase {dp?.trans ?? "-"}{(FBDrive.ExitPending ? " · parando para descer (r47)" : "")}"); float td = 0;
            for (float tw = 0; FBDrive.ExitPending && tw < 4; tw += Time.deltaTime) yield return null;   // r47: rodando devagar, freia e desce pelo clipe
            Log($"descer: fase {dp?.trans ?? "-"} a {Mathf.Abs(car.v) * 3.6f:0} km/h (clipe {(dp && dp.trans == "dismount" ? "tocando" : "NÃO tocou · lado: " + FBDrive.DoorDiag)})");
            foreach (var at in new[] { .15f, .4f, .7f, 1f, 1.6f })
            {
                while (td < at) { td += Time.deltaTime; yield return null; }
                SideShot($"desce_{at:0.0}".Replace(',', '_').Replace('.', '_')); yield return null;
            }
            var rel = car.transform.InverseTransformPoint(me.transform.position); Log($"depois de descer: na moto {FBVehicles.CarOf(me) != null} · posição na moto x {rel.x:+0.00;-0.00} z {rel.z:+0.00;-0.00} · olhando {Mathf.DeltaAngle(car.transform.eulerAngles.y, me.Yaw):0}° da frente da moto");
            if (FBVehicles.CarOf(me)) FBDrive.Exit(me, true); yield return Wait(1);
            Log("fim"); FBSession.map = keepMap; FBSession.mode = keepMode; FBSession.Save(); yield return Wait(1); Application.Quit();
        }
    }
}
