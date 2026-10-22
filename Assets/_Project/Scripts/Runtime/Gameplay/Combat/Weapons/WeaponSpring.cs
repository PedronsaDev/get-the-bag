using UnityEngine;

namespace Project.Combat.Weapons
{
    internal static class WeaponSpring
    {
        public static void Step(ref Vector3 value, ref Vector3 velocity, Vector3 target,
            float frequency, float dampingRatio, float dt)
        {
            // Analytic solution for a constant target over this time step.
            var omega = 2f * Mathf.PI * Mathf.Max(0.01f, frequency);
            var damping = Mathf.Max(0.01f, dampingRatio);
            var offset = value - target;
            if (Mathf.Abs(damping - 1f) < 0.001f)
            {
                var coefficient = velocity + omega * offset;
                var decay = Mathf.Exp(-omega * dt);
                value = target + (offset + coefficient * dt) * decay;
                velocity = (velocity - omega * coefficient * dt) * decay;
            }
            else if (damping < 1f)
            {
                var decayRate = damping * omega;
                var angularRate = omega * Mathf.Sqrt(1f - damping * damping);
                var sine = Mathf.Sin(angularRate * dt);
                var cosine = Mathf.Cos(angularRate * dt);
                var coefficient = (velocity + decayRate * offset) / angularRate;
                var decay = Mathf.Exp(-decayRate * dt);
                var displaced = offset * cosine + coefficient * sine;
                velocity = decay * (-decayRate * displaced + angularRate * (coefficient * cosine - offset * sine));
                value = target + decay * displaced;
            }
            else
            {
                var root = Mathf.Sqrt(damping * damping - 1f);
                var slowRate = -omega * (damping - root);
                var fastRate = -omega * (damping + root);
                var slow = (velocity - fastRate * offset) / (slowRate - fastRate);
                var fast = offset - slow;
                slow *= Mathf.Exp(slowRate * dt);
                fast *= Mathf.Exp(fastRate * dt);
                value = target + slow + fast;
                velocity = slowRate * slow + fastRate * fast;
            }
        }
    }
}
