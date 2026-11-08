// Palco do lobby: diretor de animação (porte do lobbyIdle/lobbyLook do main.js). Base = parado longo do IE (ANIM-Rifle_Idle de 23 s);
// a cada 4,5–9 s sorteia uma ação pela CLASSE da arma, sem repetir a última: inspecionar, recarregar, guardar e sacar (clipes do IE),
// olhar em volta, olhar para a arma, olhar para a câmera, ou um emote curto de pose. Cabeça/pescoço vão por cima da animação (LateUpdate).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public class FBLobbyDirector : MonoBehaviour
    {
        static readonly (string a, float w)[] LONG = { ("inspect", 4), ("check", 3), ("holster", 2.4f), ("reload", 1.6f), ("look", 2.2f), ("cam", 1.8f), ("emote", 1.1f) };
        static readonly (string a, float w)[] PISTOL = { ("check", 3.2f), ("holster", 3), ("look", 2.4f), ("cam", 2.2f), ("emote", 1.2f) };
        static readonly (string a, float w)[] BARE = { ("look", 3), ("cam", 2.5f), ("emote", 2.2f) };
        public bool home = true; public Camera cam;
        // r61: pose de base do palco (vaga "Rifle Idle"); -fbpose <clipe> troca sem recompilar; Quiet = sem ações sorteadas (laboratório -fbposelab)
        public static string Pose = "Rifle Idle"; public static bool Quiet;   // r61: -fbposelab comparou 11 clipes; só este segura a arma limpo nos dois sexos (ANIM-Rifle_Idle nem está no registro)
        static string PoseArg { get { var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbpose"); return i >= 0 && i + 1 < a.Length ? a[i + 1] : null; } }
        FBCharacter ch; Animator an; Transform head, neck;
        float t, k, len, at; string act, last; bool drew;

        void Start()
        {
            ch = GetComponent<FBCharacter>(); an = GetComponent<Animator>(); t = 2.5f + Random.value * 3;
            if (an && an.isHuman) { head = an.GetBoneTransform(HumanBodyBones.Head); neck = an.GetBoneTransform(HumanBodyBones.Neck); }
            if (!cam) cam = Camera.main;
            if (ch && ch.weapon != null) ch.SetOverride("Rifle Idle", PoseArg ?? Pose);                 // parado longo do IE como base
        }

        // [parceiro] v1 (thread de física): tempo de CPU do Update/LateUpdate medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_Update = FBTempo.Id("FBLobbyDirector.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            if (!ch) return; float dt = Time.deltaTime;
            if (act != null)
            {
                k += dt;
                if (act == "holster" && !drew && k >= at) { drew = true; ch.DrawAnim(); }
                if (k >= len) { last = act; act = null; t = 4.5f + Random.value * 4.5f; }
                return;
            }
            if ((t -= dt) > 0 || !home || Quiet) return;
            var set = ch.weapon == null ? BARE : ch.weapon.IsPistol ? PISTOL : LONG;
            var acts = new List<(string a, float w)>(); float sum = 0; foreach (var x in set) if (x.a != last) { acts.Add(x); sum += x.w; }
            float r = Random.value * sum; string pick = acts[0].a; foreach (var x in acts) { r -= x.w; if (r <= 0) { pick = x.a; break; } }
            if (pick == "emote")
            {
                var e = FBEmotes.RandomShort(ch.IsFemale);
                if (e != null && ch.PlayEmote(e)) { last = "emote"; t = 6 + Random.value * 4; return; }
                pick = "look";
            }
            act = pick; k = 0; drew = false;
            if (act == "inspect" || act == "reload") { ch.Trigger(act); len = act == "inspect" ? 3.2f : 2.6f; }
            else if (act == "holster") { var rel = ch.HolsterAnim(); if (rel <= 0) { act = null; t = 1; return; } at = rel + .35f; len = at + 1.2f; }
            else len = act == "check" ? 2.8f : act == "cam" ? 2.4f : 2.6f;
        }

        static readonly int __fbt_LateUpdate = FBTempo.Id("FBLobbyDirector.LateUpdate");   // [parceiro] FBTempo (ver Update)
        void LateUpdate()
        {
            if (!FBTempo.On) { LateUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
        }
        void LateUpdate__fbt()
        {
            if (act == null || !head) return;
            float u = k / len, e = Mathf.Sin(Mathf.Min(1, u) * Mathf.PI);
            if (act == "look")
            {
                float a = Mathf.Sin(u * Mathf.PI * 2) * e * .75f * Mathf.Rad2Deg;
                if (neck) neck.Rotate(Vector3.up, a * .45f, Space.World);
                head.Rotate(Vector3.up, a * .55f, Space.World); head.Rotate(transform.right, -e * .08f * Mathf.Rad2Deg, Space.World);
            }
            else if (act == "check" && ch.weaponGO) Turn(ch.weaponGO.transform.position, .55f * e);
            else if (act == "cam" && cam) Turn(cam.transform.position, .7f * e);
        }

        void Turn(Vector3 target, float w)                                          // gira a cabeça (em mundo) parte do caminho até o alvo
        {
            var fw = transform.forward; fw.y = 0; var to = (target - head.position).normalized;
            var q = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(fw.normalized, to), w);
            head.rotation = q * head.rotation;
        }
    }

    // catálogo de emotes (assets/emotes.json -> fb_data.emotes): categoria e duração, para o lobby, a roda de emotes e a vitória
    public static class FBEmotes
    {
        public class Def { public string id, name, cat; public float len; public bool loop; }
        // r47 (pedido 30/10: "remova todos os emotes do Free Fire ... interface, listas, referências e execução"): saíram os 9 do web
        // (EMOTE_PICKS) e o catálogo FF inteiro (199 clipes FF_*). Ficam as danças/poses do pacote NPC. NÃO são emotes e continuam:
        // FF_SYS_* (caído, cura, pulo de janela, paraquedas, veículo — mecânica) e FF_REB_* (abordagem/rendido do REB, só têm o prefixo).
        public static bool IsFF(string id) => id != null && id.StartsWith("FF_") && !id.StartsWith("FF_SYS_") && !id.StartsWith("FF_REB_");
        // padrão dos 8 equipados: danças sem sexo marcado no nome (servem em homem e mulher)
        public static readonly string[] DEFAULT = { "NPC_Dougie", "NPC_Boombayah_Dance", "NPC_Baam_Dance", "NPC_10_Dance_Like_Sidestep", "NPC_Air_Dance", "NPC_Down_01", "NPC_Aerobic_Dance", "NPC_Birdcage" };
        static List<Def> all;
        public static List<Def> All
        {
            get
            {
                if (all != null) return all; all = new List<Def>(); GameData.Load();
                if (GameData.Emotes != null) foreach (var o in GameData.Emotes)
                        if ((Json.S(o, "id") ?? "").StartsWith("NPC_"))   // r47: só as danças/poses NPC (FF fora)
                        all.Add(new Def { id = Json.S(o, "id"), name = Json.S(o, "name", Json.S(o, "id")), cat = Json.S(o, "cat", "fun"), len = Json.F(o, "len", 9), loop = Json.B(o, "loop") });
                return all;
            }
        }
        // SEXO DO CLIPE (rodada 42, usuário: "ao selecionar um personagem, executamos uma animação feminina em todos, inclusive nos
        // masculinos"): o confirmar caía no 1.º "pose" do catálogo = NPC_Posing_F. Os clipes do pacote NPC marcam o sexo no nome (_F/_M,
        // Female, danças sensuais/Sophie/Tiffany/catwalk); os do Free Fire são unissex. Sentar/levantar fica fora do palco (não há cadeira).
        static readonly System.Text.RegularExpressions.Regex FEM = new(@"(_F\d?$|_F\d?_|Female|Sensual|Sophie|Tiffany|Twirl_Hair|Viewing_Mirror|Catwalk|Girl|Open_Curtains)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        static readonly System.Text.RegularExpressions.Regex MASC = new(@"(_M\d?$|_M\d?_)");
        static readonly System.Text.RegularExpressions.Regex NOSTAGE = new(@"Sit|Stand_to|Saloon|Fast_Forward|Against_Wall|Break_Door", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        public static bool FitsSex(string id, bool female) => id != null && (female ? !MASC.IsMatch(id) : !FEM.IsMatch(id));
        static bool Ok(string id) => FBRegistry.I && FBRegistry.I.Clip(id);
        public static List<string> Victory => (GameData.Victory ?? new List<object>()).Select(o => o as string).Where(id => !IsFF(id) && Ok(id)).ToList();   // a lista do fb_data era só FF: vazia cai nas danças
        public static string RandomVictory(bool? female = null)
        {
            var l = Victory.Where(id => female == null || FitsSex(id, female.Value)).ToList();
            if (l.Count == 0) l = All.Where(e => e.cat == "dance" && Ok(e.id) && (female == null || FitsSex(e.id, female.Value))).Select(e => e.id).ToList();
            return l.Count > 0 ? l[Random.Range(0, l.Count)] : null;
        }
        /// <summary>Pose de confirmar do palco: a do sexo do personagem (Posing/Emote do pacote NPC), senão uma dança de vitória.</summary>
        public static string ConfirmPose(bool female)
        {
            var l = (female ? new[] { "NPC_Posing_F", "NPC_Emote_F" } : new[] { "NPC_Posing_M", "NPC_Emote_M" }).Where(Ok).ToList();
            return l.Count > 0 ? l[Random.Range(0, l.Count)] : RandomVictory(female);
        }
        public static IEnumerable<string> DanceIds() => Victory.Concat(All.Where(e => e.cat == "dance").Select(e => e.id)).Distinct();   // roleDances do web: vitórias + danças
        public static string RandomDance(bool? female = null) { var l = All.Where(e => e.cat == "dance" && Ok(e.id) && (female == null || FitsSex(e.id, female.Value))).Select(e => e.id).ToList(); return l.Count > 0 ? l[Random.Range(0, l.Count)] : null; }
        public static string NameOf(string id) => All.Find(e => e.id == id)?.name ?? id;
        // emotes equipados (até 8, escolhidos no menu EMOTES); padrão = DEFAULT. Slots salvos com FF (versões antigas) são descartados;
        // se não sobrar nenhum, volta ao padrão.
        public static List<string> Equipped()
        {
            var l = string.IsNullOrEmpty(FBSession.emoteSlots) ? new List<string>() : FBSession.emoteSlots.Split('|').Where(id => id.StartsWith("NPC_") && Ok(id)).ToList();
            if (l.Count == 0) l = DEFAULT.Where(Ok).ToList();
            return l.Take(8).ToList();
        }
        // pose de espera desarmado no 4X4 (fase de compra): parado do pacote NPC do sexo do personagem (era o FF_Emote_supercar...)
        public static string WaitPose(bool female) { var id = female ? "NPC_Idle01_F" : "NPC_Idle01_M"; return Ok(id) ? id : null; }
        public static string RandomShort(bool? female = null)
        {
            var l = All.FindAll(e => (e.cat == "pose" || e.cat == "fun") && e.len < 7 && Ok(e.id) && !NOSTAGE.IsMatch(e.id) && (female == null || FitsSex(e.id, female.Value)));
            return l.Count > 0 ? l[Random.Range(0, l.Count)].id : null;
        }
    }
}
