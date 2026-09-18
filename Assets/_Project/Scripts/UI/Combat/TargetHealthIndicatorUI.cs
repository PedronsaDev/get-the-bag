using Project.Combat;
using Project.Player;
using Project.Player.Detection;
using UnityEngine;

namespace Project.UI.Interaction
{
    public class TargetHealthIndicatorUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _rootContainer;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private ProgressBarUI _shieldBar;
        [SerializeField] private ProgressBarUI _healthBar;

        [Header("Visual Polish")]
        [SerializeField] private float _lingerDuration = 0.0f;
        [SerializeField] private float _fadeSpeed = 10.0f;

        private HealthComponent _currentTarget;
        private PlayerTargetDetector _detector;
        private bool _hasTarget = false;
        private float _targetAlpha = 0f;
        private float _lastSeenTime = -100f;

        private void Awake()
        {
            if (_canvasGroup)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
            if (_rootContainer && _rootContainer != gameObject && _rootContainer.activeSelf)
                _rootContainer.SetActive(false);
        }

        private void OnEnable()
        {
            HideImmediate();

            if (LocalPlayerContext.HasLocalPlayer && LocalPlayerContext.Player)
                BindDetector(LocalPlayerContext.Player.TargetDetector);

            LocalPlayerContext.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;
            LocalPlayerContext.OnLocalPlayerDespawned += HandleLocalPlayerDespawned;
        }

        private void OnDisable()
        {
            LocalPlayerContext.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
            LocalPlayerContext.OnLocalPlayerDespawned -= HandleLocalPlayerDespawned;

            UnbindDetector();
            HideImmediate();
        }

        private void Update()
        {
            if (!_hasTarget || !_currentTarget || _currentTarget.IsDead)
            {
                if (Time.time >= _lastSeenTime + _lingerDuration)
                {
                    _targetAlpha = 0f;
                }
            }

            if (_canvasGroup)
            {
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, _targetAlpha, Time.deltaTime * _fadeSpeed);

                bool shouldBeActive = _canvasGroup.alpha > 0.01f;
                if (_rootContainer && _rootContainer != gameObject && _rootContainer.activeSelf != shouldBeActive)
                {
                    _rootContainer.SetActive(shouldBeActive);
                }

                if (_canvasGroup.alpha <= 0.01f && !_hasTarget)
                {
                    UnbindCurrentTarget();
                }
            }
        }

        private void HandleLocalPlayerSpawned(PlayerFacade player)
        {
            if (player) BindDetector(player.TargetDetector);
        }

        private void HandleLocalPlayerDespawned(PlayerFacade player)
        {
            UnbindDetector();
            HideImmediate();
        }

        private void BindDetector(PlayerTargetDetector detector)
        {
            if (_detector == detector) return;

            UnbindDetector();
            if (!detector) return;

            _detector = detector;
            _detector.OnTargetChanged += HandleTargetChanged;

            HandleTargetChanged(_detector.CurrentTarget, false);
        }

        private void UnbindDetector()
        {
            if (_detector)
            {
                _detector.OnTargetChanged -= HandleTargetChanged;
                _detector = null;
            }
            UnbindCurrentTarget();
        }

        private void HandleTargetChanged(IDamageable target, bool isOnSight)
        {
            var targetHealth = ResolveHealth(target);

            if (targetHealth && !targetHealth.IsDead)
            {
                _hasTarget = true;
                _lastSeenTime = Time.time;
                _targetAlpha = 1f;

                if (_currentTarget != targetHealth)
                {
                    BindCurrentTarget(targetHealth);
                }
            }
            else
            {
                // Target lost (exited range or crosshair moved away)
                _hasTarget = false;

                if (_lingerDuration <= 0f || (_currentTarget && _currentTarget.IsDead))
                {
                    _targetAlpha = 0f;
                }
            }
        }

        private HealthComponent ResolveHealth(IDamageable target)
        {
            if (target == null) return null;
            if (target is Hitbox hitbox) return hitbox.TargetHealth;
            if (target is HealthComponent health) return health;
            return (target as Component)?.GetComponentInParent<HealthComponent>();
        }

        private void BindCurrentTarget(HealthComponent health)
        {
            UnbindCurrentTarget();

            _currentTarget = health;
            _currentTarget.OnHealthChanged += HandleHealthChanged;
            _currentTarget.OnShieldChanged += HandleShieldChanged;
            _currentTarget.OnShieldBroken += HandleShieldBroken;
            _currentTarget.OnDeath += HandleDeath;

            // Immediately set current visual values!
            UpdateHealthBar(_currentTarget.CurrentHealth, _currentTarget.MaxHealth);
            UpdateShieldBar(_currentTarget.CurrentShield, _currentTarget.MaxShield);

            if (_rootContainer) _rootContainer.SetActive(true);
        }

        private void UnbindCurrentTarget()
        {
            if (_currentTarget)
            {
                _currentTarget.OnHealthChanged -= HandleHealthChanged;
                _currentTarget.OnShieldChanged -= HandleShieldChanged;
                _currentTarget.OnShieldBroken -= HandleShieldBroken;
                _currentTarget.OnDeath -= HandleDeath;
                _currentTarget = null;
            }
        }

        private void HandleHealthChanged(float amount, float max) => UpdateHealthBar(amount, max);
        private void HandleShieldChanged(float amount, float max) => UpdateShieldBar(amount, max);

        private void HandleShieldBroken()
        {
            if (_shieldBar) _shieldBar.gameObject.SetActive(false);
        }

        private void HandleDeath(DamageResult result)
        {
            UpdateHealthBar(0f, _currentTarget ? _currentTarget.MaxHealth : 100f);
            _hasTarget = false;
            _targetAlpha = 0f;
            UnbindCurrentTarget();
        }

        private void UpdateHealthBar(float amount, float max)
        {
            if (_healthBar && max > 0f)
                _healthBar.SetFillAmount(Mathf.Clamp01(amount / max));
        }

        private void UpdateShieldBar(float amount, float max)
        {
            if (!_shieldBar) return;

            bool hasShield = max > 0f;
            _shieldBar.gameObject.SetActive(hasShield);

            if (hasShield)
                _shieldBar.SetFillAmount(Mathf.Clamp01(amount / max));
        }

        private void HideImmediate()
        {
            _hasTarget = false;
            _targetAlpha = 0f;
            if (_canvasGroup) _canvasGroup.alpha = 0f;
            if (_rootContainer && _rootContainer != gameObject) _rootContainer.SetActive(false);
            UnbindCurrentTarget();
        }
    }
}