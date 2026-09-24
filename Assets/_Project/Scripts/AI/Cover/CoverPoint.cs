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

    [Header("Orientation")] 
    [SerializeField] private Vector3 _forward = Vector3.forward;

    private bool _isReserved;
    private EntityId _reservedBy;
    private float _reserveExpirationTime;

    public Vector3 Position => transform.position;
    public Vector3 Forward => transform.TransformDirection(_forward).normalized;
    public CoverHeight Height => _height;
    public CoverPeekDirection PeekDirections => _availablePeeks;
    public bool IsReserved => _isReserved && Time.time < _reserveExpirationTime;
    public EntityId ReservedBy => _reservedBy;

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
