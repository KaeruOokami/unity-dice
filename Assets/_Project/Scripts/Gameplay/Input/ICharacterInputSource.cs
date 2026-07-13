using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Gameplay.Input
{
    public interface ICharacterInputSource
    {
        Vector2 ReadMove();
        bool WasLiftPressedThisFrame();
        bool WasJumpPressedThisFrame();
        bool TryGetDirectionPressedThisFrame(out Direction direction);
    }
}
