using DiceGame.Grid;

namespace DiceGame.Config
{
    public interface IVersusBoardSettings
    {
        PlayerBoardDefinition Player1 { get; }
        PlayerBoardDefinition Player2 { get; }
        VersusInitialDicePlacementMode InitialDicePlacementMode { get; }
        AttackQueueUiSettings AttackQueueUiSettings { get; }
        JumboDiceSettings JumboDiceSettings { get; }
        VersusArenaLayout CreateLayout();
        DiceSpawnSettings GetSpawnSettings(PlayerSlot slot);
        PlayerAttackSettings GetAttackSettings(PlayerSlot slot);
        DiceCatalog GetDiceCatalog(PlayerSlot slot);
        PlayerNaturalSendSettings GetNaturalSendSettings(PlayerSlot slot);
        bool TryValidate(out string errorMessage);
    }
}
