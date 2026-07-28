using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    public sealed class HeightTransferBuilder
    {
        readonly System.Action<string> log;

        public HeightTransferBuilder(System.Action<string> log = null) {
            this.log = log;
        }

        public MovementTransition Build(HeightTransferAction action, in HeightTransferFacts f) {
            switch (action) {
                case HeightTransferAction.SameTierTransfer:
                    return f.CanSameTierTransfer
                        ? LogAndReturn(f, f.SameTierTransition, "ok")
                        : MovementTransition.Blocked();

                case HeightTransferAction.DissolveDescentHold:
                    return f.CanDissolveDescentHold
                        ? LogAndReturn(f, f.DissolveDescentTransition, "dissolve-hold")
                        : MovementTransition.Blocked();

                case HeightTransferAction.LowerLevelPlayerOnlyJump:
                    if (!f.CanLowerLevelPlayerOnlyJump || f.LowerLevelTarget == null) {
                        return MovementTransition.Blocked();
                    }

                    var transition = MovementTransition.Walkable(
                        f.LowerLevelTarget,
                        f.LowerLevelTargetLevel,
                        MovementTransitionRoute.HeightTransfer);
                    return LogAndReturn(f, transition, "ok");

                case HeightTransferAction.Blocked:
                default:
                    LogReject(f);
                    return MovementTransition.Blocked();
            }
        }

        MovementTransition LogAndReturn(
            in HeightTransferFacts f,
            MovementTransition transition,
            string resultKind) {
            log?.Invoke(
                $"{resultKind} from=({f.FromCell.x},{f.FromCell.y}) to=({f.ToCell.x},{f.ToCell.y}) " +
                $"dir={f.Direction} layer={f.FromLevel} standing={FormatDice(f.StandingDice)} " +
                $"target={FormatDice(transition.TargetDice)} " +
                $"standingErasing={f.StandingDice != null && f.StandingDice.IsErasing}");
            return transition;
        }

        void LogReject(in HeightTransferFacts f) {
            var reason = f.PreferCoupledGridRoll
                ? "skipped-for-coupled-grid-roll"
                : f.SameTierRejectReason ?? "no-transfer-target";
            log?.Invoke(
                $"reject {reason} from=({f.FromCell.x},{f.FromCell.y}) to=({f.ToCell.x},{f.ToCell.y}) " +
                $"dir={f.Direction} layer={f.FromLevel} standing={FormatDice(f.StandingDice)}");
        }

        static string FormatDice(DiceController dice) {
            return dice != null ? dice.name : "(none)";
        }
    }
}
