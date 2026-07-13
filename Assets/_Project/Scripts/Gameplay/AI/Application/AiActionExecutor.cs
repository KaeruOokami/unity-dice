using DiceGame.Core;
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
            context?.InputSource.SetMove(Vector2.zero);
        }
    }
}
