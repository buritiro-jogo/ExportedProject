// Vitrine 3D da tela de personagem: o personagem no palco do lobby (mesma origem/fundo), câmera de apresentação que enquadra pelo
// ESQUELETO (corpo inteiro, tronco, rosto, pernas, pés — cada aba do guarda-roupa pede um enquadramento) dentro do retângulo livre da
// tela (entre os painéis no 16:9/21:9, metade de cima no retrato), girar arrastando (mouse ou dedo) com inércia e volta suave para a
// frente quando larga, zoom pela roda/pinça com limites, luz de estúdio (preenchimento que acompanha a câmera, recortes dourados do
// lobby) e animação (valores do bl_CharacterRotator do IE: suavização 0,1 s, frente em 180°, reset 0,3 s): parado relaxado + diretor do lobby (olhar, emote curto), pose ao confirmar, dança no clique duplo.
using UnityEngine;
using UnityEngine.EventSystems;

namespace FreeBala
{
    public class FBCustomizeStage : MonoBehaviour
    {
        public enum Frame { Full, Torso, Face, Legs, Feet, Bust }
        public Camera cam; public GameObject model; public FBCharacter ch; public FBWardrobe wardrobe; public string id;
        /// <summary>Retângulo da viewport (0..1) onde o personagem deve caber (o layout responsivo define).</summary>
        public Rect view = new(.3f, .08f, .4f, .8f);
        public Frame frame = Frame.Full;
        public float yaw = 180, yawVel, zoom = 1; float shownYaw = 180, shownVel, resetT; public const float ZoomMin = .7f, ZoomMax = 2.6f;
        float idleT, poseT, hHead = 1.6f, hHips = .95f, hKnee = .5f, measureT; bool dragging; Vector3 camVel; Quaternion camRot;
        Light key, rim, rim2, fill; float keyI, rimI, rim2I, fillI, fillR; Vector3 fillP; CanvasGroup floorGlow;

        public static FBCustomizeStage Create(Camera cam)
        {
            var st = new GameObject("FB_Vitrine").AddComponent<FBCustomizeStage>(); st.cam = cam; st.camRot = cam ? cam.transform.rotation : Quaternion.identity;
            st.Grab(ref st.key, "LuzChave", ref st.keyI); st.Grab(ref st.rim, "Recorte", ref st.rimI); st.Grab(ref st.rim2, "Recorte2", ref st.rim2I); st.Grab(ref st.fill, "Preenchimento", ref st.fillI);
            if (st.fill) { st.fillP = st.fill.transform.position; st.fillR = st.fill.range; }
            var fl = GameObject.Find("LobbyFundo/luz_chao"); if (fl) st.floorGlow = FBCustomizeFx.Group(fl.transform);
            return st;
        }
        void Grab(ref Light l, string n, ref float i) { var g = GameObject.Find(n); l = g ? g.GetComponent<Light>() : null; if (l) i = l.intensity; }
        void OnDestroy()
        {
            if (model) Destroy(model);
            if (key) key.intensity = keyI; if (rim) rim.intensity = rimI; if (rim2) rim2.intensity = rim2I;
            if (fill) { fill.intensity = fillI; fill.range = fillR; fill.transform.position = fillP; }
            if (floorGlow) floorGlow.alpha = 1;
        }

        // ---- personagem
        /// <summary>Troca o personagem do palco (o anterior sai) e aplica o visual salvo. Com a arma principal na mão (idle de fuzil do IE).</summary>
        public bool Show(string skinId, string look)
        {
            var prefab = FBRegistry.I ? FBRegistry.I.Char(skinId) : null; if (!prefab) return false;
            if (model) Destroy(model);
            model = Instantiate(prefab, Vector3.zero, Quaternion.Euler(0, yaw, 0)); model.name = "Previa"; id = skinId;
            ch = model.GetComponent<FBCharacter>(); if (ch) { ch.relaxed = true; ch.isPlayer = false; ch.skinId = skinId; }   // r43: IsFemale lê o skinId
            // com a arma principal na mão (pedido de 21/10/2026: a pose desarmada de braços abertos ficava feia) — igual à vitrine do lobby
            if (ch && GameData.Weapons.TryGetValue(FBSession.primary, out var wd)) ch.SetWeapon(wd, FBRegistry.I.Weapon(wd.id));
            var an = model.GetComponent<Animator>(); if (an) an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            wardrobe = FBWardrobe.For(model, skinId); if (wardrobe && wardrobe.Customizable) wardrobe.Apply(look); else if (wardrobe && !wardrobe.HasSheet) { Destroy(wardrobe); wardrobe = null; }
            var ld = model.AddComponent<FBLobbyDirector>(); ld.cam = cam;
            FBCustomizeFx.Pop(model.transform, .86f, .35f, 0, model.transform.localScale);
            measureT = .25f; poseT = 0; return true;
        }
        /// <summary>Alturas reais do esqueleto (cabeça, quadril, joelho) para o enquadramento; fallback pela caixa das malhas.</summary>
        void Measure()
        {
            if (!model) return; var an = model.GetComponent<Animator>(); float y0 = model.transform.position.y, s = model.transform.localScale.y;
            float B(HumanBodyBones b, float d) { var t = an && an.isHuman ? an.GetBoneTransform(b) : null; return t ? (t.position.y - y0) / Mathf.Max(.01f, s) : d; }
            var rs = model.GetComponentsInChildren<Renderer>(); float top = 1.75f; if (rs.Length > 0) { var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds); top = (bb.max.y - y0) / Mathf.Max(.01f, s); }
            hHead = B(HumanBodyBones.Head, top * .9f); hHips = B(HumanBodyBones.Hips, top * .53f); hKnee = B(HumanBodyBones.LeftLowerLeg, top * .28f);
            if (hHead < .5f || hHead > 3) hHead = 1.6f;
        }

        // ---- entrada (chamada pelo FBCustomizeStageInput)
        public void Drag(float dxPixels) { float d = dxPixels / Mathf.Max(1, Screen.width) * 540; yaw += d; yawVel = Mathf.Lerp(yawVel, d / Mathf.Max(Time.unscaledDeltaTime, .008f), .5f); idleT = 0; dragging = true; }
        public void EndDrag() { dragging = false; }
        public void Zoom(float steps) { zoom = Mathf.Clamp(zoom * Mathf.Pow(1.15f, steps), ZoomMin, ZoomMax); idleT = 0; }
        public void SetFrame(Frame f) { if (frame != f) { frame = f; zoom = 1; pan = 0; } }
        /// <summary>Pan vertical em torno do ponto de referência (UIAvatarInputHandler do FF): dois dedos ou botão direito/do meio.</summary>
        public void Pan(float dyPixels) { float h = Mathf.Max(.3f, hHead * .45f); pan = Mathf.Clamp(pan - dyPixels / Mathf.Max(1, Screen.height) * 2.2f / zoom, -h, h); idleT = 0; }
        /// <summary>Pinça: razão entre a distância atual e a anterior dos dois dedos.</summary>
        public void Pinch(float ratio) { if (ratio > 0) zoom = Mathf.Clamp(zoom * ratio, ZoomMin, ZoomMax); dragging = false; idleT = 0; }
        [System.NonSerialized] public float pan;
        public void ResetView() { zoom = 1; pan = 0; yawVel = 0; resetT = .3f; }                 // volta à frente em 0,3 s (IE)

        /// <summary>Pose curta ao confirmar (emote de pose do FF; sem pose disponível, a dança da vitória).</summary>
        public void PlayConfirm()
        {
            if (!ch) return; yawVel = 0; idleT = 99;
            string clip = FBEmotes.ConfirmPose(ch.IsFemale); if (clip == null) return;   // r42: era sempre NPC_Posing_F (1.º "pose" do catálogo)
            var ld = model.GetComponent<FBLobbyDirector>(); if (ld) ld.home = false;
            if (ch.PlayDance(clip)) poseT = Mathf.Min(FBRegistry.I.Clip(clip).length, 6);
        }
        public void PlayDance() { var d = ch ? FBEmotes.RandomDance(ch.IsFemale) : null; if (d == null || !ch) return; var ld = model.GetComponent<FBLobbyDirector>(); if (ld) ld.home = false; if (ch.PlayDance(d)) poseT = 8; }

        // [parceiro] v1 (thread de física): tempo do Update medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_Update = FBTempo.Id("FBCustomizeStage.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            float dt = Time.unscaledDeltaTime;
            if (measureT > 0 && (measureT -= dt) <= 0) Measure();
            if (poseT > 0 && (poseT -= dt) <= 0 && ch) { ch.StopDance(true); var ld = model ? model.GetComponent<FBLobbyDirector>() : null; if (ld) ld.home = true; }
            // giro com inércia; parado, volta devagar para a frente
            if (!dragging)
            {
                yaw += yawVel * dt; yawVel *= Mathf.Exp(-3.5f * dt); if (Mathf.Abs(yawVel) < 2) yawVel = 0;
                if ((idleT += dt) > 3.5f && yawVel == 0) yaw = Mathf.LerpAngle(yaw, 180, 1 - Mathf.Exp(-1.2f * dt));
            }
            if (resetT > 0) { resetT -= dt; yaw = Mathf.LerpAngle(yaw, 180, 1 - Mathf.Exp(-14 * dt)); if (resetT <= 0) yaw = 180; }
            shownYaw = Mathf.SmoothDampAngle(shownYaw, yaw, ref shownVel, .1f, Mathf.Infinity, dt);   // suavização de 0,1 s do rotator do IE
            if (model) model.transform.rotation = Quaternion.Euler(0, shownYaw, 0);
        }

        // ---- câmera de apresentação
        void Bounds(out float lo, out float hi, out float halfW, out float pitch)
        {
            switch (frame)
            {
                case Frame.Face: lo = hHead - .3f; hi = hHead + .26f; halfW = .24f; pitch = 2; break;
                case Frame.Bust: lo = hHips + (hHead - hHips) * .35f; hi = hHead + .28f; halfW = .36f; pitch = 3; break;   // busto: cabeça, ombros e peito (pele/cabelo)
                case Frame.Torso: lo = hHips - .12f; hi = hHead + .22f; halfW = .42f; pitch = 4; break;
                case Frame.Legs: lo = -.02f; hi = hHips + .18f; halfW = .3f; pitch = 9; break;
                case Frame.Feet: lo = -.05f; hi = hKnee + .1f; halfW = .25f; pitch = 16; break;
                default: lo = -.04f; hi = hHead + .3f; halfW = .5f; pitch = 3; break;
            }
        }
        /// <summary>Posição/rotação ideais da câmera para o enquadramento atual (o teste usa para conferir os limites).</summary>
        [System.NonSerialized] public Vector3 center;                           // centro do último enquadramento (teste)
        public void Target(out Vector3 pos, out Quaternion rot, out float dist)
        {
            Bounds(out var lo, out var hi, out var halfW, out var pitch);
            float s = model ? model.transform.localScale.y : 1; lo *= s; hi *= s; halfW *= s;
            var c = center = new Vector3(0, (lo + hi) / 2 + pan * s, 0); float halfH = (hi - lo) / 2;
            float fov = cam ? cam.fieldOfView : 30, asp = cam ? cam.aspect : 16f / 9, tv = Mathf.Tan(fov * .5f * Mathf.Deg2Rad), th = tv * asp;
            dist = Mathf.Max(halfH / (tv * Mathf.Max(.1f, view.height)), halfW / (th * Mathf.Max(.1f, view.width))) / zoom;
            dist = Mathf.Clamp(dist, .55f, 12);
            rot = Quaternion.Euler(pitch, 0, 0); var fwd = rot * Vector3.forward; var right = rot * Vector3.right; var up = rot * Vector3.up;
            float ox = (view.center.x - .5f) * 2, oy = (view.center.y - .5f) * 2;
            pos = c - fwd * dist - right * (ox * dist * th) - up * (oy * dist * tv);
            if (pos.y < .15f) pos.y = .15f;                                       // nunca abaixo do chão
        }
        // [parceiro] v1 (thread de física): tempo do LateUpdate medido pelo FBTempo
        static readonly int __fbt_LateUpdate = FBTempo.Id("FBCustomizeStage.LateUpdate");
        void LateUpdate()
        {
            if (!FBTempo.On) { LateUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
        }
        void LateUpdate__fbt()
        {
            if (!cam) return; float dt = Time.unscaledDeltaTime;
            Target(out var pos, out var rot, out var dist);
            cam.transform.position = Vector3.SmoothDamp(cam.transform.position, pos, ref camVel, .22f, Mathf.Infinity, dt);
            camRot = Quaternion.Slerp(cam.transform.rotation, rot, 1 - Mathf.Exp(-9 * dt)); cam.transform.rotation = camRot;
            // estúdio: preenchimento acompanha a câmera (rosto bem iluminado no close), chave mais forte de perto, brilho do chão só no corpo inteiro
            float close = Mathf.InverseLerp(6, 1, dist);
            if (fill) { fill.transform.position = cam.transform.position + cam.transform.right * .7f + cam.transform.up * .5f; fill.range = dist + 4; fill.intensity = fillI * Mathf.Lerp(Mathf.Clamp(dist * dist / 5, .6f, 6), 1, .3f); }
            if (key) key.intensity = keyI * Mathf.Lerp(1, 1.18f, close);
            if (rim) rim.intensity = rimI * Mathf.Lerp(1, .75f, close); if (rim2) rim2.intensity = rim2I * Mathf.Lerp(1, .75f, close);
            if (floorGlow) floorGlow.alpha = Mathf.MoveTowards(floorGlow.alpha, frame == Frame.Full && zoom < 1.3f ? 1 : 0, dt * 3);
        }
        /// <summary>Pula a suavização (prints/teste).</summary>
        public void Snap() { shownYaw = yaw; if (model) model.transform.rotation = Quaternion.Euler(0, yaw, 0); if (!cam) return; Target(out var p, out var r, out _); cam.transform.SetPositionAndRotation(p, r); camVel = Vector3.zero; }
    }

    /// <summary>Área de toque do palco ("AvatarRotateTouchArea" do FF): 1 dedo/botão esquerdo gira; 2 dedos = pinça (zoom) + arrastar
    /// junto (pan vertical); botão direito/do meio = pan; roda = zoom; toque/clique duplo dança. Só contam os dedos que COMEÇARAM no palco
    /// (rolar a gaveta com dois dedos não dá zoom no personagem).</summary>
    public class FBCustomizeStageInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler, IPointerClickHandler
    {
        public FBCustomizeStage stage;
        readonly System.Collections.Generic.Dictionary<int, Vector2> down = new();
        public void OnPointerDown(PointerEventData e) { down[e.pointerId] = e.position; }
        public void OnPointerUp(PointerEventData e) { down.Remove(e.pointerId); if (down.Count == 0 && stage) stage.EndDrag(); }
        public void OnBeginDrag(PointerEventData e) { }
        public void OnDrag(PointerEventData e)
        {
            if (!stage) return;
            if (down.Count >= 2 && down.ContainsKey(e.pointerId))
            {
                // dois dedos: pinça pela distância entre eles + pan pela metade do movimento vertical de cada dedo
                int other = -999; foreach (var k in down.Keys) if (k != e.pointerId) { other = k; break; }
                var o = down[other]; float d0 = (down[e.pointerId] - o).magnitude, d1 = (e.position - o).magnitude;
                if (d0 > 8) stage.Pinch(d1 / d0); stage.Pan(e.delta.y * .5f); down[e.pointerId] = e.position; return;
            }
            if (down.ContainsKey(e.pointerId)) down[e.pointerId] = e.position;
            if (e.button == PointerEventData.InputButton.Left) stage.Drag(e.delta.x); else stage.Pan(e.delta.y);
        }
        public void OnEndDrag(PointerEventData e) { if (stage && down.Count < 2) stage.EndDrag(); }
        public void OnScroll(PointerEventData e) { if (!stage) return; float d = e.scrollDelta.y; if (Mathf.Abs(d) > 10) d /= 120; stage.Zoom(Mathf.Clamp(d, -2, 2)); }
        public void OnPointerClick(PointerEventData e) { if (stage && e.clickCount == 2 && e.button == PointerEventData.InputButton.Left) stage.PlayDance(); }
        void OnDisable() { down.Clear(); }
    }
}
