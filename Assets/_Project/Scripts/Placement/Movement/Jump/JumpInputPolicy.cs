using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using SharedJump = DiceGame.SimShared.Jump;

namespace DiceGame.Placement
{
    /// <summary>
    /// Production wrapper over <see cref="SharedJump.JumpInputPolicy"/> (copied Domain).
    /// </summary>
    public readonly struct JumpCoupledMoveCapability
    {
        public bool IsJumping { get; }
        public bool AllowCrossCellMove { get; }
        public bool AllowDiceGridMove { get; }
        public int MaxDistance { get; }
        public bool AllowTierChange { get; }
        public float Timeline { get; }

        public JumpCoupledMoveCapability(
            bool isJumping,
            bool allowCrossCellMove,
            bool allowDiceGridMove,
            int maxDistance,
            bool allowTierChange,
            float timeline)
        {
            IsJumping = isJumping;
            AllowCrossCellMove = allowCrossCellMove;
            AllowDiceGridMove = allowDiceGridMove;
            MaxDistance = maxDistance;
            AllowTierChange = allowTierChange;
            Timeline = timeline;
        }

        public static JumpCoupledMoveCapability FromShared(SharedJump.JumpCoupledMoveCapability c)
        {
            return new JumpCoupledMoveCapability(
                c.IsJumping,
                c.AllowCrossCellMove,
                c.AllowDiceGridMove,
                c.MaxDistance,
                c.AllowTierChange,
                c.Timeline);
        }
    }

    public static class JumpInputPolicy
    {
        public static bool TryEvaluate(
            bool isJumping,
            bool jumpDiceGridMoved,
            PhysicsSettings physicsSettings,
            VerticalMotionState jumpMotion,
            float jumpHeight,
            out JumpCoupledMoveCapability capability)
        {
            capability = default;
            if (physicsSettings == null)
            {
                return false;
            }

            var config = new SharedJump.JumpInputPolicy.WindowConfig
            {
                Gravity = physicsSettings.Gravity,
                TwoCellMaxTimeline = physicsSettings.JumpGridMoveTwoCellMaxTimeline,
                OneCellMaxTimeline = physicsSettings.JumpGridMoveOneCellMaxTimeline,
                TierChangeMinTimeline = physicsSettings.JumpGridMoveTierChangeMinTimeline,
                TierChangeMaxTimeline = physicsSettings.JumpGridMoveTierChangeMaxTimeline,
            };

            if (!SharedJump.JumpInputPolicy.TryEvaluate(
                    isJumping,
                    jumpDiceGridMoved,
                    in config,
                    jumpMotion,
                    jumpHeight,
                    out var result))
            {
                return false;
            }

            capability = JumpCoupledMoveCapability.FromShared(result);
            return true;
        }

        public static JumpCoupledMoveCapability ApplyPlayerOnlyJumpOverride(
            JumpCoupledMoveCapability capability,
            bool canJumpCoupleWithPlayer)
        {
            var shared = new SharedJump.JumpCoupledMoveCapability(
                capability.IsJumping,
                capability.AllowCrossCellMove,
                capability.AllowDiceGridMove,
                capability.MaxDistance,
                capability.AllowTierChange,
                capability.Timeline);
            return JumpCoupledMoveCapability.FromShared(
                SharedJump.JumpInputPolicy.ApplyPlayerOnlyJumpOverride(shared, canJumpCoupleWithPlayer));
        }

        public static JumpCoupledMoveCapability ApplyStandingDiceOverrides(
            JumpCoupledMoveCapability capability,
            DiceController standingDice)
        {
            var shared = new SharedJump.JumpCoupledMoveCapability(
                capability.IsJumping,
                capability.AllowCrossCellMove,
                capability.AllowDiceGridMove,
                capability.MaxDistance,
                capability.AllowTierChange,
                capability.Timeline);
            var canJumpCouple = standingDice == null || standingDice.CanJumpCoupleWithPlayer;
            var blocksCross = standingDice != null && standingDice.Capabilities.BlocksJumpCrossCellMove;
            var blocksUp = standingDice != null && standingDice.Capabilities.BlocksJumpUpwardTierChange;
            var isSink = standingDice != null && standingDice.IsSinkErasing;
            return JumpCoupledMoveCapability.FromShared(
                SharedJump.JumpInputPolicy.ApplyStandingDiceOverrides(
                    shared,
                    canJumpCouple,
                    isSink,
                    blocksCross,
                    blocksUp));
        }
    }
}
