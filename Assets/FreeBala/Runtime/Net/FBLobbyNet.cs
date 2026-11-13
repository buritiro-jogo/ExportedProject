// r62 (07/11/2026, pedido: "retrabalhar toda a parte de jogar online ... tela de lista de amigos, formar squad pra jogar contra outros,
// ou contra bots etc. semelhante a free fire, cod"): conexão de LOBBY com o server.mjs, viva enquanto o jogo está aberto (atravessa a
// partida, então o squad continua junto ao voltar para o menu).
//  - Código de jogador fixo do aparelho (Pid, 6 letras/números, PlayerPrefs fb_pid): é o "código de amigo".
//  - Amigos: lista local (PlayerPrefs fb_friends "PID:NOME|..."); o status (no menu / em partida / offline) vem do 'who' a cada 4 s.
//  - Squad (até 4): convites 'pinv' → aceitar 'pacc' / recusar 'pdec'; pronto 'pready'; líder inicia 'pgo' → o servidor escolhe a sala
//    do mapa e reserva o MESMO time para todos; cada um recebe 'pgo' com a sala e entra (FBSession.room → FBNet entra nela).
// Sem servidor: Status = Off, reconecta a cada 6 s; a tela mostra "servidor offline" e o jogo sozinho contra bots continua normal.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
#endif

namespace FreeBala
{
    public class FBLobbyNet : MonoBehaviour
    {
        public static FBLobbyNet I { get; private set; }
        public enum St { Off, Connecting, On }
        public class Person { public string pid, name, skin, st = "offline"; public bool ready; }
        public class Invite { public string from, leader, name, skin; public float t; }

        public St status = St.Off; public string lastError = "";
        public string leader; public readonly List<Person> party = new();
        public readonly Dictionary<string, Person> seen = new();      // último status conhecido por código (amigos e quem está online)
        public readonly List<Invite> invites = new();
        public int version;                                            // muda a cada novidade (as telas se redesenham quando muda)
        public bool capX2, capRolex, capVoz;                           // r69: o servidor/Host sabe fazer 2 × 2, rolê armado e voz (vem no "hi")
        public string lastSides;                                       // r69: "A + B  ×  C + D" do último sorteio 2 × 2
        public string toast; public float toastT;                      // aviso curto vindo do servidor ("convite enviado", erros)
        public static event Action<Invite> OnInvite;

        public bool InParty => party.Count > 1;
        public bool IsLeader => !InParty || leader == Pid;
        public bool AllReady => party.Where(p => p.pid != leader).All(p => p.ready);
        public bool MeReady => party.FirstOrDefault(p => p.pid == Pid)?.ready ?? false;

        // ------------------------------------------------------------ código de jogador e amigos (locais)
        public static string Pid
        {
            get
            {
                if (pidArg == null) { var a = System.Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, "-fbpid"); pidArg = i >= 0 && i + 1 < a.Length ? CleanPid(a[i + 1]) : ""; }
                if (pidArg.Length >= 4 && pidArg.Length <= 10) return pidArg;                   // r68: teste com dois jogos no mesmo PC (PlayerPrefs é o mesmo)
                var p = PlayerPrefs.GetString("fb_pid", "");
                if (p.Length != 6) { const string A = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; var r = new System.Random(); p = new string(Enumerable.Range(0, 6).Select(_ => A[r.Next(A.Length)]).ToArray()); PlayerPrefs.SetString("fb_pid", p); PlayerPrefs.Save(); }
                return p;
            }
        }
        static string pidArg;
        public static List<(string pid, string name)> Friends
        {
            get
            {
                var l = new List<(string, string)>();
                foreach (var s in PlayerPrefs.GetString("fb_friends", "").Split('|')) { var kv = s.Split(':'); if (kv.Length == 2 && kv[0].Length >= 4) l.Add((kv[0], kv[1])); }
                return l;
            }
        }
        static void SaveFriends(List<(string pid, string name)> l) { PlayerPrefs.SetString("fb_friends", string.Join("|", l.Select(f => f.pid + ":" + (f.name ?? "").Replace("|", "").Replace(":", "")))); PlayerPrefs.Save(); }
        public static bool IsFriend(string pid) => Friends.Any(f => f.pid == pid);
        public static string CleanPid(string s) => new string((s ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        public bool AddFriend(string pid, string name = null)
        {
            pid = CleanPid(pid); if (pid.Length < 4 || pid.Length > 10 || pid == Pid) return false;
            var l = Friends; if (l.Any(f => f.pid == pid)) return false;
            l.Add((pid, name ?? (seen.TryGetValue(pid, out var p) && !string.IsNullOrEmpty(p.name) ? p.name : "AMIGO"))); SaveFriends(l); Who(); version++; return true;
        }
        public void RemoveFriend(string pid) { var l = Friends; l.RemoveAll(f => f.pid == pid); SaveFriends(l); version++; }
        public static string NameOf(string pid) { if (I && I.seen.TryGetValue(pid, out var p) && !string.IsNullOrEmpty(p.name)) return p.name; var f = Friends.FirstOrDefault(x => x.pid == pid); return f.name ?? pid; }
        public string StatusOf(string pid) => seen.TryGetValue(pid, out var p) ? p.st : "offline";

        public static FBLobbyNet Ensure()
        {
            FBVoiceChat.Ensure();   // r69: o chat de voz vive junto com o lobby (menu e partida)
            if (I) return I; var g = new GameObject("FB_LobbyNet"); DontDestroyOnLoad(g); return g.AddComponent<FBLobbyNet>();
        }
        void Awake() { if (I && I != this) { Destroy(gameObject); return; } I = this; }

        // ------------------------------------------------------------ ações
        public void Who() { if (status != St.On) return; var pids = Friends.Select(f => (object)f.pid).ToList(); foreach (var p in party) pids.Add(p.pid); Send(new() { ["t"] = "who", ["pids"] = pids }); }
        public void InviteTo(string pid) => Send(new() { ["t"] = "pinv", ["to"] = pid });
        public void Accept(Invite iv) { invites.Remove(iv); version++; Send(new() { ["t"] = "pacc", ["leader"] = iv.leader }); }
        public void Decline(Invite iv) { invites.Remove(iv); version++; Send(new() { ["t"] = "pdec", ["leader"] = iv.leader }); }
        public void LeaveParty() => Send(new() { ["t"] = "pleave" });
        public void Kick(string pid) => Send(new() { ["t"] = "pkick", ["pid"] = pid });
        public void SetReady(bool on) => Send(new() { ["t"] = "pready", ["on"] = on });
        public void StartParty(string map, string mode) => Send(new() { ["t"] = "pgo", ["map"] = map, ["mode"] = mode, ["nm"] = FBSession.netMode });   // r69: nm = modo online do SQUAD
        public void SendVoice(int q, string d) { if (status == St.On && InParty) Send(new() { ["t"] = "pvc", ["q"] = q, ["d"] = d }); }                   // r69: voz do squad no menu
        void Toast(string s) { toast = s; toastT = Time.unscaledTime; version++; }

        // ------------------------------------------------------------ mensagens
        void Recv(Dictionary<string, object> m)
        {
            switch (Json.S(m, "t"))
            {
                case "hi":
                    {
                        status = St.On; lastError = ""; var caps = Json.L(m, "caps") ?? new List<object>();
                        capX2 = caps.Contains("x2"); capRolex = caps.Contains("rolex"); capVoz = caps.Contains("voz"); version++; Who(); break;
                    }
                case "pvc": FBVoiceChat.OnLobbyVoice(Json.S(m, "from"), Json.S(m, "name"), Json.I(m, "q"), Json.S(m, "d")); break;   // r69
                case "who":
                    foreach (var o in Json.L(m, "list") ?? new List<object>())
                    {
                        var d = o as Dictionary<string, object>; var pid = Json.S(d, "pid"); if (string.IsNullOrEmpty(pid)) continue;
                        if (!seen.TryGetValue(pid, out var p)) seen[pid] = p = new Person { pid = pid };
                        var n = Json.S(d, "name"); if (!string.IsNullOrEmpty(n)) p.name = n; var sk = Json.S(d, "skin"); if (!string.IsNullOrEmpty(sk)) p.skin = sk; p.st = Json.S(d, "st") ?? "offline";
                    }
                    { var l = Friends; bool ch = false; for (int i = 0; i < l.Count; i++) if (seen.TryGetValue(l[i].pid, out var p) && !string.IsNullOrEmpty(p.name) && p.name != l[i].name) { l[i] = (l[i].pid, p.name); ch = true; } if (ch) SaveFriends(l); }
                    version++; break;
                case "pinv":
                    {
                        var iv = new Invite { from = Json.S(m, "from"), leader = Json.S(m, "leader"), name = Json.S(m, "name") ?? "JOGADOR", skin = Json.S(m, "skin"), t = Time.unscaledTime };
                        invites.RemoveAll(x => x.leader == iv.leader); invites.Add(iv); version++; FBAudio.Synth("hover"); OnInvite?.Invoke(iv); break;
                    }
                case "party":
                    {
                        leader = Json.S(m, "leader"); party.Clear();
                        foreach (var o in Json.L(m, "members") ?? new List<object>())
                        {
                            var d = o as Dictionary<string, object>; var p = new Person { pid = Json.S(d, "pid"), name = Json.S(d, "name"), skin = Json.S(d, "skin"), st = Json.S(d, "st"), ready = Json.B(d, "ready") };
                            party.Add(p); if (!string.IsNullOrEmpty(p.pid)) seen[p.pid] = p;
                        }
                        version++; break;
                    }
                case "pok": Toast(Json.S(m, "msg") ?? ""); break;
                case "perr": Toast(Json.S(m, "msg") ?? ""); break;
                case "pgo":
                    {
                        // o líder iniciou: todos entram na mesma sala do servidor, no mesmo time
                        FBSession.map = Json.S(m, "map") ?? FBSession.map; FBSession.room = Json.S(m, "room"); FBSession.online = true;
                        // r69: modo ONLINE do líder ("nm"): rolê (desarmado ou armado) roda o De Rolê; times e 2 × 2 rodam o mata-mata online.
                        // Servidor antigo não manda "nm": vale o "mode" como na r68.
                        var nm = Json.S(m, "nm");
                        if (string.IsNullOrEmpty(nm)) { var md = Json.S(m, "mode"); nm = md == "role" ? "role" : "tdm"; if (md == "role" || md == "classico" || md == "squad" || md == "10x10") FBSession.mode = md; }
                        FBSession.netMode = nm == "rolex" || nm == "x2" || nm == "tdm" ? nm : "role";
                        if (FBSession.netMode == "role" || FBSession.netMode == "rolex") FBSession.mode = "role"; else if (FBSession.mode == "role") FBSession.mode = "classico";
                        if (m.ContainsKey("team")) FBSession.team = Mathf.Clamp(Json.I(m, "team"), 0, 1);
                        lastSides = null;
                        if (Json.L(m, "lados") is List<object> ls && ls.Count == 2)
                            lastSides = string.Join("  ×  ", ls.Select(x => string.Join(" + ", ((x as List<object>) ?? new List<object>()).Select(n => (n ?? "").ToString().ToUpper()))));
                        FBSession.Save();
                        Toast(lastSides != null ? "2 × 2 SORTEADO: " + lastSides : "ENTRANDO NA PARTIDA DO SQUAD · " + FBSession.NetModeName(FBSession.netMode)); if (FBMenu.I) FBMenu.I.GoOnline(); break;
                    }
            }
        }

        // ------------------------------------------------------------ transporte (mesmo do FBNet)
        readonly ConcurrentQueue<string> inbox = new(); float retryT, whoT;
#if !UNITY_WEBGL || UNITY_EDITOR
        ClientWebSocket ws; CancellationTokenSource cts; volatile bool closed = true;
        // r68b: cada conexão tem o seu socket e o seu leitor (w, c). Antes o leitor lia o campo 'ws': depois de um Reconnect (HOSPEDAR,
        // ENTRAR num Host, SAIR DA REDE) o leitor da conexão VELHA passava a ler a nova e o '_close' dele derrubava a conexão nova.
        async void Connect()
        {
            status = St.Connecting; version++; closed = false;
            try { cts?.Cancel(); ws?.Dispose(); } catch { }
            var w = new ClientWebSocket(); var c = new CancellationTokenSource(); ws = w; cts = c; var url = FBSession.Server;   // r68: Host da rede local, se houver
            try
            {
                var t = w.ConnectAsync(new Uri(url), c.Token); if (await Task.WhenAny(t, Task.Delay(5000)) != t) { try { c.Cancel(); } catch { } throw new Exception("tempo esgotado"); }
                await t; if (w != ws) { try { w.Dispose(); } catch { } return; }
                inbox.Enqueue("{\"t\":\"_open\"}"); _ = Task.Run(() => Receive(w, c));
            }
            catch (Exception e) { if (w != ws) return; lastError = e.Message; closed = true; inbox.Enqueue("{\"t\":\"_close\"}"); }
        }
        async Task Receive(ClientWebSocket w, CancellationTokenSource c)
        {
            var buf = new byte[1 << 16]; var sb = new StringBuilder();
            while (w.State == WebSocketState.Open)
            {
                WebSocketReceiveResult r; try { r = await w.ReceiveAsync(new ArraySegment<byte>(buf), c.Token); } catch { break; }
                if (r.MessageType == WebSocketMessageType.Close) break;
                sb.Append(Encoding.UTF8.GetString(buf, 0, r.Count)); if (r.EndOfMessage) { if (w == ws) inbox.Enqueue(sb.ToString()); sb.Clear(); }
            }
            if (w == ws) { closed = true; inbox.Enqueue("{\"t\":\"_close\"}"); }
        }
        void SendRaw(string s) { if (ws == null || ws.State != WebSocketState.Open) return; var b = Encoding.UTF8.GetBytes(s); try { _ = ws.SendAsync(new ArraySegment<byte>(b), WebSocketMessageType.Text, true, cts.Token); } catch { } }
        void OnDestroy() { if (I == this) I = null; try { cts?.Cancel(); ws?.Dispose(); } catch { } }
        public void Reconnect() { try { cts?.Cancel(); ws?.Dispose(); } catch { } ws = null; status = St.Off; retryT = 0; version++; }
#else
        // WebGL: a ponte JS só atende o FBNet da partida; no navegador o lobby fica offline (jogo sozinho e sala direta continuam)
        void Connect() { status = St.Off; lastError = "indisponível no navegador"; retryT = 1e9f; }
        void SendRaw(string s) { }
        void OnDestroy() { if (I == this) I = null; }
        public void Reconnect() { }
#endif
        void Send(Dictionary<string, object> o) { if (status == St.On) SendRaw(FBNet.ToJson(o)); }

        void Update()
        {
            while (inbox.TryDequeue(out var s))
            {
                Dictionary<string, object> m; try { m = Json.Parse(s) as Dictionary<string, object>; } catch { continue; }
                var t = Json.S(m, "t");
                if (t == "_open") { SendRaw(FBNet.ToJson(new Dictionary<string, object> { ["t"] = "hello", ["lobby"] = true, ["pid"] = Pid, ["name"] = FBSession.name, ["skin"] = FBSession.skin, ["unity"] = true })); continue; }
                if (t == "_close") { bool was = status == St.On; status = St.Off; party.Clear(); leader = null; version++; retryT = was ? 1 : 6; continue; }
                try { Recv(m); } catch (Exception e) { Debug.LogWarning("[FBLobbyNet] " + e.Message); }
            }
            float dt = Time.unscaledDeltaTime;
            if (status == St.Off) { retryT -= dt; if (retryT <= 0) { retryT = 6; Connect(); } }
            if (status == St.On) { whoT -= dt; if (whoT <= 0) { whoT = 4; Who(); } }
            for (int i = invites.Count - 1; i >= 0; i--) if (Time.unscaledTime - invites[i].t > 60) { invites.RemoveAt(i); version++; }   // convite vale 1 min
        }
    }
}
