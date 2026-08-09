namespace DiceGame.SimShared.Jump
{
    using DiceGame.Core;

    /// <summary>
    /// Copied from production <c>DiceGame.Placement.JumpInputPolicy</c>.
    /// PhysicsSettings / DiceController dependencies replaced with plain config + capability flags.
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
    }

    public static class JumpInputPolicy
    {
        const float TimelineEpsilon = 0.001f;
        const float ApexTimeline = 0.5f;

        public struct WindowConfig
        {
            public float Gravity;
            public float TwoCellMaxTimeline;
            public float OneCellMaxTimeline;
            public float TierChangeMinTimeline;
            public float TierChangeMaxTimeline;
        }

        public static bool TryEvaluate(
            bool isJumping,
            bool jumpDiceGridMoved,
            in WindowConfig config,
            VerticalMotionState jumpMotion,
            float jumpHeight,
            out JumpCoupledMoveCapability capability)
        {
            capability = default;
            if (!isJumping)
            {
                return false;
            }

            if (jumpDiceGridMoved || config.Gravity <= 0f || jumpHeight <= 0f)
            {
                capability = new JumpCoupledMoveCapability(true, false, false, 0, false, 0f);
                return true;
            }

            if (!TryGetAscentTimeline(in config, jumpMotion, jumpHeight, out var timeline))
            {
                TryGetFullTimeline(in config, jumpMotion, jumpHeight, out timeline);
                capability = new JumpCoupledMoveCapability(true, false, false, 0, false, timeline);
                return true;
            }

            var twoCellMax = config.TwoCellMaxTimeline;
            var oneCellMax = config.OneCellMaxTimeline;
            if (timeline > oneCellMax + TimelineEpsilon)
            {
                capability = new JumpCoupledMoveCapability(true, false, false, 0, false, timeline);
                return true;
            }

            var maxDistance = timeline <= twoCellMax + TimelineEpsilon
                ? DiceGridRollLimits.MaxParallelRollDistance
                : 1;
            var tierMin = config.TierChangeMinTimeline;
            var tierMax = config.TierChangeMaxTimeline;
            var allowTierChange = timeline + TimelineEpsilon >= tierMin
                && timeline <= tierMax + TimelineEpsilon;
            capability = new JumpCoupledMoveCapability(
                true,
                true,
                true,
                maxDistance,
                allowTierChange,
                timeline);
            return true;
        }

        public static JumpCoupledMoveCapability ApplyPlayerOnlyJumpOverride(
            JumpCoupledMoveCapability capability,
            bool canJumpCoupleWithPlayer)
        {
            if (!capability.IsJumping || canJumpCoupleWithPlayer)
            {
                return capability;
            }

            return new JumpCoupledMoveCapability(
                capability.IsJumping,
                allowCrossCellMove: true,
                allowDiceGridMove: false,
                maxDistance: 0,
                allowTierChange: true,
                capability.Timeline);
        }

        /// <summary>
        /// Copied from production ApplyStandingDiceOverrides (DiceController → flags).
        /// </summary>
        public static JumpCoupledMoveCapability ApplyStandingDiceOverrides(
            JumpCoupledMoveCapability capability,
            bool canJumpCoupleWithPlayer,
            bool isSinkErasing,
            bool blocksJumpCrossCellMove,
            bool blocksJumpUpwardTierChange)
        {
            if (!capability.IsJumping)
            {
                return capability;
            }

            var coupleOk = canJumpCoupleWithPlayer && !isSinkErasing;
            capability = ApplyPlayerOnlyJumpOverride(capability, coupleOk);

            if (blocksJumpCrossCellMove)
            {
                var allowTierLanding = capability.AllowCrossCellMove;
                return new JumpCoupledMoveCapability(
                    capability.IsJumping,
                    allowCrossCellMove: allowTierLanding,
                    allowDiceGridMove: false,
                    maxDistance: allowTierLanding ? 1 : 0,
                    allowTierChange: allowTierLanding,
                    capability.Timeline);
            }

            if (blocksJumpUpwardTierChange && capability.AllowTierChange)
            {
                capability = new JumpCoupledMoveCapability(
                    capability.IsJumping,
                    capability.AllowCrossCellMove,
                    capability.AllowDiceGridMove,
                    capability.MaxDistance,
                    allowTierChange: false,
                    capability.Timeline);
            }

            return capability;
        }

        static bool TryGetAscentTimeline(
            in WindowConfig config,
            VerticalMotionState jumpMotion,
            float jumpHeight,
            out float timeline)
        {
            if (!TryGetFullTimeline(in config, jumpMotion, jumpHeight, out timeline))
            {
                return false;
            }

            return timeline <= ApexTimeline + TimelineEpsilon;
        }

        static bool TryGetFullTimeline(
            in WindowConfig config,
            VerticalMotionState jumpMotion,
            float jumpHeight,
            out float timeline)
        {
            timeline = 0f;
            var launchVelocityY = GravityMotion.ComputeLaunchVelocity(jumpHeight, config.Gravity);
            timeline = GravityMotion.ComputeFullJumpTimeline(jumpMotion, launchVelocityY, jumpHeight);
            return true;
        }
    }
}
