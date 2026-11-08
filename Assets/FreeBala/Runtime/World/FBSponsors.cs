using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
    // r62: logos dos PATROCINADORES pintadas como pichação nas laterais dos contêineres de todos os mapas.
    // - As logos ficam em Resources/Sponsors/*.png (feitas por unity/tools/patrocinadores/spray_logo.py a partir da logo original);
    //   para pôr mais um patrocinador basta gerar o PNG lá: elas se revezam pelos contêineres, na ordem do nome do arquivo.
    // - Contêiner = qualquer objeto do mapa com "conteiner/contêiner/container" no nome (cobertura PA_/VJ_Cob_, lanche do PT…).
    //   A caixa é medida pelas malhas (LOD0) no espaço do próprio objeto; só entra se tiver medida de contêiner de verdade.
    // - r62c: cada logo aparece UMA vez por mapa, num contêiner e numa lateral LIVRE (nada encostado a menos de 0,8 m); as malhas ficam como filhas do contêiner,
    //   então somem junto quando o De Rolê desliga os contêineres de cobertura.
    // - Material = mesmo Lit com recorte de alfa dos grafites (variante já compilada no build; não causa travada de shader).
    public static class FBSponsors
    {
        static readonly Regex RX = new(@"cont[eê]i?ner|container", RegexOptions.IgnoreCase);
        static readonly Regex LODN = new(@"_LOD[1-9]\b", RegexOptions.IgnoreCase);
        static readonly List<GameObject> made = new();
        static Material[] mats; static Texture2D[] texs;
        public static string LastReport = "";

        static bool Ensure()
        {
            if (mats != null && mats.All(m => m)) return mats.Length > 0;
            texs = Resources.LoadAll<Texture2D>("Sponsors").OrderBy(t => t.name).ToArray();
            mats = texs.Select(t =>
            {
                var m = new Material(FBShaders.Lit) { name = "patrocinio_" + t.name }; m.SetTexture("_BaseMap", t); m.mainTexture = t;
                m.SetColor("_BaseColor", new Color(.93f, .92f, .9f)); m.SetFloat("_Smoothness", .12f); m.SetFloat("_Metallic", 0);
                m = FBShaders.Transparent(m, false, true); m.SetFloat("_Cutoff", .4f); m.renderQueue = 2996; return m;
            }).ToArray();
            return mats.Length > 0;
        }

        static uint H(Vector3 p) { unchecked { uint h = 2166136261; foreach (var v in new[] { p.x, p.y, p.z }) h = (h ^ (uint)Mathf.RoundToInt(v * 10)) * 16777619; h ^= h >> 15; h *= 2246822507; h ^= h >> 13; return h; } }
        static float R01(ref uint h) { unchecked { h ^= h << 13; h ^= h >> 17; h ^= h << 5; } return (h & 0xFFFFFF) / 16777216f; }

        public static int Place(string map)
        {
            Clear(); if (!Ensure()) { LastReport = "sem logos em Resources/Sponsors"; return 0; }
            // 1) contêineres: o ancestral mais alto com nome de contêiner e tamanho de contêiner
            var roots = new HashSet<Transform>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || LODN.IsMatch(r.name)) continue;
                Transform best = null; for (var t = r.transform; t; t = t.parent) if (RX.IsMatch(t.name)) best = t;
                if (!best) continue;
                // sobe só até onde ainda é UM contêiner (pasta "Conteineres" com vários não conta)
                for (var t = r.transform; t; t = t.parent) { if (!RX.IsMatch(t.name)) continue; if (WorldSize(t) <= 14f) best = t; else break; }
                roots.Add(best);
            }
            // r62c: cada logo UMA vez no mapa, num contêiner só e numa lateral só. Primeiro junta todas as laterais livres,
            // depois escolhe: 1ª logo no contêiner livre mais perto do centro da área de jogo; as seguintes no que fica mais
            // longe das já usadas (sem ir para a borda), um contêiner por logo.
            int skipSize = 0, skipBlocked = 0, skipCounter = 0;
            var cand = new List<(Transform root, Vector3 c, Vector3 up, Vector3 nrm, float H0, float L, float D, float V)>();
            foreach (var root in roots.OrderBy(t => t.position.x).ThenBy(t => t.position.z))
            {
                if (!Measure(root, out var c, out var up, out var len, out var dep, out float H0, out float L, out float D, out int skipS)) { skipSize++; continue; }
                foreach (int s in new[] { 1, -1 })
                {
                    if (s == skipS) { skipCounter++; continue; }   // lado do balcão/toldo (janela de atendimento): sem logo
                    var nrm = dep * s; var face = c + nrm * (D * .5f);
                    if (Blocked(root, face, nrm, up, H0, L)) { skipBlocked++; continue; }
                    cand.Add((root, c, up, nrm, H0, L, D, Vista(root, face, nrm, up, H0, L)));
                }
            }
            var pb = FBWorld.GetPlayableBounds(map); var ctr0 = pb.center; ctr0.y = 0;
            bool In(Vector3 p) => p.x >= pb.min.x && p.x <= pb.max.x && p.z >= pb.min.z && p.z <= pb.max.z;
            var pool = cand.Where(k => In(k.c)).ToList(); if (pool.Count == 0) pool = cand;
            // r63: só laterais que se enxergam de frente (nada a menos de ~5 m tampando a logo); se faltar, aceita as outras
            var vis = pool.Where(k => k.V >= 5f).ToList(); if (vis.Count >= mats.Length) pool = vis;
            float Dc(Vector3 p) { p.y = 0; return (p - ctr0).magnitude; }
            var pick = new List<int>(); var used = new HashSet<Transform>();
            for (int li = 0; li < mats.Length && pick.Count < mats.Length; li++)
            {
                int best = -1; float bs = float.MinValue;
                for (int k = 0; k < pool.Count; k++)
                {
                    if (used.Contains(pool[k].root)) continue;
                    float sc;
                    if (pick.Count == 0) sc = -Dc(pool[k].c);
                    else { float md = pick.Min(j => Vector3.Distance(pool[j].c, pool[k].c)); sc = Mathf.Min(md, 90f) - .35f * Dc(pool[k].c); }
                    // a lateral que olha para o centro (onde se joga) vale um pouco mais
                    var toC = (ctr0 - pool[k].c); toC.y = 0; sc += 4f * Vector3.Dot(pool[k].nrm, toC.normalized) + 1.5f * pool[k].V;
                    if (sc > bs) { bs = sc; best = k; }
                }
                if (best < 0) break; pick.Add(best); used.Add(pool[best].root);
            }
            int n = 0;
            for (int li = 0; li < pick.Count; li++)
            {
                var (root, c, up, nrm, H0, L, D, V) = pool[pick[li]];
                uint h = H(c); var face = c + nrm * (D * .5f);
                var tex = texs[li]; float asp = tex.height / (float)tex.width;   // altura/largura
                float w = Mathf.Min(L * .62f, H0 * .8f / asp, 3.4f), hh = w * asp;
                float slide = (R01(ref h) - .5f) * Mathf.Max(0, L - w - .6f) * .7f, lift = (R01(ref h) - .5f) * .12f * H0;
                float ang = (R01(ref h) - .5f) * 8f * Mathf.Deg2Rad, ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                var t = new Vector3(nrm.z, 0, -nrm.x).normalized;   // tangente = up × normal (direita de quem olha)
                var ctr = face + t * slide + up * (lift - H0 * .03f) + nrm * .03f;
                var P = new List<Vector3>(); var UV = new List<Vector2>(); var N = new List<Vector3>();
                foreach (var (sx, sy) in new[] { (-.5f, -.5f), (.5f, -.5f), (.5f, .5f), (-.5f, .5f) })
                {
                    float x = sx * w, y = sy * hh, xr = x * ca - y * sa, yr = x * sa + y * ca;
                    P.Add(root.InverseTransformPoint(ctr - t * xr + up * yr)); UV.Add(new Vector2(sx + .5f, sy + .5f)); N.Add(root.InverseTransformDirection(nrm).normalized);
                }
                var m = new Mesh { name = "patrocinio_" + tex.name }; m.SetVertices(P); m.SetUVs(0, UV); m.SetNormals(N); m.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
                m.RecalculateBounds(); m.RecalculateTangents();
                var go = new GameObject("Patrocinio"); go.transform.SetParent(root, false); go.layer = 0;
                go.AddComponent<MeshFilter>().sharedMesh = m; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mats[li];
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; made.Add(go); n++;
                Debug.Log($"[FreeBala][patrocinio] {tex.name} → {root.name} em {c:F1} (lado {nrm:F2}, {Dc(c):0} m do centro, vista livre {V:0.0} m)");
            }
            LastReport = $"{map}: {roots.Count} contêineres achados · {cand.Count} laterais livres ({pool.Count} na área) · {n} logos, 1 por contêiner · fora de medida {skipSize} · laterais tampadas {skipBlocked} · lado de balcão {skipCounter} · {mats.Length} patrocinadores";
            Debug.Log("[FreeBala][patrocinio] " + LastReport);
            return n;
        }

        static float WorldSize(Transform t)
        {
            bool any = false; Bounds b = default;
            foreach (var r in t.GetComponentsInChildren<MeshRenderer>()) { if (LODN.IsMatch(r.name)) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            return any ? Mathf.Max(b.size.x, b.size.z) : 0;
        }

        // caixa orientada do contêiner no espaço do próprio objeto: centro, eixo vertical, eixo comprido e eixo da largura (mundo)
        static bool Measure(Transform root, out Vector3 c, out Vector3 up, out Vector3 len, out Vector3 dep, out float h, out float l, out float d, out int skip)
        {
            c = up = len = dep = default; h = l = d = 0; skip = 0; bool any = false; Vector3 mn = default, mx = default;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh || LODN.IsMatch(mf.name) || mf.GetComponent<MeshRenderer>() is not { } mr || !mr.enabled || mf.name == "Patrocinio") continue;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var q = root.InverseTransformPoint(mf.transform.TransformPoint(p)); if (!any) { mn = mx = q; any = true; } else { mn = Vector3.Min(mn, q); mx = Vector3.Max(mx, q); }
                }
            }
            if (!any) return false;
            var e = new[] { Vector3.right, Vector3.up, Vector3.forward }; var ax = new Vector3[3]; var sz = new float[3];
            for (int i = 0; i < 3; i++) { ax[i] = root.TransformDirection(e[i]); sz[i] = root.TransformVector(e[i] * (mx[i] - mn[i])).magnitude; }
            int vi = 0; for (int i = 1; i < 3; i++) if (Mathf.Abs(Vector3.Dot(ax[i], Vector3.up)) > Mathf.Abs(Vector3.Dot(ax[vi], Vector3.up))) vi = i;
            if (Mathf.Abs(Vector3.Dot(ax[vi], Vector3.up)) < .92f) return false;   // tombado/inclinado: pula
            int a1 = (vi + 1) % 3, a2 = (vi + 2) % 3; int li = sz[a1] >= sz[a2] ? a1 : a2, di = li == a1 ? a2 : a1;
            h = sz[vi]; l = sz[li]; d = sz[di];
            // corpo simétrico em volta do pivô; o que sobra de um lado só é toldo/balcão (contêiner de lanche do PT: 2,4 m de corpo + 1,1 m de toldo)
            var mid = (mn + mx) * .5f; float lo = -mn[di], hi = mx[di], kD = sz[di] / Mathf.Max(1e-5f, mx[di] - mn[di]);
            if (lo > 0 && hi > 0) { float core = Mathf.Min(lo, hi); if (core * kD >= .7f && (lo + hi - 2 * core) * kD > .25f) { skip = hi > lo ? 1 : -1; mid[di] = 0; d = 2 * core * kD; } }
            if (h < 1.8f || h > 3.6f || l < 2.4f || l > 13.5f || d < 1.6f || d > 3.4f) return false;
            c = root.TransformPoint(mid); up = Vector3.up;
            len = Vector3.ProjectOnPlane(ax[li], Vector3.up).normalized; dep = Vector3.ProjectOnPlane(ax[di], Vector3.up).normalized;
            return true;
        }

        // lateral tampada: outro objeto sólido (não o próprio contêiner) a menos de 0,8 m da face em 3 de 5 pontos
        static bool Blocked(Transform root, Vector3 face, Vector3 n, Vector3 up, float h, float l)
        {
            var t = Vector3.Cross(up, n).normalized; int hit = 0; int mask = FBWorld.SolidMask;
            foreach (var (u, v) in new[] { (0f, 0f), (-.3f, 0f), (.3f, 0f), (0f, .25f), (0f, -.25f) })
            {
                var o = face + t * (u * l) + up * (v * h) + n * .05f;
                foreach (var hh in Physics.RaycastAll(o, n, .8f, mask, QueryTriggerInteraction.Ignore))
                    if (!hh.transform.IsChildOf(root)) { hit++; break; }
            }
            return hit >= 3;
        }

        // r63: distância livre média na frente da lateral (5 raios na largura, altura do meio e um pouco acima), até 10 m
        static float Vista(Transform root, Vector3 face, Vector3 n, Vector3 up, float h, float l)
        {
            var t = Vector3.Cross(up, n).normalized; float sum = 0, mn = 10f; int k = 0; int mask = FBWorld.SolidMask;
            foreach (float u in new[] { -.4f, -.2f, 0f, .2f, .4f })
                foreach (float v in new[] { 0f, .2f })
                {
                    var o = face + t * (u * l) + up * (v * h) + n * .05f; float d = 10f;
                    foreach (var hh in Physics.RaycastAll(o, n, 10f, mask, QueryTriggerInteraction.Ignore))
                        if (!hh.transform.IsChildOf(root) && hh.distance < d) d = hh.distance;
                    sum += d; mn = Mathf.Min(mn, d); k++;
                }
            return Mathf.Min(sum / k, mn + 3f);   // um canto tampado já derruba a nota
        }

        public static IReadOnlyList<GameObject> Made => made;
        // teste -fbpatrocinio <mapa>: guarda modo/mapa salvos do jogador para devolver no fim
        public static string keepMap, keepMode;

        public static void Clear() { foreach (var g in made) if (g) { if (Application.isPlaying) Object.Destroy(g); else Object.DestroyImmediate(g); } made.Clear(); }
    }

    // -fbpatrocinio <mapa>: câmera própria de frente para cada logo (e uma vista de longe) → <build>/autoteste/patrocinio_<mapa>_NN.png; fecha no fim
    public class FBSponsorShots : MonoBehaviour
    {
        // r62b: render direto numa textura (no mata-mata a HUD e a câmera do jogo voltam a cada quadro e cobriam a câmera do teste)
        static void Shot(Camera cam, string path)
        {
            var rt = RenderTexture.GetTemporary(1920, 1080, 24); var old = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = old;
            var prev = RenderTexture.active; RenderTexture.active = rt; var t = new Texture2D(1920, 1080, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); t.Apply(false);
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); System.IO.File.WriteAllBytes(path, t.EncodeToPNG()); Object.Destroy(t);
        }
        System.Collections.IEnumerator Start()
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"); System.IO.Directory.CreateDirectory(dir);
            yield return new WaitForSecondsRealtime(5);
            foreach (var cv in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) cv.enabled = false;
            var cam = new GameObject("CamPatrocinio").AddComponent<Camera>(); cam.depth = 100; cam.fieldOfView = 50; cam.nearClipPlane = .1f; cam.farClipPlane = 1500;
            var main = Camera.main; if (main) { cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor; }
            var urp = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() ?? cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); urp.renderPostProcessing = true;
            int i = 0; string map = FBSession.map;
            foreach (var g in FBSponsors.Made.Where(x => x))
            {
                var mf = g.GetComponent<MeshFilter>(); var m = mf.sharedMesh; var root = g.transform;
                for (int q = 0; q + 3 < m.vertexCount && i < 14; q += 4)
                {
                    var v = m.vertices; var nn = m.normals; var c = root.TransformPoint((v[q] + v[q + 1] + v[q + 2] + v[q + 3]) * .25f); var n = root.TransformDirection(nn[q]).normalized;
                    float w = (root.TransformPoint(v[q]) - root.TransformPoint(v[q + 1])).magnitude;
                    cam.transform.position = c + n * (w * 1.9f + 1.5f) + Vector3.up * .4f; cam.transform.LookAt(c);
                    yield return new WaitForSecondsRealtime(.6f); yield return new WaitForEndOfFrame();
                    foreach (var cv in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) cv.enabled = false;   // a HUD nasce depois: esconde de novo
                    { var cr = root.parent ? root.parent.GetComponentsInChildren<MeshRenderer>().Where(x => x.gameObject != g).ToArray() : new MeshRenderer[0];
                      string path = ""; for (var t = root.parent; t && path.Length < 160; t = t.parent) path = t.name + "/" + path;
                      Debug.Log($"[FreeBala][patrocinio] print {i:00}: {path} · logo em {c:F1} · normal {n:F2} · {cr.Length} renderers no contêiner, visíveis {cr.Count(x => x.isVisible)}, ativos {cr.Count(x => x.enabled && x.gameObject.activeInHierarchy)} · camada {(root.parent ? root.parent.gameObject.layer : -1)} · LODGroup {(root.parent ? root.parent.GetComponentInParent<LODGroup>() != null : false)}"); }
                    Shot(cam, System.IO.Path.Combine(dir, $"patrocinio_{map}_{i:00}.png")); i++;
                    if (i == 1) { cam.transform.position = c + n * 22 + Vector3.up * 7 + Vector3.Cross(Vector3.up, n) * 9; cam.transform.LookAt(c); yield return new WaitForSecondsRealtime(.6f); yield return new WaitForEndOfFrame(); Shot(cam, System.IO.Path.Combine(dir, $"patrocinio_{map}_longe.png")); }
                }
            }
            Debug.Log($"[FreeBala][patrocinio] prints: {i} · " + FBSponsors.LastReport);
            yield return new WaitForSecondsRealtime(1);
            if (FBSponsors.keepMap != null) { FBSession.map = FBSponsors.keepMap; FBSession.mode = FBSponsors.keepMode; FBSession.Save(); }
            Application.Quit();
        }
    }
}
