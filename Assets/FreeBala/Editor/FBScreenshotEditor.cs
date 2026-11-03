// Utilitários de captura de tela em alta definição no Unity Editor (Menu BURITIRO > Screenshots).
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBScreenshotEditor
    {
        public static string ScreenshotDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Screenshots"));

        [MenuItem("BURITIRO/Screenshots/📸 Capturar Game View (4K) %&s")]
        public static void CaptureGameView4K()
        {
            Directory.CreateDirectory(ScreenshotDir);
            string time = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string path = Path.Combine(ScreenshotDir, $"Buritiro_GameView_4K_{time}.png");
            ScreenCapture.CaptureScreenshot(path, 2);
            Debug.Log($"[BURITIRO] 📸 Screenshot 4K salva: {path}");
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("BURITIRO/Screenshots/📸 Capturar Game View (8K Ultra-HD)")]
        public static void CaptureGameView8K()
        {
            Directory.CreateDirectory(ScreenshotDir);
            string time = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string path = Path.Combine(ScreenshotDir, $"Buritiro_GameView_8K_{time}.png");
            ScreenCapture.CaptureScreenshot(path, 4);
            Debug.Log($"[BURITIRO] 📸 Screenshot 8K salva: {path}");
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("BURITIRO/Screenshots/📸 Renderizar Câmera Atual em 4K (3840x2160)")]
        public static void RenderCamera4K()
        {
            var cam = Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (!cam)
            {
                EditorUtility.DisplayDialog("Screenshot", "Nenhuma câmera ativa encontrada na cena atual.", "OK");
                return;
            }
            string path = RenderCameraToFile(cam, 3840, 2160, false);
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("BURITIRO/Screenshots/🎨 Renderizar Câmera com Fundo Transparente (PNG Alpha)")]
        public static void RenderCameraTransparent()
        {
            var cam = Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (!cam)
            {
                EditorUtility.DisplayDialog("Screenshot", "Nenhuma câmera ativa encontrada na cena atual.", "OK");
                return;
            }
            string path = RenderCameraToFile(cam, 3840, 2160, true);
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("BURITIRO/Screenshots/📁 Abrir Pasta de Screenshots")]
        public static void OpenFolder()
        {
            Directory.CreateDirectory(ScreenshotDir);
            EditorUtility.RevealInFinder(ScreenshotDir);
        }

        public static string RenderCameraToFile(Camera cam, int width, int height, bool transparent)
        {
            Directory.CreateDirectory(ScreenshotDir);
            string time = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string mode = transparent ? "Alpha" : "UHD";
            string path = Path.Combine(ScreenshotDir, $"Buritiro_Camera_{cam.name}_{mode}_{width}x{height}_{time}.png");

            var prevRT = cam.targetTexture;
            var prevFlags = cam.clearFlags;
            var prevBg = cam.backgroundColor;

            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            if (transparent)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);
            }

            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, transparent ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            cam.targetTexture = prevRT;
            cam.clearFlags = prevFlags;
            cam.backgroundColor = prevBg;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            Debug.Log($"[BURITIRO] 📸 Câmera renderizada e salva: {path}");
            return path;
        }
    }
}
