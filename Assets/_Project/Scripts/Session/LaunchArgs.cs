using System;
using DiceGame.Config;
using UnityEngine;

namespace DiceGame.Session
{
    /// <summary>
    /// Command-line launch overrides for standalone builds.
    /// ML-Agents forwards these via <c>--env-args</c>.
    /// </summary>
    public static class LaunchArgs
    {
        public const string TrainFlag = "--dice-train";
        public const string ModeFlag = "--dice-mode";
        public const string SkipIntroFlag = "--dice-skip-intro";

        public static bool IsTrainingLaunch { get; private set; }
        public static bool ForceMlAgent { get; private set; }
        public static bool SkipIntro { get; private set; }
        public static GameMode TrainingGameMode { get; private set; } = GameMode.Single;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Parse() {
            IsTrainingLaunch = false;
            ForceMlAgent = false;
            SkipIntro = false;
            TrainingGameMode = GameMode.Single;

            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++) {
                var arg = args[i];
                if (arg == TrainFlag) {
                    IsTrainingLaunch = true;
                    ForceMlAgent = true;
                    SkipIntro = true;
                    continue;
                }

                if (arg == SkipIntroFlag) {
                    SkipIntro = true;
                    continue;
                }

                if (arg != ModeFlag) {
                    continue;
                }

                if (i + 1 >= args.Length) {
                    Debug.LogError($"LaunchArgs: {ModeFlag} requires a GameMode value.");
                    continue;
                }

                var raw = args[++i];
                if (!Enum.TryParse(raw, ignoreCase: true, out GameMode parsed)) {
                    Debug.LogError($"LaunchArgs: Unknown game mode '{raw}'. Using {GameMode.Single}.");
                    TrainingGameMode = GameMode.Single;
                    continue;
                }

                TrainingGameMode = parsed;
            }

            if (IsTrainingLaunch) {
                Debug.Log(
                    $"LaunchArgs: training launch mode={TrainingGameMode} forceMlAgent={ForceMlAgent} skipIntro={SkipIntro}");
            }
        }

        /// <summary>
        /// Ensures the trained player slot is AI-controlled so MlCharacterAgent is spawned.
        /// </summary>
        public static void ApplyTrainingPlayerSetup(MatchSetupSnapshot snapshot) {
            if (snapshot == null || !IsTrainingLaunch) {
                return;
            }

            var player1 = snapshot.Player1;
            player1.IsAi = true;
            snapshot.Player1 = player1;
        }
    }
}
