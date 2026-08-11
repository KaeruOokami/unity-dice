using System;
using System.Collections;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Gameplay.AI.Application;
using DiceGame.Gameplay.Input;
using DiceGame.Versus;
using UnityEngine;

namespace DiceGame.Gameplay
{
    /// <summary>
    /// Match start cue: Ready → Start, while player control and continuous dice spawn stay gated.
    /// Initial board dice are spawned before this runs and are not blocked.
    /// </summary>
    public sealed class MatchIntroController : MonoBehaviour
    {
        MatchIntroSettings settings;
        MatchIntroUi introUi;
        GameFlowInputReader inputReader;
        DiceSpawnSystem spawnSystem;
        VersusAttackController attackController;
        readonly List<AiCharacterBrain> aiBrains = new();
        Coroutine introRoutine;
        bool gameplayReleased;

        public bool IsComplete { get; private set; }
        public event Action Completed;

        public void Configure(
            MatchIntroSettings introSettings,
            UiFontSettings fontSettings,
            GameFlowInputReader flowInputReader,
            DiceSpawnSystem targetSpawnSystem,
            VersusAttackController targetAttackController,
            IReadOnlyList<CharacterController> characters) {
            settings = introSettings;
            inputReader = flowInputReader;
            spawnSystem = targetSpawnSystem;
            attackController = targetAttackController;

            aiBrains.Clear();
            if (characters != null) {
                for (var i = 0; i < characters.Count; i++) {
                    if (characters[i] == null) {
                        continue;
                    }

                    var brain = characters[i].GetComponent<AiCharacterBrain>();
                    if (brain != null) {
                        aiBrains.Add(brain);
                    }
                }
            }

            if (settings == null) {
                Debug.LogError("MatchIntroController: MatchIntroSettings is not assigned.");
                return;
            }

            introUi = GetComponent<MatchIntroUi>();
            if (introUi == null) {
                introUi = gameObject.AddComponent<MatchIntroUi>();
            }

            introUi.Configure(fontSettings, settings);
            IsComplete = false;
            gameplayReleased = false;
        }

        public void Begin() {
            if (settings == null) {
                Debug.LogError("MatchIntroController.Begin: Configure was not completed.");
                ReleaseGameplayAndComplete();
                return;
            }

            if (introRoutine != null) {
                StopCoroutine(introRoutine);
            }

            IsComplete = false;
            gameplayReleased = false;
            introRoutine = StartCoroutine(RunIntro());
        }

        void OnDisable() {
            if (introRoutine != null) {
                StopCoroutine(introRoutine);
                introRoutine = null;
            }

            introUi?.Hide();
        }

        IEnumerator RunIntro() {
            SetGameplayGated(true);

            introUi?.Show(settings.ReadyText);
            yield return WaitRealtime(settings.ReadyDurationSeconds);

            introUi?.Show(settings.StartText);
            yield return WaitRealtime(settings.StartDurationSeconds);

            introUi?.Hide();

            while (attackController != null && !attackController.AreIconsReady) {
                yield return null;
            }

            ReleaseGameplayAndComplete();
            introRoutine = null;
        }

        void ReleaseGameplayAndComplete() {
            if (gameplayReleased) {
                return;
            }

            gameplayReleased = true;
            SetGameplayGated(false);
            IsComplete = true;
            Completed?.Invoke();
        }

        void SetGameplayGated(bool gated) {
            inputReader?.SetGameplayInputEnabled(!gated);
            spawnSystem?.SetGameplayEnabled(!gated);
            attackController?.SetGameplayEnabled(!gated);

            for (var i = 0; i < aiBrains.Count; i++) {
                if (aiBrains[i] != null) {
                    aiBrains[i].enabled = !gated;
                }
            }
        }

        static IEnumerator WaitRealtime(float seconds) {
            if (seconds <= 0f) {
                yield break;
            }

            var remaining = seconds;
            while (remaining > 0f) {
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
