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

        public bool TryReleaseDue(float deltaTime, out AttackVolley released) {
            released = null;
            if (pending.Count == 0) {
                return false;
            }

            pending[0].RemainingDelay -= deltaTime;
            if (pending[0].RemainingDelay > 0f) {
                return false;
            }

            released = pending[0].Volley;
            pending.RemoveAt(0);
            Changed?.Invoke();
            return true;
        }
    }
}
