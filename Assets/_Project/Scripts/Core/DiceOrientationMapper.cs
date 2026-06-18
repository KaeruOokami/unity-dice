using UnityEngine;

namespace DiceGame.Core
{
    public static class DiceRollTransform
    {
        public static Quaternion GetRollRotation(Direction direction) {
            var setup = GetRollSetup(direction, 1f);
            return Quaternion.AngleAxis(setup.Angle, setup.Axis);
        }

        public static RollSetup GetRollSetup(Direction direction, float half) {
            var y = -half;
            return direction switch {
                Direction.East => new RollSetup(new Vector3(half, y, 0f), Vector3.forward, -90f),
                Direction.West => new RollSetup(new Vector3(-half, y, 0f), Vector3.forward, 90f),
                Direction.North => new RollSetup(new Vector3(0f, y, half), Vector3.right, 90f),
                Direction.South => new RollSetup(new Vector3(0f, y, -half), Vector3.right, -90f),
                _ => default
            };
        }
    }

    public readonly struct RollSetup
    {
        public readonly Vector3 PivotOffset;
        public readonly Vector3 Axis;
        public readonly float Angle;

        public RollSetup(Vector3 pivotOffset, Vector3 axis, float angle) {
            PivotOffset = pivotOffset;
            Axis = axis;
            Angle = angle;
        }
    }

    public static class DiceOrientationMapper
    {
        public static Quaternion ToRotation(DiceOrientation orientation) {
            var srcUp = FaceMeshNormal(orientation.Top);
            var srcForward = FaceMeshNormal(orientation.North);
            var srcRight = FaceMeshNormal(orientation.East);

            var srcMatrix = Matrix4x4.identity;
            srcMatrix.SetColumn(0, srcRight);
            srcMatrix.SetColumn(1, srcUp);
            srcMatrix.SetColumn(2, srcForward);
            srcMatrix.SetColumn(3, new Vector4(0f, 0f, 0f, 1f));

            var dstMatrix = Matrix4x4.identity;
            dstMatrix.SetColumn(0, Vector3.right);
            dstMatrix.SetColumn(1, Vector3.up);
            dstMatrix.SetColumn(2, Vector3.forward);
            dstMatrix.SetColumn(3, new Vector4(0f, 0f, 0f, 1f));

            return (dstMatrix * srcMatrix.inverse).rotation;
        }

        public static DiceOrientation FromRotation(Quaternion rotation) {
            return new DiceOrientation(
                ClosestFace(rotation, Vector3.up),
                ClosestFace(rotation, Vector3.forward),
                ClosestFace(rotation, Vector3.right));
        }

        static int ClosestFace(Quaternion rotation, Vector3 worldDirection) {
            var bestFace = 1;
            var bestDot = float.NegativeInfinity;

            for (var face = 1; face <= 6; face++) {
                var dot = Vector3.Dot(rotation * FaceMeshNormal(face), worldDirection);
                if (dot > bestDot) {
                    bestDot = dot;
                    bestFace = face;
                }
            }

            return bestFace;
        }

        public static Vector3 FaceMeshNormal(int face) {
            return face switch {
                1 => Vector3.up,
                2 => Vector3.forward,
                3 => Vector3.right,
                4 => Vector3.left,
                5 => Vector3.back,
                6 => Vector3.down,
                _ => Vector3.up
            };
        }
    }
}
