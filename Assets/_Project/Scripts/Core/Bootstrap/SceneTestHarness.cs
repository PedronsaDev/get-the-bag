using UnityEngine;

namespace Project.Core.Bootstrap
{
[DisallowMultipleComponent]
[SelectionBase]
public class SceneTestHarness : MonoBehaviour
{
    [Header("Spawn Configuration")]
    [SerializeField] private Transform _playerSpawnPoint;
    [SerializeField] private bool _spawnPlayerOnStart = true;

    [Header("Debug Arena Options")]
    [SerializeField] private bool _enableInfiniteAmmo = false;
    [SerializeField] private bool _enableGodMode = false;

    public Transform PlayerSpawnPoint => _playerSpawnPoint ? _playerSpawnPoint : transform;

    private void Awake()
    {
        // Register this spawn point as the active level test point
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Vector3 spawnPos = PlayerSpawnPoint.position;
        Gizmos.DrawWireSphere(spawnPos, 0.4f);
        Gizmos.DrawRay(spawnPos, PlayerSpawnPoint.forward * 1.5f);
            
        UnityEditor.Handles.color = new Color(0f, 1f, 1f, 0.25f);
        UnityEditor.Handles.DrawSolidDisc(spawnPos, Vector3.up, 0.4f);
    }
#endif
}
}