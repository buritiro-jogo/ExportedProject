// REDE LOCAL (pedido 10/11/2026: "dois ou mais computadores conectados à mesma rede local ... um jogador cria a sessão como Host, os outros
// encontram ou se conectam pelo endereço IP local"). Antes só existia o servidor em Node (game/server.mjs), que NÃO vai no instalador: na
// rede de casa ninguém conseguia ser Host sem instalar o Node e a pasta do jogo web.
// Este é o MESMO protocolo do server.mjs, embutido no jogo (o FBNet e o FBLobbyNet não percebem diferença):
//   hello / list / create / join / leave / st / ev / chat / ping  +  lobby r62 (who, pinv, pacc, pdec, pleave, pkick, pready, pgo)
// e o que a rodada da loja trouxe para os dois servidores:
//   - modo de sala 'role' (De Rolê cooperativo: todos no time 0, sala sem placar nem fim);
//   - COMPRA com autoridade do servidor: 'wallet' (o cliente informa o saldo e as armas que já tem; o Host guarda a carteira do jogador) e 'buy' (pedido com id
//     único 'rid': o servidor confere preço no PRÓPRIO catálogo, saldo e posse, desconta e responde; pedido repetido devolve a mesma resposta,
//     então clique duplo ou reenvio não compra duas vezes).
// Descoberta: responde em UDP 8125 a "BURITIRO?" com {t:"buritiro", name, port, players, rooms}.
// Sem UnityEngine de propósito: roda em threads próprias (a sala continua viva enquanto o Host carrega a cena) e é testado fora do Unity
// (unity/tools/lan/). Toda a lógica de sala roda numa fila única (como o laço de eventos do Node): nada de trava espalhada.
// r68b (11/11/2026, revisão): carteira POR JOGADOR (pid) que sobrevive à queda; resposta de compra nunca leva saldo -1; 'bchk' (consulta de
// compra depois de cair, nunca cobra); envio por fila e thread de cada cliente (um PC lento não trava mais a sala); nada estoura depois do
// PARAR; "conectados" conta jogadores, não conexões.
#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace FreeBala
{
    public sealed class FBLanServer : IDisposable
    {
        public const int DefaultPort = 8123, DiscoveryPort = 8125, MatchTime = 600;
        public const string DiscoveryAsk = "BURITIRO?";
        public static readonly string[] Maps = { "vila_jardim", "praca_aguas", "praca_pt", "orla", "centro_civico", "morro_sacola", "bv_city", "bermuda" };
        public static readonly HashSet<string> Consumable = new() { "ammo", "colete", "capacete", "kit", "medkit" };
        // r69 (13/11/2026): 'rolex' = ROLÊ ARMADO (cidade livre, todos armados, sem fim) e 'x2' = 2 × 2 sorteado do squad de 4 (sala privada)
        static readonly Dictionary<string, int> Limit = new() { ["tdm"] = 40, ["ffa"] = 25, ["role"] = 0, ["rolex"] = 0, ["x2"] = 20 };
        static readonly List<object> Caps = new() { "role", "loja", "rcar", "rolex", "x2", "voz" };     // o jogo confere no 'hi': servidor antigo não tem estes
        static bool IsRole(Room r) => r != null && (r.mode == "role" || r.mode == "rolex");                 // cidade livre: sem placar, sem fim, com loja
        static bool TeamMode(Room r) => r != null && (r.mode == "tdm" || r.mode == "x2");
        static bool PodeEntrar(Cl c, Room r) => r.allow == null || (c.pid != null && r.allow.Contains(c.pid)); // sala privada do 2 × 2
        const int VozMax = 6000;                                                                             // voz: base64 de 100 ms ~ 1,1 kB

        public int Port { get; private set; }
        public string HostName = "BURITIRO";
        public volatile bool Running;
        public Func<string, int> PriceOf = _ => -1;                 // catálogo do Host (-1 = não existe)
        public Action<string> Log = _ => { };
        public int Clients => players;                              // r68b: JOGADORES distintos (antes contava conexões: lobby + partida = 2 por pessoa)
        public string Error;

        TcpListener tcp; UdpClient udp; Thread acceptT, loopT, udpT, tickT;
        readonly BlockingCollection<Action> q = new();
        readonly Dictionary<string, Room> rooms = new(); int nextId = 1, tickN; volatile int players;
        readonly Dictionary<string, Cl> lobby = new(); readonly Dictionary<string, Party> parties = new(); readonly Dictionary<string, string> partyOf = new();
        volatile string snapshot = "[]";
        static readonly System.Random rnd = new();
        readonly List<Timer> timers = new();                                  // referência viva: Timer sem referência pode ser coletado antes de disparar
        // r68b: depois do PARAR a fila fecha; timer, cliente lendo ou tick que chegue atrasado só é ignorado (antes estourava exceção na thread)
        void Post(Action a) { if (q.IsAddingCompleted) return; try { q.Add(a); } catch (InvalidOperationException) { } }
        void After(int ms, Action a) { if (!Running) return; Timer t = null; t = new Timer(_ => { Post(a); lock (timers) timers.Remove(t); t?.Dispose(); }, null, Timeout.Infinite, Timeout.Infinite); lock (timers) timers.Add(t); t.Change(ms, Timeout.Infinite); }

        sealed class Cl
        {
            public int id; public TcpClient tcp; public NetworkStream ns; public string name = "Jogador", skin = "", code = "", pid; public int team, prefTeam, k, d;
            public bool unity, lobby; public volatile bool dead; public Room room; public Cart cart;
            // r68b: envio por FILA com thread própria. Antes o laço da sala escrevia direto no socket: um PC lento ou com a rede travada
            // segurava a sala inteira até 4 s POR MENSAGEM. Agora só a thread de envio dele espera: escrita parada 4 s (WriteTimeout) ou
            // 32 MB para trás = desconectado (o jogo dele reconecta sozinho). O limite é em bytes, não em mensagens: uma rajada grande para
            // quem está bem (teste: 20 mil estados em 1 s) não derruba ninguém.
            readonly BlockingCollection<byte[]> outq = new(new ConcurrentQueue<byte[]>()); long queued;
            public Action<string> log;   // r68f: o motivo de cada desconexão forçada vai para o log do Host (diagnóstico de LAN de verdade)
            public void StartWriter() => new Thread(() =>
            {
                try { foreach (var f in outq.GetConsumingEnumerable()) { ns.Write(f, 0, f.Length); Interlocked.Add(ref queued, -f.Length); } }
                catch (Exception e)
                {
                    if (!dead) log?.Invoke($"[LAN] {name} (id {id}) desconectado: " + ((e.InnerException as SocketException)?.SocketErrorCode == SocketError.TimedOut
                        ? "4 s sem ler nada (PC travado ou rede parada)" : "envio falhou (" + (e.InnerException ?? e).Message + ")"));
                }
                Kill();
            }) { IsBackground = true, Name = "FBLan envio" }.Start();
            public void Send(string s) { if (!dead) SendRaw(Frame(Encoding.UTF8.GetBytes(s))); }
            public void SendRaw(byte[] f)
            {
                if (dead) return;
                if (Interlocked.Read(ref queued) > 32L << 20) { log?.Invoke($"[LAN] {name} (id {id}) desconectado: mais de 32 MB esperando envio"); Kill(); return; }
                try { Interlocked.Add(ref queued, f.Length); outq.Add(f); } catch (InvalidOperationException) { }
            }
            public void Kill() { dead = true; try { outq.CompleteAdding(); } catch { } try { tcp.Close(); } catch { } }
        }
        // r68b: CARTEIRA POR JOGADOR (pid), não por conexão. Cair e voltar (ou sair e entrar de novo na sala) não zera nem infla o saldo e não
        // esquece compra feita (o mesmo 'rid' depois da volta devolve o mesmo resultado). Vale enquanto o jogador está numa sala De Rolê e até
        // 90 s depois de cair; saindo pelo menu ('leave') ela é solta na hora. O saldo inicial vem do perfil do jogador (não existe conta online):
        // o Host garante que cada compra acontece uma vez só e que o resultado é o mesmo para todos.
        sealed class Cart
        {
            public int wallet = -1, n; public DateTime seen = DateTime.UtcNow; public readonly HashSet<string> owned = new();
            public readonly Dictionary<string, (string item, bool ok, string msg, int paid)> done = new(); readonly Queue<string> order = new();
            public void Remember(string rid, string item, bool ok, string msg, int paid)
            {
                if (!done.ContainsKey(rid)) { order.Enqueue(rid); while (order.Count > 200) done.Remove(order.Dequeue()); }
                done[rid] = (item, ok, msg, paid);
            }
        }
        const int CartKeepS = 90;
        readonly Dictionary<string, Cart> carts = new();
        Cart CartOf(Cl c)
        {
            if (c.pid == null) return new Cart();                                   // sem código de jogador: carteira só desta entrada
            if (!carts.TryGetValue(c.pid, out var k) || (k.n <= 0 && (DateTime.UtcNow - k.seen).TotalSeconds > CartKeepS)) carts[c.pid] = k = new Cart();
            k.seen = DateTime.UtcNow; return k;
        }
        sealed class Room { public string id, name, mode, map; public bool unity; public int max; public readonly Dictionary<int, Cl> clients = new(); public double t = MatchTime; public int[] teams = { 0, 0 }; public bool ended; public Dictionary<string, int> reserve; public HashSet<string> allow; public Dictionary<string, int> fixedTeam; }
        sealed class Party { public string leader; public List<string> members = new(); public HashSet<string> ready = new(); }

        // ------------------------------------------------------------------ partida e parada
        public bool Start(int port = DefaultPort)
        {
            for (int p = port; p < port + 5; p++)
            {
                try { tcp = new TcpListener(IPAddress.Any, p); tcp.Start(); Port = p; break; }
                catch (Exception e) { Error = e.Message; tcp = null; }
            }
            if (tcp == null) { Log("[LAN] nenhuma porta livre entre " + port + " e " + (port + 4) + ": " + Error); return false; }
            Running = true; Error = null;
            loopT = new Thread(Loop) { IsBackground = true, Name = "FBLan loop" }; loopT.Start();
            acceptT = new Thread(Accept) { IsBackground = true, Name = "FBLan accept" }; acceptT.Start();
            tickT = new Thread(() => { while (Running) { Thread.Sleep(1000); if (Running) Post(Tick); } }) { IsBackground = true, Name = "FBLan tick" }; tickT.Start();
            try
            {
                udp = new UdpClient(); udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort)); udpT = new Thread(Discovery) { IsBackground = true, Name = "FBLan udp" }; udpT.Start();
            }
            catch (Exception e) { Log("[LAN] descoberta UDP " + DiscoveryPort + " indisponível (outro Host neste PC?): " + e.Message); udp = null; }
            Log($"[LAN] Host no ar: porta {Port} · IPs {string.Join(", ", LocalIPv4().Select(i => i.ip))}");
            return true;
        }
        public void Dispose()
        {
            if (q.IsAddingCompleted) return;
            Running = false;
            try { tcp?.Stop(); } catch { }
            try { udp?.Close(); } catch { }
            lock (timers) { foreach (var t in timers) try { t.Dispose(); } catch { } timers.Clear(); }
            Post(() => { foreach (var r in rooms.Values) foreach (var c in r.clients.Values) c.Kill(); foreach (var c in lobby.Values) c.Kill(); });
            try { q.CompleteAdding(); } catch { }
        }

        void Loop()
        {
            foreach (var a in q.GetConsumingEnumerable()) { try { a(); } catch (Exception e) { Log("[LAN] erro ignorado: " + e.Message); } }
        }
        void Accept()
        {
            while (Running)
            {
                TcpClient c; try { c = tcp.AcceptTcpClient(); } catch { break; }
                var t = new Thread(() => Serve(c)) { IsBackground = true, Name = "FBLan cliente" }; t.Start();
            }
        }

        // ------------------------------------------------------------------ WebSocket mínimo (RFC 6455), igual ao server.mjs
        void Serve(TcpClient tc)
        {
            Cl c = null;
            try
            {
                tc.NoDelay = true; var ns = tc.GetStream(); ns.WriteTimeout = 4000;
                var head = ReadHead(ns); if (head == null) { tc.Close(); return; }
                var lines = head.Split(new[] { "\r\n" }, StringSplitOptions.None);
                string path = lines[0].Split(' ').Skip(1).FirstOrDefault() ?? "", key = null, up = null;
                foreach (var l in lines.Skip(1)) { int i = l.IndexOf(':'); if (i < 0) continue; var k = l.Substring(0, i).Trim().ToLowerInvariant(); var v = l.Substring(i + 1).Trim(); if (k == "sec-websocket-key") key = v; else if (k == "upgrade") up = v.ToLowerInvariant(); }
                if (!path.StartsWith("/ws") || key == null || up != "websocket")
                {
                    var body = Encoding.UTF8.GetBytes("BURITIRO LAN: use ws://<ip>:" + Port + "/ws");
                    var r = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                    ns.Write(r, 0, r.Length); ns.Write(body, 0, body.Length); tc.Close(); return;
                }
                string acc; using (var sha = SHA1.Create()) acc = Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                var resp = Encoding.ASCII.GetBytes($"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {acc}\r\n\r\n");
                ns.Write(resp, 0, resp.Length);
                c = new Cl { tcp = tc, ns = ns, log = Log }; c.StartWriter();
                var cc = c; Post(() => cc.id = nextId++);
                var frag = new List<byte>();
                while (Running)
                {
                    int b0 = ns.ReadByte(), b1 = ns.ReadByte(); if (b0 < 0 || b1 < 0) break;
                    bool fin = (b0 & 0x80) != 0, masked = (b1 & 0x80) != 0; int op = b0 & 15; long len = b1 & 127;
                    if (len == 126) { var e = ReadN(ns, 2); if (e == null) break; len = (e[0] << 8) | e[1]; }
                    else if (len == 127) { var e = ReadN(ns, 8); if (e == null) break; len = 0; for (int i = 0; i < 8; i++) len = (len << 8) | e[i]; }
                    if (len > 1 << 20) break;
                    var mk = masked ? ReadN(ns, 4) : null; var p = ReadN(ns, (int)len); if (p == null || (masked && mk == null)) break;
                    if (masked) for (int i = 0; i < p.Length; i++) p[i] ^= mk[i & 3];
                    if (op == 8) break;
                    if (op == 9) { if (p.Length < 126) c.SendRaw(new byte[] { 0x8a, (byte)p.Length }.Concat(p).ToArray()); continue; }
                    if (op == 1 || op == 0)
                    {
                        frag.AddRange(p); if (!fin) continue;
                        var s = Encoding.UTF8.GetString(frag.ToArray()); frag.Clear();
                        // r68e: o JSON é lido AQUI, na thread deste cliente. Antes era lido na fila única do servidor: no teste com o Host dentro do
                        // jogo, um PC mandando 20 mil estados deixava o ping dos outros 4 s na fila atrás da leitura dele. Json.Parse não guarda
                        // estado (só variáveis locais), então pode rodar em várias threads; a ordem das mensagens de cada cliente continua a mesma.
                        Dictionary<string, object> m; try { m = Json.Parse(s) as Dictionary<string, object>; } catch { m = null; }
                        if (m != null) Post(() => { if (!cc.dead) OnMsg(cc, m, s); });
                    }
                }
            }
            catch { }
            if (c != null) { var cc = c; cc.Kill(); Post(() => Bye(cc)); }                       // Kill: a thread de envio termina junto
            try { tc.Close(); } catch { }
        }
        static string ReadHead(NetworkStream ns)
        {
            var sb = new StringBuilder(); int n = 0;
            while (n < 16384) { int b = ns.ReadByte(); if (b < 0) return null; sb.Append((char)b); n++; if (n >= 4 && sb[n - 1] == '\n' && sb[n - 2] == '\r' && sb[n - 3] == '\n' && sb[n - 4] == '\r') return sb.ToString(); }
            return null;
        }
        static byte[] ReadN(NetworkStream ns, int n)
        {
            var b = new byte[n]; int o = 0;
            while (o < n) { int r = ns.Read(b, o, n - o); if (r <= 0) return null; o += r; }
            return b;
        }
        static byte[] Frame(byte[] p)
        {
            int n = p.Length; byte[] h = n < 126 ? new byte[] { 0x81, (byte)n } : n < 65536 ? new byte[] { 0x81, 126, (byte)(n >> 8), (byte)n } : new byte[] { 0x81, 127, 0, 0, 0, 0, (byte)(n >> 24), (byte)(n >> 16), (byte)(n >> 8), (byte)n };
            var f = new byte[h.Length + n]; Buffer.BlockCopy(h, 0, f, 0, h.Length); Buffer.BlockCopy(p, 0, f, h.Length, n); return f;
        }

        // ------------------------------------------------------------------ descoberta (UDP)
        void Discovery()
        {
            var any = new IPEndPoint(IPAddress.Any, 0);
            while (Running)
            {
                byte[] d; try { d = udp.Receive(ref any); } catch { if (!Running) break; continue; }
                if (Encoding.UTF8.GetString(d).Trim() != DiscoveryAsk) continue;
                var a = Encoding.UTF8.GetBytes("{\"t\":\"buritiro\",\"v\":1,\"name\":" + J(HostName) + ",\"port\":" + Port + ",\"players\":" + players + ",\"rooms\":" + snapshot + "}");
                try { udp.Send(a, a.Length, any); } catch { }
            }
        }

        // ------------------------------------------------------------------ salas (porte 1:1 do server.mjs, + role e compra)
        static object Pub(Cl c) => new Dictionary<string, object> { ["id"] = c.id, ["name"] = c.name, ["code"] = c.code ?? "", ["pid"] = c.pid ?? "", ["skin"] = c.skin, ["team"] = c.team, ["k"] = c.k, ["d"] = c.d };
        static Dictionary<string, object> Info(Room r) => new() { ["id"] = r.id, ["name"] = r.name, ["mode"] = r.mode, ["map"] = r.map, ["players"] = r.clients.Count, ["max"] = r.max, ["t"] = (int)Math.Max(0, Math.Round(r.t)) };
        static void Cast(Room r, string s, Cl except = null) { foreach (var c in r.clients.Values.ToList()) if (c != except && !c.dead) c.Send(s); }
        static void Cast(Room r, object o, Cl except = null) => Cast(r, J(o), except);
        static Dictionary<string, object> Scores(Room r) => new() { ["t"] = "score", ["tl"] = (int)Math.Max(0, Math.Round(r.t)), ["teams"] = new List<object> { r.teams[0], r.teams[1] }, ["players"] = r.clients.Values.Select(c => (object)new Dictionary<string, object> { ["id"] = c.id, ["k"] = c.k, ["d"] = c.d }).ToList() };
        string Code() { string s; do s = new string(Enumerable.Range(0, 4).Select(_ => "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"[rnd.Next(32)]).ToArray()); while (rooms.ContainsKey(s)); return s; }
        void Snap()
        {
            snapshot = J(rooms.Values.Where(r => r.unity).Select(r => (object)Info(r)).ToList());
            // jogadores distintos: o mesmo pid no menu (lobby) e na partida é UMA pessoa
            var pids = new HashSet<string>(lobby.Keys); int anon = 0;
            foreach (var r in rooms.Values) foreach (var x in r.clients.Values) { if (x.pid != null) pids.Add(x.pid); else anon++; }
            players = pids.Count + anon;
        }

        void Join(Cl c, Room r)
        {
            if (c.room != null) Leave(c);
            int n = r.clients.Values.Count(x => x.team == 0), m = r.clients.Count - n;
            c.team = TeamMode(r) ? (n <= m ? 0 : 1) : IsRole(r) ? 0 : c.prefTeam;
            if (r.fixedTeam != null && c.pid != null && r.fixedTeam.TryGetValue(c.pid, out var ft)) c.team = ft;   // 2 × 2: o time sorteado vale também para quem cai e volta
            if (r.reserve != null && c.pid != null && r.reserve.TryGetValue(c.pid, out var rt)) { c.team = rt; r.reserve.Remove(c.pid); }
            c.k = 0; c.d = 0; c.room = r; r.clients[c.id] = c;
            if (IsRole(r)) { c.cart = CartOf(c); c.cart.n++; }                                     // carteira da loja: a do jogador (pid)
            c.Send(J(new Dictionary<string, object> { ["t"] = "joined", ["room"] = Info(r), ["you"] = Pub(c), ["players"] = r.clients.Values.Where(x => x != c).Select(Pub).ToList() }));
            Cast(r, new Dictionary<string, object> { ["t"] = "peer+", ["p"] = Pub(c) }, c); Cast(r, Scores(r)); Snap();
        }
        // asked = saiu pelo menu ('leave'): a carteira é solta na hora; caiu = ela espera CartKeepS para a volta
        void Leave(Cl c, bool asked = false)
        {
            var r = c.room; if (r == null) return; r.clients.Remove(c.id); c.room = null;
            if (c.cart != null) { var k = c.cart; k.n = Math.Max(0, k.n - 1); k.seen = asked && k.n == 0 ? DateTime.MinValue : DateTime.UtcNow; c.cart = null; }
            Cast(r, new Dictionary<string, object> { ["t"] = "peer-", ["id"] = c.id });
            if (r.clients.Count == 0) rooms.Remove(r.id); Snap();
        }
        void Bye(Cl c) { if (c.dead && c.room == null && !c.lobby) return; c.dead = true; Leave(c); LobbyBye(c); Snap(); }

        // lobby r62 (presença, amigos, squad) — mesmo comportamento do server.mjs
        static bool PidOk(string s) => s != null && s.Length >= 4 && s.Length <= 10 && s.All(ch => (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9'));
        string LobbySt(string pid) { foreach (var r in rooms.Values) foreach (var x in r.clients.Values) if (x.pid == pid) return "partida"; return lobby.ContainsKey(pid) ? "menu" : "offline"; }
        Dictionary<string, object> Person(string pid) { lobby.TryGetValue(pid, out var l); return new() { ["pid"] = pid, ["name"] = l?.name ?? "", ["skin"] = l?.skin ?? "", ["st"] = LobbySt(pid) }; }
        void PartyCast(Party p)
        {
            var msg = J(new Dictionary<string, object> { ["t"] = "party", ["leader"] = p.leader, ["members"] = p.members.Select(pid => { var d = Person(pid); d["ready"] = p.ready.Contains(pid); return (object)d; }).ToList() });
            foreach (var pid in p.members) if (lobby.TryGetValue(pid, out var l)) l.Send(msg);
        }
        static readonly string NoParty = "{\"t\":\"party\",\"leader\":null,\"members\":[]}";
        void PartyLeave(string pid)
        {
            if (!partyOf.TryGetValue(pid, out var lid)) return; partyOf.Remove(pid); if (!parties.TryGetValue(lid, out var p)) return;
            p.members.Remove(pid); p.ready.Remove(pid); if (lobby.TryGetValue(pid, out var me)) me.Send(NoParty);
            if (p.members.Count <= 1) { foreach (var x in p.members) { partyOf.Remove(x); if (lobby.TryGetValue(x, out var l)) l.Send(NoParty); } parties.Remove(lid); return; }
            if (p.leader == pid) { parties.Remove(lid); p.leader = p.members[0]; parties[p.leader] = p; foreach (var x in p.members) partyOf[x] = p.leader; }
            PartyCast(p);
        }
        void LobbyBye(Cl c) { if (!c.lobby || c.pid == null || !lobby.TryGetValue(c.pid, out var l) || l != c) return; lobby.Remove(c.pid); PartyLeave(c.pid); }
        Party PartyOfPid(string pid) => partyOf.TryGetValue(pid, out var lid) && parties.TryGetValue(lid, out var p) ? p : null;
        Room PickRoom(string map, int size, string mode, List<string> priv = null)
        {
            if (priv == null) foreach (var r in rooms.Values) if (r.unity && r.allow == null && r.map == map && r.mode == mode && !r.ended && r.clients.Count + (r.reserve?.Count ?? 0) + size <= r.max) return r;
            var nr = new Room { id = Code(), name = priv != null ? "2 × 2" : "Squad", mode = mode, map = map, unity = true, max = priv != null ? priv.Count : (mode == "role" || mode == "rolex") ? 16 : 10 };
            if (priv != null) nr.allow = new HashSet<string>(priv);
            rooms[nr.id] = nr; var id = nr.id; After(90000, () => { if (rooms.TryGetValue(id, out var x) && x == nr && x.clients.Count == 0) { rooms.Remove(id); Snap(); } });
            Snap(); return nr;
        }
        bool OnLobby(Cl c, Dictionary<string, object> m)
        {
            string me = c.pid, t = Json.S(m, "t");
            switch (t)
            {
                case "who":
                    {
                        var list = new List<object>(); var seen = new HashSet<string> { me };
                        foreach (var x in Json.L(m, "pids") ?? new List<object>()) { var p = (x?.ToString() ?? "").ToUpperInvariant(); if (PidOk(p) && seen.Add(p)) list.Add(Person(p)); }
                        foreach (var pid in lobby.Keys.ToList()) if (list.Count < 60 && seen.Add(pid)) list.Add(Person(pid));
                        c.Send(J(new Dictionary<string, object> { ["t"] = "who", ["list"] = list })); return true;
                    }
                case "pinv":
                    {
                        var to = (Json.S(m, "to") ?? "").ToUpperInvariant(); if (!lobby.TryGetValue(to, out var tl)) { c.Send(Err("perr", "Esse jogador não está no menu agora")); return true; }
                        var lid = partyOf.TryGetValue(me, out var l0) ? l0 : me; parties.TryGetValue(lid, out var p); if (p != null && p.members.Count >= 4) { c.Send(Err("perr", "Squad cheio (4)")); return true; }
                        if (partyOf.TryGetValue(to, out var a) && partyOf.TryGetValue(me, out var b) && a == b) return true;
                        tl.Send(J(new Dictionary<string, object> { ["t"] = "pinv", ["from"] = me, ["leader"] = lid, ["name"] = c.name, ["skin"] = c.skin })); c.Send(Err("pok", "Convite enviado para " + tl.name)); return true;
                    }
                case "pacc":
                    {
                        var lid = (Json.S(m, "leader") ?? "").ToUpperInvariant(); if (!lobby.ContainsKey(lid) || lid == me) { c.Send(Err("perr", "O convite expirou")); return true; }
                        var p = PartyOfPid(lid); if (p == null) { p = new Party { leader = lid }; p.members.Add(lid); parties[lid] = p; partyOf[lid] = lid; }
                        if (p.members.Contains(me)) { PartyCast(p); return true; }
                        if (p.members.Count >= 4) { c.Send(Err("perr", "Squad cheio (4)")); return true; }
                        PartyLeave(me); p.members.Add(me); partyOf[me] = p.leader; PartyCast(p); return true;
                    }
                case "pdec": { if (lobby.TryGetValue((Json.S(m, "leader") ?? "").ToUpperInvariant(), out var l)) l.Send(Err("pok", c.name + " recusou o convite")); return true; }
                case "pleave": PartyLeave(me); return true;
                case "pkick":
                    {
                        var p = PartyOfPid(me); var who = (Json.S(m, "pid") ?? "").ToUpperInvariant();
                        if (p != null && p.leader == me && who != me && p.members.Contains(who)) { PartyLeave(who); if (lobby.TryGetValue(who, out var l)) l.Send(Err("perr", "Você saiu do squad (removido pelo líder)")); }
                        return true;
                    }
                case "pready": { var p = PartyOfPid(me); if (p != null) { if (Json.B(m, "on")) p.ready.Add(me); else p.ready.Remove(me); PartyCast(p); } return true; }
                case "pgo":
                    {
                        var p = PartyOfPid(me); var members = p != null ? p.members.ToList() : new List<string> { me };
                        if (p != null && p.leader != me) { c.Send(Err("perr", "Só o líder inicia a partida")); return true; }
                        var map = Maps.Contains(Json.S(m, "map")) ? Json.S(m, "map") : "vila_jardim";
                        // r69: nm = modo online escolhido no SQUAD (role, rolex, x2, tdm); cliente antigo manda só 'mode'
                        var pedido = Json.S(m, "nm") ?? (Json.S(m, "mode") == "role" ? "role" : "tdm");
                        var mode = pedido == "role" || pedido == "rolex" || pedido == "x2" ? pedido : "tdm";
                        if (mode == "x2" && members.Count != 4) { c.Send(Err("perr", "2 × 2 precisa de 4 jogadores no squad (agora: " + members.Count + ")")); return true; }
                        var teamOf = new Dictionary<string, int>(); Room r;
                        if (mode == "x2")
                        {
                            // sorteio 2 × 2: embaralha os 4 (Fisher-Yates) e os dois primeiros ficam no time 0
                            var sh = members.ToList(); for (int i = sh.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (sh[i], sh[j]) = (sh[j], sh[i]); }
                            for (int i = 0; i < sh.Count; i++) teamOf[sh[i]] = i < 2 ? 0 : 1;
                            r = PickRoom(map, 4, "x2", members); r.fixedTeam = teamOf;
                        }
                        else
                        {
                            r = PickRoom(map, members.Count, mode);
                            int n0 = r.clients.Values.Count(x => x.team == 0) + (r.reserve?.Values.Count(x => x == 0) ?? 0), n1 = r.clients.Count + (r.reserve?.Count ?? 0) - n0, team = IsRole(r) ? 0 : n0 <= n1 ? 0 : 1;
                            foreach (var pid in members) teamOf[pid] = team;
                        }
                        r.reserve ??= new Dictionary<string, int>(); foreach (var pid in members) r.reserve[pid] = teamOf[pid];
                        var rr = r; After(60000, () => { if (rr.reserve != null) foreach (var pid in members) rr.reserve.Remove(pid); });
                        p?.ready.Clear();
                        List<object> lados = null;
                        if (mode == "x2") lados = new List<object> { 0, 1 }.Select(t => (object)members.Where(pid => teamOf[pid] == (int)t).Select(pid => (object)(lobby.TryGetValue(pid, out var lp) ? lp.name : pid)).ToList()).ToList();
                        foreach (var pid in members) if (lobby.TryGetValue(pid, out var l)) l.Send(J(new Dictionary<string, object> { ["t"] = "pgo", ["room"] = r.id, ["map"] = map, ["mode"] = Json.S(m, "mode") ?? "", ["nm"] = mode, ["team"] = teamOf[pid], ["lados"] = lados }));
                        Log($"[LAN] squad de {me}: {mode} em {map} (sala {r.id}, {members.Count} jogador(es))");
                        if (p != null) PartyCast(p); return true;
                    }
                case "pvc":                                                                              // r69: voz do squad no menu: só para os outros do squad
                    {
                        var d = Json.S(m, "d"); if (string.IsNullOrEmpty(d) || d.Length > VozMax) return true;
                        var p = PartyOfPid(me); if (p == null) return true;
                        var o = J(new Dictionary<string, object> { ["t"] = "pvc", ["from"] = me, ["name"] = c.name, ["q"] = Json.I(m, "q"), ["d"] = d });
                        foreach (var pid in p.members) if (pid != me && lobby.TryGetValue(pid, out var l) && !l.dead) l.Send(o);
                        return true;
                    }
            }
            return false;
        }
        static string Err(string t, string msg) => J(new Dictionary<string, object> { ["t"] = t, ["msg"] = msg });

        void OnMsg(Cl c, Dictionary<string, object> m, string raw)
        {
            if (c.lobby && OnLobby(c, m)) return;
            switch (Json.S(m, "t"))
            {
                case "hello":
                    c.name = Clean(Json.S(m, "name") ?? "Jogador", 16); c.code = (Json.S(m, "code") ?? "").ToUpperInvariant(); if (c.code.Length > 8) c.code = c.code.Substring(0, 8);
                    c.skin = Json.S(m, "skin") ?? ""; if (c.skin.Length > 160) c.skin = c.skin.Substring(0, 160);
                    c.prefTeam = Json.I(m, "team") == 1 ? 1 : 0; c.unity = Json.B(m, "unity");
                    { var pid = (Json.S(m, "pid") ?? "").ToUpperInvariant(); if (PidOk(pid)) c.pid = pid; }
                    if (Json.B(m, "lobby") && c.pid != null)
                    {
                        lobby.TryGetValue(c.pid, out var old); c.lobby = true; lobby[c.pid] = c;
                        if (old != null && old != c) { old.lobby = false; old.Kill(); }
                        var p = PartyOfPid(c.pid); if (p != null) PartyCast(p); Snap();
                    }
                    c.Send(J(new Dictionary<string, object> { ["t"] = "hi", ["id"] = c.id, ["code"] = c.code, ["pid"] = c.pid ?? "", ["lan"] = true, ["host"] = HostName, ["caps"] = Caps }));
                    break;
                case "list": c.Send(J(new Dictionary<string, object> { ["t"] = "rooms", ["rooms"] = rooms.Values.Where(r => (r.unity || !c.unity) && PodeEntrar(c, r)).Select(r => (object)Info(r)).ToList() })); break;
                case "create":
                    {
                        var mode = Limit.ContainsKey(Json.S(m, "mode") ?? "") && Json.S(m, "mode") != "x2" ? Json.S(m, "mode") : "tdm"; var map = Maps.Contains(Json.S(m, "map")) ? Json.S(m, "map") : "vila_jardim";   // x2 só nasce pelo squad
                        var r = new Room { id = Code(), name = Clean(Json.S(m, "name") ?? ("Sala de " + c.name), 24), mode = mode, map = map, unity = c.unity, max = Math.Min(16, Math.Max(2, Json.I(m, "max", 10))) };
                        rooms[r.id] = r; Join(c, r); break;
                    }
                case "join":
                    {
                        var id = (Json.S(m, "id") ?? "").ToUpperInvariant();
                        if (!rooms.TryGetValue(id, out var r)) c.Send(Err("err", "Sala não encontrada"));
                        else if (!PodeEntrar(c, r)) c.Send(Err("err", "Sala privada do squad"));
                        else if (r.clients.Count >= r.max) c.Send(Err("err", "Sala cheia"));
                        else Join(c, r);
                        break;
                    }
                case "leave": Leave(c, true); break;
                case "st": if (c.room != null) Cast(c.room, "{\"id\":" + c.id + "," + raw.TrimStart().Substring(1), c); break;    // só repassa (como o Node), sem refazer o JSON
                case "ev":
                    {
                        var r = c.room; if (r == null) break; m["id"] = c.id; var e = Json.S(m, "e");
                        if (e == "hit" || e == "somreq" || e == "somchk") { if (r.clients.TryGetValue(Json.I(m, "to"), out var tg)) tg.Send(J(m)); break; }
                        if (e == "mark") { if (r.mode == "ffa" || r.mode == "rolex") break; m["tm"] = c.team; var s = J(m); foreach (var o in r.clients.Values) if (o != c && o.team == c.team) o.Send(s); break; }
                        if (e == "vc")                                                                  // r69: voz: times = só o próprio time; cidade livre = a sala toda
                        {
                            var d = Json.S(m, "d"); if (string.IsNullOrEmpty(d) || d.Length > VozMax) break;
                            var s = J(m); foreach (var o in r.clients.Values) if (o != c && !o.dead && (!TeamMode(r) || o.team == c.team)) o.Send(s); break;
                        }
                        if (e == "die")
                        {
                            c.d++; r.clients.TryGetValue(Json.I(m, "by"), out var k);
                            if (k != null && k != c && !r.ended) { k.k++; if (TeamMode(r) && k.team != c.team) r.teams[k.team]++; }
                            Cast(r, m); Cast(r, Scores(r)); CheckEnd(r); break;
                        }
                        Cast(r, m, c); break;
                    }
                case "chat": if (c.room != null) Cast(c.room, new Dictionary<string, object> { ["t"] = "chat", ["id"] = c.id, ["msg"] = Clean(Json.S(m, "msg") ?? "", 80) }); break;
                case "ping": c.Send("{\"t\":\"pong\",\"ts\":" + J(Json.Get(m, "ts")) + "}"); break;
                case "wallet": Wallet(c, m); break;
                case "wdec":                                                                              // multa (civil atingido): só desce
                    { var k = c.cart; if (IsRole(c.room) && k != null && k.wallet >= 0) { k.wallet = Math.Max(0, k.wallet - (int)Math.Max(0, Math.Round(Json.F(m, "v")))); k.seen = DateTime.UtcNow; } break; }
                case "buy": Buy(c, m, false); break;
                case "bchk": Buy(c, m, true); break;                                                       // r68b: "esta compra chegou?" depois de cair: só consulta, nunca cobra
            }
        }
        static string Clean(string s, int n) { s = new string((s ?? "").Where(ch => ch != '<' && ch != '>' && ch != '&' && ch != '"').ToArray()); return s.Length > n ? s.Substring(0, n) : s; }

        // ------------------------------------------------------------------ carteira e compra (autoridade do Host)
        // a carteira é declarada UMA vez enquanto vale (ver Cart): declarar de novo só devolve a que o Host já tem
        void Wallet(Cl c, Dictionary<string, object> m)
        {
            var k = c.cart;
            if (!IsRole(c.room) || k == null) { c.Send(Err("werr", "Carteira só existe no De Rolê")); return; }
            if (k.wallet < 0)
            {
                k.wallet = (int)Math.Max(0, Math.Min(9999999, Json.F(m, "v")));
                foreach (var o in Json.L(m, "owned") ?? new List<object>()) { var id = o as string; if (id != null && !Consumable.Contains(id) && PriceOf(id) >= 0) k.owned.Add(id); }
            }
            k.seen = DateTime.UtcNow;
            c.Send(J(new Dictionary<string, object> { ["t"] = "wallet", ["v"] = k.wallet, ["owned"] = k.owned.Select(x => (object)x).ToList() }));
        }
        public const string NotArrived = "O pedido não chegou ao Host: nada foi cobrado";
        // check = 'bchk' (consulta depois de cair): devolve o resultado guardado; se o pedido nunca chegou, anota isso (o pedido velho que
        // ainda chegue depois não cobra) e responde que nada foi cobrado
        void Buy(Cl c, Dictionary<string, object> m, bool check)
        {
            var rid = Json.S(m, "rid") ?? ""; var item = Json.S(m, "item") ?? ""; var k = c.cart;
            if (rid.Length == 0 || rid.Length > 64) { c.Send(BuyR(rid, item, false, "pedido sem id", k, check)); return; }
            if (!IsRole(c.room) || k == null) { c.Send(BuyR(rid, item, false, "Loja só no De Rolê", null, check)); return; }
            k.seen = DateTime.UtcNow;
            if (k.done.TryGetValue(rid, out var d) && (check || d.item == item)) { c.Send(BuyR(rid, d.item, d.ok, d.msg, k, check, d.paid)); return; }   // reenvio / clique duplo / consulta: MESMO resultado, saldo de agora
            if (check) { k.Remember(rid, item, false, NotArrived, 0); c.Send(BuyR(rid, item, false, NotArrived, k, true)); return; }
            if (k.wallet < 0) { c.Send(BuyR(rid, item, false, "Carteira ainda não sincronizada", k, false)); return; }   // não fica guardado: depois da carteira o mesmo pedido vale
            int price = PriceOf(item); bool ok = false; string msg; int paid = 0;
            if (price < 0) msg = "Item fora do catálogo do Host";
            else if (!Consumable.Contains(item) && k.owned.Contains(item)) { ok = true; msg = "já era seu"; }
            else if (k.wallet < price) msg = "Saldo insuficiente";
            else { k.wallet -= price; if (!Consumable.Contains(item)) k.owned.Add(item); ok = true; msg = ""; paid = price; }
            k.Remember(rid, item, ok, msg, paid); c.Send(BuyR(rid, item, ok, msg, k, false, paid));
        }
        // a resposta leva o saldo e as armas de AGORA e nada disso enquanto a carteira não existe (o jogo lia o -1 e zerava o saldo)
        static string BuyR(string rid, string item, bool ok, string msg, Cart k, bool check, int paid = 0)
        {
            var o = new Dictionary<string, object> { ["t"] = "buy", ["rid"] = rid, ["item"] = item, ["ok"] = ok, ["msg"] = msg, ["paid"] = paid };
            if (check) o["chk"] = true;
            if (k != null && k.wallet >= 0) { o["wallet"] = k.wallet; o["owned"] = k.owned.Select(x => (object)x).ToList(); }
            return J(o);
        }

        void CheckEnd(Room r)
        {
            if (r.ended || IsRole(r)) return;
            int L = Limit[r.mode]; var ps = r.clients.Values.ToList(); var top = ps.OrderByDescending(x => x.k).FirstOrDefault();
            bool hit = TeamMode(r) ? (r.teams[0] >= L || r.teams[1] >= L) : (top != null && top.k >= L);
            if (!hit && r.t > 0) return;
            r.ended = true;
            int win = TeamMode(r) ? (r.teams[0] == r.teams[1] ? -1 : r.teams[0] > r.teams[1] ? 0 : 1) : (top?.id ?? -1);
            Cast(r, new Dictionary<string, object> { ["t"] = "end", ["win"] = win, ["teams"] = new List<object> { r.teams[0], r.teams[1] }, ["players"] = ps.Select(Pub).ToList() });
            var rr = r; After(12000, () => { if (!rooms.ContainsKey(rr.id)) return; rr.t = MatchTime; rr.teams = new[] { 0, 0 }; rr.ended = false; foreach (var x in rr.clients.Values) { x.k = 0; x.d = 0; } Cast(rr, "{\"t\":\"restart\"}"); Cast(rr, Scores(rr)); });
        }
        void Tick()
        {
            foreach (var r in rooms.Values.ToList())
            {
                if (IsRole(r)) continue;                                          // De Rolê e Rolê armado: sem relógio
                if (!r.ended) { r.t -= 1; if (r.t <= 0) CheckEnd(r); }
                if ((int)Math.Round(r.t) % 5 == 0) Cast(r, Scores(r));
            }
            if (++tickN % 30 == 0) foreach (var kv in carts.Where(kv => kv.Value.n <= 0 && (DateTime.UtcNow - kv.Value.seen).TotalSeconds > CartKeepS).ToList()) carts.Remove(kv.Key);   // carteiras soltas
            Snap();
        }

        // ------------------------------------------------------------------ JSON de saída
        public static string J(object o)
        {
            switch (o)
            {
                case null: return "null";
                case string s:
                    {
                        var sb = new StringBuilder("\"");
                        foreach (var ch in s) { if (ch == '"') sb.Append("\\\""); else if (ch == '\\') sb.Append("\\\\"); else if (ch < ' ') sb.Append("\\u").Append(((int)ch).ToString("x4")); else sb.Append(ch); }
                        return sb.Append('"').ToString();
                    }
                case bool b: return b ? "true" : "false";
                case int i: return i.ToString(CultureInfo.InvariantCulture);
                case long l: return l.ToString(CultureInfo.InvariantCulture);
                case float f: return f.ToString("R", CultureInfo.InvariantCulture);
                case double d: return d.ToString("R", CultureInfo.InvariantCulture);
                case Dictionary<string, object> dd: { var sb = new StringBuilder("{"); bool first = true; foreach (var kv in dd) { if (!first) sb.Append(','); first = false; sb.Append(J(kv.Key)).Append(':').Append(J(kv.Value)); } return sb.Append('}').ToString(); }
                case System.Collections.IEnumerable e: { var sb = new StringBuilder("["); bool first = true; foreach (var x in e) { if (!first) sb.Append(','); first = false; sb.Append(J(x)); } return sb.Append(']').ToString(); }
                default: return J(o.ToString());
            }
        }

        // ------------------------------------------------------------------ endereços desta máquina (para mostrar ao Host)
        public static List<(string ip, string name, int score)> LocalIPv4()
        {
            var o = new List<(string, string, int)>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    bool virt = System.Text.RegularExpressions.Regex.IsMatch(ni.Name + " " + ni.Description, "vEthernet|VirtualBox|VMware|Hyper-V|WSL|Docker|Loopback|Tailscale|ZeroTier|Hamachi|Radmin", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    bool wifi = ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue; var ip = ua.Address.ToString(); if (ip.StartsWith("169.254.") || ip.StartsWith("127.")) continue;
                        int sc = (virt ? -100 : 0) + (wifi ? 20 : 0) + (ip.StartsWith("192.168.") ? 10 : ip.StartsWith("10.") ? 6 : ip.StartsWith("172.") ? 4 : 0);
                        o.Add((ip, ni.Name, sc));
                    }
                }
            }
            catch { }
            return o.OrderByDescending(x => x.Item3).ToList();
        }
    }

    // Procura Hosts na rede local: manda "BURITIRO?" em broadcast (geral e de cada sub-rede) e junta as respostas por 'ms'.
    public static class FBLanDiscovery
    {
        public class Found { public string ip, name; public int port, players; public List<object> rooms; public string Url => $"ws://{ip}:{port}/ws"; }
        public static List<Found> Scan(int ms = 1200)
        {
            var res = new List<Found>(); var seen = new HashSet<string>();
            try
            {
                using var u = new UdpClient(); u.EnableBroadcast = true; u.Client.ReceiveTimeout = 200; u.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                var ask = Encoding.UTF8.GetBytes(FBLanServer.DiscoveryAsk);
                var targets = new List<IPAddress> { IPAddress.Broadcast, IPAddress.Loopback };
                try
                {
                    foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (ni.OperationalStatus != OperationalStatus.Up) continue;
                        foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ua.Address.AddressFamily != AddressFamily.InterNetwork || ua.IPv4Mask == null) continue;
                            var a = ua.Address.GetAddressBytes(); var mk = ua.IPv4Mask.GetAddressBytes(); var b = new byte[4]; for (int i = 0; i < 4; i++) b[i] = (byte)(a[i] | ~mk[i]);
                            targets.Add(new IPAddress(b));
                        }
                    }
                }
                catch { }
                foreach (var t in targets.Distinct()) { try { u.Send(ask, ask.Length, new IPEndPoint(t, FBLanServer.DiscoveryPort)); } catch { } }
                var until = DateTime.UtcNow.AddMilliseconds(ms);
                while (DateTime.UtcNow < until)
                {
                    try
                    {
                        var ep = new IPEndPoint(IPAddress.Any, 0); var d = u.Receive(ref ep);
                        var m = Json.Parse(Encoding.UTF8.GetString(d)) as Dictionary<string, object>; if (Json.S(m, "t") != "buritiro") continue;
                        var ip = ep.Address.ToString(); int port = Json.I(m, "port", FBLanServer.DefaultPort);
                        if (ip.StartsWith("127.")) { var best = FBLanServer.LocalIPv4().FirstOrDefault(); if (best.ip != null) ip = best.ip; }   // o próprio PC: mostra o IP de rede
                        if (!seen.Add(ip + ":" + port)) continue;
                        res.Add(new Found { ip = ip, port = port, name = Json.S(m, "name") ?? ip, players = Json.I(m, "players"), rooms = Json.L(m, "rooms") ?? new List<object>() });
                    }
                    catch (SocketException) { }
                    catch { }
                }
            }
            catch { }
            return res;
        }
        // "192.168.0.10", "192.168.0.10:8124" ou o endereço completo -> ws://.../ws (null se não parece endereço)
        public static string ToUrl(string s)
        {
            s = (s ?? "").Trim(); if (s.Length == 0) return null;
            if (s.StartsWith("ws://") || s.StartsWith("wss://")) return s;
            if (s.Contains("/")) return null;
            return s.Contains(":") ? $"ws://{s}/ws" : $"ws://{s}:{FBLanServer.DefaultPort}/ws";
        }
    }
}
#endif
