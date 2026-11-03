// r46: AUDITORIA DE CLIPES no editor (sem build, sem veículo, sem IK): separa "defeito do próprio clipe" de "defeito do código em jogo".
// Para cada personagem × clipe amostra 40 instantes (AnimationMode) e mede: joelho/cotovelo dobrando ao contrário ou de lado (eixo da
// dobra × eixo anatômico da pose T do avatar), músculos lidos de volta (GetHumanPose) × valor da curva do clipe, e o quadril em relação à raiz.
// Lote: Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBClipAudit.Batch -fbids r_tutu,x171_gorda1 -fbclips X171_A,X171_B
//   -> linhas [FreeBala][clipaudit] no log
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HB = UnityEngine.HumanBodyBones;

namespace FreeBala.EditorTools
{
    public static class FBClipAudit
    {
        static string Arg(string k, string d) { var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, k); return i >= 0 && i + 1 < a.Length ? a[i + 1] : d; }
        public static void Batch()
        {
            int code = 0;
            try
            {
                var ids = Arg("-fbids", "r_tutu").Split(','); var clips = Arg("-fbclips", "X171_Entrar_Motorista_SK_Mannequin_Anim1").Split(',');
                bool post = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbposture") >= 0;
                foreach (var id in ids) foreach (var c in clips) if (post) Posture(id, c, float.Parse(Arg("-fbdt", "0.1"), System.Globalization.CultureInfo.InvariantCulture)); else Audit(id, c, int.Parse(Arg("-fbsteps", "40")));
            }
            catch (System.Exception e) { Debug.LogError("[FreeBala][clipaudit] " + e); code = 1; }
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            EditorApplication.Exit(code);
        }
        static AnimationClip LoadClip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/FreeBala/Anim/Clips/{FBAnimImport.Safe(name)}.anim");

        // postura ao longo do clipe (levantar do chão): altura do quadril (m, sobre a raiz), tronco (cima·Y: 1 em pé, 0 deitado), peito
        // (frente·Y: + virado para cima = de costas no chão, − de bruços) e para onde a cabeça aponta no referencial da raiz (graus, 0 = frente)
        public static void Posture(string id, string clipName, float dt)
        {
            var clip = LoadClip(clipName); var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{FBCharImport.PrefabDir}/{id}.prefab"); if (!clip || !pf) { Debug.LogError($"[FreeBala][clipaudit] sem {clipName}/{id}"); return; }
            var go = Object.Instantiate(pf); foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            var an = go.GetComponent<Animator>(); an.applyRootMotion = false; an.runtimeAnimatorController = null;
            Transform hips = an.GetBoneTransform(HB.Hips), neck = an.GetBoneTransform(HB.Neck) ? an.GetBoneTransform(HB.Neck) : an.GetBoneTransform(HB.Head), lT = an.GetBoneTransform(HB.LeftUpperLeg), rT = an.GetBoneTransform(HB.RightUpperLeg);
            if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
            var sb = new System.Text.StringBuilder($"{id} · {clipName} · {clip.length:0.00} s |");
            for (float t = 0; t <= clip.length + 1e-4f; t += dt)
            {
                AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(go, clip, Mathf.Min(t, clip.length)); AnimationMode.EndSampling(); go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var up = (neck.position - hips.position).normalized; var right = (rT.position - lT.position).normalized; var fwd = Vector3.Cross(right, up).normalized;
                var hd = (neck.position - hips.position); hd.y = 0; float ang = hd.sqrMagnitude > 1e-4f ? Vector3.SignedAngle(Vector3.forward, hd, Vector3.up) : 0;
                sb.Append($" {t:0.0}:q{hips.position.y:0.00} t{up.y:+0.0;-0.0} p{fwd.y:+0.0;-0.0} c{ang:0} x{hips.position.x:0.0}z{hips.position.z:0.0}");
            }
            Debug.Log("[FreeBala][clipaudit] " + sb); Object.DestroyImmediate(go);
        }

        public static string Audit(string id, string clipName, int steps)
        {
            var clip = LoadClip(clipName); if (!clip) { Debug.LogError("[FreeBala][clipaudit] sem clipe " + clipName); return null; }
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{FBCharImport.PrefabDir}/{id}.prefab"); if (!pf) { Debug.LogError("[FreeBala][clipaudit] sem prefab " + id); return null; }
            var go = Object.Instantiate(pf); foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            var an = go.GetComponent<Animator>(); an.applyRootMotion = false; an.runtimeAnimatorController = null;
            Transform G(HB b) => an.GetBoneTransform(b);
            var hp = new HumanPoseHandler(an.avatar, go.transform); var pose = new HumanPose(); hp.GetHumanPose(ref pose);
            // pose T: músculos 0 -> eixos anatômicos (mesma regra da FBPoseProbe)
            var z = pose; z.muscles = new float[pose.muscles.Length]; z.bodyRotation = Quaternion.identity; hp.SetHumanPose(ref z);
            Transform lU = G(HB.LeftUpperArm), rU = G(HB.RightUpperArm), lL = G(HB.LeftLowerArm), rL = G(HB.RightLowerArm), lH = G(HB.LeftHand), rH = G(HB.RightHand);
            Transform lT = G(HB.LeftUpperLeg), rT = G(HB.RightUpperLeg), lK = G(HB.LeftLowerLeg), rK = G(HB.RightLowerLeg), lF = G(HB.LeftFoot), rF = G(HB.RightFoot), hips = G(HB.Hips), neck = G(HB.Neck) ? G(HB.Neck) : G(HB.Head);
            var up = (neck.position - hips.position).normalized; var right = Vector3.ProjectOnPlane(rU.position - lU.position, up).normalized;
            Vector3 Ax(Transform u, Transform l, Transform e, Vector3 anat) { var v1 = l.position - u.position; var v2 = e.position - l.position; var cr = Vector3.Cross(v1, v2); return Vector3.Angle(v1, v2) > 12 && Vector3.Dot(cr, anat) > 0 ? cr.normalized : anat; }
            var kL = Quaternion.Inverse(lT.rotation) * Ax(lT, lK, lF, right); var kR = Quaternion.Inverse(rT.rotation) * Ax(rT, rK, rF, right);
            var eL = Quaternion.Inverse(lU.rotation) * Ax(lU, lL, lH, up); var eR = Quaternion.Inverse(rU.rotation) * Ax(rU, rL, rH, -up);
            // curvas do clipe (valor que o clipe pede para cada músculo)
            var curves = new Dictionary<string, AnimationCurve>(); foreach (var b in AnimationUtility.GetCurveBindings(clip)) curves[b.propertyName] = AnimationUtility.GetEditorCurve(clip, b);
            int inv = 0, side = 0, n = 0; string worst = "-"; float worstD = 1; var readMax = new float[HumanTrait.MuscleCount]; var clipMax = new float[HumanTrait.MuscleCount]; var diffMax = new float[HumanTrait.MuscleCount];
            if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
            for (int s = 0; s <= steps; s++)
            {
                float t = clip.length * s / steps; AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(go, clip, t); AnimationMode.EndSampling(); go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); n++;
                void Hinge(Transform u, Transform l, Transform e, Vector3 axL, string nm)
                {
                    var v1 = l.position - u.position; var v2 = e.position - l.position; float flex = Vector3.Angle(v1, v2); if (flex < 20) return;
                    float d = Vector3.Dot(Vector3.Cross(v1, v2).normalized, (u.rotation * axL).normalized);
                    if (d < -.3f) { inv++; if (d < worstD) { worstD = d; worst = $"{nm} ao contrário dobra {flex:0}° eixo {d:+0.00;-0.00} t={t:0.00}"; } } else if (d < .5f && flex > 35) side++;
                }
                Hinge(lT, lK, lF, kL, "joelho E"); Hinge(rT, rK, rF, kR, "joelho D"); Hinge(lU, lL, lH, eL, "cotovelo E"); Hinge(rU, rL, rH, eR, "cotovelo D");
                hp.GetHumanPose(ref pose);
                for (int m = 0; m < pose.muscles.Length; m++)
                {
                    var nm = HumanTrait.MuscleName[m]; float cv = curves.TryGetValue(nm, out var c) ? c.Evaluate(t) : 0;
                    readMax[m] = Mathf.Max(readMax[m], Mathf.Abs(pose.muscles[m])); clipMax[m] = Mathf.Max(clipMax[m], Mathf.Abs(cv)); diffMax[m] = Mathf.Max(diffMax[m], Mathf.Abs(pose.muscles[m] - cv));
                }
            }
            bool Skip(string m) => m.Contains("Thumb") || m.Contains("Index") || m.Contains("Middle") || m.Contains("Ring") || m.Contains("Little") || m.Contains("Eye") || m.Contains("Jaw");
            var idx = Enumerable.Range(0, readMax.Length).Where(i => !Skip(HumanTrait.MuscleName[i]));
            var hi = idx.Where(i => clipMax[i] > 1.05f).OrderByDescending(i => clipMax[i]).Take(6).Select(i => $"{HumanTrait.MuscleName[i]} {clipMax[i]:0.00}");
            var df = idx.Where(i => diffMax[i] > .1f).OrderByDescending(i => diffMax[i]).Take(5).Select(i => $"{HumanTrait.MuscleName[i]} clipe {clipMax[i]:0.00} lido {readMax[i]:0.00}");
            var line = $"{id} · {clipName} · {clip.length:0.00} s · ao contrário {inv}/{n * 4} ({worst}) · de lado {side} · curva >1,05: [{string.Join("; ", hi)}] · lido≠clipe: [{string.Join("; ", df)}]";
            Debug.Log("[FreeBala][clipaudit] " + line);
            hp.Dispose(); Object.DestroyImmediate(go); return line;
        }
    }
}
