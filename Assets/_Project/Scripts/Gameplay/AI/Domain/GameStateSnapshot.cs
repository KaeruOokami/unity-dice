using System;
using System.Collections.Generic;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    public readonly struct DiceSnapshot : IEquatable<DiceSnapshot>
    {
        public DiceController Controller { get; }
        public Vector2Int GridPos { get; }
        public DiceOrientation Orientation { get; }
        public DiceStackTier Tier { get; }
        public DiceKind Kind { get; }
        public bool IsBusy { get; }
        public bool IsErasing { get; }

        public DiceSnapshot(DiceController controller) {
            Controller = controller;
            var state = controller.CurrentState;
            GridPos = state.GridPos;
            Orientation = state.Orientation;
            Tier = state.Tier;
            Kind = state.Kind;
            IsBusy = controller.IsBusy;
            IsErasing = controller.IsErasing || controller.IsSinkErasing;
        }

        public int TopFace => Orientation.Top;

        public bool Equals(DiceSnapshot other) {
            return Controller == other.Controller;
        }

        public override bool Equals(object obj) {
            return obj is DiceSnapshot other && Equals(other);
        }

        public override int GetHashCode() {
            return Controller != null ? Controller.GetHashCode() : 0;
        }
    }

    public sealed class GameStateSnapshot
    {
        public IReadOnlyList<DiceSnapshot> AllDice { get; }
        public Vector2Int PlayerCell { get; }
        public CharacterPlacement PlayerPlacement { get; }
        public bool PlayerIsOnFloor { get; }
        public bool PlayerIsCarrying { get; }
        public bool PlayerIsJumping { get; }
        public DiceController StandingDice { get; }

        GameStateSnapshot(
            IReadOnlyList<DiceSnapshot> allDice,
            Vector2Int playerCell,
            CharacterPlacement playerPlacement,
            bool playerIsOnFloor,
            bool playerIsCarrying,
            bool playerIsJumping,
            DiceController standingDice) {
            AllDice = allDice;
            PlayerCell = playerCell;
            PlayerPlacement = playerPlacement;
            PlayerIsOnFloor = playerIsOnFloor;
            PlayerIsCarrying = playerIsCarrying;
            PlayerIsJumping = playerIsJumping;
            StandingDice = standingDice;
        }

        public static GameStateSnapshot Capture(CharacterController character, DiceRegistry registry) {
            var diceList = new List<DiceSnapshot>();
            if (registry != null) {
                foreach (var dice in registry.AllDice) {
                    if (dice == null || dice.IsSpawning) {
                        continue;
                    }

                    diceList.Add(new DiceSnapshot(dice));
                }
            }

            return new GameStateSnapshot(
                diceList,
                character.StandingGridCell,
                character.StandingPlacement,
                character.IsOnFloor,
                character.IsCarrying,
                character.IsJumping,
                character.CurrentDice);
        }
    }
}
