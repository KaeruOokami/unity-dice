namespace Quantum
{
    /// <summary>
    /// Shared kind / face rolls for initial and continuous spawn (catalog weights deferred).
    /// </summary>
    public static unsafe class DiceSpawnRolls
    {
        public static DiceKind RollKind(Frame frame)
        {
            var kindRoll = frame.RNG->Next(0, 6);
            return kindRoll switch
            {
                0 => DiceKind.Wood,
                1 => DiceKind.Ice,
                2 => DiceKind.Magnet,
                _ => DiceKind.Normal,
            };
        }

        public static int RollTopFace(Frame frame)
        {
            return frame.RNG->Next(BoardDefaults.MinFaceValue, BoardDefaults.MaxFaceValue + 1);
        }

        public static int ResolveBottomWeightPermille(Frame frame)
        {
            var bottomWeight = frame.RuntimeConfig.BottomSpawnWeightPermille;
            return bottomWeight > 0
                ? bottomWeight
                : MatchSimDefaults.BottomSpawnWeightPermille;
        }

        public static int ResolveInitialDiceCount(Frame frame, int minimumStandingDice)
        {
            var count = frame.RuntimeConfig.InitialDiceCount;
            if (count <= 0)
            {
                count = BoardDefaults.InitialDiceCount;
            }

            if (count < minimumStandingDice)
            {
                count = minimumStandingDice;
            }

            return count;
        }
    }
}
