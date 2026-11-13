// Dados de jogo exportados do jogo web (tools/unity/export_data.mjs -> StreamingAssets/fb_data.json): armas com os valores FINAIS de
// balanceamento (mesmas funções do web), recuo por classe, mapas (spawns), personagens e os parâmetros do controlador original (fpdata).
// Coordenadas: o web usa three.js (mão direita); o Unity é mão esquerda. O glTFast importa os GLB espelhando X, então um ponto do web
// (x, y, z) vira (-x, y, z) aqui, e o yaw do web (frente = (sin y, 0, cos y)) vira -y.
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FreeBala
{
    public static class Coords
    {
        public static Vector3 FromWeb(float x, float y, float z) => new Vector3(-x, y, z);
        public static Vector3 FromWeb(List<object> p) => p == null || p.Count < 3 ? Vector3.zero : FromWeb(Json.Fv(p[0]), Json.Fv(p[1]), Json.Fv(p[2]));
        public static float[] ToWeb(Vector3 v) => new[] { -v.x, v.y, v.z };
        public static float YawFromWeb(float webYawRad) => -webYawRad * Mathf.Rad2Deg;
        public static float YawToWeb(float unityYawDeg) => -unityYawDeg * Mathf.Deg2Rad;
    }

    public class WeaponDef
    {
        public string id, name, cls; public int slot; public bool auto, bolt, cycled; public int pellets, mag; public float rpm, spread, recoilScale, move;
        public float[] dmg, dmgHead; public Dictionary<string, object> sounds; public List<object> fire, tail;
        public float Interval => 60f / Mathf.Max(1f, rpm);
        // dano por distância (interpolado nos pontos exportados)
        public float Damage(float dist, bool head)
        {
            var ms = FBDamageModel.Of(this);                                    // r51: modelo do CS (peito; cabeça = × multiplicador da arma)
            if (ms != null) return FBDamageModel.Base(ms, dist / FBDamageModel.UnitsPerMeter) * (head ? ms.hs : 1);
            var t = head ? dmgHead : dmg; var D = GameData.Dist;
            if (t == null || t.Length == 0) return 20;
            for (int i = 1; i < D.Length; i++) if (dist <= D[i]) return Mathf.Lerp(t[i - 1], t[i], Mathf.InverseLerp(D[i - 1], D[i], dist));
            return t[t.Length - 1];
        }
        public bool IsPistol => cls == "pistola";
        public bool IsSniper => cls == "sniper";
    }

    public class RecoilClass { public float v, h, ramp, spread, ads, bloom, bloomMax, rec; }

    // r68d: dataA/dataB/dataP = os pontos COMO VIERAM do fb_data (spawnA, spawnB e points+waypoints+bots). O EnsureLevel do FBMatch filtra as
    // listas de cima no próprio MapDef e, quando uma fica vazia, sorteia pontos novos (outros em cada PC e em cada partida); a loja do De Rolê,
    // que precisa do MESMO lugar em todos os PCs, usa estas.
    public class MapDef { public string id, name; public bool disabled; public Dictionary<string, object> level; public List<Vector3> spawnA = new(), spawnB = new(), bots = new(), points = new(), waypoints = new(), dataA = new(), dataB = new(), dataP = new(); }

    public class SkinDef { public string id, name, sex, src, npc2, func; public string[] cats; public int team; public bool role, sheet; }   // r52: cats/func = categoria e função no elenco (FBElenco)

    public static class GameData
    {
        public static float[] Dist = { 0, 10, 20, 30, 45, 60, 80, 120 };
        public static readonly Dictionary<string, WeaponDef> Weapons = new();
        public static readonly Dictionary<string, RecoilClass> Recoil = new();
        public static readonly Dictionary<string, MapDef> Maps = new();
        public static readonly List<SkinDef> Skins = new();
        public static readonly Dictionary<string, float> ClipRS = new();                   // velocidade natural dos clipes no lugar (sincronia de passos)
        public static List<object> Emotes, NpcAnims, Victory; public static Dictionary<string, object> Root, WeaponsRaw, Vehicles, TpsGrip, Som, Wheels, IE, RES;
        public static Dictionary<string, object> Npc2, RebOficina;   // pacote novo de NPCs (conjuntos por personagem) · peças da oficina por carro do REB
        public static Dictionary<string, object> FP;           // fpdata.json (movement, cameraLook, tpsCamera, puppet, weaponRecoil...)
        static bool loaded;

        public static readonly HashSet<string> BannedWeapons = new(System.StringComparer.OrdinalIgnoreCase) { "m79", "garruncha_12", "submetralhadora_caseira" };
        // [parceiro 31/10] Praça do PT, Orla e Centro Cívico liberados junto com Vila Jardim e Praça das Águas
        public static readonly HashSet<string> AllowedMaps = new(System.StringComparer.OrdinalIgnoreCase) { "vila_jardim", "praca_aguas", "praca_pt", "orla", "centro_civico", "morro_sacola", "bv_city" };

        public static void Load()
        {
            if (loaded) return; loaded = true;
            string txt = null;
            var path = Path.Combine(Application.streamingAssetsPath, "fb_data.json");
#if UNITY_ANDROID || UNITY_WEBGL
            var ta = Resources.Load<TextAsset>("fb_data");           // nessas plataformas StreamingAssets não é arquivo: o importador copia para Resources
            if (ta) txt = ta.text;
#endif
            if (txt == null && File.Exists(path)) txt = File.ReadAllText(path);
            if (txt == null) { var ta2 = Resources.Load<TextAsset>("fb_data"); if (ta2) txt = ta2.text; }
            if (txt == null) { Debug.LogError("[FreeBala] fb_data.json não encontrado (rode FreeBala > Importar tudo)"); return; }
            var root = Json.Parse(txt);
            Dist = Json.Floats(Json.L(root, "dist"));
            foreach (var kv in Json.O(root, "weapons"))
            {
                if (BannedWeapons.Contains(kv.Key)) continue;
                var o = kv.Value;
                Weapons[kv.Key] = new WeaponDef
                {
                    id = kv.Key, name = Json.S(o, "name"), cls = Json.S(o, "cls"), slot = Json.I(o, "slot"), auto = Json.B(o, "auto"), bolt = Json.B(o, "bolt"),
                    cycled = Json.B(o, "cycled"), pellets = Json.I(o, "pellets", 1), mag = Json.I(o, "mag", 30), rpm = Json.F(o, "rpm", 600), spread = Json.F(o, "spread", .25f),
                    recoilScale = Json.F(o, "recoilScale", 1), move = Json.F(o, "move", 1), dmg = Json.Floats(Json.L(o, "dmg")), dmgHead = Json.Floats(Json.L(o, "dmgHead")),
                    sounds = Json.O(o, "sounds"), fire = Json.L(o, "fire"), tail = Json.L(o, "tail"),
                };
                FBDamageModel.Apply(Weapons[kv.Key]);                            // r51: cadência/bagos/precisão/recuo/mobilidade por arma
            }
            foreach (var kv in Json.O(root, "recoil"))
            {
                var o = kv.Value;
                Recoil[kv.Key] = new RecoilClass { v = Json.F(o, "v"), h = Json.F(o, "h"), ramp = Json.F(o, "ramp", 1), spread = Json.F(o, "spread"), ads = Json.F(o, "ads"), bloom = Json.F(o, "bloom"), bloomMax = Json.F(o, "bloomMax"), rec = Json.F(o, "rec", 6) };
            }
            foreach (var kv in Json.O(root, "maps"))
            {
                var o = kv.Value; bool dis = Json.B(o, "disabled") || !AllowedMaps.Contains(kv.Key);
                var m = new MapDef { id = kv.Key, name = Json.S(o, "name"), disabled = dis, level = Json.O(o, "level") };
                void Fill(List<Vector3> dst, string k) { var l = Json.L(o, k); if (l != null) foreach (var p in l) dst.Add(Coords.FromWeb(p as List<object>)); }
                Fill(m.spawnA, "spawnA"); Fill(m.spawnB, "spawnB"); Fill(m.bots, "bots"); Fill(m.points, "points"); Fill(m.waypoints, "waypoints");
                m.dataA.AddRange(m.spawnA); m.dataB.AddRange(m.spawnB); m.dataP.AddRange(m.points); m.dataP.AddRange(m.waypoints); m.dataP.AddRange(m.bots);   // r68d: cópia intocada (ver MapDef)
                Maps[kv.Key] = m;
            }
            foreach (var o in Json.L(root, "skins"))
            {
                bool role = Json.B(o, "role"); int team = Json.I(o, "team", -1);
                Skins.Add(new SkinDef { id = Json.S(o, "id"), name = Json.S(o, "name"), sex = Json.S(o, "sex", "m"), src = Json.S(o, "src", ""), team = team, role = role, sheet = Json.B(o, "sheet"), npc2 = Json.S(o, "npc2", ""), func = Json.S(o, "func", ""), cats = FBElenco.CatsFrom(Json.L(o, "cats"), role, team) });
            }
            FP = Json.O(root, "fp"); Root = root as Dictionary<string, object>;
            Emotes = Json.L(root, "emotes"); NpcAnims = Json.L(root, "npcAnims"); Victory = Json.L(root, "victory"); WeaponsRaw = Json.O(root, "weaponsRaw"); Vehicles = Json.O(root, "vehicles"); TpsGrip = Json.O(root, "tpsGrip"); Som = Json.O(root, "som"); Wheels = Json.O(root, "wheels"); IE = Json.O(root, "ie"); RES = Json.O(root, "res");
            Npc2 = Json.O(root, "npc2"); RebOficina = Json.O(root, "rebOficina");
            var rs = Json.O(root, "clipRS"); if (rs != null) foreach (var kv in rs) ClipRS[kv.Key] = Json.Fv(kv.Value);
        }

        public static float Move(string k, float def) => Json.F(Json.O(FP, "movement"), k, def);
        public static Dictionary<string, object> Section(string k) => Json.O(FP, k);
    }
}
