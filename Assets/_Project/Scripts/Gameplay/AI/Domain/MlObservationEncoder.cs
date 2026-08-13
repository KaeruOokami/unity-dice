using System;
using DiceGame.Core;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    /// <summary>
    /// Fixed-length vector observation writer for ML character agents.
    /// </summary>
    public static class MlObservationEncoder
    {
        public const int PlayerFeatureCount = 6;
        public const int DieFeatureCount = 13;
        public const int DiceKindCount = 8;

        public static int GetObservationSize(int maxObservedDice) {
            var clamped = Math.Max(0, maxObservedDice);
            return PlayerFeatureCount + clamped * DieFeatureCount;
        }

        public static void Write(
            GameStateSnapshot snapshot,
            Board board,
            int maxObservedDice,
            float[] destination) {
            if (destination == null) {
                throw new ArgumentNullException(nameof(destination));
            }

            var expected = GetObservationSize(maxObservedDice);
            if (destination.Length < expected) {
                throw new ArgumentException(
                    $"Observation buffer length {destination.Length} < expected {expected}.",
                    nameof(destination));
            }

            var width = board != null ? Math.Max(1, board.Width) : 1;
            var height = board != null ? Math.Max(1, board.Height) : 1;
            var index = 0;

            destination[index++] = NormalizeCoord(snapshot.PlayerCell.x, width);
            destination[index++] = NormalizeCoord(snapshot.PlayerCell.y, height);
            destination[index++] = snapshot.PlayerIsOnFloor ? 1f : 0f;
            destination[index++] = snapshot.PlayerIsCarrying ? 1f : 0f;
            destination[index++] = snapshot.PlayerIsJumping ? 1f : 0f;
            destination[index++] = snapshot.StandingDice != null && snapshot.StandingDice.IsBusy ? 1f : 0f;

            var dice = snapshot.PlanningDice;
            var written = 0;
            for (var i = 0; i < dice.Count && written < maxObservedDice; i++) {
                WriteDie(dice[i], width, height, destination, ref index);
                written++;
            }

            while (written < maxObservedDice) {
                WriteEmptyDie(destination, ref index);
                written++;
            }
        }

        static void WriteDie(
            DiceSnapshot die,
            int width,
            int height,
            float[] destination,
            ref int index) {
            destination[index++] = 1f;
            destination[index++] = NormalizeCoord(die.GridPos.x, width);
            destination[index++] = NormalizeCoord(die.GridPos.y, height);
            destination[index++] = die.TopFace / 6f;
            destination[index++] = die.Tier == DiceStackTier.Top ? 1f : 0f;
            WriteKindOneHot(die.Kind, destination, ref index);
        }

        static void WriteEmptyDie(float[] destination, ref int index) {
            destination[index++] = 0f;
            destination[index++] = 0f;
            destination[index++] = 0f;
            destination[index++] = 0f;
            destination[index++] = 0f;
            for (var i = 0; i < DiceKindCount; i++) {
                destination[index++] = 0f;
            }
        }

        static void WriteKindOneHot(DiceKind kind, float[] destination, ref int index) {
            var kindIndex = (int)kind;
            for (var i = 0; i < DiceKindCount; i++) {
                destination[index++] = i == kindIndex ? 1f : 0f;
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
