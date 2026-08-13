using System;
using DiceGame.Core;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    /// <summary>
    /// Fixed-length vector observation writer sized to the configured board.
    /// Empty in-board cells use face channel 0. Layout: player (6) + carried face one-hot (7)
    /// + [y][x][tier][face 0-6].
    /// </summary>
    public static class MlObservationEncoder
    {
        public const int PlayerFeatureCount = 6;
        public const int FaceChannelCount = 7;
        public const int TierCount = 2;

        public static int GetObservationSize(Board board) {
            var width = board != null ? Math.Max(1, board.Width) : 1;
            var height = board != null ? Math.Max(1, board.Height) : 1;
            return GetObservationSize(width, height);
        }

        public static int GetObservationSize(int boardWidth, int boardHeight) {
            var width = Math.Max(1, boardWidth);
            var height = Math.Max(1, boardHeight);
            return PlayerFeatureCount
                + FaceChannelCount
                + width * height * TierCount * FaceChannelCount;
        }

        public static void Write(
            GameStateSnapshot snapshot,
            Board board,
            float[] destination) {
            if (destination == null) {
                throw new ArgumentNullException(nameof(destination));
            }

            var width = board != null ? Math.Max(1, board.Width) : 1;
            var height = board != null ? Math.Max(1, board.Height) : 1;
            var expected = GetObservationSize(width, height);
            if (destination.Length < expected) {
                throw new ArgumentException(
                    $"Observation buffer length {destination.Length} < expected {expected}.",
                    nameof(destination));
            }

            Array.Clear(destination, 0, expected);
            if (snapshot == null) {
                return;
            }

            var index = 0;

            destination[index++] = NormalizeCoord(snapshot.PlayerCell.x, width);
            destination[index++] = NormalizeCoord(snapshot.PlayerCell.y, height);
            destination[index++] = snapshot.PlayerIsOnFloor ? 1f : 0f;
            destination[index++] = snapshot.PlayerIsCarrying ? 1f : 0f;
            destination[index++] = snapshot.PlayerIsJumping ? 1f : 0f;
            destination[index++] = snapshot.StandingDice != null && snapshot.StandingDice.IsBusy ? 1f : 0f;

            WriteFaceOneHot(snapshot.CarriedTopFace, destination, ref index);

            WriteEmptyInBoardCells(board, width, height, destination);

            var dice = snapshot.AllDice;
            if (dice == null) {
                return;
            }

            for (var i = 0; i < dice.Count; i++) {
                var die = dice[i];
                if (die.IsCarried) {
                    continue;
                }

                StampDie(die, width, height, destination);
            }
        }

        static void WriteEmptyInBoardCells(
            Board board,
            int maxWidth,
            int maxHeight,
            float[] destination) {
            if (board == null) {
                return;
            }

            for (var y = 0; y < maxHeight; y++) {
                for (var x = 0; x < maxWidth; x++) {
                    var cell = new Vector2Int(x, y);
                    if (!board.IsInside(cell)) {
                        continue;
                    }

                    WriteFaceAt(x, y, DiceStackTier.Bottom, 0, maxWidth, destination);
                    WriteFaceAt(x, y, DiceStackTier.Top, 0, maxWidth, destination);
                }
            }
        }

        static void StampDie(
            DiceSnapshot die,
            int maxWidth,
            int maxHeight,
            float[] destination) {
            var face = die.TopFace;
            var expanded = DiceBehaviorResolver.GetCapabilities(die.Kind).HasExpandedFootprint;
            if (!expanded) {
                TryWriteFace(die.GridPos, die.Tier, face, maxWidth, maxHeight, destination);
                return;
            }

            for (var dx = 0; dx < JumboFootprint.Size; dx++) {
                for (var dy = 0; dy < JumboFootprint.Size; dy++) {
                    var cell = new Vector2Int(die.GridPos.x + dx, die.GridPos.y + dy);
                    TryWriteFace(cell, DiceStackTier.Bottom, face, maxWidth, maxHeight, destination);
                    TryWriteFace(cell, DiceStackTier.Top, face, maxWidth, maxHeight, destination);
                }
            }
        }

        static void TryWriteFace(
            Vector2Int cell,
            DiceStackTier tier,
            int topFace,
            int maxWidth,
            int maxHeight,
            float[] destination) {
            if (cell.x < 0 || cell.y < 0 || cell.x >= maxWidth || cell.y >= maxHeight) {
                return;
            }

            WriteFaceAt(cell.x, cell.y, tier, topFace, maxWidth, destination);
        }

        static void WriteFaceAt(
            int x,
            int y,
            DiceStackTier tier,
            int topFace,
            int maxWidth,
            float[] destination) {
            var index = FaceSlotIndex(x, y, tier, maxWidth);
            WriteFaceOneHot(topFace, destination, ref index);
        }

        static int FaceSlotIndex(int x, int y, DiceStackTier tier, int maxWidth) {
            var cell = y * maxWidth + x;
            return PlayerFeatureCount
                + FaceChannelCount
                + (cell * TierCount + (int)tier) * FaceChannelCount;
        }

        static void WriteFaceOneHot(int topFace, float[] destination, ref int index) {
            var channel = 0;
            if (topFace >= 1 && topFace <= 6) {
                channel = topFace;
            }

            for (var i = 0; i < FaceChannelCount; i++) {
                destination[index++] = i == channel ? 1f : 0f;
            }
        }

        static float NormalizeCoord(int value, int size) {
            if (size <= 1) {
                return 0f;
            }

            return Mathf.Clamp01(value / (float)(size - 1));
        }
    }
}
