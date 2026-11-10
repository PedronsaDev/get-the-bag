using Project.AI.Blackboard;
using Project.Combat.Health;
using PurrNet;
using UnityEngine;

namespace Project.AI.Presentation
{
[DisallowMultipleComponent]
public class AICivilianProne : NetworkBehaviour
{
    [SerializeField, Tooltip("The transform that holds the whole body. It is turned and moved; the agent root is not.")]
    private Transform _visual;
    [SerializeField] private AIBlackboard _blackboard;
    [SerializeField] private HealthComponent _health;
    [SerializeField, Min(0.05f)] private float _layDownSeconds = 0.7f;
    [SerializeField, Tooltip("Half the body length: the body is moved back so it lies centred on the spot where it stood.")]
    private float _halfBodyLength = 0.9f;
    [SerializeField, Tooltip("Lifts the body so it rests on the floor instead of sinking into it.")]
    private float _bodyThickness = 0.2f;

    private readonly SyncVar<bool> _isProne = new(false);

    private Vector3 _standingPosition;
    private Quaternion _standingRotation;
    private float _blend;

    private void Awake()
    {
        if (!_blackboard)
            _blackboard = GetComponent<AIBlackboard>();
        if (!_health)
            _health = GetComponent<HealthComponent>() ?? GetComponentInChildren<HealthComponent>(true);
        if (!_visual)
        {
            var animator = GetComponentInChildren<Animator>(true);
            _visual = animator && animator.transform.parent != transform ? animator.transform.parent : transform;
        }

        _standingPosition = _visual.localPosition;
        _standingRotation = _visual.localRotation;
    }

    private void Update()
    {
        if (_health && _health.IsDead)
            return;

        if (isServer && _blackboard)
            _isProne.value = _blackboard.WantsToLieDown;

        float target = _isProne.value ? 1f : 0f;
        if (Mathf.Approximately(_blend, target))
            return;

        _blend = Mathf.MoveTowards(_blend, target, Time.deltaTime / _layDownSeconds);
        ApplyPose(Mathf.SmoothStep(0f, 1f, _blend));
    }

    private void ApplyPose(float amount)
    {
        _visual.localRotation = Quaternion.Euler(90f * amount, 0f, 0f) * _standingRotation;
        _visual.localPosition = _standingPosition + new Vector3(0f, _bodyThickness * amount, -_halfBodyLength * amount);
    }
}
}
