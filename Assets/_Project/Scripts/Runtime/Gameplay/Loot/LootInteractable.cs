using Plugins.Outline;
using Project.Core.Interaction;
using Project.Core.Player;
using Project.Loot.Data;
using PurrNet;
using UnityEngine;

namespace Project.Loot
{
[DisallowMultipleComponent]
public class LootInteractable : NetworkBehaviour, IInteractable
{
    [Header("References")] 
    [SerializeField] private MeshOutline _outline;
    
    [Header("Item Data")] 
    [SerializeField] private LootDefinition _definition;
    
    private bool _isClaimed;

    public LootDefinition Definition => _definition;

    public string InteractionPrompt => _definition
        ? $"[F] to stow {_definition.Name} (${_definition.BaseValue} - {_definition.WeightKg:F1}kg)"
        : "[F] to stow";

    public string HoldPrompt => null;
    public string TargetName => _definition ? _definition.Name : "Valuable";

    public string Subtext =>
        _definition ? $"${_definition.BaseValue} ({_definition.WeightKg:F1} kg)" : null;

    public Color AccentColor => new Color(0.35f, 0.85f, 1f);
    public InteractionMode Mode => InteractionMode.Instant;
    public float HoldDuration => 0f;

    private void Awake()
    {
        if (!_outline)
            _outline = GetComponent<MeshOutline>();
    }

    public void Initialize(LootDefinition definition)
    {
        _definition = definition;
        _isClaimed = false;
    }

    public bool CanInteract(IPlayerInteractor interactor, bool isHold = false)
    {
        if (_isClaimed || interactor == null || !_definition) return false;

        if (interactor.InteractorGameObject.TryGetComponent(out IDuffleBagCarrier carrier))
        {
            if (!carrier.HasBag) return false;
            return carrier.CurrentWeight + _definition.WeightKg <= carrier.MaxCapacityKg;
        }

        return false;
    }

    public string GetDisabledReason(IPlayerInteractor interactor, bool isHold = false)
    {
        if (interactor != null && interactor.InteractorGameObject.TryGetComponent(out IDuffleBagCarrier carrier))
        {
            if (!carrier.HasBag)
                return "No Duffle Bag equipped";

            if (_definition && carrier.CurrentWeight + _definition.WeightKg > carrier.MaxCapacityKg)
                return $"Bag Full ({carrier.CurrentWeight:F1}/{carrier.MaxCapacityKg:F1}kg)";
        }

        return "Cannot stow item";
    }

    public void OnInteract(IPlayerInteractor interactor, bool isHold)
    {
        if (_isClaimed || interactor == null || !_definition) return;

        if (NetworkManager.main && NetworkManager.main.isClient && !isServer)
        {
            _isClaimed = true;
            RpcRequestStow();
        }
        else
        {
            if (interactor.InteractorGameObject.TryGetComponent(out IDuffleBagCarrier carrier))
            {
                if (carrier.TryStowItem(_definition))
                {
                    _isClaimed = true;
                    SafeDespawn();
                }
            }
        }
    }

    [ServerRpc(requireOwnership: false)]
    private void RpcRequestStow(RPCInfo info = default)
    {
        if (_isClaimed || !_definition) return;

        PlayerIdentifier senderPlayer = null;
        for (int i = 0; i < PlayerIdentifier.ActivePlayers.Count; i++)
        {
            var player = PlayerIdentifier.ActivePlayers[i];
            if (player && player.PlayerID == info.sender)
            {
                senderPlayer = player;
                break;
            }
        }

        if (senderPlayer && senderPlayer.TryGetComponent(out IDuffleBagCarrier carrier))
        {
            if (carrier.TryStowItem(_definition))
            {
                _isClaimed = true;
                SafeDespawn();
            }
        }
    }

    public void OnHoverEnter(IPlayerInteractor interactor)
    {
        if (_outline)
            _outline.enabled = true;
    }

    public void OnHoverExit(IPlayerInteractor interactor)
    {
        if (_outline)
            _outline.enabled = false;
    }

    private void SafeDespawn()
    {
        if (isSpawned && NetworkManager.main)
        {
            Despawn();
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
}