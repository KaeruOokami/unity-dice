using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;

namespace DiceGame.Placement
{
    /// <summary>
    /// Executes ice slide-until-blocked moves and elastic momentum transfer
    /// onto a stationary collision partner with <see cref="DiceCapabilities.TransfersSlideOnCollision"/>.
    /// </summary>
    public static class IceElasticSlideExecutor
    {
        public static bool TryExecute(
            DiceController mover,
            Direction direction,
            DiceRegistry registry,
            PlayerSlot actionOwner) {
            if (mover == null
                || registry == null
                || !mover.Capabilities.SlideUntilBlocked
                || mover.IsBusy) {
                return false;
            }

            if (!IceSlidePassability.TryBuildUntilBlocked(
                mover.CurrentState,
                direction,
                registry,
                out var plan,
                out var transferTarget,
                out _)) {
                return false;
            }

            if (!mover.Capabilities.TransfersSlideOnCollision) {
                transferTarget = null;
            }

            if (IceSlidePassability.HasSlideDisplacement(plan)) {
                return mover.TryExecuteSlidePlan(plan, actionOwner, direction, transferTarget);
            }

            return TryBeginTransfer(transferTarget, direction, registry, actionOwner);
        }

        public static bool TryBeginTransfer(
            DiceController transferTarget,
            Direction direction,
            DiceRegistry registry,
            PlayerSlot actionOwner) {
            if (transferTarget == null
                || registry == null
                || transferTarget.IsBusy
                || !transferTarget.Capabilities.TransfersSlideOnCollision) {
                return false;
            }

            return TryExecute(transferTarget, direction, registry, actionOwner);
        }
    }
}
