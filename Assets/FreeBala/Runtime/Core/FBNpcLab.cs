// Autoteste da ROTINA dos pedestres do 171 (rodada 43): FreeBala.exe -fbnpclab [mapa]
// No rolê: 60 s olhando a cidade (pausas, conversas, falas, reações por contagem), fotos de quem anda com atividade (celular, cigarro…)
// ao lado de quem anda normal, de uma pausa e de uma dupla conversando; depois força as reações: esbarrão do jogador, mira, tiro perto
// e batida de carro. Log [FreeBala][npclab], fotos autoteste/npc_*.png
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBNpcLab : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbnpclab"); if (i < 0) return;
            var g = new GameObject("FB_NpcLab"); DontDestroyOnLoad(g); var t = g.AddComponent<FBNpcLab>(); if (i + 1 < a.Length && !a[i + 1].StartsWith("-")) t.map = a[i + 1];
        }
        string map = "vila_jardim", dir; Camera cam;
        static void Log(string s) => Debug.Log("[FreeBala][npclab] " + s);
        IEnumerator Wait(float s) { for (float w = 0; w < s; w += Time.unscaledDeltaTime) yield return null; }
        IEnumerator Shot(Vector3 pos, Vector3 look, string n, float fov = 50)
        {
            cam.transform.position = pos; cam.transform.LookAt(look); cam.fieldOfView = fov; for (int k = 0; k < 3; k++) yield return null;
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "npc_" + n + ".png")); yield return null;
        }
        IEnumerator Side(FBCharacter c, string n, float dist = 3.2f)
        {
            if (!c) yield break; var f = Quaternion.Euler(0, c.Yaw, 0) * Vector3.forward; var r = Vector3.Cross(Vector3.up, f);
            yield return Shot(c.Pos + r * dist + f * .8f + Vector3.up * 1.3f, c.Pos + Vector3.up * .95f, n);
        }
        FBRoleNpc R(FBCharacter c) => c ? c.GetComponent<FBRoleNpc>() : null;
        IEnumerator Start()
        {
            dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"); System.IO.Directory.CreateDirectory(dir);
            yield return Wait(3); GameData.Load(); FBSession.Load(); var keepMap = FBSession.map; var keepMode = FBSession.mode;
            FBSession.map = map; FBSession.mode = "role"; FBSession.online = false; FBSession.view = "tps"; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_" + map); while (op != null && !op.isDone) yield return null; yield return Wait(8);
            FBMatch.God = true; var me = FBMatch.Player; if (!me) { Log("sem jogador"); yield break; }
            cam = new GameObject("NpcCam").AddComponent<Camera>(); cam.depth = 100; cam.cullingMask = Camera.main ? Camera.main.cullingMask : ~0; cam.nearClipPlane = .05f;
            Log($"rotina ligada {FBNpcRotina.On} · falas prontas {FBNpcVoz.Ready} · NPCs {FBRole.npcs.Count(n => n)}");
            var withAct = FBRole.npcs.Where(n => n && FBNpcRotina.ActOf(n) != null).ToList();
            Log($"com atividade: {withAct.Count} — " + string.Join(", ", withAct.GroupBy(n => FBNpcRotina.ActOf(n).id).Select(g => g.Key + " " + g.Count())));
            // 60 s olhando a cidade
            int p0 = FBNpcRotina.Pauses, c0 = FBNpcRotina.Convs, s0 = FBNpcVoz.Said, r0 = FBNpcRotina.Reacts; var seenSt = new System.Collections.Generic.Dictionary<string, int>();
            for (float t = 0; t < 60; t += 1)
            {
                foreach (var n in FBRole.npcs) { var r = R(n); if (r) seenSt[r.st] = seenSt.TryGetValue(r.st, out var k) ? k + 1 : 1; }
                yield return Wait(1);
            }
            Log($"60 s: pausas {FBNpcRotina.Pauses - p0} · rodas de conversa {FBNpcRotina.Convs - c0} · falas {FBNpcVoz.Said - s0} (última {FBNpcVoz.LastLine}) · reações {FBNpcRotina.Reacts - r0}");
            Log("estados (NPC×s): " + string.Join(", ", seenSt.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " " + kv.Value)));
            // fotos: andando com atividade (e um normal para comparar), pausa, conversa
            foreach (var g in withAct.Where(n => R(n)?.st == "walk").GroupBy(n => FBNpcRotina.ActOf(n).id).Take(4)) { var n = g.First(); yield return Side(n, "andar_" + g.Key); Log($"foto andar_{g.Key}: {n.skinId} ({n.npcSet?.sex}) clipe {n.npcSet?.walk} vel {n.walkSpeed:0.00}"); }
            var normal = FBRole.npcs.FirstOrDefault(n => n && FBNpcRotina.ActOf(n) == null && R(n)?.st == "walk" && n.npcSet?.sex == "m"); yield return Side(normal, "andar_normal_m");
            var pa = FBRole.npcs.FirstOrDefault(n => n && R(n)?.st == "pause"); if (pa) { yield return Side(pa, "pausa", 2.6f); Log($"foto pausa: {pa.skinId} clipe {pa.dance?.clip}"); } else Log("nenhum NPC em pausa agora");
            var tk = FBRole.npcs.FirstOrDefault(n => n && R(n)?.st == "talk" && R(n).talkC != null);
            if (tk) { var C = R(tk).talkC.Value; yield return Shot(C + new Vector3(3.2f, 1.5f, 2.4f), C + Vector3.up * 1f, "conversa"); Log($"foto conversa: " + string.Join(", ", FBRole.npcs.Where(n => n && R(n)?.talkC == C).Select(n => $"{n.skinId}:{n.dance?.clip}"))); }
            // conversa forçada: dois NPCs livres lado a lado, 14 s ouvindo as falas (comentário -> resposta -> fim)
            var A = FBRole.npcs.Where(n => n && n.alive && R(n) && R(n).Free).OrderBy(n => (n.Pos - me.Pos).sqrMagnitude).ToList();
            if (A.Count >= 2)
            {
                int cv0 = FBNpcRotina.Convs, s1 = FBNpcVoz.Said; R(A[0]).TalkWith(R(A[1])); var lines = new System.Collections.Generic.List<string>(); string last = null;
                for (float t = 0; t < 14; t += Time.deltaTime) { if (FBNpcVoz.LastLine != last) { last = FBNpcVoz.LastLine; if (last != null && last.StartsWith("conv_")) lines.Add(last); } yield return null; }
                var C2 = (A[0].Pos + A[1].Pos) / 2; yield return Shot(C2 + new Vector3(2.6f, 1.5f, 2f), C2 + Vector3.up, "conversa_forcada");
                Log($"conversa forçada: {A[0].skinId} ({FBNpcVoz.VozOf(A[0])}) × {A[1].skinId} ({FBNpcVoz.VozOf(A[1])}) · rodas +{FBNpcRotina.Convs - cv0} · falas: {string.Join(" → ", lines)} · clipes {A[0].dance?.clip}/{A[1].dance?.clip}");
            }
            // reações forçadas perto do jogador
            FBCharacter Pick(float maxD = 60) => FBRole.npcs.Where(n => n && n.alive && R(n) && R(n).Free && (n.Pos - me.Pos).magnitude < maxD).OrderBy(n => (n.Pos - me.Pos).sqrMagnitude).FirstOrDefault();
            var cc = me.GetComponent<CharacterController>();
            void Put(Vector3 p) { if (cc) cc.enabled = false; me.transform.position = p; if (cc) cc.enabled = true; }
            // esbarrão: jogador anda para dentro do NPC
            var b = Pick(); if (b)
            {
                var f = Quaternion.Euler(0, b.Yaw, 0) * Vector3.forward; Put(b.Pos + f * 1.6f); me.Yaw = Quaternion.LookRotation(-f).eulerAngles.y;
                FBPlayer.TestInput = inp => { if (FBPlayer.I && b) FBPlayer.I.camYaw = Quaternion.LookRotation((b.Pos - me.Pos).Flat()).eulerAngles.y; inp.move = new Vector2(0, 1); return inp; };   // o andar segue a câmera
                for (float t = 0; t < 1.2f && R(b)?.st != "react"; t += Time.deltaTime) yield return null; FBPlayer.TestInput = null;
                yield return Wait(.35f); yield return Side(b, "esbarrao", 2.8f); Log($"esbarrão: {b.skinId} estado {R(b).st} clipe {b.dance?.clip} · fala {FBNpcVoz.LastLine}");
                yield return Wait(2);
            }
            // mira
            var m = Pick(); if (m)
            {
                Put(m.Pos + (Quaternion.Euler(0, m.Yaw, 0) * Vector3.forward) * 6); me.GetComponent<FBWeaponUser>()?.Give("38");
                yield return Wait(.5f);
                FBPlayer.TestInput = inp => { if (FBPlayer.I && m) { var cm = Camera.main; var to = m.Pos + Vector3.up * 1.2f * m.ScaleK - (cm ? cm.transform.position : me.Pos + Vector3.up * 1.6f); FBPlayer.I.camYaw = Quaternion.LookRotation(to.Flat()).eulerAngles.y; FBPlayer.I.camPitch = -Mathf.Atan2(to.y, to.Flat().magnitude) * Mathf.Rad2Deg; } inp.aim = true; return inp; };
                for (float t = 0; t < 2.5f && R(m)?.st != "scared"; t += Time.deltaTime) yield return null;
                yield return Side(m, "mira", 3); Log($"mira: {m.skinId} estado {R(m).st} mãos para cima {m.surrender} · jogador mirando {me.aiming} armado {!me.Unarmed} · fala {FBNpcVoz.LastLine}"); FBPlayer.TestInput = null; yield return Wait(4);
                Log($"depois da mira: estado {R(m).st}");
            }
            // tiro perto
            var near = FBRole.npcs.Where(n => n && n.alive && (n.Pos - me.Pos).magnitude < 30).ToList(); FBBotBrain.Noise(me.Pos); yield return Wait(1.5f);
            Log($"tiro: {near.Count(n => R(n)?.st == "flee")} de {near.Count} correndo · fala {FBNpcVoz.LastLine}");
            var fl = near.FirstOrDefault(n => R(n)?.st == "flee"); if (fl) yield return Side(fl, "fuga", 4);
            yield return Wait(8);
            // batida
            var e = Pick(80); if (e) { var at = e.Pos + (Quaternion.Euler(0, e.Yaw, 0) * Vector3.forward) * 12; FBNpcRotina.Event("acidente", at, 34, 1); yield return Wait(1.2f); Log($"batida: olhando {FBRole.npcs.Count(n => R(n)?.st == "look")} · fala {FBNpcVoz.LastLine}"); yield return Side(e, "batida_olhando", 3); }
            Log($"total: falas {FBNpcVoz.Said} · reações {FBNpcRotina.Reacts} · pausas {FBNpcRotina.Pauses}");
            Log("fim"); FBSession.map = keepMap; FBSession.mode = keepMode; FBSession.Save(); yield return Wait(1); Application.Quit();
        }
    }
}
