using Project.Combat;
using Project.Core.Interaction;
using PurrNet;
using UnityEngine;

public class WorldDropSpawner : MonoBehaviour, IInteractable
{
    [SerializeField] private ItemRegistry _itemRegistry;
    [SerializeField] private WeaponCoreDefinition _testWeaponDefinition;
    [SerializeField] private WorldWeaponPickup _droppedWeaponPrefab;

    public WorldWeaponPickup SpawnDroppedWeapon(WeaponInstance weapon, Vector3 floorPosition, Vector3 throwVelocity = default)
    {
        if (!ItemRegistry.Instance || !_droppedWeaponPrefab) 
            return null;

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

    public string InteractionPrompt => "[F] Spawn Drop";
    public string HoldPrompt { get; }
    public string TargetName { get; }
    public string Subtext { get; }
    public Color AccentColor { get; }
    public InteractionMode Mode => InteractionMode.Instant;
    public float HoldDuration { get; }
    
    public bool CanInteract(IPlayerInteractor interactor, bool isHold = false)
    {
        return true;
    }

    public string GetDisabledReason(IPlayerInteractor interactor, bool isHold = false)
    {
        return "";
    }

    public void OnInteract(IPlayerInteractor interactor, bool isHold)
    {
        var w = _itemRegistry.GenerateWeapon(_testWeaponDefinition, 1234, 1, ItemRarity.Common);
        SpawnDroppedWeapon(w, transform.position);
    }

    public void OnHoverEnter(IPlayerInteractor interactor)
    {
        
    }

    public void OnHoverExit(IPlayerInteractor interactor)
    {
        
    }
}