using System;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Gameplay.AI.Application;
using DiceGame.Gameplay.Input;
using DiceGame.Grid;
using DiceGame.Placement;
using DiceGame.Session;
using DiceGame.Session.Network;
using DiceGame.View;
using DiceGame.Versus;
using UnityEngine;
using UnityEngine.Serialization;

namespace DiceGame.Gameplay
{
    [Serializable]
    public class CameraSetupSettings
    {
        [SerializeField] bool enabled = true;
        [SerializeField] float distanceScale = 1f;
        [SerializeField] Vector3 offsetFactors = new(-0.6f, 0.75f, -0.6f);
        [SerializeField] bool lookAtBoardCenter = true;
        [SerializeField] Vector3 lookAtOffset;
        [SerializeField] bool overrideFieldOfView;
        [SerializeField] float fieldOfView = 60f;

        public bool Enabled => enabled;

        public void Apply(Board board) {
            if (board == null) {
                return;
            }

            var camera = Camera.main;
            if (camera == null) {
                return;
            }

            var center = board.GridToWorld(new Vector2Int(board.Width / 2, board.Height / 2));
            var distance = board.CellSize * Mathf.Max(board.Width, board.Height) * distanceScale;
            camera.transform.position = center + Vector3.Scale(offsetFactors, Vector3.one * distance);

            if (lookAtBoardCenter) {
                camera.transform.LookAt(center + lookAtOffset);
            }

            if (overrideFieldOfView) {
                camera.fieldOfView = fieldOfView;
            }
        }
    }

    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] Board board;
        [SerializeField] BoardView boardView;
        [SerializeField] GameObject diceEntityPrefab;
        [SerializeField] GameObject characterPrefab;
        [SerializeField] int randomSeed;
        [SerializeField] GameSessionSettings gameSessionSettings;
        [SerializeField] PhysicsSettings physicsSettings;
        [SerializeField] CharacterMovementSettings characterMovementSettings;
        [SerializeField] PlayerInputSettings playerInputSettings;
        [SerializeField] DiceAnimationSettings diceAnimationSettings;
        [FormerlySerializedAs("diceDissolveSettings")]
        [SerializeField] DiceErasureSettings diceErasureSettings;
        [SerializeField] DiceOneVanishSettings diceOneVanishSettings;
        [SerializeField] DiceSpawnSettings diceSpawnSettings;
        [SerializeField] DiceCatalog diceCatalog;
        [SerializeField] AiPlayerSettings aiPlayerSettings;
        [SerializeField] CameraSetupSettings cameraSetup = new();

        DiceRegistry registry;
        PlayerMatchActionContext matchActionContext;
        DiceMatchOwnershipContext ownershipContext;
        PlacementService placement;
        DiceSpawnSystem spawnSystem;
        VersusAttackController attackController;
        System.Random spawnRandom;
        readonly List<CharacterController> characters = new();
        bool sessionStarted;
        ResolvedSessionSetup resolvedSetup;

        public Board Board => board;
        public GameObject DiceEntityPrefab => diceEntityPrefab;
        public GameObject CharacterPrefab => characterPrefab;
        public PlayerInputSettings PlayerInputSettings => playerInputSettings;
        public GameSessionSettings GameSessionSettings => gameSessionSettings;
        public bool IsSessionActive => sessionStarted;

        void OnDisable() {
            if (OnlineSessionState.Instance != null) {
                OnlineSessionState.Instance.MatchStartRequested -= OnOnlineMatchStartRequested;
            }
        }

        void Start() {
            var onlineController = FindObjectOfType<OnlineSessionController>();
            var session = OnlineSessionState.Instance;
            if (onlineController != null) {
                if (session == null) {
                    Debug.LogError("GameBootstrap: OnlineSessionController is present but OnlineSessionState is missing.");
                    return;
                }

                session.MatchStartRequested -= OnOnlineMatchStartRequested;
                session.MatchStartRequested += OnOnlineMatchStartRequested;

                if (session.PlayMode == OnlinePlayMode.Unspecified && !session.IsMatchRunning) {
                    return;
                }
            }

            BeginSession();
        }

        void OnOnlineMatchStartRequested() {
            if (sessionStarted) {
                return;
            }

            BeginSession();
        }

        void BeginSession() {
            if (sessionStarted) {
                return;
            }

            if (board == null) {
                Debug.LogError("GameBootstrap: Board is not assigned.");
                AbortPendingSessionStart();
                return;
            }

            GameWorldVisibility.SetBoardVisible(board, true);

            if (boardView == null) {
                boardView = board.GetComponent<BoardView>();
            }

            if (diceEntityPrefab == null) {
                Debug.LogError("GameBootstrap: DiceEntity prefab is not assigned.");
                AbortPendingSessionStart();
                return;
            }

            if (characterPrefab == null) {
                Debug.LogError("GameBootstrap: Character prefab must be assigned.");
                AbortPendingSessionStart();
                return;
            }

            if (gameSessionSettings == null
                || physicsSettings == null
                || characterMovementSettings == null
                || playerInputSettings == null
                || diceAnimationSettings == null
                || diceErasureSettings == null
                || diceOneVanishSettings == null) {
                Debug.LogError("GameBootstrap: Gameplay settings assets are not assigned.");
                AbortPendingSessionStart();
                return;
            }

            var session = OnlineSessionState.Instance;
            resolvedSetup = ResolvedSessionSetup.Resolve(
                gameSessionSettings,
                diceSpawnSettings,
                diceCatalog,
                FindPresetRegistry(),
                playerInputSettings,
                session?.CurrentSetup);

            if (resolvedSetup.GameMode == GameMode.Versus) {
                if (gameSessionSettings.VersusBoardSettings == null) {
                    Debug.LogError("GameBootstrap: VersusBoardSettings template is not assigned.");
                    AbortPendingSessionStart();
                    return;
                }

                if (resolvedSetup.VersusBoardSettings == null) {
                    Debug.LogError("GameBootstrap: Versus board settings are invalid.");
                    AbortPendingSessionStart();
                    return;
                }

                if (!resolvedSetup.VersusBoardSettings.TryValidate(out var versusError)) {
                    Debug.LogError($"GameBootstrap: {versusError}");
                    AbortPendingSessionStart();
                    return;
                }
            } else if (resolvedSetup.SharedSpawnSettings == null || resolvedSetup.SharedDiceCatalog == null) {
                Debug.LogError("GameBootstrap: Shared DiceSpawnSettings and DiceCatalog are required for Single/Coop.");
                AbortPendingSessionStart();
                return;
            }

            if (session?.CurrentSetup != null) {
                var registry = FindPresetRegistry();
                if (registry == null) {
                    Debug.LogError("GameBootstrap: MatchSetupPresetRegistry is not assigned.");
                    AbortPendingSessionStart();
                    return;
                }

                if (!session.CurrentSetup.TryValidate(registry, out var setupError)) {
                    Debug.LogError($"GameBootstrap: {setupError}");
                    AbortPendingSessionStart();
                    return;
                }
            } else if (!gameSessionSettings.TryValidate(out var sessionError)) {
                Debug.LogError($"GameBootstrap: {sessionError}");
                AbortPendingSessionStart();
                return;
            }

            var validationPlayerCount = session != null && session.IsOnline
                ? 1
                : resolvedSetup.RequiredPlayerCount;
            if (!playerInputSettings.TryValidateStartup(
                validationPlayerCount,
                resolvedSetup.Player1IsAi,
                resolvedSetup.Player2IsAi,
                resolvedSetup.Player1Input,
                resolvedSetup.Player2Input,
                out var inputError)) {
                Debug.LogError($"GameBootstrap: {inputError}");
                AbortPendingSessionStart();
                return;
            }

            if (!TryConfigureBoardForSession(out var boardError)) {
                Debug.LogError($"GameBootstrap: {boardError}");
                AbortPendingSessionStart();
                return;
            }

            if (cameraSetup.Enabled) {
                cameraSetup.Apply(board);
            }

            // Full-sim online experiment: both host and client run local simulation.
            // (Presentation-only OnlineClientMatchView path is intentionally skipped.)

            registry = GetComponent<DiceRegistry>();
            if (registry == null) {
                registry = gameObject.AddComponent<DiceRegistry>();
            }

            registry.Configure(board);
            matchActionContext = GetComponent<PlayerMatchActionContext>();
            if (matchActionContext == null) {
                matchActionContext = gameObject.AddComponent<PlayerMatchActionContext>();
            }

            ownershipContext = GetComponent<DiceMatchOwnershipContext>();
            if (ownershipContext == null) {
                ownershipContext = gameObject.AddComponent<DiceMatchOwnershipContext>();
            }

            matchActionContext.Configure(registry, ownershipContext);

            placement = new PlacementService(
                registry,
                board,
                new HeightStepLimits(
                    characterMovementSettings.MaxWalkStep,
                    characterMovementSettings.MaxJumpStepPlayerOnly,
                    characterMovementSettings.MaxJumpStepCoupled));
            spawnRandom = ResolveMatchRandom(out var matchSeed);
            UnityEngine.Random.InitState(matchSeed);
            if (session != null && session.IsOnline) {
                Debug.Log(
                    $"GameBootstrap: online match seed={matchSeed} " +
                    $"role={(session.IsHost ? "Host" : "Client")} " +
                    $"(initial board = local generate from seed; later spawns = host results)");
            }

            spawnSystem = GetComponent<DiceSpawnSystem>();
            if (spawnSystem == null) {
                spawnSystem = gameObject.AddComponent<DiceSpawnSystem>();
            }

            if (!TryConfigureSpawnSystem(out var spawnError)) {
                Debug.LogError($"GameBootstrap: {spawnError}");
                AbortPendingSessionStart();
                return;
            }

            spawnSystem.ConfigureOwnership(ownershipContext);
            // Hybrid: initial board is seed-local on both peers (do not emit spawn commands yet).
            spawnSystem.EmitNetworkSpawns = false;

            var playerCount = resolvedSetup.RequiredPlayerCount;
            var startDice = spawnSystem.SpawnInitialPlayerDice(playerCount);
            if (startDice.Count < playerCount) {
                Debug.LogError($"GameBootstrap: Failed to spawn initial dice for {playerCount} player(s).");
                AbortPendingSessionStart();
                return;
            }

            for (var i = 0; i < startDice.Count; i++) {
                var slot = i == 0 ? PlayerSlot.Player1 : PlayerSlot.Player2;
                ownershipContext.SetOwner(startDice[i], slot);
            }

            if (!TrySpawnPlayers(startDice, out var spawnedCharacters)) {
                AbortPendingSessionStart();
                return;
            }

            characters.Clear();
            characters.AddRange(spawnedCharacters);

            var oneVanishSystem = GetComponent<DiceOneVanishSystem>();
            if (oneVanishSystem == null) {
                oneVanishSystem = gameObject.AddComponent<DiceOneVanishSystem>();
            }

            oneVanishSystem.Configure(board, registry, characters, diceOneVanishSettings, diceErasureSettings);

            var erasureSystem = GetComponent<DiceMatchErasureSystem>();
            if (erasureSystem == null) {
                erasureSystem = gameObject.AddComponent<DiceMatchErasureSystem>();
            }

            erasureSystem.Configure(
                board,
                registry,
                characters,
                matchActionContext,
                oneVanishSystem,
                ownershipContext,
                diceErasureSettings);
            spawnSystem.ConfigureErasureSystem(erasureSystem);

            erasureSystem.ConfigureSinkingChain();

            attackController = null;
            if (resolvedSetup.GameMode == GameMode.Versus) {
                var versusSettings = resolvedSetup.VersusBoardSettings;
                erasureSystem.ConfigureVersusAttack(versusSettings);

                attackController = GetComponent<VersusAttackController>();
                if (attackController == null) {
                    attackController = gameObject.AddComponent<VersusAttackController>();
                }

                attackController.Configure(
                    versusSettings,
                    board,
                    spawnSystem,
                    erasureSystem,
                    spawnRandom,
                    transform);

                var jumboSequence = GetComponent<JumboDiceSequenceController>();
                if (jumboSequence == null) {
                    jumboSequence = gameObject.AddComponent<JumboDiceSequenceController>();
                }

                jumboSequence.Configure(
                    versusSettings,
                    board,
                    spawnSystem,
                    ownershipContext,
                    oneVanishSystem,
                    characters);

                // Client follows host jumbo spawns via OnlineDiceSpawnCommand.
                if (session != null && session.PlayMode == OnlinePlayMode.OnlineClient) {
                    jumboSequence.enabled = false;
                }
            }

            var gameFlowController = GetComponent<GameFlowController>();
            if (gameFlowController == null) {
                gameFlowController = gameObject.AddComponent<GameFlowController>();
            }

            gameFlowController.Configure(
                board,
                registry,
                spawnSystem,
                attackController,
                gameSessionSettings,
                playerInputSettings,
                resolvedSetup);

            for (var i = 0; i < characters.Count; i++) {
                characters[i].BindCrushOutcome(gameFlowController);
            }

            if (session != null && session.IsOnline) {
                // Hybrid online sync:
                // - Initial dice: already spawned above from shared MatchSeed (both peers).
                // - Continuous / attack / jumbo: host RNG + OnlineDiceSpawnCommand to client.
                BindOnlineFullSimSync(session.IsHost);
                if (session.IsHost) {
                    spawnSystem.EmitNetworkSpawns = true;
                    spawnSystem.StartSpawning();
                } else {
                    spawnSystem.EmitNetworkSpawns = false;
                    spawnSystem.AllowAutonomousSpawning = false;
                    attackController?.SetNetworkFollowerMode(true);
                    EnsureOnlineClientFlowAdapter();
                }
            } else {
                spawnSystem.StartSpawning();
            }

            sessionStarted = true;
        }

        System.Random ResolveMatchRandom(out int usedSeed) {
            var session = OnlineSessionState.Instance;
            if (session != null && session.IsOnline) {
                if (session.MatchSeed == 0) {
                    Debug.LogError(
                        "GameBootstrap: online MatchSeed is 0; peers will diverge. " +
                        "Ensure MatchStart trailing seed was applied before BeginSession.");
                }

                usedSeed = session.MatchSeed != 0 ? session.MatchSeed : 1;
                return new System.Random(usedSeed);
            }

            usedSeed = randomSeed != 0 ? randomSeed : Environment.TickCount;
            if (usedSeed == 0) {
                usedSeed = 1;
            }

            return new System.Random(usedSeed);
        }

        static void AbortPendingSessionStart() {
            OnlineSessionState.Instance?.ResetMatchFlag();
        }

        bool TryBeginOnlineClientPresentation() {
            var onlineController = FindObjectOfType<OnlineSessionController>();
            if (onlineController?.Messenger == null) {
                Debug.LogError("GameBootstrap: Online client matched without messenger.");
                return false;
            }

            GameWorldVisibility.SetBoardVisible(board, true);

            var clientView = GetComponent<OnlineClientMatchView>();
            if (clientView == null) {
                clientView = gameObject.AddComponent<OnlineClientMatchView>();
            }

            clientView.Configure(
                onlineController.Messenger,
                diceEntityPrefab,
                characterPrefab,
                playerInputSettings,
                physicsSettings,
                diceAnimationSettings,
                diceErasureSettings,
                diceOneVanishSettings,
                board,
                ResolveClientPresentationCatalog(out var alternateCatalog),
                alternateCatalog,
                ResolveClientAttackQueueUiSettings(),
                characterMovementSettings);

            var flowAdapter = GetComponent<OnlineClientFlowAdapter>();
            if (flowAdapter == null) {
                flowAdapter = gameObject.AddComponent<OnlineClientFlowAdapter>();
            }

            flowAdapter.Configure(onlineController.Messenger, playerInputSettings);
            return true;
        }

        DiceCatalog ResolveClientPresentationCatalog(out DiceCatalog alternateCatalog) {
            alternateCatalog = null;
            if (resolvedSetup == null) {
                return diceCatalog;
            }

            if (resolvedSetup.GameMode == GameMode.Versus
                && resolvedSetup.VersusBoardSettings != null) {
                var versus = resolvedSetup.VersusBoardSettings;
                alternateCatalog = versus.Player2.DiceCatalog;
                return versus.Player1.DiceCatalog != null
                    ? versus.Player1.DiceCatalog
                    : diceCatalog;
            }

            return resolvedSetup.SharedDiceCatalog != null
                ? resolvedSetup.SharedDiceCatalog
                : diceCatalog;
        }

        AttackQueueUiSettings ResolveClientAttackQueueUiSettings() {
            if (resolvedSetup?.GameMode == GameMode.Versus
                && resolvedSetup.VersusBoardSettings != null) {
                return resolvedSetup.VersusBoardSettings.AttackQueueUiSettings;
            }

            return null;
        }

        void EnsureOnlineClientFlowAdapter() {
            var onlineController = FindObjectOfType<OnlineSessionController>();
            if (onlineController?.Messenger == null) {
                Debug.LogError("GameBootstrap: Online client matched without messenger.");
                return;
            }

            var flowAdapter = GetComponent<OnlineClientFlowAdapter>();
            if (flowAdapter == null) {
                flowAdapter = gameObject.AddComponent<OnlineClientFlowAdapter>();
            }

            flowAdapter.Configure(onlineController.Messenger, playerInputSettings);
        }

        void BindOnlineFullSimSync(bool isHost) {
            var onlineController = FindObjectOfType<OnlineSessionController>();
            if (onlineController?.Messenger == null) {
                Debug.LogError("GameBootstrap: Online full-sim sync requires messenger.");
                return;
            }

            // Disable legacy presentation / snapshot binders if present on the same object.
            var legacyHostBinder = GetComponent<OnlineHostMatchBinder>();
            if (legacyHostBinder != null) {
                legacyHostBinder.enabled = false;
                Destroy(legacyHostBinder);
            }

            var legacyClientView = GetComponent<OnlineClientMatchView>();
            if (legacyClientView != null) {
                legacyClientView.enabled = false;
                Destroy(legacyClientView);
            }

            var binder = GetComponent<OnlineSimSyncBinder>();
            if (binder == null) {
                binder = gameObject.AddComponent<OnlineSimSyncBinder>();
            }

            binder.Configure(
                onlineController.Messenger,
                spawnSystem,
                ownershipContext,
                attackController,
                characters,
                isHost);
        }

        MatchSetupPresetRegistry FindPresetRegistry() {
            var onlineController = FindObjectOfType<OnlineSessionController>();
            return onlineController != null ? onlineController.MatchSetupPresetRegistry : null;
        }

        bool TryConfigureBoardForSession(out string errorMessage) {
            errorMessage = null;

            if (resolvedSetup.GameMode == GameMode.Versus) {
                var versusSettings = resolvedSetup.VersusBoardSettings;
                if (!versusSettings.TryValidate(out errorMessage)) {
                    return false;
                }

                board.ConfigureVersusArena(versusSettings.CreateLayout());
            } else {
                board.ConfigureStandardArena();
            }

            boardView?.RebuildFloor();
            return true;
        }

        bool TryConfigureSpawnSystem(out string errorMessage) {
            errorMessage = null;

            if (resolvedSetup.GameMode == GameMode.Versus) {
                var versusSettings = resolvedSetup.VersusBoardSettings;
                spawnSystem.Configure(
                    board,
                    registry,
                    diceEntityPrefab,
                    versusSettings.Player1.DiceCatalog,
                    transform,
                    physicsSettings,
                    diceAnimationSettings,
                    diceErasureSettings,
                    matchActionContext,
                    versusSettings.Player1.SpawnSettings,
                    spawnRandom);
                spawnSystem.ConfigureVersusSpawns(
                    versusSettings.Player1.SpawnSettings,
                    versusSettings.Player1.DiceCatalog,
                    versusSettings.Player2.SpawnSettings,
                    versusSettings.Player2.DiceCatalog,
                    versusSettings.InitialDicePlacementMode);
                return true;
            }

            if (resolvedSetup.SharedSpawnSettings == null) {
                errorMessage = "DiceSpawnSettings is not assigned for non-versus modes.";
                return false;
            }

            spawnSystem.Configure(
                board,
                registry,
                diceEntityPrefab,
                resolvedSetup.SharedDiceCatalog,
                transform,
                physicsSettings,
                diceAnimationSettings,
                diceErasureSettings,
                matchActionContext,
                resolvedSetup.SharedSpawnSettings,
                spawnRandom);
            return true;
        }

        bool TrySpawnPlayers(
            IReadOnlyList<DiceController> startDice,
            out List<CharacterController> spawnedCharacters) {
            spawnedCharacters = new List<CharacterController>(startDice.Count);

            for (var i = 0; i < startDice.Count; i++) {
                var slot = i == 0 ? PlayerSlot.Player1 : PlayerSlot.Player2;
                var characterObject = Instantiate(characterPrefab, transform);
                characterObject.name = slot == PlayerSlot.Player1 ? "Character_P1" : "Character_P2";

                var characterController = characterObject.GetComponent<CharacterController>();
                if (characterController == null) {
                    Debug.LogError("GameBootstrap: Character prefab must have CharacterController.");
                    foreach (var spawned in spawnedCharacters) {
                        if (spawned != null) {
                            Destroy(spawned.gameObject);
                        }
                    }

                    Destroy(characterObject);
                    spawnedCharacters.Clear();
                    return false;
                }

                var inputSettingsForSlot = playerInputSettings;
                var sessionForSpawn = OnlineSessionState.Instance;
                PlayerSlot? inputBindingSlot = null;
                if (sessionForSpawn != null && sessionForSpawn.IsOnline) {
                    // Only the local seat gets device input; the remote seat is driven by network.
                    if (slot != sessionForSpawn.LocalPlayerSlot) {
                        inputSettingsForSlot = null;
                    } else {
                        // Online local control always uses Player1 action map / keybinds (WASD etc.).
                        inputBindingSlot = PlayerSlot.Player1;
                    }
                }

                PlayerSlotInputConfig? inputOverride = null;
                if (inputSettingsForSlot != null && !resolvedSetup.IsAiControlled(slot)) {
                    inputOverride = inputBindingSlot.HasValue
                        ? resolvedSetup.GetInputConfig(inputBindingSlot.Value)
                        : resolvedSetup.GetInputConfig(slot);
                }

                characterController.Configure(
                    board,
                    placement,
                    startDice[i],
                    characterMovementSettings,
                    physicsSettings,
                    slot,
                    inputSettingsForSlot,
                    matchActionContext,
                    inputOverride,
                    inputBindingSlot);
                TryConfigureAiControl(characterObject, characterController, slot);
                spawnedCharacters.Add(characterController);
            }

            return true;
        }

        public void ApplyCameraSetup() {
            cameraSetup.Apply(board);
        }

        void TryConfigureAiControl(
            GameObject characterObject,
            CharacterController characterController,
            PlayerSlot slot) {
            var session = OnlineSessionState.Instance;
            if (session != null && session.IsOnline) {
                return;
            }

            if (!resolvedSetup.IsAiControlled(slot)) {
                return;
            }

            if (aiPlayerSettings == null) {
                Debug.LogError("GameBootstrap: AI control requested but AiPlayerSettings is not assigned.");
                return;
            }

            var humanReader = characterObject.GetComponent<CharacterInputReader>();
            if (humanReader != null) {
                humanReader.enabled = false;
            }

            var aiInput = characterObject.GetComponent<AiCharacterInputSource>();
            if (aiInput == null) {
                aiInput = characterObject.AddComponent<AiCharacterInputSource>();
            }

            var brain = characterObject.GetComponent<AiCharacterBrain>();
            if (brain == null) {
                brain = characterObject.AddComponent<AiCharacterBrain>();
            }

            characterController.SetInputSource(aiInput);
            brain.Configure(characterController, registry, aiInput, aiPlayerSettings);
        }
    }
}
