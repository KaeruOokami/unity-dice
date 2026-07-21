using DiceGame.Gameplay;
using DiceGame.Gameplay.Input;
using DiceGame.Config;
using UnityEngine;

namespace DiceGame.Session.Network
{
    /// <summary>
    /// Client-side pause/reset UI and local freeze. Authority remains on host.
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

            pauseMenuUi.Configure();
            pauseMenuUi.ResumeClicked += () => {
                messenger?.SendFlowRequestToServer(OnlineSessionConstants.FlowResume);
            };
            pauseMenuUi.ReturnToTitleClicked += () => {
                messenger?.SendFlowRequestToServer(OnlineSessionConstants.FlowReturnToTitle);
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

            if (inputReader.WasResetPressedThisFrame()) {
                messenger.SendFlowRequestToServer(OnlineSessionConstants.FlowResetMatch);
                return;
            }

            if (!inputReader.WasPausePressedThisFrame()) {
                return;
            }

            messenger.SendFlowRequestToServer(
                paused ? OnlineSessionConstants.FlowResume : OnlineSessionConstants.FlowPause);
        }

        void OnFlowCommandReceived(byte command) {
            switch (command) {
                case OnlineSessionConstants.FlowPause:
                    ApplyPaused();
                    break;
                case OnlineSessionConstants.FlowResume:
                    ApplyResumed();
                    break;
                case OnlineSessionConstants.FlowResetMatch:
                    MatchFlowFlags.ArmMatchRestart(
                        OnlinePlayMode.OnlineClient,
                        OnlineSessionState.Instance?.CurrentSetup);
                    Time.timeScale = playingTimeScale;
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                    break;
                case OnlineSessionConstants.FlowReturnToTitle:
                    MatchFlowFlags.ArmTitleReturn();
                    var session = FindObjectOfType<OnlineSessionController>();
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
            pauseMenuUi?.Show(allowHostActions: false);
        }

        void ApplyResumed() {
            paused = false;
            Time.timeScale = playingTimeScale;
            pauseMenuUi?.Hide();
        }
    }
}
