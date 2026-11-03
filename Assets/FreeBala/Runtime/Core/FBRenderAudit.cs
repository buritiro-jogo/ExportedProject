// AUDITORIA DE RENDERIZAÇÃO (rodada 40, 27/10/2026 — "objetos aparecem do nada na frente do jogador, outros desaparecem estando perto"):
// FreeBala.exe -fbrender <mapa:modo>[,...] [pontos]
// Em pontos espalhados do mapa (navmesh), na altura do olho (1ª pessoa) e atrás/acima (3ª pessoa), 6 direções, renderiza a MESMA vista
// três vezes na câmera principal (só cenário: sem personagens/FP):
//   A = como o jogo roda (oclusão + LOD do nível), B = sem oclusão, C = "verdade" (sem oclusão, LOD 0 sempre, sem corte por tamanho).
// A×B = o que a oclusão (Umbra assada) corta errado; B×C = o que o LOD/corte por distância some ou troca. Loga % de pixels diferentes,
// o pior caso, e grava autoteste/render_<mapa>_<n>.png (A | C) dos piores. Resultado no log: [FreeBala][render].
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBRenderAudit : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbrender"); if (i < 0 || i + 1 >= a.Length) return;
            var g = new GameObject("FB_RenderAudit"); DontDestroyOnLoad(g); var t = g.AddComponent<FBRenderAudit>(); t.runs = a[i + 1].Split(',');
            if (i + 2 < a.Length && int.TryParse(a[i + 2], out var n)) t.points = n;
        }
        string[] runs; int points = 16; string dir;
        static void Log(string s) => Debug.Log("[FreeBala][render] " + s);
        IEnumerator Wait(float s) { for (float w = 0; w < s; w += Time.unscaledDeltaTime) yield return null; }

        IEnumerator Start()
        {
            dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"); System.IO.Directory.CreateDirectory(dir);
            yield return Wait(3);
            foreach (var run in runs)
            {
                var mm = run.Split(':'); string map = mm[0], mode = mm.Length > 1 ? mm[1] : "classico";
                GameData.Load(); FBSession.Load(); var keepMap = FBSession.map; var keepMode = FBSession.mode;
                FBSession.map = map; FBSession.mode = mode; FBSession.online = false; FBSession.view = "tps"; FBSession.Save();
                var op = SceneManager.LoadSceneAsync("Map_" + map); if (op == null) { Log($"{map}: cena não existe"); continue; }
                while (!op.isDone) yield return null; yield return Wait(5);
                FBMatch.God = true;
                yield return Shots(map);
                yield return Audit(map);
                FBMatch.God = false; FBSession.map = keepMap; FBSession.mode = keepMode; FBSession.Save();
            }
            Log("fim"); Application.Quit();
        }

        // ---- tiro inimigo audível (rodada 40): nível REAL na saída (pico de RMS em 0,35 s, menos o fundo) de um bot parado a 10/25/50/100 m,
        // comparado ao tiro do próprio jogador; e se a arma de cada um tem disparo próprio
        IEnumerator Shots(string map)
        {
            var me = FBMatch.Player; var bots = FBMatch.Bots.Where(b => b && b.alive && b.GetComponent<FBWeaponUser>()?.Cur != null).ToList();
            if (!me || bots.Count == 0) { Log($"{map}: tiros — sem jogador/bot armado"); yield break; }
            foreach (var b in FBMatch.Bots) if (b) { var bb = b.GetComponent<FBBot>(); if (bb) bb.enabled = false; }
            var bot = bots[0]; var bw = bot.GetComponent<FBWeaponUser>(); var mw = me.GetComponent<FBWeaponUser>();
            var buf = new float[1024];
            float Peak(float secs, float t0, out float floor)
            {   // devolvido por quem chama em quadros; aqui só lê um bloco
                floor = 0; float a = 0; AudioListener.GetOutputData(buf, 0); foreach (var x in buf) a += x * x; AudioListener.GetOutputData(buf, 1); foreach (var x in buf) a += x * x; return Mathf.Sqrt(a / (2 * buf.Length));
            }
            IEnumerator Measure(System.Action fire, System.Action<float> done)
            {
                float bg = 0; for (int i = 0; i < 12; i++) { bg += Peak(0, 0, out _); yield return null; } bg /= 12;
                fire(); float pk = 0; for (float t = 0; t < .35f; t += Time.unscaledDeltaTime) { pk = Mathf.Max(pk, Peak(0, 0, out _)); yield return null; }
                done(Mathf.Max(0, pk - bg)); yield return Wait(.6f);
            }
            float own = 0;
            if (mw && mw.Cur != null)
            {
                var cam = Camera.main; mw.RefillAll(3);
                yield return Measure(() => { if (mw.Fire(cam.transform.position, cam.transform.forward, default, out _)) { } }, v => own = v);
            }
            var line = new List<string>();
            foreach (var d in new[] { 10f, 25f, 50f, 100f })
            {
                var fwd = me.transform.forward.Flat().normalized; var at = me.Pos + fwd * d; var np = FBNav.RandomPoint(at, 6) ?? at;
                FBMatch.Teleport(bot, np, Quaternion.LookRotation(me.Pos - np).eulerAngles.y); yield return Wait(.3f); bw.RefillAll(3);
                float lvl = 0; yield return Measure(() => bw.Fire(bot.Pos + Vector3.up * 1.5f, (me.Pos + Vector3.up - (bot.Pos + Vector3.up * 1.5f)).normalized, default, out _, -99), v => lvl = v);
                line.Add($"{d:0} m {lvl:0.000} ({(own > 0 ? lvl / own * 100 : 0):0}% do próprio)");
            }
            Log($"{map}: tiro do jogador ({mw?.Cur?.id}) {own:0.000} · bot ({bw.Cur.id}) → " + string.Join(" · ", line));
            int distinct = GameData.Weapons.Values.Select(w => FBWeaponSound.Fire(w, false)).Distinct().Count();
            Log($"{map}: armas com disparo próprio {GameData.Weapons.Values.Count(w => FBWeaponSound.FireCount(w) > 0)}/{GameData.Weapons.Count} · sorteio de 1 disparo por arma deu {distinct} sons diferentes · sacar {FBWeaponSound.Draw(bw.Cur)} · longe {FBWeaponSound.Far(bw.Cur)}");
            // feedback visual (rodada 40): dano no jogador vindo do bot (vinheta de borda + arco de direção + tranco) e abate pelo jogador
            {
                string dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste"); bool god = FBMatch.God; FBMatch.God = false;
                me.hp = me.maxHp; var side = me.Pos + me.transform.right * 12; FBMatch.Teleport(bot, FBNav.RandomPoint(side, 4) ?? side, 0); yield return Wait(.3f);
                FBCombat.DamageActor(me, me.maxHp * .35f, false, bot); yield return null; yield return null;
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"render_{map}_dano.png")); yield return Wait(.4f);
                me.hp = me.maxHp * .2f; yield return Wait(.8f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"render_{map}_vidacritica.png")); yield return null;
                me.hp = me.maxHp; FBCombat.DamageActor(bot, bot.maxHp * 3, true, me); yield return null; yield return null;
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"render_{map}_abate.png")); yield return Wait(.5f);
                FBMatch.God = god; Log($"{map}: prints de dano/vida crítica/abate gravados");
            }
            foreach (var b in FBMatch.Bots) if (b) { var bb = b.GetComponent<FBBot>(); if (bb) bb.enabled = true; }
        }

        // caixa de visibilidade das malhas animadas: se a pose real sai da caixa, o frustum culling corta o personagem/veículo estando
        // na tela (some "do nada" perto da borda). Compara a malha assada (BakeMesh) com SkinnedMeshRenderer.bounds
        void SkinnedBounds(string map)
        {
            int n = 0, bad = 0; var worst = new List<string>(); var tmp = new Mesh();
            foreach (var smr in FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
            {
                if (!smr || !smr.enabled || !smr.gameObject.activeInHierarchy || !smr.sharedMesh) continue; n++;
                smr.BakeMesh(tmp, true); var v = tmp.vertices; if (v.Length == 0) continue; var M = smr.transform.localToWorldMatrix; var b = smr.bounds; int outN = 0; float far = 0;
                for (int i = 0; i < v.Length; i += 7) { var p = M.MultiplyPoint3x4(v[i]); if (!b.Contains(p)) { outN++; far = Mathf.Max(far, Mathf.Sqrt(b.SqrDistance(p))); } }
                if (far > .15f) { bad++; if (worst.Count < 8) worst.Add($"{smr.transform.root.name}/{smr.name} {far:0.00} m fora ({outN} pts) upd={smr.updateWhenOffscreen}"); }
            }
            Destroy(tmp);
            Log($"{map}: malhas animadas {n} · com pose fora da caixa (>15 cm) {bad}" + (worst.Count > 0 ? " · " + string.Join(" | ", worst) : ""));
        }

        struct Sample { public string name; public Vector3 pos; public Quaternion rot; public float ocDiff, lodDiff; public Texture2D a, c; }
        IEnumerator Audit(string map)
        {
            var cam = Camera.main; if (!cam) { Log($"{map}: sem câmera"); yield break; }
            var pl = FBPlayer.I; if (pl) pl.enabled = false;                                          // a câmera é nossa durante a auditoria
            int maskKeep = cam.cullingMask, maskScene = maskKeep & ~(1 << FBWorld.LChar) & ~(1 << FBWorld.LFirstPerson), maskAll = maskKeep & ~(1 << FBWorld.LFirstPerson);
            SkinnedBounds(map);
            bool ocKeep = cam.useOcclusionCulling; float lodKeep = QualitySettings.lodBias; int maxLodKeep = QualitySettings.maximumLODLevel;
            var pts = FBNav.Points(); if (pts == null || pts.Count == 0) { Log($"{map}: sem pontos de navegação"); yield break; }
            // pontos espalhados: os mais distantes entre si (farthest point sampling)
            var pick = new List<Vector3> { pts[0] };
            while (pick.Count < Mathf.Min(points, pts.Count)) pick.Add(pts.OrderByDescending(p => pick.Min(q => (p - q).sqrMagnitude)).First());
            var rt = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32) { name = "audit" }; rt.Create();
            Texture2D Grab()
            {
                var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
                var a = RenderTexture.active; RenderTexture.active = rt; var t = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); t.Apply(false); RenderTexture.active = a; return t;
            }
            static float Diff(Texture2D x, Texture2D y)
            {
                var X = x.GetPixels32(); var Y = y.GetPixels32(); int n = 0;
                for (int i = 0; i < X.Length; i++) { int d = Mathf.Abs(X[i].r - Y[i].r) + Mathf.Abs(X[i].g - Y[i].g) + Mathf.Abs(X[i].b - Y[i].b); if (d > 60) n++; }
                return 100f * n / X.Length;
            }
            var res = new List<Sample>(); float ocSum = 0, lodSum = 0; int n0 = 0;
            for (int pi = 0; pi < pick.Count; pi++)
            {
                var g = pick[pi];
                for (int v = 0; v < 2; v++)
                    for (int k = 0; k < 6; k++)
                    {
                        var fwd = Quaternion.Euler(0, k * 60 + pi * 17, 0) * Vector3.forward; Vector3 eye;
                        if (v == 0) eye = g + Vector3.up * 1.6f;                                     // 1ª pessoa
                        else { eye = g + Vector3.up * 2.4f - fwd * 3.2f; var head = g + Vector3.up * 1.7f; if (Physics.Linecast(head, eye, out var h, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)) eye = h.point + (head - eye).normalized * .3f; }   // 3ª pessoa (câmera não entra na parede)
                        var rot = Quaternion.LookRotation(Quaternion.AngleAxis(v == 0 ? 2 : 8, Vector3.Cross(fwd, Vector3.up)) * fwd);
                        cam.transform.SetPositionAndRotation(eye, rot);
                        QualitySettings.lodBias = lodKeep; QualitySettings.maximumLODLevel = maxLodKeep; yield return Wait(.65f);   // transições de LOD (0,5 s) assentarem
                        // A×B no MESMO quadro e com personagens/veículos (pose idêntica): o que a oclusão corta de verdade, inclusive dinâmicos
                        cam.cullingMask = maskAll; cam.useOcclusionCulling = true; var A = Grab(); cam.useOcclusionCulling = false; var B = Grab();
                        cam.cullingMask = maskScene; var B2 = Grab();
                        QualitySettings.lodBias = 50; QualitySettings.maximumLODLevel = 0; yield return Wait(.65f); var C = Grab();
                        var s = new Sample { name = $"{(v == 0 ? "fp" : "tps")} p{pi} d{k}", pos = eye, rot = rot, ocDiff = Diff(A, B), lodDiff = Diff(B2, C), a = A, c = C };
                        ocSum += s.ocDiff; lodSum += s.lodDiff; n0++; Destroy(B); Destroy(B2);
                        res.Add(s);
                        // guarda só os piores (memória)
                        var keep = res.OrderByDescending(x => Mathf.Max(x.ocDiff, x.lodDiff)).Take(6).ToList();
                        foreach (var x in res) if (!keep.Contains(x)) { if (x.a) Destroy(x.a); if (x.c) Destroy(x.c); }
                        res = keep;
                    }
            }
            Log($"{map}: {n0} vistas · oclusão corta (A×B) média {ocSum / Mathf.Max(1, n0):0.00}% · LOD/corte (B×C) média {lodSum / Mathf.Max(1, n0):0.00}% · bias {lodKeep:0.00} maxLOD {maxLodKeep} · oclusão na câmera {ocKeep}");
            int idx = 0;
            foreach (var s in res)
            {
                Log($"{map}: pior {idx}: {s.name} em {s.pos} · oclusão {s.ocDiff:0.0}% · LOD {s.lodDiff:0.0}%");
                var comp = new Texture2D(s.a.width * 2 + 4, s.a.height, TextureFormat.RGB24, false); var fill = Enumerable.Repeat(new Color32(255, 0, 255, 255), comp.width * comp.height).ToArray(); comp.SetPixels32(fill);
                comp.SetPixels(0, 0, s.a.width, s.a.height, s.a.GetPixels()); comp.SetPixels(s.a.width + 4, 0, s.c.width, s.c.height, s.c.GetPixels()); comp.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, $"render_{map}_{idx++}.png"), comp.EncodeToPNG()); Destroy(comp);
            }
            cam.useOcclusionCulling = ocKeep; cam.cullingMask = maskKeep; QualitySettings.lodBias = lodKeep; QualitySettings.maximumLODLevel = maxLodKeep; rt.Release();
            if (pl) pl.enabled = true;
        }
    }
}
