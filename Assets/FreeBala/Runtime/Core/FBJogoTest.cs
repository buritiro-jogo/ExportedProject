// r50: TESTE NO CAMINHO REAL DO JOGO — FreeBala.exe -fbjogo [mapa] [-fbjogo_skins r_tutu,r_pm_almeida] [-fbjogo_veic SaveiroCross,mx_xre300]
//   [-fbjogo_lados porta,frente,direita,traseira,diagonal] [-fbjogo_partes garagem,veiculos,armas] [-fbjogo_antes 0]
// Pedido de 31/10: "não considere nenhuma correção concluída apenas porque aparenta funcionar no editor ou nas screenshots — as alterações
// precisam funcionar dentro do jogo compilado". Os laboratórios (FBVeicAnimLab) teleportavam o personagem exatamente na porta e chamavam o
// FBDrive direto, com câmera própria — nenhum passava pelo caminho de quem joga. Aqui: rolê aberto como pelo menu (prefs salvas: carro da
// garagem e a oficina dele), cada um dos 6 personagens jogáveis, sai do carro da garagem com F, ANDA até o veículo vindo de cada lado (entrada
// do FBPlayer, câmera do jogo), aperta F (FBPlayer → FBVehicles.Toggle → Nearest → FBDrive), senta, F para sair. Mede no fim de cada quadro:
// quadril dentro da caixa do veículo na ida/começo do clipe ("atravessou"), salto do quadril (> 3 m/s de gente), cabeça × teto, onde ficou em
// pé ao sair, e qual clipe o Animator toca de verdade. "antes" = FBDrive.ApproachOn desligado (como a r49n), no mesmo build.
// Armas: primária/SMG/secundária das prefs na mão e guardadas, comprimento ÷ altura do corpo × 1,75 m, fotos em 3ª e 1ª pessoa.
// Fotos JPG pela câmera do jogo em autoteste/jogo; linhas [FreeBala][jogo] no Player.log.
// r51: + postura depois de sair (inclinação quadril→cabeça final × Animator puro, quem mexe no tronco), "dano" (tiro pelo caminho real
// FBCombat.BulletFrom → DamageActor → HUD em alvo por parte × distância × colete, e paredes de teste) e "mira" (óptica 3ª→1ª pessoa,
// lente PiP, retículo × impacto, desempenho com/sem a câmera da lente).
// r50d: a câmera do jogo vai para trás do veículo no F (o corpo some nas fotos de entrar/sair) — essas fotos agora são de uma câmera lateral
// (de fora, à esquerda e à frente da porta). Mede também: qual clipe a camada de transição toca no meio (falha "clipe … não tocou" — a causa
// achada na r50c), velocidade do quadril por faixa de 5 % do clipe, perna (coxa do 30 % em diante e canela) × MALHA do veículo, e quantas
// vezes o FBDriverPose repôs o clipe. "antes" = ApproachOn e SysGuard desligados (exatamente como a r49n no mesmo build).
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBJogoTest : MonoBehaviour
    {
        public const string Rodada = "r51";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbjogo"); if (i < 0) return;
            var g = new GameObject("FB_JogoTeste"); DontDestroyOnLoad(g); var t = g.AddComponent<FBJogoTest>(); if (i + 1 < a.Length && !a[i + 1].StartsWith("-")) t.map = a[i + 1];
        }
        static string Arg(string k) { var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, k); return i >= 0 && i + 1 < a.Length && !a[i + 1].StartsWith("-") ? a[i + 1] : null; }
        static void Log(string s) => Debug.Log("[FreeBala][jogo] " + s);
        IEnumerator W(float s) { for (float w = 0; w < s; w += Time.unscaledDeltaTime) yield return null; }
        string map = "vila_jardim", dir; System.Action restore; Vector2 mv; bool press; Camera side; string pend; bool aimT, fireT, fireEdge, crouchEdge;
        const float U = 1.43f;                                                              // unidades de mundo por metro de gente (corpo médio 2,51 ÷ 1,75)
        void OnApplicationQuit() { restore?.Invoke(); FBPlayer.TestInput = null; }
        void Foto(string n) { var tex = ScreenCapture.CaptureScreenshotAsTexture(); if (!tex) return; File.WriteAllBytes(Path.Combine(dir, n + ".jpg"), tex.EncodeToJPG(80)); Destroy(tex); }   // só no fim do quadro
        static Transform Bone(FBCharacter a, HumanBodyBones b) => a && a.an && a.an.isHuman ? a.an.GetBoneTransform(b) : null;
        static bool InBody(FBCar car, Vector3 w, float shrinkW)
        {
            var T = car.transform; float ls = Mathf.Max(1e-4f, Mathf.Abs(T.lossyScale.x)), s = shrinkW / ls; var q = T.InverseTransformPoint(w); var b = car.lbox;
            return q.x > b.min.x + s && q.x < b.max.x - s && q.z > b.min.z + s && q.z < b.max.z - s && q.y < b.max.y && q.y > b.min.y - .2f / ls;
        }
        static float BoxDist(FBCar c, Vector3 p) { var lp = c.transform.InverseTransformPoint(p); var q = c.lbox.ClosestPoint(lp); var d = c.transform.TransformPoint(q) - p; d.y = 0; return d.magnitude; }
        static string Clips(Animator an)
        {
            if (!an) return "-"; var l = new List<string>();
            for (int i = 0; i < an.layerCount; i++) { float w = i == 0 ? 1 : an.GetLayerWeight(i); if (w < .5f) continue; var ci = an.GetCurrentAnimatorClipInfo(i); if (ci.Length > 0 && ci[0].clip) l.Add($"{i}:{ci[0].clip.name}({w:0.0})"); }
            return string.Join(" ", l);
        }

        IEnumerator Start()
        {
            dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste", "jogo"); Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir, "*.jpg")) File.Delete(old);
            side = new GameObject("JogoLadoCam").AddComponent<Camera>(); DontDestroyOnLoad(side.gameObject); side.depth = 100; side.enabled = false; side.nearClipPlane = .05f; side.fieldOfView = 42;
            yield return W(3); GameData.Load(); FBSession.Load();
            var asm = typeof(FBDrive).Assembly.Location;
            Log($"build: {asm} gravado {(File.Exists(asm) ? File.GetLastWriteTime(asm).ToString("dd/MM HH:mm:ss") : "?")} · {Rodada} · ida até a porta {(FBDrive.ApproachOn ? "LIGADA" : "DESLIGADA")} · animação de carro {FBDrive.CarAnims} · de moto {FBDrive.MotoAnims}");
            foreach (var c in new[] { FBDrive.CAR_IN, FBDrive.CAR_OUT, FBDrive.MOTO_MOUNT, FBDrive.MOTO_DISMOUNT, FBDrive.MOTO_RIDE })
            { var cl = FBRegistry.I ? FBRegistry.I.Clip(c) : null; Log($"  clipe {c}: {(cl ? $"{cl.length:0.00} s, {cl.frameRate:0} qps, humano {cl.humanMotion}" : "AUSENTE NA BUILD")} · caminho do quadril {(FBAnimPaths.Has(c) ? "sim" : "NÃO")}"); }
            var keep = (FBSession.map, FBSession.mode, FBSession.view, FBSession.skin, FBSession.look, FBSession.online);
            restore = () => { (FBSession.map, FBSession.mode, FBSession.view, FBSession.skin, FBSession.look, FBSession.online) = keep; FBSession.Save(); };
            Log($"prefs do jogador: skin {keep.skin} · modo {keep.mode} · armas {FBSession.primary}/{FBSession.smg}/{FBSession.secondary} · carro do rolê {PlayerPrefs.GetString("fb_role_car", "?")}");
            var skins = Arg("-fbjogo_skins")?.Split(',').ToList() ?? new List<string> { keep.skin };
            foreach (var s in FBCustomizeData.BetaChars.OrderBy(x => x)) if (!skins.Contains(s) && Arg("-fbjogo_skins") == null) skins.Add(s);
            skins = skins.Where(s => FBRegistry.I && FBRegistry.I.HasChar(s)).ToList();
            var partes = (Arg("-fbjogo_partes") ?? "garagem,veiculos,armas,dano,mira,pegada").Split(',');
            bool antes = Arg("-fbjogo_antes") != "0";
            FBPlayer.TestInput = inp => { inp.move = mv; inp.look = Vector2.zero; inp.interact = press; press = false; inp.fire = fireT; inp.firePressed = fireEdge; fireEdge = false; inp.aim = aimT; inp.aimPressed = false; inp.jump = inp.jumpHeld = false; inp.sprintToggle = inp.sprintHeld = false; inp.crouchToggle = crouchEdge; crouchEdge = false; inp.prone = false; inp.slot = -1; inp.cycle = 0; inp.toggleView = false; inp.reload = inp.heal = false; inp.leanL = leanQ; inp.leanR = leanE; return inp; };
            var tab = new List<(string skin, string veh, string lado, bool depois, List<string> fails)>();
            for (int si = 0; si < skins.Count; si++)
            {
                var sk = skins[si]; bool full = si < 2;
                FBSession.map = map; FBSession.mode = "role"; FBSession.online = false; FBSession.view = "tps"; FBSession.skin = sk; FBSession.look = ""; FBSession.Save();
                Log($"==== {sk} ({FBCustomizeData.PrettyName(sk, sk)}) · carregando {map} no rolê");
                var op = SceneManager.LoadSceneAsync("Map_" + map); if (op == null) { Log("FALHA: cena não existe"); break; }
                while (!op.isDone) yield return null; yield return W(6);
                var me = FBMatch.Player; var P = FBPlayer.I; if (!me || !P) { Log("sem jogador"); continue; }
                FBMatch.God = true; FBDrive.ApproachOn = true;
                Log($"{sk}: animator {(me.an && me.an.runtimeAnimatorController ? me.an.runtimeAnimatorController.name : "-")} · avatar {(me.an && me.an.avatar ? $"{me.an.avatar.name} humano {me.an.avatar.isHuman} válido {me.an.avatar.isValid}" : "-")} · escala {me.transform.lossyScale.y:0.000} · scaleK {me.ScaleK:0.000} · altura {FBEscalaArmaLab.MeshY(me).hi - FBEscalaArmaLab.MeshY(me).lo:0.00}");
                // ---- 1) garagem: já sentado na prévia (r38), sem clipe de entrar tocando do meio do carro (r50)
                if (partes.Contains("garagem"))
                {
                    var c0 = FBVehicles.CarOf(me); var dp0 = me.GetComponent<FBDriverPose>(); var hp = Bone(me, HumanBodyBones.Hips);
                    Log($"{sk}: garagem aberta {FBGarage.Open_} · no carro {(c0 ? c0.id : "NÃO")} · transição {(dp0 ? dp0.trans ?? "nenhuma" : "-")} · quadril a {(c0 && hp ? (hp.position - c0.L2W(c0.seat)).magnitude : -1):0.00} do banco · Animator: {Clips(me.an)}");
                    yield return new WaitForEndOfFrame(); Foto($"{sk}_0_garagem");
                }
                if (FBGarage.Open_) { FBGarage.AutoPick(); yield return W(2); }
                var roleCar = FBVehicles.CarOf(me);
                if (roleCar && partes.Contains("garagem")) { var f = new List<string>(); yield return Sair(me, roleCar, $"{sk}_1_garagem_{roleCar.id}", f); tab.Add((sk, roleCar.id + " (garagem)", "sair", true, f)); }
                if (FBVehicles.CarOf(me)) { FBDrive.Exit(me, true); yield return W(1); }
                // ---- 2) veículos vindo de cada lado
                if (partes.Contains("veiculos"))
                {
                    if (!FBRoads.Ready) FBRoads.Load(FBSession.map);
                    var spot = FBRoads.Ready ? FBRoads.spots.Where(x => !FBVehicles.list.Any(c => c && (c.transform.position - x.p).sqrMagnitude < 12 * 12) && OpenAround(x.p)).OrderBy(x => (x.p - me.Pos).sqrMagnitude).FirstOrDefault() : null;
                    Vector3 sp = spot != null ? spot.p : me.Pos + me.transform.forward * 8; float syaw = spot != null ? spot.yaw : 0;
                    Log($"{sk}: veículos na vaga {(spot != null ? spot.kind : "frente do jogador")} a {(sp - me.Pos).magnitude:0} m (rumo {syaw:0})");
                    var vlist = Arg("-fbjogo_veic")?.Split(',').ToList() ?? (full ? new List<string> { "SaveiroCross", "S10_2017", "mx_xre300", "mx_gs1250", "mx_pop110" } : new List<string> { "SaveiroCross", "mx_xre300" });
                    var lados = Arg("-fbjogo_lados")?.Split(',').ToList() ?? (full ? new List<string> { "porta", "frente", "direita", "traseira", "diagonal" } : new List<string> { "porta", "direita", "frente" });
                    foreach (var vid in vlist)
                        foreach (var lado in lados)
                            foreach (bool dep in antes && si == 0 && (lado == "direita" || lado == "frente") && (vid == "SaveiroCross" || vid == "mx_xre300") ? new[] { false, true } : new[] { true })
                            {
                                var car = Spawn(vid, sp, syaw); if (!car) { Log($"{vid} não nasceu"); break; }
                                yield return W(1.2f); var f = new List<string>(); FBDrive.ApproachOn = dep; FBDrive.SysGuard = dep; FBDriverPose.ClockSync = dep;
                                yield return Ciclo(me, P, car, lado, $"{sk}_{vid}_{lado}{(dep ? "" : "_antes")}", f);
                                FBDrive.ApproachOn = true; FBDrive.SysGuard = true; FBDriverPose.ClockSync = true; tab.Add((sk, vid, lado, dep, f)); if (lado == "armado" || lado == "fuzil") P.wu.Strip();
                                if (FBVehicles.CarOf(me)) { FBDrive.Exit(me, true); yield return W(.5f); }
                                Kill(car); yield return W(.4f);
                            }
                }
                // ---- 3) armas do jogador (prefs) na mão e guardadas, 3ª e 1ª pessoa
                if (partes.Contains("armas")) yield return Armas(me, P, sk, si == 0);
                PosturaFim(sk);
                if (si == 0 && partes.Contains("dano")) yield return Dano(me, P);
                if (si == 0 && partes.Contains("mira")) yield return Mira(me, P);
                if (si == 0 && partes.Contains("espreitar")) yield return Espreitar(me, P);
                if ((si == 0 || Arg("-fbjogo_pegada_todos") != null) && partes.Contains("pegada")) yield return Pegada(me, P, sk);
            }
            if (partes.Contains("compra")) yield return Compra();
            foreach (var g in tab.GroupBy(x => (x.veh.Contains("garagem") ? "garagem" : x.veh, x.depois)))
                Log($"TABELA {g.Key.Item1} {(g.Key.depois ? "DEPOIS" : "ANTES")}: {g.Count(x => x.fails.Count == 0)}/{g.Count()} sem defeito · {string.Join(", ", g.SelectMany(x => x.fails).GroupBy(s => s).OrderByDescending(k => k.Count()).Select(k => $"{k.Key} {k.Count()}"))}");
            var dps = tab.Where(x => x.depois).ToList(); var ant = tab.Where(x => !x.depois).ToList();
            Log($"RESUMO: depois {dps.Count(x => x.fails.Count == 0)}/{dps.Count} sem defeito · antes (sem ida até a porta e sem a proteção do clipe) {ant.Count(x => x.fails.Count == 0)}/{ant.Count} · idas {FBDrive.Approaches}, desistências {FBDrive.ApproachFallbacks} · clipe reposto pelo FBDriverPose {FBDrive.SysRestores}× (último: {FBDrive.SysRestoreLast ?? "-"}) · relógio da transição pelo clipe {(FBDriverPose.ClockSync ? "LIGADO" : "desligado")} ({FBDriverPose.ClockSyncs} quadros sincronizados, {FBDriverPose.ClockWaits} esperando o clipe)");
            FBPlayer.TestInput = null; restore?.Invoke(); restore = null; Log("fim"); yield return W(1); Application.Quit(0);
        }
        static bool OpenAround(Vector3 p)
        {   // vaga com ~5 m livres em volta (para chegar de todos os lados sem muro/poste no caminho)
            foreach (var h in Physics.OverlapBox(p + Vector3.up * 1.2f, new Vector3(4.5f, .8f, 4.5f), Quaternion.identity, FBWorld.SolidMask, QueryTriggerInteraction.Ignore))
                if (!h.GetComponentInParent<FBCar>() && !h.GetComponentInParent<FBCharacter>()) return false;
            return true;
        }
        FBCar Spawn(string vid, Vector3 at, float yaw)
        {
            if (FBWorld.GroundY(at.x, at.y + 3, at.z, .4f, 8) is float g) at.y = g;
            var c = FBVehicles.Spawn(vid, at + Vector3.up * .3f, yaw); if (!c) return null; c.syncId = "jogo:" + vid + ":" + Time.frameCount;
            if (!c.mx && c.kind != "moto") { var td = FBTune.Load(vid); FBTune.Apply(c, td); }                           // a oficina salva do jogador (SaveiroCross: kit completo)
            return c;
        }
        void Kill(FBCar c) { if (!c) return; if (c.mx) c.mx.SetActive(false); FBVehicles.list.Remove(c); Destroy(c.gameObject); }
        // câmera lateral: de fora, à esquerda e à frente da porta, olhando o caminho porta→banco; a foto sai no fim do PRÓXIMO quadro (Flush)
        void Lado(FBCar car, FBCharacter me, string n)
        {
            if (!side || !car) return; var T = car.transform; float k = me.ScaleK; bool moto = car.mx || car.kind == "moto";
            var tgt = Vector3.Lerp(FBDrive.DoorOut(car, k), FBDrive.SeatHips(car), .5f); tgt.y = T.position.y + (moto ? 1.0f : 1.15f) * k;
            side.cullingMask = Camera.main ? Camera.main.cullingMask : ~0;
            side.transform.position = tgt - T.right * (moto ? 2.7f : 3.4f) * k + T.forward * (moto ? 1.5f : -1.9f) * k + Vector3.up * (moto ? .45f : .7f) * k; side.transform.LookAt(tgt);   // carro: atrás da porta (ela abre para a frente)
            side.enabled = true; pend = n;
        }
        void Flush() { if (pend != null) { Foto(pend); pend = null; } if (side) side.enabled = false; }
        IEnumerator LadoJa(FBCar car, FBCharacter me, string n) { Lado(car, me, n); yield return null; yield return new WaitForEndOfFrame(); Flush(); }
        static int LegHits(FBCharacter me, FBCar car)
        {   // pernas (coxa do 30 % em diante — perto do quadril ela encosta no banco de verdade — e canela) × malha do veículo: quantas atravessam
            int n = 0; if (!FBLegClear.Arm(car, true)) return 0;
            try
            {
                foreach (var (u, l, f) in new[] { (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot), (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot) })
                { Transform U = Bone(me, u), L = Bone(me, l), F = Bone(me, f); if (U && L && F && (FBLegClear.Hits(Vector3.Lerp(U.position, L.position, .3f), L.position) || FBLegClear.Hits(L.position, F.position))) n++; }
            }
            finally { FBLegClear.Arm(car, false); }
            return n;
        }
        static string Faixas(float[] vb) => string.Join(" ", vb.Select(x => x.ToString("0.0")));
        static readonly bool Quadros = Arg("-fbjogo_quadros") != null;
        // r50: quadro a quadro da transição — o quadril final × onde o caminho (FBDrive, linha do warp) o deixou neste quadro
        static void Quadro(string nm, string fase, FBCharacter me, FBDriverPose dp, float kk, float dt, float v, Vector3 h)
        {
            if (!Quadros) return; var an = me.an; int l2 = an ? an.GetLayerIndex("Emote Full Body") : -1; var st = l2 >= 0 ? an.GetCurrentAnimatorStateInfo(l2) : default; var ci = l2 >= 0 ? an.GetCurrentAnimatorClipInfo(l2) : null;   // [unificação 02/11] pelo nome: a camada "BUR Tronco" (índice 2) empurrou as outras
            Log($"Q {nm} {fase} k={kk:0.000} dt={dt * 1000:0}ms v={v:0.0} caminho={(FBDrive.WarpFrame == Time.frameCount ? "sim" : "NAO")} desvio={(h - FBDrive.WarpHips).magnitude * 100:0.0}cm pés={dp.feetLift * 100:0.0}cm cab={dp.duck * 100:0.0}cm L2={st.normalizedTime:0.000} p2={(l2 >= 0 ? an.GetLayerWeight(l2) : 0):0.00} {(ci != null && ci.Length > 0 && ci[0].clip ? ci[0].clip.name : "-")} raiz={me.transform.position.x:0.000},{me.transform.position.y:0.000},{me.transform.position.z:0.000} quadril={h.x:0.000},{h.y:0.000},{h.z:0.000}");
        }

        // um ciclo: anda até o veículo vindo de 'lado', F, senta, F, sai
        IEnumerator Ciclo(FBCharacter me, FBPlayer P, FBCar car, string lado, string nm, List<string> fails)
        {
            var T = car.transform; float k = me.ScaleK; var c = T.TransformPoint(car.lbox.center); c.y = T.position.y;
            Vector3 ext = Vector3.Scale(car.lbox.extents, T.lossyScale);
            Vector3 d = lado switch { "frente" => T.forward, "direita" => T.right, "traseira" => -T.forward, "diagonal" => (T.forward + T.right).normalized, _ => -T.right };
            float reach = Mathf.Abs(Vector3.Dot(d, T.right)) * ext.x + Mathf.Abs(Vector3.Dot(d, T.forward)) * ext.z;
            var start = c + d.Flat().normalized * (reach + 1.6f * k); if (FBWorld.GroundY(start.x, start.y + 2, start.z, .4f, 5) is float gy) start.y = gy;
            float yaw = Quaternion.LookRotation((c - start).Flat()).eulerAngles.y;
            FBMatch.Teleport(me, start, yaw); P.camYaw = yaw; P.camPitch = 8; yield return W(.8f);
            // r51: "armado" (pistola) / "fuzil" (m4) = o mesmo caminho com a arma na mão (rolê do PM armado); os dois dirigem antes de sair
            string armaJ = lado == "armado" ? Arg("-fbjogo_arma") ?? GameData.Weapons.Values.Where(x => x.cls == "pistola" && FBRegistry.I.Weapon(x.id)).Select(x => x.id).OrderBy(x => x).FirstOrDefault() : lado == "fuzil" ? "m4" : null;
            if (armaJ != null)
            {
                P.wu.Strip(); P.wu.Give(new[] { armaJ }); yield return W(.3f); int sl = System.Array.FindIndex(P.wu.slots, x => x != null && x.id == armaJ); if (sl >= 0) P.wu.Equip(sl, true);
                yield return W(1.4f); Log($"{nm}: armado com {armaJ} (na mão {(me.weaponGO && me.weaponGO.activeInHierarchy)}, combate {me.combat})");
            }
            FBPostProbe.Watch(me); yield return new WaitForEndOfFrame(); { var ln = Postura(me, P, out var fi, out _, out var ro); if (armaJ == null) postRef.Add((fi.f, fi.s, ro)); postRefBy[nm] = (fi.f, fi.s, ro); Log($"{nm} · POSTURA de pé antes: {ln}"); }
            if (lado == "dirigir" || armaJ != null)
            {   // r51e: a saída é medida com a câmera a −24° (alta, como a do veículo); a referência justa é o MESMO personagem de pé com a mesma câmera —
                // senão o desvio inclui o efeito da câmera (20 % do pitch fora de ação, FBRig.RelaxPitchK) e não só o que a saída deixou
                float cp0 = P.camPitch; P.camPitch = -24; yield return W(1.2f); yield return new WaitForEndOfFrame();
                var ln2 = Postura(me, P, out var fi2, out _, out var ro2); postRefBy[nm] = (fi2.f, fi2.s, ro2); Log($"{nm} · POSTURA de pé antes, câmera −24° (referência): {ln2}");
                P.camPitch = cp0; yield return W(.4f);
            }
            // anda até encostar (0,5–0,8 m de gente da lataria): como quem chega e aperta F
            float press0 = (lado == "porta" ? .55f : .7f) * U * .6f * k; mv = Vector2.up; float tw = 0;
            while (tw < 6 && (BoxDist(car, me.Pos) > press0 || FBVehicles.Nearest(me.Pos, 2.2f) != car)) { P.camYaw = Quaternion.LookRotation((c - me.Pos).Flat()).eulerAngles.y; tw += Time.deltaTime; yield return null; }
            bool holdW = lado == "traseira" || lado == "diagonal";                            // chega andando e aperta F com o W ainda seguro (solta 0,4 s depois)
            if (!holdW) { mv = Vector2.zero; yield return W(.35f); }
            if (FBVehicles.Nearest(me.Pos, 2.2f) != car) { mv = Vector2.zero; fails.Add("não chegou perto"); Log($"{nm}: não chegou perto ({BoxDist(car, me.Pos):0.00} da lataria)"); yield break; }
            yield return new WaitForEndOfFrame(); Foto(nm + "_0_antesF");
            if (holdW) StartCoroutine(SoltaW(.4f));
            float dDoor0 = (FBDrive.DoorOut(car, k) - me.Pos).Flat().magnitude;
            FBDrive.PathDiag = null; FBDrive.ApproachDiag = null; FBDrive.DuckMax = 0; FBDrive.Ducks = 0; FBDrive.DuckLeft = 0; int sr0 = FBDrive.SysRestores;
            press = true; var vb = new float[20]; int legF = 0; bool moto = car.mx || car.kind == "moto"; string want = moto ? FBDrive.MOTO_MOUNT : FBDrive.CAR_IN;
            // ---- ida + entrada
            var hips = Bone(me, HumanBodyBones.Hips); Vector3 hp = hips ? hips.position : me.Pos; float t = 0, tIda = 0, vMax = 0, vMaxT = 0, idaLen = 0; int idaIn = 0, entIn = 0; string clipMid = null, vPh = "";
            bool sawAppr = false, sawTrans = false; int shot = 0; float[] shotK = { .2f, .45f, .7f }; Vector3 lastRoot = me.Pos;
            while (t < 14)
            {
                yield return new WaitForEndOfFrame(); Flush(); float dt = Mathf.Max(1e-4f, Time.deltaTime); t += dt;
                var dp = me.GetComponent<FBDriverPose>(); bool appr = FBDrive.Approaching(me); string tr = dp ? dp.trans : null;
                var h = hips ? hips.position : me.Pos; float v = (h - hp).magnitude / dt / U; hp = h;
                if (appr) { sawAppr = true; tIda += dt; idaLen += (me.Pos - lastRoot).Flat().magnitude; if (InBody(car, h, .03f)) idaIn++; if (Mathf.Abs(tIda - .6f) < dt) Foto(nm + "_1_indo"); }
                lastRoot = me.Pos;
                if (tr == "mount")
                {
                    float kk = dp.transLen > 0 ? dp.transT / dp.transLen : 1; sawTrans = true;
                    if (v > vMax) { vMax = v; vMaxT = kk; vPh = "entrada"; }                     // inclui o 1º quadro: o pulo do PathInit (> 1,6 m da porta) é nele
                    int bi = Mathf.Clamp((int)(kk * 20), 0, 19); vb[bi] = Mathf.Max(vb[bi], v); Quadro(nm, "entra", me, dp, kk, dt, v, h);
                    if (kk < .3f && InBody(car, h, .03f)) entIn++;
                    if (kk < .65f && LegHits(me, car) > 0) legF++;
                    if (clipMid == null && kk > .5f) clipMid = Clips(me.an);
                    if (shot < shotK.Length && kk >= shotK[shot] && pend == null) { Lado(car, me, $"{nm}_{2 + shot}_entra{(int)(shotK[shot] * 100)}"); shot++; }
                }
                else if (appr && v > vMax && tIda > .1f) { vMax = v; vPh = "ida"; }
                if (FBVehicles.CarOf(me) == car && tr == null && t > .3f) break;
            }
            Flush(); bool seated = FBVehicles.CarOf(me) == car;
            yield return W(.8f); yield return new WaitForEndOfFrame(); Foto(nm + "_5_sentado"); yield return LadoJa(car, me, nm + "_5_sentado_lado");
            string seatInfo = car.mx || car.kind == "moto" ? "" : $" · cabeça abaixou até {FBDrive.DuckMax * 100:0} cm, topo acima do teto até {FBDrive.DuckLeft * 100:0} cm";
            if (car.mx && seated)
            {   // r50: parado com o pé esquerdo no chão — coxa (do 30 %) e canela não podem atravessar a malha da moto
                yield return W(.5f); yield return new WaitForEndOfFrame(); Transform ul = Bone(me, HumanBodyBones.LeftUpperLeg), ll = Bone(me, HumanBodyBones.LeftLowerLeg), ft = Bone(me, HumanBodyBones.LeftFoot); bool lh = false;
                if (ul && ll && ft && FBLegClear.Arm(car, true)) { try { lh = FBLegClear.Hits(Vector3.Lerp(ul.position, ll.position, .3f), ll.position) || FBLegClear.Hits(ll.position, ft.position); } finally { FBLegClear.Arm(car, false); } }
                seatInfo = $" · perna de apoio {(lh ? "ATRAVESSA a moto" : "livre")} (pé afastado até {FBMxBike.GroundPushMax * 100:0} cm)"; if (lh) fails.Add("perna de apoio dentro da moto"); FBMxBike.GroundPushMax = 0;
                Foto(nm + "_5_sentado_apoio");
            }
            bool clipOk = clipMid != null && clipMid.Contains(want);
            Log($"{nm}: começou a {dDoor0:0.00} da porta · ida {(sawAppr ? $"{tIda:0.00} s, {idaLen:0.00} un ({FBDrive.ApproachDiag})" : "nenhuma")} · Animator no meio: {clipMid ?? "-"} (clipe de entrar {(clipOk ? "TOCANDO" : "NÃO TOCOU")}, reposto {FBDrive.SysRestores - sr0}×) · quadril dentro da caixa: ida {idaIn} quadros, começo do clipe {entIn} · perna × malha até 65 % do clipe: {legF} quadros · salto máx {vMax:0.0} m/s ({vPh}{(vPh == "entrada" ? $" em {vMaxT * 100:0}%" : "")}) · v/5%: {Faixas(vb)} · sentou {seated}{seatInfo} · caminho: {FBDrive.PathDiag}");
            if (!seated) fails.Add("não sentou");
            if (!clipOk) fails.Add("clipe de entrar não tocou");
            if (idaIn > 2) fails.Add("atravessou na ida");
            if (entIn > 2 && !moto) fails.Add("atravessou no começo do clipe");
            if (legF > 4) fails.Add("perna atravessa entrando");
            if (vMax > (vPh == "ida" ? 5f : 3.2f)) fails.Add($"salto ({vPh})");                      // ida = a caminhada normal do jogo (quadril 3–3,7 m/s de gente)
            if (FBDrive.DuckLeft > .04f) fails.Add("cabeça no teto");
            if (!seated) yield break;
            bool pressed = false;
            if (lado == "dirigir" || armaJ != null)
            {   // r51: como o jogador da foto — anda 3 s virando um pouco, solta o W e aperta F ainda rodando (saída pedida em movimento: "PARANDO
                // PARA SAIR", o veículo freia sozinho e sai pelo clipe); câmera alta olhando para baixo, como na foto
                mv = new Vector2(.3f, 1); yield return W(3); float vTop = Mathf.Abs(car.v); mv = Vector2.zero; float tc = 0;
                while (tc < 5 && Mathf.Abs(car.v) > 4.5f) { tc += Time.deltaTime; yield return null; }
                Log($"{nm}: dirigiu 3 s (até {vTop / U * 3.6f:0} km/h), apertou F a {Mathf.Abs(car.v) / U * 3.6f:0.0} km/h");
                P.camPitch = -24; press = true; pressed = true;
            }
            yield return Sair(me, car, nm, fails, pressed);
        }
        IEnumerator SoltaW(float s) { yield return W(s); mv = Vector2.zero; }
        IEnumerator Sair(FBCharacter me, FBCar car, string nm, List<string> fails, bool pressed = false)
        {
            if (!pressed) { yield return W(.6f); press = true; }
            var hips = Bone(me, HumanBodyBones.Hips); Vector3 hp = hips ? hips.position : me.Pos; float t = 0, vMax = 0, vT = 0; int shot = 0; float[] shotK = { .3f, .6f, .9f }; bool sawTrans = false; string clipMid = null;
            var vb = new float[20]; int legF = 0; bool moto = car.mx || car.kind == "moto"; string want = moto ? FBDrive.MOTO_DISMOUNT : FBDrive.CAR_OUT;
            while (t < 12)
            {
                yield return new WaitForEndOfFrame(); Flush(); float dt = Mathf.Max(1e-4f, Time.deltaTime); t += dt;
                var dp = me.GetComponent<FBDriverPose>(); string tr = dp ? dp.trans : null; var h = hips ? hips.position : me.Pos; float v = (h - hp).magnitude / dt / U; hp = h;
                if (tr == "dismount")
                {
                    float kk = dp.transLen > 0 ? dp.transT / dp.transLen : 1; if (sawTrans && v > vMax) { vMax = v; vT = kk; } sawTrans = true;
                    int bi = Mathf.Clamp((int)(kk * 20), 0, 19); vb[bi] = Mathf.Max(vb[bi], v); Quadro(nm, "sai", me, dp, kk, dt, v, h);
                    if (kk > .35f && LegHits(me, car) > 0) legF++;
                    if (clipMid == null && kk > .5f) clipMid = Clips(me.an);
                    if (shot < shotK.Length && kk >= shotK[shot] && pend == null) { Lado(car, me, $"{nm}_{6 + shot}_sai{(int)(shotK[shot] * 100)}"); shot++; }
                }
                else if (sawTrans && t > .2f && v > vMax) { vMax = v; vT = 1; }
                if (!FBVehicles.CarOf(me) && tr == null && t > .2f) break;
            }
            for (int i = 0; i < 6; i++) { yield return new WaitForEndOfFrame(); Flush(); var h = hips ? hips.position : me.Pos; float v = (h - hp).magnitude / Mathf.Max(1e-4f, Time.deltaTime) / U; hp = h; if (v > vMax) { vMax = v; vT = 1.01f; } }
            yield return W(.5f); yield return new WaitForEndOfFrame(); Foto(nm + "_9_fora");
            bool outOk = !FBVehicles.CarOf(me); var hp2 = hips ? hips.position : me.Pos; bool inside = InBody(car, hp2, .05f) || InBody(car, me.Pos + Vector3.up * .5f, .05f);
            float gy = FBWorld.GroundY(me.Pos.x, me.Pos.y + .5f, me.Pos.z, .3f, 2) ?? me.Pos.y;
            bool clipOk = !sawTrans || (clipMid != null && clipMid.Contains(want));
            Log($"{nm} · SAIR: clipe {(sawTrans ? (clipOk ? "TOCANDO" : "NÃO TOCOU") : "NÃO (saída direta)")} · perna × malha depois de 35 %: {legF} quadros · v/5%: {Faixas(vb)} · fora {outOk} · em pé a {(FBDrive.DoorOut(car, me.ScaleK) - me.Pos).Flat().magnitude:0.00} da porta, {(me.Pos.y - gy) * 100:0} cm do chão, dentro da caixa {inside} · salto máx {vMax:0.0} m/s em {vT * 100:0}% · Animator no meio: {clipMid} · {FBDrive.DoorDiag}");
            if (!clipOk) fails.Add("clipe de sair não tocou"); if (legF > 4) fails.Add("perna atravessa saindo");
            if (!outOk) fails.Add("não saiu"); if (inside) fails.Add("ficou dentro do veículo"); if (vMax > 3.2f) fails.Add("salto na saída"); if (Mathf.Abs(me.Pos.y - gy) > .15f) fails.Add("fora do chão ao sair");
            // r51: postura depois de sair (pedido: "permanece com o corpo inclinado") — +0,6 s, +1,2 s, +3 s parado e depois de andar 1 s
            if (!outOk) yield break; FBPostProbe.Watch(me); postFails[nm] = fails; var PP = FBPlayer.I;
            foreach (var (fase, at) in new[] { ("+0,6s", 0f), ("+1,2s", .6f), ("+3s", 1.8f) })
            {
                if (at > 0) yield return W(at); yield return new WaitForEndOfFrame();
                var ln = Postura(me, PP, out var fi, out var ra, out var ro); post.Add((nm, fase, fi.f, fi.s, ra.f, ra.s, ro)); Log($"{nm} · POSTURA {fase}: {ln}");
                if (fase == "+1,2s") yield return CorpoLado(me, car, nm + "_9b_postura");
            }
            mv = Vector2.up; yield return W(1f); mv = Vector2.zero; yield return W(.8f); yield return new WaitForEndOfFrame();
            { var ln = Postura(me, PP, out var fi, out var ra, out var ro); post.Add((nm, "andou", fi.f, fi.s, ra.f, ra.s, ro)); Log($"{nm} · POSTURA depois de andar: {ln}"); }
        }

        // armas: comprimento proporcional ao corpo (L ÷ altura × 1,75), na mão e guardadas, fotos pela câmera do jogo (3ª) e na 1ª pessoa
        IEnumerator Armas(FBCharacter me, FBPlayer P, string sk, bool fp)
        {
            if (FBVehicles.CarOf(me)) { FBDrive.Exit(me, true); yield return W(1); }
            var ids = (Arg("-fbjogo_armas")?.Split(',') ?? new[] { FBSession.primary, FBSession.smg, FBSession.secondary }).Where(x => x != null && GameData.Weapons.ContainsKey(x) && FBRegistry.I.Weapon(x)).ToArray(); if (ids.Length == 0) { Log("sem armas nas prefs"); yield break; }
            var (lo, hi) = FBEscalaArmaLab.MeshY(me); float H = hi - lo;
            P.wu.Strip(); P.wu.Give(ids); yield return W(.5f);
            var side = new GameObject("JogoArmaCam").AddComponent<Camera>(); side.depth = 100; side.cullingMask = Camera.main ? Camera.main.cullingMask : ~0; side.enabled = false; side.nearClipPlane = .05f; side.fieldOfView = 38;
            foreach (var id in ids)
            {
                if (Arg("-fbjogo_armas") != null) { P.wu.Strip(); P.wu.Give(new[] { id }); yield return W(.3f); }   // lista avulsa: uma de cada vez (duas SMG disputariam o mesmo espaço)
                int sl = System.Array.FindIndex(P.wu.slots, x => x != null && x.id == id); if (sl < 0) { Log($"{sk}: ARMA {id} sem espaço no inventário"); continue; } P.wu.Equip(sl, true); yield return W(1.6f);
                var (L, _) = FBEscalaArmaLab.Len(me.weaponGO); var hol = me.HolsterObjects.Where(g => g && g.activeInHierarchy).Select(g => $"{g.name.Replace("Coldre_", "")} {FBEscalaArmaLab.Len(g).len / H * 1.75f:0.00} m").ToList();
                Log($"{sk}: ARMA {id} na mão {L / H * 1.75f:0.00} m de gente (escala de mundo {(me.weaponGO ? me.weaponGO.transform.lossyScale.x : 0):0.000}, ajuste do modelo {FBCharacter.WeaponModelFix(id):0.00}) · guardadas: {string.Join(", ", hol)} · corpo {H:0.00}");
                yield return new WaitForEndOfFrame(); Foto($"{sk}_arma_{id}_3p");
                var hp = (Bone(me, HumanBodyBones.Hips) ? Bone(me, HumanBodyBones.Hips).position : me.Pos + Vector3.up) + Vector3.up * .25f; var f = me.transform.forward.Flat().normalized; var r = Vector3.Cross(Vector3.up, f);
                side.enabled = true; side.transform.position = hp + f * 2.4f + r * 1.3f + Vector3.up * .25f; side.transform.LookAt(hp); yield return null; yield return new WaitForEndOfFrame(); Foto($"{sk}_arma_{id}_lado"); side.enabled = false;
                if (fp) { P.ForceFirstPerson(); yield return W(1.2f); yield return new WaitForEndOfFrame(); Foto($"{sk}_arma_{id}_1p"); P.ForceThirdPerson(); yield return W(.6f); }
            }
            Destroy(side.gameObject);
        }

        // ================================================================ r51: fase de compra (4V4 30 s, Farinha × Buriti 25 s) e raio da área de combate
        IEnumerator Compra()
        {
            foreach (var mode in (Arg("-fbjogo_modos") ?? "squad,pvl,classico").Split(','))
            {
                FBSession.map = map; FBSession.mode = mode; FBSession.online = false; FBSession.view = "tps"; FBSession.Save();
                Log($"==== COMPRA: carregando {map} no modo {mode}");
                var op = SceneManager.LoadSceneAsync("Map_" + map); if (op == null) yield break; while (!op.isDone) yield return null;
                float t0 = Time.time; while ((!FBMatch.I || FBMatch.I.state != "play") && Time.time - t0 < 40) yield return null;
                var G = FBMatch.I; if (!G) { Log($"COMPRA {mode}: partida não começou"); continue; }
                var ar = G.arena; float r0 = FBArena.RADIUS.TryGetValue(mode, out var rr) ? rr : 0;
                Log($"COMPRA {mode}: área {(ar != null ? $"raio {ar.r:0} m (antes {r0:0} m) · mapa {FBArena.MapRadius:0} m · {ar.why} · zona {(G.mode?.Zone != null ? $"{G.mode.Zone.r:0} m" : "-")}" : "sem área (modo sem área de combate)")} · compra longa {(FBEconomy.LongBuy ? "sim" : "não (-fbcompracurta)")}");
                if (!FBEconomy.Econ) continue;
                float tb = FBEconomy.buyTotal, left0 = FBEconomy.buyT, start = Time.time; var marks = new List<string>(); int lastS = -1; bool frozen = G.freeze, shop = FBShop.Open;
                while (FBEconomy.buying && Time.time - start < 60)
                {
                    int sec = Mathf.CeilToInt(FBEconomy.buyT);
                    if (sec != lastS && (sec % 5 == 0 || sec <= 3)) marks.Add($"{sec}s: HUD \"{FBHUD.I?.BoxText}\" · loja \"{FBShop.TimeText}\" barra {FBShop.BarFill:0.00} · placar \"{Mathf.CeilToInt(FBEconomy.buyT)}\"");
                    lastS = sec; frozen &= G.freeze; yield return null;
                }
                float took = Time.time - start; yield return W(.4f);
                bool ok = Mathf.Abs(took - left0) < .6f && frozen && !G.freeze;
                Log($"COMPRA {mode}: fase de {tb:0} s (restavam {left0:0.0} s quando a partida abriu, terminou em {took:0.0} s) · loja aberta {shop} · todos parados na compra {frozen} · rodada liberou {!G.freeze} {(ok ? "ok" : "FALHA")} · {string.Join(" | ", marks)}");
            }
        }

        // ================================================================ r51: postura depois de sair do veículo
        readonly List<(string nm, string fase, float f, float s, float rf, float rs, float ro)> post = new(); readonly List<(float f, float s, float ro)> postRef = new();
        readonly Dictionary<string, List<string>> postFails = new(); readonly Dictionary<string, (float f, float s, float ro)> postRefBy = new();   // referência do PRÓPRIO ciclo (armado × armado)
        static (float f, float s) Tilt(FBCharacter me, Vector3 v)
        {   // inclinação do vetor (graus): + frente = para a frente do corpo, + lado = para a direita
            var fw = me.transform.forward.Flat().normalized; var rt = Vector3.Cross(Vector3.up, fw); float y = Mathf.Max(1e-4f, Vector3.Dot(v, Vector3.up));
            return (Mathf.Atan2(Vector3.Dot(v, fw), y) * Mathf.Rad2Deg, Mathf.Atan2(Vector3.Dot(v, rt), y) * Mathf.Rad2Deg);
        }
        // no fim do quadro: tronco final (quadril→cabeça e quadril→peito) × o mesmo no Animator puro (FBPostProbe, antes de FBRig/IK/pés) e
        // tudo que mexe no tronco depois (mira da coluna, ponto mirado, câmera, IK dos pés e onde cada pé apoiou, camadas do Animator)
        float Roll(FBCharacter me, HumanBodyBones l, HumanBodyBones r)
        {   // + = lado direito do corpo mais alto (graus)
            var a = Bone(me, l); var b = Bone(me, r); if (!a || !b) return 0; var d = b.position - a.position; return Mathf.Atan2(d.y, d.Flat().magnitude) * Mathf.Rad2Deg;
        }
        string Postura(FBCharacter me, FBPlayer P, out (float f, float s) fin, out (float f, float s) raw, out float roll)
        {
            roll = Roll(me, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm); float hroll = Roll(me, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg);
            Transform fl = Bone(me, HumanBodyBones.LeftFoot), fr = Bone(me, HumanBodyBones.RightFoot); float feetDy = fl && fr ? (fr.position.y - fl.position.y) / U * 100 : 0;
            var hp = Bone(me, HumanBodyBones.Hips); var hd = Bone(me, HumanBodyBones.Head); var cs = Bone(me, HumanBodyBones.UpperChest) ? Bone(me, HumanBodyBones.UpperChest) : Bone(me, HumanBodyBones.Chest);
            fin = raw = default; if (!hp || !hd) return "sem ossos";
            fin = Tilt(me, hd.position - hp.position); var fc = cs ? Tilt(me, cs.position - hp.position) : fin;
            bool fresh = FBPostProbe.who == me && FBPostProbe.frame == Time.frameCount; raw = fresh ? Tilt(me, FBPostProbe.head) : (float.NaN, float.NaN); var rc = fresh ? Tilt(me, FBPostProbe.chest) : raw;
            var rig = me.GetComponent<FBRig>(); var ik = me.GetComponent<FBFootIK>(); var e = me.transform.eulerAngles; var cp = cs ? cs.position : hd.position;
            string Pe(FBFootIK.Foot f) => f == null ? "-" : $"{(f.Hit ? (f.GroundY - me.Pos.y) * 100 : 0):+0;-0}cm {(f.rayCol ? f.rayCol.name + "/" + LayerMask.LayerToName(f.rayCol.gameObject.layer) : "-")}";
            string aim = me.hasAim ? $"mira a {(me.aimPoint - cp).magnitude / U:0.0} m ({(me.aimPoint.y - cp.y) / U:+0.00;-0.00} m de altura)" : "sem ponto de mira";
            return $"tronco final frente {fin.f:+0.0;-0.0}° lado {fin.s:+0.0;-0.0}° (peito {fc.f:+0.0;-0.0}/{fc.s:+0.0;-0.0}) · ombros {roll:+0.0;-0.0}° quadril {hroll:+0.0;-0.0}° pé D−E {feetDy:+0;-0} cm · Animator puro {raw.f:+0.0;-0.0}/{raw.s:+0.0;-0.0} (peito {rc.f:+0.0;-0.0}/{rc.s:+0.0;-0.0}) · raiz x {Mathf.DeltaAngle(0, e.x):0.0}° z {Mathf.DeltaAngle(0, e.z):0.0}° · "
                + $"coluna pela mira {(rig ? rig.AimW : -1):0.00} (baixa {(rig ? rig.LowerDeg : 0):0}°) · {aim} · câmera pitch {(P ? P.camPitch : 0):0.0} yaw−corpo {(P ? Mathf.DeltaAngle(me.Yaw, P.camYaw) : 0):0} · arma {(me.weaponGO && me.weaponGO.activeInHierarchy ? "na mão" : "não")} combate {me.combat} postura {me.CombatStance} hot {me.Hot} mirando {me.aiming} agach {me.crouch} chão {me.grounded} · "
                + $"espreitar {me.lean:+0.00;-0.00} · sys {me.sys ?? "-"} dança {me.dance?.clip ?? "-"} veículo {me.vehiclePose} danW {me.DanW:0.00} upperW {me.UpperW:0.00} mistura {FBPoseBlend.Active(me)} · "
                + $"pés {(ik ? $"w {ik.Weight:0.00} pelve {ik.HipsOffset * 100:+0;-0}cm tronco {ik.Tilt:0.0}° E {Pe(ik.Left)} D {Pe(ik.Right)}" : "-")} · camadas {LayersAll(me.an)}";
        }
        static string LayersAll(Animator an)
        {
            if (!an) return "-"; var l = new List<string>();
            for (int i = 0; i < an.layerCount; i++) { float w = i == 0 ? 1 : an.GetLayerWeight(i); if (w < .02f) continue; var ci = an.GetCurrentAnimatorClipInfo(i); l.Add($"{i}:{(ci.Length > 0 && ci[0].clip ? ci[0].clip.name : "-")}({w:0.00})"); }
            return string.Join(" ", l);
        }
        // foto do corpo de lado (do lado oposto ao veículo) para ver a inclinação
        IEnumerator CorpoLado(FBCharacter me, FBCar car, string n)
        {
            var hp = Bone(me, HumanBodyBones.Hips); if (!side || !hp) yield break; float k = me.ScaleK; var tgt = hp.position + Vector3.up * .3f * k;
            var r = me.transform.right; if (car && Vector3.Dot(car.transform.position - me.Pos, r) > 0) r = -r;
            side.cullingMask = Camera.main ? Camera.main.cullingMask : ~0; side.transform.position = tgt + r * 3.6f * k + Vector3.up * .1f * k; side.transform.LookAt(tgt); side.enabled = true; pend = n;
            yield return null; yield return new WaitForEndOfFrame(); Flush();
        }
        // fim do personagem: referência = mediana das posturas de pé antes de andar até o veículo; falha = +1,2 s/+3 s/depois de andar fora dela
        void PosturaFim(string sk)
        {
            if (post.Count == 0) return;
            float rf = 0, rs = 0, rr = 0; if (postRef.Count > 0) { var fs = postRef.Select(x => x.f).OrderBy(x => x).ToList(); var ss = postRef.Select(x => x.s).OrderBy(x => x).ToList(); var rs2 = postRef.Select(x => x.ro).OrderBy(x => x).ToList(); rf = fs[fs.Count / 2]; rs = ss[ss.Count / 2]; rr = rs2[rs2.Count / 2]; }
            foreach (var g in post.GroupBy(x => x.nm))
            {
                var (rf0, rs0, rr0) = (rf, rs, rr); if (postRefBy.TryGetValue(g.Key, out var pr)) (rf, rs, rr) = pr;
                float dmax = g.Where(x => x.fase != "+0,6s").Select(x => Mathf.Max(Mathf.Max(Mathf.Abs(x.f - rf), Mathf.Abs(x.s - rs)), Mathf.Abs(x.ro - rr))).DefaultIfEmpty(0).Max();
                bool bad = postRef.Count > 0 && dmax > 7;
                Log($"POSTURA {g.Key}: {string.Join(" · ", g.Select(x => $"{x.fase} {x.f:+0.0;-0.0}/{x.s:+0.0;-0.0}° (Animator {x.rf:+0.0;-0.0}/{x.rs:+0.0;-0.0}) ombros {x.ro:+0.0;-0.0}"))} · de pé {rf:+0.0;-0.0}/{rs:+0.0;-0.0}° ombros {rr:+0.0;-0.0}° ({postRef.Count} medidas) · desvio máx {dmax:0.0}° {(bad ? "INCLINADO" : "ok")}");
                if (bad && postFails.TryGetValue(g.Key, out var fl)) fl.Add("corpo inclinado depois de sair");
                (rf, rs, rr) = (rf0, rs0, rr0);
            }
            post.Clear(); postRef.Clear(); postFails.Clear(); postRefBy.Clear();
        }

        // ================================================================ r51: dano pelo caminho real do tiro
        static string[] DanoArmas() => new[] { "ak", "m4", "fal", "asval", "rpk", "mp5", "ump", "p90", "g18", "beretta_gold", "desert_eagle", "38", "remington_870", "m31", "barret_50", "sniper_hunting" };
        static float Mult(FBDamageModel.Stat st, string part) => part == "head" ? st.hs : part == "stomach" ? 1.25f : part == "leg" ? .75f : 1f;
        // conta do CS refeita aqui (independente do FBDamageModel.Hit): região × queda pela distância × colete/capacete × parede, inteiro
        static float Expect(FBDamageModel.Stat st, string part, float worldDist, bool vest, bool helmet, float armor, float wallK, out float armorLost)
        {
            float m = worldDist / 1.43f, raw = st.dmg * Mathf.Pow(st.rm, m / 12.7f) * Mult(st, part), hp = raw; armorLost = 0;
            bool prot = part == "head" ? helmet : part == "leg" ? false : vest && armor > 0;
            if (prot) { hp = raw * st.ap; float ta = (raw - hp) * .5f; if (part != "head") { if (ta > armor) { hp = raw - armor / .5f; ta = armor; } armorLost = ta; } hp = Mathf.Max(0, hp); }
            hp *= wallK; return hp <= 0 ? 0 : Mathf.Max(1, Mathf.Round(hp));
        }
        static (Vector3 p, Vector3 dir, float len) LongLine(IEnumerable<Vector3> spots, float h, float maxM)
        {   // ponto + direção com a linha livre mais longa na altura h e na altura da perna (até maxM metros)
            (Vector3, Vector3, float) best = (Vector3.zero, Vector3.forward, 0);
            foreach (var p0 in spots)
            {
                var p = p0; if (FBWorld.GroundY(p.x, p.y + 2, p.z, .4f, 5) is float g) p.y = g; else continue; var c = p + Vector3.up * h;
                if (Physics.CheckSphere(c, .3f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)) continue;
                for (int a = 0; a < 24; a++)
                {
                    var d = Quaternion.Euler(0, a * 15, 0) * Vector3.forward; float L = Physics.Raycast(c, d, out var hit, maxM * U, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) ? hit.distance : maxM * U;
                    float L2 = Physics.Raycast(p + Vector3.up * .45f * U, d, out var h2, maxM * U, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) ? h2.distance : maxM * U;
                    L = Mathf.Min(L, L2); if (L > best.Item3) best = (p, d, L);
                }
                if (best.Item3 >= maxM * U) break;
            }
            return best;
        }
        IEnumerator Dano(FBCharacter me, FBPlayer P)
        {
            if (FBVehicles.CarOf(me)) { FBDrive.Exit(me, true); yield return W(1); }
            var ws = (Arg("-fbjogo_dano_armas")?.Split(',') ?? DanoArmas()).Where(x => GameData.Weapons.ContainsKey(x) && FBDamageModel.Table.ContainsKey(x)).Select(x => GameData.Weapons[x]).ToList();
            if (!FBRoads.Ready) FBRoads.Load(FBSession.map);
            var spots = new List<Vector3> { me.Pos }; if (FBRoads.Ready) spots.AddRange(FBRoads.spots.Select(x => x.p).OrderBy(x => (x - me.Pos).sqrMagnitude).Take(60));
            var (tp, dir0, lenL) = LongLine(spots, 1.2f * U, 42);
            Log($"DANO: modelo {(FBDamageModel.On ? "CS (r51)" : "antigo")} · modo {FBSession.mode} · vida do FF (BR) {(FBMatch.I && FBMatch.I.FFDamage)} · linha livre {lenL / U:0} m · {ws.Count} armas: {string.Join(",", ws.Select(w => w.id))}");
            float yawT = Quaternion.LookRotation(dir0).eulerAngles.y;
            var tgt = FBStreetEvents.Spawn(FBStreetEvents.FreeCivilian(true), tp, yawT, "dano"); if (!tgt) { Log("DANO: alvo não nasceu"); yield break; }
            yield return W(1);
            FBDamage last = default; int evN = 0; System.Action<FBDamage> on = d => { if (d.victim == tgt) { last = d; evN++; } }; FBDamageEvents.OnDamage += on;
            int total = 0, okN = 0, miss = 0, blockedN = 0, wallHits = 0; var bads = new List<string>();
            IEnumerator Shoot(WeaponDef w, string part, float m, bool arm, List<string> row, float wallK = 1, string tagW = null)
            {
                int ev0 = 0, fl0 = 0; float hp0 = 0, ar0 = 0; var st = FBDamageModel.Table[w.id]; FBCombat.Shot r = default;
                for (int tr = 0; ; tr++)
                {   // r51: no rolê passa pedestre/carro na linha (40 m: "acertou PM ELAINE") — bloqueado por outro = espera e tenta de novo (até 6×)
                    tgt.hp = tgt.maxHp = 1000; tgt.armor = arm ? 100 : 0; tgt.helmet = arm; FBMatch.Teleport(tgt, tp, yawT); yield return null; yield return null;
                    var hb = tgt.GetComponentsInChildren<FBHitbox>().Where(x => x.part == part || (part == "head" && x.head)).OrderByDescending(x => Vector3.Dot(x.GetComponent<Collider>().bounds.center - tp, dir0)).FirstOrDefault();
                    if (!hb) { row.Add($"{part}: sem hitbox"); yield break; }
                    var aimP = hb.GetComponent<Collider>().bounds.center;
                    // r51e: peito/barriga = meio da região AO LONGO DA COLUNA (quadril → pescoço). O centro da cápsula do peito (raio 0,28) fica na altura do
                    // pescoço nos rigs de osso Chest alto — "tiro no peito" que o jogador daria no pescoço (r51e: 7 de 54 viravam cabeça)
                    if (hb.lo && hb.hi && (part == "chest" || part == "stomach")) aimP = Vector3.Lerp(hb.lo.position, hb.hi.position, part == "chest" ? .72f : .25f);
                    var o = aimP + dir0 * m * U;
                    ev0 = evN; hp0 = tgt.hp; ar0 = tgt.armor; fl0 = FBHUD.FloatN;
                    r = FBCombat.BulletFrom(o, -dir0, o, me, w, 0, false, m * U + 3, 1);
                    if (r.hit == tgt || wallK < 1 || tr >= 5) break; blockedN++; yield return W(.6f);
                }
                total++;
                if (r.hit != tgt) { miss++; row.Add($"{part}: {(r.hit ? "acertou " + r.hit.Name : wallK < 1 ? "PAROU na parede" : "não acertou")}"); if (wallK >= 1) bads.Add($"{w.id} {part} {m:0} m não acertou"); yield break; }
                // r51: tiro que perdeu dano por "parede" no caminho: lista o que a bala atravessou (o dano esperado usa o mesmo fator, a conta do resto continua estrita)
                float wk = wallK < 1 ? wallK : FBCombat.LastHit.wallK; string thru = "";
                if (wallK >= 1 && wk < .999f)
                {
                    var o2 = FBCombat.LastO; var hs2 = Physics.RaycastAll(o2, FBCombat.LastDir, r.dist + .01f, FBWorld.ShotMask, QueryTriggerInteraction.Ignore);
                    thru = " (atravessou " + string.Join(", ", hs2.OrderBy(x => x.distance).Select(x => $"{x.collider.name}/{LayerMask.LayerToName(x.collider.gameObject.layer)}{(x.collider.GetComponentInParent<FBCharacter>() is FBCharacter oc ? " de " + oc.Name : "")}")) + $", fator {wk:0.00})"; wallHits++;
                }
                float ex = Expect(st, r.part, r.dist, arm, arm, ar0, wk, out float arL);
                float amount = evN > ev0 ? last.amount : -1, applied = evN > ev0 ? last.applied : -1, drop = hp0 - tgt.hp; int hud = FBHUD.FloatN > fl0 ? FBHUD.LastFloat : -1;
                bool good = Mathf.Abs(amount - ex) < .51f && Mathf.Abs(applied - drop) < .01f && hud == Mathf.RoundToInt(applied) && Mathf.Abs(ar0 - tgt.armor - arL) < .51f;
                if (good) okN++; else bads.Add($"{w.id} {part}→{r.part} {m:0} m{(arm ? " colete" : "")}{tagW}: pedido {amount:0.##} esperado {ex:0.##} · vida −{drop:0.##} aplicado {applied:0.##} · HUD {hud} · colete −{ar0 - tgt.armor:0.#} esperado −{arL:0.#}");
                row.Add($"{part}{(r.part != part ? "→" + r.part : "")} {amount:0}{(good ? "" : $"≠{ex:0}")}{thru} HUD {hud}{(arm && r.part != "leg" ? $" colete −{ar0 - tgt.armor:0}" : "")}");
            }
            foreach (var w in ws)
            {
                var st = FBDamageModel.Table[w.id];
                foreach (float m0 in new[] { 3f, 15f, 40f })
                {
                    float m = Mathf.Min(m0, lenL / U - 1.5f); if (m < 2) continue;
                    foreach (bool arm in new[] { false, true })
                    {
                        var row = new List<string>();
                        foreach (var part in new[] { "head", "chest", "stomach", "arm", "leg" }) yield return Shoot(w, part, m, arm, row);
                        Log($"DANO {w.id} {m:0} m {(arm ? "colete+capacete" : "sem proteção")}: {string.Join(" · ", row)}");
                    }
                }
                if (st.pellets > 1)
                {   // disparo inteiro (todos os bagos no mesmo quadro): o número do HUD = soma da vida que saiu
                    tgt.hp = tgt.maxHp = 1000; tgt.armor = 0; tgt.helmet = false; FBMatch.Teleport(tgt, tp, yawT); yield return null; yield return null;
                    var hb = tgt.GetComponentsInChildren<FBHitbox>().FirstOrDefault(x => x.part == "chest");
                    if (hb)
                    {
                        var aimP = hb.GetComponent<Collider>().bounds.center; var o = aimP + dir0 * 5 * U; float hp0 = tgt.hp; int fl0 = FBHUD.FloatN;
                        for (int k = 0; k < st.pellets; k++) FBCombat.BulletFrom(o, -dir0, o, me, w, 0, false, 5 * U + 3, st.pellets);
                        bool good = FBHUD.LastFloat == Mathf.RoundToInt(hp0 - tgt.hp) && FBHUD.FloatN - fl0 == st.pellets; total++; if (good) okN++; else bads.Add($"{w.id} disparo inteiro: HUD {FBHUD.LastFloat} × vida −{hp0 - tgt.hp:0}");
                        Log($"DANO {w.id} disparo inteiro a 5 m ({st.pellets} bagos no peito): HUD mostra {FBHUD.LastFloat} · vida −{hp0 - tgt.hp:0} {(good ? "ok" : "DIFERENTE")}");
                    }
                }
                // tiros para matar (100 de vida) pela tabela: 15 m peito sem/com colete, cabeça com capacete
                float b15 = FBDamageModel.Base(st, 15); int pl = Mathf.Max(1, st.pellets);
                int n0 = Mathf.CeilToInt(100 / Mathf.Max(1, Mathf.Round(b15) * pl)), nv = Mathf.CeilToInt(100 / Mathf.Max(1, Mathf.Round(b15 * st.ap) * pl)); float hh = Mathf.Round(b15 * st.hs * st.ap);
                Log($"DANO {w.id} ({st.real}) · 15 m: peito {Mathf.Round(b15)}{(pl > 1 ? $"×{pl}" : "")} → {n0} tiro(s) em {(n0 - 1) * 60f / st.rpm:0.00} s · com colete {nv} em {(nv - 1) * 60f / st.rpm:0.00} s · cabeça c/ capacete {hh}{(pl > 1 ? $"×{pl}" : "")} ({(hh * pl >= 100 ? "mata" : "não mata")}) · {st.rpm} rpm · penetração {st.pen} · precisão ×{st.acc} recuo ×{st.rec} mobilidade ×{st.move}");
            }
            // paredes de teste a meio caminho (10 m): madeira 3 cm, chapa 1 cm, concreto 20 cm — peito, sem colete
            float mw = Mathf.Min(10, lenL / U - 1.5f);
            foreach (var (mat, kind, thick) in new[] { ("madeira 3 cm", 5, .03f), ("chapa 1 cm", 4, .01f), ("concreto 20 cm", 0, .20f) })
            {
                var wall = new GameObject("parede_teste") { layer = FBWorld.LMap }; var bc = wall.AddComponent<BoxCollider>(); bc.size = new Vector3(3 * U, 3 * U, thick * U); wall.AddComponent<FBSurface>().kind = kind;
                wall.transform.SetPositionAndRotation(tp + Vector3.up * 1.2f * U + dir0 * mw * .5f * U, Quaternion.LookRotation(dir0)); Physics.SyncTransforms(); yield return null;
                var row = new List<string>(); var surf = FBImpactFx.Classify(bc);
                foreach (var w in ws.Where(x => x.pellets <= 1))
                {
                    var st = FBDamageModel.Table[w.id]; float maxT = FBDamageModel.Resist(surf) * st.pen; float wk = thick <= maxT ? FBDamageModel.AfterWall(thick, maxT) : 0;
                    var sub = new List<string>(); yield return Shoot(w, "chest", mw, false, sub, wk > 0 ? wk : .5f, " parede " + mat);
                    var res = sub.FirstOrDefault() ?? "-"; bool stopped = res.Contains("PAROU");
                    row.Add($"{w.id} {(wk <= 0 ? "(deve parar)" : $"(×{wk:0.00})")} {res}");
                    if (wk <= 0 && !stopped) bads.Add($"{w.id} atravessou {mat} (não devia)");
                    if (wk > 0 && stopped) bads.Add($"{w.id} parou em {mat} (devia atravessar ×{wk:0.00})");
                    if (wk <= 0 && stopped) { okN++; total++; }
                }
                Log($"DANO parede {mat} ({surf}): {string.Join(" · ", row)}"); Destroy(wall); yield return null;
            }
            FBDamageEvents.OnDamage -= on; FBStreetEvents.Despawn(tgt);
            Log($"DANO RESUMO: {okN}/{total} tiros com pedido = conta do CS, vida perdida = aplicado e HUD = vida perdida (paredes: parar onde devia conta como certo) · {miss} sem acertar o alvo ({blockedN} tiros repetidos por pedestre/carro na linha) · {wallHits} tiros atravessando algo antes do alvo · penetrações {FBDamageModel.Penetrations} (bloqueadas {FBDamageModel.PenBlocked}){(bads.Count > 0 ? " · DIFERENÇAS: " + string.Join(" | ", bads.Take(14)) : "")}");
        }


        // ================================================================ r51: pegada de cada arma (mãos × arma) parado/mirando/andando/agachado/atirando
        // Referência = a animação de 1ª pessoa DAQUELA arma (tpsGrip, espaço do nó da arma): punho e palma direitos no cabo, ponta do indicador
        // no gatilho, apoio da mão esquerda (o IK põe a palma no corte real do guarda-mão; "folga" = quanto o braço não alcançou).
        static Vector3 GVt(object o) { var l = o as List<object>; return l != null && l.Count >= 3 ? new Vector3(-Json.Fv(l[0]), Json.Fv(l[1]), Json.Fv(l[2])) : Vector3.zero; }
        static Transform N0(GameObject w) { var t = w.transform; return t.childCount == 1 && !t.GetComponent<Renderer>() ? t.GetChild(0) : t; }
        static Vector3 TipOf(Transform mid, Transform dis) => dis.position + (dis.position - mid.position) * .8f;   // = FBRig.Tip
        IEnumerator Pegada(FBCharacter me, FBPlayer P, string sk)
        {
            if (FBVehicles.CarOf(me)) { FBDrive.Exit(me, true); yield return W(1); }
            if (!FBRoads.Ready) FBRoads.Load(FBSession.map);
            var spots = new List<Vector3> { me.Pos }; if (FBRoads.Ready) spots.AddRange(FBRoads.spots.Select(x => x.p).OrderBy(x => (x - me.Pos).sqrMagnitude).Take(40));
            var (pp, pd, _) = LongLine(spots, 1.6f * U, 60); float pyaw = Quaternion.LookRotation(pd).eulerAngles.y;
            var ids = (Arg("-fbjogo_pegada")?.Split(',') ?? GameData.Weapons.Keys.ToArray()).Where(id => GameData.Weapons.ContainsKey(id) && FBRegistry.I.Weapon(id) && Json.O(GameData.TpsGrip, id) != null).OrderBy(id => GameData.Weapons[id].cls).ThenBy(id => id).ToList();
            var rig = me.GetComponent<FBRig>(); var cam = new GameObject("JogoPegadaCam").AddComponent<Camera>(); cam.depth = 100; cam.enabled = false; cam.nearClipPlane = .03f; cam.fieldOfView = 30; cam.cullingMask = Camera.main ? Camera.main.cullingMask : ~0;
            Transform B(HumanBodyBones b) => Bone(me, b);
            Log($"PEGADA {sk}: {ids.Count} armas · gatilho por arma {(FBRig.TriggerFit ? "LIGADO" : "desligado (-fbgatilhoantigo)")} · dedos finos {(rig ? "sim" : "-")}");
            var resumo = new List<(string id, string cls, float rP, float trA, float trD, float lg, float ret, float err, string st)>(); var palmVec = new Dictionary<string, Vector3>();
            foreach (var id in ids)
            {
                var w = GameData.Weapons[id]; var cho = FBWeaponModel.Default(w); FBWeaponModel.Set(w, cho);
                FBMatch.Teleport(me, pp, pyaw); P.camYaw = pyaw; P.camPitch = 2;
                P.wu.Strip(); P.wu.Give(new[] { id }); yield return W(.3f); int sl = System.Array.FindIndex(P.wu.slots, x => x != null && x.id == id); if (sl < 0) { Log($"PEGADA {id}: sem espaço"); continue; }
                P.wu.Equip(sl, true); yield return W(1.6f);
                if (!me.weaponGO) { Log($"PEGADA {id}: arma não apareceu na mão"); continue; }
                var R = Json.O(Json.O(GameData.TpsGrip, id), "r"); var il = Json.L(R, "i"); var ml = Json.L(R, "m");
                var fpW = GVt(R?["w"]); var fpPalm = ml != null && ml.Count > 0 ? Vector3.Lerp(fpW, GVt(ml[0]), .5f) : fpW; var fpTrig = il != null && il.Count >= 4 ? GVt(il[3]) : Vector3.zero;
                var linhas = new List<string>(); float worstP = 0, worstGap = 0, worstRet = 0, worstErr = 0, trA = -1, trD = -1; string worstSt = "";
                foreach (var st in new[] { "parado", "mirando", "andando", "agachado", "atirando" })
                {
                    aimT = st == "mirando" || st == "atirando"; mv = st == "andando" ? Vector2.up * .6f : Vector2.zero; if (st == "agachado") crouchEdge = true;
                    float tt = 0; while (tt < .9f) { tt += Time.deltaTime; if (st == "atirando") { if (w.auto) fireT = true; else if (Time.frameCount % 9 == 0) fireEdge = true; } P.camYaw = pyaw; yield return null; }
                    float sP = 0, sW = 0, sT = 0, sG = 0, sR = 0, sE = 0; int n = 0; Vector3 vSum = Vector3.zero;
                    for (int f = 0; f < 12; f++)
                    {
                        if (st == "atirando") { if (w.auto) fireT = true; else if (Time.frameCount % 9 == 0) fireEdge = true; }
                        yield return new WaitForEndOfFrame(); if (!me.weaponGO) break; var n0 = N0(me.weaponGO);
                        Transform rh = B(HumanBodyBones.RightHand), rm = B(HumanBodyBones.RightMiddleProximal), ri = B(HumanBodyBones.RightIndexIntermediate), rd = B(HumanBodyBones.RightIndexDistal); if (!rh) break;
                        var palm = rm ? Vector3.Lerp(rh.position, rm.position, .5f) : rh.position; var fpP = n0.TransformPoint(fpPalm);
                        sP += (palm - fpP).magnitude / U; sW += (rh.position - n0.TransformPoint(fpW)).magnitude / U; vSum += rh.InverseTransformVector(fpP - palm) / U;
                        if (ri && rd && il != null) sT += (TipOf(ri, rd) - n0.TransformPoint(fpTrig)).magnitude / U;
                        if (rig) { sG += rig.LeftGap / U; sR += rig.Retreat / U; if (aimT) sE += rig.BarrelErr; }
                        n++;
                    }
                    fireT = false; if (n == 0) { linhas.Add($"{st}: sem medida"); continue; }
                    float mP = sP / n * 100, mW = sW / n * 100, mT = sT / n * 100, mG = sG / n * 100, mR = sR / n * 100, mE = sE / n;
                    if (st == "parado")
                    {   // gatilho: ângulo fixo antigo × dobra por arma, no mesmo quadro de pose
                        trD = mT; bool keep = FBRig.TriggerFit; FBRig.TriggerFit = false; float sa = 0; int na = 0;
                        for (int f = 0; f < 6; f++) { yield return new WaitForEndOfFrame(); Transform ri = B(HumanBodyBones.RightIndexIntermediate), rd = B(HumanBodyBones.RightIndexDistal); if (ri && rd && il != null && me.weaponGO) { sa += (TipOf(ri, rd) - N0(me.weaponGO).TransformPoint(fpTrig)).magnitude / U; na++; } }
                        FBRig.TriggerFit = keep; trA = na > 0 ? sa / na * 100 : -1; palmVec[id] = vSum / n * 100;
                    }
                    if (mP > worstP) { worstP = mP; worstSt = st; } worstGap = Mathf.Max(worstGap, mG); worstRet = Mathf.Max(worstRet, mR); worstErr = Mathf.Max(worstErr, mE);
                    linhas.Add($"{st}: palma {mP:0.0} punho {mW:0.0} gatilho {mT:0.0} · esq. folga {mG:0.0} recuo {mR:0.0}{(aimT ? $" · cano {mE:0.0}°" : "")}");
                    if (st == "mirando")
                    {
                        var hp = Bone(me, HumanBodyBones.RightHand); if (hp)
                        {
                            var c = Vector3.Lerp(hp.position, me.weaponGO.transform.position, .5f); var r = me.transform.right; var fw = me.transform.forward;
                            // r51: a câmera ia sempre 1,3 à direita — com parede ao lado as 54 fotos da r51c saíram só reboco. Lado/distância com linha livre até a mão.
                            Vector3 cp = c; foreach (var (sd, dk) in new[] { (1f, 1.3f), (-1f, 1.3f), (1f, .75f), (-1f, .75f), (1f, .45f) })
                            {
                                cp = c + r * sd * dk * me.ScaleK + fw * .5f * me.ScaleK + Vector3.up * .15f * me.ScaleK; var a0 = c + (cp - c).normalized * .22f * me.ScaleK;
                                bool hitW = Physics.Linecast(a0, cp, out var bh, ~0, QueryTriggerInteraction.Ignore) && !bh.collider.GetComponentInParent<FBCharacter>();
                                if (!hitW && !Physics.CheckSphere(cp, .08f * me.ScaleK, ~0, QueryTriggerInteraction.Ignore)) break;
                            }
                            cam.transform.position = cp; cam.transform.LookAt(c); cam.enabled = true; yield return null; yield return new WaitForEndOfFrame(); Foto($"pegada_{id}_mirando"); cam.enabled = false;
                        }
                    }
                    if (st == "agachado") { crouchEdge = true; yield return W(.5f); }
                }
                aimT = false; mv = Vector2.zero; fireT = false;
                bool hgOk = rig && rig.LeftSec;
                resumo.Add((id, w.cls, worstP, trA, trD, worstGap, worstRet, worstErr, worstSt));
                Log($"PEGADA {id} ({w.cls}): {string.Join(" | ", linhas)} · gatilho fixo {trA:0.0} → por arma {trD:0.0} cm (dobra {(rig && rig.TrigErr.ContainsKey(id) ? "ok" : "-")}) · guarda-mão {(hgOk ? "corte real" : "CILINDRO GENÉRICO")} · cabo alinhado {(FBCharacter.GripAlign ? $"{(Bone(me, HumanBodyBones.RightHand) ? Bone(me, HumanBodyBones.RightHand).TransformVector(me.GripShift).magnitude / U * 100 : 0):0.0} cm" : "não (-fbpegadaantiga)")} · palma FP−3ªP na mão {(palmVec.TryGetValue(id, out var pv) ? $"{pv.x:+0.0;-0.0},{pv.y:+0.0;-0.0},{pv.z:+0.0;-0.0}" : "-")} cm");
            }
            Destroy(cam.gameObject);
            // resumo: distribuição e as armas fora do grupo (palma > 3 cm do lugar da 1ª pessoa, gatilho > 1,5 cm, mão esquerda sem alcançar > 2 cm, cano > 3°)
            foreach (var g in resumo.GroupBy(x => x.cls))
                Log($"PEGADA classe {g.Key}: palma {g.Min(x => x.rP):0.0}–{g.Max(x => x.rP):0.0} cm · gatilho fixo {g.Average(x => x.trA):0.0} → por arma {g.Average(x => x.trD):0.0} cm · folga esq. máx {g.Max(x => x.lg):0.0} cm · recuo máx {g.Max(x => x.ret):0.0} cm · cano máx {g.Max(x => x.err):0.0}°");
            var fora = resumo.Where(x => x.rP > 3 || x.trD > 1.5f || x.lg > 2 || x.err > 3).Select(x => $"{x.id} (palma {x.rP:0.0} em {x.st}, gatilho {x.trD:0.0}, folga {x.lg:0.0}, cano {x.err:0.0}°)").ToList();
            Log($"PEGADA RESUMO {sk}: {resumo.Count} armas · gatilho fixo méd {resumo.Where(x => x.trA >= 0).Select(x => x.trA).DefaultIfEmpty(0).Average():0.0} cm → por arma {resumo.Where(x => x.trD >= 0).Select(x => x.trD).DefaultIfEmpty(0).Average():0.0} cm · fora do limite {fora.Count}: {string.Join(" | ", fora)}");
        }
        // ================================================================ r51: mira óptica (3ª → 1ª pessoa), lente PiP, retículo × impacto
        static List<string> ScopeNodes(WeaponDef w) => (Json.L(Json.O(Json.O(GameData.WeaponsRaw, w.id), "att"), "scopes") ?? new List<object>()).Select(x => Json.S(x, "node") ?? "").ToList();
        // r51l: marcadores do profiler por quadro no traço da entrada (quadros de 55–80 ms no início e na virada de TODA mira óptica, fora dos
        // trechos de script do FBPlayer, todos < 3 ms): criação de shader na GPU, GC, scripts, render do URP, espera da GPU
        static readonly (string nome, Unity.Profiling.ProfilerCategory cat, string marca)[] Marcas = {
            ("shaderGPU", Unity.Profiling.ProfilerCategory.Render, "Shader.CreateGPUProgram"), ("GC", Unity.Profiling.ProfilerCategory.Memory, "GC.Collect"),
            ("update", Unity.Profiling.ProfilerCategory.Scripts, "BehaviourUpdate"), ("late", Unity.Profiling.ProfilerCategory.Scripts, "PreLateUpdate.ScriptRunBehaviourLateUpdate"),
            ("render", Unity.Profiling.ProfilerCategory.Render, "RenderPipelineManager.DoRenderLoop_Internal()"), ("esperaGPU", Unity.Profiling.ProfilerCategory.Render, "Gfx.WaitForPresentOnGfxThread"),
            ("físicas", Unity.Profiling.ProfilerCategory.Physics, "FixedUpdate.PhysicsFixedUpdate"), ("anim", Unity.Profiling.ProfilerCategory.Animation, "PreLateUpdate.DirectorUpdateAnimationEnd"),
            ("upload", Unity.Profiling.ProfilerCategory.Loading, "Loading.AsyncRead"), ("skin", Unity.Profiling.ProfilerCategory.Render, "SkinnedMeshRenderer.UpdateSkinnedMeshes") };
        Unity.Profiling.ProfilerRecorder[] marcas; readonly List<string> picosMundo = new();
        string MarcasQuadro()
        {
            if (marcas == null) return ""; var sb = new System.Text.StringBuilder();
            for (int i = 0; i < marcas.Length; i++) { var r = marcas[i]; if (!r.Valid) continue; double ms = r.LastValue / 1e6; if (ms > 3 || (i == 0 && ms > 0)) sb.Append($"{Marcas[i].nome} {ms:0} "); }
            return sb.ToString().Trim();
        }
        // ================================================================ r52: ESPREITAR (Q/E) × cobertura — "vê o inimigo mas o tiro bate no carro"
        // Jogador atrás de uma cobertura (S10 de lado; depois uma parede de teste), alvo além da quina a 14 m. Varre a distância até a quina ×
        // Q/E × armas × quadril/mirando; a câmera mira o peito do alvo e mede: a câmera vê o peito? o cano tem linha? onde o tiro REAL parou
        // (FBCombat.Last) e onde o indicador de impacto disse que ia parar (PredictImpact). Antes (conserto desligado) e depois, no mesmo exe.
        bool leanQ, leanE;
        static Vector3 Chest(FBCharacter t)
        {
            var hb = t.GetComponentsInChildren<FBHitbox>().FirstOrDefault(x => x.part == "chest");
            if (hb && hb.lo && hb.hi) return Vector3.Lerp(hb.lo.position, hb.hi.position, .72f);
            return hb ? hb.GetComponent<Collider>().bounds.center : t.Pos + Vector3.up * 1.3f * U;
        }
        static void AimAt(FBPlayer P, FBCharacter t)
        {
            var cam = Camera.main; if (!cam || !t) return; var dv = Chest(t) - cam.transform.position;
            P.camYaw = Mathf.Atan2(dv.x, dv.z) * Mathf.Rad2Deg; P.camPitch = Mathf.Asin(Mathf.Clamp(dv.y / Mathf.Max(1e-4f, dv.magnitude), -1, 1)) * Mathf.Rad2Deg;
        }
        static void Conserto(bool on) { FBPlayer.PeekFix = on; FBPlayer.FpBore = on; FBCharacter.LeanFirst = on; }
        // extensão ao longo de um eixo pela caixa LOCAL (malha/caixa girada não incha como a caixa de mundo)
        static (float lo, float hi) Ext(Bounds b, Transform tr, Vector3 o, Vector3 ax)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < 8; i++) { var q = tr.TransformPoint(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z)); float v = Vector3.Dot(q - o, ax); lo = Mathf.Min(lo, v); hi = Mathf.Max(hi, v); }
            return (lo, hi);
        }
        static (float lo, float hi) Ext(Collider c, Vector3 o, Vector3 ax) =>
            c is MeshCollider mc && mc.sharedMesh ? Ext(mc.sharedMesh.bounds, c.transform, o, ax) : c is BoxCollider bc ? Ext(new Bounds(bc.center, bc.size), c.transform, o, ax) : (float.MaxValue, float.MinValue);
        IEnumerator Espreitar(FBCharacter me, FBPlayer P)
        {
            if (FBVehicles.CarOf(me)) { FBDrive.Exit(me, true); yield return W(1); }
            if (!FBRoads.Ready) FBRoads.Load(FBSession.map);
            var spots = new List<Vector3> { me.Pos }; if (FBRoads.Ready) spots.AddRange(FBRoads.spots.Select(x => x.p).OrderBy(x => (x - me.Pos).sqrMagnitude).Take(60));
            var (sp, f, len) = LongLine(spots, 1.6f * U, 30); f.y = 0; f.Normalize(); var r = Vector3.Cross(Vector3.up, f); float yawF = Quaternion.LookRotation(f).eulerAngles.y;
            WeaponDef W1(string id) => GameData.Weapons.TryGetValue(id, out var x) && FBRegistry.I.Weapon(id) ? x : null;
            var casos = new List<(WeaponDef w, int sc, string nome, bool[] modos)>();
            var pist = GameData.Weapons.Values.Where(x => x.IsPistol && FBRegistry.I.Weapon(x.id)).OrderBy(x => x.id.Contains("glock") ? 0 : 1).FirstOrDefault();
            var ak = W1("ak47") ?? GameData.Weapons.Values.FirstOrDefault(x => x.cls == "fuzil" && FBRegistry.I.Weapon(x.id));
            var m4 = GameData.Weapons.Values.Where(x => x.cls == "fuzil" && FBRegistry.I.Weapon(x.id)).OrderBy(x => x.id == "m4" ? 0 : 1).FirstOrDefault(x => ScopeNodes(x).Count(n => n.Contains("ATT_Scope_0")) >= 5);
            if (pist != null) casos.Add((pist, -1, "ferro", new[] { false, true }));
            if (ak != null) casos.Add((ak, -1, "ferro", new[] { false, true }));
            var rd = W1("ar15_prime"); if (rd != null) { int i = ScopeNodes(rd).FindIndex(x => x.Contains("Ponto_Vermelho")); if (i >= 0) casos.Add((rd, i, "ponto_vermelho", new[] { true })); }
            if (m4 != null) { int i = ScopeNodes(m4).FindIndex(x => x.Contains("Scope_05")); if (i >= 0) casos.Add((m4, i, "4x", new[] { true })); }
            var bar = W1("barret_50"); if (bar != null) casos.Add((bar, -2, "6x", new[] { true }));
            if (Arg("-fbjogo_espreitar") is string only) casos = casos.Where(c => only.Split(',').Any(o => c.nome.Contains(o) || c.w.id == o)).ToList();
            Log($"ESPREITAR: linha livre {len / U:0} m · {casos.Count} armas: {string.Join(", ", casos.Select(c => c.w.id + "/" + c.nome))} · conserto = câmera só no passo da inclinação + corpo sai até {FBPlayer.PeekMax / U * 100:0} cm para o cano ter linha + inclinação antes da mira do tronco + 1ª pessoa/óptica pelo cano");
            var tgt = FBStreetEvents.Spawn(FBStreetEvents.FreeCivilian(true), sp + f * 14 * U, yawF + 180, "espreitar"); if (!tgt) { Log("ESPREITAR: alvo não nasceu"); yield break; }
            var car = Spawn("S10_2017", sp + f * 2.4f * U, yawF + 90) ?? Spawn("SaveiroCross", sp + f * 2.4f * U, yawF + 90); if (!car) { Log("ESPREITAR: carro não nasceu"); FBStreetEvents.Despawn(tgt); yield break; }
            yield return W(1.2f); Physics.SyncTransforms();
            {   // de LADO (comprimento ao longo de r): o eixo de frente do modelo nem sempre é o +z — mede e, se nasceu de frente, gira 90°
                float Span(Vector3 ax) { float a = float.MaxValue, b = float.MinValue; foreach (var c in car.cols) { if (!c || !c.enabled) continue; var (x, y) = Ext(c, sp, ax); a = Mathf.Min(a, x); b = Mathf.Max(b, y); } return b - a; }
                float sr = Span(r), sf = Span(f);
                if (sr < sf) { string vid = car.id; Kill(car); yield return null; car = Spawn(vid, sp + f * 2.4f * U, yawF); yield return W(1.2f); Physics.SyncTransforms(); }
                Log($"ESPREITAR carro de lado: ao longo da quina {Span(r) / U:0.0} m, profundidade {Span(f) / U:0.0} m{(sr < sf ? " (nasceu de frente, girado 90°)" : "")}");
            }
            // ---- colisão × visual do carro de cobertura (mesmos eixos: quina esquerda/direita, frente, teto)
            float eL = float.MaxValue, eR = float.MinValue, nearF = float.MaxValue, top = float.MinValue;
            foreach (var c in car.cols) { if (!c || !c.enabled) continue; var (a, b) = Ext(c, sp, r); eL = Mathf.Min(eL, a); eR = Mathf.Max(eR, b); nearF = Mathf.Min(nearF, Ext(c, sp, f).lo); top = Mathf.Max(top, Ext(c, sp, Vector3.up).hi); }
            float vL = float.MaxValue, vR = float.MinValue, vF = float.MaxValue, vT = float.MinValue;
            foreach (var mf in car.GetComponentsInChildren<MeshFilter>()) { var mr = mf.GetComponent<MeshRenderer>(); if (!mf.sharedMesh || !mr || !mr.enabled || mf.name.Contains("sombra")) continue; var (a, b) = Ext(mf.sharedMesh.bounds, mf.transform, sp, r); vL = Mathf.Min(vL, a); vR = Mathf.Max(vR, b); vF = Mathf.Min(vF, Ext(mf.sharedMesh.bounds, mf.transform, sp, f).lo); vT = Mathf.Max(vT, Ext(mf.sharedMesh.bounds, mf.transform, sp, Vector3.up).hi); }
            string Col(Collider c) => c is MeshCollider mc ? $"malha {(mc.sharedMesh ? mc.sharedMesh.name : "-")}{(mc.sharedMesh && mc.sharedMesh.isReadable ? $" {mc.sharedMesh.triangles.Length / 3} tri" : "")}{(mc.convex ? " CONVEXA" : "")}" : c is BoxCollider bc ? $"caixa {bc.size.x * c.transform.lossyScale.x / U:0.00}×{bc.size.y * c.transform.lossyScale.y / U:0.00}×{bc.size.z * c.transform.lossyScale.z / U:0.00} m" : c.GetType().Name;
            Log($"ESPREITAR cobertura {car.id}: {car.cols.Count(c => c)} colisores ({string.Join(", ", car.cols.Where(c => c).Select(Col).GroupBy(x => x).Select(g => g.Count() > 1 ? $"{g.Count()}× {g.Key}" : g.Key))}) · colisão além do visual: quina esq {(vL - eL) / U * 100:0} cm, dir {(eR - vR) / U * 100:0} cm, frente {(vF - nearF) / U * 100:0} cm, teto {(top - vT) / U * 100:0} cm (+ = colisão maior que o carro visto)");
            int nMesh = 0, nBox = 0; var boxCars = new List<string>();
            foreach (var vc in FBVehicles.list) { if (!vc) continue; bool mesh = vc.cols.Any(c => c is MeshCollider); if (mesh) nMesh++; else { nBox++; if (boxCars.Count < 8) boxCars.Add(vc.id); } }
            Log($"ESPREITAR carros na cena: {nMesh} com colisão pela malha da carroceria, {nBox} só com caixas{(boxCars.Count > 0 ? " (" + string.Join(", ", boxCars) + ")" : "")}");
            // ---- colisores sem nada visível no lugar (bloqueio invisível) num raio de 30 m
            {
                var rends = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(x => x.enabled && x.gameObject.activeInHierarchy && !(x is ParticleSystemRenderer) && !(x is TrailRenderer) && !(x is LineRenderer) && x.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly).Select(x => x.bounds).ToList();
                var inv = new List<(string n, Vector3 sz, float d)>(); int tot = 0;
                foreach (var c in Physics.OverlapSphere(sp, 30 * U, FBWorld.SolidMask, QueryTriggerInteraction.Ignore))
                {
                    if (c is TerrainCollider || c.GetComponentInParent<FBCharacter>()) continue; tot++;
                    var b = c.bounds; b.Expand(-.02f); bool vis = false; foreach (var rb in rends) if (rb.Intersects(b)) { vis = true; break; }
                    if (!vis) inv.Add(($"{(c.transform.parent ? c.transform.parent.name + "/" : "")}{c.name} ({LayerMask.LayerToName(c.gameObject.layer)}, {c.GetType().Name})", c.bounds.size / U, Vector3.Distance(c.bounds.ClosestPoint(sp), sp) / U));
                }
                Log($"ESPREITAR colisores num raio de 30 m: {tot}, sem nada visível no lugar: {inv.Count}{(inv.Count > 0 ? " — " + string.Join("; ", inv.OrderByDescending(x => x.sz.x * x.sz.y * x.sz.z).Take(10).Select(x => $"{x.n} {x.sz.x:0.0}×{x.sz.y:0.0}×{x.sz.z:0.0} m a {x.d:0} m")) : "")}");
            }
            var insets = new[] { .9f, .75f, .6f, .45f, .3f, .15f, 0f }; var afastas = new[] { 1.5f, .75f };   // corpo até a face da cobertura (m)
            int nDiag = 0;
            var st = new Dictionary<string, int[]>();   // estado → [câmera via, viu e acertou, VIU MAS BATEU NA COBERTURA, não via e acertou, tiros, indicador = real]
            GameObject wall = null;
            foreach (string cob in new[] { "carro", "parede" })
            {
                if (cob == "parede")
                {
                    Kill(car); car = null; yield return null;
                    wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "parede_espreitar"; wall.layer = FBWorld.LMap;
                    wall.transform.SetPositionAndRotation(sp + f * 2.4f * U + Vector3.up * 1.5f * U, Quaternion.LookRotation(f)); wall.transform.localScale = new Vector3(3 * U, 3 * U, .25f * U);
                    var wm = new Material(FBShaders.Lit); wm.SetColor("_BaseColor", new Color(.62f, .6f, .56f)); wall.GetComponent<MeshRenderer>().sharedMaterial = wm;
                    Physics.SyncTransforms(); yield return null; var bc = wall.GetComponent<Collider>(); (eL, eR) = Ext(bc, sp, r); nearF = Ext(bc, sp, f).lo;
                }
                foreach (var c in cob == "carro" ? casos : casos.Where(x => x.w == ak || x.nome == "4x").ToList())
                {
                    var cho = FBWeaponModel.Default(c.w); if (c.sc != -2) cho.scope = c.sc; FBWeaponModel.Set(c.w, cho);
                    P.wu.Strip(); P.wu.Give(new[] { c.w.id }); yield return W(.3f); int sl = System.Array.FindIndex(P.wu.slots, x => x != null && x.id == c.w.id); if (sl < 0) { Log($"ESPREITAR {c.w.id}: sem espaço"); continue; }
                    P.wu.Equip(sl, true); yield return W(1.5f);
                    foreach (bool mira in c.modos)
                        foreach (bool depois in new[] { false, true })
                        foreach (float afast in afastas)
                        {
                            Conserto(depois); string est = $"{(depois ? "depois" : "antes")} a {afast * 100:0} cm"; if (!st.ContainsKey(est)) st[est] = new int[6]; var S = st[est];
                            foreach (int lado in new[] { 1, -1 })   // 1 = E (quina da direita), −1 = Q (quina da esquerda)
                            {
                                var row = new List<string>(); float edge = lado > 0 ? eR : eL;
                                foreach (float ins in insets)
                                {
                                    var root = sp + r * (edge - lado * ins * U) + f * (nearF - afast * U); var tp = sp + r * (edge + lado * 1.4f * U) + f * 14 * U;
                                    tgt.hp = tgt.maxHp = 1000; FBMatch.Teleport(tgt, tp, yawF + 180); FBMatch.Teleport(me, root, yawF);
                                    leanQ = lado < 0; leanE = lado > 0; aimT = mira; P.camYaw = yawF; P.camPitch = 0;
                                    float settle = FBOptic.Has(c.w) && mira ? 1.1f : .85f;
                                    for (float t = 0; t < settle; t += Time.deltaTime) { AimAt(P, tgt); yield return null; }
                                    float wt = 0; while ((P.wu.cool > 0 || P.wu.boltT > 0 || P.wu.reloadT > 0) && wt < 4) { wt += Time.deltaTime; AimAt(P, tgt); yield return null; }
                                    if (P.wu.cur >= 0 && P.wu.cur < 3) P.wu.ammo[P.wu.cur] = Mathf.Max(P.wu.ammo[P.wu.cur], 5);
                                    yield return new WaitForEndOfFrame();
                                    // medidas com a câmera final do quadro (a do tiro do próximo Update)
                                    var cam = Camera.main; var cp = cam.transform.position; var ct = Chest(tgt); var mz = me.MuzzlePos; bool fpv = P.firstPerson || P.OpticE >= .5f;
                                    bool see = !Physics.Linecast(cp, ct, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
                                    var bore = fpv ? P.FpMuzzle(cp, cam.transform.forward) : mz; bool boreOk = !Physics.Linecast(bore, ct, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
                                    float La(Vector3 q) => (Vector3.Dot(q - sp, r) - edge) * lado / U * 100;   // cm além da quina (+ = já passou da quina)
                                    float extra = Mathf.Abs(me.PeekShift - me.PeekBase) / U * 100, barErr = Vector3.Angle(me.BarrelDir(), me.aimPoint - mz);
                                    bool Cov(Collider k) => k && (car ? k.GetComponentInParent<FBCar>() == car : k.gameObject == wall);
                                    char predC = !P.ImpactOn ? '?' : P.ImpactHit == tgt ? 'A' : P.ImpactHit ? 'O' : Cov(P.ImpactCol) ? 'C' : 'M'; var predP = P.ImpactPoint;
                                    // diagnóstico (só quando a previsão divergir): raio da câmera × peito, acertos crus, hitbox do peito na física × no osso,
                                    // e a mesma trajetória seca refeita agora, antes e depois de sincronizar a física
                                    var dgCamP = cam.transform.position; var dgCamF = cam.transform.forward; var dgFrom = P.ImpactFrom; string dgPre = null;
                                    {
                                        var cv = ct - dgCamP; float alo = Vector3.Dot(cv, dgCamF); float off = (cv - dgCamF * alo).magnitude / U * 100;
                                        string Nm(Collider k) => k ? (k.GetComponent<FBHitbox>() ? "HB:" + k.GetComponent<FBHitbox>().part : k.name) : "-";
                                        var hs = Physics.RaycastAll(dgCamP, dgCamF, 40 * U, FBWorld.ShotMask, QueryTriggerInteraction.Collide).OrderBy(h => h.distance).Take(4).Select(h => $"{Nm(h.collider)}@{h.distance / U:0.0}").ToArray();
                                        var chb = tgt.GetComponentsInChildren<FBHitbox>().FirstOrDefault(x => x.part == "chest"); float stale = -1;
                                        if (chb && chb.GetComponent<CapsuleCollider>() is CapsuleCollider cc) stale = (cc.transform.TransformPoint(cc.center) - cc.bounds.center).magnitude / U * 100;
                                        var dv = predP - dgFrom; float dl = dv.magnitude; string again = "-", synced = "-";
                                        if (dl > .05f && P.wu.Cur != null)
                                        {
                                            var a1 = FBCombat.BulletFrom(dgFrom, dv / dl, dgFrom, me, P.wu.Cur, 0, false, 40 * U, 1, true); again = a1.hit ? "A:" + a1.part : Nm(a1.col) + $"@{a1.dist / U:0.0}";
                                            Physics.SyncTransforms();
                                            var a2 = FBCombat.BulletFrom(dgFrom, dv / dl, dgFrom, me, P.wu.Cur, 0, false, 40 * U, 1, true); synced = a2.hit ? "A:" + a2.part : Nm(a2.col) + $"@{a2.dist / U:0.0}";
                                        }
                                        dgPre = $"raio da câmera a {off:0} cm do peito · acertos [{string.Join(", ", hs)}] · hitbox peito física×osso {stale:0} cm · previsto {Nm(P.ImpactCol)} L={(predP - dgFrom).magnitude / U:0.0} · refeito {again} · sincronizado {synced}";
                                    }
                                    string ind = P.Fp && P.Fp.ImpactMarkPx is Vector2 mk ? $" Xlente {mk.x - Screen.width * .5f - P.Fp.LensScreenOff.x * Screen.height * .5f:+0;-0},{mk.y - Screen.height * .5f - P.Fp.LensScreenOff.y * Screen.height * .5f:+0;-0}px" : FBHUD.I && FBHUD.I.ImpactShown ? $" Xhud" : "";
                                    bool foto = afast > 1 && (Mathf.Abs(ins - .45f) < .01f && lado > 0 || Mathf.Abs(ins - .3f) < .01f && lado < 0);
                                    string fn = $"esp_{cob}_{c.w.id}_{c.nome}_{(mira ? "mira" : "quadril")}_{(lado > 0 ? "E" : "Q")}{ins * 100:0}_{(depois ? "depois" : "antes")}{afast * 100:0}";
                                    if (foto) Foto(fn);
                                    Vector2 hudPx = FBHUD.I ? FBHUD.I.ImpactPx : default; bool hudOn = FBHUD.I && FBHUD.I.ImpactShown;
                                    int n0 = FBCombat.ShotN; fireEdge = true; fireT = c.w.auto;
                                    for (int i = 0; i < 8 && FBCombat.ShotN == n0; i++) { yield return null; AimAt(P, tgt); }
                                    fireT = false; var L = FBCombat.Last; bool shot = FBCombat.ShotN != n0;
                                    char code = !shot ? '?' : L.hit == tgt ? 'A' : L.hit ? 'O' : L.col && (car ? L.col.GetComponentInParent<FBCar>() == car : L.col.gameObject == wall) ? 'C' : 'M';
                                    if (shot)
                                    {
                                        S[4]++; if (see) { S[0]++; if (code == 'A') S[1]++; else if (code == 'C') S[2]++; } else if (code == 'A') S[3]++;
                                        if (predC == code) S[5]++;
                                    }
                                    string hudErr = hudOn && shot ? $" X↔impacto {Vector2.Distance(hudPx, (Vector2)cam.WorldToScreenPoint(L.end)):0}px" : "";
                                    if (shot && predC != code && nDiag++ < 24)
                                        Log($"ESPREITAR DIAG {est} {cob} {c.w.id} {(mira ? "mira" : "quadril")} {(lado > 0 ? "E" : "Q")}{ins * 100:0}: previsto {predC} real {code} · {dgPre} · câmera no tiro mudou {(cam.transform.position - dgCamP).magnitude / U * 100:0} cm/{Vector3.Angle(cam.transform.forward, dgCamF):0.00}° · origem real×prevista {(FBCombat.LastO - dgFrom).magnitude / U * 100:0} cm · direção {Vector3.Angle(FBCombat.LastDir, predP - dgFrom):0.00}° · real parou em {(L.hit ? "A:" + L.part : L.col ? L.col.name : "-")} a {L.dist / U:0.0} m");
                                    row.Add($"{ins * 100:0}: {(see ? "V" : "-")}{(boreOk ? "c" : "x")}→{code}{(predC != code ? "≠" + predC : "")} [câm {La(cp):+0;-0} cab {(me.Head ? La(me.Head.position) : 0):+0;-0} cano {La(bore):+0;-0}{(extra > .5f ? $" extra {extra:0}" : "")}{(barErr > 3 ? $" arma {barErr:0}°" : "")}{ind}{hudErr}]");
                                }
                                Log($"ESPREITAR {est} {cob} {c.w.id}/{c.nome} {(mira ? "mirando" : "quadril")} {(lado > 0 ? "E" : "Q")} (distância até a quina cm: V/- câmera vê o peito, c/x cano com linha, →tiro real A alvo C cobertura M mapa O outro, ≠ indicador previu outra coisa): {string.Join(" · ", row)}");
                            }
                        }
                }
            }
            leanQ = leanE = false; aimT = false; fireT = false; Conserto(true);
            if (wall) Destroy(wall); if (car) Kill(car); FBStreetEvents.Despawn(tgt);
            foreach (var kv in st) { var S = kv.Value; Log($"ESPREITAR RESUMO {kv.Key}: câmera via o peito em {S[0]} tiros → acertou {S[1]}, BATEU NA COBERTURA {S[2]} · não via e acertou {S[3]} · indicador = tiro real em {S[5]}/{S[4]}"); }
        }
        IEnumerator Mira(FBCharacter me, FBPlayer P)
        {
            marcas ??= Marcas.Select(m => Unity.Profiling.ProfilerRecorder.StartNew(m.cat, m.marca)).ToArray();
            Log($"MIRA marcas do profiler: {string.Join(", ", Marcas.Select((m, i) => m.nome + (marcas[i].Valid ? "" : " (inválida)")))}");
            if (FBVehicles.CarOf(me)) { FBDrive.Exit(me, true); yield return W(1); }
            if (!FBRoads.Ready) FBRoads.Load(FBSession.map);
            var spots = new List<Vector3> { me.Pos }; if (FBRoads.Ready) spots.AddRange(FBRoads.spots.Select(x => x.p).OrderBy(x => (x - me.Pos).sqrMagnitude).Take(40));
            var (mp, md, ml) = LongLine(spots, 1.6f * U, 80); float myaw = Quaternion.LookRotation(md).eulerAngles.y;
            FBMatch.Teleport(me, mp, myaw); P.camYaw = myaw; P.camPitch = 1; yield return W(.8f);
            var casos = new List<(WeaponDef w, int sc, string nome)>();
            var rifle = GameData.Weapons.Values.Where(x => x.cls == "fuzil" && FBRegistry.I.Weapon(x.id)).OrderBy(x => x.id == "m4" ? 0 : 1).FirstOrDefault(x => ScopeNodes(x).Count(n => n.Contains("ATT_Scope_0")) >= 5);
            if (rifle != null) { var nl = ScopeNodes(rifle); casos.Add((rifle, -1, "ferro")); foreach (var nn in new[] { "Scope_04", "Scope_05", "Scope_06", "Scope_07", "Scope_08" }) { int i = nl.FindIndex(x => x.Contains(nn)); if (i >= 0) casos.Add((rifle, i, nn)); } }
            foreach (var wid in new[] { "ar15_prime", "ag14w" })
                if (GameData.Weapons.TryGetValue(wid, out var rd) && FBRegistry.I.Weapon(wid)) { var nl = ScopeNodes(rd); foreach (var nn in new[] { "Ponto_Vermelho", "Holo", "Reflex" }) { int i = nl.FindIndex(x => x.Contains(nn)); if (i >= 0) casos.Add((rd, i, nn)); } break; }
            // r51h: fal_prime/parafal usam a mesma malha de mira de ferro da Barret (SM_Sniper_02_Scope_Default): conferir que miram em 3ª sem lente
            foreach (var sid in new[] { "barret_50", "sniper_hunting", "fal_prime", "parafal" }) if (GameData.Weapons.TryGetValue(sid, out var sw) && FBRegistry.I.Weapon(sid)) casos.Add((sw, -2, "fabrica"));
            if (Arg("-fbjogo_mira") is string only) casos = casos.Where(c => only.Split(',').Any(o => c.nome.Contains(o) || c.w.id == o)).ToList();
            Log($"MIRA: mira óptica {(FBOptic.On ? "LIGADA" : "desligada (-fbmiraantiga)")} · lente com câmera {(FBOptic.PipOn ? "sim" : "não (-fbsempip)")} · celular {FBQuality.Mobile} · tela {Screen.width}×{Screen.height} · linha de visão {ml / U:0} m · {casos.Count} casos: {string.Join(", ", casos.Select(c => c.w.id + "/" + c.nome))}");
            int okN = 0; var bads = new List<string>();
            foreach (var c in casos)
            {
                var cho = FBWeaponModel.Default(c.w); if (c.sc != -2) cho.scope = c.sc; FBWeaponModel.Set(c.w, cho);
                P.wu.Strip(); P.wu.Give(new[] { c.w.id }); yield return W(.3f); int sl = System.Array.FindIndex(P.wu.slots, x => x != null && x.id == c.w.id); if (sl < 0) { Log($"MIRA {c.w.id}: sem espaço"); continue; }
                P.wu.Equip(sl, true); P.camYaw = myaw; P.camPitch = 1; yield return W(1.8f);
                var cam = Camera.main; string tag = $"mira_{c.w.id}_{c.nome}"; bool optic = FBOptic.Has(c.w); float z = FBScopes.Zoom(c.w);
                yield return new WaitForEndOfFrame(); Foto(tag + "_0_quadril"); var tpsPos = cam.transform.position; float hipFov = cam.fieldOfView;
                yield return null; yield return null;   // r51m: a foto (captura + JPG, ~35 ms) caía no 1º quadro da mira — todo traço começava com 55 ms
                // entrada: curva do peso, maior salto da câmera por quadro, quando o corpo some, tiro no meio da transição
                aimT = true; float t = 0, tIn = -1, jump = 0, prevE = P.OpticE, pIn = 0, tMv = 0, vPk = 0; bool mid = false; Vector3 lastC = cam.transform.position; string bodyAt = "-", midShot = "-";
                var trIn = new List<string>(); int gc0 = System.GC.CollectionCount(0); FBPlayer.Diag = true; float dtMax = 0, picoMundo = 0; int fotoQ0 = -9;   // r51i: traço por quadro da entrada (r51i: Barret 0,51 s e pico ÷ média 4,1 com o mesmo código que deu 0,19 s)
                while (t < 1.0f)
                {
                    yield return new WaitForEndOfFrame(); t += Time.deltaTime; float e = P.OpticE, dsp = (cam.transform.position - lastC).magnitude / U; jump = Mathf.Max(jump, dsp); lastC = cam.transform.position;
                    int gcN = System.GC.CollectionCount(0); var fpT = P.Fp; float fdt = Time.unscaledDeltaTime; bool fotoQ = Time.frameCount == fotoQ0 + 1;   // a foto do fim do quadro N pesa no N+1
                    // r51m: quadro depois da foto do meio não conta; quadro longo com física + scripts ≥ metade dele = pico do mundo (rolê), não da mira
                    double mundo = marcas == null ? 0 : ((marcas[2].Valid ? marcas[2].LastValue : 0) + (marcas[6].Valid ? marcas[6].LastValue : 0)) / 1e6;
                    if (e > 0 && (tIn < 0 || t - tIn < .15f) && !fotoQ) { if (fdt > .1f && mundo > fdt * 1000 * .5f) picoMundo = Mathf.Max(picoMundo, fdt); else dtMax = Mathf.Max(dtMax, fdt); }
                    if ((tIn < 0 || t - tIn < .15f) && trIn.Count < 60 && (e > 0 || P.OpticGate != null)) trIn.Add($"{t:0.00}:{fdt * 1000:0}ms{(fotoQ ? "(foto)" : "")} e{e:0.00} {dsp * 100:0}cm{(gcN != gc0 ? $" GC+{gcN - gc0}" : "")}{(fpT && fpT.PipLive ? $" pip{fpT.PipRes}" : "")}{(P.OpticGate != null ? " [" + P.OpticGate.Trim() + "]" : "")}{(MarcasQuadro() is string mq && mq.Length > 0 ? " <" + mq + ">" : "")}");
                    gc0 = gcN;
                    if (Mathf.Abs(e - prevE) > 1e-4f) { pIn += dsp; tMv += Time.deltaTime; vPk = Mathf.Max(vPk, dsp / Mathf.Max(1e-4f, Time.deltaTime)); } prevE = e;
                    if (bodyAt == "-" && !P.BodyVisible) bodyAt = $"{t:0.00}s (e {e:0.00})";
                    if (!mid && e >= .5f)
                    {
                        mid = true; Foto(tag + "_1_meio"); fotoQ0 = Time.frameCount;
                        if (optic && !c.w.IsSniper)
                        {
                            var cf0 = cam.transform.forward; var cp0 = cam.transform.position; float fv0 = cam.fieldOfView; int n0 = FBCombat.ShotN; fireEdge = true; fireT = c.w.auto; yield return null;   // automática dispara pelo "segurar"
                            for (int i = 0; i < 4 && FBCombat.ShotN == n0; i++) yield return null; fireT = false;
                            if (FBCombat.ShotN != n0) { float a = Vector3.Angle(cf0, (FBCombat.LastEnd - cp0).normalized) * Mathf.Deg2Rad; midShot = $"{a * 1000:0.0} mrad ({Mathf.Tan(a) / Mathf.Tan(fv0 * .5f * Mathf.Deg2Rad) * Screen.height * .5f:0} px)"; }
                            lastC = cam.transform.position;
                        }
                    }
                    if (tIn < 0 && (optic ? e >= .999f : t > .3f)) tIn = t;
                }
                yield return W(.6f); float wt = 0; while ((P.wu.cool > 0 || P.wu.boltT > 0) && wt < 3) { wt += Time.deltaTime; yield return null; } yield return W(.25f);
                yield return new WaitForEndOfFrame(); Foto(tag + "_2_mirando");
                var fp = P.Fp; var vm = P.VmCam; bool lens = fp && fp.HasLens && fp.PipLive;
                string geo = "-"; var lensOff = fp ? fp.LensScreenOff : Vector2.zero; if (fp && fp.gameObject.activeInHierarchy && fp.SightWorld is Vector3 sw && vm && vm.enabled) { var vp = vm.WorldToViewportPoint(sw); geo = $"mira (centro da lente/soquete) a {(vp.x - .5f) * Screen.width:+0;-0},{(vp.y - .5f) * Screen.height:+0;-0} px do centro da tela"; }
                bool clip = Physics.CheckSphere(cam.transform.position, Mathf.Max(.05f, cam.nearClipPlane) * 1.5f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
                // r51e: o que fica VISÍVEL entre o olho e a lente (foto da Barret: bloco preto na frente da lente que os números não pegavam) — caixa de
                // cada renderizador visto pela câmera principal ou pela da arma, a até 2 cm do eixo olho → lente; separa rig de 1ª, corpo de 3ª e mundo
                string obst = "-";
                if (optic && fp && fp.SightWorld is Vector3 sw2)
                {
                    var eye = cam.transform.position; var ax = sw2 - eye; float La = ax.magnitude; var axn = ax / Mathf.Max(1e-4f, La); var obs = new List<string>();
                    foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    {
                        if (!r.enabled || !r.gameObject.activeInHierarchy || r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer || r.name.StartsWith("lente_")) continue;
                        int lm = 1 << r.gameObject.layer; if ((cam.cullingMask & lm) == 0 && !(vm && vm.enabled && (vm.cullingMask & lm) != 0)) continue;
                        var b = r.bounds; var q = b.ClosestPoint(eye + axn * Mathf.Clamp(Vector3.Dot(b.center - eye, axn), 0, La * .97f));
                        float al = Vector3.Dot(q - eye, axn); if (al <= 0 || al >= La * .97f || (q - eye - axn * al).magnitude > .02f * me.ScaleK) continue;
                        string dono = r.transform.IsChildOf(me.transform) ? (fp && r.transform.IsChildOf(fp.transform) ? "rig 1ª" : "CORPO 3ª") : "mundo";
                        obs.Add($"{(r.transform.parent ? r.transform.parent.name : "")}/{r.name} ({dono}, {LayerMask.LayerToName(r.gameObject.layer)}, caixa a {al / U * 100:0} cm)");
                    }
                    obst = obs.Count == 0 ? "nada" : string.Join("; ", obs.Take(8));
                }
                float mag = lens ? fp.LensScreenR * Mathf.Tan(hipFov * .5f * Mathf.Deg2Rad) / Mathf.Tan(fp.PipFov * .5f * Mathf.Deg2Rad) : Mathf.Tan(hipFov * .5f * Mathf.Deg2Rad) / Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad);
                // tiro parado: ângulo entre o eixo da câmera e o impacto (= distância ao retículo na lente, que mostra o eixo da câmera)
                var cf = cam.transform.forward; var cpp = cam.transform.position; float fov = cam.fieldOfView; int s0 = FBCombat.ShotN; fireEdge = true; fireT = c.w.auto; yield return null;
                for (int i = 0; i < 6 && FBCombat.ShotN == s0; i++) yield return null; fireT = false;
                string imp = "sem tiro"; float pxErr = -1;
                if (FBCombat.ShotN != s0)
                {
                    float a = Vector3.Angle(cf, (FBCombat.LastEnd - cpp).normalized) * Mathf.Deg2Rad; pxErr = Mathf.Tan(a) / Mathf.Tan(fov * .5f * Mathf.Deg2Rad) * Screen.height * .5f;
                    imp = $"impacto a {a * 1000:0.00} mrad do eixo ({pxErr:0.0} px na tela{(lens ? $", {Mathf.Tan(a) / Mathf.Tan(fp.PipFov * .5f * Mathf.Deg2Rad) * 100:0.0} % do raio da lente" : "")}) a {(FBCombat.LastEnd - cpp).magnitude / U:0} m · origem do tiro a {Vector3.Cross(cf, FBCombat.LastO - cpp).magnitude / U * 100:0.0} cm do eixo";
                }
                // desempenho: 90 quadros com a lente desenhando × sem a câmera da lente
                string perf = "-";
                if (lens)
                {
                    yield return W(.8f); float a1 = 0, m1 = 0; for (int i = 0; i < 90; i++) { yield return null; a1 += Time.unscaledDeltaTime; m1 = Mathf.Max(m1, Time.unscaledDeltaTime); }
                    FBOptic.PipOn = false; yield return W(.2f); float a2 = 0, m2 = 0; for (int i = 0; i < 90; i++) { yield return null; a2 += Time.unscaledDeltaTime; m2 = Mathf.Max(m2, Time.unscaledDeltaTime); }
                    FBOptic.PipOn = true; perf = $"com lente {a1 / 90 * 1000:0.0} ms/quadro (pior {m1 * 1000:0}) × sem {a2 / 90 * 1000:0.0} (pior {m2 * 1000:0}) · RT {fp.PipRes}²";
                    yield return W(.2f);
                }
                // saída
                aimT = false; t = 0; float tOut = -1, jumpOut = 0, prevO = P.OpticE, pOut = 0, tMo = 0, vPo = 0; lastC = cam.transform.position;
                while (t < .8f)
                {
                    yield return new WaitForEndOfFrame(); t += Time.deltaTime; float dso = (cam.transform.position - lastC).magnitude / U; jumpOut = Mathf.Max(jumpOut, dso); lastC = cam.transform.position;
                    if (Mathf.Abs(P.OpticE - prevO) > 1e-4f) { pOut += dso; tMo += Time.deltaTime; vPo = Mathf.Max(vPo, dso / Mathf.Max(1e-4f, Time.deltaTime)); } prevO = P.OpticE; if (tOut < 0 && P.OpticE <= .001f) tOut = t;
                }
                float kIn = tMv > 0 && pIn > 1e-3f ? vPk / (pIn / tMv) : 0, kOut = tMo > 0 && pOut > 1e-3f ? vPo / (pOut / tMo) : 0;
                if (optic) Log($"MIRA-TRAÇO {c.w.id}/{c.nome} entrada (t:dt peso deslocamento [trava]): {string.Join(" | ", trIn)}");
                yield return new WaitForEndOfFrame(); Foto(tag + "_3_saiu"); float back = (cam.transform.position - tpsPos).magnitude / U;
                bool bodyBack = P.BodyVisible, fpOff = !(fp && fp.gameObject.activeSelf);
                var fl = new List<string>();
                if (optic && tIn < 0) fl.Add("não chegou na mira");
                if (optic && !bodyBack) fl.Add("corpo não voltou");
                if (!optic && bodyAt != "-") fl.Add("mira de ferro escondeu o corpo");
                if (optic && fp && fp.HasLens && !lens) fl.Add("lente sem câmera");
                if (clip) fl.Add("câmera dentro de geometria");
                if (pxErr > (c.w.IsSniper ? 6 : 40)) fl.Add($"impacto longe do retículo ({pxErr:0} px)");
                if (optic && (tOut < 0 || !fpOff)) fl.Add("não voltou para a 3ª pessoa");
                if (kIn > 3 || kOut > 3) fl.Add("câmera saltou");
                if (optic && dtMax > .1f) fl.Add($"quadro travado na entrada ({dtMax * 1000:0} ms)");
                if (picoMundo > 0) picosMundo.Add($"{c.w.id}/{c.nome} {picoMundo * 1000:0} ms");
                if (fl.Count == 0) okN++; else bads.Add($"{c.w.id}/{c.nome}: {string.Join(", ", fl)}");
                Log($"MIRA {c.w.id}/{c.nome}: {(optic ? "ÓPTICA 3ª→1ª" : "3ª pessoa (sem óptica)")} · aumento {z:0.##}x (visão de fora {FBScopes.AdsZoom(c.w):0.##}x) · entrou em {tIn:0.00} s (pior quadro {dtMax * 1000:0} ms{(picoMundo > 0 ? $", pico do mundo {picoMundo * 1000:0} ms" : "")}), saiu em {tOut:0.00} s · corpo sumiu {bodyAt}, voltou {bodyBack} · câmera {jump:0.00}/{jumpOut:0.00} m/quadro, pico ÷ média da transição {kIn:0.0}/{kOut:0.0} (suave ≈ 1,5) · "
                    + $"{(lens ? $"lente PiP: FOV {fp.PipFov:0.00}°, raio na tela {fp.LensScreenR * 100:0.0} % da meia-altura, centro da lente {lensOff.x * 100:+0.0;-0.0},{lensOff.y * 100:+0.0;-0.0} %, aumento medido {mag:0.00}x" : $"aumento medido {mag:0.00}x")} · {geo} · entre o olho e a lente: {obst} · {imp} · tiro na transição {midShot} · câmera em parede {clip} · volta da câmera {back:0.00} m · {perf} · retículo do HUD {(FBScopes.Optic ? "escondido" : "visível")}{(fl.Count > 0 ? " · FALHA: " + string.Join(", ", fl) : " · ok")}");
            }
            aimT = false; fireT = false;
            Log($"MIRA RESUMO: {okN}/{casos.Count} casos sem defeito{(bads.Count > 0 ? " · " + string.Join(" | ", bads) : "")}{(picosMundo.Count > 0 ? $" · picos do mundo na entrada (física/scripts do rolê, fora da mira): {string.Join(", ", picosMundo)}" : "")}");
        }
    }

    // r51: pose do Animator ANTES de qualquer correção do quadro (FBRig, IK de mãos/pés e o resto rodam no LateUpdate depois) — separa
    // "o clipe/camada está inclinado" de "alguém entorta a coluna depois"
    [DefaultExecutionOrder(-32000)]
    public class FBPostProbe : MonoBehaviour
    {
        public static FBCharacter who; public static Vector3 head, chest; public static int frame = -1; static FBPostProbe inst;
        public static void Watch(FBCharacter c) { who = c; if (!inst) { inst = new GameObject("FB_PosturaSonda").AddComponent<FBPostProbe>(); DontDestroyOnLoad(inst.gameObject); } }
        void LateUpdate()
        {
            if (!who || !who.an || !who.an.isHuman) return; var hp = who.an.GetBoneTransform(HumanBodyBones.Hips); var hd = who.an.GetBoneTransform(HumanBodyBones.Head);
            var cs = who.an.GetBoneTransform(HumanBodyBones.UpperChest); if (!cs) cs = who.an.GetBoneTransform(HumanBodyBones.Chest);
            if (!hp || !hd) return; head = hd.position - hp.position; chest = cs ? cs.position - hp.position : head; frame = Time.frameCount;
        }
    }
}
