using System;
using System.Collections.Generic;
using Project.Combat;
using PurrNet;
using UnityEngine;

namespace Project.Player.Weapons
{
/// <summary>
/// Drives player firearm input, weapon swapping between 2 slots, trigger execution,
/// dynamic recoil/spread recovery, and reloading.
/// </summary>
public class PlayerWeaponController : NetworkBehaviour, IWeaponPickupReceiver
{
    [Header("Weapon Sockets & Visuals")] 
    [SerializeField] private WeaponVisualAssembler _visualAssembler;
    [SerializeField] private Transform _weaponMount;
    [SerializeField] private LayerMask _hitMask = ~0;

    [Header("Initial Test Loadout (Optional)")] 
    [SerializeField] private WeaponCoreDefinition _defaultSlot1Core;
    [SerializeField] private WeaponCoreDefinition _defaultSlot2Core;

    private PlayerFacade _playerFacade;
    private FPSCameraController _cameraController;
    private WeaponContext _weaponContext;

    private WeaponInstance _slot1Weapon;
    private WeaponInstance _slot2Weapon;
    private int _activeSlotIndex = 1; // 1 or 2

    private ITriggerStrategy _triggerStrategy = new SemiAutoTrigger();
    private IProjectileDeliveryStrategy _deliveryStrategy = new HitscanDelivery();

    private float _currentSpread;
    private float _spreadRecoveryTimer;
    private float _reloadTimer;
    private bool _isReloading;

    public WeaponInstance ActiveWeapon => _activeSlotIndex == 1 ? _slot1Weapon : _slot2Weapon;
    public int ActiveSlotIndex => _activeSlotIndex;
    public float CurrentSpread => _currentSpread;
    public bool IsReloading => _isReloading;

    public event Action<WeaponInstance> OnWeaponEquipped;
    public event Action<int, int> OnAmmoChanged; // (currentInMag, maxMag)
    public event Action OnFired;
    public event Action OnReloadStarted;
    public event Action OnReloadFinished;

    public WeaponInstance Slot1Weapon => _slot1Weapon;
    public WeaponInstance Slot2Weapon => _slot2Weapon;

    /// <summary>
    /// The physical barrel tip / fire point transform from which projectiles, tracers, and muzzle flash originate.
    /// </summary>
    public Transform FirePoint => _weaponContext?.MuzzlePoint ? _weaponContext.MuzzlePoint : (_visualAssembler ? _visualAssembler.MuzzleSocket : (_cameraController ? _cameraController.PitchPivot : transform));
    public Transform MuzzleTransform => FirePoint;

    private void Awake()
    {
        _playerFacade = GetComponentInParent<PlayerFacade>();
        _cameraController = GetComponentInChildren<FPSCameraController>();

        if (!_visualAssembler)
            _visualAssembler = GetComponentInChildren<WeaponVisualAssembler>();
    }

    protected override void OnSpawned()
    {
        if (!isOwner) return;

        Transform aimOrigin = _cameraController ? _cameraController.PitchPivot : transform;
        Transform muzzle = _visualAssembler ? _visualAssembler.MuzzleSocket : aimOrigin;
        _weaponContext = new WeaponContext(gameObject, aimOrigin, muzzle, localPlayer, _hitMask);

        // Initialize default weapons if assigned
        if (_defaultSlot1Core)
        {
            _slot1Weapon = ItemRegistry.Instance
                ? ItemRegistry.Instance.GenerateWeapon(_defaultSlot1Core, (uint)UnityEngine.Random.Range(1, 99999), 1,
                    ItemRarity.Common)
                : new WeaponInstance(_defaultSlot1Core, seed: 1);
        }

        if (_defaultSlot2Core)
        {
            _slot2Weapon = ItemRegistry.Instance
                ? ItemRegistry.Instance.GenerateWeapon(_defaultSlot2Core, (uint)UnityEngine.Random.Range(1, 99999), 1,
                    ItemRarity.Uncommon)
                : new WeaponInstance(_defaultSlot2Core, seed: 2);
        }

        EquipSlot(1);
    }

    private void Update()
    {
        if (!isOwner) return;

        if (!_playerFacade)
            _playerFacade = GetComponentInParent<PlayerFacade>();

        if (!_playerFacade) return;

        var input = _playerFacade.CurrentInput;
        var currentWeapon = ActiveWeapon;

        // 1. Slot swapping
        if (input.WeaponSlotPressed == 1 && _activeSlotIndex != 1) EquipSlot(1);
        if (input.WeaponSlotPressed == 2 && _activeSlotIndex != 2) EquipSlot(2);

        if (currentWeapon == null) return;

        // 2. Reloading loop
        if (_isReloading)
        {
            _reloadTimer -= Time.deltaTime;
            if (_reloadTimer <= 0f)
            {
                FinishReload();
            }

            return;
        }

        if (input.ReloadPressed && currentWeapon.CurrentAmmoInMag < currentWeapon.CompiledStats.MagazineSize)
        {
            StartReload();
            return;
        }

        // 3. Trigger & firing tick
        _triggerStrategy.Tick(currentWeapon, Time.deltaTime, input.PrimaryFirePressed, input.PrimaryFireHeld,
            ExecuteFire);

        // 4. Spread dynamics (movement inaccuracy and firing recovery)
        float targetBaseSpread = currentWeapon.CompiledStats.BaseSpread;

        if (_playerFacade != null)
        {
            var locomotion = _playerFacade.LocomotionState;
            if (locomotion.Phase == LocomotionPhase.Airborne)
            {
                targetBaseSpread += 2.5f; // Jump bloom
            }
            else if (locomotion.IsSprinting)
            {
                targetBaseSpread += 1.5f; // Sprinting bloom
            }
            else if (locomotion.MoveInputMagnitude > 0.1f)
            {
                targetBaseSpread += 0.8f * locomotion.MoveInputMagnitude; // Walking bloom
            }
        }

        if (_currentSpread < targetBaseSpread)
        {
            _currentSpread = Mathf.MoveTowards(_currentSpread, targetBaseSpread, Time.deltaTime * 12.0f);
        }
        else if (_spreadRecoveryTimer > 0f)
        {
            _spreadRecoveryTimer -= Time.deltaTime;
        }
        else if (_currentSpread > targetBaseSpread)
        {
            _currentSpread = Mathf.MoveTowards(_currentSpread, targetBaseSpread, Time.deltaTime * 6.0f);
        }
    }

    public void EquipSlot(int slotIndex)
    {
        if (slotIndex != 1 && slotIndex != 2) return;

        _activeSlotIndex = slotIndex;
        _isReloading = false;
        _spreadRecoveryTimer = 0f;
        _triggerStrategy.Reset();

        var weapon = ActiveWeapon;
        if (weapon != null)
        {
            _currentSpread = weapon.CompiledStats.BaseSpread;

            // Select trigger strategy
            _triggerStrategy = weapon.CompiledStats.FiringMode switch
            {
                FiringMode.FullAuto => new FullAutoTrigger(),
                FiringMode.Burst => new BurstTrigger(),
                _ => new SemiAutoTrigger()
            };

            // Select delivery strategy (Hitscan vs Ballistic/Projectile)
            bool hasProjectileBehaviors = weapon.ProjectileBehaviors is { Count: > 0 } ||
                                          weapon.UniqueBehaviorIds is { Count: > 0 };

            if (weapon.CompiledStats.IsHitscan && !hasProjectileBehaviors)
            {
                _deliveryStrategy = new HitscanDelivery();
            }
            else
            {
                _deliveryStrategy = new BallisticDelivery();
            }

            // Refresh visuals
            if (_visualAssembler)
            {
                _visualAssembler.AssembleVisuals(weapon);
                if (_weaponContext != null)
                {
                    _weaponContext.MuzzlePoint = _visualAssembler.MuzzleSocket;
                }
            }

            OnWeaponEquipped?.Invoke(weapon);
            OnAmmoChanged?.Invoke(weapon.CurrentAmmoInMag, weapon.CompiledStats.MagazineSize);

            // Broadcast equipped weapon to remote observers (Weakness B fix)
            if (NetworkManager.main && ItemRegistry.Instance)
            {
                var snapshot = ItemRegistry.Instance.SerializeWeapon(weapon);
                RpcSyncEquippedWeapon(snapshot, slotIndex);
            }
        }
        else
        {
            if (_visualAssembler) _visualAssembler.ClearVisuals();
            if (_weaponContext != null) _weaponContext.MuzzlePoint = _cameraController ? _cameraController.PitchPivot : transform;
            OnWeaponEquipped?.Invoke(null);
            OnAmmoChanged?.Invoke(0, 0);

            if (NetworkManager.main)
            {
                RpcSyncEquippedWeapon(default, slotIndex);
            }
        }
    }

    [ObserversRpc(bufferLast: true, requireServer: false)]
    private void RpcSyncEquippedWeapon(ItemNetSnapshot snapshot, int slotIndex)
    {
        if (isOwner) return; // Owner already executed locally

        var weapon = (!string.IsNullOrEmpty(snapshot.CoreGuid) && ItemRegistry.Instance)
            ? ItemRegistry.Instance.ReconstructWeapon(snapshot)
            : null;

        if (slotIndex == 1) _slot1Weapon = weapon;
        else _slot2Weapon = weapon;

        _activeSlotIndex = slotIndex;

        if (_visualAssembler)
        {
            if (weapon != null)
                _visualAssembler.AssembleVisuals(weapon);
            else
                _visualAssembler.ClearVisuals();
        }
    }

    /// <summary>
    /// Equips a weapon instance directly into the specified slot (1 or 2, defaults to active).
    /// </summary>
    public void EquipWeapon(WeaponInstance weapon, int slotIndex = -1)
    {
        if (slotIndex != 1 && slotIndex != 2)
            slotIndex = _activeSlotIndex;

        if (slotIndex == 1)
            _slot1Weapon = weapon;
        else
            _slot2Weapon = weapon;

        if (slotIndex == _activeSlotIndex)
        {
            EquipSlot(slotIndex);
        }
    }

    /// <summary>
    /// Equips a picked-up weapon: fills empty active slot, or fills secondary slot,
    /// or drops the active weapon onto the ground to make room!
    /// </summary>
    public void EquipOrSwapWeapon(WeaponInstance newWeapon, ItemNetSnapshot snapshot = default)
    {
        if (newWeapon == null && !string.IsNullOrEmpty(snapshot.CoreGuid) && ItemRegistry.Instance)
        {
            newWeapon = ItemRegistry.Instance.ReconstructWeapon(snapshot);
        }

        if (newWeapon == null) return;

        // If active slot is empty:
        if (ActiveWeapon == null)
        {
            EquipWeapon(newWeapon, _activeSlotIndex);
            return;
        }

        // If the secondary slot is empty:
        int otherSlot = _activeSlotIndex == 1 ? 2 : 1;
        var otherWeapon = otherSlot == 1 ? _slot1Weapon : _slot2Weapon;
        if (otherWeapon == null)
        {
            EquipWeapon(newWeapon, otherSlot);
            EquipSlot(otherSlot);
            return;
        }

        // Both slots full: Drop active weapon to ground and equip new one!
        DropActiveWeapon();
        EquipWeapon(newWeapon, _activeSlotIndex);
    }

    public void ReceiveWeaponPickup(WeaponInstance weapon, ItemNetSnapshot snapshot, bool isHold)
    {
        if (isHold)
        {
            // Force equip into active slot (drop currently held if active is full)
            EquipOrSwapWeapon(weapon, snapshot);
        }
        else
        {
            // Tap pickup: equip or store
            EquipOrSwapWeapon(weapon, snapshot);
        }
    }

    /// <summary>
    /// Drops the currently held weapon into the world as a WorldWeaponPickup.
    /// </summary>
    public void DropActiveWeapon()
    {
        var weaponToDrop = ActiveWeapon;
        if (weaponToDrop == null) return;

        if (!ItemRegistry.Instance) return;
        var snapshot = ItemRegistry.Instance.SerializeWeapon(weaponToDrop);

        // Clear active slot
        if (_activeSlotIndex == 1) _slot1Weapon = null;
        else _slot2Weapon = null;

        // Switch to secondary slot if populated, otherwise clear
        int otherSlot = _activeSlotIndex == 1 ? 2 : 1;
        var otherWeapon = otherSlot == 1 ? _slot1Weapon : _slot2Weapon;
        EquipSlot(otherWeapon != null ? otherSlot : _activeSlotIndex);

        // Spawn the physical pickup in the world
        Vector3 dropPos = transform.position + transform.forward * 1.2f + Vector3.up * 0.8f;
        Vector3 throwVel = transform.forward * 3.5f + Vector3.up * 2f;
        RpcRequestSpawnWorldPickup(snapshot, dropPos, throwVel);
    }

    [ServerRpc(requireOwnership: false)]
    private void RpcRequestSpawnWorldPickup(ItemNetSnapshot snapshot, Vector3 position, Vector3 throwVelocity,
        RPCInfo info = default)
    {
        if (!isServer) return;

        var pickupObj = new GameObject($"WorldPickup_{snapshot.CustomName ?? "Weapon"}");
        pickupObj.transform.position = position;

        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0)
        {
            pickupObj.layer = interactableLayer;
        }

        var collider = pickupObj.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 1.2f;

        var rb = pickupObj.AddComponent<Rigidbody>();
        rb.mass = 2f;
        rb.linearDamping = 1f;

        var visualRoot = new GameObject("Visual_Root");
        visualRoot.transform.SetParent(pickupObj.transform, false);

        visualRoot.AddComponent<WeaponVisualAssembler>();
        var pickup = pickupObj.AddComponent<WorldWeaponPickup>();

        if (NetworkManager.main != null && NetworkManager.main.isServer)
        {
            NetworkManager.main.Spawn(pickupObj);
        }

        pickup.Initialize(snapshot, throwVelocity);
    }


    public void SetSlotWeapon(int slotIndex, WeaponInstance weapon)
    {
        if (slotIndex == 1) _slot1Weapon = weapon;
        else if (slotIndex == 2) _slot2Weapon = weapon;

        if (_activeSlotIndex == slotIndex)
        {
            EquipSlot(slotIndex);
        }
    }

    private void ExecuteFire()
    {
        var weapon = ActiveWeapon;
        if (weapon == null || weapon.CurrentAmmoInMag <= 0) return;

        // Check for weapon perks (e.g. ammo refund)
        bool ammoRefunded = false;
        if (weapon.WeaponPerks != null && weapon.WeaponPerks.Count > 0)
        {
            for (int i = 0; i < weapon.WeaponPerks.Count; i++)
            {
                var perk = weapon.WeaponPerks[i];
                if (perk && perk.OnPreFire(weapon, _weaponContext))
                {
                    ammoRefunded = true;
                    break;
                }
            }
        }

        // Fallback for legacy string IDs
        if (!ammoRefunded && weapon.UniqueBehaviorIds != null)
        {
            for (int i = 0; i < weapon.UniqueBehaviorIds.Count; i++)
            {
                if (weapon.UniqueBehaviorIds[i] == LegendaryBehaviorIds.TheButcher)
                {
                    if (UnityEngine.Random.value < 0.35f) // 35% chance to refund
                    {
                        ammoRefunded = true;
                        break;
                    }
                }
            }
        }

        if (!ammoRefunded)
        {
            weapon.CurrentAmmoInMag--;
        }

        // Post fire perks
        if (weapon.WeaponPerks != null)
        {
            for (int i = 0; i < weapon.WeaponPerks.Count; i++)
            {
                weapon.WeaponPerks[i]?.OnPostFire(weapon, _weaponContext);
            }
        }

        OnAmmoChanged?.Invoke(weapon.CurrentAmmoInMag, weapon.CompiledStats.MagazineSize);

        // Expand spread and kick recoil
        float spreadRange = Mathf.Max(0f, weapon.CompiledStats.MaxSpread - weapon.CompiledStats.BaseSpread);
        float spreadStep = Mathf.Max(weapon.CompiledStats.RecoilKick * 0.75f, spreadRange * 0.25f);
        _currentSpread = Mathf.Min(weapon.CompiledStats.MaxSpread, _currentSpread + spreadStep);
        _spreadRecoveryTimer = 0.18f;

        // Deliver projectile
        _deliveryStrategy.Deliver(_weaponContext, weapon, _currentSpread);
        OnFired?.Invoke();

        // Broadcast firing feedback and cosmetic tracers/projectiles to remote observers
        if (NetworkManager.main)
        {
            Vector3 muzzle = _weaponContext.MuzzlePoint ? _weaponContext.MuzzlePoint.position : transform.position;
            Vector3 forward = _weaponContext.AimOrigin ? _weaponContext.AimOrigin.forward : transform.forward;
            Vector3 aimOrigin = _weaponContext.AimOrigin ? _weaponContext.AimOrigin.position : transform.position;

            Vector3 targetPoint;
            if (Physics.Raycast(aimOrigin, forward, out RaycastHit hit, 300f, _hitMask, QueryTriggerInteraction.Ignore))
            {
                targetPoint = hit.point;
            }
            else
            {
                targetPoint = aimOrigin + forward * 200f;
            }

            int seed = UnityEngine.Random.Range(1, int.MaxValue);
            bool isBallistic = !weapon.CompiledStats.IsHitscan ||
                               (weapon.ProjectileBehaviors is { Count: > 0 } || weapon.UniqueBehaviorIds is { Count: > 0 });
            float speed = Mathf.Max(10f, weapon.CompiledStats.ProjectileSpeed);
            string patternId = weapon.PelletPattern ? weapon.PelletPattern.name : string.Empty;

            var fireData = new FireCosmeticsNetData(
                muzzlePosition: muzzle,
                targetPoint: targetPoint,
                aimForward: forward,
                currentSpread: _currentSpread,
                randomSeed: seed,
                pelletCount: Mathf.Max(1, weapon.CompiledStats.PelletsPerShot),
                element: weapon.CompiledStats.Element,
                isBallistic: isBallistic,
                projectileSpeed: speed,
                patternId: patternId
            );

            RpcFireCosmetics(fireData);
        }

        // Auto-reload on empty clip
        if (weapon.CurrentAmmoInMag <= 0)
        {
            StartReload();
        }
    }

    private void StartReload()
    {
        var weapon = ActiveWeapon;
        if (weapon == null || _isReloading) return;

        _isReloading = true;
        _reloadTimer = weapon.CompiledStats.ReloadSpeed;
        OnReloadStarted?.Invoke();
    }

    private void FinishReload()
    {
        var weapon = ActiveWeapon;
        if (weapon != null)
        {
            weapon.CurrentAmmoInMag = weapon.CompiledStats.MagazineSize;
            OnAmmoChanged?.Invoke(weapon.CurrentAmmoInMag, weapon.CompiledStats.MagazineSize);
        }

        _isReloading = false;
        OnReloadFinished?.Invoke();
    }

    [ObserversRpc(runLocally: false, requireServer: false)]
    private void RpcFireCosmetics(FireCosmeticsNetData data)
    {
        SimulateRemoteFireCosmetics(data);
    }

    private void SimulateRemoteFireCosmetics(FireCosmeticsNetData data)
    {
        if (isOwner) return;

        Vector3 muzzle = _visualAssembler && _visualAssembler.MuzzleSocket
            ? _visualAssembler.MuzzleSocket.position
            : data.MuzzlePosition;

        OnFired?.Invoke();

        var weapon = ActiveWeapon;
        var pattern = (weapon != null && weapon.PelletPattern != null) ? weapon.PelletPattern : null;

        // Converge trajectory towards target point with point-blank inversion safety
        Vector3 aimForward = data.AimForward.sqrMagnitude > 0.001f ? data.AimForward.normalized : transform.forward;
        Vector3 toTarget = data.TargetPoint - muzzle;
        float targetForwardReach = Vector3.Dot(toTarget, aimForward);

        Vector3 baseDirection;
        if (targetForwardReach <= 0.1f)
        {
            baseDirection = aimForward;
        }
        else
        {
            baseDirection = toTarget.normalized;
        }

        if (Vector3.Dot(baseDirection, aimForward) < 0.1f)
        {
            baseDirection = aimForward;
        }

        Vector3 aimRight = Vector3.Cross(Vector3.up, aimForward);
        if (aimRight.sqrMagnitude < 0.001f) aimRight = Vector3.Cross(Vector3.forward, aimForward);
        aimRight.Normalize();
        Vector3 aimUp = Vector3.Cross(aimForward, aimRight).normalized;

        var prevRandomState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(data.RandomSeed);

        int pellets = Mathf.Max(1, (int)data.PelletCount);
        for (int i = 0; i < pellets; i++)
        {
            Vector3 shootDir;
            if (pattern != null)
            {
                float baseSpread = weapon != null ? weapon.CompiledStats.BaseSpread : 0f;
                Vector2 patternOffset = pattern.GetPointOffset(i, pellets, data.CurrentSpread, baseSpread);
                shootDir = PelletPatternDefinition.CalculatePatternDirection(baseDirection, aimRight, aimUp, patternOffset);
            }
            else
            {
                shootDir = CombatVisualHelper.CalculateSpreadDirection(baseDirection, data.CurrentSpread);
            }

            if (data.IsBallistic)
            {
                Vector3 velocity = shootDir * data.ProjectileSpeed;
                List<IProjectileBehavior> behaviors = null;
                if (weapon?.ProjectileBehaviors != null && weapon.ProjectileBehaviors.Count > 0)
                {
                    behaviors = new List<IProjectileBehavior>();
                    for (int b = 0; b < weapon.ProjectileBehaviors.Count; b++)
                    {
                        var behDef = weapon.ProjectileBehaviors[b];
                        if (behDef)
                        {
                            var inst = behDef.CreateRuntimeInstance();
                            if (inst != null) behaviors.Add(inst);
                        }
                    }
                }

                ProjectilePool.Instance.SpawnCosmetic(muzzle, velocity, _hitMask, behaviors, data.Element);
            }
            else
            {
                Vector3 tracerTarget = muzzle + (shootDir * 200f);
                bool didHit = false;
                Vector3 hitNormal = Vector3.up;

                if (Physics.Raycast(muzzle, shootDir, out RaycastHit hit, 300f, _hitMask, QueryTriggerInteraction.Ignore))
                {
                    tracerTarget = hit.point;
                    hitNormal = hit.normal;
                    didHit = true;
                }

                Color tracerColor = CombatVisualHelper.GetElementColor(data.Element);
                DamageType element = data.Element;
                Vector3 finalImpact = tracerTarget;
                Vector3 finalNormal = hitNormal;
                bool finalDidHit = didHit;

                TracerPool.Instance.Spawn(
                    muzzle,
                    tracerTarget,
                    450f,
                    tracerColor,
                    onArrival: () =>
                    {
                        if (finalDidHit)
                        {
                            ImpactEffectPool.Instance.Spawn(finalImpact, finalNormal, element);
                        }
                    }
                );
            }
        }

        UnityEngine.Random.state = prevRandomState;
    }
}
}