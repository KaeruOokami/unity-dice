using UnityEngine;

namespace DiceGame.Gameplay.AI.Application
{
    public sealed class AiActionExecutor
    {
        enum ExecutorPhase
        {
            Idle,
            Executing,
            WaitingForIdle
        }

        ExecutorPhase phase = ExecutorPhase.Idle;
        AiDiscreteAction currentAction;
        AiExecutionContext context;
        bool hasCompletedAction;
        bool lastActionFailed;

        public bool IsIdle => phase == ExecutorPhase.Idle;
        public bool IsWaitingForIdle => phase == ExecutorPhase.WaitingForIdle;

        public void Configure(AiExecutionContext executionContext) {
            context = executionContext;
        }

        public bool IsReadyToPlan() {
            if (context == null) {
                return false;
            }

            if (phase == ExecutorPhase.Executing) {
                return false;
            }

            if (phase == ExecutorPhase.WaitingForIdle) {
                return context.IsWorldIdle();
            }

            return context.IsWorldIdle();
        }

        public bool TryConsumeCompletedAction(out bool failed) {
            failed = false;
            if (!hasCompletedAction) {
                return false;
            }

            failed = lastActionFailed;
            hasCompletedAction = false;
            lastActionFailed = false;
            return true;
        }

        public void StartAction(AiDiscreteAction action) {
            if (action == null || context == null) {
                return;
            }

            currentAction = action;
            currentAction.Begin(context);
            phase = ExecutorPhase.Executing;
        }

        public void Tick() {
            if (context == null) {
                return;
            }

            switch (phase) {
                case ExecutorPhase.Executing:
                    if (currentAction == null) {
                        phase = ExecutorPhase.WaitingForIdle;
                        return;
                    }

                    currentAction.Tick(context);
                    if (currentAction.IsComplete(context)) {
                        context.InputSource.SetMove(Vector2.zero);
                        hasCompletedAction = true;
                        lastActionFailed = currentAction.Failed;
                        phase = ExecutorPhase.WaitingForIdle;
                        currentAction = null;
                    }
                    break;
                case ExecutorPhase.WaitingForIdle:
                    context.InputSource.SetMove(Vector2.zero);
                    if (context.IsWorldIdle()) {
                        phase = ExecutorPhase.Idle;
                    }
                    break;
            }
        }

        public void Cancel() {
            currentAction = null;
            phase = ExecutorPhase.Idle;
            hasCompletedAction = false;
            lastActionFailed = false;
            context?.InputSource.SetMove(Vector2.zero);
        }
    }
}
