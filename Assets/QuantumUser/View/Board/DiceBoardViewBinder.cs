namespace Quantum
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Debug view for the Quantum board slice: kind/tier tinting, erase fade, stacked Y offsets.
    /// </summary>
    public sealed class DiceBoardViewBinder : MonoBehaviour
    {
        [SerializeField] float pawnScale = 0.7f;
        [SerializeField] float diceScale = 0.45f;
        [SerializeField] Color pawnColor = new Color(0.2f, 0.65f, 1f, 1f);
        [SerializeField] Color carriedDiceColor = new Color(1f, 0.45f, 0.1f, 1f);
        [SerializeField] Color normalColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] Color woodColor = new Color(0.72f, 0.45f, 0.2f, 1f);
        [SerializeField] Color ironColor = new Color(0.55f, 0.55f, 0.6f, 1f);
        [SerializeField] Color magnetColor = new Color(0.85f, 0.2f, 0.35f, 1f);
        [SerializeField] Color iceColor = new Color(0.55f, 0.85f, 1f, 1f);
        [SerializeField] Color stoneColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        [SerializeField] Color ghostColor = new Color(0.75f, 0.75f, 1f, 0.55f);
        [SerializeField] Color jumboColor = new Color(0.95f, 0.55f, 0.1f, 1f);

        readonly Dictionary<EntityRef, Transform> views = new();
        Transform root;

        void OnEnable()
        {
            root = new GameObject("DiceBoardViews").transform;
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
            var filter = frame.Filter<GridPose>();
            while (filter.Next(out var entity, out var pose))
            {
                alive.Add(entity);
                var view = GetOrCreateView(frame, entity);
                var world = new Vector3(pose.X, 0f, pose.Y);
                if (frame.TryGet<Transform2D>(entity, out var transform2D))
                {
                    world = new Vector3(transform2D.Position.X.AsFloat, 0f, transform2D.Position.Y.AsFloat);
                }

                if (frame.TryGet<Dice>(entity, out var dice))
                {
                    var color = ColorForKind(dice.Kind);
                    if (dice.IsCarried)
                    {
                        world.y = 0.9f;
                        color = carriedDiceColor;
                    }
                    else
                    {
                        world.y = dice.Tier == DiceStackTier.Top ? 0.55f : 0.15f;
                    }

                    if (dice.IsErasing && dice.EraseTicksTotal > 0)
                    {
                        var t = dice.EraseTicksRemaining / (float)dice.EraseTicksTotal;
                        color.a = Mathf.Lerp(0.15f, color.a, Mathf.Clamp01(t));
                        world.y *= Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(t));
                    }

                    SetColor(view, color);
                    view.localScale = Vector3.one * diceScale;
                    view.name = $"Dice_{dice.Kind}_{dice.TopFace}_{entity.Index}";
                }
                else
                {
                    world.y = 0.35f;
                    SetColor(view, pawnColor);
                    view.localScale = Vector3.one * pawnScale;
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

        Color ColorForKind(DiceKind kind)
        {
            switch (kind)
            {
                case DiceKind.Wood: return woodColor;
                case DiceKind.Iron: return ironColor;
                case DiceKind.Magnet: return magnetColor;
                case DiceKind.Ice: return iceColor;
                case DiceKind.Stone: return stoneColor;
                case DiceKind.Ghost: return ghostColor;
                case DiceKind.Jumbo: return jumboColor;
                default: return normalColor;
            }
        }

        Transform GetOrCreateView(Frame frame, EntityRef entity)
        {
            if (views.TryGetValue(entity, out var existing) && existing != null)
            {
                return existing;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = frame.Has<PlayerPawn>(entity)
                ? $"Pawn_{entity.Index}"
                : $"Dice_{entity.Index}";
            go.transform.SetParent(root, false);

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
