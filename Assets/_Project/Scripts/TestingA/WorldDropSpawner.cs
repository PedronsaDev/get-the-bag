using Project.Combat;
using PurrNet;
using UnityEngine;

public class WorldDropSpawner : MonoBehaviour
{
    [SerializeField] private WorldWeaponPickup _droppedWeaponPrefab;

    public WorldWeaponPickup SpawnDroppedWeapon(WeaponInstance weapon, Vector3 floorPosition, Vector3 throwVelocity = default)
    {
        if (!ItemRegistry.Instance || !_droppedWeaponPrefab) return null;

        ItemNetSnapshot snapshot = ItemRegistry.Instance.SerializeWeapon(weapon);

        WorldWeaponPickup pickup = Instantiate(_droppedWeaponPrefab, floorPosition, Quaternion.identity);

        if (NetworkManager.main && NetworkManager.main.isServer)
        {
            NetworkManager.main.Spawn(pickup.gameObject);
        }

        // 4. Initialize visuals, stats, and physical impulse
        pickup.Initialize(snapshot, throwVelocity);
        return pickup;
    }
}