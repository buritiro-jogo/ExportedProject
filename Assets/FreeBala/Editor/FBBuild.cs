// Builds das 4 plataformas (menu FreeBala > Build e em lote: Unity -batchmode -executeMethod FreeBala.EditorTools.FBBuild.<Alvo>):
//  Windows (IL2CPP x64, .exe) · WebGL (gzip + descompressão embutida: abre em qualquer servidor estático) · Android (APK ARM64 e AAB para
//  a Play Store; assinatura de debug se não houver keystore configurada) · iOS (projeto Xcode — compilar/assinar exige macOS + Xcode).
// Saída em unity/Builds/<plataforma>. Cenas: Menu + todos os Map_* (FBImport monta a lista).
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBBuild
    {
        // Android: texturas ASTC; AAB divide o conteúdo em asset pack de instalação (Play Asset Delivery) — o módulo base da Play Store tem limite de 200 MB
        static void Android(bool aab) { EditorUserBuildSettings.buildAppBundle = aab; EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC; PlayerSettings.Android.splitApplicationBinary = aab; Assinatura(); }
        // [mobile 09/11] Assinatura da Play Store: a chave e as senhas NUNCA entram no repositório. Vêm de variáveis de ambiente do PC que gera o build:
        //  FB_KEYSTORE (caminho do .keystore), FB_KEYSTORE_PASS, FB_KEY_ALIAS (padrão "buritiro"), FB_KEY_PASS (padrão: a mesma senha).
        //  Sem FB_KEYSTORE o build sai com a assinatura de debug (serve para teste e instalação direta do APK, não para a Play Store).
        static void Assinatura()
        {
            string E(string k) => System.Environment.GetEnvironmentVariable(k);
            string ks = E("FB_KEYSTORE"), pass = E("FB_KEYSTORE_PASS");
            if (string.IsNullOrEmpty(ks) || !File.Exists(ks) || string.IsNullOrEmpty(pass)) { PlayerSettings.Android.useCustomKeystore = false; Debug.Log("[FreeBala][build] Android: sem FB_KEYSTORE/FB_KEYSTORE_PASS, assinatura de debug"); return; }
            string alias = E("FB_KEY_ALIAS"), kpass = E("FB_KEY_PASS");
            PlayerSettings.Android.useCustomKeystore = true; PlayerSettings.Android.keystoreName = ks; PlayerSettings.Android.keystorePass = pass;
            PlayerSettings.Android.keyaliasName = string.IsNullOrEmpty(alias) ? "buritiro" : alias; PlayerSettings.Android.keyaliasPass = string.IsNullOrEmpty(kpass) ? pass : kpass;
            Debug.Log("[FreeBala][build] Android: assinando com " + ks);
        }
        // [mobile 09/11] Android/WebGL não leem StreamingAssets como arquivo: copia o servidor.txt para Resources antes do build (FBSession.ServidorPadrao lê de lá)
        static void ServidorParaResources()
        {
            try
            {
                var src = Path.Combine(Application.streamingAssetsPath, "servidor.txt"); if (!File.Exists(src)) return;
                Directory.CreateDirectory(Path.Combine(Application.dataPath, "Resources"));
                File.Copy(src, Path.Combine(Application.dataPath, "Resources/servidor.txt"), true); AssetDatabase.ImportAsset("Assets/Resources/servidor.txt");
            }
            catch (System.Exception e) { Debug.LogWarning("[FreeBala][build] servidor.txt para Resources: " + e.Message); }
        }
        // Compatibilidade por plataforma (Agente 9), aplicada antes de todo build — garante o que o FBProjectSetup não fixa:
        //  Android: só ARM64 (Play Store exige 64 bits; ARMv7 dobraria a lib), IL2CPP, ASTC, frame pacing otimizado (Swappy) contra engasgos de vsync.
        //  iOS: IL2CPP, mínimo iOS 15 (Metal), ARM64. WebGL: WebGL2, memória 512 MB → 4 GB com crescimento geométrico, gzip + descompressão
        //  embutida (abre em qualquer servidor estático), GPU de alto desempenho, cache de dados no IndexedDB. Windows: IL2CPP x64.
        static void Compat(BuildTarget t)
        {
            switch (t)
            {
                case BuildTarget.Android:
                    PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                    PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64; PlayerSettings.Android.optimizedFramePacing = true;
                    if ((int)PlayerSettings.Android.minSdkVersion < 26) PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
                    PlayerSettings.Android.textureCompressionFormats = new[] { TextureCompressionFormat.ASTC };
                    break;
                case BuildTarget.iOS:
                    PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
                    if (!System.Version.TryParse(PlayerSettings.iOS.targetOSVersionString, out var v) || v.Major < 15) PlayerSettings.iOS.targetOSVersionString = "15.0";
                    break;
                case BuildTarget.WebGL:
                    PlayerSettings.WebGL.initialMemorySize = 512; PlayerSettings.WebGL.maximumMemorySize = 4096; PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
                    PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip; PlayerSettings.WebGL.decompressionFallback = true; PlayerSettings.WebGL.dataCaching = true;
                    PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance; PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;   // "None" derrubaria o player no primeiro try/catch
                    break;
                case BuildTarget.StandaloneWindows64:
                    PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, mono ? ScriptingImplementation.Mono2x : ScriptingImplementation.IL2CPP);
                    break;
            }
        }
        // os 40 maiores assets do build (para cortar tamanho: APK/WebGL) em <saída>.assets.txt
        static void AssetReport(BuildReport r, string path)
        {
            try
            {
                var top = r.packedAssets.SelectMany(p => p.contents).GroupBy(c => c.sourceAssetPath).Select(g => (path: g.Key, size: g.Sum(c => (long)c.packedSize))).OrderByDescending(x => x.size).Take(40).ToList();
                var byDir = r.packedAssets.SelectMany(p => p.contents).GroupBy(c => Dir(c.sourceAssetPath)).Select(g => (dir: g.Key, size: g.Sum(c => (long)c.packedSize))).OrderByDescending(x => x.size).Take(25);
                File.WriteAllText(path + ".assets.txt", "== por pasta ==\n" + string.Join("\n", byDir.Select(x => $"{x.size / 1048576f,8:0.0} MB  {x.dir}")) + "\n\n== maiores assets ==\n" + string.Join("\n", top.Select(x => $"{x.size / 1048576f,8:0.0} MB  {x.path}")));
            }
            catch (System.Exception e) { Debug.LogWarning("[FreeBala][build] relatório de assets: " + e.Message); }
        }
        static string Dir(string p) { if (string.IsNullOrEmpty(p)) return "(embutido)"; var parts = p.Split('/'); return string.Join("/", parts.Take(Mathf.Min(parts.Length - 1, 5))); }
        // FB_BUILD_DIR (r43): saída em outro disco (o C: está com pouco espaço; build Android + IL2CPP passa de 10 GB de intermediários)
        static string Out(string p) => Path.GetFullPath(Path.Combine(System.Environment.GetEnvironmentVariable("FB_BUILD_DIR") is string d && d.Length > 0 ? d : Path.Combine(Application.dataPath, "../../Builds"), p));
        // cenas do Build Settings + cenas de teste que existirem (IKTest do Agente 3: o autoteste do executável abre pelo nome)
        static readonly string[] TestScenes = { FBImport.SceneDir + "/IKTest.unity" };
        static string[] Scenes => EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => s.path).Concat(TestScenes.Where(File.Exists)).Distinct().ToArray();
        // [mobile 09/11] Android leve: o build com os 6 mapas deu 12 GB (o APK não fecha acima de ~4 GB). O celular leva só o Menu e os
        //  mapas de FBAndroidLeve.Mapas (padrão: Praça das Águas e Vila Jardim; FB_ANDROID_MAPS=id1,id2 troca a lista). O menu já esconde
        //  sozinho os mapas que não estão no build (FBMenu: Application.CanStreamedLevelBeLoaded).
        static string[] ScenesFor(BuildTarget t)
        {
            if (t != BuildTarget.Android) return Scenes;
            var mapas = FBAndroidLeve.Mapas;
            return Scenes.Where(p => { var n = Path.GetFileNameWithoutExtension(p); return !n.StartsWith("Map_") || mapas.Contains(n.Substring(4)); }).ToArray();
        }
        static bool Run(BuildTarget t, BuildTargetGroup g, string path, BuildOptions o = BuildOptions.None)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (EditorUserBuildSettings.activeBuildTarget != t) EditorUserBuildSettings.SwitchActiveBuildTarget(g, t);
            Compat(t); FBOptimize.GpuSkinning(); if (t == BuildTarget.Android || t == BuildTarget.WebGL) ServidorParaResources();
            FBAndroidLeve.DesempacotarX171();                                                      // texturas dos objetos de rua comprimidas (vale para todas as plataformas)
            var cenas = ScenesFor(t);
            // [lab 09/11] FB_SO_MENU=1: build só com a cena do Menu. O laboratório de animação (-fbanimlab) monta a própria cena, então não
            // precisa dos mapas: a build de laboratório cai de ~15 min e 13 GB para poucos minutos. Use com FB_LAB_OUT para não sobrescrever.
            if (System.Environment.GetEnvironmentVariable("FB_SO_MENU") == "1") cenas = cenas.Take(1).ToArray();
            Debug.Log($"[FreeBala][build] {t}: {cenas.Length} cenas · {string.Join(", ", cenas.Select(Path.GetFileNameWithoutExtension))}");
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = cenas, locationPathName = path, target = t, targetGroup = g, options = o });
            var s = r.summary; Debug.Log($"[FreeBala][build] {t}: {s.result} · {s.totalSize / 1048576f:0} MB · {s.totalTime} · erros {s.totalErrors} · avisos {s.totalWarnings} → {path}");
            File.WriteAllText(path + ".build.txt", $"{t} {s.result} {s.totalSize} bytes {s.totalTime} erros={s.totalErrors}\n" + string.Join("\n", r.steps.SelectMany(st => st.messages).Where(m => m.type == LogType.Error || m.type == LogType.Exception).Select(m => m.content)));
            if (s.result == BuildResult.Succeeded) AssetReport(r, path);
            return s.result == BuildResult.Succeeded;
        }
        [MenuItem("FreeBala/Build/Windows (.exe)")] public static void Windows() => Run(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, Out("Windows/Buritiro.exe"));
        [MenuItem("FreeBala/Build/WebGL")] public static void WebGL() => Run(BuildTarget.WebGL, BuildTargetGroup.WebGL, Out("WebGL"));
        [MenuItem("FreeBala/Build/Android APK")] public static void AndroidAPK() { Android(false); Run(BuildTarget.Android, BuildTargetGroup.Android, Out("Android/Buritiro.apk")); }
        [MenuItem("FreeBala/Build/Android AAB (Play Store)")] public static void AndroidAAB() { Android(true); Run(BuildTarget.Android, BuildTargetGroup.Android, Out("Android/Buritiro.aab")); }
        [MenuItem("FreeBala/Build/iOS (projeto Xcode)")] public static void iOS() => Run(BuildTarget.iOS, BuildTargetGroup.iOS, Out("iOS"));
        // versões em lote que fecham o editor com código de saída
        public static void WindowsDevBatch() => EditorApplication.Exit(Ok(Run(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, Out("WindowsDev/Buritiro.exe"), BuildOptions.Development)));   // diagnóstico (contadores do Profiler)
        // iteração dos testes (laboratório de animação, autoteste): mesmo build de desenvolvimento em MONO — sem a compilação C++ do IL2CPP
        // (~25 min a cada mudança de código). Saída própria (WindowsLab); o IL2CPP volta a ser o padrão logo depois (Compat).
        public static void WindowsLabBatch()
        {
            bool ok;
            var outp = System.Environment.GetEnvironmentVariable("FB_LAB_OUT"); if (string.IsNullOrEmpty(outp)) outp = Out("WindowsLab/Buritiro.exe");   // C: com pouco espaço: o pipeline manda para outro disco
            FBProneClips.Ensure();                                                                // r49: clipe de deitar (FF_SYS_KO_GetUp ao contrário)
            try { mono = true; ok = Run(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, outp, BuildOptions.Development); }
            finally { mono = false; PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP); }
            EditorApplication.Exit(Ok(ok));
        }
        static bool mono;
        public static void WindowsBatch() => EditorApplication.Exit(Ok(Run(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, Out("Windows/Buritiro.exe"))));
        // [parceiro 07/11] entrega em MONO, sem Development: plano B do instalador quando o PC não tem o módulo IL2CPP ou o C++ do Visual Studio.
        // Saída própria (WindowsMono); o IL2CPP volta a ser o padrão logo depois, como no WindowsLabBatch.
        public static void WindowsMonoBatch()
        {
            bool ok;
            try { mono = true; ok = Run(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, Out("WindowsMono/Buritiro.exe")); }
            finally { mono = false; PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP); }
            EditorApplication.Exit(Ok(ok));
        }
        public static void WebGLBatch() => EditorApplication.Exit(Ok(Run(BuildTarget.WebGL, BuildTargetGroup.WebGL, Out("WebGL"))));
        public static void AndroidAPKBatch() { Android(false); EditorApplication.Exit(Ok(Run(BuildTarget.Android, BuildTargetGroup.Android, Out("Android/Buritiro.apk")))); }
        public static void AndroidAABBatch() { Android(true); EditorApplication.Exit(Ok(Run(BuildTarget.Android, BuildTargetGroup.Android, Out("Android/Buritiro.aab")))); }
        public static void iOSBatch() => EditorApplication.Exit(Ok(Run(BuildTarget.iOS, BuildTargetGroup.iOS, Out("iOS"))));
        static int Ok(bool b) => b ? 0 : 1;
        // só remonta a cena do menu e a lista de cenas (rápido, sem reimportar modelos)
        public static void MenuSceneBatch() { var m = typeof(FBImport).GetMethod("MenuScene", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static); m.Invoke(null, null); EditorApplication.Exit(0); }
    }
}
