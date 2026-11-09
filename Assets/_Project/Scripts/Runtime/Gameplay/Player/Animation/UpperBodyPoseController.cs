using System;
using Project.Combat.Weapons;
using Project.Content;
using Project.Definitions.Weapons;
using Project.Logging;
using Project.Player.Combat;
using Project.Player.Locomotion;
using PurrNet;
using UnityEngine;

namespace Project.Player.Animation
{
[DefaultExecutionOrder(-100)]
public class UpperBodyPoseController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterRigBinding _rigBinding;
    [SerializeField] private NetworkAnimator _networkAnimator;
    [SerializeField] private PlayerDownedState _downedState;
    [SerializeField] private PlayerWeaponController _weaponController;

    [Header("Controller Template Slots")]
    [Tooltip("Original reload clip in the body controller. Per-weapon replacements belong in WeaponPresentationDefinition.")]
    [SerializeField] private AnimationClip _reloadClip;
    [Tooltip("Original holding clip in the body controller.")]
    [SerializeField] private AnimationClip _holdClip;

    private AnimationClip _activeReloadClip;
    private WeaponReloadAnimation _normalReload;
    private WeaponReloadAnimation _emptyReload;

    private AnimationCurve _reloadSupportHandWeight;

    private AnimatorOverrideController _bodyOverrides;

    private static readonly int ReloadState = Animator.StringToHash("Upper_Body_Layer.Weap_Reload");
    private static readonly int HoldState = Animator.StringToHash("Upper_Body_Layer.Weap_Hold");
    private static readonly int ReloadSpeed = Animator.StringToHash("BodyReloadSpeed");

    private Animator _bodyAnimator;
    private int _upperBodyLayer = -1;
    private uint _lastActionSequence;
    private uint _lastEquipRevision;
    private uint _boundEquipRevision;
    private bool _playingReload;
    private bool _isDowned;

    private void Awake()
    {
        if (!_rigBinding)
            _rigBinding = GetComponentInParent<CharacterRigBinding>();

        if (!_networkAnimator)
            _networkAnimator = GetComponentInChildren<NetworkAnimator>();

        if (!_downedState)
            _downedState = GetComponentInChildren<PlayerDownedState>();

        if (!_weaponController)
            _weaponController = GetComponentInParent<PlayerWeaponController>();

        if (!_rigBinding || !_rigBinding.BodyAnimator || !_rigBinding.UpperBodyRig ||
            !_networkAnimator || !_downedState || !_weaponController || !_reloadClip || !_holdClip)
        {
            AppLog.LogError(LogChannel.Animation, "Upper body poses require rig, network Animator, player state and reload clip references.", this);
            enabled = false;
            return;
        }

        _bodyAnimator = _rigBinding.BodyAnimator;
        _upperBodyLayer = _bodyAnimator.GetLayerIndex("Upper_Body_Layer");

        _activeReloadClip = _reloadClip;

        var hasReloadSpeed = false;
        foreach (var parameter in _bodyAnimator.parameters)
            if (parameter.nameHash == ReloadSpeed && parameter.type == AnimatorControllerParameterType.Float)
                hasReloadSpeed = true;
        if (_upperBodyLayer < 0 || !_bodyAnimator.HasState(_upperBodyLayer, ReloadState) ||
            !_bodyAnimator.HasState(_upperBodyLayer, HoldState) || !hasReloadSpeed ||
            _reloadClip.length <= 0f || _reloadClip.isLooping || _bodyAnimator.applyRootMotion)
        {
            AppLog.LogError(LogChannel.Animation, "Upper body poses require Pistol Hold/Pistol Reload, BodyReloadSpeed, a non-looping reload clip and disabled root motion.", this);
            enabled = false;
            return;
        }

        _bodyOverrides = new AnimatorOverrideController(_bodyAnimator.runtimeAnimatorController);
        _bodyAnimator.runtimeAnimatorController = _bodyOverrides;

        _bodyAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    private void OnEnable()
    {
        if (_downedState)
        {
            _downedState.OnDownedStateChanged += HandleDownedStateChanged;
            HandleDownedStateChanged(_downedState.IsDowned);
        }

        if (_weaponController)
        {
            _weaponController.OnEquipmentChanged += HandleWeaponEquipmentChanged;
            HandleWeaponEquipmentChanged(_weaponController.ConfirmedState);
        }
    }

    private void OnDisable()
    {
        if (_downedState)
            _downedState.OnDownedStateChanged -= HandleDownedStateChanged;

        if (_weaponController)
            _weaponController.OnEquipmentChanged -= HandleWeaponEquipmentChanged;

        StopReload();

        if (_rigBinding && _rigBinding.SupportHandRig)
            _rigBinding.SupportHandRig.weight = 0f;
    }

    private void OnDestroy()
    {
        if (!_bodyOverrides) return;
        if (_bodyAnimator && _bodyAnimator.runtimeAnimatorController == _bodyOverrides)
            _bodyAnimator.runtimeAnimatorController = _bodyOverrides.runtimeAnimatorController;

        Destroy(_bodyOverrides);
    }

    private void Update()
    {
        UpdateSupportHandWeight();
    }

    private void HandleDownedStateChanged(bool isDowned)
    {
        _isDowned = isDowned;
        if (isDowned) StopReload();
        else ApplyPoseWeights();
    }

    private void HandleWeaponEquipmentChanged(WeaponEquipmentState state)
    {
        if (_boundEquipRevision != state.EquipRevision)
        {
            StopReload();
            if (!BindToEquipment(state))
                return;

            _boundEquipRevision = state.EquipRevision;
        }

        if (_isDowned || state.Action != WeaponActionState.Reloading)
        {
            StopReload();
            return;
        }

        if (_playingReload && _lastActionSequence == state.ActionSequence && _lastEquipRevision == state.EquipRevision)
            return;

        if (!_weaponController.isSpawned)
            return;

        if (!float.IsFinite(state.ActionDuration) || state.ActionDuration <= 0f || !double.IsFinite(state.ActionStartedAt))
        {
            AppLog.LogWarning(LogChannel.Animation, "Body reload ignored invalid confirmed action timing.", this);
            StopReload();
            return;
        }

        var elapsed = Math.Max(0d, _weaponController.NetworkTime - state.ActionStartedAt);
        if (elapsed >= state.ActionDuration)
        {
            StopReload();
            return;
        }

        SelectReloadAnimation(state.ReloadType);
        _lastActionSequence = state.ActionSequence;
        _lastEquipRevision = state.EquipRevision;
        _bodyAnimator.SetFloat(ReloadSpeed, _activeReloadClip.length / state.ActionDuration);
        _bodyAnimator.Play(ReloadState, _upperBodyLayer, (float)(elapsed / state.ActionDuration));
        _playingReload = true;

        ApplyPoseWeights();
    }

    private void SelectReloadAnimation(ReloadVariant variant)
    {
        var reload = _normalReload;
        if (variant == ReloadVariant.Empty && _emptyReload != null && _emptyReload.BodyClip)
            reload = _emptyReload;

        _reloadSupportHandWeight = reload?.SupportHandWeight;
        var clip = reload?.BodyClip;
        if (!clip) clip = _reloadClip;
        if (_activeReloadClip == clip) return;

        _bodyOverrides[_reloadClip.name] = clip;
        _activeReloadClip = clip;
    }

    private void StopReload()
    {
        if (_playingReload && _bodyAnimator && _bodyAnimator.isActiveAndEnabled)
            _bodyAnimator.CrossFadeInFixedTime(HoldState, 0.05f, _upperBodyLayer, 0f);
        _playingReload = false;
        ApplyPoseWeights();
    }

    private void ApplyPoseWeights()
    {
        if (!_rigBinding || !_rigBinding.UpperBodyRig || !_networkAnimator || _upperBodyLayer < 0)
            return;
        _networkAnimator.SetLayerWeight(_upperBodyLayer, _isDowned ? 0f : 1f);
        _rigBinding.UpperBodyRig.weight = _isDowned || _playingReload ? 0f : 1f;
    }

    private void UpdateSupportHandWeight()
    {
        if (!_rigBinding || !_rigBinding.SupportHandRig )
        {
            return;
        }

        if (!_weaponController || !_weaponController.isSpawned || _downedState.IsDowned)
        {
            _rigBinding.SupportHandRig.weight = 0f;
            return;
        }

        var state = _weaponController.ConfirmedState;

        if (!_playingReload)
        {
            _rigBinding.SupportHandRig.weight = 1f;
            return;
        }

        if (_reloadSupportHandWeight == null || _reloadSupportHandWeight.length == 0)
        {
            _rigBinding.SupportHandRig.weight = 0f;
            return;
        }

        var elapsed = _weaponController.NetworkTime - state.ActionStartedAt;
        var progress = Mathf.Clamp01((float)(elapsed / state.ActionDuration));

        _rigBinding.SupportHandRig.weight = _reloadSupportHandWeight.Evaluate(progress);
    }

    private bool BindToEquipment(WeaponEquipmentState state)
    {
        if (!GameContent.IsLoaded)
            return false;

        if (!GameContent.WeaponRegistry.TryGetById(state.ActiveWeapon.WeaponId, out var weaponDef))
            return false;

        var presentation = weaponDef.PresentationDefinition;

        if (!presentation)
        {
            AppLog.LogError(LogChannel.Animation, $"Upper body pose controller failed to find presentation definition for '{weaponDef.Id}'.", this);
            return false;
        }

        var holdClip = presentation.BodyHoldClip;
        var normalReload = presentation.ReloadNormal;
        var reloadClip = normalReload?.BodyClip;

        if (!holdClip || !reloadClip)
        {
            AppLog.LogError(LogChannel.Animation, $"Upper body pose controller failed to find hold/reload clips for '{weaponDef.Id}'.", this);
            return false;
        }

        _bodyOverrides[_holdClip] = holdClip;
        _bodyOverrides[_reloadClip] = reloadClip;
        _normalReload = normalReload;
        _emptyReload = presentation.ReloadEmpty;
        _activeReloadClip = reloadClip;

        _reloadSupportHandWeight = normalReload.SupportHandWeight;
        return true;
    }

}
}
