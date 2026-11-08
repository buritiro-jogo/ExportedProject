// r62 (08/11/2026): MOONWALK — emote EXCLUSIVO do personagem Michael (r_michael).
// Origem: F:\CRIAÇÃO DE GAMES ASSETS\Moonwalk.glb (Mixamo via Sketchfab) → unity/tools/moonwalk/glb_para_fbx.py (Blender: tira o _NN
// dos ossos, exporta FBX em pose T) → aqui: Humanoid com o avatar do próprio FBX → Anim/Clips/NPC_Moonwalk_Michael.anim (no lugar,
// em loop) → FBRegistry. O fb_data ganha a entrada com "only": "r_michael" e "reps" (quantas passadas o emote faz).
// Lote: Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBMoonwalk.Batch
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBMoonwalk
    {
        public const string Id = "NPC_Moonwalk_Michael";
        const string Stage = "Assets/BURITIRO_Animacoes/_r62_moonwalk_stage";
        static string Src => Path.GetFullPath(Path.Combine(Application.dataPath, "../../tools/moonwalk/Moonwalk.fbx"));

        public static void Batch() { int code = 0; try { Run(); } catch (System.Exception e) { Debug.LogError("[FreeBala][moonwalk] " + e); code = 1; } EditorApplication.Exit(code); }

        [MenuItem("FreeBala/r62/Importar Moonwalk (Michael)")]
        public static void Run()
        {
            if (!File.Exists(Src)) throw new System.Exception("FBX não encontrado: " + Src);
            if (AssetDatabase.IsValidFolder(Stage)) AssetDatabase.DeleteAsset(Stage);
            Directory.CreateDirectory(Stage); var fbx = Stage + "/Moonwalk.fbx"; File.Copy(Src, fbx, true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
            mi.animationType = ModelImporterAnimationType.Human; mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true; mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.importBlendShapes = false; mi.importCameras = false; mi.importLights = false;
            mi.resampleCurves = true; mi.animationCompression = ModelImporterAnimationCompression.Off;
            mi.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault();
            if (!avatar || !avatar.isHuman || !avatar.isValid) throw new System.Exception("avatar do Moonwalk inválido");
            var takes = mi.defaultClipAnimations; Debug.Log("[FreeBala][moonwalk] tomadas: " + string.Join(", ", takes.Select(t => t.takeName + $" ({t.lastFrame - t.firstFrame:0} q)")));
            var take = takes.FirstOrDefault(t => t.takeName.Contains("Object_4")) ?? takes.OrderByDescending(t => t.lastFrame - t.firstFrame).First();
            take.name = Id; take.loopTime = true; take.loopPose = true;
            take.lockRootRotation = true; take.keepOriginalOrientation = true; take.lockRootHeightY = true; take.keepOriginalPositionY = true; take.heightFromFeet = false;
            take.lockRootPositionXZ = true; take.keepOriginalPositionXZ = true;   // passada no lugar: o moonwalk do Mixamo já não anda
            mi.clipAnimations = new[] { take }; mi.SaveAndReimport();
            var src = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(x => !x.name.StartsWith("__preview__"));
            if (!src || !src.humanMotion) throw new System.Exception("sem clipe humanoide no FBX");
            var clip = Object.Instantiate(src); clip.name = Id;
            var st = AnimationUtility.GetAnimationClipSettings(clip);
            st.loopTime = true; st.loopBlend = true; st.loopBlendOrientation = true; st.loopBlendPositionY = true; st.loopBlendPositionXZ = true;
            st.keepOriginalOrientation = true; st.keepOriginalPositionY = true; st.keepOriginalPositionXZ = true; st.heightFromFeet = false;
            AnimationUtility.SetAnimationClipSettings(clip, st);
            var path = $"{FBAnimImport.ClipDir}/{Id}.anim"; var old = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (old) { EditorUtility.CopySerialized(clip, old); Object.DestroyImmediate(clip); clip = old; EditorUtility.SetDirty(old); } else AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();
            var reg = AssetDatabase.LoadAssetAtPath<FBRegistry>("Assets/FreeBala/Resources/FBRegistry.asset");
            if (reg) { reg.clips.RemoveAll(x => !x || x.name == Id); reg.clips.Add(clip); EditorUtility.SetDirty(reg); AssetDatabase.SaveAssets(); }
            AssetDatabase.DeleteAsset(Stage);
            Debug.Log($"[FreeBala][moonwalk] {Id}: {clip.length:0.00} s · humano {clip.humanMotion} · loop · registro {(reg ? reg.clips.Count : 0)} clipes");
        }
    }
}
