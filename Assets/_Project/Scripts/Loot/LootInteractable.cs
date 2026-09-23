using Plugins.Outline;
using Project.Core.Interaction;
using Project.Core.Player;
using PurrNet;
using UnityEngine;

namespace Project.Loot
{
    /// <summary>
    /// Interactive world entity representing a collectible valuable/contraband item.
    /// Can be stowed directly into a player's worn duffle bag.
    /// </summary>
    [DisallowMultipleComponent]
    public class LootInteractable : NetworkBehaviour, IInteractable
    {
        [Header("References")] 
        [SerializeField] private MeshOutline _outline;
        
        [Header("Item Data")]
        [SerializeField] private LootItemDefinition _itemDefinition;

        private bool _isClaimed;

        public LootItemDefinition ItemDefinition => _itemDefinition;

        public string InteractionPrompt => _itemDefinition 
            ? $"Press [F] to stow {_itemDefinition.DisplayName} (${_itemDefinition.BaseValue} - {_itemDefinition.WeightKg:F1}kg)" 
            : "Press [F] to stow";

        public string HoldPrompt => null;
        public string TargetName => _itemDefinition ? _itemDefinition.DisplayName : "Valuable";
        public string Subtext => _itemDefinition ? $"${_itemDefinition.BaseValue} ({_itemDefinition.WeightKg:F1} kg)" : null;
        public Color AccentColor => new Color(0.35f, 0.85f, 1f);
        public InteractionMode Mode => InteractionMode.Instant;
        public float HoldDuration => 0f;

        private void Awake()
        {
            if (!_outline) 
                _outline = GetComponent<MeshOutline>();
        }

        public void Initialize(LootItemDefinition definition)
        {
            _itemDefinition = definition;
            _isClaimed = false;
        }

        public bool CanInteract(IPlayerInteractor interactor, bool isHold = false)
        {
            if (_isClaimed || interactor == null || !_itemDefinition) return false;

            if (interactor.InteractorGameObject.TryGetComponent(out IDuffleBagCarrier carrier))
            {
                if (!carrier.HasBag) return false;
                return carrier.CurrentWeight + _itemDefinition.WeightKg <= carrier.MaxCapacityKg;
            }

            return false;
        }

        public string GetDisabledReason(IPlayerInteractor interactor, bool isHold = false)
        {
            if (interactor != null && interactor.InteractorGameObject.TryGetComponent(out IDuffleBagCarrier carrier))
            {
                if (!carrier.HasBag)
                    return "No Duffle Bag equipped";

                if (_itemDefinition && carrier.CurrentWeight + _itemDefinition.WeightKg > carrier.MaxCapacityKg)
                    return $"Bag Full ({carrier.CurrentWeight:F1}/{carrier.MaxCapacityKg:F1}kg)";
            }

            return "Cannot stow item";
        }

        public void OnInteract(IPlayerInteractor interactor, bool isHold)
        {
            if (_isClaimed || interactor == null || !_itemDefinition) return;

            if (NetworkManager.main && NetworkManager.main.isClient && !isServer)
            {
                _isClaimed = true;
                RpcRequestStow();
            }
            else
            {
                if (interactor.InteractorGameObject.TryGetComponent(out IDuffleBagCarrier carrier))
                {
                    if (carrier.TryStowItem(_itemDefinition))
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
            if (_isClaimed || !_itemDefinition) return;

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
                if (carrier.TryStowItem(_itemDefinition))
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
