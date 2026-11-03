// Perfis de voz de dano: QUAL voz cada personagem usa (sexo + timbre) e QUAIS clipes cada banco tem.
// Dados em Resources/Audio/Voice/voice_profiles.json (editável sem recompilar):
//   profiles.<m|f>.pitch [min,max]  faixa do timbre base; cada personagem recebe um ponto FIXO dessa faixa pelo hash do id (identidade
//                                   estável entre partidas e entre clientes da rede), ou chars.<id>.pitch quando marcado à mão;
//   profiles.<m|f>.banks.<Banco>    nomes dos clipes (Resources/Audio/Voice/<nome>.ogg);
//   rules.<Banco>                   sobrepõe prioridade/cooldown/volume/altura/alcance padrão (FBVoiceRule.Default);
//   chars.<id>.sex                  sexo por personagem. Antes a voz feminina saía MASCULINA: FBVoice só olhava GameData.Skins
//                                   (65 skins) e ~50 personagens do roster (c_mia, c_clara, p_bianca...) não estão lá → caíam em "m";
//                                   além disso só existiam grunhidos masculinos (ie_hit/ie_injured) e o jogador nunca recebia o ajuste.
// Damage Event → Damage Type → **Voice Profile** → Variation → Playback Controller.
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
    // bancos de reação (a ordem é o id que vai na rede: só ACRESCENTAR no fim)
    public enum FBVoiceBank : byte
    {
        DorLeve, DorMedia, DorGrave, Cabeca, Membro, PoucaVida, Caido, Sangrando, Morte,
        GasTosse, GasIrrita, GasResp, GasEsforco, GasFraca, GasMorte,
        Queda, QuedaGrave, Explosao, Atropelo, Fogo, Ambiente, Esforco
    }

    // regra de um banco: prioridade (morte > grave > leve), cooldown do banco por personagem, volume, altura, distância de referência
    public struct FBVoiceRule
    {
        public int prio; public float cd, vol, pitch, refD, npcChance;
        public FBVoiceRule(int prio, float cd, float vol, float pitch = 1, float refD = 5, float npcChance = 1) { this.prio = prio; this.cd = cd; this.vol = vol; this.pitch = pitch; this.refD = refD; this.npcChance = npcChance; }
        public static FBVoiceRule Default(FBVoiceBank b) => b switch
        {
            FBVoiceBank.Morte => new(100, 0, 1f, 1, 7),
            FBVoiceBank.GasMorte => new(100, 0, .95f, 1, 6),
            FBVoiceBank.Caido => new(80, 2, .95f, 1, 6),
            FBVoiceBank.Explosao => new(70, 1.2f, 1f, .97f, 7),
            FBVoiceBank.Atropelo => new(70, 1.2f, 1f, 1, 7),
            FBVoiceBank.QuedaGrave => new(60, 1, .95f, .98f, 6),
            FBVoiceBank.Cabeca => new(55, .6f, .95f, 1.04f, 6),
            FBVoiceBank.DorGrave => new(50, .7f, .95f, 1, 6),
            FBVoiceBank.Fogo => new(50, 1.4f, .9f, 1, 6),
            FBVoiceBank.DorMedia => new(40, .6f, .85f, 1, 5, .8f),
            FBVoiceBank.Queda => new(35, .8f, .7f, 1, 4),
            FBVoiceBank.Membro => new(30, .6f, .75f, 1.02f, 5, .7f),
            FBVoiceBank.DorLeve => new(30, .55f, .75f, 1.02f, 5, .6f),
            FBVoiceBank.PoucaVida => new(25, 2.5f, .7f, .98f, 4),
            FBVoiceBank.Ambiente => new(25, 1.2f, .7f, 1, 4, .7f),
            FBVoiceBank.GasFraca => new(20, 2.5f, .6f, .97f, 3.5f),
            FBVoiceBank.GasTosse => new(18, 1.6f, .8f, 1, 4.5f),
            FBVoiceBank.Sangrando => new(15, 5, .55f, .96f, 3.5f, .6f),
            FBVoiceBank.GasIrrita => new(12, 3, .6f, 1, 3.5f),
            FBVoiceBank.GasEsforco => new(12, 3, .55f, 1, 3.5f),
            FBVoiceBank.GasResp => new(10, 1.4f, .55f, 1, 3),
            _ => new(10, 1, .6f, 1, 4)
        };
    }

    public class FBVoiceProfile
    {
        public string sex; public Vector2 pitch = Vector2.one;
        public readonly Dictionary<FBVoiceBank, string[]> banks = new();
        public int Count(FBVoiceBank b) => banks.TryGetValue(b, out var l) ? l.Length : 0;
        public string Clip(FBVoiceBank b, int v) => banks.TryGetValue(b, out var l) && l.Length > 0 ? l[Mathf.Abs(v) % l.Length] : null;
    }

    public class FBVoiceProfiles
    {
        public readonly Dictionary<string, FBVoiceProfile> profiles = new();
        public readonly Dictionary<string, (string sex, float pitch)> chars = new();
        public readonly FBVoiceRule[] rules = new FBVoiceRule[System.Enum.GetValues(typeof(FBVoiceBank)).Length];
        // mesma regra do web (main.js FEM_IDS) como última garantia para ids novos ainda fora do JSON
        static readonly Regex FEM = new(@"^(p_(bianca|brenda|bruna|clara|claudia|cristiana)|c_(mia|andreia|spec|clara|dezenove|renata|helena|adoida|fzera)\d*)$");

        public FBVoiceProfiles() { for (int i = 0; i < rules.Length; i++) rules[i] = FBVoiceRule.Default((FBVoiceBank)i); }
        public FBVoiceRule Rule(FBVoiceBank b) => rules[(int)b];

        static FBVoiceProfiles inst;
        public static FBVoiceProfiles I { get { if (inst == null) { var t = Resources.Load<TextAsset>("Audio/Voice/voice_profiles"); inst = Parse(t ? t.text : null); } return inst; } }

        public static FBVoiceProfiles Parse(string json)
        {
            var p = new FBVoiceProfiles(); var o = string.IsNullOrEmpty(json) ? null : Json.Parse(json) as Dictionary<string, object>;
            var pr = Json.O(o, "profiles");
            if (pr != null) foreach (var kv in pr)
                {
                    var vp = new FBVoiceProfile { sex = kv.Key }; var pt = Json.Floats(Json.L(kv.Value, "pitch")); if (pt.Length == 2) vp.pitch = new Vector2(pt[0], pt[1]);
                    var bk = Json.O(kv.Value, "banks");
                    if (bk != null) foreach (var b in bk)
                            if (System.Enum.TryParse<FBVoiceBank>(b.Key, out var e)) { var l = b.Value as List<object>; if (l == null) continue; var a = new string[l.Count]; for (int i = 0; i < a.Length; i++) a[i] = l[i] as string; vp.banks[e] = a; }
                    p.profiles[kv.Key] = vp;
                }
            var ru = Json.O(o, "rules");
            if (ru != null) foreach (var kv in ru)
                    if (System.Enum.TryParse<FBVoiceBank>(kv.Key, out var e))
                    {
                        var d = p.rules[(int)e]; var r = kv.Value;
                        p.rules[(int)e] = new FBVoiceRule(Json.I(r, "prio", d.prio), Json.F(r, "cd", d.cd), Json.F(r, "vol", d.vol), Json.F(r, "pitch", d.pitch), Json.F(r, "refD", d.refD), Json.F(r, "npcChance", d.npcChance));
                    }
            var ch = Json.O(o, "chars");
            if (ch != null) foreach (var kv in ch) p.chars[kv.Key] = (Json.S(kv.Value, "sex", null), Json.F(kv.Value, "pitch", 0));
            if (!p.profiles.ContainsKey("m")) p.profiles["m"] = new FBVoiceProfile { sex = "m" };
            if (!p.profiles.ContainsKey("f")) p.profiles["f"] = new FBVoiceProfile { sex = "f" };
            return p;
        }

        static string Base(string id) => string.IsNullOrEmpty(id) ? id : id.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');

        // sexo da voz: JSON (id exato → id sem número) → fb_data.skins → regex do web → conjunto NPC → masculino
        public string SexOf(string skinId, string npcSex = null)
        {
            if (!string.IsNullOrEmpty(skinId))
            {
                if (chars.TryGetValue(skinId, out var c) && !string.IsNullOrEmpty(c.sex)) return c.sex == "f" ? "f" : "m";
                var b = Base(skinId); if (b != skinId && chars.TryGetValue(b, out c) && !string.IsNullOrEmpty(c.sex)) return c.sex == "f" ? "f" : "m";
                var s = GameData.Skins?.Find(k => k.id == skinId)?.sex; if (!string.IsNullOrEmpty(s)) return s == "f" ? "f" : "m";
                if (FEM.IsMatch(skinId)) return "f";
            }
            return npcSex == "f" ? "f" : "m";
        }
        public FBVoiceProfile ProfileOf(string sex) => profiles.TryGetValue(sex == "f" ? "f" : "m", out var p) ? p : profiles["m"];

        // timbre fixo por personagem (identidade): marcado à mão no JSON ou ponto da faixa do perfil pelo hash estável do id
        public float PitchOf(string skinId, FBVoiceProfile prof)
        {
            if (!string.IsNullOrEmpty(skinId) && chars.TryGetValue(skinId, out var c) && c.pitch > 0) return c.pitch;
            return Mathf.Lerp(prof.pitch.x, prof.pitch.y, Hash01(skinId));
        }
        public static float Hash01(string s)
        {
            if (string.IsNullOrEmpty(s)) return .5f; uint h = 2166136261; foreach (char ch in s) { h ^= ch; h *= 16777619; }   // FNV-1a (igual em todos os clientes)
            return (h % 10007) / 10006f;
        }
    }
}
