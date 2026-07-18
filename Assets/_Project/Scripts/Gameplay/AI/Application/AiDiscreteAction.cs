namespace DiceGame.Gameplay.AI.Application
{
    public abstract class AiDiscreteAction
    {
        /// <summary>
        /// True when the action ended without achieving its purpose (e.g. Timeout).
        /// </summary>
        public bool Failed { get; private set; }

        public abstract void Begin(AiExecutionContext context);
        public abstract void Tick(AiExecutionContext context);
        public abstract bool IsComplete(AiExecutionContext context);

        protected void ClearFailure() {
            Failed = false;
        }

        protected void MarkFailed() {
            Failed = true;
        }
    }
}
