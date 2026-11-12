using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Grid;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay
{
    /// <summary>
    /// Fire lifetime, adjacent spread ticks, and stack extinguish (Bottom under Top).
    /// Versus: spread stays inside the source die's field (does not cross the partition).
    /// </summary>
    public sealed class DiceFireSystem : MonoBehaviour
    {
        static readonly Direction[] CardinalDirections = {
            Direction.East, Direction.West, Direction.North, Direction.South
        };

        Board board;
        DiceRegistry registry;
        DiceFireSettings settings;
        System.Random random;
        float spreadTimer;
        readonly List<DiceController> igniteBuffer = new();
        readonly List<DiceController> extinguishBuffer = new();

        public DiceFireSettings Settings => settings;

        public void Configure(
            Board targetBoard,
            DiceRegistry targetRegistry,
            DiceFireSettings fireSettings,
            System.Random spawnRandom) {
            board = targetBoard;
            registry = targetRegistry;
            settings = fireSettings;
            random = spawnRandom ?? new System.Random();
            spreadTimer = 0f;
        }

        void Update() {
            if (GameplaySimClock.IsActive) {
                return;
            }

            SimulateLockstepTick(Time.deltaTime);
        }

        public void SimulateLockstepTick(float deltaTime) {
            if (registry == null || settings == null || deltaTime <= 0f) {
                return;
            }

            TickLifetime(deltaTime);
            TickRedHotCool(deltaTime);
            ExtinguishCoveredBottoms();
            TickSpread(deltaTime);
        }

        public bool TryIgnite(DiceController dice) {
            if (dice == null || settings == null) {
                return false;
            }

            return dice.TryIgnite(settings.Duration);
        }

        public void ApplySpawnFire(DiceController dice, bool wantFire) {
            if (!wantFire || dice == null) {
                return;
            }

            TryIgnite(dice);
        }

        void TickLifetime(float deltaTime) {
            var diceList = registry.AllDice;
            for (var i = 0; i < diceList.Count; i++) {
                var dice = diceList[i];
                if (dice == null || !dice.IsOnFire) {
                    continue;
                }

                dice.TickFire(deltaTime);
            }
        }

        void TickRedHotCool(float deltaTime) {
            var diceList = registry.AllDice;
            for (var i = 0; i < diceList.Count; i++) {
                var dice = diceList[i];
                if (dice == null || dice.IsOnFire || dice.RedHotCoolRemaining <= 0f) {
                    continue;
                }

                dice.TickRedHotCool(deltaTime);
            }
        }

        void ExtinguishCoveredBottoms() {
            extinguishBuffer.Clear();
            var diceList = registry.AllDice;
            for (var i = 0; i < diceList.Count; i++) {
                var dice = diceList[i];
                if (dice == null
                    || !dice.IsOnFire
                    || dice.CurrentState.Tier != DiceStackTier.Bottom) {
                    continue;
                }

                if (registry.TryGetTopAt(dice.CurrentState.GridPos, out var top)
                    && top != null
                    && top != dice) {
                    extinguishBuffer.Add(dice);
                }
            }

            for (var i = 0; i < extinguishBuffer.Count; i++) {
                extinguishBuffer[i]?.ExtinguishFire();
            }
        }

        void TickSpread(float deltaTime) {
            spreadTimer += deltaTime;
            if (spreadTimer < settings.SpreadInterval) {
                return;
            }

            spreadTimer -= settings.SpreadInterval;
            igniteBuffer.Clear();

            var diceList = registry.AllDice;
            for (var i = 0; i < diceList.Count; i++) {
                var source = diceList[i];
                if (source == null || !source.IsOnFire || source.IsErasing || source.IsVanishing) {
                    continue;
                }

                TryCollectSpreadTargets(source, igniteBuffer);
            }

            for (var i = 0; i < igniteBuffer.Count; i++) {
                TryIgnite(igniteBuffer[i]);
            }
        }

        void TryCollectSpreadTargets(DiceController source, List<DiceController> targets) {
            var tier = source.CurrentState.Tier;
            var cell = source.CurrentState.GridPos;
            for (var i = 0; i < CardinalDirections.Length; i++) {
                if (random.NextDouble() >= settings.SpreadChance) {
                    continue;
                }

                var neighborCell = cell + CardinalDirections[i].ToGridDelta();
                if (board != null
                    && board.IsVersusArena
                    && board.VersusLayout != null
                    && board.VersusLayout.CrossesPartition(cell, neighborCell)) {
                    continue;
                }

                if (!registry.TryGetDiceAt(neighborCell, tier, out var neighbor)
                    || neighbor == null
                    || neighbor.IsOnFire
                    || neighbor.IsErasing
                    || neighbor.IsVanishing
                    || !DiceFirePolicy.CanCarryFire(neighbor.Behavior)) {
                    continue;
                }

                if (!targets.Contains(neighbor)) {
                    targets.Add(neighbor);
                }
            }
        }
    }
}
