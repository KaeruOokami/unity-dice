using System;
using System.Collections;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Gameplay.AI.Application;
using DiceGame.Gameplay.Input;
using DiceGame.Session;
using DiceGame.Versus;
using Unity.MLAgents;
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
        readonly List<Behaviour> aiControllers = new();
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

            aiControllers.Clear();
            if (characters != null) {
                for (var i = 0; i < characters.Count; i++) {
                    if (characters[i] == null) {
                        continue;
                    }

                    CollectAiControllers(characters[i].gameObject);
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
            if (LaunchArgs.SkipIntro) {
                introUi?.Hide();
                while (attackController != null && !attackController.AreIconsReady) {
                    yield return null;
                }

                ReleaseGameplayAndComplete();
                introRoutine = null;
                yield break;
            }

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

            for (var i = 0; i < aiControllers.Count; i++) {
                if (aiControllers[i] != null) {
                    aiControllers[i].enabled = !gated;
                }
            }
        }

        void CollectAiControllers(GameObject characterObject) {
            var brain = characterObject.GetComponent<AiCharacterBrain>();
            if (brain != null && brain.enabled) {
                aiControllers.Add(brain);
            }

            var agent = characterObject.GetComponent<MlCharacterAgent>();
            if (agent != null && agent.enabled) {
                aiControllers.Add(agent);
            }

            var decisionRequester = characterObject.GetComponent<DecisionRequester>();
            if (decisionRequester != null && decisionRequester.enabled) {
                aiControllers.Add(decisionRequester);
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
