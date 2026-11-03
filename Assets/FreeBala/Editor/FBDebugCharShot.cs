// Diagnóstico de personagem: renderiza o prefab em 3 modos (só textura sem luz, só luz de trás, só luz da frente) para separar defeito de
// malha/UV de defeito de iluminação/normal. Lote: Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBDebugCharShot.Batch
//   -fbid r_petrik[,r_tutu] -> unity/logs/dbg_<id>_<modo>.png
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBDebugCharShot
    {
        public static void Batch()
        {
            int code = 0;
            try
            {
                var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbid");
                foreach (var id in (i >= 0 && i + 1 < a.Length ? a[i + 1] : "r_petrik").Split(',')) Shot(id);
            }
            catch (System.Exception e) { Debug.LogError("[FreeBala][dbg] " + e); code = 1; }
            EditorApplication.Exit(code);
        }
        static void Shot(string id)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{FBCharImport.PrefabDir}/{id}.prefab"); if (!pf) { Debug.LogError("[FreeBala][dbg] sem prefab " + id); return; }
            var go = Object.Instantiate(pf); go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.Euler(0, 180, 0);
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) l.enabled = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = Color.black;
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(); var b = smr.bounds;
            var cam = new GameObject("dbgcam").AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.15f, .15f, .2f);
            cam.orthographic = true; cam.orthographicSize = b.size.y * .14f; cam.transform.position = new Vector3(b.center.x, b.max.y - b.size.y * .12f, b.center.z - 5); cam.transform.LookAt(cam.transform.position + Vector3.forward);
            var rt = new RenderTexture(700, 700, 24); cam.targetTexture = rt;
            var lit = smr.sharedMaterials; var tex = lit[0].mainTexture;
            var unlit = new Material(Shader.Find("Universal Render Pipeline/Unlit")); unlit.SetTexture("_BaseMap", tex); unlit.SetFloat("_Cull", 0);
            var lg = new GameObject("dbglight").AddComponent<Light>(); lg.type = LightType.Directional; lg.intensity = 2; lg.shadows = LightShadows.None;
            void Save(string mode)
            {
                cam.Render(); RenderTexture.active = rt; var t = new Texture2D(700, 700, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, 700, 700), 0, 0); t.Apply(); RenderTexture.active = null;
                var p = Path.GetFullPath(Path.Combine(Application.dataPath, "../../logs/dbg_" + id + "_" + mode + ".png")); File.WriteAllBytes(p, t.EncodeToPNG()); Debug.Log("[FreeBala][dbg] " + p);
            }
            smr.sharedMaterials = new[] { unlit }; lg.enabled = false; Save("textura");
            smr.sharedMaterials = lit; lg.enabled = true;
            lg.color = new Color(1, .5f, .05f); lg.transform.rotation = Quaternion.LookRotation(new Vector3(0, -.2f, -1)); Save("luz_tras");   // vem de trás (luz aponta para -z, câmera em -z)
            lg.color = Color.white; lg.transform.rotation = Quaternion.LookRotation(new Vector3(0, -.3f, 1)); Save("luz_frente");
            var one = new Material(lit[0]); if (one.HasProperty("_Cull")) one.SetFloat("_Cull", 2); one.doubleSidedGI = false;
            foreach (var kw in one.shaderKeywords) if (kw.ToUpper().Contains("DOUBLE")) one.DisableKeyword(kw);
            smr.sharedMaterials = new[] { one }; Save("um_lado_frente");
            if (tex) { var bias = tex.mipMapBias; tex.mipMapBias = -8; Save("um_lado_sem_mip"); tex.mipMapBias = bias; }   // força o nível 0 do mipmap
            smr.sharedMaterials = lit;
            Debug.Log("[FreeBala][dbg] keywords " + string.Join(",", lit[0].shaderKeywords) + " · props cull " + string.Join(",", System.Array.FindAll(lit[0].GetPropertyNames(MaterialPropertyType.Float), n => n.ToLower().Contains("cull") || n.ToLower().Contains("double"))));
            Debug.Log($"[FreeBala][dbg] {id}: material {lit[0].shader.name} cull {(lit[0].HasProperty("_Cull") ? lit[0].GetFloat("_Cull") : -1)} malha {smr.sharedMesh.vertexCount} vértices");
            Object.DestroyImmediate(go); Object.DestroyImmediate(cam.gameObject); Object.DestroyImmediate(lg.gameObject);
        }
    }
}
