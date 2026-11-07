// RODADA 55 (04/11/2026) — POLICIAIS TÁTICOS: pacote de animações do Mixamo do usuário
// (F:\CRIAÇÃO DE GAMES ASSETS\PERSONAGENS REGIONAIS\ANIMAÇÕES\MIXAMO: 8 pacotes, 182 clipes + o personagem NARC em cada um).
// Retarget NATIVO do Unity (Humanoid): o NARC.fbx (esqueleto mixamorig em pose T) vira o Avatar de origem e todo clipe é importado
// com "Copy From Other Avatar" = o mesmo caminho que deu erro 0 por junta nos clipes de veículo da r48 (FBRetargetUE), em vez do
// retarget JS (src/tools_retarget.mjs) que hiperestendia dedos e travava ombros. Cada clipe sai como .anim POL_<pacote>_<nome> em
// Anim/Clips (a pasta que todos os importadores usam para montar o registro — um reimport não perde os clipes) e:
//   • loops ficam "no lugar" com a MESMA regra do FBAnimImport.MakeClip: deriva LINEAR da raiz tirada (o balanço do ciclo fica) e a
//     velocidade natural (rs, unidades do Humanoid) vai para Resources/FBClipRS.json → speedMatch do FBCharacter (LocoRate);
//   • emenda do loop > 0,3 de músculo ganha Loop Pose; quedas/reação/transições mantêm o deslocamento no corpo (Bake Into Pose);
//   • pacotes repetidos (Lite ⊂ Pro, 8-Way = Pro, Slim ⊂ Basic, metade do Shooter = Basic) são comparados quadro a quadro pelos
//     músculos e o repetido NÃO vira clipe (relatório diz de quem é cópia);
//   • quedas medidas no corpo de um policial (quadril: frente/direita em metros do corpo, chegada ao chão, de costas/de bruços) e
//     somadas ao Resources/FBDeathLib.json com "pol": true (FBDeath só as usa em policial).
// Lote: Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBPoliciaMixamo.Batch
// Relatório: unity/logs/r55_mixamo.txt + unity/logs/r55_mixamo.json (por clipe: pacote, duração, loop, rs, giro, emenda, músculo máximo, cópia).
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FreeBala.EditorTools
{
    public static class FBPoliciaMixamo
    {
        public const string Src = @"F:\CRIAÇÃO DE GAMES ASSETS\PERSONAGENS REGIONAIS\ANIMAÇÕES\MIXAMO";
        const string Stage = "Assets/BURITIRO_Animacoes/_r55_mixamo_stage";       // só durante o import (apagada no fim: os .anim ficam)
        const string Prefix = "POL_";
        // ordem = prioridade na deduplicação (o primeiro fica, os iguais depois dele são marcados como cópia)
        static readonly (string dir, string code)[] Packs =
        {
            ("Pro Rifle Pack", "PR"), ("Rifle 8-Way Locomotion Pack", "R8"), ("Lite Rifle Pack", "LR"),
            ("Basic Shooter Pack", "BS"), ("Shooter Pack", "SH"), ("Slim Shooter Pack", "SL"),
            ("Pistol_Handgun Locomotion Pack", "PI"), ("Locomotion Pack", "LO"),
        };
        // clipes que NÃO repetem (ação única); o resto é ciclo
        static readonly string[] OneShot = { "death", "dying", "hit_reaction", "reloading", "toss_grenade", "jump_up", "jump_down", "jump_forward", "jump_backward",
            "rifle_jump", "pistol_jump", "turn", "start_walking", "stop_walking", "walk_backwards_stop", "stand_to_kneel", "kneel_to_stand" };
        static bool IsLoop(string slug) => slug == "jump_loop" || (slug != "jump" && !OneShot.Any(k => slug.Contains(k)));   // "jump" do Locomotion Pack = pulo inteiro → um só
        public static string Slug(string file) => Path.GetFileNameWithoutExtension(file).ToLowerInvariant().Replace("(", "").Replace(")", "").Replace(" ", "_").Replace("__", "_").Trim('_');
        static string LogDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../logs"));
        static readonly StringBuilder log = new();
        static void L(string s) { Debug.Log("[FreeBala][r55] " + s); log.AppendLine(s); }

        public static void Batch()
        {
            int code = 0;
            try { Run(); } catch (System.Exception e) { Debug.LogError("[FreeBala][r55] " + e); L("ERRO " + e); code = 1; }
            Directory.CreateDirectory(LogDir); File.WriteAllText(Path.Combine(LogDir, "r55_mixamo.txt"), log.ToString());
            EditorApplication.Exit(code);
        }

        class Info { public string name, pack, file, slug, dupOf; public float len, rs, turn, seam, maxMus; public string maxMusName; public bool loop; public float[] sig; }

        public static void Run()
        {
            if (!Directory.Exists(Src)) throw new System.Exception("pasta do pacote não encontrada: " + Src);
            // 1) cópia para o projeto (Unity só importa dentro de Assets): o NARC uma vez, os clipes por pacote
            if (AssetDatabase.IsValidFolder(Stage)) AssetDatabase.DeleteAsset(Stage);
            Directory.CreateDirectory(Stage);
            File.Copy(Path.Combine(Src, Packs[0].dir, "NARC.fbx"), Stage + "/NARC.fbx", true);
            var infos = new List<Info>();
            foreach (var (dir, code) in Packs)
            {
                var pd = Path.Combine(Src, dir); if (!Directory.Exists(pd)) { L($"pacote ausente: {dir}"); continue; }
                Directory.CreateDirectory($"{Stage}/{code}");
                foreach (var f in Directory.GetFiles(pd, "*.fbx").OrderBy(x => x))
                {
                    if (Path.GetFileName(f).Equals("NARC.fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                    var slug = Slug(f); File.Copy(f, $"{Stage}/{code}/{slug}.fbx", true);
                    infos.Add(new Info { pack = code, file = $"{Stage}/{code}/{slug}.fbx", slug = slug, name = $"{Prefix}{code}_{slug}", loop = IsLoop(slug) });
                }
            }
            L($"copiados: NARC + {infos.Count} clipes de {Packs.Length} pacotes");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 2) NARC = avatar de origem (pose T do Mixamo)
            var narc = (ModelImporter)AssetImporter.GetAtPath(Stage + "/NARC.fbx");
            narc.animationType = ModelImporterAnimationType.Human; narc.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            narc.importAnimation = false; narc.materialImportMode = ModelImporterMaterialImportMode.None; narc.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(Stage + "/NARC.fbx").OfType<Avatar>().FirstOrDefault();
            if (!avatar || !avatar.isHuman || !avatar.isValid) throw new System.Exception("avatar do NARC inválido");
            L($"avatar do NARC: humano válido ({avatar.humanDescription.human.Length} ossos mapeados)");

            // 3) clipes: Humanoid copiando o avatar do NARC
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var it in infos)
                {
                    var mi = (ModelImporter)AssetImporter.GetAtPath(it.file);
                    mi.animationType = ModelImporterAnimationType.Human; mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther; mi.sourceAvatar = avatar;
                    mi.importAnimation = true; mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.importBlendShapes = false;
                    mi.importCameras = false; mi.importLights = false; mi.resampleCurves = true; mi.animationCompression = ModelImporterAnimationCompression.Off;
                    var dc = mi.defaultClipAnimations; if (dc.Length == 0) { L($"  {it.name}: FBX sem take"); continue; }
                    var c = dc[0]; c.name = it.name; c.loopTime = it.loop; c.loopPose = false;
                    c.lockRootRotation = true; c.keepOriginalOrientation = true; c.lockRootHeightY = true; c.keepOriginalPositionY = true; c.heightFromFeet = false;
                    c.lockRootPositionXZ = false; c.keepOriginalPositionXZ = true;   // a deriva fica nas curvas RootT (medida e tirada abaixo)
                    mi.clipAnimations = new[] { c };
                    AssetDatabase.WriteImportSettingsIfDirty(it.file);
                    AssetDatabase.ImportAsset(it.file, ImportAssetOptions.ForceUpdate);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }

            // 4) .anim no lugar + medidas + deduplicação
            var rsPath = "Assets/FreeBala/Resources/FBClipRS.json";
            var allRS = File.Exists(rsPath) ? Json.Parse(File.ReadAllText(rsPath)) as Dictionary<string, object> ?? new() : new();
            var made = new List<Info>(); var muscles = HumanTrait.MuscleName;
            foreach (var it in infos)
            {
                var src = AssetDatabase.LoadAllAssetsAtPath(it.file).OfType<AnimationClip>().FirstOrDefault(x => !x.name.StartsWith("__preview__"));
                if (!src || !src.humanMotion) { L($"  {it.name}: sem clipe humanoide (não importado)"); continue; }
                var clip = Object.Instantiate(src); clip.name = it.name;
                var binds = AnimationUtility.GetCurveBindings(clip);
                AnimationCurve Get(string p) { var b = binds.FirstOrDefault(x => x.propertyName == p); return b.propertyName == p ? AnimationUtility.GetEditorCurve(clip, b) : null; }
                it.len = clip.length;
                // assinatura (músculos em 12 tempos) para achar pacotes repetidos
                var mcur = muscles.Select(Get).ToArray(); it.sig = new float[12 * muscles.Length];
                for (int k = 0; k < 12; k++) for (int m = 0; m < muscles.Length; m++) it.sig[k * muscles.Length + m] = mcur[m]?.Evaluate(it.len * k / 11f) ?? 0;
                var twin = made.FirstOrDefault(o => o.slug == it.slug && Mathf.Abs(o.len - it.len) < .05f && Sig(o.sig, it.sig) < .02f)
                        ?? made.FirstOrDefault(o => Mathf.Abs(o.len - it.len) < .02f && Sig(o.sig, it.sig) < .005f);
                if (twin != null) { it.dupOf = twin.name; L($"  {it.name}: cópia de {twin.name} (não gera clipe)"); Object.DestroyImmediate(clip); continue; }
                // músculo máximo e emenda do loop
                for (int m = 0; m < muscles.Length; m++)
                {
                    var cv = mcur[m]; if (cv == null || cv.length == 0) continue;
                    foreach (var kf in cv.keys) if (Mathf.Abs(kf.value) > it.maxMus) { it.maxMus = Mathf.Abs(kf.value); it.maxMusName = muscles[m]; }
                    if (it.loop) it.seam = Mathf.Max(it.seam, Mathf.Abs(cv.Evaluate(it.len) - cv.Evaluate(0)));
                }
                // giro do corpo no clipe (graus) — curvas de virar e "arc" giram; o resto deveria ficar perto de 0
                var qx = Get("RootQ.x"); var qy = Get("RootQ.y"); var qz = Get("RootQ.z"); var qw = Get("RootQ.w");
                if (qw != null) { var q0 = Q(qx, qy, qz, qw, 0); var q1 = Q(qx, qy, qz, qw, it.len); it.turn = Mathf.DeltaAngle((q0 * Vector3.forward).Yaw(), (q1 * Vector3.forward).Yaw()); }
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                var tx = Get("RootT.x"); var tz = Get("RootT.z");
                if (it.loop && tx != null && tz != null && it.len > 0)
                {   // no lugar: deriva linear da raiz (e dos alvos de IK, mesmo espaço) tirada — o balanço do ciclo fica
                    float dx = tx.Evaluate(it.len) - tx.Evaluate(0), dz = tz.Evaluate(it.len) - tz.Evaluate(0);
                    it.rs = Mathf.Sqrt(dx * dx + dz * dz) / it.len;
                    foreach (var b in binds.Where(b => b.propertyName.EndsWith("T.x") || b.propertyName.EndsWith("T.z")))
                    {
                        float d = b.propertyName.EndsWith(".x") ? dx : dz; var cv = AnimationUtility.GetEditorCurve(clip, b);
                        var ks = cv.keys; for (int i = 0; i < ks.Length; i++) { ks[i].value -= d * ks[i].time / it.len; ks[i].inTangent -= d / it.len; ks[i].outTangent -= d / it.len; }
                        cv.keys = ks; AnimationUtility.SetEditorCurve(clip, b, cv);
                    }
                    if (it.rs > 1e-3f) allRS[it.name] = (double)it.rs; else allRS.Remove(it.name);
                    settings.loopBlend = it.seam > .3f;
                }
                settings.loopTime = it.loop;
                settings.loopBlendOrientation = true; settings.loopBlendPositionY = true; settings.loopBlendPositionXZ = true;   // tudo no corpo: o deslocamento vem do controle
                settings.keepOriginalOrientation = true; settings.keepOriginalPositionY = true; settings.keepOriginalPositionXZ = true; settings.heightFromFeet = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                var path = $"{FBAnimImport.ClipDir}/{it.name}.anim";
                var old = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (old) { EditorUtility.CopySerialized(clip, old); Object.DestroyImmediate(clip); clip = old; EditorUtility.SetDirty(old); } else AssetDatabase.CreateAsset(clip, path);   // mantém o GUID num reimport
                made.Add(it);
                L($"  {it.name}: {it.len:0.00} s · {(it.loop ? "loop" : "único")} · rs {it.rs:0.00} · giro {it.turn:0}° · emenda {it.seam:0.00}{(settings.loopBlend ? " (Loop Pose)" : "")} · músculo máx {it.maxMus:0.00} {it.maxMusName}");
            }
            File.WriteAllText(rsPath, "{" + string.Join(",", allRS.Select(kv => "\"" + kv.Key.Replace("\"", "") + "\":" + System.Convert.ToDouble(kv.Value, CultureInfo.InvariantCulture).ToString("0.####", CultureInfo.InvariantCulture))) + "}");
            AssetDatabase.SaveAssets();

            // 5) registro: soma (não troca a lista — o registro também guarda clipes de fora de Anim/Clips)
            var reg = AssetDatabase.LoadAssetAtPath<FBRegistry>("Assets/FreeBala/Resources/FBRegistry.asset");
            int added = 0;
            if (reg)
            {
                foreach (var it in made) { var c = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{FBAnimImport.ClipDir}/{it.name}.anim"); if (c && !reg.clips.Contains(c)) { reg.clips.RemoveAll(x => x && x.name == it.name); reg.clips.Add(c); added++; } }
                EditorUtility.SetDirty(reg); AssetDatabase.SaveAssets();
            }
            L($"clipes POL_: {made.Count} gerados, {infos.Count(i => i.dupOf != null)} cópias puladas; registro +{added} ({(reg ? reg.clips.Count : 0)} no total)");

            // 6) quedas medidas no corpo do policial → FBDeathLib.json (pol: true)
            Deaths(made.Where(i => !i.loop && (i.slug.Contains("death") || i.slug.Contains("dying"))).Select(i => i.name).ToList());

            // 7) relatório JSON e limpeza da pasta temporária (a fonte fica no F:)
            var js = new StringBuilder("{\"clips\":[\n");
            js.Append(string.Join(",\n", infos.Select(i => $"{{\"name\":\"{i.name}\",\"pack\":\"{i.pack}\",\"slug\":\"{i.slug}\",\"len\":{F(i.len)},\"loop\":{(i.loop ? "true" : "false")},\"rs\":{F(i.rs)},\"turn\":{F(i.turn)},\"seam\":{F(i.seam)},\"maxMus\":{F(i.maxMus)},\"maxMusName\":\"{i.maxMusName}\",\"dupOf\":{(i.dupOf == null ? "null" : "\"" + i.dupOf + "\"")}}}")));
            js.Append("\n]}"); Directory.CreateDirectory(LogDir); File.WriteAllText(Path.Combine(LogDir, "r55_mixamo.json"), js.ToString());
            AssetDatabase.DeleteAsset(Stage); AssetDatabase.Refresh();
            L("pronto");
        }
        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        static float Sig(float[] a, float[] b) { float m = 0; for (int i = 0; i < a.Length; i++) m = Mathf.Max(m, Mathf.Abs(a[i] - b[i])); return m; }
        static Quaternion Q(AnimationCurve x, AnimationCurve y, AnimationCurve z, AnimationCurve w, float t) => new Quaternion(x?.Evaluate(t) ?? 0, y?.Evaluate(t) ?? 0, z?.Evaluate(t) ?? 0, w.Evaluate(t)).normalized;
        static float Yaw(this Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;

        // quedas: o clipe puro num policial em escala de jogo (ScaleK = 1), quadril amostrado a 30 qps
        public static void DeathsBatch() { int code = 0; try { Deaths(null); } catch (System.Exception e) { Debug.LogError("[FreeBala][r55] " + e); code = 1; } EditorApplication.Exit(code); }

        // ações com tempo de jogo fixo: a recarga (Rifle_Reload_2, 2,17 s — o FBWeaponUser recarrega em 2,2 s) e a granada (1,8 s) do
        // Complexo. O Mixamo tem 3,3 s e 3,2 s com pausa parada no começo e no fim → corta a pausa (atividade dos músculos < 8 % do pico)
        // e ajusta o resto ao tempo da vaga: POL_BS_reloading_fit, POL_BS_toss_grenade_fit
        public static void FitBatch()
        {
            int code = 0;
            try
            {
                Fit("POL_BS_reloading", "POL_BS_reloading_fit", 2.1667f); Fit("POL_BS_toss_grenade", "POL_BS_toss_grenade_fit", 1.8f);
                Deaths(null);
            }
            catch (System.Exception e) { Debug.LogError("[FreeBala][r55] " + e); L("ERRO " + e); code = 1; }
            Directory.CreateDirectory(LogDir); File.AppendAllText(Path.Combine(LogDir, "r55_mixamo.txt"), log.ToString());
            EditorApplication.Exit(code);
        }
        // ---------------------------------------------------------------------------------------------------------------
        // LIMITE DAS JUNTAS (04/11, retorno do usuário: "os rigs dos policiais estão quebrados, pernas, braços").
        // CAUSA MEDIDA: o retarget nativo gravou os músculos no espaço do Avatar de ORIGEM (o NARC do Mixamo), e 102 dos 105
        // clipes saíram com valor FORA de ±1 — o limite anatômico do Humanoid. Piores: "Foot Twist In-Out" 4,15 (pé torcido a
        // quatro vezes o limite), "Lower Leg Stretch" 1,48 (joelho esticado para trás, dobrando ao contrário) e "Upper Leg
        // Front-Back" 1,37. O Unity NÃO corta esses valores ao tocar (medido no RigBatch: o corpo devolve o mesmo 4,15 que está
        // escrito), então o osso passa do limite e a perna/o pé "quebram".
        // CORREÇÃO: prende cada curva de MÚSCULO em [-1, +1] nos clipes POL_*. As curvas que não são músculo (RootT/RootQ e os
        // alvos de pé LeftFootT/Q etc.) não são tocadas — ali passar de 1 é normal, é posição e não ângulo.
        // Lote: -executeMethod FreeBala.EditorTools.FBPoliciaMixamo.ClampBatch
        public static void ClampBatch()
        {
            int code = 0;
            try { Clamp(); } catch (System.Exception e) { Debug.LogError("[FreeBala][r55] " + e); L("ERRO " + e); code = 1; }
            Directory.CreateDirectory(LogDir); File.WriteAllText(Path.Combine(LogDir, "r55_limite.txt"), log.ToString());
            EditorApplication.Exit(code);
        }

        static void Clamp()
        {
            var mus = new HashSet<string>(HumanTrait.MuscleName);
            var ids = Directory.GetFiles(FBAnimImport.ClipDir, Prefix + "*.anim").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x).ToList();
            int mexidos = 0, curvas = 0; var piores = new List<(float v, string clip, string m)>();
            foreach (var id in ids)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{FBAnimImport.ClipDir}/{id}.anim"); if (!clip) continue;
                bool mudou = false; float pior = 0; string piorM = "-";
                foreach (var b in AnimationUtility.GetCurveBindings(clip))
                {
                    if (!mus.Contains(b.propertyName)) continue;
                    var cv = AnimationUtility.GetEditorCurve(clip, b); if (cv == null) continue;
                    var ks = cv.keys; bool esse = false;
                    for (int i = 0; i < ks.Length; i++)
                    {
                        float v = ks[i].value; if (Mathf.Abs(v) <= 1f) continue;
                        if (Mathf.Abs(v) > pior) { pior = Mathf.Abs(v); piorM = b.propertyName; }
                        ks[i].value = Mathf.Clamp(v, -1f, 1f); ks[i].inTangent = ks[i].outTangent = 0; esse = true;   // no limite a curva fica plana (sem salto)
                    }
                    if (!esse) continue;
                    cv.keys = ks;
                    for (int i = 0; i < ks.Length; i++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(cv, i, AnimationUtility.TangentMode.ClampedAuto);
                        AnimationUtility.SetKeyRightTangentMode(cv, i, AnimationUtility.TangentMode.ClampedAuto);
                    }
                    AnimationUtility.SetEditorCurve(clip, b, cv); curvas++; mudou = true;
                }
                if (mudou) { mexidos++; EditorUtility.SetDirty(clip); piores.Add((pior, id, piorM)); }
            }
            AssetDatabase.SaveAssets();
            L($"limite das juntas: {mexidos} clipes de {ids.Count} corrigidos · {curvas} curvas de músculo presas em ±1");
            foreach (var p in piores.OrderByDescending(p => p.v).Take(12)) L($"    {p.clip}: pedia {p.v:0.00} em {p.m}");
        }

        // ---------------------------------------------------------------------------------------------------------------
        // CONFERÊNCIA DO RIG, POLICIAL POR POLICIAL (pedido do usuário: "certifique de corrigir os rigs corretamente com o
        // osso de cada personagem policial"). Para cada policial e cada clipe POL_:
        //   · o Avatar é humano? todos os ossos do Humanoid estão mapeados? (osso faltando = a animação não chega nele)
        //   · o clipe pede um músculo que o Avatar DESTE personagem não entrega? Compara o músculo escrito no clipe com o
        //     que o corpo do personagem devolve (HumanPoseHandler) depois de tocar o quadro: diferença = limite do próprio
        //     Avatar cortando a pose (é isso que torce ombro/cotovelo quando o rig não bate com o do Mixamo).
        //   · a pose de bind bate com a do NARC (fonte dos clipes)? ângulo de braço e perna.
        // Lote: -executeMethod FreeBala.EditorTools.FBPoliciaMixamo.RigBatch [-fbrig_chars a,b,c]
        public const string PolChars = "r_elaine,r_pm_almeida,r_pm_silva,r_silva";
        static string Arg(string k, string d) { var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, k); return i >= 0 && i + 1 < a.Length ? a[i + 1] : d; }

        public static void RigBatch()
        {
            int code = 0;
            try { Rig(Arg("-fbrig_chars", PolChars).Split(',')); }
            catch (System.Exception e) { Debug.LogError("[FreeBala][r55] " + e); L("ERRO " + e); code = 1; }
            Directory.CreateDirectory(LogDir); File.WriteAllText(Path.Combine(LogDir, "r55_rig.txt"), log.ToString());
            EditorApplication.Exit(code);
        }

        static void Rig(string[] chars)
        {
            var ids = Directory.GetFiles(FBAnimImport.ClipDir, Prefix + "*.anim").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x).ToList();
            L($"conferência de rig: {chars.Length} policiais × {ids.Count} clipes POL_");
            var musName = HumanTrait.MuscleName;
            // músculo escrito em cada clipe, amostrado em 5 tempos
            var want = new Dictionary<string, (float[] m, float t)[]>();
            foreach (var id in ids)
            {
                var cl = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{FBAnimImport.ClipDir}/{id}.anim"); if (!cl) continue;
                var binds = AnimationUtility.GetCurveBindings(cl);
                var byName = new Dictionary<string, AnimationCurve>();
                foreach (var b in binds) if (System.Array.IndexOf(musName, b.propertyName) >= 0) byName[b.propertyName] = AnimationUtility.GetEditorCurve(cl, b);
                var sam = new (float[] m, float t)[5];
                for (int i = 0; i < 5; i++)
                {
                    float t = cl.length * (i + .5f) / 5; var v = new float[musName.Length];
                    for (int k = 0; k < musName.Length; k++) v[k] = byName.TryGetValue(musName[k], out var cv) ? cv.Evaluate(t) : float.NaN;
                    sam[i] = (v, t);
                }
                want[id] = sam;
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var skin in chars)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/FreeBala/Resources/FBPrefabs/Chars/{skin}.prefab");
                if (!pf) { L($"{skin}: SEM PREFAB"); continue; }
                var go = Object.Instantiate(pf); go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); go.transform.localScale = Vector3.one * FBConst.ModelScale;
                var an = go.GetComponentInChildren<Animator>();
                if (!an || !an.avatar || !an.avatar.isHuman || !an.avatar.isValid) { L($"{skin}: AVATAR NÃO HUMANO/INVÁLIDO"); Object.DestroyImmediate(go); continue; }
                an.applyRootMotion = false; an.runtimeAnimatorController = null; an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                // ossos do Humanoid presentes
                var falta = new List<string>(); int mapeados = 0;
                for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
                {
                    var hb = (HumanBodyBones)i; if (hb == HumanBodyBones.LastBone) break;
                    if (an.GetBoneTransform(hb)) mapeados++;
                    else if (HumanTrait.RequiredBone(i)) falta.Add(hb.ToString());
                }
                // pose de bind: ângulo do braço com a horizontal e da perna com a vertical (T-pose do Mixamo ≈ 0°)
                Vector3 V(HumanBodyBones a, HumanBodyBones b) { var ta = an.GetBoneTransform(a); var tb = an.GetBoneTransform(b); return ta && tb ? (tb.position - ta.position) : Vector3.zero; }
                var braco = V(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm);
                var perna = V(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg);
                float angBraco = braco == Vector3.zero ? -1 : Vector3.Angle(braco, -go.transform.right);
                float angPerna = perna == Vector3.zero ? -1 : Vector3.Angle(perna, -go.transform.up);
                var hph = new HumanPoseHandler(an.avatar, go.transform); var pose = new HumanPose();
                // limites do próprio Avatar por músculo (o que o corpo deste personagem aceita)
                int fora = 0, amostras = 0; var pior = new Dictionary<string, (float d, string clip)>();
                foreach (var id in ids)
                {
                    var cl = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{FBAnimImport.ClipDir}/{id}.anim"); if (!cl || !want.ContainsKey(id)) continue;
                    var g = PlayableGraph.Create("rig"); g.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var cp = AnimationClipPlayable.Create(g, cl); cp.SetApplyFootIK(false);
                    AnimationPlayableOutput.Create(g, "o", an).SetSourcePlayable(cp);
                    foreach (var (m, t) in want[id])
                    {
                        cp.SetTime(t); cp.SetTime(t); g.Evaluate(0);
                        hph.GetHumanPose(ref pose);
                        for (int k = 0; k < musName.Length && k < pose.muscles.Length; k++)
                        {
                            if (float.IsNaN(m[k])) continue; amostras++;
                            float d = Mathf.Abs(pose.muscles[k] - Mathf.Clamp(m[k], -1f, 1f));     // ±1 é o limite do Humanoid: fora disso o corte é do formato, não do personagem
                            if (d > .02f) { fora++; if (!pior.TryGetValue(musName[k], out var p) || d > p.d) pior[musName[k]] = (d, id); }
                        }
                    }
                    g.Destroy();
                }
                hph.Dispose();
                L($"{skin}: avatar humano OK · {mapeados} ossos do Humanoid · faltando obrigatório: {(falta.Count == 0 ? "nenhum" : string.Join(", ", falta))}" +
                  $" · bind: braço {angBraco:0.0}° da horizontal, perna {angPerna:0.0}° da vertical" +
                  $" · músculo entregue ≠ escrito em {fora} de {amostras} amostras ({100f * fora / Mathf.Max(1, amostras):0.00} %)");
                foreach (var kv in pior.OrderByDescending(k => k.Value.d).Take(6)) L($"    {kv.Key}: até {kv.Value.d:0.00} de diferença (ex.: {kv.Value.clip})");
                Object.DestroyImmediate(go);
            }
            L("conferência de rig: fim");
        }

        static void Fit(string srcId, string dstId, float target)
        {
            var src = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{FBAnimImport.ClipDir}/{srcId}.anim"); if (!src) { L($"ajuste: sem {srcId}"); return; }
            var binds = AnimationUtility.GetCurveBindings(src); var curves = binds.Select(b => AnimationUtility.GetEditorCurve(src, b)).ToArray();
            var mus = new HashSet<string>(HumanTrait.MuscleName);
            int n = Mathf.Max(2, Mathf.RoundToInt(src.length * 30)); var act = new float[n];
            for (int i = 0; i < n; i++) for (int c = 0; c < binds.Length; c++) if (mus.Contains(binds[c].propertyName))
                        act[i] += Mathf.Abs(curves[c].Evaluate((i + 1) * src.length / n) - curves[c].Evaluate(i * src.length / n));
            float peak = act.Max(), thr = peak * .08f; int a = System.Array.FindIndex(act, v => v > thr), z = System.Array.FindLastIndex(act, v => v > thr);
            float t0 = Mathf.Max(0, (a - 3) * src.length / n), t1 = Mathf.Min(src.length, (z + 4) * src.length / n), k = target / (t1 - t0);
            var clip = Object.Instantiate(src); clip.name = dstId;
            for (int c = 0; c < binds.Length; c++)
            {
                int m = Mathf.Max(2, Mathf.CeilToInt(target * 60)); var ks = new Keyframe[m + 1];
                for (int i = 0; i <= m; i++) { float t = target * i / m; ks[i] = new Keyframe(t, curves[c].Evaluate(t0 + t / k)); }
                var cv = new AnimationCurve(ks); for (int i = 0; i <= m; i++) AnimationUtility.SetKeyLeftTangentMode(cv, i, AnimationUtility.TangentMode.ClampedAuto);
                for (int i = 0; i <= m; i++) AnimationUtility.SetKeyRightTangentMode(cv, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetEditorCurve(clip, binds[c], cv);
            }
            var st = AnimationUtility.GetAnimationClipSettings(src); st.stopTime = target; st.loopTime = false; AnimationUtility.SetAnimationClipSettings(clip, st);
            var path = $"{FBAnimImport.ClipDir}/{dstId}.anim"; var old = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (old) { EditorUtility.CopySerialized(clip, old); Object.DestroyImmediate(clip); clip = old; EditorUtility.SetDirty(old); } else AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();
            var reg = AssetDatabase.LoadAssetAtPath<FBRegistry>("Assets/FreeBala/Resources/FBRegistry.asset");
            if (reg && !reg.clips.Contains(clip)) { reg.clips.RemoveAll(x => x && x.name == dstId); reg.clips.Add(clip); EditorUtility.SetDirty(reg); AssetDatabase.SaveAssets(); }
            L($"ajuste {srcId} → {dstId}: trecho {t0:0.00}–{t1:0.00} s de {src.length:0.00} s → {clip.length:0.00} s (ritmo ×{1 / k:0.00})");
        }
        static void Deaths(List<string> ids)
        {
            ids ??= Directory.GetFiles(FBAnimImport.ClipDir, Prefix + "*.anim").Select(Path.GetFileNameWithoutExtension).Where(n => n.Contains("death") || n.Contains("dying")).ToList();
            if (ids.Count == 0) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FreeBala/Resources/FBPrefabs/Chars/r_pm_almeida.prefab"); if (!pf) { L("quedas: sem prefab r_pm_almeida"); return; }
            var go = Object.Instantiate(pf); go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); go.transform.localScale = Vector3.one * FBConst.ModelScale;
            var an = go.GetComponentInChildren<Animator>(); an.applyRootMotion = false; an.runtimeAnimatorController = null; an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var hips = an.GetBoneTransform(HumanBodyBones.Hips); var ls = an.GetBoneTransform(HumanBodyBones.LeftUpperArm); var rsh = an.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var libPath = "Assets/FreeBala/Resources/FBDeathLib.json";
            var lib = Json.Parse(File.ReadAllText(libPath)) as Dictionary<string, object>; var list = Json.L(lib, "clips") ?? new List<object>();
            list.RemoveAll(o => (Json.S(o, "id") ?? "").StartsWith(Prefix));
            foreach (var id in ids)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{FBAnimImport.ClipDir}/{id}.anim"); if (!clip) continue;
                var g = PlayableGraph.Create("r55"); g.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var cp = AnimationClipPlayable.Create(g, clip); cp.SetApplyFootIK(false);
                var o = AnimationPlayableOutput.Create(g, "o", an); o.SetSourcePlayable(cp);
                Vector3 H(float t) { cp.SetTime(t); cp.SetTime(t); g.Evaluate(0); return hips.position; }
                var h0 = H(0); var hE = H(clip.length); float land = clip.length;
                for (float t = 0; t <= clip.length; t += 1 / 30f) { var h = H(t); if (h.y - hE.y < .15f * (h0.y - hE.y)) { land = t; break; } }
                H(clip.length); var right = (rsh.position - ls.position).normalized; var spine = Vector3.up; var front = Vector3.Cross(right, spine);
                // de costas = peito para cima: normal do peito pelo produto do eixo dos ombros com o eixo quadril→ombros
                var up = ((ls.position + rsh.position) * .5f - hips.position).normalized; var chestN = Vector3.Cross(right, up);   // Unity é mão esquerda: right × up = frente
                string face = chestN.y > 0 ? "up" : "down"; float k = FBConst.ModelScale / go.transform.lossyScale.y;   // ScaleK = 1
                var d = (hE - h0) * k;
                list.Add(new Dictionary<string, object> { ["id"] = id, ["fwd"] = System.Math.Round(d.z, 2), ["right"] = System.Math.Round(d.x, 2), ["dur"] = System.Math.Round(clip.length, 2), ["land"] = System.Math.Round(land, 2), ["face"] = face, ["kind"] = "stand", ["pol"] = true });
                L($"  queda {id}: frente {d.z:0.00} · direita {d.x:0.00} · dura {clip.length:0.00} s · chão {land:0.00} s · {(face == "up" ? "de costas" : "de bruços")} (front {front})");
                g.Destroy();
            }
            Object.DestroyImmediate(go);
            var sb = new StringBuilder("{\n\"clips\": [\n");
            sb.Append(string.Join(",\n", list.Select(o =>
            {
                var m = o as Dictionary<string, object>;
                return "{\n" + string.Join(",\n", m.Select(kv => $"\"{kv.Key}\": " + (kv.Value is string s ? "\"" + s + "\"" : kv.Value is bool bb ? (bb ? "true" : "false") : System.Convert.ToDouble(kv.Value, CultureInfo.InvariantCulture).ToString("0.0#", CultureInfo.InvariantCulture)))) + "\n}";
            })));
            sb.Append("\n]\n}\n"); File.WriteAllText(libPath, sb.ToString()); AssetDatabase.ImportAsset(libPath);
            L($"quedas: {ids.Count} medidas e somadas ao FBDeathLib.json ({list.Count} no total)");
        }
    }
}
