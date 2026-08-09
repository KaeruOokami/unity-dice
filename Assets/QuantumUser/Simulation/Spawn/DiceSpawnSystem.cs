namespace Quantum
{
    /// <summary>
    /// SimTickSchedule stage 3: continuous spawn 竕・<c>DiceSpawnSystem.SimulateLockstepTick</c>.
    /// </summary>
    public unsafe class DiceSpawnSystem : SystemMainThread
    {
        public override void OnInit(Frame frame)
        {
            EnsureState(frame);
        }

        public override void Update(Frame frame)
        {
            if (!frame.TryGetSingleton<Board>(out var board) || !board.Initialized)
            {
                return;
            }

            var state = frame.Unsafe.GetOrAddSingletonPointer<SpawnState>();
            if (!state->Enabled)
            {
                return;
            }

            TickChannel(frame, board, ref state->CooldownTicksP1, (PlayerRef)0);
            TickChannel(frame, board, ref state->CooldownTicksP2, (PlayerRef)1);
        }

        static void EnsureState(Frame frame)
        {
            var state = frame.Unsafe.GetOrAddSingletonPointer<SpawnState>();
            if (state->Enabled)
            {
                return;
            }

            state->Enabled = true;
            state->CooldownTicksP1 = SampleDelay(frame);
            state->CooldownTicksP2 = SampleDelay(frame);
        }

        static void TickChannel(Frame frame, Board board, ref int cooldown, PlayerRef owner)
        {
            cooldown -= 1;
            while (cooldown <= 0)
            {
                if (!TrySpawnOne(frame, board, owner))
                {
                    cooldown = SampleDelay(frame);
                    break;
                }

                cooldown += SampleDelay(frame);
            }
        }

        static bool TrySpawnOne(Frame frame, Board board, PlayerRef owner)
        {
            var bottomWeight = DiceSpawnRolls.ResolveBottomWeightPermille(frame);
            if (!DiceSpawnCellPicker.TryPickRandomSpawnSlot(
                    frame,
                    board,
                    bottomWeight,
                    out var x,
                    out var y,
                    out var tier))
            {
                return false;
            }

            return BoardBootstrapSystem.TrySpawnDice(
                frame,
                x,
                y,
                DiceSpawnRolls.RollKind(frame),
                tier,
                DiceSpawnRolls.RollTopFace(frame),
                owner);
        }

        static int SampleDelay(Frame frame)
        {
            var interval = frame.RuntimeConfig.SpawnIntervalTicks;
            if (interval <= 0)
            {
                interval = MatchSimDefaults.SpawnIntervalTicks;
            }

            var jitter = frame.RuntimeConfig.SpawnJitterTicks;
            if (jitter < 0)
            {
                jitter = MatchSimDefaults.SpawnJitterTicks;
            }

            if (jitter == 0)
            {
                return interval;
            }

            var delta = frame.RNG->Next(-jitter, jitter + 1);
            var delay = interval + delta;
            return delay < 1 ? 1 : delay;
        }
    }
}
