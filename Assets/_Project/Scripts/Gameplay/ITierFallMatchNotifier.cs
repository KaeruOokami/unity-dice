namespace DiceGame.Gameplay
{
    public interface ITierFallMatchNotifier
    {
        void NotifyTierFallCompleted(DiceController fallenDice);
    }
}
