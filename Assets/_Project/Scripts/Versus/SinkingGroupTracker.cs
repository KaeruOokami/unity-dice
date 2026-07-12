using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Versus.Core;
using DiceGame.Gameplay;

namespace DiceGame.Versus
{
    public sealed class SinkingGroupTracker
    {
        sealed class Group
        {
            public int Id;
            public readonly HashSet<DiceController> Members = new();
            public int ChainCount;
            public int Face;
            public PlayerSlot LastAttacker;
        }

        readonly Dictionary<DiceController, int> diceToGroupId = new();
        readonly Dictionary<int, Group> groups = new();
        int nextGroupId = 1;

        public SinkingChainResult RegisterCluster(
            IReadOnlyList<DiceController> dissolvingMembers,
            IReadOnlyList<DiceController> newMembers,
            int face,
            PlayerSlot attacker,
            out int clusterSize) {
            clusterSize = 0;
            if (newMembers == null || newMembers.Count == 0) {
                return new SinkingChainResult(0, false);
            }

            clusterSize = CountClusterSize(dissolvingMembers, newMembers);
            var touchedGroupIds = CollectTouchedGroupIds(dissolvingMembers);
            var hasExistingSinking = touchedGroupIds.Count > 0;

            var maxChain = 0;
            var lastAttacker = attacker;
            if (hasExistingSinking) {
                foreach (var groupId in touchedGroupIds) {
                    if (!groups.TryGetValue(groupId, out var existing)) {
                        continue;
                    }

                    if (existing.ChainCount > maxChain) {
                        maxChain = existing.ChainCount;
                    }

                    lastAttacker = existing.LastAttacker;
                }
            }

            var chainResult = SinkingChainResolver.Resolve(
                maxChain,
                lastAttacker,
                attacker,
                hasExistingSinking,
                touchedGroupIds.Count > 1);

            var mergedGroup = MergeOrCreateGroup(touchedGroupIds, dissolvingMembers, newMembers);
            mergedGroup.Face = face;
            mergedGroup.ChainCount = chainResult.ChainCount;
            mergedGroup.LastAttacker = attacker;

            foreach (var dice in newMembers) {
                if (dice == null) {
                    continue;
                }

                mergedGroup.Members.Add(dice);
                diceToGroupId[dice] = mergedGroup.Id;
            }

            return chainResult;
        }

        public SinkingChainResult RegisterFollowUpAttack(
            IReadOnlyList<DiceController> cluster,
            PlayerSlot attacker,
            out int face,
            out int clusterSize) {
            face = 0;
            clusterSize = 0;
            if (cluster == null || cluster.Count == 0) {
                return new SinkingChainResult(0, false);
            }

            var erasingMembers = CollectErasingMembers(cluster);
            if (erasingMembers.Count == 0) {
                return new SinkingChainResult(0, false);
            }

            face = erasingMembers[0].CurrentState.Orientation.Top;
            clusterSize = CountActiveClusterDice(cluster);

            var touchedGroupIds = CollectTouchedGroupIds(erasingMembers);
            var hasExistingSinking = touchedGroupIds.Count > 0;
            var maxChain = 0;
            var lastAttacker = attacker;

            if (hasExistingSinking) {
                foreach (var groupId in touchedGroupIds) {
                    if (!groups.TryGetValue(groupId, out var existing)) {
                        continue;
                    }

                    if (existing.ChainCount > maxChain) {
                        maxChain = existing.ChainCount;
                    }

                    lastAttacker = existing.LastAttacker;
                }
            }

            var chainResult = SinkingChainResolver.Resolve(
                maxChain,
                lastAttacker,
                attacker,
                hasExistingSinking,
                touchedGroupIds.Count > 1);

            if (hasExistingSinking && TryGetMergedGroup(touchedGroupIds, out var mergedGroup)) {
                mergedGroup.Face = face;
                mergedGroup.ChainCount = chainResult.ChainCount;
                mergedGroup.LastAttacker = attacker;
            }

            return chainResult;
        }

        public void RemoveDice(DiceController dice) {
            if (dice == null || !diceToGroupId.TryGetValue(dice, out var groupId)) {
                return;
            }

            diceToGroupId.Remove(dice);
            if (!groups.TryGetValue(groupId, out var group)) {
                return;
            }

            group.Members.Remove(dice);
            if (group.Members.Count == 0) {
                groups.Remove(groupId);
            }
        }

        static int CountClusterSize(
            IReadOnlyList<DiceController> dissolvingMembers,
            IReadOnlyList<DiceController> newMembers) {
            var count = newMembers.Count;
            if (dissolvingMembers != null) {
                count += dissolvingMembers.Count;
            }

            return count;
        }

        HashSet<int> CollectTouchedGroupIds(IReadOnlyList<DiceController> dissolvingMembers) {
            var touched = new HashSet<int>();
            if (dissolvingMembers == null) {
                return touched;
            }

            for (var i = 0; i < dissolvingMembers.Count; i++) {
                var dice = dissolvingMembers[i];
                if (dice != null && diceToGroupId.TryGetValue(dice, out var groupId)) {
                    touched.Add(groupId);
                }
            }

            return touched;
        }

        static List<DiceController> CollectErasingMembers(IReadOnlyList<DiceController> cluster) {
            var erasingMembers = new List<DiceController>();
            if (cluster == null) {
                return erasingMembers;
            }

            for (var i = 0; i < cluster.Count; i++) {
                var dice = cluster[i];
                if (dice != null && dice.IsErasing) {
                    erasingMembers.Add(dice);
                }
            }

            return erasingMembers;
        }

        static int CountActiveClusterDice(IReadOnlyList<DiceController> cluster) {
            var count = 0;
            if (cluster == null) {
                return count;
            }

            for (var i = 0; i < cluster.Count; i++) {
                if (cluster[i] != null && !cluster[i].IsSpawning) {
                    count++;
                }
            }

            return count;
        }

        bool TryGetMergedGroup(HashSet<int> touchedGroupIds, out Group mergedGroup) {
            mergedGroup = null;
            if (touchedGroupIds == null || touchedGroupIds.Count == 0) {
                return false;
            }

            foreach (var groupId in touchedGroupIds) {
                if (!groups.TryGetValue(groupId, out var group)) {
                    continue;
                }

                if (mergedGroup == null) {
                    mergedGroup = group;
                    continue;
                }

                foreach (var member in group.Members) {
                    mergedGroup.Members.Add(member);
                    diceToGroupId[member] = mergedGroup.Id;
                }

                groups.Remove(groupId);
            }

            return mergedGroup != null;
        }

        Group MergeOrCreateGroup(
            HashSet<int> touchedGroupIds,
            IReadOnlyList<DiceController> dissolvingMembers,
            IReadOnlyList<DiceController> newMembers) {
            if (touchedGroupIds.Count == 0) {
                return CreateGroup();
            }

            Group merged = null;
            foreach (var groupId in touchedGroupIds) {
                if (!groups.TryGetValue(groupId, out var group)) {
                    continue;
                }

                if (merged == null) {
                    merged = group;
                    continue;
                }

                foreach (var member in group.Members) {
                    merged.Members.Add(member);
                    diceToGroupId[member] = merged.Id;
                }

                groups.Remove(groupId);
            }

            if (merged == null) {
                merged = CreateGroup();
            }

            if (dissolvingMembers != null) {
                for (var i = 0; i < dissolvingMembers.Count; i++) {
                    var dice = dissolvingMembers[i];
                    if (dice == null) {
                        continue;
                    }

                    merged.Members.Add(dice);
                    diceToGroupId[dice] = merged.Id;
                }
            }

            return merged;
        }

        Group CreateGroup() {
            var group = new Group {
                Id = nextGroupId++
            };
            groups[group.Id] = group;
            return group;
        }
    }
}
