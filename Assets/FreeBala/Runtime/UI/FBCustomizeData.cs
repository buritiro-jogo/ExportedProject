// Dados da tela de personagem: roster (originais dos APKs + NPCs), filtros (sexo, origem, favoritos), busca sem acento, favoritos e
// o visual do guarda-roupa salvo POR personagem (como o sel.looks do web). Tudo em PlayerPrefs; o personagem em uso e o visual dele
// vão para o FBSession (skin/look), que é o que a partida e a rede leem (FBSession.FullSkin = "id~código").
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public static class FBCustomizeData
    {
        public enum Origin { All, Original, Npc, Fav }
        public class Entry { public SkinDef def; public string id, name, key; public bool fem, npc; public int index; }

        public static readonly HashSet<string> BetaChars = new(System.StringComparer.OrdinalIgnoreCase)
        {
            "r_pm_almeida", "r_perseu", "r_silva", "r_caboco", "r_marcin", "r_tutu"
        };
        public static string PrettyName(string id, string fallback = null)
        {
            if (!string.IsNullOrEmpty(fallback) && fallback != id) return fallback.ToUpperInvariant();
            return id switch
            {
                "r_pm_almeida" => "PM ALMEIDA",
                "r_perseu" => "PERSEU",
                "r_silva" => "SILVA",
                "r_caboco" => "CABOCO",
                "r_marcin" => "MARCIN",
                "r_tutu" => "TUTU",
                "r_elaine" => "PM ELAINE",
                "r_mikele" => "MIKELE",
                "r_rany" => "RANY",
                "r_mari" => "MARI",
                "r_faria" => "FARIA",
                "r_vivi" => "VIVI",
                "r_estrela" => "ESTRELA",
                "r_lane" => "LANE",
                "r_rigote" => "RIGOTE",
                "r_giga" => "GIGA",
                "r_miguel" => "MIGUEL",
                "r_pm_silva" => "PM LUIS",
                "r_aka_junin" => "AKA JUNIN",
                "r_arthur" => "ARTHUR",
                "r_white" => "WHITE",
                _ => id != null && id.StartsWith("r_") ? id.Substring(2).Replace("_", " ").ToUpperInvariant() : (fallback ?? id ?? "").ToUpperInvariant()
            };
        }

        static List<Entry> roster;
        /// <summary>Todos os personagens com prefab importado: os do fb_data (inclusive os NPCs do rolê) + os do roster original dos APKs e
        /// pedestres que só existem no registro (Resources/UI/Personagem/roster_extra.json, nome e sexo). Ordem: originais, depois NPCs.</summary>
        public static List<Entry> Roster
        {
            get
            {
                if (roster != null) return roster; GameData.Load(); var all = new List<Entry>(); var seen = new HashSet<string>();
                void Add(SkinDef s)
                {
                    if (s == null || s.id == null || !seen.Add(s.id) || !FBRegistry.I || !FBRegistry.I.HasChar(s.id)) return;
                    string nm = PrettyName(s.id, s.name);
                    all.Add(new Entry { def = s, id = s.id, name = string.IsNullOrEmpty(nm) ? s.id : nm, key = Norm(nm ?? s.id) + " " + Norm(s.id), fem = s.sex == "f", npc = IsNpc(s) });
                }
                foreach (var s in GameData.Skins) Add(s);
                var extra = Resources.Load<TextAsset>("UI/Personagem/roster_extra");
                if (extra) foreach (var o in Json.L(Json.Parse(extra.text), "skins") ?? new List<object>())
                        Add(new SkinDef { id = Json.S(o, "id"), name = Json.S(o, "name"), sex = Json.S(o, "sex", "m"), src = Json.S(o, "src", ""), team = -1 });
                if (FBRegistry.I) foreach (var id in FBRegistry.I.charIds) Add(new SkinDef { id = id, name = id, sex = "m", src = id.StartsWith("r_") ? "" : "npc", team = -1 });
                roster = all.Where(e => !e.npc).Concat(all.Where(e => e.npc)).ToList(); for (int i = 0; i < roster.Count; i++) roster[i].index = i;
                return roster;
            }
        }
        public static void Reset() => roster = null;
        public static Entry Find(string id) => Roster.Find(e => e.id == id);
        // NPC = pacote NPCS do rolê (n2_*) e pedestres (p_*); jogáveis = personagens regionais (r_*)
        static bool IsNpc(SkinDef s) => s.role || s.id.StartsWith("n_") || s.id.StartsWith("n2_") || s.id.StartsWith("p_") || s.src == "npc";

        /// <summary>Minúsculas sem acento (busca "joao" acha "JOÃO").</summary>
        public static string Norm(string t)
        {
            if (string.IsNullOrEmpty(t)) return ""; var d = t.Normalize(System.Text.NormalizationForm.FormD); var sb = new System.Text.StringBuilder(d.Length);
            foreach (var c in d) if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }
        /// <summary>Filtro da grade: sexo ("all", "m", "f"), origem e busca.</summary>
        public static List<Entry> Filter(string sex, Origin origin, string query)
        {
            var q = Norm(query ?? "").Trim(); var favs = Favs;
            return Roster.Where(e => Match(e, sex, origin, q, favs)).ToList();
        }
        public static bool Match(Entry e, string sex, Origin origin, string q, HashSet<string> favs)
        {
            if (sex == "m" && e.fem || sex == "f" && !e.fem) return false;
            if (origin == Origin.Original && e.npc || origin == Origin.Npc && !e.npc || origin == Origin.Fav && !favs.Contains(e.id)) return false;
            return string.IsNullOrEmpty(q) || e.key.Contains(q);
        }

        // ---- favoritos
        static HashSet<string> favs;
        public static HashSet<string> Favs { get { if (favs == null) favs = new HashSet<string>(PlayerPrefs.GetString("fb_favs", "").Split('|').Where(x => x.Length > 0)); return favs; } }
        public static bool IsFav(string id) => Favs.Contains(id);
        public static bool ToggleFav(string id) { bool on = !Favs.Remove(id); if (on) Favs.Add(id); PlayerPrefs.SetString("fb_favs", string.Join("|", Favs)); PlayerPrefs.Save(); return on; }

        // ---- visual salvo por personagem ("id=código;id=código")
        static Dictionary<string, string> looks;
        static Dictionary<string, string> Looks
        {
            get
            {
                if (looks != null) return looks; looks = new Dictionary<string, string>();
                foreach (var e in PlayerPrefs.GetString("fb_looks", "").Split(';')) { int i = e.IndexOf('='); if (i > 0) looks[e.Substring(0, i)] = e.Substring(i + 1); }
                if (!string.IsNullOrEmpty(FBSession.look) && !looks.ContainsKey(FBSession.skin)) looks[FBSession.skin] = FBSession.look;   // migra o visual antigo (um só)
                return looks;
            }
        }
        public static string LookFor(string id) => id != null && Looks.TryGetValue(id, out var c) ? c : "";
        public static void SetLook(string id, string code)
        {
            if (string.IsNullOrEmpty(code)) Looks.Remove(id); else Looks[id] = code;
            PlayerPrefs.SetString("fb_looks", string.Join(";", Looks.Select(kv => kv.Key + "=" + kv.Value)));
            if (id == FBSession.skin) { FBSession.look = code ?? ""; FBSession.Save(); } else PlayerPrefs.Save();
        }
        /// <summary>Define o personagem em uso (fixo): skin + visual dele no FBSession, desliga o sorteio.</summary>
        public static void Use(string id)
        {
            FBSession.skin = id; FBSession.look = LookFor(id); FBSession.pickRandom = false; FBSession.Save();
        }
        /// <summary>Recarrega o que está em PlayerPrefs (o teste usa para simular reabrir o jogo).</summary>
        public static void Reload() { favs = null; looks = null; FBSession.Load(); }
    }
}
