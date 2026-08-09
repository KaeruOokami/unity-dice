namespace Quantum
{
    /// <summary>
    /// SimTickSchedule stage 6: evaluate settled match requests (after MotionBusy clears).
    /// </summary>
    public unsafe class DiceMatchErasureSystem : SystemMainThread
    {
        const int MaxSettleBatch = 32;

        public override void Update(Frame frame)
        {
            MigrateLegacyMatchPending(frame);

            var settled = stackalloc EntityRef[MaxSettleBatch];
            var players = stackalloc PlayerRef[MaxSettleBatch];
            var count = 0;

            var filter = frame.Filter<Dice>();
            while (filter.Next(out var entity, out var dice))
            {
                if (!dice.HasPendingMatch
                    || dice.IsMotionBusy
                    || dice.IsSpawning
                    || dice.IsErasing
                    || dice.IsCarried)
                {
                    continue;
                }

                if (count >= MaxSettleBatch)
                {
                    break;
                }

                settled[count] = entity;
                players[count] = dice.PendingMatchPlayer;
                count++;
            }

            for (var i = 0; i < count; i++)
            {
                var entity = settled[i];
                var actingPlayer = players[i];
                if (frame.Unsafe.TryGetPointer<Dice>(entity, out var dicePtr))
                {
                    dicePtr->HasPendingMatch = false;
                    dicePtr->PendingMatchPlayer = default;
                }

                if (!DiceMatchFinder.TryBeginEraseClustersTouching(
                        frame,
                        entity,
                        actingPlayer,
                        out var face,
                        out var clusterSize))
                {
                    continue;
                }

                AttackVolleyBuilder.EnqueueFromErasure(
                    frame,
                    actingPlayer,
                    face,
                    clusterSize);
            }
        }

        static void MigrateLegacyMatchPending(Frame frame)
        {
            if (!frame.TryGetSingleton<MatchPending>(out var pending) || !pending.HasPending)
            {
                return;
            }

            var action = pending.ActionDice;
            var actingPlayer = pending.ActingPlayer;
            frame.SetSingleton(new MatchPending
            {
                HasPending = false,
                ActionDice = EntityRef.None,
                ActingPlayer = default,
            });

            if (action.IsValid)
            {
                MatchSettleRequest.Request(frame, action, actingPlayer);
            }
        }
    }
}
