namespace Quantum
{
    /// <summary>
    /// MVP AttackVolleyBuilder + AttackPowerCalculator (Normal-only, permille math).
    /// </summary>
    public static unsafe class AttackVolleyBuilder
    {
        public static void EnqueueFromErasure(
            Frame frame,
            PlayerRef attacker,
            int face,
            int clusterSize)
        {
            if (attacker == PlayerRef.None || face < 2 || face > 6)
            {
                return;
            }

            var count = ResolveDiceCount(frame, face, clusterSize);
            if (count <= 0)
            {
                return;
            }

            var target = GetOpponent(attacker);
            var delay = ResolveQueueDelay(frame);
            var state = frame.Unsafe.GetOrAddSingletonPointer<VersusAttackState>();

            if (target == 0)
            {
                if (state->RemainingDiceP1 <= 0)
                {
                    state->DelayTicksP1 = delay;
                    state->AttackFaceP1 = face;
                }

                state->RemainingDiceP1 += count;
            }
            else
            {
                if (state->RemainingDiceP2 <= 0)
                {
                    state->DelayTicksP2 = delay;
                    state->AttackFaceP2 = face;
                }

                state->RemainingDiceP2 += count;
            }
        }

        public static int ResolveDiceCount(Frame frame, int face, int clusterSize)
        {
            var maxVolley = frame.RuntimeConfig.AttackMaxVolley;
            if (maxVolley <= 0)
            {
                maxVolley = MatchSimDefaults.AttackMaxVolley;
            }

            var faceGain = ResolvePermille(frame.RuntimeConfig.AttackFaceGainPermille, MatchSimDefaults.AttackFaceGainPermille);
            var sizeGain = ResolvePermille(frame.RuntimeConfig.AttackSizeGainPermille, MatchSimDefaults.AttackSizeGainPermille);
            var attackMul = ResolvePermille(frame.RuntimeConfig.AttackMultiplierPermille, MatchSimDefaults.AttackMultiplierPermille);
            var faceWeight = MatchSimDefaults.AttackFaceWeightPermille;

            // power 竕・Clamp01( (1+(face-2)*FaceGain) * FaceWeight * (1+extra*SizeGain) * AttackMultiplier )
            var faceFactorPermille = 1000 + (face - 2) * faceGain;
            var extra = clusterSize - face;
            if (extra < 0)
            {
                extra = 0;
            }

            var sizeFactorPermille = 1000 + extra * sizeGain;
            var powerPermille = faceFactorPermille * faceWeight / 1000;
            powerPermille = powerPermille * sizeFactorPermille / 1000;
            powerPermille = powerPermille * attackMul / 1000;
            if (powerPermille > 1000)
            {
                powerPermille = 1000;
            }

            if (powerPermille <= 0)
            {
                return 0;
            }

            var count = (powerPermille * maxVolley + 500) / 1000;
            if (count < 0)
            {
                count = 0;
            }

            if (count > maxVolley)
            {
                count = maxVolley;
            }

            return count;
        }

        public static PlayerRef GetOpponent(PlayerRef attacker)
        {
            return attacker == 0 ? (PlayerRef)1 : (PlayerRef)0;
        }

        static int ResolveQueueDelay(Frame frame)
        {
            var ticks = frame.RuntimeConfig.AttackQueueDelayTicks;
            return ticks > 0 ? ticks : MatchSimDefaults.AttackQueueDelayTicks;
        }

        static int ResolvePermille(int configured, int fallback)
        {
            return configured > 0 ? configured : fallback;
        }
    }
}
