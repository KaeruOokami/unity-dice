using DiceGame.Config;
using DiceGame.Core;
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
        const string NormalDicePrefabPath = "Assets/_Project/Prefabs/Normal Dice.prefab";
        const string DiceEntityPrefabPath = "Assets/_Project/Prefabs/DiceEntity.prefab";
        const string CharacterPrefabPath = "Assets/_Project/Prefabs/Character.prefab";
        const string DiceSpawnSettingsPath = "Assets/_Project/Settings/Gameplay/DiceSpawnSettings.asset";
        const string DiceCatalogPath = "Assets/_Project/Settings/Gameplay/DiceCatalog.asset";

        const string WoodDicePrefabPath = "Assets/_Project/Prefabs/Wood Dice.prefab";
        const string IronDicePrefabPath = "Assets/_Project/Prefabs/Iron Dice.prefab";
        const string MagnetDicePrefabPath = "Assets/_Project/Prefabs/Magnet Dice.prefab";
        const string IceDicePrefabPath = "Assets/_Project/Prefabs/Ice Dice.prefab";
        const string StoneDicePrefabPath = "Assets/_Project/Prefabs/Stone Dice.prefab";

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
            var diceController = root.AddComponent<DiceController>();

            var pushCollider = new GameObject("PushCollider");
            pushCollider.transform.SetParent(positionRoot.transform, false);
            var pushColliderBox = pushCollider.AddComponent<BoxCollider>();
            pushColliderBox.isTrigger = true;
            var pushBody = pushCollider.AddComponent<DicePushBody>();

            var serializedPush = new SerializedObject(pushBody);
            serializedPush.FindProperty("dice").objectReferenceValue = diceController;
            serializedPush.ApplyModifiedPropertiesWithoutUndo();

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

        [MenuItem("Dice/Create Dice Catalog Asset")]
        public static void CreateDiceCatalogAsset() {
            EnsureSettingsFolder();
            var existing = AssetDatabase.LoadAssetAtPath<DiceCatalog>(DiceCatalogPath);
            if (existing != null) {
                Selection.activeObject = existing;
                Debug.Log($"DiceCatalog already exists at {DiceCatalogPath}");
                return;
            }

            var catalog = ScriptableObject.CreateInstance<DiceCatalog>();
            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("entries");
            entries.arraySize = 6;
            SetCatalogEntry(entries, 0, DiceKind.Normal, NormalDicePrefabPath, 5f);
            SetCatalogEntry(entries, 1, DiceKind.Wood, WoodDicePrefabPath, 2f);
            SetCatalogEntry(entries, 2, DiceKind.Iron, IronDicePrefabPath, 1f);
            SetCatalogEntry(entries, 3, DiceKind.Magnet, MagnetDicePrefabPath, 1f);
            SetCatalogEntry(entries, 4, DiceKind.Ice, IceDicePrefabPath, 1.5f);
            SetCatalogEntry(entries, 5, DiceKind.Stone, StoneDicePrefabPath, 1.5f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(catalog, DiceCatalogPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = catalog;
            Debug.Log($"Created {DiceCatalogPath}");
        }

        static void SetCatalogEntry(
            SerializedProperty entries,
            int index,
            DiceKind kind,
            string prefabPath,
            float spawnWeight) {
            var entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("Kind").enumValueIndex = (int)kind;
            entry.FindPropertyRelative("MeshPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            entry.FindPropertyRelative("SpawnWeight").floatValue = spawnWeight;
        }

        [MenuItem("Dice/Setup Game Scene")]
        public static void SetupGameScene() {
            CreateDiceEntityPrefab();
            CreateDiceCatalogAsset();

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
            serializedBootstrap.FindProperty("diceSpawnSettings").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<DiceGame.Config.DiceSpawnSettings>(DiceSpawnSettingsPath);
            serializedBootstrap.FindProperty("diceCatalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<DiceCatalog>(DiceCatalogPath);
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
                }

                EnsureCharacterPushCollider(existingRoot);
                PrefabUtility.SaveAsPrefabAsset(existingRoot, CharacterPrefabPath);
                PrefabUtility.UnloadPrefabContents(existingRoot);
                return AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            }

            var character = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            character.name = "Character";
            character.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            var primitiveCollider = character.GetComponent<Collider>();
            if (primitiveCollider != null) {
                Object.DestroyImmediate(primitiveCollider);
            }

            character.AddComponent<DiceGame.Gameplay.CharacterController>();
            EnsureCharacterPushCollider(character);

            var prefab = PrefabUtility.SaveAsPrefabAsset(character, CharacterPrefabPath);
            Object.DestroyImmediate(character);
            return prefab;
        }

        static void EnsureCharacterPushCollider(GameObject character) {
            var capsule = character.GetComponent<CapsuleCollider>();
            if (capsule == null) {
                capsule = character.AddComponent<CapsuleCollider>();
            }

            capsule.isTrigger = true;
            capsule.radius = 0.25f;
            capsule.height = 0.6f;
            capsule.center = new Vector3(0f, 0.35f, 0f);
        }

        static void EnsurePrefabFolder() {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs")) {
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            }
        }

        static void EnsureSettingsFolder() {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Settings")) {
                AssetDatabase.CreateFolder("Assets/_Project", "Settings");
            }

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Settings/Gameplay")) {
                AssetDatabase.CreateFolder("Assets/_Project/Settings", "Gameplay");
            }
        }

        static void EditorSceneManagerMarkDirty() {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }
    }
}
