// Layout customizável dos botões de toque (Mobile HUD): posições, escalas, opacidades e visibilidade
// dos botões na versão mobile, salvos em PlayerPrefs com persistência e restauração de padrões.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    [Serializable]
    public class FBMobileButtonData
    {
        public string id;          // Identificador único (ex: "FIRE", "AIM", "JUMP")
        public string name;        // Nome amigável exibido no editor (ex: "ATIRAR")
        public string icon;        // Rótulo curto / ícone no botão (ex: "TIRO")
        public Vector2 pos;        // Posição no canvas de referência (1600 x 900)
        public float radius;       // Raio base em pixels
        public float scale = 1f;   // Escala de tamanho (0.5 a 1.6)
        public float opacity = .7f;// Opacidade / transparência (0.15 a 1.0)
        public bool enabled = true;// Visibilidade / ativo

        public float TotalRadius => radius * Mathf.Clamp(scale, .5f, 1.8f);

        public FBMobileButtonData Clone()
        {
            return new FBMobileButtonData
            {
                id = this.id,
                name = this.name,
                icon = this.icon,
                pos = this.pos,
                radius = this.radius,
                scale = this.scale,
                opacity = this.opacity,
                enabled = this.enabled
            };
        }
    }

    public static class FBMobileLayout
    {
        const string PrefKey = "fb_mobile_hud_layout_v1";

        // Layout de fábrica ergonômico padrão (resolução base 1600 x 900)
        public static List<FBMobileButtonData> GetDefaults()
        {
            return new List<FBMobileButtonData>
            {
                // Movimento e suporte (lado esquerdo)
                new FBMobileButtonData { id = "JOYSTICK",   name = "ANDAR",        icon = "JOY",     pos = new Vector2(210, 190),  radius = 95, scale = 1f,   opacity = .55f, enabled = true },
                new FBMobileButtonData { id = "SPRINT",     name = "CORRER",       icon = "CORRER",  pos = new Vector2(210, 350),  radius = 42, scale = 1f,   opacity = .70f, enabled = true },
                new FBMobileButtonData { id = "FIRE_LEFT",  name = "TIRO ESQ.",    icon = "TIRO",    pos = new Vector2(190, 480),  radius = 54, scale = 1f,   opacity = .65f, enabled = true },
                new FBMobileButtonData { id = "HEAL",       name = "CURAR",        icon = "CURA",    pos = new Vector2(380, 85),   radius = 44, scale = 1f,   opacity = .70f, enabled = true },
                new FBMobileButtonData { id = "PICKUP",     name = "PEGAR",        icon = "PEGAR",   pos = new Vector2(380, 190),  radius = 44, scale = 1f,   opacity = .65f, enabled = true },

                // Centro / Topo
                new FBMobileButtonData { id = "EMOTE",      name = "EMOTES",       icon = "EMOTE",   pos = new Vector2(800, 830),  radius = 42, scale = 1f,   opacity = .70f, enabled = true },
                new FBMobileButtonData { id = "MAP",        name = "MAPA",         icon = "MAPA",    pos = new Vector2(1420, 830), radius = 42, scale = 1f,   opacity = .75f, enabled = true },

                // Combate e ações (lado direito)
                new FBMobileButtonData { id = "FIRE",       name = "ATIRAR",       icon = "TIRO",    pos = new Vector2(1390, 240), radius = 76, scale = 1.05f,opacity = .80f, enabled = true },
                new FBMobileButtonData { id = "AIM",        name = "MIRA",         icon = "MIRA",    pos = new Vector2(1230, 220), radius = 58, scale = 1f,   opacity = .75f, enabled = true },
                new FBMobileButtonData { id = "JUMP",       name = "PULAR",        icon = "PULO",    pos = new Vector2(1485, 115), radius = 54, scale = 1f,   opacity = .75f, enabled = true },
                new FBMobileButtonData { id = "CROUCH",     name = "AGACHAR",      icon = "AGACH.",  pos = new Vector2(1350, 80),  radius = 50, scale = 1f,   opacity = .75f, enabled = true },
                new FBMobileButtonData { id = "PRONE",      name = "DEITAR",       icon = "DEITAR",  pos = new Vector2(1220, 70),  radius = 44, scale = 1f,   opacity = .70f, enabled = true },
                new FBMobileButtonData { id = "RELOAD",     name = "RECARREGAR",   icon = "RECAR.",  pos = new Vector2(1190, 350), radius = 48, scale = 1f,   opacity = .70f, enabled = true },
                new FBMobileButtonData { id = "SWAP",       name = "TROCAR ARMA",  icon = "TROCA",   pos = new Vector2(1430, 390), radius = 48, scale = 1f,   opacity = .70f, enabled = true },
                new FBMobileButtonData { id = "INTERACT",   name = "AÇÃO / VEÍCULO",icon= "AÇÃO",    pos = new Vector2(1310, 380), radius = 48, scale = 1f,   opacity = .70f, enabled = true },
                new FBMobileButtonData { id = "CAMERA",     name = "CÂMERA",       icon = "CAM",     pos = new Vector2(1510, 560), radius = 40, scale = 1f,   opacity = .65f, enabled = true }
            };
        }

        // Carrega o layout salvo ou retorna o padrão
        public static List<FBMobileButtonData> Load()
        {
            var defs = GetDefaults();
            string raw = PlayerPrefs.GetString(PrefKey, "");
            if (string.IsNullOrEmpty(raw)) return defs;

            try
            {
                var dict = new Dictionary<string, FBMobileButtonData>();
                foreach (var d in defs) dict[d.id] = d;

                var entries = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var entry in entries)
                {
                    var parts = entry.Split(':');
                    if (parts.Length < 5) continue;
                    string id = parts[0];
                    if (!dict.TryGetValue(id, out var btn)) continue;

                    var coords = parts[1].Split(',');
                    if (coords.Length == 2 &&
                        float.TryParse(coords[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                        float.TryParse(coords[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                    {
                        btn.pos = new Vector2(Mathf.Clamp(x, 40, 1560), Mathf.Clamp(y, 40, 860));
                    }

                    if (float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float scale))
                        btn.scale = Mathf.Clamp(scale, .5f, 1.6f);

                    if (float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float opacity))
                        btn.opacity = Mathf.Clamp(opacity, .15f, 1f);

                    if (int.TryParse(parts[4], out int en))
                        btn.enabled = en == 1;
                }

                return defs;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FreeBala] Erro ao ler layout mobile salvo: " + e.Message);
                return defs;
            }
        }

        // Salva o layout atual em disco
        public static void Save(List<FBMobileButtonData> buttons)
        {
            if (buttons == null || buttons.Count == 0) return;
            var sb = new System.Text.StringBuilder();
            var inv = CultureInfo.InvariantCulture;

            for (int i = 0; i < buttons.Count; i++)
            {
                var b = buttons[i];
                if (b == null) continue;
                if (i > 0) sb.Append(';');
                sb.Append(b.id).Append(':');
                sb.Append(b.pos.x.ToString("F1", inv)).Append(',').Append(b.pos.y.ToString("F1", inv)).Append(':');
                sb.Append(b.scale.ToString("F2", inv)).Append(':');
                sb.Append(b.opacity.ToString("F2", inv)).Append(':');
                sb.Append(b.enabled ? "1" : "0");
            }

            PlayerPrefs.SetString(PrefKey, sb.ToString());
            PlayerPrefs.Save();
        }

        // Restaura para os padrões de fábrica
        public static void ResetToDefaults()
        {
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
        }
    }
}

