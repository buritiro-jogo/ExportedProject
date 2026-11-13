// Autoteste de LAN com DOIS jogos no mesmo PC (r68d): Host e cliente entram no De Rolê online do mesmo mapa (o FBNet entra na sala do
// mesmo mapa/modo) e cada um grava o que vê do outro em autoteste/lan_<papel>.txt (+ fotos lan_<papel>_*.png):
//   Host:    Buritiro.exe -fbpid HOSTA1 -fblanhost -fblanteste <mapa> host
//   Cliente: Buritiro.exe -fbpid CLIB22 -fblanjoin 127.0.0.1 -fblanteste <mapa> cliente      (abrir depois que o Host estiver na sala)
// Roteiro (t = desde que um viu o outro na sala):
//   0–14 s  cada um no carro da garagem: o outro tem que ver esse carro como "de outro dono" (rcar);
//   15 s    Host desce a 7 m da frente da loja 1 e anda de lado 2 vezes; cliente desce no balcão da loja 1, de costas para ela;
//   17 s    cliente compra a pistola mais barata PELO HOST (FBNet.ShopAuthority): desconta, fica sua e vai para a mão;
//   22 s    cliente mira no Host (mesma conta do FBPiloto) e dá 3 tiros; o Host confere os tiros mostrados (som + clarão) e a arma
//           comprada na mão do outro. r68e: no De Rolê todos são aliados (time 0), a bala atravessa e não fere; fora dele vale o dano.
//   32/34 s conferência final dos carros enquanto os dois ainda estão na sala (antes o Host conferia depois do cliente sair).
// Linhas "pos <ms UTC> eu|outro x y z" a cada 0,2 s: tools/lan/analisa_lan.py compara o que um faz com o que o outro vê.
// Só neste teste: runInBackground ligado (os dois jogos rodam sem foco). No fim devolve saldo, armas, mapa e modo do perfil.
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBLanTeste : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fblanteste"); if (i < 0) return;
            Application.runInBackground = true;
            var g = new GameObject("FB_LanTeste"); DontDestroyOnLoad(g); var t = g.AddComponent<FBLanTeste>();
            t.map = i + 1 < a.Length && !a[i + 1].StartsWith("-") ? a[i + 1] : "praca_aguas";
            t.host = !(i + 2 < a.Length && a[i + 2] == "cliente");
        }

        static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        static readonly FieldInfo fFric = typeof(FBPlayer).GetField("assistFric", BindingFlags.NonPublic | BindingFlags.Instance);
        string map, dir; bool host; string Papel => host ? "host" : "cliente";
        readonly List<string> rel = new(); int falhas; System.Action restore;
        float t0 = -1, nextPos; FBCharacter mirar; int atirar, danoN, acertos; float danoSoma, erroMira = -1;

        static long Ms => System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        static string V(Vector3 p) => $"({p.x.ToString("0.0", CI)}, {p.y.ToString("0.0", CI)}, {p.z.ToString("0.0", CI)})";
        void Log(string s) { Debug.Log($"[FreeBala][lan-teste] {Papel} {s}"); rel.Add($"{Ms} {s}"); if (s.Contains("FALHA")) falhas++; }
        void Pos(string quem, Vector3 p) => rel.Add($"pos {Ms} {quem} {p.x.ToString("0.000", CI)} {p.y.ToString("0.000", CI)} {p.z.ToString("0.000", CI)}");
        static IEnumerator Wait(float s) { for (float w = 0; w < s; w += Time.unscaledDeltaTime) yield return null; }
        IEnumerator Ate(float t) { while (Time.unscaledTime - t0 < t) yield return null; }
        IEnumerator Shot(string name) { yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"lan_{Papel}_{name}.png")); yield return null; }
        void OnApplicationQuit() => restore?.Invoke();

        void Update()
        {
            if (t0 < 0 || Time.unscaledTime < nextPos) return; nextPos = Time.unscaledTime + .2f;
            var me = FBMatch.Player; if (me) Pos("eu", me.Pos);
            var o = FBMatch.Remotes.FirstOrDefault(c => c); if (o) Pos("outro", o.Pos);
        }

        IEnumerator Start()
        {
            var root = System.IO.Path.GetDirectoryName(Application.dataPath);
            dir = System.IO.Path.Combine(root, "autoteste"); System.IO.Directory.CreateDirectory(dir);
            FBInput.TestMove = Vector2.zero; yield return Wait(3);
            GameData.Load(); FBSession.Load();
            int keepMoney = FBSession.money; string keepOwned = PlayerPrefs.GetString(FBGunShop.PREF, ""), keepMap = FBSession.map, keepMode = FBSession.mode, keepView = FBSession.view; bool keepOnline = FBSession.online;
            restore = () => { FBSession.money = keepMoney; PlayerPrefs.SetString(FBGunShop.PREF, keepOwned); FBGunShop.ReloadOwned(); FBSession.map = keepMap; FBSession.mode = keepMode; FBSession.view = keepView; FBSession.online = keepOnline; FBSession.Save(); };
            Log($"papel {Papel} · pid {FBLobbyNet.Pid} · mapa {map} · saldo do perfil guardado $ {keepMoney}");
            float w = 0; while (string.IsNullOrEmpty(FBSession.lanServer) && w < 60) { w += Time.unscaledDeltaTime; yield return null; }
            if (string.IsNullOrEmpty(FBSession.lanServer)) { Log("FALHA: sem servidor da LAN (faltou -fblanhost / -fblanjoin?)"); yield return Fim(); yield break; }
            Log($"servidor da LAN: {FBSession.lanServer} · hospedando aqui: {FBLanHost.Hosting}");

            FBSession.map = map; FBSession.mode = "role"; FBSession.online = true; FBSession.view = "tps"; FBSession.money = 60000; FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_" + map); if (op == null) { Log($"FALHA: a cena Map_{map} não existe"); yield return Fim(); yield break; }
            while (!op.isDone) yield return null;
            yield return Wait(4); FBGarage.AutoPick(); yield return Wait(2); FBGarage.AutoPick();
            var net = FBNet.I; w = 0;
            while ((!net || !net.InRoom) && w < 45) { w += Time.unscaledDeltaTime; yield return null; net = FBNet.I; }
            if (!net || !net.InRoom) { Log($"FALHA: não entrou na sala ({(net ? net.status : "sem FBNet")})"); yield return Fim(); yield break; }
            Log($"na sala {net.roomId} ({net.roomMode}) · eu id {net.myId} · caps loja {net.capLoja} rcar {net.capRcar} · compra pelo Host {net.ShopAuthority} · ping {net.ping:0} ms");
            w = 0; while (!FBMatch.Remotes.Any(c => c) && w < 180) { w += Time.unscaledDeltaTime; yield return null; }
            var outro = FBMatch.Remotes.FirstOrDefault(c => c);
            if (!outro) { Log("FALHA: o outro jogo não apareceu na sala em 180 s"); yield return Fim(); yield break; }
            var orr = outro.GetComponent<FBRemote>();
            t0 = Time.unscaledTime; Log($"vi o outro jogo: id {(orr ? orr.netId : -1)} '{(orr ? orr.displayName : "?")}' em {V(outro.Pos)} · na sala comigo: {FBMatch.Remotes.Count(c => c)}");
            FBDamageEvents.OnDamage += Dano;
            yield return host ? Host(outro) : Cliente(outro);
            yield return Fim();
        }

        void Carros(string quando, FBCharacter outro)
        {
            var orr = outro ? outro.GetComponent<FBRemote>() : null; int dono = orr ? orr.netId : -99;
            var deOutros = FBVehicles.list.Where(c => c && c.netOwner >= 0).ToList(); var meu = FBVehicles.CarOf(FBMatch.Player);
            bool ok = deOutros.Any(c => c.netOwner == dono);
            Log($"{quando}: {(ok ? "OK" : "FALHA")} carro do outro jogador aqui: {deOutros.Count} carro(s) de outro dono [{string.Join("; ", deOutros.Select(c => $"{c.id} dono {c.netOwner} em {V(c.transform.position)}{(c.remoteDriver >= 0 ? $" motorista {c.remoteDriver}" : "")}"))}]" +
                $" · o outro está em {(outro ? V(outro.Pos) : "?")} · eu {(meu ? "no carro " + meu.id : "a pé")}");
        }

        IEnumerator Host(FBCharacter outro)
        {
            var me = FBMatch.Player;
            yield return Ate(6); Carros("6 s", outro);
            yield return Ate(12); Carros("12 s", outro); yield return Shot("carros");
            yield return Ate(15);
            var s = FBGunShop.shops.FirstOrDefault(); if (s == null || !me) { Log("FALHA: sem loja ou sem jogador"); yield break; }
            if (FBVehicles.CarOf(me)) { FBDrive.Exit(me, true); yield return Wait(1f); }
            var F = Quaternion.Euler(0, s.yaw, 0) * Vector3.forward; var M = s.front + F * 7; float yaw = s.yaw + 180;
            FBMatch.Teleport(me, M, yaw); FBPlayer.I?.OnRespawn(yaw); yield return Wait(.5f);
            Log($"Host a 7 m da loja 1 em {V(me.Pos)} · vida {me.hp:0}");
            for (int k = 0; k < 2; k++) { FBInput.TestMove = new Vector2(1, 0); yield return Wait(1f); FBInput.TestMove = new Vector2(-1, 0); yield return Wait(1f); }
            FBInput.TestMove = Vector2.zero;
            yield return Ate(21); float hp0 = me.hp; int vistos0 = FBRemote.ShotsShown; Log($"Host parado em {V(me.Pos)} esperando os tiros · vida {hp0:0}");
            yield return Ate(30); yield return Shot("depois_tiros");
            // r68e: no De Rolê online todos ficam no time 0 (aliados): a bala ATRAVESSA o outro jogador (FBCombat: "aliado") e não fere.
            // O que tem de chegar é o tiro VISÍVEL (som + clarão na arma do outro) e a arma comprada na mão dele.
            int vistos = FBRemote.ShotsShown - vistos0; bool aliados = !FBMatch.Hostile(outro, me);
            string armaOutro = outro ? outro.weapon?.id : null;
            Log($"{(vistos >= 3 ? "OK" : "FALHA")} tiros do cliente mostrados aqui (som + clarão): {vistos} de 3 · arma na mão do outro: {armaOutro ?? "nenhuma"}");
            if (aliados) Log($"{(danoN == 0 ? "OK" : "FALHA")} aliados no De Rolê: o tiro não fere ({danoN} acerto(s), dano {danoSoma:0}) · vida {hp0:0} → {me.hp:0}");
            else Log($"{(danoN > 0 ? "OK" : "FALHA")} tiros do cliente chegaram pela rede: {danoN} acerto(s), dano {danoSoma:0} · vida {hp0:0} → {me.hp:0}");
            yield return Ate(32); Carros("fim", outro);                                                      // r68e: antes do cliente sair (ele sai aos ~36 s)
            yield return Ate(40);
        }

        IEnumerator Cliente(FBCharacter outro)
        {
            var me = FBMatch.Player;
            yield return Ate(6); Carros("6 s", outro);
            yield return Ate(12); Carros("12 s", outro); yield return Shot("carros");
            yield return Ate(15);
            var s = FBGunShop.shops.FirstOrDefault(); if (s == null || !me) { Log("FALHA: sem loja ou sem jogador"); yield break; }
            if (FBVehicles.CarOf(me)) { FBDrive.Exit(me, true); yield return Wait(1f); }
            FBMatch.Teleport(me, s.front, s.yaw); FBPlayer.I?.OnRespawn(s.yaw);                     // no balcão, olhando para a rua (onde o Host fica)
            yield return Ate(17);
            var net = FBNet.I; bool auth = net && net.ShopAuthority;
            var arma = FBGunShop.ShopData(me).Where(c => c.cat != "EQUIPAMENTO").SelectMany(c => c.items).Where(i => !i.own && !i.gear)
                .OrderBy(i => i.cls == "pistola" ? 0 : 1).ThenBy(i => i.price).FirstOrDefault();
            if (arma == null) Log("FALHA: nada para comprar na vitrine");
            else
            {
                int m0 = FBSession.money; FBGunShop.Buy(arma); string pedido = FBGunShop.Pending;
                float w = 0; while (FBGunShop.Pending != null && w < 8) { w += Time.unscaledDeltaTime; yield return null; }
                yield return Wait(.6f);
                bool ok = FBSession.money == m0 - arma.price && FBGunShop.Owns(arma.id) && me.weapon?.id == arma.id;
                Log($"{(ok ? "OK" : "FALHA")} compra {(auth ? "PELO HOST (o servidor confere)" : "SEM autoridade do Host")}: {arma.id} ({arma.name}) $ {arma.price} · pedido {(pedido != null ? "enviado" : "NÃO enviado")}" +
                    $" · saldo {m0} → {FBSession.money} · é sua {FBGunShop.Owns(arma.id)} · na mão {me.weapon?.id} · status da loja: {FBGunShop.Status}");
                if (!auth) Log("FALHA: a sala não tem a compra conferida pelo Host");
            }
            yield return Ate(22);
            mirar = outro; FBPlayer.TestInput = Entrada; yield return Wait(1.6f);
            yield return Shot("mirando");
            var P = FBPlayer.I; int t0s = P ? P.ShotCount : 0; float erroAntes = erroMira;
            for (int k = 0; k < 3; k++) { atirar = 2; yield return Wait(.6f); }
            yield return Wait(1.2f);
            int tiros = P ? P.ShotCount - t0s : 0;
            Log($"{(tiros > 0 ? "OK" : "FALHA")} tiros dados: {tiros} · acertos no Host vistos aqui: {acertos}{(!FBMatch.Hostile(outro, me) ? " (aliados no De Rolê: a bala atravessa)" : "")} · erro da mira antes do 1º tiro {erroAntes:0.0}°");
            FBPlayer.TestInput = null; mirar = null;
            yield return Ate(34); Carros("fim", outro);
            yield return Ate(36);
        }

        // mira no peito do outro jogador com a mesma conta do FBPiloto (graus divididos pelo fator que o FBPlayer.Look aplica)
        FBInputState Entrada(FBInputState s)
        {
            var P = FBPlayer.I; if (!P || !mirar || !P.cam) return s;
            Vector3 d = mirar.Pos + Vector3.up * 1.15f - P.cam.transform.position;
            float yawA = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, pitchA = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            float dy = Mathf.DeltaAngle(P.camYaw, yawA), dp = pitchA - P.camPitch; erroMira = Vector3.Angle(P.cam.transform.forward, d);
            float k = 1f - Mathf.Exp(-14f * Time.deltaTime), cap = 400f * Time.deltaTime;
            float fr = fFric != null ? (float)fFric.GetValue(P) : 1f; if (fr < .05f) fr = 1f;
            s.look = new Vector2(Mathf.Clamp(dy * k, -cap, cap), Mathf.Clamp(dp * k, -cap, cap)) / fr;
            if (atirar > 0) { s.fire = true; s.firePressed = atirar == 2; atirar--; }
            return s;
        }

        void Dano(FBDamage d)
        {
            if (t0 < 0) return;
            if (d.victim && d.victim == FBMatch.Player) { danoN++; danoSoma += d.amount; Log($"dano recebido: {d.amount:0} de {(d.source ? d.source.name : "?")} ({d.type}{(d.head ? ", cabeça" : "")}) · vida {d.hpBefore:0} → {d.hpAfter:0}"); }
            else if (d.victim && d.victim.isRemote) { acertos++; Log($"acerto no outro jogador: {d.amount:0} ({(d.head ? "cabeça" : "corpo")})"); }
        }

        IEnumerator Fim()
        {
            FBDamageEvents.OnDamage -= Dano; FBPlayer.TestInput = null; FBInput.TestMove = null; mirar = null;
            restore?.Invoke(); restore = null;
            Log($"fim · {falhas} falha(s) · saldo e armas do perfil devolvidos ($ {FBSession.money})");
            try { System.IO.File.WriteAllLines(System.IO.Path.Combine(dir, $"lan_{Papel}.txt"), rel); } catch { }
            yield return Wait(1);
            Application.Quit(0);
        }
    }
}
