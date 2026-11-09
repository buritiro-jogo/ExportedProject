// Janela BURITIRO > Ambiente (rodada 64): controla o BuritiroEnvironmentManager em tempo real no Play — horários prontos (12:00 meio-dia,
// 17:30 golden hour, 18:00 pôr do sol, 18:30 blue hour, 20:00 noite, 00:00 madrugada, 05:30 amanhecer), hora livre, velocidade do ciclo,
// perfil CINEMATIC e o preset (ScriptableObject) em uso. Fora do Play mostra uma PRÉVIA do sol na cena aberta (direção/cor/força).
// Também cria o preset padrão em Resources/Env (FreeBala > Ambiente > Criar presets, ou em lote: FreeBala.EditorTools.FBEnvSetup.Batch).
using UnityEditor;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public class BuritiroEnvironmentWindow : EditorWindow
    {
        float previewHour = 18f; FBEnvPreset previewPreset; Vector2 scroll;
        [MenuItem("BURITIRO/Ambiente (horário e luz)", priority = 1)] static void Open() => GetWindow<BuritiroEnvironmentWindow>("Ambiente BURITIRO");
        void OnInspectorUpdate() { if (Application.isPlaying) Repaint(); }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var m = BuritiroEnvironmentManager.I;
            EditorGUILayout.LabelField("Horários prontos", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
                foreach (var (nome, h) in FBEnv.Presets)
                    if (GUILayout.Button($"{FBEnv.HourText(h)}\n{nome}", GUILayout.Height(38))) { if (m) { m.SetHour(h); } else { previewHour = h; Preview(); } }
            EditorGUILayout.Space();
            if (Application.isPlaying && m)
            {
                EditorGUI.BeginChangeCheck();
                float h = EditorGUILayout.Slider("Hora", m.hour, 0, 24);
                if (EditorGUI.EndChangeCheck()) m.SetHour(h);
                m.cycleSpeed = EditorGUILayout.Slider(new GUIContent("Ciclo (min de jogo / s)", "0 = horário parado"), m.cycleSpeed, 0, 120);
                bool cine = EditorGUILayout.Toggle(new GUIContent("Perfil CINEMATIC", "sombra 4096 até 220 m, volumétrica com mais passos"), FBEnvQuality.Cinematic);
                if (cine != FBEnvQuality.Cinematic) { FBEnvQuality.ApplyCinematic(cine); m.cinematic = cine; }
                EditorGUI.BeginChangeCheck();
                m.preset = (FBEnvPreset)EditorGUILayout.ObjectField("Preset", m.preset, typeof(FBEnvPreset), false);
                if (EditorGUI.EndChangeCheck() && m.preset) m.MarkDirty();
                if (m.preset && GUILayout.Button("Reaplicar o preset (depois de editar as curvas)")) m.MarkDirty();
                EditorGUILayout.Space();
                var s = m.state;
                EditorGUILayout.LabelField("Estado", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(s.ToString().Replace(" · ", "\n"), MessageType.None);
                EditorGUILayout.LabelField("Sol", $"rumo {s.sunAz:0.0}° · elevação {s.sunEl:0.0}°");
                EditorGUILayout.LabelField("Lua", $"rumo {s.moonAz:0.0}° · elevação {s.moonEl:0.0}° · {s.moonIllum * 100:0}% {(s.moonPhase < 0 ? "minguante" : "crescente")}");
                EditorGUILayout.ColorField("Cor da luz", s.lightColor); EditorGUILayout.ColorField("Céu (ambiente)", s.ambSky); EditorGUILayout.ColorField("Neblina", s.fogColor);
                EditorGUILayout.LabelField("Postes/janelas", $"noite {s.night:0.00} · janelas {s.windows:0.00} · postes acesos {FBStreetLights.Lit}");
            }
            else
            {
                EditorGUILayout.HelpBox("Fora do Play: prévia só do sol (direção, cor e força) na cena aberta. O sistema completo (céu físico, ambiente, pós, postes) liga no Play/partida.", MessageType.Info);
                previewPreset = (FBEnvPreset)EditorGUILayout.ObjectField("Preset", previewPreset ? previewPreset : Resources.Load<FBEnvPreset>("Env/BuritiroEnv_Padrao"), typeof(FBEnvPreset), false);
                EditorGUI.BeginChangeCheck(); previewHour = EditorGUILayout.Slider("Hora (prévia)", previewHour, 0, 24); if (EditorGUI.EndChangeCheck()) Preview();
                if (GUILayout.Button("Criar/atualizar o preset padrão (Resources/Env)")) FBEnvSetup.Create(false);
            }
            EditorGUILayout.EndScrollView();
        }

        void Preview()
        {
            var p = previewPreset ? previewPreset : Resources.Load<FBEnvPreset>("Env/BuritiroEnv_Padrao"); if (!p) p = FBEnvPreset.Padrao();
            var sun = RenderSettings.sun; if (!sun) foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sun = l; break; }
            if (!sun) return;
            var b = FBSolar.Sun(FBSolar.Julian(p.year, p.month, p.day, previewHour, p.utcOffset), p.latitude, p.longitude);
            FBAtmosphere.Turbidity = p.turbidity; var T = FBAtmosphere.Transmittance(b.elevation); float mx = Mathf.Max(1e-5f, Mathf.Max(T.x, Mathf.Max(T.y, T.z)));
            Undo.RecordObject(sun.transform, "Prévia do sol"); Undo.RecordObject(sun, "Prévia do sol");
            sun.transform.rotation = Quaternion.LookRotation(-b.dir); sun.useColorTemperature = false; sun.color = new Color(T.x / mx, T.y / mx, T.z / mx);
            SceneView.RepaintAll();
            Debug.Log($"[ambiente] prévia {FBEnv.HourText(previewHour)}: sol rumo {b.azimuth:0.0}° elevação {b.elevation:0.0}°");
        }
    }

    public static class FBEnvSetup
    {
        const string Dir = "Assets/FreeBala/Resources/Env", Path = Dir + "/BuritiroEnv_Padrao.asset";
        [MenuItem("FreeBala/Ambiente/Criar presets (Resources/Env)")] static void Menu() => Create(false);
        public static void Batch() { Create(System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbreset") >= 0); EditorApplication.Exit(0); }   // -fbreset: regrava as curvas
        // cria o preset padrão (curvas calibradas da rodada 64) + ajustes por mapa; 'reset' regrava as curvas de um asset existente
        public static void Create(bool reset)
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FreeBala/Resources", "Env");
            var p = AssetDatabase.LoadAssetAtPath<FBEnvPreset>(Path);
            if (!p) { p = FBEnvPreset.Padrao(); AssetDatabase.CreateAsset(p, Path); }
            else if (reset) p.Fill();
            // por mapa: o Morro tem pouca iluminação pública (menos poluição luminosa); o centro (PT/Praça das Águas) mais
            void Map(string id, float lp, float fog = 1)
            {
                var o = p.For(id); if (o == null) { o = new FBEnvPreset.MapOverride { mapId = id }; p.maps.Add(o); }
                o.lightPollution = lp; o.fogMul = fog;
            }
            Map("vila_jardim", .5f); Map("praca_pt", .7f); Map("praca_aguas", .72f); Map("orla", .6f, 1.1f); Map("centro_civico", .75f); Map("morro_sacola", .4f);
            EditorUtility.SetDirty(p); AssetDatabase.SaveAssets();
            Debug.Log($"[FreeBala][ambiente] preset {Path} pronto ({p.maps.Count} mapas)");
        }
    }
}
