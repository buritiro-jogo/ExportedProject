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
        var agua = sc.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).Where(r => r.sharedMaterials.Any(m => m && m.name.ToLower().Contains("agua_rio"))).OrderByDescending(r => r.bounds.size.sqrMagnitude).FirstOrDefault();
        Vector3 rio = agua ? agua.bounds.center : Torre + new Vector3(60, 0, 0); rio.y = agua ? agua.bounds.max.y : 0;
        Vector3 paraRio = (rio - Torre); paraRio.y = 0; paraRio = paraRio.sqrMagnitude > 1 ? paraRio.normalized : Vector3.right;
        L($"rio {rio} ({(agua ? agua.name : "não achado")}) · direção torre→rio {paraRio}");
        var angulos = new[]
        {
            new Angulo { nome = "1_chao_parque", pos = Torre - paraRio * 55 + Vector3.Cross(Vector3.up, paraRio) * 30 + Vector3.up * 1.7f, alvo = Torre + Vector3.up * 55 },
            new Angulo { nome = "2_base_torre", pos = Torre + new Vector3(0, 1.7f, 26), alvo = Torre + new Vector3(0, 14, 4) },
            new Angulo { nome = "3_elevador", pos = Torre + new Vector3(4, 14, 18), alvo = Torre + new Vector3(0, 12, 4.8f) },
            new Angulo { nome = "4_varanda_25m", pos = Torre + new Vector3(4.05f, 26.6f, -2.5f), alvo = Torre + new Vector3(40, 12, 10) },
            new Angulo { nome = "5_plataforma_87m_cidade", pos = Torre + new Vector3(0, 88.5f, 0) - paraRio * 8, alvo = Torre - paraRio * 400 + Vector3.up * 20 },
            new Angulo { nome = "6_orla_rio", pos = Torre + paraRio * 40 + Vector3.Cross(Vector3.up, paraRio) * 40 + Vector3.up * 3, alvo = rio },
            new Angulo { nome = "7_por_do_sol", pos = Torre + paraRio * 70 + Vector3.Cross(Vector3.up, paraRio) * -50 + Vector3.up * 4, alvo = Torre + Vector3.up * 45, porDoSol = true },
        };
        var sol = RenderSettings.sun; Quaternion sol0 = sol ? sol.transform.rotation : Quaternion.identity; float int0 = sol ? sol.intensity : 1; Color cor0 = sol ? sol.color : Color.white;
        int q0 = QualitySettings.GetQualityLevel();
        var go = new GameObject("FB_FotoCam") { hideFlags = HideFlags.HideAndDontSave }; var cam = go.AddComponent<Camera>(); cam.fieldOfView = 60; cam.nearClipPlane = .1f; cam.farClipPlane = 3000;
        var cd = go.AddComponent<UniversalAdditionalCameraData>(); cd.renderPostProcessing = true; cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; cd.antialiasingQuality = AntialiasingQuality.High; cd.renderShadows = true;
        var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 }; var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            foreach (var nivel in new[] { "PC Alto", "Ultra" })
            {
                int qi = Array.IndexOf(QualitySettings.names, nivel); if (qi < 0) { L("nível não existe: " + nivel); continue; }
                QualitySettings.SetQualityLevel(qi, true);
                L($"nível {nivel}: pipeline {(QualitySettings.renderPipeline ? QualitySettings.renderPipeline.name : "-")}");
                foreach (var a in angulos)
                {
                    if (sol)
                    {
                        if (a.porDoSol) { sol.transform.rotation = Quaternion.LookRotation(new Vector3(paraRio.x, -.09f, paraRio.z));  /* sol baixo atrás da torre, vindo para a câmera */ sol.intensity = int0 * .55f; sol.color = new Color(1f, .62f, .38f); }
                        else { sol.transform.rotation = sol0; sol.intensity = int0; sol.color = cor0; }
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
            cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(go);
            File.WriteAllText(Path.Combine(Dir, "relatorio.txt"), log.ToString());
        }
    }
    static void L(string s) { log.AppendLine(s); Debug.Log("[FreeBala][fotos] " + s); }
}
