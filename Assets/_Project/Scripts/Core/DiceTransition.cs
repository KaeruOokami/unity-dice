using UnityEngine;

namespace DiceGame.Core
{
    public enum DiceTransitionPath
    {
        Direct,
        HorizontalThenDrop,
        FreeMove
    }

    public struct DiceTransition
    {
        public DiceState From;
        public DiceState To;
        public DiceTransitionPath Path;
        public Vector3? FromWorldOverride;
        public Vector3? ToWorldOverride;
        public bool SnapToGridOnComplete;

        public static DiceTransition GridMove(DiceState from, DiceState to) {
            var path = from.Tier == DiceStackTier.Top && to.Tier == DiceStackTier.Bottom
                ? DiceTransitionPath.HorizontalThenDrop
                : DiceTransitionPath.Direct;

            return new DiceTransition {
                From = from,
                To = to,
                Path = path,
                SnapToGridOnComplete = true
            };
        }

        public static DiceTransition FreeMove(
            Vector3 fromWorld,
            Vector3 toWorld,
            bool snapToGridOnComplete,
            DiceState toState = default) {
            return new DiceTransition {
                To = toState,
                Path = DiceTransitionPath.FreeMove,
                FromWorldOverride = fromWorld,
                ToWorldOverride = toWorld,
                SnapToGridOnComplete = snapToGridOnComplete
            };
        }

        public static DiceTransition CrushDemote(DiceState from, DiceState to, Vector3 fromWorld) {
            return new DiceTransition {
                From = from,
                To = to,
                Path = DiceTransitionPath.HorizontalThenDrop,
                FromWorldOverride = fromWorld,
                SnapToGridOnComplete = true
            };
        }
    }
}
