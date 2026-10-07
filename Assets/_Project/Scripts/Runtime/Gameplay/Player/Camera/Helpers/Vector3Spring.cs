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
        Vector3 displacement = TargetValue - CurrentValue;
        Vector3 springForce = displacement * settings.Stiffness;
        Vector3 dampingForce = -_velocity * settings.Damping;
        Vector3 acceleration = springForce + dampingForce;

        _velocity += acceleration * deltaTime;
        CurrentValue += _velocity * deltaTime;
    }

    public void AddVelocity(Vector3 velocityToAdd)
    {
        _velocity += velocityToAdd;
    }
}
}