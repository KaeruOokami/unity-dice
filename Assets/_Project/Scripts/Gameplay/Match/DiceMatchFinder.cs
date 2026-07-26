using System.Collections.Generic;
using System.Text;
using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public readonly struct DiceMatchCluster
    {
        public List<DiceController> Members { get; }
        public int Weight { get; }
        public DiceStackTier MatchTier { get; }

        public DiceMatchCluster(List<DiceController> members, int weight, DiceStackTier matchTier) {
            Members = members;
            Weight = weight;
            MatchTier = matchTier;
        }
    }

    public static class DiceMatchFinder
    {
        const string LogPrefix = "[JumboMatch]";

        static readonly Direction[] Directions = {
            Direction.East, Direction.West, Direction.North, Direction.South
        };

        static readonly DiceStackTier[] BothTiers = {
            DiceStackTier.Bottom,
            DiceStackTier.Top
        };

        static readonly List<Vector2Int> FootprintBuffer = new(JumboFootprint.CellCount);

        public static List<DiceMatchCluster> FindMatchingClusters(
            IReadOnlyList<DiceController> allDice,
            IReadOnlyCollection<DiceController> actionDice) {
            var results = new List<DiceMatchCluster>();
            // Pre-sink jumbo is consumed as a whole dice; sinking matches are per (dice, tier).
            var consumedDice = new HashSet<DiceController>();
            var consumedSlots = new HashSet<(DiceController, DiceStackTier)>();
            var diagnose = HasAnyExpandedFootprint(allDice);
            if (diagnose) {
                LogMatchBegin(allDice, actionDice);
            }

            for (var face = 2; face <= 6; face++) {
                FindPreSinkBridgedClusters(
                    allDice, actionDice, face, consumedDice, consumedSlots, results, diagnose);
                FindSinkingTierClusters(
                    allDice, actionDice, face, DiceStackTier.Bottom, consumedDice, consumedSlots, results);
                FindSinkingTierClusters(
                    allDice, actionDice, face, DiceStackTier.Top, consumedDice, consumedSlots, results);
            }

            if (diagnose) {
                LogMatchEnd(allDice, actionDice, results);
            }

            return results;
        }

        /// <summary>
        /// Pre-sink jumbo: weight 1, bridges Bottom 2x2 and Top 2x2 into one cluster.
        /// Normal Bottom/Top still do not connect except through that jumbo.
        /// </summary>
        static void FindPreSinkBridgedClusters(
            IReadOnlyList<DiceController> allDice,
            IReadOnlyCollection<DiceController> actionDice,
            int face,
            HashSet<DiceController> consumedDice,
            HashSet<(DiceController, DiceStackTier)> consumedSlots,
            List<DiceMatchCluster> results,
            bool diagnose) {
            var lookup = BuildPreSinkBridgedLookup(
                allDice, face, consumedDice, consumedSlots, diagnose);
            // Bridged pass only matters when a pre-sink jumbo is in the lookup for this face.
            if (!LookupContainsExpandedFootprint(lookup)) {
                return;
            }

            var visited = new HashSet<(Vector2Int, DiceStackTier)>();

            foreach (var pair in lookup) {
                if (visited.Contains(pair.Key)) {
                    continue;
                }

                var cluster = FloodFillBridged(lookup, pair.Key, visited);
                var hasJumbo = ContainsPreSinkJumbo(cluster.Members);

                if (cluster.Weight < face) {
                    if (diagnose && hasJumbo) {
                        Debug.Log(
                            $"{LogPrefix} bridged discard face={face}: weight {cluster.Weight} < {face} " +
                            $"members={FormatMembers(cluster.Members)}");
                    }

                    continue;
                }

                if (!HasActionParticipant(cluster.Members, actionDice)) {
                    if (diagnose && hasJumbo) {
                        Debug.Log(
                            $"{LogPrefix} bridged discard face={face}: no action participant " +
                            $"members={FormatMembers(cluster.Members)} " +
                            $"action={FormatActionDice(actionDice)}");
                    }

                    continue;
                }

                // Bridged pass is only meaningful when a pre-sink jumbo is involved.
                if (!hasJumbo) {
                    if (diagnose) {
                        Debug.Log(
                            $"{LogPrefix} bridged discard face={face}: no pre-sink jumbo in cluster " +
                            $"members={FormatMembers(cluster.Members)}");
                    }

                    continue;
                }

                if (diagnose) {
                    Debug.Log(
                        $"{LogPrefix} bridged ACCEPT face={face} weight={cluster.Weight} " +
                        $"members={FormatMembers(cluster.Members)}");
                }

                results.Add(cluster);
                foreach (var dice in cluster.Members) {
                    consumedDice.Add(dice);
                    consumedSlots.Add((dice, DiceStackTier.Bottom));
                    consumedSlots.Add((dice, DiceStackTier.Top));
                }
            }
        }

        /// <summary>
        /// Sinking (and normal same-tier) matches: no Bottom/Top bridge.
        /// Sinking jumbo contributes weight 4 on the active tier only.
        /// </summary>
        static void FindSinkingTierClusters(
            IReadOnlyList<DiceController> allDice,
            IReadOnlyCollection<DiceController> actionDice,
            int face,
            DiceStackTier matchTier,
            HashSet<DiceController> consumedDice,
            HashSet<(DiceController, DiceStackTier)> consumedSlots,
            List<DiceMatchCluster> results) {
            var lookup = BuildSameTierLookup(allDice, face, matchTier, consumedDice, consumedSlots);
            var visited = new HashSet<Vector2Int>();

            foreach (var pair in lookup) {
                if (visited.Contains(pair.Key)) {
                    continue;
                }

                var cluster = FloodFillSameTier(lookup, pair.Key, visited, matchTier);
                if (cluster.Weight < face || !HasActionParticipant(cluster.Members, actionDice)) {
                    continue;
                }

                results.Add(cluster);
                foreach (var dice in cluster.Members) {
                    consumedSlots.Add((dice, matchTier));
                    if (dice.Capabilities.HasExpandedFootprint && dice.IsSinkErasing) {
                        // Sinking jumbo may still match the other tier later.
                        continue;
                    }

                    if (!dice.Capabilities.HasExpandedFootprint) {
                        consumedDice.Add(dice);
                    }
                }
            }
        }

        static bool ContainsPreSinkJumbo(IReadOnlyList<DiceController> members) {
            for (var i = 0; i < members.Count; i++) {
                var dice = members[i];
                if (dice != null
                    && dice.Capabilities.HasExpandedFootprint
                    && !dice.IsSinkErasing) {
                    return true;
                }
            }

            return false;
        }

        static bool HasActionParticipant(
            IReadOnlyList<DiceController> cluster,
            IReadOnlyCollection<DiceController> actionDice) {
            if (actionDice == null || actionDice.Count == 0) {
                return false;
            }

            foreach (var dice in cluster) {
                foreach (var participant in actionDice) {
                    if (dice == participant) {
                        return true;
                    }
                }
            }

            return false;
        }

        static Dictionary<(Vector2Int, DiceStackTier), DiceController> BuildPreSinkBridgedLookup(
            IReadOnlyList<DiceController> allDice,
            int face,
            HashSet<DiceController> consumedDice,
            HashSet<(DiceController, DiceStackTier)> consumedSlots,
            bool diagnose) {
            var lookup = new Dictionary<(Vector2Int, DiceStackTier), DiceController>();

            foreach (var dice in allDice) {
                if (dice == null) {
                    continue;
                }

                var isJumbo = dice.Capabilities.HasExpandedFootprint;
                if (consumedDice.Contains(dice)) {
                    if (diagnose && isJumbo && dice.CurrentState.Orientation.Top == face) {
                        Debug.Log(
                            $"{LogPrefix} bridged skip {FormatDice(dice)} face={face}: already consumed");
                    }

                    continue;
                }

                if (!IsMatchEligible(dice)) {
                    if (diagnose && isJumbo && dice.CurrentState.Orientation.Top == face) {
                        Debug.Log(
                            $"{LogPrefix} bridged skip {FormatDice(dice)} face={face}: " +
                            $"ineligible IsSpawning={dice.IsSpawning} IsRolling={dice.IsRolling}");
                    }

                    continue;
                }

                if (dice.CurrentState.Orientation.Top != face) {
                    continue;
                }

                // Sinking jumbos use the per-tier pass.
                if (isJumbo && dice.IsSinkErasing) {
                    if (diagnose) {
                        Debug.Log(
                            $"{LogPrefix} bridged skip {FormatDice(dice)} face={face}: " +
                            "IsSinkErasing (uses same-tier pass)");
                    }

                    continue;
                }

                if (isJumbo) {
                    FootprintBuffer.Clear();
                    JumboFootprint.AppendCells(dice.CurrentState.GridPos, FootprintBuffer);
                    for (var i = 0; i < FootprintBuffer.Count; i++) {
                        var cell = FootprintBuffer[i];
                        lookup[(cell, DiceStackTier.Bottom)] = dice;
                        lookup[(cell, DiceStackTier.Top)] = dice;
                    }

                    if (diagnose) {
                        Debug.Log(
                            $"{LogPrefix} bridged include {FormatDice(dice)} face={face} " +
                            $"footprint={FormatFootprint(dice.CurrentState.GridPos)} " +
                            $"weight={DiceMatchWeight.GetPreSinkBridgedWeight(dice)}");
                    }

                    continue;
                }

                var tier = dice.CurrentState.Tier;
                if (consumedSlots.Contains((dice, tier))) {
                    continue;
                }

                lookup[(dice.CurrentState.GridPos, tier)] = dice;
            }

            return lookup;
        }

        static Dictionary<Vector2Int, DiceController> BuildSameTierLookup(
            IReadOnlyList<DiceController> allDice,
            int face,
            DiceStackTier matchTier,
            HashSet<DiceController> consumedDice,
            HashSet<(DiceController, DiceStackTier)> consumedSlots) {
            var lookup = new Dictionary<Vector2Int, DiceController>();

            foreach (var dice in allDice) {
                if (dice == null
                    || !IsMatchEligible(dice)
                    || dice.CurrentState.Orientation.Top != face) {
                    continue;
                }

                if (consumedSlots.Contains((dice, matchTier))) {
                    continue;
                }

                // Pre-sink jumbo already handled (or skipped) in bridged pass.
                if (dice.Capabilities.HasExpandedFootprint && !dice.IsSinkErasing) {
                    continue;
                }

                if (dice.Capabilities.HasExpandedFootprint) {
                    var weight = DiceMatchWeight.Get(dice, matchTier);
                    if (weight <= 0) {
                        continue;
                    }

                    FootprintBuffer.Clear();
                    JumboFootprint.AppendCells(dice.CurrentState.GridPos, FootprintBuffer);
                    for (var i = 0; i < FootprintBuffer.Count; i++) {
                        lookup[FootprintBuffer[i]] = dice;
                    }

                    continue;
                }

                if (consumedDice.Contains(dice) || dice.CurrentState.Tier != matchTier) {
                    continue;
                }

                lookup[dice.CurrentState.GridPos] = dice;
            }

            return lookup;
        }

        static bool IsMatchEligible(DiceController dice) {
            return !dice.IsSpawning && !dice.IsRolling;
        }

        static DiceMatchCluster FloodFillBridged(
            Dictionary<(Vector2Int, DiceStackTier), DiceController> lookup,
            (Vector2Int, DiceStackTier) start,
            HashSet<(Vector2Int, DiceStackTier)> visited) {
            var members = new List<DiceController>();
            var memberSet = new HashSet<DiceController>();
            var queue = new Queue<(Vector2Int, DiceStackTier)>();
            queue.Enqueue(start);
            var weight = 0;

            while (queue.Count > 0) {
                var key = queue.Dequeue();
                if (visited.Contains(key) || !lookup.TryGetValue(key, out var dice)) {
                    continue;
                }

                visited.Add(key);
                if (memberSet.Add(dice)) {
                    members.Add(dice);
                    weight += DiceMatchWeight.GetPreSinkBridgedWeight(dice);
                }

                EnqueueBridgedNeighbors(lookup, key, dice, queue);
            }

            return new DiceMatchCluster(members, weight, DiceStackTier.Bottom);
        }

        static void EnqueueBridgedNeighbors(
            Dictionary<(Vector2Int, DiceStackTier), DiceController> lookup,
            (Vector2Int cell, DiceStackTier tier) key,
            DiceController dice,
            Queue<(Vector2Int, DiceStackTier)> queue) {
            // Same-tier orthogonal (normal adjacency).
            foreach (var direction in Directions) {
                var neighbor = (key.cell + direction.ToGridDelta(), key.tier);
                if (lookup.ContainsKey(neighbor)) {
                    queue.Enqueue(neighbor);
                }
            }

            // Pre-sink jumbo bridges Bottom and Top across its footprint only.
            if (dice == null
                || !dice.Capabilities.HasExpandedFootprint
                || dice.IsSinkErasing) {
                return;
            }

            FootprintBuffer.Clear();
            JumboFootprint.AppendCells(dice.CurrentState.GridPos, FootprintBuffer);
            for (var i = 0; i < FootprintBuffer.Count; i++) {
                var cell = FootprintBuffer[i];
                for (var t = 0; t < BothTiers.Length; t++) {
                    var footprintKey = (cell, BothTiers[t]);
                    if (lookup.ContainsKey(footprintKey)) {
                        queue.Enqueue(footprintKey);
                    }
                }
            }
        }

        static DiceMatchCluster FloodFillSameTier(
            Dictionary<Vector2Int, DiceController> lookup,
            Vector2Int start,
            HashSet<Vector2Int> visited,
            DiceStackTier matchTier) {
            var members = new List<DiceController>();
            var memberSet = new HashSet<DiceController>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            var weight = 0;

            while (queue.Count > 0) {
                var cell = queue.Dequeue();
                if (visited.Contains(cell) || !lookup.TryGetValue(cell, out var dice)) {
                    continue;
                }

                visited.Add(cell);
                if (memberSet.Add(dice)) {
                    members.Add(dice);
                    weight += DiceMatchWeight.Get(dice, matchTier);
                }

                foreach (var direction in Directions) {
                    var neighbor = cell + direction.ToGridDelta();
                    if (lookup.ContainsKey(neighbor)) {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return new DiceMatchCluster(members, weight, matchTier);
        }

        static bool HasAnyExpandedFootprint(IReadOnlyList<DiceController> allDice) {
            if (allDice == null) {
                return false;
            }

            for (var i = 0; i < allDice.Count; i++) {
                var dice = allDice[i];
                if (dice != null && dice.Capabilities.HasExpandedFootprint) {
                    return true;
                }
            }

            return false;
        }

        static bool LookupContainsExpandedFootprint(
            Dictionary<(Vector2Int, DiceStackTier), DiceController> lookup) {
            foreach (var pair in lookup) {
                if (pair.Value != null && pair.Value.Capabilities.HasExpandedFootprint) {
                    return true;
                }
            }

            return false;
        }

        static void LogMatchBegin(
            IReadOnlyList<DiceController> allDice,
            IReadOnlyCollection<DiceController> actionDice) {
            var sb = new StringBuilder();
            sb.Append(LogPrefix).Append(" BEGIN action=").Append(FormatActionDice(actionDice));
            for (var i = 0; i < allDice.Count; i++) {
                var dice = allDice[i];
                if (dice == null || !dice.Capabilities.HasExpandedFootprint) {
                    continue;
                }

                sb.Append(" | jumbo=").Append(FormatDice(dice));
                sb.Append(" spawn=").Append(dice.IsSpawning);
                sb.Append(" rolling=").Append(dice.IsRolling);
                sb.Append(" sink=").Append(dice.IsSinkErasing);
                sb.Append(" face=").Append(dice.CurrentState.Orientation.Top);
                sb.Append(" tier=").Append(dice.CurrentState.Tier);
                sb.Append(" anchor=").Append(FormatCell(dice.CurrentState.GridPos));
                sb.Append(" fp=").Append(FormatFootprint(dice.CurrentState.GridPos));
                sb.Append(" keepTop=").Append(dice.KeepsJumboTopOccupancy);
                sb.Append(" eligible=").Append(IsMatchEligible(dice));
                LogActionAdjacency(sb, dice, actionDice);
            }

            Debug.Log(sb.ToString());
        }

        static void LogMatchEnd(
            IReadOnlyList<DiceController> allDice,
            IReadOnlyCollection<DiceController> actionDice,
            List<DiceMatchCluster> results) {
            var jumboInResults = false;
            for (var i = 0; i < results.Count; i++) {
                if (ContainsAnyExpandedFootprint(results[i].Members)) {
                    jumboInResults = true;
                    Debug.Log(
                        $"{LogPrefix} RESULT[{i}] tier={results[i].MatchTier} " +
                        $"weight={results[i].Weight} members={FormatMembers(results[i].Members)}");
                }
            }

            if (jumboInResults) {
                return;
            }

            for (var i = 0; i < allDice.Count; i++) {
                var jumbo = allDice[i];
                if (jumbo == null || !jumbo.Capabilities.HasExpandedFootprint) {
                    continue;
                }

                Debug.LogWarning(
                    $"{LogPrefix} END no cluster included jumbo {FormatDice(jumbo)} " +
                    $"(face={jumbo.CurrentState.Orientation.Top} eligible={IsMatchEligible(jumbo)} " +
                    $"sink={jumbo.IsSinkErasing}). action={FormatActionDice(actionDice)}");
            }
        }

        static void LogActionAdjacency(
            StringBuilder sb,
            DiceController jumbo,
            IReadOnlyCollection<DiceController> actionDice) {
            if (actionDice == null) {
                sb.Append(" adjAction=none");
                return;
            }

            sb.Append(" adjAction=");
            var any = false;
            foreach (var action in actionDice) {
                if (action == null) {
                    continue;
                }

                any = true;
                var sameFace = action.CurrentState.Orientation.Top
                    == jumbo.CurrentState.Orientation.Top;
                var ortho = IsOrthogonallyAdjacentToFootprint(
                    jumbo.CurrentState.GridPos,
                    action.CurrentState.GridPos);
                sb.Append('[')
                    .Append(FormatDice(action))
                    .Append(" sameFace=").Append(sameFace)
                    .Append(" orthoFp=").Append(ortho)
                    .Append(" cell=").Append(FormatCell(action.CurrentState.GridPos))
                    .Append(" tier=").Append(action.CurrentState.Tier)
                    .Append(']');
            }

            if (!any) {
                sb.Append("none");
            }
        }

        static bool IsOrthogonallyAdjacentToFootprint(Vector2Int anchor, Vector2Int cell) {
            FootprintBuffer.Clear();
            JumboFootprint.AppendCells(anchor, FootprintBuffer);
            for (var i = 0; i < FootprintBuffer.Count; i++) {
                var fp = FootprintBuffer[i];
                var dx = Mathf.Abs(fp.x - cell.x);
                var dy = Mathf.Abs(fp.y - cell.y);
                if (dx + dy == 1) {
                    return true;
                }
            }

            return false;
        }

        static bool ContainsAnyExpandedFootprint(IReadOnlyList<DiceController> members) {
            for (var i = 0; i < members.Count; i++) {
                if (members[i] != null && members[i].Capabilities.HasExpandedFootprint) {
                    return true;
                }
            }

            return false;
        }

        static string FormatDice(DiceController dice) {
            if (dice == null) {
                return "(null)";
            }

            return $"{dice.name}({dice.Kind}@{FormatCell(dice.CurrentState.GridPos)}/" +
                   $"{dice.CurrentState.Tier}/f{dice.CurrentState.Orientation.Top})";
        }

        static string FormatMembers(IReadOnlyList<DiceController> members) {
            if (members == null || members.Count == 0) {
                return "[]";
            }

            var sb = new StringBuilder();
            sb.Append('[');
            for (var i = 0; i < members.Count; i++) {
                if (i > 0) {
                    sb.Append(", ");
                }

                sb.Append(FormatDice(members[i]));
            }

            sb.Append(']');
            return sb.ToString();
        }

        static string FormatActionDice(IReadOnlyCollection<DiceController> actionDice) {
            if (actionDice == null) {
                return "[]";
            }

            var sb = new StringBuilder();
            sb.Append('[');
            var first = true;
            foreach (var dice in actionDice) {
                if (!first) {
                    sb.Append(", ");
                }

                first = false;
                sb.Append(FormatDice(dice));
            }

            sb.Append(']');
            return sb.ToString();
        }

        static string FormatCell(Vector2Int cell) {
            return $"({cell.x},{cell.y})";
        }

        static string FormatFootprint(Vector2Int anchor) {
            FootprintBuffer.Clear();
            JumboFootprint.AppendCells(anchor, FootprintBuffer);
            var sb = new StringBuilder();
            sb.Append('[');
            for (var i = 0; i < FootprintBuffer.Count; i++) {
                if (i > 0) {
                    sb.Append(' ');
                }

                sb.Append(FormatCell(FootprintBuffer[i]));
            }

            sb.Append(']');
            return sb.ToString();
        }
    }
}
