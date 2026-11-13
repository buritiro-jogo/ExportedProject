// Escolhas e ajustes do jogador (porte do menu.sel + SET do web, salvos em PlayerPrefs como o localStorage): personagem e visual por
// personagem, mapa, modo, loadout, dificuldade, servidor, câmera (1ª/3ª), FOV, sensibilidade, mira (padrão/alternativa/manual), corrida segurando
// ou automática, dança da vitória, créditos/XP/passe, horário e clima por mapa, volumes e qualidade.
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public static class FBSession
    {
        public const string DefaultSkin = "r_tutu";   // personagens regionais (rodada 36)
        public static string skin = DefaultSkin, look = "", map = "vila_jardim", mode = "classico", primary = "ak", smg = "p90", secondary = "38", name = "JOGADOR";
        public static int team, diff = 1; public static bool online; public static string server = "ws://localhost:8123/ws", room;
        // r68 REDE LOCAL: endereço do Host da rede (o próprio PC hospedando ou um achado em PROCURAR NA REDE). NÃO é salvo: na próxima
        // abertura o jogo volta ao servidor online normal (antes um endereço de LAN salvo deixava o jogo "offline" quando o Host fechava).
        public static string lanServer;
        public static string Server => string.IsNullOrEmpty(lanServer) ? server : lanServer;
        // r69 (13/11/2026): modo ONLINE escolhido no SQUAD, separado do modo contra bots: "role" (De Rolê desarmado, loja), "rolex"
        // (ROLÊ ARMADO: a cidade livre com todo mundo armado, um contra o outro), "tdm" (times) e "x2" (2 × 2 sorteado, squad de 4).
        public static string netMode = "role";
        public static bool RoleArmed => online && mode == "role" && netMode == "rolex";
        public static bool Duel2x2 => online && netMode == "x2";
        public static string NetModeName(string m) => m == "rolex" ? "ROLÊ ARMADO" : m == "x2" ? "2 × 2" : m == "tdm" ? "TIMES" : "DE ROLÊ";
        // r43: nome de usuário escolhido pelo jogador (placar, feed de abates, online) — independente do nome do personagem
        public static bool nameChosen => PlayerPrefs.GetInt("fb_nameChosen", 0) == 1 && !string.IsNullOrWhiteSpace(name) && name != "JOGADOR";
        public static string CleanName(string s, out string why)
        {
            why = null; s = (s ?? "").Trim(); var sb = new System.Text.StringBuilder();
            foreach (var c in s) if (char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '-' || c == ' ') sb.Append(c);
            var r = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), " +", " ").Trim();
            if (r.Length < 3) { why = "USE PELO MENOS 3 LETRAS OU NÚMEROS"; return null; }
            if (r.Length > 16) r = r.Substring(0, 16).Trim();
            if (r != s.Trim()) why = "SÍMBOLOS TIRADOS (SÓ LETRAS, NÚMEROS, _ . -)";
            return r;
        }
        public const string OldDefaultServer = "ws://localhost:8123/ws";
        public const string OldVpsServer = "ws://179.66.136.216:8123/ws";   // r69 (porte do ramo mobile do parceiro, 09/11): VPS antigo, desligado na migração para o Brasil
        public static string ServidorPadrao()
        {
            try
            {
                var f = System.IO.Path.Combine(Application.streamingAssetsPath, "servidor.txt");
                if (System.IO.File.Exists(f)) { foreach (var l in System.IO.File.ReadAllLines(f)) { var t = l.Trim(); if (t.StartsWith("ws://") || t.StartsWith("wss://")) return t; } }
            }
            catch { }
            return OldDefaultServer;
        }
        public static void SetName(string n) { name = n; PlayerPrefs.SetInt("fb_nameChosen", 1); Save(); }
        public static readonly bool Lab = System.Array.Exists(System.Environment.GetCommandLineArgs(), x => x.StartsWith("-fb") && x.EndsWith("lab"));   // r45b: executável rodando um laboratório (-fb*lab)
        public static string view = "tps", aimAssist = "assist", victory; public static float fov = .375f, sens = .5f, adsSens = .5f, master = .9f, music = .5f, sfx = 1, voice = 1, vehicles = 1, ambient = 1; public static bool holdRun, invertY, aimToggle, blood = true, pickRandom, freeLookToggle, mobileHUD;
        // r57: rig de combate novo (KINEMATION FPS Animation Framework) por cima do Animator. O rig clássico (FBRig) fica
        // de reserva e volta escolhendo "CLÁSSICO" nos ajustes. Policiais não entram neste teste.
        public static bool rigNovo = true;
        // r58: animação de COMBATE nova (mira/tiro/recarga do Mixamo) para quem não é policial. A locomoção NÃO muda.
        public static bool animNova = true;   // padrões do SET do main.js
        public static int money = 2000, xp, passXp, quality = -1; public static string emoteSlots = "";
        static readonly Dictionary<string, (string time, string weather)> wx = new();
        public static (string time, string weather) WeatherFor(string mapId) => wx.TryGetValue(mapId, out var w) ? w : ("dia", "limpo");
        public static void SetWeather(string mapId, string time, string weather) { wx[mapId] = (time, weather); Save(); }

        public static void Save()
        {
            foreach (var (k, v) in new[] { ("skin", skin), ("look", look), ("map", map), ("mode", mode), ("primary", primary), ("smg", smg), ("secondary", secondary), ("name", name), ("server", server), ("view", view), ("aimAssist", aimAssist), ("victory", victory ?? ""), ("emotes", emoteSlots), ("netMode", netMode) })
                PlayerPrefs.SetString("fb_" + k, v);
            PlayerPrefs.SetInt("fb_diff", diff); PlayerPrefs.SetInt("fb_money", money); PlayerPrefs.SetInt("fb_xp", xp); PlayerPrefs.SetInt("fb_passXp", passXp); PlayerPrefs.SetInt("fb_quality", quality);
            PlayerPrefs.SetFloat("fb_fov", fov); PlayerPrefs.SetFloat("fb_sens", sens); PlayerPrefs.SetFloat("fb_adsSens", adsSens); PlayerPrefs.SetFloat("fb_master", master); PlayerPrefs.SetFloat("fb_music", music); PlayerPrefs.SetFloat("fb_sfx", sfx); PlayerPrefs.SetFloat("fb_voice", voice); PlayerPrefs.SetFloat("fb_vehicles", vehicles); PlayerPrefs.SetFloat("fb_ambient", ambient);
            PlayerPrefs.SetInt("fb_holdRun", holdRun ? 1 : 0); PlayerPrefs.SetInt("fb_invertY", invertY ? 1 : 0); PlayerPrefs.SetInt("fb_aimToggle", aimToggle ? 1 : 0); PlayerPrefs.SetInt("fb_blood", blood ? 1 : 0); PlayerPrefs.SetInt("fb_pickRandom", pickRandom ? 1 : 0); PlayerPrefs.SetInt("fb_freeLookToggle", freeLookToggle ? 1 : 0); PlayerPrefs.SetInt("fb_mobileHUD", mobileHUD ? 1 : 0); PlayerPrefs.SetInt("fb_rigNovo", rigNovo ? 1 : 0); PlayerPrefs.SetInt("fb_animNova", animNova ? 1 : 0);
            var sb = new System.Text.StringBuilder(); foreach (var kv in wx) sb.Append(kv.Key).Append('=').Append(kv.Value.time).Append(',').Append(kv.Value.weather).Append(';'); PlayerPrefs.SetString("fb_wx", sb.ToString());
            PlayerPrefs.Save(); Apply();
        }
        public static void Load()
        {
            string G(string k, string d) => PlayerPrefs.GetString("fb_" + k, d);
            // [parceiro 07/11] servidor online padrão: StreamingAssets/servidor.txt (o instalador do jogo grava o IP do VPS).
            // Quem ainda tem o padrão antigo (localhost) salvo passa a usar o do arquivo; quem digitou outro endereço mantém o seu.
            server = ServidorPadrao(); var salvo = G("server", server); if (salvo != OldDefaultServer && salvo != OldVpsServer) server = salvo;   // r69: quem tem o VPS antigo salvo passa para o do arquivo
            netMode = G("netMode", netMode); if (netMode != "role" && netMode != "rolex" && netMode != "tdm" && netMode != "x2") netMode = "role";   // r69
            skin = G("skin", skin); look = G("look", look); map = G("map", map); mode = G("mode", mode); primary = G("primary", primary); smg = G("smg", smg); secondary = G("secondary", secondary); name = G("name", name);
            view = "tps"; /* r45b: câmera em 1ª pessoa removida do jogo (pedido) */ aimAssist = G("aimAssist", aimAssist); victory = G("victory", ""); if (victory == "" || FBEmotes.IsFF(victory)) victory = null;   /* r47: dança de vitória FF salva */ emoteSlots = G("emotes", "");
            diff = PlayerPrefs.GetInt("fb_diff", diff); money = PlayerPrefs.GetInt("fb_money", money); xp = PlayerPrefs.GetInt("fb_xp", xp); passXp = PlayerPrefs.GetInt("fb_passXp", passXp); quality = PlayerPrefs.GetInt("fb_quality", quality);
            fov = PlayerPrefs.GetFloat("fb_fov", fov); sens = PlayerPrefs.GetFloat("fb_sens", sens); adsSens = PlayerPrefs.GetFloat("fb_adsSens", adsSens); master = PlayerPrefs.GetFloat("fb_master", master); music = PlayerPrefs.GetFloat("fb_music", music); sfx = PlayerPrefs.GetFloat("fb_sfx", sfx); voice = PlayerPrefs.GetFloat("fb_voice", voice); vehicles = PlayerPrefs.GetFloat("fb_vehicles", vehicles); ambient = PlayerPrefs.GetFloat("fb_ambient", ambient);
            holdRun = PlayerPrefs.GetInt("fb_holdRun", 0) == 1; invertY = PlayerPrefs.GetInt("fb_invertY", 0) == 1; aimToggle = PlayerPrefs.GetInt("fb_aimToggle", 0) == 1; blood = PlayerPrefs.GetInt("fb_blood", 1) == 1; pickRandom = PlayerPrefs.GetInt("fb_pickRandom", 0) == 1; freeLookToggle = PlayerPrefs.GetInt("fb_freeLookToggle", 0) == 1; rigNovo = PlayerPrefs.GetInt("fb_rigNovo", 1) == 1; animNova = PlayerPrefs.GetInt("fb_animNova", 1) == 1;
            mobileHUD = PlayerPrefs.GetInt("fb_mobileHUD", Application.isMobilePlatform ? 1 : 0) == 1;
            if (FBRegistry.I && !FBRegistry.I.HasChar(skin)) { skin = FBRegistry.I.HasChar(DefaultSkin) ? DefaultSkin : skin; look = ""; }   // salvo com um jogável que saiu do jogo
            // liberado para todos os personagens (masculino e feminino)
            if (mode == "4v4") mode = "squad";
            if (mode != "role" && mode != "classico" && mode != "squad" && mode != "10x10") mode = "classico";   // [parceiro 31/10] modo 10x10 (FBModo10x10) aceito
            // [parceiro] mapas liberados em GameData.AllowedMaps, desde que a cena esteja no build (orla só entra quando a cena existir)
            if (!GameData.AllowedMaps.Contains(map) || !Application.CanStreamedLevelBeLoaded("Map_" + map)) map = "vila_jardim";
            if (GameData.BannedWeapons.Contains(primary)) primary = "ak";
            if (GameData.BannedWeapons.Contains(smg)) smg = "p90";
            if (GameData.BannedWeapons.Contains(secondary)) secondary = "38";
            // r60b: arma salva que SAIU do jogo — o mesmo tratamento que o personagem que saiu, algumas linhas acima. Sem isto o
            // jogador fica de mãos vazias: no lobby a pose de fuzil toca sem fuzil e os braços parecem quebrados (relatado em 07/11,
            // depois que as armas pac_* do pacote experimental foram retiradas). Só com o catálogo já carregado.
            if (GameData.Weapons.Count > 0)
            {
                if (!GameData.Weapons.ContainsKey(primary)) primary = "ak";
                if (!GameData.Weapons.ContainsKey(smg)) smg = "p90";
                if (!GameData.Weapons.ContainsKey(secondary)) secondary = "38";
            }
            wx.Clear(); foreach (var e in G("wx", "").Split(';')) { var kv = e.Split('='); if (kv.Length == 2) { var tw = kv[1].Split(','); if (tw.Length == 2) wx[kv[0]] = (tw[0], tw[1]); } }
            Apply();
        }
        // aplica sem gravar em disco (os sliders chamam a cada quadro do arrasto; Save grava ao soltar)
        public static void Apply()
        {
            FBInput.Sens = sens; FBInput.AdsSens = adsSens; FBInput.InvertY = invertY; FBInput.AimToggle = aimToggle; FBAudio.SetVolumes(master, sfx, music, voice, vehicles, ambient); FBFx.BloodOn = blood;
            if (FBTouch.I) { FBTouch.I.CheckActivation(); FBTouch.I.SetEnabled(FBTouch.I.on); }
            // r47: tirado o "QualitySettings.SetQualityLevel(quality)" do fb_quality antigo (3 níveis do Unity): a qualidade é do FBQuality
            // (fb_qlevel), e esse nível velho, quando existia, disparava activeQualityLevelChanged -> FBQuality reaplicava a cada Save
        }
        public static string FullSkin => string.IsNullOrEmpty(look) ? skin : skin + "~" + look;   // id + visual (guarda-roupa), como no web
    }
}
