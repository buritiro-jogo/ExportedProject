// Controles de toque e HUD Mobile: analógico virtual, botões ergonômicos de combate (tiro, mira, pulo,
// agachar, deitar, correr, recarregar, troca de arma, emotes, curar, pegar, ação, mapa e câmera livre),
// suporte a layout customizado pelo jogador (FBMobileLayout) e suporte simultâneo a multi-touch e mouse.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace FreeBala
{
    public class FBTouch : MonoBehaviour
    {
        public static FBTouch I;
        public static bool Active => I && I.on;

        public Vector2 move, look;
        public bool fire, firePressed, aim, jump, crouchPressed, reload, swap, sprint,
            interact, interactHeld, pickup, heal, buy, view, prone, freeLook, freeLookPressed, mapPressed;

        public bool on;

        class RuntimeBtn
        {
            public FBMobileButtonData data;
            public RectTransform rt;
            public Image bg;
            public Image border;
            public Text label;
            public bool isPressed;
            public int activeTouchId = -1;
        }

        List<RuntimeBtn> buttons = new();
        Canvas canvas;
        RectTransform stickBase, stickKnob;
        int moveTouchId = -1, lookTouchId = -1;
        Vector2 moveOrigin, lastLookPos;
        bool mouseMoveActive = false, mouseLookActive = false;
        Vector2 mouseOrigin, lastMousePos;

        void Awake()
        {
            I = this;
            CheckActivation();
            if (!on) return;

            InitCanvas();
            ApplyLayout();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        public void CheckActivation()
        {
            on = Application.isMobilePlatform || FBSession.mobileHUD ||
                 (Touchscreen.current != null && SystemInfo.deviceType == DeviceType.Handheld);
        }

        public void SetEnabled(bool enabled)
        {
            on = enabled;
            if (canvas) canvas.enabled = on;
            if (on && (buttons == null || buttons.Count == 0))
            {
                InitCanvas();
                ApplyLayout();
            }
        }

        void InitCanvas()
        {
            if (canvas) return;
            EnhancedTouchSupport.Enable();

            var go = new GameObject("ToqueMobile");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1600, 900);
            sc.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
        }

        public void ApplyLayout()
        {
            if (!canvas) InitCanvas();
            if (!canvas) return;

            // Remove botões visuais antigos
            foreach (Transform child in canvas.transform)
            {
                Destroy(child.gameObject);
            }
            buttons.Clear();

            var layout = FBMobileLayout.Load();

            // Encontra ou cria o joystick
            FBMobileButtonData joyData = null;
            foreach (var b in layout)
            {
                if (b.id == "JOYSTICK") { joyData = b; break; }
            }
            if (joyData == null) joyData = new FBMobileButtonData { id = "JOYSTICK", name = "ANDAR", pos = new Vector2(210, 190), radius = 95, scale = 1f, opacity = .55f, enabled = true };

            if (joyData.enabled)
            {
                float baseDiam = joyData.TotalRadius * 2f;
                stickBase = CreateCircle("StickBase", canvas.transform, joyData.pos, new Vector2(baseDiam, baseDiam), new Color(1, 1, 1, joyData.opacity * 0.25f)).rectTransform;
                stickKnob = CreateCircle("StickKnob", canvas.transform, joyData.pos, new Vector2(baseDiam * 0.42f, baseDiam * 0.42f), new Color(1, 1, 1, Mathf.Min(1f, joyData.opacity + 0.25f))).rectTransform;
            }
            else
            {
                stickBase = null;
                stickKnob = null;
            }

            // Cria cada botão habilitado
            foreach (var b in layout)
            {
                if (b.id == "JOYSTICK" || !b.enabled) continue;

                var rb = new RuntimeBtn { data = b };
                float diam = b.TotalRadius * 2f;

                // Raiz do botão
                var bGo = new GameObject("Btn_" + b.id, typeof(RectTransform));
                rb.rt = (RectTransform)bGo.transform;
                rb.rt.SetParent(canvas.transform, false);
                rb.rt.anchorMin = rb.rt.anchorMax = Vector2.zero;
                rb.rt.pivot = new Vector2(0.5f, 0.5f);
                rb.rt.sizeDelta = new Vector2(diam, diam);
                rb.rt.anchoredPosition = b.pos;

                // Fundo circular
                rb.bg = CreateCircle("Bg", rb.rt, Vector2.zero, Vector2.zero, new Color(0.08f, 0.1f, 0.14f, b.opacity));
                rb.bg.rectTransform.anchorMin = Vector2.zero;
                rb.bg.rectTransform.anchorMax = Vector2.one;

                // Borda circular
                rb.border = CreateCircle("Border", rb.rt, Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, b.opacity * 0.45f));
                rb.border.rectTransform.anchorMin = Vector2.zero;
                rb.border.rectTransform.anchorMax = Vector2.one;
                rb.border.rectTransform.sizeDelta = new Vector2(-4, -4);

                // Label com o ícone/nome
                var lGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
                var lrt = (RectTransform)lGo.transform;
                lrt.SetParent(rb.rt, false);
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.sizeDelta = Vector2.zero;

                rb.label = lGo.GetComponent<Text>();
                rb.label.text = b.icon;
                rb.label.font = FBUI.Font;
                rb.label.fontSize = b.radius > 60 ? 18 : 13;
                rb.label.alignment = TextAnchor.MiddleCenter;
                rb.label.color = Color.white;
                rb.label.raycastTarget = false;

                buttons.Add(rb);
            }
        }

        Image CreateCircle(string name, Transform parent, Vector2 pos, Vector2 size, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.sprite = FBUI.CircleSprite;
            img.color = col;
            img.raycastTarget = false;
            return img;
        }

        public void Show(bool v)
        {
            if (canvas) canvas.enabled = v && on;
        }

        Vector2 ScreenToCanvasPos(Vector2 screen)
        {
            return new Vector2(screen.x / Screen.width * 1600f, screen.y / Screen.height * 900f);
        }

        bool IsPointInButton(RuntimeBtn b, Vector2 canvasPoint)
        {
            float maxDist = b.data.TotalRadius * 1.15f;
            return Vector2.Distance(canvasPoint, b.data.pos) <= maxDist;
        }

        // [parceiro] v1 (thread de física): tempo de CPU medido pelo FBTempo (desligado = só um teste de bool)
        static readonly int __fbt_Update = FBTempo.Id("FBTouch.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }

        void Update__fbt()
        {
            if (!on) return;

            look = Vector2.zero;
            bool fireNow = false;
            bool aimNow = false;
            bool actNow = false;
            bool flNow = false;

            // 1. Processamento de Toques (Mobile nativo)
            var touches = ETouch.activeTouches;
            if (touches.Count > 0)
            {
                foreach (var t in touches)
                {
                    int id = t.touchId;
                    Vector2 pos = t.screenPosition;
                    Vector2 cPos = ScreenToCanvasPos(pos);
                    bool began = t.phase == UnityEngine.InputSystem.TouchPhase.Began;
                    bool ended = t.phase == UnityEngine.InputSystem.TouchPhase.Ended || t.phase == UnityEngine.InputSystem.TouchPhase.Canceled;

                    // Verifica se tocou em algum botão de ação
                    RuntimeBtn hit = null;
                    foreach (var b in buttons)
                    {
                        if (IsPointInButton(b, cPos))
                        {
                            hit = b;
                            break;
                        }
                    }

                    if (hit != null && id != moveTouchId && id != lookTouchId)
                    {
                        if (began)
                        {
                            hit.isPressed = true;
                            hit.activeTouchId = id;
                            TriggerButtonAction(hit.data.id, true);
                        }

                        if (hit.data.id == "FIRE" || hit.data.id == "FIRE_LEFT")
                        {
                            fireNow = true;
                            // Arrastar no botão de tiro principal também controla a mira
                            if (hit.data.id == "FIRE") look += t.delta;
                        }
                        else if (hit.data.id == "AIM")
                        {
                            aimNow = true;
                        }
                        else if (hit.data.id == "INTERACT")
                        {
                            actNow = true;
                        }
                        else if (hit.data.id == "CAMERA")
                        {
                            flNow = true;
                            look += t.delta;
                        }

                        if (ended && hit.activeTouchId == id)
                        {
                            hit.isPressed = false;
                            hit.activeTouchId = -1;
                        }
                        continue;
                    }

                    // Se não tocou em botão, verifica analógico ou olhar
                    if (began)
                    {
                        // Lado esquerdo (abaixo de 65% da tela) controla o analógico
                        if (cPos.x < 750 && cPos.y < 600 && moveTouchId < 0)
                        {
                            moveTouchId = id;
                            moveOrigin = pos;
                            if (stickBase) stickBase.anchoredPosition = cPos;
                        }
                        // Lado direito controla a rotação da câmera (look)
                        else if (cPos.x >= 750 && lookTouchId < 0)
                        {
                            lookTouchId = id;
                            lastLookPos = pos;
                        }
                    }

                    if (id == moveTouchId)
                    {
                        Vector2 raw = (pos - moveOrigin) / (Screen.height * 0.09f);
                        Vector2 d = Vector2.ClampMagnitude(raw, 1f);
                        move = d;
                        if (stickKnob && stickBase)
                        {
                            stickKnob.anchoredPosition = stickBase.anchoredPosition + d * (stickBase.sizeDelta.x * 0.38f);
                        }
                        sprint = raw.magnitude > 1.25f && raw.y > 0.75f;

                        if (ended)
                        {
                            moveTouchId = -1;
                            move = Vector2.zero;
                            sprint = false;
                            // Retorna o analógico para a posição configurada
                            FBMobileButtonData joyData = FBMobileLayout.Load().Find(x => x.id == "JOYSTICK");
                            Vector2 defPos = joyData != null ? joyData.pos : new Vector2(210, 190);
                            if (stickBase) stickBase.anchoredPosition = defPos;
                            if (stickKnob) stickKnob.anchoredPosition = defPos;
                        }
                    }
                    else if (id == lookTouchId)
                    {
                        look += pos - lastLookPos;
                        lastLookPos = pos;
                        if (ended) lookTouchId = -1;
                    }
                }
            }
            // 2. Simulação via Mouse (para testes no PC e Editor)
            else if (Mouse.current != null)
            {
                var mouse = Mouse.current;
                Vector2 mPos = mouse.position.ReadValue();
                Vector2 cPos = ScreenToCanvasPos(mPos);
                bool mDown = mouse.leftButton.wasPressedThisFrame;
                bool mHeld = mouse.leftButton.isPressed;
                bool mUp = mouse.leftButton.wasReleasedThisFrame;

                RuntimeBtn hit = null;
                foreach (var b in buttons)
                {
                    if (IsPointInButton(b, cPos))
                    {
                        hit = b;
                        break;
                    }
                }

                if (hit != null && !mouseMoveActive && !mouseLookActive)
                {
                    if (mDown)
                    {
                        hit.isPressed = true;
                        TriggerButtonAction(hit.data.id, true);
                    }

                    if (hit.data.id == "FIRE" || hit.data.id == "FIRE_LEFT")
                    {
                        fireNow = mHeld;
                        if (mHeld && hit.data.id == "FIRE") look += mouse.delta.ReadValue();
                    }
                    else if (hit.data.id == "AIM")
                    {
                        aimNow = mHeld;
                    }
                    else if (hit.data.id == "INTERACT")
                    {
                        actNow = mHeld;
                    }
                    else if (hit.data.id == "CAMERA")
                    {
                        flNow = mHeld;
                        if (mHeld) look += mouse.delta.ReadValue();
                    }

                    if (mUp) hit.isPressed = false;
                }
                else
                {
                    if (mDown)
                    {
                        if (cPos.x < 750 && cPos.y < 600)
                        {
                            mouseMoveActive = true;
                            mouseOrigin = mPos;
                            if (stickBase) stickBase.anchoredPosition = cPos;
                        }
                        else if (cPos.x >= 750)
                        {
                            mouseLookActive = true;
                            lastMousePos = mPos;
                        }
                    }

                    if (mouseMoveActive)
                    {
                        Vector2 raw = (mPos - mouseOrigin) / (Screen.height * 0.09f);
                        Vector2 d = Vector2.ClampMagnitude(raw, 1f);
                        move = d;
                        if (stickKnob && stickBase)
                        {
                            stickKnob.anchoredPosition = stickBase.anchoredPosition + d * (stickBase.sizeDelta.x * 0.38f);
                        }
                        sprint = raw.magnitude > 1.25f && raw.y > 0.75f;

                        if (mUp)
                        {
                            mouseMoveActive = false;
                            move = Vector2.zero;
                            sprint = false;
                            FBMobileButtonData joyData = FBMobileLayout.Load().Find(x => x.id == "JOYSTICK");
                            Vector2 defPos = joyData != null ? joyData.pos : new Vector2(210, 190);
                            if (stickBase) stickBase.anchoredPosition = defPos;
                            if (stickKnob) stickKnob.anchoredPosition = defPos;
                        }
                    }
                    else if (mouseLookActive)
                    {
                        look += mPos - lastMousePos;
                        lastMousePos = mPos;
                        if (mUp) mouseLookActive = false;
                    }
                }
            }

            // Atualiza estados
            fire = fireNow;
            aim = aimNow;
            interactHeld = actNow;
            freeLook = flNow;

            // Feedback visual dos botões
            foreach (var b in buttons)
            {
                if (b.bg)
                {
                    Color baseCol = b.isPressed ? FBKit.Gold : new Color(0.08f, 0.1f, 0.14f, b.data.opacity);
                    b.bg.color = baseCol;
                }
                if (b.border)
                {
                    b.border.color = b.isPressed ? Color.white : new Color(1f, 1f, 1f, b.data.opacity * 0.45f);
                }
                if (b.label)
                {
                    b.label.color = b.isPressed ? new Color(0.1f, 0.08f, 0.02f) : Color.white;
                }
            }
        }

        void TriggerButtonAction(string btnId, bool began)
        {
            if (!began) return;

            switch (btnId)
            {
                case "FIRE":
                case "FIRE_LEFT":
                    firePressed = true;
                    break;

                case "JUMP":
                    jump = true;
                    break;

                case "CROUCH":
                    crouchPressed = true;
                    break;

                case "PRONE":
                    prone = true;
                    break;

                case "SPRINT":
                    sprint = !sprint;
                    break;

                case "RELOAD":
                    reload = true;
                    break;

                case "SWAP":
                    swap = true;
                    break;

                case "EMOTE":
                    buy = true;
                    if (FBEmoteWheel.IsOpen) FBEmoteWheel.Close(true);
                    else FBEmoteWheel.Open();
                    break;

                case "HEAL":
                    heal = true;
                    break;

                case "PICKUP":
                    pickup = true;
                    break;

                case "INTERACT":
                    interact = true;
                    break;

                case "MAP":
                    mapPressed = true;
                    FBMatchMap.Toggle();
                    break;

                case "CAMERA":
                    view = true;
                    freeLookPressed = true;
                    break;
            }
        }

        public void EndFrame()
        {
            firePressed = false;
            jump = false;
            reload = false;
            swap = false;
            crouchPressed = false;
            interact = false;
            pickup = false;
            heal = false;
            buy = false;
            view = false;
            prone = false;
            freeLookPressed = false;
            mapPressed = false;
        }
    }
}
