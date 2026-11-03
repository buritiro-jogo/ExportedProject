// FBPiloto (01/11, só no teste do .exe): um bot que joga com o NOSSO jogador para achar bugs.
// Ele só usa a entrada normal do jogo (FBPlayer.TestInput, o mesmo caminho do teclado e do mouse): WASD relativo à
// câmera, mouse em graus, tiro, mira, recarga, troca de arma, pulo, cura e a tecla B. Na loja ele clica nos botões
// como um mouse (aba, carta, COMPRAR) e confere pelo dinheiro e pelo efeito se a compra aconteceu.
// Segue o Goal() do modo (o mesmo destino que os bots do time recebem) e a mesma ordem de reserva dos bots.
// Anota: preso, sem arma, compra que falhou, morte (e se foi queda) e câmera atravessando parede.
// Ligado pelo FBTesteExe com a opção "piloto" (ex.: -fbtesteexe 10x10 praca_pt 13 piloto). Não altera o FreeBala.
// [parceiro 02/11] copiado de parceiro_ExportedProject/Assets/Scripts/FBTeste/FBPiloto.cs (lá no Assembly-CSharp, fora de namespace);
// aqui fica no FreeBala.Runtime, dentro do namespace FreeBala.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FreeBala
{
    [DefaultExecutionOrder(-500)]
    public class FBPiloto : MonoBehaviour
    {
        // ---- ligações com o harness ----
        public Action<string> Log = s => Debug.Log("[FreeBala][piloto] " + s);
        public Func<string> Fase = () => "?";
        public Func<Vector3, Vector3, string> NavEstado = (a, b) => "?";
        public string FotoPedida; // nome da foto da tela que o harness deve tirar (o harness zera)

        public class Conta
        {
            public int compras, compraFalhou, semArma, presos, camParede, tiros, acertos, abates, mortes, mortesQueda, curas, recargas, trocas, rodadas;
            public float andou, primeiraVista = -1f;
        }
        public Conta rod = new Conta(), tot = new Conta();
        readonly List<string> comprasRodada = new List<string>();
        readonly List<string> fins = new List<string>();
        string morteRodada, armaRodada = "?";

        FBPlayer P;
        FBCharacter me;
        FBInputState sai;
        int feito = -1;
        bool ligado;
        readonly System.Random rng = new System.Random(20261101);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        static readonly FieldInfo fAutoRun = typeof(FBPlayer).GetField("autoRun", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo fFric = typeof(FBPlayer).GetField("assistFric", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo fShopGo = typeof(FBShop).GetField("go", BindingFlags.NonPublic | BindingFlags.Static);
        static readonly FieldInfo fSelId = typeof(FBShop).GetField("selId", BindingFlags.NonPublic | BindingFlags.Static);

        string faseAnt = "";
        int rodadaAnt = -1;
        float tFase, tLive;
        int fotosRodada;
        bool fotoMira, fotoPreso, fotoCam, foto20;

        // ---- compra ----
        class Plano { public string id, cat; public int price; public bool gear; }
        string compraEst = "inicio";
        List<Plano> plano;
        int idx, sub, pulsoB, tentB, tentFechar, proxQuadro;
        float espera;
        int m0, kits0; float armor0; bool helmet0;
        string modoTime = "?"; int modoRodada = -1;

        // ---- navegação ----
        Vector3? goalP;
        string fonteGoal = "";
        float goalT, navT;
        List<Vector3> path;
        Vector3 pathDest;
        Vector3 amostraPos, posAnt;
        float amostraT, paradoT, lateralT, lateralLado;
        int fasePreso;
        float proxSprint;

        // ---- combate ----
        readonly Vector3[] pts = new Vector3[3];
        FBCharacter alvo, atacante;
        float sigMira;
        public bool MiraBot; // v19: erro de mira igual ao dos bots (opção "mirabot" do FBTesteExe), para teste de equilíbrio
        bool alvoVisto;
        int parteVista = 1;
        float thinkT, viuT = -99f, ultimoVistoT = -99f, atacadoT = -99f, reacao;
        Vector3 ultimoVisto, ruido;
        float ruidoT, strafeT, strafeLado = 1f;
        int rajadaN, rajadaTiros, shot0;
        float pausaRajada, proxSemi;
        float proxTroca, proxRecarga, proxCura, curaIni;
        bool curando, avisouSemBala, mortoAnotado, vivoAnt, curaPedida, mirandoAds;
        float paraCuraT, velCura;
        bool chaoCura;
        Vector3 presoIni;
        float estritoAte = -99f;
        float camT;
        int camRuim;
        int shotsLive;

        // ================= liga / desliga =================
        public void Liga()
        {
            if (ligado) return;
            ligado = true;
            FBPlayer.TestInput = Entrada;
            FBDamageEvents.OnDamage += Dano;
            Application.logMessageReceived += LogJogo;
        }

        public void Desliga()
        {
            if (!ligado) return;
            ligado = false;
            if (FBPlayer.TestInput == (Func<FBInputState, FBInputState>)Entrada) FBPlayer.TestInput = null;
            FBDamageEvents.OnDamage -= Dano;
            Application.logMessageReceived -= LogJogo;
        }

        void OnDestroy() { Desliga(); }

        FBInputState Entrada(FBInputState real)
        {
            // a entrada real (teclado e mouse do PC) é ignorada: o piloto manda tudo
            if (feito != Time.frameCount) PassoSeguro();
            return sai;
        }

        void Update()
        {
            if (ligado && feito != Time.frameCount) PassoSeguro();
        }

        int errosPiloto;
        void PassoSeguro()
        {
            try { Passo(); }
            catch (Exception e)
            {
                sai = Neutro();
                if (errosPiloto++ < 5) Log("   piloto: erro interno do piloto (não é bug do jogo): " + e.GetType().Name + " " + e.Message);
            }
        }

        static FBInputState Neutro()
        {
            var s = default(FBInputState);
            s.slot = -1; // default teria slot 0 e trocaria para o fuzil todo quadro
            return s;
        }

        void LogJogo(string c, string st, LogType ty)
        {
            // "[BURITIRO] 10x10 rodada N compra: time0=X time1=Y" diz o modo de compra do time (COMPLETA, FORCADA ou ECO)
            if (ty != LogType.Log || c == null || !c.StartsWith("[BURITIRO] 10x10 rodada") || !c.Contains(" compra: time0=")) return;
            try
            {
                string l = c.Split('\n')[0];
                int r0 = "[BURITIRO] 10x10 rodada ".Length;
                int r1 = l.IndexOf(' ', r0);
                if (!int.TryParse(l.Substring(r0, r1 - r0), out int rr)) return;
                int t = FBMatch.Player ? FBMatch.Player.team : FBSession.team;
                string chave = " time" + t + "=";
                int a = l.IndexOf(chave);
                if (a < 0) return;
                a += chave.Length;
                int b = l.IndexOf(' ', a);
                modoTime = (b < 0 ? l.Substring(a) : l.Substring(a, b - a)).Trim();
                modoRodada = rr;
            }
            catch { }
        }

        // ================= quadro =================
        void Passo()
        {
            feito = Time.frameCount;
            sai = Neutro();
            P = FBPlayer.I;
            var g = FBMatch.I;
            if (!P || !g || !P.ch || P.wu == null) return;
            me = P.ch;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            string f = Fase();
            int r = g.mode != null ? g.mode.Round : 0;
            if (f != faseAnt || r != rodadaAnt) MudouFase(faseAnt, f, r);
            tFase += dt;
            if (f == "live") tLive += dt;
            // morte sem evento de dano (por segurança)
            if (vivoAnt && !me.alive && !mortoAnotado && f == "live") { mortoAnotado = true; rod.mortes++; morteRodada = $"sem evento de dano · em ({me.Pos.x:0}, {me.Pos.y:0.0}, {me.Pos.z:0}) · último motivo \"{me.lastWhy}\""; Log($"   piloto MORREU na rodada {r} com {tLive:0} s: {morteRodada}"); }
            vivoAnt = me.alive;
            if (FBMatch.Paused) return;
            if (!me.alive || g.state != "play") return; // morto (espectando) ou partida acabou
            CameraNaParede(dt, r);
            if (f == "buy") { Compra(dt, r); return; }
            if (f == "live" && !g.freeze) Joga(dt, g, r);
        }

        void MudouFase(string de, string para, int r)
        {
            if (de == "live") FimRodada(rodadaAnt);
            if (para == "buy")
            {
                if (de == "post")
                {
                    // no pós-rodada os bots ainda atiram: o que aconteceu ali entra no total (sem contar mais uma rodada)
                    tot.mortes += rod.mortes; tot.mortesQueda += rod.mortesQueda; tot.camParede += rod.camParede;
                    if (rod.mortes > 0 || rod.camParede > 0) Log($"   piloto depois do fim da rodada {rodadaAnt}: mortes {rod.mortes} (de queda {rod.mortesQueda}) · câmera na parede {rod.camParede}");
                }
                compraEst = "inicio"; plano = null; idx = 0; sub = 0; pulsoB = 0; tentB = 0; tentFechar = 0; espera = 0f;
                comprasRodada.Clear();
                rod = new Conta(); morteRodada = null; mortoAnotado = false;
                fotosRodada = 0; fotoMira = fotoPreso = fotoCam = foto20 = false;
                ResetaNav();
                alvo = null; atacante = null; alvoVisto = false; viuT = ultimoVistoT = atacadoT = -99f; curando = false; avisouSemBala = false;
                curaPedida = false; paraCuraT = 0f; mirandoAds = false; estritoAte = -99f;
            }
            if (para == "live")
            {
                tLive = 0f; ResetaNav(); posAnt = me ? me.Pos : Vector3.zero; shot0 = P ? P.ShotCount : 0; shotsLive = 0;
                var wu = P ? P.wu : null;
                armaRodada = wu != null ? string.Join("+", wu.slots.Where(x => x != null).Select(x => x.id)) : "?";
                if (armaRodada == "") armaRodada = "nenhuma";
                if (wu != null && me && me.alive && !wu.HasAny)
                {
                    rod.semArma++; avisouSemBala = true;
                    Log($"   piloto SEM ARMA no começo da rodada {r} · dinheiro ${FBEconomy.Money(me)} · comprou: {(comprasRodada.Count > 0 ? string.Join(", ", comprasRodada) : "nada")}");
                }
            }
            faseAnt = para; rodadaAnt = r; tFase = 0f;
        }

        void ResetaNav()
        {
            goalP = null; goalT = 0f; navT = 0f; path = null; paradoT = 0f; fasePreso = 0; amostraT = 0f; lateralT = 0f;
            if (me) amostraPos = me.Pos;
        }

        void Foto(string nome)
        {
            if (fotosRodada >= 4 || FotoPedida != null) return;
            fotosRodada++;
            FotoPedida = nome;
        }

        // ================= compra =================
        void Compra(float dt, int r)
        {
            if (!FBEconomy.buying) return;
            if (pulsoB == 1) { sai.buyReleased = true; pulsoB = 0; espera = 0.2f; proxQuadro = Time.frameCount + 2; return; } // soltou o B
            if (compraEst == "feita") return;
            espera -= dt;
            if (espera > 0f || Time.frameCount < proxQuadro) return;
            var wu = P.wu;
            if (!FBShop.Open)
            {
                if (compraEst == "fechar")
                {
                    compraEst = "feita";
                    int s = MelhorSlot(wu);
                    if (s >= 0 && wu.cur != s) sai.slot = s;
                    return;
                }
                // a loja abre sozinha em toda fase de compra; se estiver fechada, aperta B (o caminho normal)
                if (FBEconomy.buyT < 1.5f) return;
                if (tentB == 0)
                {
                    rod.compraFalhou++;
                    Log(compraEst == "clicando"
                        ? $"   piloto rodada {r}: a loja FECHOU sozinha no meio da compra (item {(plano != null && idx < plano.Count ? plano[idx].id : "?")}); reabrindo com B"
                        : $"   piloto rodada {r}: a loja NÃO abriu sozinha na fase de compra (faltam {FBEconomy.buyT:0.0} s); abrindo com B");
                }
                if (tentB >= 3) { Log($"   piloto rodada {r}: a loja não abriu com B (3 tentativas)"); rod.compraFalhou++; compraEst = "feita"; return; }
                tentB++;
                sai.buy = true; pulsoB = 1;
                return;
            }
            if (compraEst == "inicio")
            {
                plano = Planejar(r);
                idx = 0; sub = 0;
                compraEst = plano.Count > 0 ? "clicando" : "fechar";
                espera = R(0.6f, 1.1f); // tempo de ler a loja
                return;
            }
            if (compraEst == "clicando") { PassoCompra(r); return; }
            if (compraEst == "fechar")
            {
                if (tentFechar >= 3) { Log($"   piloto rodada {r}: a loja não fechou com B (3 tentativas)"); compraEst = "feita"; return; }
                tentFechar++;
                sai.buy = true; pulsoB = 1; // B fecha a loja (FBShop.Show(!Open))
            }
        }

        List<Plano> Planejar(int r)
        {
            var wu = P.wu;
            var dados = FBEconomy.ShopData();
            var lista = new List<Plano>();
            int resta = FBEconomy.Money(me);
            string modo = modoRodada == r ? modoTime : "?";
            bool temFuzil = wu.slots[0] != null;
            bool temSmg = wu.slots[1] != null;
            bool temPistola = wu.slots[2] != null;
            bool querColete = me.armor < 60f;
            FBEconomy.Item Melhor(string cat, int ate)
            {
                FBEconomy.Item m = null;
                foreach (var c in dados) if (c.cat == cat) foreach (var it in c.items) if (!it.gear && !it.own && it.price <= ate && (m == null || it.price > m.price)) m = it;
                return m;
            }
            FBEconomy.Item Barata(string cat)
            {
                FBEconomy.Item m = null;
                foreach (var c in dados) if (c.cat == cat) foreach (var it in c.items) if (!it.gear && (m == null || it.price < m.price)) m = it;
                return m;
            }
            FBEconomy.Item Gear(string id)
            {
                foreach (var c in dados) foreach (var it in c.items) if (it.gear && it.id == id) return it;
                return null;
            }
            string CatDe(FBEconomy.Item it) { foreach (var c in dados) if (c.items.Contains(it)) return c.cat; return "?"; }
            void Poe(FBEconomy.Item it) { if (it == null) return; lista.Add(new Plano { id = it.id, cat = CatDe(it), price = it.price, gear = it.gear }); resta -= it.price; }
            var armas = new List<FBEconomy.Item>();
            var equip = new List<FBEconomy.Item>();
            if (modo == "ECO")
            {
                if (!wu.HasAny) { var p = Barata("PISTOLAS"); if (p != null && p.price <= resta) { armas.Add(p); resta -= p.price; } }
                var col = Gear("colete");
                if (querColete && col != null && resta - col.price >= 1900) { equip.Add(col); resta -= col.price; }
            }
            else
            {
                int reserva = querColete ? 650 : 0;
                FBEconomy.Item pri = null;
                if (!temFuzil)
                {
                    pri = Melhor("FUZIS", Mathf.Min(resta - reserva, 2700));
                    if (pri == null && !temSmg) pri = Melhor("SUBMETRALHADORAS", Mathf.Min(resta - reserva, 1500));
                }
                if (pri != null) resta -= pri.price;
                if (pri == null && !temFuzil && !temSmg && !temPistola)
                {
                    var p = Barata("PISTOLAS");
                    if (p != null && p.price <= resta) { armas.Add(p); resta -= p.price; }
                }
                var col = Gear("colete");
                if (querColete && col != null && resta >= col.price) { equip.Add(col); resta -= col.price; }
                var cap = Gear("capacete");
                if (!me.helmet && cap != null && resta >= cap.price && (temFuzil || pri != null)) { equip.Add(cap); resta -= cap.price; }
                var med = Gear("medkit");
                if (FBMed.kits < 2 && med != null && resta >= med.price + 800) { equip.Add(med); resta -= med.price; }
                if (pri != null) armas.Add(pri); // a arma principal por último: Give equipa a última comprada
            }
            resta = FBEconomy.Money(me);
            foreach (var it in equip) Poe(it);
            foreach (var it in armas) Poe(it);
            Log($"   piloto rodada {r} compra: dinheiro ${FBEconomy.Money(me)} · modo do time {modo} · tem {(wu.HasAny ? string.Join("+", wu.slots.Where(x => x != null).Select(x => x.id)) : "nada")} · colete {me.armor:0} · capacete {(me.helmet ? "sim" : "não")} · plano: {(lista.Count > 0 ? string.Join(", ", lista.Select(x => $"{x.id} ${x.price}")) : "nada")}");
            return lista;
        }

        static GameObject Raiz() => fShopGo?.GetValue(null) as GameObject;

        static Button Botao(string nome, bool prefixo = false)
        {
            var raiz = Raiz();
            if (!raiz) return null;
            foreach (var b in raiz.GetComponentsInChildren<Button>(false))
                if (b && b.isActiveAndEnabled && (prefixo ? b.gameObject.name.StartsWith(nome) : b.gameObject.name == nome)) return b;
            return null;
        }

        static void Clica(Button b)
        {
            // o mesmo evento do clique do mouse (respeita interactable)
            var ev = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(b.gameObject, ev, ExecuteEvents.pointerClickHandler);
        }

        void Prox() { idx++; sub = 0; espera = R(0.2f, 0.4f); proxQuadro = Time.frameCount + 2; }

        void Falhou(Plano it, string motivo, int r)
        {
            rod.compraFalhou++;
            Log($"   piloto rodada {r}: COMPRA FALHOU {it.id} (${it.price}): {motivo} · dinheiro ${FBEconomy.Money(me)} · fase de compra {FBEconomy.buyT:0.0} s");
        }

        void PassoCompra(int r)
        {
            if (idx >= plano.Count) { compraEst = "fechar"; espera = R(0.3f, 0.6f); return; }
            var it = plano[idx];
            var wu = P.wu;
            switch (sub)
            {
                case 0:
                {
                    var b = Botao("cat_" + it.cat);
                    if (!b) { Falhou(it, $"a aba {it.cat} não está na loja", r); Prox(); return; }
                    Clica(b);
                    sub = 1; espera = R(0.25f, 0.45f); proxQuadro = Time.frameCount + 2;
                    return;
                }
                case 1:
                {
                    string sel = fSelId?.GetValue(null) as string;
                    if (sel != it.id)
                    {
                        // a aba já escolhe sozinha a mais cara que dá; se não for a do plano, 1 clique na carta seleciona
                        var c = Botao("carta_" + it.id);
                        if (!c) { Falhou(it, "a carta não está na aba (arma sem modelo ou fora da loja)", r); Prox(); return; }
                        Clica(c);
                    }
                    sub = 2; espera = R(0.25f, 0.45f); proxQuadro = Time.frameCount + 2;
                    return;
                }
                case 2:
                {
                    string sel = fSelId?.GetValue(null) as string;
                    if (sel != it.id) { Falhou(it, $"a carta não ficou selecionada (selecionada: {sel ?? "nenhuma"})", r); Prox(); return; }
                    m0 = FBEconomy.Money(me); armor0 = me.armor; helmet0 = me.helmet; kits0 = FBMed.kits;
                    // 2º clique na carta já selecionada = DoBuy(item) (FBShop.Grid). O botão COMPRAR do painel compra
                    // Find(hoverId) ?? Find(selId), e o mouse real parado em cima de outra carta trocaria o item.
                    var bc = Botao("carta_" + it.id);
                    if (!bc || !FBEconomy.buying || m0 < it.price)
                    {
                        string mot = !bc ? "a carta sumiu da aba" : !FBEconomy.buying ? "fora da fase de compra" : $"sem dinheiro (tem ${m0})";
                        Falhou(it, mot, r); Prox(); return;
                    }
                    Clica(bc);
                    sub = 3; espera = 0.05f; proxQuadro = Time.frameCount + 2;
                    return;
                }
                default:
                {
                    int m = FBEconomy.Money(me);
                    bool efeito;
                    if (!it.gear)
                    {
                        int s = GameData.Weapons.TryGetValue(it.id, out var def) && def != null ? def.slot : -1;
                        efeito = s >= 0 && s < 3 && wu.slots[s] != null && wu.slots[s].id == it.id;
                    }
                    else if (it.id == "colete") efeito = me.armor >= 99f;
                    else if (it.id == "capacete") efeito = me.helmet;
                    else if (it.id == "kit") efeito = me.armor >= 99f && me.helmet;
                    else if (it.id == "medkit") efeito = FBMed.kits == kits0 + 1;
                    else efeito = m < m0;
                    bool pagou = m == m0 - it.price;
                    if (efeito && pagou) { rod.compras++; comprasRodada.Add($"{it.id} ${it.price}"); }
                    else Falhou(it, $"cobrou ${m0 - m} (preço ${it.price}) · efeito {(efeito ? "aconteceu" : "NÃO aconteceu")} (colete {armor0:0}→{me.armor:0}, capacete {helmet0}→{me.helmet}, medkits {kits0}→{FBMed.kits})", r);
                    Prox();
                    return;
                }
            }
        }

        static int MelhorSlotComBala(FBWeaponUser wu)
        {
            for (int s = 0; s < 3; s++) if (wu.slots[s] != null && wu.ammo[s] + wu.reserve[s] > 0) return s;
            return -1;
        }

        static int MelhorSlot(FBWeaponUser wu)
        {
            for (int s = 0; s < 3; s++) if (wu.slots[s] != null && wu.ammo[s] + wu.reserve[s] > 0) return s;
            for (int s = 0; s < 3; s++) if (wu.slots[s] != null) return s;
            return -1;
        }

        // ================= jogo (fase valendo) =================
        void Joga(float dt, FBMatch g, int r)
        {
            var wu = P.wu;
            float now = Time.time;
            var cam = P.cam;
            if (!cam) return;
            // andou (sem contar teleporte)
            float passo = (me.Pos - posAnt).Flat().magnitude;
            if (passo < 5f) rod.andou += passo;
            posAnt = me.Pos;
            rod.tiros = P.ShotCount - shot0;
            if (!foto20 && tLive >= 20f) { foto20 = true; Foto($"piloto_rodada{r}_20s"); }

            // alvo: quem está na tela, a até 120 m, com linha de visão da câmera
            thinkT -= dt;
            if (thinkT <= 0f) { thinkT = R(0.12f, 0.2f); EscolheAlvo(cam, now); }
            if (alvo && !alvo.alive) { alvo = null; alvoVisto = false; }

            // cura parada quando não tem inimigo à vista (H, depois fica parado sem tocar em nada até acabar)
            if (curando && curaPedida && now > curaIni)
            {
                curaPedida = false;
                if (FBMed.Busy) rod.curas++;
                else { Log($"   piloto: H não começou a cura · ao apertar: velocidade {velCura:0.00} m/s, no chão {chaoCura}, trocando arma {P.wu.switching > 0f} · vida {me.hp:0} · medkits {FBMed.kits}"); proxCura = now + 1.5f; curando = false; }
            }
            if (curando)
            {
                bool acabou = (!FBMed.Busy && now - curaIni > 0.6f) || now - curaIni > 7f;
                if (!acabou && !alvoVisto && now - atacadoT > 0.5f) return; // parado: qualquer tecla cancela a cura
                curando = false;
            }

            // arma: melhor slot com bala; troca se a da mão secou
            bool semBalaNaMao = wu.Cur == null || (wu.Ammo == 0 && wu.Reserve == 0);
            int melhor = MelhorSlot(wu);
            if (melhor >= 0 && wu.cur != melhor && wu.switching <= 0f && now >= proxTroca && (semBalaNaMao || !alvoVisto))
            { sai.slot = melhor; proxTroca = now + 0.8f; rod.trocas++; }
            if (!avisouSemBala && !wu.HasAny) { avisouSemBala = true; rod.semArma++; Log($"   piloto SEM ARMA na rodada {r} com {tLive:0} s"); }
            else if (!avisouSemBala && semBalaNaMao && MelhorSlotComBala(wu) < 0)
            { avisouSemBala = true; Log($"   piloto ficou SEM MUNIÇÃO na rodada {r} com {tLive:0} s ({armaRodada})"); }

            // recarga: R quando o pente está baixo e não tem ninguém à vista, ou vazio
            if (wu.Cur != null && wu.reloadT <= 0f && wu.switching <= 0f && wu.Reserve > 0 && now >= proxRecarga && wu.Ammo < wu.Cur.mag &&
                (wu.Ammo == 0 || (!alvoVisto && now - ultimoVistoT > 1.2f && wu.Ammo < wu.Cur.mag * 0.5f)))
            { sai.reload = true; proxRecarga = now + 1f; rod.recargas++; }

            // cura
            if (me.hp < 55f && FBMed.kits > 0 && !alvoVisto && now - ultimoVistoT > 3f && now - atacadoT > 3f && !FBMed.Busy && now >= proxCura && wu.reloadT <= 0f && wu.switching <= 0f && !me.knocked)
            {
                // o jogo recusa a cura andando ("PARE PARA SE CURAR"): solta o WASD, espera parar e só então aperta H
                bool autoRunC = fAutoRun != null && (bool)fAutoRun.GetValue(P);
                if (me.velocity.Flat().magnitude > 0.25f || !P.OnGround || autoRunC)
                {
                    if (autoRunC && now >= proxSprint) { sai.sprintToggle = true; proxSprint = now + 0.4f; }
                    paraCuraT += dt;
                    if (paraCuraT > 1.5f) { paraCuraT = 0f; proxCura = now + 3f; Log($"   piloto não conseguiu parar para se curar (velocidade {me.velocity.Flat().magnitude:0.00} m/s, no chão {P.OnGround})"); }
                    return;
                }
                paraCuraT = 0f;
                sai.heal = true; curando = true; curaPedida = true; curaIni = now; proxCura = now + 8f; velCura = me.velocity.Flat().magnitude; chaoCura = P.OnGround;
                return;
            }
            paraCuraT = 0f;

            bool combate = alvo && alvoVisto;
            Vector3 dirMov = Vector3.zero;
            bool correr = false;
            if (combate)
            {
                FBBotSense.TargetPoints(alvo, pts);
                ruidoT -= dt;
                if (ruidoT <= 0f)
                {
                    if (MiraBot && alvo)
                    {
                        // v19 "mirabot": o ponto mirado leva o mesmo erro de mira dos bots (FBBotSense.AimErrorDeg com o tempo desde
                        // que viu o alvo e se está andando), sorteado de novo a cada 0,12-0,2 s, quase a cada tiro como nos bots.
                        ruidoT = R(0.12f, 0.2f);
                        Vector3 co = me.MuzzlePos, alvoPt = pts[parteVista];
                        float dd = Mathf.Max(0.5f, (alvoPt - co).magnitude);
                        float sig = FBBotSense.AimErrorDeg(me, alvo, now - viuT, me.velocity.Flat().magnitude > 1f);
                        sigMira = sig;
                        ruido = co + FBBotSense.Scatter((alvoPt - co) / dd, sig) * dd - alvoPt;
                    }
                    else { ruidoT = R(0.3f, 0.5f); ruido = new Vector3(R(-0.12f, 0.12f), R(-0.1f, 0.12f), R(-0.12f, 0.12f)); }
                }
                Vector3 ponto = pts[parteVista] + ruido;
                float dist = (ponto - cam.transform.position).magnitude;
                mirandoAds = dist > 12.5f || (mirandoAds && dist > 11.5f);
                sai.aim = wu.Cur != null && wu.reloadT <= 0f && wu.switching <= 0f && (mirandoAds || wu.Cur.IsSniper);
                Mira(cam, ponto, 13f, 720f, dt);
                float erro = Vector3.Angle(cam.transform.forward, ponto - cam.transform.position);
                if (MiraBot) erro = Mathf.Max(0f, Vector3.Angle(cam.transform.forward, pts[parteVista] - cam.transform.position) - 3.3f * sigMira);
                float tol = Mathf.Max(0.7f, Mathf.Atan2(0.28f, Mathf.Max(1f, dist)) * Mathf.Rad2Deg);
                Atira(wu, now, dist, erro, tol, r);
                // movimento: de lado (strafe), chega perto se estiver longe do alcance da arma, recua se muito perto
                float alcance = FBBotSense.EngageRange(wu.Cur);
                Vector3 para = (alvo.Pos - me.Pos).Flat();
                Vector3 fr = para.sqrMagnitude > 0.01f ? para.normalized : Vector3.forward;
                Vector3 lado = Vector3.Cross(Vector3.up, fr);
                strafeT -= dt;
                if (strafeT <= 0f) { strafeT = R(0.5f, 1.3f); strafeLado = rng.NextDouble() < 0.15 ? 0f : (rng.NextDouble() < 0.5 ? -1f : 1f); }
                if (P.AimBlocked && strafeLado == 0f) strafeLado = 1f;
                dirMov = lado * strafeLado;
                if (dist > alcance) { goalP = alvo.Pos; fonteGoal = "inimigo fora do alcance"; goalT = Mathf.Max(goalT, 2f); dirMov += Navega(dt, g) * 1.2f; }
                else if (dist < 6f) dirMov -= fr * 0.6f;
            }
            else
            {
                sai.aim = false; mirandoAds = false;
                bool procura = now - ultimoVistoT < 6f;
                bool atacado = atacante && atacante.alive && now - atacadoT < 3f;
                if (atacado)
                {
                    // tomou tiro sem ver quem: vira para o atirador e vai para lá
                    Mira(cam, atacante.Pos + Vector3.up * 1.4f, 9f, 540f, dt);
                    if ((atacante.Pos - me.Pos).magnitude < 90f) { goalP = atacante.Pos; fonteGoal = "quem atirou"; goalT = 6f; }
                    dirMov = Navega(dt, g);
                }
                else if (procura && (ultimoVisto - me.Pos).Flat().magnitude > 3f)
                {
                    // perdeu o inimigo de vista: vai até onde viu por último, olhando para lá
                    if (goalP != ultimoVisto) { goalP = ultimoVisto; fonteGoal = "onde viu o inimigo"; goalT = 6f; }
                    dirMov = Navega(dt, g);
                    Mira(cam, ultimoVisto + Vector3.up * 1.4f, 7f, 400f, dt);
                }
                else
                {
                    dirMov = Navega(dt, g);
                    // olha para onde anda, um pouco abaixo do horizonte
                    if (dirMov.sqrMagnitude > 0.01f)
                    {
                        float yawA = Mathf.Atan2(dirMov.x, dirMov.z) * Mathf.Rad2Deg;
                        Gira(yawA, -4f, 7f, 360f, dt);
                        correr = Mathf.Abs(Mathf.DeltaAngle(P.camYaw, yawA)) < 35f && goalP.HasValue && (goalP.Value - me.Pos).Flat().magnitude > 6f;
                    }
                }
            }

            // preso: querendo andar e não sai do lugar
            Vector3 dirFinal = dirMov.sqrMagnitude > 1f ? dirMov.normalized : dirMov;
            Preso(dt, r, ref dirFinal, combate);
            Move(dirFinal);

            // sprint: um quadro de sprintToggle (FBSession.holdRun desligado), com W para frente
            bool autoRun = fAutoRun != null && (bool)fAutoRun.GetValue(P);
            if (now >= proxSprint && P.OnGround && !sai.fire && !sai.aim)
            {
                if (correr && !autoRun && sai.move.y >= 0.5f && Mathf.Abs(sai.move.x) <= 0.5f) { sai.sprintToggle = true; proxSprint = now + 0.4f; }
                else if (!correr && autoRun && (combate || now - ultimoVistoT < 6f)) { sai.sprintToggle = true; proxSprint = now + 0.4f; }
            }
        }

        void EscolheAlvo(Camera cam, float now)
        {
            Vector3 co = cam.transform.position, cf = cam.transform.forward;
            FBCharacter melhor = null;
            float score = 1e9f;
            int parte = 1;
            foreach (var c in FBMatch.All)
            {
                if (!c || !c.alive || c == me || c.team > 1 || !FBMatch.Hostile(me, c)) continue;
                FBBotSense.TargetPoints(c, pts);
                Vector3 d = pts[1] - co;
                float dist = d.magnitude;
                if (dist > 120f || dist < 0.1f) continue;
                float ang = Vector3.Angle(cf, d);
                bool campo = ang <= 50f || (c == alvo && ang <= 100f) || (c == atacante && now - atacadoT < 3f && ang <= 120f);
                if (!campo) continue;
                int vis = -1;
                foreach (int k in new[] { 1, 0, 2 })
                {
                    Vector3 dd = pts[k] - co;
                    float l = dd.magnitude;
                    if (l < 0.5f || !FBWorld.RayHit(co, dd / l, l - 0.3f, 0.04f)) { vis = k; break; }
                }
                if (vis < 0) continue;
                float s = dist + ang * 0.6f - (c == alvo ? 12f : 0f);
                if (s < score) { score = s; melhor = c; parte = vis; }
            }
            if (melhor && melhor != alvo) { viuT = now; reacao = R(0.22f, 0.4f) * FBCombat.DiffReact; rajadaTiros = 0; rajadaN = 0; }
            if (melhor) { alvo = melhor; parteVista = parte; ultimoVisto = melhor.Pos; ultimoVistoT = now; if (rod.primeiraVista < 0f) rod.primeiraVista = tLive; }
            alvoVisto = melhor;
            if (!melhor && alvo && now - ultimoVistoT > 6f) alvo = null;
        }

        float Sens(bool aim)
        {
            var cur = P.wu.Cur;
            if (!aim || cur == null) return 1f;
            return Mathf.Min(Json.F(FBWeaponModel.Scope(cur), "multiplierMouseSensitivity", 0.8f), 1.15f / FBScopes.Zoom(cur)) * (0.4f + FBInput.AdsSens * 1.2f);
        }

        // mouse: delta em graus dividido pelo mesmo fator que o FBPlayer.Look aplica (mira e aim assist)
        void Gira(float yawA, float pitchA, float rate, float cap, float dt)
        {
            float dy = Mathf.DeltaAngle(P.camYaw, yawA), dp = pitchA - P.camPitch;
            float k = 1f - Mathf.Exp(-rate * dt);
            float py = Mathf.Clamp(dy * k, -cap * dt, cap * dt), pp = Mathf.Clamp(dp * k, -cap * dt, cap * dt);
            float num = Mathf.Max(0.05f, Sens(sai.aim));
            float fr = fFric != null ? (float)fFric.GetValue(P) : 1f;
            if (fr < 0.05f) fr = 1f;
            sai.look = new Vector2(py, pp) / (num * fr);
        }

        void Mira(Camera cam, Vector3 p, float rate, float cap, float dt)
        {
            Vector3 d = p - cam.transform.position;
            float yawA = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitchA = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            Gira(yawA, pitchA, rate, cap, dt);
        }

        void Atira(FBWeaponUser wu, float now, float dist, float erro, float tol, int r)
        {
            var w = wu.Cur;
            if (w == null || wu.reloadT > 0f || wu.switching > 0f || wu.Ammo <= 0) return;
            if (now - viuT < reacao) return;
            if (P.AimBlocked) return; // parede entre o cano e o ponto mirado: não gasta bala
            if (!fotoMira && erro <= tol * 2f) { fotoMira = true; Foto($"piloto_rodada{r}_mirando"); }
            if (w.auto)
            {
                if (now < pausaRajada) return;
                int feitos = P.ShotCount - rajadaN;
                if (rajadaTiros <= 0) { rajadaTiros = dist > 45f ? rng.Next(3, 6) : dist > 18f ? rng.Next(5, 9) : rng.Next(8, 14); rajadaN = P.ShotCount; feitos = 0; if (erro > tol * 1.6f) { rajadaTiros = 0; return; } }
                if (feitos >= rajadaTiros || erro > tol * 4f) { rajadaTiros = 0; pausaRajada = now + (dist > 45f ? R(0.35f, 0.7f) : R(0.15f, 0.4f)); return; }
                sai.fire = true;
                if (feitos == 0) sai.firePressed = true;
            }
            else
            {
                if (now < proxSemi || erro > tol * 1.5f) return;
                sai.fire = true; sai.firePressed = true; // fire também desliga o sprint, como no teclado
                proxSemi = now + Mathf.Max(w.Interval * 1.1f, w.IsSniper ? R(0.9f, 1.4f) : R(0.16f, 0.3f));
            }
        }

        // ================= navegação =================
        Vector3 Navega(float dt, FBMatch g)
        {
            goalT -= dt;
            bool perto = goalP.HasValue && (goalP.Value - me.Pos).magnitude < 2.5f;
            if (!goalP.HasValue || goalT <= 0f || perto) NovoDestino(g);
            if (!goalP.HasValue) return Vector3.zero;
            Vector3 dest = goalP.Value;
            if (FBNav.TemLimite && !FBNav.DentroLimite(dest, 1f)) dest = FBNav.PraDentro(dest, 1.5f);
            Vector3 alvoP;
            bool estrito = Time.time < estritoAte; // depois de travar: segue os cantos do NavMesh sem atalho por 8 s
            if (!estrito && (dest - me.Pos).Flat().magnitude <= 12f && FBWorld.ClearLine(me.Pos, dest)) alvoP = dest;
            else
            {
                navT -= dt;
                if (path == null || navT <= 0f || (dest - pathDest).sqrMagnitude > 9f)
                {
                    path = FBNav.Path(me.Pos, dest) ?? new List<Vector3> { dest };
                    if (path.Count == 0) path.Add(dest);
                    pathDest = dest; navT = R(2f, 3f);
                }
                while (path.Count > 1 && (path[0] - me.Pos).Flat().magnitude < 1.3f) path.RemoveAt(0);
                if (!estrito && path.Count > 1 && FBWorld.ClearLine(me.Pos, path[1])) path.RemoveAt(0);
                alvoP = path[0];
            }
            Vector3 d = (alvoP - me.Pos).Flat();
            if (d.magnitude < 0.4f) return Vector3.zero;
            return d.normalized;
        }

        void NovoDestino(FBMatch g)
        {
            Vector3? p = null;
            string fonte = "modo (Goal)";
            try { p = g.mode != null ? g.mode.Goal(me) : null; } catch (Exception e) { Log("   piloto: Goal() deu erro: " + e.Message); }
            if (!p.HasValue)
            {
                var intel = FBBotBrain.FreshIntel(me.team, 9f);
                if (intel.HasValue && (intel.Value - me.Pos).magnitude < 120f) { p = intel; fonte = "aviso do time"; }
            }
            if (!p.HasValue && rng.NextDouble() < 0.55)
            {
                var sp = g.TeamSpawns(1 - me.team);
                if (sp != null && sp.Count > 0)
                {
                    Vector3 cen = sp.Aggregate(Vector3.zero, (a, b) => a + b) / sp.Count;
                    cen = Vector3.Lerp(cen, me.Pos, R(0.15f, 0.55f));
                    p = FBNav.RandomPoint(cen, R(14f, 30f));
                    fonte = "lado inimigo";
                }
            }
            if (!p.HasValue) { p = FBNav.RandomPoint(me.Pos, R(30f, 85f)) ?? FBNav.RandomPoint(); fonte = "ponto aleatório"; }
            goalP = p; goalT = R(10f, 20f); fonteGoal = fonte; path = null;
        }

        void Move(Vector3 dirMundo)
        {
            // WASD relativo ao camYaw: y = frente, x = direita
            Vector3 l = Quaternion.Euler(0f, -P.camYaw, 0f) * dirMundo;
            var m = new Vector2(l.x, l.z);
            if (m.sqrMagnitude > 1f) m.Normalize();
            sai.move = m;
        }

        void Preso(float dt, int r, ref Vector3 dir, bool combate)
        {
            if (lateralT > 0f)
            {
                lateralT -= dt;
                Vector3 fr = dir.sqrMagnitude > 0.01f ? dir.normalized : Quaternion.Euler(0f, P.camYaw, 0f) * Vector3.forward;
                dir = (Vector3.Cross(Vector3.up, fr) * lateralLado + fr * 0.3f).normalized;
            }
            // em combate o piloto anda de lado de propósito e pode encostar na parede: não conta como preso
            if (dir.sqrMagnitude < 0.25f || combate) { amostraT = 0f; amostraPos = me.Pos; return; }
            amostraT += dt;
            if (amostraT < 0.5f) return;
            float d = (me.Pos - amostraPos).Flat().magnitude / amostraT;
            amostraT = 0f; amostraPos = me.Pos;
            if (d < 0.7f) { if (paradoT <= 0f) presoIni = me.Pos; paradoT += 0.5f; }
            else if (d > 2f && lateralT <= 0f && (paradoT <= 0f || (me.Pos - presoIni).Flat().magnitude > 7f))
            {
                if (fasePreso >= 3) Log($"   piloto saiu de onde estava preso depois de {paradoT:0} s (rodada {r})");
                paradoT = 0f; fasePreso = 0;
            }
            if (paradoT >= 1.5f && fasePreso == 0) { fasePreso = 1; sai.jump = true; estritoAte = Time.time + 8f; path = null; navT = 0f; }
            else if (paradoT >= 3f && fasePreso == 1) { fasePreso = 2; lateralT = 1.2f; lateralLado = rng.NextDouble() < 0.5 ? -1f : 1f; }
            else if (paradoT >= 5f && fasePreso == 2)
            {
                fasePreso = 3; rod.presos++;
                Vector3 gp = goalP ?? me.Pos;
                Log($"   piloto PRESO na rodada {r} com {tLive:0} s em ({me.Pos.x:0.0}, {me.Pos.y:0.00}, {me.Pos.z:0.0}) · pulou e foi de lado e não saiu · destino ({gp.x:0}, {gp.y:0.0}, {gp.z:0}) a {(gp - me.Pos).Flat().magnitude:0} m ({fonteGoal}) · caminho {NavEstado(me.Pos, gp)} · cantos {(path != null ? path.Count : 0)} · no chão {P.OnGround} · velocidade {me.velocity.magnitude:0.0} m/s");
                if (!fotoPreso) { fotoPreso = true; Foto($"piloto_rodada{r}_preso"); }
                goalP = null; path = null;
            }
            else if (paradoT >= 12f && fasePreso == 3)
            {
                fasePreso = 4;
                Log($"   piloto continua PRESO há 12 s na rodada {r} em ({me.Pos.x:0.0}, {me.Pos.y:0.00}, {me.Pos.z:0.0})");
                goalP = null; path = null; lateralT = 1.5f; lateralLado = -lateralLado;
            }
        }

        // ================= câmera atravessando parede =================
        void CameraNaParede(float dt, int r)
        {
            camT -= dt;
            if (camT > 0f) return;
            camT = 0.25f;
            var cam = P.cam;
            if (!cam || P.firstPerson) { camRuim = 0; return; }
            Vector3 c = cam.transform.position;
            bool dentro = Physics.CheckSphere(c, 0.06f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
            Vector3 cab = me.Head ? me.Head.position : me.Pos + Vector3.up * 1.6f;
            Vector3 pei = me.Chest ? me.Chest.position : me.Pos + Vector3.up * 1.3f;
            string nome = null;
            float distBat = 0f;
            bool Corta(Vector3 o)
            {
                Vector3 d = c - o;
                float l = d.magnitude;
                if (l < 0.35f) return false;
                if (FBWorld.RayHit(o, d / l, l - 0.05f, out RaycastHit h, 0f, 0f)) { nome = h.collider ? h.collider.name : "?"; distBat = h.distance; return true; }
                return false;
            }
            // o jogo põe a câmera em L: pivô (altura do corpo) -> ombro (de lado) -> câmera (para trás) e só libera esses 2 trechos
            Vector3 fw = cam.transform.forward;
            Vector3 piv = me.Pos + Vector3.up * ((me.prone ? 0.45f : me.crouch ? 0.95f : 1.6f) * me.ScaleK);
            Vector3 ombro = c + fw * Vector3.Dot(piv - c, fw);
            bool Seg(Vector3 a, Vector3 b)
            {
                Vector3 d = b - a;
                float l = d.magnitude;
                if (l < 0.1f) return false;
                if (FBWorld.RayHit(a, d / l, l - 0.05f, out RaycastHit h, 0f, 0f)) { nome = h.collider ? h.collider.name : "?"; distBat = h.distance; return true; }
                return false;
            }
            bool atras = Seg(piv, ombro) || Seg(ombro, c);
            string nomeL = nome; float distL = distBat;
            bool atrasCabeca = atras && Corta(cab) && Corta(pei);
            nome = nomeL; distBat = distL;
            if (!dentro && !atras) { camRuim = 0; return; }
            camRuim++;
            if (camRuim != 2) return; // 2 checagens seguidas (0,5 s) para não contar o quadro em que a câmera ainda está encostando
            rod.camParede++;
            if (rod.camParede <= 3)
                Log($"   piloto CÂMERA NA PAREDE na rodada {r} ({Fase()}, {tFase:0} s): {(dentro ? "a câmera está dentro de um colisor" : "tem parede entre o jogador e a câmera")}{(nome != null ? $" ({nome} a {distBat:0.00} m)" : "")}{(atras ? (atrasCabeca ? " · cabeça e peito também não veem a câmera" : " · cabeça vê a câmera") : "")} · jogador em ({me.Pos.x:0.0}, {me.Pos.y:0.00}, {me.Pos.z:0.0}) · câmera a {(c - cab).magnitude:0.00} m da cabeça{(sai.aim ? " · mirando" : "")}");
            if (!fotoCam) { fotoCam = true; Foto($"piloto_rodada{r}_camera_parede"); }
        }

        // ================= dano, morte e acertos =================
        void Dano(FBDamage d)
        {
            if (!me || !ligado) return;
            try
            {
                if (d.victim == me)
                {
                    if (d.source && d.source != me && FBMatch.Hostile(me, d.source)) { atacante = d.source; atacadoT = Time.time; }
                    if (d.killed && !mortoAnotado)
                    {
                        mortoAnotado = true;
                        rod.mortes++;
                        bool queda = d.type == FBDamageType.Fall || d.why == "fall";
                        if (queda) rod.mortesQueda++;
                        var s = d.source;
                        morteRodada = queda ? $"QUEDA (motivo \"{d.why}\") em ({me.Pos.x:0}, {me.Pos.y:0.0}, {me.Pos.z:0})"
                            : $"{d.type} por {(s ? s.Name : "?")} ({(s && s.weapon != null ? s.weapon.id : "?")}) a {(s ? (s.Pos - me.Pos).magnitude : 0f):0} m{(d.head ? ", na cabeça" : "")} · em ({me.Pos.x:0}, {me.Pos.y:0.0}, {me.Pos.z:0})";
                        Log($"   piloto MORREU na rodada {rodadaAnt} com {tLive:0} s: {morteRodada}");
                    }
                }
                else if (d.source == me && d.victim && d.victim != me && FBMatch.Hostile(me, d.victim) && d.applied > 0f)
                {
                    rod.acertos++;
                    if (d.killed) rod.abates++;
                }
            }
            catch { }
        }

        // ================= relatório =================
        void Soma(Conta a, Conta b)
        {
            b.compras += a.compras; b.compraFalhou += a.compraFalhou; b.semArma += a.semArma; b.presos += a.presos; b.camParede += a.camParede;
            b.tiros += a.tiros; b.acertos += a.acertos; b.abates += a.abates; b.mortes += a.mortes; b.mortesQueda += a.mortesQueda;
            b.curas += a.curas; b.recargas += a.recargas; b.trocas += a.trocas; b.andou += a.andou; b.rodadas++;
        }

        string Texto(Conta c, bool rodada)
        {
            string compra = rodada ? (comprasRodada.Count > 0 ? string.Join(", ", comprasRodada) : "nada") : $"{c.compras} itens";
            return $"comprou {compra}{(c.compraFalhou > 0 ? $" · {c.compraFalhou} COMPRAS FALHARAM" : "")}{(rodada ? $" · armas {armaRodada}" : "")} · andou {c.andou:0} m · {c.tiros} tiros, {c.acertos} acertos em inimigo ({(c.tiros > 0 ? 100f * c.acertos / c.tiros : 0f):0}%), {c.abates} abates" +
                (rodada ? (c.primeiraVista >= 0f ? $" · viu inimigo pela 1ª vez com {c.primeiraVista:0.0} s" : " · não viu inimigo") : "") +
                $" · recargas {c.recargas} · trocas de arma {c.trocas} · curas {c.curas} · presos {c.presos} · câmera na parede {c.camParede} · sem arma {c.semArma}" +
                (rodada ? (morteRodada != null ? $" · morreu: {morteRodada}" : (me && me.alive ? " · vivo no fim" : " · morto")) : $" · mortes {c.mortes} (de queda {c.mortesQueda})");
        }

        void FimRodada(int r)
        {
            if (P) rod.tiros = P.ShotCount - shot0;
            string t = $"piloto rodada {r}: " + Texto(rod, true);
            Log("   " + t);
            fins.Add(t);
            Soma(rod, tot);
            rod = new Conta();
        }

        public void Resumo()
        {
            if (faseAnt == "live") { Log("   piloto: o teste parou no meio da rodada " + rodadaAnt + ", ela entra até aqui"); FimRodada(rodadaAnt); faseAnt = "fim"; }
            else if (faseAnt == "post") { tot.mortes += rod.mortes; tot.mortesQueda += rod.mortesQueda; tot.camParede += rod.camParede; rod = new Conta(); faseAnt = "fim"; }
            Log($"piloto somando {tot.rodadas} rodadas: " + Texto(tot, false));
        }
    }
}
