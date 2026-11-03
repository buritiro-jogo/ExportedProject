// CHUVA (refeita 20/10 — retorno: "as gotas não chegam corretamente ao chão"). Antes: caixa de emissão 14 m acima da câmera, vida
// fixa de 1,1 s a 12–15 m/s -> as gotas morriam 0,7–2,7 m acima do piso, atravessavam telhados e os respingos saíam embaixo das coberturas.
// Agora (recursos do Dyna-Wet que cabem no nosso URP, reimplementados — o repositório não tem licença):
//  · COLISÃO: grade de 32×32 células de 1,5 m presa ao mundo em volta da câmera guarda a altura da PRIMEIRA superfície vista de cima
//    (telhado, marquise, carro, chão ou lâmina d'água), refeita aos poucos por raios (96/quadro no PC, 32 no celular). Cada gota vive o
//    bastante para cair 30 m e é morta exatamente quando passa dessa altura -> chega ao chão, para no telhado, não entra no galpão;
//  · RESPINGOS: no ponto onde a gota morre (anel abrindo + gotículas que pulam; na água das fontes só o anel);
//  · SUPERFÍCIE MOLHADA: o FBWeather escurece/alisa os pisos (ApplyWet) e aqui entram POÇAS (FreeBala/Puddle) que enchem com o tempo
//    de chuva, refletem (sonda + SSR) e têm anéis de gota — lugares fixos por mapa (centros da navmesh, sorteio pela posição), só em
//    asfalto/calçada/piso plano e descoberto, longe umas das outras. Uma malha, um draw.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
    public class FBRain : MonoBehaviour
    {
        const int G = 32; const float C = 1.5f;
        ParticleSystem drops, ring, spray; ParticleSystem.Particle[] buf; readonly float[] top = new float[G * G]; readonly bool[] water = new bool[G * G];
        readonly int[] stamp = new int[G * G]; Vector2Int origin = new(int.MinValue, 0); int cursor, raysPerFrame; float fill = .7f, groundFallback; bool mobile;   // a partida já começa com chuva: chão com poças (enchem até o máximo em ~20 s)
        GameObject puddles; static Material puddleMat;
        public static FBRain I;

        public static FBRain Begin(Transform parent, float groundY)
        {
            var go = new GameObject("FB_Chuva"); go.transform.SetParent(parent, false); var r = go.AddComponent<FBRain>(); r.groundFallback = groundY; return r;
        }

        void Start()
        {
            I = this; mobile = FBQuality.Mobile; raysPerFrame = mobile ? 32 : 96; buf = new ParticleSystem.Particle[mobile ? 2600 : 9200];
            drops = MakeDrops(); (ring, spray) = MakeSplash(); Shader.SetGlobalFloat("_FBRainK", 1); Shader.SetGlobalFloat("_FBPuddleFill", 0);
            BuildPuddles();
        }
        void OnDestroy() { if (I == this) I = null; Shader.SetGlobalFloat("_FBPuddleFill", 0); Shader.SetGlobalFloat("_FBRainK", 0); if (puddles) Destroy(puddles); }

        static Material Mat(Texture2D t)
        { var m = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit), false); m.SetTexture("_BaseMap", t); m.mainTexture = t; m.SetFloat("_Cull", 0); return m; }

        // riscos finos com degradê, inclinados pelo vento, mais densos perto da câmera; vida longa: quem mata é a grade
        ParticleSystem MakeDrops()
        {
            var g = new GameObject("Gotas"); g.transform.SetParent(transform, false); var ps = g.AddComponent<ParticleSystem>(); ps.Stop();
            var m = ps.main; m.loop = true; m.startLifetime = 2.6f; m.startSpeed = 0; m.maxParticles = buf.Length; m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startSize3D = true; m.startSizeX = new ParticleSystem.MinMaxCurve(.008f, .016f); m.startSizeY = new ParticleSystem.MinMaxCurve(.35f, .6f); m.startSizeZ = .01f; m.startColor = new Color(.78f, .83f, .92f, .32f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.x = new ParticleSystem.MinMaxCurve(1.2f, 2.2f); vel.y = new ParticleSystem.MinMaxCurve(-15f, -12f); vel.z = new ParticleSystem.MinMaxCurve(.3f, .9f);
            var e = ps.emission; e.rateOverTime = mobile ? 1900 : 6800; var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(44, 1, 44);
            var r = g.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = .028f; r.lengthScale = 1.2f; r.shadowCastingMode = ShadowCastingMode.Off;
            var t = new Texture2D(8, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 8; x++) { float v = y / 63f, u = Mathf.Abs(x - 3.5f) / 3.5f; float a = Mathf.Sin(v * Mathf.PI) * Mathf.Pow(v, .4f) * (1 - u * u); t.SetPixel(x, y, new Color(1, 1, 1, a)); }
            t.Apply(); r.material = Mat(t); ps.Play(); return ps;
        }
        // respingo: anel deitado que abre e some + gotículas que pulam (só emitidos por script, no ponto de impacto)
        (ParticleSystem, ParticleSystem) MakeSplash()
        {
            var g = new GameObject("Respingos"); g.transform.SetParent(transform, false);
            var rp = g.AddComponent<ParticleSystem>(); rp.Stop();
            var m = rp.main; m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(.25f, .45f); m.startSpeed = 0; m.maxParticles = mobile ? 400 : 1800; m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startSize = new ParticleSystem.MinMaxCurve(.05f, .12f); m.startColor = new Color(.85f, .9f, 1, .45f);
            var e = rp.emission; e.rateOverTime = 0; var sh = rp.shape; sh.enabled = false;
            var so = rp.sizeOverLifetime; so.enabled = true; so.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .3f, 1, 2.6f));
            var co = rp.colorOverLifetime; co.enabled = true; var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) }); co.color = gr;
            var rr = g.GetComponent<ParticleSystemRenderer>(); rr.renderMode = ParticleSystemRenderMode.HorizontalBillboard; rr.shadowCastingMode = ShadowCastingMode.Off;
            var rt = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) { float d = new Vector2(x - 15.5f, y - 15.5f).magnitude / 15.5f; float a = Mathf.Clamp01(1 - Mathf.Abs(d - .8f) / .14f); rt.SetPixel(x, y, new Color(1, 1, 1, a)); }
            rt.Apply(); rr.material = Mat(rt); rp.Play();
            var g2 = new GameObject("Goticulas"); g2.transform.SetParent(g.transform, false); var dr = g2.AddComponent<ParticleSystem>(); dr.Stop();
            var m2 = dr.main; m2.loop = true; m2.startLifetime = new ParticleSystem.MinMaxCurve(.18f, .3f); m2.startSpeed = 0; m2.gravityModifier = 1.2f; m2.maxParticles = mobile ? 300 : 1400;
            m2.simulationSpace = ParticleSystemSimulationSpace.World; m2.startSize = new ParticleSystem.MinMaxCurve(.012f, .025f); m2.startColor = new Color(.85f, .9f, 1, .5f);
            var e2 = dr.emission; e2.rateOverTime = 0; var sh2 = dr.shape; sh2.enabled = false;
            var r2 = g2.GetComponent<ParticleSystemRenderer>(); r2.shadowCastingMode = ShadowCastingMode.Off;
            var dt = new Texture2D(16, 16, TextureFormat.RGBA32, false); for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) { float d = new Vector2(x - 7.5f, y - 7.5f).magnitude / 7.5f; dt.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1 - d))); }
            dt.Apply(); r2.material = Mat(dt); dr.Play();
            return (rp, dr);
        }

        // grade de colisão: célula (i, j) do mundo = floor(x / C); a janela 32×32 acompanha a câmera e só refaz o que mudou
        int Idx(int cx, int cz) { int i = cx - origin.x, j = cz - origin.y; return (uint)i < G && (uint)j < G ? j * G + i : -1; }
        void Refresh(Vector3 cam)
        {
            var o = new Vector2Int(Mathf.FloorToInt(cam.x / C) - G / 2, Mathf.FloorToInt(cam.z / C) - G / 2);
            if (o != origin) { origin = o; System.Array.Clear(stamp, 0, stamp.Length); cursor = 0; }
            int miss = 0;
            for (int k = 0; k < G * G && miss < raysPerFrame; k++)
            {
                int idx = (cursor + k) % (G * G); if (stamp[idx] != 0 && Time.frameCount - stamp[idx] < 90) continue;   // refresca a cada ~1,5 s
                int i = idx % G, j = idx / G; float x = (origin.x + i + .5f) * C, z = (origin.y + j + .5f) * C;
                var p = new Vector3(x, cam.y + 45, z);
                float y = Physics.Raycast(p, Vector3.down, out var h, 140, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) ? h.point.y : groundFallback;
                bool w = false; if (FBWorld.WaterAt(x, z) is float wy && wy >= y - .05f) { y = wy; w = true; }
                top[idx] = y; water[idx] = w; stamp[idx] = Mathf.Max(1, Time.frameCount); miss++;
            }
            cursor = (cursor + raysPerFrame) % (G * G);
        }

        // [parceiro 01/11] medidor de tempo por sistema (FBTempo): com FBTempo.On soma o custo do LateUpdate da chuva
        static readonly int __fbt_LateUpdate = FBTempo.Id("FBRain.LateUpdate");
        void LateUpdate()
        {
            if (!FBTempo.On) { LateUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
        }
        void LateUpdate__fbt()
        {
            var cam = Camera.main; if (!cam || !drops) return; var cp = cam.transform.position;
            drops.transform.position = cp + cam.transform.forward.Flat() * 6 + Vector3.up * 16;
            Refresh(cp);
            fill = Mathf.Min(1, fill + Time.deltaTime / 75f); Shader.SetGlobalFloat("_FBPuddleFill", Mathf.SmoothStep(.15f, 1, fill));   // poças enchem em ~1 min
            int n = drops.GetParticles(buf); int splashes = 0, maxSpl = mobile ? 14 : 48; var ep = new ParticleSystem.EmitParams();
            float fallback = groundFallback;
            for (int k = 0; k < n; k++)
            {
                var p = buf[k].position; int idx = Idx(Mathf.FloorToInt(p.x / C), Mathf.FloorToInt(p.z / C));
                float y = idx >= 0 && stamp[idx] != 0 ? top[idx] : fallback;
                if (p.y > y) continue;
                buf[k].remainingLifetime = -1;                                                       // morreu na superfície
                if (splashes < maxSpl && (p - cp).sqrMagnitude < 26 * 26)
                {
                    splashes++; ep.position = new Vector3(p.x, y + .02f, p.z); ep.applyShapeToPosition = false; ring.Emit(ep, 1);
                    if (idx < 0 || !water[idx]) { ep.velocity = new Vector3(Random.Range(-.6f, .6f), Random.Range(.9f, 1.6f), Random.Range(-.6f, .6f)); spray.Emit(ep, 1); ep.velocity = Vector3.zero; }
                }
            }
            drops.SetParticles(buf, n);
        }

        // ---------------------------------------------------------------- poças
        void BuildPuddles()
        {
            if (!FBNav.Ready) return; var pts = FBNav.Fixed(); int want = mobile ? 45 : 140; var got = new List<(Vector3 p, float s, float a, float seed)>();
            var sh = Shader.Find("FreeBala/Puddle"); var refs = Resources.Load<FBShaderRefs>("FBShaderRefs"); if (refs && refs.puddle) sh = refs.puddle; if (!sh) return;
            foreach (var c0 in pts)
            {
                if (got.Count >= want) break;
                float h1 = Hash(c0.x, c0.z, 1), h2 = Hash(c0.x, c0.z, 2), h3 = Hash(c0.x, c0.z, 3); if (h1 > .55f) continue;   // ~metade dos pontos
                var c = c0 + new Vector3((h2 - .5f) * 3, 0, (h3 - .5f) * 3);
                if (!Physics.Raycast(c + Vector3.up * 3, Vector3.down, out var hit, 8, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) || hit.normal.y < .985f) continue;
                if (hit.collider.gameObject.layer != FBWorld.LMap || !HardFloor(hit) || FBWorld.WaterAt(hit.point.x, hit.point.z) != null) continue;   // só piso duro
                if (Physics.Raycast(hit.point + Vector3.up * .3f, Vector3.up, 30, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)) continue;                   // coberto: sem poça
                float s = Mathf.Lerp(1.4f, 3.6f, Hash(c.x, c.z, 4));
                bool flat = true;                                                                       // a poça inteira no mesmo plano (sem meio-fio)
                for (int q = 0; q < 4 && flat; q++)
                {
                    var o = hit.point + Quaternion.Euler(0, q * 90 + 45, 0) * Vector3.forward * (s * .45f);
                    flat = Physics.Raycast(o + Vector3.up * .5f, Vector3.down, out var h2_, 1.2f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) && Mathf.Abs(h2_.point.y - hit.point.y) < .03f;
                }
                if (!flat || got.Exists(g => (g.p - hit.point).sqrMagnitude < 6 * 6)) continue;
                got.Add((hit.point, s, Hash(c.x, c.z, 5) * 360, Hash(c.x, c.z, 6)));
            }
            Debug.Log($"[FreeBala][chuva] poças {got.Count} (de {pts.Count} pontos da navmesh)");
            if (got.Count == 0) return;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var uv2 = new List<Vector2>(); var tri = new List<int>();
            foreach (var (p, s, a, seed) in got)
            {
                var rot = Quaternion.Euler(0, a, 0); int b = v.Count; float sx = s, sz = s * Mathf.Lerp(.55f, 1f, seed);
                foreach (var (u, w) in new[] { (0f, 0f), (1f, 0f), (1f, 1f), (0f, 1f) })
                { v.Add(p + Vector3.up * .012f + rot * new Vector3((u - .5f) * sx, 0, (w - .5f) * sz)); uv.Add(new Vector2(u, w)); uv2.Add(new Vector2(seed, Hash(p.x, p.z, 7))); }
                tri.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            var mesh = new Mesh { name = "FB_Pocas" }; mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetUVs(1, uv2); mesh.SetTriangles(tri, 0);
            mesh.SetNormals(v.ConvertAll(_ => Vector3.up)); mesh.RecalculateBounds();
            puddleMat ??= new Material(sh) { name = "FB_Poca", enableInstancing = false };
            puddles = new GameObject("FB_Pocas") { layer = FBWorld.LMap }; puddles.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = puddles.AddComponent<MeshRenderer>(); mr.sharedMaterial = puddleMat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = true;
            puddles.transform.SetParent(transform, true);
        }
        // material do TRIÂNGULO atingido (o chão do mapa é uma malha com vários materiais: o 1º material dizia "grama" para tudo)
        static readonly System.Text.RegularExpressions.Regex Hard = new("asphalt|asfalto|road|sidewalk|calcada|paver|pav_|piso|concrete|concreto|curb|slab|laje|stone|pedra|bike", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        static bool HardFloor(RaycastHit h)
        {
            var r = h.collider.GetComponent<MeshRenderer>(); var mesh = (h.collider as MeshCollider) ? ((MeshCollider)h.collider).sharedMesh : null;   // malha do COLISOR: no build o
            if (!r || !mesh) return false;                                                                                                        // MeshFilter vira o lote estático
            var ms = r.sharedMaterials; int sub = 0;
            if (mesh.subMeshCount > 1 && h.triangleIndex >= 0)
            {
                int ti = h.triangleIndex * 3;
                for (int s = 0; s < mesh.subMeshCount; s++) if (ti >= (int)mesh.GetSubMesh(s).indexStart) sub = s;
            }
            return sub < ms.Length && ms[sub] && Hard.IsMatch(ms[sub].name);
        }
        static float Hash(float x, float z, int s) { uint h = (uint)(Mathf.RoundToInt(x * 10) * 374761393 + Mathf.RoundToInt(z * 10) * 668265263 + s * 2246822519u); h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffffff) / 16777215f; }
    }
}
