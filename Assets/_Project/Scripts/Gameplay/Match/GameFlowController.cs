using System;
using System.Collections;
using System.Collections.Generic;
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
        MatchSeriesHud seriesHud;
        SessionController sessionController;
        float playingTimeScale;
        float roundEndDelaySeconds;
        bool isConfigured;
        bool ownsTimeScale;
        bool applyingRemoteFlow;
        bool pauseOwnerIsHost;
        Coroutine nextRoundRoutine;
        Coroutine trainingResetRoutine;
        bool trainingResetQueued;

        public GameFlowState State { get; private set; } = GameFlowState.Playing;
        public bool IsSimulationFrozen => State != GameFlowState.Playing;
        public event Action<MatchEndEvent> MatchEnded;

        public void Configure(
            Board targetBoard,
            DiceRegistry targetRegistry,
            DiceSpawnSystem targetSpawnSystem,
            VersusAttackController targetVersusAttackController,
            GameSessionSettings targetSessionSettings,
            PlayerInputSettings playerInputSettings,
            ResolvedSessionSetup resolvedSetup = null,
            MatchSeriesHudSettings seriesHudSettings = null)
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
            sessionController = FindFirstObjectByType<SessionController>();
            roundEndDelaySeconds = ResolveRoundEndDelaySeconds(targetSessionSettings);

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

            var fontSettings = sessionController != null ? sessionController.UiFontSettings : null;
            pauseMenuUi.Configure(fontSettings);
            pauseMenuUi.ResumeClicked -= OnPauseMenuResumeClicked;
            pauseMenuUi.ReturnToTitleClicked -= OnPauseMenuReturnToTitleClicked;
            pauseMenuUi.ResumeClicked += OnPauseMenuResumeClicked;
            pauseMenuUi.ReturnToTitleClicked += OnPauseMenuReturnToTitleClicked;

            EnsureSeriesState();
            if (GameModeRules.IsVersusLike(activeGameMode))
            {
                if (seriesHudSettings == null)
                {
                    Debug.LogError("GameFlowController: MatchSeriesHudSettings is not assigned.");
                }
                else
                {
                    seriesHud = GetComponent<MatchSeriesHud>();
                    if (seriesHud == null)
                    {
                        seriesHud = gameObject.AddComponent<MatchSeriesHud>();
                    }

                    seriesHud.Configure(fontSettings, seriesHudSettings);
                }
            }

            BindOnlineFlowEvents(true);

            State = GameFlowState.Playing;
            isConfigured = true;
            BoardVisibility.SetBoardVisible(board, true);
        }

        void EnsureSeriesState()
        {
            if (activeGameMode == GameMode.Challenge)
            {
                MatchSeriesState.Clear();
                if (!ChallengeRunState.IsActive && !TryBeginChallengeRunFromCurrentSetup())
                {
                    Debug.LogError("GameFlowController: Failed to begin ChallengeRunState.");
                }

                return;
            }

            ChallengeRunState.Clear();
            if (activeGameMode != GameMode.Versus)
            {
                MatchSeriesState.Clear();
                return;
            }

            if (MatchSeriesState.IsActive)
            {
                return;
            }

            MatchSeriesState.Begin(ResolveWinsToWin());
        }

        bool TryBeginChallengeRunFromCurrentSetup()
        {
            var setup = SessionState.Instance?.CurrentSetup;
            if (setup == null || setup.GameMode != GameMode.Challenge)
            {
                return false;
            }

            var challengeSettings = sessionController != null
                ? sessionController.MatchSetupPresetRegistry?.ChallengeModeSettings
                : null;
            if (challengeSettings == null)
            {
                Debug.LogError("GameFlowController: ChallengeModeSettings is not assigned.");
                return false;
            }

            if (!challengeSettings.TryValidate(out var error))
            {
                Debug.LogError($"GameFlowController: {error}");
                return false;
            }

            var opponents = BuildChallengeOpponentAttacks(challengeSettings);
            if (opponents.Length < 1)
            {
                Debug.LogError("GameFlowController: Challenge has no opponent attacks.");
                return false;
            }

            ChallengeRunState.Begin(setup.Player1.Attack, opponents);
            return true;
        }

        static PlayerAttackSettingsData[] BuildChallengeOpponentAttacks(ChallengeModeSettings challengeSettings)
        {
            var source = challengeSettings.OpponentAttacksByMatch;
            var list = new List<PlayerAttackSettingsData>();
            for (var i = 0; i < source.Length; i++)
            {
                if (source[i] == null)
                {
                    continue;
                }

                list.Add(PlayerAttackSettingsData.FromTemplate(source[i]));
            }

            return list.ToArray();
        }

        int ResolveWinsToWin()
        {
            var setup = SessionState.Instance?.CurrentSetup;
            if (setup != null && setup.GameMode == GameMode.Versus)
            {
                return Mathf.Max(1, setup.WinsToWin);
            }

            if (sessionSettings?.VersusBoardSettings != null)
            {
                return sessionSettings.VersusBoardSettings.WinsToWin;
            }

            return 1;
        }

        float ResolveRoundEndDelaySeconds(GameSessionSettings targetSessionSettings)
        {
            if (activeGameMode == GameMode.Challenge)
            {
                var challengeSettings = sessionController != null
                    ? sessionController.MatchSetupPresetRegistry?.ChallengeModeSettings
                    : null;
                if (challengeSettings != null && challengeSettings.BoardSettings != null)
                {
                    return challengeSettings.BoardSettings.RoundEndDelaySeconds;
                }
            }

            return targetSessionSettings != null && targetSessionSettings.VersusBoardSettings != null
                ? targetSessionSettings.VersusBoardSettings.RoundEndDelaySeconds
                : 0f;
        }

        void OnDestroy()
        {
            if (nextRoundRoutine != null)
            {
                StopCoroutine(nextRoundRoutine);
                nextRoundRoutine = null;
            }

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

        void Update()
        {
            if (!isConfigured || inputReader == null)
            {
                return;
            }

            if (inputReader.WasResetPressedThisFrame())
            {
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
            if (!GameModeRules.IsVersusLike(activeGameMode))
            {
                if (BoardFillEvaluator.IsStandardBottomFull(board, registry))
                {
                    EnterStandardGameOver(StandardGameOverLog);
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
                EnterVersusRoundEnd(null, DrawLog);
            }
            else if (player1Full)
            {
                EnterVersusRoundEnd(PlayerSlot.Player2, Player2WinLog);
            }
            else if (player2Full)
            {
                EnterVersusRoundEnd(PlayerSlot.Player1, Player1WinLog);
            }
        }

        public void NotifyPlayerCrushed(PlayerSlot crushed)
        {
            if (State != GameFlowState.Playing)
            {
                return;
            }

            if (!GameModeRules.IsVersusLike(activeGameMode))
            {
                EnterStandardGameOver(StandardGameOverLog);
                return;
            }

            var winner = SinkingChainResolver.GetOpponent(crushed);
            EnterVersusRoundEnd(
                winner,
                winner == PlayerSlot.Player1 ? Player1WinLog : Player2WinLog);
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
            var matchSeed = applyingRemoteFlow
                ? explicitSeed
                : UnityEngine.Random.Range(1, int.MaxValue);

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

            ApplySeriesScoreOnReset();

            var playMode = SessionState.Instance != null
                ? SessionState.Instance.PlayMode
                : SessionPlayMode.Local;
            var setup = SessionState.Instance?.CurrentSetup;
            MatchFlowFlags.ArmMatchRestart(playMode, setup, matchSeed);
            ReloadActiveScene();
        }

        void ApplySeriesScoreOnReset()
        {
            if (activeGameMode == GameMode.Challenge)
            {
                MatchSeriesState.Clear();
                return;
            }

            if (activeGameMode != GameMode.Versus)
            {
                MatchSeriesState.Clear();
                ChallengeRunState.Clear();
                return;
            }

            var preserveSeriesScores = MatchSeriesState.IsActive
                && State == GameFlowState.GameOver
                && MatchSeriesState.Player1Wins < MatchSeriesState.WinsToWin
                && MatchSeriesState.Player2Wins < MatchSeriesState.WinsToWin;

            if (!preserveSeriesScores)
            {
                MatchSeriesState.Begin(ResolveWinsToWin());
            }
        }

        public void ApplyReturnToTitle(bool broadcast)
        {
            if (broadcast && !applyingRemoteFlow)
            {
                sessionController?.Messenger?.BroadcastFlowCommand(SessionConstants.FlowReturnToTitle);
            }

            MatchSeriesState.Clear();
            ChallengeRunState.Clear();
            MatchFlowFlags.ArmTitleReturn();
            if (sessionController != null)
            {
                sessionController.PrepareReturnToTitle();
            }

            ReloadActiveScene();
        }

        void EnterStandardGameOver(string resultLog)
        {
            if (State == GameFlowState.GameOver)
            {
                return;
            }

            State = GameFlowState.GameOver;
            FreezeSimulation();
            pauseMenuUi?.Hide();
            Debug.Log(resultLog);
            RaiseMatchEnded(new MatchEndEvent(roundWinner: null, isStandardGameOver: true));
        }

        void EnterVersusRoundEnd(PlayerSlot? roundWinner, string resultLog)
        {
            if (State == GameFlowState.GameOver)
            {
                return;
            }

            State = GameFlowState.GameOver;
            FreezeSimulation();
            pauseMenuUi?.Hide();
            Debug.Log(resultLog);
            RaiseMatchEnded(new MatchEndEvent(roundWinner, isStandardGameOver: false));

            if (HasMatchEndedListeners())
            {
                // Training / external listeners own the reset path.
                return;
            }

            if (activeGameMode == GameMode.Challenge)
            {
                EnterChallengeRoundEnd(roundWinner);
                return;
            }

            EnsureSeriesState();
            var matchComplete = MatchSeriesState.RegisterRoundResult(roundWinner, out var matchWinner);
            seriesHud?.Refresh();

            if (matchComplete)
            {
                var matchLog = matchWinner == PlayerSlot.Player1 ? Player1WinLog : Player2WinLog;
                Debug.Log($"Match Over: {matchLog}");
                return;
            }

            if (IsOnlineClient())
            {
                return;
            }

            if (nextRoundRoutine != null)
            {
                StopCoroutine(nextRoundRoutine);
            }

            nextRoundRoutine = StartCoroutine(ContinueSeriesAfterDelay());
        }

        void RaiseMatchEnded(MatchEndEvent matchEnd)
        {
            MatchEnded?.Invoke(matchEnd);
        }

        bool HasMatchEndedListeners()
        {
            return MatchEnded != null;
        }

        /// <summary>
        /// Queues a one-shot match reset on the next frame (used by ML episode boundaries).
        /// </summary>
        public void QueueTrainingMatchReset()
        {
            var session = SessionState.Instance;
            if (session != null && session.IsOnline)
            {
                return;
            }

            if (trainingResetQueued)
            {
                return;
            }

            trainingResetQueued = true;
            if (trainingResetRoutine != null)
            {
                StopCoroutine(trainingResetRoutine);
            }

            trainingResetRoutine = StartCoroutine(TrainingMatchResetNextFrame());
        }

        IEnumerator TrainingMatchResetNextFrame()
        {
            yield return null;
            trainingResetRoutine = null;
            trainingResetQueued = false;
            MatchSeriesState.Clear();
            ChallengeRunState.Clear();
            ApplyResetMatch(broadcast: false);
        }

        void EnterChallengeRoundEnd(PlayerSlot? roundWinner)
        {
            seriesHud?.Refresh();

            if (roundWinner == PlayerSlot.Player1)
            {
                if (ChallengeRunState.TryAdvanceToNextMatch(out var nextOpponentAttack))
                {
                    if (!TryApplyChallengeOpponentAttack(nextOpponentAttack))
                    {
                        Debug.LogError("GameFlowController: Failed to apply next Challenge opponent attack.");
                        return;
                    }

                    seriesHud?.Refresh();
                    if (IsOnlineClient())
                    {
                        return;
                    }

                    if (nextRoundRoutine != null)
                    {
                        StopCoroutine(nextRoundRoutine);
                    }

                    nextRoundRoutine = StartCoroutine(ContinueSeriesAfterDelay());
                    return;
                }

                Debug.Log("Challenge Clear");
                return;
            }

            Debug.Log(roundWinner == null ? "Challenge Draw" : "Challenge Failed");
        }

        static bool TryApplyChallengeOpponentAttack(PlayerAttackSettingsData opponentAttack)
        {
            var session = SessionState.Instance;
            var setup = session?.CurrentSetup;
            if (session == null || setup == null || setup.GameMode != GameMode.Challenge)
            {
                return false;
            }

            var player2 = setup.Player2;
            player2.Attack = opponentAttack;
            setup.Player2 = player2;
            session.SetCurrentSetup(setup);
            return true;
        }

        IEnumerator ContinueSeriesAfterDelay()
        {
            var remaining = Mathf.Max(0f, roundEndDelaySeconds);
            while (remaining > 0f)
            {
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            nextRoundRoutine = null;
            ApplyResetMatch(broadcast: IsOnlineHost());
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
                    ApplyPause(broadcast: true, pausedByHost: false);
                    break;
                case SessionConstants.FlowResume:
                    if (!pauseOwnerIsHost)
                    {
                        ApplyResume(broadcast: true);
                    }
                    break;
                case SessionConstants.FlowResetMatch:
                    break;
                case SessionConstants.FlowReturnToTitle:
                    break;
            }
        }

        void OnFlowCommandReceived(byte command, int data)
        {
            if (IsOnlineHost())
            {
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
                && SessionState.Instance.PlayMode == SessionPlayMode.Host
                && NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsServer;
        }

        bool IsOnlineClient()
        {
            return SessionState.Instance != null
                && SessionState.Instance.PlayMode == SessionPlayMode.Client
                && NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsClient
                && !NetworkManager.Singleton.IsServer;
        }

        PauseMenuRole ResolvePauseMenuRole()
        {
            return IsOnlineClient() ? PauseMenuRole.RemoteClient : PauseMenuRole.Owner;
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
