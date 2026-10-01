using System;
using System.Collections.Generic;
using Project.Combat;
using Project.Player.FPSCamera;
using Project.Player.Weapons;
using PurrNet;
using UnityEngine;

namespace Project.Player.Loot
{
public class PlayerInventoryController : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerWeaponController _playerWeaponController;
    [SerializeField] private Transform _dropSpawnPoint;
    
    [Header("Capacity")] 
    [SerializeField] private int _maxEquippedSlots = 2;
    [SerializeField] private int _backpackCapacity = 18;

    private readonly WeaponInstance[] _equippedSlots = new WeaponInstance[2];
    private readonly List<InventoryItemEntry> _backpack = new();

    public IReadOnlyList<WeaponInstance> EquippedSlots => _equippedSlots;
    public IReadOnlyList<InventoryItemEntry> Backpack => _backpack;

    public int BackpackCapacity => _backpackCapacity;
    public int BackpackCount => _backpack.Count;

    public event Action OnInventoryUpdated;
    public event Action<int, WeaponInstance> OnEquippedSlotChanged;

    public bool HasEmptyEquippedSlot
    {
        get
        {
            SyncFromController();
            return _equippedSlots[0] == null || _equippedSlots[1] == null;
        }
    }

    public bool CanPickupWeapon => HasEmptyEquippedSlot || _backpack.Count < _backpackCapacity;

    protected override void OnSpawned()
    {
        enabled = isOwner;
    }

    private void Awake()
    {
        if (!_playerWeaponController)
            _playerWeaponController = GetComponent<PlayerWeaponController>() ?? GetComponentInParent<PlayerWeaponController>();

        if (!_dropSpawnPoint)
        {
            var cameraController = GetComponentInChildren<FPSCameraController>() ?? GetComponentInParent<FPSCameraController>();
            if (cameraController && cameraController.PitchPivot)
                _dropSpawnPoint = cameraController.PitchPivot;
            else
                _dropSpawnPoint = transform;
        }
    }

    private void Start()
    {
        SyncFromController();
    }

    public void SyncFromController()
    {
        if (!_playerWeaponController) return;

        bool changed = _equippedSlots[0] != _playerWeaponController.Slot1Weapon ||
                       _equippedSlots[1] != _playerWeaponController.Slot2Weapon;

        _equippedSlots[0] = _playerWeaponController.Slot1Weapon;
        _equippedSlots[1] = _playerWeaponController.Slot2Weapon;

        if (changed)
        {
            OnInventoryUpdated?.Invoke();
        }
    }
    
    public bool HandleTapPickup(WeaponInstance weapon)
    {
        if (weapon == null) return false;

        SyncFromController();

        if (_equippedSlots[0] == null)
        {
            EquipToSlot(0, weapon);
            return true;
        }

        if (_equippedSlots[1] == null)
        {
            EquipToSlot(1, weapon);
            return true;
        }

        return TryAddToBackpack(weapon);
    }

    public void SwapActiveWithFloor(WeaponInstance newWeapon)
    {
        if (newWeapon == null) return;

        SyncFromController();

        int activeSlot1Based = (_playerWeaponController && _playerWeaponController.ActiveSlotIndex > 0)
            ? _playerWeaponController.ActiveSlotIndex
            : 1;
        int activeSlot0Based = Mathf.Clamp(activeSlot1Based - 1, 0, _maxEquippedSlots - 1);

        var currentWeapon = _equippedSlots[activeSlot0Based];
        if (currentWeapon == null && _playerWeaponController)
        {
            currentWeapon = _playerWeaponController.ActiveWeapon;
        }

        if (currentWeapon != null)
        {
            SpawnWorldPickup(currentWeapon);
        }

        _equippedSlots[activeSlot0Based] = newWeapon;

        if (_playerWeaponController)
        {
            _playerWeaponController.SetSlotWeapon(activeSlot0Based + 1, newWeapon);
        }

        OnEquippedSlotChanged?.Invoke(activeSlot0Based, newWeapon);
        OnInventoryUpdated?.Invoke();
    }
    
    public bool TryAddToBackpack(WeaponInstance weapon)
    {
        if (weapon == null) return false;
        if (_backpack.Count >= _backpackCapacity) return false;

        _backpack.Add(new InventoryItemEntry(weapon));
        OnInventoryUpdated?.Invoke();
        return true;
    }

    public void EquipToSlot(int slotIndex0Based, WeaponInstance weapon)
    {
        if (slotIndex0Based < 0 || slotIndex0Based >= _maxEquippedSlots || weapon == null) 
            return;

        SyncFromController();
        
        int backpackIdx = _backpack.FindIndex(e => e.Weapon == weapon);
        if (backpackIdx >= 0)
            _backpack.RemoveAt(backpackIdx);

        var previousWeapon = _equippedSlots[slotIndex0Based];

        int otherSlot0Based = slotIndex0Based == 0 ? 1 : 0;
        if (_equippedSlots[otherSlot0Based] == weapon)
        {
            _equippedSlots[otherSlot0Based] = previousWeapon;
            if (_playerWeaponController)
            {
                _playerWeaponController.SetSlotWeapon(otherSlot0Based + 1, previousWeapon);
            }
            OnEquippedSlotChanged?.Invoke(otherSlot0Based, previousWeapon);
            previousWeapon = null;
        }

        if (previousWeapon != null && previousWeapon != weapon)
        {
            if (_backpack.Count < _backpackCapacity)
            {
                _backpack.Add(new InventoryItemEntry(previousWeapon));
            }
            else
            {
                SpawnWorldPickup(previousWeapon);
            }
        }

        _equippedSlots[slotIndex0Based] = weapon;

        if (_playerWeaponController)
        {
            _playerWeaponController.SetSlotWeapon(slotIndex0Based + 1, weapon);
        }

        OnEquippedSlotChanged?.Invoke(slotIndex0Based, weapon);
        OnInventoryUpdated?.Invoke();
    }

    public void DropFromBackpack(int index)
    {
        if (index < 0 || index >= _backpack.Count) return;
        var item = _backpack[index];
        if (item.IsFavorite) return; // Protected from accidental dropping

        _backpack.RemoveAt(index);
        SpawnWorldPickup(item.Weapon);
        OnInventoryUpdated?.Invoke();
    }

    public void ToggleFavorite(int backpackIndex)
    {
        if (backpackIndex < 0 || backpackIndex >= _backpack.Count) return;
        _backpack[backpackIndex].IsFavorite = !_backpack[backpackIndex].IsFavorite;
        if (_backpack[backpackIndex].IsFavorite)
            _backpack[backpackIndex].IsTrash = false;
        OnInventoryUpdated?.Invoke();
    }

    public void ToggleTrash(int backpackIndex)
    {
        if (backpackIndex < 0 || backpackIndex >= _backpack.Count) return;
        _backpack[backpackIndex].IsTrash = !_backpack[backpackIndex].IsTrash;
        if (_backpack[backpackIndex].IsTrash)
            _backpack[backpackIndex].IsFavorite = false;
        OnInventoryUpdated?.Invoke();
    }

    public void DropAllTrash()
    {
        for (int i = _backpack.Count - 1; i >= 0; i--)
        {
            if (_backpack[i].IsTrash && !_backpack[i].IsFavorite)
            {
                var weapon = _backpack[i].Weapon;
                _backpack.RemoveAt(i);
                SpawnWorldPickup(weapon);
            }
        }
        OnInventoryUpdated?.Invoke();
    }

    private void SpawnWorldPickup(WeaponInstance weapon)
    {
        if (!ItemRegistry.Instance || weapon == null) return;
        var snapshot = ItemRegistry.Instance.SerializeWeapon(weapon);
        Transform origin = _dropSpawnPoint ? _dropSpawnPoint : transform;
        Vector3 dropPos = origin.position + origin.forward * 1.5f + Vector3.up * 0.8f;
        Vector3 throwVel = origin.forward * 3f + Vector3.up * 2f;

        _playerWeaponController?.DropSpecifiedWeapon(snapshot, dropPos, throwVel);
    }
}

[Serializable]
public class InventoryItemEntry
{
    public WeaponInstance Weapon;
    public bool IsFavorite;
    public bool IsTrash;

    public InventoryItemEntry(WeaponInstance weapon)
    {
        Weapon = weapon;
    }
}
}