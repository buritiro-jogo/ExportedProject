// Personagens: GLB (importado pelo glTFast) + <id>.avatar.json (o Avatar Humanoid ORIGINAL do Unity, extraído do APK por tools/char2.py:
// T-pose do esqueleto, mapa osso humano -> nó). Monta o Avatar com AvatarBuilder usando exatamente aquela T-pose, então os clipes de
// músculo caem no corpo como no jogo de origem; e salva um prefab com Animator + controller + componentes do jogo.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBCharImport
    {
        public const string PrefabDir = "Assets/FreeBala/Resources/FBPrefabs/Chars", AvatarDir = "Assets/FreeBala/Anim/Avatars";
        // ordem interna do Mecanim (avatar.json "bones") -> nome do HumanTrait
        static readonly string[] BODY = { "Hips", "LeftUpperLeg", "RightUpperLeg", "LeftLowerLeg", "RightLowerLeg", "LeftFoot", "RightFoot", "Spine", "Chest", "UpperChest", "Neck", "Head",
            "LeftShoulder", "RightShoulder", "LeftUpperArm", "RightUpperArm", "LeftLowerArm", "RightLowerArm", "LeftHand", "RightHand", "LeftToes", "RightToes", "LeftEye", "RightEye", "Jaw" };
        static readonly string[] FING = { "Thumb", "Index", "Middle", "Ring", "Little" }, PH = { "Proximal", "Intermediate", "Distal" };
        public const float ModelScale = 2.2f;                                         // mesmo fator do jogo web (TYPES[id].k)

        public static GameObject Import(string id, string glbAssetPath, string avatarJson, string sheetJson, RuntimeAnimatorController ctrl)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(glbAssetPath);
            if (!src) { Debug.LogWarning("[FreeBala] GLB não importado: " + glbAssetPath); return null; }
            var av = Json.Parse(File.ReadAllText(avatarJson));
            var go = (GameObject)Object.Instantiate(src); go.name = id;
            go.transform.localScale = Vector3.one;
            var all = new Dictionary<string, Transform>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (!all.ContainsKey(t.name)) all[t.name] = t;
            var nodes = Json.L(av, "nodes");
            string NodeName(int idx) => idx >= 0 && idx < nodes.Count ? Json.S(nodes[idx], "name") : null;
            // ossos humanos
            var human = new List<HumanBone>();
            void Add(int idx, string hn) { var n = NodeName(idx); if (string.IsNullOrEmpty(n) || !all.ContainsKey(n)) return; var hb = new HumanBone { boneName = n, humanName = hn }; hb.limit.useDefaultValues = true; human.Add(hb); }
            var bones = Json.L(av, "bones");
            for (int i = 0; i < BODY.Length && i < bones.Count; i++) Add((int)Json.Fv(bones[i], -1), BODY[i]);
            foreach (var (side, key) in new[] { ("Left", "lhand"), ("Right", "rhand") })
            {
                var H = Json.L(av, key); if (H == null) continue;
                for (int f = 0; f < 5; f++) for (int j = 0; j < 3; j++) if (3 * f + j < H.Count) Add((int)Json.Fv(H[3 * f + j], -1), $"{side} {FING[f]} {PH[j]}");
            }
            // esqueleto: T-pose do avatar original onde houver, pose do modelo no resto
            var tpose = new Dictionary<string, (Vector3 t, Quaternion q)>();
            foreach (var n in nodes)
            {
                var nm = Json.S(n, "name"); if (string.IsNullOrEmpty(nm)) continue;
                var t = Json.Floats(Json.L(n, "t")); var q = Json.Floats(Json.L(n, "q"));
                if (t.Length == 3 && q.Length == 4) tpose[nm] = (new Vector3(t[0], t[1], t[2]), new Quaternion(q[0], q[1], q[2], q[3]));
            }
            var skel = new List<SkeletonBone>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                var has = tpose.TryGetValue(t.name, out var p) && t != go.transform;
                skel.Add(new SkeletonBone { name = t.name, position = has ? p.t : t.localPosition, rotation = has ? p.q : t.localRotation, scale = t.localScale });
                if (has) { t.localPosition = p.t; t.localRotation = p.q; }
            }
            var hd = new HumanDescription
            {
                human = human.ToArray(), skeleton = skel.ToArray(), upperArmTwist = .5f, lowerArmTwist = .5f, upperLegTwist = .5f, lowerLegTwist = .5f,
                armStretch = .05f, legStretch = .05f, feetSpacing = 0, hasTranslationDoF = false,
            };
            var avatar = AvatarBuilder.BuildHumanAvatar(go, hd); avatar.name = id + "Avatar";
            Directory.CreateDirectory(AvatarDir);
            var apath = $"{AvatarDir}/{id}.asset"; AssetDatabase.DeleteAsset(apath); AssetDatabase.CreateAsset(avatar, apath);
            if (!avatar.isValid || !avatar.isHuman) Debug.LogWarning($"[FreeBala] avatar inválido: {id} ({human.Count} ossos)");
            var anim = go.GetOrAdd<Animator>();
            anim.avatar = avatar; anim.runtimeAnimatorController = ctrl; anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            go.transform.localScale = Vector3.one * ModelScale;
            var ch = go.AddComponent<FBCharacter>(); ch.skinId = id;
            if (File.Exists(sheetJson)) { var w = go.AddComponent<FBWardrobe>(); w.sheetJson = File.ReadAllText(sheetJson); }
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { r.updateWhenOffscreen = false; r.skinnedMotionVectors = false; }
            Directory.CreateDirectory(PrefabDir);
            var path = $"{PrefabDir}/{id}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
