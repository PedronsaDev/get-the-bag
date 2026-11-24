using Project.Logging;
using UnityEngine;

namespace Project.Combat.Weapons
{
public enum WeaponAnimationView
{
    FirstPerson,
    World
}

[DisallowMultipleComponent]
[RequireComponent(typeof(WeaponInstance))]
public sealed class WeaponAnimator : MonoBehaviour
{
    private static readonly int ReloadSpeed = Animator.StringToHash("ReloadSpeed");
    private static readonly int IdleState = Animator.StringToHash("Idle");
    private static readonly int FireState = Animator.StringToHash("Fire");
    private static readonly int ReloadState = Animator.StringToHash("Reload");

    [Header("References")]
    [SerializeField] private Animator _animator;
    [SerializeField] private WeaponInstance _weapon;

    [SerializeField, Min(0)] private int _layer;
    [SerializeField] private WeaponAnimationView _view;

    [Header("Controller Template Slots")]
    [Tooltip("Original Reload clip in the Animator Controller. Per-weapon replacements belong in WeaponPresentationDefinition.")]
    [SerializeField] private AnimationClip _reloadClip;
    [Tooltip("Original Idle clip in the Animator Controller, used to restore the normal holding pose.")]
    [SerializeField] private AnimationClip _idleClip;

    private AnimatorOverrideController _animationOverrides;
    private AnimationClip _defaultReloadClip;
    private AnimationClip _defaultIdleClip;

    private bool _reportedInvalidBinding;
    private bool _reloadOwnsPlayback;

    private bool HasPlayableAnimator => _animator && _animator.isActiveAndEnabled &&
        _animator.runtimeAnimatorController && _layer >= 0 && _layer < _animator.layerCount;

    private void Awake()
    {
        if (!_weapon)
            _weapon = GetComponent<WeaponInstance>();

        if (_animator && _animator.runtimeAnimatorController && _reloadClip && _idleClip)
        {
            _animationOverrides = new AnimatorOverrideController(_animator.runtimeAnimatorController);
            _defaultReloadClip = _animationOverrides[_reloadClip.name];
            _defaultIdleClip = _animationOverrides[_idleClip.name];
            _animator.runtimeAnimatorController = _animationOverrides;
        }
    }

    private void OnEnable()
    {
        _reportedInvalidBinding = false;
        if (_animator)
            _animator.updateMode = AnimatorUpdateMode.UnscaledTime;

        _weapon.OnFired += OnFired;
        _weapon.OnReloadingChanged += OnReloadingChanged;
        _weapon.OnAmmoChanged += OnAmmoChanged;

        OnAmmoChanged(_weapon.LoadedAmmo);

        if (_weapon.IsReloading)
            OnReloadingChanged(true);
    }

    private void OnDisable()
    {
        _weapon.OnFired -= OnFired;
        _weapon.OnReloadingChanged -= OnReloadingChanged;
        _weapon.OnAmmoChanged -= OnAmmoChanged;
        _reloadOwnsPlayback = false;
    }

    private void OnDestroy()
    {
        if (!_animationOverrides) return;
        if (_animator && _animator.runtimeAnimatorController == _animationOverrides)
            _animator.runtimeAnimatorController = _animationOverrides.runtimeAnimatorController;
        Destroy(_animationOverrides);
    }

    private void OnFired()
    {

        if (_view == WeaponAnimationView.World && HasPlayableAnimator && !_animator.HasState(_layer, FireState))
            return;

        if (!HasPlayableAnimator || !_animator.HasState(_layer, FireState))
        {
            if (!_reportedInvalidBinding)
            {
                AppLog.LogWarning(LogChannel.Combat,
                    "Weapon fire animation requires an enabled Animator and a valid full state path/layer.", this);
                _reportedInvalidBinding = true;
            }

            return;
        }

        _reloadOwnsPlayback = false;
        _animator.Play(FireState, _layer, 0f);
    }

    private void OnAmmoChanged(int ammo)
    {
        if (!_animationOverrides || !_idleClip)
            return;

        var selectedClip = _defaultIdleClip;
        var presentation = _weapon.Definition ? _weapon.Definition.PresentationDefinition : null;

        if (ammo == 0 && presentation)
        {
            var emptyClip = _view == WeaponAnimationView.World
                ? presentation.WorldIdleEmptyClip
                : presentation.FirstPersonIdleEmptyClip;
            if (emptyClip) selectedClip = emptyClip;
        }

        if (_animationOverrides[_idleClip] != selectedClip)
            _animationOverrides[_idleClip.name] = selectedClip;
    }

    private void OnReloadingChanged(bool isReloading)
    {
        if (!isReloading && !_reloadOwnsPlayback) return;
        if (!HasPlayableAnimator || !_animationOverrides || !_animator.HasState(_layer, ReloadState))
        {
            if (!_reportedInvalidBinding)
            {
                AppLog.LogWarning(LogChannel.Combat,
                    "Weapon reload animation requires an enabled Animator, original reload clip slot and valid state/layer.", this);
                _reportedInvalidBinding = true;
            }

            return;
        }

        if (isReloading)
        {
            var clip = ResolveReloadClip();
            if (!clip || clip.length <= 0f || clip.isLooping || clip.isHumanMotion != _animator.isHuman ||
                !float.IsFinite(_weapon.ReloadDuration) || _weapon.ReloadDuration <= 0f)
            {
                AppLog.LogWarning(LogChannel.Combat, "Reload animation requires a compatible non-looping clip and positive gameplay duration.",
                    this);
                return;
            }

            _animationOverrides[_reloadClip.name] = clip;
            var reloadSpeed = clip.length / _weapon.ReloadDuration;
            var phase = Mathf.Clamp01((float)((Time.unscaledTimeAsDouble - _weapon.ReloadStartedAt) / _weapon.ReloadDuration));
            _animator.SetFloat(ReloadSpeed, reloadSpeed);
            _reloadOwnsPlayback = true;
            _animator.Play(ReloadState, _layer, phase);
        }
        else
        {
            _reloadOwnsPlayback = false;
            if (_animator.HasState(_layer, IdleState))
                _animator.CrossFadeInFixedTime(IdleState, 0.05f, _layer, 0f);
        }
    }

    private AnimationClip ResolveReloadClip()
    {
        var clip = _defaultReloadClip;
        var presentation = _weapon.Definition ? _weapon.Definition.PresentationDefinition : null;
        if (!presentation) return clip;

        var normalClip = _view == WeaponAnimationView.World
            ? presentation.ReloadNormal?.WeaponClip
            : presentation.ReloadNormal?.FirstPersonClip;
        var emptyClip = _view == WeaponAnimationView.World
            ? presentation.ReloadEmpty?.WeaponClip
            : presentation.ReloadEmpty?.FirstPersonClip;
        if (normalClip) clip = normalClip;
        if (_weapon.ReloadType == ReloadVariant.Empty && emptyClip)
            clip = emptyClip;
        return clip;
    }
}
}
