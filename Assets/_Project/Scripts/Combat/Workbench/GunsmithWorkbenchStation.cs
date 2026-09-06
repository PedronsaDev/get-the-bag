using System;
using Project.Core.Interaction;
using PurrNet;
using UnityEngine;

namespace Project.Combat
{
    [RequireComponent(typeof(Collider))]
    public class GunsmithWorkbenchStation : NetworkBehaviour, IInteractable
    {
        [Header("Interaction Settings")]
        [SerializeField] private string _interactionPrompt = "[F] Modify Weapons";
        [SerializeField] private string _targetName = "Gunsmith Workbench";
        [SerializeField] private string _subtext = "Swap parts between equipped weapons";
        [SerializeField] private Color _accentColor = new Color(0.2f, 0.8f, 1f);

        public static event Action<GunsmithWorkbenchStation, GameObject> OnWorkbenchOpened;
        private readonly SyncVar<bool> _isOpen = new(false);

        public bool IsInUse => _isOpen.value;

        public string InteractionPrompt => _isOpen.value ? "In Use" : _interactionPrompt;
        public string HoldPrompt => string.Empty;
        public string TargetName => _targetName;
        public string Subtext => _isOpen.value ? "Another player is modifying weapons" : _subtext;
        public Color AccentColor => _accentColor;
        public InteractionMode Mode => InteractionMode.Instant;
        public float HoldDuration => 0f;

        private void Awake()
        {
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer >= 0)
            {
                gameObject.layer = interactableLayer;
            }
        }

        public bool CanInteract(IPlayerInteractor interactor)
        {
            return !_isOpen.value && interactor != null;
        }

        public void OnInteract(IPlayerInteractor interactor, bool isHold)
        {
            if (_isOpen.value) return;

            if (interactor != null && interactor.InteractorGameObject)
            {
                SetOpenState(true);
                OnWorkbenchOpened?.Invoke(this, interactor.InteractorGameObject);
            }
        }

        public void CloseWorkbench()
        {
            SetOpenState(false);
        }

        private PlayerID? _currentOccupant;

        private void Update()
        {
            if (!isServer || !_isOpen.value) return;

            if (_currentOccupant.HasValue && NetworkManager.main != null && NetworkManager.main.playerModule != null)
            {
                bool stillConnected = false;
                var players = NetworkManager.main.playerModule.players;
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i] == _currentOccupant.Value)
                    {
                        stillConnected = true;
                        break;
                    }
                }

                if (!stillConnected)
                {
                    _isOpen.value = false;
                    _currentOccupant = null;
                }
            }
        }

        private void SetOpenState(bool isOpen)
        {
            if (NetworkManager.main != null && !isServer)
            {
                RpcRequestSetOpen(isOpen);
            }
            else
            {
                _isOpen.value = isOpen;
                if (!isOpen) _currentOccupant = null;
            }
        }

        [ServerRpc(requireOwnership: false)]
        private void RpcRequestSetOpen(bool isOpen, RPCInfo info = default)
        {
            if (isOpen)
            {
                if (_isOpen.value) return; // Station already occupied
                _isOpen.value = true;
                _currentOccupant = info.sender;
            }
            else
            {
                if (_currentOccupant.HasValue && info.sender != _currentOccupant.Value)
                    return; // Ignore close from non-occupants

                _isOpen.value = false;
                _currentOccupant = null;
            }
        }

        private void OnDisable()
        {
            if (isServer && _isOpen.value)
            {
                _isOpen.value = false;
                _currentOccupant = null;
            }
        }

        public void OnHoverEnter(IPlayerInteractor interactor) { }
        public void OnHoverExit(IPlayerInteractor interactor) { }
    }
}