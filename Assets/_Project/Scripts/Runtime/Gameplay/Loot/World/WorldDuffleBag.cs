using Plugins.Outline;
using Project.Core.Interaction;
using Project.Core.Player;
using Project.Loot.Data;
using PurrNet;
using UnityEngine;

namespace Project.Loot.World
{
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class WorldDuffleBag : NetworkBehaviour, IInteractable
{
    [Header("References")] 
    [SerializeField] private Rigidbody _rigidbody;
    [SerializeField] private Collider _collider;
    [SerializeField] private MeshOutline _outline;

    [Header("Replicated Prompt Data")] 
    [SerializeField] private SyncVar<float> _netWeight = new(0f);
    [SerializeField] private SyncVar<int> _netValue = new(0);
    [SerializeField] private SyncVar<int> _netItemCount = new(0);
    
    [Header("Visual Settings")]
    [SerializeField, ColorUsage(true, true)]
    private Color _outlineColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField, ColorUsage(true, true)] 
    private Color _hoverOutlineColor = new Color(1f, 1f, 1f);

    private DuffleBagSnapshot _snapshot;
    private bool _isClaimed;

    public DuffleBagSnapshot CurrentSnapshot => _snapshot;
    public float Weight => _netWeight.value;
    public int Value => _netValue.value;
    public int ItemCount => _netItemCount.value;

    public string InteractionPrompt =>
        $"[F] to pick up Duffle Bag (${_netValue.value} - {_netWeight.value:F1}kg)";

    public string HoldPrompt => null;
    public string TargetName => "Duffle Bag";
    public string Subtext => $"{_netItemCount.value} items (${_netValue.value})";
    public Color AccentColor => new Color(1f, 0.85f, 0.2f);
    public InteractionMode Mode => InteractionMode.Instant;
    public float HoldDuration => 0f;

    private void Awake()
    {
        if (!_rigidbody) _rigidbody = GetComponent<Rigidbody>();
        if (!_collider) _collider = GetComponentInChildren<Collider>();
        if (!_outline) _outline = GetComponent<MeshOutline>();
    }
    
    public void Initialize(DuffleBagSnapshot snapshot)
    {
        _snapshot = snapshot;
        _netWeight.value = snapshot.TotalWeight;
        _netValue.value = snapshot.TotalValue;
        _netItemCount.value = snapshot.Items?.Count ?? 0;
        _isClaimed = false;
        _outline.OutlineColor = _outlineColor;
    }
    
    public void Throw(Vector3 velocity)
    {
        if (_rigidbody)
        {
            _rigidbody.linearVelocity = velocity;
            _rigidbody.angularVelocity = UnityEngine.Random.insideUnitSphere * 3f;
        }
    }

    public bool CanInteract(IPlayerInteractor interactor, bool isHold = false)
    {
        if (_isClaimed || interactor == null) return false;

        if (interactor.InteractorGameObject.TryGetComponent(out IDuffleBagCarrier carrier))
        {
            return !carrier.HasBag;
        }

        return false;
    }

    public string GetDisabledReason(IPlayerInteractor interactor, bool isHold = false)
    {
        if (interactor != null && interactor.InteractorGameObject.TryGetComponent(out IDuffleBagCarrier carrier))
        {
            if (carrier.HasBag)
                return "Already carrying a duffle bag";
        }

        return null;
    }

    public void OnInteract(IPlayerInteractor interactor, bool isHold)
    {
        if (_isClaimed || interactor == null) return;

        if (NetworkManager.main && NetworkManager.main.isClient && !isServer)
        {
            _isClaimed = true;
            RpcRequestPickup();
        }
        else
        {
            if (interactor.InteractorGameObject.TryGetComponent(out IDuffleBagCarrier carrier))
            {
                if (!carrier.HasBag)
                {
                    _isClaimed = true;
                    carrier.EquipBag(_snapshot);
                    SafeDespawn();
                }
            }
        }
    }

    [ServerRpc(requireOwnership: false)]
    private void RpcRequestPickup(RPCInfo info = default)
    {
        if (_isClaimed) return;

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
            if (!carrier.HasBag)
            {
                _isClaimed = true;
                carrier.EquipBag(_snapshot);
                SafeDespawn();
            }
        }
    }

    public void OnHoverEnter(IPlayerInteractor interactor)
    {
        if (_outline) 
            _outline.OutlineColor = _hoverOutlineColor; 
    }

    public void OnHoverExit(IPlayerInteractor interactor)
    {
        if (_outline) 
            _outline.OutlineColor = _outlineColor;
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