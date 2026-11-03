// Impacto de bala no cenário por material: concreto/reboco (poeira + lascas + buraco), metal (faíscas + clarão + furo), madeira
// (farpas + pó marrom), terra/areia/lama (coluna de terra, torrões, marca escura), vidro (cacos + trinca), água (coluna + anel),
// vegetação (folhas picadas). Material: FBSurface do colisor → material do submesh atingido (triangleIndex) → nome do colisor.
// Orçamento (como o ImpactFX do Complexo, ver DESCOBERTAS_RE): dedupe de 8 posições recentes (raio 0,25 m / 0,1 s), chance de
// partícula por qualidade, LOD por distância e corte atrás da câmera; o decal (buraco) sai sempre que houver vaga no pool.
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
    public static class FBImpactFx
    {
        public enum Surf { Concrete, Dirt, Sand, Mud, Metal, Wood, Glass, Water, Vegetation, Plaster }

        static ParticleSystem psDust, psChip, psSplinter, psLeaf, psShard, psSpark, psSplash, psRipple, psFlash; static bool ready;
        public static void Warm()
        {
            if (ready) return; ready = true; FBFxQuality.Ensure(); bool hi = FBFxQuality.PoolDecals > 100;
            var smoke = FBFxLib.Mat("Particles_Alpha", FBFxLib.Tex("smoke"), Color.white, "ImpactoPoeira");
            psDust = FBFxLib.PS("ImpactoPoeira", smoke, hi ? 220 : 90, new FBFxLib.Opt { gravity = -.02f, drag = .3f, tilesX = 2, tilesY = 2, spin = true, size = FBFxLib.Curve(0, .45f, .3f, 1, 1, 1.9f), color = FBFxLib.Fade(.06f, .3f) });
            var deb = FBFxLib.Mat("Particles_Alpha", FBFxLib.Tex("debris"), Color.white, "ImpactoDetritos");
            FBFxLib.Opt D(int row, float g) => new FBFxLib.Opt { gravity = g, tilesX = 4, tilesY = 4, row = row, spin = true, color = FBFxLib.Fade(0, .8f) };
            psChip = FBFxLib.PS("ImpactoLascas", deb, hi ? 260 : 100, D(0, 1.1f)); psSplinter = FBFxLib.PS("ImpactoFarpas", deb, hi ? 120 : 50, D(1, 1));
            var lf = D(2, .22f); lf.drag = .25f; psLeaf = FBFxLib.PS("ImpactoFolhas", deb, hi ? 120 : 50, lf); psShard = FBFxLib.PS("ImpactoCacos", deb, hi ? 160 : 60, D(3, 1.2f));
            psSpark = FBFxLib.PS("ImpactoFaiscas", FBFxLib.Mat("Particles_Add", FBFxLib.Tex("spark"), Color.white, "ImpactoFaisca"), hi ? 260 : 100,
                new FBFxLib.Opt { gravity = .7f, mode = ParticleSystemRenderMode.Stretch, stretch = .035f, color = FBFxLib.Fade(0, .4f, new Color(1, .45f, .15f)) });
            psFlash = FBFxLib.PS("ImpactoClarao", FBFxLib.Mat("Particles_Add", FBFxLib.Tex("spark"), Color.white, "ImpactoClarao"), 30, new FBFxLib.Opt { color = FBFxLib.Fade(0, .2f) });
            var drop = FBFxLib.Mat("Particles_Alpha", FBFxLib.Tex("droplet"), new Color(.85f, .92f, 1, 1), "AguaGota");
            psSplash = FBFxLib.PS("AguaRespingo", drop, hi ? 260 : 100, new FBFxLib.Opt { gravity = 1, mode = ParticleSystemRenderMode.Stretch, stretch = .02f, color = FBFxLib.Fade(0, .7f) });
            psRipple = FBFxLib.PS("AguaAnel", FBFxLib.Mat("Particles_Alpha", FBFxLib.Tex("ripple"), Color.white, "AguaAnel"), 24,
                new FBFxLib.Opt { mode = ParticleSystemRenderMode.HorizontalBillboard, size = FBFxLib.Curve(0, .15f, 1, 1), color = FBFxLib.Fade(.05f, .2f) });
        }

        // ---- classificação do material (cache por colisor / submesh)
        static readonly Regex rxGlass = new(@"vidro|glass|janela|window|vitr", RegexOptions.IgnoreCase), rxVeg = new(@"folha|leaf|leaves|arvore|árvore|tree|palm|palmeira|bush|arbust|planta|plant|hedge|foliage|vegeta|capim", RegexOptions.IgnoreCase),
            // [parceiro 01/11] "rio"/"lago" só no começo de palavra (separador _ espaço . -): "armario", "frio", "vestiario" não são mais água
            rxWater = new(@"agua|água|water|(^|[_\s.\-])(rio|lago)|mar\b|ocean", RegexOptions.IgnoreCase), rxPlaster = new(@"reboco|plaster|pintura|paint|tinta|azulejo|tile|ceram", RegexOptions.IgnoreCase);
        static readonly Dictionary<Collider, Surf> byCollider = new(); static readonly Dictionary<(Collider, int), Surf> bySub = new();
        public static Surf Classify(Collider c, int triangleIndex = -1)
        {
            if (!c) return Surf.Concrete;
            if (c.gameObject.layer == FBWorld.LGlass) return Surf.Glass;
            var fs = c.GetComponent<FBSurface>(); if (fs) return FromKind(fs.kind);
            var mc = c as MeshCollider; var mesh = mc ? mc.sharedMesh : null;
            if (mesh && triangleIndex >= 0 && mesh.subMeshCount > 1 && mesh.isReadable)
            {
                int sub = SubOf(mesh, triangleIndex); var key = (c, sub);
                if (bySub.TryGetValue(key, out var s)) return s;
                var r = c.GetComponent<Renderer>(); var mats = r ? r.sharedMaterials : null;
                s = Name(c.name + " " + (mats != null && sub < mats.Length && mats[sub] ? mats[sub].name : "")); bySub[key] = s; return s;
            }
            if (byCollider.TryGetValue(c, out var sc)) return sc;
            var rr = c.GetComponent<Renderer>(); sc = Name(c.name + " " + (rr && rr.sharedMaterial ? rr.sharedMaterial.name : "")); byCollider[c] = sc; return sc;
        }
        static Surf FromKind(int k) => k switch { 1 => Surf.Dirt, 2 => Surf.Sand, 3 => Surf.Mud, 4 => Surf.Metal, 5 => Surf.Wood, _ => Surf.Concrete };
        static Surf Name(string n)
        {
            if (rxGlass.IsMatch(n)) return Surf.Glass; if (rxVeg.IsMatch(n)) return Surf.Vegetation;
            // [parceiro] fisica 01/11: colisor COL_* do mapa nunca é água (a água não tem colisor); vale a palavra do tipo no fim (terra, concreto...).
            // Também ignora os falsos "agua/rio" do FBWater.NaoEAgua (verde-água, areia do rio, azulejo azul-piscina...).
            if (rxWater.IsMatch(n) && !FBWater.NaoEAgua(n) && !n.StartsWith("COL_", System.StringComparison.OrdinalIgnoreCase)) return Surf.Water;
            int k = FBWorld.SurfOf(n); if (k != 0) return FromKind(k); return rxPlaster.IsMatch(n) ? Surf.Plaster : Surf.Concrete;
        }
        static readonly Dictionary<Mesh, int[]> subStarts = new();
        static int SubOf(Mesh m, int tri)
        {
            if (!subStarts.TryGetValue(m, out var st)) { st = new int[m.subMeshCount]; for (int i = 0; i < st.Length; i++) st[i] = (int)m.GetSubMesh(i).indexStart / 3; subStarts[m] = st; }
            for (int i = st.Length - 1; i >= 0; i--) if (tri >= st[i]) return i; return 0;
        }

        // ---- dedupe (8 posições recentes) — rajada no mesmo ponto não empilha poeira
        static readonly Vector4[] recent = new Vector4[8]; static int recentI;
        static bool Dup(Vector3 p)
        {
            float now = Time.time;
            foreach (var r in recent) if (now - r.w < .1f && (new Vector3(r.x, r.y, r.z) - p).sqrMagnitude < .0625f) return true;
            recent[recentI = (recentI + 1) % recent.Length] = new Vector4(p.x, p.y, p.z, now); return false;
        }
        static float Chance => FBFxQuality.Level == 0 ? .65f : 1;

        /// <summary>Impacto completo de bala (o FBCombat.Impact deve chamar isto com o RaycastHit e a direção da bala).</summary>
        public static void Bullet(RaycastHit h, Vector3 dir)
        {
            var n = h.normal; if (Vector3.Dot(n, dir) > 0) n = -n;
            Surface(h.point, n, h.collider, h.triangleIndex, dir);
        }
        /// <summary>Impacto em p com normal n no colisor c (triangleIndex &lt; 0 = refaz o raio curto para achar o submesh).</summary>
        public static void Surface(Vector3 p, Vector3 n, Collider c, int tri = -1, Vector3? dir = null)
        {
            Warm(); n = n.sqrMagnitude > 1e-6f ? n.normalized : Vector3.up; var d = dir ?? -n;
            if (tri < 0 && c is MeshCollider && c.Raycast(new Ray(p + n * .05f, -n), out var rh, .15f)) tri = rh.triangleIndex;
            var s = Classify(c, tri); var mover = FBBloodFx.Mover(c);
            if (c) Hole(s, p, n, d, mover);
            if (Dup(p) || Random.value > Chance) return;
            float lod = FBFxQuality.Lod(p); if (lod <= 0) return;
            Particles(s, p, n, d, lod);
        }

        static void Hole(Surf s, Vector3 p, Vector3 n, Vector3 d, Transform mover)
        {
            int cell; float size;
            switch (s)
            {
                case Surf.Metal: cell = FBDecalFx.HoleMetal; size = Random.Range(.05f, .075f); break;
                case Surf.Wood: cell = FBDecalFx.HoleWood; size = Random.Range(.07f, .1f); break;
                case Surf.Glass: cell = FBDecalFx.HoleGlass; size = Random.Range(.14f, .22f); break;
                case Surf.Dirt: case Surf.Mud: cell = FBDecalFx.HoleDirt; size = Random.Range(.06f, .1f); break;
                case Surf.Plaster: cell = FBDecalFx.HolePlaster; size = Random.Range(.08f, .12f); break;
                case Surf.Concrete: cell = FBDecalFx.HoleConcrete + Random.Range(0, 2); size = Random.Range(.08f, .13f); break;
                default: return;                                              // areia, água, vegetação: sem marca
            }
            float cos = Mathf.Abs(Vector3.Dot(n, d)); var along = Vector3.ProjectOnPlane(d, n);   // tiro rasante: buraco alongado
            float asp = cos < .5f && along.sqrMagnitude > 1e-4f ? Mathf.Clamp(1 / Mathf.Max(cos, .4f), 1, 1.8f) : 1;
            FBDecalFx.Place(FBDecalFx.Atlas.Holes, cell, p, n, size, asp > 1 ? Vector3.Cross(along.normalized, n) : (Vector3?)null, asp, mover);
        }

        static void Particles(Surf s, Vector3 p, Vector3 n, Vector3 d, float lod)
        {
            var refl = Vector3.Reflect(d, n);                                  // ricochete: parte do material sai espelhado
            var o = p + n * .03f;
            switch (s)
            {
                case Surf.Concrete: case Surf.Plaster:
                    {
                        var col = s == Surf.Plaster ? new Color(.82f, .8f, .76f, .5f) : new Color(.7f, .66f, .58f, .5f);
                        Dust(o, n, col, 3, lod, .12f, .32f); Chips(psChip, o, n, refl, 6, lod, new Color(.62f, .6f, .56f), .012f, .035f, 4);
                        if (Random.value < .3f) Sparks(o, n, refl, 2, lod); break;
                    }
                case Surf.Metal:
                    Sparks(o, n, refl, 12, lod); FBFxLib.Emit(psFlash, o, Vector3.zero, Random.Range(.1f, .18f), .05f, new Color(1, .85f, .6f));
                    Dust(o, n, new Color(.35f, .34f, .33f, .35f), 1, lod, .08f, .18f); break;
                case Surf.Wood:
                    Chips(psSplinter, o, n, refl, 7, lod, new Color(.85f, .75f, .62f), .02f, .05f, 3.5f); Dust(o, n, new Color(.55f, .44f, .32f, .45f), 2, lod, .1f, .25f); break;
                case Surf.Dirt: case Surf.Sand: case Surf.Mud:
                    {
                        var col = s == Surf.Sand ? new Color(.8f, .72f, .56f, .55f) : s == Surf.Mud ? new Color(.27f, .22f, .16f, .45f) : new Color(.47f, .38f, .27f, .55f);
                        int nd = FBFxQuality.Count(s == Surf.Mud ? 1 : 4, lod);
                        for (int i = 0; i < nd; i++) FBFxLib.Emit(psDust, o + Random.insideUnitSphere * .04f, (n * Random.Range(1.2f, 2.8f) + Vector3.up * Random.Range(.5f, 1.6f)) * (1 - i * .12f),
                            Random.Range(.12f, .3f), Random.Range(.7f, 1.3f), FBFxLib.Jit(col, .1f), Random.Range(0, 360f));
                        Chips(psChip, o, n, n, s == Surf.Sand ? 3 : 7, lod, new Color(col.r * .85f, col.g * .85f, col.b * .85f), .012f, .03f, 3.5f); break;
                    }
                case Surf.Glass: Glass(o, n, d, lod); break;
                case Surf.Water: Water(p, 1, lod); break;
                case Surf.Vegetation:
                    Chips(psLeaf, o, n, d, 7, lod, Color.white, .025f, .06f, 2.2f); Dust(o, n, new Color(.36f, .44f, .24f, .3f), 1, lod, .08f, .18f); break;
            }
        }
        static void Dust(Vector3 o, Vector3 n, Color c, float count, float lod, float s0, float s1)
        {
            int k = FBFxQuality.Count(count, lod); if (k == 0 && lod > .2f) k = FBFxQuality.Take(1);
            for (int i = 0; i < k; i++) FBFxLib.Emit(psDust, o + Random.insideUnitSphere * .03f, FBFxLib.Cone(n, .45f) * Random.Range(.35f, 1.1f) + Vector3.up * .15f, Random.Range(s0, s1), Random.Range(.6f, 1.3f), FBFxLib.Jit(c, .08f), Random.Range(0, 360f));
        }
        static void Chips(ParticleSystem ps, Vector3 o, Vector3 n, Vector3 refl, float count, float lod, Color c, float s0, float s1, float speed)
        {
            int k = FBFxQuality.Count(count, lod);
            for (int i = 0; i < k; i++)
                FBFxLib.Emit(ps, o, FBFxLib.Cone(Random.value < .5f ? n : refl, .55f) * Random.Range(.4f, 1f) * speed + Vector3.up * Random.Range(0, 1.2f), Random.Range(s0, s1), Random.Range(.5f, 1.1f), FBFxLib.Jit(c, .12f), Random.Range(0, 360f), Random.Range(-12f, 12f));
        }
        static void Sparks(Vector3 o, Vector3 n, Vector3 refl, float count, float lod)
        {
            int k = FBFxQuality.Count(count, lod);
            for (int i = 0; i < k; i++) FBFxLib.Emit(psSpark, o, FBFxLib.Cone((refl + n) * .5f, .7f) * Random.Range(2.5f, 7f), Random.Range(.012f, .025f), Random.Range(.12f, .38f), new Color(1, Random.Range(.75f, .95f), Random.Range(.4f, .65f)));
        }

        // ---- vidro (FBGlass.Break pode chamar isto) e água (FBFx.Splash)
        public static void Glass(Vector3 p, Vector3 n, Vector3 d, float lod = -1)
        {
            Warm(); if (lod < 0) lod = FBFxQuality.Lod(p); if (lod <= 0) return;
            int k = FBFxQuality.Count(10, lod);
            for (int i = 0; i < k; i++) FBFxLib.Emit(psShard, p + Random.insideUnitSphere * .05f, (FBFxLib.Cone(d, .6f) * Random.Range(1f, 3.5f) + n * Random.Range(.2f, 1.5f)), Random.Range(.012f, .04f), Random.Range(.6f, 1.2f), Color.white, Random.Range(0, 360f), Random.Range(-15f, 15f));
            Dust(p, n, new Color(.88f, .92f, .94f, .25f), 1, lod, .08f, .16f);
        }
        public static void Water(Vector3 p, float k = 1, float lod = -1)
        {
            Warm(); if (lod < 0) lod = FBFxQuality.Lod(p); if (lod <= 0) return;
            int nd = FBFxQuality.Count(16 * k, lod);
            for (int i = 0; i < nd; i++) FBFxLib.Emit(psSplash, p, new Vector3(Random.Range(-.7f, .7f), Random.Range(2f, 4.6f), Random.Range(-.7f, .7f)) * Mathf.Sqrt(k), Random.Range(.01f, .03f) * Mathf.Sqrt(k), Random.Range(.35f, .75f), new Color(.85f, .92f, 1, .7f));
            FBFxLib.Emit(psRipple, p + Vector3.up * .01f, Vector3.zero, Random.Range(.7f, 1.1f) * Mathf.Sqrt(k), Random.Range(.8f, 1.2f), new Color(.9f, .95f, 1, .55f), Random.Range(0, 360f));
            if (k > .7f) FBFxLib.Emit(psDust, p + Vector3.up * .1f, Vector3.up * .6f, .25f * k, .5f, new Color(.92f, .96f, 1, .3f), Random.Range(0, 360f));
        }
    }
}
