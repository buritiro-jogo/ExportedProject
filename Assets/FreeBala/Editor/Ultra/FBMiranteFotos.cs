// [mirante 03/11/2026] Fotos antes/depois do Mirante (prompt, item 4): os mesmos ângulos no tier "PC Alto" (antes) e no "Ultra"
// (depois), 1920x1080, na cena Map_orla aberta no editor. Saída: <Buritiro_Unity>\_diag\mirante_fotos\<nivel>_<angulo>.png
// (copie para Docs/mirante/screens/ no commit). Fotos de editor: sem água animada nem o FBQuality de tempo de jogo; para
// isso use o .exe. Lab: "metodo FBMiranteFotos.Tirar".
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class FBMiranteFotos
{
    const string Cena = "Assets/FreeBala/Scenes/Map_orla.unity";
    static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../_diag/mirante_fotos"));
    static readonly Vector3 Torre = new(-29.55f, 0, 68.84f);   // centro da torre no Unity (glTF x espelhado)
    static readonly StringBuilder log = new();

    struct Angulo { public string nome; public Vector3 pos, alvo; public bool porDoSol; }

    [MenuItem("FreeBala/Mapas/Mirante: fotos antes e depois")]
    public static void Tirar()
    {
        log.Clear(); Directory.CreateDirectory(Dir);
        var sc = EditorSceneManager.GetActiveScene(); if (sc.path != Cena) sc = EditorSceneManager.OpenScene(Cena, OpenSceneMode.Single);
        // rio: maior renderer com material de água do rio (para o ângulo da orla)
        // rio: renderers só com o material de água do Rio Branco (os pedaços de terreno também usam esse material junto com outros)
        var aguas = sc.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).Where(r => r.sharedMaterials.Length > 0 && r.sharedMaterials.All(m => m && m.name.StartsWith("A2_agua_rio"))).ToList();
        var agua = aguas.OrderBy(r => (r.bounds.center - Torre).sqrMagnitude).FirstOrDefault();
        Vector3 rio = agua ? agua.bounds.ClosestPoint(Torre) : Torre + new Vector3(-150, 0, 0); rio.y = agua ? agua.bounds.max.y : 0;
        Vector3 paraRio = (rio - Torre); paraRio.y = 0; paraRio = paraRio.sqrMagnitude > 1 ? paraRio.normalized : Vector3.left;   // a malha do rio passa por baixo da torre: lado do rio = -x (foto 3_elevador)
        if ((rio - Torre).sqrMagnitude < 400 + rio.y * rio.y) rio = new Vector3(Torre.x, rio.y, Torre.z) + paraRio * 90;          // margem a ~90 m da torre
        L($"rio {rio} ({(agua ? agua.name : "não achado")}) · direção torre→rio {paraRio}");
        var angulos = new[]
        {
            new Angulo { nome = "1_chao_parque", pos = Torre - paraRio * 55 + Vector3.Cross(Vector3.up, paraRio) * 30 + Vector3.up * 1.7f, alvo = Torre + Vector3.up * 55 },
            new Angulo { nome = "2_base_torre", pos = Torre + new Vector3(0, 1.7f, 26), alvo = Torre + new Vector3(0, 14, 4) },
            new Angulo { nome = "3_elevador", pos = Torre + new Vector3(4, 14, 18), alvo = Torre + new Vector3(0, 12, 4.8f) },
            new Angulo { nome = "4_varanda_25m", pos = Torre + new Vector3(4.05f, 26.6f, -2.5f), alvo = Torre + new Vector3(40, 12, 10) },
            new Angulo { nome = "5_plataforma_87m_cidade", pos = Torre + new Vector3(0, 88.5f, 0) - paraRio * 8, alvo = Torre - paraRio * 400 + Vector3.up * 20 },
            new Angulo { nome = "6_orla_rio", pos = rio - paraRio * 25 + Vector3.up * 6, alvo = rio + paraRio * 120 + Vector3.Cross(Vector3.up, paraRio) * 140 },
            // pôr do sol do jogo (FBWeather "anoitecer"): sol a 8° de elevação, rumo fixo -35°; câmera do lado oposto, sol atrás da torre
            new Angulo { nome = "7_por_do_sol", pos = Torre + Quaternion.Euler(0, -35, 0) * Vector3.forward * 95 + Vector3.up * 3, alvo = Torre + Vector3.up * 50, porDoSol = true },
        };
        var sol = RenderSettings.sun; Quaternion sol0 = sol ? sol.transform.rotation : Quaternion.identity; float int0 = sol ? sol.intensity : 1; Color cor0 = sol ? sol.color : Color.white;
        int q0 = QualitySettings.GetQualityLevel();
        // luz ambiente como o FBWeather monta no jogo (sonda a partir das cores céu/horizonte/chão; no editor a sonda é a assada da cena)
        var amb0 = RenderSettings.ambientProbe;
        { var k = RenderSettings.ambientIntensity; var sh = new SphericalHarmonicsL2(); var eq = RenderSettings.ambientEquatorColor * k;
          sh.AddAmbientLight(eq); sh.AddDirectionalLight(Vector3.up, (RenderSettings.ambientSkyColor * k - eq) * .9f, 1); sh.AddDirectionalLight(Vector3.down, (RenderSettings.ambientGroundColor * k - eq) * .9f, 1);
          L($"sonda de ambiente: assada {amb0[0, 0]:0.00}/{amb0[1, 0]:0.00}/{amb0[2, 0]:0.00} → do jogo {sh[0, 0]:0.00}/{sh[1, 0]:0.00}/{sh[2, 0]:0.00}"); RenderSettings.ambientProbe = sh; }
        float fs0 = RenderSettings.fogStartDistance, fe0 = RenderSettings.fogEndDistance; Color fc0 = RenderSettings.fogColor;
        var sky = RenderSettings.skybox; Vector4 skySun0 = sky && sky.HasProperty("_SunDir") ? sky.GetVector("_SunDir") : Vector4.zero; float skyDusk0 = sky && sky.HasProperty("_Dusk") ? sky.GetFloat("_Dusk") : 0;
        var go = new GameObject("FB_FotoCam") { hideFlags = HideFlags.HideAndDontSave }; var cam = go.AddComponent<Camera>(); cam.fieldOfView = 60; cam.nearClipPlane = .1f; cam.farClipPlane = 3000;
        var cd = go.AddComponent<UniversalAdditionalCameraData>(); cd.renderPostProcessing = true; cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; cd.antialiasingQuality = AntialiasingQuality.High; cd.renderShadows = true;
        var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 }; var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            foreach (var nivel in new[] { "PC Alto", "Ultra" })
            {
                int qi = Array.IndexOf(QualitySettings.names, nivel); if (qi < 0) { L("nível não existe: " + nivel); continue; }
                QualitySettings.SetQualityLevel(qi, true);
                // névoa como o FBWeather deixa no jogo (dia): PC Alto 0,75×início / 0,85×fim; Ultra estende (2,2× / 2,6×)
                float k0 = nivel == "Ultra" ? 2.2f : 1, k1 = nivel == "Ultra" ? 2.6f : 1;
                RenderSettings.fogStartDistance = Mathf.Max(fs0, 70) * k0 * .75f; RenderSettings.fogEndDistance = Mathf.Max(fe0, 512) * k1 * .85f;
                L($"nível {nivel}: pipeline {(QualitySettings.renderPipeline ? QualitySettings.renderPipeline.name : "-")}");
                foreach (var a in angulos)
                {
                    if (sol)
                    {
                        if (a.porDoSol) { sol.transform.rotation = Quaternion.Euler(8, -35, 0); sol.intensity = int0 * .62f; sol.color = new Color(1f, .64f, .40f); RenderSettings.fogColor = new Color(.86f, .62f, .48f); }
                        else { sol.transform.rotation = sol0; sol.intensity = int0; sol.color = cor0; RenderSettings.fogColor = fc0; }
                        if (sky && sky.HasProperty("_SunDir")) { var d = -sol.transform.forward; sky.SetVector("_SunDir", new Vector4(-d.x, d.y, d.z, 0).normalized); if (sky.HasProperty("_Dusk")) sky.SetFloat("_Dusk", a.porDoSol ? 1 : 0); }
                    }
                    cam.transform.position = a.pos; cam.transform.LookAt(a.alvo); cam.targetTexture = rt;
                    cam.Render(); cam.Render();   // 2 quadros: o primeiro aquece sombras/SSAO
                    var prev = RenderTexture.active; RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply(); RenderTexture.active = prev;
                    var arq = Path.Combine(Dir, $"{(nivel == "Ultra" ? "depois_ultra" : "antes_pcalto")}_{a.nome}.png"); File.WriteAllBytes(arq, tex.EncodeToPNG());
                    L($"  {a.nome}: câmera {a.pos} → {a.alvo}");
                }
            }
        }
        catch (Exception e) { L("ERRO " + e); }
        finally
        {
            if (sol) { sol.transform.rotation = sol0; sol.intensity = int0; sol.color = cor0; }
            QualitySettings.SetQualityLevel(q0, true);
            RenderSettings.fogStartDistance = fs0; RenderSettings.fogEndDistance = fe0; RenderSettings.fogColor = fc0; RenderSettings.ambientProbe = amb0;
            if (sky && sky.HasProperty("_SunDir")) { sky.SetVector("_SunDir", skySun0); if (sky.HasProperty("_Dusk")) sky.SetFloat("_Dusk", skyDusk0); }
            cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(go);
            File.WriteAllText(Path.Combine(Dir, "relatorio.txt"), log.ToString());
        }
    }
    static void L(string s) { log.AppendLine(s); Debug.Log("[FreeBala][fotos] " + s); }
}
