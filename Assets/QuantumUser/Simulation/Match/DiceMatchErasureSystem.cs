namespace Quantum
{
    /// <summary>
    /// SimTickSchedule stage 6: ErasureMatch 窶・begin timed erase + enqueue versus attack.
    /// </summary>
    public unsafe class DiceMatchErasureSystem : SystemMainThread
    {
        public override void Update(Frame frame)
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

            if (!DiceMatchFinder.TryBeginEraseClustersTouching(
                    frame,
                    action,
                    actingPlayer,
                    out var face,
                    out var clusterSize))
            {
                return;
            }

            AttackVolleyBuilder.EnqueueFromErasure(
                frame,
                actingPlayer,
                face,
                clusterSize);
        }
    }
}
