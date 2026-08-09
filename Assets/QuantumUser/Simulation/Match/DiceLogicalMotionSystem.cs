namespace Quantum
{
    /// <summary>
    /// SimTickSchedule stage 0: DiceLogicalMotions — tick motion/spawn busy then erasure finish.
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
                if (!frame.Unsafe.TryGetPointer<Dice>(entity, out var dicePtr))
                {
                    continue;
                }

                if (dicePtr->IsMotionBusy)
                {
                    dicePtr->MotionTicksRemaining -= 1;
                    if (dicePtr->MotionTicksRemaining <= 0)
                    {
                        dicePtr->IsMotionBusy = false;
                        dicePtr->MotionTicksRemaining = 0;
                        if (dicePtr->IsSpawning)
                        {
                            dicePtr->IsSpawning = false;
                        }
                    }
                }

                if (!dicePtr->IsErasing)
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
