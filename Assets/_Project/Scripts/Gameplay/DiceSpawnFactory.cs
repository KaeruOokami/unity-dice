using System;
using System.Collections;
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
            DiceDissolveSettings dissolveSettings) {
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

            diceView.Configure(physicsSettings, animationSettings, dissolveSettings);
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
        Board board;
        DiceRegistry registry;
        GameObject diceEntityPrefab;
        DiceCatalog diceCatalog;
        Transform spawnParent;
        PhysicsSettings physicsSettings;
        DiceAnimationSettings diceAnimationSettings;
        DiceDissolveSettings diceDissolveSettings;
        DiceSpawnSettings spawnSettings;
        System.Random random;

        Coroutine spawnCoroutine;

        public void Configure(
            Board targetBoard,
            DiceRegistry targetRegistry,
            GameObject prefab,
            DiceCatalog catalog,
            Transform parent,
            PhysicsSettings physics,
            DiceAnimationSettings animationSettings,
            DiceDissolveSettings dissolveSettings,
            DiceSpawnSettings settings,
            System.Random spawnRandom) {
            board = targetBoard;
            registry = targetRegistry;
            diceEntityPrefab = prefab;
            diceCatalog = catalog;
            spawnParent = parent;
            physicsSettings = physics;
            diceAnimationSettings = animationSettings;
            diceDissolveSettings = dissolveSettings;
            spawnSettings = settings;
            random = spawnRandom;
        }

        public void StartSpawning() {
            if (spawnSettings == null || !spawnSettings.ContinuousSpawnEnabled) {
                return;
            }

            if (spawnCoroutine != null) {
                StopCoroutine(spawnCoroutine);
            }

            spawnCoroutine = StartCoroutine(SpawnLoop());
        }

        public void StopSpawning() {
            if (spawnCoroutine != null) {
                StopCoroutine(spawnCoroutine);
                spawnCoroutine = null;
            }
        }

        public DiceController SpawnInitialDice() {
            if (spawnSettings == null || board == null || registry == null) {
                return null;
            }

            var initialCount = Mathf.Max(1, spawnSettings.InitialDiceCount);
            var slots = DiceSpawnCellPicker.PickRandomSpawnSlots(
                board,
                registry,
                initialCount,
                spawnSettings.BottomSpawnWeight,
                random);

            if (slots.Count == 0) {
                Debug.LogError("DiceSpawnSystem: No valid Bottom / Top slots for initial dice.");
                return null;
            }

            DiceController firstDice = null;

            for (var i = 0; i < slots.Count; i++) {
                var slot = slots[i];
                var diceController = SpawnDiceAt(
                    slot.Cell,
                    slot.Tier,
                    useSpawnAppear: ShouldAnimateInitialDice(i));
                if (diceController == null) {
                    continue;
                }

                firstDice ??= diceController;
            }

            return firstDice;
        }

        bool ShouldAnimateInitialDice(int index) {
            return spawnSettings.AnimateInitialDiceSpawn && index > 0;
        }

        DiceController SpawnDiceAt(
            Vector2Int gridPos,
            DiceStackTier tier,
            bool useSpawnAppear,
            Action onComplete = null) {
            if (diceCatalog == null) {
                Debug.LogError("DiceSpawnSystem: DiceCatalog is not assigned.");
                return null;
            }

            if (!diceCatalog.TryPickRandomKind(random, out var kind)) {
                Debug.LogError("DiceSpawnSystem: Failed to pick dice kind. Check DiceCatalog spawn weights.");
                return null;
            }

            if (!diceCatalog.TryGetMeshPrefab(kind, out var meshPrefab)) {
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
                diceDissolveSettings);

            if (diceController == null) {
                return null;
            }

            var diceView = diceController.View;
            if (useSpawnAppear) {
                diceController.ConfigureWithSpawnAppear(
                    board,
                    diceView,
                    registry,
                    gridPos,
                    orientation,
                    spawnSettings,
                    tier,
                    kind,
                    onComplete);
            } else {
                diceController.Configure(board, diceView, registry, gridPos, orientation, tier, kind);
            }

            return diceController;
        }

        public DiceController SpawnDiceWithAppear(DiceSpawnSlot slot, Action onComplete = null) {
            if (spawnSettings == null || board == null || registry == null) {
                return null;
            }

            return SpawnDiceAt(slot.Cell, slot.Tier, useSpawnAppear: true, onComplete);
        }

        IEnumerator SpawnLoop() {
            while (enabled) {
                var delay = spawnSettings.SpawnInterval
                    + Random.Range(-spawnSettings.SpawnIntervalJitter, spawnSettings.SpawnIntervalJitter);
                yield return new WaitForSeconds(Mathf.Max(0.01f, delay));

                if (!DiceSpawnCellPicker.TryPickRandomSpawnSlot(
                        board,
                        registry,
                        spawnSettings.BottomSpawnWeight,
                        random,
                        out var slot)) {
                    yield break;
                }

                SpawnDiceWithAppear(slot);
            }
        }

        void OnDisable() {
            StopSpawning();
        }
    }
}
