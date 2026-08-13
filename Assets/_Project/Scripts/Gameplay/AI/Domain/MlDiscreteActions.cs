using DiceGame.Core;

namespace DiceGame.Gameplay.AI.Domain
{
    /// <summary>
    /// Discrete action ids for ML-Agents. Must stay aligned with BehaviorParameters branch size.
    /// </summary>
    public static class MlDiscreteActions
    {
        public const int BranchIndex = 0;
        public const int MoveEast = 0;
        public const int MoveWest = 1;
        public const int MoveNorth = 2;
        public const int MoveSouth = 3;
        public const int Jump = 4;
        public const int Lift = 5;
        public const int Wait = 6;
        public const int Count = 7;

        public static bool TryGetMoveDirection(int actionId, out Direction direction) {
            switch (actionId) {
                case MoveEast:
                    direction = Direction.East;
                    return true;
                case MoveWest:
                    direction = Direction.West;
                    return true;
                case MoveNorth:
                    direction = Direction.North;
                    return true;
                case MoveSouth:
                    direction = Direction.South;
                    return true;
                default:
                    direction = default;
                    return false;
            }
        }

        public static string ToLabel(int actionId) {
            return actionId switch {
                MoveEast => "MoveEast",
                MoveWest => "MoveWest",
                MoveNorth => "MoveNorth",
                MoveSouth => "MoveSouth",
                Jump => "Jump",
                Lift => "Lift",
                Wait => "Wait",
                _ => $"Unknown({actionId})"
            };
        }
    }
}
