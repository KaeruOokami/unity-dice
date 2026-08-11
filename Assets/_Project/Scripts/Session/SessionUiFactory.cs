using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceGame.Session
{
    public static class LobbyUiFactory
    {
        static TMP_FontAsset uiFont;

        public static void Configure(TMP_FontAsset font) {
            uiFont = font;
        }

        public static void StretchFull(RectTransform rect) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void CenterPanel(RectTransform rect, Vector2 size) {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }

        public static GameObject CreatePanel(Transform parent, string name, Color color) {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            panel.AddComponent<RectTransform>();
            var image = panel.AddComponent<Image>();
            image.color = color;
            return panel;
        }

        public static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            string content,
            int fontSize,
            TextAnchor anchor) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            StretchFull(rect);
            var text = go.AddComponent<TextMeshProUGUI>();
            ApplyFont(text);
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = ToTextAlignment(anchor);
            text.color = Color.white;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchoredPosition,
            Action onClick) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(360f, 56f);
            rect.anchoredPosition = anchoredPosition;

            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.45f, 0.85f, 1f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());

            var labelText = CreateText(go.transform, "Label", label, 26, TextAnchor.MiddleCenter);
            labelText.color = Color.white;
            return button;
        }

        public static TMP_InputField CreateInputField(
            Transform parent,
            string name,
            string placeholder,
            Vector2 anchoredPosition) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(360f, 48f);
            rect.anchoredPosition = anchoredPosition;

            var image = go.AddComponent<Image>();
            image.color = new Color(0.18f, 0.18f, 0.2f, 1f);

            var input = go.AddComponent<TMP_InputField>();
            var textAreaRect = CreateInputTextArea(go.transform, new Vector2(12f, 6f), new Vector2(-12f, -6f));

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(textAreaRect, false);
            var textRect = textGo.AddComponent<RectTransform>();
            StretchFull(textRect);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            ApplyFont(text);
            text.fontSize = 24;
            text.color = Color.white;
            text.richText = false;

            var placeholderGo = new GameObject("Placeholder");
            placeholderGo.transform.SetParent(textAreaRect, false);
            var placeholderRect = placeholderGo.AddComponent<RectTransform>();
            StretchFull(placeholderRect);
            var placeholderText = placeholderGo.AddComponent<TextMeshProUGUI>();
            ApplyFont(placeholderText);
            placeholderText.fontSize = 24;
            placeholderText.fontStyle = FontStyles.Italic;
            placeholderText.color = new Color(1f, 1f, 1f, 0.45f);
            placeholderText.text = placeholder;

            input.textViewport = textAreaRect;
            input.textComponent = text;
            input.placeholder = placeholderText;
            input.characterLimit = 8;
            input.contentType = TMP_InputField.ContentType.Alphanumeric;
            ConfigureInputFieldVisuals(input);
            return input;
        }

        public static void ConfigureInputFieldVisuals(TMP_InputField input) {
            if (input == null) {
                return;
            }

            input.customCaretColor = true;
            input.caretColor = Color.white;
            input.caretWidth = 2;
            input.selectionColor = new Color(0.25f, 0.55f, 1f, 0.45f);

            var colors = input.colors;
            colors.normalColor = new Color(0.18f, 0.18f, 0.2f, 1f);
            colors.highlightedColor = new Color(0.22f, 0.22f, 0.26f, 1f);
            colors.selectedColor = new Color(0.28f, 0.32f, 0.42f, 1f);
            colors.pressedColor = colors.selectedColor;
            input.colors = colors;
        }

        public static RectTransform CreateInputTextArea(
            Transform parent,
            Vector2 offsetMin,
            Vector2 offsetMax) {
            var textArea = new GameObject("Text Area");
            textArea.transform.SetParent(parent, false);
            var textAreaRect = textArea.AddComponent<RectTransform>();
            StretchFull(textAreaRect);
            textAreaRect.offsetMin = offsetMin;
            textAreaRect.offsetMax = offsetMax;
            textArea.AddComponent<RectMask2D>();
            return textAreaRect;
        }

        public static TextMeshProUGUI CreateInlineText(
            GameObject go,
            string content,
            int fontSize,
            TextAlignmentOptions alignment) {
            var text = go.AddComponent<TextMeshProUGUI>();
            ApplyFont(text);
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static TextMeshProUGUI CreateDropdownCaption(Transform parent) {
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(parent, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            StretchFull(labelRect);
            labelRect.offsetMin = new Vector2(12f, 0f);
            labelRect.offsetMax = new Vector2(-36f, 0f);
            var labelText = labelGo.AddComponent<TextMeshProUGUI>();
            ApplyFont(labelText);
            labelText.fontSize = 22;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelText.raycastTarget = false;
            return labelText;
        }

        public static RectTransform CreateDropdownTemplate(Transform parent) {
            var template = new GameObject("Template");
            template.transform.SetParent(parent, false);
            template.SetActive(false);
            var templateRect = template.AddComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.sizeDelta = new Vector2(0f, 160f);
            templateRect.anchoredPosition = new Vector2(0f, 2f);
            template.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 1f);

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(template.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            StretchFull(viewportRect);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            viewport.AddComponent<Image>().color = Color.white;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 36f);

            var scroll = template.AddComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            var item = new GameObject("Item");
            item.transform.SetParent(content.transform, false);
            var itemRect = item.AddComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.pivot = new Vector2(0.5f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, 36f);

            var itemBackground = item.AddComponent<Image>();
            itemBackground.color = new Color(0.18f, 0.18f, 0.2f, 1f);

            var checkmarkGo = new GameObject("Item Checkmark");
            checkmarkGo.transform.SetParent(item.transform, false);
            var checkmarkRect = checkmarkGo.AddComponent<RectTransform>();
            StretchFull(checkmarkRect);
            var checkmark = checkmarkGo.AddComponent<Image>();
            checkmark.color = new Color(0.2f, 0.45f, 0.85f, 0.35f);

            var toggle = item.AddComponent<Toggle>();
            toggle.targetGraphic = itemBackground;
            toggle.graphic = checkmark;

            var itemLabelGo = new GameObject("Item Label");
            itemLabelGo.transform.SetParent(item.transform, false);
            var itemLabelRect = itemLabelGo.AddComponent<RectTransform>();
            StretchFull(itemLabelRect);
            itemLabelRect.offsetMin = new Vector2(12f, 0f);
            var itemLabel = itemLabelGo.AddComponent<TextMeshProUGUI>();
            ApplyFont(itemLabel);
            itemLabel.fontSize = 20;
            itemLabel.color = Color.white;
            itemLabel.alignment = TextAlignmentOptions.MidlineLeft;
            itemLabel.raycastTarget = false;
            return templateRect;
        }

        public static void ConfigureVerticalScrollContent(RectTransform content) {
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 12, 24);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        public static RectTransform CreateVerticalSection(Transform parent, string name) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var element = go.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            return rect;
        }

        public static GameObject CreateLayoutRow(Transform parent, string name, float height) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var element = go.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleWidth = 1f;
            return go;
        }

        public static TextMeshProUGUI CreateLayoutLabel(Transform parent, string text, int fontSize, float height) {
            var go = CreateLayoutRow(parent, text, height);
            return CreateInlineText(go, text, fontSize, TextAlignmentOptions.MidlineLeft);
        }

        public static TMP_InputField CreateLayoutInputField(
            Transform parent,
            string name,
            float height,
            TMP_InputField.ContentType contentType) {
            var go = CreateLayoutRow(parent, name, height);
            go.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.2f, 1f);
            var input = go.AddComponent<TMP_InputField>();
            var textAreaRect = CreateInputTextArea(go.transform, new Vector2(12f, 6f), new Vector2(-12f, -6f));

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(textAreaRect, false);
            var textRect = textGo.AddComponent<RectTransform>();
            StretchFull(textRect);
            var text = CreateInlineText(textGo, string.Empty, 22, TextAlignmentOptions.MidlineLeft);

            input.textViewport = textAreaRect;
            input.textComponent = text;
            input.contentType = contentType;
            ConfigureInputFieldVisuals(input);
            return input;
        }

        public static Toggle CreateLayoutToggle(Transform parent, string name, float height) {
            var go = CreateLayoutRow(parent, name, height);
            var toggle = go.AddComponent<Toggle>();
            var background = new GameObject("Background");
            background.transform.SetParent(go.transform, false);
            var backgroundRect = background.AddComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(0f, 0.5f);
            backgroundRect.pivot = new Vector2(0f, 0.5f);
            backgroundRect.sizeDelta = new Vector2(28f, 28f);
            backgroundRect.anchoredPosition = new Vector2(8f, 0f);
            background.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.2f, 1f);

            var checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(background.transform, false);
            var checkRect = checkmark.AddComponent<RectTransform>();
            StretchFull(checkRect);
            checkmark.AddComponent<Image>().color = new Color(0.2f, 0.75f, 0.35f, 1f);

            toggle.targetGraphic = background.GetComponent<Image>();
            toggle.graphic = checkmark.GetComponent<Image>();
            return toggle;
        }

        public static Button CreateLayoutButton(Transform parent, string name, string label, float height, Action onClick) {
            var go = CreateLayoutRow(parent, name, height);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.45f, 0.85f, 1f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            StretchFull(labelRect);
            CreateInlineText(labelGo, label, 20, TextAlignmentOptions.Center);
            return button;
        }

        public static TMP_Dropdown CreateLayoutDropdown(Transform parent, string name, string[] options, float height) {
            var go = CreateLayoutRow(parent, name, height);
            go.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.2f, 1f);
            var dropdown = go.AddComponent<TMP_Dropdown>();
            dropdown.captionText = CreateDropdownCaption(go.transform);
            var template = CreateDropdownTemplate(go.transform);
            dropdown.template = template;
            dropdown.itemText = template.GetComponentInChildren<TextMeshProUGUI>();
            dropdown.options = new System.Collections.Generic.List<TMP_Dropdown.OptionData>(options.Length);
            for (var i = 0; i < options.Length; i++) {
                dropdown.options.Add(new TMP_Dropdown.OptionData(options[i]));
            }

            dropdown.value = 0;
            dropdown.RefreshShownValue();
            return dropdown;
        }

        public static Slider CreateLayoutSlider(Transform parent, string name, float height, out TextMeshProUGUI valueLabel) {
            var go = CreateLayoutRow(parent, name, height);
            var slider = go.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.5f;

            var background = new GameObject("Background");
            background.transform.SetParent(go.transform, false);
            var backgroundRect = background.AddComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(1f, 0.5f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.sizeDelta = new Vector2(-120f, 12f);
            backgroundRect.anchoredPosition = new Vector2(-40f, 0f);
            background.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.2f, 1f);

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(background.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            StretchFull(fillAreaRect);
            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            StretchFull(fillRect);
            fill.AddComponent<Image>().color = new Color(0.2f, 0.45f, 0.85f, 1f);

            var handleSlideArea = new GameObject("Handle Slide Area");
            handleSlideArea.transform.SetParent(background.transform, false);
            var handleAreaRect = handleSlideArea.AddComponent<RectTransform>();
            StretchFull(handleAreaRect);
            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleSlideArea.transform, false);
            var handleRect = handle.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(18f, 18f);
            handle.AddComponent<Image>().color = Color.white;

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();

            var labelGo = new GameObject("ValueLabel");
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(1f, 0.5f);
            labelRect.anchorMax = new Vector2(1f, 0.5f);
            labelRect.pivot = new Vector2(1f, 0.5f);
            labelRect.sizeDelta = new Vector2(96f, 28f);
            valueLabel = CreateInlineText(labelGo, "0.50", 20, TextAlignmentOptions.MidlineRight);
            var capturedLabel = valueLabel;
            slider.onValueChanged.AddListener(v => {
                if (capturedLabel != null) {
                    capturedLabel.text = v.ToString("0.00");
                }
            });
            return slider;
        }

        public static void ClearChildren(Transform parent) {
            for (var i = parent.childCount - 1; i >= 0; i--) {
                var child = parent.GetChild(i).gameObject;
                child.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(child);
            }
        }

        public static string[] GetDiceKindOptionLabels() {
            var values = (DiceGame.Core.DiceKind[])System.Enum.GetValues(typeof(DiceGame.Core.DiceKind));
            var labels = new string[values.Length];
            for (var i = 0; i < values.Length; i++) {
                labels[i] = values[i].ToString();
            }

            return labels;
        }

        public static TMP_InputField CreateLabeledIntInput(Transform parent, string label) {
            CreateLayoutLabel(parent, label, 18, 24f);
            return CreateLayoutInputField(parent, $"{label}Input", 40f, TMP_InputField.ContentType.IntegerNumber);
        }

        public static TMP_InputField CreateLabeledFloatInput(Transform parent, string label) {
            CreateLayoutLabel(parent, label, 18, 24f);
            return CreateLayoutInputField(parent, $"{label}Input", 40f, TMP_InputField.ContentType.DecimalNumber);
        }

        public static Toggle CreateLabeledToggle(Transform parent, string label) {
            CreateLayoutLabel(parent, label, 18, 24f);
            return CreateLayoutToggle(parent, $"{label}Toggle", 36f);
        }

        public static void ForceRebuildLayout(RectTransform root) {
            if (root == null) {
                return;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        }

        static void ApplyFont(TextMeshProUGUI text) {
            if (uiFont == null) {
                Debug.LogError("[LobbyUiFactory] TMP font is not configured.");
                return;
            }

            text.font = uiFont;
        }

        static TextAlignmentOptions ToTextAlignment(TextAnchor anchor) {
            return anchor switch {
                TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
                TextAnchor.UpperCenter => TextAlignmentOptions.Top,
                TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
                TextAnchor.MiddleLeft => TextAlignmentOptions.MidlineLeft,
                TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
                TextAnchor.MiddleRight => TextAlignmentOptions.MidlineRight,
                TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
                TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
                TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
                _ => TextAlignmentOptions.MidlineLeft
            };
        }
    }
}
