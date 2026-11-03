// ELENCO POR CATEGORIA (rodada 52, 01/11/2026). Pedido: personagens distribuídos em jogáveis, NPCs civis, policiais, eletricistas e NPCs de
// funções específicas, cada um com a sua função, sem personagem exclusivo de uma categoria aparecendo em função incompatível; os NPCs do
// jogo 171 (x171_*) saíram. Fonte única: assets/skins.json (tools/unity/export_data.mjs -> fb_data.json) — nenhum id de personagem no código:
//   "cats" = onde o personagem pode aparecer, "func" = o que ele faz lá.
//  • jogavel     — menu PERSONAGEM e bots dos modos de combate (role = false no skins.json)
//  • civil       — pedestres do rolê/modos, motoristas e caronas do trânsito, abordados, figurantes (açaí, racha). func pedestre/morador/figura_de_rua
//  • policia     — func pm_viatura (patrulha, perseguição, abordagem), guarda_municipal, choque, bope. Nunca civil nem motorista comum
//  • eletricista — só no evento de manutenção elétrica (FBEletricista): mao_de_obra sobe a escada, fiscal acompanha/segura
//  • funcao      — outras profissões: entregador (motoboy/racha), pedreiro (obra), gari... Nunca pedestre comum
// Um jogável pode ser também civil (os moradores andam pela rua) ou policial (fardados); policia/eletricista/funcao são exclusivas.
// Trocar/acrescentar personagem = uma linha no skins.json (id, sexo, cats, func) + o .glb/prefab. Auditoria: FBElenco.Audit() (log ao carregar).
// skins.json antigo sem "cats": role → civil; time 0 → jogável + polícia (pm_viatura); senão jogável + civil.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public static class FBElenco
    {
        public const string JOGAVEL = "jogavel", CIVIL = "civil", POLICIA = "policia", ELETRICISTA = "eletricista", FUNCAO = "funcao";
        public static readonly string[] CATS = { JOGAVEL, CIVIL, POLICIA, ELETRICISTA, FUNCAO };
        static readonly string[] EXCLUSIVE = { POLICIA, ELETRICISTA, FUNCAO };   // não somam com civil nem entre si

        public static string[] CatsFrom(List<object> l, bool role, int team)
        {
            var c = (l ?? new List<object>()).Select(o => o as string).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToArray();
            if (c.Length > 0) return c;
            return role ? new[] { CIVIL } : team == 0 ? new[] { JOGAVEL, POLICIA } : new[] { JOGAVEL, CIVIL };
        }
        public static SkinDef Of(string id) => string.IsNullOrEmpty(id) ? null : GameData.Skins.Find(s => s.id == id);
        public static bool In(SkinDef s, string cat) => s != null && s.cats != null && System.Array.IndexOf(s.cats, cat) >= 0;
        public static bool In(string id, string cat) => In(Of(id), cat);
        public static string FuncOf(string id) => Of(id)?.func;
        static bool Ready(SkinDef s) => FBRegistry.I && FBRegistry.I.HasChar(s.id);
        /// <summary>ids registrados (com prefab) da categoria; func = só os desta função</summary>
        public static List<string> Ids(string cat, string func = null, string except = null) =>
            GameData.Skins.Where(s => In(s, cat) && (func == null || s.func == func) && s.id != except && Ready(s)).Select(s => s.id).ToList();

        public static string Summary() => string.Join(" · ", CATS.Select(c => $"{c} {Ids(c).Count}"));
        /// <summary>conflitos de categoria/registro (vazio = elenco consistente)</summary>
        public static List<string> Audit()
        {
            var bad = new List<string>();
            foreach (var s in GameData.Skins)
            {
                if (s.cats == null || s.cats.Length == 0) { bad.Add($"{s.id}: sem categoria"); continue; }
                foreach (var c in s.cats) if (System.Array.IndexOf(CATS, c) < 0) bad.Add($"{s.id}: categoria desconhecida '{c}'");
                var ex = s.cats.Where(c => System.Array.IndexOf(EXCLUSIVE, c) >= 0).ToList();
                if (ex.Count > 1 || ex.Count == 1 && In(s, CIVIL)) bad.Add($"{s.id}: {string.Join("+", s.cats)} (categoria exclusiva somada)");
                if (In(s, JOGAVEL) == s.role) bad.Add($"{s.id}: role={s.role} não bate com {(In(s, JOGAVEL) ? "jogável" : "NPC")}");
                if (ex.Count == 1 && string.IsNullOrEmpty(s.func)) bad.Add($"{s.id}: {ex[0]} sem função");
                if (s.id.StartsWith("x171_")) bad.Add($"{s.id}: personagem do 171 no skins.json");
            }
            if (FBRegistry.I) foreach (var id in FBRegistry.I.charIds) if (Of(id) == null) bad.Add($"{id}: no registro sem linha no skins.json");
            return bad;
        }
        /// <summary>autoteste: quem está em cena fazendo o quê × a categoria de cada um. "incompatíveis 0" = nenhum personagem em função errada</summary>
        public static string SceneCheck()
        {
            var bad = new List<string>(); var n = new Dictionary<string, int>();
            void Need(FBCharacter c, string papel, System.Func<SkinDef, bool> ok)
            {
                if (!c || string.IsNullOrEmpty(c.skinId)) return; n[papel] = n.TryGetValue(papel, out var k) ? k + 1 : 1;
                var s = Of(c.skinId); if (s == null || !ok(s)) bad.Add($"{c.skinId}={papel}({(s == null ? "sem linha" : string.Join("+", s.cats) + "/" + s.func)})");
            }
            foreach (var c in FBRole.npcs) Need(c, "pedestre", s => In(s, CIVIL));
            foreach (var t in FBTransito.List)
            {
                bool viatura = t.car && FBSiren.IsPolice(t.car);
                Need(t.actor, viatura ? "viatura" : "motorista", s => viatura ? In(s, POLICIA) && s.func == "pm_viatura" : In(s, CIVIL));
                Need(t.garupa, "garupa", s => In(s, CIVIL));
            }
            if (FBBaculejo.Frisker != null) Need(FBBaculejo.Frisker.c, "PM abordagem", s => In(s, POLICIA));
            if (FBBaculejo.Cover != null) Need(FBBaculejo.Cover.c, "PM abordagem", s => In(s, POLICIA));
            if (FBBaculejo.Frisker?.c && FBBaculejo.Cover?.c && FBBaculejo.Frisker.c.skinId == FBBaculejo.Cover.c.skinId) bad.Add($"{FBBaculejo.Cover.c.skinId}=os dois PMs da abordagem");
            foreach (var a in FBBaculejo.Suspects) Need(a.c, "abordado", s => In(s, CIVIL));
            Need(FBEletricista.Worker, "eletricista", s => In(s, ELETRICISTA)); Need(FBEletricista.Helper, "eletricista ajudante", s => In(s, ELETRICISTA));
            foreach (var c in FBObra.Workers) Need(c, "pedreiro", s => In(s, FUNCAO) && s.func == "pedreiro");
            foreach (var c in FBMatch.Bots) Need(c, "bot", s => In(s, JOGAVEL));
            return $"{string.Join(" · ", n.Select(kv => kv.Key + " " + kv.Value))} · incompatíveis {bad.Count}{(bad.Count > 0 ? ": " + string.Join(", ", bad.Take(10)) : "")}";
        }
        static bool logged;
        public static void LogOnce()
        {
            if (logged || GameData.Skins.Count == 0 || !FBRegistry.I) return; logged = true;
            var bad = Audit(); int pend = GameData.Skins.Count(s => !Ready(s));
            Debug.Log($"[FreeBala][elenco] {Summary()} · viatura {Ids(POLICIA, "pm_viatura").Count} · sem modelo ainda {pend} · problemas {bad.Count}{(bad.Count > 0 ? ": " + string.Join(" | ", bad.Take(12)) : "")}");
        }
    }
}
