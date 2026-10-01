using System;
using System.Collections.Generic;
using Project.Core.Input;
using Project.Core.UI;
using Project.Loot;
using Project.Player.FPSCamera;
using Project.Player.Input;
using Project.Player.Interaction;
using PurrNet;
using UnityEngine;

namespace Project.Player.Loot
{
[DisallowMultipleComponent]
public class PlayerDuffleBagController : NetworkBehaviour, IDuffleBagCarrier
{
    [Header("References")]
    [SerializeField] private LocalPlayerInputProvider _inputProvider;
    [SerializeField] private PlayerInteractor _interactor;
    
    [Header("Capacity & Weight")] 
    [SerializeField] private float _maxCapacityKg = 30.0f;

    [Header("Throwing Tuning")] 
    [SerializeField] private float _maxChargeDuration = 1f;
    [SerializeField] private WorldDuffleBag _duffleBagPrefab;
    [SerializeField] private float _throwSpeed = 9.0f;
    [SerializeField] private float _throwUpwardSpeed = 2.5f;
    [SerializeField] private Transform _dropSpawnPoint;
    [SerializeField] private Vector3 _dropSpawnPointOffset;

    [Header("Visuals (Optional)")] 
    [SerializeField] private GameObject _wornBagVisual;

    [Header("Networked State")] 
    [SerializeField] private SyncVar<bool> _hasBag = new(false);
    [SerializeField] private SyncVar<float> _currentWeight = new(0f);
    [SerializeField] private SyncVar<int> _currentValue = new(0);

    private readonly List<DuffleBagItemEntry> _stowedItems = new();
    private float _throwChargeTimer;

    public bool HasBag => _hasBag.value;
    public float CurrentWeight => _currentWeight.value;
    public float MaxCapacityKg => _maxCapacityKg;
    public int CurrentValue => _currentValue.value;
    public float EncumbranceRatio => _maxCapacityKg > 0.01f ? Mathf.Clamp01(_currentWeight.value / _maxCapacityKg) : 0f;
    public IReadOnlyList<DuffleBagItemEntry> StowedItems => _stowedItems;

    public event Action OnBagStateChanged;
    public event Action<float, float> OnWeightChanged;
    public event Action<int> OnValueChanged;
    public event Action<LootItemDefinition> OnItemStowed;

    private void Awake()
    {
        if (!_dropSpawnPoint)
        {
            var cameraController = GetComponentInChildren<FPSCameraController>() ??
                                   GetComponentInParent<FPSCameraController>();
            if (cameraController && cameraController.PitchPivot)
                _dropSpawnPoint = cameraController.PitchPivot;
            else
                _dropSpawnPoint = transform;
        }

        if (!_inputProvider) 
            _inputProvider = GetComponent<LocalPlayerInputProvider>();

        if (!_interactor) 
            _interactor = GetComponent<PlayerInteractor>();

        _hasBag.onChanged += HandleHasBagChanged;
        _currentWeight.onChanged += HandleWeightChanged;
        _currentValue.onChanged += HandleValueChanged;
    }

    protected override void OnSpawned()
    {
        UpdateVisuals();
    }

    protected override void OnDespawned()
    {
        _hasBag.onChanged -= HandleHasBagChanged;
        _currentWeight.onChanged -= HandleWeightChanged;
        _currentValue.onChanged -= HandleValueChanged;
    }

    private void Update()
    {
        if (!isOwner)
            return;

        ProcessThrowInput(_inputProvider.GetCurrentInput(), Time.deltaTime);
    }

    private void ProcessThrowInput(PlayerInputData input, float deltaTime)
    {
        if (!_hasBag.value && _interactor.CurrentTarget != null)
        {
            _throwChargeTimer = 0f;
            return;
        }

        if (input.DropBagHeld)
        {
            _throwChargeTimer = Mathf.Min(_throwChargeTimer + deltaTime, _maxChargeDuration);
        }

        if (input.DropBagReleased)
        {
            float chargeRatio = _throwChargeTimer / _maxChargeDuration;
            float rawSpeed = Mathf.Lerp(6.0f, 15.0f, chargeRatio);

            float finalThrowSpeed = rawSpeed * Mathf.Lerp(1.0f, 0.65f, EncumbranceRatio);

            ThrowBag(finalThrowSpeed);
            _throwChargeTimer = 0f;
        }
    }

    private void ResetHold()
    {
        _throwChargeTimer = 0f;
    }

    private void HandleHasBagChanged(bool hasBag)
    {
        UpdateVisuals();
        OnBagStateChanged?.Invoke();
    }

    private void HandleWeightChanged(float weight)
    {
        OnWeightChanged?.Invoke(weight, _maxCapacityKg);
    }

    private void HandleValueChanged(int value)
    {
        OnValueChanged?.Invoke(value);
    }

    private void UpdateVisuals()
    {
        if (_wornBagVisual)
        {
            _wornBagVisual.SetActive(_hasBag.value);
        }
    }
    
    public void EquipBag(DuffleBagSnapshot snapshot)
    {
        if (!isServer) return;

        _stowedItems.Clear();
        if (snapshot.Items != null)
        {
            _stowedItems.AddRange(snapshot.Items);
        }

        _hasBag.value = true;
        _currentWeight.value = snapshot.TotalWeight;
        _currentValue.value = snapshot.TotalValue;
    }
    
    public bool TryStowItem(LootItemDefinition item)
    {
        if (!isServer) return false;
        if (!_hasBag.value || !item) return false;

        if (_currentWeight.value + item.WeightKg > _maxCapacityKg)
            return false;
        
        int existingIndex =
            _stowedItems.FindIndex(e => string.Equals(e.ItemId, item.Id, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            var existing = _stowedItems[existingIndex];
            existing.Quantity++;
            _stowedItems[existingIndex] = existing;
        }
        else
        {
            _stowedItems.Add(new DuffleBagItemEntry(item.Id, 1));
        }

        _currentWeight.value += item.WeightKg;
        _currentValue.value += item.BaseValue;
        
        RpcNotifyItemStowed(item.Id);
        return true;
    }
    
    [ObserversRpc(runLocally: true)]
    private void RpcNotifyItemStowed(string itemId)
    {
        if (LootRegistry.Instance.TryGetItem(itemId, out var def))
        {
            OnItemStowed?.Invoke(def);
        }
    }
    
    public void ThrowBag(float throwSpeed)
    {
        if (!_hasBag.value) return;
        Vector3 aimDirection = _dropSpawnPoint ? _dropSpawnPoint.forward : transform.forward;
        if (isServer)
        {
            ExecuteThrowServer(aimDirection, throwSpeed);
        }
        else
        {
            RpcRequestThrowBag(aimDirection, throwSpeed);
        }
    }
    
    [ServerRpc(requireOwnership: true)]
    private void RpcRequestThrowBag(Vector3 aimForward, float throwSpeed, RPCInfo info = default)
    {
        ExecuteThrowServer(aimForward, throwSpeed);
    }
    
    private void ExecuteThrowServer(Vector3 aimForward, float throwSpeed)
    {
        if (!isServer || !_hasBag.value) return;
        if (!_duffleBagPrefab) return;
        var snapshot = new DuffleBagSnapshot(_stowedItems, _currentWeight.value, _currentValue.value);
        Vector3 spawnOrigin = _dropSpawnPoint ? _dropSpawnPoint.position : transform.position + Vector3.up * 1.2f;
        Vector3 spawnPos = spawnOrigin + (aimForward * 0.8f);
        var instance = Instantiate(_duffleBagPrefab, spawnPos + _dropSpawnPointOffset, Quaternion.LookRotation(aimForward));
        if (NetworkManager.main && isSpawned)
        {
            NetworkManager.main.Spawn(instance.gameObject);
        }
        instance.Initialize(snapshot);

        instance.Throw((aimForward * throwSpeed) + (Vector3.up * _throwUpwardSpeed));
        _stowedItems.Clear();
        _hasBag.value = false;
        _currentWeight.value = 0f;
        _currentValue.value = 0;
    }
    
    public void GrantEmptyBag()
    {
        if (!isServer) return;
        EquipBag(DuffleBagSnapshot.Empty);
    }
}
}