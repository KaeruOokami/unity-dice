using System;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Gameplay;
using DiceGame.Grid;
using DiceGame.View;
using DiceGame.Versus.Core;
using UnityEngine;

namespace DiceGame.Versus
{
    public sealed class VersusAttackController : MonoBehaviour
    {
        VersusBoardSettings versusSettings;
        DiceSpawnSystem spawnSystem;
        DiceMatchDissolveSystem dissolveSystem;
        AttackQueueView queueView;
        System.Random random;

        readonly Dictionary<PlayerSlot, AttackQueue> incomingQueues = new();

        public void Configure(
            VersusBoardSettings settings,
            Board board,
            DiceCatalog catalog,
            DiceSpawnSystem targetSpawnSystem,
            DiceMatchDissolveSystem targetDissolveSystem,
            System.Random attackRandom,
            Transform viewParent) {
            versusSettings = settings;
            spawnSystem = targetSpawnSystem;
            dissolveSystem = targetDissolveSystem;
            random = attackRandom ?? new System.Random();

            if (dissolveSystem != null) {
                dissolveSystem.ErasureResolved -= OnErasureResolved;
                dissolveSystem.ErasureResolved += OnErasureResolved;
            }

            EnsureQueues();
            EnsureQueueView(board, catalog, viewParent);
        }

        void OnDisable() {
            if (dissolveSystem != null) {
                dissolveSystem.ErasureResolved -= OnErasureResolved;
            }
        }

        void Update() {
            if (versusSettings == null || spawnSystem == null) {
                return;
            }

            TickQueue(PlayerSlot.Player1);
            TickQueue(PlayerSlot.Player2);
        }

        void EnsureQueues() {
            if (!incomingQueues.ContainsKey(PlayerSlot.Player1)) {
                var queue = new AttackQueue();
                queue.Changed += RefreshQueueView;
                incomingQueues[PlayerSlot.Player1] = queue;
            }

            if (!incomingQueues.ContainsKey(PlayerSlot.Player2)) {
                var queue = new AttackQueue();
                queue.Changed += RefreshQueueView;
                incomingQueues[PlayerSlot.Player2] = queue;
            }
        }

        void EnsureQueueView(Board board, DiceCatalog catalog, Transform viewParent) {
            if (queueView == null) {
                var viewObject = new GameObject("AttackQueueView");
                viewObject.transform.SetParent(viewParent != null ? viewParent : transform, false);
                queueView = viewObject.AddComponent<AttackQueueView>();
            }

            queueView.Configure(board, catalog, viewParent != null ? viewParent : transform);
            RefreshQueueView();
        }

        void OnErasureResolved(ErasureResolvedEvent e) {
            if (versusSettings == null) {
                return;
            }

            var attackSettings = versusSettings.GetAttackSettings(e.Attacker);
            if (attackSettings == null) {
                return;
            }

            if (!AttackVolleyBuilder.TryBuild(
                    attackSettings,
                    e.Face,
                    e.ChainCount,
                    e.ClusterSize,
                    e.IsSnatch,
                    random,
                    out var volley)) {
                return;
            }

            incomingQueues[e.Target].Enqueue(volley, attackSettings.QueueToBoardDelay);
        }

        void TickQueue(PlayerSlot defenderSlot) {
            if (!incomingQueues.TryGetValue(defenderSlot, out var queue)) {
                return;
            }

            while (queue.TryReleaseDue(Time.deltaTime, out var released)) {
                SpawnVolley(defenderSlot, released);
            }
        }

        void SpawnVolley(PlayerSlot defenderSlot, AttackVolley volley) {
            var spawnSettings = versusSettings.GetSpawnSettings(defenderSlot);
            if (spawnSettings == null || volley == null) {
                return;
            }

            for (var i = 0; i < volley.Count; i++) {
                var spec = volley.Dice[i];
                spawnSystem.SpawnAttackDice(defenderSlot, spec.Kind, spec.Pip, spawnSettings);
            }
        }

        void RefreshQueueView() {
            if (queueView == null) {
                return;
            }

            queueView.RenderAll(
                incomingQueues[PlayerSlot.Player1].GetPendingVolleys(),
                incomingQueues[PlayerSlot.Player2].GetPendingVolleys());
        }
    }
}
