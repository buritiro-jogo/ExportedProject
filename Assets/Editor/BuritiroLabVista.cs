// BURITIRO lab: por que a foto aérea da Vila Jardim sai sem chão e sem paredes?
// Lab: metodo BuritiroLabVista.Aerea  -> _diag\vista_aerea\relatorio.txt + fotos
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class BuritiroLabVista
{
    const string Cena = "Assets/FreeBala/Scenes/Map_vila_jardim.unity";
    static string Base => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
    static string Dir => Path.Combine(Base, "_diag", "vista_aerea");
    static readonly StringBuilder sb = new StringBuilder();
    static void L(string s) { sb.AppendLine(s); }
    static string V(Vector3 v) => $"({v.x:0.0}, {v.y:0.0}, {v.z:0.0})";

    public static void Aerea()
    {
        sb.Clear();
        Directory.CreateDirectory(Dir);
        string antes = SceneManager_GetActivePath();
        try { Rodar(); }
        catch (Exception e) { L("ERRO " + e); }
        finally
        {
            File.WriteAllText(Path.Combine(Dir, "relatorio.txt"), sb.ToString(), new UTF8Encoding(false));
            if (!string.IsNullOrEmpty(antes) && antes != Cena) EditorSceneManager.OpenScene(antes, OpenSceneMode.Single);
        }
    }

    static string SceneManager_GetActivePath() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;

    static void Rodar()
    {
        L("vista aérea " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        L("lodBias " + QualitySettings.lodBias + "  maxLOD " + QualitySettings.maximumLODLevel + "  qualidade " + QualitySettings.names[QualitySettings.GetQualityLevel()]
          + "  pipeline " + (GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.name : "nenhum"));
        try { L("meshLodThreshold " + QualitySettings.meshLodThreshold); } catch { }
        EditorSceneManager.OpenScene(Cena, OpenSceneMode.Single);
        Physics.SyncTransforms();
        L("skybox " + (RenderSettings.skybox ? RenderSettings.skybox.name + " / " + RenderSettings.skybox.shader.name : "nenhum") + "  fog " + RenderSettings.fog);

        var de = new Vector3(-49.29f, 30.04f, 39.54f);
        var para = new Vector3(-46.5f, 0.06f, 2f);
        var baixo = new Vector3(-45.22f, 1.76f, 8.57f);

        var go = new GameObject("VISTA_Cam"); go.hideFlags = HideFlags.HideAndDontSave;
        var cam = go.AddComponent<Camera>(); cam.fieldOfView = 60; cam.nearClipPlane = 0.1f; cam.farClipPlane = 1500f;
        var ucd = go.AddComponent<UniversalAdditionalCameraData>(); ucd.renderPostProcessing = false;
        var rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
        try
        {
            go.transform.position = de; go.transform.rotation = Quaternion.LookRotation(para - de, Vector3.up);
            Foto(cam, rt, tex, "a1_padrao");
            Foto(cam, rt, tex, "a2_de_novo");
            cam.useOcclusionCulling = false;
            Foto(cam, rt, tex, "a3_sem_oclusao");
            cam.useOcclusionCulling = true;

            // raios da câmera aérea: o que deveria aparecer em cada ponto
            var planes = GeometryUtility.CalculateFrustumPlanes(cam);
            var vistos = new Dictionary<Renderer, string>();
            for (int iy = 1; iy <= 6; iy++)
                for (int ix = 1; ix <= 8; ix++)
                {
                    var ray = cam.ViewportPointToRay(new Vector3(ix / 9f, iy / 7f, 0));
                    if (!Physics.Raycast(ray, out var hit, 1500f, ~0, QueryTriggerInteraction.Ignore)) { L($"raio {ix},{iy}: nada"); continue; }
                    var r = AchaRenderer(hit);
                    string txt = r ? Descreve(r, planes, cam) : "sem renderer (colisor " + hit.collider.name + ")";
                    L($"raio {ix},{iy}: acerta {hit.collider.name} a {hit.distance:0.0} m em {V(hit.point)} -> {txt}");
                    if (r) vistos[r] = txt;
                }

            // a mesma coisa da câmera baixa, para comparar
            go.transform.position = baixo; go.transform.rotation = Quaternion.LookRotation(para - baixo, Vector3.up);
            Foto(cam, rt, tex, "b1_baixa");
            var planesB = GeometryUtility.CalculateFrustumPlanes(cam);
            L("--- os mesmos renderers vistos da câmera baixa:");
            foreach (var kv in vistos.Take(25)) L("  " + kv.Key.name + ": " + Descreve(kv.Key, planesB, cam));

            // lista de LODGroups com o renderer do chão
            L("--- LODGroups dos renderers acertados:");
            foreach (var r in vistos.Keys.Take(25))
            {
                var lg = r.GetComponentInParent<LODGroup>();
                if (!lg) { L("  " + r.name + ": sem LODGroup"); continue; }
                var lods = lg.GetLODs();
                L($"  {r.name}: LODGroup {lg.name} tamanho {lg.size:0.0} local {V(lg.localReferencePoint)} lods " + string.Join(" | ", lods.Select((l, i) => $"{i}:{l.screenRelativeTransitionHeight:0.###} n{l.renderers.Length} tem={l.renderers.Contains(r)}")) + $"  fade {lg.fadeMode} animado {lg.animateCrossFading}");
            }
        }
        finally
        {
            cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(go);
        }
    }

    static Renderer AchaRenderer(RaycastHit hit)
    {
        var t = hit.collider.transform;
        var r = t.GetComponent<Renderer>();
        if (r) return r;
        r = t.GetComponentInChildren<Renderer>();
        if (r) return r;
        return t.parent ? t.parent.GetComponentInChildren<Renderer>() : null;
    }

    static string Descreve(Renderer r, Plane[] planes, Camera cam)
    {
        var mf = r.GetComponent<MeshFilter>();
        var m = mf ? mf.sharedMesh : null;
        var mat = r.sharedMaterial;
        bool noFrustum = GeometryUtility.TestPlanesAABB(planes, r.bounds);
        var lg = r.GetComponentInParent<LODGroup>();
        return $"{r.name} ativo={r.gameObject.activeInHierarchy} enabled={r.enabled} forceOff={r.forceRenderingOff} layer={LayerMask.LayerToName(r.gameObject.layer)} " +
               $"lote={r.isPartOfStaticBatch} bounds c{V(r.bounds.center)} t{V(r.bounds.size)} noFrustum={noFrustum} " +
               $"malha={(m ? m.name + " v" + m.vertexCount + " sub" + m.subMeshCount : "nenhuma")} mat={(mat ? mat.name + "/" + mat.shader.name + " fila" + mat.renderQueue : "nenhum")} " +
               $"LODGroup={(lg ? lg.name : "-")} sombra={r.shadowCastingMode}";
    }

    static void Foto(Camera cam, RenderTexture rt, Texture2D tex, string nome)
    {
        cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(Path.Combine(Dir, nome + ".jpg"), tex.EncodeToJPG(85));
        L("foto " + nome + " de " + V(cam.transform.position));
    }
}
