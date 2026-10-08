using System;
using Project.Combat.Core;
using Project.Combat.Health;
using Project.Core;
using Project.Core.Player;
using Project.Logging;
using Project.Player.Interaction;
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

    [Header("Settings")] 
    [SerializeField] private float _bleedOutTimer = 45f;
    [SerializeField] private float _reviveHealthPercent = 0.35f;
    [SerializeField] private float _maxDragDistance = 4.5f;

    protected readonly SyncVar<bool> IsDown = new();
    public SyncVar<float> BleedOutTimer = new(45f);
    public SyncVar<PlayerID?> DraggedByPlayer = new();

    private PlayerID? _currentDragger;

    public bool IsBeingDragged => DraggedByPlayer.value.HasValue;

    public Vector3 BodyPosition =>
        _locomotionController ? _locomotionController.transform.position : transform.position;

    public event Action<bool> OnDownedStateChanged;
    public event Action<PlayerID?> OnDraggedByChanged;
    public event Action OnTrueDeath;

    public bool IsDead => !IsDowned && _healthComponent is { IsDead: true };
    public bool IsDowned => IsDown.value;
    public bool IsStanding => !IsDead && !IsDowned;

    private void Awake()
    {
        if (!_healthComponent)
            _healthComponent = GetComponent<HealthComponent>() ??
                               GetComponentInParent<HealthComponent>() ?? GetComponentInChildren<HealthComponent>();

        if (!_locomotionController)
            _locomotionController = GetComponent<LocomotionController>() ??
                                    GetComponentInParent<LocomotionController>() ??
                                    GetComponentInChildren<LocomotionController>();

        if (!_playerDownedInteractable)
            _playerDownedInteractable = GetComponentInChildren<PlayerDownedInteractable>() ??
                                        GetComponent<PlayerDownedInteractable>();

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

        if (isServer)
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
        bool isMatchHost = isServer;

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
        bool isMatchHost = isServer;
        if (!isMatchHost) return;

        if (IsDown.value) return;

        if (IsTeamWiped())
        {
            AppLog.Log(LogChannel.Player, "Team wiped; triggering true death.", this);
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
        if (!isSpawned) return;
        if (isServer)
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
        if (!isServer || !IsDown.value || !_healthComponent ||
            !TryGetLivingInteractor(reviver, out _)) return;
        ReviveServer();
    }

    // Gameplay RPCs send interaction intent. The server resolves the actor and range.
    private bool TryGetLivingInteractor(PlayerID playerId, out PlayerIdentifier player)
    {
        if (!PlayerIdentifier.TryGetPlayer(playerId, out player) || !player) return false;
        var status = player.GetComponentInParent<PlayerDownedState>();
        var health = player.GetComponentInParent<HealthComponent>();
        return status && status != this && status.IsStanding && health && !health.IsDead &&
               (player.BodyTransform.position - BodyPosition).sqrMagnitude <= _maxDragDistance * _maxDragDistance;
    }

    // Administrative/server gameplay APIs. No RPC exposes arbitrary health or timers.
    public bool ReviveServer(float? healthPercent = null)
    {
        if (!isServer || !_healthComponent) return false;
        var fraction = healthPercent ?? _reviveHealthPercent;
        if (!float.IsFinite(fraction) || fraction <= 0f ||
            (!IsDown.value && !_healthComponent.IsDead)) return false;
        if (_healthComponent.IsDead &&
            !_healthComponent.Revive(_healthComponent.MaxHealth * Mathf.Clamp01(fraction))) return false;
        DraggedByPlayer.value = null;
        IsDown.value = false;
        BleedOutTimer.value = 0f;
        return true;
    }

    public void DownServer()
    {
        if (!isServer || IsDown.value || IsDead || !_healthComponent) return;
        _healthComponent.TakeDamage(new DamagePayload(
            _healthComponent.CurrentHealth + _healthComponent.CurrentShield));
    }

    public void SetBleedOutTimerServer(float seconds)
    {
        if (isServer && IsDown.value && float.IsFinite(seconds))
            BleedOutTimer.value = Mathf.Max(0f, seconds);
    }

    public void KillServer()
    {
        if (!isServer || IsDead || !_healthComponent) return;
        if (!_healthComponent.IsDead &&
            !_healthComponent.TakeDamage(new DamagePayload(
                _healthComponent.CurrentHealth + _healthComponent.CurrentShield)).Applied) return;
        // Fatal damage may already have triggered true death through the team-wipe rule.
        if (IsDown.value) ExecuteTrueDeath();
    }

    public void RequestStartDrag()
    {
        if (!isSpawned) return;
        if (isServer)
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
        if (!isServer || !IsDown.value || DraggedByPlayer.value.HasValue ||
            !TryGetLivingInteractor(sender, out _)) return;
        DraggedByPlayer.value = sender;
    }

    public void RequestStopDrag()
    {
        if (!isSpawned) return;
        if (isServer)
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
        if (isServer && DraggedByPlayer.value.HasValue && (DraggedByPlayer.value.Value == sender || sender.isServer))
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

        float dist = Vector3.Distance(dragger.BodyTransform.position, BodyPosition);
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
            _locomotionController?.SetDragTarget(dragger.BodyTransform);
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
