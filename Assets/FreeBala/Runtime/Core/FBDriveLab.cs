// Laboratório de DIREÇÃO (rodada 43): FreeBala.exe -fbdrivelab [mapa:modo] [carro] [antigo|novo|ambos]
// Pista plana isolada (300 m acima do mapa) e o MESMO carro nas MESMAS manobras com o modelo antigo (cinemático) e o novo (FBCarPhys):
// arrancada 0-100, frenagem de 100, curva constante a 60, degrau de volante a 80, troca de faixa a 90, freio de mão a 60, freada na curva a 80.
// Tudo medido pelo movimento do transform (igual para os dois modelos): velocidade, aceleração lateral (g), giro (°/s), deriva β, lataria.
// Entrada pelo FBPlayer.TestInput (o mesmo caminho do teclado: FBPlayer → FBDrive.PlayerInput → Step, um passo por quadro).
// Saída: [FreeBala][drivelab] no log, autoteste/drive_<modelo>_<manobra>.png e .csv (série temporal para gráfico).
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBDriveLab : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbdrivelab"); if (i < 0) return;
            var g = new GameObject("FB_DriveLab"); DontDestroyOnLoad(g); var t = g.AddComponent<FBDriveLab>();
            string Arg(int k) => i + k < a.Length && !a[i + k].StartsWith("-") ? a[i + k] : null;
            t.run = Arg(1) ?? t.run; t.carId = Arg(2) ?? t.carId; t.which = Arg(3) ?? t.which;
        }
        string run = "vila_jardim:classico", carId = "GolG2", which = "ambos", dir, model; Camera cam; FBCar car; FBCharacter me; Vector3 pad0;
        float cThr, cSt; bool cHb; const float G = 9.8f * 1.2f;
        static void Log(string s) => Debug.Log("[FreeBala][drivelab] " + s);
        IEnumerator Wait(float s) { for (float w = 0; w < s; w += Time.deltaTime) yield return null; }
        static float Kmh(float v) => v * 3.6f / 1.2f;

        // amostra do movimento (pelo transform)
        struct S { public float t, kmh, st, thr, yr, ay, beta, pit, rol, sF, sR, x, z, yaw; public bool hb; }
        readonly List<S> ser = new(); Vector3 lastP, lastV; float lastYaw, t0, ayF;
        void Sample()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f); var p = car.transform.position; var V = (p - lastP) / dt; V.y = 0;
            var f = FBDrive.Fwd(car.yaw); var l = FBDrive.Left(car.yaw); float yr = Mathf.DeltaAngle(lastYaw * Mathf.Rad2Deg, car.yaw * Mathf.Rad2Deg) / dt;
            float sp = V.magnitude, hw = car.wheelbase / 2, beta = sp > 1 ? Mathf.Atan2(Vector3.Dot(V, l) - hw * yr * Mathf.Deg2Rad, Vector3.Dot(V, f)) * Mathf.Rad2Deg : 0;   // deriva do eixo traseiro
            ayF = Mathf.Lerp(ayF, Vector3.Dot((V - lastV) / dt, l) / G, .25f); lastV = V;                                    // aceleração lateral de verdade (derivada da velocidade)
            ser.Add(new S { t = Time.time - t0, kmh = Kmh(sp) * Mathf.Sign(Vector3.Dot(V, f) + 1e-3f), st = cSt, thr = cThr, hb = cHb, yr = yr, ay = ayF, beta = beta,
                pit = car.bPit * Mathf.Rad2Deg, rol = car.bRol * Mathf.Rad2Deg, sF = car.slipF, sR = car.slipR, x = p.x - pad0.x, z = p.z - pad0.z, yaw = car.yaw * Mathf.Rad2Deg });
            lastP = p; lastYaw = car.yaw;
        }
        IEnumerator Drive(float secs, System.Func<float, bool> stop = null)
        {
            for (float t = 0; t < secs; t += Time.deltaTime) { yield return null; Sample(); if (stop != null && stop(t)) yield break; }
        }
        float SpeedNow => ser.Count > 0 ? Mathf.Abs(ser[^1].kmh) : 0;
        // reta até a velocidade e segura 1 s (controle P do acelerador)
        IEnumerator Reach(float kmh)
        {
            cSt = 0; cHb = false; cThr = 1; yield return Drive(25, _ => SpeedNow >= kmh);
            for (float t = 0; t < 1; t += Time.deltaTime) { cThr = Mathf.Clamp((kmh - SpeedNow) * .15f, -.3f, 1); yield return null; Sample(); }
        }
        void Place()
        {
            car.transform.SetPositionAndRotation(pad0 + Vector3.up * .05f, FBDrive.CarRot(0, 0, 0)); car.yaw = 0; car.v = 0; car.vy = 0; car.pitch = car.roll = 0;
            car.stIn = car.steer = 0; car.gb = null; FBCarPhys.Reset(car); car.crashT = 0; lastP = car.transform.position; lastV = Vector3.zero; ayF = 0; lastYaw = 0; ser.Clear(); t0 = Time.time;
        }
        void Shot(string n, Vector3 camLocal, float fov = 50)
        {
            var T = car.transform; cam.transform.position = T.TransformPoint(camLocal); cam.transform.LookAt(T.position + Vector3.up * .7f); cam.fieldOfView = fov;
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"drive_{model}_{n}.png"));
        }
        void Csv(string n)
        {
            var sb = new StringBuilder("t,kmh,st,thr,hb,yr,ay,beta,pitch,roll,slipF,slipR,x,z,yaw\n"); var ci = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var s in ser) sb.Append(string.Format(ci, "{0:0.000},{1:0.0},{2:0.00},{3:0.00},{4},{5:0.0},{6:0.000},{7:0.0},{8:0.00},{9:0.00},{10:0.00},{11:0.00},{12:0.0},{13:0.0},{14:0.0}\n",
                s.t, s.kmh, s.st, s.thr, s.hb ? 1 : 0, s.yr, s.ay, s.beta, s.pit, s.rol, s.sF, s.sR, s.x, s.z, s.yaw));
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, $"drive_{model}_{n}.csv"), sb.ToString());
        }
        IEnumerable<S> From(float t) => ser.Where(s => s.t >= t);

        IEnumerator Start()
        {
            dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"); System.IO.Directory.CreateDirectory(dir);
            yield return new WaitForSecondsRealtime(3); var mm = run.Split(':'); GameData.Load(); FBSession.Load(); var keepMap = FBSession.map; var keepMode = FBSession.mode;
            FBSession.map = mm[0]; FBSession.mode = mm.Length > 1 ? mm[1] : "classico"; FBSession.online = false; FBSession.view = "tps"; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_" + mm[0]); while (op != null && !op.isDone) yield return null; yield return new WaitForSecondsRealtime(5);
            FBMatch.God = true; foreach (var b in FBMatch.Bots.ToList()) if (b) b.gameObject.SetActive(false);
            me = FBMatch.Player; if (!me) { Log("sem jogador"); yield break; }
            // pista: caixa de 2,5 km no mapa (camada LMap), quadriculado de 4 m para ver o deslize nas fotos
            pad0 = new Vector3(me.Pos.x, me.Pos.y + 300, me.Pos.z);
            var pad = GameObject.CreatePrimitive(PrimitiveType.Plane); pad.name = "FB_DrivePad"; Destroy(pad.GetComponent<Collider>()); pad.layer = FBWorld.LMap;
            pad.transform.position = pad0; pad.transform.localScale = new Vector3(300, 1, 300);
            var bc = new GameObject("FB_DrivePadCol").AddComponent<BoxCollider>(); bc.gameObject.layer = FBWorld.LMap; bc.size = new Vector3(3000, 2, 3000); bc.transform.position = pad0 - Vector3.up;
            var tex = new Texture2D(64, 64) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) { bool c = ((x / 32) + (y / 32)) % 2 == 0, line = x % 32 == 0 || y % 32 == 0; tex.SetPixel(x, y, line ? new Color(.85f, .85f, .8f) : c ? new Color(.33f, .34f, .35f) : new Color(.27f, .28f, .29f)); }
            tex.Apply(); var sh = Shader.Find("Universal Render Pipeline/Lit"); var mat = new Material(sh ? sh : pad.GetComponent<Renderer>().sharedMaterial.shader);
            mat.mainTexture = tex; if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex); mat.mainTextureScale = new Vector2(3000 / 8f, 3000 / 8f); if (mat.HasProperty("_BaseMap")) mat.SetTextureScale("_BaseMap", new Vector2(3000 / 8f, 3000 / 8f));
            pad.GetComponent<Renderer>().sharedMaterial = mat; Physics.SyncTransforms();
            car = FBVehicles.Spawn(carId, pad0 + Vector3.up * .3f, 0); yield return null;
            if (!car) { Log("carro não nasceu: " + carId); yield break; }
            var cc = me.GetComponent<CharacterController>(); if (cc) cc.enabled = false; me.transform.position = pad0 + Vector3.right * 2; if (cc) cc.enabled = true; yield return Wait(.3f);
            if (!FBDrive.Enter(me, car)) { Log("não entrou no carro"); yield break; }
            yield return Wait(2.5f);
            FBPlayer.TestInput = inp => { inp.move = new Vector2(-cSt, cThr); inp.jumpHeld = cHb; inp.interact = false; inp.multimidia = false; return inp; };
            cam = new GameObject("DriveCam").AddComponent<Camera>(); cam.depth = 100; cam.cullingMask = Camera.main ? Camera.main.cullingMask : ~0; cam.nearClipPlane = .05f;
            Log($"carro {car.id} · tração {car.tracao ?? "-"} · entre-eixos {car.wheelbase / 1.2f:0.00} m · máxima {FBDrive.TopKmh(car):0} km/h");
            var models = which == "ambos" ? new[] { "antigo", "novo" } : new[] { which };
            var res = new List<string>();
            foreach (var md in models)
            {
                model = md; FBCarPhys.On = md == "novo"; string R(string n, string s) { var line = $"{md} · {n}: {s}"; res.Add(line); Log(line); return line; }
                // 1) arrancada
                Place(); cThr = 1; yield return Drive(14, _ => SpeedNow >= 100);
                float T60 = ser.FirstOrDefault(s => s.kmh >= 60).t, T100 = ser.FirstOrDefault(s => s.kmh >= 100).t; Csv("arrancada");
                R("arrancada", $"0-60 {T60:0.0} s · 0-100 {T100:0.0} s · arfagem máx {ser.Min(s => s.pit):0.0}°");
                // 2) frenagem de 100
                Place(); yield return Reach(100); var pB = car.transform.position; float tb = Time.time - t0; cThr = -1; bool shotB = false;
                yield return Drive(10, t => { if (!shotB && t > .35f) { shotB = true; Shot("frenagem", new Vector3(4.5f, 1.2f, 1)); } return SpeedNow < 1; });
                float dB = (car.transform.position - pB).magnitude / 1.2f; Csv("frenagem");
                R("frenagem 100-0", $"{dB:0.0} m em {Time.time - t0 - tb:0.0} s · nariz desce {From(tb).Max(s => s.pit):0.0}°");
                // 3) curva constante a 60 (volante todo à esquerda, acelerador segurando 60)
                Place(); yield return Reach(60); float tc = Time.time - t0; cSt = 1; bool shotC = false;
                for (float t = 0; t < 6; t += Time.deltaTime)
                {
                    cThr = Mathf.Clamp((60 - SpeedNow) * .15f, -.3f, 1); yield return null; Sample();
                    if (!shotC && t > 4) { shotC = true; Shot("curva60_tras", new Vector3(0, 1.6f, -6.5f)); }
                }
                var cs = From(tc + 4).ToList(); float ay = cs.Average(s => s.ay), kc = cs.Average(s => s.kmh), rad = Sq(kc / 3.6f) / Mathf.Max(.01f, ay * 9.8f); Csv("curva60");
                R("curva 60 (volante todo)", $"{kc:0} km/h · lateral {ay:0.00} g · raio {rad:0} m · giro {cs.Average(s => s.yr):0}°/s · β {cs.Average(s => s.beta):+0.0;-0.0}° · rolagem {cs.Average(s => s.rol):+0.0;-0.0}° · deriva T {cs.Average(s => s.sR):0.00}");
                // 4) degrau de volante a 80 (direita), em ponto morto (só a dirigibilidade)
                Place(); yield return Reach(80); float ts = Time.time - t0; cSt = -1; cThr = 0;
                for (float t = 0; t < 2.5f; t += Time.deltaTime) { yield return null; Sample(); }
                var ss = From(ts).ToList(); float fin = ss.Where(s => s.t > ts + 2).Average(s => -s.yr), pk = ss.Max(s => -s.yr), t90 = ss.FirstOrDefault(s => -s.yr >= .9f * fin).t - ts; Csv("degrau80");
                R("degrau de volante a 80", $"giro final {fin:0}°/s · pico {pk:0}°/s (passa {Mathf.Max(0, pk / Mathf.Max(1, fin) - 1) * 100:0} %) · 90 % em {t90:0.00} s · lateral {ss.Where(s => s.t > ts + 2).Average(s => -s.ay):0.00} g");
                // 5) troca de faixa a 90
                Place(); yield return Reach(90); float tl = Time.time - t0;
                foreach (var (st, d) in new[] { (1f, .45f), (-1f, .8f), (1f, .4f), (0f, 2.5f) })
                {
                    cSt = st; for (float t = 0; t < d; t += Time.deltaTime) { cThr = Mathf.Clamp((90 - SpeedNow) * .15f, -.3f, 1); yield return null; Sample(); }
                }
                var ls = From(tl).ToList(); var tail = ls.Where(s => s.t > ls[^1].t - 1).ToList(); Csv("trocafaixa");
                R("troca de faixa a 90", $"β máx {ls.Max(s => Mathf.Abs(s.beta)):0.0}° · lateral máx {ls.Max(s => Mathf.Abs(s.ay)):0.00} g · balanço no fim {tail.Max(s => Mathf.Abs(s.yr)):0.0}°/s · {(ls.Any(s => Mathf.Abs(s.beta) > 60) ? "RODOU" : "estável")}");
                // 6) freio de mão a 60: volante esquerda + freio de mão 0,9 s, solta, acelerador 0,4 reto
                Place(); yield return Reach(60); float th = Time.time - t0; float yaw0 = car.yaw; cSt = 1; cHb = true; cThr = 0; bool shotH = false;
                yield return Drive(.9f, t => { if (!shotH && t > .75f) { shotH = true; Shot("freiodemao", new Vector3(-3, 9, -4), 60); } return false; });
                cHb = false; cSt = 0; cThr = .4f; yield return Drive(3); var hs = From(th).ToList(); Csv("freiodemao");
                float turn = Mathf.Abs(Mathf.DeltaAngle(yaw0 * Mathf.Rad2Deg, hs.FirstOrDefault(s => s.t >= th + 1.5f).yaw));
                R("freio de mão a 60", $"β máx {hs.Max(s => Mathf.Abs(s.beta)):0}° · virou {turn:0}° em 1,5 s · deriva T máx {hs.Max(s => s.sR):0.00} · {(hs.Any(s => Mathf.Abs(s.beta) > 100) ? "RODOU" : "segurou")} · {SpeedNow:0} km/h no fim");
                // 7) freada na curva a 80
                Place(); yield return Reach(80); float tf = Time.time - t0; cSt = .6f; yield return Drive(1); cThr = -1; yield return Drive(3, _ => SpeedNow < 3);
                var fs = From(tf).ToList(); Csv("freadacurva");
                R("freada na curva a 80", $"β máx {fs.Max(s => Mathf.Abs(s.beta)):0.0}° · {(fs.Any(s => Mathf.Abs(s.beta) > 60) ? "RODOU" : "estável")} · rolagem máx {fs.Max(s => Mathf.Abs(s.rol)):0.0}° · nariz {fs.Max(s => s.pit):0.0}°");
                cThr = 0; cSt = 0; cHb = false;
            }
            System.IO.File.WriteAllLines(System.IO.Path.Combine(dir, "drive_resumo.txt"), res);
            FBPlayer.TestInput = null; FBCarPhys.On = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbfisicaantiga") < 0;
            FBDrive.Exit(me, true); yield return Wait(1);
            Log("fim"); FBSession.map = keepMap; FBSession.mode = keepMode; FBSession.Save(); yield return new WaitForSecondsRealtime(1); Application.Quit();
        }
        static float Sq(float x) => x * x;
    }
}
