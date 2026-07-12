using System;
using System.Collections.Generic;
using DiceGame.Versus.Core;
using UnityEngine;

namespace DiceGame.Versus
{
    sealed class PendingVolley
    {
        public AttackVolley Volley;
        public float RemainingDelay;
    }

    public sealed class AttackQueue
    {
        readonly List<PendingVolley> pending = new();

        public event Action Changed;

        public int Count => pending.Count;

        public IReadOnlyList<AttackVolley> GetPendingVolleys() {
            var results = new List<AttackVolley>(pending.Count);
            for (var i = 0; i < pending.Count; i++) {
                results.Add(pending[i].Volley);
            }

            return results;
        }

        public void Enqueue(AttackVolley volley, float delay) {
            if (volley == null || volley.Count == 0) {
                return;
            }

            pending.Add(new PendingVolley {
                Volley = volley,
                RemainingDelay = Mathf.Max(0f, delay)
            });
            Changed?.Invoke();
        }

        public bool IsHeadReady(float deltaTime) {
            if (pending.Count == 0) {
                return false;
            }

            pending[0].RemainingDelay -= deltaTime;
            return pending[0].RemainingDelay <= 0f;
        }

        public AttackVolley PeekHead() {
            return pending.Count == 0 ? null : pending[0].Volley;
        }

        public void DequeueHead() {
            if (pending.Count == 0) {
                return;
            }

            pending.RemoveAt(0);
            Changed?.Invoke();
        }

        public void ReplaceHead(AttackVolley volley) {
            if (pending.Count == 0 || volley == null) {
                return;
            }

            if (volley.Count == 0) {
                DequeueHead();
                return;
            }

            pending[0].Volley = volley;
            Changed?.Invoke();
        }
    }
}
