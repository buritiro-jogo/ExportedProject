// Consultas ao mundo (porte do world.js: rayHit/groundY/clearLine/superfície). No web a colisão é um BVH único do mapa; aqui são os
// MeshColliders do mapa (camada "Mapa"), os colisores extras (muros invisíveis, portas de laje) e os veículos (camada "Veiculo").
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public static class FBWorld
    {
        public const int LMap = 8, LHitbox = 9, LChar = 10, LVehicle = 11, LGlass = 12, LTrigger = 13, LFirstPerson = 14;
        public static int SolidMask => (1 << LMap) | (1 << LVehicle) | 1;              // mapa + veículos + Default (props soltos)
        public static int ShotMask => SolidMask | (1 << LHitbox) | (1 << LGlass);
        public static readonly string[] SURF = { "concrete", "dirt", "sand", "mud", "metal", "wood" };
        static readonly (System.Text.RegularExpressions.Regex rx, int k)[] SURF_RX = {
            (new(@"mud|lama|barro|brejo", System.Text.RegularExpressions.RegexOptions.IgnoreCase), 3),
            (new(@"sand|areia|praia", System.Text.RegularExpressions.RegexOptions.IgnoreCase), 2),
            (new(@"dirt|terra|soil|grass|grama|lawn|mato|jardim|canteiro|campo|trilha", System.Text.RegularExpressions.RegexOptions.IgnoreCase), 1),
            (new(@"metal|ferro|steel|chapa|zinco|lata|grade", System.Text.RegularExpressions.RegexOptions.IgnoreCase), 4),
            (new(@"wood|madeira|tabua|deck", System.Text.RegularExpressions.RegexOptions.IgnoreCase), 5) };
        public static int SurfOf(string name) { if (name != null) foreach (var (rx, k) in SURF_RX) if (rx.IsMatch(name)) return k; return 0; }

        // rayHit(o, dir, far, near, raio): o raio > 0 vira SphereCast (os "raios finos" de 0,05–0,15 do web)
        public static bool RayHit(Vector3 o, Vector3 dir, float far, out RaycastHit hit, float radius = 0, float near = 0)
        {
            if (near > 0) { o += dir * near; far -= near; }
            if (far <= 0) { hit = default; return false; }
            return radius > 0 ? Physics.SphereCast(o, radius, dir, out hit, far, SolidMask, QueryTriggerInteraction.Ignore)
                              : Physics.Raycast(o, dir, out hit, far, SolidMask, QueryTriggerInteraction.Ignore);
        }
        public static bool RayHit(Vector3 o, Vector3 dir, float far, float radius = 0) => RayHit(o, dir, far, out _, radius);
        public static float? GroundY(float x, float y, float z, float up = 1, float far = 4)
        {
            var o = new Vector3(x, y + up, z); return Physics.Raycast(o, Vector3.down, out var h, up + far, SolidMask, QueryTriggerInteraction.Ignore) ? h.point.y : (float?)null;
        }
        // linha livre entre dois pés (duas alturas, como o clearLine do main.js)
        public static bool ClearLine(Vector3 a, Vector3 b)
        {
            foreach (var hh in new[] { .6f, 1.5f })
            {
                var o = new Vector3(a.x, a.y + hh, a.z); var t = new Vector3(b.x, b.y + hh, b.z); var d = t - o; float L = d.magnitude; if (L < .01f) continue;
                if (RayHit(o, d / L, L)) return false;
            }
            return true;
        }
        public static int SurfaceAt(RaycastHit h)
        {
            if (!h.collider) return 0; var s = h.collider.GetComponent<FBSurface>(); if (s) return s.kind;
            var r = h.collider.GetComponent<Renderer>(); return SurfOf(h.collider.name + " " + (r && r.sharedMaterial ? r.sharedMaterial.name : ""));
        }
        public static int SurfaceBelow(Vector3 p) => Physics.Raycast(p + Vector3.up * .5f, Vector3.down, out var h, 2, SolidMask, QueryTriggerInteraction.Ignore) ? SurfaceAt(h) : 0;

        // ---- água (level.water = [[x,z]..., [y]] no espaço do web) — tiro/passos/respingo
        public class Water { public List<Vector2> poly = new(); public float y; public Rect box; }
        public static readonly List<Water> WaterList = new();
        public static void SetWater(Dictionary<string, object> level)
        {
            WaterList.Clear(); var l = Json.L(level, "water"); if (l == null) return;
            foreach (var wo in l)
            {
                var w = wo as List<object>; if (w == null || w.Count < 4) continue; var W = new Water { y = Json.Fv((w[w.Count - 1] as List<object>)[0]) };
                float x0 = 1e9f, z0 = 1e9f, x1 = -1e9f, z1 = -1e9f;
                for (int i = 0; i < w.Count - 1; i++) { var p = w[i] as List<object>; var v = new Vector2(-Json.Fv(p[0]), Json.Fv(p[1])); W.poly.Add(v); x0 = Mathf.Min(x0, v.x); x1 = Mathf.Max(x1, v.x); z0 = Mathf.Min(z0, v.y); z1 = Mathf.Max(z1, v.y); }
                W.box = Rect.MinMaxRect(x0, z0, x1, z1); WaterList.Add(W);
            }
        }
        static bool InPoly(List<Vector2> poly, float x, float z)
        {
            bool c = false; for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++) { var a = poly[i]; var b = poly[j]; if ((a.y > z) != (b.y > z) && x < (b.x - a.x) * (z - a.y) / (b.y - a.y) + a.x) c = !c; }
            return c;
        }
        public static float? WaterAt(float x, float z) { foreach (var w in WaterList) { if (!w.box.Contains(new Vector2(x, z))) continue; if (InPoly(w.poly, x, z)) return w.y; } return null; }
        public static bool WaterHit(Vector3 o, Vector3 dir, float far, out Vector3 p)
        {
            p = default; if (WaterList.Count == 0 || Mathf.Abs(dir.y) < 1e-4f) return false;
            foreach (var w in WaterList) { float t = (w.y - o.y) / dir.y; if (t <= 0 || t > far) continue; var q = o + dir * t; if (WaterAt(q.x, q.z) is float y && Mathf.Abs(y - w.y) < .01f) { p = q; return true; } }
            return false;
        }

        // Limites jogáveis reais de cada cenário (evita arena e spawns fora de muros ou em terreno inacessível)
        public static Bounds GetPlayableBounds(string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) mapId = FBSession.map ?? "vila_jardim";
            mapId = mapId.ToLowerInvariant().Replace("map_", "");
            return mapId switch
            {
                "praca_aguas" => new Bounds(new Vector3(-120f, 0, 0), new Vector3(460f, 60f, 96f)), // Muros Z em [-48, 48]
                "vila_jardim" => new Bounds(new Vector3(4f, 0, 19f), new Vector3(300f, 60f, 320f)),
                "praca_pt" => new Bounds(new Vector3(-32f, 0, 14f), new Vector3(790f, 60f, 600f)),
                "orla" => new Bounds(new Vector3(31f, 0, -83f), new Vector3(430f, 60f, 410f)),
                "morro_sacola" => new Bounds(Vector3.zero, new Vector3(612f, 80f, 612f)),   // arena 600 x 600 (paredes em ±300)
                // r68: centro_civico e bv_city caíam no padrão em volta de (0,0,0). O Centro Cívico fica em x -117..467 / z -1337..-753
                // (pontos do fb_data, mesmo centro que o minimapa acha pela NavMesh): sem isto o De Rolê nascia a 1 km do mapa e a loja não achava lugar.
                // BV City = corredor x -175..1095 / y -250..215 do referencial -> Unity (-x, h, +y).
                "centro_civico" => new Bounds(new Vector3(175f, 0, -1045f), new Vector3(612f, 80f, 612f)),
                "bv_city" => new Bounds(new Vector3(-460f, 0, -17.5f), new Vector3(1290f, 80f, 485f)),
                "estande" => new Bounds(new Vector3(2f, 0, 30f), new Vector3(64f, 40f, 70f)),
                "bermuda" => new Bounds(new Vector3(42f, 0, 225f), new Vector3(1550f, 150f, 1520f)),
                _ => new Bounds(Vector3.zero, new Vector3(500f, 60f, 500f))
            };
        }
    }

}
