using System;
using System.Collections.Generic;
using DiceGame.Core;
using DiceGame.Grid;
using DiceGame.View;
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
        [SerializeField] int diceCount = 3;
        [SerializeField] int randomSeed;
        [SerializeField] CameraSetupSettings cameraSetup = new();

        DiceRegistry registry;

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

            registry = GetComponent<DiceRegistry>();
            if (registry == null) {
                registry = gameObject.AddComponent<DiceRegistry>();
            }

            registry.Configure(board);

            var positions = PickRandomDicePositions(diceCount);
            if (positions.Count == 0) {
                Debug.LogError("GameBootstrap: No valid positions for dice.");
                return;
            }

            DiceController firstDice = null;

            foreach (var gridPos in positions) {
                var diceEntity = Instantiate(diceEntityPrefab, transform);
                diceEntity.name = $"DiceEntity_{gridPos.x}_{gridPos.y}";

                var diceView = diceEntity.GetComponent<DiceView>();
                if (diceView == null) {
                    Debug.LogError("GameBootstrap: DiceEntity prefab must have DiceView.");
                    Destroy(diceEntity);
                    continue;
                }

                var diceController = diceEntity.GetComponent<DiceController>();
                if (diceController == null) {
                    Debug.LogError("GameBootstrap: DiceEntity prefab must have DiceController.");
                    Destroy(diceEntity);
                    continue;
                }

                var orientation = CreateRandomOrientation();
                diceController.Configure(board, diceView, registry, gridPos, orientation);
                firstDice ??= diceController;
            }

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

            characterController.Configure(board, registry, firstDice);

            var dissolveSystem = GetComponent<DiceMatchDissolveSystem>();
            if (dissolveSystem == null) {
                dissolveSystem = gameObject.AddComponent<DiceMatchDissolveSystem>();
            }

            dissolveSystem.Configure(board, registry, characterController);

            if (cameraSetup.Enabled) {
                cameraSetup.Apply(board);
            }
        }

        List<Vector2Int> PickRandomDicePositions(int count) {
            var cells = new List<Vector2Int>();
            for (var x = 0; x < board.Width; x++) {
                for (var z = 0; z < board.Height; z++) {
                    cells.Add(new Vector2Int(x, z));
                }
            }

            var random = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();
            for (var i = cells.Count - 1; i > 0; i--) {
                var j = random.Next(i + 1);
                (cells[i], cells[j]) = (cells[j], cells[i]);
            }

            var take = Mathf.Min(count, cells.Count);
            return cells.GetRange(0, take);
        }

        static DiceOrientation CreateRandomOrientation() {
            var orientation = DiceOrientation.Default;
            var directions = new[] { Direction.East, Direction.West, Direction.North, Direction.South };
            var steps = UnityEngine.Random.Range(0, 12);

            for (var i = 0; i < steps; i++) {
                orientation = orientation.Roll(directions[UnityEngine.Random.Range(0, directions.Length)]);
            }

            return orientation;
        }

        public void ApplyCameraSetup() {
            cameraSetup.Apply(board);
        }
    }
}
