using Project.Combat.Health;
using TMPro;
using UnityEngine;

namespace Project.UI.Combat
{
public class PlayerHealthUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _healthText;
    [SerializeField] private TMP_Text _shieldText;
    private HealthComponent _health;
    private bool _subscribed;

    public void Bind(HealthComponent health)
    {
        if (ReferenceEquals(_health, health)) return;
        Unbind();
        _health = health;
        if (isActiveAndEnabled) Subscribe();
    }

    public void Unbind()
    {
        Unsubscribe();
        _health = null;
        Clear();
    }

    private void OnEnable() => Subscribe();

    private void OnDisable()
    {
        Unsubscribe();
        Clear();
    }

    private void Subscribe()
    {
        if (!_health) { Clear(); return; }
        if (!_subscribed)
        {
            _health.OnHealthChanged += RenderHealth;
            _health.OnShieldChanged += RenderShield;
            _subscribed = true;
        }
        RenderHealth(_health.CurrentHealth, _health.MaxHealth);
        RenderShield(_health.CurrentShield, _health.MaxShield);
    }

    private void Unsubscribe()
    {
        if (_subscribed && !ReferenceEquals(_health, null))
        {
            _health.OnHealthChanged -= RenderHealth;
            _health.OnShieldChanged -= RenderShield;
        }
        _subscribed = false;
    }

    private void RenderHealth(float current, float max)
    {
        if (_healthText) _healthText.SetText("{0:0} / {1:0}", current, max);
    }

    private void RenderShield(float current, float max)
    {
        if (!_shieldText) return;
        if (max > 0) _shieldText.SetText("{0:0} / {1:0}", current, max);
        else _shieldText.text = string.Empty;
    }

    private void Clear()
    {
        if (_healthText) _healthText.text = string.Empty;
        if (_shieldText) _shieldText.text = string.Empty;
    }
}
}
