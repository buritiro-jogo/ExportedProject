// r68 REDE LOCAL pelo próprio jogo: este PC vira Host (o FBLanServer embutido, mesmo protocolo do server.mjs) ou procura um Host na rede.
//  - HOSPEDAR: liga o servidor nas portas 8123..8127 (TCP) + descoberta em UDP 8125 e aponta o lobby/partida deste PC para ele
//    (FBSession.lanServer = ws://127.0.0.1:porta/ws). Os outros PCs acham pelo PROCURAR NA REDE ou digitam o IP mostrado na tela.
//  - PROCURAR: pergunta "BURITIRO?" em broadcast numa thread (a tela não trava) e devolve a lista de Hosts que responderam.
//  - O preço de cada item da loja vem do catálogo DESTE jogo (FBGunShop.PriceTable), calculado aqui na thread principal: o servidor
//    roda em threads próprias e não pode tocar no Unity.
// Nada disso é salvo: ao abrir o jogo de novo ele volta ao servidor online normal (FBSession.server).
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public class FBLanHost : MonoBehaviour
    {
        public class Achado { public string ip, name, url; public int port, players, rooms; }

        static FBLanHost inst;
        public static string LastError;
        public static int ScanVersion;                    // muda quando uma procura termina (a tela se redesenha)
        public static bool Scanning { get; private set; }
        public static readonly List<Achado> Found = new();
        readonly ConcurrentQueue<System.Action> main = new();
#if !UNITY_WEBGL || UNITY_EDITOR
        FBLanServer srv;
        public static bool Hosting => inst && inst.srv != null && inst.srv.Running;
        public static int Port => Hosting ? inst.srv.Port : 0;
        public static int Clients => Hosting ? inst.srv.Clients : 0;
#else
        public static bool Hosting => false;
        public static int Port => 0;
        public static int Clients => 0;
#endif
        public static bool Supported => Application.platform != RuntimePlatform.WebGLPlayer;
        // conectado a um Host da rede (este PC ou outro) em vez do servidor online
        public static bool OnLan => !string.IsNullOrEmpty(FBSession.lanServer);

        static FBLanHost Ensure()
        {
            if (inst) return inst; var g = new GameObject("FB_RedeLocal"); DontDestroyOnLoad(g); inst = g.AddComponent<FBLanHost>(); return inst;
        }
        void Update() { while (main.TryDequeue(out var a)) { try { a(); } catch (System.Exception e) { Debug.LogWarning("[FreeBala][LAN] " + e.Message); } } }

        // endereços desta máquina para mostrar ao Host (melhor primeiro: Wi-Fi/cabo de casa antes de adaptador virtual)
        public static List<(string ip, string name)> LocalIPs()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return FBLanServer.LocalIPv4().Where(x => x.score > -50).Select(x => (x.ip, x.name)).ToList();
#else
            return new List<(string, string)>();
#endif
        }

        public static bool StartHost()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (Hosting) return true;
            var h = Ensure();
            var prices = FBGunShop.PriceTable();
            if (prices.Count <= FBGunShop.GEAR_IDS.Length) Debug.LogWarning("[FreeBala][LAN] catálogo da loja vazio ao hospedar (registro de armas não carregou?): só equipamento à venda");
            var s = new FBLanServer { HostName = string.IsNullOrWhiteSpace(FBSession.name) ? "BURITIRO" : FBSession.name.Trim() };
            s.PriceOf = id => id != null && prices.TryGetValue(id, out var v) ? v : -1;
            s.Log = m => h.main.Enqueue(() => Debug.Log("[FreeBala]" + m));
            if (!s.Start()) { LastError = s.Error ?? "nenhuma porta livre"; Debug.LogWarning("[FreeBala][LAN] não deu para hospedar: " + LastError); try { s.Dispose(); } catch { } return false; }
            h.srv = s; LastError = null;
            FBSession.lanServer = $"ws://127.0.0.1:{s.Port}/ws"; FBLobbyNet.Ensure().Reconnect();
            Debug.Log($"[FreeBala][LAN] HOSPEDANDO na porta {s.Port} · {prices.Count} preços da loja · IPs: {string.Join(", ", LocalIPs().Select(i => i.ip))}");
            Fundo(); return true;
#else
            LastError = "indisponível no navegador"; return false;
#endif
        }
        public static void StopHost()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (inst && inst.srv != null) { try { inst.srv.Dispose(); } catch { } inst.srv = null; Debug.Log("[FreeBala][LAN] Host parado"); }
#endif
            LeaveLan(); Fundo();
        }
        // r68e: em rede o jogo NÃO pausa quando a janela perde o foco. Antes, o Host que trocava de janela congelava o próprio jogador e tudo
        // o que o jogo dele comanda para a sala inteira, e o cliente minimizado ficava parado para os outros. Fora da rede (sem Host ligado e
        // sem partida online) volta o padrão do projeto: pausa ao perder o foco. Só desliga o que foi ligado aqui: quem ligou o
        // runInBackground por conta própria (autotestes FBLanTeste/FBLuzTeste) continua com ele ligado.
        static bool ligouAqui;
        public static void Fundo()
        {
            bool rede = Hosting || FBNet.I != null;
            if (rede && !Application.runInBackground) { Application.runInBackground = true; ligouAqui = true; }
            else if (!rede && ligouAqui) { Application.runInBackground = false; ligouAqui = false; }
        }
        // entrar num Host achado ou digitado (ip, ip:porta ou ws://...)
        public static bool Join(string addr)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            var url = FBLanDiscovery.ToUrl(addr); if (url == null) { LastError = "endereço inválido"; return false; }
            FBSession.lanServer = url; LastError = null; FBLobbyNet.Ensure().Reconnect(); Debug.Log("[FreeBala][LAN] entrando no Host " + url); return true;
#else
            LastError = "indisponível no navegador"; return false;
#endif
        }
        // r68b: o endereço parece válido? (o CONECTAR conferia só depois de parar o Host: endereço errado derrubava a sala de todo mundo)
        public static bool ValidAddr(string addr)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return FBLanDiscovery.ToUrl(addr) != null;
#else
            return false;
#endif
        }
        // o endereço é este próprio Host? (127.x ou um IP deste PC, na porta em que ele está hospedando)
        public static bool IsSelf(string addr)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (!Hosting) return false; var url = FBLanDiscovery.ToUrl(addr); if (url == null) return false;
            try { var u = new System.Uri(url); var h = u.Host; return u.Port == Port && (h.StartsWith("127.") || h == "localhost" || LocalIPs().Any(x => x.ip == h)); } catch { return false; }
#else
            return false;
#endif
        }
        // volta ao servidor online normal (se este PC era Host, o servidor continua só se StopHost não foi chamado)
        public static void LeaveLan() { if (!OnLan) return; FBSession.lanServer = null; FBLobbyNet.I?.Reconnect(); }

        public static void Scan(int ms = 1500)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (Scanning) return; var h = Ensure(); Scanning = true; ScanVersion++;
            System.Threading.Tasks.Task.Run(() =>
            {
                List<FBLanDiscovery.Found> r; try { r = FBLanDiscovery.Scan(ms); } catch { r = new List<FBLanDiscovery.Found>(); }
                h.main.Enqueue(() =>
                {
                    Found.Clear();
                    foreach (var f in r) Found.Add(new Achado { ip = f.ip, port = f.port, name = f.name, url = f.Url, players = f.players, rooms = f.rooms?.Count ?? 0 });
                    Scanning = false; ScanVersion++;
                    Debug.Log($"[FreeBala][LAN] procura na rede: {Found.Count} Host(s) {string.Join(", ", Found.Select(f => f.name + "@" + f.ip + ":" + f.port))}");
                });
            });
#endif
        }

        void OnApplicationQuit()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            try { srv?.Dispose(); } catch { } srv = null;
#endif
        }
        void OnDestroy()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            try { srv?.Dispose(); } catch { } srv = null;
#endif
            if (inst == this) inst = null;
        }

        // -fblanhost: abre o jogo já hospedando (teste com dois jogos: um com -fblanhost, outro com -fblanjoin 127.0.0.1)
        static bool argsDone;
        public static void FromArgs()
        {
            if (argsDone) return; argsDone = true;   // o menu abre de novo a cada volta da partida: só na primeira vez
            var a = System.Environment.GetCommandLineArgs();
            if (System.Array.IndexOf(a, "-fblanhost") >= 0) StartHost();
            int j = System.Array.IndexOf(a, "-fblanjoin"); if (j >= 0 && j + 1 < a.Length) Join(a[j + 1]);
        }
    }
}
