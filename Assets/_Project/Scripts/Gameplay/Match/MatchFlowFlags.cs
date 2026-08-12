using DiceGame.Config;
using DiceGame.Session;

namespace DiceGame.Gameplay
{
    /// <summary>
    /// Survives scene reload for Plan A match reset / title return.
    /// </summary>
    public static class MatchFlowFlags
    {
        public static bool SkipTitleOnNextLoad;
        public static SessionPlayMode ResumePlayMode = SessionPlayMode.Local;
        public static MatchSetupSnapshot PendingSetup;
        public static int PendingMatchSeed;

        public static void ArmMatchRestart(
            SessionPlayMode playMode,
            MatchSetupSnapshot setup = null,
            int matchSeed = 0) {
            SkipTitleOnNextLoad = true;
            ResumePlayMode = playMode == SessionPlayMode.Unspecified
                ? SessionPlayMode.Local
                : playMode;
            PendingSetup = setup?.Clone();
            PendingMatchSeed = matchSeed;
        }

        public static void ArmTitleReturn() {
            SkipTitleOnNextLoad = false;
            ResumePlayMode = SessionPlayMode.Unspecified;
            PendingSetup = null;
            PendingMatchSeed = 0;
            MatchSeriesState.Clear();
        }

        public static bool ConsumeSkipTitle(out SessionPlayMode playMode) {
            if (!SkipTitleOnNextLoad) {
                playMode = SessionPlayMode.Unspecified;
                return false;
            }

            SkipTitleOnNextLoad = false;
            playMode = ResumePlayMode;
            ResumePlayMode = SessionPlayMode.Unspecified;
            return true;
        }

        public static MatchSetupSnapshot ConsumePendingSetup() {
            var setup = PendingSetup;
            PendingSetup = null;
            return setup;
        }

        public static int ConsumePendingMatchSeed() {
            var seed = PendingMatchSeed;
            PendingMatchSeed = 0;
            return seed;
        }
    }
}
