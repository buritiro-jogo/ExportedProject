// Colisão dos props dos mapas por TIPO (o import punha MeshCollider da malha inteira em tudo que fosse opaco e nada no que tivesse alfa):
//  - árvore: tronco + copa na MESMA malha; quando o 1º material era a folha (alfa) a árvore ficava sem colisão nenhuma, e quando era o
//    tronco a copa inteira virava parede. Agora: cápsula só no tronco (medido por raios na própria malha a 1 m do chão), copa sem colisão;
//  - poste / placa / antena de rua / hidrante: cápsula fina no fuste (o braço da luminária fica de fora);
//  - alambrado / grade / portão / muro baixo / tapume: BoxCollider fino do tamanho da peça (o alambrado é alfa → antes não tinha colisão);
//  - banco, mesa, tambor, caixa d'água, lixeira, contêiner, jersey, carrinho, motos/carros de enfeite: BoxCollider;
//  - vegetação rasteira (capim, mato, junco, flores, moita, planta): SEM colisão.
// Os colisores ficam em filhos "FBCol_<tipo>" na camada do mapa (tiro, câmera e NavMesh enxergam; o NavMesh contorna). Malhas agrupadas
// (mobiliário/postes/tapumes fundidos num objeto só) continuam com o MeshCollider do import — listadas como casos ambíguos no log.
// Chamada pelo FBImport depois dos colisores do mapa e antes do NavMesh: FBPropColliders.Apply(id, raiz).
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBPropColliders
    {
        public enum Kind { None, Pole, Tree, Fence, Box, Merged }
        public const string Prefix = "FBCol_";
        static readonly RegexOptions I = RegexOptions.IgnoreCase;
        // ordem importa: vegetação antes de árvore (PA_fl_*), blocos de prédio com nome de árvore (VJ_Samauma/Araca) nunca são árvore
        static readonly (Regex rx, Kind k)[] RULES = {
            (new Regex(@"capim|mato|junco|_fl_|planta|moita|grass|lavanda|lirio|folhagem|arbusto|flor", I), Kind.None),
            (new Regex(@"^\w\w_(arvore|palmeira)", I), Kind.Tree),
            (new Regex(@"poste_br|poste$|placa|hidrante|semaforo|antena_rua|totem|orelhao", I), Kind.Pole),
            (new Regex(@"alambrado|grade|portao|port[aã]o|cerca|gradil|tapume_|muret", I), Kind.Fence),
            (new Regex(@"banco|mesas?_|mesa$|tambor|caixa_dagua|caixa_tanque|lixeira|lixo|conteiner|jersey|cor_sacos|carrinho|food_truck|churrasqueira|letra|moto_|fusca|_gol|taunus|saveiro|peugeot|onibus|cadeira|cone|barril|vaso", I), Kind.Box),
            (new Regex(@"mobiliario|postes_|tapumes|equipamentos|sinalizacao|parquinho|vida_detalhes", I), Kind.Merged),
        };
        public static Kind KindOf(string name) { var b = Base(name); foreach (var (rx, k) in RULES) if (rx.IsMatch(b)) return k; return (Kind)(-1); }
        static string Base(string n) => Regex.Replace(n, @"[\d_.]+$", "");

        public class Report { public readonly Dictionary<Kind, int> n = new(); public readonly List<string> ambiguous = new(); public int removedMesh; public override string ToString() => string.Join(" · ", n.Select(kv => $"{kv.Key} {kv.Value}")) + $" · MeshCollider removidos {removedMesh}" + (ambiguous.Count > 0 ? " · ambíguos: " + string.Join(", ", ambiguous.Distinct().Take(20)) : ""); }

        public static Report Apply(string mapId, GameObject root)
        {
            var rep = new Report(); if (!root) return rep;
            Physics.SyncTransforms();
            // instâncias de prop = filhos diretos da raiz do mapa (é assim que os .gltf dos 5 mapas vêm: PA_poste_br_012, PT_Arvore_3…)
            var props = new List<Transform>(); foreach (Transform t in root.transform) props.Add(t);
            foreach (var p in props)
            {
                var k = KindOf(p.name); if ((int)k < 0) continue;
                foreach (var old in p.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith(Prefix)).ToList()) Object.DestroyImmediate(old.gameObject);
                var mfs = p.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh).ToList(); if (mfs.Count == 0) continue;
                // nome de prop em objeto grande (ex.: VJ_Bat_Muro_Portao = muro inteiro do batalhão) = malha agrupada: fica com o MeshCollider
                var sz = LocalBounds(p, mfs).size; float big = Mathf.Max(sz.x, sz.z) * Mathf.Max(p.lossyScale.x, p.lossyScale.z);
                if (k != Kind.None && big > (k == Kind.Fence ? 8 : k == Kind.Box ? 16 : 14)) k = Kind.Merged;
                if (k == Kind.Merged) { rep.n[k] = rep.n.TryGetValue(k, out var m0) ? m0 + 1 : 1; rep.ambiguous.Add(Base(p.name) + " (malha agrupada: MeshCollider do import)"); continue; }
                bool ok = k switch { Kind.None => true, Kind.Tree => Trunk(p, mfs, true), Kind.Pole => Trunk(p, mfs, false), _ => Box(p, mfs, k) };
                if (!ok) { rep.ambiguous.Add(Base(p.name) + " (sem medida: mantém MeshCollider)"); continue; }
                foreach (var mf in mfs) foreach (var c in mf.GetComponents<MeshCollider>()) { Object.DestroyImmediate(c); rep.removedMesh++; }
                rep.n[k] = rep.n.TryGetValue(k, out var c0) ? c0 + 1 : 1;
            }
            Debug.Log($"[FreeBala][props] {mapId}: {rep}");
            return rep;
        }

        static GameObject Child(Transform p, Kind k)
        {
            var g = new GameObject(Prefix + k.ToString().ToLowerInvariant()); g.layer = p.gameObject.layer; g.isStatic = true;
            g.transform.SetParent(p, false); return g;
        }

        // caixa local da peça inteira (bounds das malhas no espaço da raiz do prop — não precisa de malha legível)
        static Bounds LocalBounds(Transform p, List<MeshFilter> mfs, System.Func<MeshFilter, int, bool> sub = null)
        {
            var w2l = p.worldToLocalMatrix; bool first = true; var b = new Bounds();
            foreach (var mf in mfs)
            {
                var m = mf.sharedMesh; var M = w2l * mf.transform.localToWorldMatrix;
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    if (sub != null && !sub(mf, s)) continue; var sb = m.GetSubMesh(s).bounds; if (sb.size == Vector3.zero) sb = m.bounds;
                    for (int c = 0; c < 8; c++) { var v = M.MultiplyPoint3x4(sb.center + Vector3.Scale(sb.extents, new Vector3((c & 1) * 2 - 1, (c & 2) - 1, (c & 4) / 2 - 1))); if (first) { b = new Bounds(v, Vector3.zero); first = false; } else b.Encapsulate(v); }
                }
            }
            return b;
        }

        static bool Box(Transform p, List<MeshFilter> mfs, Kind k)
        {
            var b = LocalBounds(p, mfs); if (b.size.sqrMagnitude < 1e-4f) return false;
            var sz = b.size; if (k == Kind.Fence) { int thin = sz.x < sz.z ? 0 : 2; sz[thin] = Mathf.Max(sz[thin], .12f); }   // grade fina: no mínimo 12 cm (o CC não "vaza")
            sz.y = Mathf.Max(sz.y, .05f);
            var bc = Child(p, k).AddComponent<BoxCollider>(); bc.center = b.center; bc.size = sz; return true;
        }

        // fuste/tronco: raios horizontais na própria malha a 1 m (e 0,4 m) do pé, dos quatro lados → largura do tronco; cápsula vertical
        static bool Trunk(Transform p, List<MeshFilter> mfs, bool tree)
        {
            bool Trunky(MeshFilter mf, int s) { var r = mf.GetComponent<Renderer>(); var mats = r ? r.sharedMaterials : null; var n = mats != null && s < mats.Length && mats[s] ? mats[s].name : ""; return !tree || mats == null || mats.Length < 2 || Regex.IsMatch(n, "tronco|trunk|bark|casca", I); }
            var all = LocalBounds(p, mfs); var tb = LocalBounds(p, mfs, Trunky); if (all.size.sqrMagnitude < 1e-4f) return false;
            // malha temporária para medir (o import pode ter pulado a árvore por causa do alfa da copa)
            var temp = new List<MeshCollider>();
            foreach (var mf in mfs) if (!mf.GetComponent<MeshCollider>()) { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; temp.Add(mc); }
            var cols = mfs.SelectMany(m => m.GetComponents<MeshCollider>()).ToList(); Physics.SyncTransforms();
            Vector3 c = Vector3.zero; float rad = 0; int nOk = 0;
            foreach (float hy in new[] { 1f, .4f })
            {
                var o = p.TransformPoint(new Vector3(tb.center.x, all.min.y, tb.center.z)) + Vector3.up * hy * p.lossyScale.y;
                float far = Mathf.Max(all.size.x, all.size.z) * Mathf.Max(p.lossyScale.x, p.lossyScale.z) + 1; var hits = new List<Vector3>();
                foreach (var d in new[] { p.right, -p.right, p.forward, -p.forward })
                {
                    var ray = new Ray(o + d * far, -d); float best = 1e9f; Vector3 bp = default;
                    foreach (var mc in cols) if (mc.Raycast(ray, out var h, far * 2) && h.distance < best) { best = h.distance; bp = h.point; }
                    if (best < 1e9f) hits.Add(bp);
                }
                if (hits.Count < 4) continue;
                var cc = (hits[0] + hits[1] + hits[2] + hits[3]) / 4; float r = Mathf.Max((hits[0] - hits[1]).magnitude, (hits[2] - hits[3]).magnitude) * .5f;
                if (r > (tree ? .9f : .45f)) continue;                                           // pegou galho/braço/placa: tenta mais baixo
                c = cc; rad = r; nOk++; break;
            }
            foreach (var mc in temp) Object.DestroyImmediate(mc);
            if (nOk == 0) { c = p.TransformPoint(new Vector3(tb.center.x, all.min.y, tb.center.z)); rad = tree ? .25f : .12f; }   // sem medida: centro do tronco
            float sy = Mathf.Max(.01f, p.lossyScale.y), baseY = p.TransformPoint(new Vector3(0, all.min.y, 0)).y;
            float topY = tree ? Mathf.Min(p.TransformPoint(new Vector3(0, tb.max.y, 0)).y, baseY + 3.2f) : p.TransformPoint(new Vector3(0, all.max.y, 0)).y;
            if (tree && Regex.IsMatch(p.name, "palmeira", I)) topY = baseY + (p.TransformPoint(new Vector3(0, all.max.y, 0)).y - baseY) * .75f;   // palmeira: estipe até a copa
            var go = Child(p, tree ? Kind.Tree : Kind.Pole); var cap = go.AddComponent<CapsuleCollider>();
            go.transform.position = new Vector3(c.x, baseY, c.z); go.transform.rotation = Quaternion.identity;
            var ls = go.transform.lossyScale; float sx = Mathf.Max(.01f, Mathf.Max(ls.x, ls.z)); sy = Mathf.Max(.01f, ls.y);
            cap.direction = 1; cap.radius = Mathf.Clamp(rad, tree ? .12f : .06f, tree ? .6f : .3f) / sx; cap.height = Mathf.Max(topY - baseY, cap.radius * 2 * sx) / sy; cap.center = new Vector3(0, cap.height * .5f, 0);
            return true;
        }
    }
}
