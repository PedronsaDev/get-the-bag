using Project.Combat.Weapons;
using Project.Player.Combat;
using TMPro;
using UnityEngine;

namespace Project.UI.Combat
{
public class WeaponAmmoUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _ammoText;
    [SerializeField] private TMP_Text _reloadText;
    private PlayerWeaponController _weapon;
    private bool _subscribed;

    private void Awake()
    {
        if (!_ammoText) _ammoText = GetComponent<TMP_Text>();
    }

    public void Bind(PlayerWeaponController weapon)
    {
        if (ReferenceEquals(_weapon, weapon)) return;
        Unbind();
        _weapon = weapon;
        if (isActiveAndEnabled) Subscribe();
    }

    public void Unbind()
    {
        Unsubscribe();
        _weapon = null;
        Render(default);
    }

    private void OnEnable() => Subscribe();

    private void OnDisable()
    {
        Unsubscribe();
        Render(default);
    }

    private void Subscribe()
    {
        if (_weapon && !_subscribed)
        {
            _weapon.OnEquipmentChanged += Render;
            _subscribed = true;
        }
        Render(_weapon ? _weapon.ConfirmedState : default);
    }

    private void Unsubscribe()
    {
        if (_subscribed && !ReferenceEquals(_weapon, null))
            _weapon.OnEquipmentChanged -= Render;
        _subscribed = false;
    }

    private void Render(WeaponEquipmentState state)
    {
        var weapon = state.ActiveWeapon;
        bool equipped = !string.IsNullOrEmpty(weapon.WeaponId);
        if (_ammoText)
        {
            if (equipped) _ammoText.SetText("{0} / {1}", (float)weapon.MagazineAmmo, (float)weapon.ReserveAmmo);
            else _ammoText.text = string.Empty;
        }
        if (_reloadText)
            _reloadText.text = equipped && state.Action == WeaponActionState.Reloading ? "Reloading…" : string.Empty;
    }
}
}
