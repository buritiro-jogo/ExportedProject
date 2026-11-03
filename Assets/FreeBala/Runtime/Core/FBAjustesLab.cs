// Autoteste dos AJUSTES (rodada 47): FreeBala.exe -fbajusteslab [gravar|conferir]
// Pedido (30/10): "todos os controles devem ter efeito real ... aplicação imediata, salvamento, carregamento, persistência", nenhum botão só visual.
//  · gravar: abre AJUSTES pelo botão do menu e, em cada aba, clica cada botão / seta / aba pelo EventSystem no CENTRO dele (raycast da UI:
//    se outra coisa cobre o botão, falha) e confere o efeito — seletor tem de mudar o ajuste, aba e créditos a tela, RESTAURAR PADRÕES os
//    valores da aba, VOLTAR fecha. Slider: valor novo -> aplicado na hora (FBInput / FBAudio / AudioListener) e o % da tela; soltar -> PlayerPrefs.
//    Som de verdade no ouvinte (AudioListener.GetOutputData): tom de teste já tocando em cada categoria, a categoria a 25 % tem de cair ~12 dB
//    na hora e as outras categorias a 0 não podem mexer nele; o mesmo com loop (SetLoop) e fonte de outro sistema (Track). Por fim grava
//    valores-marca, confere ida e volta no disco (Load) e sai.
//  · conferir (2ª execução, processo novo): o menu carregou sozinho os valores-marca e eles estão aplicados (FBAudio, AudioListener, FBInput,
//    qualidade)? Depois devolve os ajustes que o jogador tinha antes do teste.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FreeBala
{
    public class FBAjustesLab : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbajusteslab"); if (i < 0) return;
            var g = new GameObject("FB_AjustesLab"); DontDestroyOnLoad(g); var t = g.AddComponent<FBAjustesLab>(); if (i + 1 < a.Length && !a[i + 1].StartsWith("-")) t.fase = a[i + 1];
        }
        string fase = "gravar"; int fails, checks;
        static void Log(string s) => Debug.Log("[FreeBala][ajusteslab] " + s);
        void Check(bool ok, string what) { checks++; if (!ok) { fails++; Log("FALHA: " + what); } }
        IEnumerator Wait(float s) { for (float w = 0; w < s; w += Time.unscaledDeltaTime) yield return null; }

        // ajustes da tela (FBSession) + o que eles controlam de verdade
        static readonly string[] Keys = { "sens", "adsSens", "fov", "aimAssist", "aimToggle", "holdRun", "invertY", "freeLookToggle", "master", "sfx", "music", "voice", "vehicles", "ambient", "blood" };
        static FieldInfo F(string k) => typeof(FBSession).GetField(k, BindingFlags.Public | BindingFlags.Static);
        static Dictionary<string, string> State()
        {
            var d = new Dictionary<string, string>();
            foreach (var k in Keys) d[k] = System.Convert.ToString(F(k).GetValue(null), System.Globalization.CultureInfo.InvariantCulture);
            d["qualidade"] = FBQuality.Choice.ToString(); d["FBInput.Sens"] = FBInput.Sens.ToString("0.###"); d["FBInput.AdsSens"] = FBInput.AdsSens.ToString("0.###");
            d["FBInput.InvertY"] = FBInput.InvertY.ToString(); d["FBInput.AimToggle"] = FBInput.AimToggle.ToString(); d["FBFx.BloodOn"] = FBFx.BloodOn.ToString();
            d["AudioListener"] = AudioListener.volume.ToString("0.###");
            foreach (FBAudio.Cat c in System.Enum.GetValues(typeof(FBAudio.Cat))) d["FBAudio." + c] = FBAudio.Vol(c).ToString("0.###");
            return d;
        }
        static string Diff(Dictionary<string, string> a, Dictionary<string, string> b) => string.Join(", ", a.Keys.Where(k => a[k] != b[k]).Select(k => $"{k} {a[k]}→{b[k]}"));
        static void Set(string k, object v) { var f = F(k); f.SetValue(null, System.Convert.ChangeType(v, f.FieldType, System.Globalization.CultureInfo.InvariantCulture)); }
        static string Screen() => string.Join("|", FindObjectsByType<Text>(FindObjectsInactive.Exclude).Where(t => t.isActiveAndEnabled).Select(t => Path(t.transform) + "=" + t.text).OrderBy(s => s));
        static string Path(Transform t) { var s = t ? t.name : "?"; for (var p = t ? t.parent : null; p; p = p.parent) s = p.name + "/" + s; return s; }
        static Transform Panel() { var g = GameObject.Find("ajustes"); return g ? g.transform : null; }

        // valores-marca (nenhum é padrão) e os padrões do FBSettings.Defaults
        static readonly (string k, object v)[] Marks = { ("sens", .61f), ("adsSens", .67f), ("fov", .71f), ("aimAssist", "manual"), ("aimToggle", true), ("holdRun", true), ("invertY", true), ("freeLookToggle", true),
            ("master", .37f), ("sfx", .41f), ("music", .43f), ("voice", .47f), ("vehicles", .53f), ("ambient", .59f), ("blood", false) };
        const int MarkQ = 1;
        static readonly Dictionary<string, object>[] Defaults = {
            new() { ["sens"] = .5f, ["adsSens"] = .5f, ["fov"] = .375f, ["aimAssist"] = "assist", ["aimToggle"] = false, ["holdRun"] = false, ["invertY"] = false, ["freeLookToggle"] = false },
            new() { ["master"] = .9f, ["sfx"] = 1f, ["music"] = .5f, ["voice"] = 1f, ["vehicles"] = 1f, ["ambient"] = 1f },
            new() { ["blood"] = true } };
        static bool Eq(object a, object b) => a is float fa ? Mathf.Abs(fa - System.Convert.ToSingle(b)) < .002f : Equals(a, b);
        const string Bak = "fb_lab_ajustes_bak";

        // clique de verdade: raycast da UI no centro do controle, o alvo tem de ser ele (nada por cima), e down/up/click pelo EventSystem
        string Click(Selectable b)
        {
            var es = EventSystem.current; if (!es) return "sem EventSystem";
            var rt = (RectTransform)b.transform; var cv = b.GetComponentInParent<Canvas>().rootCanvas; var cam = cv.renderMode == RenderMode.ScreenSpaceOverlay ? null : cv.worldCamera;
            Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
            var ped = new PointerEventData(es) { position = sp, pressPosition = sp, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); es.RaycastAll(ped, hits);
            if (hits.Count == 0) return $"nada recebe clique no centro ({sp.x:0},{sp.y:0})";
            var h = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            if (h != b.gameObject) return "o centro do botão clica em " + Path((h ? h : hits[0].gameObject).transform);
            ped.pointerCurrentRaycast = hits[0]; ped.pointerPress = h; ped.rawPointerPress = hits[0].gameObject;
            ExecuteEvents.Execute(h, ped, ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(h, ped, ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(h, ped, ExecuteEvents.pointerClickHandler);
            return null;
        }
        static string Label(Selectable b)
        {
            var op = b.transform.parent && b.transform.parent.name.StartsWith("op_") ? b.transform.parent.name.Substring(3) + " " : b.name.StartsWith("op_") ? b.name.Substring(3) + " " : "";
            var t = b.GetComponentInChildren<Text>(); return (op + (b.name.StartsWith("seta") ? b.name : t ? t.text : b.name)).Trim();
        }

        IEnumerator Start()
        {
            yield return Wait(4);
            Log($"fase {fase} · {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            if (fase == "conferir") { yield return Conferir(); }
            else
            {
                var orig = State(); if (!PlayerPrefs.HasKey(Bak)) { PlayerPrefs.SetString(Bak, string.Join(";", Keys.Select(k => k + "=" + orig[k])) + ";q=" + FBQuality.Choice); PlayerPrefs.Save(); }
                yield return Interface(); yield return Som(); yield return Gravar();
            }
            Log($"RESUMO {fase}: {checks} verificações, {fails} falhas"); yield return Wait(1); Application.Quit();
        }

        // ---------------- interface: cada controle de cada aba
        IEnumerator Interface()
        {
            var nav = FindObjectsByType<Button>(FindObjectsInactive.Exclude).FirstOrDefault(b => b.GetComponentsInChildren<Text>().Any(t => t.text == "AJUSTES"));
            if (!nav) { Check(false, "botão AJUSTES do menu não achado"); yield break; }
            var why = Click(nav); Check(why == null, "botão AJUSTES do menu: " + why); yield return null; yield return null;
            var panel = Panel(); Check(panel, "AJUSTES pelo menu não abriu o painel"); if (!panel) yield break;
            var root = panel.parent;
            for (int tab = 0; tab < 3; tab++)
            {
                void Reopen() { var p = Panel(); if (p) DestroyImmediate(p.gameObject); FBSettings.Open(root, tab); }
                Reopen(); yield return null;
                int nb = Panel().GetComponentsInChildren<Button>().Length, ns = Panel().GetComponentsInChildren<Slider>().Length;
                Log($"aba {tab}: {nb} botões, {ns} sliders");
                for (int i = 0; i < nb; i++)
                {
                    Reopen(); yield return null;
                    var b = Panel().GetComponentsInChildren<Button>()[i]; var lab = Label(b);
                    bool restore = lab == "RESTAURAR PADRÕES", isTab = new[] { "CONTROLES", "ÁUDIO", "VÍDEO" }.Contains(lab), choice = b.name.StartsWith("op_") || b.transform.parent.name.StartsWith("op_");
                    if (restore) { foreach (var (k, v) in Marks) if (Defaults[tab].ContainsKey(k)) Set(k, v); if (tab == 2) FBQuality.Set(MarkQ); FBSession.Apply(); }
                    var s0 = State(); var v0 = Screen();
                    var w = Click(b); yield return null; yield return null;
                    var s1 = State(); var v1 = Screen(); string ds = Diff(s0, s1); bool scr = v0 != v1;
                    if (w != null) { Check(false, $"aba {tab} «{lab}»: {w}"); continue; }
                    if (isTab && lab == new[] { "CONTROLES", "ÁUDIO", "VÍDEO" }[tab]) { Log($"  «{lab}» (aba atual: sem ação, esperado) {(scr ? "tela mudou" : "")}"); continue; }
                    string res;
                    if (choice) { Check(ds != "", $"aba {tab} «{lab}»: seletor não mudou nenhum ajuste"); res = ds; }
                    else if (restore)
                    {
                        var bad = Defaults[tab].Where(kv => !Eq(F(kv.Key).GetValue(null), kv.Value)).Select(kv => kv.Key).ToList(); if (tab == 2 && FBQuality.Choice != -1) bad.Add("qualidade");
                        Check(bad.Count == 0, $"aba {tab} RESTAURAR PADRÕES deixou fora do padrão: {string.Join(",", bad)}"); res = ds;
                    }
                    else if (lab == "VOLTAR") { Check(!Panel(), "VOLTAR não fechou o painel"); res = Panel() ? "painel aberto" : "fechou o painel"; }
                    else { Check(scr || ds != "", $"aba {tab} «{lab}»: clique sem efeito"); res = ds != "" ? ds : scr ? "tela mudou" : "nada"; }
                    Log($"  «{lab}» -> {res}");
                }
                for (int i = 0; i < ns; i++)
                {
                    Reopen(); yield return null;
                    var s = Panel().GetComponentsInChildren<Slider>()[i]; var lab = s.name.StartsWith("s_") ? s.name.Substring(2) : s.name;
                    // alvo de clique (nada cobrindo o slider)
                    var es = EventSystem.current; var rt = (RectTransform)s.transform; Vector2 sp = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
                    var ped = new PointerEventData(es) { position = sp }; var hits = new List<RaycastResult>(); es.RaycastAll(ped, hits);
                    var hd = hits.Count > 0 ? ExecuteEvents.GetEventHandler<IPointerDownHandler>(hits[0].gameObject) : null;
                    Check(hd == s.gameObject, $"slider «{lab}»: o centro não pega no slider ({(hits.Count > 0 ? Path(hits[0].gameObject.transform) : "nada")})");
                    var key = lab switch { "SENSIBILIDADE" => "sens", "SENSIBILIDADE MIRANDO" => "adsSens", "CAMPO DE VISÃO" => "fov", "VOLUME GERAL" => "master", "EFEITOS" => "sfx", "MÚSICA" => "music", "VOZES" => "voice", "VEÍCULOS" => "vehicles", "AMBIENTE" => "ambient", _ => null };
                    if (key == null) { Check(false, $"slider «{lab}» desconhecido pelo laboratório"); continue; }
                    float nv = s.value > .5f ? .2f : .8f; var s0 = State();
                    s.value = nv; yield return null;                                  // = arrastar (onValueChanged)
                    var s1 = State(); float live = key switch
                    {
                        "sens" => FBInput.Sens, "adsSens" => FBInput.AdsSens, "fov" => FBSession.fov, "master" => AudioListener.volume, "sfx" => FBAudio.SfxVol, "music" => FBAudio.MusicVol,
                        "voice" => FBAudio.VoiceVol, "vehicles" => FBAudio.VehVol, _ => FBAudio.AmbVol
                    };
                    var pct = s.transform.parent.GetComponentsInChildren<Text>().Any(t => t.text == Mathf.RoundToInt(nv * 100) + "%");
                    Check(Mathf.Abs(live - nv) < .002f, $"slider «{lab}»: não aplicou na hora (valor em uso {live:0.###}, esperado {nv})");
                    Check(pct, $"slider «{lab}»: o % na tela não acompanhou");
                    ExecuteEvents.Execute(s.gameObject, new PointerEventData(es) { position = sp }, ExecuteEvents.pointerUpHandler); yield return null;   // soltar
                    float pref = PlayerPrefs.GetFloat("fb_" + key, -1);
                    Check(Mathf.Abs(pref - nv) < .002f, $"slider «{lab}»: soltar não gravou (PlayerPrefs fb_{key} = {pref:0.###})");
                    Log($"  slider «{lab}» {s0[key]}→{nv}: em uso {live:0.###} · gravado {pref:0.###} · {Diff(s0, s1)}");
                }
            }
            var pp = Panel(); if (pp) Destroy(pp.gameObject);
        }

        // ---------------- som de verdade no ouvinte
        static readonly float[] buf = new float[2048];
        IEnumerator Power(float secs, float[] r)
        {
            double sum = 0; int n = 0;
            for (float t = 0; t < secs; t += Time.unscaledDeltaTime) { for (int ch = 0; ch < 2; ch++) { AudioListener.GetOutputData(buf, ch); double a = 0; for (int i = 0; i < buf.Length; i++) a += buf[i] * buf[i]; sum += a / buf.Length; n++; } yield return null; }
            r[0] = (float)(sum / Mathf.Max(1, n));
        }
        static float Db(float p) => 10 * Mathf.Log10(Mathf.Max(1e-12f, p));
        static void Vols(float m, float sfx, float mus, float voi, float veh, float amb) { FBSession.master = m; FBSession.sfx = sfx; FBSession.music = mus; FBSession.voice = voi; FBSession.vehicles = veh; FBSession.ambient = amb; FBSession.Apply(); }
        static void Cat(FBAudio.Cat c, float v) { switch (c) { case FBAudio.Cat.Music: FBSession.music = v; break; case FBAudio.Cat.Voice: FBSession.voice = v; break; case FBAudio.Cat.Vehicle: FBSession.vehicles = v; break; case FBAudio.Cat.Ambient: FBSession.ambient = v; break; default: FBSession.sfx = v; break; } FBSession.Apply(); }
        static void Others(FBAudio.Cat c, float v) { foreach (FBAudio.Cat o in System.Enum.GetValues(typeof(FBAudio.Cat))) if (o != c) Cat(o, v); }

        IEnumerator Som()
        {
            var lis = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude).FirstOrDefault(l => l.enabled); if (!lis) { Check(false, "sem AudioListener no menu"); yield break; }
            var paused = FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude).Where(a => a.isPlaying).ToList(); foreach (var a in paused) a.Pause();
            const int SR = 44100; var tone = AudioClip.Create("lab_tom", SR * 6, 1, SR, false); var d = new float[SR * 6]; for (int i = 0; i < d.Length; i++) d[i] = .25f * Mathf.Sin(2 * Mathf.PI * 440 * i / SR); tone.SetData(d, 0);
            float[] r = new float[1]; Vols(1, 1, 1, 1, 1, 1); yield return Wait(.3f); yield return Power(.4f, r); float bg = r[0];
            Log($"fundo com tudo pausado: {Db(bg):0.0} dB");
            // tom 3D em cima do ouvinte, uma categoria por vez (fonte já tocando quando o volume muda)
            foreach (FBAudio.Cat c in System.Enum.GetValues(typeof(FBAudio.Cat)))
            {
                Vols(1, 1, 1, 1, 1, 1); var s = FBAudio.PlayAt(tone, lis.transform.position, 1, 4, 0, 0, 1.1f, c); if (!s) { Check(false, $"PlayAt {c} não tocou"); continue; }
                yield return Wait(.25f); yield return Power(.5f, r); float p1 = r[0];
                Cat(c, .25f); yield return Wait(.1f); yield return Power(.5f, r); float p2 = r[0];
                Cat(c, 1); Others(c, 0); yield return Wait(.1f); yield return Power(.5f, r); float p3 = r[0]; s.Stop();
                float d12 = Db(p2) - Db(p1), d13 = Db(p3) - Db(p1);
                Check(p1 > bg * 10, $"{c}: tom não saiu no ouvinte ({Db(p1):0.0} dB, fundo {Db(bg):0.0})");
                Check(Mathf.Abs(d12 + 12.04f) < 1.5f, $"{c}: categoria a 25 % mudou {d12:0.0} dB (esperado −12) na fonte já tocando");
                Check(Mathf.Abs(d13) < 1f, $"{c}: as outras categorias a 0 mexeram {d13:0.0} dB nele");
                Log($"  {c}: tom {Db(p1):0.0} dB · categoria 25 % {d12:+0.0;-0.0} dB · outras a 0 {d13:+0.0;-0.0} dB");
            }
            // volume geral = AudioListener.volume (o medidor pode ler antes ou depois dele: vale o valor e, se der, a medida)
            { Vols(1, 1, 1, 1, 1, 1); var s = FBAudio.PlayAt(tone, lis.transform.position, 1, 4, 0, 0, 1.1f, FBAudio.Cat.Sfx); yield return Wait(.25f); yield return Power(.5f, r); float p1 = r[0];
              FBSession.master = .5f; FBSession.Apply(); yield return Wait(.1f); yield return Power(.5f, r); float p2 = r[0]; s.Stop(); float dd = Db(p2) - Db(p1);
              Check(Mathf.Abs(AudioListener.volume - .5f) < .002f, $"GERAL: AudioListener.volume = {AudioListener.volume}");
              Log($"  GERAL 50 %: AudioListener.volume {AudioListener.volume:0.##} · saída medida {dd:+0.0;-0.0} dB {(Mathf.Abs(dd + 6.02f) < 1.5f ? "(−6 dB, ok)" : Mathf.Abs(dd) < .5f ? "(o medidor lê antes do volume do ouvinte)" : "")}");
              if (Mathf.Abs(dd) >= .5f) Check(Mathf.Abs(dd + 6.02f) < 1.5f, $"GERAL 50 %: saída mudou {dd:0.0} dB (esperado −6)"); }
            // loops (FBAudio.SetLoop: a categoria vem da chave) — 2D, volume suavizado em 80 ms pelo Tick
            foreach (var (key, clip, c) in new[] { ("menu_music", "music", FBAudio.Cat.Music), ("veh_lab", "reb_wind", FBAudio.Cat.Vehicle), ("amb_lab", "amb", FBAudio.Cat.Ambient), ("lab_efeito", "amb", FBAudio.Cat.Sfx) })
            {
                Vols(1, 1, 1, 1, 1, 1); FBAudio.StopLoop(key); FBAudio.SetLoop(key, clip, .5f);   // StopLoop: a música do menu estava pausada acima if (FBAudio.LoopClip(key) == null) { Check(false, $"loop {key} ({clip}) não criado"); continue; }
                yield return Wait(.6f); yield return Power(.6f, r); float p1 = r[0];
                Cat(c, .25f); yield return Wait(.5f); yield return Power(.6f, r); float p2 = r[0]; float lv = FBAudio.LoopLevel(key);
                FBAudio.StopLoop(key); float dd = Db(p2) - Db(p1);
                Check(Mathf.Abs(lv - .125f) < .002f, $"loop {key}: alvo {lv:0.###} (esperado 0,125)");
                Check(dd < -8, $"loop {key} ({c}): categoria a 25 % mudou {dd:0.0} dB na saída");
                Log($"  loop {key} ({c}): {Db(p1):0.0} dB · categoria 25 % {dd:+0.0;-0.0} dB · alvo {lv:0.###}");
            }
            // fonte própria de outro sistema registrada com FBAudio.Track
            {
                Vols(1, 1, 1, 1, 1, 1); var g = new GameObject("lab_track"); var src = g.AddComponent<AudioSource>(); src.clip = tone; src.loop = true; src.spatialBlend = 0; FBAudio.Track(src, .8f, FBAudio.Cat.Ambient); src.Play();
                yield return Wait(.25f); yield return Power(.5f, r); float p1 = r[0]; Cat(FBAudio.Cat.Ambient, .25f); yield return Wait(.1f); yield return Power(.5f, r); float p2 = r[0];
                float dd = Db(p2) - Db(p1); Check(Mathf.Abs(dd + 12.04f) < 1.5f, $"fonte registrada (Ambient): {dd:0.0} dB (esperado −12)"); Log($"  fonte registrada (Track, Ambient): categoria 25 % {dd:+0.0;-0.0} dB"); Destroy(g);
            }
            foreach (var a in paused) if (a) a.UnPause();
        }

        // ---------------- gravação: marca -> Save -> embaralha -> Load (ida e volta no disco); a 2ª execução confere no processo novo
        IEnumerator Gravar()
        {
            foreach (var (k, v) in Marks) Set(k, v); FBQuality.Set(MarkQ); FBSession.Save();
            foreach (var (k, v) in Marks) Set(k, v is float ? .11f : v is bool bv ? (object)!bv : "assist"); FBSession.Apply();
            FBSession.Load(); yield return null;
            Marcas("ida e volta (Save → Load)");
        }
        void Marcas(string when)
        {
            var bad = Marks.Where(m => !Eq(F(m.k).GetValue(null), m.v)).Select(m => $"{m.k}={F(m.k).GetValue(null)} (esperado {m.v})").ToList();
            if (FBQuality.Choice != MarkQ) bad.Add($"qualidade {FBQuality.Choice} (esperado {MarkQ})");
            if (Mathf.Abs(AudioListener.volume - .37f) > .002f) bad.Add($"AudioListener.volume {AudioListener.volume:0.###}");
            if (Mathf.Abs(FBAudio.SfxVol - .41f) > .002f || Mathf.Abs(FBAudio.MusicVol - .43f) > .002f || Mathf.Abs(FBAudio.VoiceVol - .47f) > .002f || Mathf.Abs(FBAudio.VehVol - .53f) > .002f || Mathf.Abs(FBAudio.AmbVol - .59f) > .002f)
                bad.Add($"FBAudio {FBAudio.SfxVol:0.##}/{FBAudio.MusicVol:0.##}/{FBAudio.VoiceVol:0.##}/{FBAudio.VehVol:0.##}/{FBAudio.AmbVol:0.##}");
            if (Mathf.Abs(FBInput.Sens - .61f) > .002f || Mathf.Abs(FBInput.AdsSens - .67f) > .002f || !FBInput.InvertY || !FBInput.AimToggle || FBFx.BloodOn) bad.Add("FBInput/FBFx não aplicados");
            Check(bad.Count == 0, $"{when}: {string.Join("; ", bad)}");
            Log($"{when}: {(bad.Count == 0 ? $"os {Marks.Length} ajustes + qualidade iguais e aplicados (som, ouvinte, controles, sangue)" : bad.Count + " diferenças")}");
        }
        IEnumerator Conferir()
        {
            yield return null;
            Marcas("processo novo (carregado pelo menu, sem o laboratório chamar Load)");
            // devolve o que o jogador tinha
            var bak = PlayerPrefs.GetString(Bak, "");
            if (bak == "") { Log("sem cópia dos ajustes originais (nada a devolver)"); yield break; }
            int q = -1; foreach (var kv in bak.Split(';').Select(x => x.Split('=')).Where(x => x.Length == 2)) { if (kv[0] == "q") int.TryParse(kv[1], out q); else if (F(kv[0]) != null) Set(kv[0], kv[1]); }
            FBQuality.Set(q); FBSession.Save(); PlayerPrefs.DeleteKey(Bak); PlayerPrefs.Save(); Log("ajustes originais devolvidos");
        }
    }
}
