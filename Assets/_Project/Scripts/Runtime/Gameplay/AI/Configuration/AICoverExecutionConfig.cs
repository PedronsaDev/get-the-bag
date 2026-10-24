using System;
using UnityEngine;

namespace Project.AI.Configuration
{
[Serializable]
public class AICoverExecutionConfig
{
    [Header("Distance & Tolerances")]
    [SerializeField] private float _maxLateralPeekDistance = 1.5f;
    [SerializeField] private float _arrivalTolerance = 0.15f;
    [SerializeField] private float _maxNavigationSnapDistance = 0.1f;
    [SerializeField] private float _maxVerticalDeviation = 0.1f;

    [Header("Body Clearance")]
    [SerializeField] private LayerMask _bodyClearanceMask = ~0;
    [SerializeField] private float _clearanceSkin = 0.02f;

    [Header("Timing & Intervals")]
    [SerializeField] private float _validationInterval = 0.2f;
    [SerializeField] private float _approachTimeout = 3.0f;
    [SerializeField] private float _returnTimeout = 3.0f;
    [SerializeField] private float _retryDelay = 1.0f;

    public float MaxLateralPeekDistance => _maxLateralPeekDistance;
    public float ArrivalTolerance => _arrivalTolerance;
    public float MaxNavigationSnapDistance => _maxNavigationSnapDistance;
    public float MaxVerticalDeviation => _maxVerticalDeviation;
    public LayerMask BodyClearanceMask => _bodyClearanceMask;
    public float ClearanceSkin => _clearanceSkin;
    public float ValidationInterval => _validationInterval;
    public float ApproachTimeout => _approachTimeout;
    public float ReturnTimeout => _returnTimeout;
    public float RetryDelay => _retryDelay;
}
}
