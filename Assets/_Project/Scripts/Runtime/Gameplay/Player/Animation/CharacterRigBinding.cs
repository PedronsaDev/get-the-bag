using Project.Definitions.Weapons;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Project.Logging;

namespace Project.Player.Animation
{
public class CharacterRigBinding : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterVisualDefinition _visualDefinition;
    [SerializeField] private Animator _bodyAnimator;
    [SerializeField] private Transform _worldWeaponSocket;
    [SerializeField] private Transform _firstPersonSocket;
    [SerializeField] private Transform _aimOrigin;
    [SerializeField] private Transform _bodyAimTarget;
    [SerializeField] private TwoBoneIKConstraint _leftHandIK;
    [SerializeField] private Renderer[] _bodyRenderers;
    [SerializeField] private RigBuilder _rigBuilder;
    [SerializeField] private Rig _upperBodyRig;
    [SerializeField] private Rig _supportHandRig;

    private Transform _rightHandBone;
    private Transform _leftHandBone;

    public CharacterVisualDefinition VisualDefinition => _visualDefinition;

    public Animator BodyAnimator => _bodyAnimator;
    public Transform RightHandBone => _rightHandBone;
    public Transform LeftHandBone => _leftHandBone;
    public Transform WorldWeaponSocket => _worldWeaponSocket;
    public Transform FirstPersonSocket => _firstPersonSocket;
    public Transform AimOrigin => _aimOrigin;
    public Transform BodyAimTarget => _bodyAimTarget;
    public TwoBoneIKConstraint LeftHandIK => _leftHandIK;
    public Renderer[] BodyRenderers => _bodyRenderers;
    public RigBuilder RigBuilder => _rigBuilder;
    public Rig UpperBodyRig => _upperBodyRig;
    public Rig SupportHandRig => _supportHandRig;
    public bool IsBindingUsable { get; private set; }

    private void Awake()
    {
        IsBindingUsable = false;

        if (!_bodyAnimator)
            _bodyAnimator = GetComponentInChildren<Animator>();

        if (!_rigBuilder)
            _rigBuilder = GetComponentInChildren<RigBuilder>();

        if (!_upperBodyRig)
            _upperBodyRig = GetComponentInChildren<Rig>();

        if (!_bodyAnimator)
        {
            AppLog.LogError(LogChannel.Animation, "Character rig binding requires a body Animator.", this);
            return;
        }

        if (!_bodyAnimator.avatar || !_bodyAnimator.avatar.isValid || !_bodyAnimator.isHuman)
        {
            AppLog.LogError(LogChannel.Animation, "Character rig binding requires a valid Humanoid Avatar.", this);
            return;
        }

        _rightHandBone = _bodyAnimator.GetBoneTransform(HumanBodyBones.RightHand);
        _leftHandBone = _bodyAnimator.GetBoneTransform(HumanBodyBones.LeftHand);

        if (!_rightHandBone || !_leftHandBone)
        {
            AppLog.LogError(LogChannel.Animation, "Character rig binding requires both Humanoid hand-bone transforms.", this);
            return;
        }

        if (!_worldWeaponSocket)
        {
            AppLog.LogError(LogChannel.Animation, "Character rig binding requires a world weapon socket.", this);
            return;
        }

        if (_worldWeaponSocket != _rightHandBone && !_worldWeaponSocket.IsChildOf(_rightHandBone))
        {
            AppLog.LogError(LogChannel.Animation, "World weapon socket must belong to this body's right-hand hierarchy.", this);
            return;
        }

        if (!_firstPersonSocket || !_aimOrigin || !_bodyAimTarget ||
            !_rigBuilder || !_upperBodyRig || _bodyRenderers == null || _bodyRenderers.Length == 0)
        {
            AppLog.LogError(LogChannel.Animation,
                "Character rig binding is missing one or more required presentation references.", this);
            return;
        }

        foreach (var renderer in _bodyRenderers)
        {
            if (!renderer)
            {
                AppLog.LogError(LogChannel.Player, "Character rig binding has a null body renderer reference.", this);
                return;
            }
        }

        if (!_leftHandIK)
        {
            AppLog.LogError(LogChannel.Animation, "Character rig binding requires a left-hand IK target.", this);
            return;
        }

        if (!_supportHandRig)
        {
            AppLog.LogError(LogChannel.Animation, "Character rig binding requires a support-hand rig.", this);
            return;
        }

        IsBindingUsable = true;
        AppLog.Log(LogChannel.Animation, "Character rig binding is usable.", this);
    }

    public void SetSupportHandGrip(Transform grip)
    {
        if (!_leftHandIK)
        {
            AppLog.LogWarning(LogChannel.Animation, "Character rig binding has no left-hand IK target to set.", this);
            return;
        }

        _leftHandIK.weight = 0;

        if (!grip) return;

        if (!_leftHandIK.data.target)
        {
            AppLog.LogWarning(LogChannel.Animation, "Character rig binding has no left-hand IK target to set.", this);
            return;
        }

        var target = _leftHandIK.data.target;
        target.SetPositionAndRotation(grip.position, grip.rotation);

        _leftHandIK.weight = 1;
    }
}
}