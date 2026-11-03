// Toda abertura do editor (inclusive em lote, testes e builds): desliga o depurador de jobs antes de qualquer importação — o glTFast 6.14
// tem uma corrida no SortAndNormalizeBoneWeightsJob que só aparece com ele ligado e faz GLBs com pele (personagens n_*, algumas armas) falharem.
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace FreeBala.EditorTools
{
    [InitializeOnLoad]
    static class FBEditorBoot
    {
        static FBEditorBoot()
        {
            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = false;
            var menuScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/FreeBala/Scenes/Menu.unity");
            if (menuScene != null && EditorSceneManager.playModeStartScene != menuScene)
            {
                EditorSceneManager.playModeStartScene = menuScene;
            }
        }

        [MenuItem("FreeBala/Mobile/Gerar APK Mobile (Beta)")]
        public static void BuildMobileApk()
        {
            FBBuild.AndroidAPK();
        }

        [MenuItem("FreeBala/Mobile/Alternar HUD Mobile (Ativar ou Desativar)")]
        public static void ToggleMobileHud()
        {
            FBSession.Load();
            FBSession.mobileHUD = !FBSession.mobileHUD;
            FBSession.Save();
            Debug.Log("[FreeBala] HUD Mobile: " + (FBSession.mobileHUD ? "ATIVADO" : "DESATIVADO"));
        }
    }
}
