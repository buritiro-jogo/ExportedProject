// Ícones gerados a partir dos modelos 3D reais (não reutiliza as silhuetas antigas): arma de perfil com luz de estúdio e fundo
// transparente, e o personagem do jogador congelado no quadro mais expressivo de cada emote. Renderizados uma vez por câmera
// ortográfica/perspectiva numa camada isolada e guardados em cache (memória). Usados na loja do 4V4, na roda de emotes e no HUD.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FreeBala
{
    public static class FBIcons
    {
        const int LAYER = 30;                                  // camada das miniaturas (a mesma da tela de personagem)
        static readonly Dictionary<string, Texture2D> cache = new();
        static Camera cam; static Light key, fill;

        static void Rig()
        {
            if (cam) return;
            var g = new GameObject("FB_IconStudio"); Object.DontDestroyOnLoad(g); g.transform.position = new Vector3(0, -8000, 0);
            cam = new GameObject("cam").AddComponent<Camera>(); cam.transform.SetParent(g.transform, false); cam.enabled = false; cam.cullingMask = 1 << LAYER;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0); cam.nearClipPlane = .01f; cam.farClipPlane = 50; cam.allowHDR = false; cam.allowMSAA = true;
            var ucd = cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); ucd.renderPostProcessing = false; ucd.renderShadows = false;
            key = new GameObject("key").AddComponent<Light>(); key.transform.SetParent(g.transform, false); key.type = LightType.Directional; key.intensity = 1.35f; key.color = new Color(1, .96f, .9f); key.cullingMask = 1 << LAYER; key.transform.rotation = Quaternion.Euler(35, -40, 0);
            // luzes do estúdio só acesas durante a foto: o URP ignora a máscara de camada da luz direcional principal e a 'key' (1,35)
            // virava o SOL da partida — a noite ficava clara como dia
            fill = new GameObject("fill").AddComponent<Light>(); fill.transform.SetParent(g.transform, false); fill.type = LightType.Directional; fill.intensity = .55f; fill.color = new Color(.75f, .85f, 1); fill.cullingMask = 1 << LAYER; fill.transform.rotation = Quaternion.Euler(-20, 140, 0); key.enabled = fill.enabled = false;
        }

        static Texture2D Shoot(int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w * 2, h * 2, 24, RenderTextureFormat.ARGB32); rt.antiAliasing = 4;
            key.enabled = fill.enabled = true; cam.targetTexture = rt; cam.aspect = w / (float)h; cam.Render(); cam.targetTexture = null; key.enabled = fill.enabled = false;
            var small = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32); Graphics.Blit(rt, small);   // reduz 2× (bordas suaves)
            var prev = RenderTexture.active; RenderTexture.active = small; var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; t.ReadPixels(new Rect(0, 0, w, h), 0, 0); t.Apply(false, false);
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); RenderTexture.ReleaseTemporary(small); return t;
        }
        static void Layer(GameObject g) { foreach (var t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LAYER; foreach (var r in g.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; } }
        static Bounds B(GameObject g) { var rs = g.GetComponentsInChildren<Renderer>(); var b = new Bounds(g.transform.position, Vector3.zero); bool f = true; foreach (var r in rs) { if (!r.enabled || !r.gameObject.activeInHierarchy) continue; if (f) { b = r.bounds; f = false; } else b.Encapsulate(r.bounds); } return b; }

        // arma de perfil (cano para a direita), enquadrada pelo comprimento
        public static Texture2D Weapon(string id, int w = 256, int h = 112)
        {
            if (string.IsNullOrEmpty(id)) return null; string k = "w|" + id + "|" + w; if (cache.TryGetValue(k, out var c) && c) return c;
            if (!GameData.Weapons.TryGetValue(id, out var def)) return null; Rig();
            var go = FBWeaponModel.Make(def, cam.transform.parent); if (!go) return null;
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(col); Layer(go);
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            // eixo do cano: SOCKET_Muzzle a partir do centro; gira para que o cano aponte para +X da câmera
            var b0 = B(go); Transform mz = null; foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("SOCKET_Muzzle")) { mz = t; break; }
            var dir = mz ? Vector3.ProjectOnPlane(mz.position - b0.center, Vector3.up).normalized : Vector3.forward; if (dir.sqrMagnitude < .5f) dir = Vector3.forward;
            go.transform.rotation = Quaternion.FromToRotation(dir, Vector3.right) * go.transform.rotation;
            var b = B(go); float aspect = w / (float)h;
            cam.orthographic = true; cam.orthographicSize = Mathf.Max(b.extents.y, b.extents.x / aspect) * 1.08f;
            cam.transform.position = b.center + new Vector3(0, 0, -5); cam.transform.rotation = Quaternion.identity;
            key.transform.rotation = Quaternion.Euler(30, -25, 0);
            var tex = Shoot(w, h); Object.DestroyImmediate(go); tex.name = "icone_" + id; Mono(tex); return cache[k] = tex;
        }

        // emote: personagem do jogador congelado no quadro de maior movimento do clipe (≈40 % do tempo), corpo inteiro de frente-três-quartos
        public static Texture2D Emote(string clipId, string charId, int size = 160)
        {
            if (string.IsNullOrEmpty(clipId)) return null; string k = "e|" + clipId + "|" + charId + "|" + size; if (cache.TryGetValue(k, out var c) && c) return c;
            var clip = FBRegistry.I ? FBRegistry.I.Clip(clipId) : null; var prefab = FBRegistry.I ? FBRegistry.I.Char(charId) : null; if (!clip || !prefab) return null; Rig();
            var go = Object.Instantiate(prefab, cam.transform.parent); go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.Euler(0, 200, 0);
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false; foreach (var col in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(col); Layer(go);
            var an = go.GetComponentInChildren<Animator>();
            if (an)
            {
                var graph = PlayableGraph.Create("icone"); var outp = AnimationPlayableOutput.Create(graph, "o", an); var cp = AnimationClipPlayable.Create(graph, clip); outp.SetSourcePlayable(cp);
                cp.SetTime(clip.length * .4f); graph.Evaluate(); graph.Destroy();
            }
            var b = B(go); cam.orthographic = false; cam.fieldOfView = 26;
            float dist = b.extents.magnitude / Mathf.Tan(13 * Mathf.Deg2Rad) * 1.02f;
            cam.transform.position = b.center + new Vector3(0, b.extents.y * .05f, -dist); cam.transform.LookAt(b.center);
            key.transform.rotation = Quaternion.Euler(30, -35, 0);
            var tex = Shoot(size, size); Object.DestroyImmediate(go); tex.name = "emote_" + clipId; return cache[k] = tex;
        }

        // silhueta branca (HUD preto e branco, estilo FF): mantém o alfa, um leve sombreado pela luminância dá volume
        static void Mono(Texture2D t)
        {
            var px = t.GetPixels32();
            for (int i = 0; i < px.Length; i++) { var c = px[i]; if (c.a < 8) { px[i] = new Color32(255, 255, 255, 0); continue; } byte v = (byte)Mathf.Lerp(205, 255, (c.r * .3f + c.g * .59f + c.b * .11f) / 255f); px[i] = new Color32(v, v, v, (byte)Mathf.Min(255, c.a * 1.2f)); }
            t.SetPixels32(px); t.Apply(false, false);
        }
        public static void Clear() { foreach (var t in cache.Values) if (t) Object.Destroy(t); cache.Clear(); }
    }
}
