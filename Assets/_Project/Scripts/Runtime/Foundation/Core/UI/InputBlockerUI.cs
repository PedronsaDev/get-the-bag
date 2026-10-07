using System;
using UnityEngine;
using UnityEngine.Events;

namespace Project.Core.UI
{
    /// <summary>
    /// Base component for UI screens, modals, or menus that block player input when open.
    /// Handles registration with UIBlockerSystem automatically on Open/OnEnable and unregistration on Close/OnDisable/OnDestroy.
    /// </summary>
    [DisallowMultipleComponent]
    public class InputBlockerUI : MonoBehaviour, IInputBlocker
    {
        [Header("Blocker Settings")]
        [Tooltip("Human-readable identifier for debugging and stack inspection.")]
        [SerializeField] private string _blockerId;

        [Tooltip("If true, prevents player movement (WASD, sprint, crouch, jump) while open.")]
        [SerializeField] private bool _blocksMovement = true;

        [Tooltip("If true, prevents camera look rotation while open.")]
        [SerializeField] private bool _blocksLook = true;

        [Tooltip("If true, prevents weapon firing, abilities, reloading, weapon switching, and interactions.")]
        [SerializeField] private bool _blocksActions = true;

        [Tooltip("If true, unlocks hardware cursor and makes it visible.")]
        [SerializeField] private bool _unlocksCursor = true;

        [Tooltip("If true, pressing Cancel (Escape) will invoke Close() on this blocker.")]
        [SerializeField] private bool _closesOnCancel = true;

        [Tooltip("If true, pressing the Inventory toggle key (Tab) will invoke Close() if this blocker is open.")]
        [SerializeField] private bool _closesOnInventoryToggle;

        [Header("Lifecycle Options")]
        [Tooltip("If true, automatically registers with UIBlockerSystem when this component is enabled in the scene.")]
        [SerializeField] protected bool _registerOnEnable = true;

        [Tooltip("If true, automatically unregisters with UIBlockerSystem when this component is disabled.")]
        [SerializeField] private bool _unregisterOnDisable = true;

        [Header("Events")]
        public UnityEvent OnOpened;
        public UnityEvent OnClosed;

        public string BlockerId => string.IsNullOrEmpty(_blockerId) ? gameObject.name : _blockerId;
        public bool BlocksMovement => _blocksMovement;
        public bool BlocksLook => _blocksLook;
        public bool BlocksActions => _blocksActions;
        public bool UnlocksCursor => _unlocksCursor;
        public bool ClosesOnCancel => _closesOnCancel;
        public bool ClosesOnInventoryToggle => _closesOnInventoryToggle;

        public bool IsOpen { get; private set; }

        protected virtual void OnEnable()
        {
            UIInput.OnToggleInventory += HandleToggleInventory;

            if (_registerOnEnable)
            {
                Open();
            }
        }

        protected virtual void OnDisable()
        {
            UIInput.OnToggleInventory -= HandleToggleInventory;

            if (_unregisterOnDisable)
            {
                Close();
            }
        }

        private void HandleToggleInventory()
        {
            if (_closesOnInventoryToggle && IsOpen)
            {
                Close();
            }
        }

        protected virtual void OnDestroy()
        {
            if (IsOpen)
            {
                UIBlockerSystem.Unregister(this);
                IsOpen = false;
            }
        }

        /// <summary>
        /// Opens this UI and registers it with UIBlockerSystem.
        /// </summary>
        public virtual void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            UIBlockerSystem.Register(this);
            OnOpened?.Invoke();
        }

        /// <summary>
        /// Closes this UI and unregisters it from UIBlockerSystem.
        /// </summary>
        public virtual void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            UIBlockerSystem.Unregister(this);
            OnClosed?.Invoke();
        }
    }
}