using DiceGame.Config;
using DiceGame.Core;

namespace DiceGame.Gameplay.Coupling
{
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
            float timeline) {
            IsJumping = isJumping;
            AllowCrossCellMove = allowCrossCellMove;
            AllowDiceGridMove = allowDiceGridMove;
            MaxDistance = maxDistance;
            AllowTierChange = allowTierChange;
            Timeline = timeline;
        }
    }

    static class JumpCoupledMoveGate
    {
        const float TimelineEpsilon = 0.001f;
        const float ApexTimeline = 0.5f;

        public static bool TryEvaluate(
            bool isJumping,
            bool jumpDiceGridMoved,
            PhysicsSettings physicsSettings,
            VerticalMotionState jumpMotion,
            float jumpHeight,
            out JumpCoupledMoveCapability capability) {
            capability = default;
            if (!isJumping) {
                return false;
            }

            if (jumpDiceGridMoved || physicsSettings == null || jumpHeight <= 0f) {
                capability = new JumpCoupledMoveCapability(true, false, false, 0, false, 0f);
                return true;
            }

            if (!TryGetAscentTimeline(physicsSettings, jumpMotion, jumpHeight, out var timeline)) {
                TryGetFullTimeline(physicsSettings, jumpMotion, jumpHeight, out timeline);
                capability = new JumpCoupledMoveCapability(true, false, false, 0, false, timeline);
                return true;
            }

            var twoCellMax = physicsSettings.JumpGridMoveTwoCellMaxTimeline;
            var oneCellMax = physicsSettings.JumpGridMoveOneCellMaxTimeline;
            if (timeline > oneCellMax + TimelineEpsilon) {
                capability = new JumpCoupledMoveCapability(true, false, false, 0, false, timeline);
                return true;
            }

            var maxDistance = timeline <= twoCellMax + TimelineEpsilon
                ? RollResolver.MaxParallelRollDistance
                : 1;
            var tierMin = physicsSettings.JumpGridMoveTierChangeMinTimeline;
            var tierMax = physicsSettings.JumpGridMoveTierChangeMaxTimeline;
            var allowTierChange = timeline + TimelineEpsilon >= tierMin
                && timeline <= tierMax + TimelineEpsilon;
            var allowed = true;
            capability = new JumpCoupledMoveCapability(
                true,
                allowed,
                allowed,
                maxDistance,
                allowTierChange,
                timeline);
            return true;
        }

        static bool TryGetAscentTimeline(
            PhysicsSettings physicsSettings,
            VerticalMotionState jumpMotion,
            float jumpHeight,
            out float timeline) {
            if (!TryGetFullTimeline(physicsSettings, jumpMotion, jumpHeight, out timeline)) {
                return false;
            }

            return timeline <= ApexTimeline + TimelineEpsilon;
        }

        static bool TryGetFullTimeline(
            PhysicsSettings physicsSettings,
            VerticalMotionState jumpMotion,
            float jumpHeight,
            out float timeline) {
            timeline = 0f;
            var launchVelocityY = GravityMotion.ComputeLaunchVelocity(jumpHeight, physicsSettings.Gravity);
            timeline = GravityMotion.ComputeFullJumpTimeline(jumpMotion, launchVelocityY, jumpHeight);
            return true;
        }
    }
}
