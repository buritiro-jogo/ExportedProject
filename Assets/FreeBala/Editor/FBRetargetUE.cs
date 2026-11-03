// r48: RETARGET NATIVO dos clipes de VEÍCULO do 171 (entrar/sair do carro, subir/descer/pilotar a moto) pelo próprio humanoide do Unity.
// Pedido: "os esqueletos continuam completamente quebrados ... articulações deslocadas". Causa achada: o retarget antigo (src/tools_retarget.mjs)
// reimplementa o espaço de músculos em JS e corta em ±1,6 — no Entrar/Sair_Motorista o ombro esquerdo, a torção do pé esquerdo e o pulso
// direito ficavam no limite (±1,6), coxa e pé pulavam ~1 unidade de músculo em UM quadro (estalo), e os dedos de TODOS os clipes ficavam
// esticados para trás (3 Stretched = 1,6 fixo). Aqui: o esqueleto SK_Mannequin do GLB (tools/re/ue_glb2unity.py) vira um Avatar humanoide
// (AvatarBuilder, pose T com braços na horizontal, palma para baixo e dedos retos — o repouso do UE é pose A com os dedos dobrados) e cada
// quadro sai do HumanPoseHandler.GetHumanPose — a mesma conta que o Unity faz ao importar um FBX humanoide. Mantém o GUID dos clipes.
// Também reescreve Resources/FBAnimPaths.json (trajetória do corpo) dos clipes de transição a partir dos clipes novos.
// Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBRetargetUE.Batch [-fbru_only nome1,nome2] [-fbru_dry 1]
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBRetargetUE
    {
        [System.Serializable] class UeNode { public string n; public int p; public float[] t, r, s; }
        [System.Serializable] class UeTrack { public int i; public float[] t, r; }
        [System.Serializable] class UeAnim { public string name, src; public float fps, len, srcLen; public int frames; public UeNode[] nodes; public UeTrack[] tracks; }

        public static string SrcDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../tools/re/out/171/ue_unity"));
        static string Arg(string k, string d) { var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, k); return i >= 0 && i + 1 < a.Length ? a[i + 1] : d; }

        // osso humano do Mecanim -> osso do SK_Mannequin (minúsculo; o ALS do 171 mistura Pelvis/UpperArm_L/hand_r)
        static readonly (string h, string ue)[] BODY = {
            ("Hips", "pelvis"), ("LeftUpperLeg", "thigh_l"), ("RightUpperLeg", "thigh_r"), ("LeftLowerLeg", "calf_l"), ("RightLowerLeg", "calf_r"), ("LeftFoot", "foot_l"), ("RightFoot", "foot_r"),
            ("Spine", "spine_01"), ("Chest", "spine_02"), ("UpperChest", "spine_03"), ("Neck", "neck_01"), ("Head", "head"),
            ("LeftShoulder", "clavicle_l"), ("RightShoulder", "clavicle_r"), ("LeftUpperArm", "upperarm_l"), ("RightUpperArm", "upperarm_r"), ("LeftLowerArm", "lowerarm_l"), ("RightLowerArm", "lowerarm_r"),
            ("LeftHand", "hand_l"), ("RightHand", "hand_r"), ("LeftToes", "ball_l"), ("RightToes", "ball_r") };
        static readonly (string h, string ue)[] FING = { ("Thumb", "thumb"), ("Index", "index"), ("Middle", "middle"), ("Ring", "ring"), ("Little", "pinky") };
        static readonly string[] PH = { "Proximal", "Intermediate", "Distal" };
        static IEnumerable<(string h, string ue)> Map()
        {
            foreach (var b in BODY) yield return b;
            foreach (var (side, s) in new[] { ("Left", "l"), ("Right", "r") })
                foreach (var (fh, fu) in FING) for (int k = 0; k < 3; k++) yield return ($"{side} {fh} {PH[k]}", $"{fu}_0{k + 1}_{s}");
        }

        public static void Batch()
        {
            int code = 0;
            try { var only = Arg("-fbru_only", ""); Run(only.Length > 0 ? only.Split(',') : null, Arg("-fbru_dry", "0") != "1"); }
            catch (System.Exception e) { Debug.LogError("[FreeBala][ru] " + e); code = 1; }
            EditorApplication.Exit(code);
        }

        class Rig { public GameObject top; public Transform fix; public Transform[] tr; public Dictionary<string, Transform> by; public UeNode[] nodes; public Avatar av; public HumanPoseHandler h; public string sig; }

        static Vector3 V(float[] a, int o = 0) => new(a[o], a[o + 1], a[o + 2]);
        static Quaternion Q(float[] a, int o = 0) => new Quaternion(a[o], a[o + 1], a[o + 2], a[o + 3]).normalized;
        static void Rest(Rig g) { for (int i = 0; i < g.tr.Length; i++) { var n = g.nodes[i]; g.tr[i].localPosition = V(n.t); g.tr[i].localRotation = Q(n.r); g.tr[i].localScale = V(n.s); } }
        static Transform B(Rig g, string ue) => g.by.TryGetValue(ue, out var t) ? t : null;
        // translação animada só na raiz e no quadril: os outros ossos do GLB trazem o comprimento do esqueleto em que a animação foi feita
        // (ombro 7 cm e twist 9 cm diferentes do repouso do SK_Mannequin); o retarget usa só as ROTAÇÕES deles (o alvo tem os próprios ossos)
        static bool MovesT(UeAnim a, int i) { var n = a.nodes[i].n.ToLowerInvariant(); return n == "root" || n == "pelvis"; }
        // gira o osso (no mundo) para a direção osso->alvo virar 'want' (giro mínimo: mantém a torção do osso)
        static void Aim(Transform b, Vector3 target, Vector3 want) { var cur = target - b.position; if (cur.sqrMagnitude < 1e-10f) return; b.rotation = Quaternion.FromToRotation(cur.normalized, want.normalized) * b.rotation; }

        static Rig Build(UeAnim a, List<string> log)
        {
            var g = new Rig { nodes = a.nodes, top = new GameObject("ue_top") };
            g.fix = new GameObject("ue_fix").transform; g.fix.SetParent(g.top.transform, false);
            g.tr = new Transform[a.nodes.Length]; g.by = new Dictionary<string, Transform>();
            for (int i = 0; i < a.nodes.Length; i++) { g.tr[i] = new GameObject(a.nodes[i].n).transform; g.by[a.nodes[i].n.ToLowerInvariant()] = g.tr[i]; }
            for (int i = 0; i < a.nodes.Length; i++) g.tr[i].SetParent(a.nodes[i].p >= 0 ? g.tr[a.nodes[i].p] : g.fix, false);
            Rest(g);
            // de frente para +Z, cabeça para +Y (o mesmo referencial dos clipes antigos: frente do repouso = frente do clipe)
            var up = (B(g, "neck_01").position - B(g, "pelvis").position).normalized; var rt = B(g, "thigh_r").position - B(g, "thigh_l").position;
            rt = (rt - up * Vector3.Dot(rt, up)).normalized; var fw = Vector3.Cross(rt, up);
            g.fix.localRotation = Quaternion.Inverse(Quaternion.LookRotation(fw, up));
            // ---- pose T do avatar (só para definir o zero dos músculos; a animação usa o repouso + as faixas do GLB)
            foreach (var (s, x) in new[] { ("l", -1f), ("r", 1f) })
            {
                var dir = new Vector3(x, 0, 0); Transform ua = B(g, "upperarm_" + s), fa = B(g, "lowerarm_" + s), hd = B(g, "hand_" + s);
                Aim(ua, fa.position, dir); Aim(fa, hd.position, dir); Aim(hd, B(g, "middle_01_" + s).position, dir);
                // palma para baixo: a linha dos nós (indicador - mínimo) deitada e apontando para a frente; gira o ANTEBRAÇO (leva a mão junto,
                // sem criar torção entre os dois) em torno do próprio eixo
                var kn = B(g, "index_01_" + s).position - B(g, "pinky_01_" + s).position; kn -= dir * Vector3.Dot(kn, dir);
                float roll = Vector3.SignedAngle(kn, Vector3.forward, dir); fa.rotation = Quaternion.AngleAxis(roll, dir) * fa.rotation;
                // dedos retos (tira a curvatura do repouso, mantém a abertura): 1ª falange no plano da palma, 2ª e 3ª seguindo a 1ª
                foreach (var f in new[] { "index", "middle", "ring", "pinky" })
                {
                    Transform f1 = B(g, $"{f}_01_{s}"), f2 = B(g, $"{f}_02_{s}"), f3 = B(g, $"{f}_03_{s}"); if (!f1 || !f2 || !f3) continue;
                    var d1 = f2.position - f1.position; d1.y = 0; Aim(f1, f2.position, d1); var dd = (f2.position - f1.position).normalized;
                    Aim(f2, f3.position, dd);
                    // 3ª falange: o nó _end do GLB NÃO está no eixo do osso (vem ~50° torto); no UE o eixo do osso é o mesmo eixo local da falange
                    // anterior (direção f2->f3 no espaço de f2), então alinha esse eixo de f3 com o da 2ª
                    var ax = f3.rotation * f3.localPosition.normalized; f3.rotation = Quaternion.FromToRotation(ax, dd) * f3.rotation;
                }
                log.Add($"pose T {s}: rolagem do antebraço {roll:0.0}°");
            }
            var skel = g.top.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray();
            var human = new List<HumanBone>();
            foreach (var (h, ue) in Map()) { var t = B(g, ue); if (!t) { log.Add("sem osso " + ue); continue; } human.Add(new HumanBone { humanName = h, boneName = t.name, limit = new HumanLimit { useDefaultValues = true } }); }
            var hdsc = new HumanDescription { human = human.ToArray(), skeleton = skel, upperArmTwist = .5f, lowerArmTwist = .5f, upperLegTwist = .5f, lowerLegTwist = .5f, armStretch = .05f, legStretch = .05f, feetSpacing = 0, hasTranslationDoF = false };
            g.av = AvatarBuilder.BuildHumanAvatar(g.top, hdsc); g.av.name = "ue_mannequin";
            if (!g.av.isValid || !g.av.isHuman) throw new System.Exception("avatar do SK_Mannequin inválido");
            Rest(g); g.h = new HumanPoseHandler(g.av, g.top.transform);
            g.sig = string.Join(",", a.nodes.Select(n => n.n + ":" + n.p));
            return g;
        }

        static readonly System.Text.RegularExpressions.Regex MOTO_ONCE = new(@"Go_to|Get_bike|Jump_from|Enter|Exit|Helmet|helmet|_to_|throw|colision|hitground|Return_grau|Pilotando_to|_to_Pilotando", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        static bool CarLoop(string name) => System.Text.RegularExpressions.Regex.IsMatch(name, "Idle|Dirigindo_Assustado|Girando_Volante_.*Baixa|Motorista_MarchaRe|Virando_Volante") && !name.Contains("para_");   // = FBAnimImport (carro)
        // trajetória do corpo (FBAnimPaths) dos clipes de transição que o jogo segue
        static readonly HashSet<string> PATHS = new() { "X171_Entrar_Motorista_SK_Mannequin_Anim1", "X171_Sair_Motorista_Anim2", "X171_Anim_MB_Enter_Passenger_L", "X171_Anim_MB_Exit_Passenger_L",
            "X171_Bike_Go_to_idle_L__Renato", "X171_Get_bike_up_L", "X171_Go_to_bike_L", "X171_Jump_from_bike_L" };

        public static int Run(string[] only, bool write)
        {
            if (!Directory.Exists(SrcDir)) { Debug.LogWarning("[FreeBala][ru] sem " + SrcDir + " (rode tools/re/ue_glb2unity.py)"); return 0; }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var muscles = HumanTrait.MuscleName; var log = new List<string>(); Rig g = null; int n = 0;
            var paths = new Dictionary<string, List<Vector3>>(); var pathFps = new Dictionary<string, float>();
            string[] KEY = { "hand_l", "hand_r", "foot_l", "foot_r", "head", "lowerarm_l", "lowerarm_r", "calf_l", "calf_r" };
            foreach (var file in Directory.GetFiles(SrcDir, "*.json").OrderBy(f => f))
            {
                var a = JsonUtility.FromJson<UeAnim>(File.ReadAllText(file)); if (a == null || a.nodes == null) continue;
                var src = a.src ?? ""; bool car = src.Contains("/Vehicle/Car/"), moto = src.Contains("/Vehicle/Motorcycle/") && !src.Contains("/Weapon/");
                if (!car && !moto) continue;                                                       // armas da moto (mira da garupa): ficam como estavam
                var id = "X171_" + a.name; if (only != null && !only.Contains(a.name) && !only.Contains(id)) continue;
                var clipPath = $"{FBAnimImport.ClipDir}/{FBAnimImport.Safe(id)}.anim"; var old = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (!old) continue;                                                                // só os que o jogo já tem (não cria clipe novo)
                var sig = string.Join(",", a.nodes.Select(x => x.n + ":" + x.p));
                if (g == null || g.sig != sig) { if (g != null) { g.h.Dispose(); Object.DestroyImmediate(g.top); } g = Build(a, log); }
                if (Arg("-fbru_dbg", "0") == "1") { DebugRT(g, a, -1, log); DebugRT(g, a, a.frames / 2, log); }
                var tr = g.tr; int F = a.frames; var d = new float[F * 102]; var hp = new HumanPose(); float errMax = 0, errSum = 0; string errAt = ""; Quaternion prevQ = Quaternion.identity;
                var keyT = KEY.Select(k => B(g, k)).ToArray(); var srcP = new Vector3[keyT.Length]; var pel = B(g, "pelvis");
                // erro no referencial do QUADRIL (tira a raiz): fonte × clipe novo × clipe antigo, aplicados no próprio esqueleto do UE
                var jNew = new float[keyT.Length]; var jOld = new float[keyT.Length]; float oldMaxE = 0; string oldAt = "";
                var obAll = AnimationUtility.GetCurveBindings(old).ToDictionary(b => b.propertyName, b => AnimationUtility.GetEditorCurve(old, b));
                Vector3 Loc(Transform t) => g.top.transform.InverseTransformPoint(t.position) - g.top.transform.InverseTransformPoint(pel.position);   // só tira a translação (o giro do quadril no Unity sai da média do corpo)
                for (int f = 0; f < F; f++)
                {
                    Rest(g);
                    foreach (var t in a.tracks)
                    {
                        if (t.t != null && t.t.Length >= (f + 1) * 3 && MovesT(a, t.i)) tr[t.i].localPosition = V(t.t, f * 3);
                        if (t.r != null && t.r.Length >= (f + 1) * 4) tr[t.i].localRotation = Q(t.r, f * 4);
                    }
                    for (int k = 0; k < keyT.Length; k++) srcP[k] = keyT[k] ? Loc(keyT[k]) : Vector3.zero;
                    g.h.GetHumanPose(ref hp);
                    var bq = hp.bodyRotation; if (f > 0 && Quaternion.Dot(bq, prevQ) < 0) bq = new Quaternion(-bq.x, -bq.y, -bq.z, -bq.w); prevQ = bq;
                    int o = f * 102; d[o] = hp.bodyPosition.x; d[o + 1] = hp.bodyPosition.y; d[o + 2] = hp.bodyPosition.z; d[o + 3] = bq.x; d[o + 4] = bq.y; d[o + 5] = bq.z; d[o + 6] = bq.w;
                    for (int m = 0; m < 95 && m < hp.muscles.Length; m++) d[o + 7 + m] = hp.muscles[m];
                    // volta: aplica os músculos no próprio esqueleto do UE e mede onde mãos/pés/cabeça foram parar (fidelidade do retarget)
                    var keep = hp; keep.muscles = (float[])hp.muscles.Clone();
                    g.h.SetHumanPose(ref hp);
                    for (int k = 0; k < keyT.Length; k++) { if (!keyT[k]) continue; float e = (Loc(keyT[k]) - srcP[k]).magnitude; errSum += e; jNew[k] += e / F; if (e > errMax) { errMax = e; errAt = $"{KEY[k]} t={f / a.fps:0.00}"; } }
                    // o clipe ANTIGO no mesmo tempo, no mesmo esqueleto
                    var po = keep; po.muscles = new float[keep.muscles.Length]; float tt = f / a.fps;
                    for (int m = 0; m < po.muscles.Length && m < muscles.Length; m++) po.muscles[m] = obAll.TryGetValue(muscles[m], out var cv) ? cv.Evaluate(tt) : 0;
                    float OC(string c, float dv) => obAll.TryGetValue(c, out var cv2) ? cv2.Evaluate(tt) : dv;
                    po.bodyRotation = new Quaternion(OC("RootQ.x", 0), OC("RootQ.y", 0), OC("RootQ.z", 0), OC("RootQ.w", 1)).normalized;
                    g.h.SetHumanPose(ref po);
                    for (int k = 0; k < keyT.Length; k++) { if (!keyT[k]) continue; float e = (Loc(keyT[k]) - srcP[k]).magnitude; jOld[k] += e / F; if (e > oldMaxE) { oldMaxE = e; oldAt = $"{KEY[k]} t={tt:0.00}"; } }
                }
                log.Add($"   {id} erro médio por junta (cm, relativo à posição do quadril) novo|antigo: " + string.Join(" ", KEY.Select((k, i) => $"{k} {jNew[i] * 100:0.0}|{jOld[i] * 100:0.0}")) + $" · pior antigo {oldMaxE * 100:0.0} cm ({oldAt})");
                // comparação com o clipe antigo (mesmos tempos): raiz e músculos do corpo
                float dRoot = 0, dMus = 0; int nm = 0; var oldMax = 0f; var oldMaxN = "";
                var ob = AnimationUtility.GetCurveBindings(old).ToDictionary(b => b.propertyName, b => b);
                for (int f = 0; f < F; f++)
                {
                    float t = f / a.fps;
                    for (int c = 0; c < 3; c++) if (ob.TryGetValue(new[] { "RootT.x", "RootT.y", "RootT.z" }[c], out var b)) dRoot = Mathf.Max(dRoot, Mathf.Abs(AnimationUtility.GetEditorCurve(old, b).Evaluate(t) - d[f * 102 + c]));
                    for (int m = 0; m < 55; m++) if (ob.TryGetValue(muscles[m], out var b)) { float ov = AnimationUtility.GetEditorCurve(old, b).Evaluate(t); dMus += Mathf.Abs(ov - d[f * 102 + 7 + m]); nm++; if (Mathf.Abs(ov) > oldMax) { oldMax = Mathf.Abs(ov); oldMaxN = muscles[m]; } }
                }
                float newMax = 0; string newMaxN = ""; float jump = 0; string jumpN = "";
                for (int m = 0; m < 95; m++) for (int f = 0; f < F; f++)
                    {
                        float v = d[f * 102 + 7 + m]; if (m < 55 && Mathf.Abs(v) > newMax) { newMax = Mathf.Abs(v); newMaxN = muscles[m]; }
                        if (f > 0 && m < 55) { float j = Mathf.Abs(v - d[(f - 1) * 102 + 7 + m]) * 30f / a.fps; if (j > jump) { jump = j; jumpN = $"{muscles[m]} t={f / a.fps:0.00}"; } }
                    }
                bool loop = car ? CarLoop(a.name) : !MOTO_ONCE.IsMatch(a.name);
                log.Add($"{id}: {F} q a {a.fps} qps · volta máx {errMax * 100:0.0} cm ({errAt}) média {errSum / Mathf.Max(1, F * keyT.Length) * 100:0.0} cm · corpo |m| máx {newMax:0.00} ({newMaxN}) [antigo {oldMax:0.00} {oldMaxN}] · maior salto/quadro {jump:0.00} ({jumpN}) · Δraiz vs antigo {dRoot:0.000} · Δmúsculo médio {dMus / Mathf.Max(1, nm):0.000}{(loop ? " · laço" : "")}");
                if (PATHS.Contains(id)) { var L = new List<Vector3>(); for (int f = 0; f < F; f++) L.Add(new Vector3(d[f * 102], d[f * 102 + 1], d[f * 102 + 2])); paths[id] = L; pathFps[id] = a.fps; }
                if (write) FBAnimImport.MakeClip(id, a.fps, d, 1f, loop);
                n++;
            }
            if (g != null) { g.h.Dispose(); Object.DestroyImmediate(g.top); }
            if (write && paths.Count > 0)
            {
                var pf = "Assets/FreeBala/Resources/FBAnimPaths.json"; var all = File.Exists(pf) ? Json.Parse(File.ReadAllText(pf)) as Dictionary<string, object> ?? new() : new();
                var inv = System.Globalization.CultureInfo.InvariantCulture; string F3(float v) => v.ToString("0.###", inv);
                foreach (var kv in paths) all[kv.Key] = "@" + "{\"fps\":" + F3(pathFps[kv.Key]) + ",\"t\":[" + string.Join(",", kv.Value.Select(p => $"[{F3(p.x)},{F3(p.y)},{F3(p.z)}]")) + "]}";
                string Ser(object o) => o is string s && s.StartsWith("@") ? s.Substring(1) : MiniJson(o);
                File.WriteAllText(pf, "{\n" + string.Join(",\n", all.Select(kv => "\"" + kv.Key + "\":" + Ser(kv.Value))) + "\n}");
                log.Add($"FBAnimPaths.json: {paths.Count} trajetórias refeitas ({string.Join(", ", paths.Keys)})");
            }
            if (write) { AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); }
            foreach (var l in log) Debug.Log("[FreeBala][ru] " + l);
            Debug.Log($"[FreeBala][ru] {n} clipes de veículo {(write ? "regravados" : "medidos (sem gravar)")} pelo retarget nativo");
            return n;
        }

        // diagnóstico: ida e volta (GetHumanPose -> SetHumanPose) num quadro (-1 = repouso), erro de posição/rotação por osso humano
        static void DebugRT(Rig g, UeAnim a, int f, List<string> log)
        {
            Rest(g);
            if (f >= 0) foreach (var t in a.tracks) { if (t.t != null && t.t.Length >= (f + 1) * 3 && MovesT(a, t.i)) g.tr[t.i].localPosition = V(t.t, f * 3); if (t.r != null && t.r.Length >= (f + 1) * 4) g.tr[t.i].localRotation = Q(t.r, f * 4); }
            var hb = Map().Select(m => (m.h, t: B(g, m.ue))).Where(x => x.t).ToList(); var T = g.top.transform;
            var p0 = hb.Select(x => T.InverseTransformPoint(x.t.position)).ToArray(); var q0 = hb.Select(x => Quaternion.Inverse(T.rotation) * x.t.rotation).ToArray();
            var hp = new HumanPose(); g.h.GetHumanPose(ref hp); g.h.SetHumanPose(ref hp);
            var mus = HumanTrait.MuscleName; var big = Enumerable.Range(0, hp.muscles.Length).Where(m => Mathf.Abs(hp.muscles[m]) > 1).Select(m => $"{mus[m]} {hp.muscles[m]:0.00}");
            log.Add($"dbg {a.name} quadro {f}: corpo {hp.bodyPosition} {hp.bodyRotation.eulerAngles} · |m|>1: {string.Join(", ", big)}");
            log.Add("   " + string.Join(" · ", hb.Select((x, i) => $"{x.h} {(T.InverseTransformPoint(x.t.position) - p0[i]).magnitude * 100:0.0}cm/{Quaternion.Angle(q0[i], Quaternion.Inverse(T.rotation) * x.t.rotation):0}°").Where((s, i) => i < 22)));
        }

        // serializa de volta as entradas antigas do FBAnimPaths (dicionário/lista/número do Json.Parse)
        static string MiniJson(object o)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            switch (o)
            {
                case null: return "null";
                case string s: return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                case bool b: return b ? "true" : "false";
                case double dv: return dv.ToString("0.####", inv);
                case long lv: return lv.ToString(inv);
                case int iv: return iv.ToString(inv);
                case Dictionary<string, object> dct: return "{" + string.Join(",", dct.Select(kv => "\"" + kv.Key + "\":" + MiniJson(kv.Value))) + "}";
                case List<object> lst: return "[" + string.Join(",", lst.Select(MiniJson)) + "]";
                default: return System.Convert.ToString(o, inv);
            }
        }
    }
}
