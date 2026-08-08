namespace Quantum
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Lightweight debug view for Phase B entities (no EntityPrototype required).
    /// Observes verified/predicted frames and mirrors grid poses to Unity transforms.
    /// </summary>
    public sealed class PhaseBSliceViewBinder : MonoBehaviour
    {
        [SerializeField] float pawnScale = 0.7f;
        [SerializeField] float diceScale = 0.45f;
        [SerializeField] Color pawnColor = new Color(0.2f, 0.65f, 1f, 1f);
        [SerializeField] Color diceColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] Color carriedDiceColor = new Color(1f, 0.45f, 0.1f, 1f);

        readonly Dictionary<EntityRef, Transform> views = new();
        Transform root;

        void OnEnable()
        {
            root = new GameObject("PhaseBViews").transform;
            root.SetParent(transform, false);
            QuantumCallback.Subscribe(this, (CallbackUpdateView callback) => OnUpdateView(callback));
            QuantumCallback.Subscribe(this, (CallbackGameDestroyed callback) => ClearViews());
        }

        void OnDisable()
        {
            ClearViews();
        }

        void OnUpdateView(CallbackUpdateView callback)
        {
            var frame = callback.Game.Frames.Predicted;
            if (frame == null)
            {
                return;
            }

            var alive = new HashSet<EntityRef>();
            var filter = frame.Filter<PhaseBGridPose>();
            while (filter.Next(out var entity, out var pose))
            {
                alive.Add(entity);
                var view = GetOrCreateView(frame, entity);
                var world = new Vector3(pose.X, 0f, pose.Y);
                if (frame.TryGet<Transform2D>(entity, out var transform2D))
                {
                    world = new Vector3(transform2D.Position.X.AsFloat, 0f, transform2D.Position.Y.AsFloat);
                }

                if (frame.TryGet<PhaseBDice>(entity, out var dice) && dice.IsCarried)
                {
                    world.y = 0.75f;
                    SetColor(view, carriedDiceColor);
                }
                else if (frame.Has<PhaseBDice>(entity))
                {
                    SetColor(view, diceColor);
                }
                else
                {
                    SetColor(view, pawnColor);
                }

                view.position = world;
            }

            var stale = new List<EntityRef>();
            foreach (var pair in views)
            {
                if (!alive.Contains(pair.Key))
                {
                    stale.Add(pair.Key);
                }
            }

            foreach (var entity in stale)
            {
                if (views.TryGetValue(entity, out var t) && t != null)
                {
                    Destroy(t.gameObject);
                }

                views.Remove(entity);
            }
        }

        Transform GetOrCreateView(Frame frame, EntityRef entity)
        {
            if (views.TryGetValue(entity, out var existing) && existing != null)
            {
                return existing;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = frame.Has<PhaseBPlayerPawn>(entity)
                ? $"Pawn_{entity.Index}"
                : $"Dice_{entity.Index}";
            go.transform.SetParent(root, false);
            var scale = frame.Has<PhaseBPlayerPawn>(entity) ? pawnScale : diceScale;
            go.transform.localScale = Vector3.one * scale;

            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            views[entity] = go.transform;
            return go.transform;
        }

        static void SetColor(Transform view, Color color)
        {
            var renderer = view.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }
        }

        void ClearViews()
        {
            foreach (var pair in views)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value.gameObject);
                }
            }

            views.Clear();
            if (root != null)
            {
                Destroy(root.gameObject);
                root = null;
            }
        }
    }
}
