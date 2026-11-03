// LASER e LANTERNA das armas (r45 — pedido: "lasers e lanternas só devem funcionar enquanto o jogador estiver mirando"). Antes o traço fixo
// do modelo (SM_Laser_Beam) era escondido e o feixe "de verdade" nunca existiu. Agora:
//  · feixe vermelho fino saindo do próprio acessório (nó do laser escolhido no menu) até onde o tiro vai (jogador: o ponto mirado pela
//    câmera; bot: o alvo dele), com o ponto na superfície atingida;
//  · lanterna (Laser_Flash / Laser_Tactical): cone de luz pelo mesmo eixo, mais forte à noite;
//  · aceso SÓ mirando (ADS do jogador / bot mirando); em 1ª pessoa sai da arma de visão, em 3ª da arma na mão; bots até 3 ao mesmo tempo;
//  · pools fixos (6 feixes, 3 lanternas) criados no carregamento (Warm) — nada de luz nova no meio da partida.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public static class FBLaser
    {
        class E { public FBCharacter owner; public Transform emit; public bool fp, flash; }
        static readonly List<E> all = new(); static readonly List<LineRenderer> beams = new(); static readonly List<Transform> dots = new(); static readonly List<Light> lamps = new();
        static Material beamMat, dotMat; static Runner runner;
        public static int LitBeams, LitLamps;                                                   // laboratório
        public static void Warm()
        {
            if (beams.Count > 0 && beams[0]) return; beams.Clear(); dots.Clear(); lamps.Clear();
            var root = new GameObject("FB_Laser"); Object.DontDestroyOnLoad(root); runner = root.AddComponent<Runner>();
            beamMat = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit), true); beamMat.SetColor("_BaseColor", new Color(1, .08f, .05f, .55f)); beamMat.color = new Color(1, .08f, .05f, .55f);
            var t = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) { float r = new Vector2(x - 15.5f, y - 15.5f).magnitude / 15.5f, a = Mathf.Clamp01(1 - r); t.SetPixel(x, y, new Color(1, 1, 1, a * a * a)); }
            t.Apply(); dotMat = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit), true); dotMat.SetTexture("_BaseMap", t); dotMat.mainTexture = t; dotMat.SetColor("_BaseColor", new Color(1, .15f, .1f, 1)); dotMat.SetFloat("_Cull", 0);
            var quad = new Mesh { name = "ponto_laser" }; quad.SetVertices(new[] { new Vector3(-.5f, -.5f), new Vector3(.5f, -.5f), new Vector3(-.5f, .5f), new Vector3(.5f, .5f) }); quad.SetUVs(0, new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one }); quad.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            for (int i = 0; i < 6; i++)
            {
                var g = new GameObject("feixe_" + i); g.transform.SetParent(root.transform); var lr = g.AddComponent<LineRenderer>(); lr.sharedMaterial = beamMat; lr.positionCount = 2; lr.useWorldSpace = true;
                lr.startWidth = .0035f; lr.endWidth = .006f; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false; lr.numCapVertices = 0; lr.enabled = false; beams.Add(lr);
                var d = new GameObject("ponto_" + i); d.transform.SetParent(root.transform); d.AddComponent<MeshFilter>().sharedMesh = quad; var mr = d.AddComponent<MeshRenderer>(); mr.sharedMaterial = dotMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; d.SetActive(false); dots.Add(d.transform);
            }
            for (int i = 0; i < 3; i++)
            {
                var l = new GameObject("lanterna_" + i).AddComponent<Light>(); l.transform.SetParent(root.transform); l.type = LightType.Spot; l.range = 30; l.spotAngle = 42; l.innerSpotAngle = 16; l.color = new Color(1, .97f, .9f);
                l.shadows = LightShadows.None; l.intensity = 0; l.enabled = false; lamps.Add(l);
            }
        }
        // arma equipada (3ª pessoa: FBCharacter.SetWeapon; 1ª pessoa: FBFpRig.SetWeapon)
        public static void Bind(FBCharacter owner, GameObject weapon, WeaponDef d, bool fp)
        {
            all.RemoveAll(e => !e.owner || (e.owner == owner && e.fp == fp)); if (!owner || !weapon || d == null) return;
            var c = FBWeaponModel.Of(d); var node = Json.S(FBWeaponModel.Att(d, "lasers", c.laser), "node"); if (string.IsNullOrEmpty(node) || node == "None") return;
            Transform n = null; foreach (var t in weapon.GetComponentsInChildren<Transform>(true)) if (t.name == node) { n = t; break; } if (!n) return;
            Transform emit = null; foreach (var t in n.GetComponentsInChildren<Transform>(true)) if (t != n && System.Text.RegularExpressions.Regex.IsMatch(t.name, "SOCKET|Emit|Beam|Lens|Light", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) { emit = t; break; }
            if (!emit)
            {   // sem soquete: ponta da frente do acessório (centro das malhas, empurrado até a borda no sentido do cano)
                var rs = n.GetComponentsInChildren<Renderer>(true); var g = new GameObject("laser_saida").transform; g.SetParent(n, false);
                if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); g.position = b.center; }
                emit = g;
            }
            Warm(); all.Add(new E { owner = owner, emit = emit, fp = fp, flash = node.Contains("Flash") || node.Contains("Tactical") });
        }
        class Runner : MonoBehaviour
        {
            // [parceiro 01/11] cronômetro por script (FBTempo, ligado só pelo teste do .exe); desligado custa só um teste de bool
            static readonly int __fbt_LateUpdate = FBTempo.Id("FBLaser.Runner.LateUpdate");
            void LateUpdate()
            {
                if (!FBTempo.On) { LateUpdate__fbt(); return; }
                long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
            }
            void LateUpdate__fbt() { Tick(); }
        }
        static void Tick()
        {
            int bi = 0, li = 0; var cam = Camera.main; var me = FBMatch.Player; bool fpv = FBPlayer.I && FBPlayer.I.FirstPerson; int bots = 0;
            float night = FBWeather.NightK;
            all.RemoveAll(e => !e.owner || !e.emit);
            foreach (var e in all.OrderBy(e => e.owner == me ? 0 : 1))
            {
                var a = e.owner; bool mine = a == me;
                if (!a.alive || a.knocked || !a.aiming || a.vehiclePose || !e.emit.gameObject.activeInHierarchy) continue;
                if (mine && e.fp != fpv) continue;                                                // 1ª pessoa: sai da arma de visão; 3ª: da arma na mão
                if (!mine && (e.fp || bots >= 3 || !cam || (a.Pos - cam.transform.position).sqrMagnitude > 70 * 70)) continue;
                Vector3 o = e.emit.position, to;
                if (mine && cam) { var co = cam.transform.position; var cf = cam.transform.forward; to = Physics.Raycast(co + cf * .3f, cf, out var ch, 250, FBWorld.SolidMask | (1 << FBWorld.LChar), QueryTriggerInteraction.Ignore) ? ch.point : co + cf * 250; }
                else { var bt = a.GetComponent<FBBot>()?.target; to = bt ? bt.Pos + Vector3.up * 1.25f * bt.ScaleK : o + a.transform.forward * 40; bots++; }
                var dir = to - o; float L = dir.magnitude; if (L < .05f) continue; dir /= L;
                bool hit = Physics.Raycast(o + dir * .05f, dir, out var h, Mathf.Min(L + .5f, 250), FBWorld.SolidMask | (1 << FBWorld.LChar), QueryTriggerInteraction.Ignore) && h.collider.GetComponentInParent<FBCharacter>() != a;
                var end = hit ? h.point : o + dir * Mathf.Min(L, 250);
                if (bi < beams.Count)
                {
                    var lr = beams[bi]; var d = dots[bi]; bi++; lr.enabled = true; lr.SetPosition(0, o); lr.SetPosition(1, end);
                    var col = new Color(1, .08f, .05f, Mathf.Lerp(.22f, .5f, night)); lr.startColor = col; lr.endColor = new Color(col.r, col.g, col.b, col.a * .35f);
                    d.gameObject.SetActive(hit); if (hit && cam) { d.position = h.point + h.normal * .01f; d.rotation = Quaternion.LookRotation(cam.transform.position - d.position); d.localScale = Vector3.one * Mathf.Clamp(Vector3.Distance(cam.transform.position, h.point) * .006f, .02f, .25f); }
                }
                if (e.flash && li < lamps.Count && !(FBQuality.Mobile && !mine))
                {
                    var l = lamps[li++]; l.enabled = true; l.transform.SetPositionAndRotation(o + dir * .05f, Quaternion.LookRotation(dir)); l.intensity = Mathf.Lerp(2.5f, 14, night);
                }
            }
            LitBeams = bi; LitLamps = li;
            for (; bi < beams.Count; bi++) { if (beams[bi] && beams[bi].enabled) beams[bi].enabled = false; if (dots[bi] && dots[bi].gameObject.activeSelf) dots[bi].gameObject.SetActive(false); }
            for (; li < lamps.Count; li++) if (lamps[li] && lamps[li].enabled) { lamps[li].enabled = false; lamps[li].intensity = 0; }
        }
    }
}
