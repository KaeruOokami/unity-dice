using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    public readonly struct PushContactCandidate
    {
        public DiceController Dice { get; }
        public Direction Direction { get; }
        public float InputAlignment { get; }
        public float FaceDistance { get; }

        public PushContactCandidate(
            DiceController dice,
            Direction direction,
            float inputAlignment,
            float faceDistance) {
            Dice = dice;
            Direction = direction;
            InputAlignment = inputAlignment;
            FaceDistance = faceDistance;
        }
    }

    /// <summary>
    /// Pure push-contact geometry (no Physics). CharacterController only runs overlap queries.
    /// </summary>
    public static class PushContactEvaluator
    {
        const float EdgeEpsilon = 0.001f;

        public static bool TryEvaluate(
            Bounds bounds,
            Vector2 characterPosition,
            Vector2 input,
            Direction direction,
            float minInputAlignment,
            out float inputAlignment,
            out float faceDistance,
            out string rejectReason) {
            rejectReason = null;
            inputAlignment = Vector2.Dot(input, GetDirectionInputVector(direction));
            if (inputAlignment < minInputAlignment) {
                faceDistance = 0f;
                rejectReason = $"input={inputAlignment:F2}<{minInputAlignment:F2}";
                return false;
            }

            var charX = characterPosition.x;
            var charZ = characterPosition.y;

            switch (direction) {
                case Direction.East:
                    if (charX > bounds.center.x + EdgeEpsilon) {
                        faceDistance = 0f;
                        rejectReason = $"charX={charX:F3}>centerX={bounds.center.x:F3}";
                        return false;
                    }

                    faceDistance = Mathf.Abs(charX - bounds.min.x);
                    break;
                case Direction.West:
                    if (charX < bounds.center.x - EdgeEpsilon) {
                        faceDistance = 0f;
                        rejectReason = $"charX={charX:F3}<centerX={bounds.center.x:F3}";
                        return false;
                    }

                    faceDistance = Mathf.Abs(charX - bounds.max.x);
                    break;
                case Direction.North:
                    if (charZ > bounds.center.z + EdgeEpsilon) {
                        faceDistance = 0f;
                        rejectReason = $"charZ={charZ:F3}>centerZ={bounds.center.z:F3}";
                        return false;
                    }

                    faceDistance = Mathf.Abs(charZ - bounds.min.z);
                    break;
                case Direction.South:
                    if (charZ < bounds.center.z - EdgeEpsilon) {
                        faceDistance = 0f;
                        rejectReason = $"charZ={charZ:F3}<centerZ={bounds.center.z:F3}";
                        return false;
                    }

                    faceDistance = Mathf.Abs(charZ - bounds.max.z);
                    break;
                default:
                    faceDistance = 0f;
                    rejectReason = "invalidDirection";
                    return false;
            }

            return true;
        }

        public static void TryAdd(
            System.Collections.Generic.List<PushContactCandidate> candidates,
            DiceController dice,
            Bounds bounds,
            Vector2 input,
            Vector2 characterPosition,
            Direction direction,
            float minInputAlignment) {
            if (!TryEvaluate(
                bounds,
                characterPosition,
                input,
                direction,
                minInputAlignment,
                out var inputAlignment,
                out var faceDistance,
                out _)) {
                return;
            }

            candidates.Add(new PushContactCandidate(dice, direction, inputAlignment, faceDistance));
        }

        public static int Compare(PushContactCandidate a, PushContactCandidate b) {
            var alignmentCompare = b.InputAlignment.CompareTo(a.InputAlignment);
            if (alignmentCompare != 0) {
                return alignmentCompare;
            }

            return a.FaceDistance.CompareTo(b.FaceDistance);
        }

        public static Vector2 GetDirectionInputVector(Direction direction) {
            return direction switch {
                Direction.East => Vector2.right,
                Direction.West => Vector2.left,
                Direction.North => Vector2.up,
                Direction.South => Vector2.down,
                _ => Vector2.zero
            };
        }
    }
}
