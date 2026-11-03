// GRAMA — versão final (20/10, pedido do usuário: "cobertura uniforme, natural, de altura baixa, com densidade e movimentação realistas,
// respeitando o desempenho"). Substitui o tapete de tufos em cartões do FBVegetation (que fica só com as touceiras de borda):
//  · máscara do gramado + cor do chão + altura do chão (FBVegData.grassMask / grassHeight, gerados na importação pelo FBVegBake);
//  · dois anéis em volta da câmera principal — perto: touceiras pequenas e densas (12 lâminas); longe: 7 lâminas mais largas, encolhendo
//    até sumir; a passagem entre os dois é pontilhada (sem anel visível). Cada anel = UM draw (Graphics.RenderMeshPrimitives) só com os
//    BLOCOS de 4 m que têm gramado, estão dentro do raio e no campo de visão (a CPU escolhe os blocos, até 256 por anel; antes a grade
//    inteira ia para a GPU e custava ~40 % do quadro). Posição, sorteio, máscara, vento e corte fino ficam no vertex shader
//    (FreeBala/GrassCarpet): nenhum GameObject por tufo, nenhuma matriz por instância;
//  · alcance/densidade por nível (FBGfx.F.grass*): PC Alto 14 m + 34 m, PC Médio 11 + 26, Web/Mobile 8 + 17;
//  · contrato com o benchmark: FBGrassCarpet.Cells = touceiras submetidas no quadro.
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
    public class FBGrassCarpet : MonoBehaviour
    {
        public FBVegData data;
        public static int Cells;
        const float TS = 4f; const int MaxTiles = 256;
        Mesh near, far; MaterialPropertyBlock mpN, mpF; Material mat; bool[] tileHas; int tnx, tnz; FBVegData built;
        readonly Vector4[] tilesN = new Vector4[MaxTiles], tilesF = new Vector4[MaxTiles]; readonly Plane[] planes = new Plane[6];

        // filho do objeto "Vegetacao" do mapa: some junto com o mapa (não desenha o gramado de um mapa no outro)
        public static void Ensure(FBVegetation veg)
        {
            var g = veg.GetComponentInChildren<FBGrassCarpet>(true); var d = veg.data;
            if (!d || !d.grassMask || !d.grassMat) { if (g) g.enabled = false; return; }
            if (!g) { var go = new GameObject("FB_GramaTapete"); go.transform.SetParent(veg.transform, false); g = go.AddComponent<FBGrassCarpet>(); }
            g.data = d; g.enabled = true;
        }

        void OnEnable()
        {
            near ??= Clump(12, 1); far ??= Clump(7, 2); mpN ??= new MaterialPropertyBlock(); mpF ??= new MaterialPropertyBlock();
        }
        void OnDestroy() { if (near) Destroy(near); if (far) Destroy(far); Cells = 0; }

        // touceira: 'n' lâminas na unidade (-0,5..0,5), cada uma 5 vértices (raiz, meio, ponta) = 3 triângulos, sem textura
        static Mesh Clump(int n, int seed)
        {
            var rnd = new System.Random(seed * 7919); float R() => (float)rnd.NextDouble();
            var v = new Vector3[n * 5]; var nr = new Vector3[n * 5]; var uv0 = new Vector2[n * 5]; var uv1 = new Vector2[n * 5]; var tri = new int[n * 9];
            for (int b = 0; b < n; b++)
            {
                var p = new Vector3(R() - .5f, 0, R() - .5f); float a = R() * Mathf.PI * 2; var f = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var u1 = new Vector2(R(), (R() * 2 - 1) * .8f);
                (float side, float t)[] k = { (-1, 0), (1, 0), (-1, .55f), (1, .55f), (0, 1) };
                for (int j = 0; j < 5; j++) { int i = b * 5 + j; v[i] = p; nr[i] = f; uv0[i] = new Vector2(k[j].side, k[j].t); uv1[i] = u1; }
                int o = b * 5, q = b * 9; tri[q] = o; tri[q + 1] = o + 2; tri[q + 2] = o + 1; tri[q + 3] = o + 1; tri[q + 4] = o + 2; tri[q + 5] = o + 3; tri[q + 6] = o + 2; tri[q + 7] = o + 4; tri[q + 8] = o + 3;
            }
            var m = new Mesh { name = "FB_touceira_" + n, vertices = v, normals = nr, uv = uv0, uv2 = uv1, triangles = tri };
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 2); m.UploadMeshData(true); return m;
        }

        // blocos de 4 m com algum gramado (lido uma vez da máscara: alfa > 0)
        void BuildTiles()
        {
            built = data; tnx = Mathf.CeilToInt(data.grassRect.z / TS); tnz = Mathf.CeilToInt(data.grassRect.w / TS); tileHas = new bool[tnx * tnz];
            var mk = data.grassMask; Color32[] px;
            try { px = mk.GetPixels32(); } catch { for (int i = 0; i < tileHas.Length; i++) tileHas[i] = true; return; }   // máscara não legível: desenha todos
            int w = mk.width, h = mk.height; float sx = w / data.grassRect.z, sz = h / data.grassRect.w;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x].a == 0) continue;
                    int tx = Mathf.Min(tnx - 1, (int)(x / sx / TS)), tz = Mathf.Min(tnz - 1, (int)(y / sz / TS)); tileHas[tz * tnx + tx] = true;
                }
        }

        // [parceiro] v1 (thread de física): tempo do Update medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_Update = FBTempo.Id("FBGrassCarpet.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            Cells = 0; var cam = Camera.main; var f = FBGfx.F;
            if (!cam || !data || !data.grassMask || !f.grass) return;
            if (built != data || tileHas == null) BuildTiles();
            mat = data.grassMat; var cp = cam.transform.position; GeometryUtility.CalculateFrustumPlanes(cam, planes);
            var rect = new Vector4(data.grassRect.x, data.grassRect.y, 1f / Mathf.Max(1, data.grassRect.z), 1f / Mathf.Max(1, data.grassRect.w));
            // perto: até grassNear (some pontilhado nos 2 m finais); longe: entra pontilhado no mesmo trecho e encolhe até grassFar
            Cells += Ring(cam, cp, near, mpN, tilesN, rect, f.grassNearSpacing, f.grassNear, 0, f.grassNear, f.grassNear, 1.7f, 0, f.grassDensity, 0);
            Cells += Ring(cam, cp, far, mpF, tilesF, rect, f.grassFarSpacing, f.grassFar, f.grassNear, f.grassFar * .72f, f.grassFar, 1.6f, .012f, f.grassDensity, 1);
        }

        int Ring(Camera cam, Vector3 cp, Mesh mesh, MaterialPropertyBlock mp, Vector4[] tiles, Vector4 rect, float sp0, float R, float r0, float r1, float r2, float clumpK, float extraW, float dens, int outer)
        {
            if (sp0 <= 0 || R <= 0) return 0;
            int cpt = Mathf.Max(2, Mathf.RoundToInt(TS / sp0)); float sp = TS / cpt;                  // passo que fecha o bloco (células inteiras)
            float gx = data.grassRect.x, gz = data.grassRect.y, y0 = data.grassY0; int nt = 0;
            int tx0 = Mathf.Max(0, Mathf.FloorToInt((cp.x - R - gx) / TS)), tx1 = Mathf.Min(tnx - 1, Mathf.FloorToInt((cp.x + R - gx) / TS));
            int tz0 = Mathf.Max(0, Mathf.FloorToInt((cp.z - R - gz) / TS)), tz1 = Mathf.Min(tnz - 1, Mathf.FloorToInt((cp.z + R - gz) / TS));
            for (int tz = tz0; tz <= tz1 && nt < MaxTiles; tz++)
                for (int tx = tx0; tx <= tx1 && nt < MaxTiles; tx++)
                {
                    if (!tileHas[tz * tnx + tx]) continue;
                    float ax = gx + tx * TS, az = gz + tz * TS;
                    float dx = Mathf.Max(ax - cp.x, 0, cp.x - (ax + TS)), dz = Mathf.Max(az - cp.z, 0, cp.z - (az + TS));
                    if (dx * dx + dz * dz > R * R) continue;                                          // fora do raio
                    if (outer == 1)
                    {
                        float fx = Mathf.Max(Mathf.Abs(ax - cp.x), Mathf.Abs(ax + TS - cp.x)), fz = Mathf.Max(Mathf.Abs(az - cp.z), Mathf.Abs(az + TS - cp.z));
                        if (fx * fx + fz * fz < (r0 - 2) * (r0 - 2)) continue;                         // inteiro dentro do anel de perto
                    }
                    if (!GeometryUtility.TestPlanesAABB(planes, new Bounds(new Vector3(ax + TS / 2, y0 + 3, az + TS / 2), new Vector3(TS + 1, 12, TS + 1)))) continue;
                    tiles[nt++] = new Vector4(ax, az, 0, 0);
                }
            if (nt == 0) return 0;
            mp.SetVector("_GC_Grid", new Vector4(0, 0, sp, cpt)); mp.SetVector("_GC_Ring", new Vector4(r0, r1, r2, sp * clumpK));
            mp.SetVectorArray("_GC_Tiles", tiles);
            mp.SetVector("_GC_Rect", rect); mp.SetVector("_GC_Misc", new Vector4(y0, dens, extraW, outer));
            mp.SetTexture("_GC_Mask", data.grassMask); mp.SetTexture("_GC_HeightTex", data.grassHeight);
            var rp = new RenderParams(mat)
            {
                camera = cam, layer = FBWorld.LMap, matProps = mp, receiveShadows = true, shadowCastingMode = ShadowCastingMode.Off,
                lightProbeUsage = LightProbeUsage.Off, reflectionProbeUsage = ReflectionProbeUsage.Off,
                worldBounds = new Bounds(new Vector3(cp.x, y0 + 2, cp.z), new Vector3(2 * R + 8, 60, 2 * R + 8)),
            };
            int n = nt * cpt * cpt; Graphics.RenderMeshPrimitives(rp, mesh, 0, n); return n;
        }
    }
}
