using DiceGame.Core;
using DiceGame.Gameplay;
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
        JumpTopFallRoll
    }

    public struct CharacterMovePlan
    {
        public CharacterMoveKind Kind;
        public Vector2Int FromCell;
        public Vector2Int ToCell;
        public Direction Direction;
        public MovementTransition Transition;
        public CoupledMoveIntent CoupledIntent;
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
