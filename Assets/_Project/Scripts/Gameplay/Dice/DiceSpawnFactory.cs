using System;
using System.Collections;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Grid;
using DiceGame.Placement;
using DiceGame.View;
using UnityEngine;

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

        public static DiceOrientation CreateRandomOrientation(System.Random random) {
            var orientation = DiceOrientation.Default;
            var directions = new[] { Direction.East, Direction.West, Direction.North, Direction.South };
            var rng = random ?? new System.Random();
            var steps = rng.Next(0, 12);

            for (var i = 0; i < steps; i++) {
                orientation = orientation.Roll(directions[rng.Next(0, directions.Length)]);
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
        DiceMatchOwnershipContext ownershipContext;
        DiceMatchErasureSystem erasureSystem;
        DiceSpawnSettings spawnSettings;
        System.Random random;
        readonly List<PlayerSpawnChannel> versusChannels = new();
        VersusInitialDicePlacementMode versusInitialPlacementMode =
            VersusInitialDicePlacementMode.Mirrored;

        Coroutine spawnCoroutine;
        bool gameplayEnabled = true;
        bool emitNetworkSpawns;
        bool allowAutonomousSpawning = true;

        /// <summary>
        /// When false, <see cref="StartSpawning"/> is a no-op (online client follower).
        /// </summary>
        public bool AllowAutonomousSpawning {
            get => allowAutonomousSpawning;
            set => allowAutonomousSpawning = value;
        }

        /// <summary>
        /// When true, successful continuous/attack/jumbo spawns raise <see cref="NetworkSpawnEmitted"/>.
        /// Initial board spawn should leave this false so both peers can seed-spawn locally.
        /// </summary>
        public bool EmitNetworkSpawns {
            get => emitNetworkSpawns;
            set => emitNetworkSpawns = value;
        }

        /// <summary>
        /// Args: dice, reason code, useSpawnAppear, forceFallFromAbove.
        /// Reason: 1=continuous, 2=attack, 3=jumbo.
        /// </summary>
        public event Action<DiceController, byte, bool, bool> NetworkSpawnEmitted;

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
            gameplayEnabled = true;
        }

        public void ConfigureOwnership(DiceMatchOwnershipContext matchOwnership) {
            ownershipContext = matchOwnership;
        }

        public void ConfigureVersusSpawns(
            DiceSpawnSettings player1Settings,
            DiceCatalog player1Catalog,
            DiceSpawnSettings player2Settings,
            DiceCatalog player2Catalog,
            VersusInitialDicePlacementMode initialPlacementMode =
                VersusInitialDicePlacementMode.Mirrored) {
            versusChannels.Clear();
            versusChannels.Add(new PlayerSpawnChannel {
                Slot = PlayerSlot.Player1,
                Settings = player1Settings,
                Catalog = player1Catalog
            });
            versusChannels.Add(new PlayerSpawnChannel {
                Slot = PlayerSlot.Player2,
                Settings = player2Settings,
                Catalog = player2Catalog
            });
            versusInitialPlacementMode = initialPlacementMode;
        }

        public void ConfigureErasureSystem(DiceMatchErasureSystem targetErasureSystem) {
            erasureSystem = targetErasureSystem;
        }

        public void StartSpawning() {
            StopSpawning();

            if (!gameplayEnabled || !allowAutonomousSpawning) {
                return;
            }

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

        public void SetGameplayEnabled(bool enabled) {
            if (gameplayEnabled == enabled) {
                return;
            }

            gameplayEnabled = enabled;
            if (gameplayEnabled) {
                StartSpawning();
            } else {
                StopSpawning();
            }
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
            if (versusInitialPlacementMode == VersusInitialDicePlacementMode.Mirrored) {
                return SpawnInitialVersusPlayersMirrored(requiredCount);
            }

            for (var i = 0; i < requiredCount; i++) {
                var channel = versusChannels[i];
                if (!TryValidateVersusChannel(channel)) {
                    results.Clear();
                    return results;
                }

                if (!TrySpawnInitialChannelDice(channel, out var standingDice)) {
                    results.Clear();
                    return results;
                }

                results.Add(standingDice);
            }

            return results;
        }

        List<DiceController> SpawnInitialVersusPlayersMirrored(int requiredCount) {
            var results = new List<DiceController>(requiredCount);
            if (requiredCount < 2
                || board.VersusLayout == null
                || !TryFindVersusChannel(PlayerSlot.Player1, out var player1Channel)
                || !TryFindVersusChannel(PlayerSlot.Player2, out var player2Channel)) {
                Debug.LogError("DiceSpawnSystem: Mirrored initial spawn requires both player channels and VersusLayout.");
                return results;
            }

            if (!TryValidateVersusChannel(player1Channel) || !TryValidateVersusChannel(player2Channel)) {
                return results;
            }

            var initialCount = Mathf.Max(1, player1Channel.Settings.InitialDiceCount);
            var slots = DiceSpawnCellPicker.PickRandomSpawnSlots(
                board,
                registry,
                PlayerSlot.Player1,
                initialCount,
                player1Channel.Settings.BottomSpawnWeight,
                random);
            if (slots.Count == 0) {
                Debug.LogError("DiceSpawnSystem: No valid spawn slots for Player1.");
                return results;
            }

            var spawnedSpecs = new List<(Vector2Int Cell, DiceStackTier Tier, DiceKind Kind, DiceOrientation Orientation)>(
                slots.Count);
            DiceController player1Standing = null;
            for (var i = 0; i < slots.Count; i++) {
                var diceController = SpawnDiceAt(
                    slots[i].Cell,
                    slots[i].Tier,
                    player1Channel.Settings,
                    player1Channel.Catalog,
                    useSpawnAppear: player1Channel.Settings.AnimateInitialDiceSpawn,
                    catalogOwner: PlayerSlot.Player1);
                if (diceController == null) {
                    continue;
                }

                spawnedSpecs.Add((
                    diceController.CurrentState.GridPos,
                    diceController.CurrentState.Tier,
                    diceController.Kind,
                    diceController.CurrentState.Orientation));
                if (player1Standing == null) {
                    player1Standing = diceController;
                }
            }

            if (player1Standing == null || spawnedSpecs.Count == 0) {
                Debug.LogError("DiceSpawnSystem: Failed to spawn initial dice for Player1.");
                results.Clear();
                return results;
            }

            DiceController player2Standing = null;
            for (var i = 0; i < spawnedSpecs.Count; i++) {
                var spec = spawnedSpecs[i];
                if (!board.VersusLayout.TryMapMirroredCell(PlayerSlot.Player1, spec.Cell, out var mirroredCell)) {
                    Debug.LogError(
                        $"DiceSpawnSystem: Failed to mirror Player1 cell {spec.Cell} for initial spawn.");
                    results.Clear();
                    return results;
                }

                if (!player2Channel.Catalog.TryGetMeshPrefab(spec.Kind, out _)) {
                    Debug.LogError(
                        $"DiceSpawnSystem: Player2 catalog has no mesh for mirrored kind={spec.Kind}.");
                    results.Clear();
                    return results;
                }

                var diceController = SpawnDiceAt(
                    mirroredCell,
                    spec.Tier,
                    player2Channel.Settings,
                    player2Channel.Catalog,
                    useSpawnAppear: player2Channel.Settings.AnimateInitialDiceSpawn,
                    onComplete: null,
                    fixedKind: spec.Kind,
                    fixedOrientation: spec.Orientation,
                    catalogOwner: PlayerSlot.Player2);
                if (diceController == null) {
                    Debug.LogError(
                        $"DiceSpawnSystem: Failed to spawn mirrored dice at {mirroredCell} for Player2.");
                    results.Clear();
                    return results;
                }

                if (player2Standing == null) {
                    player2Standing = diceController;
                }
            }

            if (player2Standing == null) {
                Debug.LogError("DiceSpawnSystem: Failed to spawn initial dice for Player2.");
                results.Clear();
                return results;
            }

            results.Add(player1Standing);
            if (requiredCount > 1) {
                results.Add(player2Standing);
            }

            return results;
        }

        bool TryFindVersusChannel(PlayerSlot slot, out PlayerSpawnChannel channel) {
            for (var i = 0; i < versusChannels.Count; i++) {
                if (versusChannels[i].Slot != slot) {
                    continue;
                }

                channel = versusChannels[i];
                return true;
            }

            channel = null;
            return false;
        }

        bool TryValidateVersusChannel(PlayerSpawnChannel channel) {
            if (channel.Settings == null) {
                Debug.LogError($"DiceSpawnSystem: Spawn settings for {channel.Slot} are not assigned.");
                return false;
            }

            if (channel.Catalog == null) {
                Debug.LogError($"DiceSpawnSystem: Dice catalog for {channel.Slot} is not assigned.");
                return false;
            }

            return true;
        }

        bool TrySpawnInitialChannelDice(PlayerSpawnChannel channel, out DiceController standingDice) {
            standingDice = null;
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
                return false;
            }

            for (var j = 0; j < slots.Count; j++) {
                var diceController = SpawnDiceAt(
                    slots[j].Cell,
                    slots[j].Tier,
                    channel.Settings,
                    channel.Catalog,
                    useSpawnAppear: channel.Settings.AnimateInitialDiceSpawn,
                    catalogOwner: channel.Slot);
                if (diceController == null) {
                    continue;
                }

                if (standingDice == null) {
                    standingDice = diceController;
                }
            }

            if (standingDice == null) {
                Debug.LogError($"DiceSpawnSystem: Failed to spawn initial dice for {channel.Slot}.");
                return false;
            }

            return true;
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

        public bool TryGetSpawnSettings(PlayerSlot ownerSlot, out DiceSpawnSettings settings) {
            for (var i = 0; i < versusChannels.Count; i++) {
                var channel = versusChannels[i];
                if (channel.Slot == ownerSlot) {
                    settings = channel.Settings;
                    return settings != null;
                }
            }

            settings = spawnSettings;
            return settings != null;
        }

        DiceController SpawnDiceAt(
            Vector2Int gridPos,
            DiceStackTier tier,
            DiceSpawnSettings activeSpawnSettings,
            DiceCatalog catalog,
            bool useSpawnAppear,
            Action onComplete = null,
            DiceKind? fixedKind = null,
            DiceOrientation? fixedOrientation = null,
            PlayerSlot? catalogOwner = null) {
            if (catalog == null) {
                Debug.LogError("DiceSpawnSystem: DiceCatalog is not assigned.");
                return null;
            }

            DiceKind kind;
            if (fixedKind.HasValue) {
                kind = fixedKind.Value;
            } else if (!catalog.TryPickRandomKind(random, out kind)) {
                Debug.LogError("DiceSpawnSystem: Failed to pick dice kind. Check DiceCatalog spawn weights.");
                return null;
            }

            if (!catalog.TryGetMeshPrefab(kind, out var meshPrefab)) {
                Debug.LogError($"DiceSpawnSystem: Mesh prefab not found for kind={kind}.");
                return null;
            }

            var orientation = fixedOrientation ?? DiceSpawnFactory.CreateRandomOrientation(random);
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
            if (ownershipContext != null) {
                diceController.ConfigureOwnershipContext(ownershipContext);
                if (catalogOwner.HasValue) {
                    ownershipContext.SetOwner(diceController, catalogOwner.Value);
                }
            }

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
            if (emitNetworkSpawns) {
                NetworkSpawnEmitted?.Invoke(diceController, 1, useSpawnAppear, false);
            }

            return diceController;
        }

        public DiceController SpawnDiceWithAppear(
            DiceSpawnSlot slot,
            PlayerSlot? ownerSlot,
            DiceSpawnSettings activeSpawnSettings,
            Action onComplete = null) {
            if (!gameplayEnabled
                || activeSpawnSettings == null
                || board == null
                || registry == null) {
                return null;
            }

            return SpawnDiceAt(
                slot.Cell,
                slot.Tier,
                activeSpawnSettings,
                ResolveCatalog(ownerSlot),
                useSpawnAppear: true,
                onComplete,
                catalogOwner: ownerSlot);
        }

        public DiceController SpawnAttackDice(
            PlayerSlot targetSlot,
            DiceKind kind,
            int pip,
            DiceSpawnSettings spawnSettings) {
            var catalog = ResolveCatalog(targetSlot);
            if (!gameplayEnabled
                || spawnSettings == null
                || board == null
                || registry == null
                || catalog == null) {
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
            if (ownershipContext != null) {
                diceController.ConfigureOwnershipContext(ownershipContext);
                ownershipContext.SetOwner(diceController, targetSlot);
            }

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
            if (emitNetworkSpawns) {
                NetworkSpawnEmitted?.Invoke(diceController, 2, true, true);
            }

            return diceController;
        }

        public DiceController SpawnJumboDice(
            PlayerSlot targetSlot,
            int topFace,
            DiceSpawnSettings spawnSettings,
            IReadOnlyList<Vector2Int> blockedCells) {
            var catalog = ResolveCatalog(targetSlot);
            if (!gameplayEnabled
                || spawnSettings == null
                || board == null
                || registry == null
                || catalog == null
                || !board.IsVersusArena) {
                return null;
            }

            if (!catalog.TryGetMeshPrefab(DiceKind.Jumbo, out var meshPrefab)) {
                // Fallback to Normal mesh so jumbo remains playable before catalog assets are filled.
                if (!catalog.TryGetMeshPrefab(DiceKind.Normal, out meshPrefab)) {
                    Debug.LogError("DiceSpawnSystem: Mesh prefab not found for Jumbo (or Normal fallback).");
                    return null;
                }
            }

            if (!DiceSpawnCellPicker.TryPickJumboSpawnAnchor(
                    board,
                    targetSlot,
                    blockedCells,
                    random,
                    out var anchor)) {
                Debug.LogError($"DiceSpawnSystem: No valid 2x2 jumbo spawn for {targetSlot}.");
                return null;
            }

            var orientation = DiceOrientation.CreateWithTopFace(topFace);
            var diceController = DiceSpawnFactory.TryCreate(
                diceEntityPrefab,
                spawnParent,
                anchor,
                DiceStackTier.Bottom,
                DiceKind.Jumbo,
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
                anchor,
                orientation,
                spawnSettings,
                DiceStackTier.Bottom,
                DiceKind.Jumbo,
                forceFallFromAbove: true);

            erasureSystem?.EnsureDiceSubscribed(diceController);
            if (emitNetworkSpawns) {
                NetworkSpawnEmitted?.Invoke(diceController, 3, true, true);
            }

            return diceController;
        }

        /// <summary>
        /// Client full-sim experiment: apply a host-authoritative spawn without local RNG.
        /// </summary>
        public DiceController ApplyNetworkSpawn(
            Vector2Int gridPos,
            DiceStackTier tier,
            DiceKind kind,
            DiceOrientation orientation,
            PlayerSlot ownerSlot,
            DiceSpawnSettings activeSpawnSettings,
            bool useSpawnAppear,
            bool forceFallFromAbove) {
            var catalog = ResolveCatalog(ownerSlot);
            if (!gameplayEnabled
                || activeSpawnSettings == null
                || board == null
                || registry == null
                || catalog == null) {
                return null;
            }

            if (!catalog.TryGetMeshPrefab(kind, out var meshPrefab)) {
                if (kind == DiceKind.Jumbo
                    && catalog.TryGetMeshPrefab(DiceKind.Normal, out meshPrefab)) {
                    // Jumbo mesh fallback
                } else {
                    Debug.LogError($"DiceSpawnSystem.ApplyNetworkSpawn: mesh missing for kind={kind}.");
                    return null;
                }
            }

            var previousEmit = emitNetworkSpawns;
            emitNetworkSpawns = false;

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
                emitNetworkSpawns = previousEmit;
                return null;
            }

            diceController.ConfigureMatchActionContext(matchActionContext);
            if (ownershipContext != null) {
                diceController.ConfigureOwnershipContext(ownershipContext);
                ownershipContext.SetOwner(diceController, ownerSlot);
            }

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
                    forceFallFromAbove,
                    onComplete: null);
            } else {
                diceController.Configure(board, diceView, registry, gridPos, orientation, tier, kind);
            }

            erasureSystem?.EnsureDiceSubscribed(diceController);
            emitNetworkSpawns = previousEmit;
            return diceController;
        }

        bool TryPickAttackSpawnSlot(PlayerSlot targetSlot, out DiceSpawnSlot slot) {
            if (board != null && board.IsVersusArena) {
                return DiceSpawnCellPicker.TryPickSequentialAttackSpawnSlot(
                    board,
                    registry,
                    targetSlot,
                    out slot);
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
            while (enabled && gameplayEnabled) {
                var jitter = activeSpawnSettings.SpawnIntervalJitter;
                var delay = activeSpawnSettings.SpawnInterval
                    + (float)((random.NextDouble() * 2.0 - 1.0) * jitter);
                yield return GameplaySimClock.WaitForSeconds(Mathf.Max(0.01f, delay));

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
