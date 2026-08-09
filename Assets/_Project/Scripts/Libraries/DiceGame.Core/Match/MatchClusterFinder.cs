namespace DiceGame.Core
{
    /// <summary>
    /// Snapshot for Domain match flood-fill (production <c>DiceMatchFinder</c>).
    /// </summary>
    public struct MatchDiceSnapshot
    {
        public int Id;
        public int CellX;
        public int CellY;
        public int Tier; // 0=Bottom, 1=Top
        public int TopFace;
        public bool HasExpandedFootprint;
        public bool IsSinkErasing;
        public bool KeepsJumboTopOccupancy;
        public bool ParticipatesInBothTiersWhileSinking;
        public int SinkingMatchWeightPerTier;
        public bool Eligible;
    }

    /// <summary>
    /// Pure match cluster finder with Jumbo bridged + same-tier sinking weights.
    /// </summary>
    public static class MatchClusterFinder
    {
        public const int MaxDice = 128;
        public const int MaxCluster = 64;
        const int MaxLookup = 512;

        static readonly int[] NeighborX = { 0, 1, 0, -1 };
        static readonly int[] NeighborY = { 1, 0, -1, 0 };

        /// <summary>
        /// Finds one matching cluster that includes <paramref name="actionId"/> for the given face.
        /// Writes member ids into <paramref name="memberIds"/>; returns member count (0 if none).
        /// </summary>
        public static int TryFindClusterTouching(
            MatchDiceSnapshot[] dice,
            int diceCount,
            int actionId,
            int face,
            int[] memberIds)
        {
            if (dice == null || memberIds == null || diceCount <= 0 || face < 2 || face > 6)
            {
                return 0;
            }

            var consumedDice = new bool[MaxDice];
            var consumedBottom = new bool[MaxDice];
            var consumedTop = new bool[MaxDice];

            var count = TryFindPreSinkBridged(
                dice, diceCount, actionId, face, consumedDice, consumedBottom, consumedTop, memberIds);
            if (count > 0)
            {
                return count;
            }

            count = TryFindSameTier(
                dice, diceCount, actionId, face, 0, consumedDice, consumedBottom, consumedTop, memberIds);
            if (count > 0)
            {
                return count;
            }

            return TryFindSameTier(
                dice, diceCount, actionId, face, 1, consumedDice, consumedBottom, consumedTop, memberIds);
        }

        static int TryFindPreSinkBridged(
            MatchDiceSnapshot[] dice,
            int diceCount,
            int actionId,
            int face,
            bool[] consumedDice,
            bool[] consumedBottom,
            bool[] consumedTop,
            int[] memberIds)
        {
            if (!HasPreSinkJumbo(dice, diceCount, face, consumedDice))
            {
                return 0;
            }

            // lookup keys packed: (cellX, cellY, tier) -> dice index
            var keyX = new int[MaxLookup];
            var keyY = new int[MaxLookup];
            var keyTier = new int[MaxLookup];
            var keyDice = new int[MaxLookup];
            var lookupCount = 0;

            for (var i = 0; i < diceCount && i < MaxDice; i++)
            {
                ref var d = ref dice[i];
                if (!d.Eligible || d.TopFace != face || consumedDice[i])
                {
                    continue;
                }

                if (d.HasExpandedFootprint && d.IsSinkErasing)
                {
                    continue;
                }

                if (d.HasExpandedFootprint)
                {
                    var xs = new int[JumboFootprintCells.CellCount];
                    var ys = new int[JumboFootprintCells.CellCount];
                    JumboFootprintCells.AppendCells(d.CellX, d.CellY, xs, ys, out var cells);
                    for (var c = 0; c < cells; c++)
                    {
                        AddLookup(keyX, keyY, keyTier, keyDice, ref lookupCount, xs[c], ys[c], 0, i);
                        AddLookup(keyX, keyY, keyTier, keyDice, ref lookupCount, xs[c], ys[c], 1, i);
                    }

                    continue;
                }

                var slotConsumed = d.Tier == 1 ? consumedTop[i] : consumedBottom[i];
                if (slotConsumed)
                {
                    continue;
                }

                AddLookup(keyX, keyY, keyTier, keyDice, ref lookupCount, d.CellX, d.CellY, d.Tier, i);
            }

            var visited = new bool[MaxLookup];
            for (var start = 0; start < lookupCount; start++)
            {
                if (visited[start])
                {
                    continue;
                }

                var members = new int[MaxCluster];
                var memberMask = new bool[MaxDice];
                var memberCount = 0;
                var weight = 0;
                FloodBridged(
                    keyX, keyY, keyTier, keyDice, lookupCount, visited, start,
                    dice, members, memberMask, ref memberCount, ref weight);

                if (weight < face || memberCount <= 0)
                {
                    continue;
                }

                if (!ContainsId(members, memberCount, actionId) || !ContainsPreSinkJumbo(dice, members, memberCount))
                {
                    continue;
                }

                CopyMembers(members, memberCount, memberIds);
                return memberCount;
            }

            return 0;
        }

        static int TryFindSameTier(
            MatchDiceSnapshot[] dice,
            int diceCount,
            int actionId,
            int face,
            int matchTier,
            bool[] consumedDice,
            bool[] consumedBottom,
            bool[] consumedTop,
            int[] memberIds)
        {
            var keyX = new int[MaxLookup];
            var keyY = new int[MaxLookup];
            var keyDice = new int[MaxLookup];
            var lookupCount = 0;

            for (var i = 0; i < diceCount && i < MaxDice; i++)
            {
                ref var d = ref dice[i];
                if (!d.Eligible || d.TopFace != face)
                {
                    continue;
                }

                var slotConsumed = matchTier == 1 ? consumedTop[i] : consumedBottom[i];
                if (slotConsumed)
                {
                    continue;
                }

                if (d.HasExpandedFootprint && !d.IsSinkErasing)
                {
                    continue;
                }

                if (d.HasExpandedFootprint)
                {
                    var w = MatchWeightRules.GetSameTier(
                        true,
                        true,
                        d.Tier,
                        matchTier,
                        d.KeepsJumboTopOccupancy,
                        d.ParticipatesInBothTiersWhileSinking,
                        d.SinkingMatchWeightPerTier);
                    if (w <= 0)
                    {
                        continue;
                    }

                    var xs = new int[JumboFootprintCells.CellCount];
                    var ys = new int[JumboFootprintCells.CellCount];
                    JumboFootprintCells.AppendCells(d.CellX, d.CellY, xs, ys, out var cells);
                    for (var c = 0; c < cells; c++)
                    {
                        AddLookup2(keyX, keyY, keyDice, ref lookupCount, xs[c], ys[c], i);
                    }

                    continue;
                }

                if (consumedDice[i] || d.Tier != matchTier)
                {
                    continue;
                }

                AddLookup2(keyX, keyY, keyDice, ref lookupCount, d.CellX, d.CellY, i);
            }

            var visited = new bool[MaxLookup];
            for (var start = 0; start < lookupCount; start++)
            {
                if (visited[start])
                {
                    continue;
                }

                var members = new int[MaxCluster];
                var memberMask = new bool[MaxDice];
                var memberCount = 0;
                var weight = 0;
                FloodSameTier(
                    keyX, keyY, keyDice, lookupCount, visited, start,
                    dice, matchTier, members, memberMask, ref memberCount, ref weight);

                if (weight < face || memberCount <= 0 || !ContainsId(members, memberCount, actionId))
                {
                    continue;
                }

                CopyMembers(members, memberCount, memberIds);
                return memberCount;
            }

            return 0;
        }

        static void FloodBridged(
            int[] keyX,
            int[] keyY,
            int[] keyTier,
            int[] keyDice,
            int lookupCount,
            bool[] visited,
            int start,
            MatchDiceSnapshot[] dice,
            int[] members,
            bool[] memberMask,
            ref int memberCount,
            ref int weight)
        {
            var queue = new int[MaxLookup];
            var head = 0;
            var tail = 0;
            queue[tail++] = start;

            while (head < tail)
            {
                var idx = queue[head++];
                if (visited[idx])
                {
                    continue;
                }

                visited[idx] = true;
                var di = keyDice[idx];
                if (di < 0 || di >= MaxDice)
                {
                    continue;
                }

                if (!memberMask[di])
                {
                    memberMask[di] = true;
                    if (memberCount < MaxCluster)
                    {
                        members[memberCount++] = dice[di].Id;
                    }

                    weight += MatchWeightRules.GetPreSinkBridged(
                        dice[di].HasExpandedFootprint,
                        dice[di].IsSinkErasing);
                }

                for (var n = 0; n < 4; n++)
                {
                    var nx = keyX[idx] + NeighborX[n];
                    var ny = keyY[idx] + NeighborY[n];
                    EnqueueLookup(keyX, keyY, keyTier, lookupCount, queue, ref tail, nx, ny, keyTier[idx], visited);
                }

                if (!dice[di].HasExpandedFootprint || dice[di].IsSinkErasing)
                {
                    continue;
                }

                var xs = new int[JumboFootprintCells.CellCount];
                var ys = new int[JumboFootprintCells.CellCount];
                JumboFootprintCells.AppendCells(dice[di].CellX, dice[di].CellY, xs, ys, out var cells);
                for (var c = 0; c < cells; c++)
                {
                    EnqueueLookup(keyX, keyY, keyTier, lookupCount, queue, ref tail, xs[c], ys[c], 0, visited);
                    EnqueueLookup(keyX, keyY, keyTier, lookupCount, queue, ref tail, xs[c], ys[c], 1, visited);
                }
            }
        }

        static void FloodSameTier(
            int[] keyX,
            int[] keyY,
            int[] keyDice,
            int lookupCount,
            bool[] visited,
            int start,
            MatchDiceSnapshot[] dice,
            int matchTier,
            int[] members,
            bool[] memberMask,
            ref int memberCount,
            ref int weight)
        {
            var queue = new int[MaxLookup];
            var head = 0;
            var tail = 0;
            queue[tail++] = start;

            while (head < tail)
            {
                var idx = queue[head++];
                if (visited[idx])
                {
                    continue;
                }

                visited[idx] = true;
                var di = keyDice[idx];
                if (di < 0 || di >= MaxDice)
                {
                    continue;
                }

                if (!memberMask[di])
                {
                    memberMask[di] = true;
                    if (memberCount < MaxCluster)
                    {
                        members[memberCount++] = dice[di].Id;
                    }

                    weight += MatchWeightRules.GetSameTier(
                        dice[di].HasExpandedFootprint,
                        dice[di].IsSinkErasing,
                        dice[di].Tier,
                        matchTier,
                        dice[di].KeepsJumboTopOccupancy,
                        dice[di].ParticipatesInBothTiersWhileSinking,
                        dice[di].SinkingMatchWeightPerTier);
                }

                for (var n = 0; n < 4; n++)
                {
                    var nx = keyX[idx] + NeighborX[n];
                    var ny = keyY[idx] + NeighborY[n];
                    for (var i = 0; i < lookupCount; i++)
                    {
                        if (!visited[i] && keyX[i] == nx && keyY[i] == ny && tail < MaxLookup)
                        {
                            queue[tail++] = i;
                        }
                    }
                }
            }
        }

        static void EnqueueLookup(
            int[] keyX,
            int[] keyY,
            int[] keyTier,
            int lookupCount,
            int[] queue,
            ref int tail,
            int x,
            int y,
            int tier,
            bool[] visited)
        {
            for (var i = 0; i < lookupCount; i++)
            {
                if (!visited[i]
                    && keyX[i] == x
                    && keyY[i] == y
                    && keyTier[i] == tier
                    && tail < MaxLookup)
                {
                    queue[tail++] = i;
                    return;
                }
            }
        }

        static void AddLookup(
            int[] keyX,
            int[] keyY,
            int[] keyTier,
            int[] keyDice,
            ref int count,
            int x,
            int y,
            int tier,
            int diceIndex)
        {
            if (count >= MaxLookup)
            {
                return;
            }

            keyX[count] = x;
            keyY[count] = y;
            keyTier[count] = tier;
            keyDice[count] = diceIndex;
            count++;
        }

        static void AddLookup2(
            int[] keyX,
            int[] keyY,
            int[] keyDice,
            ref int count,
            int x,
            int y,
            int diceIndex)
        {
            if (count >= MaxLookup)
            {
                return;
            }

            keyX[count] = x;
            keyY[count] = y;
            keyDice[count] = diceIndex;
            count++;
        }

        static bool HasPreSinkJumbo(MatchDiceSnapshot[] dice, int diceCount, int face, bool[] consumedDice)
        {
            for (var i = 0; i < diceCount && i < MaxDice; i++)
            {
                ref var d = ref dice[i];
                if (d.Eligible
                    && !consumedDice[i]
                    && d.TopFace == face
                    && d.HasExpandedFootprint
                    && !d.IsSinkErasing)
                {
                    return true;
                }
            }

            return false;
        }

        static bool ContainsPreSinkJumbo(MatchDiceSnapshot[] dice, int[] members, int memberCount)
        {
            for (var m = 0; m < memberCount; m++)
            {
                var id = members[m];
                for (var i = 0; i < dice.Length; i++)
                {
                    if (dice[i].Id == id
                        && dice[i].HasExpandedFootprint
                        && !dice[i].IsSinkErasing)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        static bool ContainsId(int[] members, int count, int id)
        {
            for (var i = 0; i < count; i++)
            {
                if (members[i] == id)
                {
                    return true;
                }
            }

            return false;
        }

        static void CopyMembers(int[] src, int count, int[] dst)
        {
            var n = count < dst.Length ? count : dst.Length;
            for (var i = 0; i < n; i++)
            {
                dst[i] = src[i];
            }
        }
    }
}
