using UnityEngine;

namespace DiceGame.Placement
{
    public static class NormalizedHeight
    {
        public static float ToNormalized(float worldY, float floorWorldY, float cellSize) {
            if (cellSize <= Mathf.Epsilon) {
                return 0f;
            }

            return (worldY - floorWorldY) / cellSize;
        }

        public static float ToWorld(float normalizedY, float floorWorldY, float cellSize) {
            return floorWorldY + normalizedY * cellSize;
        }
    }
}
