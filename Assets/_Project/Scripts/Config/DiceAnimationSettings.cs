using DiceGame.Session;
using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "DiceAnimationSettings", menuName = "Dice/Dice Animation Settings")]
    public class DiceAnimationSettings : ScriptableObject
    {
        [SerializeField] float rollAnimationDuration = 0.3f;
        [SerializeField] float jumpParallelRollDurationPerCell = 0.3f;
        [SerializeField] float slideDuration = 0.3f;
        [SerializeField] float fallHorizontalDuration = 0.3f;
        [SerializeField] float liftDuration = 0.3f;
        [SerializeField] float placeDuration = 0.3f;
        [SerializeField] float throwDuration = 0.45f;
        [SerializeField] float throwSpinTurns = 2f;
        [SerializeField, Min(1)] int jumpArcMoveTicksPerCell = 12;

        public float RollAnimationDuration => rollAnimationDuration;
        public float JumpParallelRollDurationPerCell => jumpParallelRollDurationPerCell;
        public float SlideDuration => slideDuration;
        public float FallHorizontalDuration => fallHorizontalDuration;
        public float LiftDuration => liftDuration;
        public float PlaceDuration => placeDuration;
        public float ThrowDuration => throwDuration;
        public float ThrowSpinTurns => throwSpinTurns;
        /// <summary>
        /// Sim ticks to travel one cell on a jump-arc dice move (at <see cref="SessionConstants.SimTickHz"/>).
        /// </summary>
        public int JumpArcMoveTicksPerCell => jumpArcMoveTicksPerCell;

        public float GetThrowDuration(int cellDistance = 1) {
            // Flight time is distance-independent so vertical arc timing stays constant;
            // longer throws simply move faster horizontally.
            return throwDuration;
        }

        public float GetGroundParallelRollDuration(int distance) {
            return rollAnimationDuration * Mathf.Max(1, distance);
        }

        public float GetJumpParallelRollDuration(int distance) {
            return jumpParallelRollDurationPerCell * Mathf.Max(1, distance);
        }

        /// <summary>
        /// Seconds to travel <paramref name="pathLength"/> at jump-arc speed
        /// (<see cref="jumpArcMoveTicksPerCell"/> ticks per cell of path length).
        /// </summary>
        public float GetJumpArcPathDurationSeconds(
            float pathLength,
            float cellSize,
            float rollDurationMultiplier = 1f) {
            var ticksPerCell = Mathf.Max(1, jumpArcMoveTicksPerCell)
                * Mathf.Max(0.01f, rollDurationMultiplier);
            var safeCellSize = Mathf.Max(0.01f, cellSize);
            var cells = pathLength <= 0.0001f
                ? 1f
                : pathLength / safeCellSize;
            return cells * ticksPerCell * SessionConstants.SimTickSeconds;
        }
    }
}
