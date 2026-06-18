using DiceGame.Grid;
using DiceGame.Gameplay;
using DiceGame.View;
using UnityEditor;
using UnityEngine;

namespace DiceGame.Editor
{
    public static class DiceEntityPrefabCreator
    {
        const string DiceVisualPrefabPath = "Assets/Packages/Dice_6/Prefabs/Dice_d6_Plastic Glossy Pure blue.prefab";
        const string DiceEntityPrefabPath = "Assets/_Project/Prefabs/DiceEntity.prefab";
        const string CharacterPrefabPath = "Assets/_Project/Prefabs/Character.prefab";

        [MenuItem("Dice/Create DiceEntity Prefab")]
        public static void CreateDiceEntityPrefab() {
            var diceVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DiceVisualPrefabPath);
            if (diceVisualPrefab == null) {
                Debug.LogError($"Dice visual prefab not found at {DiceVisualPrefabPath}");
                return;
            }

            var characterPrefab = CreateOrLoadCharacterPrefab();

            var root = new GameObject("DiceEntity");
            var diceView = root.AddComponent<DiceView>();
            root.AddComponent<DiceController>();
            var characterController = root.AddComponent<DiceGame.Gameplay.CharacterController>();

            var serializedView = new SerializedObject(diceView);
            serializedView.FindProperty("diceMeshPrefab").objectReferenceValue = diceVisualPrefab;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            var serializedCharacter = new SerializedObject(characterController);
            serializedCharacter.FindProperty("characterObject").objectReferenceValue = characterPrefab;
            serializedCharacter.ApplyModifiedPropertiesWithoutUndo();

            EnsurePrefabFolder();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, DiceEntityPrefabPath);
            Object.DestroyImmediate(root);

            Selection.activeObject = prefab;
            Debug.Log($"Created {DiceEntityPrefabPath}");
        }

        [MenuItem("Dice/Setup Game Scene")]
        public static void SetupGameScene() {
            CreateDiceEntityPrefab();

            var diceEntityPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DiceEntityPrefabPath);
            if (diceEntityPrefab == null) {
                Debug.LogError("Failed to load DiceEntity prefab.");
                return;
            }

            var boardObject = GameObject.Find("Board");
            if (boardObject == null) {
                boardObject = new GameObject("Board");
            }

            var board = boardObject.GetComponent<Board>();
            if (board == null) {
                board = boardObject.AddComponent<Board>();
            }

            var boardView = boardObject.GetComponent<BoardView>();
            if (boardView == null) {
                boardView = boardObject.AddComponent<BoardView>();
            }

            var bootstrapObject = GameObject.Find("GameBootstrap");
            if (bootstrapObject == null) {
                bootstrapObject = new GameObject("GameBootstrap");
            }

            var bootstrap = bootstrapObject.GetComponent<GameBootstrap>();
            if (bootstrap == null) {
                bootstrap = bootstrapObject.AddComponent<GameBootstrap>();
            }

            var serializedBootstrap = new SerializedObject(bootstrap);
            serializedBootstrap.FindProperty("board").objectReferenceValue = board;
            serializedBootstrap.FindProperty("diceEntityPrefab").objectReferenceValue = diceEntityPrefab;
            serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();

            var camera = Camera.main;
            if (camera != null) {
                var center = board.GridToWorld(new Vector2Int(board.Width / 2, board.Height / 2));
                var distance = board.CellSize * Mathf.Max(board.Width, board.Height);
                camera.transform.position = center + new Vector3(-distance * 0.6f, distance * 0.75f, -distance * 0.6f);
                camera.transform.LookAt(center);
            }

            EditorSceneManagerMarkDirty();
            Debug.Log("Game scene setup complete. Save the scene and press Play.");
        }

        static GameObject CreateOrLoadCharacterPrefab() {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            if (existing != null) {
                return existing;
            }

            EnsurePrefabFolder();
            var character = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            character.name = "Character";
            character.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            Object.DestroyImmediate(character.GetComponent<Collider>());

            var prefab = PrefabUtility.SaveAsPrefabAsset(character, CharacterPrefabPath);
            Object.DestroyImmediate(character);
            return prefab;
        }

        static void EnsurePrefabFolder() {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs")) {
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            }
        }

        static void EditorSceneManagerMarkDirty() {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }
    }
}
