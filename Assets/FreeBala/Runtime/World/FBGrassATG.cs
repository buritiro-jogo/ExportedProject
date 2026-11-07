// GRAMA ESPALHADA EM TEMPO DE JOGO (malhas do Advanced Terrain Grass, asset do usuário) — Vila Jardim.
// Por que não é tudo assado no asset: a área jogável da Vila Jardim tem ~1 km². O teto do asset (120 mil instâncias) daria ~0,1 tufo por m²,
// ou seja, tufo solto no chão pelado. Aqui a grama PERTO da câmera é gerada na hora, a partir da máscara de chão que o FBVegBake gravou
// (FBVegData.grassMask: o alfa é a DENSIDADE — gramado 1, terra ~0,4, junta de muro/meio-fio ~0,9, asfalto 0 — e grassHeight é a altura do
// chão). Nada de raio de física: a máscara já nasceu de raios no bake, com teto/marquise/parede descontados.
//  · GRAMADO FECHADO perto e ralo longe: a célula é gerada UMA vez na densidade máxima (carpetDensity, ~14 tufos/m²) com as instâncias
//    ORDENADAS por sorteio fixo; no desenho usa-se um PREFIXO do array conforme a distância (1,0 até 1/4 do alcance, caindo a 0,08 no fim).
//    Assim o chão fecha junto da câmera, rareia com a distância e nada "pisca" quando o jogador anda (o prefixo só cresce ou diminui);
//  · células de 8 m; 30 por quadro ao chegar num lugar novo, 4 depois; o cache joga fora o que ficou longe (nunca o tapete inteiro);
//  · desenho por tipo com Graphics.RenderMeshInstanced em lotes de 1023 — nenhum GameObject, nenhum colisor, um material só (atlas do ATG);
//  · teto de segurança: o orçamento de instâncias visíveis do FBQuality (Ultra PC 90 mil, celular 10 mil), repartido por tipo;
//  · as touceiras altas (capim, mato alto) continuam vindo do asset assado (FBVegetation), porque são manchas fixas do mapa.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
    public class FBGrassATG : MonoBehaviour
    {
        public FBVegData data;
        public static int Tufos;                                                    // contrato com o benchmark (tufos desenhados no quadro)
        const float CELL = 8f;
        const int MaxCells = 900;                                                   // ~225 células cabem no alcance; o resto é rastro de onde o jogador passou

        readonly Dictionary<Vector2Int, Matrix4x4[][]> cells = new();
        readonly List<Matrix4x4> batch = new(1023);
        readonly List<Vector2Int> longe = new();
        readonly Plane[] planes = new Plane[6];
        FBVegData built; byte[] mask; ushort[] height; int mw, mh, hw, hh, cota; float spacing, radius, dens, raioCheio;

        // filho do objeto "Vegetacao" do mapa (some junto com o mapa)
        public static void Ensure(FBVegetation veg)
        {
            var g = veg.GetComponentInChildren<FBGrassATG>(true); var d = veg.data;
            bool pode = d && d.grassMask && d.carpetKinds != null && d.carpetKinds.Length > 0;
            if (!pode) { if (g) g.enabled = false; return; }
            if (!g) { var go = new GameObject("FB_GramaATG"); go.transform.SetParent(veg.transform, false); g = go.AddComponent<FBGrassATG>(); }
            g.data = d; g.enabled = true; g.Config();
        }

        void OnEnable() { FBGfx.Changed += Config; FBQuality.Changed += Config; Config(); }
        void OnDisable() { FBGfx.Changed -= Config; FBQuality.Changed -= Config; Tufos = 0; }

        public void Config()
        {
            var f = FBGfx.F;
            dens = Mathf.Max(.5f, (data ? data.carpetDensity : 12f) * Mathf.Clamp(f.grassDensity, .2f, 1.5f) * Mathf.Clamp(FBQuality.VegetationDensity, .25f, 1.4f));
            spacing = 1f / Mathf.Sqrt(dens);
            radius = Mathf.Max(8, (f.grassFar > 0 ? f.grassFar : 24) * 1.6f);       // mais longe que o tapete de lâminas: aqui é malha, não cartão de 2 tri
            raioCheio = radius * .42f;                                              // até aqui o gramado é cheio; depois vai rareando
            // (06/11) com .25 o gramado só fechava nos 13 m da frente: do meio da tela para trás já se via o chão entre os tufos
            int nk = data && data.carpetKinds != null ? Mathf.Max(1, data.carpetKinds.Length) : 1;
            cota = Mathf.Max(600, FBQuality.VegMaxInstances / nk);
            cells.Clear();                                                          // densidade/alcance mudaram: refaz
        }

        bool Ler()
        {
            if (built == data && mask != null) return true;
            built = data; mask = null; height = null;
            if (!data || !data.grassMask) return false;
            try
            {
                mw = data.grassMask.width; mh = data.grassMask.height;
                if (data.grassMask.format == TextureFormat.R8)                       // máscara da grama ATG: 1 byte por célula (só a densidade)
                {
                    var raw = data.grassMask.GetRawTextureData<byte>(); mask = new byte[raw.Length]; raw.CopyTo(mask);
                }
                else                                                                 // formato antigo (RGBA, cor do chão + densidade no alfa)
                {
                    var px = data.grassMask.GetPixels32(); mask = new byte[px.Length];
                    for (int i = 0; i < px.Length; i++) mask[i] = px[i].a;
                }
                if (data.grassHeight)
                {
                    var raw = data.grassHeight.GetRawTextureData<ushort>(); hw = data.grassHeight.width; hh = data.grassHeight.height;
                    height = new ushort[raw.Length]; raw.CopyTo(height);
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[FreeBala] grama ATG: máscara não legível (" + e.Message + ")"); mask = null; }
            return mask != null;
        }

        float Densidade(float x, float z)
        {
            var r = data.grassRect; float u = (x - r.x) / Mathf.Max(.01f, r.z), v = (z - r.y) / Mathf.Max(.01f, r.w);
            if (u < 0 || v < 0 || u >= 1 || v >= 1) return 0;
            int px = Mathf.Min(mw - 1, (int)(u * mw)), pz = Mathf.Min(mh - 1, (int)(v * mh));
            return mask[pz * mw + px] / 255f;
        }

        // altura do chão: bilinear na textura RHalf (metros acima de grassY0)
        float Altura(float x, float z)
        {
            if (height == null) return data.grassY0;
            var r = data.grassRect; float u = (x - r.x) / Mathf.Max(.01f, r.z) * hw - .5f, v = (z - r.y) / Mathf.Max(.01f, r.w) * hh - .5f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(u), 0, hw - 1), z0 = Mathf.Clamp(Mathf.FloorToInt(v), 0, hh - 1);
            int x1 = Mathf.Min(hw - 1, x0 + 1), z1 = Mathf.Min(hh - 1, z0 + 1); float fx = Mathf.Clamp01(u - x0), fz = Mathf.Clamp01(v - z0);
            float a = Mathf.HalfToFloat(height[z0 * hw + x0]), b = Mathf.HalfToFloat(height[z0 * hw + x1]);
            float c = Mathf.HalfToFloat(height[z1 * hw + x0]), d2 = Mathf.HalfToFloat(height[z1 * hw + x1]);
            return data.grassY0 + Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d2, fx), fz);
        }

        static float Hash(int x, int z, int k)
        {
            uint h = (uint)(x * 73856093 ^ z * 19349663 ^ k * 83492791); h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }

        // gera a célula na densidade MÁXIMA, com as instâncias de cada tipo ordenadas pelo sorteio (o desenho usa um prefixo)
        readonly List<(float ordem, Matrix4x4 m)>[] tmp = new List<(float, Matrix4x4)>[16];
        Matrix4x4[][] Gerar(Vector2Int key)
        {
            int nk = data.carpetKinds.Length;
            for (int i = 0; i < nk && i < tmp.Length; i++) { tmp[i] ??= new List<(float, Matrix4x4)>(256); tmp[i].Clear(); }
            int n = Mathf.Clamp(Mathf.CeilToInt(CELL / spacing), 1, 48);             // até 48x48 = 2304 tufos por célula de 8 m (36/m²)
            float x0 = key.x * CELL, z0 = key.y * CELL, sp = CELL / n;
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
                {
                    int gx = key.x * 64 + i, gz = key.y * 64 + j;                    // semente estável por posição na grade
                    float x = x0 + (i + .12f + Hash(gx, gz, 1) * .76f) * sp, z = z0 + (j + .12f + Hash(gx, gz, 2) * .76f) * sp;
                    float d = Densidade(x, z); if (d <= .02f || Hash(gx, gz, 3) > d) continue;
                    int k = Mathf.Min(nk - 1, (int)(Hash(gx, gz, 4) * nk));
                    float h5 = Hash(gx, gz, 5);
                    float s = .85f + h5 * .75f, sy = s * (.85f + Hash(gx, gz, 6) * .5f);
                    var rot = Quaternion.Euler((Hash(gx, gz, 7) - .5f) * 14, Hash(gx, gz, 8) * 360, (Hash(gx, gz, 9) - .5f) * 14);
                    // ORDEM puxada pelo TAMANHO: longe da câmera o desenho só pega o começo da lista, e o começo são os tufos MAIORES —
                    // com a mesma contagem de instâncias o chão fecha bem mais no meio-campo. Um terço de sorteio puro evita que o
                    // gramado distante vire um tapete de tufos todos do mesmo tamanho.
                    tmp[k].Add(((1 - h5) * .68f + Hash(gx, gz, 10) * .32f, Matrix4x4.TRS(new Vector3(x, Altura(x, z) - .03f, z), rot, new Vector3(s, sy, s))));
                }
            var saida = new Matrix4x4[nk][];
            for (int i = 0; i < nk; i++)
            {
                var l = tmp[i]; l.Sort((a, b) => a.ordem.CompareTo(b.ordem));        // ordem fixa: o prefixo por distância fica estável
                var arr = new Matrix4x4[l.Count];
                for (int t = 0; t < l.Count; t++) arr[t] = l[t].m;
                saida[i] = arr;
            }
            return saida;
        }

        float relogio; bool contou;                                                 // relatório único depois de 3 s (aparece no log do jogo)
        void Relatorio(string porque)
        {
            if (contou) return; contou = true;
            Debug.Log($"[FreeBala] grama ATG: {porque} · tufos={Tufos} células={cells.Count} raio={radius:0} cheio até={raioCheio:0} m " +
                      $"densidade={dens:0.0}/m² passo={spacing:0.00} cota={cota} máscara={(mask != null ? mw + "x" + mh : "não lida")}");
        }

        static readonly int __fbt_Update = FBTempo.Id("FBGrassATG.Update");          // medidor de CPU da casa
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }

        void Update__fbt()
        {
            Tufos = 0; var cam = Camera.main; var f = FBGfx.F;
            relogio += Time.unscaledDeltaTime;
            if (!cam || !data || !f.grass || !Ler() || data.kinds == null)
            {
                if (relogio > 3) Relatorio(!cam ? "sem câmera principal" : !data ? "sem dados" : !f.grass ? "grama desligada no FBGfx" : mask == null ? "máscara não lida" : "sem tipos");
                return;
            }
            var cp = cam.transform.position; GeometryUtility.CalculateFrustumPlanes(cam, planes);
            int r = Mathf.CeilToInt(radius / CELL), cx = Mathf.FloorToInt(cp.x / CELL), cz = Mathf.FloorToInt(cp.z / CELL), novas = 0;
            int nk = data.carpetKinds.Length;
            // ao chegar num lugar novo (nascer, trocar de pose) enche depressa; depois vai devagar
            int orcamento = cells.Count < 90 ? 30 : 4;
            // cache cheio: joga fora só o que está LONGE (um Clear() geral apagava o tapete inteiro a cada quadro)
            if (cells.Count > MaxCells)
            {
                float lim = (radius * 1.5f) * (radius * 1.5f); longe.Clear();
                foreach (var kv in cells)
                {
                    float ax = kv.Key.x * CELL + CELL * .5f - cp.x, az = kv.Key.y * CELL + CELL * .5f - cp.z;
                    if (ax * ax + az * az > lim) longe.Add(kv.Key);
                }
                foreach (var k2 in longe) cells.Remove(k2);
                if (cells.Count > MaxCells) cells.Clear();
            }

            for (int k = 0; k < nk; k++)
            {
                int ki = data.carpetKinds[k]; if (ki < 0 || ki >= data.kinds.Length) continue;
                var kd = data.kinds[ki]; if (!kd.mesh || !kd.mat) continue;
                var rp = new RenderParams(kd.mat)
                {
                    camera = cam, layer = FBWorld.LMap, receiveShadows = true, shadowCastingMode = ShadowCastingMode.Off,
                    lightProbeUsage = LightProbeUsage.Off, reflectionProbeUsage = ReflectionProbeUsage.Off,
                    worldBounds = new Bounds(new Vector3(cp.x, data.grassY0 + 2, cp.z), new Vector3(2 * radius + 8, 80, 2 * radius + 8)),
                };
                batch.Clear(); int noTipo = 0;
                for (int i = -r; i <= r && noTipo < cota; i++) for (int j = -r; j <= r && noTipo < cota; j++)
                    {
                        var key = new Vector2Int(cx + i, cz + j);
                        float ax = key.x * CELL, az = key.y * CELL;
                        float dx = Mathf.Max(ax - cp.x, 0, cp.x - (ax + CELL)), dz = Mathf.Max(az - cp.z, 0, cp.z - (az + CELL));
                        float d2 = dx * dx + dz * dz; if (d2 > radius * radius) continue;
                        if (!cells.TryGetValue(key, out var arr))
                        {
                            if (k > 0 || novas >= orcamento) continue;               // só o primeiro tipo gera (a célula já sai com todos)
                            arr = cells[key] = Gerar(key); novas++;
                        }
                        var m = arr[k]; if (m.Length == 0) continue;
                        if (!GeometryUtility.TestPlanesAABB(planes, new Bounds(new Vector3(ax + CELL / 2, data.grassY0 + 2, az + CELL / 2), new Vector3(CELL + 2, 60, CELL + 2)))) continue;
                        // gramado cheio até 1/4 do alcance, rareando até 8 % na borda (prefixo do array: nada pisca)
                        float dist = Mathf.Sqrt(d2);
                        float frac = dist <= raioCheio ? 1f : Mathf.Lerp(1f, .22f, Mathf.SmoothStep(0, 1, (dist - raioCheio) / Mathf.Max(1, radius - raioCheio)));
                        int take = Mathf.Min(m.Length, Mathf.CeilToInt(m.Length * frac));
                        for (int t = 0; t < take; t++)
                        {
                            batch.Add(m[t]); noTipo++;
                            if (batch.Count == 1023) { Graphics.RenderMeshInstanced(rp, kd.mesh, 0, batch); Tufos += 1023; batch.Clear(); }
                            if (noTipo >= cota) break;
                        }
                    }
                if (batch.Count > 0) { Graphics.RenderMeshInstanced(rp, kd.mesh, 0, batch); Tufos += batch.Count; batch.Clear(); }
            }
            if (relogio > 3) Relatorio(Tufos > 0 ? "desenhando" : "nenhum tufo em volta da câmera " + cam.transform.position);
        }
    }
}
