using Project.AI.Waves;
using UnityEditor;
using UnityEngine;

namespace Project.AI.Patrol.Editor
{
public static class WaveSpawnPointMenu
{
    [MenuItem("GameObject/AI/Wave Spawn Point", false, 11)]
    private static void Create(MenuCommand command)
    {
        var spawnPoint = new GameObject("Wave Spawn Point");
        GameObjectUtility.SetParentAndAlign(spawnPoint, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(spawnPoint, "Create Wave Spawn Point");
        Undo.AddComponent<AIWaveSpawnPoint>(spawnPoint);
        Selection.activeGameObject = spawnPoint;
    }
}
}
