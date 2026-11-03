// Cenário vivo da partida (porte do spawnAmbientScenery/updateAmbient* do main.js + heli.js) e o KILLSTREAK do Complexo:
//  · helicóptero do IE (Bell UH-1N) orbitando o mapa a 48 m com farol de busca varrendo o centro; hélices girando no eixo real do cubo
//    (principal = Y do pai, cauda = X) com disco de borrão que aparece com a rotação; som 3D do rotor.
//  · Caveirão estacionado na base da polícia (Vila Jardim: o do BOPE na vaga gravada; outros mapas: o do IE) com giroflex vermelho/azul
//    e caixa de colisão (as balas param nele).
//  · KILLSTREAK (CF: HeliGunnerDirector / CaveiraoGunnerDirector): 10 abates = atirador do Caveirão, 15 = do helicóptero (G chama;
//    segurar G 0,6 s sai). O circuito do heli fecha sobre o mapa a 14 m/s; tiros inimigos acertam o veículo (3–8 % / 2–6 % × dmgMul); heli
//    abatido cai girando 200°/s e explode, volta em 45 s. Só no clássico e no todos × todos offline (como no web).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public class FBAmbientScenery : MonoBehaviour
    {
        public static FBAmbientScenery I;
        class Rotor { public List<(Transform n, Quaternion q0)> nodes = new(); public Vector3 axis; public float a; public Renderer disc; public Material mat; }
        GameObject heli, cav; Rotor main, tail; float rpm = 1, angle, r, alt, R0 = 120, A0 = 48, t, fxT = 15; Vector3 center; Light search, red, blue; bool crash; float crashVy; float strobe; Bounds heliBox;
        public bool HeliUp => heli && !crash; public bool CavUp => cav && Time.time > cavDeadUntil; float cavDeadUntil;
        public Transform Heli => heli ? heli.transform : null; public Transform Cav => cav ? cav.transform : null;

        public static void Spawn(FBMatch G) { var go = new GameObject("FB_CenarioVivo"); I = go.AddComponent<FBAmbientScenery>(); I.Build(G); }
        void Build(FBMatch G)
        {
            var m = G.Map; var all = m.spawnA.Concat(m.spawnB).Concat(m.points).ToList(); center = all.Count > 0 ? all.Aggregate(Vector3.zero, (a, b) => a + b) / all.Count : Vector3.zero; center.y = 0;
            R0 = 100 + Mathf.Min(45, all.Count * 2); r = R0; alt = A0; angle = Random.value * Mathf.PI * 2;
            SpawnHeli(); if (G.mapId != "estande") SpawnCaveirao(G);   // estande de tiro: sem caveirão estacionado (caía na linha de tiro — pedido 22/10)
        }
        void SpawnHeli()
        {
            var prefab = FBRegistry.I.Vehicle("heli"); if (!prefab) return; crash = false; crashVy = 0;
            heli = Instantiate(prefab); heli.name = "Helicoptero"; foreach (var c in heli.GetComponentsInChildren<Collider>()) Destroy(c);
            // caixa da FUSELAGEM (rodada 37, heli RR): antes entrava o disco das pás (11–14 m) e o assento do atirador caía fora da aeronave
            heliBox = new Bounds(heli.transform.position, Vector3.zero); bool any = false;
            foreach (var rd in heli.GetComponentsInChildren<Renderer>())
            {
                bool rotor = false; for (var t = rd.transform; t && t != heli.transform; t = t.parent) if (t.name.StartsWith("Helices") || t.name.StartsWith("Rotor")) { rotor = true; break; }
                if (rotor) continue; if (!any) { heliBox = rd.bounds; any = true; } else heliBox.Encapsulate(rd.bounds);
            }
            heliBox.center -= heli.transform.position;
            main = MakeRotor(new[] { "Helices", "Rotor" }, true); tail = MakeRotor(new[] { "Helices-02", "Rotor-02" }, false);
            var lg = new GameObject("Farol"); lg.transform.SetParent(heli.transform, false); lg.transform.localPosition = new Vector3(0, 1.2f, 3.5f); search = lg.AddComponent<Light>(); search.type = LightType.Spot; search.range = 140; search.spotAngle = 45; search.intensity = 30; search.color = new Color(1, .93f, .87f); search.shadows = LightShadows.None;
        }
        Rotor MakeRotor(string[] names, bool mainR)
        {
            var nodes = heli.GetComponentsInChildren<Transform>(true).Where(t => names.Contains(t.name)).ToList(); if (nodes.Count == 0) return null;
            var R = new Rotor { axis = mainR ? Vector3.up : Vector3.right }; foreach (var n in nodes) R.nodes.Add((n, n.localRotation));
            var blade = nodes[0]; var b = new Bounds(blade.position, Vector3.zero); foreach (var rd in blade.GetComponentsInChildren<Renderer>()) b.Encapsulate(rd.bounds);
            float rad = mainR ? Mathf.Max(b.size.x, b.size.z) / 2 : Mathf.Max(b.size.y, b.size.z) / 2;
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(disc.GetComponent<Collider>()); disc.name = "DiscoBorrao"; disc.transform.SetParent(blade.parent, true); disc.transform.position = b.center;
            disc.transform.rotation = blade.parent.rotation * (mainR ? Quaternion.identity : Quaternion.Euler(0, 0, 90)); disc.transform.localScale = Vector3.one; var ls = disc.transform.lossyScale; disc.transform.localScale = new Vector3(rad * 2 / ls.x, .002f / ls.y, rad * 2 / ls.z);
            R.mat = new Material(FBShaders.Unlit); R.mat.SetColor("_BaseColor", new Color(.15f, .15f, .15f, 0)); R.mat.SetFloat("_Surface", 1); R.mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); R.mat.SetOverrideTag("RenderType", "Transparent"); if (R.mat.renderQueue < 3000) R.mat.renderQueue = 3000; R.mat.SetInt("_SrcBlend", 5); R.mat.SetInt("_DstBlend", 10); R.mat.SetFloat("_ZWrite", 0); R.mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); R.mat.renderQueue = 3050;
            R.disc = disc.GetComponent<Renderer>(); R.disc.sharedMaterial = R.mat; R.disc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return R;
        }
        void Spin(Rotor X, float w, float dt)
        {
            if (X == null) return; X.a = (X.a + w * rpm * dt) % (Mathf.PI * 2);
            foreach (var (n, q0) in X.nodes) if (n) n.localRotation = Quaternion.AngleAxis(X.a * Mathf.Rad2Deg, X.axis) * q0;
            float o = Mathf.Clamp01((rpm - .3f) / .6f) * .55f; X.mat.SetColor("_BaseColor", new Color(.15f, .15f, .15f, o)); X.disc.enabled = o > .02f;
        }
        void SpawnCaveirao(FBMatch G)
        {
            bool bope = G.mapId == "vila_jardim" && FBRegistry.I.Vehicle("caveirao_bope"); var prefab = bope ? FBRegistry.I.Vehicle("caveirao_bope") : FBRegistry.I.Vehicle("caveirao"); if (!prefab) return;
            cav = Instantiate(prefab); cav.name = "Caveirao"; foreach (var c in cav.GetComponentsInChildren<Collider>()) Destroy(c);
            Vector3 pos; float rotY = 0; var cv = Json.O(G.Level, "caveirao");
            if (bope && cv != null) { pos = Coords.FromWeb(Json.L(cv, "p")); rotY = Coords.YawFromWeb(Json.F(cv, "yaw")); cav.transform.localScale = Vector3.one * 1.2f; }
            else if (G.Map.spawnA.Count > 0) { pos = G.Map.spawnA[0] + new Vector3(6.5f, 0, -4.5f); rotY = -60; }
            else pos = G.Map.waypoints.Count > 0 ? G.Map.waypoints[0] : Vector3.zero;
            if (FBWorld.GroundY(pos.x, pos.y + 2, pos.z, .3f, 5) is float gy) pos.y = gy; cav.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, rotY + (bope ? -90 : 180), 0));
            var b = new Bounds(cav.transform.position, Vector3.zero); foreach (var rd in cav.GetComponentsInChildren<Renderer>()) b.Encapsulate(rd.bounds);
            var col = cav.AddComponent<BoxCollider>(); col.center = cav.transform.InverseTransformPoint(b.center); var sz = cav.transform.InverseTransformVector(b.size); col.size = new Vector3(Mathf.Abs(sz.x), Mathf.Abs(sz.y), Mathf.Abs(sz.z)) * .96f; cav.layer = FBWorld.LVehicle;
            float ly = b.max.y - cav.transform.position.y; red = Giro(new Vector3(.45f, ly, 0), new Color(1, .07f, 0)); blue = Giro(new Vector3(-.45f, ly, 0), new Color(.07f, .4f, 1));
        }
        Light Giro(Vector3 local, Color c) { var g = new GameObject("Giroflex"); g.transform.SetParent(cav.transform, false); g.transform.position = cav.transform.position + cav.transform.rotation * local; var l = g.AddComponent<Light>(); l.type = LightType.Point; l.range = 24; l.color = c; l.shadows = LightShadows.None; return l; }

        // [parceiro] v1 (thread de física): tempo do Update medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_Update = FBTempo.Id("FBAmbientScenery.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            float dt = Time.deltaTime; t += dt;
            if (cav) { strobe += dt * 12; bool on = Mathf.Sin(strobe) > 0; if (red) red.intensity = on ? 3.5f : .1f; if (blue) blue.intensity = on ? .1f : 3.5f; }
            if (!heli)
            {
                FBHeliRecon.SetScanState(false, Vector3.zero);
                return;
            }
            Spin(main, 34, dt); Spin(tail, 52, dt);
            if (crash)
            {
                FBHeliRecon.SetScanState(false, Vector3.zero);
                rpm += (.55f - rpm) * (1 - Mathf.Exp(-dt * 2)); crashVy = Mathf.Min(24, crashVy + 11 * dt); heli.transform.position += Vector3.down * crashVy * dt; heli.transform.Rotate(0, 200 * dt, 0, Space.World);
                var hsC = FBHeliSound.Attach(heli, 1f); if (hsC) hsC.rpm = rpm;
                var gyc = FBWorld.GroundY(heli.transform.position.x, heli.transform.position.y + 1, heli.transform.position.z, .3f, 4);
                if (gyc != null || heli.transform.position.y < -20)
                {
                    var p = heli.transform.position; FBKillstreak2.Boom(p); if (FBKillstreak2.gunner != null && FBKillstreak2.gunner.kind == "heli") FBKillstreak2.GunnerDie();
                    FBAudio.StopLoop("ambient_heli"); Destroy(heli); heli = null; FBMatch.I?.Invoke2(45, () => { if (this && !heli) SpawnHeli(); });
                }
                return;
            }
            rpm += (1 - rpm) * (1 - Mathf.Exp(-dt * 2));
            bool run = FBKillstreak2.gunner != null && FBKillstreak2.gunner.kind == "heli"; float kR = 1 - Mathf.Exp(-dt * .6f);
            r += ((run ? 46 : R0) - r) * kR; alt += ((run ? 30 : A0) - alt) * kR; angle += (run ? 14 / r : .16f) * dt;
            float a2 = alt + Mathf.Sin(t * .4f) * (run ? 1.2f : 3.2f);
            heli.transform.position = new Vector3(center.x + Mathf.Cos(angle) * r, a2, center.z + Mathf.Sin(angle) * r);
            var tangent = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle)); heli.transform.rotation = Quaternion.LookRotation(tangent) * Quaternion.Euler(3, 0, 10);
            var hsA = FBHeliSound.Attach(heli, .9f); if (hsA) hsA.rpm = rpm;
            if ((fxT -= dt) <= 0) { fxT = Random.Range(18f, 30f); FBAudio.PlayAt("ie_heli_fx", heli.transform.position, .35f, 60, .08f, 0, .4f); }
            if (search) search.transform.rotation = Quaternion.LookRotation(center + new Vector3(Mathf.Sin(t * .5f) * 16, 2, Mathf.Cos(t * .5f) * 16) - search.transform.position);

            // Reconhecimento aéreo (danger zone de radar): varre o solo sob o helicóptero e revela alvos por 60s
            Vector3 scanGround = new Vector3(heli.transform.position.x, 0, heli.transform.position.z);
            if (FBWorld.GroundY(scanGround.x, heli.transform.position.y, scanGround.z, .3f, 60) is float gy) scanGround.y = gy;
            FBHeliRecon.SetScanState(true, scanGround);
            float rScanSq = FBHeliRecon.ScanRadius * FBHeliRecon.ScanRadius;
            foreach (var ch in FBMatch.Actors())
            {
                if (ch && ch.alive)
                {
                    float dx = ch.Pos.x - scanGround.x;
                    float dz = ch.Pos.z - scanGround.z;
                    if (dx * dx + dz * dz <= rScanSq)
                    {
                        FBHeliRecon.Tag(ch, 60f);
                    }
                }
            }
        }
        public void ShootDown() { crash = true; }
        public void KillCav() { cavDeadUntil = Time.time + FBKillstreak.STREAK["caveirao"].run; }
        public Vector3 GunnerSeat(string kind)
        {
            if (kind == "heli" && heli) return heli.transform.TransformPoint(heliBox.min.x + .55f, heliBox.min.y + .45f, heliBox.max.z - 2.6f) ;
            if (cav) { var b = cav.GetComponent<BoxCollider>(); return cav.transform.TransformPoint(b.center + new Vector3(0, b.size.y / 2 - .2f, .3f)); }
            return Vector3.zero;
        }
        void OnDestroy() { FBHeliRecon.Clear(); FBAudio.StopLoop("ambient_heli"); if (heli) Destroy(heli); if (cav) Destroy(cav); }
    }

    // atirador do killstreak (o jogador vira o atirador do veículo que já está no mapa; tiros inimigos acertam o veículo)
    public static class FBKillstreak2
    {
        public class G { public string kind; public float t, total, hp = 1, exitHold; public bool armed, dead; public Vector3 back; public float backYaw; public FBCharacter lastSrc; }
        public static G gunner; static readonly Dictionary<string, Vector2> GUN_VEH = new() { ["heli"] = new Vector2(.03f, .08f), ["caveirao"] = new Vector2(.02f, .06f) };
        public static bool On => (FBSession.mode == "classico" || FBSession.mode == "ffa") && !FBSession.online;
        public static void Call()
        {
            var m = FBMatch.I; var p = FBMatch.Player; if (!On || gunner != null || !m || !m.ks.Ready(Time.time) || !p || !p.alive || p.knocked || FBVehicles.Driving(p) || m.state != "play") return;
            var kind = m.ks.reward; var A = FBAmbientScenery.I; bool up = A && (kind == "heli" ? A.HeliUp : A.CavUp);
            if (!up) { FBHUD.I?.Center(FBKillstreak.STREAK[kind].name + " INDISPONÍVEL AGORA", 1.6f); FBAudio.Synth("deny"); return; }
            m.ks.Consume(Time.time); gunner = new G { kind = kind, total = FBKillstreak.STREAK[kind].run, back = p.Pos, backYaw = p.Yaw };
            FBAudio.Synth("go"); FBHUD.I?.Center("ATIRADOR · " + FBKillstreak.STREAK[kind].name, 1.6f);
        }
        public static void Hit(FBCharacter src)
        {
            var g = gunner; if (g == null || g.dead) return; var r = GUN_VEH[g.kind]; g.hp -= Random.Range(r.x, r.y) * FBKillstreak.STREAK[g.kind].dmgMul; if (src) g.lastSrc = src;
            FBHUD.I?.Damaged(.1f); FBAudio.Play2D("thud", .35f, 0, Random.Range(1.3f, 1.6f)); if (g.hp > 0) return; g.hp = 0; g.dead = true;
            if (g.kind == "heli") { FBAmbientScenery.I?.ShootDown(); FBHUD.I?.Center("HELICÓPTERO ABATIDO!", 2.5f); }
            else { var p = FBAmbientScenery.I.Cav.position + Vector3.up * 1.5f; FBAmbientScenery.I.KillCav(); Boom(p); GunnerDie(); }
        }
        public static void Boom(Vector3 p)
        {
            FBFx.Explosion(p, 4); FBAudio.PlayAt("grenade", p, 1, 20);
            foreach (var a in FBMatch.All.ToList()) if (a && a.alive && !a.isPlayer && (a.Pos - p).magnitude < 7) FBCombat.DamageActor(a, 120 * (1 - (a.Pos - p).magnitude / 7), false, null, "zone");
        }
        public static void GunnerDie() { var src = gunner?.lastSrc; End(false); if (FBMatch.Player) FBCombat.DamageActor(FBMatch.Player, 999, false, src, "gunner"); }
        public static void End(bool back)
        {
            var g = gunner; if (g == null) return; gunner = null; var p = FBMatch.Player;
            if (back && p && p.alive) FBMatch.Teleport(p, g.back, g.backYaw); var cc = p ? p.GetComponent<CharacterController>() : null; if (cc) cc.enabled = true;
        }
        // por quadro: segura o jogador no assento, conta o tempo, segurar G sai
        public static bool Tick(FBCharacter p, float dt, bool gHeld)
        {
            var g = gunner; if (g == null) return false; g.t += dt; if (!gHeld) g.armed = true; g.exitHold = g.armed && gHeld && !g.dead ? g.exitHold + dt : 0;
            var seat = FBAmbientScenery.I ? FBAmbientScenery.I.GunnerSeat(g.kind) : p.Pos; var cc = p.GetComponent<CharacterController>(); if (cc) cc.enabled = false; p.transform.position = seat; p.velocity = Vector3.zero;
            FBHUD.I?.SetBox($"{FBKillstreak.STREAK[g.kind].name} · {Mathf.CeilToInt(g.total - g.t)}s · BLINDAGEM {Mathf.RoundToInt(g.hp * 100)}% · SEGURE G PARA SAIR", true);
            if (!g.dead && (g.t >= g.total || g.exitHold >= FBKillstreak.HoldToExit)) { End(true); FBHUD.I?.SetBox("", false); }
            return true;
        }
    }
}
