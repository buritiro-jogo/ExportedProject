// VITRINE GRÁFICA (Agente 5): tira os prints antes/depois do graficos.md sem ninguém jogar.
//   FreeBala.exe -fbgfx [segundos_de_medição=3] [lista_de_prints=todos, ex.: 1,5,7 · "g" = só os de vegetação]
// Para cada print: força mapa + horário/clima (FBSession -> FBWeather, como o menu faz), carrega a cena no modo treino com o jogador
// invulnerável, espera o aquecimento, pula o ANOITECER para o tempo pedido, põe uma câmera própria no ângulo da tabela (achado por raios no
// cenário: fachada, árvore, contra o sol, para a lua, poste, rua comprida), esconde o HUD, mede FPS médio / pior quadro e grava
// <pasta do build>/autoteste/gfx_<n>.png + gfx.csv. Níveis: PC Alto (Ultra do FBQuality), Mobile (Baixo) e Web (emulado: pipeline Mobile +
// flags Web do FBGfx — no PC o FBQuality não escolhe o nível "Web"). No fim devolve clima/qualidade salvos do jogador e fecha o jogo.
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBGfxShowcase : MonoBehaviour
    {
        enum Pose { Fachada, Arvore, Sol, Lua, Poste, Rua, GramaCanteiro, GramaPraca, GramaBorda, GramaCima, GramaPerto, Container, Fixa, Passaros, Urubu }
        enum Nivel { PCAlto, Mobile, Web, PCMedio }
        struct Shot { public string n, map, time, weather; public float duskAt; public Pose pose; public Nivel nivel; public float delay; public Vector3 eye, tgt; public bool plane; }
        static readonly Shot[] Shots =
        {
            new() { n = "1",  map = "vila_jardim", time = "dia",       weather = "limpo", pose = Pose.Fachada },
            new() { n = "2a", map = "vila_jardim", time = "dia",       weather = "limpo", pose = Pose.Arvore },
            new() { n = "2b", map = "vila_jardim", time = "dia",       weather = "limpo", pose = Pose.Arvore, delay = 1 },
            new() { n = "3",  map = "praca_aguas", time = "anoitecer", weather = "limpo", pose = Pose.Sol, duskAt = 40 },
            new() { n = "4",  map = "praca_aguas", time = "anoitecer", weather = "limpo", pose = Pose.Sol, duskAt = 240 },
            new() { n = "5",  map = "praca_pt",    time = "noite",     weather = "limpo", pose = Pose.Lua },
            new() { n = "6",  map = "vila_jardim", time = "noite",     weather = "limpo", pose = Pose.Poste },
            new() { n = "7",  map = "orla",        time = "dia",       weather = "chuva", pose = Pose.Rua },
            new() { n = "8m", map = "vila_jardim", time = "dia",       weather = "limpo", pose = Pose.Fachada, nivel = Nivel.Mobile },
            new() { n = "8w", map = "vila_jardim", time = "dia",       weather = "limpo", pose = Pose.Fachada, nivel = Nivel.Web },
            // rodada 3 — vegetação (rua com canteiro, praça aberta, borda de calçada, de cima) + FPS da praça aberta por nível
            new() { n = "g1", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.GramaCanteiro },
            new() { n = "g2", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.GramaPraca },
            new() { n = "g3", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.GramaBorda },
            new() { n = "g4", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.GramaCima },
            new() { n = "g2m", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.GramaPraca, nivel = Nivel.PCMedio },
            new() { n = "g2b", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.GramaPraca, nivel = Nivel.Mobile },
            new() { n = "g5", map = "vila_jardim", time = "dia", weather = "limpo", pose = Pose.GramaCanteiro },
            new() { n = "g6", map = "vila_jardim", time = "dia", weather = "limpo", pose = Pose.GramaPraca },
            new() { n = "g7", map = "vila_jardim", time = "dia", weather = "limpo", pose = Pose.GramaBorda },
            new() { n = "g8", map = "vila_jardim", time = "dia", weather = "limpo", pose = Pose.GramaCima },
            new() { n = "g6m", map = "vila_jardim", time = "dia", weather = "limpo", pose = Pose.GramaPraca, nivel = Nivel.PCMedio },
            new() { n = "g6b", map = "vila_jardim", time = "dia", weather = "limpo", pose = Pose.GramaPraca, nivel = Nivel.Mobile },
            // conferência do retorno do usuário: recorte ampliado da grama (halo de alfa) e brilho de contêiner/metal
            new() { n = "g9", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.GramaPerto },
            new() { n = "g10", map = "vila_jardim", time = "dia", weather = "limpo", pose = Pose.GramaPerto },
            new() { n = "g11", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.Container },
            new() { n = "g12", map = "vila_jardim", time = "dia", weather = "limpo", pose = Pose.Container },
            // noite com postes acesos (retorno: noite clara demais, postes sem luz)
            new() { n = "n1", map = "praca_aguas", time = "noite", weather = "limpo", pose = Pose.Poste },
            new() { n = "n2", map = "praca_aguas", time = "noite", weather = "limpo", pose = Pose.GramaPraca },
            new() { n = "n3", map = "vila_jardim", time = "noite", weather = "limpo", pose = Pose.Fachada },
            // Praça das Águas — ângulos FIXOS das referências (capturas retrabalhadas do usuário e fotos reais), em metros do script do mapa
            // (tools/build_praca_aguas.py: x ao longo da Av. Ene Garcez para SE, y para o sul, z altura) — antes/depois comparáveis
            new() { n = "p1", map = "praca_aguas", time = "noite", weather = "limpo", pose = Pose.Fixa, eye = new(-4, -23, 1.8f), tgt = new(30, -3, 1.6f) },        // calçada → fontes coloridas (ref. 1)
            new() { n = "p2", map = "praca_aguas", time = "noite", weather = "limpo", pose = Pose.Fixa, eye = new(62, 1, 1.9f), tgt = new(46, 1, 2.5f) },           // letreiro EU AMO BOA VISTA de frente
            new() { n = "p3", map = "praca_aguas", time = "noite", weather = "limpo", pose = Pose.Fixa, eye = new(58, 9, 3.2f), tgt = new(22, 0, 1.5f) },           // fonte em Z
            new() { n = "p4", map = "praca_aguas", time = "noite", weather = "limpo", pose = Pose.Fixa, eye = new(212, -14, 1.7f), tgt = new(240, 2, 1.6f) },      // galpão de restaurantes (ref. 3)
            new() { n = "p5", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.Fixa, eye = new(165, 30, 3), tgt = new(131, 0, 7) },              // Portal do Milênio à tarde (ref. 5)
            new() { n = "p6", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.Fixa, eye = new(-30, 31, 1.4f), tgt = new(10, 8, 3) },            // avenida, letreiro e palmeiras (ref. 6)
            new() { n = "p7", map = "praca_aguas", time = "noite", weather = "limpo", pose = Pose.Fixa, eye = new(-70, 0, 42), tgt = new(30, 0, 0) },           // drone sobre a rosa dos ventos (fotos)
            new() { n = "p8", map = "praca_aguas", time = "noite", weather = "chuva", pose = Pose.Fixa, eye = new(70, 31, 1.7f), tgt = new(130, 29, 1.5f) },     // avenida molhada
            new() { n = "p9", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.Fixa, eye = new(100, 0, 38), tgt = new(150, 0, 0) },             // drone sobre o Portal (foto aérea)
            new() { n = "p10", map = "praca_aguas", time = "anoitecer", weather = "limpo", pose = Pose.Fixa, eye = new(-30, 31, 1.4f), tgt = new(10, 8, 3), duskAt = 200 },  // pôr do sol
            new() { n = "p11", map = "praca_aguas", time = "noite", weather = "limpo", pose = Pose.Fixa, eye = new(-6, -21.5f, 2.4f), tgt = new(2, -22, .2f) },          // calçada sob o poste: sombra de banco/lixeira/floreira
            new() { n = "p12", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.Fixa, eye = new(40, 0, 1.7f), tgt = new(75, 15, 50), plane = true },   // avião com rastro de condensação
            new() { n = "p13", map = "praca_aguas", time = "noite", weather = "limpo", pose = Pose.Fixa, eye = new(30, 12, 2.2f), tgt = new(18, 2, .8f) },          // borda da fonte em Z de perto (brilho)
            new() { n = "p1m", map = "praca_aguas", time = "noite", weather = "limpo", pose = Pose.Fixa, eye = new(-4, -23, 1.8f), tgt = new(30, -3, 1.6f), nivel = Nivel.Mobile },
            new() { n = "p2m", map = "praca_aguas", time = "noite", weather = "limpo", pose = Pose.Fixa, eye = new(62, 1, 1.9f), tgt = new(46, 1, 2.5f), nivel = Nivel.Mobile },
            // rodada 31 (20/10): contêineres novos, sinalização, lixeiras, terminal, galpão, grama final, chuva com poças, pássaros
            new() { n = "e1", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.Fixa, eye = new(72, -37, 1.7f), tgt = new(100, -34, 1.6f) },      // contêineres na pista norte
            new() { n = "e2", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.Fixa, eye = new(98, -37, 1.7f), tgt = new(82, -40, 5.2f) },      // placa azul de travessia no braço
            new() { n = "e3", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.Fixa, eye = new(76, -39, 1.7f), tgt = new(59.8f, -45.5f, 2.6f) }, // esquina R. Cel. Mota: placas de rua + PARE
            new() { n = "e4", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.Fixa, eye = new(-24.2f, -9.6f, 1.45f), tgt = new(-24, -12.7f, .75f) },   // lixeiras de coleta seletiva
            new() { n = "e5", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.Fixa, eye = new(592, 40, 3.6f), tgt = new(598, 15, 3) },           // terminal (sebe)
            new() { n = "e6", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.Fixa, eye = new(222, -15, 1.6f), tgt = new(232, -13, 4.2f) },     // galpão: ventiladores, cardápios, freezer
            new() { n = "e7", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.GramaPraca },
            new() { n = "e8", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.GramaPerto },
            new() { n = "e9", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.GramaCanteiro },
            new() { n = "e10", map = "praca_aguas", time = "dia", weather = "chuva", pose = Pose.Fixa, eye = new(-35, 8, 1.7f), tgt = new(-20, 4, 0) },          // chuva de dia: respingos, poças
            new() { n = "e11", map = "praca_aguas", time = "noite", weather = "chuva", pose = Pose.Fixa, eye = new(70, 31, 1.7f), tgt = new(100, 30, .2f) },     // chuva à noite: poças refletindo
            new() { n = "e12", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.Passaros },
            new() { n = "e13", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.Urubu },
            new() { n = "e14", map = "praca_aguas", time = "noite", weather = "limpo", pose = Pose.Fixa, eye = new(-20, -30, 1.7f), tgt = new(10, -24, 6) },      // névoa volumétrica nos postes
            new() { n = "e7b", map = "praca_aguas", time = "dia", weather = "limpo", pose = Pose.GramaPraca, nivel = Nivel.Mobile },
            new() { n = "e11b", map = "praca_aguas", time = "noite", weather = "chuva", pose = Pose.Fixa, eye = new(70, 31, 1.7f), tgt = new(100, 30, .2f), nivel = Nivel.Mobile },
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbgfx"); if (i < 0) return;
            var g = new GameObject("FB_VitrineGrafica"); DontDestroyOnLoad(g); var t = g.AddComponent<FBGfxShowcase>();
            if (i + 1 < a.Length && float.TryParse(a[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var s)) t.secs = Mathf.Max(.5f, s);
            if (i + 2 < a.Length && !a[i + 2].StartsWith("-")) t.only = new HashSet<string>(a[i + 2].Split(','));
        }
        float secs = 3; HashSet<string> only;

        IEnumerator Start()
        {
            var dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste"); Directory.CreateDirectory(dir);
            var csv = new List<string> { "print;mapa;hora;clima;nivel;fps_medio;pior_quadro_ms;veg_visiveis;veg_total;arquivo" };
            yield return new WaitForSeconds(3);                                                    // menu carregado (FBQuality/FBSession prontos)
            GameData.Load(); FBSession.Load();
            var wx0 = Shots.Select(s => s.map).Distinct().ToDictionary(m => m, FBSession.WeatherFor); var map0 = FBSession.map; var mode0 = FBSession.mode; int q0 = FBQuality.Choice;
            var list = Shots.Where(s => only == null || only.Contains(s.n) || only.Contains(s.n.TrimEnd('a', 'b', 'm', 'w')) || only.Contains("g") && s.n.StartsWith("g") || only.Contains("p") && s.n.StartsWith("p") || only.Contains("e") && s.n.StartsWith("e")).ToList();
            string loaded = null; var poses = new Dictionary<string, (Vector3 p, Quaternion r)>();   // mesmo mapa + mesmo ângulo = mesma câmera (2a/2b, 3/4, 1/8m/8w)
            foreach (var s in list)
            {
                // nível gráfico
                FBGfx.TierOverride = s.nivel == Nivel.Web ? () => FBGfxTier.Web : null;
                FBQuality.Set(s.nivel == Nivel.PCAlto ? 3 : s.nivel == Nivel.PCMedio ? 2 : 0); FBGfx.Refresh();
                // mapa + clima (só recarrega quando muda algo que a partida lê na largada)
                string key = $"{s.map}|{s.time}|{s.weather}|{s.nivel}";
                if (key != loaded)
                {
                    FBSession.SetWeather(s.map, s.time, s.weather); FBSession.map = s.map; FBSession.mode = "treino"; FBSession.online = false; FBSession.Save();
                    Debug.Log($"[FreeBala][gfx] print {s.n}: carregando {s.map} ({s.time}/{s.weather}, {s.nivel})");
                    var op = SceneManager.LoadSceneAsync("Map_" + s.map); if (op == null) { Debug.Log($"[FreeBala][gfx] FALHA: cena Map_{s.map} não existe"); continue; }
                    while (!op.isDone) yield return null; loaded = key;
                    for (float w = 0; w < 5; w += Time.unscaledDeltaTime) yield return null;     // aquecimento (shaders, streaming, céu)
                }
                FBMatch.God = true;
                if (s.duskAt > 0) { FBWeather.SkipDusk(s.duskAt); yield return null; }
                if (s.delay > 0) for (float w = 0; w < s.delay; w += Time.unscaledDeltaTime) yield return null;
                // câmera própria
                var main = Camera.main; var cg = new GameObject("CamVitrine"); var cam = cg.AddComponent<Camera>();
                cam.fieldOfView = 60; cam.nearClipPlane = .05f; cam.farClipPlane = 1400; cam.cullingMask = main ? main.cullingMask : ~(1 << FBWorld.LFirstPerson);
                var ud = cg.AddComponent<UniversalAdditionalCameraData>(); ud.renderPostProcessing = true; ud.antialiasing = FBGfx.F.camAA; ud.antialiasingQuality = AntialiasingQuality.High;
                if (main) { main.enabled = false; main.tag = "Untagged"; } cg.tag = "MainCamera";
                var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 }; cam.targetTexture = rt;   // só esta câmera: sem HUD
                var hidden = FBMatch.Player ? FBMatch.Player.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToList() : new List<Renderer>(); foreach (var r in hidden) r.enabled = false;
                (Vector3 p, Quaternion r) pose;
                if (s.pose == Pose.Fixa) pose = Fixed(s.map, s.eye, s.tgt);
                else if (!poses.TryGetValue(s.map + s.pose, out pose)) poses[s.map + s.pose] = pose = Place(s.pose);
                cg.transform.SetPositionAndRotation(pose.p, pose.r);
                for (int k = 0; k < 10; k++) yield return null;
                if (s.plane) { FBSkyPlanes.Flyover(pose.p, pose.p.y, true); for (float w = 0; w < 28; w += Time.unscaledDeltaTime) yield return null; var tm = FBSkyPlanes.TrailMid(); if (tm.HasValue) cg.transform.rotation = Quaternion.LookRotation((tm.Value - cg.transform.position).normalized); for (int k = 0; k < 5; k++) yield return null; }   // rastro formado; câmera no meio dele
                float t = 0, worst = 0; int frames = 0; while (t < secs) { yield return null; t += Time.unscaledDeltaTime; frames++; worst = Mathf.Max(worst, Time.unscaledDeltaTime); }
                yield return new WaitForEndOfFrame();
                var file = $"gfx_{s.n}.png"; var prev = RenderTexture.active; RenderTexture.active = rt; var shot = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); shot.Apply(); RenderTexture.active = prev; File.WriteAllBytes(Path.Combine(dir, file), shot.EncodeToPNG()); Destroy(shot);
                float fps = frames / Mathf.Max(.01f, t);
                csv.Add(string.Join(";", s.n, s.map, s.time + (s.duskAt > 0 ? $"+{s.duskAt:0}s" : ""), s.weather, s.nivel, fps.ToString("0.0", CultureInfo.InvariantCulture), (worst * 1000).ToString("0.0", CultureInfo.InvariantCulture), FBVegetation.Visible, FBVegetation.Total, file));
                Debug.Log("[FreeBala][gfx][luz] " + FBWeather.Diag());
                Debug.Log($"[FreeBala][gfx] print {s.n} {s.pose} em {pose.p} · {fps:0} FPS (pior {worst * 1000:0} ms) · nível {FBGfx.Tier} · vegetação {FBVegetation.Visible}/{FBVegetation.Total}");
                foreach (var r in hidden) if (r) r.enabled = true;
                cam.targetTexture = null; rt.Release(); Destroy(rt);
                if (main) { main.tag = "MainCamera"; main.enabled = true; } Destroy(cg); yield return null;
                File.WriteAllLines(Path.Combine(dir, "gfx.csv"), csv);
            }
            // devolve o que era do jogador
            foreach (var kv in wx0) FBSession.SetWeather(kv.Key, kv.Value.time, kv.Value.weather);
            FBSession.map = map0; FBSession.mode = mode0; FBSession.Save(); FBGfx.TierOverride = null; FBQuality.Set(q0);
            Debug.Log("[FreeBala][gfx] fim"); Application.Quit(0);
        }

        // ---------- ângulos (raios no cenário a partir do jogador)
        static Vector3 Origin() => (FBMatch.Player ? FBMatch.Player.Pos : Vector3.zero) + Vector3.up * 1.7f;
        const int MapMask = 1 << FBWorld.LMap;                                                  // só o cenário (nada de hitbox/personagem)
        static bool Ray(Vector3 o, Vector3 d, float far, out RaycastHit h) => Physics.Raycast(o, d, out h, far, MapMask, QueryTriggerInteraction.Ignore);
        static Vector3 Ground(Vector3 p) => Physics.Raycast(p + Vector3.up * 40, Vector3.down, out var h, 120, MapMask, QueryTriggerInteraction.Ignore) ? h.point : p;
        // o chão atingido é gramado? (mesmo critério de nome do FBVegBake)
        static bool IsLawn(RaycastHit h) { var r = h.collider.GetComponent<Renderer>(); var n = r && r.sharedMaterial ? r.sharedMaterial.name : ""; return System.Text.RegularExpressions.Regex.IsMatch(n, "grass|grama|lawn|gramado|canteiro|jardim", System.Text.RegularExpressions.RegexOptions.IgnoreCase); }
        static Vector3 Yaw(float deg) => new Vector3(Mathf.Sin(deg * Mathf.Deg2Rad), 0, Mathf.Cos(deg * Mathf.Deg2Rad));
        static Quaternion Look(Vector3 flat, float pitch) => Quaternion.LookRotation(Quaternion.AngleAxis(-pitch, Vector3.Cross(Vector3.up, flat.normalized)) * flat.normalized);
        static Light Sun() => RenderSettings.sun ? RenderSettings.sun : FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);

        // metros do script do mapa (Blender, ampliado 1,2x no fim) -> glTF (x, z, y) -> Unity (x espelhado) no espaço da raiz Map_<id>
        static (Vector3 p, Quaternion r) Fixed(string map, Vector3 eye, Vector3 tgt)
        {
            var root = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Map_" + map);
            Vector3 W(Vector3 v) { var l = new Vector3(-1.2f * v.x, 1.2f * v.z, 1.2f * v.y); return root ? root.transform.TransformPoint(l) : l; }
            var e = W(eye); var t = W(tgt);                                                // altura absoluta (o chão da praça é plano; raio de cima pegava telhado)
            return (e, Quaternion.LookRotation((t - e).normalized));
        }
        static (Vector3 p, Quaternion r) Place(Pose pose)
        {
            var o = Origin();
            switch (pose)
            {
                case Pose.Fachada:                                                                  // prédio de frente a ~18 m, ~30 % de céu
                    {
                        float best = 1e9f; Vector3 bd = Vector3.forward;
                        for (int a = 0; a < 360; a += 15) { var d = Yaw(a); if (Ray(o, d, 60, out var h) && Mathf.Abs(h.normal.y) < .3f && h.distance > 9) { float sc = Mathf.Abs(h.distance - 18); if (sc < best) { best = sc; bd = d; } } }
                        return (o, Look(bd, 9));
                    }
                case Pose.Arvore:
                    {
                        var tree = FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.StartsWith("VJ_Arvore") || t.name.Contains("Arvore")).OrderBy(t => (t.position - o).sqrMagnitude).FirstOrDefault();
                        if (!tree) return (o, Look(Vector3.forward, 10));
                        var away = o - tree.position; away.y = 0; if (away.sqrMagnitude < .01f) away = Vector3.back;
                        var p = Ground(tree.position + away.normalized * 7) + Vector3.up * 1.7f; var tgt = tree.position + Vector3.up * 3.5f;
                        return (p, Quaternion.LookRotation(tgt - p));
                    }
                case Pose.Sol:
                case Pose.Lua:                                                                      // virado para o astro, com vista livre
                    {
                        var L = Sun(); var to = L ? -L.transform.forward : Vector3.forward; var flat = new Vector3(to.x, 0, to.z); if (flat.sqrMagnitude < 1e-4f) flat = Vector3.forward;
                        float pitch = pose == Pose.Sol ? 10 : Mathf.Clamp(Mathf.Asin(Mathf.Clamp(to.y, -1, 1)) * Mathf.Rad2Deg, 20, 50);
                        var rot = Look(flat, pitch); var dir = rot * Vector3.forward;
                        for (float r = 0; r <= 48; r += 8)
                            for (int a = 0; a < 360; a += r == 0 ? 360 : 45)
                            {
                                var p = Ground(o + Yaw(a) * r) + Vector3.up * 1.7f;
                                if (!Ray(p, dir, 90, out _) && !Ray(p, Vector3.up, 2.5f, out _)) return (p, rot);
                            }
                        return (o, rot);
                    }
                case Pose.Poste:
                    {
                        var lamps = FBWeather.Lamps; if (lamps == null || lamps.Count == 0) return (o, Look(Vector3.forward, 5));
                        var lp = lamps.OrderBy(l => (l - o).sqrMagnitude).First(); var away = o - lp; away.y = 0; if (away.sqrMagnitude < .01f) away = Vector3.back;
                        var p = Ground(lp + away.normalized * 4.5f) + Vector3.up * 1.7f; var tgt = lp + Vector3.down * 1.4f;
                        return (p, Quaternion.LookRotation(tgt - p));
                    }
                case Pose.GramaPerto:                                                               // recorte ampliado: 0,9 m do tufo, 35 cm de altura
                    {
                        var vd = FBVegetation.I ? FBVegetation.I.data : null; var spots = vd ? vd.openSpots.Concat(vd.edgeSpots).ToList() : null;
                        if (spots == null || spots.Count == 0) return (o, Look(Vector3.forward, -20));
                        var sp = spots.OrderBy(q => (q - o).sqrMagnitude).First(); var f = Yaw(20);
                        var cp = sp - f * .9f + Vector3.up * .35f; return (cp, Quaternion.LookRotation(sp + Vector3.up * .12f + f * .3f - cp));
                    }
                case Pose.Container:                                                                // contêiner/metal mais perto, 6 m, 3/4
                    {
                        var r = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(x => x.sharedMaterials.Any(m => m && System.Text.RegularExpressions.Regex.IsMatch(m.name, "Ft_Generic|Ft_Var|container|conteiner|galpao_metal|chapa", System.Text.RegularExpressions.RegexOptions.IgnoreCase)))
                            .OrderBy(x => (x.bounds.center - o).sqrMagnitude).FirstOrDefault();
                        if (!r) return (o, Look(Vector3.forward, 0));
                        var c = r.bounds.center; var dir = (o - c); dir.y = 0; dir = dir.sqrMagnitude > .01f ? dir.normalized : Vector3.back; dir = Quaternion.Euler(0, 35, 0) * dir;
                        var cp = Ground(c + dir * (r.bounds.extents.magnitude + 5)) + Vector3.up * 1.7f; return (cp, Quaternion.LookRotation(c - cp));
                    }
                case Pose.Passaros:                                                                 // bando de pombos no chão mais perto, a 6 m, olho baixo
                    {
                        var fc = FBBirds.I ? FBBirds.I.GroundFlock(o) : null; if (fc == null) return (o, Look(Vector3.forward, -5));
                        var away = o - fc.Value; away.y = 0; away = away.sqrMagnitude > .01f ? away.normalized : Vector3.back;
                        var cp = Ground(fc.Value + away * 6.5f) + Vector3.up * 1.1f; return (cp, Quaternion.LookRotation(fc.Value + Vector3.up * .2f - cp));
                    }
                case Pose.Urubu:                                                                    // drone perto de um urubu planando
                    {
                        var u = FBBirds.I ? FBBirds.I.VulturePos() : null; if (u == null) return (o, Look(Vector3.forward, 40));
                        var cp = u.Value + new Vector3(-9, 2.5f, -9); return (cp, Quaternion.LookRotation(u.Value - cp));
                    }
                case Pose.GramaCanteiro:                                                            // da rua, olhando o canteiro na altura do olho
                case Pose.GramaBorda:                                                               // beira de calçada, câmera baixa
                case Pose.GramaPraca:                                                               // meio do gramado aberto
                case Pose.GramaCima:                                                                // de cima (14 m), 60° para baixo
                    {
                        var vd = FBVegetation.I ? FBVegetation.I.data : null; bool edge = pose == Pose.GramaCanteiro || pose == Pose.GramaBorda;
                        var spots = vd ? (edge ? vd.edgeSpots : vd.openSpots) : null; if (spots == null || spots.Count == 0) return (o, Look(Vector3.forward, -10));
                        var sp = spots.OrderBy(q => (q - o).sqrMagnitude).First();
                        if (pose == Pose.GramaCima) { var f = Yaw(35); return (sp + Vector3.up * 14 - f * 8, Look(f, -60)); }
                        if (pose == Pose.GramaPraca)
                        {
                            float best = -1; Vector3 bd = Vector3.forward;
                            for (int a = 0; a < 360; a += 20) { var d = Yaw(a); float dist = Ray(sp + Vector3.up * 1.7f, d, 200, out var h) ? h.distance : 200; if (dist > best) { best = dist; bd = d; } }
                            return (sp + Vector3.up * 1.7f, Look(bd, -12));
                        }
                        // borda: o lado onde o chão deixa de ser gramado (rua/calçada) recebe a câmera
                        Vector3 outDir = Vector3.forward;
                        for (int a = 0; a < 360; a += 45) { var d = Yaw(a); if (Physics.Raycast(sp + d * 1.2f + Vector3.up * 5, Vector3.down, out var hh, 10, MapMask, QueryTriggerInteraction.Ignore) && !IsLawn(hh)) { outDir = d; break; } }
                        float back = pose == Pose.GramaCanteiro ? 4.5f : 2.2f, hgt = pose == Pose.GramaCanteiro ? 1.7f : .9f;
                        var cp = Ground(sp + outDir * back) + Vector3.up * hgt; return (cp, Quaternion.LookRotation(sp + Vector3.up * .25f - cp));
                    }
                default:                                                                            // rua: a direção mais comprida e livre
                    {
                        float best = -1; Vector3 bd = Vector3.forward;
                        for (int a = 0; a < 360; a += 10) { var d = Yaw(a); float dist = Ray(o, d, 300, out var h) ? h.distance : 300; if (dist > best) { best = dist; bd = d; } }
                        return (o, Look(bd, -3));
                    }
            }
        }
    }
}
