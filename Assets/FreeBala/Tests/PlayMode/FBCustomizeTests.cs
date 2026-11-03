// Tela de personagem (FBCustomize): abre pelo lobby, troca personagem/peças/tom/cor, confere que o modelo muda NA HORA (mesma instância,
// sem recarregar o prefab), que reaplicar não acumula cor, salva (USAR), fecha, "reabre o jogo" (recarrega PlayerPrefs e a cena) e confere
// a persistência e que o Animator continua tocando. Também confere miniaturas, filtros/busca e um NPC sem ficha (cor do cabelo).
// As preferências do jogador são guardadas antes e devolvidas no fim. Prints em unity/logs/print_personagem_*.png.
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FreeBala.Tests
{
    public class FBCustomizeTests
    {
        static readonly string[] KEYS = { "fb_skin", "fb_look", "fb_looks", "fb_favs", "fb_pickRandom" };
        static IEnumerator Wait(float s) { for (float t = 0; t < s; t += Time.unscaledDeltaTime) yield return null; }
        static IEnumerator OpenMenu()
        {
            var op = SceneManager.LoadSceneAsync("Menu"); while (!op.isDone) yield return null;
            yield return Wait(1.5f); Assert.IsNotNull(FBMenu.I, "lobby não abriu");
            FBMenu.I.Characters(); yield return Wait(.6f);
            Assert.IsNotNull(FBCustomize.I, "tela de personagem não abriu"); FBCustomizeFx.Finish();
        }
        static void Print(string n)
        {
            var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../logs")); System.IO.Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "print_personagem_" + n + ".png"));
        }
        // o Animator está tocando: ligado, com controlador, e o tempo do estado ou a pose dos ossos anda
        static IEnumerator AssertAnimating(GameObject go, string tag)
        {
            var an = go.GetComponent<Animator>(); Assert.IsNotNull(an, tag + ": sem Animator"); Assert.IsTrue(an.isActiveAndEnabled, tag + ": Animator desligado");
            Assert.IsNotNull(an.runtimeAnimatorController, tag + ": sem controlador");
            var bone = an.isHuman ? an.GetBoneTransform(HumanBodyBones.Spine) ?? an.GetBoneTransform(HumanBodyBones.Hips) : null;
            float n0 = an.GetCurrentAnimatorStateInfo(0).normalizedTime; var r0 = bone ? bone.localRotation : Quaternion.identity; float moved = 0;
            for (float t = 0; t < 1.2f; t += Time.deltaTime) { yield return null; if (bone) moved = Mathf.Max(moved, Quaternion.Angle(r0, bone.localRotation)); }
            float n1 = an.GetCurrentAnimatorStateInfo(0).normalizedTime;
            Assert.IsTrue(n1 > n0 + .01f || moved > .05f, $"{tag}: Animator parado (tempo {n0:0.000}→{n1:0.000}, osso {moved:0.000}°)");
        }
        static Texture Tex(Material m) { if (!m) return null; foreach (var n in new[] { "_BaseMap", "baseColorTexture", "_MainTex" }) if (m.HasProperty(n) && m.GetTexture(n)) return m.GetTexture(n); return null; }
        static Color Col(Renderer r) { var m = r.sharedMaterial; return !m ? Color.clear : m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("baseColorFactor") ? m.GetColor("baseColorFactor") : m.color; }

        [UnityTest, Timeout(240000)]
        public IEnumerator Personagem_TrocaSalvaReabre()
        {
            var saved = KEYS.ToDictionary(k => k, k => PlayerPrefs.HasKey(k) ? (k == "fb_pickRandom" ? PlayerPrefs.GetInt(k).ToString() : PlayerPrefs.GetString(k)) : null);
            try
            {
                FBSession.Load(); FBSession.pickRandom = false; FBSession.Save(); PlayerPrefs.DeleteKey("fb_looks"); FBCustomizeData.Reload();
                yield return OpenMenu(); var C = FBCustomize.I;
                // roster e grade
                int n = FBCustomizeData.Roster.Count; Assert.Greater(n, 60, "roster pequeno demais"); Debug.Log($"[FreeBala][teste] personagens no roster: {n} ({FBCustomizeData.Roster.Count(e => e.fem)} F, {FBCustomizeData.Roster.Count(e => e.npc)} NPC)");
                Assert.IsNotNull(GameObject.Find("Previa"), "sem personagem no palco");
                // filtros e busca
                C.sexF = "f"; C.SetTab("look"); C.SetTab("chars"); Assert.IsTrue(FBCustomizeData.Filter("f", FBCustomizeData.Origin.All, "").All(e => e.fem), "filtro feminino");
                Assert.IsTrue(FBCustomizeData.Filter("all", FBCustomizeData.Origin.All, "BANDIDO").Any(e => e.id == "ie_bandido") || !FBRegistry.I.HasChar("ie_bandido"), "busca sem acento/maiúscula");
                C.sexF = "all";
                // personagem com ficha (IE): troca de peça, tom e cor
                var ie = FBCustomizeData.Roster.FirstOrDefault(e => e.id == "ie_bandido") ?? FBCustomizeData.Roster.FirstOrDefault(e => e.id.StartsWith("ie_") && e.def.sheet);
                Assert.IsNotNull(ie, "nenhum personagem IE com guarda-roupa"); C.Select(ie.id); yield return Wait(.5f);
                var model = C.stage.model; var w = C.stage.wardrobe; Assert.IsTrue(w && w.HasSheet, "IE sem ficha");
                var L = w.look.Clone(); int part = Enumerable.Range(0, w.Parts.Count).FirstOrDefault(i => w.Parts[i].cat == "top" && !L.on.Contains(i) && w.Parts[i].tr);
                Assert.IsNotNull(w.Parts[part].tr, "peça sem nó"); w.Toggle(L, part); L.tone = 3; L.tint["top"] = 4;
                var body = w.Parts.FirstOrDefault(p => p.cat == "body" && p.tr && p.tr.GetComponentInChildren<Renderer>(true));
                var br = body?.tr.GetComponentInChildren<Renderer>(true); var c0 = br ? Col(br) : Color.clear;
                C.Commit(L); yield return null;
                Assert.AreSame(model, C.stage.model, "o prefab foi recarregado (devia trocar no mesmo modelo)");
                Assert.IsTrue(w.Parts[part].tr.gameObject.activeSelf, "peça escolhida não apareceu");
                var code = w.current; Assert.IsFalse(string.IsNullOrEmpty(code), "sem código de visual");
                if (br) { var c1 = Col(br); Assert.AreNotEqual(c0, c1, "tom de pele não mudou"); C.Commit(w.Decode(code)); yield return null; Assert.AreEqual(c1, Col(br), "reaplicar o mesmo visual acumulou cor"); }
                Print("ie_visual"); yield return AssertAnimating(model, "depois da troca");
                // USAR = personagem fixo + visual na sessão (vai para a partida e para a rede)
                C.Use(); Assert.AreEqual(ie.id, FBSession.skin); Assert.AreEqual(code, FBSession.look); Assert.AreEqual(ie.id + "~" + code, FBSession.FullSkin); Assert.IsFalse(FBSession.pickRandom);
                bool fav = FBCustomizeData.ToggleFav(ie.id); Assert.IsTrue(fav, "favorito");
                // miniaturas da grade
                for (float t = 0; t < 8 && !FBCustomizeData.Roster.Any(e => FBCustomizeThumbs.Cached(e.id)); t += Time.unscaledDeltaTime) yield return null;
                Assert.IsTrue(FBCustomizeData.Roster.Any(e => FBCustomizeThumbs.Cached(e.id)), "nenhuma miniatura renderizada");
                var mini = FBCustomizeThumbs.Get(FBCustomizeData.Roster.First(e => FBCustomizeThumbs.Cached(e.id)).id); Assert.AreEqual(FBCustomizeThumbs.W, mini.width);
                // NPC sem ficha: cor do cabelo pelos materiais
                var npc = FBCustomizeData.Roster.FirstOrDefault(e => e.id.StartsWith("n_"));
                if (npc != null)
                {
                    C.Select(npc.id); yield return Wait(.3f); var nw = C.stage.wardrobe;
                    if (nw && nw.HasHairMats) { var NL = nw.look ?? nw.Default(); NL.tint["hair"] = 4; C.Commit(NL); Assert.AreEqual(nw.Encode(NL), FBCustomizeData.LookFor(npc.id), "visual do NPC não salvou"); }
                    Debug.Log($"[FreeBala][teste] NPC {npc.id}: personalizável {(nw ? nw.Customizable : false)} · cabelo {(nw ? nw.HasHairMats : false)} · pele {(nw ? nw.HasSkinMats : false)}");
                    yield return AssertAnimating(C.stage.model, "NPC");
                    C.Select(ie.id); yield return Wait(.3f);
                }
                // atlas único (c_*/p_*) com máscara de pele: tom troca a textura SÓ desta instância, o prefab/material compartilhado fica intacto
                var atl = FBCustomizeData.Roster.FirstOrDefault(e => e.id == "c_helena") ?? FBCustomizeData.Roster.FirstOrDefault(e => e.id.StartsWith("p_"));
                if (atl != null)
                {
                    C.Select(atl.id); yield return Wait(.3f); var aw = C.stage.wardrobe;
                    Assert.IsTrue(aw && aw.HasSkinMask, $"{atl.id}: sem máscara de pele"); C.SetTab("look"); Assert.AreEqual("pele", C.group, "aba PELE não abriu sozinha");
                    var smr = C.stage.model.GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.sharedMaterials.Any(m => Tex(m)));
                    var shared = FBRegistry.I.Char(atl.id).GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.name == smr.name).sharedMaterials.Select(Tex).ToArray();
                    var AL = aw.look ?? aw.Default(); AL.tone = 4; var t0 = Time.realtimeSinceStartup; C.Commit(AL); float ms = (Time.realtimeSinceStartup - t0) * 1000;
                    Assert.IsTrue(C.stage.model.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(r => r.sharedMaterials).Any(m => Tex(m) && Tex(m).name.EndsWith("_tom4")), "textura com o tom não foi aplicada");
                    Assert.IsTrue(FBRegistry.I.Char(atl.id).GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.name == smr.name).sharedMaterials.Select(Tex).SequenceEqual(shared), "o prefab foi alterado");
                    Assert.AreEqual(aw.Encode(AL), FBCustomizeData.LookFor(atl.id), "tom do atlas não salvou");
                    Debug.Log($"[FreeBala][teste] tom por máscara em {atl.id}: {ms:0} ms na 1ª vez"); Print("atlas_tom"); yield return AssertAnimating(C.stage.model, "atlas com tom");
                    AL.tone = -1; C.Commit(AL); C.SetTab("chars"); C.Select(ie.id); yield return Wait(.3f);
                }
                // fecha: volta ao lobby com o personagem escolhido
                C.Close(); yield return Wait(.8f); Assert.IsNull(FBCustomize.I, "tela não fechou");
                var pv = GameObject.Find("Previa"); Assert.IsNotNull(pv, "lobby sem personagem"); Assert.AreEqual(ie.id, pv.GetComponent<FBCharacter>()?.skinId ?? ie.id);
                // "reabre o jogo": relê PlayerPrefs, recarrega a cena e a tela
                FBSession.skin = "x"; FBSession.look = ""; FBCustomizeData.Reload();
                Assert.AreEqual(ie.id, FBSession.skin, "personagem não persistiu"); Assert.AreEqual(code, FBSession.look, "visual não persistiu");
                Assert.IsTrue(FBCustomizeData.IsFav(ie.id), "favorito não persistiu");
                yield return OpenMenu(); C = FBCustomize.I; yield return Wait(.4f);
                Assert.AreEqual(ie.id, C.sel, "não abriu no personagem salvo"); w = C.stage.wardrobe;
                Assert.AreEqual(code, w.current, "visual restaurado diferente"); Assert.IsTrue(w.Parts[part].tr.gameObject.activeSelf, "peça salva não voltou"); Assert.AreEqual(3, w.look.tone);
                Print("reaberto"); yield return AssertAnimating(C.stage.model, "depois de reabrir");
                C.Close(); yield return Wait(.6f);
            }
            finally
            {
                foreach (var kv in saved) { if (kv.Value == null) PlayerPrefs.DeleteKey(kv.Key); else if (kv.Key == "fb_pickRandom") PlayerPrefs.SetInt(kv.Key, int.Parse(kv.Value)); else PlayerPrefs.SetString(kv.Key, kv.Value); }
                PlayerPrefs.Save(); FBCustomizeData.Reload();
            }
        }

        // layout responsivo: a vitrine cabe no retângulo livre em cada proporção (16:9, 21:9, 4:3, retrato) e o zoom respeita os limites
        [Test]
        public void Vitrine_EnquadramentoELimites()
        {
            var go = new GameObject("cam"); var cam = go.AddComponent<Camera>(); cam.fieldOfView = 30;
            var st = FBCustomizeStage.Create(cam);
            try
            {
                foreach (var (w, h) in new[] { (1920, 1080), (2560, 1080), (1600, 1200), (1080, 1920) })
                {
                    cam.aspect = (float)w / h; var a = FBCustomizeFx.Classify(w, h);
                    st.view = a == FBCustomizeFx.Aspect.Portrait ? new Rect(.08f, .54f, .84f, .38f) : new Rect(.3f, .06f, .45f, .84f);
                    foreach (FBCustomizeStage.Frame f in System.Enum.GetValues(typeof(FBCustomizeStage.Frame)))
                    {
                        st.SetFrame(f); st.zoom = 1; st.Target(out var p, out var r, out var d);
                        Assert.Greater(p.y, .1f, $"{a}/{f}: câmera abaixo do chão"); Assert.Less(d, 12.01f); Assert.Greater(d, .54f);
                        // o centro do enquadramento cai no centro do retângulo livre (no chão a câmera é travada: tolerância maior na vertical)
                        go.transform.SetPositionAndRotation(p, r); var vp = cam.WorldToViewportPoint(st.center);
                        Assert.AreEqual(st.view.center.x, vp.x, .02f, $"{a}/{f}: fora do centro livre (x)"); Assert.AreEqual(st.view.center.y, vp.y, .2f, $"{a}/{f}: fora do centro livre (y)");
                    }
                }
                st.SetFrame(FBCustomizeStage.Frame.Full); st.Pan(-1e6f); Assert.LessOrEqual(Mathf.Abs(st.pan), 1.6f, "pan sem limite"); st.ResetView(); Assert.AreEqual(0, st.pan, 1e-4);
                st.Pinch(100); Assert.AreEqual(FBCustomizeStage.ZoomMax, st.zoom, 1e-4); st.Pinch(.0001f); Assert.AreEqual(FBCustomizeStage.ZoomMin, st.zoom, 1e-4);
                st.zoom = 1; st.Zoom(50); Assert.AreEqual(FBCustomizeStage.ZoomMax, st.zoom, 1e-4); st.Zoom(-80); Assert.AreEqual(FBCustomizeStage.ZoomMin, st.zoom, 1e-4);
                Assert.AreEqual(FBCustomizeFx.Aspect.Ultra, FBCustomizeFx.Classify(2560, 1080)); Assert.AreEqual(FBCustomizeFx.Aspect.Wide, FBCustomizeFx.Classify(1920, 1080));
                Assert.AreEqual(FBCustomizeFx.Aspect.Narrow, FBCustomizeFx.Classify(1024, 768)); Assert.AreEqual(FBCustomizeFx.Aspect.Portrait, FBCustomizeFx.Classify(1080, 1920));
            }
            finally { Object.DestroyImmediate(st.gameObject); Object.DestroyImmediate(go); }
        }

        // código do visual = o mesmo formato do wardrobe.js (rede web ↔ Unity)
        [Test]
        public void CodigoDoVisual_IgualAoWeb()
        {
            var go = new GameObject("w"); var w = go.AddComponent<FBWardrobe>();
            w.sheetJson = "{\"cats\":[[\"body\",\"Corpo\"],[\"top\",\"Camisa\"]],\"parts\":[{\"node\":\"a\",\"cat\":\"body\",\"on\":true},{\"node\":\"b\",\"cat\":\"top\",\"on\":true},{\"node\":\"c\",\"cat\":\"top\",\"on\":false},{\"node\":\"d\",\"cat\":\"acc\",\"on\":false},{\"node\":\"e\",\"cat\":\"acc\",\"on\":true}]}";
            try
            {
                var L = w.Default(); Assert.AreEqual("21.-1", w.Encode(L));               // peças 1 e 4 ligadas: 4 bits por dígito → 0..3 = 0x2, 4..7 = 0x1
                w.Toggle(L, 2); Assert.IsFalse(L.on.Contains(1), "camisa empilhou"); L.tone = 2; L.tint["top"] = 3;
                var code = w.Encode(L); var back = w.Decode(code); Assert.AreEqual(code, w.Encode(back)); Assert.AreEqual(2, back.tone); Assert.AreEqual(3, back.Tint("top"));
                Assert.IsTrue(w.IsDefault(w.Decode("")));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
