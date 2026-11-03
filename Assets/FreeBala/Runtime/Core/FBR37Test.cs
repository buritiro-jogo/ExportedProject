// Autoteste da RODADA 37 numa partida de verdade: FreeBala.exe -fbr37 <mapa>[:modo] [segundos de combate]
// Saída em <build>/autoteste/r37/ e no log ([FreeBala][r37t]):
//  1) câmera livre — entrada simulada (andar para a frente + segurar a câmera livre + girar o mouse): o rumo do movimento e a frente do
//     corpo NÃO podem mudar enquanto a câmera gira; ao soltar, a câmera volta para trás do personagem (tempo medido); prints;
//  2) luminária — noite forçada, tiro na luminária mais perto à vista: luz, brilho e CABEÇA (emissão por bloco de propriedades) apagados;
//     prints antes/depois;
//  3) combate com bots — jogador invulnerável parado num ponto aberto; telemetria de cada tiro dos bots (distância, acerto) por faixa,
//     tiro mais longe, quem acertou de onde; print do indicador de direção do dano no primeiro acerto.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public static class FBCombatStats
    {
        public static bool On; public static readonly List<(float d, bool hit)> botShots = new(); public static readonly List<(float d, string who, bool head)> playerHits = new();
        public static void BotShot(float d) { if (On) botShots.Add((d, false)); }
        public static void Damage(FBCharacter src, FBCharacter victim, bool head)
        {
            if (!On || !src || !victim || src == victim) return; float d = (src.Pos - victim.Pos).magnitude;
            if (src.isBot && botShots.Count > 0) for (int i = botShots.Count - 1; i >= 0 && i >= botShots.Count - 8; i--) if (!botShots[i].hit && Mathf.Abs(botShots[i].d - d) < 3) { botShots[i] = (botShots[i].d, true); break; }
            if (victim.isPlayer) playerHits.Add((d, src.Name, head));
        }
    }

    public class FBR37Test : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbr37"); if (i < 0) return;
            var g = new GameObject("FB_R37Teste"); DontDestroyOnLoad(g); var t = g.AddComponent<FBR37Test>();
            var mm = i + 1 < a.Length && !a[i + 1].StartsWith("-") ? a[i + 1].Split(':') : new[] { "vila_jardim" }; t.map = mm[0]; t.mode = mm.Length > 1 ? mm[1] : "tatico";
            if (i + 2 < a.Length && float.TryParse(a[i + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s)) t.secs = s;
        }
        string map = "vila_jardim", mode = "tatico"; float secs = 60; string dir;
        static void Log(string s) => Debug.Log("[FreeBala][r37t] " + s);
        IEnumerator Shot(string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png")); yield return null; yield return null; }

        IEnumerator MotoTest(FBCharacter me, string tag)
        {
            var car = FBVehicles.Spawn("xj6", me.Pos + me.transform.forward * 4, me.transform.eulerAngles.y); if (!car) { Log("moto: xj6 não nasceu"); yield break; }
            yield return null; bool ok = FBDrive.Enter(me, car); float t = 0; var an = me.an;
            FBPlayer.TestInput = inp => { inp.move = t > 2.5f && t < 5.5f ? new Vector2(0, 1) : Vector2.zero; inp.look = t > 7f && t < 9.5f ? new Vector2(4f, .3f) : Vector2.zero; inp.fire = inp.firePressed = inp.aim = inp.aimPressed = inp.freeLook = false; return inp; };
            var shots = new[] { (2f, "parado"), (5f, "andando"), (7f, "parou"), (10f, "camera_girada") }; int si = 0; float handMax = 0, headMax = 0;
            for (; t < 10.5f; t += Time.deltaTime)
            {
                yield return null;
                var dp = me.GetComponent<FBDriverPose>(); var T = dp ? dp.Last : default;
                if (T.hands && an) { float dl = (an.GetBoneTransform(HumanBodyBones.LeftHand).position - T.hL).magnitude, dr = (an.GetBoneTransform(HumanBodyBones.RightHand).position - T.hR).magnitude; if (t > 1) handMax = Mathf.Max(handMax, Mathf.Max(dl, dr)); }
                var hd = an ? an.GetBoneTransform(HumanBodyBones.Head) : null; if (hd && t > 1) headMax = Mathf.Max(headMax, Vector3.Angle(Vector3.ProjectOnPlane(hd.forward, Vector3.up), Vector3.ProjectOnPlane(car.transform.forward, Vector3.up)));
                if (si < shots.Length && t >= shots[si].Item1) { yield return Shot($"moto_{tag}_{shots[si].Item2}"); si++; }
            }
            int LU = an.GetLayerIndex("Upper Body"), LE = an.GetLayerIndex("Emote Full Body");
            Log($"moto ({tag}): entrou {ok} · pose {me.sys} · camada corpo inteiro {(LE >= 0 ? an.GetLayerWeight(LE) : -1):0.00} · camada braços {(LU >= 0 ? an.GetLayerWeight(LU) : -1):0.00} · InCombat {me.CombatStance} · mãos longe do guidão até {handMax * 100:0} cm · cabeça virada até {headMax:0}° da frente da moto");
            FBPlayer.TestInput = null; FBDrive.Exit(me, true); for (float w = 0; w < .8f; w += Time.deltaTime) yield return null;
            FBVehicles.list.Remove(car); Destroy(car.gameObject); yield return null;
        }

        // entrando na moto a partir da 1ª PESSOA (o usuário vinha da 1ª pessoa — print da luneta): anda em 1ª pessoa, monta, câmera do jogo
        IEnumerator MotoFromFp(FBCharacter me)
        {
            var P = FBPlayer.I; if (!P) yield break; bool tv = !P.FirstPerson; float t = 0;
            FBPlayer.TestInput = inp => { inp.toggleView = tv; tv = false; inp.move = t < 1.5f ? new Vector2(0, 1) : Vector2.zero; inp.look = t > 4.5f && t < 5.7f ? new Vector2(5f, 0) : Vector2.zero; inp.fire = inp.aim = inp.freeLook = inp.interact = false; return inp; };
            for (; t < 2f; t += Time.deltaTime) yield return null;
            Log($"moto da 1ª pessoa: em 1ª pessoa {P.FirstPerson}");
            var car = FBVehicles.Spawn("start", me.Pos + me.transform.forward * 3, me.transform.eulerAngles.y); if (!car) car = FBVehicles.Spawn("xj6", me.Pos + me.transform.forward * 3, me.transform.eulerAngles.y);
            if (car) { var cc1 = me.GetComponent<CharacterController>(); if (cc1) cc1.enabled = false; me.transform.position = car.transform.position - car.transform.right * 1.2f; if (cc1) cc1.enabled = true; }
            if (!car) { FBPlayer.TestInput = null; yield break; }
            yield return null; bool ok = FBDrive.Enter(me, car);
            for (; t < 4f; t += Time.deltaTime) yield return null; yield return Shot("moto_da_1p_atras");
            { var go = new GameObject("CamMoto1p"); var c2 = go.AddComponent<Camera>(); c2.depth = 50; c2.fieldOfView = 40; var cc0 = car.transform.position + Vector3.up * 1.1f;
              c2.transform.position = cc0 - car.transform.right * 3.6f + Vector3.up * .3f; c2.transform.LookAt(cc0); yield return Shot("moto_da_1p_lado"); Destroy(go); }
            for (; t < 6.2f; t += Time.deltaTime) yield return null; yield return Shot("moto_da_1p_frente");
            int vis = 0, all = 0; foreach (var r in me.GetComponentsInChildren<Renderer>(true)) { all++; if (r.enabled && r.gameObject.activeInHierarchy && r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly && !r.forceRenderingOff) vis++; }
            var dp = me.GetComponent<FBDriverPose>(); var T = dp ? dp.Last : default; var an = me.an; float hm = 0;
            if (T.hands && an) hm = Mathf.Max((an.GetBoneTransform(HumanBodyBones.LeftHand).position - T.hL).magnitude, (an.GetBoneTransform(HumanBodyBones.RightHand).position - T.hR).magnitude);
            var ra = an ? an.GetBoneTransform(HumanBodyBones.RightUpperArm) : null; var la = an ? an.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null;
            int fpLeft = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Count(r => r && r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy && r.gameObject.layer == FBWorld.LFirstPerson);
            Log($"moto da 1ª pessoa ({car.id}): objetos de 1ª pessoa ainda visíveis no mundo {fpLeft} · entrou {ok} · pose {me.sys} · camada corpo {an.GetLayerWeight(an.GetLayerIndex("Emote Full Body")):0.00} · renderizadores do corpo visíveis {vis}/{all} · mãos longe do guidão {hm * 100:0} cm · escala braço d/e {(ra ? ra.lossyScale.x : 0):0.00}/{(la ? la.lossyScale.x : 0):0.00}");
            FBPlayer.TestInput = null; FBDrive.Exit(me, true); for (float w = 0; w < .8f; w += Time.deltaTime) yield return null;
            if (P.FirstPerson) P.ForceThirdPerson(); FBVehicles.list.Remove(car); Destroy(car.gameObject); yield return null;
        }

        // moto ESTACIONADA do mapa (como no teste do usuário), entrando com F depois de correr até ela; câmera do jogo girada até a frente
        IEnumerator MotoParked(FBCharacter me)
        {
            var bikes = FBVehicles.list.Where(c => c && c.kind == "moto" && !c.driver).OrderBy(c => (c.transform.position - me.Pos).sqrMagnitude).Take(3).ToList();
            Log($"motos estacionadas no mapa: {FBVehicles.list.Count(c => c && c.kind == "moto")}");
            int n = 0;
            foreach (var car in bikes)
            {
                n++; var side = car.transform.position - car.transform.right * 2.2f; var cc = me.GetComponent<CharacterController>(); if (cc) cc.enabled = false; me.transform.position = side; if (cc) cc.enabled = true;
                float t = 0; for (int k = 0; k < 3; k++) yield return null;
                if (!FBDrive.Enter(me, car)) { Log($"moto do mapa {car.id}: não entrou"); continue; }
                FBPlayer.TestInput = inp => { inp.move = Vector2.zero; inp.interact = false; inp.look = t > 3f && t < 4.2f ? new Vector2(5f, -.6f) : Vector2.zero; inp.fire = inp.aim = inp.freeLook = false; return inp; };
                for (; t < 5f; t += Time.deltaTime) { yield return null; if (t > 2.4f && t < 2.4f + Time.deltaTime * 1.5f) yield return Shot($"moto_mapa{n}_{car.id}_atras"); }
                yield return Shot($"moto_mapa{n}_{car.id}_frente");
                var an = me.an; var T = me.GetComponent<FBDriverPose>()?.Last ?? default; float hm = 0;
                if (T.hands && an) hm = Mathf.Max((an.GetBoneTransform(HumanBodyBones.LeftHand).position - T.hL).magnitude, (an.GetBoneTransform(HumanBodyBones.RightHand).position - T.hR).magnitude);
                Log($"moto do mapa {car.id}: dirigindo {FBVehicles.Driving(me)} · pose {me.sys} · mãos longe do guidão {hm * 100:0} cm · grau {car.wheelie:0.00} · manobra {car.trick:0.00}");
                FBPlayer.TestInput = null; if (FBVehicles.Driving(me)) FBDrive.Exit(me, true); for (float w = 0; w < .8f; w += Time.deltaTime) yield return null;
            }
        }

        // todos os jogáveis na xj6 (o defeito do usuário pode depender do rig da skin): prints de lado/frente numa câmera própria + mãos × guidão
        IEnumerator MotoSkins(FBCharacter me)
        {
            if (!FBMatch.I || !FBRegistry.I) yield break;
            var go = new GameObject("CamMoto"); var cam = go.AddComponent<Camera>(); cam.depth = 50; cam.fieldOfView = 40;
            var basePos = me.Pos + me.transform.right * 8; float yaw = me.transform.eulerAngles.y; var worst = new List<string>();
            var skins = GameData.Skins.Where(x => x.id.StartsWith("r_") && FBRegistry.I.HasChar(x.id)).Select(x => x.id).ToList();
            if (skins.Count > 1) { skins.Add(skins[0]); skins.RemoveAt(0); }      // o 1º da fila caía no chão (teste) — vai para o fim para separar teste × personagem
            foreach (var sk in skins)
            {
                var car = FBVehicles.Spawn("xj6", basePos, yaw); if (!car) { Log("moto: xj6 não nasceu"); break; }
                var a = FBMatch.I.Spawn(sk, "", 0, basePos + Vector3.up * .2f, false); if (!a) { FBVehicles.list.Remove(car); Destroy(car.gameObject); continue; }
                foreach (var b in a.GetComponents<FBBot>()) Destroy(b);
                yield return null; FBDrive.SeatTest(a, car);
                float hm = 0; var an = a.an;
                for (float w = 0; w < 1.4f; w += Time.deltaTime)
                {
                    yield return null; var T = a.GetComponent<FBDriverPose>()?.Last ?? default;
                    if (w > .8f && T.hands && an) hm = Mathf.Max(hm, Mathf.Max((an.GetBoneTransform(HumanBodyBones.LeftHand).position - T.hL).magnitude, (an.GetBoneTransform(HumanBodyBones.RightHand).position - T.hR).magnitude));
                }
                var c = car.transform.position + Vector3.up * 1.1f;
                cam.transform.position = c - car.transform.right * 3.6f + Vector3.up * .3f; cam.transform.LookAt(c); yield return Shot($"motoskin_{sk}_lado");
                cam.transform.position = c + car.transform.forward * 3.4f - car.transform.right * 1.2f + Vector3.up * .2f; cam.transform.LookAt(c); yield return Shot($"motoskin_{sk}_frente");
                Log($"moto {sk}: mãos longe do guidão até {hm * 100:0} cm"); if (hm > .05f) worst.Add($"{sk} {hm * 100:0} cm");
                a.gameObject.SetActive(false); Destroy(a.gameObject); FBVehicles.list.Remove(car); Destroy(car.gameObject); yield return null;
            }
            Destroy(go); Log("moto (todas as skins): " + (worst.Count == 0 ? "todas com as mãos no guidão" : "mãos fora: " + string.Join(", ", worst)));
        }

        IEnumerator FpAds(FBCharacter me)
        {
            var P = FBPlayer.I; if (!P || !P.wu || !FBRegistry.I) yield break;
            var ids = new List<string>();
            foreach (var cls in new[] { "fuzil", "submetralhadora", "pistola", "escopeta" }) { var id = GameData.Weapons.Values.Where(x => x.cls == cls && FBRegistry.I.Weapon(x.id)).Select(x => x.id).FirstOrDefault(); if (id != null) ids.Add(id); }
            ids.AddRange(GameData.Weapons.Values.Where(x => x.cls == "sniper" && FBRegistry.I.Weapon(x.id)).Select(x => x.id));
            bool tv = true, aimOn = false;
            FBPlayer.TestInput = inp => { inp.toggleView = tv; tv = false; inp.aim = aimOn; inp.aimPressed = false; inp.fire = inp.firePressed = false; inp.move = Vector2.zero; inp.look = Vector2.zero; inp.freeLook = false; return inp; };
            for (int k = 0; k < 6; k++) yield return null;
            foreach (var id in ids)
            {
                P.wu.Strip(); P.wu.Give(id); int sl = System.Array.FindIndex(P.wu.slots, x => x != null && x.id == id); P.wu.Equip(sl >= 0 ? sl : 0, true);
                aimOn = false; for (float w = 0; w < 1.2f; w += Time.deltaTime) yield return null;
                aimOn = true; for (float w = 0; w < 1.4f; w += Time.deltaTime) yield return null;
                var fp = P.GetComponentInChildren<FBFpRig>(true); int vis = 0, all = 0; if (fp) foreach (var r in fp.GetComponentsInChildren<Renderer>(true)) { all++; if (r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy) vis++; }
                Log($"1ª pessoa mirando {id}: luneta {FBScopes.Scoped} · rig de 1ª pessoa {(fp ? (fp.gameObject.activeInHierarchy ? "ativo" : "inativo") : "nenhum")} · renderizadores visíveis {vis}/{all}");
                yield return Shot($"fp_mira_{id}");
            }
            aimOn = false; tv = true; for (int k = 0; k < 6; k++) yield return null;
            FBPlayer.TestInput = null; P.wu.Strip(); P.wu.Give(FBSession.primary, FBSession.smg, FBSession.secondary); P.wu.Equip(0, true);
            for (int k = 0; k < 10; k++) yield return null;
        }

        IEnumerator Start()
        {
            dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste", "r37"); System.IO.Directory.CreateDirectory(dir);
            for (float w = 0; w < 4; w += Time.unscaledDeltaTime) yield return null;
            GameData.Load(); FBSession.Load(); FBSession.map = map; FBSession.mode = mode; FBSession.online = false; FBSession.view = "tps"; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_" + map); while (op != null && !op.isDone) yield return null;
            for (float w = 0; w < 5; w += Time.unscaledDeltaTime) yield return null;
            var P = FBPlayer.I; var me = FBMatch.Player; if (!P || !me) { Log("FALHA: partida sem jogador"); Application.Quit(1); yield break; }
            FBMatch.God = true;

            // ---- 1) câmera livre
            {
                float yaw0 = P.camYaw, t = 0; var heads = new List<float>(); float camMax = 0;
                FBPlayer.TestInput = inp => { inp.move = new Vector2(0, 1); inp.freeLook = t > .6f && t < 2.6f; inp.freeLookPressed = false; inp.look = t > .7f && t < 2.4f ? new Vector2(2.2f, .25f) : Vector2.zero; inp.fire = inp.firePressed = inp.aim = inp.aimPressed = false; return inp; };
                bool shot = false;
                for (; t < 2.6f; t += Time.deltaTime)
                {
                    yield return null; var v = me.velocity.Flat(); if (v.magnitude > .5f && t > .8f) heads.Add(Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg);
                    camMax = Mathf.Max(camMax, Mathf.Abs(Mathf.DeltaAngle(P.ViewYaw, P.camYaw)));
                    if (!shot && t > 1.4f) { shot = true; yield return Shot("camera_livre_olhando"); }
                }
                float hMin = heads.Count > 0 ? heads.Min() : 0, hMax = heads.Count > 0 ? heads.Max() : 0;
                // soltou a câmera livre (t = 2,6 s): tempo até voltar para trás do personagem (< 2°)
                float back = 0; for (; back < 3 && Mathf.Abs(Mathf.DeltaAngle(P.ViewYaw, P.camYaw)) > 2; back += Time.deltaTime) { yield return null; t += Time.deltaTime; }
                for (; t < 4.2f; t += Time.deltaTime) yield return null;
                FBPlayer.TestInput = null; yield return Shot("camera_livre_voltou");
                Log($"câmera livre: câmera girou até {camMax:0}° do corpo · rumo do movimento variou {Mathf.DeltaAngle(hMin, hMax):0.0}° · yaw do corpo {Mathf.DeltaAngle(yaw0, P.camYaw):0.0}° · voltou em {back:0.00} s");
            }

            // ---- 1b) moto (22/10, teste do usuário: "as animações/rig das motos estão todas quebradas") — o jogador ARMADO monta na xj6,
            // fica parado, acelera, solta, gira a câmera do jogo até a frente; prints da câmera do JOGO e medidas da pose do piloto
            yield return MotoTest(me, "armado");
            if (FBPlayer.I && FBPlayer.I.wu) { FBPlayer.I.wu.Strip(); me.SetUnarmedLocomotion(true); for (int k = 0; k < 10; k++) yield return null; }
            yield return MotoTest(me, "desarmado");
            yield return MotoFromFp(me);
            yield return MotoParked(me);
            yield return MotoSkins(me);
            if (FBPlayer.I && FBPlayer.I.wu) { FBPlayer.I.wu.Give(FBSession.primary, FBSession.smg, FBSession.secondary); FBPlayer.I.wu.Equip(0, true); for (int k = 0; k < 10; k++) yield return null; }

            // ---- 1c) 1ª pessoa mirando (22/10: "erros nas miras em primeira pessoa de algumas armas, a arma está aparecendo junto a mira"):
            // uma arma de cada classe + todas as sniper, mirando parado; print da tela e se os braços/arma de 1ª pessoa somem na luneta
            yield return FpAds(me);

            // ---- 2) luminária à noite
            {
                FBWeather.ForceNight(true); for (float w = 0; w < 2.5f; w += Time.deltaTime) yield return null;
                var cam = Camera.main; int li = -1; float bd = 1e9f; var L = FBStreetLights.Lamps;
                // cobertura: luminárias com cabeça separada (FBLampPrep) e as que ficaram no renderizador do poste
                var withHead = new HashSet<int>(FBLampHead.All.Where(h => h && h.lamp >= 0).Select(h => h.lamp));
                Log($"luminárias: {L.Count} no mapa · {withHead.Count} com cabeça apagável · {FBLampHead.All.Count(h => h && h.lamp < 0)} cabeças sem luminária ligada");
                for (int i = 0; i < L.Count; i++) { if (!withHead.Contains(i)) continue; float d = (L[i].p - me.Pos).magnitude; if (d < bd && d > 4) { bd = d; li = i; } }   // a mais perto COM cabeça
                if (li < 0) for (int i = 0; i < L.Count; i++) { float d = (L[i].p - me.Pos).magnitude; if (d < bd) { bd = d; li = i; } }
                if (li >= 0)
                {
                    var lp = L[li].p; var go = new GameObject("CamLuz"); var c2 = go.AddComponent<Camera>(); c2.depth = 50; c2.fieldOfView = 45; c2.transform.position = lp + (me.Pos - lp).Flat().normalized * 7 + Vector3.down * 2.5f; c2.transform.LookAt(lp);
                    for (int k = 0; k < 20; k++) yield return null; yield return Shot("luminaria_antes");
                    FBStreetPoleFx.Ray(lp + Vector3.down * 3, Vector3.up, 6);
                    for (float w = 0; w < 1.5f; w += Time.deltaTime) yield return null; yield return Shot("luminaria_depois");
                    var heads = FBLampHead.All.Where(h => h && h.lamp == li).ToList(); string headSt = "sem cabeça separada (bloco no renderizador do poste)";
                    if (heads.Count > 0) { var mpb = new MaterialPropertyBlock(); var r = heads[0].GetComponent<Renderer>(); r.GetPropertyBlock(mpb, 0); headSt = $"{heads.Count} cabeça(s), emissão no bloco {mpb.GetColor("_EmissionColor")}"; }
                    Log($"luminária {li} a {bd:0} m: apagada {FBStreetLights.IsDead(li)} · {headSt}");
                    Destroy(go);
                }
                else Log("luminária: nenhuma no mapa");
                FBWeather.ForceNight(false);
            }

            // ---- 3) combate com bots (telemetria)
            {
                FBCombatStats.On = true; FBCombatStats.botShots.Clear(); FBCombatStats.playerHits.Clear(); bool shotInd = false, front = false; float diagT = 0;
                // invencível zerava o dano ANTES da contagem (FBCombat: God → dmg 0): no combate o jogador leva o dano de verdade e a vida é
                // reposta a cada quadro (o indicador de direção e a telemetria veem o acerto)
                FBMatch.God = false; me.maxHp = Mathf.Max(me.maxHp, 100000); me.hp = me.maxHp;
                for (float w = 0; w < secs; w += Time.deltaTime)
                {
                    yield return null; me.hp = me.maxHp; if ((diagT -= Time.deltaTime) <= 0) { diagT = 10; Log($"t={w:0} s · {FBBot.Diag()} · tiros de bots {FBCombatStats.botShots.Count}"); }
                    // metade do tempo: o jogador vai para a linha de frente (ao lado de um aliado em combate) para levar tiro de verdade
                    if (!front && w > secs * .5f)
                    {
                        var ally = FBMatch.Bots.FirstOrDefault(b => b && b.alive && !FBMatch.Hostile(b, me) && b.GetComponent<FBBot>() is FBBot fb && (fb.ai == "combat" || fb.ai == "cover"));
                        if (ally) { front = true; var cc = me.GetComponent<CharacterController>(); if (cc) cc.enabled = false; me.transform.position = ally.Pos + ally.transform.right * 1.6f; if (cc) cc.enabled = true; Log($"t={w:0} s · jogador levado para perto de {ally.Name} (em combate)"); }
                    }
                    if (!shotInd && FBCombatStats.playerHits.Count > 0) { shotInd = true; for (int k = 0; k < 3; k++) yield return null; yield return Shot("indicador_dano"); }
                }
                FBCombatStats.On = false; var S = FBCombatStats.botShots;
                string Band(float a, float b) { var x = S.Where(s => s.d >= a && s.d < b).ToList(); return $"{a:0}–{b:0} m: {x.Count} tiros, {x.Count(s => s.hit)} acertos ({(x.Count > 0 ? 100f * x.Count(s => s.hit) / x.Count : 0):0}%)"; }
                Log($"bots: {S.Count} tiros · mais longe {(S.Count > 0 ? S.Max(s => s.d) : 0):0} m · " + string.Join(" · ", new[] { Band(0, 15), Band(15, 35), Band(35, 60), Band(60, 100), Band(100, 999) }));
                var H = FBCombatStats.playerHits; Log($"jogador atingido {H.Count}× · distâncias {string.Join(",", H.Take(30).Select(h => h.d.ToString("0")))} · mais longe {(H.Count > 0 ? H.Max(h => h.d) : 0):0} m");
            }
            Log("fim"); yield return null;
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbauto") < 0) Application.Quit(0);
        }
    }
}
