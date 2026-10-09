using Project.Player;
using Project.UI.Combat;
using UnityEngine;

namespace Project.UI.Core
{
[DisallowMultipleComponent]
public sealed class PlayerHUDBinder : MonoBehaviour
{
    [SerializeField] private WeaponAmmoUI _ammoUI;
    [SerializeField] private PlayerHealthUI _healthUI;
    private PlayerFacade _player;

    private void Awake()
    {
        if (!_ammoUI) _ammoUI = GetComponentInChildren<WeaponAmmoUI>(true);
        if (!_healthUI) _healthUI = GetComponentInChildren<PlayerHealthUI>(true);
    }

    private void OnEnable()
    {
        LocalPlayerContext.OnLocalPlayerSpawned += Bind;
        LocalPlayerContext.OnLocalPlayerDespawned += HandleDespawned;
        Bind(LocalPlayerContext.Player);
    }

    private void OnDisable()
    {
        LocalPlayerContext.OnLocalPlayerSpawned -= Bind;
        LocalPlayerContext.OnLocalPlayerDespawned -= HandleDespawned;
        Bind(null);
    }

    private void Bind(PlayerFacade player)
    {
        _player = player;
        if (_ammoUI) _ammoUI.Bind(player ? player.WeaponController : null);
        if (_healthUI) _healthUI.Bind(player ? player.HealthComponent : null);
    }

    private void HandleDespawned(PlayerFacade player)
    {
        if (ReferenceEquals(_player, player)) Bind(null);
    }
}
}
