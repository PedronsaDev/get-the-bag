using Project.Match;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.Hub.Editor
{
public static class LevelGateSetup
{
    [MenuItem("Tools/Level/Add Ready Gate To Open Scene")]
    private static void AddGate()
    {
        if (Object.FindAnyObjectByType<LevelReadyGate>())
        {
            EditorUtility.DisplayDialog("Level ready gate", "This scene already has a LevelReadyGate.", "OK");
            return;
        }

        var go = new GameObject("LevelReadyGate");
        Undo.RegisterCreatedObjectUndo(go, "Add level ready gate");
        go.AddComponent<LevelReadyGate>();
        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        Debug.Log("[LevelGateSetup] Added LevelReadyGate. Save the scene.");
    }
}
}
