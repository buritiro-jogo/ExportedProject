// OFICINA MECÂNICA do De Rolê (porte do oficina.js + Vehicles.tune do vehicles.js; REB 3.9.71 — docs/re/REB_OFICINA.md):
//  · abas CARRO (cor, metálico, brilho, vidro e fumê, farol, neon), RODAS (os aros do wheels.json, tamanho, cor do aro), SUSPENSÃO
//    (SuspensaoAr: altura, posição da roda e cambagem na frente e atrás), MOTOR (os 8 roncos, turbo, tração FWD/RWD/4x4, segurar = acelera
//    em ponto morto como o TestarRoncoCarro), SOM (OficinaCriarSom: 20 slots, 6 tipos de módulo, mover/girar/crescer, cores da caixa /
//    alto-falante / corneta), SKIN e PLACA (Mercosul);
//  · o carro muda ao vivo, câmera orbital (arrastar / roda do mouse), configuração salva por carro (PlayerPrefs, como o localStorage do web).
// Diferença: no web a oficina é um galpão montado num prédio do mapa; aqui ela abre com O perto/dentro do seu carro no De Rolê.
// Rodada 35 (20/10/2026): organizada como a do REB (abas INÍCIO · CARRO · RODA · SOM · SKIN com os ícones originais) e com as peças de
// CADA carro do REB (FBReb: bodykit por zona, bandeiras, forros, engate, os 37 roncos com o de fábrica marcado, tração e pneus de fábrica).
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace FreeBala
{
    [Serializable] public class FBSomSlot { public string m; public Vector3 p; public float r, s = 1; }
    [Serializable]
    public class FBTuneData
    {
        public bool hasPaint, hasVidro, hasFarol, hasRodaCor, neon, turbo, hasCaixa, hasFalante, hasCorneta;
        public Color paint = Color.white, vidro = new(.043f, .055f, .07f), farol = Color.white, neonC = new(0, .9f, 1), rodaCor = Color.white, caixa, falante, corneta;
        public float metal = .3f, brilho = .55f, fume = .62f, wsize = 1, fh, foff, fcam, rh, roff, rcam;
        public string skin = "", wheel = "", ronco = "", tracao = "", placa = ""; public List<FBSomSlot> som = new(); public bool somSet;
        // peças do REB (FBReb): variante de bodykit por zona (-1 = de fábrica), forros frente/trás, engate, bandeiras e qual, tamanho de pneu (13 do REB)
        public int[] bk = { -1, -1, -1, -1, -1 }; public int forroF, forroT, bandeira, pneu = -1; public bool engate, bandL, bandR;
    }

    public static class FBTune
    {
        const string KEY = "fb_tune_v1_";
        public static FBTuneData Load(string id) { var s = PlayerPrefs.GetString(KEY + id, ""); if (string.IsNullOrEmpty(s)) return null; try { return JsonUtility.FromJson<FBTuneData>(s); } catch { return null; } }
        public static void Save(string id, FBTuneData T) { PlayerPrefs.SetString(KEY + id, JsonUtility.ToJson(T)); PlayerPrefs.Save(); }
        static Material Own(FBCar car, Material m, string kind)
        {
            car.ownMats ??= new Dictionary<string, Material>(); var k = kind + "|" + m.name; if (car.ownMats.TryGetValue(k, out var o) && o) return o; o = new Material(m) { name = m.name }; car.ownMats[k] = o; return o;
        }
        public static void Apply(FBCar car, FBTuneData T)
        {
            if (!car || car.ie || !car.shell || T == null) return; var d = car.def;
            FBReb.Apply(car, T);                                                                                 // peças do REB antes das cores (pintura própria vale nelas)
            if (T.pneu >= 0) { var pn = FBReb.Pneus(car.id); T.wsize = pn[Mathf.Clamp(T.pneu, 0, pn.Length - 1)]; }
            // materiais próprios deste carro (os de fábrica são compartilhados entre os carros iguais)
            foreach (var r in car.shell.GetComponentsInChildren<Renderer>(true))
            {
                if (car.soundWallNode && r.transform.IsChildOf(car.soundWallNode)) continue; var ms = r.sharedMaterials; bool ch = false;
                for (int i = 0; i < ms.Length; i++) if (ms[i] && (ms[i].name == "veh_paint" || ms[i].name == "veh_glass")) { ms[i] = Own(car, ms[i], ms[i].name); ch = true; }
                if (ch) r.sharedMaterials = ms;
            }
            Texture2D skinTex = !string.IsNullOrEmpty(T.skin) ? Resources.Load<Texture2D>("CarSkins/" + T.skin) : null;
            if (car.ownMats != null) foreach (var kv in car.ownMats)
                {
                    var m = kv.Value; if (!m) continue;
                    if (kv.Key.StartsWith("veh_paint")) { m.SetTexture("_BaseMap", skinTex); m.SetColor("_BaseColor", skinTex ? Color.white : T.hasPaint ? T.paint : car.paint); m.SetFloat("_Metallic", .05f + T.metal * .6f); m.SetFloat("_Smoothness", .3f + T.brilho * .65f); }
                    else if (kv.Key.StartsWith("veh_glass")) { var c = T.hasVidro ? T.vidro : new Color(.043f, .055f, .07f); c.a = T.fume; m.SetColor("_BaseColor", c); }
                }
            // rodas: modelo, tamanho, posição/cambagem, cor do aro
            var wid = !string.IsNullOrEmpty(T.wheel) && GameData.Wheels.ContainsKey(T.wheel) ? T.wheel : car.wid;
            if (wid != car.curWid)
            {
                var src = wid != null ? FBRegistry.I.Wheel(wid) : null; var tpl = src ? src : car.wheelTpl ? car.wheelTpl.gameObject : null;
                if (tpl) foreach (var w in car.wheels) { var wm = UnityEngine.Object.Instantiate(tpl, w.obj.parent, false); wm.SetActive(true); wm.name = w.obj.name; foreach (var t in wm.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = FBWorld.LVehicle; UnityEngine.Object.Destroy(w.obj.gameObject); w.obj = wm.transform; }
                car.curWid = wid; car.wsrc = src;
            }
            float wdr = car.wsrc && car.curWid != null ? Json.F(Json.O(GameData.Wheels, car.curWid), "r") : 0;
            foreach (var w in car.wheels)
            {
                float off = w.front ? T.foff : T.roff, cam = w.front ? T.fcam : T.rcam, sx = Mathf.Sign(w.webP.x == 0 ? 1 : w.webP.x);
                w.obj.localPosition = new Vector3(-(w.webP.x + sx * off), w.webP.y, w.webP.z);
                var ry = Quaternion.Euler(0, w.webP.x < 0 ? 180 : 0, 0); w.obj.localScale = Vector3.one * ((wdr > 0 ? w.rr / wdr : 1) * T.wsize); w.r = w.rr * FBVehicles.SCALE * T.wsize;
                w.bas = Quaternion.AngleAxis(cam * sx, Vector3.forward) * ry; w.obj.localRotation = w.bas;   // cambagem: topo da roda para dentro
                foreach (var r in w.obj.GetComponentsInChildren<Renderer>(true))
                {
                    var ms = r.sharedMaterials;
                    for (int i = 0; i < ms.Length; i++) { if (!ms[i]) continue; var bn = ms[i].name.Replace("rim_own|", ""); bool tire = System.Text.RegularExpressions.Regex.IsMatch(bn, "pneu|tire|tyre|borracha", System.Text.RegularExpressions.RegexOptions.IgnoreCase); if (tire) continue; if (T.hasRodaCor) { var m = Own(car, ms[i], "rim"); m.SetColor("_BaseColor", T.rodaCor); ms[i] = m; } }
                    r.sharedMaterials = ms;
                }
            }
            // suspensão a ar: altura dianteira / traseira -> carroceria sobe/desce e inclina; banco e rig acompanham
            float hf = T.fh, hr = T.rh; float At(float z) => hr + (hf - hr) * Mathf.Clamp01((z - car.zb) / Mathf.Max(.1f, car.wbL));
            car.shell.localPosition = new Vector3(0, car.shellY0 + (hf + hr) / 2, 0); car.shell.localRotation = Quaternion.Euler(-Mathf.Atan2(hf - hr, car.wbL) * Mathf.Rad2Deg, 0, 0);
            car.shellP0 = car.shell.localPosition; car.shellR0 = car.shell.localRotation;   // base da lataria (FBCarPhys.Body balança em cima dela)
            car.seat = car.seat0 + Vector3.up * At(car.seat0.z);
            Vector3[] Adj(Vector3[] a) => a?.Select(p => p + Vector3.up * At(p.z)).ToArray();
            if (car.rigHands0 != null) car.rigHands = Adj(car.rigHands0); if (car.rigPoles0 != null) car.rigPoles = Adj(car.rigPoles0); if (car.rigFeet0 != null) car.rigFeet = Adj(car.rigFeet0);
            if (car.rigWheel0 is Vector3 rw) car.rigWheel = rw + Vector3.up * At(rw.z);
            car.vol = null; car.volTried = false; if (car.driver) car.driver.fitCar = null;   // refaz eixo do volante e cabeça no teto
            // neon, farol, placa
            Neon(car, T); Farol(car, T); Placa(car, T);
            car.engineProf = !string.IsNullOrEmpty(T.ronco) && Json.O(Json.O(GameData.RES, "profiles"), T.ronco) != null ? T.ronco : FBCarSound.ProfileFor(car.id, car); car.turbo = T.turbo; car.tracao = string.IsNullOrEmpty(T.tracao) ? FBReb.Tracao(car.id) : T.tracao;
            if (T.somSet) BuildSom(car, T);
        }
        static Texture2D glow; static Texture2D Glow()
        {
            if (glow) return glow; glow = new Texture2D(64, 64, TextureFormat.RGBA32, false); for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) { float r = new Vector2(x - 31.5f, y - 31.5f).magnitude / 32; float a = r < .17f ? 1 : r < 1 ? Mathf.Lerp(.55f, 0, (r - .35f) / .65f) : 0; if (r >= .17f && r < .35f) a = Mathf.Lerp(1, .55f, (r - .17f) / .18f); glow.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(a))); }
            glow.Apply(); return glow;
        }
        static Material GlowMat(Color c) { var m = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit), true); m.SetTexture("_BaseMap", Glow()); m.mainTexture = Glow(); m.SetColor("_BaseColor", c); m.SetFloat("_Cull", 0); return m; }
        static void Neon(FBCar car, FBTuneData T)
        {
            var old = car.transform.Find("neon"); if (!T.neon) { if (old) UnityEngine.Object.Destroy(old.gameObject); return; }
            if (!old)
            {
                var zs = car.wheels.Select(w => w.webP.z).DefaultIfEmpty(0).ToList(); var xs = car.wheels.Select(w => Mathf.Abs(w.webP.x)).DefaultIfEmpty(.7f).ToList(); float L = (zs.Max() - zs.Min()) * 1.55f + 1, W = xs.Max() * 2 * 1.7f + .6f;
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); UnityEngine.Object.Destroy(q.GetComponent<Collider>()); q.name = "neon"; q.transform.SetParent(car.transform, false);
                q.transform.localPosition = new Vector3(0, .035f, (zs.Max() + zs.Min()) / 2); q.transform.localRotation = Quaternion.Euler(90, 0, 0); q.transform.localScale = new Vector3(W, L, 1); q.GetComponent<Renderer>().sharedMaterial = GlowMat(T.neonC); old = q.transform;
            }
            old.GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor", T.neonC);
        }
        // cor do farol (oficina): tinge a lente e o facho de verdade do farol (FBCarLights) — antes eram quadrados de brilho sempre acesos
        static void Farol(FBCar car, FBTuneData T)
        {
            var old = car.shell.Find("farol"); if (old) UnityEngine.Object.Destroy(old.gameObject);
            car.headTint = T.hasFarol ? T.farol : (Color?)null;
        }
        // placa Mercosul na traseira (entre as lanternas, abaixo delas)
        static void Placa(FBCar car, FBTuneData T)
        {
            var old = car.shell.Find("placa_txt"); if (string.IsNullOrEmpty(T.placa)) { if (old) UnityEngine.Object.Destroy(old.gameObject); FBPlates.SetText(car.shell, FBPlates.Of(car.paint)); return; }
            // placa própria do modelo: só troca o texto na textura (a placa flutuante abaixo é o último recurso, modelo sem placa)
            if (FBPlates.SetText(car.shell, T.placa.ToUpperInvariant().Substring(0, Mathf.Min(7, T.placa.Length)))) { if (old) UnityEngine.Object.Destroy(old.gameObject); return; }
            var L = Json.O(car.def, "lights"); var B = Json.L(L, "BrakeLight") ?? Json.L(L, "ReverseLight"); if (B == null || B.Count == 0) return;
            float y = B.Average(p => Json.Fv((p as List<object>)[1])) - .26f, z = B.Min(p => Json.Fv((p as List<object>)[2])) - .015f;
            if (!old)
            {
                var g = new GameObject("placa_txt").transform; g.SetParent(car.shell, false); g.localPosition = new Vector3(0, y, z); g.localRotation = Quaternion.Euler(0, 180, 0);
                var bg = GameObject.CreatePrimitive(PrimitiveType.Quad); UnityEngine.Object.Destroy(bg.GetComponent<Collider>()); bg.transform.SetParent(g, false); bg.transform.localScale = new Vector3(.4f, .13f, 1); bg.transform.localRotation = Quaternion.Euler(0, 180, 0);
                var bm = new Material(FBShaders.Unlit); bm.SetColor("_BaseColor", new Color(.96f, .96f, .94f)); bg.GetComponent<Renderer>().sharedMaterial = bm;
                var band = GameObject.CreatePrimitive(PrimitiveType.Quad); UnityEngine.Object.Destroy(band.GetComponent<Collider>()); band.transform.SetParent(g, false); band.transform.localScale = new Vector3(.4f, .03f, 1); band.transform.localPosition = new Vector3(0, .05f, -.001f); band.transform.localRotation = Quaternion.Euler(0, 180, 0);
                var bandM = new Material(FBShaders.Unlit); bandM.SetColor("_BaseColor", new Color(.11f, .25f, .6f)); band.GetComponent<Renderer>().sharedMaterial = bandM;
                var tg = new GameObject("txt"); tg.transform.SetParent(g, false); tg.transform.localPosition = new Vector3(0, -.012f, -.002f); tg.transform.localRotation = Quaternion.Euler(0, 180, 0);
                var tm = tg.AddComponent<TextMesh>(); tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.fontSize = 64; tm.characterSize = .0105f; tm.font = FBUI.Font; tg.GetComponent<MeshRenderer>().sharedMaterial = FBUI.Font.material; old = g;
            }
            var t = old.GetComponentInChildren<TextMesh>(); t.text = T.placa.ToUpperInvariant().Substring(0, Mathf.Min(7, T.placa.Length)); t.color = new Color(.07f, .07f, .07f);
        }
        // ---- SOM (OficinaCriarSom): grupo no BaseSom/caçamba, virado para trás; cada slot = módulo com posição, giro e escala
        public static (Vector3 p, float w, bool bed)? SomAnchor(Dictionary<string, object> d)
        {
            var som = Json.O(d, "som"); var bed = Json.O(som, "bed"); var bse = Json.L(som, "base"); var mala = Json.L(Json.O(d, "doors"), "mala");
            if (bed != null) return (new Vector3(0, Json.F(bed, "floor") + .01f, Json.F(bed, "cab") - .04f), Json.F(bed, "w"), true);
            if (bse != null) return (new Vector3(-Json.Fv(bse[0]), Json.Fv(bse[1]), Json.Fv(bse[2])), 1.1f, false);
            if (mala != null) return (new Vector3(0, Json.Fv(mala[1]) + .12f, Json.Fv(mala[2]) + .4f), 1.1f, false);
            return null;
        }
        public static void BuildSom(FBCar car, FBTuneData T)
        {
            if (car.soundWallNode) UnityEngine.Object.Destroy(car.soundWallNode.gameObject); car.soundWallNode = null;
            var a = SomAnchor(car.def); var slots = T.som.Where(s => s != null && FBRegistry.I.Som(s.m)).ToList(); var W = FBParedao.Get(car); W.leds.Clear(); W.vus.Clear(); W.spk.Clear();
            if (a == null || slots.Count == 0) return;
            var g = new GameObject("sound_wall").transform; g.SetParent(car.shell, false); g.localPosition = a.Value.p; g.localRotation = Quaternion.Euler(0, 180, 0);
            foreach (var s in slots) { var o = FBParedao.Module(s.m, W); if (!o) continue; o.transform.SetParent(g, false); o.transform.localPosition = new Vector3(-s.p.x, s.p.y, s.p.z); o.transform.localRotation = Quaternion.Euler(0, -s.r, 0); o.transform.localScale = Vector3.one * s.s; }
            foreach (var t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = FBWorld.LVehicle;
            car.soundWallNode = g; SomColors(car, T); FBParedao.Rebuilt(car);
        }
        public static void SomColors(FBCar car, FBTuneData T)
        {
            if (!car.soundWallNode) return;
            foreach (var r in car.soundWallNode.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "led_fita" || r.name == "led" || r.name == "vu" || (r.transform.parent && (r.transform.parent.name == "led" || r.transform.parent.name == "vu"))) continue;
                bool spk = System.Text.RegularExpressions.Regex.IsMatch((r.transform.parent ? r.transform.parent.name : "") + "|" + r.name, "(^|\\|)spk\\d"); var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    if (!ms[i]) continue; bool carc = ms[i].name.StartsWith("Som_Carcaca"); string k = carc ? "caixa" : spk ? "falante" : "corneta";
                    bool has = k == "caixa" ? T.hasCaixa : k == "falante" ? T.hasFalante : T.hasCorneta; if (!has) continue; var c = k == "caixa" ? T.caixa * .85f : k == "falante" ? T.falante : T.corneta; c.a = 1;
                    var m = Own(car, ms[i], "som_" + k); m.SetColor("_BaseColor", c); ms[i] = m;
                }
                r.sharedMaterials = ms;
            }
        }
    }

    // ---- UI da oficina
    public static class FBOficina
    {
        public static bool on; static GameObject ui; static FBCar car; static FBTuneData T; static string tab = "inicio"; static float camYaw = 35, camPitch = 14, camDist = 7.5f, fov = 45; static int selSlot = -1; static Transform body;
        static readonly (string pre, string name)[] SOM_TIPOS = { ("CaixaSom", "Caixa de som"), ("Caixa_Grave", "Caixa grave"), ("Caixa_Medio", "Caixa média"), ("Corneteiras", "Corneteira"), ("TrioGoiano", "Trio goiano"), ("HackMovel", "Rack móvel") };
        static readonly string[] PRESETS = { "#f4f4f0", "#c9cbcd", "#55595e", "#18191b", "#8e1712", "#e2231a", "#ff6a00", "#ffc400", "#d9b21c", "#2e5a3a", "#00c853", "#00e5ff", "#1d3f7a", "#2962ff", "#7b1fa2", "#ff2a6d", "#c9b98f", "#5d4037" };
        const int SLOTS = 20;
        public static void Toggle()
        {
            if (on) { Close(); return; }
            var me = FBMatch.Player; var c = FBVehicles.CarOf(me) ?? FBRole.roleCar; if (!c || c.ie) { FBHUD.I?.Center("SÓ COM O SEU CARRO DO ROLÊ (O = OFICINA)", 2); return; }
            if (me && Vector3.Distance(me.Pos, c.transform.position) > 12) { FBHUD.I?.Center("CHEGUE PERTO DO SEU CARRO", 1.6f); return; }
            Open(c);
        }
        public static void Open(FBCar c)
        {
            car = c; T = FBTune.Load(c.id) ?? new FBTuneData(); on = true; FBDrive.hold = FBVehicles.CarOf(FBMatch.Player) == c;
            if (!T.somSet && c.soundWallNode) T.som = new List<FBSomSlot>();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true; Build();
        }
        static void Close()
        {
            on = false; FBDrive.hold = false; FBDrive.rev = false; if (car) FBTune.Save(car.id, T); if (ui) UnityEngine.Object.Destroy(ui); ui = null;
            if (!FBInput.Touch) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }
        static void Changed() { FBTune.Apply(car, T); FBTune.Save(car.id, T); }
        // câmera orbital da oficina
        public static bool Camera(Camera cam, float dt)
        {
            if (!on || !car) return false; var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed || mouse != null && mouse.leftButton.isPressed && !UnityEngine.EventSystems.EventSystem.current?.IsPointerOverGameObject() == true) { var d = mouse.delta.ReadValue(); camYaw += d.x * .25f; camPitch = Mathf.Clamp(camPitch - d.y * .2f, -5, 70); }
            if (mouse != null) { float s = mouse.scroll.ReadValue().y; if (Mathf.Abs(s) > .01f) camDist = Mathf.Clamp(camDist - Mathf.Sign(s) * .6f, 3.5f, 14); }
            var c = car.transform.position + Vector3.up * 1.0f; var rot = Quaternion.Euler(camPitch, car.transform.eulerAngles.y + 180 + camYaw, 0);
            cam.transform.position = c - rot * Vector3.forward * camDist; cam.transform.rotation = Quaternion.LookRotation(c - cam.transform.position); cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1 - Mathf.Exp(-dt * 6));
            return true;
        }
        static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.white;
        // ícones originais da oficina do REB (assets/oficina/icons -> Resources/UI/Oficina)
        static readonly Dictionary<string, Sprite> icos = new();
        static Sprite Ico(string n)
        {
            if (icos.TryGetValue(n, out var s)) return s; var t = Resources.Load<Texture2D>("UI/Oficina/" + n);
            s = t ? Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, .5f), 100) : null; icos[n] = s; return s;
        }
        static Button IcoBtn(Transform p, string ico, string label, Vector2 pos, Vector2 size, Action a, bool sel = false)
        {
            var b = FBUI.Button(p, "", new Vector2(.5f, .5f), new Vector2(.5f, .5f), pos, size, a, false); var im = b.GetComponent<Image>(); var sp = Ico(ico);
            if (sp) { im.sprite = sp; im.preserveAspect = true; im.color = sel ? FBUI.Gold : Color.white; } else im.color = sel ? FBUI.Gold : new Color(1, 1, 1, .08f);
            if (!string.IsNullOrEmpty(label)) FBUI.Label(b.transform, label, 10, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, -9), new Vector2(size.x + 30, 16), TextAnchor.MiddleCenter, false, sel ? FBUI.Gold : Color.white);
            return b;
        }
        static string sub = "";
        public static void TestTab(string t, string s) { if (!on) return; tab = t; sub = s; Build(); }   // autoteste: abre uma aba
        public static void TestClose() { if (on) Close(); }
        static readonly (string id, string name, string ico)[] TABS = { ("inicio", "INÍCIO", "Oficina_AbaInicio"), ("carro", "CARRO", "Oficina_AbaCarro"), ("roda", "RODA", "Oficina_AbaRoda"), ("som", "SOM", "Oficina_AbaSom"), ("skin", "SKIN", "Oficina_AbaSkin") };
        static void Build()
        {
            if (ui) UnityEngine.Object.Destroy(ui); var cv = FBUI.Canvas("Oficina", 36); ui = cv.gameObject;
            var top = FBUI.Panel(cv.transform, "topo", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -32), new Vector2(0, 64), new Color(0, 0, 0, .55f)); top.rectTransform.offsetMin = new Vector2(0, top.rectTransform.offsetMin.y); top.rectTransform.offsetMax = new Vector2(0, top.rectTransform.offsetMax.y);
            FBUI.Label(top.transform, "OFICINA · " + (Json.S(car.def, "name") ?? car.id).Replace('_', ' ').ToUpperInvariant(), 21, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(260, 0), new Vector2(480, 40), TextAnchor.MiddleLeft, true, FBUI.Gold);
            FBUI.Label(top.transform, "FOV", 10, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-420, 0), new Vector2(40, 20), TextAnchor.MiddleRight, false, FBUI.Mut);
            Slider(top.transform, new Vector2(1, .5f), new Vector2(-330, 0), 150, Mathf.InverseLerp(25, 70, fov), v => fov = Mathf.Lerp(25, 70, v));
            FBUI.Button(top.transform, "VOLTAR", new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-90, 0), new Vector2(140, 40), Close);
            // abas do REB (ícone normal / selecionado)
            for (int i = 0; i < TABS.Length; i++)
            {
                var (k, n, ic) = TABS[i]; bool on = tab == k; var spn = ic + (on ? (k == "inicio" ? "_Selecionado" : "_Selecionada") : "_Normal");
                var b = FBUI.Button(cv.transform, Ico(spn) ? "" : n, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2((i - 2) * 118, 46), new Vector2(108, 62), () => { tab = k; sub = ""; selSlot = -1; Build(); }, on && !Ico(spn));
                var im = b.GetComponent<Image>(); if (Ico(spn)) { im.sprite = Ico(spn); im.preserveAspect = true; im.color = Color.white; FBUI.Label(b.transform, n, 11, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, -10), new Vector2(120, 16), TextAnchor.MiddleCenter, true, on ? FBUI.Gold : Color.white); }
            }
            var side = FBUI.Panel(cv.transform, "painel", new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-210, 20), new Vector2(390, 600), new Color(.07f, .08f, .1f, .93f)); body = side.transform; y = 272;
            if (tab == "carro") Subs(("pintura", "PINTURA"), ("bodykit", "BODYKIT"), ("acess", "ACESSÓRIOS"));
            if (tab == "roda") Subs(("rodas", "RODAS"), ("susp", "SUSPENSÃO"));
            switch (tab)
            {
                case "inicio": InicioTab(); break;
                case "carro": if (sub == "bodykit") BodykitTab(); else if (sub == "acess") AcessTab(); else PinturaTab(); break;
                case "roda": if (sub == "susp") SuspTab(); else RodasTab(); break;
                case "som": SomTab(); break;
                case "skin": SkinTab(); break;
            }
            // texto dos botões cabe no botão (o FBUI.Button usa fonte fixa 18)
            foreach (var t in side.GetComponentsInChildren<Text>(true)) if (t.GetComponentInParent<Button>()) { t.resizeTextForBestFit = true; t.resizeTextMinSize = 7; t.resizeTextMaxSize = Mathf.Min(t.fontSize, 14); t.horizontalOverflow = HorizontalWrapMode.Wrap; }
        }
        static void Subs(params (string id, string name)[] s)
        {
            if (string.IsNullOrEmpty(sub)) sub = s[0].id; float w = 350f / s.Length - 6;
            for (int i = 0; i < s.Length; i++) { var k = s[i].id; FBUI.Button(body, s[i].name, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-175 + w / 2 + i * (w + 6), y), new Vector2(w, 34), () => { sub = k; Build(); }, sub == k); }
            y -= 44;
        }
        // INÍCIO: consertar, ronco (os 37 do REB; o de fábrica marcado), tração, turbo, placa
        static void InicioTab()
        {
            IcoBtn(body, "Oficina_ConcertarCarro", "CONSERTAR", new Vector2(-120, y - 22), new Vector2(58, 58), () => { FBCarDamage.Repair(car); FBHUD.I?.Center("CARRO CONSERTADO", 1.2f); });
            IcoBtn(body, T.turbo ? "Oficina_TurboON" : "Oficina_TurboOFF", T.turbo ? "TURBO ON" : "TURBO OFF", new Vector2(0, y - 22), new Vector2(58, 58), () => { T.turbo = !T.turbo; Changed(); Build(); }, T.turbo);
            IcoBtn(body, "Oficina_MudarPlaca", "PLACA", new Vector2(120, y - 22), new Vector2(58, 58), () => { tab = "skin"; sub = "placa"; Build(); });
            y -= 84;
            var fab = FBReb.Tracao(car.id) ?? "FWD"; var cur = string.IsNullOrEmpty(T.tracao) ? fab : T.tracao;
            H($"TRAÇÃO · de fábrica {fab}");
            foreach (var (t, ic, x) in new[] { ("FWD", "Oficina_Tracao_FWD", -120f), ("RWD", "Oficina_Tracao_RWD", 0f), ("4x4", "Oficina_Tracao_4x4", 120f) }) { var k = t; IcoBtn(body, ic, t, new Vector2(x, y - 16), new Vector2(84, 40), () => { T.tracao = k == fab ? "" : k; Changed(); Build(); }, cur == k); }
            y -= 62;
            var all = FBReb.Roncos; var fabR = FBCarSound.ProfileFor(car.id, car); var curR = string.IsNullOrEmpty(T.ronco) ? fabR : T.ronco;
            H($"RONCO ({all.Count}) · de fábrica {FBReb.RoncoNome(fabR).ToUpperInvariant()}");
            const int per = 8; int pg = Mathf.Clamp(page, 0, Mathf.Max(0, (all.Count - 1) / per));
            for (int i = pg * per, n = 0; i < Mathf.Min(all.Count, (pg + 1) * per); i++, n++)
            {
                var k = all[i]; float x = n % 2 == 0 ? -88 : 88, yy = y - (n / 2) * 34;
                FBUI.Button(body, (k == fabR ? "★ " : "") + FBReb.RoncoNome(k), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x, yy), new Vector2(170, 30), () => { T.ronco = k == fabR ? "" : k; Changed(); Build(); }, curR == k);
            }
            y -= (per / 2) * 34 + 4;
            Btns(("◀", () => { page = Mathf.Max(0, page - 1); }), ($"{pg + 1}/{(all.Count + per - 1) / per}", () => { }), ("▶", () => { page = Mathf.Min((all.Count - 1) / per, page + 1); }), ("FÁBRICA", () => T.ronco = ""));
            { var b = FBUI.Button(body, "TESTAR RONCO (SEGURE)", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, y - 6), new Vector2(300, 40), () => { }, true); var tr = b.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>(); Add(tr, UnityEngine.EventSystems.EventTriggerType.PointerDown, () => FBDrive.rev = true); Add(tr, UnityEngine.EventSystems.EventTriggerType.PointerUp, () => FBDrive.rev = false); y -= 48; }
            FBUI.Label(body, FBDrive.hold ? "Motor ligado no elevador da oficina." : "Entre no carro para ouvir o ronco.", 11, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, y), new Vector2(340, 20), TextAnchor.MiddleCenter, false, FBUI.Mut);
        }
        static void PinturaTab()
        {
            H("COR DA PINTURA"); Swatches(c => { T.hasPaint = true; T.paint = c; T.skin = ""; });
            Row("METÁLICO", T.metal, v => T.metal = v); Row("BRILHO", T.brilho, v => T.brilho = v);
            H("VIDROS"); Row("FUMÊ", Mathf.InverseLerp(.2f, .95f, T.fume), v => T.fume = Mathf.Lerp(.2f, .95f, v)); Swatches(c => { T.hasVidro = true; T.vidro = c * .35f; }, 9);
            H("FAROL"); Btns(("BRANCO", () => { T.hasFarol = true; T.farol = Color.white; }), ("XENON", () => { T.hasFarol = true; T.farol = new Color(.75f, .85f, 1); }), ("AMARELO", () => { T.hasFarol = true; T.farol = new Color(1, .85f, .35f); }), ("TIRAR", () => T.hasFarol = false));
            H("NEON"); Btns((T.neon ? "DESLIGAR NEON" : "INSTALAR NEON", () => T.neon = !T.neon)); if (T.neon) Swatches(c => T.neonC = c, 9);
        }
        // BODYKIT: só as zonas que este carro tem no REB (BodykitsCombiner); FÁBRICA = peça original
        static void BodykitTab()
        {
            if (!FBReb.HasBodykit(car)) { H("ESTE CARRO NÃO TEM BODYKIT NO REB"); FBUI.Label(body, "No REB têm bodykit: Saveiro Cross, Gol G2, Chevette, Golf, Gol G5 e BMW 320i.", 11, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, y - 10), new Vector2(350, 40), TextAnchor.UpperLeft, false, FBUI.Mut); return; }
            string[] ICO = { "Oficina_Bodykit_Aerofolio", "Oficina_Bodykit_Capo", "Oficina_Bodykit_Frente", "Oficina_Bodykit_Lateral", "Oficina_Bodykit_Traseira" };
            for (int zi = 0; zi < FBReb.ZONAS.Length; zi++)
            {
                var z = FBReb.ZONAS[zi]; if (!FBReb.HasZone(car, z)) continue; int zk = zi; var vs = FBReb.ZoneVariants(car, z);
                IcoBtn(body, ICO[zi], "", new Vector2(-150, y - 18), new Vector2(46, 46), () => { }); FBUI.Label(body, FBReb.ZONA_NOMES[zi], 11, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-30, y), new Vector2(200, 18), TextAnchor.MiddleLeft, false, FBUI.Mut);
                var opts = new List<(string, int)> { ("FÁBRICA", -1) }; for (int i = 0; i < vs.Count; i++) opts.Add(($"KIT {i + 1}", vs[i]));
                float w = 250f / opts.Count - 6;
                for (int i = 0; i < opts.Count; i++) { var (nm, v) = opts[i]; FBUI.Button(body, nm, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-100 + w / 2 + i * (w + 6) + 25, y - 26), new Vector2(w, 30), () => { T.bk[zk] = v; Changed(); Build(); }, (T.bk != null && zk < T.bk.Length ? T.bk[zk] : -1) == v); }
                y -= 62;
            }
            Btns(("TUDO DE FÁBRICA", () => { for (int i = 0; i < T.bk.Length; i++) T.bk[i] = -1; }));
        }
        // ACESSÓRIOS do REB: bandeiras nas janelas, forros das portas, engate
        static void AcessTab()
        {
            bool any = false;
            if (FBReb.HasBandeira(car))
            {
                any = true; H("BANDEIRAS");
                IcoBtn(body, "Oficina_Bandeira1", "ESQUERDA", new Vector2(-120, y - 20), new Vector2(54, 54), () => { T.bandL = !T.bandL; Changed(); Build(); }, T.bandL);
                IcoBtn(body, "Oficina_Bandeira2", "DIREITA", new Vector2(-40, y - 20), new Vector2(54, 54), () => { T.bandR = !T.bandR; Changed(); Build(); }, T.bandR);
                for (int i = 0; i < FBReb.BANDEIRAS.Length; i++) { int k = i; FBUI.Button(body, FBReb.BANDEIRAS[i], new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(i % 2 == 0 ? 68 : 150, y - 4 - (i / 2) * 32), new Vector2(78, 28), () => { T.bandeira = k; if (!T.bandL && !T.bandR) T.bandL = T.bandR = true; Changed(); Build(); }, T.bandeira == k); }
                y -= 80;
            }
            int nf = Mathf.Max(FBReb.Forros(car, "L"), FBReb.Forros(car, "R")), nt = Mathf.Max(FBReb.Forros(car, "L02"), FBReb.Forros(car, "R02"));
            if (nf > 1 || nt > 1)
            {
                any = true; H("FORROS DAS PORTAS");
                if (nf > 1) { IcoBtn(body, "Oficina_PortaFrente", "", new Vector2(-150, y - 14), new Vector2(40, 40), () => { }); for (int i = 0; i < nf; i++) { int k = i; FBUI.Button(body, i == 0 ? "ORIGINAL" : "MODELO " + i, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-50 + i * 120, y - 14), new Vector2(112, 30), () => { T.forroF = k; Changed(); Build(); }, T.forroF == k); } y -= 46; }
                if (nt > 1) { IcoBtn(body, "Oficina_PortaTras", "", new Vector2(-150, y - 14), new Vector2(40, 40), () => { }); for (int i = 0; i < nt; i++) { int k = i; FBUI.Button(body, i == 0 ? "ORIGINAL" : "MODELO " + i, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-50 + i * 120, y - 14), new Vector2(112, 30), () => { T.forroT = k; Changed(); Build(); }, T.forroT == k); } y -= 46; }
                FBUI.Label(body, "Abra as portas (U) para ver o forro.", 10, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, y + 4), new Vector2(340, 16), TextAnchor.MiddleCenter, false, FBUI.Mut); y -= 18;
            }
            if (FBReb.HasEngate(car))
            {
                any = true; H("ENGATE");
                IcoBtn(body, T.engate ? "Oficina_RemoverReboque" : "Oficina_InstalarReboque", T.engate ? "REMOVER ENGATE" : "INSTALAR ENGATE", new Vector2(0, y - 22), new Vector2(60, 60), () => { T.engate = !T.engate; Changed(); Build(); }, T.engate); y -= 80;
            }
            if (!any) H("ESTE CARRO NÃO TEM ACESSÓRIOS NO REB");
        }
        static void RodasTab()
        {
            H("RODAS (" + GameData.Wheels.Count + ")"); var ids = GameData.Wheels.Keys.Where(k => FBRegistry.I.Wheel(k)).ToList(); int per = 12, pg = Mathf.Clamp(page, 0, Mathf.Max(0, (ids.Count - 1) / per));
            for (int i = pg * per, n = 0; i < Mathf.Min(ids.Count, (pg + 1) * per); i++, n++) { var id = ids[i]; float x = -120 + (n % 3) * 120, yy = y - (n / 3) * 46; FBUI.Button(body, id.Replace('_', ' '), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x, yy), new Vector2(114, 40), () => { T.wheel = id; Changed(); Build(); }, (T.wheel == id) || (string.IsNullOrEmpty(T.wheel) && car.curWid == id)); }
            y -= 4 * 46 + 6; Btns(("◀", () => { page = Mathf.Max(0, page - 1); }), ($"{pg + 1}/{(ids.Count + per - 1) / per}", () => { }), ("▶", () => { page++; }), ("DE FÁBRICA", () => T.wheel = ""));
            // tamanho do pneu: os 13 tamanhos do REB (Carro/TamanhoRodas), 1,0 = de fábrica
            var pn = FBReb.Pneus(car.id); int pi = T.pneu >= 0 ? T.pneu : System.Array.FindIndex(pn, v => Mathf.Abs(v - 1) < .001f);
            H($"TAMANHO DO PNEU · {pn[Mathf.Clamp(pi, 0, pn.Length - 1)]:0.00}×");
            Btns(("◀", () => { T.pneu = Mathf.Max(0, pi - 1); }), ("DE FÁBRICA", () => { T.pneu = -1; T.wsize = 1; }), ("▶", () => { T.pneu = Mathf.Min(pn.Length - 1, pi + 1); }));
            H("COR DO ARO"); Swatches(c => { T.hasRodaCor = true; T.rodaCor = c; }, 9); Btns(("ORIGINAL", () => T.hasRodaCor = false));
        }
        static void SuspTab()
        {
            H("DIANTEIRA"); Row("ALTURA", Mathf.InverseLerp(-.18f, .25f, T.fh), v => T.fh = Mathf.Lerp(-.18f, .25f, v)); Row("POSIÇÃO RODA", Mathf.InverseLerp(-.08f, .15f, T.foff), v => T.foff = Mathf.Lerp(-.08f, .15f, v)); Row("CAMBAGEM", Mathf.InverseLerp(-5, 18, T.fcam), v => T.fcam = Mathf.Lerp(-5, 18, v));
            H("TRASEIRA"); Row("ALTURA", Mathf.InverseLerp(-.18f, .25f, T.rh), v => T.rh = Mathf.Lerp(-.18f, .25f, v)); Row("POSIÇÃO RODA", Mathf.InverseLerp(-.08f, .15f, T.roff), v => T.roff = Mathf.Lerp(-.08f, .15f, v)); Row("CAMBAGEM", Mathf.InverseLerp(-5, 18, T.rcam), v => T.rcam = Mathf.Lerp(-5, 18, v));
            Btns(("ZERAR", () => { T.fh = T.rh = T.foff = T.roff = T.fcam = T.rcam = 0; }));
        }
        static void SkinTab()
        {
            if (sub == "placa")
            {
                H("PLACA MERCOSUL"); var go = new GameObject("campo", typeof(RectTransform), typeof(Image), typeof(InputField)); go.transform.SetParent(body, false); var rt = (RectTransform)go.transform; rt.anchoredPosition = new Vector2(0, y - 10); rt.sizeDelta = new Vector2(240, 44);
                go.GetComponent<Image>().color = new Color(.95f, .95f, .93f); var tx = FBUI.Label(go.transform, "", 24, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, true, Color.black); tx.rectTransform.offsetMin = tx.rectTransform.offsetMax = Vector2.zero; tx.supportRichText = false;
                var f = go.GetComponent<InputField>(); f.textComponent = tx; f.characterLimit = 7; f.text = T.placa ?? ""; f.onEndEdit.AddListener(s => { T.placa = s.ToUpperInvariant(); Changed(); }); y -= 60;
                Btns(("ALEATÓRIA", () => { const string L = "ABCDEFGHIJKLMNPQRSTUVWXYZ"; var r = new System.Random(); T.placa = $"{"NOQR"[r.Next(4)]}{L[r.Next(25)]}{L[r.Next(25)]}{r.Next(10)}{L[r.Next(25)]}{r.Next(10)}{r.Next(10)}"; }), ("TIRAR", () => T.placa = ""), ("SKINS", () => sub = ""));
                return;
            }
            H("SKINS"); var skins = (Json.L(car.def, "skins") ?? new List<object>()).Select(s => s as string).Where(s => Resources.Load<Texture2D>("CarSkins/" + s)).ToList();
            Btns(("PINTURA (SEM SKIN)", () => T.skin = ""), ("PLACA", () => sub = "placa"));
            foreach (var s in skins) { var k = s; FBUI.Button(body, s.Replace('_', ' '), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, y), new Vector2(340, 36), () => { T.skin = k; Changed(); Build(); }, T.skin == k); y -= 40; }
            if (skins.Count == 0) FBUI.Label(body, "ESTE CARRO NÃO TEM SKIN DO REB", 12, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, y), new Vector2(340, 24), TextAnchor.MiddleCenter, false, FBUI.Mut);
        }
        static int page; static float y;
        static void Add(UnityEngine.EventSystems.EventTrigger t, UnityEngine.EventSystems.EventTriggerType k, Action a) { var e = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = k }; e.callback.AddListener(_ => a()); t.triggers.Add(e); }
        static void H(string s) { FBUI.Label(body, s, 11, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, y), new Vector2(350, 20), TextAnchor.MiddleLeft, false, FBUI.Mut); y -= 26; }
        static void Row(string label, float v01, Action<float> set)
        {
            FBUI.Label(body, label, 11, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-115, y), new Vector2(120, 20), TextAnchor.MiddleLeft, false, FBUI.Mut);
            Slider(body, new Vector2(.5f, .5f), new Vector2(55, y), 210, v01, v => { set(v); Changed(); }); y -= 32;
        }
        static void Btns(params (string t, Action a)[] bs)
        {
            float w = Mathf.Min(170, 350f / bs.Length - 6); for (int i = 0; i < bs.Length; i++) { var a = bs[i].a; FBUI.Button(body, bs[i].t, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-175 + w / 2 + i * (w + 6), y), new Vector2(w, 34), () => { a(); Changed(); Build(); }); }
            y -= 40;
        }
        static void Swatches(Action<Color> set, int n = 18)
        {
            for (int i = 0; i < Mathf.Min(n, PRESETS.Length); i++) { var c = Hex(PRESETS[i]); float x = -160 + (i % 9) * 40, yy = y - (i / 9) * 38; var b = FBUI.Button(body, "", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x, yy), new Vector2(34, 32), () => { set(c); Changed(); Build(); }); b.GetComponent<Image>().color = c; }
            y -= ((Mathf.Min(n, PRESETS.Length) + 8) / 9) * 38 + 4;
        }
        static Slider Slider(Transform p, Vector2 anchor, Vector2 pos, float w, float val, Action<float> on)
        {
            var root = FBUI.Rect(p, "slider", anchor, anchor, pos, new Vector2(w, 20));
            var bg = FBUI.Panel(root, "bg", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1, 1, 1, .14f)); bg.rectTransform.offsetMin = new Vector2(0, 7); bg.rectTransform.offsetMax = new Vector2(0, -7);
            var fa = FBUI.Rect(root, "fa", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); fa.offsetMin = new Vector2(0, 7); fa.offsetMax = new Vector2(-10, -7);
            var fill = FBUI.Panel(fa, "fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, FBUI.Gold); fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            var ha = FBUI.Rect(root, "ha", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); ha.offsetMin = new Vector2(10, 0); ha.offsetMax = new Vector2(-10, 0);
            var hd = FBUI.Panel(ha, "kn", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(18, 18), Color.white); hd.sprite = FBUI.CircleSprite;
            var s = root.gameObject.AddComponent<Slider>(); s.fillRect = fill.rectTransform; s.handleRect = hd.rectTransform; s.targetGraphic = hd; s.minValue = 0; s.maxValue = 1; s.value = Mathf.Clamp01(val);
            s.onValueChanged.AddListener(v => on(v)); return s;
        }
        // ---- SOM: tipos -> módulos -> slot; selecionado: mover/subir/girar/crescer; cores
        static void SomTab()
        {
            var a = FBTune.SomAnchor(car.def); if (a == null) { H("ESTE CARRO NÃO TEM LUGAR PARA SOM"); return; }
            if (!T.somSet) { T.somSet = true; T.som ??= new List<FBSomSlot>(); }
            H($"SLOTS {T.som.Count}/{SLOTS}");
            for (int i = 0; i < T.som.Count; i++) { int k = i; float x = -140 + (i % 5) * 70, yy = y - (i / 5) * 34; FBUI.Button(body, (i + 1) + "", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x, yy), new Vector2(62, 30), () => { selSlot = k; Build(); }, selSlot == k); }
            y -= ((T.som.Count + 4) / 5) * 34 + 6;
            if (selSlot >= 0 && selSlot < T.som.Count)
            {
                var s = T.som[selSlot]; H("MÓDULO " + (selSlot + 1) + " · " + s.m.Replace('_', ' '));
                void M(Vector3 d) { s.p += d; FBTune.BuildSom(car, T); FBTune.Save(car.id, T); }
                Btns(("◀", () => M(new Vector3(-.05f, 0, 0))), ("▶", () => M(new Vector3(.05f, 0, 0))), ("FRENTE", () => M(new Vector3(0, 0, .05f))), ("TRÁS", () => M(new Vector3(0, 0, -.05f))));
                Btns(("SOBE", () => M(new Vector3(0, .05f, 0))), ("DESCE", () => M(new Vector3(0, -.05f, 0))), ("GIRA", () => { s.r = (s.r + 15) % 360; }), ("TIRAR", () => { T.som.RemoveAt(selSlot); selSlot = -1; }));
                Row("TAMANHO", Mathf.InverseLerp(.5f, 1.6f, s.s), v => s.s = Mathf.Lerp(.5f, 1.6f, v));
            }
            if (T.som.Count < SLOTS)
            {
                H("ADICIONAR"); var mods = GameData.Som.Keys.Where(k => FBRegistry.I.Som(k)).ToList();
                foreach (var (pre, nm) in SOM_TIPOS)
                {
                    var list = mods.Where(m => m.StartsWith(pre)).ToList(); if (list.Count == 0) continue; int idx = 0; var label = nm;
                    FBUI.Button(body, $"{label} ({list.Count})", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, y), new Vector2(340, 32), () =>
                    {
                        var m = list[(T.som.Count(x => x.m.StartsWith(pre)) + idx) % list.Count]; var md = Json.O(GameData.Som, m); var mn = Json.L(md, "min"); var mx = Json.L(md, "max");
                        float w = Json.Fv(mx[0]) - Json.Fv(mn[0]), h = Json.Fv(mx[1]) - Json.Fv(mn[1]); int row = 0; float x = 0;
                        // empilha: lado a lado na largura da caçamba/porta-malas, depois para cima
                        float used = T.som.Where(o => Mathf.Abs(o.p.y) < .01f).Sum(o => { var od = Json.O(GameData.Som, o.m); return (Json.Fv(Json.L(od, "max")[0]) - Json.Fv(Json.L(od, "min")[0])) * o.s; });
                        if (used + w <= a.Value.w) x = -a.Value.w / 2 + used + w / 2; else { row = 1; x = 0; }
                        T.som.Add(new FBSomSlot { m = m, p = new Vector3(x, row * (h + .02f) - Json.Fv(mn[1]), -Json.Fv(mn[2])), r = 0, s = 1 }); selSlot = T.som.Count - 1; Changed(); Build();
                    }); y -= 36;
                }
            }
            H("CORES"); Btns(("CAIXA", () => { T.hasCaixa = true; T.caixa = Hex(PRESETS[UnityEngine.Random.Range(0, PRESETS.Length)]); }), ("FALANTE", () => { T.hasFalante = true; T.falante = Hex(PRESETS[UnityEngine.Random.Range(0, PRESETS.Length)]); }), ("CORNETA", () => { T.hasCorneta = true; T.corneta = Hex(PRESETS[UnityEngine.Random.Range(0, PRESETS.Length)]); }), ("ORIGINAIS", () => { T.hasCaixa = T.hasFalante = T.hasCorneta = false; }));
            FBUI.Label(body, "LED / VU e músicas: central multimídia (K).", 11, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, y), new Vector2(340, 20), TextAnchor.MiddleCenter, false, FBUI.Mut);
        }
    }
}
