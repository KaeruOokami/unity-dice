namespace DiceGame.SimShared.Push
{
    /// <summary>
    /// Push-follow contact XZ matching production
    /// <c>CharacterController.SyncPositionToPushingDice</c>.
    /// </summary>
    public static class PushFollowMotion
    {
        public static void ContactWorldXZ(
            float diceWorldX,
            float diceWorldZ,
            int dirX,
            int dirY,
            float cellHalf,
            float pushRadius,
            out float followerX,
            out float followerZ)
        {
            var contactOffset = cellHalf + pushRadius;
            followerX = diceWorldX;
            followerZ = diceWorldZ;

            if (dirX == 1 && dirY == 0)
            {
                followerX = diceWorldX - contactOffset;
                return;
            }

            if (dirX == -1 && dirY == 0)
            {
                followerX = diceWorldX + contactOffset;
                return;
            }

            if (dirX == 0 && dirY == 1)
            {
                followerZ = diceWorldZ - contactOffset;
                return;
            }

            if (dirX == 0 && dirY == -1)
            {
                followerZ = diceWorldZ + contactOffset;
            }
        }

        public static void Lerp(
            float fromX,
            float fromZ,
            float toX,
            float toZ,
            float t01,
            out float x,
            out float z)
        {
            if (t01 < 0f)
            {
                t01 = 0f;
            }
            else if (t01 > 1f)
            {
                t01 = 1f;
            }

            x = fromX + (toX - fromX) * t01;
            z = fromZ + (toZ - fromZ) * t01;
        }
    }
}
