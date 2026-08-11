using System;
using System.Collections.Generic;
using DiceGame.Config;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DiceGame.Session
{
    sealed class ControlsPanelUi : IDisposable
    {
        readonly InputActionAsset actionsAsset;
        readonly Transform contentRoot;
        readonly Action<string> setStatus;
        readonly List<BindingRow> rows = new();
        readonly GameObject[] playerRoots = new GameObject[2];

        InputActionRebindingExtensions.RebindingOperation activeRebind;
        BindingRow activeRow;
        bool suppressCancelStatus;

        sealed class BindingRow
        {
            public InputAction Action;
            public int BindingIndex;
            public Button Button;
            public TextMeshProUGUI ButtonLabel;
        }

        public ControlsPanelUi(
            PlayerInputSettings inputSettings,
            Transform parent,
            Action<string> statusWriter) {
            if (inputSettings == null) {
                throw new ArgumentNullException(nameof(inputSettings));
            }

            actionsAsset = inputSettings.InputActions;
            if (actionsAsset == null) {
                throw new InvalidOperationException("PlayerInputSettings.InputActions is not assigned.");
            }

            contentRoot = parent;
            setStatus = statusWriter;
            Build();
        }

        void Build() {
            SessionUiFactory.CreateLayoutLabel(contentRoot, ControlsPanelLabels.OnlineNote, 16, 36f);
            SessionUiFactory.CreateLayoutLabel(contentRoot, ControlsPanelLabels.Player, 18, 24f);
            var playerDropdown = SessionUiFactory.CreateLayoutDropdown(
                contentRoot,
                ControlsPanelLabels.PlayerSlotDropdown,
                ControlsPanelLabels.PlayerOptions,
                40f);
            playerDropdown.onValueChanged.AddListener(ShowPlayerSlot);

            playerRoots[0] = SessionUiFactory.CreateVerticalSection(contentRoot, "Player1Bindings").gameObject;
            playerRoots[1] = SessionUiFactory.CreateVerticalSection(contentRoot, "Player2Bindings").gameObject;
            BuildPlayerBindings(PlayerSlot.Player1, playerRoots[0].transform);
            BuildPlayerBindings(PlayerSlot.Player2, playerRoots[1].transform);

            SessionUiFactory.CreateLayoutButton(
                contentRoot,
                "ResetBindingsButton",
                ControlsPanelLabels.ResetDefaults,
                44f,
                ResetToDefaults);

            ShowPlayerSlot(0);
            RefreshAllLabels();
        }

        void BuildPlayerBindings(PlayerSlot slot, Transform parent) {
            var mapName = slot == PlayerSlot.Player1
                ? PlayerInputSettings.Player1ActionMap
                : PlayerInputSettings.Player2ActionMap;
            var map = actionsAsset.FindActionMap(mapName, throwIfNotFound: true);
            var move = map.FindAction(PlayerInputSettings.MoveAction, throwIfNotFound: true);
            var lift = map.FindAction(PlayerInputSettings.LiftAction, throwIfNotFound: true);
            var jump = map.FindAction(PlayerInputSettings.JumpAction, throwIfNotFound: true);

            AddCompositeRow(parent, ControlsPanelLabels.MoveUp, move, PlayerInputSettings.MovePartUp);
            AddCompositeRow(parent, ControlsPanelLabels.MoveDown, move, PlayerInputSettings.MovePartDown);
            AddCompositeRow(parent, ControlsPanelLabels.MoveLeft, move, PlayerInputSettings.MovePartLeft);
            AddCompositeRow(parent, ControlsPanelLabels.MoveRight, move, PlayerInputSettings.MovePartRight);
            AddButtonRow(parent, ControlsPanelLabels.Lift, lift);
            AddButtonRow(parent, ControlsPanelLabels.Jump, jump);
        }

        void AddCompositeRow(
            Transform parent,
            string label,
            InputAction action,
            string partName) {
            if (!PlayerBindingOverridesService.TryFindBindingIndex(
                    action,
                    PlayerInputSettings.KeyboardScheme,
                    partName,
                    out var bindingIndex)) {
                Debug.LogError(
                    $"[ControlsPanelUi] Missing keyboard binding for {action.actionMap.name}/{action.name}.{partName}.");
                return;
            }

            AddRow(parent, label, action, bindingIndex);
        }

        void AddButtonRow(Transform parent, string label, InputAction action) {
            if (!PlayerBindingOverridesService.TryFindBindingIndex(
                    action,
                    PlayerInputSettings.KeyboardScheme,
                    null,
                    out var bindingIndex)) {
                Debug.LogError(
                    $"[ControlsPanelUi] Missing keyboard binding for {action.actionMap.name}/{action.name}.");
                return;
            }

            AddRow(parent, label, action, bindingIndex);
        }

        void AddRow(Transform parent, string label, InputAction action, int bindingIndex) {
            SessionUiFactory.CreateLayoutLabel(parent, label, 18, 24f);
            var button = SessionUiFactory.CreateLayoutButton(
                parent,
                $"{label}Rebind",
                string.Empty,
                40f,
                () => { });
            var buttonLabel = button.GetComponentInChildren<TextMeshProUGUI>();
            var row = new BindingRow {
                Action = action,
                BindingIndex = bindingIndex,
                Button = button,
                ButtonLabel = buttonLabel
            };
            button.onClick.AddListener(() => BeginRebind(row));
            rows.Add(row);
        }

        void ShowPlayerSlot(int index) {
            CancelRebind();
            var showPlayer1 = index == 0;
            if (playerRoots[0] != null) {
                playerRoots[0].SetActive(showPlayer1);
            }

            if (playerRoots[1] != null) {
                playerRoots[1].SetActive(!showPlayer1);
            }

            if (contentRoot is RectTransform contentRect) {
                SessionUiFactory.ForceRebuildLayout(contentRect);
            }
        }

        void BeginRebind(BindingRow row) {
            if (row?.Action == null) {
                return;
            }

            CancelRebind();
            activeRow = row;
            SetRowLabel(row, ControlsPanelLabels.WaitingForInput);
            setStatus?.Invoke(ControlsPanelLabels.WaitingForInput);

            row.Action.Disable();
            activeRebind = row.Action.PerformInteractiveRebinding(row.BindingIndex)
                .WithControlsExcluding("<Mouse>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(OnRebindComplete)
                .OnCancel(OnRebindCancelled);
            activeRebind.Start();
        }

        void OnRebindComplete(InputActionRebindingExtensions.RebindingOperation operation) {
            var row = activeRow;
            CleanupRebindOperation(enableAction: true);
            if (row == null) {
                return;
            }

            if (PlayerBindingOverridesService.TryGetDuplicateKeyboardBindingMessage(
                    actionsAsset,
                    out var conflict)) {
                row.Action.RemoveBindingOverride(row.BindingIndex);
                RefreshAllLabels();
                setStatus?.Invoke(conflict);
                return;
            }

            if (!PlayerBindingOverridesService.TrySave(actionsAsset, out var saveError)) {
                setStatus?.Invoke(saveError ?? "Failed to save key bindings.");
                RefreshAllLabels();
                return;
            }

            RefreshAllLabels();
            setStatus?.Invoke("Key binding saved.");
        }

        void OnRebindCancelled(InputActionRebindingExtensions.RebindingOperation operation) {
            CleanupRebindOperation(enableAction: true);
            RefreshAllLabels();
            if (!suppressCancelStatus) {
                setStatus?.Invoke(ControlsPanelLabels.RebindCancelled);
            }
        }

        void ResetToDefaults() {
            CancelRebind();
            if (!PlayerBindingOverridesService.TryReset(actionsAsset, out var error)) {
                setStatus?.Invoke(error ?? "Failed to reset key bindings.");
                return;
            }

            RefreshAllLabels();
            setStatus?.Invoke("Key bindings reset to defaults.");
        }

        void RefreshAllLabels() {
            for (var i = 0; i < rows.Count; i++) {
                RefreshRowLabel(rows[i]);
            }
        }

        static void RefreshRowLabel(BindingRow row) {
            if (row?.Action == null || row.ButtonLabel == null) {
                return;
            }

            var display = row.Action.GetBindingDisplayString(row.BindingIndex);
            SetRowLabel(row, string.IsNullOrWhiteSpace(display) ? "-" : display);
        }

        static void SetRowLabel(BindingRow row, string text) {
            if (row?.ButtonLabel != null) {
                row.ButtonLabel.text = text;
            }
        }

        public void CancelRebind() {
            if (activeRebind == null) {
                return;
            }

            suppressCancelStatus = true;
            try {
                activeRebind.Cancel();
            } finally {
                suppressCancelStatus = false;
            }
        }

        void CleanupRebindOperation(bool enableAction) {
            var action = activeRow?.Action;
            if (activeRebind != null) {
                activeRebind.Dispose();
                activeRebind = null;
            }

            activeRow = null;
            if (enableAction) {
                action?.Enable();
            }
        }

        public void Dispose() {
            CancelRebind();
        }
    }
}
