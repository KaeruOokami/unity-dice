using System;
using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "ChallengeModeSettings", menuName = "Dice/Challenge Mode Settings")]
    public sealed class ChallengeModeSettings : ScriptableObject
    {
        [Header("Fixed Match")]
        [SerializeField] BoardSettings boardSettings;

        [Header("Opponent Gauntlet")]
        [Tooltip("Index 0 = match 1, index 1 = match 2, and so on.")]
        [SerializeField] PlayerAttackSettings[] opponentAttacksByMatch = Array.Empty<PlayerAttackSettings>();

        [Header("Player Attack Choices")]
        [Tooltip("When null, MatchSetupPresetRegistry.AttackDefaultPresetCatalog is used.")]
        [SerializeField] AttackDefaultPresetCatalog playerAttackCatalog;

        public BoardSettings BoardSettings => boardSettings;
        public PlayerAttackSettings[] OpponentAttacksByMatch =>
            opponentAttacksByMatch ?? Array.Empty<PlayerAttackSettings>();
        public AttackDefaultPresetCatalog PlayerAttackCatalog => playerAttackCatalog;

        public int MatchCount {
            get {
                var count = 0;
                var source = OpponentAttacksByMatch;
                for (var i = 0; i < source.Length; i++) {
                    if (source[i] != null) {
                        count++;
                    }
                }

                return count;
            }
        }

        public AttackDefaultPresetCatalog ResolvePlayerAttackCatalog(MatchSetupPresetRegistry registry) {
            if (playerAttackCatalog != null) {
                return playerAttackCatalog;
            }

            return registry != null ? registry.AttackDefaultPresetCatalog : null;
        }

        public bool TryGetOpponentAttack(int matchIndex, out PlayerAttackSettings attack) {
            attack = null;
            var source = OpponentAttacksByMatch;
            var resolvedIndex = 0;
            for (var i = 0; i < source.Length; i++) {
                if (source[i] == null) {
                    continue;
                }

                if (resolvedIndex == matchIndex) {
                    attack = source[i];
                    return true;
                }

                resolvedIndex++;
            }

            return false;
        }

        public bool TryValidate(out string errorMessage) {
            if (boardSettings == null) {
                errorMessage = "ChallengeModeSettings: boardSettings is not assigned.";
                return false;
            }

            if (!boardSettings.TryValidate(out errorMessage)) {
                return false;
            }

            if (MatchCount < 1) {
                errorMessage = "ChallengeModeSettings: opponentAttacksByMatch must contain at least one entry.";
                return false;
            }

            var source = OpponentAttacksByMatch;
            for (var i = 0; i < source.Length; i++) {
                var attack = source[i];
                if (attack == null) {
                    continue;
                }

                if (!attack.TryValidate(out errorMessage)) {
                    errorMessage = $"ChallengeModeSettings: opponentAttacksByMatch[{i}] is invalid. {errorMessage}";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }

        public bool TryCreateSnapshot(
            MatchSetupPresetRegistry registry,
            PlayerAttackSettings playerAttack,
            int matchIndex,
            out MatchSetupSnapshot snapshot,
            out string errorMessage) {
            snapshot = null;
            if (registry == null) {
                errorMessage = "ChallengeModeSettings: Preset registry is not assigned.";
                return false;
            }

            if (!TryValidate(out errorMessage)) {
                return false;
            }

            if (playerAttack == null) {
                errorMessage = "ChallengeModeSettings: Player attack preset is not assigned.";
                return false;
            }

            if (!playerAttack.TryValidate(out errorMessage)) {
                return false;
            }

            if (!TryGetOpponentAttack(matchIndex, out var opponentAttack)) {
                errorMessage =
                    $"ChallengeModeSettings: No opponent attack for match index {matchIndex}.";
                return false;
            }

            var player1Control = registry.GetControlDefaults(PlayerSlot.Player1);
            var player2Control = registry.GetControlDefaults(PlayerSlot.Player2);
            var board = boardSettings;
            var sharedInitialDiceCount = board.SharedInitialDiceCount;

            snapshot = new MatchSetupSnapshot {
                GameMode = GameMode.Challenge,
                WinsToWin = 1,
                SharedSpawn = DiceSpawnSettingsData.FromTemplate(board.Player1.SpawnSettings)
                    .WithInitialDiceCount(sharedInitialDiceCount),
                SharedCatalog = DiceCatalogData.FromTemplate(board.Player1.DiceCatalog),
                Jumbo = JumboDiceSettingsData.FromTemplate(board.JumboDiceSettings),
                Player1 = PlayerSlotSetup.CreateDefault(
                    isAi: false,
                    inputConfig: player1Control.InputConfig,
                    spawn: DiceSpawnSettingsData.FromTemplate(board.Player1.SpawnSettings)
                        .WithInitialDiceCount(sharedInitialDiceCount),
                    catalog: DiceCatalogData.FromTemplate(board.Player1.DiceCatalog),
                    attack: PlayerAttackSettingsData.FromTemplate(playerAttack),
                    naturalSend: PlayerNaturalSendSettingsData.FromTemplate(board.Player1.NaturalSendSettings)),
                Player2 = PlayerSlotSetup.CreateDefault(
                    isAi: true,
                    inputConfig: player2Control.InputConfig,
                    spawn: DiceSpawnSettingsData.FromTemplate(board.Player2.SpawnSettings)
                        .WithInitialDiceCount(sharedInitialDiceCount),
                    catalog: DiceCatalogData.FromTemplate(board.Player2.DiceCatalog),
                    attack: PlayerAttackSettingsData.FromTemplate(opponentAttack),
                    naturalSend: PlayerNaturalSendSettingsData.FromTemplate(board.Player2.NaturalSendSettings))
            };
            snapshot.NormalizeVersusSharedInitialDiceCount();
            snapshot.NormalizeWinsToWin();

            if (!snapshot.TryValidate(registry, out errorMessage)) {
                snapshot = null;
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
