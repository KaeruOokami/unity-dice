using System;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using DiceGame.Placement;
using DiceGame.Placement.Support;
using DiceGame.SimShared.Move;
using UnityEngine;

namespace DiceGame.Gameplay.Character
{
    public sealed class CharacterTransformDriver
    {
        Board board;
        Transform characterTransform;
        Func<CharacterSupportState> getSupportState;
        Func<float> getCharacterWorldY;
        Func<bool> isTrackingDiceRoll;
        PlayerSlot? movementOwner;

        public void Configure(
            Board targetBoard,
            Transform transform,
            Func<CharacterSupportState> supportStateProvider,
            Func<float> characterWorldYProvider,
            Func<bool> trackingDiceRollProvider,
            PlayerSlot owner) {
            board = targetBoard;
            characterTransform = transform;
            getSupportState = supportStateProvider;
            getCharacterWorldY = characterWorldYProvider;
            isTrackingDiceRoll = trackingDiceRollProvider;
            movementOwner = owner;
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
            var x = position.x;
            var z = position.y;
            CellSurfaceMotion.ClampToCellInterior(ref x, ref z, center.x, center.y, halfExtent);
            return new Vector2(x, z);
        }

        public Vector2 ClampToBoardBounds(Vector2 position) {
            var clamped = ClampToWalkBounds(new Vector3(position.x, 0f, position.y));
            return new Vector2(clamped.x, clamped.z);
        }

        public Vector3 ClampToWalkBounds(Vector3 worldPos) {
            var supportState = getSupportState();
            if (supportState.Support.Kind == SupportKind.Floor) {
                if (board != null && board.IsVersusArena && board.VersusLayout != null && movementOwner.HasValue) {
                    board.VersusLayout.GetPlayerGridBounds(movementOwner.Value, out var minCell, out var maxCell);
                    var minX = minCell.x * board.CellSize;
                    var minZ = minCell.y * board.CellSize;
                    var maxX = maxCell.x * board.CellSize;
                    var maxZ = maxCell.y * board.CellSize;
                    worldPos.x = Mathf.Clamp(worldPos.x, minX, maxX);
                    worldPos.z = Mathf.Clamp(worldPos.z, minZ, maxZ);
                    return worldPos;
                }

                var minXBoard = 0f;
                var minZBoard = 0f;
                var maxXBoard = (board.Width - 1) * board.CellSize;
                var maxZBoard = (board.Height - 1) * board.CellSize;
                worldPos.x = Mathf.Clamp(worldPos.x, minXBoard, maxXBoard);
                worldPos.z = Mathf.Clamp(worldPos.z, minZBoard, maxZBoard);
                return worldPos;
            }

            var center = GetCellCenterXZ(supportState.Cell);
            var limit = GetWalkHalfExtent();
            if (supportState.Support.Kind == SupportKind.Dice
                && supportState.Support.Dice != null
                && supportState.Support.Dice.Capabilities.HasExpandedFootprint) {
                center = ExpandedFootprintWalkPolicy.GetFootprintCenterXZ(supportState.Support.Dice);
                limit = ExpandedFootprintWalkPolicy.GetFootprintWalkHalfExtent(board.CellSize);
            }

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
            if (dice == null) {
                return Vector2.zero;
            }

            var center = dice.GetLogicalCenterXZ();
            return new Vector2(worldPos.x - center.x, worldPos.z - center.y);
        }

        public static Vector2 WorldOffsetFromDiceCenter(Vector3 diceCenter, Vector2 worldXZ) {
            return new Vector2(worldXZ.x - diceCenter.x, worldXZ.y - diceCenter.z);
        }

        public void AlignToDiceFace(DiceController dice, Vector2 nextXZ, float halfExtent) {
            if (dice == null) {
                return;
            }

            AlignToDiceFaceAtCenter(dice.GetLogicalCenterWorld(), nextXZ, halfExtent);
        }

        /// <summary>
        /// Clamp the character onto a dice face using an explicit world center.
        /// Use the move's <c>From</c> cell when logical state has already committed to <c>To</c>
        /// (lockstep), so follow anchors stay on the visual start pose.
        /// </summary>
        public void AlignToDiceFaceAtCenter(Vector3 diceCenter, Vector2 nextXZ, float halfExtent) {
            var nextOffset = WorldOffsetFromDiceCenter(diceCenter, nextXZ);
            var clamped = ClampToFace(nextOffset, halfExtent);
            ApplyWorldPosition(new Vector3(diceCenter.x + clamped.x, 0f, diceCenter.z + clamped.y));
        }

        public bool IsAtOrPastRollTrigger(Vector2 xz, Vector2Int cell, Direction direction, float triggerHalfExtent) {
            var center = GetCellCenterXZ(cell);
            InputDirection.ToGridDelta(direction, out var dx, out var dy);
            return CellSurfaceMotion.IsAtOrPastRollTrigger(
                xz.x,
                xz.y,
                center.x,
                center.y,
                dx,
                dy,
                triggerHalfExtent);
        }

        public static Vector2 CancelMoveIntoDirection(Vector2 current, Vector2 proposed, Direction direction) {
            var x = proposed.x;
            var z = proposed.y;
            InputDirection.ToGridDelta(direction, out var dx, out var dy);
            CellSurfaceMotion.CancelMoveIntoDirection(current.x, current.y, ref x, ref z, dx, dy);
            return new Vector2(x, z);
        }
    }
}
