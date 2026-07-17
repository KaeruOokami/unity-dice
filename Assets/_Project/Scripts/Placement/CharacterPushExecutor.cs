using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;

namespace DiceGame.Placement
{
    /// <summary>
    /// Executes a push according to <see cref="IDiceBehavior.ResolvePushStyle"/>.
    /// </summary>
    public static class CharacterPushExecutor
    {
        public static bool TryExecute(
            DiceController dice,
            Direction direction,
            DiceRegistry registry,
            MovementTransitionEvaluator movementTransition,
            float footingWorldY,
            PlayerSlot playerSlot) {
            if (dice == null || registry == null || movementTransition == null) {
                return false;
            }

            switch (dice.Behavior.ResolvePushStyle()) {
                case DicePushMoveStyle.Roll: {
                    var context = PassabilityContext.ForGround(footingWorldY, playerSlot);
                    return movementTransition.TryBuildGridMovePlan(
                            dice.CurrentState,
                            direction,
                            1,
                            context,
                            out var rollPlan,
                            out _)
                        && dice.TryExecuteGroundMovePlan(rollPlan, context);
                }

                case DicePushMoveStyle.SlideUntilBlocked:
                    return IceSlidePassability.TryBuildUntilBlocked(
                            dice.CurrentState,
                            direction,
                            registry,
                            out var iceSlidePlan,
                            out _)
                        && dice.TryExecuteSlidePlan(iceSlidePlan, playerSlot);

                default:
                    return DiceSlidePassability.TryEvaluate(
                            dice.CurrentState,
                            direction,
                            registry,
                            out var normalSlidePlan,
                            out _)
                        && dice.TryExecuteSlidePlan(normalSlidePlan, playerSlot);
            }
        }

        public static bool LimitsPushFollowToOneCell(DiceController dice) {
            return dice != null
                && dice.Behavior.ResolvePushStyle() == DicePushMoveStyle.SlideUntilBlocked;
        }
    }
}
