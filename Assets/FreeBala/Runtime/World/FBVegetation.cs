// VEGETAÇÃO INSTANCIADA dos mapas (Agente 5). Os tufos/capins vêm do FBVegData (gerado na importação pelo FBVegBake: Poisson nas superfícies
// verdes, raio no chão, sem ruas/calçadas/prédios). Aqui só se DESENHA — nenhum GameObject por tufo, nenhum colisor:
//  · células de ~24 m testadas contra o frustum e a distância; as visíveis, da mais perto para a mais longe, enchem um buffer por tipo até o
//    teto de instâncias do nível; a lista só é refeita quando a câmera anda/gira (ou a cada 0,4 s);
//  · Graphics.RenderMeshInstanced em lotes de 1023 (material FreeBala/Foliage com _FB_VEG: vento por tufo, encolhe até sumir no fim do
//    alcance, tom variando por manchas no mundo — sem MaterialPropertyBlock, compatível com instancing);
//  · densidade = prefixo do array de cada célula (sorteio fixo por posição: o mesmo tufo existe em todos os clientes do mesmo nível);
//  · nível (FBQuality): VegetationDensity, VegDrawDistance, VegMaxInstances, VegShadows (FBVegetation.ConfigOverride substitui tudo);
//  · contrato com o Agente 9: FBQuality.VegVisibleInstances / VegTotalInstances (= FBVegetation.Visible / Total) a cada quadro.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
    public struct FBVegConfig { public float density, drawDistance; public int maxInstances; public bool shadows; }

    public class FBVegetation : MonoBehaviour
    {
        public FBVegData data;
        public static System.Func<FBVegConfig> ConfigOverride;
        public static int Visible, Total;
        public static FBVegetation I;
        FBVegConfig cfg; Matrix4x4[][] buf; int[] cnt; Vector3 lastPos; Quaternion lastRot; float rebuildT; readonly Plane[] planes = new Plane[6];
        readonly List<(float d, FBVegData.Cell c)> vis = new(); Bounds visBounds;

        // orçamento do Agente 9 (FBQuality: densidade, alcance, teto de instâncias visíveis, sombra por nível e plataforma)
        public static FBVegConfig DefaultConfig() => new FBVegConfig
        {
            density = FBQuality.VegetationDensity, drawDistance = Mathf.Max(15, FBQuality.VegDrawDistance),
            maxInstances = Mathf.Max(500, FBQuality.VegMaxInstances), shadows = FBQuality.VegShadows,
        };

        void OnEnable() { I = this; FBQuality.Changed += Apply; FBGfx.Changed += Apply; Apply(); FBGrassCarpet.Ensure(this); FBGrassATG.Ensure(this); }   // tapete de lâminas (Praça das Águas) + grama do ATG espalhada pela máscara (Vila Jardim)
        void OnDisable() { FBQuality.Changed -= Apply; FBGfx.Changed -= Apply; if (I == this) { I = null; Visible = Total = 0; } }

        void Apply()
        {
            cfg = ConfigOverride != null ? ConfigOverride() : DefaultConfig(); cfg.density = Mathf.Clamp01(cfg.density);
            if (!data || data.kinds == null) return;
            buf = new Matrix4x4[data.kinds.Length][]; cnt = new int[data.kinds.Length];
            for (int k = 0; k < buf.Length; k++) buf[k] = new Matrix4x4[Mathf.Max(1, cfg.maxInstances)];
            int tot = 0; foreach (var c in data.cells) tot += Take(c); Total = tot; FBQuality.VegTotalInstances = tot;
            Shader.SetGlobalVector("_FBVegFade", new Vector4(cfg.drawDistance * .72f, cfg.drawDistance, 0, 0));
            rebuildT = 0; lastPos = new Vector3(1e9f, 0, 0);
        }
        int Take(FBVegData.Cell c) => c.m == null ? 0 : Mathf.CeilToInt(c.m.Length * cfg.density);

        void Rebuild(Camera cam)
        {
            GeometryUtility.CalculateFrustumPlanes(cam, planes); var cp = cam.transform.position; vis.Clear();
            foreach (var c in data.cells)
            {
                float d = Vector3.Distance(c.center, cp) - c.radius; if (d > cfg.drawDistance || c.m == null || c.m.Length == 0) continue;
                if (!GeometryUtility.TestPlanesAABB(planes, new Bounds(c.center, Vector3.one * (c.radius * 2)))) continue;
                vis.Add((d, c));
            }
            vis.Sort((a, b) => a.d.CompareTo(b.d));
            for (int k = 0; k < cnt.Length; k++) cnt[k] = 0;
            int left = cfg.maxInstances; bool first = true;
            foreach (var (_, c) in vis)
            {
                int n = Mathf.Min(Take(c), left); if (n <= 0) break;
                var b = buf[c.kind]; n = Mathf.Min(n, b.Length - cnt[c.kind]); if (n <= 0) continue;
                System.Array.Copy(c.m, 0, b, cnt[c.kind], n); cnt[c.kind] += n; left -= n;
                var cb = new Bounds(c.center, Vector3.one * (c.radius * 2 + 4)); if (first) { visBounds = cb; first = false; } else visBounds.Encapsulate(cb);
            }
            lastPos = cp; lastRot = cam.transform.rotation; rebuildT = .4f;
        }

        // [parceiro] v1 (thread de física): tempo de CPU medido pelo FBTempo (desligado = só um teste de bool)
        static readonly int __fbt_Update = FBTempo.Id("FBVegetation.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }

        void Update__fbt()
        {
            var cam = Camera.main; if (!cam || !data || buf == null) { Visible = 0; return; }
            rebuildT -= Time.deltaTime;
            if (rebuildT <= 0 || (cam.transform.position - lastPos).sqrMagnitude > 2.25f || Quaternion.Angle(cam.transform.rotation, lastRot) > 4) Rebuild(cam);
            int drawn = 0;
            for (int k = 0; k < cnt.Length; k++)
            {
                var kd = data.kinds[k]; if (cnt[k] == 0 || !kd.mesh || !kd.mat) continue;
                var rp = new RenderParams(kd.mat)
                {
                    camera = cam, layer = FBWorld.LMap, receiveShadows = true, worldBounds = visBounds, lightProbeUsage = LightProbeUsage.Off,
                    shadowCastingMode = kd.shadows && cfg.shadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                };
                for (int s = 0; s < cnt[k]; s += 1023) Graphics.RenderMeshInstanced(rp, kd.mesh, 0, buf[k], Mathf.Min(1023, cnt[k] - s), s);
                drawn += cnt[k];
            }
            Visible = drawn; FBQuality.VegVisibleInstances = drawn;                            // contrato com o Agente 9 (benchmark)
        }
    }
}
