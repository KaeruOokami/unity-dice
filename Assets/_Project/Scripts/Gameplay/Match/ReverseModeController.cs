using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Placement;
using DiceGame.View;
using UnityEngine;

namespace DiceGame.Gameplay
{
    /// <summary>
    /// Timed global reverse mode: inverts pip labels on all dice (mesh) and match faces,
    /// while attack effects remap through <see cref="DiceGame.Versus.Core.AttackFaceReverse"/>.
    /// Logical <see cref="DiceOrientation"/> stays physical so rolls keep working.
    /// </summary>
    public sealed class ReverseModeController : MonoBehaviour
    {
        ReverseModeSettings settings;
        DiceRegistry registry;
        IReadOnlyList<CharacterController> characters;
        ReverseScreenInvertView screenInvertView;
        bool gameplayEnabled = true;
        bool isActive;
        float remainingSeconds;

        public bool IsActive => isActive;
        public float RemainingSeconds => isActive ? remainingSeconds : 0f;

        public void Configure(
            ReverseModeSettings reverseSettings,
            DiceRegistry diceRegistry,
            IReadOnlyList<CharacterController> spawnedCharacters) {
            settings = reverseSettings;
            registry = diceRegistry;
            characters = spawnedCharacters;
            EnsureScreenInvertView();
            gameplayEnabled = false;
            ForceClearWithoutFlip();
        }

        public void SetGameplayEnabled(bool enabled) {
            if (gameplayEnabled == enabled) {
                return;
            }

            gameplayEnabled = enabled;
            if (!enabled) {
                ForceDeactivate();
            }
        }

        public void ApplyNetworkState(bool active, float remaining) {
            isActive = active;
            remainingSeconds = active ? Mathf.Max(0f, remaining) : 0f;
            DiceFaceLabels.Reversed = active;
            RefreshAllPresentation();
        }

        void Update() {
            if (GameplaySimClock.IsActive) {
                return;
            }

            SimulateLockstepTick(Time.deltaTime);
        }

        public void SimulateLockstepTick(float deltaTime) {
            if (!gameplayEnabled || settings == null || registry == null || deltaTime <= 0f) {
                return;
            }

            PollActivationInput();
            if (!isActive) {
                return;
            }

            remainingSeconds -= deltaTime;
            if (remainingSeconds > 0f) {
                return;
            }

            Deactivate();
        }

        void PollActivationInput() {
            if (characters == null || settings == null) {
                return;
            }

            for (var i = 0; i < characters.Count; i++) {
                var character = characters[i];
                if (character == null || !character.WasReversePressedThisFrame()) {
                    continue;
                }

                TryActivate();
                return;
            }
        }

        void TryActivate() {
            if (settings == null) {
                return;
            }

            if (isActive) {
                remainingSeconds = settings.DurationSeconds;
                return;
            }

            isActive = true;
            remainingSeconds = settings.DurationSeconds;
            DiceFaceLabels.Reversed = true;
            RefreshAllPresentation();
        }

        void Deactivate() {
            if (!isActive) {
                return;
            }

            isActive = false;
            remainingSeconds = 0f;
            DiceFaceLabels.Reversed = false;
            RefreshAllPresentation();
        }

        void ForceDeactivate() {
            if (!isActive && !DiceFaceLabels.Reversed) {
                return;
            }

            isActive = false;
            remainingSeconds = 0f;
            DiceFaceLabels.Reversed = false;
            RefreshAllPresentation();
        }

        void ForceClearWithoutFlip() {
            isActive = false;
            remainingSeconds = 0f;
            DiceFaceLabels.Reversed = false;
            RefreshAllPresentation();
        }

        void RefreshAllPresentation() {
            EnsureScreenInvertView();
            screenInvertView?.SetVisible(DiceFaceLabels.Reversed);

            if (registry == null) {
                return;
            }

            var allDice = registry.AllDice;
            for (var i = 0; i < allDice.Count; i++) {
                var dice = allDice[i];
                if (dice == null) {
                    continue;
                }

                dice.RefreshReversePresentation();
            }
        }

        void EnsureScreenInvertView() {
            if (screenInvertView != null) {
                return;
            }

            screenInvertView = GetComponent<ReverseScreenInvertView>();
            if (screenInvertView == null) {
                screenInvertView = gameObject.AddComponent<ReverseScreenInvertView>();
            }
        }
    }
}
