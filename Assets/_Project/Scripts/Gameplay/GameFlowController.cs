using DiceGame.Config;
using DiceGame.Gameplay.Input;
using DiceGame.Grid;
using DiceGame.Placement;
using DiceGame.Versus;
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
        GameFlowInputReader inputReader;
        float playingTimeScale;
        bool isConfigured;
        bool ownsTimeScale;

        public GameFlowState State { get; private set; } = GameFlowState.Playing;
        public bool IsSimulationFrozen => State != GameFlowState.Playing;

        public void Configure(
            Board targetBoard,
            DiceRegistry targetRegistry,
            DiceSpawnSystem targetSpawnSystem,
            VersusAttackController targetVersusAttackController,
            GameSessionSettings targetSessionSettings,
            PlayerInputSettings playerInputSettings)
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
            playingTimeScale = Time.timeScale;

            inputReader = GetComponent<GameFlowInputReader>();
            if (inputReader == null)
            {
                inputReader = gameObject.AddComponent<GameFlowInputReader>();
            }

            inputReader.Configure(playerInputSettings);
            State = GameFlowState.Playing;
            isConfigured = true;
        }

        void Update()
        {
            if (!isConfigured || inputReader == null)
            {
                return;
            }

            if (inputReader.WasResetPressedThisFrame())
            {
                ResetGame();
                return;
            }

            if (inputReader.WasPausePressedThisFrame())
            {
                if (State == GameFlowState.Playing)
                {
                    EnterPaused();
                }
                else if (State == GameFlowState.Paused)
                {
                    ResumePlaying();
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
            if (sessionSettings.GameMode != GameMode.Versus)
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

        void EnterPaused()
        {
            State = GameFlowState.Paused;
            FreezeSimulation();
        }

        void ResumePlaying()
        {
            Time.timeScale = playingTimeScale;
            ownsTimeScale = false;
            spawnSystem.SetGameplayEnabled(true);
            versusAttackController?.SetGameplayEnabled(true);
            inputReader.SetGameplayInputEnabled(true);
            State = GameFlowState.Playing;
        }

        void EnterGameOver(string resultLog)
        {
            State = GameFlowState.GameOver;
            FreezeSimulation();
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

        void ResetGame()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.buildIndex < 0)
            {
                Debug.LogError(
                    $"GameFlowController: Active scene '{activeScene.name}' is not in Build Settings.");
                return;
            }

            Time.timeScale = playingTimeScale;
            ownsTimeScale = false;
            SceneManager.LoadScene(activeScene.buildIndex);
        }

        void OnDestroy()
        {
            if (ownsTimeScale)
            {
                Time.timeScale = playingTimeScale;
            }
        }
    }
}
