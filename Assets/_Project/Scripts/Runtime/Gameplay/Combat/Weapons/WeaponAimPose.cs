using UnityEngine;

namespace Project.Combat.Weapons
{
[DisallowMultipleComponent]
public sealed class WeaponAimPose : MonoBehaviour
{
    [Header("Target Pose")] 
    [SerializeField] private Transform _motionRoot;
    [SerializeField] private Vector3 _aimLocalPosition;
    [SerializeField] private Vector3 _aimLocalEulerAngles;

    [Header("Sight Calibration")] 
    [SerializeField] private Transform _aimReference;
    [SerializeField] private AnimationClip _referencePose;
    [SerializeField, Min(0f)] private float _referenceTime;
    [SerializeField, Min(0.01f)] private float _sightDistance = 0.32f;

    [Header("Animation Settings")] 
    [SerializeField, Min(0.01f)] private float _aimInDuration = 0.15f;
    [SerializeField, Min(0.01f)] private float _aimOutDuration = 0.22f;
    [SerializeField] private AnimationCurve _blendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Camera / FOV")] 
    [SerializeField, Range(20f, 120f)] private float _hipFov = 90f;
    [SerializeField, Range(20f, 120f)] private float _aimFov = 60f;

    private Vector3 _hipPosition;
    private Quaternion _hipRotation;
    private Quaternion _aimRotation;

    public float Progress { get; private set; }
    public float EvaluatedWeight { get; private set; }
    public float CurrentFov => Mathf.Lerp(_hipFov, _aimFov, EvaluatedWeight);
    public float AimFov => _aimFov;

    private void Awake()
    {
        CachePositionsAndRotations();
    }

    private void OnValidate()
    {
        _aimRotation = Quaternion.Euler(_aimLocalEulerAngles);
    }

    public void CachePositionsAndRotations()
    {
        if (!_motionRoot) return;
        _hipPosition = _motionRoot.localPosition;
        _hipRotation = _motionRoot.localRotation;
        _aimRotation = Quaternion.Euler(_aimLocalEulerAngles);
    }

    public void Tick(bool aiming, float deltaTime)
    {
        if (!_motionRoot) return;

        float duration = aiming ? _aimInDuration : _aimOutDuration;
        float targetProgress = aiming ? 1f : 0f;

        Progress = Mathf.MoveTowards(Progress, targetProgress, deltaTime / Mathf.Max(0.001f, duration));

        EvaluatedWeight = _blendCurve.Evaluate(Progress);

        _motionRoot.SetLocalPositionAndRotation(
            Vector3.Lerp(_hipPosition, _aimLocalPosition, EvaluatedWeight),
            Quaternion.Slerp(_hipRotation, _aimRotation, EvaluatedWeight)
        );
    }

    public void ResetPose()
    {
        Progress = 0f;
        EvaluatedWeight = 0f;
        if (_motionRoot)
        {
            _motionRoot.SetLocalPositionAndRotation(_hipPosition, _hipRotation);
        }
    }

    private void OnDisable() => ResetPose();

#if UNITY_EDITOR
    [ContextMenu("Capture Current Pose As Aim")]
    private void CaptureCurrentPoseAsAim()
    {
        if (!_motionRoot) return;
        _aimLocalPosition = _motionRoot.localPosition;
        _aimLocalEulerAngles = _motionRoot.localEulerAngles;
        _aimRotation = _motionRoot.localRotation;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("Snap To Aim Pose")]
    private void SnapToAimPose()
    {
        if (!_motionRoot) return;
        _motionRoot.SetLocalPositionAndRotation(_aimLocalPosition, Quaternion.Euler(_aimLocalEulerAngles));
    }

    [ContextMenu("Snap To Hip Pose")]
    private void SnapToHipPose()
    {
        if (!_motionRoot) return;
        _motionRoot.SetLocalPositionAndRotation(_hipPosition, _hipRotation);
    }
#endif
}
}