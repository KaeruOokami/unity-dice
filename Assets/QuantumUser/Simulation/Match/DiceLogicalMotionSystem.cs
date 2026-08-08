namespace Quantum
{
    /// <summary>
    /// SimTickSchedule stage 0: DiceLogicalMotions 窶・tick erasure timers then finish.
    /// </summary>
    public unsafe class DiceLogicalMotionSystem : SystemMainThread
    {
        const int MaxFinishing = 64;

        public override void Update(Frame frame)
        {
            var finishing = stackalloc EntityRef[MaxFinishing];
            var finishCount = 0;

            var filter = frame.Filter<Dice>();
            while (filter.Next(out var entity, out var dice))
            {
                if (!dice.IsErasing)
                {
                    continue;
                }

                if (!frame.Unsafe.TryGetPointer<Dice>(entity, out var dicePtr))
                {
                    continue;
                }

                dicePtr->EraseTicksRemaining -= 1;
                if (dicePtr->EraseTicksRemaining > 0)
                {
                    continue;
                }

                if (finishCount < MaxFinishing)
                {
                    finishing[finishCount++] = entity;
                }
            }

            for (var i = 0; i < finishCount; i++)
            {
                DiceMatchFinder.FinishEraseAndDemote(frame, finishing[i]);
            }
        }
    }
}
