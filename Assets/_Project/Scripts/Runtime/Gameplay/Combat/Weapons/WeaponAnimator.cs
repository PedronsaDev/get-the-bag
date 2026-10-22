using Project.Logging;
using UnityEngine;

namespace Project.Combat.Weapons
{
[DisallowMultipleComponent]
[RequireComponent(typeof(WeaponInstance))]
public sealed class WeaponAnimator : MonoBehaviour
{
    private static readonly int ReloadSpeed = Animator.StringToHash("ReloadSpeed");
    private static readonly int IdleState = Animator.StringToHash("Idle");

    [Header("References")] 
    [SerializeField] private Animator _animator;
    [SerializeField] private WeaponInstance _weapon;

    [SerializeField, Min(0)] private int _layer;

    [Header("Settings")] 
    [SerializeField] private float _reloadClipDuration;

    private string _fireState = "Fire";
    private string _reloadState = "Reload";


    private int _fireStateHash;
    private int _reloadStateHash;
    private bool _reportedInvalidBinding;
    private bool _reloadOwnsPlayback;

    private void Awake()
    {
        if (!_weapon) 
            _weapon = GetComponent<WeaponInstance>();
        
        _fireStateHash = Animator.StringToHash(_fireState);
        _reloadStateHash = Animator.StringToHash(_reloadState);
    }

    private void OnEnable()
    {
        _reportedInvalidBinding = false;
        _weapon.Fired += OnFired;
        _weapon.ReloadingChanged += OnReloadingChanged;
        if (_weapon.IsReloading) OnReloadingChanged(true);
    }

    private void OnDisable()
    {
        _weapon.Fired -= OnFired;
        _weapon.ReloadingChanged -= OnReloadingChanged;
        _reloadOwnsPlayback = false;
    }

    private void OnFired()
    {
        if (!_animator || !_animator.isActiveAndEnabled || !_animator.runtimeAnimatorController || _layer < 0 ||
            _layer >= _animator.layerCount || !_animator.HasState(_layer, _fireStateHash))
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
        _animator.Play(_fireStateHash, _layer, 0f);
    }

    private void OnReloadingChanged(bool isReloading)
    {
        if (!isReloading && !_reloadOwnsPlayback) return;
        if (!_animator || !_animator.isActiveAndEnabled || !_animator.runtimeAnimatorController || _layer < 0 ||
            _layer >= _animator.layerCount || !_animator.HasState(_layer, _reloadStateHash))
        {
            if (!_reportedInvalidBinding)
            {
                AppLog.LogWarning(LogChannel.Combat,
                    "Weapon reload animation requires an enabled Animator and a valid full state path/layer.", this);
                _reportedInvalidBinding = true;
            }

            return;
        }

        if (isReloading)
        {
            if (!float.IsFinite(_reloadClipDuration) || _reloadClipDuration <= 0f ||
                !float.IsFinite(_weapon.ReloadDuration) || _weapon.ReloadDuration <= 0f)
            {
                AppLog.LogWarning(LogChannel.Combat, "Reload animation requires positive clip and gameplay durations.",
                    this);
                return;
            }

            var reloadSpeed = _reloadClipDuration / _weapon.ReloadDuration;
            var phase = Mathf.Clamp01((float)((Time.timeAsDouble - _weapon.ReloadStartedAt) / _weapon.ReloadDuration));
            _animator.SetFloat(ReloadSpeed, reloadSpeed);
            _reloadOwnsPlayback = true;
            _animator.Play(_reloadStateHash, _layer, phase);
        }
        else
        {
            _reloadOwnsPlayback = false;
            if (_animator.HasState(_layer, IdleState))
                _animator.CrossFadeInFixedTime(IdleState, 0.05f, _layer, 0f);
        }
    }
}
}