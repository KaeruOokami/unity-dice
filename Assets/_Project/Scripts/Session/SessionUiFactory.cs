using System;
using System.Collections.Generic;
using DiceGame.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceGame.Session
{
    public static class SessionUiFactory
    {
        static TMP_FontAsset uiFont;
        static DiceKindIconCatalog diceKindIcons;

        public static void Configure(TMP_FontAsset font) {
            uiFont = font;
        }

        public static void ConfigureDiceKindIcons(DiceKindIconCatalog icons) {
            diceKindIcons = icons;
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

        public static void AnchorBottomCenter(RectTransform rect, float yFromBottom) {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, yFromBottom);
        }

        public static RectTransform CreateTwoColumnRow(Transform parent, string name = "TwoColumnRow") {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
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

        public static RectTransform CreateFieldCell(Transform parent, string name) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var element = go.AddComponent<LayoutElement>();
            // ラベル文字の preferred 幅で列が広がらないよう、幅は flex のみで等分する。
            element.minWidth = 0f;
            element.preferredWidth = 0f;
            element.flexibleWidth = 1f;
            return rect;
        }

        public static RectTransform CreateBottomButtonBar(
            Transform parent,
            string name,
            float height,
            float yFromBottom) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.08f, 0f);
            rect.anchorMax = new Vector2(0.92f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = new Vector2(0f, yFromBottom);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return rect;
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
            text.textWrappingMode = TextWrappingModes.Normal;
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
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static TextMeshProUGUI CreateDropdownCaption(Transform parent, float textLeftInset = 12f) {
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(parent, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            StretchFull(labelRect);
            labelRect.offsetMin = new Vector2(textLeftInset, 0f);
            labelRect.offsetMax = new Vector2(-36f, 0f);
            var labelText = labelGo.AddComponent<TextMeshProUGUI>();
            ApplyFont(labelText);
            labelText.fontSize = 22;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelText.raycastTarget = false;
            return labelText;
        }

        public static Image CreateDropdownCaptionIcon(Transform parent, float iconSize) {
            var iconGo = new GameObject("Caption Icon");
            iconGo.transform.SetParent(parent, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            iconRect.anchoredPosition = new Vector2(8f, 0f);
            var image = iconGo.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            return image;
        }

        public static RectTransform CreateDropdownTemplate(
            Transform parent,
            float itemHeight = 36f,
            bool withIcons = false,
            float iconSize = 32f) {
            var template = new GameObject("Template");
            template.transform.SetParent(parent, false);
            template.SetActive(false);
            var templateRect = template.AddComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.sizeDelta = new Vector2(0f, Mathf.Max(160f, itemHeight * 4f));
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
            contentRect.sizeDelta = new Vector2(0f, itemHeight);

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
            itemRect.sizeDelta = new Vector2(0f, itemHeight);

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

            var labelLeft = 12f;
            if (withIcons) {
                var resolvedIconSize = Mathf.Max(16f, Mathf.Min(iconSize, itemHeight - 8f));
                var itemIconGo = new GameObject("Item Icon");
                itemIconGo.transform.SetParent(item.transform, false);
                var itemIconRect = itemIconGo.AddComponent<RectTransform>();
                itemIconRect.anchorMin = new Vector2(0f, 0.5f);
                itemIconRect.anchorMax = new Vector2(0f, 0.5f);
                itemIconRect.pivot = new Vector2(0f, 0.5f);
                itemIconRect.sizeDelta = new Vector2(resolvedIconSize, resolvedIconSize);
                itemIconRect.anchoredPosition = new Vector2(8f, 0f);
                var itemIcon = itemIconGo.AddComponent<Image>();
                itemIcon.preserveAspect = true;
                itemIcon.raycastTarget = false;
                itemIcon.color = Color.white;
                labelLeft = 8f + resolvedIconSize + 8f;
            }

            var itemLabelGo = new GameObject("Item Label");
            itemLabelGo.transform.SetParent(item.transform, false);
            var itemLabelRect = itemLabelGo.AddComponent<RectTransform>();
            StretchFull(itemLabelRect);
            itemLabelRect.offsetMin = new Vector2(labelLeft, 0f);
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
            element.minWidth = 0f;
            element.preferredWidth = 0f;
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
            var optionData = new List<TMP_Dropdown.OptionData>(options.Length);
            for (var i = 0; i < options.Length; i++) {
                optionData.Add(new TMP_Dropdown.OptionData(options[i]));
            }

            return CreateLayoutDropdown(parent, name, optionData, height, withIcons: false);
        }

        public static TMP_Dropdown CreateDiceKindDropdown(Transform parent, string name, float height) {
            var kinds = (DiceKind[])Enum.GetValues(typeof(DiceKind));
            var optionData = new List<TMP_Dropdown.OptionData>(kinds.Length);
            for (var i = 0; i < kinds.Length; i++) {
                var kind = kinds[i];
                Sprite icon = null;
                diceKindIcons?.TryGet(kind, out icon);
                var option = new TMP_Dropdown.OptionData(kind.ToString()) {
                    image = icon
                };
                optionData.Add(option);
            }

            var iconSize = diceKindIcons != null ? diceKindIcons.IconSize : 32f;
            var dropdown = CreateLayoutDropdown(parent, name, optionData, height, withIcons: true, iconSize);
            diceKindIcons?.RegisterDropdown(dropdown);
            return dropdown;
        }

        public static TMP_Dropdown CreateLayoutDropdown(
            Transform parent,
            string name,
            List<TMP_Dropdown.OptionData> options,
            float height,
            bool withIcons,
            float iconSize = 32f) {
            var go = CreateLayoutRow(parent, name, height);
            go.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.2f, 1f);
            var dropdown = go.AddComponent<TMP_Dropdown>();
            var textInset = withIcons ? 8f + iconSize + 8f : 12f;
            dropdown.captionText = CreateDropdownCaption(go.transform, textInset);
            if (withIcons) {
                dropdown.captionImage = CreateDropdownCaptionIcon(go.transform, iconSize);
            }

            var template = CreateDropdownTemplate(
                go.transform,
                withIcons ? height : 36f,
                withIcons,
                iconSize);
            dropdown.template = template;
            dropdown.itemText = template.GetComponentInChildren<TextMeshProUGUI>();
            if (withIcons) {
                var itemIcon = template.Find("Viewport/Content/Item/Item Icon");
                if (itemIcon != null) {
                    dropdown.itemImage = itemIcon.GetComponent<Image>();
                }
            }

            dropdown.options = options ?? new List<TMP_Dropdown.OptionData>();
            dropdown.value = 0;
            dropdown.RefreshShownValue();
            return dropdown;
        }

        public static TextMeshProUGUI CreateKindLabeledRow(
            Transform parent,
            DiceKind kind,
            string text,
            int fontSize,
            float height) {
            var go = CreateLayoutRow(parent, text, height);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(4, 4, 0, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            if (diceKindIcons != null && diceKindIcons.TryGet(kind, out var icon) && icon != null) {
                var iconGo = new GameObject("KindIcon");
                iconGo.transform.SetParent(go.transform, false);
                var iconImage = iconGo.AddComponent<Image>();
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
                var iconElement = iconGo.AddComponent<LayoutElement>();
                var size = Mathf.Min(diceKindIcons.IconSize, height - 4f);
                iconElement.preferredWidth = size;
                iconElement.preferredHeight = size;
                iconElement.minWidth = size;
                iconElement.minHeight = size;
                diceKindIcons.RegisterImage(iconImage, kind);
            }

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var labelElement = labelGo.AddComponent<LayoutElement>();
            labelElement.minWidth = 0f;
            labelElement.preferredWidth = 0f;
            labelElement.flexibleWidth = 1f;
            var labelText = CreateInlineText(labelGo, text, fontSize, TextAlignmentOptions.MidlineLeft);
            labelText.overflowMode = TextOverflowModes.Ellipsis;
            return labelText;
        }

        public static TMP_InputField CreateKindLabeledFloatInput(Transform parent, DiceKind kind, string suffix) {
            var cell = CreateFieldCell(parent, kind.ToString());
            CreateKindLabeledRow(cell, kind, $"{kind} {suffix}", 18, 28f);
            return CreateLayoutInputField(cell, $"{kind}{suffix}Input", 40f, TMP_InputField.ContentType.DecimalNumber);
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

        public static TMP_InputField CreateLabeledTextInput(Transform parent, string label) {
            var cell = CreateFieldCell(parent, label);
            CreateLayoutLabel(cell, label, 18, 24f);
            return CreateLayoutInputField(cell, $"{label}Input", 40f, TMP_InputField.ContentType.Standard);
        }

        public static TMP_InputField CreateLabeledIntInput(Transform parent, string label) {
            var cell = CreateFieldCell(parent, label);
            CreateLayoutLabel(cell, label, 18, 24f);
            return CreateLayoutInputField(cell, $"{label}Input", 40f, TMP_InputField.ContentType.IntegerNumber);
        }

        public static TMP_InputField CreateLabeledFloatInput(Transform parent, string label) {
            var cell = CreateFieldCell(parent, label);
            CreateLayoutLabel(cell, label, 18, 24f);
            return CreateLayoutInputField(cell, $"{label}Input", 40f, TMP_InputField.ContentType.DecimalNumber);
        }

        public static Toggle CreateLabeledToggle(Transform parent, string label) {
            var cell = CreateFieldCell(parent, label);
            CreateLayoutLabel(cell, label, 18, 24f);
            return CreateLayoutToggle(cell, $"{label}Toggle", 36f);
        }

        /// <summary>
        /// Trigger Faces 用: Normal ダイスの出目アイコン（固定）+ ラベル + Toggle。
        /// </summary>
        public static Toggle CreateFaceLabeledToggle(Transform parent, int face) {
            var label = $"Face {face}";
            var cell = CreateFieldCell(parent, label);
            CreateFaceLabeledRow(cell, face, label, 18, 28f);
            return CreateLayoutToggle(cell, $"{label}Toggle", 36f);
        }

        public static TextMeshProUGUI CreateFaceLabeledRow(
            Transform parent,
            int face,
            string text,
            int fontSize,
            float height) {
            var go = CreateLayoutRow(parent, text, height);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(4, 4, 0, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            if (diceKindIcons != null
                && diceKindIcons.TryGet(DiceKind.Normal, face, out var icon)
                && icon != null) {
                var iconGo = new GameObject("FaceIcon");
                iconGo.transform.SetParent(go.transform, false);
                var iconImage = iconGo.AddComponent<Image>();
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
                var iconElement = iconGo.AddComponent<LayoutElement>();
                var size = Mathf.Min(diceKindIcons.IconSize, height - 4f);
                iconElement.preferredWidth = size;
                iconElement.preferredHeight = size;
                iconElement.minWidth = size;
                iconElement.minHeight = size;
            }

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var labelElement = labelGo.AddComponent<LayoutElement>();
            labelElement.minWidth = 0f;
            labelElement.preferredWidth = 0f;
            labelElement.flexibleWidth = 1f;
            var labelText = CreateInlineText(labelGo, text, fontSize, TextAlignmentOptions.MidlineLeft);
            labelText.overflowMode = TextOverflowModes.Ellipsis;
            return labelText;
        }

        public static Button CreateTwoColumnLayoutButton(
            Transform parent,
            string name,
            string label,
            float height,
            Action onClick) {
            var cell = CreateFieldCell(parent, name);
            return CreateLayoutButton(cell, name, label, height, onClick);
        }

        public static void ForceRebuildLayout(RectTransform root) {
            if (root == null) {
                return;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        }

        static void ApplyFont(TextMeshProUGUI text) {
            if (uiFont == null) {
                Debug.LogError("[SessionUiFactory] TMP font is not configured.");
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
