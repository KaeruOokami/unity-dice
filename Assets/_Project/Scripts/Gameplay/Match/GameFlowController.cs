using DiceGame.Config;
using DiceGame.Gameplay.Input;
using DiceGame.Grid;
using DiceGame.Placement;
using DiceGame.Session;
using DiceGame.Session.Network;
using DiceGame.Versus;
using DiceGame.Versus.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DiceGame.Gameplay
{
    public enum GameFlowState
    {
        Playing,
        Paused,
        GameOver
    }

    public sealed class GameFlowController : MonoBehaviour
    {
        const string StandardGameOverLog = "Game Over";
        const string Player1WinLog = "Player 1";
        const string Player2WinLog = "Player 2";
        const string DrawLog = "引き分け";

        Board board;
        DiceRegistry registry;
        DiceSpawnSystem spawnSystem;
        VersusAttackController versusAttackController;
        GameSessionSettings sessionSettings;
        GameMode activeGameMode;
        int activeRequiredPlayerCount;
        GameFlowInputReader inputReader;
        PauseMenuUi pauseMenuUi;
        SessionController sessionController;
        float playingTimeScale;
        bool isConfigured;
        bool ownsTimeScale;
        bool applyingRemoteFlow;
        bool pauseOwnerIsHost;

        public GameFlowState State { get; private set; } = GameFlowState.Playing;
        public bool IsSimulationFrozen => State != GameFlowState.Playing;

        public void Configure(
            Board targetBoard,
            DiceRegistry targetRegistry,
            DiceSpawnSystem targetSpawnSystem,
            VersusAttackController targetVersusAttackController,
            GameSessionSettings targetSessionSettings,
            PlayerInputSettings playerInputSettings,
            ResolvedSessionSetup resolvedSetup = null)
        {
            if (targetBoard == null
                || targetRegistry == null
                || targetSpawnSystem == null
                || targetSessionSettings == null
                || playerInputSettings == null)
            {
                Debug.LogError("GameFlowController: Required dependencies are not assigned.");
                return;
            }

            if (Time.timeScale <= 0f)
            {
                Debug.LogError("GameFlowController: Cannot initialize while Time.timeScale is zero.");
                return;
            }

            board = targetBoard;
            registry = targetRegistry;
            spawnSystem = targetSpawnSystem;
            versusAttackController = targetVersusAttackController;
            sessionSettings = targetSessionSettings;
            activeGameMode = resolvedSetup?.GameMode ?? targetSessionSettings.GameMode;
            activeRequiredPlayerCount = resolvedSetup?.RequiredPlayerCount ?? targetSessionSettings.RequiredPlayerCount;
            playingTimeScale = Time.timeScale;
            sessionController = FindObjectOfType<SessionController>();

            inputReader = GetComponent<GameFlowInputReader>();
            if (inputReader == null)
            {
                inputReader = gameObject.AddComponent<GameFlowInputReader>();
            }

            inputReader.Configure(playerInputSettings, activeRequiredPlayerCount);

            pauseMenuUi = GetComponent<PauseMenuUi>();
            if (pauseMenuUi == null)
            {
                pauseMenuUi = gameObject.AddComponent<PauseMenuUi>();
            }

            pauseMenuUi.Configure(ResolveUiFontSettings());
            pauseMenuUi.ResumeClicked -= OnPauseMenuResumeClicked;
            pauseMenuUi.ReturnToTitleClicked -= OnPauseMenuReturnToTitleClicked;
            pauseMenuUi.ResumeClicked += OnPauseMenuResumeClicked;
            pauseMenuUi.ReturnToTitleClicked += OnPauseMenuReturnToTitleClicked;

            BindOnlineFlowEvents(true);

            State = GameFlowState.Playing;
            isConfigured = true;
            GameWorldVisibility.SetBoardVisible(board, true);
        }

        void OnDestroy()
        {
            if (pauseMenuUi != null)
            {
                pauseMenuUi.ResumeClicked -= OnPauseMenuResumeClicked;
                pauseMenuUi.ReturnToTitleClicked -= OnPauseMenuReturnToTitleClicked;
            }

            BindOnlineFlowEvents(false);

            if (ownsTimeScale)
            {
                Time.timeScale = playingTimeScale;
            }
        }

        static UiFontSettings ResolveUiFontSettings() {
            var session = FindFirstObjectByType<SessionController>();
            if (session == null || session.UiFontSettings == null) {
                Debug.LogError(
                    "GameFlowController: SessionController.UiFontSettings is not assigned.");
                return null;
            }

            return session.UiFontSettings;
        }

        void Update()
        {
            if (!isConfigured || inputReader == null)
            {
                return;
            }

            if (inputReader.WasResetPressedThisFrame())
            {
                // Online: match reset is host-only (Backspace ignored on client).
                if (!IsOnlineClient())
                {
                    RequestOrApplyResetMatch();
                }

                return;
            }

            if (inputReader.WasPausePressedThisFrame())
            {
                if (State == GameFlowState.Playing)
                {
                    RequestOrApplyPause();
                }
                else if (State == GameFlowState.Paused)
                {
                    // Only the peer that initiated the pause may resume it.
                    if (LocalIsPauseOwner())
                    {
                        RequestOrApplyResume();
                    }
                }

                return;
            }

            if (State == GameFlowState.Playing)
            {
                EvaluateGameOver();
            }
        }

        void EvaluateGameOver()
        {
            if (activeGameMode != GameMode.Versus)
            {
                if (BoardFillEvaluator.IsStandardBottomFull(board, registry))
                {
                    EnterGameOver(StandardGameOverLog);
                }

                return;
            }

            var player1Full = BoardFillEvaluator.IsVersusRegionFull(
                board,
                registry,
                PlayerSlot.Player1);
            var player2Full = BoardFillEvaluator.IsVersusRegionFull(
                board,
                registry,
                PlayerSlot.Player2);

            if (player1Full && player2Full)
            {
                EnterGameOver(DrawLog);
            }
            else if (player1Full)
            {
                EnterGameOver(Player2WinLog);
            }
            else if (player2Full)
            {
                EnterGameOver(Player1WinLog);
            }
        }

        /// <summary>
        /// Iron / Stone covering crush: crushed player loses (Versus) or Game Over (Standard).
        /// </summary>
        public void NotifyPlayerCrushed(PlayerSlot crushed)
        {
            if (State != GameFlowState.Playing)
            {
                return;
            }

            if (activeGameMode != GameMode.Versus)
            {
                EnterGameOver(StandardGameOverLog);
                return;
            }

            var winner = SinkingChainResolver.GetOpponent(crushed);
            EnterGameOver(winner == PlayerSlot.Player1 ? Player1WinLog : Player2WinLog);
        }

        void OnPauseMenuResumeClicked()
        {
            RequestOrApplyResume();
        }

        void OnPauseMenuReturnToTitleClicked()
        {
            RequestOrApplyReturnToTitle();
        }

        void RequestOrApplyPause()
        {
            if (IsOnlineClient())
            {
                sessionController.Messenger?.SendFlowRequestToServer(SessionConstants.FlowPause);
                return;
            }

            ApplyPause(broadcast: IsOnlineHost());
        }

        void RequestOrApplyResume()
        {
            if (IsOnlineClient())
            {
                sessionController.Messenger?.SendFlowRequestToServer(SessionConstants.FlowResume);
                return;
            }

            ApplyResume(broadcast: IsOnlineHost());
        }

        void RequestOrApplyResetMatch()
        {
            if (IsOnlineClient())
            {
                return;
            }

            ApplyResetMatch(broadcast: IsOnlineHost());
        }

        void RequestOrApplyReturnToTitle()
        {
            if (IsOnlineClient())
            {
                sessionController.Messenger?.SendFlowRequestToServer(SessionConstants.FlowReturnToTitle);
                return;
            }

            ApplyReturnToTitle(broadcast: IsOnlineHost());
        }

        public void ApplyPause(bool broadcast)
        {
            // Local / host-initiated pause: owner is the host side (local single = host).
            ApplyPause(broadcast, pausedByHost: true);
        }

        public void ApplyPause(bool broadcast, bool pausedByHost)
        {
            if (State == GameFlowState.Paused)
            {
                return;
            }

            pauseOwnerIsHost = pausedByHost;

            if (broadcast && !applyingRemoteFlow)
            {
                sessionController?.Messenger?.BroadcastFlowCommand(
                    SessionConstants.FlowPause,
                    pausedByHost ? 1 : 0);
            }

            State = GameFlowState.Paused;
            FreezeSimulation();
            pauseMenuUi?.Show(ResolvePauseMenuRole(), LocalIsPauseOwner());
        }

        public void ApplyResume(bool broadcast)
        {
            if (State != GameFlowState.Paused)
            {
                return;
            }

            if (broadcast && !applyingRemoteFlow)
            {
                sessionController?.Messenger?.BroadcastFlowCommand(SessionConstants.FlowResume);
            }

            pauseMenuUi?.Hide();
            Time.timeScale = playingTimeScale;
            ownsTimeScale = false;
            spawnSystem.SetGameplayEnabled(true);
            versusAttackController?.SetGameplayEnabled(true);
            // Dual-sim: both peers run attack detection locally (no client follower mode).
            versusAttackController?.SetNetworkFollowerMode(false);
            inputReader.SetGameplayInputEnabled(true);
            State = GameFlowState.Playing;
        }

        public void ApplyResetMatch(bool broadcast)
        {
            ApplyResetMatch(broadcast, explicitSeed: 0);
        }

        public void ApplyResetMatch(bool broadcast, int explicitSeed)
        {
            // Initiator (host / local) rolls a fresh seed each reset so the next
            // match differs; a client applying the host's broadcast reuses its seed.
            var matchSeed = applyingRemoteFlow
                ? explicitSeed
                : MatchRandom.CreateMatchSeed();

            if (broadcast && !applyingRemoteFlow)
            {
                sessionController?.Messenger?.BroadcastFlowCommand(
                    SessionConstants.FlowResetMatch,
                    matchSeed);
            }

            if (matchSeed != 0)
            {
                SessionState.Instance?.SetMatchSeed(matchSeed);
            }

            var playMode = SessionState.Instance != null
                ? SessionState.Instance.PlayMode
                : SessionPlayMode.Local;
            var setup = SessionState.Instance?.CurrentSetup;
            MatchFlowFlags.ArmMatchRestart(playMode, setup, matchSeed);
            ReloadActiveScene();
        }

        public void ApplyReturnToTitle(bool broadcast)
        {
            if (broadcast && !applyingRemoteFlow)
            {
                sessionController?.Messenger?.BroadcastFlowCommand(SessionConstants.FlowReturnToTitle);
            }

            MatchFlowFlags.ArmTitleReturn();
            if (sessionController != null)
            {
                sessionController.PrepareReturnToTitle();
            }

            ReloadActiveScene();
        }

        void EnterGameOver(string resultLog)
        {
            State = GameFlowState.GameOver;
            FreezeSimulation();
            pauseMenuUi?.Hide();
            Debug.Log(resultLog);
        }

        void FreezeSimulation()
        {
            inputReader.SetGameplayInputEnabled(false);
            spawnSystem.SetGameplayEnabled(false);
            versusAttackController?.SetGameplayEnabled(false);
            Time.timeScale = 0f;
            ownsTimeScale = true;
        }

        void ReloadActiveScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.buildIndex < 0)
            {
                Debug.LogError(
                    $"GameFlowController: Active scene '{activeScene.name}' is not in Build Settings.");
                return;
            }

            Time.timeScale = playingTimeScale > 0f ? playingTimeScale : 1f;
            ownsTimeScale = false;
            SceneManager.LoadScene(activeScene.buildIndex);
        }

        void BindOnlineFlowEvents(bool bind)
        {
            var messenger = sessionController != null ? sessionController.Messenger : null;
            if (messenger == null)
            {
                return;
            }

            messenger.FlowCommandReceived -= OnFlowCommandReceived;
            messenger.FlowRequestReceived -= OnFlowRequestReceived;
            if (bind)
            {
                messenger.FlowCommandReceived += OnFlowCommandReceived;
                messenger.FlowRequestReceived += OnFlowRequestReceived;
            }
        }

        void OnFlowRequestReceived(ulong senderClientId, byte command)
        {
            if (!IsOnlineHost())
            {
                return;
            }

            switch (command)
            {
                case SessionConstants.FlowPause:
                    // Client requested the pause: client owns it.
                    ApplyPause(broadcast: true, pausedByHost: false);
                    break;
                case SessionConstants.FlowResume:
                    // Only the pause owner may resume; ignore stray client requests.
                    if (!pauseOwnerIsHost)
                    {
                        ApplyResume(broadcast: true);
                    }
                    break;
                case SessionConstants.FlowResetMatch:
                    // Match reset is host-local only; ignore client requests.
                    break;
                case SessionConstants.FlowReturnToTitle:
                    // Return to title is host-only.
                    break;
            }
        }

        void OnFlowCommandReceived(byte command, int data)
        {
            if (IsOnlineHost())
            {
                // Host already applied locally before broadcast.
                return;
            }

            applyingRemoteFlow = true;
            try
            {
                switch (command)
                {
                    case SessionConstants.FlowPause:
                        ApplyPause(broadcast: false, pausedByHost: data != 0);
                        break;
                    case SessionConstants.FlowResume:
                        ApplyResume(broadcast: false);
                        break;
                    case SessionConstants.FlowResetMatch:
                        ApplyResetMatch(broadcast: false, explicitSeed: data);
                        break;
                    case SessionConstants.FlowReturnToTitle:
                        ApplyReturnToTitle(broadcast: false);
                        break;
                }
            }
            finally
            {
                applyingRemoteFlow = false;
            }
        }

        bool IsOnlineHost()
        {
            return SessionState.Instance != null
                && SessionState.Instance.PlayMode == SessionPlayMode.OnlineHost
                && NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsServer;
        }

        bool IsOnlineClient()
        {
            return SessionState.Instance != null
                && SessionState.Instance.PlayMode == SessionPlayMode.OnlineClient
                && NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsClient
                && !NetworkManager.Singleton.IsServer;
        }

        PauseMenuRole ResolvePauseMenuRole()
        {
            return IsOnlineClient() ? PauseMenuRole.Client : PauseMenuRole.Host;
        }

        bool LocalIsPauseOwner()
        {
            var session = SessionState.Instance;
            if (session == null || !session.IsOnline)
            {
                return true;
            }

            return session.IsHost == pauseOwnerIsHost;
        }
    }
}
