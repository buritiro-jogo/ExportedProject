// Rotina dos PEDESTRES no estilo do 171 (rodada 43) — por cima do cérebro do rolê (FBRoleNpc), sem trocar o que já funciona.
// Do 171 (Characters/Blueprints/Logic/AI: Enum_AcaoIdlePedestre, Enum_PedestreReacoes, Enum_Conversation_State/AssuntoConversa; animações
// Characters/Animations/{Idle,Movement/Walk,Action/Scared,Action/Bump,Idle/Conversation}; falas FMOD Dublagens/Personagens/*):
//  • ATIVIDADE: parte dos pedestres anda mexendo no celular, falando ao telefone, fumando, bebendo ou olhando em volta, e quando para
//    continua a mesma coisa (parado no celular / fumando / olhando o relógio / braços cruzados). Quem fala ao telefone solta frases de celular.
//  • PAUSA: ao chegar num ponto, às vezes fica parado um tempo fazendo a ação (como o idle de pedestre do 171), em vez de seguir direto.
//  • CONVERSA: dupla/roda com quem fala e quem escuta (Convo_02 / Convo_11) e as falas na ordem do 171: comentário → resposta → fim,
//    no mesmo assunto (algo / alguém / algum lugar), cada um com a SUA voz (Homem1–3, Mulher1–2 — fixa por NPC).
//  • REAÇÕES com fala: carro quase atropelando (xinga, às vezes cai de susto e levanta), esbarrão do jogador (tropeço pela direção +
//    reclamação), jogador armado por perto (comenta), na mira (mãos para cima + "calma"), tiro perto (corre + "chama a polícia"),
//    batida de carro (olha e comenta), grau de moto (comenta), tombo de moto (ri / pergunta se machucou).
// Material do 171 (clipes X171_*, falas Resources/Audio/171/npc): uso condicionado à autorização da Betagames. Desligar: -fbsemrotina171
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    // ---- falas por situação e voz (tools/re/npcvoz171.py -> Resources/Audio/171/npc/vozes.json)
    public static class FBNpcVoz
    {
        class Line { public string c; public float d; }
        static Dictionary<string, Dictionary<string, List<Line>>> cats; static bool tried;
        static readonly Dictionary<int, float> nextT = new(); static float globalT;
        static readonly List<(float t, FBCharacter c, string cat, float vol, float refD)> queue = new();
        public static int Said; public static string LastLine;
        public static int Id(Object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);   // identidade do objeto (GetInstanceID é obsoleto no Unity 6.6)
        static void Load()
        {
            if (tried) return; tried = true; var ta = Resources.Load<TextAsset>("Audio/171/npc/vozes"); if (!ta) return;
            cats = new();
            foreach (var kv in Json.O(Json.Parse(ta.text), "cats") ?? new Dictionary<string, object>())
            {
                var byV = new Dictionary<string, List<Line>>();
                foreach (var v in kv.Value as Dictionary<string, object> ?? new()) byV[v.Key] = (v.Value as List<object> ?? new()).Select(o => new Line { c = Json.S(o, "c"), d = Json.F(o, "d", 1.5f) }).Where(l => l.c != null).ToList();
                cats[kv.Key] = byV;
            }
        }
        public static bool Ready { get { Load(); return cats != null && cats.Count > 0; } }
        public static string VozOf(FBCharacter c)
        {
            bool f = c.npcSet != null ? c.npcSet.sex == "f" : c.IsFemale; uint h = (uint)((c.skinId ?? "").GetHashCode() * 31 + FBNpcVoz.Id(c));
            return f ? (h % 2 == 0 ? "Mulher1" : "Mulher2") : "Homem" + (1 + h % 3);
        }
        // fala uma frase da situação 'cat' na voz do NPC. gap = intervalo mínimo deste NPC; global = intervalo mínimo entre reações de NPCs
        // diferentes (sem coro). Devolve a duração (0 = não falou).
        public static float Say(FBCharacter c, string cat, float vol = 1, float refD = 4, float gap = 8, float global = 1.2f, float delay = 0)
        {
            if (!c || !c.alive || !Ready || !cats.TryGetValue(cat, out var byV)) return 0;
            if (delay > 0) { queue.Add((Time.time + delay, c, cat, vol, refD)); return .1f; }
            int id = FBNpcVoz.Id(c); if (nextT.TryGetValue(id, out var nt) && Time.time < nt) return 0; if (global > 0 && Time.time < globalT) return 0;
            var voz = VozOf(c); if (!byV.TryGetValue(voz, out var l) || l.Count == 0) { var sx = voz.StartsWith("M") ? "Mulher" : "Homem"; l = byV.Where(kv => kv.Key.StartsWith(sx) && kv.Value.Count > 0).Select(kv => kv.Value).FirstOrDefault(); }
            if (l == null || l.Count == 0) return 0;
            var ln = l[Random.Range(0, l.Count)]; var clip = Resources.Load<AudioClip>("Audio/171/npc/" + ln.c); if (!clip) return 0;
            var head = c.Pos + Vector3.up * 1.6f * c.ScaleK; FBAudio.PlayAt(clip, head, vol, refD, .03f, 0, 1.1f, FBAudio.Cat.Voice);
            nextT[id] = Time.time + ln.d + gap; if (global > 0) globalT = Time.time + global; Said++; LastLine = cat + ":" + ln.c; return ln.d;
        }
        public static void Tick()
        {
            for (int i = queue.Count - 1; i >= 0; i--) if (Time.time >= queue[i].t) { var q = queue[i]; queue.RemoveAt(i); Say(q.c, q.cat, q.vol, q.refD); }
        }
        public static void Reset() { nextT.Clear(); queue.Clear(); globalT = 0; }
    }

    public static class FBNpcRotina
    {
        public static bool On = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbsemrotina171") < 0;
        static bool Has(string n) => FBRegistry.I && FBRegistry.I.Clip(n);
        public static int Pauses, Reacts, Convs;                                                   // contadores (autoteste)
        // atividade: andar do 171 (velocidade natural do clipe, m/s) + o que faz quando para
        public class Act { public string id, walk; public float rs; public string[] idle; public bool fem; public string voice; }
        static readonly Act[] ACT =
        {
            new() { id = "celular",  walk = "X171_Walking_Texting_looped", rs = 1.11f, idle = new[] { "X171_texting_looped", "X171_051_Mocap101_SL_STATIC_B_WriteMobile_UE4" }, fem = true },
            new() { id = "telefone", walk = "X171_023_Mocap101_SL_WALK_B_TalkMobile_UE4", rs = .76f, idle = new[] { "X171_051_Mocap101_SL_STATIC_B_WriteMobile_UE4" }, fem = true, voice = "celular" },
            new() { id = "procura",  walk = "X171_022_Mocap101_SL_WALK_B_SearchMobile_UE4", rs = .54f, idle = new[] { "X171_texting_looped" }, fem = true },
            new() { id = "cigarro",  walk = "X171_Walk_Fumando_looped", rs = .96f, idle = new[] { "X171_fumando_looped", "X171_055_Mocap101_SL_STATIC_B_Smoke_UE41" } },
            new() { id = "cigarro2", walk = "X171_037_Mocap101_SL_WALK_B_Smoke_UE4", rs = .95f, idle = new[] { "X171_fumando_looped", "X171_055_Mocap101_SL_STATIC_B_Smoke_UE41" } },
            new() { id = "bebida",   walk = "X171_Bebendo_looped", rs = .96f, idle = new[] { "X171_004_Mocap101_SL_IDLE_B_Armscross_UE4" } },
            new() { id = "olhando",  walk = "X171_Walk_06_Look_Around_Loop_IP", rs = 1.22f, idle = new[] { "X171_053_Mocap101_SL_STATIC_B_Watch1_UE4", "X171_004_Mocap101_SL_IDLE_B_Armscross_UE4" }, fem = true },
        };
        static readonly string[] PAUSE = { "X171_053_Mocap101_SL_STATIC_B_Watch1_UE4", "X171_054_Mocap101_SL_STATIC_B_Watch2_UE4", "X171_051_Mocap101_SL_STATIC_B_WriteMobile_UE4", "X171_texting_looped", "X171_004_Mocap101_SL_IDLE_B_Armscross_UE4" };
        static readonly string[] PAUSE_M = { "X171_fumando_looped", "X171_055_Mocap101_SL_STATIC_B_Smoke_UE41" };
        public const string TALK = "X171_Convo_02_Up_Beat_Loop", TALK2 = "X171_Convo_01_Low_Key_Loop", LISTEN = "X171_Convo_11_Listening_Loop";
        static readonly Dictionary<int, Act> actOf = new();
        public static Act ActOf(FBCharacter c) => c && actOf.TryGetValue(FBNpcVoz.Id(c), out var a) ? a : null;

        // ao montar o conjunto do NPC (FBNpcAnims.Apply): ~35 % ganha uma atividade (o andar dela no lugar do andar comum)
        public static void Decorate(FBCharacter.NpcSet set, FBCharacter c)
        {
            if (!On || set == null || !c) return; actOf.Remove(FBNpcVoz.Id(c));
            if (Random.value > .35f) return;
            bool f = set.sex == "f"; var pool = ACT.Where(a => (a.fem || !f) && Has(a.walk) && a.idle.Any(Has)).ToList(); if (pool.Count == 0) return;
            var act = pool[Random.Range(0, pool.Count)]; actOf[FBNpcVoz.Id(c)] = act;
            set.walk = act.walk; set.ovr["WalkFwdLoop"] = act.walk; set.walkRs = act.rs; set.activity = true;
        }
        // o que cada FUNÇÃO faz quando para (mapeado das classes do 171, Characters/Blueprints/Logic/AI/NPCDataClass/AC_ClassePersonagem_*): gari
        // varre, pedreiro mede/olha a planta, eletricista olha o poste, comerciante e serviço de braços cruzados/celular, entregador (motoboy) no
        // celular/relógio, figura de rua de braços cruzados/olhando o relógio. r52: chave = func do personagem no elenco (FBElenco), não mais a classe do 171
        static readonly Dictionary<string, string[]> CLS_IDLE = new()
        {
            ["gari"] = new[] { "X171_ObraDeRua_Varrendo" }, ["pedreiro"] = new[] { "X171_ObraDeRua_Medindo", "X171_ObraDeRua_VendoPlanos", "X171_004_Mocap101_SL_IDLE_B_Armscross_UE4" },
            ["eletricista"] = new[] { "X171_ObraDeRua_VendoPlanos", "X171_053_Mocap101_SL_STATIC_B_Watch1_UE4" }, ["comerciante"] = new[] { "X171_004_Mocap101_SL_IDLE_B_Armscross_UE4", "X171_051_Mocap101_SL_STATIC_B_WriteMobile_UE4" },
            ["servico"] = new[] { "X171_004_Mocap101_SL_IDLE_B_Armscross_UE4", "X171_053_Mocap101_SL_STATIC_B_Watch1_UE4" }, ["entregador"] = new[] { "X171_051_Mocap101_SL_STATIC_B_WriteMobile_UE4", "X171_053_Mocap101_SL_STATIC_B_Watch1_UE4" },
            ["figura_de_rua"] = new[] { "X171_004_Mocap101_SL_IDLE_B_Armscross_UE4", "X171_053_Mocap101_SL_STATIC_B_Watch1_UE4" },
        };
        public static string ClsOf(FBCharacter c) { var s = c ? FBElenco.Of(c.skinId) : null; return s == null ? null : FBElenco.In(s, FBElenco.ELETRICISTA) ? "eletricista" : s.func; }
        // chegou num ponto: às vezes para e faz a ação (idle de pedestre do 171). Devolve o tempo parado (0 = segue)
        public static float Pause(FBCharacter c)
        {
            if (!On || !c || c.dance != null || Random.value > .38f) return 0;
            var a = ActOf(c); var l = (a != null ? a.idle : PAUSE.Concat(c.npcSet?.sex == "f" ? new string[0] : PAUSE_M)).Where(Has).ToList();
            var cls = ClsOf(c); if (cls != null && CLS_IDLE.TryGetValue(cls, out var ci) && Random.value < .7f) { var l2 = ci.Where(Has).ToList(); if (l2.Count > 0) l = l2; }
            if (l.Count == 0) return 0;
            var clip = l[Random.Range(0, l.Count)]; if (!c.PlayDance(clip)) return 0;
            float T = Random.Range(6f, 16f); c.dance.len = T + 1; Pauses++;
            if (a?.voice != null && Random.value < .6f) FBNpcVoz.Say(c, a.voice, .7f, 2.6f, 6, 0, Random.Range(.5f, 2.5f));
            return T;
        }
        // andando com atividade de telefone: frase de celular de vez em quando
        public static void WalkTick(FBCharacter c, float dt)
        {
            var a = ActOf(c); if (a?.voice == null || Random.value > dt / 14f) return;
            FBNpcVoz.Say(c, a.voice, .65f, 2.4f, 10, 0);
        }

        // ---- conversa (Enum_Conversation_State do 171): comentário -> resposta -> finalização no mesmo assunto, fala quem é a vez
        class Roda { public List<FBCharacter> m = new(); public int spk, step; public string topic; public float nextT, seen; }
        static readonly Dictionary<long, Roda> rodas = new();
        static long Key(Vector3 p) => ((long)Mathf.RoundToInt(p.x * 4) << 32) ^ (uint)Mathf.RoundToInt(p.z * 4);
        static readonly string[] TOPICS = { "algo", "alguem", "lugar" }, STEPS = { "com", "resp", "fim" };
        public static void TalkTick(FBCharacter c, Vector3 center, float dt)
        {
            if (!On || !c) return; var k = Key(center);
            if (!rodas.TryGetValue(k, out var R)) { R = new Roda { nextT = Time.time + Random.Range(.8f, 2.5f), topic = TOPICS[Random.Range(0, 3)], spk = Random.Range(0, 2) }; rodas[k] = R; Convs++; }
            if (!R.m.Contains(c)) R.m.Add(c); R.m.RemoveAll(x => !x || !x.alive || !x.talking); R.seen = Time.time;
            int me = R.m.IndexOf(c); if (me != 0) return;                                           // o primeiro da roda conduz
            if (Time.time < R.nextT || R.m.Count < 2) return;
            var s = R.m[R.spk % R.m.Count];
            float d = FBNpcVoz.Say(s, $"conv_{STEPS[R.step]}_{R.topic}", .6f, 2.2f, 0, 0);
            foreach (var x in R.m) Gesture(x, x == s);
            R.nextT = Time.time + Mathf.Max(1.2f, d) + Random.Range(.35f, .9f);
            if (++R.step >= 3) { R.step = 0; R.topic = TOPICS[Random.Range(0, 3)]; R.nextT += Random.Range(3f, 8f); R.spk++; }   // novo assunto, começa o outro
            else R.spk = R.step == 1 ? R.spk + 1 : R.spk - 1;                                        // resposta = o outro; fim = quem comentou
            if (rodas.Count > 64) foreach (var kk in rodas.Where(kv => Time.time - kv.Value.seen > 5).Select(kv => kv.Key).ToList()) rodas.Remove(kk);
        }
        // quem fala gesticula (Convo_02/01), quem escuta fica no "ouvindo" (Convo_11)
        static void Gesture(FBCharacter c, bool speaking)
        {
            var want = speaking ? (Random.value < .6f ? TALK : TALK2) : LISTEN; if (!Has(want)) return;
            if (c.dance != null && (c.dance.clip == want || speaking && (c.dance.clip == TALK || c.dance.clip == TALK2))) return;
            if (c.dance != null && c.dance.clip.Contains("Handshake")) return;
            if (c.PlayDance(want)) c.dance.len = 1e6f;
        }

        // ---- reações com clipe do 171 (o FBRoleNpc fica parado enquanto o clipe toca)
        // clipes que andam com o corpo (cair de susto, recuar assustado: o quadril termina 1,3–1,7 m longe): o FBRoleNpc leva a raiz até o
        // corpo e corta o clipe no fim (senão o corpo voltava de uma vez). Devolve o tempo até esse corte (um pouco antes do fim do clipe).
        public static bool Moves(string clip) => clip != null && (clip.Contains("SCR_Stand_") || clip.Contains("Tropeco"));
        public static float OneShot(FBCharacter c, string clip, float maxLen = 99)
        {
            if (!Has(clip) || !c.PlayDance(clip)) return 0; float L = Mathf.Min(maxLen, FBRegistry.I.Clip(clip).length); c.dance.len = L; Reacts++; return Moves(clip) ? L - .3f : L;
        }
        // lado de onde veio (para o clipe de tropeço): em relação a para onde o NPC olha
        public static string Side(FBCharacter c, Vector3 from)
        {
            var to = (from - c.Pos).Flat(); var f = Quaternion.Euler(0, c.Yaw, 0) * Vector3.forward; float a = Vector3.SignedAngle(f, to, Vector3.up);
            return Mathf.Abs(a) < 50 ? "Front" : Mathf.Abs(a) > 130 ? "Back" : a > 0 ? "Right" : "Left";
        }
        public static float Bump(FBCharacter c, Vector3 from)
        {
            var side = Side(c, from); if (side == "Back") side = Random.value < .5f ? "Left" : "Right";       // o pacote não tem "de trás" leve
            float L = OneShot(c, $"X171_BumpReaction_HitFrom{side}_LightStandingStumble");
            FBNpcVoz.Say(c, "veiculo", .9f, 3.5f, 6, .8f, .35f); return L;
        }
        public static float NearMiss(FBCharacter c, Vector3 carP, float passDist)
        {
            float L = 0;
            if (passDist < 1.6f && Random.value < .45f) { var side = Side(c, carP); L = OneShot(c, $"X171_SCR_Stand_Spooked_Fall_{(side == "Left" ? "Right" : side == "Right" ? "Left" : "Back")}_Stand"); }   // recua para longe do carro
            else if (Random.value < .5f) { c.Yaw = Quaternion.LookRotation((carP - c.Pos).Flat()).eulerAngles.y; L = OneShot(c, Random.value < .5f ? "X171_InteracaoPersonagem_Hostil1" : "X171_InteracaoPersonagem_Hostil2"); }
            FBNpcVoz.Say(c, "veiculo", 1, 4.5f, 6, .6f, L > 0 ? .25f : 0); return L;
        }
        // eventos do mundo: quem está perto olha e comenta (acidente, tombo de moto, grau, explosão)
        public static void Event(string cat, Vector3 p, float range = 32, int talkers = 1)
        {
            if (!On || FBRole.npcs == null) return;
            var near = FBRole.npcs.Where(n => n && n.alive && !n.vehiclePose && (n.Pos - p).sqrMagnitude < range * range).OrderBy(n => (n.Pos - p).sqrMagnitude).Take(6).ToList();
            int said = 0;
            foreach (var n in near)
            {
                var r = n.GetComponent<FBRoleNpc>(); if (!r || !r.Free) continue;
                r.LookAt(p, Random.Range(2.5f, 4.5f));
                if (said < talkers && FBNpcVoz.Say(n, cat, 1, 4.5f, 6, .4f, Random.Range(.25f, .9f)) > 0) said++;
            }
        }
        static float grauT;
        public static void Grau(FBCar car)
        {
            if (!On || Time.time < grauT) return; grauT = Time.time + Random.Range(4f, 8f);
            var p = car.transform.position; var n = FBRole.npcs?.Where(x => x && x.alive && !x.vehiclePose && (x.Pos - p).sqrMagnitude < 22 * 22).OrderBy(x => Random.value).FirstOrDefault(); if (!n) return;
            var r = n.GetComponent<FBRoleNpc>(); if (r && r.Free) r.LookAt(p, 2.5f); FBNpcVoz.Say(n, "grau", 1, 4.5f, 10, 1, .2f);
        }
    }
}
