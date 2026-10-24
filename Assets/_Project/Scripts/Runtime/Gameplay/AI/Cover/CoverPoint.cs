using UnityEngine;

namespace Project.AI.Cover
{

[DisallowMultipleComponent]
[SelectionBase]
public class CoverPoint : MonoBehaviour
{
    [Header("Tactical Properties")]
    [SerializeField] private CoverHeight _height = CoverHeight.Low;
    [SerializeField] private CoverPeekDirection _availablePeeks = CoverPeekDirection.None;
    [SerializeField] private float _reservationTimeout = 5.0f;

    [Header("Lateral Peek Distances (Local Space)")]
    [SerializeField] private float _leftLocalOffset;
    [SerializeField] private float _rightLocalOffset;
    [SerializeField, HideInInspector] private float _upLocalOffset;

    [Header("Orientation")]
    [SerializeField] private Vector3 _forward = Vector3.forward;

    private bool _isReserved;
    private EntityId _reservedBy;
    private float _reserveExpirationTime;

    public Vector3 Position => transform.position;
    public Vector3 Forward => transform.TransformDirection(_forward).normalized;
    public CoverHeight Height => _height;
    public CoverPeekDirection PeekDirections
    {
        get
        {
            CoverPeekDirection directions = CoverPeekDirection.None;
            if (TryGetPeekDestination(CoverPeekDirection.Left, out _))
                directions |= CoverPeekDirection.Left;
            if (TryGetPeekDestination(CoverPeekDirection.Right, out _))
                directions |= CoverPeekDirection.Right;
            if (_height == CoverHeight.Low && (_availablePeeks & CoverPeekDirection.Over) != 0)
                directions |= CoverPeekDirection.Over;
            return directions;
        }
    }
    public bool IsReserved => _isReserved && Time.time < _reserveExpirationTime;
    public EntityId ReservedBy => _reservedBy;

    public float LeftPeekOffset => _leftLocalOffset;
    public float RightPeekOffset => _rightLocalOffset;
    public float UpPeekOffset => _upLocalOffset;

    private void OnEnable()
    {
        CoverRegistry.Register(this);
    }

    private void OnDisable()
    {
        CoverRegistry.Unregister(this);
        ReleaseReservation();
    }

    public bool TryReserve(EntityId agentId)
    {
        float currentTime = Time.time;

        if (_isReserved && _reservedBy == agentId)
        {
            _reserveExpirationTime = currentTime + _reservationTimeout;
            return true;
        }

        if (!_isReserved || currentTime >= _reserveExpirationTime)
        {
            _isReserved = true;
            _reservedBy = agentId;
            _reserveExpirationTime = currentTime + _reservationTimeout;
            return true;
        }

        return false;
    }
    
    public bool TryGetPeekDestination(CoverPeekDirection direction, out Vector3 destination)
    {
        destination = default;
        float signedDistance;
        switch (direction)
        {
            case CoverPeekDirection.Left:
                signedDistance = -_leftLocalOffset;
                break;
            case CoverPeekDirection.Right:
                signedDistance = _rightLocalOffset;
                break;
            default:
                return false;
        }

        float distance = direction == CoverPeekDirection.Left ? _leftLocalOffset : _rightLocalOffset;
        if ((_availablePeeks & direction) == 0 || !float.IsFinite(distance) || distance <= 0f || !IsFinite(_forward))
            return false;

        Vector3 localRight = Vector3.Cross(Vector3.up, _forward);
        float tangentLengthSqr = localRight.sqrMagnitude;
        if (!float.IsFinite(tangentLengthSqr) || tangentLengthSqr <= 0.000001f)
            return false;

        Vector3 worldDestination = transform.TransformPoint(localRight.normalized * signedDistance);
        Vector3 anchor = Position;
        float displacementSqr = (worldDestination - anchor).sqrMagnitude;
        if (!IsFinite(worldDestination) || !IsFinite(anchor) ||
            !float.IsFinite(displacementSqr) || displacementSqr <= 0.000001f)
            return false;

        destination = worldDestination;
        return true;
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

    public void Release(EntityId agentId)
    {
        if (_isReserved && _reservedBy == agentId)
        {
            ReleaseReservation();
        }
    }

    private void ReleaseReservation()
    {
        _isReserved = false;
        _reservedBy = default;
        _reserveExpirationTime = 0f;
    }
}
}
