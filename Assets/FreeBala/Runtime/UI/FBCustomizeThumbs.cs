// Miniaturas da grade de personagens: retrato de busto renderizado do PRÓPRIO modelo 3D (como os retratos do web), um personagem por
// vez, longe do palco (x = 1000) numa câmera desligada com camada própria e luz própria (não aparece na vitrine nem ilumina o palco).
// Cache em memória (vale enquanto o jogo estiver aberto) e em disco (persistentDataPath/fb_thumbs/v<N>/<id>.png), então só a primeira
// abertura renderiza. Prefab carregado direto do Resources (fora do cache do FBRegistry) e descarregado a cada lote: abrir a grade
// inteira não segura os 170 personagens na memória do celular/WebGL. Pedidos mais recentes (o que está na tela agora) saem primeiro.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
    public class FBCustomizeThumbs : MonoBehaviour
    {
        public const int Version = 2, Layer = 30;
        public static int W => Application.isMobilePlatform ? 160 : 224;
        public static int H => W * 5 / 4;
        static readonly Dictionary<string, Texture2D> mem = new();
        readonly List<(string id, System.Action<Texture2D> cb)> queue = new(); readonly Dictionary<string, List<System.Action<Texture2D>>> waiting = new();
        Camera tcam; Light tlight, tfill; RenderTexture rt; GameObject inst; string cur; int frames, rendered; bool diskOk = true;
        static readonly Vector3 Spot = new(1000, 0, 0);
        public int Pending => queue.Count + (cur != null ? 1 : 0);
        public static bool Cached(string id) => mem.ContainsKey(id) && mem[id];
        public static Texture2D Get(string id) => mem.TryGetValue(id, out var t) ? t : null;
        static string Dir => System.IO.Path.Combine(Application.persistentDataPath, "fb_thumbs", "v" + Version);

        public static FBCustomizeThumbs Create(Transform parent)
        {
            var g = new GameObject("FB_Miniaturas"); g.transform.SetParent(parent, false); var t = g.AddComponent<FBCustomizeThumbs>();
            var cg = new GameObject("cam_miniatura"); cg.transform.SetParent(g.transform, false); t.tcam = cg.AddComponent<Camera>(); t.tcam.enabled = false;
            t.tcam.cullingMask = 1 << Layer; t.tcam.clearFlags = CameraClearFlags.SolidColor; t.tcam.backgroundColor = new Color(0, 0, 0, 0); t.tcam.fieldOfView = 24; t.tcam.nearClipPlane = .05f; t.tcam.farClipPlane = 20;
            t.tcam.allowHDR = false; t.tcam.allowMSAA = true;
            var lg = new GameObject("luz_miniatura"); lg.transform.SetParent(g.transform, false); t.tlight = lg.AddComponent<Light>(); t.tlight.type = LightType.Point; t.tlight.cullingMask = 1 << Layer;
            t.tlight.range = 6; t.tlight.intensity = 5.5f; t.tlight.color = new Color(1, .94f, .84f); t.tlight.shadows = LightShadows.None; t.tlight.enabled = false;
            var lfg = new GameObject("luz_fill"); lfg.transform.SetParent(g.transform, false); t.tfill = lfg.AddComponent<Light>(); t.tfill.type = LightType.Point; t.tfill.cullingMask = 1 << Layer;
            t.tfill.range = 6; t.tfill.intensity = 3.5f; t.tfill.color = new Color(.82f, .90f, 1f); t.tfill.shadows = LightShadows.None; t.tfill.enabled = false;
            t.rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1, name = "rt_miniatura" };   // sem MSAA: ReadPixels de RT multiamostrado falha em alguns WebGL/celulares
            if (Camera.main) Camera.main.cullingMask &= ~(1 << Layer);
            return t;
        }
        void OnDestroy() { if (inst) Destroy(inst); if (rt) { rt.Release(); Destroy(rt); } }

        /// <summary>Pede a miniatura; o callback roda na hora se já estiver no cache.</summary>
        public void Request(string id, System.Action<Texture2D> cb)
        {
            if (mem.TryGetValue(id, out var t) && t) { cb?.Invoke(t); return; }
            if (waiting.TryGetValue(id, out var l)) { l.Add(cb); queue.RemoveAll(q => q.id == id); queue.Add((id, null)); return; }   // sobe para o topo
            if (TryDisk(id, out t)) { cb?.Invoke(t); return; }
            waiting[id] = new List<System.Action<Texture2D>> { cb }; queue.Add((id, null));
        }
        bool TryDisk(string id, out Texture2D t)
        {
            t = null; if (!diskOk) return false;
            try
            {
                var p = System.IO.Path.Combine(Dir, id + ".png"); if (!System.IO.File.Exists(p)) return false;
                t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "mini_" + id, wrapMode = TextureWrapMode.Clamp };
                if (!t.LoadImage(System.IO.File.ReadAllBytes(p), true)) { Destroy(t); t = null; return false; }
                mem[id] = t; return true;
            }
            catch { diskOk = false; return false; }
        }

        static readonly int __fbt_Update = FBTempo.Id("FBCustomizeThumbs.Update");   // [parceiro 01/11] medidor FBTempo
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            if (cur == null)
            {
                if (queue.Count == 0) return; var job = queue[^1]; queue.RemoveAt(queue.Count - 1); cur = job.id; frames = 0;
                var prefab = Resources.Load<GameObject>(FBRegistry.RChars + cur);
                if (!prefab) { Done(null); return; }
                inst = Instantiate(prefab, Spot, Quaternion.Euler(0, 192, 0)); inst.name = "Miniatura_" + cur;
                var ch = inst.GetComponent<FBCharacter>(); if (ch) { ch.relaxed = true; ch.isPlayer = false; ch.skinId = cur; }
                var an = inst.GetComponent<Animator>(); if (an) an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var w = inst.GetComponent<FBWardrobe>(); if (w) w.Apply(FBCustomizeData.LookFor(cur));
                foreach (var tr in inst.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = Layer;
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
                return;
            }
            if (++frames < 4) return;                                            // deixa o Animator assentar a pose parada
            Texture2D tex = null;
            try { tex = Render(); } catch (System.Exception e) { Debug.LogWarning("[FreeBala] miniatura " + cur + ": " + e.Message); }
            Done(tex);
        }
        Texture2D Render()
        {
            var an = inst.GetComponent<Animator>(); var head = an && an.isHuman ? an.GetBoneTransform(HumanBodyBones.Head) : null;
            float y0 = Spot.y, hy;
            if (head) hy = head.position.y; else { var rs = inst.GetComponentsInChildren<Renderer>(); var bb = rs.Length > 0 ? rs[0].bounds : new Bounds(Spot + Vector3.up, Vector3.one); foreach (var r in rs) bb.Encapsulate(r.bounds); hy = bb.max.y - (bb.size.y * .08f); }
            float scale = Mathf.Max(.3f, (hy - y0) / 1.55f);                     // personagens maiores/menores ficam do mesmo tamanho no cartão
            float lo = hy - .62f * scale, hi = hy + .24f * scale, half = (hi - lo) / 2;
            float d = half / Mathf.Tan(tcam.fieldOfView * .5f * Mathf.Deg2Rad);
            var c = new Vector3(Spot.x, (lo + hi) / 2, Spot.z); var rot = Quaternion.Euler(4, 0, 0);
            tcam.transform.SetPositionAndRotation(c - rot * Vector3.forward * d, rot); tcam.aspect = (float)W / H;
            tlight.transform.position = tcam.transform.position + new Vector3(-.5f, .6f, .3f); tlight.range = d + 4; tlight.enabled = true;
            tfill.transform.position = tcam.transform.position + new Vector3(.6f, .2f, .4f); tfill.range = d + 4; tfill.enabled = true;
            tcam.targetTexture = rt;
            var req = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(tcam, req)) RenderPipeline.SubmitRenderRequest(tcam, req); else tcam.Render();
            tlight.enabled = false; tfill.enabled = false;
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "mini_" + cur, wrapMode = TextureWrapMode.Clamp };
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); RenderTexture.active = prev;
            byte[] png = null; try { png = tex.EncodeToPNG(); } catch { }
            tex.Apply(false, true);                                              // libera a cópia da CPU
            if (png != null && diskOk) try { System.IO.Directory.CreateDirectory(Dir); System.IO.File.WriteAllBytes(System.IO.Path.Combine(Dir, cur + ".png"), png); } catch { diskOk = false; }
            return tex;
        }
        void Done(Texture2D tex)
        {
            if (inst) Destroy(inst); inst = null;
            if (tex) mem[cur] = tex;
            if (waiting.TryGetValue(cur, out var l)) { waiting.Remove(cur); foreach (var cb in l) try { cb?.Invoke(tex); } catch (System.Exception e) { Debug.LogException(e); } }
            cur = null; if (++rendered % 10 == 0) Resources.UnloadUnusedAssets();
        }
        /// <summary>Invalida a miniatura de um personagem (ex.: troca de roupa/pele no guarda-roupa).</summary>
        public static void Invalidate(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            mem.Remove(id);
            try { var p = System.IO.Path.Combine(Dir, id + ".png"); if (System.IO.File.Exists(p)) System.IO.File.Delete(p); } catch { }
        }
        /// <summary>Apaga o cache em disco (nova versão dos modelos).</summary>
        public static void ClearDisk() { try { var p = System.IO.Path.Combine(Application.persistentDataPath, "fb_thumbs"); if (System.IO.Directory.Exists(p)) System.IO.Directory.Delete(p, true); } catch { } mem.Clear(); }
    }
}
