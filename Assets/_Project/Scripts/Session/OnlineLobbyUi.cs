using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DiceGame.Session
{
    public sealed class OnlineLobbyUi : MonoBehaviour
    {
        OnlineSessionController controller;
        Canvas canvas;
        GameObject mainPanel;
        GameObject hostPanel;
        GameObject clientPanel;
        InputField joinCodeInput;
        Text statusText;
        Text hostCodeText;
        Text clientStatusText;

        public void Configure(OnlineSessionController sessionController) {
            controller = sessionController;
            EnsureEventSystem();
            BuildUi();
            ShowMainPanel();

            if (OnlineSessionState.Instance != null) {
                OnlineSessionState.Instance.StateChanged += RefreshStatus;
            }
        }

        void OnDestroy() {
            if (OnlineSessionState.Instance != null) {
                OnlineSessionState.Instance.StateChanged -= RefreshStatus;
            }
        }

        public void ShowMainPanel() {
            SetPanel(main: true, host: false, client: false);
            RefreshStatus();
        }

        public void ShowHostPanel(string lobbyCode) {
            SetPanel(main: false, host: true, client: false);
            if (hostCodeText != null) {
                hostCodeText.text = $"参加コード\n{lobbyCode}";
            }

            RefreshStatus();
        }

        public void ShowClientWaitingPanel(string lobbyCode) {
            SetPanel(main: false, host: false, client: true);
            if (clientStatusText != null) {
                clientStatusText.text = $"参加中: {lobbyCode}\nホストの開始待ち…";
            }

            RefreshStatus();
        }

        public void Hide() {
            if (canvas != null) {
                canvas.gameObject.SetActive(false);
            }
        }

        void RefreshStatus() {
            var status = OnlineSessionState.Instance?.StatusMessage ?? string.Empty;
            if (statusText != null) {
                statusText.text = status;
            }
        }

        void SetPanel(bool main, bool host, bool client) {
            if (canvas != null) {
                canvas.gameObject.SetActive(true);
            }

            if (mainPanel != null) {
                mainPanel.SetActive(main);
            }

            if (hostPanel != null) {
                hostPanel.SetActive(host);
            }

            if (clientPanel != null) {
                clientPanel.SetActive(client);
            }
        }

        void BuildUi() {
            if (canvas != null) {
                return;
            }

            var canvasObject = new GameObject("OnlineLobbyCanvas");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            var root = CreatePanel(canvasObject.transform, "Root", new Color(0f, 0f, 0f, 0.65f));
            StretchFull(root.GetComponent<RectTransform>());

            statusText = CreateText(root.transform, "Status", string.Empty, 28, TextAnchor.LowerCenter);
            var statusRect = statusText.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.1f, 0.05f);
            statusRect.anchorMax = new Vector2(0.9f, 0.15f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;

            mainPanel = CreatePanel(root.transform, "MainPanel", new Color(0.12f, 0.12f, 0.14f, 0.95f));
            CenterPanel(mainPanel.GetComponent<RectTransform>(), new Vector2(520f, 420f));
            CreateText(mainPanel.transform, "Title", "オンライン対戦", 40, TextAnchor.UpperCenter);
            CreateButton(mainPanel.transform, "LocalButton", "ローカルプレイ", new Vector2(0f, 80f), () => {
                controller?.StartLocalPlay();
            });
            CreateButton(mainPanel.transform, "HostButton", "ルーム作成（ホスト）", new Vector2(0f, 0f), () => {
                controller?.CreateHostLobby();
            });

            joinCodeInput = CreateInputField(mainPanel.transform, "JoinCodeInput", "参加コード", new Vector2(0f, -90f));
            CreateButton(mainPanel.transform, "JoinButton", "コードで参加", new Vector2(0f, -170f), () => {
                controller?.JoinLobbyByCode(joinCodeInput != null ? joinCodeInput.text : string.Empty);
            });

            hostPanel = CreatePanel(root.transform, "HostPanel", new Color(0.12f, 0.12f, 0.14f, 0.95f));
            CenterPanel(hostPanel.GetComponent<RectTransform>(), new Vector2(520f, 360f));
            hostCodeText = CreateText(hostPanel.transform, "HostCode", "参加コード", 36, TextAnchor.UpperCenter);
            CreateButton(hostPanel.transform, "StartMatchButton", "試合開始", new Vector2(0f, -40f), () => {
                controller?.StartOnlineMatchAsHost();
            });
            CreateButton(hostPanel.transform, "HostLeaveButton", "キャンセル", new Vector2(0f, -130f), () => {
                controller?.LeaveSession();
            });

            clientPanel = CreatePanel(root.transform, "ClientPanel", new Color(0.12f, 0.12f, 0.14f, 0.95f));
            CenterPanel(clientPanel.GetComponent<RectTransform>(), new Vector2(520f, 300f));
            clientStatusText = CreateText(clientPanel.transform, "ClientStatus", "接続中…", 32, TextAnchor.MiddleCenter);
            CreateButton(clientPanel.transform, "ClientLeaveButton", "キャンセル", new Vector2(0f, -100f), () => {
                controller?.LeaveSession();
            });
        }

        static void EnsureEventSystem() {
            if (FindObjectOfType<EventSystem>() != null) {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        static GameObject CreatePanel(Transform parent, string name, Color color) {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            var image = panel.AddComponent<Image>();
            image.color = color;
            panel.AddComponent<RectTransform>();
            return panel;
        }

        static Text CreateText(
            Transform parent,
            string name,
            string content,
            int fontSize,
            TextAnchor anchor) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            StretchFull(rect);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static Button CreateButton(
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

        static InputField CreateInputField(
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

            var input = go.AddComponent<InputField>();
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            StretchFull(textRect);
            textRect.offsetMin = new Vector2(12f, 6f);
            textRect.offsetMax = new Vector2(-12f, -6f);
            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 24;
            text.color = Color.white;
            text.supportRichText = false;

            var placeholderGo = new GameObject("Placeholder");
            placeholderGo.transform.SetParent(go.transform, false);
            var placeholderRect = placeholderGo.AddComponent<RectTransform>();
            StretchFull(placeholderRect);
            placeholderRect.offsetMin = new Vector2(12f, 6f);
            placeholderRect.offsetMax = new Vector2(-12f, -6f);
            var placeholderText = placeholderGo.AddComponent<Text>();
            placeholderText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            placeholderText.fontSize = 24;
            placeholderText.fontStyle = FontStyle.Italic;
            placeholderText.color = new Color(1f, 1f, 1f, 0.45f);
            placeholderText.text = placeholder;

            input.textComponent = text;
            input.placeholder = placeholderText;
            input.characterLimit = 8;
            input.contentType = InputField.ContentType.Alphanumeric;
            return input;
        }

        static void StretchFull(RectTransform rect) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void CenterPanel(RectTransform rect, Vector2 size) {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
