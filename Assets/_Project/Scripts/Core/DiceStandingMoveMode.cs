namespace DiceGame.Core
{
    /// <summary>
    /// Standing-dice coupled movement mode for the player.
    /// Resolved once via <see cref="DiceBehaviorResolver.ResolveStandingMoveMode"/>;
    /// L2 evaluators switch on this instead of ordered capability probes.
    /// </summary>
    public enum DiceStandingMoveMode
    {
        /// <summary>No standing die, or no coupled move capability.</summary>
        None = 0,
        /// <summary>L1: player moves alone (Iron, Stone-on-jump, sink-erasing, immovable Magnet, etc.).</summary>
        PlayerOnly = 1,
        /// <summary>L2: slide-until-blocked (Ice).</summary>
        Slide = 2,
        /// <summary>L2: grid roll / top-fall (Normal, Wood, Magnet, Stone-on-ground, etc.).</summary>
        Roll = 3
    }
}
