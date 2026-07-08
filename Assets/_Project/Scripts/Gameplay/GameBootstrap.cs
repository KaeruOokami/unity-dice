using System;
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
        [SerializeField] DiceAnimationSettings diceAnimationSettings;
        [SerializeField] DiceDissolveSettings diceDissolveSettings;
        [SerializeField] DiceSpawnSettings diceSpawnSettings;
        [SerializeField] DiceCatalog diceCatalog;
        [SerializeField] CameraSetupSettings cameraSetup = new();

        DiceRegistry registry;
        PlacementService placement;
        DiceSpawnSystem spawnSystem;
        System.Random spawnRandom;

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
                || diceAnimationSettings == null
                || diceDissolveSettings == null
                || diceSpawnSettings == null
                || diceCatalog == null) {
                Debug.LogError("GameBootstrap: Gameplay settings assets are not assigned.");
                return;
            }

            registry = GetComponent<DiceRegistry>();
            if (registry == null) {
                registry = gameObject.AddComponent<DiceRegistry>();
            }

            registry.Configure(board);
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
                diceSpawnSettings,
                spawnRandom);

            var firstDice = spawnSystem.SpawnInitialDice();
            if (firstDice == null) {
                Debug.LogError("GameBootstrap: Failed to spawn any dice.");
                return;
            }

            var characterObject = Instantiate(characterPrefab, transform);
            characterObject.name = "Character";

            var characterController = characterObject.GetComponent<CharacterController>();
            if (characterController == null) {
                Debug.LogError("GameBootstrap: Character prefab must have CharacterController.");
                Destroy(characterObject);
                return;
            }

            characterController.Configure(
                board,
                placement,
                firstDice,
                characterMovementSettings,
                physicsSettings);

            var dissolveSystem = GetComponent<DiceMatchDissolveSystem>();
            if (dissolveSystem == null) {
                dissolveSystem = gameObject.AddComponent<DiceMatchDissolveSystem>();
            }

            dissolveSystem.Configure(board, registry, characterController);

            if (cameraSetup.Enabled) {
                cameraSetup.Apply(board);
            }

            spawnSystem.StartSpawning();
        }

        public void ApplyCameraSetup() {
            cameraSetup.Apply(board);
        }
    }
}
