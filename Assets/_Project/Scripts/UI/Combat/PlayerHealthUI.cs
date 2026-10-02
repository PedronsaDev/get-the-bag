using Project.Combat.Core;
using Project.Combat.Health;
using Project.Player;
using UnityEngine;

namespace Project.UI.Player
{
public class PlayerHealthUI : MonoBehaviour
{
    [Header("References")]
    
    private HealthComponent _health;

    private void OnEnable()
    {
        if (LocalPlayerContext.HasLocalPlayer && LocalPlayerContext.Player)
            BindHealthComponent(LocalPlayerContext.Player.HealthComponent);

        LocalPlayerContext.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;
        LocalPlayerContext.OnLocalPlayerDespawned += HandleLocalPlayerDespawned;
    }
    
    private void OnDisable()
    {
        LocalPlayerContext.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
        LocalPlayerContext.OnLocalPlayerDespawned -= HandleLocalPlayerDespawned;
        UnbindHealthComponent();
    }
    
    private void HandleLocalPlayerDespawned(PlayerFacade player)
    {
        if (player) 
            UnbindHealthComponent();
    }

    private void HandleLocalPlayerSpawned(PlayerFacade player)
    {
        if (player) 
            BindHealthComponent(player.HealthComponent);
    }
    
    private void BindHealthComponent(HealthComponent healthComponent)
    {
        _health  = healthComponent;
        if (_health)
        {
            _health.OnHealthChanged += HandleHealthChanged;
            _health.OnShieldChanged += HandleShieldChanged;
            _health.OnDeath += HandlePlayerDeath;
        }
    }

    private void UnbindHealthComponent()
    {
        if (_health)
        {
            _health.OnHealthChanged -= HandleHealthChanged;
            _health.OnShieldChanged -= HandleShieldChanged;
            _health.OnDeath -= HandlePlayerDeath;
        }
        _health = null;
    }
    
    private void HandleHealthChanged(float current, float max)
    {
        
    }

    private void HandleShieldChanged(float current, float max)
    {
        
    }
    
    private void HandlePlayerDeath(DamageResult result)
    {
        
    }
}
}