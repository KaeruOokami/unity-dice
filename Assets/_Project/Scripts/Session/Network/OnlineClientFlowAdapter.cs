using DiceGame.Gameplay;
using DiceGame.Gameplay.Input;
using DiceGame.Config;
using UnityEngine;

namespace DiceGame.Session.Network
{
    /// <summary>
    /// Legacy client-side pause/reset UI. Dual-sim uses <see cref="GameFlowController"/> instead.
    /// </summary>
    public sealed class OnlineClientFlowAdapter : MonoBehaviour
    {
        OnlineNetMessenger messenger;
        GameFlowInputReader inputReader;
        PauseMenuUi pauseMenuUi;
        float playingTimeScale = 1f;
        bool paused;

        public void Configure(OnlineNetMessenger netMessenger, PlayerInputSettings playerInputSettings) {
            messenger = netMessenger;
            playingTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;

            inputReader = GetComponent<GameFlowInputReader>();
            if (inputReader == null) {
                inputReader = gameObject.AddComponent<GameFlowInputReader>();
            }

            inputReader.Configure(playerInputSettings, requiredPlayerCount: 1);

            pauseMenuUi = GetComponent<PauseMenuUi>();
            if (pauseMenuUi == null) {
                pauseMenuUi = gameObject.AddComponent<PauseMenuUi>();
            }

            var sessionController = FindFirstObjectByType<SessionController>();
            pauseMenuUi.Configure(sessionController != null ? sessionController.UiFontSettings : null);
            pauseMenuUi.ResumeClicked += () => {
                messenger?.SendFlowRequestToServer(SessionConstants.FlowResume);
            };

            if (messenger != null) {
                messenger.FlowCommandReceived += OnFlowCommandReceived;
            }
        }

        void OnDestroy() {
            if (messenger != null) {
                messenger.FlowCommandReceived -= OnFlowCommandReceived;
            }
        }

        void Update() {
            if (inputReader == null || messenger == null) {
                return;
            }

            // Match reset is host-only.
            if (inputReader.WasResetPressedThisFrame()) {
                return;
            }

            if (!inputReader.WasPausePressedThisFrame()) {
                return;
            }

            messenger.SendFlowRequestToServer(
                paused ? SessionConstants.FlowResume : SessionConstants.FlowPause);
        }

        void OnFlowCommandReceived(byte command, int data) {
            switch (command) {
                case SessionConstants.FlowPause:
                    ApplyPaused();
                    break;
                case SessionConstants.FlowResume:
                    ApplyResumed();
                    break;
                case SessionConstants.FlowResetMatch:
                    if (data != 0) {
                        SessionState.Instance?.SetMatchSeed(data);
                    }

                    // Legacy adapter: treat reset as a fresh series start.
                    {
                        var setup = SessionState.Instance?.CurrentSetup;
                        if (setup != null && setup.GameMode == GameMode.Versus) {
                            MatchSeriesState.Begin(Mathf.Max(1, setup.WinsToWin));
                        } else {
                            MatchSeriesState.Clear();
                        }
                    }

                    MatchFlowFlags.ArmMatchRestart(
                        SessionPlayMode.Client,
                        SessionState.Instance?.CurrentSetup,
                        data != 0 ? data : SessionState.Instance?.MatchSeed ?? 0);
                    Time.timeScale = playingTimeScale;
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                    break;
                case SessionConstants.FlowReturnToTitle:
                    MatchFlowFlags.ArmTitleReturn();
                    var session = FindFirstObjectByType<SessionController>();
                    session?.PrepareReturnToTitle();
                    Time.timeScale = playingTimeScale;
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                    break;
            }
        }

        void ApplyPaused() {
            paused = true;
            Time.timeScale = 0f;
            pauseMenuUi?.Show(PauseMenuRole.RemoteClient, canOperate: false);
        }

        void ApplyResumed() {
            paused = false;
            Time.timeScale = playingTimeScale;
            pauseMenuUi?.Hide();
        }
    }
}
