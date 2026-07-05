using DiceGame.Core;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.Character
{
    public enum CharacterMoveKind
    {
        None,
        FaceSlide,
        Transfer,
        CoupledDiceMove,
        StepToFloor,
        Blocked
    }

    public enum CoupledMoveIntent
    {
        GroundParallelRoll,
        GroundTopFallRoll,
        JumpGridMove,
        JumpTopFallRoll,
        GroundIceSlide
    }

    public struct CharacterMovePlan
    {
        public CharacterMoveKind Kind;
        public Vector2Int FromCell;
        public Vector2Int ToCell;
        public Direction Direction;
        public MovementTransition Transition;
        public CoupledMoveIntent CoupledIntent;
        public bool HasDiceGridMovePlan;
        public DiceGridMovePlan DiceGridMovePlan;
        public bool HasDiceSlidePlan;
        public DiceSlidePlan DiceSlidePlan;
        public bool BlockFailedJumpGridFallback;
        public bool BlockJumpStackTransfer;
        public string BlockReason;

        public static CharacterMovePlan FaceSlide(Vector2Int cell) {
            return new CharacterMovePlan {
                Kind = CharacterMoveKind.FaceSlide,
                FromCell = cell,
                ToCell = cell
            };
        }

        public static CharacterMovePlan None(Vector2Int cell) {
            return FaceSlide(cell);
        }
    }
}
