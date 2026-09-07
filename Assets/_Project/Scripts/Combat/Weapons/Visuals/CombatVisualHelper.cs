using UnityEngine;

namespace Project.Combat
{
public static class CombatVisualHelper
{
    public static Color GetElementColor(DamageType type)
    {
        return type switch
        {
            DamageType.Physical => new Color(1.0f, 0.78f, 0.35f, 1.0f),
            DamageType.Incendiary => new Color(1.0f, 0.32f, 0.05f, 1.0f),
            DamageType.Corrosive => new Color(0.25f, 1.0f, 0.15f, 1.0f),
            DamageType.Shock => new Color(0.2f, 0.85f, 1.0f, 1.0f),
            DamageType.Explosive => new Color(1.0f, 0.55f, 0.12f, 1.0f),
            DamageType.TrueDamage => new Color(0.92f, 0.25f, 1.0f, 1.0f),
            _ => new Color(1.0f, 0.85f, 0.4f, 1.0f)
        };
    }

    public static Vector3 CalculateSpreadDirection(Vector3 baseDirection, float spreadAngleDegrees)
    {
        if (spreadAngleDegrees <= 0.01f || baseDirection.sqrMagnitude < 0.001f)
            return baseDirection;

        float angle = Random.Range(0f, spreadAngleDegrees);
        float rotation = Random.Range(0f, 360f);

        Quaternion coneRotation = Quaternion.AngleAxis(angle, Vector3.up);
        Quaternion rollRotation = Quaternion.AngleAxis(rotation, Vector3.forward);

        return Quaternion.LookRotation(baseDirection) * rollRotation * coneRotation * Vector3.forward;
    }
}
}