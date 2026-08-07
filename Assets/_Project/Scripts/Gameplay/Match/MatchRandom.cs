using System;
using UnityEngine;

namespace DiceGame.Gameplay
{
    /// <summary>
    /// Single seeded PRNG for match simulation. Gameplay must not use <see cref="UnityEngine.Random"/>.
    /// </summary>
    public sealed class MatchRandom
    {
        readonly System.Random random;

        public int Seed { get; }
        public int DrawCount { get; private set; }

        public MatchRandom(int seed) {
            if (seed == 0) {
                throw new ArgumentException("MatchRandom seed must be non-zero.", nameof(seed));
            }

            Seed = seed;
            random = new System.Random(seed);
        }

        public System.Random Source => random;

        public int Next() {
            DrawCount++;
            return random.Next();
        }

        public int Next(int maxExclusive) {
            DrawCount++;
            return random.Next(maxExclusive);
        }

        public int Next(int minInclusive, int maxExclusive) {
            DrawCount++;
            return random.Next(minInclusive, maxExclusive);
        }

        public float NextFloat() {
            DrawCount++;
            return (float)random.NextDouble();
        }

        public double NextDouble() {
            DrawCount++;
            return random.NextDouble();
        }

        /// <summary>
        /// Host/local seed roll only. Not for gameplay simulation draws.
        /// </summary>
        public static int CreateMatchSeed() {
            return UnityEngine.Random.Range(1, int.MaxValue);
        }
    }
}
