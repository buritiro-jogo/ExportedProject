// Campo de grama denso em volta da câmera (complementa o FBVegetation, que espalha arbustos/tufos maiores pelo mapa todo).
// Tufo = lâminas em geometria real (sem textura com alfa → sem borda branca de PNG), com degradê base escura → ponta clara.
// O chão é dividido em quadros de 6 m; cada quadro perto da câmera é gerado UMA vez (grade com sorteio fixo por posição, raio até o piso,
// só onde o material do piso é grama) e guardado; o desenho é Graphics.RenderMeshInstanced em lotes de 1023. Densidade e alcance pelo
// FBQuality (celular bem menos). Sem colisor, sem GameObject por tufo.
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
    public class FBGrassField : MonoBehaviour
    {
        const float CELL = 6f;
        static readonly Regex GRASS = new(@"grass|grama|lawn|gramado|canteiro", RegexOptions.IgnoreCase);
        readonly Dictionary<Vector2Int, Matrix4x4[]> cells = new(); readonly Dictionary<Collider, bool> greenCol = new();
        readonly List<Matrix4x4> batch = new(1023); Mesh tuft; Material mat; float spacing, radius;

        public static void Ensure() { if (!FindAnyObjectByType<FBGrassField>()) new GameObject("FB_CampoGrama").AddComponent<FBGrassField>(); }

        void Start()
        {
            enabled = false; return;   // DESLIGADO a pedido (tufos destoavam do cenário); código mantido para referência
#pragma warning disable CS0162
            tuft = BuildTuft(); mat = BuildMat();
            float d = Mathf.Clamp(FBQuality.VegetationDensity, .2f, 1.5f);
            spacing = Mathf.Lerp(.95f, .42f, Mathf.InverseLerp(.2f, 1.2f, d));      // ~1 a ~5,7 tufos por m²
            radius = FBQuality.Mobile ? 18 : Mathf.Lerp(22, 38, Mathf.InverseLerp(.4f, 1.2f, d));
        }

        static Mesh BuildTuft()
        {
            // 7 lâminas inclinadas em leque (2 triângulos cada), alturas e curvas variadas; UV.y = altura (degradê no material)
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>(); var n = new List<Vector3>(); var rnd = new System.Random(11);
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2 + (float)rnd.NextDouble() * .6f, h = .22f + (float)rnd.NextDouble() * .2f, w = .022f + (float)rnd.NextDouble() * .012f, lean = .06f + (float)rnd.NextDouble() * .1f;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); var side = new Vector3(-dir.z, 0, dir.x); var root = dir * .03f;
                int b = v.Count;
                v.Add(root - side * w); v.Add(root + side * w); v.Add(root + dir * lean * .5f + Vector3.up * h * .55f - side * w * .6f); v.Add(root + dir * lean * .5f + Vector3.up * h * .55f + side * w * .6f); v.Add(root + dir * lean + Vector3.up * h);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(0, .55f)); uv.Add(new Vector2(1, .55f)); uv.Add(new Vector2(.5f, 1));
                for (int k = 0; k < 5; k++) n.Add(Vector3.up);
                tri.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3, b + 2, b + 4, b + 3 });
            }
            var m = new Mesh { name = "tufo_grama" }; m.SetVertices(v); m.SetUVs(0, uv); m.SetNormals(n); m.SetTriangles(tri, 0); m.RecalculateBounds(); return m;
        }
        static Material BuildMat()
        {
            var tx = new Texture2D(4, 64, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) { float t = y / 63f; var c = Color.Lerp(new Color(.11f, .2f, .05f), new Color(.42f, .55f, .2f), Mathf.Pow(t, .8f)); for (int x = 0; x < 4; x++) tx.SetPixel(x, y, c); }
            tx.Apply();
            var tpl = Resources.Load<Material>("FBMats/Lit_Clip"); var m = tpl ? new Material(tpl) : new Material(FBShaders.Lit);
            m.name = "grama_campo"; m.SetTexture("_BaseMap", tx); m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", .05f); m.SetFloat("_Cull", 0); m.enableInstancing = true; return m;
        }

        // grama só onde o TRIÂNGULO atingido é de material de grama (o chão do mapa é uma malha com vários materiais: asfalto, calçada, grama…)
        readonly Dictionary<Mesh, (int[] end, bool[] green)> subs = new();
        bool Green(RaycastHit h)
        {
            var c = h.collider; if (!c) return false; var r = c.GetComponent<Renderer>(); if (!r) return false; var mats = r.sharedMaterials;
            var mc = c as MeshCollider; var mesh = mc ? mc.sharedMesh : null;
            if (!mesh || mesh.subMeshCount <= 1 || h.triangleIndex < 0) { if (greenCol.TryGetValue(c, out var g1)) return g1; bool any = mats.Length == 1 && mats[0] && GRASS.IsMatch(mats[0].name); return greenCol[c] = any; }
            if (!subs.TryGetValue(mesh, out var info))
            {
                var end = new int[mesh.subMeshCount]; var gr = new bool[mesh.subMeshCount]; int acc = 0;
                for (int i = 0; i < mesh.subMeshCount; i++) { var d = mesh.GetSubMesh(i); acc = d.indexStart / 3 + d.indexCount / 3; end[i] = acc; gr[i] = i < mats.Length && mats[i] && GRASS.IsMatch(mats[i].name); }
                subs[mesh] = info = (end, gr);
            }
            for (int i = 0; i < info.end.Length; i++) if (h.triangleIndex < info.end[i]) return info.green[i];
            return false;
        }

        Matrix4x4[] Generate(Vector2Int key)
        {
            var list = new List<Matrix4x4>(); var rnd = new System.Random(key.x * 73856093 ^ key.y * 19349663);
            float x0 = key.x * CELL, z0 = key.y * CELL; int n = Mathf.CeilToInt(CELL / spacing);
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
            {
                float x = x0 + (i + (float)rnd.NextDouble()) * spacing, z = z0 + (j + (float)rnd.NextDouble()) * spacing;
                if (!Physics.Raycast(new Vector3(x, 400, z), Vector3.down, out var h, 800, 1 << FBWorld.LMap, QueryTriggerInteraction.Ignore)) continue;
                if (h.normal.y < .75f || !Green(h)) continue;
                // tufos menores (o mundo está em escala ~2,2: tufos grandes viravam moitas)
                float s = .45f + (float)rnd.NextDouble() * .4f; var rot = Quaternion.FromToRotation(Vector3.up, h.normal) * Quaternion.Euler(0, (float)rnd.NextDouble() * 360, 0);
                list.Add(Matrix4x4.TRS(h.point - h.normal * .01f, rot, new Vector3(s, s * (.8f + (float)rnd.NextDouble() * .5f), s)));
            }
            return list.ToArray();
        }

        static readonly int __fbt_Update = FBTempo.Id("FBGrassField.Update");   // [parceiro 01/11] medidor FBTempo
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            var cam = Camera.main; if (!cam || !tuft) return; var p = cam.transform.position;
            int r = Mathf.CeilToInt(radius / CELL), cx = Mathf.FloorToInt(p.x / CELL), cz = Mathf.FloorToInt(p.z / CELL), made = 0;
            var rp = new RenderParams(mat) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true, layer = 0 };
            var planes = GeometryUtility.CalculateFrustumPlanes(cam); batch.Clear();
            for (int i = -r; i <= r; i++) for (int j = -r; j <= r; j++)
            {
                var key = new Vector2Int(cx + i, cz + j); var cc = new Vector3((key.x + .5f) * CELL, p.y, (key.y + .5f) * CELL);
                if ((new Vector2(cc.x - p.x, cc.z - p.z)).sqrMagnitude > (radius + CELL) * (radius + CELL)) continue;
                if (!cells.TryGetValue(key, out var arr)) { if (made >= 3) continue; arr = cells[key] = Generate(key); made++; }   // gera no máximo 3 quadros por frame
                if (arr.Length == 0 || !GeometryUtility.TestPlanesAABB(planes, new Bounds(cc, new Vector3(CELL, 400, CELL)))) continue;
                foreach (var m in arr) { batch.Add(m); if (batch.Count == 1023) { Graphics.RenderMeshInstanced(rp, tuft, 0, batch); batch.Clear(); } }
            }
            if (batch.Count > 0) Graphics.RenderMeshInstanced(rp, tuft, 0, batch);
            if (cells.Count > 900) cells.Clear();   // memória limitada: recomeça quando o jogador anda muito
        }
    }
}
