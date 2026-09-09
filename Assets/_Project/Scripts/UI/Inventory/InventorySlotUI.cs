using System;
using Project.Combat;
using Project.Player.Loot;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.UI
{
public class InventorySlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Visual Components")]
    [SerializeField] private Image _rarityBorder;
    [SerializeField] private Image _weaponIcon;
    [SerializeField] private TMP_Text _weaponNameText;
    [SerializeField] private TMP_Text _weaponLevelText;
    [SerializeField] private TMP_Text _weaponTypeText;
    [SerializeField] private GameObject _favoriteBadge;
    [SerializeField] private GameObject _trashBadge;
    [SerializeField] private GameObject _emptyStateRoot;
    [SerializeField] private GameObject _filledStateRoot;
    [SerializeField] private GameObject _selectionHighlight;

    public WeaponInstance CurrentWeapon { get; private set; }
    public int SlotIndex { get; private set; } = -1;
    public bool IsEmpty => CurrentWeapon == null;

    public event Action<InventorySlotUI> OnSlotHovered;
    public event Action<InventorySlotUI> OnSlotUnhovered;
    public event Action<InventorySlotUI> OnSlotClicked;
    public event Action<InventorySlotUI> OnSlotRightClicked;

    private void Awake()
    {
        AutoWireComponents();
    }

    private void AutoWireComponents()
    {
        if (!_weaponNameText)
        {
            var texts = GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in texts)
            {
                if (t.name.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0)
                    _weaponNameText = t;
                else if (t.name.IndexOf("level", StringComparison.OrdinalIgnoreCase) >= 0)
                    _weaponLevelText = t;
                else if (t.name.IndexOf("type", StringComparison.OrdinalIgnoreCase) >= 0)
                    _weaponTypeText = t;
            }
            if (!_weaponNameText && texts.Length > 0) _weaponNameText = texts[0];
        }

        if (!_weaponIcon)
        {
            var images = GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.gameObject == gameObject) continue;
                if (img.name.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0)
                    _weaponIcon = img;
                else if (img.name.IndexOf("border", StringComparison.OrdinalIgnoreCase) >= 0)
                    _rarityBorder = img;
            }
        }

        if (!_selectionHighlight)
        {
            var highlightTrans = transform.Find("Highlight") ?? transform.Find("Selection");
            if (highlightTrans) _selectionHighlight = highlightTrans.gameObject;
        }

        if (_selectionHighlight)
            _selectionHighlight.SetActive(false);
    }

    public void Bind(InventoryItemEntry entry, int index)
    {
        SlotIndex = index;
        CurrentWeapon = entry?.Weapon;

        if (CurrentWeapon == null)
        {
            Clear(index);
            return;
        }

        _emptyStateRoot?.SetActive(false);
        _filledStateRoot?.SetActive(true);

        if (_weaponNameText) _weaponNameText.text = CurrentWeapon.DisplayName;
        if (_weaponLevelText) _weaponLevelText.text = $"Lv.{CurrentWeapon.Level}";
        if (_weaponTypeText && CurrentWeapon.Core) _weaponTypeText.text = CurrentWeapon.Core.WeaponType.ToString();

        Color rarityColor = RarityUtility.GetColor(CurrentWeapon.Rarity);
        if (_rarityBorder) _rarityBorder.color = rarityColor;
        if (_weaponNameText) _weaponNameText.color = rarityColor;

        if (_weaponIcon)
        {
            var brandIcon = CurrentWeapon.Core?.Manufacturer?.BrandIcon;
            if (brandIcon)
            {
                _weaponIcon.sprite = brandIcon;
                _weaponIcon.enabled = true;
            }
            else
            {
                _weaponIcon.enabled = false;
            }
        }

        if (_favoriteBadge) _favoriteBadge.SetActive(entry != null && entry.IsFavorite);
        if (_trashBadge) _trashBadge.SetActive(entry != null && entry.IsTrash);

        if (TryGetComponent<CanvasGroup>(out var cg))
        {
            cg.alpha = (entry != null && entry.IsTrash) ? 0.55f : 1f;
        }
    }

    public void BindWeapon(WeaponInstance weapon, int index)
    {
        if (weapon == null)
        {
            Clear(index);
            return;
        }

        Bind(new InventoryItemEntry(weapon), index);
    }

    public void Clear(int index = -1)
    {
        SlotIndex = index >= 0 ? index : SlotIndex;
        CurrentWeapon = null;

        _emptyStateRoot?.SetActive(true);
        _filledStateRoot?.SetActive(false);

        if (_weaponNameText) _weaponNameText.text = "Empty";
        if (_weaponLevelText) _weaponLevelText.text = "";
        if (_weaponTypeText) _weaponTypeText.text = "";
        if (_weaponIcon) _weaponIcon.enabled = false;
        if (_rarityBorder) _rarityBorder.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        if (_favoriteBadge) _favoriteBadge.SetActive(false);
        if (_trashBadge) _trashBadge.SetActive(false);
        if (_selectionHighlight) _selectionHighlight.SetActive(false);

        if (TryGetComponent<CanvasGroup>(out var cg))
        {
            cg.alpha = 1f;
        }
    }

    public void SetHighlighted(bool highlighted)
    {
        if (_selectionHighlight)
            _selectionHighlight.SetActive(highlighted);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHighlighted(true);
        OnSlotHovered?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHighlighted(false);
        OnSlotUnhovered?.Invoke(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            OnSlotClicked?.Invoke(this);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            OnSlotRightClicked?.Invoke(this);
        }
    }
}
}