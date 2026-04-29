using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OmokTrollTestIntegrator
{
    private const string TargetScenePath = "Assets/Scenes/TrollTest.unity";
    private const string SourceScenePath = "Assets/Scripts/Client/_Yeongwoong/omok_pan.unity";
    private const string IntegrationRootName = "BoardSystem";
    private const string LegacyIntegrationRootName = "Omok_Troll_Integration";
    private const string WorldRootName = "OmokPlayArea";
    private const float WorldScale = 0.1f;

    [MenuItem("Tools/Omok/Integrate Into TrollTest")]
    public static void IntegrateIntoTrollTest()
    {
        Scene targetScene = SceneManager.GetSceneByPath(TargetScenePath);
        if (!targetScene.IsValid() || !targetScene.isLoaded)
        {
            targetScene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        }

        RemoveExistingIntegrationRoot(targetScene);

        Scene sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);
        GameObject sourceRoot = BuildSourceRoot(sourceScene);
        GameObject clone = Object.Instantiate(sourceRoot);
        clone.name = IntegrationRootName;
        SceneManager.MoveGameObjectToScene(clone, targetScene);

        ConfigureClone(clone, targetScene);
        EditorSceneManager.CloseScene(sourceScene, true);
        EditorSceneManager.MarkSceneDirty(targetScene);
        EditorSceneManager.SaveScene(targetScene);

        Debug.Log($"Integrated Omok objects into {TargetScenePath}.");
    }

    private static GameObject BuildSourceRoot(Scene sourceScene)
    {
        GameObject sourceRoot = new GameObject(IntegrationRootName);
        SceneManager.MoveGameObjectToScene(sourceRoot, sourceScene);

        string[] rootNames =
        {
            "Omok_Board",
            "tong_black",
            "tone_white",
            "Cube",
            "OmokMatchManager",
            "Canvas"
        };

        foreach (string rootName in rootNames)
        {
            GameObject root = FindRootObject(sourceScene, rootName);
            if (root == null)
            {
                Debug.LogWarning($"Could not find source root object: {rootName}");
                continue;
            }

            root.transform.SetParent(sourceRoot.transform, true);
        }

        return sourceRoot;
    }

    private static void ConfigureClone(GameObject clone, Scene targetScene)
    {
        clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        clone.transform.localScale = Vector3.one;

        Transform worldRoot = CreateWorldRoot(clone.transform);
        MoveChildToParent(clone.transform, "Omok_Board", worldRoot);
        MoveChildToParent(clone.transform, "tong_black", worldRoot);
        MoveChildToParent(clone.transform, "tone_white", worldRoot);
        MoveChildToParent(clone.transform, "Cube", worldRoot);

        worldRoot.SetLocalPositionAndRotation(new Vector3(0f, 0.05f, 0f), Quaternion.identity);
        worldRoot.localScale = Vector3.one * WorldScale;

        int boardLayer = LayerMask.NameToLayer("Board");
        int blockerLayer = LayerMask.NameToLayer("Blocker");

        GameObject boardRoot = FindChildByName(clone.transform, "Omok_Board");
        if (boardRoot != null && boardLayer >= 0)
        {
            SetLayerRecursively(boardRoot, boardLayer);
        }

        GameObject blockerRoot = FindChildByName(clone.transform, "Cube");
        if (blockerRoot != null && blockerLayer >= 0)
        {
            SetLayerRecursively(blockerRoot, blockerLayer);
        }

        Camera mainCamera = FindMainCamera(targetScene);
        OmokStoneDropper stoneDropper = clone.GetComponentInChildren<OmokStoneDropper>(true);
        if (stoneDropper != null)
        {
            SerializedObject serializedDropper = new SerializedObject(stoneDropper);
            serializedDropper.FindProperty("targetCamera").objectReferenceValue = mainCamera;
            serializedDropper.FindProperty("stoneRoot").objectReferenceValue = worldRoot;
            serializedDropper.FindProperty("useBuiltInMouseInput").boolValue = true;
            ScaleFloat(serializedDropper, "dragHoverHeight", WorldScale);
            ScaleFloat(serializedDropper, "raycastDistance", WorldScale);
            ScaleFloat(serializedDropper, "settleOffset", WorldScale);
            ScaleFloat(serializedDropper, "blockerCenterStackGap", WorldScale);
            ScaleFloat(serializedDropper, "initialFallSpeed", WorldScale);
            ScaleFloat(serializedDropper, "fallGravityScale", WorldScale);
            ScaleFloat(serializedDropper, "previewLineWidth", WorldScale);
            ScaleFloat(serializedDropper, "previewTargetHeightOffset", WorldScale);
            serializedDropper.ApplyModifiedPropertiesWithoutUndo();
        }

        OmokAimController aimController = clone.GetComponentInChildren<OmokAimController>(true);
        if (aimController != null)
        {
            SerializedObject serializedAim = new SerializedObject(aimController);
            serializedAim.FindProperty("targetCamera").objectReferenceValue = mainCamera;
            serializedAim.ApplyModifiedPropertiesWithoutUndo();
        }

        OmokMatchManager matchManager = clone.GetComponentInChildren<OmokMatchManager>(true);
        if (matchManager != null)
        {
            SerializedObject serializedMatch = new SerializedObject(matchManager);
            GameObject resultOverlayRoot = FindChildByName(clone.transform, "Panel");
            serializedMatch.FindProperty("resultOverlayRoot").objectReferenceValue = resultOverlayRoot;
            serializedMatch.ApplyModifiedPropertiesWithoutUndo();
        }

        OmokAiController aiController = clone.GetComponentInChildren<OmokAiController>(true);
        if (aiController != null)
        {
            SerializedObject serializedAi = new SerializedObject(aiController);
            serializedAi.FindProperty("useAi").boolValue = true;
            serializedAi.ApplyModifiedPropertiesWithoutUndo();
        }

        OmokTrollInputBridge bridge = clone.GetComponent<OmokTrollInputBridge>();
        if (bridge == null)
        {
            bridge = clone.AddComponent<OmokTrollInputBridge>();
        }

        SerializedObject serializedBridge = new SerializedObject(bridge);
        serializedBridge.FindProperty("stoneDropper").objectReferenceValue = stoneDropper;
        serializedBridge.FindProperty("matchManager").objectReferenceValue = matchManager;
        serializedBridge.FindProperty("aiController").objectReferenceValue = aiController;
        serializedBridge.FindProperty("allowOmokInputInExpansionMode").boolValue = false;
        serializedBridge.FindProperty("startWithOmokInputEnabled").boolValue = true;
        serializedBridge.ApplyModifiedPropertiesWithoutUndo();

        GameObject canvasRoot = FindChildByName(clone.transform, "Canvas");
        if (canvasRoot != null)
        {
            canvasRoot.name = "Omok_ResultCanvas";
        }
    }

    private static Transform CreateWorldRoot(Transform integrationRoot)
    {
        GameObject worldRoot = new GameObject(WorldRootName);
        worldRoot.transform.SetParent(integrationRoot, false);
        return worldRoot.transform;
    }

    private static void MoveChildToParent(Transform root, string childName, Transform targetParent)
    {
        GameObject child = FindChildByName(root, childName);
        if (child == null)
        {
            return;
        }

        child.transform.SetParent(targetParent, false);
    }

    private static void RemoveExistingIntegrationRoot(Scene targetScene)
    {
        GameObject existingRoot = FindRootObject(targetScene, IntegrationRootName);
        if (existingRoot != null)
        {
            Object.DestroyImmediate(existingRoot);
        }

        GameObject legacyRoot = FindRootObject(targetScene, LegacyIntegrationRootName);
        if (legacyRoot != null)
        {
            Object.DestroyImmediate(legacyRoot);
        }
    }

    private static GameObject FindRootObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
            {
                return root;
            }
        }

        return null;
    }

    private static Camera FindMainCamera(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Camera camera = root.GetComponentInChildren<Camera>(true);
            if (camera != null && camera.CompareTag("MainCamera"))
            {
                return camera;
            }
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Camera camera = root.GetComponentInChildren<Camera>(true);
            if (camera != null)
            {
                return camera;
            }
        }

        return null;
    }

    private static GameObject FindChildByName(Transform root, string objectName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = layer;
        }
    }

    private static void ScaleFloat(SerializedObject serializedObject, string propertyName, float scale)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue *= scale;
        }
    }
}
