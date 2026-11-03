// Folha de poses de clipes (Humanoid) num personagem, sem entrar no Play: amostra cada clipe pelo PlayableGraph em N instantes e
// renderiza de lado e de frente; mede as mãos em relação à raiz (onde fica o corpo de quem é revistado, por exemplo).
// Lote: Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBPoseShot.Batch
//   -fbid r_pm_almeida -fbclips FF_REB_Policia_Revista,FF_REB_Rendido -fbframes 6 [-fbpar r_tutu:FF_REB_Rendido:0,0,0.6:180]
//   -> unity/logs/pose_<id>_<clipe>.png (linha de cima: lado; de baixo: frente) + linhas [FreeBala][pose] no log
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBPoseShot
    {
        static string Arg(string k, string d) { var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, k); return i >= 0 && i + 1 < a.Length ? a[i + 1] : d; }

        public static void Batch()
        {
            int code = 0;
            try
            {
                string id = Arg("-fbid", "r_pm_almeida"); int n = int.Parse(Arg("-fbframes", "6"));
                string par = Arg("-fbpar", null);
                foreach (var clip in Arg("-fbclips", "FF_REB_Policia_Revista").Split(',')) Sheet(id, clip, n, par);
            }
            catch (System.Exception e) { Debug.LogError("[FreeBala][pose] " + e); code = 1; }
            EditorApplication.Exit(code);
        }

        static AnimationClip LoadClip(string name)
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/FreeBala/Anim/Clips/{name}.anim");
            if (!c) foreach (var g in AssetDatabase.FindAssets(name + " t:AnimationClip")) { var p = AssetDatabase.GUIDToAssetPath(g); if (Path.GetFileNameWithoutExtension(p) == name) { c = AssetDatabase.LoadAssetAtPath<AnimationClip>(p); break; } }
            return c;
        }

        class Actor { public GameObject go; public Animator an; public AnimationClip clip; }
        static Actor Make(string id, AnimationClip clip, Vector3 pos, float yaw)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{FBCharImport.PrefabDir}/{id}.prefab"); if (!pf) throw new System.Exception("sem prefab " + id);
            var go = Object.Instantiate(pf); go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            var an = go.GetComponent<Animator>(); an.applyRootMotion = false; an.runtimeAnimatorController = null;
            return new Actor { go = go, an = an, clip = clip };
        }
        // AnimationMode amostra clipe Humanoid no editor (o PlayableGraph não avalia fora do Play em lote); a raiz volta ao lugar
        static void Pose(Actor a, float t, Actor b = null, float tb = 0)
        {
            if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
            var p = a.go.transform.position; var r = a.go.transform.rotation; Vector3 pb = default; Quaternion rb = default; if (b != null) { pb = b.go.transform.position; rb = b.go.transform.rotation; }
            AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(a.go, a.clip, t); if (b != null) AnimationMode.SampleAnimationClip(b.go, b.clip, tb); AnimationMode.EndSampling();
            a.go.transform.SetPositionAndRotation(p, r); if (b != null) b.go.transform.SetPositionAndRotation(pb, rb);
        }

        static void Sheet(string id, string clipName, int n, string par)
        {
            var clip = LoadClip(clipName); if (!clip) { Debug.LogError("[FreeBala][pose] sem clipe " + clipName); return; }
            var A = Make(id, clip, Vector3.zero, 0);
            Actor B = null; Vector3 bOff = Vector3.zero; float bYaw = 0;
            if (!string.IsNullOrEmpty(par))
            {   // segundo personagem (quem é revistado): id:clipe:x,y,z:yaw (no referencial do primeiro)
                var s = par.Split(':'); var bc = LoadClip(s[1]); var v = s[2].Split(','); bOff = new Vector3(float.Parse(v[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(v[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(v[2], System.Globalization.CultureInfo.InvariantCulture)); bYaw = float.Parse(s[3], System.Globalization.CultureInfo.InvariantCulture);
                if (bc) B = Make(s[0], bc, bOff, bYaw);
            }
            const int W = 300, H = 420; var sheet = new Texture2D(W * n, H * 2, TextureFormat.RGB24, false);
            var cam = new GameObject("posecam").AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.18f, .2f, .24f); cam.orthographic = true; cam.orthographicSize = 1.25f;
            var rt = new RenderTexture(W, H, 24); cam.targetTexture = rt;
            var lg = new GameObject("poseluz").AddComponent<Light>(); lg.type = LightType.Directional; lg.intensity = 1.6f; lg.transform.rotation = Quaternion.Euler(40, 30, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.55f, .55f, .6f);
            Transform hl = A.an.GetBoneTransform(HumanBodyBones.LeftHand), hr = A.an.GetBoneTransform(HumanBodyBones.RightHand), hips = A.an.GetBoneTransform(HumanBodyBones.Hips), head = A.an.GetBoneTransform(HumanBodyBones.Head);
            var sum = Vector3.zero; int cnt = 0; float minY = 9, maxY = -9, minZ = 9, maxZ = -9;
            // mãos ao longo do clipe todo (30 amostras): onde trabalham, em relação à raiz
            for (int k = 0; k <= 30; k++)
            {
                Pose(A, clip.length * k / 30f);
                foreach (var h in new[] { hl, hr }) { if (!h) continue; var lp = A.go.transform.InverseTransformPoint(h.position); sum += lp; cnt++; minY = Mathf.Min(minY, lp.y); maxY = Mathf.Max(maxY, lp.y); minZ = Mathf.Min(minZ, lp.z); maxZ = Mathf.Max(maxZ, lp.z); }
            }
            Pose(A, 0); var hp0 = hips ? A.go.transform.InverseTransformPoint(hips.position) : Vector3.zero; var hd0 = head ? A.go.transform.InverseTransformPoint(head.position) : Vector3.zero;
            Debug.Log($"[FreeBala][pose] {id} {clipName} len={clip.length:0.00}s loop={clip.isLooping} maos_media={(cnt > 0 ? sum / cnt : Vector3.zero)} y[{minY:0.00},{maxY:0.00}] z[{minZ:0.00},{maxZ:0.00}] quadril0={hp0} cabeca0={hd0}");
            for (int i = 0; i < n; i++)
            {
                float t = n > 1 ? clip.length * i / (n - 1) : 0; Pose(A, t, B, B != null ? Mathf.Min(t, B.clip.length) : 0);
                var ctr = new Vector3(0, 1.0f, B != null ? bOff.z * .5f : 0);
                for (int view = 0; view < 2; view++)
                {
                    var dir = view == 0 ? Vector3.right : Vector3.forward;                           // lado (de +x) / frente (de +z)
                    cam.transform.position = ctr + dir * 6; cam.transform.rotation = Quaternion.LookRotation(-dir);
                    cam.Render(); RenderTexture.active = rt; var tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = null;
                    sheet.SetPixels(i * W, (1 - view) * H, W, H, tex.GetPixels()); Object.DestroyImmediate(tex);
                }
            }
            sheet.Apply();
            var outp = Path.GetFullPath(Path.Combine(Application.dataPath, $"../../logs/pose_{id}_{clipName}{(B != null ? "_par" : "")}.png")); File.WriteAllBytes(outp, sheet.EncodeToPNG());
            Debug.Log("[FreeBala][pose] " + outp);
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            Object.DestroyImmediate(A.go); if (B != null) Object.DestroyImmediate(B.go);
            Object.DestroyImmediate(cam.gameObject); Object.DestroyImmediate(lg.gameObject); Object.DestroyImmediate(sheet);
        }
    }
}
