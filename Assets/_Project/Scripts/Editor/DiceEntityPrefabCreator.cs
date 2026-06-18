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

            CreateOrLoadCharacterPrefab();

            var root = new GameObject("DiceEntity");
            var positionRoot = new GameObject("PositionRoot");
            var rotationRoot = new GameObject("RotationRoot");
            var dissolvePivot = new GameObject("DissolvePivot");

            positionRoot.transform.SetParent(root.transform, false);
            rotationRoot.transform.SetParent(positionRoot.transform, false);
            dissolvePivot.transform.SetParent(rotationRoot.transform, false);

            var diceView = root.AddComponent<DiceView>();
            root.AddComponent<DiceController>();

            var serializedView = new SerializedObject(diceView);
            serializedView.FindProperty("diceMeshPrefab").objectReferenceValue = diceVisualPrefab;
            serializedView.FindProperty("positionRoot").objectReferenceValue = positionRoot.transform;
            serializedView.FindProperty("rotationRoot").objectReferenceValue = rotationRoot.transform;
            serializedView.FindProperty("dissolvePivot").objectReferenceValue = dissolvePivot.transform;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

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
            var characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            if (diceEntityPrefab == null) {
                Debug.LogError("Failed to load DiceEntity prefab.");
                return;
            }

            if (characterPrefab == null) {
                Debug.LogError("Failed to load Character prefab.");
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
            serializedBootstrap.FindProperty("characterPrefab").objectReferenceValue = characterPrefab;
            serializedBootstrap.FindProperty("diceCount").intValue = 3;
            serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();

            bootstrap.ApplyCameraSetup();

            EditorSceneManagerMarkDirty();
            Debug.Log("Game scene setup complete. Save the scene and press Play.");
        }

        static GameObject CreateOrLoadCharacterPrefab() {
            EnsurePrefabFolder();

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            if (existing != null) {
                var existingRoot = PrefabUtility.LoadPrefabContents(CharacterPrefabPath);
                if (existingRoot.GetComponent<DiceGame.Gameplay.CharacterController>() == null) {
                    existingRoot.AddComponent<DiceGame.Gameplay.CharacterController>();
                    PrefabUtility.SaveAsPrefabAsset(existingRoot, CharacterPrefabPath);
                }

                PrefabUtility.UnloadPrefabContents(existingRoot);
                return AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            }

            var character = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            character.name = "Character";
            character.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            Object.DestroyImmediate(character.GetComponent<Collider>());
            character.AddComponent<DiceGame.Gameplay.CharacterController>();

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
