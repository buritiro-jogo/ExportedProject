// Guarda-roupa modular (porte do wardrobe.js): mesma ficha (<id>.sheet.json) e MESMO código de visual que o jogo web manda pela rede
// ("ie_bandido~<código>"), então web e Unity mostram o mesmo personagem. Todas as peças estão presas ao MESMO esqueleto (skin weights do
// próprio modelo): trocar visual = ligar/desligar malhas + tingir materiais, sem recarregar o prefab nem mexer no rig/Animator.
// Esconde a pele coberta pela roupa ("covers"), nunca some com a pele toda, tom de pele e cor das roupas por instâncias de material
// (mesmo shader, nenhuma compilação nova). As instâncias são criadas UMA vez por renderer e guardam a cor original: reaplicar não acumula.
// Personagens sem ficha (NPCs CC_Base) ainda trocam tom de pele, cor do cabelo e cor da roupa pelos NOMES dos materiais
// (Std_Skin_*, *hair*/scalp/beard, o resto é roupa); os originais de atlas único (materiais sem nome) não têm o que trocar.
// código: <bits das peças em hex, 4 por dígito>.<tom>[.<cat><tinta>,...]  — "hair<k>" e "roupa<k>" são extensões que o web ignora.
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public class FBWardrobe : MonoBehaviour
    {
        [TextArea] public string sheetJson;
        public static readonly Color[] TONES = { new(1.1f, 1.04f, 1f), new(.96f, .84f, .74f), new(.8f, .62f, .5f), new(.66f, .48f, .37f), new(.5f, .35f, .27f), new(.36f, .25f, .2f) };
        public static readonly string[] TONE_NAMES = { "Clara", "Média", "Morena", "Parda", "Escura", "Retinta" };
        public static readonly Color?[] TINTS = { null, new Color(.08f, .08f, .09f), new Color(.92f, .92f, .9f), new Color(.62f, .1f, .1f), new Color(.12f, .25f, .55f), new Color(.18f, .32f, .16f), new Color(.55f, .45f, .3f), new Color(.45f, .45f, .47f) };
        public static readonly string[] TINT_NAMES = { "Original", "Preto", "Branco", "Vermelho", "Azul", "Verde", "Caqui", "Cinza" };
        // cabelo: até 8 cores (o índice cabe no mesmo formato "<cat><dígito>" do web)
        public static readonly Color?[] HAIR = { null, new Color(.12f, .1f, .09f), new Color(.32f, .2f, .13f), new Color(.55f, .36f, .2f), new Color(1.25f, 1.05f, .7f), new Color(.85f, .3f, .12f), new Color(.75f, .74f, .72f), new Color(1.5f, 1.45f, 1.35f) };
        public static readonly string[] HAIR_NAMES = { "Original", "Preto", "Castanho escuro", "Castanho", "Loiro", "Ruivo", "Grisalho", "Platinado" };
        public static readonly string[] EXCL = { "top", "bottom", "shoes", "head" };                 // uma peça por vez (não empilha camisas)
        public static readonly string[] TINTABLE = { "top", "vest", "bottom", "hat", "shoes" };      // categorias que aceitam cor (igual ao web)

        public class Part { public string node, cat, name; public bool on; public List<string> covers = new(); [System.NonSerialized] public Transform tr; }
        public class Look
        {
            public HashSet<int> on = new(); public int tone = -1; public Dictionary<string, int> tint = new();
            public Look Clone() => new() { on = new HashSet<int>(on), tone = tone, tint = new Dictionary<string, int>(tint) };
            public int Tint(string cat) => tint.TryGetValue(cat, out var k) ? k : 0;
        }
        enum Slot { None, Skin, Hair, Cloth }

        List<Part> parts; List<(string key, string name)> cats; public string current; public Look look;
        public List<Part> Parts { get { Parse(); return parts ?? new List<Part>(); } }
        public List<(string key, string name)> Cats { get { Parse(); return cats ?? new List<(string, string)>(); } }
        public bool HasSheet { get { Parse(); return parts != null && parts.Count > 0; } }
        public bool HasSkinMats { get { ScanMats(); return nSkin > 0; } }
        public bool HasHairMats { get { ScanMats(); return nHair > 0; } }
        public bool HasClothMats { get { ScanMats(); return nCloth > 0 && nSkin > 0; } }   // sem pele nomeada = atlas único: tingir pintaria a pele junto
        /// <summary>Tem algo para personalizar (peças, pele, cabelo ou roupa)?</summary>
        public bool Customizable => HasSheet || HasSkinMats || HasHairMats || HasSkinMask;
        /// <summary>Atlas único com máscara de pele gerada (Art/PersonagemTools~/make_skin_masks.py): só o tom de pele, aproximado.</summary>
        public bool HasSkinMask { get { ScanMats(); return !HasSheet && nSkin == 0 && MaskInfo(SkinId) != null; } }
        /// <summary>Id do personagem (nome do prefab); quem renomeia a instância ("Previa") define antes de aplicar.</summary>
        public string skinId;
        string SkinId { get { if (!string.IsNullOrEmpty(skinId)) return skinId; var c = GetComponent<FBCharacter>(); return c && !string.IsNullOrEmpty(c.skinId) ? c.skinId : name.Replace("(Clone)", "").Trim(); } }
        void Awake() { if (string.IsNullOrEmpty(skinId) && name.EndsWith("(Clone)")) skinId = name.Replace("(Clone)", "").Trim(); }

        void Parse()
        {
            if (parts != null || string.IsNullOrEmpty(sheetJson)) return; parts = new List<Part>(); cats = new List<(string, string)>();
            var root = Json.Parse(sheetJson);
            foreach (var o in Json.L(root, "parts") ?? new List<object>())
            {
                var p = new Part { node = Json.S(o, "node"), cat = Json.S(o, "cat"), name = Json.S(o, "name"), on = Json.B(o, "on") };
                var cv = Json.L(o, "covers"); if (cv != null) foreach (var c in cv) p.covers.Add(c as string);
                parts.Add(p);
            }
            foreach (var c in Json.L(root, "cats") ?? new List<object>()) if (c is List<object> kv && kv.Count > 1) cats.Add((kv[0] as string, kv[1] as string));
            var byName = new Dictionary<string, Transform>(); foreach (var t in GetComponentsInChildren<Transform>(true)) byName[t.name] = t;
            foreach (var p in parts) if (p.node != null && byName.TryGetValue(p.node, out var tr)) p.tr = tr;
        }

        // ---- código <-> visual (igual ao encodeLook/decodeLook do wardrobe.js)
        public Look Default() { var L = new Look(); var P = Parts; for (int i = 0; i < P.Count; i++) if (P[i].on && P[i].cat != "body") L.on.Add(i); return L; }
        public Look Decode(string code)
        {
            if (string.IsNullOrEmpty(code)) return Default();
            var P = Parts; var L = new Look(); var seg = code.Split('.'); var hex = seg[0];
            for (int k = 0; k < hex.Length; k++)
            {
                int v; try { v = System.Convert.ToInt32(hex[k].ToString(), 16); } catch { v = 0; }
                for (int j = 0; j < 4; j++) if ((v & (1 << j)) != 0) { int i = k * 4 + j; if (i < P.Count && P[i].cat != "body") L.on.Add(i); }
            }
            if (seg.Length > 1 && int.TryParse(seg[1], out var tone)) L.tone = Mathf.Clamp(tone, -1, TONES.Length - 1);
            if (seg.Length > 2) foreach (var e in seg[2].Split(',')) if (e.Length > 1 && int.TryParse(e.Substring(e.Length - 1), out var t) && t >= 0 && t < 8) L.tint[e.Substring(0, e.Length - 1)] = t;
            return L;
        }
        public string Encode(Look L)
        {
            var P = Parts; var sb = new System.Text.StringBuilder();
            for (int i = 0; i < P.Count; i += 4) { int v = 0; for (int j = 0; j < 4; j++) if (L.on.Contains(i + j)) v |= 1 << j; sb.Append(v.ToString("x")); }
            sb.Append('.').Append(L.tone);
            var t = new List<string>(); foreach (var kv in L.tint) if (kv.Value > 0) t.Add(kv.Key + kv.Value); t.Sort();
            if (t.Count > 0) sb.Append('.').Append(string.Join(",", t));
            return sb.ToString();
        }
        /// <summary>Código do visual original (sem trocas): string vazia, como o web.</summary>
        public bool IsDefault(Look L) { var d = Default(); return L.tone < 0 && L.on.SetEquals(d.on) && !HasTint(L); }
        static bool HasTint(Look L) { foreach (var kv in L.tint) if (kv.Value > 0) return true; return false; }

        public Look RandomLook(System.Func<float> rnd = null)
        {
            rnd ??= () => Random.value; var L = Default(); var P = Parts; var by = new Dictionary<string, List<int>>();
            for (int i = 0; i < P.Count; i++) { if (P[i].cat == "body") continue; if (!by.ContainsKey(P[i].cat)) by[P[i].cat] = new List<int>(); by[P[i].cat].Add(i); }
            foreach (var kv in by)
            {
                var alts = kv.Value.FindAll(i => !P[i].on); if (alts.Count == 0 || rnd() < .5f) continue;   // metade mantém o original
                if (System.Array.IndexOf(EXCL, kv.Key) >= 0) foreach (var i in kv.Value) L.on.Remove(i);
                L.on.Add(alts[Mathf.Min(alts.Count - 1, (int)(rnd() * alts.Count))]);
            }
            if (rnd() < .6f && (HasSheet || HasSkinMats)) L.tone = Mathf.Min(TONES.Length - 1, (int)(rnd() * TONES.Length));
            if (HasHairMats && rnd() < .5f) L.tint["hair"] = 1 + Mathf.Min(HAIR.Length - 2, (int)(rnd() * (HAIR.Length - 1)));
            return L;
        }

        /// <summary>Liga/desliga uma peça respeitando as categorias exclusivas (camisa troca a camisa).</summary>
        public void Toggle(Look L, int i)
        {
            var P = Parts; if (i < 0 || i >= P.Count || P[i].cat == "body") return;
            if (L.on.Contains(i)) { L.on.Remove(i); return; }
            if (System.Array.IndexOf(EXCL, P[i].cat) >= 0) for (int j = 0; j < P.Count; j++) if (P[j].cat == P[i].cat) L.on.Remove(j);
            L.on.Add(i);
        }
        public void ClearCat(Look L, string cat) { var P = Parts; for (int j = 0; j < P.Count; j++) if (P[j].cat == cat) L.on.Remove(j); }

        // ---- aplicar
        public void Apply(string code, bool random = false)
        {
            var L = random ? RandomLook() : Decode(code);
            ApplyLook(L);
        }
        public void ApplyLook(Look L)
        {
            look = L; current = Encode(L); var P = Parts;
            // peças: visibilidade + pele coberta pela roupa
            var vis = new bool[P.Count];
            for (int i = 0; i < P.Count; i++) vis[i] = P[i].cat == "body" || L.on.Contains(i);
            for (int i = 0; i < P.Count; i++) if (P[i].cat != "body" && vis[i]) foreach (var n in P[i].covers) { int j = P.FindIndex(q => q.node == n); if (j >= 0) vis[j] = false; }
            bool anySkin = false; bool hasHead = false; for (int i = 0; i < P.Count; i++) { if (vis[i] && P[i].cat == "body") anySkin = true; if (P[i].cat == "head") hasHead = true; }
            if (!anySkin && !hasHead) { int j = P.FindIndex(q => q.cat == "body"); if (j >= 0) vis[j] = true; }   // nunca some a pele toda
            Color? toneC = L.tone >= 0 && L.tone < TONES.Length ? TONES[L.tone] : null;
            for (int i = 0; i < P.Count; i++)
            {
                var tr = P[i].tr; if (!tr) continue; if (tr.gameObject.activeSelf != vis[i]) tr.gameObject.SetActive(vis[i]);
                Color? col = P[i].cat == "body" || P[i].cat == "head" ? toneC : (L.Tint(P[i].cat) is int k && k > 0 && k < TINTS.Length ? TINTS[k] : null);
                foreach (var r in tr.GetComponentsInChildren<Renderer>(true)) Paint(r, -1, col);
            }
            // materiais nomeados (NPCs sem ficha; cabelo também nos com ficha)
            ScanMats();
            int hk = L.Tint("hair"), rk = L.Tint("roupa");
            foreach (var s in slots)
            {
                if (s.slot == Slot.Hair) Paint(s.r, s.idx, hk > 0 && hk < HAIR.Length ? HAIR[hk] : null);
                else if (!HasSheet && s.slot == Slot.Skin) Paint(s.r, s.idx, toneC);
                else if (!HasSheet && s.slot == Slot.Cloth && nSkin > 0) Paint(s.r, s.idx, rk > 0 && rk < TINTS.Length ? TINTS[rk] : null);
            }
            if (HasSkinMask) MaskTone(L.tone);
        }

        // cor original de cada instância de material (criadas uma vez por renderer, só quando alguma cor é pedida)
        readonly Dictionary<Renderer, Material[]> inst = new(); readonly Dictionary<Material, Color> baseCol = new();
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), GltfColor = Shader.PropertyToID("baseColorFactor"), LegacyColor = Shader.PropertyToID("_Color");
        static int ColorProp(Material m) => m.HasProperty(BaseColor) ? BaseColor : m.HasProperty(GltfColor) ? GltfColor : m.HasProperty(LegacyColor) ? LegacyColor : -1;
        void Paint(Renderer r, int idx, Color? col)
        {
            if (!r) return;
            if (!inst.TryGetValue(r, out var mats))
            {
                if (col == null) return;                                         // nunca tingido: segue com o material compartilhado
                mats = r.materials; inst[r] = mats;
                foreach (var m in mats) { int p = m ? ColorProp(m) : -1; if (p >= 0) baseCol[m] = m.GetColor(p); }
            }
            for (int i = 0; i < mats.Length; i++)
            {
                if (idx >= 0 && i != idx) continue; var m = mats[i]; if (!m || !baseCol.TryGetValue(m, out var b)) continue;
                var c = col.HasValue ? new Color(b.r * col.Value.r, b.g * col.Value.g, b.b * col.Value.b, b.a) : b;
                m.SetColor(ColorProp(m), c);
            }
        }
        void OnDestroy() { foreach (var kv in inst) foreach (var m in kv.Value) if (m) Destroy(m); }

        // ---- tom de pele por MÁSCARA (personagens de atlas único c_*/p_*): a textura do corpo é refeita UMA vez por (textura, tom) e
        // trocada só nesta instância — mesmo material/shader (nenhuma variante nova), sem "Read/Write" no importador (cópia pela GPU).
        // A máscara (256², borda suave) marca a pele aprendida do próprio personagem; cada pixel de pele vai para a cor-alvo do tom
        // mantendo o sombreado: curva de potência por canal (média da pele → alvo; 0 e 1 fixos, sem estourar os realces), via tabela de 256.
        // Aproximado: pode pegar detalhes cor-de-pele da roupa.
        public static readonly Color[] TONE_SKIN = { new(.86f, .7f, .6f), new(.84f, .66f, .53f), new(.72f, .53f, .4f), new(.6f, .42f, .31f), new(.43f, .29f, .21f), new(.3f, .2f, .15f) };
        class MaskDef { public string id, tex; public Color skin; public Texture2D mask; }
        static Dictionary<string, MaskDef> maskIdx; static readonly Dictionary<(Texture, int), Texture2D> toned = new();
        static MaskDef MaskInfo(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (maskIdx == null)
            {
                maskIdx = new Dictionary<string, MaskDef>(); var ta = Resources.Load<TextAsset>("UI/Personagem/Mascaras/index");
                if (ta && Json.Parse(ta.text) is Dictionary<string, object> d)
                    foreach (var kv in d) { var sk = Json.L(kv.Value, "skin"); if (sk == null || sk.Count < 3) continue; maskIdx[kv.Key] = new MaskDef { id = kv.Key, tex = Json.S(kv.Value, "tex"), skin = new Color(Json.Fv(sk[0]), Json.Fv(sk[1]), Json.Fv(sk[2])) }; }
            }
            return maskIdx.TryGetValue(id, out var m) ? m : null;
        }
        Renderer maskR; int maskMat = -1; Texture maskOrig;
        static readonly int[] TexProps = { Shader.PropertyToID("_BaseMap"), Shader.PropertyToID("baseColorTexture"), Shader.PropertyToID("_MainTex") };
        void MaskTone(int tone)
        {
            var md = MaskInfo(SkinId); if (md == null) return;
            if (!maskR)
            {
                foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var sm = r.sharedMaterials; for (int i = 0; i < sm.Length; i++) { var t = MainTex(sm[i]); if (t && t.name == md.tex) { maskR = r; maskMat = i; maskOrig = t; break; } }
                    if (maskR) break;
                }
                if (!maskR) return;
            }
            if (tone < 0 && !inst.ContainsKey(maskR)) return;                   // nunca mexido: fica no material compartilhado
            if (!inst.TryGetValue(maskR, out var mats)) { mats = maskR.materials; inst[maskR] = mats; foreach (var m in mats) { int p = m ? ColorProp(m) : -1; if (p >= 0) baseCol[m] = m.GetColor(p); } }
            var mat = mats[maskMat]; Texture tex = maskOrig;
            if (tone >= 0 && tone < TONE_SKIN.Length) { var t2 = Toned(maskOrig, md, tone); if (t2) tex = t2; }
            foreach (var p in TexProps) if (mat.HasProperty(p)) mat.SetTexture(p, tex);
        }
        static Texture MainTex(Material m) { if (!m) return null; foreach (var p in TexProps) if (m.HasProperty(p) && m.GetTexture(p)) return m.GetTexture(p); return null; }
        /// <summary>Textura do corpo com a pele no tom pedido (cache por textura+tom; ~30 ms na 1ª vez para 1024²).</summary>
        static Texture2D Toned(Texture src, MaskDef md, int tone)
        {
            var key = (src, tone); if (toned.TryGetValue(key, out var done) && done) return done;
            if (!md.mask)
            {
                var ta = Resources.Load<TextAsset>("UI/Personagem/Mascaras/" + md.id); if (!ta) return null;
                md.mask = new Texture2D(2, 2, TextureFormat.RGBA32, false); if (!md.mask.LoadImage(ta.bytes, false)) return null;
            }
            int w = Mathf.Min(1024, src.width), h = Mathf.Min(1024, src.height);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); var prev = RenderTexture.active;
            Graphics.Blit(src, rt); RenderTexture.active = rt;
            var tmp = new Texture2D(w, h, TextureFormat.RGBA32, false); tmp.ReadPixels(new Rect(0, 0, w, h), 0, 0); RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            var px = tmp.GetPixels32(); Destroy(tmp);
            var mk = md.mask; int mw = mk.width, mh = mk.height; var mp = mk.GetPixels32();
            var tgt = TONE_SKIN[tone];
            byte[] Lut(float t, float sk) { float g = Mathf.Clamp(Mathf.Log(t) / Mathf.Log(Mathf.Clamp(sk, .03f, .97f)), .3f, 3.5f); var l = new byte[256]; for (int i = 0; i < 256; i++) l[i] = (byte)Mathf.RoundToInt(255 * Mathf.Pow(i / 255f, g)); return l; }
            var lr = Lut(tgt.r, md.skin.r); var lg = Lut(tgt.g, md.skin.g); var lb = Lut(tgt.b, md.skin.b);
            // máscara e textura do Unity têm a linha 0 embaixo (LoadImage e o import do glTF já desviram), então os UVs coincidem
            for (int y = 0; y < h; y++)
            {
                float fy = (y + .5f) / h * mh - .5f; int y0 = Mathf.Clamp((int)fy, 0, mh - 1), y1 = Mathf.Min(mh - 1, y0 + 1); float ty = Mathf.Clamp01(fy - y0);
                int r0 = y0 * mw, r1 = y1 * mw;
                for (int x = 0; x < w; x++)
                {
                    float fx = (x + .5f) / w * mw - .5f; int x0 = Mathf.Clamp((int)fx, 0, mw - 1), x1 = Mathf.Min(mw - 1, x0 + 1); float tx = Mathf.Clamp01(fx - x0);
                    float m = Mathf.Lerp(Mathf.Lerp(mp[r0 + x0].r, mp[r0 + x1].r, tx), Mathf.Lerp(mp[r1 + x0].r, mp[r1 + x1].r, tx), ty) / 255f;
                    if (m < .01f) continue; ref var c = ref px[y * w + x];
                    c.r = (byte)(c.r + (lr[c.r] - c.r) * m); c.g = (byte)(c.g + (lg[c.g] - c.g) * m); c.b = (byte)(c.b + (lb[c.b] - c.b) * m);
                }
            }
            var o = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = src.name + "_tom" + tone, wrapMode = src.wrapMode, filterMode = FilterMode.Bilinear, anisoLevel = 2 };
            o.SetPixels32(px); o.Apply(true, false);
            if (!Application.isMobilePlatform && SystemInfo.SupportsTextureFormat(TextureFormat.DXT5)) o.Compress(false);   // 4 MB → 1 MB no PC
            o.Apply(false, true); toned[key] = o; return o;
        }

        // ---- materiais por nome (sem ficha)
        struct MatSlot { public Renderer r; public int idx; public Slot slot; }
        List<MatSlot> slots; int nSkin, nHair, nCloth;
        static Slot Classify(string n)
        {
            n = (n ?? "").ToLowerInvariant().Replace(" (instance)", "");
            if (n.Length == 0) return Slot.None;
            if (n.Contains("eye") || n.Contains("teeth") || n.Contains("tongue") || n.Contains("cornea") || n.Contains("occlusion") || n.Contains("tearline")) return Slot.None;
            if (n.Contains("hair") || n.Contains("scalp") || n.Contains("beard") || n.Contains("bushy") || n.Contains("brow") || n.Contains("mustache") || n.Contains("moustache")) return Slot.Hair;
            if (n.Contains("skin") || n.Contains("nails")) return Slot.Skin;
            return Slot.Cloth;
        }
        void ScanMats()
        {
            if (slots != null) return; slots = new List<MatSlot>(); Parse();
            var sheetR = new HashSet<Renderer>(); if (parts != null) foreach (var p in parts) if (p.tr) foreach (var r in p.tr.GetComponentsInChildren<Renderer>(true)) sheetR.Add(r);
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is not SkinnedMeshRenderer) continue;                       // só a malha do corpo (arma na mão é MeshRenderer)
                var sm = r.sharedMaterials;
                for (int i = 0; i < sm.Length; i++)
                {
                    var s = Classify(sm[i] ? sm[i].name : null); if (s == Slot.None) continue;
                    if (sheetR.Contains(r) && s != Slot.Hair) continue;          // peças da ficha seguem a regra da ficha
                    slots.Add(new MatSlot { r = r, idx = i, slot = s });
                    if (s == Slot.Skin) nSkin++; else if (s == Slot.Hair) nHair++; else nCloth++;
                }
            }
        }

        // ---- atalhos para quem só tem o GameObject (partida, rede, menu)
        /// <summary>Guarda-roupa do personagem; cria um sem ficha (só materiais) se o prefab não tiver.</summary>
        public static FBWardrobe For(GameObject go, string id = null) { if (!go) return null; var w = go.GetComponent<FBWardrobe>(); if (!w) w = go.AddComponent<FBWardrobe>(); if (id != null) w.skinId = id; return w; }
        public static void ApplyTo(GameObject go, string code, bool random = false)
        {
            if (!go) return; var w = go.GetComponent<FBWardrobe>();
            if (!w) { if (!random && string.IsNullOrEmpty(code)) return; w = go.AddComponent<FBWardrobe>(); if (!w.Customizable) return; }
            w.Apply(code, random); go.GetComponent<FBCharacter>()?.HolsterRemeasure();   // roupa nova: o coldre mede o corpo de novo
        }
    }
}
