using Project.Match;
using Project.Loot.Spawning;
using UnityEditor;
using UnityEngine;

namespace Project.Loot.Editor
{
internal static class LootCreateMenu
{
    [MenuItem("GameObject/Loot/Loot Spawn Point", false, 10)]
    private static void CreatePoint(MenuCommand command) => Create<LootSpawnPoint>("Loot Spawn Point", command);

    [MenuItem("GameObject/Loot/Loot Spawn Group", false, 11)]
    private static void CreateGroup(MenuCommand command) => Create<LootSpawnGroup>("Loot Spawn Group", command);

    [MenuItem("GameObject/Loot/Loot Spawn Director", false, 12)]
    private static void CreateDirector(MenuCommand command)
    {
        if (Object.FindAnyObjectByType<LootSpawnDirector>())
        {
            EditorUtility.DisplayDialog("Loot", "This scene already has a LootSpawnDirector.", "OK");
            return;
        }

        Create<LootSpawnDirector>("Loot Spawn Director", command);
    }

    private static void Create<T>(string name, MenuCommand command) where T : Component
    {
        var go = new GameObject(name, typeof(T));
        var parent = command.context as GameObject;

        if (parent)
        {
            GameObjectUtility.SetParentAndAlign(go, parent);
        }
        else if (SceneView.lastActiveSceneView)
        {
            var view = SceneView.lastActiveSceneView;
            var ray = new Ray(view.camera.transform.position, view.camera.transform.forward);
            go.transform.position = Physics.Raycast(ray, out var hit, 200f) ? hit.point : view.pivot;
        }

        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        Selection.activeGameObject = go;
    }
}
}
