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
            // testes PlayMode em lote (-runTests): o Play tem de abrir a cena de teste do Test Framework. Com o Menu forçado aqui, a cena
            // de teste era trocada pelo Menu, nenhum teste começava e o editor ficava no menu para sempre (o Timeout nunca dispara).
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-runTests") >= 0) { EditorSceneManager.playModeStartScene = null; return; }
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
