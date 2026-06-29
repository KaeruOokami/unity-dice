namespace DiceGame.Core
{
    public readonly struct DiceSlidePlan
    {
        public DiceState From { get; }
        public DiceState To { get; }

        public DiceSlidePlan(DiceState from, DiceState to) {
            From = from;
            To = to;
        }
    }
}
