using UnityEngine;
using System;

namespace Project.Player.Camera.Helpers
{
[Serializable]
public class SpringSettings
{
    public float Stiffness = 150f;
    public float Damping = 15f;

    public SpringSettings(float stiffness, float damping)
    {
        Stiffness = stiffness;
        Damping = damping;
    }
}

public class Vector3Spring
{
    // Semi-implicit Euler goes unstable with large steps, so a long frame is split into small ones.
    private const float MAX_STEP = 1f / 120f;
    private const float MAX_FRAME = 0.1f;

    public Vector3 CurrentValue { get; private set; }
    public Vector3 TargetValue { get; set; }

    private Vector3 _velocity;

    public void Reset()
    {
        CurrentValue = Vector3.zero;
        TargetValue = Vector3.zero;
        _velocity = Vector3.zero;
    }

    public void Update(float deltaTime, SpringSettings settings)
    {
        if (!(deltaTime > 0f)) return;

        deltaTime = Mathf.Min(deltaTime, MAX_FRAME);
        int steps = Mathf.CeilToInt(deltaTime / MAX_STEP);
        float step = deltaTime / steps;

        for (int i = 0; i < steps; i++)
        {
            Vector3 displacement = TargetValue - CurrentValue;
            Vector3 acceleration = displacement * settings.Stiffness - _velocity * settings.Damping;
            _velocity += acceleration * step;
            CurrentValue += _velocity * step;
        }
    }

    public void AddVelocity(Vector3 velocityToAdd)
    {
        _velocity += velocityToAdd;
    }
}
}
