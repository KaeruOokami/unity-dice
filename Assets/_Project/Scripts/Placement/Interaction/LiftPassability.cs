using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.SimShared.Lift;

namespace DiceGame.Placement
{
    public static class LiftPassability
    {
        public static bool CanLift(
            CharacterPlacement standing,
            bool isOnFloor,
            DiceController standingDice,
            DiceController dice,
            DiceRegistry registry)
        {
            if (dice == null || registry == null)
            {
                return false;
            }

            if (!IsReachable(standing, dice))
            {
                return false;
            }

            var effective = DiceEffectiveBehaviorFactory.For(dice, registry);
            var hasTop = registry.HasTopAt(dice.CurrentState.GridPos);
            return LiftEligibility.CanLift(
                isOnFloor,
                standing.IsOnFloor ? 0 : (standing.Tier == DiceStackTier.Top ? 1 : 0),
                standingDice != null && dice == standingDice,
                dice.Capabilities.CanBeLiftedByPlayer,
                effective.IsPlayerMovable,
                isCarried: false,
                dice.IsErasing || dice.IsVanishing,
                dice.IsBusy,
                dice.IsSpawning,
                dice.CurrentState.Tier == DiceStackTier.Top ? 1 : 0,
                hasTop);
        }

        public static bool IsReachable(CharacterPlacement standing, DiceController dice)
        {
            if (dice == null)
            {
                return false;
            }

            var playerTier = standing.IsOnFloor ? DiceStackTier.Bottom : standing.Tier;
            var playerSlot = new DiceSlot(standing.GridCell, playerTier);
            return DiceStackAdjacency.IsAdjacentForLift(playerSlot, DiceSlot.FromDice(dice));
        }
    }
}
