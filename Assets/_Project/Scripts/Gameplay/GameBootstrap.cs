using System;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Grid;
using DiceGame.Placement;
using UnityEngine;

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
        [SerializeField] GameObject diceEntityPrefab;
        [SerializeField] GameObject characterPrefab;
        [SerializeField] int randomSeed;
        [SerializeField] PhysicsSettings physicsSettings;
        [SerializeField] CharacterMovementSettings characterMovementSettings;
        [SerializeField] PlayerInputSettings playerInputSettings;
        [SerializeField] DiceAnimationSettings diceAnimationSettings;
        [SerializeField] DiceDissolveSettings diceDissolveSettings;
        [SerializeField] DiceOneVanishSettings diceOneVanishSettings;
        [SerializeField] DiceSpawnSettings diceSpawnSettings;
        [SerializeField] DiceCatalog diceCatalog;
        [SerializeField] CameraSetupSettings cameraSetup = new();

        DiceRegistry registry;
        PlayerMatchActionContext matchActionContext;
        PlacementService placement;
        DiceSpawnSystem spawnSystem;
        System.Random spawnRandom;
        readonly List<CharacterController> characters = new();

        void Start() {
            if (board == null) {
                Debug.LogError("GameBootstrap: Board is not assigned.");
                return;
            }

            if (diceEntityPrefab == null) {
                Debug.LogError("GameBootstrap: DiceEntity prefab is not assigned.");
                return;
            }

            if (characterPrefab == null) {
                Debug.LogError("GameBootstrap: Character prefab is not assigned.");
                return;
            }

            if (physicsSettings == null
                || characterMovementSettings == null
                || playerInputSettings == null
                || diceAnimationSettings == null
                || diceDissolveSettings == null
                || diceOneVanishSettings == null
                || diceSpawnSettings == null
                || diceCatalog == null) {
                Debug.LogError("GameBootstrap: Gameplay settings assets are not assigned.");
                return;
            }

            if (!playerInputSettings.TryValidateStartup(out var inputError)) {
                Debug.LogError($"GameBootstrap: {inputError}");
                return;
            }

            registry = GetComponent<DiceRegistry>();
            if (registry == null) {
                registry = gameObject.AddComponent<DiceRegistry>();
            }

            registry.Configure(board);
            matchActionContext = GetComponent<PlayerMatchActionContext>();
            if (matchActionContext == null) {
                matchActionContext = gameObject.AddComponent<PlayerMatchActionContext>();
            }

            matchActionContext.Configure(registry);

            placement = new PlacementService(
                registry,
                board,
                new HeightStepLimits(
                    characterMovementSettings.MaxWalkStep,
                    characterMovementSettings.MaxJumpStep));
            spawnRandom = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();

            spawnSystem = GetComponent<DiceSpawnSystem>();
            if (spawnSystem == null) {
                spawnSystem = gameObject.AddComponent<DiceSpawnSystem>();
            }

            spawnSystem.Configure(
                board,
                registry,
                diceEntityPrefab,
                diceCatalog,
                transform,
                physicsSettings,
                diceAnimationSettings,
                diceDissolveSettings,
                matchActionContext,
                diceSpawnSettings,
                spawnRandom);

            var playerCount = playerInputSettings.ActivePlayerCount;
            var startDice = spawnSystem.SpawnInitialPlayerDice(playerCount);
            if (startDice.Count < playerCount) {
                Debug.LogError($"GameBootstrap: Failed to spawn initial dice for {playerCount} player(s).");
                return;
            }

            if (!TrySpawnPlayers(startDice, out var spawnedCharacters)) {
                return;
            }

            characters.Clear();
            characters.AddRange(spawnedCharacters);

            var oneVanishSystem = GetComponent<DiceOneVanishSystem>();
            if (oneVanishSystem == null) {
                oneVanishSystem = gameObject.AddComponent<DiceOneVanishSystem>();
            }

            oneVanishSystem.Configure(board, registry, characters, diceOneVanishSettings);

            var dissolveSystem = GetComponent<DiceMatchDissolveSystem>();
            if (dissolveSystem == null) {
                dissolveSystem = gameObject.AddComponent<DiceMatchDissolveSystem>();
            }

            dissolveSystem.Configure(board, registry, characters, matchActionContext, oneVanishSystem);

            if (cameraSetup.Enabled) {
                cameraSetup.Apply(board);
            }

            spawnSystem.StartSpawning();
        }

        bool TrySpawnPlayers(IReadOnlyList<DiceController> startDice, out List<CharacterController> spawnedCharacters) {
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

                characterController.Configure(
                    board,
                    placement,
                    startDice[i],
                    characterMovementSettings,
                    physicsSettings,
                    slot,
                    playerInputSettings,
                    matchActionContext);
                spawnedCharacters.Add(characterController);
            }

            return true;
        }

        public void ApplyCameraSetup() {
            cameraSetup.Apply(board);
        }
    }
}
