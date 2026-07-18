using DiceGame.Session;

namespace DiceGame.Gameplay
{
    /// <summary>
    /// Survives scene reload for Plan A match reset / title return.
    /// </summary>
    public static class MatchFlowFlags
    {
        public static bool SkipTitleOnNextLoad;
        public static OnlinePlayMode ResumePlayMode = OnlinePlayMode.Local;

        public static void ArmMatchRestart(OnlinePlayMode playMode) {
            SkipTitleOnNextLoad = true;
            ResumePlayMode = playMode == OnlinePlayMode.Unspecified
                ? OnlinePlayMode.Local
                : playMode;
        }

        public static void ArmTitleReturn() {
            SkipTitleOnNextLoad = false;
            ResumePlayMode = OnlinePlayMode.Unspecified;
        }

        public static bool ConsumeSkipTitle(out OnlinePlayMode playMode) {
            if (!SkipTitleOnNextLoad) {
                playMode = OnlinePlayMode.Unspecified;
                return false;
            }

            SkipTitleOnNextLoad = false;
            playMode = ResumePlayMode;
            ResumePlayMode = OnlinePlayMode.Unspecified;
            return true;
        }
    }
}
