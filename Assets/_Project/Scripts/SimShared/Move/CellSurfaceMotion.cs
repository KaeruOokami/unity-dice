namespace DiceGame.SimShared.Move
{
    /// <summary>
    /// Shared continuous surface-move helpers extracted from
    /// <c>CharacterTransformDriver</c> / <c>CharacterMovePlanner</c> / accel loop.
    /// Unity and Quantum both call these — do not fork roll-trigger / clamp rules.
    /// </summary>
    public static class CellSurfaceMotion
    {
        public const float EdgeEpsilon = 0.001f;

        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (maxDelta < 0f)
            {
                maxDelta = 0f;
            }

            if (current < target)
            {
                var next = current + maxDelta;
                return next > target ? target : next;
            }

            if (current > target)
            {
                var next = current - maxDelta;
                return next < target ? target : next;
            }

            return current;
        }

        /// <summary>
        /// Dominant-axis cardinalization matching <c>InputDirection.TryFromVector2</c>.
        /// </summary>
        public static bool TryGetPrimaryDirection(float moveX, float moveY, out int dx, out int dy)
        {
            dx = 0;
            dy = 0;
            var ax = moveX < 0f ? -moveX : moveX;
            var ay = moveY < 0f ? -moveY : moveY;
            if (ax <= 0f && ay <= 0f)
            {
                return false;
            }

            if (ax >= ay)
            {
                dx = moveX > 0f ? 1 : -1;
                return true;
            }

            dy = moveY > 0f ? 1 : -1;
            return true;
        }

        public static bool IsAtOrPastRollTrigger(
            float worldX,
            float worldZ,
            float cellCenterX,
            float cellCenterZ,
            int dirX,
            int dirY,
            float triggerHalfExtent)
        {
            if (dirX == 1 && dirY == 0)
            {
                return worldX >= cellCenterX + triggerHalfExtent - EdgeEpsilon;
            }

            if (dirX == -1 && dirY == 0)
            {
                return worldX <= cellCenterX - triggerHalfExtent + EdgeEpsilon;
            }

            if (dirX == 0 && dirY == 1)
            {
                return worldZ >= cellCenterZ + triggerHalfExtent - EdgeEpsilon;
            }

            if (dirX == 0 && dirY == -1)
            {
                return worldZ <= cellCenterZ - triggerHalfExtent + EdgeEpsilon;
            }

            return false;
        }

        public static void ClampToCellInterior(
            ref float worldX,
            ref float worldZ,
            float cellCenterX,
            float cellCenterZ,
            float halfExtent)
        {
            if (worldX < cellCenterX - halfExtent)
            {
                worldX = cellCenterX - halfExtent;
            }
            else if (worldX > cellCenterX + halfExtent)
            {
                worldX = cellCenterX + halfExtent;
            }

            if (worldZ < cellCenterZ - halfExtent)
            {
                worldZ = cellCenterZ - halfExtent;
            }
            else if (worldZ > cellCenterZ + halfExtent)
            {
                worldZ = cellCenterZ + halfExtent;
            }
        }

        public static void CancelMoveIntoDirection(
            float currentX,
            float currentZ,
            ref float proposedX,
            ref float proposedZ,
            int dirX,
            int dirY)
        {
            if (dirX == 1 && proposedX > currentX)
            {
                proposedX = currentX;
            }
            else if (dirX == -1 && proposedX < currentX)
            {
                proposedX = currentX;
            }

            if (dirY == 1 && proposedZ > currentZ)
            {
                proposedZ = currentZ;
            }
            else if (dirY == -1 && proposedZ < currentZ)
            {
                proposedZ = currentZ;
            }
        }

        public static void CellCenter(
            int cellX,
            int cellY,
            float cellSize,
            out float centerX,
            out float centerZ)
        {
            centerX = cellX * cellSize;
            centerZ = cellY * cellSize;
        }
    }
}
