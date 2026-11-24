using System;
using UnityEngine;

namespace Project.AI.Configuration
{
[Serializable]
public class AICombatTuningConfig
{
    [Header("Cover")]
    [SerializeField] private float _coverCheckInterval = 0.5f;

    [Header("Flanking")]
    [SerializeField] private float _reflankMinDelay = 1.5f;
    [SerializeField] private float _reflankMaxDelay = 2.5f;
    [SerializeField] private float _flankAngleDegrees = 60f;
    [SerializeField] private float _flankSampleRadius = 4f;

    [Header("Retreat")]
    [SerializeField] private float _retreatStepDistance = 4f;
    [SerializeField] private float _retreatSampleRadius = 2f;

    public float CoverCheckInterval => _coverCheckInterval;
    public float ReflankMinDelay => _reflankMinDelay;
    public float ReflankMaxDelay => _reflankMaxDelay;
    public float FlankAngleDegrees => _flankAngleDegrees;
    public float FlankSampleRadius => _flankSampleRadius;
    public float RetreatStepDistance => _retreatStepDistance;
    public float RetreatSampleRadius => _retreatSampleRadius;
}
}
