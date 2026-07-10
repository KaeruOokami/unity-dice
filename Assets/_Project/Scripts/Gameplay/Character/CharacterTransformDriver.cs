using System;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using DiceGame.Placement;
using DiceGame.Placement.Support;
using UnityEngine;

namespace DiceGame.Gameplay.Character
{
    public sealed class CharacterTransformDriver
    {
        const float EdgeEpsilon = 0.001f;

        Board board;
        Transform characterTransform;
        Func<CharacterSupportState> getSupportState;
        Func<float> getCharacterWorldY;
        Func<bool> isTrackingDiceRoll;

        public void Configure(
            Board targetBoard,
            Transform transform,
            Func<CharacterSupportState> supportStateProvider,
            Func<float> characterWorldYProvider,
            Func<bool> trackingDiceRollProvider) {
            board = targetBoard;
            characterTransform = transform;
            getSupportState = supportStateProvider;
            getCharacterWorldY = characterWorldYProvider;
            isTrackingDiceRoll = trackingDiceRollProvider;
        }

        public float GetWalkHalfExtent() {
            return board.CellSize * 0.5f;
        }

        public Vector2 GetWorldXZ() {
            if (characterTransform == null) {
                return Vector2.zero;
            }

            var position = characterTransform.position;
            return new Vector2(position.x, position.z);
        }

        public Vector2 GetCellCenterXZ(Vector2Int grid) {
            var world = board.GridToWorld(grid);
            return new Vector2(world.x, world.z);
        }

        public void ApplyWorldPosition(Vector3 worldPos) {
            if (characterTransform == null || board == null) {
                return;
            }

            worldPos.y = getCharacterWorldY();
            characterTransform.position = worldPos;
            characterTransform.rotation = Quaternion.identity;
        }

        public void SnapYToSurface() {
            if (characterTransform == null || (isTrackingDiceRoll?.Invoke() ?? false)) {
                return;
            }

            var position = characterTransform.position;
            position.y = getCharacterWorldY();
            characterTransform.position = position;
            characterTransform.rotation = Quaternion.identity;
        }

        public Vector2 ClampToCellInterior(Vector2 position, Vector2Int cell, float halfExtent) {
            var center = GetCellCenterXZ(cell);
            return new Vector2(
                Mathf.Clamp(position.x, center.x - halfExtent, center.x + halfExtent),
                Mathf.Clamp(position.y, center.y - halfExtent, center.y + halfExtent));
        }

        public Vector2 ClampToBoardBounds(Vector2 position) {
            var clamped = ClampToWalkBounds(new Vector3(position.x, 0f, position.y));
            return new Vector2(clamped.x, clamped.z);
        }

        public Vector3 ClampToWalkBounds(Vector3 worldPos) {
            var supportState = getSupportState();
            if (supportState.Support.Kind == SupportKind.Floor) {
                var minX = 0f;
                var minZ = 0f;
                var maxX = (board.Width - 1) * board.CellSize;
                var maxZ = (board.Height - 1) * board.CellSize;
                worldPos.x = Mathf.Clamp(worldPos.x, minX, maxX);
                worldPos.z = Mathf.Clamp(worldPos.z, minZ, maxZ);
                return worldPos;
            }

            var center = GetCellCenterXZ(supportState.Cell);
            var limit = GetWalkHalfExtent();
            worldPos.x = Mathf.Clamp(worldPos.x, center.x - limit, center.x + limit);
            worldPos.z = Mathf.Clamp(worldPos.z, center.y - limit, center.y + limit);
            return worldPos;
        }

        public static Vector2 ClampToFace(Vector2 offset, float edgeLimit) {
            return new Vector2(
                Mathf.Clamp(offset.x, -edgeLimit, edgeLimit),
                Mathf.Clamp(offset.y, -edgeLimit, edgeLimit));
        }

        public static Vector2 GetOffsetFromDiceCenter(DiceController dice, Vector3 worldPos) {
            if (dice?.View.DiceTransform == null) {
                return Vector2.zero;
            }

            var center = dice.View.DiceTransform.position;
            return new Vector2(worldPos.x - center.x, worldPos.z - center.z);
        }

        public static Vector2 WorldOffsetFromDiceCenter(Vector3 diceCenter, Vector2 worldXZ) {
            return new Vector2(worldXZ.x - diceCenter.x, worldXZ.y - diceCenter.z);
        }

        public void AlignToDiceFace(DiceController dice, Vector2 nextXZ, float halfExtent) {
            if (dice?.View.DiceTransform == null) {
                return;
            }

            var diceCenter = dice.View.DiceTransform.position;
            var nextOffset = WorldOffsetFromDiceCenter(diceCenter, nextXZ);
            var clamped = ClampToFace(nextOffset, halfExtent);
            ApplyWorldPosition(new Vector3(diceCenter.x + clamped.x, 0f, diceCenter.z + clamped.y));
        }

        public bool IsAtOrPastRollTrigger(Vector2 xz, Vector2Int cell, Direction direction, float triggerHalfExtent) {
            var center = GetCellCenterXZ(cell);

            switch (direction) {
                case Direction.East:
                    return xz.x >= center.x + triggerHalfExtent - EdgeEpsilon;
                case Direction.West:
                    return xz.x <= center.x - triggerHalfExtent + EdgeEpsilon;
                case Direction.North:
                    return xz.y >= center.y + triggerHalfExtent - EdgeEpsilon;
                case Direction.South:
                    return xz.y <= center.y - triggerHalfExtent + EdgeEpsilon;
                default:
                    return false;
            }
        }

        public static Vector2 CancelMoveIntoDirection(Vector2 current, Vector2 proposed, Direction direction) {
            var result = proposed;

            switch (direction) {
                case Direction.East:
                    if (proposed.x > current.x) {
                        result.x = current.x;
                    }

                    break;
                case Direction.West:
                    if (proposed.x < current.x) {
                        result.x = current.x;
                    }

                    break;
                case Direction.North:
                    if (proposed.y > current.y) {
                        result.y = current.y;
                    }

                    break;
                case Direction.South:
                    if (proposed.y < current.y) {
                        result.y = current.y;
                    }

                    break;
            }

            return result;
        }
    }
}
