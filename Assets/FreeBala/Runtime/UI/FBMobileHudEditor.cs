// Editor interativo de HUD Mobile (Customização de Controles): permite ao jogador mover, redimensionar,
// alterar a transparência e ocultar/remover qualquer botão da interface mobile.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FreeBala
{
    public class FBMobileHudEditor : MonoBehaviour
    {
        public static FBMobileHudEditor Instance { get; private set; }
        public static bool IsOpen => Instance != null;

        Canvas canvas;
        List<FBMobileButtonData> workingButtons = new();
        int selectedIndex = -1;

        // Elementos da UI do editor
        RectTransform buttonContainer;
        List<RectTransform> buttonRects = new();
        List<Image> buttonImages = new();
        List<Text> buttonLabels = new();
        List<GameObject> hiddenBadges = new();
        Image selectionRing;

        // Barra de propriedades do botão selecionado
        GameObject inspectorPanel;
        Text selectedTitle;
        Text sizeLabel;
        Slider sizeSlider;
        Text opacityLabel;
        Slider opacitySlider;
        Button toggleVisibilityBtn;
        Text toggleVisibilityLabel;

        // Arraste
        bool isDragging = false;
        Vector2 dragOffset;

        public static void Open()
        {
            if (Instance) return;
            var go = new GameObject("FBMobileHudEditor");
            Instance = go.AddComponent<FBMobileHudEditor>();
        }

        public static void Close()
        {
            if (Instance)
            {
                Destroy(Instance.gameObject);
                Instance = null;
            }
        }

        void Awake()
        {
            Instance = this;
            workingButtons = FBMobileLayout.Load();
            BuildUI();
            SelectButton(0);
        }

        void BuildUI()
        {
            canvas = FBUI.Canvas("FBMobileHudEditor_Canvas", 90, false);
            canvas.transform.SetParent(transform, false);
            var root = canvas.transform;

            // Fundo escuro com grid
            var bg = FBUI.Panel(root, "Fundo", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.04f, 0.05f, 0.08f, 0.88f));
            bg.raycastTarget = true;

            // Container para os botões virtuais
            var bcGo = new GameObject("ButtonContainer", typeof(RectTransform));
            buttonContainer = bcGo.GetComponent<RectTransform>();
            buttonContainer.SetParent(root, false);
            buttonContainer.anchorMin = Vector2.zero;
            buttonContainer.anchorMax = Vector2.one;
            buttonContainer.sizeDelta = Vector2.zero;

            // Anel de seleção dourado
            var ringGo = new GameObject("SelectionRing", typeof(RectTransform), typeof(Image));
            ringGo.transform.SetParent(buttonContainer, false);
            selectionRing = ringGo.GetComponent<Image>();
            selectionRing.sprite = FBUI.CircleSprite;
            selectionRing.color = new Color(1f, 0.74f, 0.12f, 0.95f);
            selectionRing.raycastTarget = false;
            ringGo.SetActive(false);

            // Monta os botões na tela
            BuildButtonViews();

            // Barra Superior (Header & Ações)
            BuildTopBar(root);

            // Barra de Ajustes do Botão Selecionado (Inspector)
            BuildInspector(root);
        }

        void BuildTopBar(Transform root)
        {
            var bar = FBUI.Panel(root, "TopBar", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -40), new Vector2(0, 80), new Color(0.05f, 0.06f, 0.08f, 0.95f));
            var bt = bar.transform;

            // Título e Dica
            FBKit.T(bt, "PERSONALIZAR CONTROLES MOBILE", 20, 32, 14, 460, 26, TextAnchor.MiddleLeft, true, Color.white);
            FBKit.T(bt, "Toque e arraste para mover  ·  Ajuste tamanho e opacidade abaixo", 12, 32, 42, 460, 20, TextAnchor.MiddleLeft, false, FBKit.Dim);

            // Botões de Ação na direita
            float btnW = 160, btnH = 44, gap = 12;
            float rx = 1600 - 32;

            // SALVAR & CONCLUIR
            rx -= btnW;
            var saveBtn = FBKit.Primary(bt, "SALVAR", rx, 18, btnW, btnH, OnSave);

            // RESTAURAR PADRÃO
            rx -= (btnW + 20);
            FBKit.TextTile(bt, "RESTAURAR", rx, 18, btnW + 20, btnH, false, OnResetDefaults, 13);

            // CANCELAR
            rx -= (btnW - 30);
            FBKit.TextTile(bt, "CANCELAR", rx, 18, btnW - 30, btnH, false, Close, 13);
        }

        void BuildInspector(Transform root)
        {
            // Barra inferior flutuante estilizada para editar o botão selecionado
            const float W = 1040, H = 84;
            var panel = FBUI.Panel(root, "Inspector", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 52), new Vector2(W, H), new Color(0.06f, 0.07f, 0.1f, 0.96f));
            FBKit.Frame(panel);
            inspectorPanel = panel.gameObject;
            var pt = panel.transform;

            // Título do botão selecionado
            selectedTitle = FBKit.T(pt, "BOTÃO: ATIRAR", 15, 24, 14, 200, 24, TextAnchor.MiddleLeft, true, FBKit.Gold);

            // Status Visível / Oculto
            toggleVisibilityBtn = FBKit.TextTile(pt, "OCULTAR", 24, 44, 180, 28, false, OnToggleVisibility, 11);
            toggleVisibilityLabel = toggleVisibilityBtn.GetComponentInChildren<Text>();

            // Slider de Tamanho (Escala)
            float cx = 230;
            sizeLabel = FBKit.T(pt, "TAMANHO: 100%", 12, cx, 14, 180, 20, TextAnchor.MiddleLeft, true, Color.white);
            sizeSlider = CreateSlider(pt, "SizeSlider", new Vector2(cx, 44), 200, 0.5f, 1.6f, 1f, val =>
            {
                if (selectedIndex >= 0 && selectedIndex < workingButtons.Count)
                {
                    workingButtons[selectedIndex].scale = val;
                    sizeLabel.text = $"TAMANHO: {Mathf.RoundToInt(val * 100)}%";
                    UpdateButtonVisual(selectedIndex);
                }
            });

            // Slider de Transparência (Opacidade)
            cx = 470;
            opacityLabel = FBKit.T(pt, "TRANSPARÊNCIA: 70%", 12, cx, 14, 180, 20, TextAnchor.MiddleLeft, true, Color.white);
            opacitySlider = CreateSlider(pt, "OpacitySlider", new Vector2(cx, 44), 200, 0.15f, 1.0f, 0.7f, val =>
            {
                if (selectedIndex >= 0 && selectedIndex < workingButtons.Count)
                {
                    workingButtons[selectedIndex].opacity = val;
                    opacityLabel.text = $"TRANSPARÊNCIA: {Mathf.RoundToInt(val * 100)}%";
                    UpdateButtonVisual(selectedIndex);
                }
            });

            // Posição Coordenadas e Dica de alinhamento
            cx = 710;
            FBKit.T(pt, "POSICIONAMENTO", 12, cx, 14, 300, 20, TextAnchor.MiddleLeft, true, FBKit.Dim);
            FBKit.T(pt, "Arraste na tela para ajustar a posição", 11, cx, 46, 300, 20, TextAnchor.MiddleLeft, false, FBKit.Txt);
        }

        Slider CreateSlider(Transform parent, string name, Vector2 pos, float width, float min, float max, float def, Action<float> onChange)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(pos.x, -pos.y);
            rt.sizeDelta = new Vector2(width, 14);

            var bg = FBUI.Panel(rt, "bg", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1, 1, 1, 0.12f));
            FBKit.Pill(bg);

            var fa = FBUI.Rect(rt, "fa", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fill = FBUI.Panel(fa, "f", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, FBKit.Gold);
            FBKit.Pill(fill);

            var ha = FBUI.Rect(rt, "ha", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var hd = FBUI.Panel(ha, "h", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(22, 22), Color.white);
            hd.sprite = FBUI.CircleSprite;

            var slider = go.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = hd.rectTransform;
            slider.targetGraphic = hd;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = def;
            slider.onValueChanged.AddListener(v => onChange?.Invoke(v));
            return slider;
        }

        void BuildButtonViews()
        {
            // Limpa anteriores
            foreach (var r in buttonRects) if (r) Destroy(r.gameObject);
            buttonRects.Clear();
            buttonImages.Clear();
            buttonLabels.Clear();
            hiddenBadges.Clear();

            for (int i = 0; i < workingButtons.Count; i++)
            {
                var b = workingButtons[i];
                int idx = i;

                var go = new GameObject("BtnView_" + b.id, typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(buttonContainer, false);
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);

                var img = go.GetComponent<Image>();
                img.sprite = FBUI.CircleSprite;
                img.raycastTarget = false;

                // Borda do botão
                var borderGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
                borderGo.transform.SetParent(rt, false);
                var brt = (RectTransform)borderGo.transform;
                brt.anchorMin = Vector2.zero;
                brt.anchorMax = Vector2.one;
                brt.sizeDelta = Vector2.zero;
                var borderImg = borderGo.GetComponent<Image>();
                borderImg.sprite = FBUI.CircleSprite;
                borderImg.color = new Color(1f, 1f, 1f, 0.35f);
                borderImg.raycastTarget = false;

                // Miolo escuro
                var innerGo = new GameObject("Inner", typeof(RectTransform), typeof(Image));
                innerGo.transform.SetParent(rt, false);
                var irt = (RectTransform)innerGo.transform;
                irt.anchorMin = Vector2.zero;
                irt.anchorMax = Vector2.one;
                irt.sizeDelta = new Vector2(-6, -6);
                var innerImg = innerGo.GetComponent<Image>();
                innerImg.sprite = FBUI.CircleSprite;
                innerImg.color = new Color(0.08f, 0.1f, 0.14f, 0.85f);
                innerImg.raycastTarget = false;

                // Texto do botão
                var lblGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
                var lrt = (RectTransform)lblGo.transform;
                lrt.SetParent(rt, false);
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.sizeDelta = Vector2.zero;
                var lbl = lblGo.GetComponent<Text>();
                lbl.text = b.icon;
                lbl.font = FBUI.Font;
                lbl.fontSize = 14;
                lbl.alignment = TextAnchor.MiddleCenter;
                lbl.color = Color.white;
                lbl.raycastTarget = false;

                // Badge de "OCULTO" caso o botão esteja desativado
                var badge = FBKit.Box(rt, "HiddenBadge", 0, 0, 70, 20, new Color(0.86f, 0.12f, 0.06f, 0.85f));
                var brt2 = badge.rectTransform;
                brt2.anchorMin = brt2.anchorMax = new Vector2(0.5f, 0.5f);
                brt2.anchoredPosition = new Vector2(0, -22);
                FBKit.T(badge.transform, "OCULTO", 10, 0, 0, 70, 20, TextAnchor.MiddleCenter, true, Color.white);
                badge.gameObject.SetActive(!b.enabled);

                buttonRects.Add(rt);
                buttonImages.Add(img);
                buttonLabels.Add(lbl);
                hiddenBadges.Add(badge.gameObject);

                UpdateButtonVisual(i);
            }
        }

        void UpdateButtonVisual(int index)
        {
            if (index < 0 || index >= workingButtons.Count) return;
            var b = workingButtons[index];
            var rt = buttonRects[index];
            var img = buttonImages[index];
            var badge = hiddenBadges[index];

            float diam = b.TotalRadius * 2f;
            rt.sizeDelta = new Vector2(diam, diam);
            rt.anchoredPosition = b.pos;

            // Opacidade e cores
            float alpha = b.enabled ? b.opacity : Mathf.Min(0.35f, b.opacity);
            img.color = b.enabled ? new Color(0.12f, 0.16f, 0.22f, alpha) : new Color(0.4f, 0.1f, 0.1f, 0.35f);
            badge.SetActive(!b.enabled);

            // Atualiza anel de seleção se este for o selecionado
            if (index == selectedIndex && selectionRing)
            {
                selectionRing.rectTransform.sizeDelta = new Vector2(diam + 12, diam + 12);
                selectionRing.rectTransform.anchoredPosition = b.pos;
            }
        }

        void SelectButton(int index)
        {
            if (index < 0 || index >= workingButtons.Count) return;
            selectedIndex = index;
            var b = workingButtons[index];

            selectedTitle.text = "BOTÃO: " + b.name.ToUpper();
            sizeSlider.SetValueWithoutNotify(b.scale);
            sizeLabel.text = $"TAMANHO: {Mathf.RoundToInt(b.scale * 100)}%";

            opacitySlider.SetValueWithoutNotify(b.opacity);
            opacityLabel.text = $"TRANSPARÊNCIA: {Mathf.RoundToInt(b.opacity * 100)}%";

            UpdateVisibilityButtonLabel();

            if (selectionRing)
            {
                selectionRing.gameObject.SetActive(true);
                float diam = b.TotalRadius * 2f;
                selectionRing.rectTransform.sizeDelta = new Vector2(diam + 12, diam + 12);
                selectionRing.rectTransform.anchoredPosition = b.pos;
            }
        }

        void UpdateVisibilityButtonLabel()
        {
            if (selectedIndex < 0 || selectedIndex >= workingButtons.Count) return;
            bool en = workingButtons[selectedIndex].enabled;
            if (toggleVisibilityLabel) toggleVisibilityLabel.text = en ? "OCULTAR" : "REATIVAR";
            var colors = toggleVisibilityBtn.colors;
            colors.normalColor = en ? new Color(0.86f, 0.12f, 0.06f, 0.6f) : new Color(0.2f, 0.75f, 0.3f, 0.8f);
            toggleVisibilityBtn.colors = colors;
        }

        void OnToggleVisibility()
        {
            if (selectedIndex < 0 || selectedIndex >= workingButtons.Count) return;
            workingButtons[selectedIndex].enabled = !workingButtons[selectedIndex].enabled;
            UpdateVisibilityButtonLabel();
            UpdateButtonVisual(selectedIndex);
        }

        void OnResetDefaults()
        {
            workingButtons = FBMobileLayout.GetDefaults();
            BuildButtonViews();
            SelectButton(Mathf.Clamp(selectedIndex, 0, workingButtons.Count - 1));
        }

        void OnSave()
        {
            FBMobileLayout.Save(workingButtons);
            // Avisa o HUD em tempo de jogo se estiver rodando
            if (FBTouch.I)
            {
                FBTouch.I.ApplyLayout();
            }
            Close();
        }

        void Update()
        {
            HandlePointerInput();
        }

        Vector2 GetCanvasPointerPos(Vector2 screenPos)
        {
            return new Vector2(screenPos.x / Screen.width * 1600f, screenPos.y / Screen.height * 900f);
        }

        void HandlePointerInput()
        {
            // Suporta mouse e toque na tela
            bool isDown = false, isHeld = false, isUp = false;
            Vector2 screenPos = Vector2.zero;

            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                screenPos = t.position;
                isDown = t.phase == TouchPhase.Began;
                isHeld = t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary;
                isUp = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
            }
            else
            {
                screenPos = Input.mousePosition;
                isDown = Input.GetMouseButtonDown(0);
                isHeld = Input.GetMouseButton(0);
                isUp = Input.GetMouseButtonUp(0);
            }

            Vector2 canvasPos = GetCanvasPointerPos(screenPos);

            // Não processa arrasto se clicou na barra superior ou no inspector
            if (canvasPos.y < 120 || canvasPos.y > 820)
            {
                if (isDown) isDragging = false;
                return;
            }

            if (isDown)
            {
                // Verifica se clicou em algum botão (procura do último para o primeiro para priorizar o topo)
                int hitIdx = -1;
                for (int i = workingButtons.Count - 1; i >= 0; i--)
                {
                    var b = workingButtons[i];
                    float dist = Vector2.Distance(canvasPos, b.pos);
                    if (dist <= b.TotalRadius)
                    {
                        hitIdx = i;
                        break;
                    }
                }

                if (hitIdx >= 0)
                {
                    SelectButton(hitIdx);
                    isDragging = true;
                    dragOffset = workingButtons[hitIdx].pos - canvasPos;
                }
            }
            else if (isHeld && isDragging && selectedIndex >= 0)
            {
                var b = workingButtons[selectedIndex];
                Vector2 targetPos = canvasPos + dragOffset;
                float r = b.TotalRadius;
                // Mantém dentro dos limites úteis da tela (1600 x 900)
                targetPos.x = Mathf.Clamp(targetPos.x, r + 20, 1600 - r - 20);
                targetPos.y = Mathf.Clamp(targetPos.y, r + 90, 900 - r - 70);

                b.pos = targetPos;
                UpdateButtonVisual(selectedIndex);
            }
            else if (isUp)
            {
                isDragging = false;
            }
        }
    }
}

