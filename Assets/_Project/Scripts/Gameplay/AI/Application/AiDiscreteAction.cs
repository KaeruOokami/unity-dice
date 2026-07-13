namespace DiceGame.Gameplay.AI.Application
{
    public abstract class AiDiscreteAction
    {
        public abstract void Begin(AiExecutionContext context);
        public abstract void Tick(AiExecutionContext context);
        public abstract bool IsComplete(AiExecutionContext context);
    }
}
