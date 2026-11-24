using PurrNet;
using Project.Logging;
using UnityEngine;

namespace Project.Player.Animation
{
public class PlayerAimTarget : NetworkBehaviour
{
    [SerializeField] private Transform _aimTargetTransform;
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private float _targetDistance = 100f;

    protected override void OnSpawned()
    {
        enabled = isOwner;
        if (isOwner && (!_cameraTransform || !_aimTargetTransform))
        {
            AppLog.LogWarning(LogChannel.Player, "Player aim requires camera and aim target references.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (!isOwner || !_cameraTransform || !_aimTargetTransform)
            return;

        var aimDirection = _cameraTransform.forward;
        _aimTargetTransform.position = _cameraTransform.position + aimDirection * _targetDistance;
    }
}
}
