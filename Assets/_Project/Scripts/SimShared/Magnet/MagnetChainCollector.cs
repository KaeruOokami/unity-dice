namespace DiceGame.SimShared.Magnet
{
    /// <summary>
    /// Pure magnet chain collect (production <c>MagnetChainResolver</c>).
    /// Origin plus perpendicular arms of magnet-coupling dice at the same tier.
    /// </summary>
    public static class MagnetChainCollector
    {
        public const int MaxChain = 16;

        public delegate bool TryGetMagnetAt(int x, int y, int tier, out int diceId);

        public static int Collect(
            int originId,
            int originX,
            int originY,
            int tier,
            int moveDirX,
            int moveDirY,
            bool originHasMagnetCoupling,
            TryGetMagnetAt tryGetMagnetAt,
            int[] chainIds)
        {
            if (chainIds == null || chainIds.Length == 0)
            {
                return 0;
            }

            chainIds[0] = originId;
            var count = 1;
            if (!originHasMagnetCoupling || tryGetMagnetAt == null)
            {
                return count;
            }

            GetPerpendicular(moveDirX, moveDirY, out var ax0, out var ay0, out var ax1, out var ay1);
            count = CollectArm(originX, originY, tier, ax0, ay0, tryGetMagnetAt, chainIds, count);
            count = CollectArm(originX, originY, tier, ax1, ay1, tryGetMagnetAt, chainIds, count);
            return count;
        }

        static int CollectArm(
            int originX,
            int originY,
            int tier,
            int armX,
            int armY,
            TryGetMagnetAt tryGetMagnetAt,
            int[] chainIds,
            int count)
        {
            var x = originX + armX;
            var y = originY + armY;
            while (count < chainIds.Length
                   && count < MaxChain
                   && tryGetMagnetAt(x, y, tier, out var id))
            {
                chainIds[count++] = id;
                x += armX;
                y += armY;
            }

            return count;
        }

        public static void GetPerpendicular(
            int moveDirX,
            int moveDirY,
            out int ax0,
            out int ay0,
            out int ax1,
            out int ay1)
        {
            if (moveDirX != 0)
            {
                ax0 = 0;
                ay0 = 1;
                ax1 = 0;
                ay1 = -1;
                return;
            }

            ax0 = 1;
            ay0 = 0;
            ax1 = -1;
            ay1 = 0;
        }
    }
}
