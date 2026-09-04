using System;
using Project.Combat;
using Project.Core.Input;
using PurrNet;
using UnityEngine;

namespace Project.Player.Weapons
{
    /// <summary>
    /// Drives player firearm input, weapon swapping between 2 slots, trigger execution,
    /// dynamic recoil/spread recovery, and reloading.
    /// </summary>
    public class PlayerWeaponController : NetworkBehaviour
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
                    ? ItemRegistry.Instance.GenerateWeapon(_defaultSlot1Core, (uint)UnityEngine.Random.Range(1, 99999), 1, ItemRarity.Common)
                    : new WeaponInstance(_defaultSlot1Core, null, 1);
            }

            if (_defaultSlot2Core)
            {
                _slot2Weapon = ItemRegistry.Instance 
                    ? ItemRegistry.Instance.GenerateWeapon(_defaultSlot2Core, (uint)UnityEngine.Random.Range(1, 99999), 1, ItemRarity.Uncommon)
                    : new WeaponInstance(_defaultSlot2Core, null, 2);
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

            // 2. Interaction: Pick up nearby world weapons
            if (input.InteractPressed)
            {
                TryInteractWithNearbyPickup();
            }

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
            _triggerStrategy.Tick(currentWeapon, Time.deltaTime, input.PrimaryFirePressed, input.PrimaryFireHeld, ExecuteFire);

            // 4. Spread recovery
            float baseSpread = currentWeapon.CompiledStats.BaseSpread;
            if (_currentSpread > baseSpread)
            {
                _currentSpread = Mathf.MoveTowards(_currentSpread, baseSpread, Time.deltaTime * 8.0f);
            }
        }

        public void EquipSlot(int slotIndex)
        {
            if (slotIndex != 1 && slotIndex != 2) return;

            _activeSlotIndex = slotIndex;
            _isReloading = false;
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
                if (weapon.CompiledStats.IsHitscan && weapon.UniqueBehaviorIds.Count == 0)
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
                if (NetworkManager.main != null && ItemRegistry.Instance != null)
                {
                    var snapshot = ItemRegistry.Instance.SerializeWeapon(weapon);
                    RpcSyncEquippedWeapon(snapshot, slotIndex);
                }
            }
            else
            {
                if (_visualAssembler) _visualAssembler.ClearVisuals();
                OnWeaponEquipped?.Invoke(null);
                OnAmmoChanged?.Invoke(0, 0);

                if (NetworkManager.main != null)
                {
                    RpcSyncEquippedWeapon(default, slotIndex);
                }
            }
        }

        [ObserversRpc(bufferLast: true)]
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

        /// <summary>
        /// Drops the currently held weapon into the world as a WorldWeaponPickup.
        /// </summary>
        public void DropActiveWeapon()
        {
            var weaponToDrop = ActiveWeapon;
            if (weaponToDrop == null) return;

            if (ItemRegistry.Instance == null) return;
            var snapshot = ItemRegistry.Instance.SerializeWeapon(weaponToDrop);

            // Clear active slot
            if (_activeSlotIndex == 1) _slot1Weapon = null;
            else _slot2Weapon = null;

            // Switch to secondary slot if populated, otherwise clear
            int otherSlot = _activeSlotIndex == 1 ? 2 : 1;
            var otherWeapon = otherSlot == 1 ? _slot1Weapon : _slot2Weapon;
            if (otherWeapon != null)
            {
                EquipSlot(otherSlot);
            }
            else
            {
                EquipSlot(_activeSlotIndex);
            }

            // Spawn the physical pickup in the world
            Vector3 dropPos = transform.position + transform.forward * 1.2f + Vector3.up * 0.8f;
            Vector3 throwVel = transform.forward * 3.5f + Vector3.up * 2f;
            RpcRequestSpawnWorldPickup(snapshot, dropPos, throwVel);
        }

        [ServerRpc(requireOwnership: false)]
        private void RpcRequestSpawnWorldPickup(ItemNetSnapshot snapshot, Vector3 position, Vector3 throwVelocity, RPCInfo info = default)
        {
            if (!isServer) return;

            var pickupObj = new GameObject($"WorldPickup_{snapshot.CustomName ?? "Weapon"}");
            pickupObj.transform.position = position;

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

            pickup.Initialize(snapshot, throwVelocity);
        }

        private void TryInteractWithNearbyPickup()
        {
            Transform aimOrigin = _cameraController ? _cameraController.PitchPivot : transform;
            Vector3 origin = aimOrigin.position;
            Vector3 forward = aimOrigin.forward;

            // 1. Raycast check looking at pickup
            if (Physics.Raycast(origin, forward, out var hit, 4.0f))
            {
                var pickup = hit.collider.GetComponentInParent<WorldWeaponPickup>();
                if (pickup != null)
                {
                    pickup.RequestPickup();
                    return;
                }
            }

            // 2. Proximity overlap check
            var hits = Physics.OverlapSphere(origin, 2.5f);
            WorldWeaponPickup closestPickup = null;
            float closestDist = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                var pickup = hits[i].GetComponentInParent<WorldWeaponPickup>();
                if (pickup != null)
                {
                    float d = Vector3.Distance(origin, pickup.transform.position);
                    if (d < closestDist)
                    {
                        closestDist = d;
                        closestPickup = pickup;
                    }
                }
            }

            if (closestPickup != null)
            {
                closestPickup.RequestPickup();
            }
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

            // Check for The Butcher / ammo refund perk
            bool ammoRefunded = false;
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

            if (!ammoRefunded)
            {
                weapon.CurrentAmmoInMag--;
            }

            OnAmmoChanged?.Invoke(weapon.CurrentAmmoInMag, weapon.CompiledStats.MagazineSize);

            // Expand spread and kick recoil
            _currentSpread = Mathf.Min(weapon.CompiledStats.MaxSpread, _currentSpread + (weapon.CompiledStats.RecoilKick * 0.5f));

            if (_cameraController)
            {
                _cameraController.AddPitchRecoil(weapon.CompiledStats.RecoilKick);
            }

            // Deliver projectile
            _deliveryStrategy.Deliver(_weaponContext, weapon, _currentSpread);
            OnFired?.Invoke();

            // Broadcast firing feedback to remote observers
            if (NetworkManager.main != null)
            {
                Vector3 muzzle = _weaponContext.MuzzlePoint ? _weaponContext.MuzzlePoint.position : transform.position;
                Vector3 forward = _weaponContext.AimOrigin ? _weaponContext.AimOrigin.forward : transform.forward;
                RpcFireFeedback(muzzle, forward);
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

        [ObserversRpc(runLocally: false)]
        private void RpcFireFeedback(Vector3 muzzlePosition, Vector3 aimDirection)
        {
            // Remote observers trigger muzzle flash / audio
        }
    }
}
