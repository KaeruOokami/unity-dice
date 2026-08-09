namespace Quantum
{
    /// <summary>
    /// Queues match evaluation for after dice motion settles (production ActionCompleted).
    /// </summary>
    public static unsafe class MatchSettleRequest
    {
        public static void Request(Frame frame, EntityRef diceEntity, PlayerRef actingPlayer)
        {
            if (!diceEntity.IsValid
                || !frame.Unsafe.TryGetPointer<Dice>(diceEntity, out var dice))
            {
                return;
            }

            dice->HasPendingMatch = true;
            dice->PendingMatchPlayer = actingPlayer;
        }
    }
}
