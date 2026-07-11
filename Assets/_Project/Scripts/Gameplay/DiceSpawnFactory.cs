using System;
using System.Collections;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Grid;
using DiceGame.Placement;
using DiceGame.View;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DiceGame.Gameplay
{
    public static class DiceSpawnFactory
    {
        public static DiceController TryCreate(
            GameObject prefab,
            Transform parent,
            Vector2Int gridPos,
            DiceStackTier tier,
            DiceKind kind,
            GameObject meshPrefab,
            PhysicsSettings physicsSettings,
            DiceAnimationSettings animationSettings,
            DiceErasureSettings erasureSettings) {
            if (prefab == null) {
                return null;
            }

            var diceEntity = UnityEngine.Object.Instantiate(prefab, parent);
            var tierLabel = tier == DiceStackTier.Top ? "Top" : "Bottom";
            diceEntity.name = $"DiceEntity_{gridPos.x}_{gridPos.y}_{tierLabel}_{kind}";

            var diceView = diceEntity.GetComponent<DiceView>();
            if (diceView == null) {
                Debug.LogError("DiceSpawnFactory: DiceEntity prefab must have DiceView.");
                UnityEngine.Object.Destroy(diceEntity);
                return null;
            }

            diceView.Configure(physicsSettings, animationSettings, erasureSettings);
            if (meshPrefab != null) {
                diceView.SetMeshPrefab(meshPrefab);
            }

            var diceController = diceEntity.GetComponent<DiceController>();
            if (diceController == null) {
                Debug.LogError("DiceSpawnFactory: DiceEntity prefab must have DiceController.");
                UnityEngine.Object.Destroy(diceEntity);
                return null;
            }

            return diceController;
        }

        public static DiceOrientation CreateRandomOrientation() {
            var orientation = DiceOrientation.Default;
            var directions = new[] { Direction.East, Direction.West, Direction.North, Direction.South };
            var steps = Random.Range(0, 12);

            for (var i = 0; i < steps; i++) {
                orientation = orientation.Roll(directions[Random.Range(0, directions.Length)]);
            }

            return orientation;
        }
    }

    public sealed class DiceSpawnSystem : MonoBehaviour
    {
        sealed class PlayerSpawnChannel
        {
            public PlayerSlot Slot;
            public DiceSpawnSettings Settings;
            public DiceCatalog Catalog;
            public Coroutine SpawnCoroutine;
        }

        Board board;
        DiceRegistry registry;
        GameObject diceEntityPrefab;
        DiceCatalog diceCatalog;
        Transform spawnParent;
        PhysicsSettings physicsSettings;
        DiceAnimationSettings diceAnimationSettings;
        DiceErasureSettings diceErasureSettings;
        PlayerMatchActionContext matchActionContext;
        DiceMatchErasureSystem erasureSystem;
        DiceSpawnSettings spawnSettings;
        System.Random random;
        readonly List<PlayerSpawnChannel> versusChannels = new();
        readonly Dictionary<PlayerSlot, int> attackSpawnCellIndices = new();

        Coroutine spawnCoroutine;

        public void Configure(
            Board targetBoard,
            DiceRegistry targetRegistry,
            GameObject prefab,
            DiceCatalog catalog,
            Transform parent,
            PhysicsSettings physics,
            DiceAnimationSettings animationSettings,
            DiceErasureSettings erasureSettings,
            PlayerMatchActionContext actionContext,
            DiceSpawnSettings settings,
            System.Random spawnRandom) {
            board = targetBoard;
            registry = targetRegistry;
            diceEntityPrefab = prefab;
            diceCatalog = catalog;
            spawnParent = parent;
            physicsSettings = physics;
            diceAnimationSettings = animationSettings;
            diceErasureSettings = erasureSettings;
            matchActionContext = actionContext;
            spawnSettings = settings;
            random = spawnRandom;
            versusChannels.Clear();
            attackSpawnCellIndices.Clear();
        }

        public void ConfigureVersusSpawns(
            DiceSpawnSettings player1Settings,
            DiceCatalog player1Catalog,
            DiceSpawnSettings player2Settings,
            DiceCatalog player2Catalog) {
            versusChannels.Clear();
            versusChannels.Add(new PlayerSpawnChannel {
                Slot = PlayerSlot.Player1,
                Settings = player1Settings,
                Catalog = player1Catalog
            });
            attackSpawnCellIndices[PlayerSlot.Player1] = 0;
            versusChannels.Add(new PlayerSpawnChannel {
                Slot = PlayerSlot.Player2,
                Settings = player2Settings,
                Catalog = player2Catalog
            });
            attackSpawnCellIndices[PlayerSlot.Player2] = 0;
        }

        public void ConfigureErasureSystem(DiceMatchErasureSystem targetErasureSystem) {
            erasureSystem = targetErasureSystem;
        }

        public void StartSpawning() {
            StopSpawning();

            if (versusChannels.Count > 0) {
                for (var i = 0; i < versusChannels.Count; i++) {
                    var channel = versusChannels[i];
                    if (channel.Settings == null || !channel.Settings.ContinuousSpawnEnabled) {
                        continue;
                    }

                    channel.SpawnCoroutine = StartCoroutine(SpawnLoop(channel.Settings, channel.Slot));
                }

                return;
            }

            if (spawnSettings == null || !spawnSettings.ContinuousSpawnEnabled) {
                return;
            }

            spawnCoroutine = StartCoroutine(SpawnLoop(spawnSettings, null));
        }

        public void StopSpawning() {
            if (spawnCoroutine != null) {
                StopCoroutine(spawnCoroutine);
                spawnCoroutine = null;
            }

            for (var i = 0; i < versusChannels.Count; i++) {
                var channel = versusChannels[i];
                if (channel.SpawnCoroutine != null) {
                    StopCoroutine(channel.SpawnCoroutine);
                    channel.SpawnCoroutine = null;
                }
            }
        }

        public DiceController SpawnInitialDice() {
            var spawned = SpawnInitialPlayerDice(1);
            return spawned.Count > 0 ? spawned[0] : null;
        }

        public List<DiceController> SpawnInitialPlayerDice(int playerCount) {
            if (versusChannels.Count > 0) {
                return SpawnInitialVersusPlayers(playerCount);
            }

            var results = new List<DiceController>();
            if (spawnSettings == null || board == null || registry == null) {
                return results;
            }

            var requiredCount = Mathf.Max(1, playerCount);
            var initialCount = Mathf.Max(spawnSettings.InitialDiceCount, requiredCount);
            var slots = DiceSpawnCellPicker.PickRandomSpawnSlots(
                board,
                registry,
                initialCount,
                spawnSettings.BottomSpawnWeight,
                random);

            if (slots.Count == 0) {
                Debug.LogError("DiceSpawnSystem: No valid Bottom / Top slots for initial dice.");
                return results;
            }

            DiceController standingDice = null;
            for (var i = 0; i < slots.Count; i++) {
                var slot = slots[i];
                var diceController = SpawnDiceAt(
                    slot.Cell,
                    slot.Tier,
                    spawnSettings,
                    diceCatalog,
                    useSpawnAppear: spawnSettings.AnimateInitialDiceSpawn);
                if (diceController == null) {
                    continue;
                }

                if (results.Count < requiredCount) {
                    results.Add(diceController);
                }
            }

            if (results.Count < requiredCount) {
                Debug.LogError(
                    $"DiceSpawnSystem: Failed to spawn {requiredCount} standing dice for players. Spawned {results.Count}.");
                results.Clear();
            }

            return results;
        }

        List<DiceController> SpawnInitialVersusPlayers(int playerCount) {
            var results = new List<DiceController>(playerCount);
            if (board == null || registry == null) {
                return results;
            }

            var requiredCount = Mathf.Min(playerCount, versusChannels.Count);
            for (var i = 0; i < requiredCount; i++) {
                var channel = versusChannels[i];
                if (channel.Settings == null) {
                    Debug.LogError($"DiceSpawnSystem: Spawn settings for {channel.Slot} are not assigned.");
                    results.Clear();
                    return results;
                }

                if (channel.Catalog == null) {
                    Debug.LogError($"DiceSpawnSystem: Dice catalog for {channel.Slot} is not assigned.");
                    results.Clear();
                    return results;
                }

                var initialCount = Mathf.Max(1, channel.Settings.InitialDiceCount);
                var slots = DiceSpawnCellPicker.PickRandomSpawnSlots(
                    board,
                    registry,
                    channel.Slot,
                    initialCount,
                    channel.Settings.BottomSpawnWeight,
                    random);
                if (slots.Count == 0) {
                    Debug.LogError($"DiceSpawnSystem: No valid spawn slots for {channel.Slot}.");
                    results.Clear();
                    return results;
                }

                DiceController standingDice = null;
                for (var j = 0; j < slots.Count; j++) {
                    var diceController = SpawnDiceAt(
                        slots[j].Cell,
                        slots[j].Tier,
                        channel.Settings,
                        channel.Catalog,
                        useSpawnAppear: channel.Settings.AnimateInitialDiceSpawn);
                    if (diceController == null) {
                        continue;
                    }

                    if (standingDice == null) {
                        standingDice = diceController;
                    }
                }

                if (standingDice == null) {
                    Debug.LogError($"DiceSpawnSystem: Failed to spawn initial dice for {channel.Slot}.");
                    results.Clear();
                    return results;
                }

                results.Add(standingDice);
            }

            return results;
        }

        DiceCatalog ResolveCatalog(PlayerSlot? ownerSlot) {
            if (ownerSlot.HasValue) {
                for (var i = 0; i < versusChannels.Count; i++) {
                    var channel = versusChannels[i];
                    if (channel.Slot == ownerSlot.Value) {
                        return channel.Catalog;
                    }
                }
            }

            return diceCatalog;
        }

        DiceController SpawnDiceAt(
            Vector2Int gridPos,
            DiceStackTier tier,
            DiceSpawnSettings activeSpawnSettings,
            DiceCatalog catalog,
            bool useSpawnAppear,
            Action onComplete = null) {
            if (catalog == null) {
                Debug.LogError("DiceSpawnSystem: DiceCatalog is not assigned.");
                return null;
            }

            if (!catalog.TryPickRandomKind(random, out var kind)) {
                Debug.LogError("DiceSpawnSystem: Failed to pick dice kind. Check DiceCatalog spawn weights.");
                return null;
            }

            if (!catalog.TryGetMeshPrefab(kind, out var meshPrefab)) {
                Debug.LogError($"DiceSpawnSystem: Mesh prefab not found for kind={kind}.");
                return null;
            }

            var orientation = DiceSpawnFactory.CreateRandomOrientation();
            var diceController = DiceSpawnFactory.TryCreate(
                diceEntityPrefab,
                spawnParent,
                gridPos,
                tier,
                kind,
                meshPrefab,
                physicsSettings,
                diceAnimationSettings,
                diceErasureSettings);

            if (diceController == null) {
                return null;
            }

            diceController.ConfigureMatchActionContext(matchActionContext);

            var diceView = diceController.View;
            if (useSpawnAppear) {
                diceController.ConfigureWithSpawnAppear(
                    board,
                    diceView,
                    registry,
                    gridPos,
                    orientation,
                    activeSpawnSettings,
                    tier,
                    kind,
                    onComplete);
            } else {
                diceController.Configure(board, diceView, registry, gridPos, orientation, tier, kind);
            }

            erasureSystem?.EnsureDiceSubscribed(diceController);
            return diceController;
        }

        public DiceController SpawnDiceWithAppear(
            DiceSpawnSlot slot,
            PlayerSlot? ownerSlot,
            DiceSpawnSettings activeSpawnSettings,
            Action onComplete = null) {
            if (activeSpawnSettings == null || board == null || registry == null) {
                return null;
            }

            return SpawnDiceAt(
                slot.Cell,
                slot.Tier,
                activeSpawnSettings,
                ResolveCatalog(ownerSlot),
                useSpawnAppear: true,
                onComplete);
        }

        public DiceController SpawnAttackDice(
            PlayerSlot targetSlot,
            DiceKind kind,
            int pip,
            DiceSpawnSettings spawnSettings) {
            var catalog = ResolveCatalog(targetSlot);
            if (spawnSettings == null || board == null || registry == null || catalog == null) {
                return null;
            }

            if (!TryPickAttackSpawnSlot(targetSlot, out var slot)) {
                Debug.LogError($"DiceSpawnSystem: No valid spawn slot for attack dice on {targetSlot}.");
                return null;
            }

            if (!catalog.TryGetMeshPrefab(kind, out var meshPrefab)) {
                Debug.LogError($"DiceSpawnSystem: Mesh prefab not found for attack kind={kind}.");
                return null;
            }

            var orientation = DiceOrientation.CreateWithTopFace(pip);
            var diceController = DiceSpawnFactory.TryCreate(
                diceEntityPrefab,
                spawnParent,
                slot.Cell,
                slot.Tier,
                kind,
                meshPrefab,
                physicsSettings,
                diceAnimationSettings,
                diceErasureSettings);

            if (diceController == null) {
                return null;
            }

            diceController.ConfigureMatchActionContext(matchActionContext);
            var diceView = diceController.View;
            diceController.ConfigureWithSpawnAppear(
                board,
                diceView,
                registry,
                slot.Cell,
                orientation,
                spawnSettings,
                slot.Tier,
                kind,
                forceFallFromAbove: true);

            erasureSystem?.EnsureDiceSubscribed(diceController);
            return diceController;
        }

        bool TryPickAttackSpawnSlot(PlayerSlot targetSlot, out DiceSpawnSlot slot) {
            if (board != null && board.IsVersusArena) {
                attackSpawnCellIndices.TryGetValue(targetSlot, out var nextCellIndex);
                var result = DiceSpawnCellPicker.TryPickSequentialAttackSpawnSlot(
                    board,
                    registry,
                    targetSlot,
                    ref nextCellIndex,
                    out slot);
                attackSpawnCellIndices[targetSlot] = nextCellIndex;
                return result;
            }

            return DiceSpawnCellPicker.TryPickRandomSpawnSlot(
                board,
                registry,
                targetSlot,
                0.5f,
                random,
                out slot);
        }

        IEnumerator SpawnLoop(DiceSpawnSettings activeSpawnSettings, PlayerSlot? ownerSlot) {
            while (enabled) {
                var delay = activeSpawnSettings.SpawnInterval
                    + Random.Range(-activeSpawnSettings.SpawnIntervalJitter, activeSpawnSettings.SpawnIntervalJitter);
                yield return new WaitForSeconds(Mathf.Max(0.01f, delay));

                if (!DiceSpawnCellPicker.TryPickRandomSpawnSlot(
                        board,
                        registry,
                        ownerSlot,
                        activeSpawnSettings.BottomSpawnWeight,
                        random,
                        out var slot)) {
                    yield break;
                }

                SpawnDiceWithAppear(slot, ownerSlot, activeSpawnSettings);
            }
        }

        void OnDisable() {
            StopSpawning();
        }
    }
}
