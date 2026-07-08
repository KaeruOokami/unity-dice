using DiceGame.Core;
using DiceGame.Gameplay;

namespace DiceGame.Placement
{
    public static class PushPassability
    {
        public static bool CanPush(
            CharacterPlacement standing,
            bool isOnFloor,
            DiceController standingDice,
            DiceController dice,
            DiceRegistry registry,
            out string rejectReason) {
            rejectReason = null;
            if (dice == null || registry == null) {
                rejectReason = dice == null ? "nullDice" : "nullRegistry";
                return false;
            }

            if (standingDice != null && dice == standingDice) {
                rejectReason = "standingDice";
                return false;
            }

            if (dice.IsVanishing) {
                rejectReason = "vanishing";
                return false;
            }

            if (!dice.Capabilities.CanBePushedByPlayer) {
                rejectReason = "notPushable";
                return false;
            }

            if (!IronAdjacencyBlock.IsPlayerMovable(dice, registry)) {
                rejectReason = "notPlayerMovable";
                return false;
            }

            if (!IsReachable(standing, isOnFloor, dice)) {
                rejectReason = "notReachable";
                return false;
            }

            if (isOnFloor) {
                if (dice.CurrentState.Tier != DiceStackTier.Bottom) {
                    rejectReason = "floorRequiresBottom";
                    return false;
                }

                if (registry.HasTopAt(dice.CurrentState.GridPos)) {
                    rejectReason = "floorRequiresNoTop";
                    return false;
                }

                return true;
            }

            if (dice.CurrentState.Tier != DiceStackTier.Top) {
                rejectReason = "onDiceRequiresTop";
                return false;
            }

            return true;
        }

        public static bool IsReachable(CharacterPlacement standing, bool isOnFloor, DiceController dice) {
            if (dice == null) {
                return false;
            }

            var playerTier = isOnFloor ? DiceStackTier.Bottom : standing.Tier;
            var playerSlot = new DiceSlot(standing.GridCell, playerTier);
            return DiceStackAdjacency.IsAdjacentForPush(
                playerSlot,
                DiceSlot.FromDice(dice),
                isOnFloor);
        }
    }
}
