// anims.json (curvas de MÚSCULO do Mecanim, extraídas dos APKs) -> AnimationClips Humanoid nativos, e controllers.json (AnimatorController
// original do jogo: parâmetros, camadas com máscara, árvores 1D/2D, transições e Any State) -> AnimatorController do Unity.
// A ordem dos 95 músculos do humanoid.js (web) é a mesma de HumanTrait.MuscleName; os 7 primeiros canais são RootT.xyz + RootQ.xyzw.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBAnimImport
    {
        public const string ClipDir = "Assets/FreeBala/Anim/Clips", CtrlDir = "Assets/FreeBala/Anim/Controllers";
        static readonly string[] RootCh = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };

        public static string Safe(string n) { foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_'); return n.Replace('|', '_').Replace(':', '_'); }

        // ---- clipes: anims.json (floats), emotes.json (FF_*, inteiros/1000), npc_anims.json (NPC_*), reb_vehicle_animations.json
        public static Dictionary<string, AnimationClip> ImportClips(string animsJson)
        {
            Directory.CreateDirectory(ClipDir);
            var res = new Dictionary<string, AnimationClip>(); var dir = Path.GetDirectoryName(animsJson);
            try
            {
                var root = Json.Parse(File.ReadAllText(animsJson)) as Dictionary<string, object>; int n = 0;
                // + clipes de combate do Invasão Elite (tiro/recarga por arma, agachado por classe): assets/anims_ie_combat.json, mesmo formato
                var extra = Path.Combine(dir, "anims_ie_combat.json");
                if (File.Exists(extra) && Json.Parse(File.ReadAllText(extra)) is Dictionary<string, object> ex) foreach (var kv in ex) if (!root.ContainsKey(kv.Key)) root[kv.Key] = kv.Value;
                foreach (var kv in root)
                {
                    EditorUtility.DisplayProgressBar("FreeBala: animações", kv.Key, (float)n++ / root.Count);
                    var d = Json.Floats(Json.L(kv.Value, "d")); if (d.Length == 0) continue;
                    res[kv.Key] = MakeClip(kv.Key, Json.F(kv.Value, "fps", 30), d, 1, Json.B(kv.Value, "loop"));
                }
                // rodada 37: músculos a 20 qps remontados dos ossos b3 (src/tools_muscle20.mjs). O 'd' destas listas é só a RESERVA do jogo web
                // (1 a cada 4 quadros = 5 qps): cura, caído, pulo de janela, sentar, dirigir, danças e andares dos NPCs saíam com 5 poses por
                // segundo e interpolação linear ("robótico"). Sem a versão cheia (clipes de veículo ajustados à mão): reserva com tangente suave.
                var m20p = Path.Combine(dir, "anims_muscle20.json"); var m20 = File.Exists(m20p) ? Json.Parse(File.ReadAllText(m20p)) as Dictionary<string, object> : null; int full20 = 0, smooth5 = 0;
                foreach (var (file, prefix, idKey) in new[] { ("emotes.json", "FF_", "name"), ("npc_anims.json", "", "id"), ("reb_vehicle_animations.json", "REB_", "name"), ("walk_regional.json", "REG_", "name"), ("anims_171.json", "X171_", "name") })
                {
                    var f = Path.Combine(dir, file); if (!File.Exists(f)) continue;
                    var list = Json.L(Json.Parse(File.ReadAllText(f)), "clips"); n = 0; var mf = m20 != null ? Json.O(m20, file) : null;
                    foreach (var e in list)
                    {
                        var id = prefix + Json.S(e, idKey); EditorUtility.DisplayProgressBar("FreeBala: " + file, id, (float)n++ / list.Count);
                        if (res.ContainsKey(id)) continue;
                        var full = mf != null ? Json.O(mf, Json.S(e, idKey)) : null;
                        var d = Json.Floats(Json.L(full ?? e, "d")); float fps = full != null ? Json.F(full, "fps", 20) : Json.F(e, "fps", 30); if (d.Length < 102) continue;
                        var name = Json.S(e, "name") ?? "";
                        bool once = Json.Get(e, "loop") is bool lb ? !lb : false;
                        once |= System.Text.RegularExpressions.Regex.IsMatch(name, @"^SYS_(?:Vault|SG_(?:Entrar|Sair)(?:Carro[DE]|Moto)|REB_(?:EntrarCarro[DE]|SairCarro[DE]|Sentar))$") || prefix == "REB_";
                        bool smooth = fps < 15; if (full != null) full20++; else if (smooth) smooth5++;
                        res[id] = MakeClip(id, fps, d, .001f, !once, smooth);
                        // andares novos (22/10): o masculino é "no lugar" (sem deriva para medir) — a velocidade natural vem do 'rs' do retarget
                        if (file == "walk_regional.json" && !measuredRS.ContainsKey(id) && Json.F(e, "rs", 0) > 0) measuredRS[id] = Json.F(e, "rs", 0);
                    }
                }
                Debug.Log($"[FreeBala][r37] clipes de sistema/emote/NPC: {full20} a 20 qps (ossos b3), {smooth5} reservas de 5 qps com tangente suave");
            }
            finally { EditorUtility.ClearProgressBar(); }
            // velocidade natural dos loops medida ANTES de tirar a deriva (rootSpeed do humanoid.js) → speedMatch do FBCharacter
            var rsPath = "Assets/FreeBala/Resources/FBClipRS.json"; Directory.CreateDirectory(Path.GetDirectoryName(rsPath));
            File.WriteAllText(rsPath, "{" + string.Join(",", measuredRS.Select(kv => "\"" + kv.Key.Replace("\"", "") + "\":" + kv.Value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture))) + "}");
            AssetDatabase.ImportAsset(rsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FreeBala] {res.Count} clipes Humanoid importados ({measuredRS.Count} loops com deriva da raiz removida)");
            return res;
        }

        // só os clipes extras (combate do IE) sem refazer o import inteiro:
        // Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBAnimImport.ImportExtraBatch
        public static void ImportExtraBatch()
        {
            int code = 0;
            try { ImportExtra(); } catch (System.Exception e) { Debug.LogError("[FreeBala] clipes extras: " + e); code = 1; }
            EditorApplication.Exit(code);
        }
        [MenuItem("FreeBala/Importar clipes de combate do IE", priority = 4)]
        public static void ImportExtra()
        {
            var f = Path.Combine(FBImport.Src, "anims_ie_combat.json"); if (!File.Exists(f)) { Debug.LogWarning("[FreeBala] faltou " + f); return; }
            Directory.CreateDirectory(ClipDir); int n = 0;
            if (Json.Parse(File.ReadAllText(f)) is Dictionary<string, object> root)
                foreach (var kv in root) { var d = Json.Floats(Json.L(kv.Value, "d")); if (d.Length < 102) continue; MakeClip(kv.Key, Json.F(kv.Value, "fps", 30), d, 1, Json.B(kv.Value, "loop")); n++; }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            var reg = AssetDatabase.LoadAssetAtPath<FBRegistry>("Assets/FreeBala/Resources/FBRegistry.asset");
            if (reg) { reg.clips = Directory.GetFiles(ClipDir, "*.anim").Select(p => AssetDatabase.LoadAssetAtPath<AnimationClip>(p.Replace(Path.DirectorySeparatorChar, '/'))).Where(c => c).ToList(); EditorUtility.SetDirty(reg); AssetDatabase.SaveAssets(); }
            Debug.Log($"[FreeBala] {n} clipes de combate do IE importados; registro com {(reg ? reg.clips.Count : 0)} clipes");
        }

        // r60b MIRA DO COMPLEXO FAVELA: assets/anims_cf_mira.json com "Aiming" e "Idle Aiming".
        // Por que nao vieram no import geral: o tools/anims.py le o datapack do Complexo Favela mas filtra
        // SO os objetos de resources.assets, e esses dois estao em sharedassets5.assets (unicos 2 clipes de la).
        // Por isso a vaga Rifle_Idle so tinha o "Rifle Idle", que e de ARMA BAIXA — e mirar a partir dele fazia
        // o rig deitar a coluna ate 22 graus para levantar o cano (o "corpo inclinando" relatado em 07/11).
        // Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBAnimImport.ImportMiraCfBatch
        public static void ImportMiraCfBatch()
        {
            int code = 0;
            try { ImportMiraCf(); } catch (System.Exception e) { Debug.LogError("[FreeBala] mira do CF: " + e); code = 1; }
            EditorApplication.Exit(code);
        }
        [MenuItem("FreeBala/Importar mira do Complexo Favela", priority = 5)]
        public static void ImportMiraCf()
        {
            var f = Path.Combine(FBImport.Src, "anims_cf_mira.json"); if (!File.Exists(f)) { Debug.LogWarning("[FreeBala] faltou " + f); return; }
            Directory.CreateDirectory(ClipDir); int n = 0;
            if (Json.Parse(File.ReadAllText(f)) is Dictionary<string, object> root)
                foreach (var kv in root) { var d = Json.Floats(Json.L(kv.Value, "d")); if (d.Length < 102) continue; MakeClip(kv.Key, Json.F(kv.Value, "fps", 30), d, 1, Json.B(kv.Value, "loop")); n++; }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            var reg = AssetDatabase.LoadAssetAtPath<FBRegistry>("Assets/FreeBala/Resources/FBRegistry.asset");
            if (reg) { reg.clips = Directory.GetFiles(ClipDir, "*.anim").Select(p => AssetDatabase.LoadAssetAtPath<AnimationClip>(p.Replace(Path.DirectorySeparatorChar, '/'))).Where(c => c).ToList(); EditorUtility.SetDirty(reg); AssetDatabase.SaveAssets(); }
            Debug.Log($"[FreeBala] {n} clipes de mira do Complexo Favela importados; registro com {(reg ? reg.clips.Count : 0)} clipes");
        }

        // clipes do 171 (rodada 39 — uso condicionado à autorização da Betagames): assets/anims_171.json, gerado por tools/re/ue_anim (GLB do
        // esqueleto ALS) -> src/tools_retarget.mjs (FULL=. : 20 qps). Só este arquivo, sem refazer o import inteiro:
        // Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBAnimImport.Import171Batch
        public static void Import171Batch()
        {
            int code = 0;
            try { Import171(); } catch (System.Exception e) { Debug.LogError("[FreeBala] clipes do 171: " + e); code = 1; }
            EditorApplication.Exit(code);
        }
        static readonly System.Text.RegularExpressions.Regex ONCE171 = new(@"Start|Enter|Exit|Entrar|Sair|Levant|Sentando|_To_|Montage|Fim$|End$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        static readonly System.Text.RegularExpressions.Regex MOTO_ONCE = new(@"Go_to|Get_bike|Jump_from|Enter|Exit|Helmet|helmet|_to_|throw|colision|hitground|Return_grau|Pilotando_to|_to_Pilotando", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        static readonly System.Text.RegularExpressions.Regex NPC_ONCE = new(@"Bump|Shove|_Start$|Tropeco|SCR_Stand_|Interacao|PegandoLixo", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        public static void Import171()
        {
            var f = Path.Combine(FBImport.Src, "anims_171.json"); if (!File.Exists(f)) { Debug.LogWarning("[FreeBala] faltou " + f); return; }
            Directory.CreateDirectory(ClipDir); int n = 0; measuredRS.Clear();
            foreach (var e in Json.L(Json.Parse(File.ReadAllText(f)), "clips") ?? new List<object>())
            {
                var name = Json.S(e, "name"); var id = "X171_" + name; var d = Json.Floats(Json.L(e, "d")); if (d.Length < 102) continue;
                MakeClip(id, Json.F(e, "fps", 20), d, .001f, !ONCE171.IsMatch(name)); n++;
                if (!measuredRS.ContainsKey(id) && Json.F(e, "rs", 0) > 0) measuredRS[id] = Json.F(e, "rs", 0);
            }
            // mortes do 171 (rodada 42: Damage/Death direcionais + Shooted correndo + poses de impacto por parte do corpo), mesmo uso
            // condicionado; nunca em laço (o corpo cai e fica): assets/anims_171_morte.json
            var fm = Path.Combine(FBImport.Src, "anims_171_morte.json");
            if (File.Exists(fm)) foreach (var e in Json.L(Json.Parse(File.ReadAllText(fm)), "clips") ?? new List<object>())
                {
                    var name = Json.S(e, "name"); var d = Json.Floats(Json.L(e, "d")); if (d.Length < 102) continue;
                    MakeClip("X171_" + name, Json.F(e, "fps", 20), d, .001f, false); n++;
                }
            // r43: motos do 171 (pilotar, montar/descer pelos dois lados, levantar a moto, pular fora, inclinação, vento, grau, zerinho, garupa,
            // capacete, mira na moto) — mesmo uso condicionado: assets/anims_171_moto.json (tools/re/ue_anim export + src/tools_retarget.mjs)
            var seen = new HashSet<string>();
            // + eventos de rua do 171 (eletricista no poste, obra: medindo/vendo a planta/varrendo): assets/anims_171_eventos.json (em laço)
            foreach (var fmo in new[] { Path.Combine(FBImport.Src, "anims_171_moto.json"), Path.Combine(FBImport.Src, "anims_171_eventos.json") })
            if (File.Exists(fmo)) foreach (var e in Json.L(Json.Parse(File.ReadAllText(fmo)), "clips") ?? new List<object>())
                {
                    var name = Json.S(e, "name"); var d = Json.Floats(Json.L(e, "d")); if (d.Length < 102 || !seen.Add(name)) continue;   // nomes repetidos (piloto × garupa): o 1.º
                    MakeClip("X171_" + name, Json.F(e, "fps", 20), d, .001f, !MOTO_ONCE.IsMatch(name)); n++;
                }
            // r45: CARRO do 171 (entrar/sair pela porta do motorista e do carona, dirigir, volante, ré, colisão, morto ao volante):
            // assets/anims_171_carro.json (ue_anim export Characters/Animations/Vehicle/Car + tools_retarget FULL) — entrar/sair/colisão nunca em laço
            var fca = Path.Combine(FBImport.Src, "anims_171_carro.json");
            if (File.Exists(fca)) foreach (var e in Json.L(Json.Parse(File.ReadAllText(fca)), "clips") ?? new List<object>())
                {
                    var name = Json.S(e, "name"); var d = Json.Floats(Json.L(e, "d")); if (d.Length < 102 || !seen.Add(name)) continue;
                    bool loop = System.Text.RegularExpressions.Regex.IsMatch(name, "Idle|Dirigindo_Assustado|Girando_Volante_.*Baixa|Motorista_MarchaRe|Virando_Volante") && !name.Contains("para_");
                    MakeClip("X171_" + name, Json.F(e, "fps", 20), d, .001f, loop); n++;
                }
            // r43: rotina dos PEDESTRES do 171 (andar no celular/fumando/bebendo/olhando em volta, parado com relógio/celular/cigarro/braços
            // cruzados, conversa falando/ouvindo, sentado, susto (recuar/cair/fugir), esbarrões por direção, gestos): assets/anims_171_npc.json
            var fn = Path.Combine(FBImport.Src, "anims_171_npc.json");
            if (File.Exists(fn)) foreach (var e in Json.L(Json.Parse(File.ReadAllText(fn)), "clips") ?? new List<object>())
                {
                    var name = Json.S(e, "name"); var d = Json.Floats(Json.L(e, "d")); if (d.Length < 102 || !seen.Add(name)) continue;
                    var id = "X171_" + name; MakeClip(id, Json.F(e, "fps", 20), d, .001f, !NPC_ONCE.IsMatch(name)); n++;
                    if (!measuredRS.ContainsKey(id) && Json.F(e, "rs", 0) > 0) measuredRS[id] = Json.F(e, "rs", 0);
                }
            // r48: clipes de VEÍCULO (entrar/sair do carro, subir/descer/pilotar a moto) refeitos pelo retarget NATIVO do Unity a partir dos GLBs
            // (FBRetargetUE: erro médio por junta 7,3 cm -> 0) — por cima dos do JS acima, para um reimport não trazer de volta os quebrados
            FBRetargetUE.Run(null, true);
            // velocidades naturais somadas às que já existem
            var rsPath = "Assets/FreeBala/Resources/FBClipRS.json"; var all = File.Exists(rsPath) ? Json.Parse(File.ReadAllText(rsPath)) as Dictionary<string, object> ?? new() : new();
            foreach (var kv in measuredRS) all[kv.Key] = (double)kv.Value;
            File.WriteAllText(rsPath, "{" + string.Join(",", all.Select(kv => "\"" + kv.Key.Replace("\"", "") + "\":" + System.Convert.ToDouble(kv.Value, System.Globalization.CultureInfo.InvariantCulture).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture))) + "}");
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            var reg = AssetDatabase.LoadAssetAtPath<FBRegistry>("Assets/FreeBala/Resources/FBRegistry.asset");
            if (reg) { reg.clips = Directory.GetFiles(ClipDir, "*.anim").Select(p => AssetDatabase.LoadAssetAtPath<AnimationClip>(p.Replace(Path.DirectorySeparatorChar, '/'))).Where(c => c).ToList(); EditorUtility.SetDirty(reg); AssetDatabase.SaveAssets(); }
            Debug.Log($"[FreeBala] {n} clipes do 171 importados (X171_*); registro com {(reg ? reg.clips.Count : 0)} clipes");
        }

        static readonly Dictionary<string, float> measuredRS = new();
        public static AnimationClip MakeClip(string name, float fps, float[] d, float scale, bool loop, bool smooth = false)
        {
            var muscles = HumanTrait.MuscleName; int frames = d.Length / 102;
            // loops "no lugar" (sample do humanoid.js): tira a deriva LINEAR da raiz, mantendo o balanço dentro do ciclo. Os loops do pacote
            // de tiro traziam até 2,6–3,7 m de translação por ciclo; com Bake Into Pose XZ o corpo andava à frente do GameObject (CharacterController
            // e câmera) e voltava de uma vez no loop — o corpo "fugia" da câmera e os pés patinavam. O deslocamento vem do controle.
            if (loop && frames > 1)
            {
                d = (float[])d.Clone(); int L = (frames - 1) * 102; float dx = d[L] - d[0], dz = d[L + 2] - d[2];
                float len = (frames - 1) / fps, rs = Mathf.Sqrt(dx * dx + dz * dz) * scale / Mathf.Max(1e-3f, len);
                if (rs > 1e-3f) measuredRS[name] = rs;
                for (int f = 0; f < frames; f++) { float u = f / (float)(frames - 1); d[f * 102] -= dx * u; d[f * 102 + 2] -= dz * u; }
            }
            var clip = new AnimationClip { name = name, frameRate = fps };
            var bindings = new List<EditorCurveBinding>(); var curves = new List<AnimationCurve>();
            for (int ch = 0; ch < 102; ch++)
            {
                string prop = ch < 7 ? RootCh[ch] : (ch - 7 < muscles.Length ? muscles[ch - 7] : null); if (prop == null) continue;
                var keys = new Keyframe[frames];
                for (int f = 0; f < frames; f++) keys[f] = new Keyframe(f / fps, d[f * 102 + ch] * scale);
                for (int f = 0; f < frames; f++)                                       // tangentes lineares (como o sampler do web)
                {
                    float tin = f > 0 ? (keys[f].value - keys[f - 1].value) * fps : 0, tout = f < frames - 1 ? (keys[f + 1].value - keys[f].value) * fps : tin;
                    if (smooth && f > 0 && f < frames - 1) tin = tout = (keys[f + 1].value - keys[f - 1].value) * fps * .5f;   // poucas poses: Catmull-Rom (sem quina a cada 0,2 s)
                    keys[f].inTangent = f > 0 ? tin : tout; keys[f].outTangent = tout;
                }
                bindings.Add(EditorCurveBinding.FloatCurve("", typeof(Animator), prop)); curves.Add(new AnimationCurve(keys));
            }
            AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
            var st = AnimationUtility.GetAnimationClipSettings(clip);
            st.loopTime = loop;
            // emenda do loop (rodada 37): o HumanoidIdle (parado desarmado do Complexo) termina com os dedos 2,5 unidades de músculo longe (HumanoidRun: ombro 0,6)
            // do começo — a mão "estalava" a cada volta. Só os loops com emenda real nos músculos ganham o "Loop Pose" do Unity (espalha a
            // diferença pelo ciclo); os outros ficam idênticos ao original.
            if (loop && frames > 1) { float seam = 0; for (int c = 7; c < 102; c++) seam = Mathf.Max(seam, Mathf.Abs(d[(frames - 1) * 102 + c] - d[c]) * scale); st.loopBlend = seam > .3f; }   // × scale: emotes/sistema vêm ×1000
            st.loopBlendOrientation = true; st.loopBlendPositionY = true; st.loopBlendPositionXZ = true;   // "no lugar": o deslocamento vem do controle, não do clipe
            st.keepOriginalOrientation = true; st.keepOriginalPositionY = true; st.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, st);
            var path = $"{ClipDir}/{Safe(name)}.anim";
            var old = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (old) { EditorUtility.CopySerialized(clip, old); return old; }                // mantém o GUID (controllers/prefabs não quebram)
            AssetDatabase.CreateAsset(clip, path); return clip;
        }

        // ---- máscaras: o controller guarda a máscara humana do Mecanim em 3 inteiros; "tudo ligado" = corpo inteiro, o resto = metade de cima
        // HumanPoseMask original (maskMuscles do mecanim.js): bit 0 = raiz; bits 1..55 = músculos 0..54; 60/61 = dedos esq./dir.
        // ATENÇÃO: os valores vêm como double; converter por float perdia precisão (4294967295 virava 4294967296 -> 0) e TODA camada
        // (inclusive a base) recebia a máscara de "parte de cima" — pernas e raiz sem animação, personagem afundado no chão.
        static AvatarMask Mask(string ctrlName, string layer, List<object> bits)
        {
            if (bits == null || bits.Count < 2) return null;
            var w = bits.Select(o => o is double d ? (uint)(long)d : 0u).ToArray();
            bool Bit(int i) => (i >> 5) < w.Length && ((w[i >> 5] >> (i & 31)) & 1) != 0;
            bool Range(int a0, int a1) { int on = 0; for (int i = a0; i <= a1; i++) if (Bit(1 + i)) on++; return on * 2 > a1 - a0 + 1; }
            var parts = new Dictionary<AvatarMaskBodyPart, bool>
            {
                [AvatarMaskBodyPart.Root] = Bit(0), [AvatarMaskBodyPart.Body] = Range(0, 8), [AvatarMaskBodyPart.Head] = Range(9, 20),
                [AvatarMaskBodyPart.LeftLeg] = Range(21, 28), [AvatarMaskBodyPart.RightLeg] = Range(29, 36), [AvatarMaskBodyPart.LeftArm] = Range(37, 45), [AvatarMaskBodyPart.RightArm] = Range(46, 54),
                [AvatarMaskBodyPart.LeftFingers] = Bit(60), [AvatarMaskBodyPart.RightFingers] = Bit(61),
            };
            parts[AvatarMaskBodyPart.LeftFootIK] = parts[AvatarMaskBodyPart.LeftLeg]; parts[AvatarMaskBodyPart.RightFootIK] = parts[AvatarMaskBodyPart.RightLeg];
            parts[AvatarMaskBodyPart.LeftHandIK] = parts[AvatarMaskBodyPart.LeftArm]; parts[AvatarMaskBodyPart.RightHandIK] = parts[AvatarMaskBodyPart.RightArm];
            if (parts.Values.All(v => v)) return null;                                          // máscara cheia: camada sem máscara
            var m = new AvatarMask { name = layer + "_Mask" };
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) { var p = (AvatarMaskBodyPart)i; m.SetHumanoidBodyPartActive(p, parts.TryGetValue(p, out var on) && on); }
            var path = $"{CtrlDir}/{Safe(ctrlName)}_{Safe(layer)}.mask";
            AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(m, path);
            Debug.Log($"[FreeBala] máscara {ctrlName}/{layer}: " + string.Join(" ", parts.Where(kv => kv.Value).Select(kv => kv.Key)));
            return m;
        }

        // ---- controller
        public const string LocoSpeed = "FB_LocoSpeed";
        public static AnimatorController ImportController(string name, Dictionary<string, object> c, Dictionary<string, AnimationClip> clips)
        {
            Directory.CreateDirectory(CtrlDir);
            var path = $"{CtrlDir}/{Safe(name)}.controller";
            // mesmo arquivo/GUID (rodada 37): os prefabs dos personagens apontam para este controller — apagar e recriar deixava a referência
            // perdida até um import completo; agora limpa camadas, parâmetros e subobjetos (máquinas de estado, árvores) e remonta
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ctrl)
            {
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o && o != ctrl && !(o is AnimatorController)) AssetDatabase.RemoveObjectFromAsset(o);
                ctrl.layers = new AnimatorControllerLayer[0]; ctrl.parameters = new AnimatorControllerParameter[0];
            }
            else ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            while (ctrl.layers.Length > 0) ctrl.RemoveLayer(0);
            foreach (var kv in Json.O(c, "params"))
            {
                var t = Json.S(kv.Value, "t"); var p = new AnimatorControllerParameter { name = kv.Key };
                p.type = t == "float" ? AnimatorControllerParameterType.Float : t == "int" ? AnimatorControllerParameterType.Int : t == "bool" ? AnimatorControllerParameterType.Bool : AnimatorControllerParameterType.Trigger;
                if (p.type == AnimatorControllerParameterType.Float) p.defaultFloat = Json.F(kv.Value, "v");
                else if (p.type == AnimatorControllerParameterType.Int) p.defaultInt = Json.I(kv.Value, "v");
                else if (p.type == AnimatorControllerParameterType.Bool) p.defaultBool = Json.B(kv.Value, "v");
                ctrl.AddParameter(p);
            }
            var sms = Json.L(c, "sms");
            foreach (var lo in Json.L(c, "layers"))
            {
                string ln = Json.S(lo, "name"); var smo = sms[Json.I(lo, "sm")];
                var sm = new AnimatorStateMachine { name = ln, hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(sm, ctrl);
                var layer = new AnimatorControllerLayer
                {
                    name = ln, stateMachine = sm, defaultWeight = ctrl.layers.Length == 0 ? 1 : Json.F(lo, "w"), iKPass = Json.B(lo, "ik") || ctrl.layers.Length == 0,   // base com IK: pés no chão (OnAnimatorIK)
                    blendingMode = Json.I(lo, "blend") == 1 ? AnimatorLayerBlendingMode.Additive : AnimatorLayerBlendingMode.Override,
                    avatarMask = Mask(name, ln, Json.L(lo, "mask")),
                };
                ctrl.AddLayer(layer);
                var stList = Json.L(smo, "states"); var states = new List<AnimatorState>(); int i = 0;
                foreach (var so in stList)
                {
                    var st = sm.AddState(Json.S(so, "name"), new Vector3(300 + (i % 4) * 230, 80 + (i / 4) * 70, 0)); i++;
                    st.speed = Json.F(so, "speed", 1); st.mirror = Json.B(so, "mirror"); st.cycleOffset = Json.F(so, "cyc"); st.iKOnFeet = Json.B(so, "footIK");
                    var sp = Json.S(so, "speedP"); if (!string.IsNullOrEmpty(sp)) { st.speedParameterActive = true; st.speedParameter = sp; }
                    st.motion = Motion(ctrl, Json.Get(so, "motion"), clips);
                    // casamento de passo SÓ na locomoção da camada base (como o speedMatch do web): o FBCharacter escreve FB_LocoSpeed;
                    // antes o jogo mexia no Animator.speed e acelerava também recarga/tiro/emotes das outras camadas
                    if (ctrl.layers.Length == 1 && st.motion is BlendTree && !st.speedParameterActive)
                    {
                        if (!ctrl.parameters.Any(p => p.name == LocoSpeed)) ctrl.AddParameter(new AnimatorControllerParameter { name = LocoSpeed, type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
                        st.speedParameterActive = true; st.speedParameter = LocoSpeed;
                    }
                    states.Add(st);
                }
                int def = Json.I(smo, "def"); if (def >= 0 && def < states.Count) sm.defaultState = states[def];
                for (int s = 0; s < states.Count; s++)
                    foreach (var to in Json.L(stList[s], "tr") ?? new List<object>())
                    {
                        var dst = Target(states, to, out bool exit);
                        if (exit) Trans(states[s].AddExitTransition(), to, false); else if (dst) Trans(states[s].AddTransition(dst), to, false);
                    }
                foreach (var to in Json.L(smo, "any") ?? new List<object>()) { var dst = Target(states, to, out _); if (dst) Trans(sm.AddAnyStateTransition(dst), to, true); }
                // segundo estado com outro placeholder: troca de emote / pose de sistema com mistura (CrossFade A <-> B), como o SYS_FADE do web
                foreach (var st in states.ToArray())
                    if (st.motion is AnimationClip pc && pc.name.Contains("PLACEHOLDER"))
                    {
                        var b = sm.AddState(st.name + "B", new Vector3(300, 420, 0)); b.motion = Placeholder(pc.name + "_B");
                        b.speed = st.speed; b.speedParameterActive = st.speedParameterActive; b.speedParameter = st.speedParameter; b.iKOnFeet = st.iKOnFeet;
                    }
            }
            if (name.StartsWith("EnemyAnimator_AAA")) AdaptMain(ctrl, clips);
            EditorUtility.SetDirty(ctrl); AssetDatabase.SaveAssets();
            return ctrl;
        }

        // ---- RODADA 37: Complexo Favela como base de TODA a locomoção (o controller dele, com pequenas adaptações de configuração):
        //  1) cada clipe das árvores de locomoção vai para a posição da SUA velocidade natural (deriva da raiz medida no import), convertida
        //     pelo mesmo remapSpeed do PlayerVisualPuppet; a direção de cada filho é mantida. Antes: andar armado (3,4 m/s) caía no clipe de
        //     1,5 m/s, o ritmo travava em 1,45× e o pé deslizava (o Complexo original não casava passo com velocidade);
        //     (tentativa descartada: trocar a corrida lateral da CombatLocomotion por RunRtLoop/RunLtLoop — são corridas "relaxadas" com
        //     outra pegada; o laboratório mostrou a mão esquerda a 40 cm e estalos de 5.700°/s);
        //  2) (22/10) RelaxedLocomotion e as vagas de corrida armada ficam com as posições originais: tocam o conjunto aprovado antes da
        //     rodada (corridas do IE, FBAnimProfile) — a troca pelo PedestrianAnimator do Complexo foi rejeitada no teste do usuário.
        public const float HumanK = 1.23f;   // m/s por unidade de velocidade do clipe (humanScale ~0,56 × escala 2,2 dos personagens)
        static float Remap(float s)
        {
            var P = GameData.Section("puppet"); float pw = Json.F(P, "playerWalkSpeed", 4), pr = Json.F(P, "playerRunSpeed", 6.8f), aw = Json.F(P, "aiWalkAnimSpeed", 2), ar = Json.F(P, "aiRunAnimSpeed", 5);
            return s <= pw ? s / pw * aw : aw + (s - pw) / (pr - pw) * (ar - aw);
        }
        static float NaturalPos(string clip)
        {
            if (!measuredRS.TryGetValue(clip, out var rs))
            {
                var ta = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/FreeBala/Resources/FBClipRS.json"); var all = ta ? Json.Parse(ta.text) as Dictionary<string, object> : null;
                rs = all != null && all.TryGetValue(clip, out var v) ? Json.Fv(v) : 0;
            }
            return rs > .05f ? Remap(rs * HumanK) : 0;
        }
        static void AdaptMain(AnimatorController ctrl, Dictionary<string, AnimationClip> clips)
        {
            GameData.Load(); var sm = ctrl.layers[0].stateMachine; var log = new List<string>();
            AnimationClip C(string n) => clips.TryGetValue(n, out var c) && c ? c : null;
            foreach (var cs in sm.states)
            {
                var st = cs.state; if (!(st.motion is BlendTree bt) || bt.blendType == BlendTreeType.Simple1D) continue;
                var kids = bt.children.ToList();
                // [parceiro 31/10] CombatLocomotion com corrida de lado e na diagonal para trás de verdade (antes o "de lado" tocava a diagonal
                // para frente e o pé escorregava): as diagonais da frente vão para 45° e entram 5 vagas; armado, o FBAnimProfile.ArmedRun troca
                // todas pelas corridas do IE (IE_Rifle_Run_R/L/BR/BL, IE_Rifle_Sprint_F). Chaves do ArmedRun: o laço abaixo não mexe nelas.
                if (st.name == "CombatLocomotion" && !kids.Any(k => k.motion && k.motion.name == "RunRtLoop"))
                {
                    for (int k = 0; k < kids.Count; k++)
                    {
                        var ch = kids[k]; if (!ch.motion) continue;
                        if (ch.motion.name == "Run Forward Right") { ch.position = new Vector2(1.7465f, 1.7465f); kids[k] = ch; }
                        if (ch.motion.name == "Run Forward Left") { ch.position = new Vector2(-1.7465f, 1.7465f); kids[k] = ch; }
                    }
                    foreach (var (n, p) in new[] { ("RunRtLoop", new Vector2(2.4f, 0)), ("RunLtLoop", new Vector2(-2.4f, 0)), ("RunStrafeRight135Loop", new Vector2(1.8668f, -1.8668f)),
                                                   ("RunStrafeLeft135Loop", new Vector2(-1.8668f, -1.8668f)), ("Rifle_SprintLoop", new Vector2(0, 5)) })
                        if (C(n)) kids.Add(new ChildMotion { motion = C(n), position = p, timeScale = 1 });   // timeScale à mão: o padrão do ChildMotion é 0
                }
                // 22/10 (teste do usuário): a corrida aprovada antes da rodada voltou (FBAnimProfile.Unarmed/ArmedRun) — essas vagas ficam nas
                // posições ORIGINAIS do controller (como antes; a árvore relaxada inteira, com as diagonais). Só os clipes do próprio Complexo
                // que continuam tocando (andar de combate, agachado) vão para a velocidade natural.
                for (int k = 0; k < kids.Count; k++)
                {
                    var ch = kids[k]; if (!ch.motion || ch.position.sqrMagnitude < 1e-4f) continue;
                    // 22/10: anel de ANDAR da relaxada na velocidade de andar desarmado do jogo (FBAnimProfile.UnarmedWalkSpeed) — toca só o
                    // andar novo (REG_Walk_M/F), sem misturar com a corrida; o anel de corrida/sprint fica na posição original
                    if (st.name == "RelaxedLocomotion")
                    {
                        if (System.Text.RegularExpressions.Regex.IsMatch(ch.motion.name, @"^(WalkFwdLoop|WalkBwdLoop|StrafeRightLoop|StrafeLeftLoop|StrafeRight45Loop|StrafeLeft45Loop|StrafeRight135Loop|StrafeLeft135Loop)$"))
                        { ch.position = ch.position.normalized * Remap(FBAnimProfile.UnarmedWalkSpeed); kids[k] = ch; }
                        continue;
                    }
                    if (FBAnimProfile.ArmedRun.ContainsKey(ch.motion.name)) continue;
                    string playing = ch.motion.name;
                    float r = NaturalPos(playing); if (r <= 0) continue;
                    ch.position = ch.position.normalized * r; kids[k] = ch;
                }
                bt.children = kids.ToArray();
                log.Add(st.name + ": " + string.Join(", ", kids.Select(k => $"{(k.motion ? k.motion.name : "-")}@({k.position.x:0.00},{k.position.y:0.00})")));
            }
            Debug.Log("[FreeBala][r37] controller do Complexo adaptado:\n  " + string.Join("\n  ", log));
        }

        public static AnimationClip Placeholder(string name)
        {
            var path = $"{ClipDir}/{Safe(name)}.anim"; var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(path); if (c) return c;
            c = new AnimationClip { name = name, frameRate = 30 };
            AnimationUtility.SetEditorCurve(c, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.y"), AnimationCurve.Constant(0, 1f / 30, 1));
            AssetDatabase.CreateAsset(c, path); return c;
        }

        static AnimatorState Target(List<AnimatorState> states, object to, out bool exit) { int t = Json.I(to, "to", -1); exit = t < 0; return t >= 0 && t < states.Count ? states[t] : null; }

        static void Trans(AnimatorStateTransition t, object o, bool any)
        {
            if (t == null) return;
            t.duration = Json.F(o, "dur"); t.hasFixedDuration = Json.B(o, "fixed", true); t.hasExitTime = Json.B(o, "exit"); t.exitTime = Json.F(o, "et");
            t.offset = Json.F(o, "off"); t.interruptionSource = (TransitionInterruptionSource)Json.I(o, "int"); t.orderedInterruption = Json.B(o, "ord", true);
            if (any) t.canTransitionToSelf = Json.B(o, "self");
            foreach (var co in Json.L(o, "cond") ?? new List<object>())
            {
                var c = co as List<object>; if (c == null || c.Count < 3) continue;
                var mode = c[0] as string; var p = c[1] as string; float th = Json.Fv(c[2]);
                var m = mode == "if" ? AnimatorConditionMode.If : mode == "ifnot" ? AnimatorConditionMode.IfNot : mode == "gt" ? AnimatorConditionMode.Greater
                      : mode == "lt" ? AnimatorConditionMode.Less : mode == "eq" ? AnimatorConditionMode.Equals : AnimatorConditionMode.NotEqual;
                t.AddCondition(m, th, p);
            }
        }

        static Motion Motion(AnimatorController ctrl, object mo, Dictionary<string, AnimationClip> clips)
        {
            if (mo == null) return null;
            var clipName = Json.S(mo, "clip");
            if (clipName != null && Json.Get(mo, "type") == null)
            {
                clips.TryGetValue(clipName, out var c);
                if (!c && clipName.Contains("PLACEHOLDER")) c = clips[clipName] = Placeholder(clipName);         // trocado em tempo de execução (AnimatorOverrideController)
                if (!c) Debug.LogWarning("[FreeBala] clipe ausente: " + clipName); return c;
            }
            var type = Json.S(mo, "type"); var kids = Json.L(mo, "kids"); if (kids == null) return null;
            var bt = new BlendTree { name = "Blend " + (Json.S(mo, "p") ?? ""), hideFlags = HideFlags.HideInHierarchy, useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(bt, ctrl);
            bt.blendType = type == "1d" ? BlendTreeType.Simple1D : type == "2d_freeform_cart" ? BlendTreeType.FreeformCartesian2D : type == "2d_simple" ? BlendTreeType.SimpleDirectional2D : BlendTreeType.FreeformDirectional2D;
            bt.blendParameter = Json.S(mo, "p") ?? "Speed"; if (Json.S(mo, "py") != null) bt.blendParameterY = Json.S(mo, "py");
            var pos = Json.L(mo, "pos"); var th = Json.L(mo, "th");
            var ch = new ChildMotion[kids.Count];
            for (int k = 0; k < kids.Count; k++)
            {
                var kid = kids[k]; var m = Motion(ctrl, kid, clips);
                ch[k] = new ChildMotion { motion = m, timeScale = Json.F(kid, "speed", 1), mirror = Json.B(kid, "mirror"), cycleOffset = Json.F(kid, "off"), directBlendParameter = "Speed" };
                if (bt.blendType == BlendTreeType.Simple1D) ch[k].threshold = th != null && k < th.Count ? Json.Fv(th[k]) : k;
                else if (pos != null && k < pos.Count && pos[k] is List<object> p2) ch[k].position = new Vector2(Json.Fv(p2[0]), Json.Fv(p2[1]));
            }
            bt.children = ch;
            return bt;
        }
    }
}
