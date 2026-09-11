using System;
using Project.Combat;
using Project.Core;
using Project.Core.Player;
using Project.Player.Weapons;
using PurrNet;
using UnityEngine;

namespace Project.Player.Locomotion
{
    public class PlayerDownedState : NetworkBehaviour, IPlayerStatusProvider
    {
        [Header("References")]
        [SerializeField] private HealthComponent _healthComponent;
        [SerializeField] private LocomotionController _locomotionController;
        [SerializeField] private PlayerDownedInteractable _playerDownedInteractable;
        [SerializeField] private PlayerWeaponController _weaponController;
        [SerializeField] private PlayerFacade _playerFacade;

        [Header("Settings")]
        [SerializeField] private float _bleedOutTimer = 45f;
        [SerializeField] private float _reviveHealthPercent = 0.35f;
        [SerializeField] private float _maxDragDistance = 4.5f;

        public SyncVar<float> BleedOutTimer = new(45f);
        public SyncVar<bool> IsDown = new();
        public SyncVar<PlayerID?> DraggedByPlayer = new();

        private PlayerID? _currentDragger;

        public bool IsBeingDragged => DraggedByPlayer.value.HasValue;
        public Vector3 BodyPosition => _locomotionController ? _locomotionController.transform.position : transform.position;

        public event Action<bool> OnDownedStateChanged;
        public event Action<PlayerID?> OnDraggedByChanged;
        public event Action OnTrueDeath;
        
        public bool IsDead => _healthComponent is { IsDead: true };
        public bool IsDowned => IsDown.value;
        public bool IsStanding => !IsDead && !IsDowned;

        private void Awake()
        {
            if (!_healthComponent)
                _healthComponent = GetComponent<HealthComponent>() ?? GetComponentInParent<HealthComponent>() ?? GetComponentInChildren<HealthComponent>();

            if (!_locomotionController)
                _locomotionController = GetComponent<LocomotionController>() ?? GetComponentInParent<LocomotionController>() ?? GetComponentInChildren<LocomotionController>();

            if (!_playerDownedInteractable)
                _playerDownedInteractable = GetComponentInChildren<PlayerDownedInteractable>() ?? GetComponent<PlayerDownedInteractable>();

            if (!_weaponController)
                _weaponController = GetComponent<PlayerWeaponController>() ?? GetComponentInParent<PlayerWeaponController>() ?? GetComponentInChildren<PlayerWeaponController>();

            if (!_playerFacade)
                _playerFacade = GetComponent<PlayerFacade>() ?? GetComponentInParent<PlayerFacade>() ?? GetComponentInChildren<PlayerFacade>();

            var identifier = GetComponentInChildren<PlayerIdentifier>() ?? GetComponent<PlayerIdentifier>();
            if (identifier)
            {
                identifier.StatusProvider = this;
            }
            
            IsDown.onChanged += HandleDownedChanged;
            DraggedByPlayer.onChanged += HandleDraggedByChanged;
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();

            if (isServer || !NetworkManager.main || NetworkManager.main.isOffline)
            {
                if (_healthComponent)
                {
                    _healthComponent.OnDeath += HandleDeathOrDowned;
                }
            }

            UpdateInteractableCollider();
        }

        protected override void OnDespawned()
        {
            base.OnDespawned();

            if (_healthComponent)
            {
                _healthComponent.OnDeath -= HandleDeathOrDowned;
            }

            ClearLocalDraggingIfTarget();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            IsDown.onChanged -= HandleDownedChanged;
            DraggedByPlayer.onChanged -= HandleDraggedByChanged;

            if (_healthComponent)
            {
                _healthComponent.OnDeath -= HandleDeathOrDowned;
            }

            var identifier = GetComponentInChildren<PlayerIdentifier>() ?? GetComponent<PlayerIdentifier>();
            if (identifier && ReferenceEquals(identifier.StatusProvider, this))
            {
                identifier.StatusProvider = null;
            }

            ClearLocalDraggingIfTarget();
        }

        private void Update()
        {
            bool isMatchHost = isServer || !NetworkManager.main || NetworkManager.main.isOffline;

            if (isMatchHost && IsDown.value)
            {
                BleedOutTimer.value -= Time.deltaTime;

                if (DraggedByPlayer.value.HasValue)
                {
                    ValidateDraggingOnServer();
                }

                if (BleedOutTimer.value <= 0f)
                {
                    ExecuteTrueDeath();
                }
            }

            // On downed player owner: update drag target follow if being dragged
            if (isOwner && IsDown.value && DraggedByPlayer.value.HasValue)
            {
                UpdateOwnerDragFollow();
            }
        }

        public bool IsBeingDraggedByLocalPlayer()
        {
            var localId = GetLocalPlayerId();
            return localId.HasValue && DraggedByPlayer.value.HasValue && DraggedByPlayer.value.Value == localId.Value;
        }

        public static PlayerID? GetLocalPlayerId()
        {
            if (LocalPlayerContext.Player)
            {
                var id = LocalPlayerContext.Player.GetComponentInChildren<PlayerIdentifier>();
                if (id && id.PlayerID.HasValue) return id.PlayerID.Value;
            }

            if (NetworkManager.main && !NetworkManager.main.isOffline)
                return NetworkManager.main.localPlayer;

            return PurrNet.PlayerID.Server;
        }

        private void HandleDeathOrDowned(DamageResult result)
        {
            bool isMatchHost = isServer || !NetworkManager.main || NetworkManager.main.isOffline;
            if (!isMatchHost) return;

            if (IsDown.value) return;

            if (IsTeamWiped())
            {
                Debug.Log("[PlayerDownedState] Team wiped! Triggering true death.");
                ExecuteTrueDeath();
                return;
            }

            ExecuteDowned();
        }

        private bool IsTeamWiped()
        {
            var activePlayers = PlayerIdentifier.ActivePlayers;
            if (activePlayers == null || activePlayers.Count <= 1)
            {
                // Solo player allowed downed bleedout state
                return false;
            }

            int livingCount = 0;
            for (int i = 0; i < activePlayers.Count; i++)
            {
                var p = activePlayers[i];
                if (!p) continue;

                var downed = p.GetComponentInParent<PlayerDownedState>();
                var health = p.GetComponentInParent<HealthComponent>();

                if (downed != this && health && !health.IsDead && downed && !downed.IsDown.value)
                {
                    livingCount++;
                }
            }

            return livingCount == 0;
        }

        private void ExecuteDowned()
        {
            IsDown.value = true;
            BleedOutTimer.value = _bleedOutTimer;
            DraggedByPlayer.value = null;
        }

        private void ExecuteTrueDeath()
        {
            IsDown.value = false;
            DraggedByPlayer.value = null;
            RpcOnTrueDeath();
        }

        [ObserversRpc(runLocally: true)]
        private void RpcOnTrueDeath()
        {
            _playerDownedInteractable?.Disable();
            OnTrueDeath?.Invoke();
        }

        public void RequestRevive()
        {
            bool isMatchHost = isServer || !NetworkManager.main || NetworkManager.main.isOffline;
            if (isMatchHost)
            {
                ExecuteRevive(GetLocalPlayerId() ?? PurrNet.PlayerID.Server);
            }
            else
            {
                RpcRevivePlayer();
            }
        }

        [ServerRpc(requireOwnership: false)]
        private void RpcRevivePlayer(RPCInfo info = default)
        {
            ExecuteRevive(info.sender);
        }

        private void ExecuteRevive(PlayerID reviver)
        {
            if (!IsDown.value && (!_healthComponent || !_healthComponent.IsDead)) return;

            if (PlayerIdentifier.TryGetPlayer(reviver, out var reviverPlayer))
            {
                // Only validate distance and teammate downed state if another player is reviving us
                if (reviverPlayer.transform.root != transform.root)
                {
                    float dist = Vector3.Distance(reviverPlayer.transform.position, BodyPosition);
                    if (dist > _maxDragDistance) return;

                    var reviverDowned = reviverPlayer.GetComponentInParent<PlayerDownedState>();
                    if (reviverDowned && reviverDowned.IsDown.value) return;
                }
            }

            DraggedByPlayer.value = null;
            IsDown.value = false;

            if (_healthComponent)
            {
                float hp = Mathf.Max(1f, _healthComponent.MaxHealth * _reviveHealthPercent);
                _healthComponent.Revive(hp);
            }
        }

        public void RequestDirectRevive(float? healthPercent = null)
        {
            bool isMatchHost = isServer || !NetworkManager.main || NetworkManager.main.isOffline;
            if (isMatchHost)
            {
                ExecuteDirectRevive(healthPercent);
            }
            else
            {
                RpcDirectRevive(healthPercent ?? -1f);
            }
        }

        [ServerRpc(requireOwnership: false)]
        private void RpcDirectRevive(float healthPercent = -1f)
        {
            ExecuteDirectRevive(healthPercent < 0 ? null : healthPercent);
        }

        public void ExecuteDirectRevive(float? healthPercent = null)
        {
            DraggedByPlayer.value = null;
            IsDown.value = false;

            if (_healthComponent)
            {
                float pct = healthPercent ?? _reviveHealthPercent;
                float hp = Mathf.Clamp(_healthComponent.MaxHealth * pct, 1f, _healthComponent.MaxHealth);
                _healthComponent.Revive(hp);
            }
        }

        public void RequestDown()
        {
            bool isMatchHost = isServer || !NetworkManager.main || NetworkManager.main.isOffline;
            if (isMatchHost)
            {
                ExecuteDowned();
            }
            else
            {
                RpcRequestDown();
            }
        }

        [ServerRpc(requireOwnership: false)]
        private void RpcRequestDown()
        {
            ExecuteDowned();
        }

        public void RequestSetBleedOutTimer(float seconds)
        {
            bool isMatchHost = isServer || !NetworkManager.main || NetworkManager.main.isOffline;
            if (isMatchHost)
            {
                BleedOutTimer.value = Mathf.Max(0.1f, seconds);
            }
            else
            {
                RpcRequestSetBleedOutTimer(seconds);
            }
        }

        [ServerRpc(requireOwnership: false)]
        private void RpcRequestSetBleedOutTimer(float seconds)
        {
            BleedOutTimer.value = Mathf.Max(0.1f, seconds);
        }

        public void RequestKill()
        {
            bool isMatchHost = isServer || !NetworkManager.main || NetworkManager.main.isOffline;
            if (isMatchHost)
            {
                ExecuteTrueDeath();
            }
            else
            {
                RpcRequestKill();
            }
        }

        [ServerRpc(requireOwnership: false)]
        private void RpcRequestKill()
        {
            ExecuteTrueDeath();
        }

        public void RequestStartDrag()
        {
            bool isMatchHost = isServer || !NetworkManager.main || NetworkManager.main.isOffline;
            if (isMatchHost)
            {
                ExecuteStartDrag(GetLocalPlayerId() ?? PurrNet.PlayerID.Server);
            }
            else
            {
                RpcRequestStartDrag();
            }
        }

        [ServerRpc(requireOwnership: false)]
        private void RpcRequestStartDrag(RPCInfo info = default)
        {
            ExecuteStartDrag(info.sender);
        }

        private void ExecuteStartDrag(PlayerID sender)
        {
            if (!IsDown.value) return;
            if (DraggedByPlayer.value.HasValue) return;

            if (PlayerIdentifier.TryGetPlayer(sender, out var dragger))
            {
                float dist = Vector3.Distance(dragger.transform.position, BodyPosition);
                if (dist > _maxDragDistance) return;

                var draggerDowned = dragger.GetComponentInParent<PlayerDownedState>();
                if (draggerDowned && draggerDowned.IsDown.value) return;
            }

            DraggedByPlayer.value = sender;
        }

        public void RequestStopDrag()
        {
            bool isMatchHost = isServer || !NetworkManager.main || NetworkManager.main.isOffline;
            if (isMatchHost)
            {
                ExecuteStopDrag(GetLocalPlayerId() ?? PurrNet.PlayerID.Server);
            }
            else
            {
                RpcRequestStopDrag();
            }
        }

        [ServerRpc(requireOwnership: false)]
        private void RpcRequestStopDrag(RPCInfo info = default)
        {
            ExecuteStopDrag(info.sender);
        }

        private void ExecuteStopDrag(PlayerID sender)
        {
            if (DraggedByPlayer.value.HasValue && (DraggedByPlayer.value.Value == sender || sender.isServer))
            {
                DraggedByPlayer.value = null;
            }
        }

        private void ValidateDraggingOnServer()
        {
            if (!DraggedByPlayer.value.HasValue) return;

            if (!PlayerIdentifier.TryGetPlayer(DraggedByPlayer.value.Value, out var dragger))
            {
                DraggedByPlayer.value = null;
                return;
            }

            var draggerHealth = dragger.GetComponentInParent<HealthComponent>();
            var draggerDowned = dragger.GetComponentInParent<PlayerDownedState>();

            if ((draggerHealth && draggerHealth.IsDead) || (draggerDowned && draggerDowned.IsDown.value))
            {
                DraggedByPlayer.value = null;
                return;
            }

            float dist = Vector3.Distance(dragger.transform.position, BodyPosition);
            if (dist > _maxDragDistance + 1.0f)
            {
                DraggedByPlayer.value = null;
            }
        }

        private void UpdateOwnerDragFollow()
        {
            if (!DraggedByPlayer.value.HasValue)
            {
                _locomotionController?.ClearDragTarget();
                return;
            }

            if (PlayerIdentifier.TryGetPlayer(DraggedByPlayer.value.Value, out var dragger))
            {
                _locomotionController?.SetDragTarget(dragger.transform);
            }
            else
            {
                _locomotionController?.ClearDragTarget();
            }
        }

        private void HandleDownedChanged(bool isDown)
        {
            if (isOwner)
            {
                _locomotionController?.SetDowned(isDown);
                if (!isDown)
                {
                    _locomotionController?.ClearDragTarget();
                }

                if (_weaponController)
                {
                    _weaponController.SetDowned(isDown);
                }
            }

            UpdateInteractableCollider(isDown);
            OnDownedStateChanged?.Invoke(isDown);
        }

        private void HandleDraggedByChanged(PlayerID? newDragger)
        {
            var oldDragger = _currentDragger;
            _currentDragger = newDragger;

            var localId = GetLocalPlayerId();

            if (oldDragger.HasValue && localId.HasValue && oldDragger.Value == localId.Value && newDragger != localId)
            {
                if (LocalPlayerContext.Player && LocalPlayerContext.Player.LocomotionController)
                {
                    LocalPlayerContext.Player.LocomotionController.SetDraggingTeammate(false);
                }
            }

            if (newDragger.HasValue && localId.HasValue && newDragger.Value == localId.Value)
            {
                if (LocalPlayerContext.Player && LocalPlayerContext.Player.LocomotionController)
                {
                    LocalPlayerContext.Player.LocomotionController.SetDraggingTeammate(true);
                }
            }

            if (isOwner && !newDragger.HasValue)
            {
                _locomotionController?.ClearDragTarget();
            }

            OnDraggedByChanged?.Invoke(newDragger);
        }

        private void UpdateInteractableCollider(bool? downedOverride = null)
        {
            if (!_playerDownedInteractable) return;

            if (isOwner)
            {
                _playerDownedInteractable.Disable();
            }
            else
            {
                bool isDown = downedOverride ?? IsDown.value;
                if (isDown)
                    _playerDownedInteractable.Enable();
                else
                    _playerDownedInteractable.Disable();
            }
        }

        private void ClearLocalDraggingIfTarget()
        {
            var localId = GetLocalPlayerId();
            if (_currentDragger.HasValue && localId.HasValue && _currentDragger.Value == localId.Value)
            {
                if (LocalPlayerContext.Player && LocalPlayerContext.Player.LocomotionController)
                {
                    LocalPlayerContext.Player.LocomotionController.SetDraggingTeammate(false);
                }
            }
        }
    }
}
