using System;
using PrimeTween;
using Project.Combat;
using Project.Core.UI;
using Project.Player;
using Project.Player.Loot;
using Project.Player.Weapons;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI
{
public class InventoryUI : InputBlockerUI
{
    [Header("References")] 
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private GameObject _rootContainer;
    [SerializeField] private InventorySlotUI _inventorySlotPrefab;
    [SerializeField] private RectTransform _slotsContainer;

    [Header("Equipped Slots (Optional)")] 
    [SerializeField] private InventorySlotUI _slot1UI;
    [SerializeField] private InventorySlotUI _slot2UI;

    [Header("Details / Tooltip Panel (Optional)")] 
    [SerializeField] private GameObject _detailsPanel;
    [SerializeField] private TMP_Text _detailsNameText;
    [SerializeField] private TMP_Text _detailsTypeText;
    [SerializeField] private TMP_Text _detailsStatsText;
    [SerializeField] private TMP_Text _detailsElementText;

    [Header("Capacity & Status (Optional)")] 
    [SerializeField] private TMP_Text _capacityText;

    [Header("Visual Settings")] 
    [SerializeField] private float _fadeDuration = 0.2f;
    
    private InventorySlotUI[] _inventorySlots;
    private PlayerFacade _localPlayer;
    private PlayerInventoryController _inventoryController;
    private int _hoveredIndex = -1;
    private Tween _fadeTween;

    private void Awake()
    {
        if (!_canvasGroup)
            _canvasGroup = GetComponent<CanvasGroup>();

        if (!_slotsContainer)
        {
            var grid = GetComponentInChildren<GridLayoutGroup>();
            if (grid) _slotsContainer = grid.GetComponent<RectTransform>();
            else _slotsContainer = transform as RectTransform;
        }

        if (!_slot1UI || !_slot2UI)
        {
            var slots = GetComponentsInChildren<InventorySlotUI>(true);
            foreach (var slot in slots)
            {
                if (slot.transform.parent &&
                    slot.transform.parent.name.IndexOf("Equipped", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (!_slot1UI) _slot1UI = slot;
                    else if (!_slot2UI && slot != _slot1UI) _slot2UI = slot;
                }
            }
        }

        if (_detailsPanel)
            _detailsPanel.SetActive(false);
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        UIInput.OnToggleInventory += HandleToggleInventoryInput;
        UIInput.OnDropItem += HandleDropItemInput;
        UIInput.OnFavoriteItem += HandleFavoriteItemInput;
        UIInput.OnTrashItem += HandleTrashItemInput;
        UIInput.OnDropAllTrash += HandleDropAllTrashInput;

        if (LocalPlayerContext.HasLocalPlayer && LocalPlayerContext.Player)
            BindInventoryController(LocalPlayerContext.Player);

        LocalPlayerContext.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;
        LocalPlayerContext.OnLocalPlayerDespawned += HandleLocalPlayerDespawned;

        if (_slot1UI)
        {
            _slot1UI.OnSlotClicked += HandleEquippedSlot1Clicked;
            _slot1UI.OnSlotHovered += HandleSlotHovered;
            _slot1UI.OnSlotUnhovered += HandleSlotUnhovered;
        }

        if (_slot2UI)
        {
            _slot2UI.OnSlotClicked += HandleEquippedSlot2Clicked;
            _slot2UI.OnSlotHovered += HandleSlotHovered;
            _slot2UI.OnSlotUnhovered += HandleSlotUnhovered;
        }
        
        HideInstantly();
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        UIInput.OnToggleInventory -= HandleToggleInventoryInput;
        UIInput.OnDropItem -= HandleDropItemInput;
        UIInput.OnFavoriteItem -= HandleFavoriteItemInput;
        UIInput.OnTrashItem -= HandleTrashItemInput;
        UIInput.OnDropAllTrash -= HandleDropAllTrashInput;

        LocalPlayerContext.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
        LocalPlayerContext.OnLocalPlayerDespawned -= HandleLocalPlayerDespawned;

        if (_slot1UI)
        {
            _slot1UI.OnSlotClicked -= HandleEquippedSlot1Clicked;
            _slot1UI.OnSlotHovered -= HandleSlotHovered;
            _slot1UI.OnSlotUnhovered -= HandleSlotUnhovered;
        }

        if (_slot2UI)
        {
            _slot2UI.OnSlotClicked -= HandleEquippedSlot2Clicked;
            _slot2UI.OnSlotHovered -= HandleSlotHovered;
            _slot2UI.OnSlotUnhovered -= HandleSlotUnhovered;
        }

        UnbindInventoryController();
        HideInstantly();
    }

    private void HandleLocalPlayerSpawned(PlayerFacade player) => BindInventoryController(player);

    private void HandleLocalPlayerDespawned(PlayerFacade player) => UnbindInventoryController();

    private void BindInventoryController(PlayerFacade player)
    {
        if (_localPlayer && _localPlayer == player)
        {
            return;
        }

        UnbindInventoryController();

        _localPlayer = player;
        _inventoryController = _localPlayer.InventoryController;

        if (_inventoryController)
        {
            _inventoryController.OnEquippedSlotChanged += HandleEquippedSlotChanged;
            _inventoryController.OnInventoryUpdated += HandleInventoryControllerUpdated;

            InitializeInventorySlots();
            HandleInventoryControllerUpdated();
        }
    }

    private void InitializeInventorySlots()
    {
        if (!_inventoryController)
            return;

        var prefab = _inventorySlotPrefab ? _inventorySlotPrefab : _slot1UI;
        if (!prefab)
            return;

        Transform container = _slotsContainer ? _slotsContainer : transform;

        if (_inventorySlots != null)
        {
            foreach (var slot in _inventorySlots)
            {
                if (slot && slot != _slot1UI && slot != _slot2UI)
                {
                    slot.OnSlotHovered -= HandleSlotHovered;
                    slot.OnSlotUnhovered -= HandleSlotUnhovered;
                    slot.OnSlotClicked -= HandleSlotClicked;
                    Destroy(slot.gameObject);
                }
            }
        }

        int capacity = _inventoryController.BackpackCapacity;
        _inventorySlots = new InventorySlotUI[capacity];

        for (var i = 0; i < capacity; i++)
        {
            var slotUI = Instantiate(prefab, container);
            slotUI.gameObject.SetActive(true);
            slotUI.OnSlotHovered += HandleSlotHovered;
            slotUI.OnSlotUnhovered += HandleSlotUnhovered;
            slotUI.OnSlotClicked += HandleSlotClicked;

            var item = i < _inventoryController.Backpack.Count ? _inventoryController.Backpack[i] : null;
            slotUI.Bind(item, i);
            _inventorySlots[i] = slotUI;
        }
    }

    private void UnbindInventoryController()
    {
        if (!_inventoryController)
            return;

        _inventoryController.OnEquippedSlotChanged -= HandleEquippedSlotChanged;
        _inventoryController.OnInventoryUpdated -= HandleInventoryControllerUpdated;
        _inventoryController = null;
        _localPlayer = null;
    }

    private void HandleEquippedSlotChanged(int slot0Based, WeaponInstance weapon)
    {
        if (slot0Based == 0 && _slot1UI)
        {
            _slot1UI.BindWeapon(weapon, 0);
        }
        else if (slot0Based == 1 && _slot2UI)
        {
            _slot2UI.BindWeapon(weapon, 1);
        }
    }

    private void HandleInventoryControllerUpdated()
    {
        if (!_inventoryController) return;

        if (_inventorySlots == null || _inventorySlots.Length != _inventoryController.BackpackCapacity)
        {
            InitializeInventorySlots();
        }

        if (_inventorySlots != null)
        {
            for (var i = 0; i < _inventorySlots.Length; i++)
            {
                if (!_inventorySlots[i]) continue;
                var item = i < _inventoryController.Backpack.Count ? _inventoryController.Backpack[i] : null;
                _inventorySlots[i].Bind(item, i);
            }
        }

        if (_slot1UI && _inventoryController.EquippedSlots.Count > 0)
            _slot1UI.BindWeapon(_inventoryController.EquippedSlots[0], 0);

        if (_slot2UI && _inventoryController.EquippedSlots.Count > 1)
            _slot2UI.BindWeapon(_inventoryController.EquippedSlots[1], 1);

        if (_capacityText)
            _capacityText.text = $"{_inventoryController.BackpackCount} / {_inventoryController.BackpackCapacity}";

        if (_hoveredIndex >= 0 && _hoveredIndex < _inventoryController.BackpackCount)
        {
            UpdateDetailsPanel(_inventoryController.Backpack[_hoveredIndex]?.Weapon);
        }
        else if (_hoveredIndex < 0)
        {
            UpdateDetailsPanel(null);
        }
    }

    private void HandleSlotHovered(InventorySlotUI slot)
    {
        if (!slot) return;
        _hoveredIndex = slot.SlotIndex;
        UpdateDetailsPanel(slot.CurrentWeapon);
    }

    private void HandleSlotUnhovered(InventorySlotUI slot)
    {
        if (!slot) return;
        if (_hoveredIndex == slot.SlotIndex)
        {
            _hoveredIndex = -1;
            UpdateDetailsPanel(null);
        }
    }

    private void HandleSlotClicked(InventorySlotUI slot)
    {
        if (!slot || slot.IsEmpty || !_inventoryController) return;

        var pwc = _localPlayer ? _localPlayer.GetComponentInChildren<PlayerWeaponController>() : null;
        int activeSlot0Based = (pwc && pwc.ActiveSlotIndex == 2) ? 1 : 0;

        _inventoryController.EquipToSlot(activeSlot0Based, slot.CurrentWeapon);
    }

    private void HandleEquippedSlot1Clicked(InventorySlotUI slot)
    {
        var pwc = _localPlayer ? _localPlayer.GetComponentInChildren<PlayerWeaponController>() : null;
        if (pwc && pwc.Slot1Weapon != null)
        {
            pwc.EquipSlot(1);
        }
    }

    private void HandleEquippedSlot2Clicked(InventorySlotUI slot)
    {
        var pwc = _localPlayer ? _localPlayer.GetComponentInChildren<PlayerWeaponController>() : null;
        if (pwc && pwc.Slot2Weapon != null)
        {
            pwc.EquipSlot(2);
        }
    }

    private void HandleToggleInventoryInput()
    {
        if (!_inventoryController) return;

        if (IsOpen)
            Close();
        else if (!UIBlockerSystem.HasActiveBlocker)
            Open();
    }

    private void HandleDropItemInput()
    {
        if (!IsOpen || !_inventoryController) return;
        if (_hoveredIndex >= 0 && _hoveredIndex < _inventoryController.BackpackCount)
        {
            _inventoryController.DropFromBackpack(_hoveredIndex);
        }
    }

    private void HandleFavoriteItemInput()
    {
        if (!IsOpen || !_inventoryController) return;
        if (_hoveredIndex >= 0 && _hoveredIndex < _inventoryController.BackpackCount)
        {
            _inventoryController.ToggleFavorite(_hoveredIndex);
        }
    }

    private void HandleTrashItemInput()
    {
        if (!IsOpen || !_inventoryController) return;
        if (_hoveredIndex >= 0 && _hoveredIndex < _inventoryController.BackpackCount)
        {
            _inventoryController.ToggleTrash(_hoveredIndex);
        }
    }

    private void HandleDropAllTrashInput()
    {
        if (!IsOpen || !_inventoryController) return;
        _inventoryController.DropAllTrash();
    }

    private void UpdateDetailsPanel(WeaponInstance weapon)
    {
        if (!_detailsPanel) return;

        if (weapon == null)
        {
            _detailsPanel.SetActive(false);
            return;
        }

        _detailsPanel.SetActive(true);

        if (_detailsNameText)
        {
            _detailsNameText.text = weapon.DisplayName;
            _detailsNameText.color = RarityUtility.GetColor(weapon.Rarity);
        }

        if (_detailsTypeText)
        {
            string mfg = weapon.Core?.Manufacturer?.DisplayName ?? "Custom";
            _detailsTypeText.text = $"{weapon.Rarity} {weapon.Core?.WeaponType} • Lv.{weapon.Level} • {mfg}";
        }

        if (_detailsStatsText)
        {
            var s = weapon.CompiledStats;
            float accuracy = Mathf.Clamp(100f - s.BaseSpread * 10f, 10f, 100f);
            _detailsStatsText.text =
                $"Damage: {s.Damage:F0}\nFire Rate: {s.FireRate} RPM\nMagazine: {s.MagazineSize}\nAccuracy: {accuracy:F0}%\nReload: {s.ReloadSpeed:F1}s";
        }

        if (_detailsElementText)
        {
            var s = weapon.CompiledStats;
            _detailsElementText.text = s.Element != DamageType.Physical ? s.Element.ToString() : "Kinetic";
            _detailsElementText.color = CombatVisualHelper.GetElementColor(s.Element);
        }
    }

    public override void Open()
    {
        base.Open();
        Show();
        HandleInventoryControllerUpdated();
    }

    public override void Close()
    {
        base.Close();
        Hide();
        _hoveredIndex = -1;
        UpdateDetailsPanel(null);
    }

    private void Show()
    {
        if (!_canvasGroup) return;

        _fadeTween.Stop();
        _rootContainer.SetActive(true);

        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;

        _fadeTween = Tween.Alpha(_canvasGroup, 0f, 1f, _fadeDuration);
    }

    private void Hide()
    {
        if (!_canvasGroup) return;

        _fadeTween.Stop();

        if (!gameObject.activeInHierarchy)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            if (_rootContainer) _rootContainer.SetActive(false);
            return;
        }

        _fadeTween = Tween.Alpha(_canvasGroup, _canvasGroup.alpha, 0f, _fadeDuration).OnComplete(() =>
        {
            if (_rootContainer) _rootContainer.SetActive(false);
        });

        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }

    private void HideInstantly()
    {
        base.Close();
        _fadeTween.Stop();
        if (!_canvasGroup) return;

        if (_rootContainer) 
            _rootContainer.SetActive(false);

        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
        _canvasGroup.alpha = 0f;
    }
}
}