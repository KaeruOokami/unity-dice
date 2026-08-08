namespace Quantum
{
    /// <summary>
    /// SimTickSchedule stage 4: VersusAttack 窶・drain delayed attack queue onto opponent board.
    /// </summary>
    public unsafe class VersusAttackSystem : SystemMainThread
    {
        public override void Update(Frame frame)
        {
            if (!frame.TryGetSingleton<Board>(out var board) || !board.Initialized)
            {
                return;
            }

            var state = frame.Unsafe.GetOrAddSingletonPointer<VersusAttackState>();
            TickQueue(frame, board, ref state->DelayTicksP1, ref state->RemainingDiceP1, state->AttackFaceP1, (PlayerRef)0);
            TickQueue(frame, board, ref state->DelayTicksP2, ref state->RemainingDiceP2, state->AttackFaceP2, (PlayerRef)1);
        }

        static void TickQueue(
            Frame frame,
            Board board,
            ref int delayTicks,
            ref int remaining,
            int attackFace,
            PlayerRef targetOwner)
        {
            if (remaining <= 0)
            {
                return;
            }

            if (delayTicks > 0)
            {
                delayTicks -= 1;
                return;
            }

            // Spawn one die per tick while remaining, retry next tick if board full.
            if (!DiceSpawnCellPicker.TryPickAttackSpawnSlot(frame, board, out var x, out var y, out var tier))
            {
                return;
            }

            var faceMax = attackFace < 1 ? 6 : attackFace;
            var pip = frame.RNG->Next(1, faceMax + 1);
            if (BoardBootstrapSystem.TrySpawnDice(
                    frame,
                    x,
                    y,
                    DiceKind.Normal,
                    tier,
                    pip,
                    targetOwner))
            {
                remaining -= 1;
            }
        }
    }
}
