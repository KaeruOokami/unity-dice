namespace DiceGame.SimShared.Motion
{
    using DiceGame.Core;

    /// <summary>
    /// Copied from production <c>DiceGame.Core.DiceGridMovePlanner</c>.
    /// </summary>
    public static class DiceGridMovePlanner
    {
        public static bool TryBuildPlan(
            DiceState fromState,
            Direction direction,
            int distance,
            DiceStackTier landingTier,
            DiceGridMoveKind kind,
            out DiceGridMovePlan plan,
            out string rejectReason)
        {
            plan = default;
            rejectReason = null;

            if (distance < 1 || distance > DiceGridRollLimits.MaxParallelRollDistance)
            {
                rejectReason = $"distance-out-of-range distance={distance}";
                return false;
            }

            var expectedKind = ResolveMoveKind(fromState.Tier, landingTier);
            if (kind != expectedKind)
            {
                rejectReason = $"kind-mismatch expected={expectedKind} actual={kind}";
                return false;
            }

            direction.GetGridDelta(out var dx, out var dy);
            var landingX = fromState.GridX + dx * distance;
            var landingY = fromState.GridY + dy * distance;
            if (!TryBuildRolledState(fromState, landingX, landingY, landingTier, direction, distance, out var toState))
            {
                rejectReason = $"landing=({landingX},{landingY}) invalid-orientation";
                return false;
            }

            plan = new DiceGridMovePlan
            {
                From = fromState,
                To = toState,
                Kind = kind,
                Direction = direction,
                Distance = distance
            };
            return true;
        }

        public static DiceGridMoveKind ResolveMoveKind(DiceStackTier fromTier, DiceStackTier toTier)
        {
            if (fromTier == toTier)
            {
                return DiceGridMoveKind.Parallel;
            }

            return fromTier == DiceStackTier.Top
                ? DiceGridMoveKind.Demote
                : DiceGridMoveKind.Stack;
        }

        static bool TryBuildRolledState(
            DiceState fromState,
            int landingX,
            int landingY,
            DiceStackTier landingTier,
            Direction direction,
            int distance,
            out DiceState toState)
        {
            toState = default;
            var orientation = fromState.Orientation;
            var capabilities = DiceBehaviorResolver.GetBehavior(fromState.Kind).Capabilities;
            if (capabilities.PreservesOrientationOnGridMove)
            {
                if (!orientation.IsValid())
                {
                    return false;
                }
            }
            else
            {
                for (var step = 0; step < distance; step++)
                {
                    orientation = orientation.Roll(direction);
                    if (!orientation.IsValid())
                    {
                        return false;
                    }
                }
            }

            toState = new DiceState(landingX, landingY, orientation, landingTier, fromState.Kind);
            return true;
        }
    }
}
