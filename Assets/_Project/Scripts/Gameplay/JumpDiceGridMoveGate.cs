using DiceGame.Config;
using DiceGame.Core;

namespace DiceGame.Gameplay
{
    public readonly struct JumpDiceGridMoveCapability
    {
        public bool IsJumping { get; }
        public bool AllowAnyDiceGridMove { get; }
        public int MaxDistance { get; }
        public bool AllowTierChange { get; }
        public float Timeline { get; }

        public JumpDiceGridMoveCapability(
            bool isJumping,
            bool allowAnyDiceGridMove,
            int maxDistance,
            bool allowTierChange,
            float timeline) {
            IsJumping = isJumping;
            AllowAnyDiceGridMove = allowAnyDiceGridMove;
            MaxDistance = maxDistance;
            AllowTierChange = allowTierChange;
            Timeline = timeline;
        }
    }

    static class JumpDiceGridMoveGate
    {
        const float TimelineEpsilon = 0.001f;
        const float ApexTimeline = 0.5f;

        public static bool TryEvaluate(
            bool isJumping,
            bool jumpDiceGridMoved,
            PhysicsSettings physicsSettings,
            VerticalMotionState jumpMotion,
            float jumpHeight,
            out JumpDiceGridMoveCapability capability) {
            capability = default;
            if (!isJumping) {
                return false;
            }

            if (jumpDiceGridMoved || physicsSettings == null || jumpHeight <= 0f) {
                capability = new JumpDiceGridMoveCapability(true, false, 0, false, 0f);
                return true;
            }

            if (!TryGetAscentTimeline(physicsSettings, jumpMotion, jumpHeight, out var timeline)) {
                TryGetFullTimeline(physicsSettings, jumpMotion, jumpHeight, out timeline);
                capability = new JumpDiceGridMoveCapability(true, false, 0, false, timeline);
                return true;
            }

            var twoCellMax = physicsSettings.JumpGridMoveTwoCellMaxTimeline;
            var oneCellMax = physicsSettings.JumpGridMoveOneCellMaxTimeline;
            if (timeline > oneCellMax + TimelineEpsilon) {
                capability = new JumpDiceGridMoveCapability(true, false, 0, false, timeline);
                return true;
            }

            var maxDistance = timeline <= twoCellMax + TimelineEpsilon
                ? RollResolver.MaxParallelRollDistance
                : 1;
            var tierMin = physicsSettings.JumpGridMoveTierChangeMinTimeline;
            var tierMax = physicsSettings.JumpGridMoveTierChangeMaxTimeline;
            var allowTierChange = timeline + TimelineEpsilon >= tierMin
                && timeline <= tierMax + TimelineEpsilon;
            capability = new JumpDiceGridMoveCapability(true, true, maxDistance, allowTierChange, timeline);
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
