// Objetos pequenos destrutíveis (cadeiras, mesas, lixeiras, cones, caixotes, tambores, bancos de plástico): cada um tem "vida" pelo
// material/tamanho; o tiro (FBCombat.Impact) tira vida e dá um tranco; ao zerar, o objeto some e se parte em pedaços com física
// (cubos com o material dele, empurrados na direção do tiro), poeira e som. Pedaços dormem e somem em alguns segundos (pool global com teto).
// Montado no início da partida a partir dos colisores de caixa que o FBPropColliders criou nos props.
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
    public class FBDestructible : MonoBehaviour
    {
        static readonly Regex SMALL = new(@"cadeira|mesas?_|mesa$|lixeira|lixo|cone|caixote|caixa_feira|tambor|banco_plast|engradado|balde|vaso|placa_rua|cavalete", RegexOptions.IgnoreCase);
        static readonly Regex PLASTIC = new(@"plast|cadeira|cone|balde|lixeira|engradado", RegexOptions.IgnoreCase), WOOD = new(@"madeira|caixote|cavalete|mesa_bar", RegexOptions.IgnoreCase);
        public float hp, max; Renderer[] rends; Collider[] cols; bool broken; Vector3 size; string kind;
        static readonly List<(GameObject g, float t)> debris = new(); const int MaxDebris = 90;

        public static int Setup(GameObject mapRoot)
        {
            int n = 0; if (!mapRoot) return 0;
            foreach (var bc in mapRoot.GetComponentsInChildren<BoxCollider>(true))
            {
                if (!bc.name.StartsWith("FBCol_box")) continue; var prop = bc.transform.parent; if (!prop || !SMALL.IsMatch(prop.name) || prop.GetComponent<FBDestructible>()) continue;
                var d = prop.gameObject.AddComponent<FBDestructible>(); var s = Vector3.Scale(bc.size, bc.transform.lossyScale); d.size = s;
                d.kind = PLASTIC.IsMatch(prop.name) ? "plastico" : WOOD.IsMatch(prop.name) ? "madeira" : "metal";
                float vol = Mathf.Clamp(s.x * s.y * s.z, .02f, 2f); d.max = d.hp = (d.kind == "plastico" ? 35 : d.kind == "madeira" ? 70 : 120) * Mathf.Lerp(.7f, 1.6f, vol / 2f);
                d.rends = prop.GetComponentsInChildren<Renderer>(); d.cols = prop.GetComponentsInChildren<Collider>(); n++;
            }
            return n;
        }

        public static bool TryHit(Collider c, float dmg, Vector3 point, Vector3 dir)
        {
            var d = c ? c.GetComponentInParent<FBDestructible>() : null; if (!d || d.broken) return false;
            d.hp -= dmg; d.transform.position += dir.normalized * .015f;                       // tranco leve a cada tiro
            if (d.hp <= 0) d.Break(point, dir); return true;
        }

        // quebra: a PRÓPRIA malha do objeto se parte em pedaços (triângulos agrupados por uma grade 3×2×3 sobre a caixa — cada pedaço mantém
        // material e UV, então parece o objeto quebrado), com física, empurrados pelo tiro/carro; somem encolhendo. Malha sem leitura:
        // lascas pequenas na cor média do material (nada de cubos grandes com a textura inteira)
        void Break(Vector3 point, Vector3 dir)
        {
            broken = true; var b = new Bounds(transform.position, Vector3.zero); bool fb = true; foreach (var r in rends) if (r) { if (fb) { b = r.bounds; fb = false; } else b.Encapsulate(r.bounds); }
            foreach (var c in cols) if (c) c.enabled = false; int made = 0;
            foreach (var r in rends)
            {
                if (!r || !r.enabled || made >= 14) continue; var mf = r.GetComponent<MeshFilter>(); var m = mf ? mf.sharedMesh : null; if (!m || !m.isReadable) continue;
                var V = m.vertices; var N = m.normals; var U = m.uv; var M = r.transform.localToWorldMatrix; var W = new Vector3[V.Length]; for (int i = 0; i < V.Length; i++) W[i] = M.MultiplyPoint3x4(V[i]);
                var mb = r.bounds; float mg = mb.size.magnitude; int nx = mb.size.x > mg * .3f ? 3 : 1, ny = mb.size.y > mg * .3f ? 2 : 1, nz = mb.size.z > mg * .3f ? 3 : 1;
                var cells = new Dictionary<int, List<(int sub, int a, int b, int c)>>();
                for (int sm = 0; sm < m.subMeshCount; sm++)
                {
                    var T = m.GetTriangles(sm);
                    for (int t = 0; t < T.Length; t += 3)
                    {
                        var cen = (W[T[t]] + W[T[t + 1]] + W[T[t + 2]]) / 3;
                        int ix = Mathf.Clamp((int)((cen.x - mb.min.x) / Mathf.Max(.001f, mb.size.x) * nx), 0, nx - 1), iy = Mathf.Clamp((int)((cen.y - mb.min.y) / Mathf.Max(.001f, mb.size.y) * ny), 0, ny - 1), iz = Mathf.Clamp((int)((cen.z - mb.min.z) / Mathf.Max(.001f, mb.size.z) * nz), 0, nz - 1);
                        int key = (ix * 4 + iy) * 4 + iz; if (!cells.TryGetValue(key, out var l)) cells[key] = l = new List<(int, int, int, int)>(); l.Add((sm, T[t], T[t + 1], T[t + 2]));
                    }
                }
                foreach (var cell in cells.Values)
                {
                    if (made >= 14) break; var map = new Dictionary<int, int>(); var pv = new List<Vector3>(); var pn = new List<Vector3>(); var pu = new List<Vector2>();
                    var subs = new List<int>[m.subMeshCount]; for (int k = 0; k < subs.Length; k++) subs[k] = new List<int>();
                    Vector3 c0 = Vector3.zero; foreach (var t in cell) c0 += W[t.a]; c0 /= cell.Count;
                    int Vtx(int i) { if (map.TryGetValue(i, out var o)) return o; map[i] = pv.Count; pv.Add(W[i] - c0); pn.Add(N.Length == V.Length ? M.MultiplyVector(N[i]).normalized : Vector3.up); pu.Add(U.Length == V.Length ? U[i] : Vector2.zero); return pv.Count - 1; }
                    foreach (var t in cell) { subs[t.sub].Add(Vtx(t.a)); subs[t.sub].Add(Vtx(t.b)); subs[t.sub].Add(Vtx(t.c)); }
                    var pm = new Mesh { name = "pedaco" }; if (pv.Count > 65000) pm.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    pm.SetVertices(pv); pm.SetNormals(pn); pm.SetUVs(0, pu); pm.subMeshCount = subs.Length; for (int k = 0; k < subs.Length; k++) pm.SetTriangles(subs[k], k); pm.RecalculateBounds();
                    var g = new GameObject("pedaco"); g.transform.position = c0; g.AddComponent<MeshFilter>().sharedMesh = pm; var pr = g.AddComponent<MeshRenderer>(); pr.sharedMaterials = r.sharedMaterials; pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var bc = g.AddComponent<BoxCollider>(); bc.center = pm.bounds.center; bc.size = Vector3.Max(pm.bounds.size, Vector3.one * .04f);
                    Fly(g, c0, point, dir, Mathf.Clamp(pm.bounds.size.magnitude * 4, .3f, 3)); made++;
                }
            }
            if (made == 0)
            {   // reserva: lascas pequenas na cor do material
                var src = rends.Length > 0 && rends[0] ? rends[0].sharedMaterial : null; var col = src && src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : new Color(.6f, .6f, .6f);
                int n = Mathf.Clamp(Mathf.RoundToInt(b.size.magnitude * 10), 8, 16);
                for (int i = 0; i < n; i++)
                {
                    var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = "lasca"; var p = new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y), Random.Range(b.min.z, b.max.z));
                    float k = Mathf.Min(.09f, b.size.magnitude * .06f) * Random.Range(.6f, 1.3f); g.transform.SetPositionAndRotation(p, Random.rotation); g.transform.localScale = new Vector3(k, k * Random.Range(.2f, .5f), k * Random.Range(.6f, 1.4f));
                    var rr = g.GetComponent<Renderer>(); rr.sharedMaterial = new Material(FBShaders.Lit); rr.sharedMaterial.SetColor("_BaseColor", col * Random.Range(.75f, 1.05f));   // material do cubo primitivo é do pipeline antigo (rosa no URP)
                    rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    Fly(g, p, point, dir, .3f);
                }
            }
            foreach (var r in rends) if (r) r.enabled = false;
            while (debris.Count > MaxDebris) { if (debris[0].g) Destroy(debris[0].g); debris.RemoveAt(0); }
            FBFx.Puff(b.center, kind == "plastico" ? new Color(.8f, .8f, .8f) : new Color(.62f, .55f, .45f), .35f, .6f, .4f);
            FBAudio.PlayAt(kind == "metal" ? "impact3" : kind == "madeira" ? "impact2" : "impact1", b.center, .9f, 6);
            if (!cleaner) cleaner = new GameObject("FB_Cacos").AddComponent<Cleaner>();
        }
        static void Fly(GameObject g, Vector3 p, Vector3 point, Vector3 dir, float mass)
        {
            var rb = g.AddComponent<Rigidbody>(); rb.mass = mass; rb.linearDamping = .3f; rb.angularDamping = .6f; rb.sleepThreshold = .2f;
            var away = p - point; away.y = Mathf.Max(0, away.y);
            rb.AddForce(dir.normalized * Random.Range(1.5f, 3.5f) + away.normalized * Random.Range(.8f, 2f) + Vector3.up * Random.Range(1f, 2.5f), ForceMode.VelocityChange); rb.AddTorque(Random.insideUnitSphere * 5, ForceMode.VelocityChange);
            debris.Add((g, Time.time + Random.Range(6f, 9f)));
        }
        static Cleaner cleaner;
        class Cleaner : MonoBehaviour
        {
            // [parceiro] v1 (thread de física): tempo de CPU medido pelo FBTempo (desligado = só um teste de bool)
            static readonly int __fbt_Update = FBTempo.Id("FBDestructible.Cleaner.Update");
            void Update()
            {
                if (!FBTempo.On) { Update__fbt(); return; }
                long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
            }
            void Update__fbt() { for (int i = debris.Count - 1; i >= 0; i--) { var d = debris[i]; if (!d.g || Time.time > d.t) { if (d.g) Destroy(d.g); debris.RemoveAt(i); continue; } float left = d.t - Time.time; if (left < 1) d.g.transform.localScale = Vector3.one * Mathf.Max(.01f, left); } } }   // somem encolhendo no último segundo
    }
}
