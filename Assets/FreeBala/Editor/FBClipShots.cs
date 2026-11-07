// r48: FOLHA DE FOTOS DE CLIPE NO EDITOR (sem build) — o clipe PURO no personagem (PlayableGraph, sem IK, sem lógica de jogo e sem o
// veículo na frente), em 4 ângulos × N tempos, para ver o que o retarget entrega antes de qualquer correção em jogo.
// Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBClipShots.Batch
//   -fbcs_chars r_elaine,x171_gorda1 -fbcs_clips X171_Go_to_bike_L,... [-fbcs_n 8] [-fbcs_dir Assets/FreeBala/Anim/Clips] [-fbcs_tag r48b]
//   -> unity/logs/<tag>_cs/<personagem>__<clipe>.png (linhas: frente, lado esquerdo, lado direito, de cima; colunas: tempos)
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FreeBala.EditorTools
{
    public static class FBClipShots
    {
        const int Cell = 300;
        static string Arg(string k, string d) { var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, k); return i >= 0 && i + 1 < a.Length ? a[i + 1] : d; }

        public static void Batch()
        {
            int code = 0;
            try
            {
                Run(Arg("-fbcs_chars", "r_tutu").Split(','), Arg("-fbcs_clips", "X171_Go_to_bike_L").Split(','), int.Parse(Arg("-fbcs_n", "8")),
                    Arg("-fbcs_dir", "Assets/FreeBala/Anim/Clips"), Arg("-fbcs_tag", "cs"));
            }
            catch (System.Exception e) { Debug.LogError("[FreeBala][cs] " + e); code = 1; }
            EditorApplication.Exit(code);
        }

        public static void Run(string[] chars, string[] clips, int n, string dir, string tag)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, $"../../logs/{tag}_cs")); Directory.CreateDirectory(outDir);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.45f, .45f, .5f);
            var lg = new GameObject("cs_luz").AddComponent<Light>(); lg.type = LightType.Directional; lg.intensity = 1.3f; lg.shadows = LightShadows.None;
            lg.transform.rotation = Quaternion.Euler(40, 150, 0);
            var floorMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); floorMat.SetColor("_BaseColor", new Color(.32f, .34f, .3f));
            var floor = GameObject.CreatePrimitive(PrimitiveType.Quad); floor.transform.rotation = Quaternion.Euler(90, 0, 0); floor.transform.localScale = Vector3.one * 30;
            floor.GetComponent<Renderer>().sharedMaterial = floorMat; Object.DestroyImmediate(floor.GetComponent<Collider>());
            var cam = new GameObject("cs_cam").AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.62f, .68f, .76f);
            cam.orthographic = true; cam.orthographicSize = 1.15f; cam.nearClipPlane = .05f; cam.farClipPlane = 50;
            var rt = new RenderTexture(Cell, Cell, 24); cam.targetTexture = rt;
            // vista: direção de onde a câmera olha (o personagem nasce olhando para +Z)
            var views = new (string nome, Vector3 dirCam)[] { ("frente", new Vector3(0, .05f, 1)), ("lado_esq", new Vector3(-1, .05f, 0)), ("lado_dir", new Vector3(1, .05f, 0)), ("cima", new Vector3(-.6f, .9f, .6f)) };
            foreach (var id in chars)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/FreeBala/Resources/FBPrefabs/Chars/{id}.prefab"); if (!pf) { Debug.LogError("[FreeBala][cs] sem prefab " + id); continue; }
                var go = Object.Instantiate(pf); go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var an = go.GetComponent<Animator>() ?? go.GetComponentInChildren<Animator>(); if (!an || !an.avatar || !an.avatar.isHuman) { Debug.LogError("[FreeBala][cs] sem avatar humano " + id); Object.DestroyImmediate(go); continue; }
                an.cullingMode = AnimatorCullingMode.AlwaysAnimate; an.applyRootMotion = false; an.runtimeAnimatorController = null;
                var hips = an.GetBoneTransform(HumanBodyBones.Hips);
                // r55: em lote o skinning não roda entre as fotos (só no laço do player) → cópia "assada" da malha na pose do instante
                var baked = new List<(SkinnedMeshRenderer smr, Mesh mesh, Transform t)>();
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var bo = new GameObject("cs_baked_" + smr.name); var m = new Mesh(); bo.AddComponent<MeshFilter>().sharedMesh = m;
                    bo.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials; smr.enabled = false; baked.Add((smr, m, bo.transform));
                }
                // r55: BakeMesh(useScale: false) já entrega os vértices no tamanho de mundo destes personagens — pôr também
                // localScale = lossyScale aplicava a escala DUAS vezes (corpo ~2,2× maior que o esqueleto: a folha só mostrava as pernas).
                void Bake() { foreach (var b in baked) { b.smr.BakeMesh(b.mesh, false); b.t.SetPositionAndRotation(b.smr.transform.position, b.smr.transform.rotation); b.t.localScale = Vector3.one; } }
                void Sample(AnimationClip cl, float t) { if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode(); AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(go, cl, t); AnimationMode.EndSampling(); }
                foreach (var cid in clips)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{dir}/{cid}.anim"); if (!clip) { Debug.LogError("[FreeBala][cs] sem clipe " + dir + "/" + cid); continue; }
                    var g = PlayableGraph.Create("cs"); g.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var cp = AnimationClipPlayable.Create(g, clip); cp.SetApplyFootIK(false);
                    var o = AnimationPlayableOutput.Create(g, "o", an); o.SetSourcePlayable(cp);
                    var sheet = new Texture2D(Cell * n, Cell * views.Length, TextureFormat.RGB24, false); var times = new List<string>();
                    // r55: enquadramento do clipe inteiro, medido UMA vez na metade dele pela caixa do que é realmente desenhado (as
                    // cópias assadas). Antes a câmera era fixa (raio 1,15 no quadril) e o corpo saía maior que a janela: as folhas
                    // mostravam só as pernas. Medir uma vez só mantém o personagem do mesmo tamanho em todas as colunas.
                    Sample(clip, clip.length * .5f); Bake();
                    var bb = new Bounds(hips.position, Vector3.one * .1f);   // pelos OSSOS humanos: a caixa da malha estoura com um
                    for (int hb = 0; hb < (int)HumanBodyBones.LastBone; hb++)   // vértice solto (dedo) e o corpo sairia minúsculo
                    { var t2 = an.GetBoneTransform((HumanBodyBones)hb); if (t2) bb.Encapsulate(t2.position); }
                    bb.Expand(.22f);
                    var ctr = bb.center; float half = Mathf.Max(.6f, bb.extents.y, bb.extents.x, bb.extents.z) * 1.18f, dist = Mathf.Max(6, half * 5);
                    cam.orthographicSize = half;
                    Debug.Log($"[FreeBala][cs] {id} × {cid}: caixa {bb.size.y:0.00} u · câmera {half:0.00} · centro {ctr.y:0.00}");
                    for (int k = 0; k < n; k++)
                    {
                        float t = n == 1 ? 0 : clip.length * k / (n - 1); times.Add(t.ToString("0.00"));
                        // r55: no Unity 6.6 em lote o grafo manual só aplicava a 1ª avaliação (todas as fotos iguais, inclusive de clipes antigos)
                        // → amostra pelo AnimationMode (a prévia do Editor); -fbcs_grafo 1 = caminho antigo
                        if (Arg("-fbcs_grafo", "0") == "1") { cp.SetTime(t); cp.SetTime(t); g.Evaluate(0); }
                        else Sample(clip, t);
                        Bake();
                        for (int v = 0; v < views.Length; v++)
                        {
                            cam.transform.position = ctr + views[v].dirCam.normalized * dist; cam.transform.LookAt(ctr);
                            cam.Render(); RenderTexture.active = rt; sheet.ReadPixels(new Rect(0, 0, Cell, Cell), k * Cell, (views.Length - 1 - v) * Cell); RenderTexture.active = null;
                        }
                    }
                    sheet.Apply(); var p = Path.Combine(outDir, $"{id}__{cid}.png"); File.WriteAllBytes(p, sheet.EncodeToPNG()); Object.DestroyImmediate(sheet);
                    Debug.Log($"[FreeBala][cs] {id} × {cid} ({clip.length:0.00} s) tempos {string.Join(" ", times)} -> {p}");
                    g.Destroy();
                }
                foreach (var b in baked) { Object.DestroyImmediate(b.t.gameObject); Object.DestroyImmediate(b.mesh); }
                Object.DestroyImmediate(go);
            }
        }
    }
}
