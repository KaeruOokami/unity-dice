using UnityEngine;

namespace DiceGame.Placement.Support
{
    public readonly struct CharacterSupportState
    {
        public Vector2Int Cell { get; }

        /// <summary>
        /// Discrete height level for standing resolution.
        /// 0=floor, 1=bottom surface height, 2=top surface height, 3=airborne (no support).
        /// </summary>
        public int Level { get; }

        public SupportRef Support { get; }

        public bool IsAirborne => Support.Kind == SupportKind.None;

        CharacterSupportState(Vector2Int cell, int level, SupportRef support) {
            Cell = cell;
            Level = level;
            Support = support;
        }

        public static CharacterSupportState OnFloor(Vector2Int cell) =>
            new CharacterSupportState(cell, level: 0, SupportRef.Floor());

        public static CharacterSupportState OnDice(Vector2Int cell, int level, SupportRef support) =>
            new CharacterSupportState(cell, level, support);

        public static CharacterSupportState Airborne(Vector2Int cell, int level = 3) =>
            new CharacterSupportState(cell, level, SupportRef.None());
    }
}

