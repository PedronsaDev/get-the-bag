using Project.AI.Combat;
using UnityEngine;

namespace Project.AI.Configuration
{
/// <summary>Validation for the currently supported ranged archetypes. Never repairs or mutates shared data.</summary>
internal static class AIArchetypeValidation
{
    public static bool TryValidate(AIArchetypeConfig archetype, out string error)
    {
        if (string.IsNullOrWhiteSpace(archetype.ArchetypeName))
            return Fail("ArchetypeName must contain a name.", out error);

        if (archetype.PreferredTokenType != CombatTokenType.RangedBurst)
            return Fail("Only RangedBurst is currently supported by the combat executor and token director.", out error);

        var movement = archetype.MovementConfig;
        if (movement == null)
            return Fail("MovementConfig is required.", out error);

        if (!NonNegative(movement.PatrolSpeed) || !NonNegative(movement.InvestigateSpeed) ||
            !NonNegative(movement.CombatRunSpeed))
            return Fail("Movement speeds must be finite and nonnegative.", out error);

        if (!NonNegative(movement.MinCombatDistance) || !Positive(movement.IdealCombatDistance) ||
            !Positive(movement.MaxCombatDistance) || movement.MinCombatDistance > movement.IdealCombatDistance ||
            movement.IdealCombatDistance > movement.MaxCombatDistance)
            return Fail("Combat distances must be finite and satisfy 0 <= minimum <= ideal <= maximum, with ideal > 0.", out error);

        var vision = archetype.VisionConfig;
        if (vision == null)
            return Fail("VisionConfig is required.", out error);

        if (!Positive(vision.ViewDistance) || !Positive(vision.PeripheralDistance) || !NonNegative(vision.EyeHeight))
            return Fail("Vision distances must be finite and positive; eye height must be finite and nonnegative.", out error);

        if (!InRange(vision.FieldOfView, 1f, 360f) || !InRange(vision.PeripheralFOV, 1f, 360f))
            return Fail("Vision field-of-view angles must be finite and between 1 and 360 degrees.", out error);

        if (vision.TargetMask.value == 0)
            return Fail("Vision TargetMask must include at least one target layer.", out error);

        if ((vision.ObstacleMask.value & vision.TargetMask.value) != 0)
            return Fail("Vision obstacle and target masks must not overlap.", out error);

        var weapon = archetype.WeaponConfig;
        if (weapon == null)
            return Fail("WeaponConfig is required for ranged archetypes.", out error);

        if (!NonNegative(weapon.DamagePerShot) || !Positive(weapon.MaxRange) || !Positive(weapon.RoundsPerMinute))
            return Fail("Weapon damage must be finite and nonnegative; range and RPM must be finite and positive.", out error);

        // The executor converts RPM to seconds and uses MaxBurstCount + 1 for an exclusive random bound.
        if (!Positive(60f / weapon.RoundsPerMinute))
            return Fail("Weapon RPM must produce a finite positive shot interval.", out error);

        if (weapon.MinBurstCount < 1 || weapon.MaxBurstCount < weapon.MinBurstCount || weapon.MaxBurstCount == int.MaxValue)
            return Fail("Burst counts must satisfy 1 <= minimum <= maximum < int.MaxValue.", out error);

        if (!NonNegative(weapon.MinBurstPause) || !NonNegative(weapon.MaxBurstPause) ||
            weapon.MinBurstPause > weapon.MaxBurstPause)
            return Fail("Burst pauses must be finite and satisfy 0 <= minimum <= maximum.", out error);

        if (!NonNegative(weapon.SpreadAngleDegrees) || weapon.SpreadAngleDegrees >= 90f)
            return Fail("Weapon spread must be finite and in the range [0, 90) degrees.", out error);

        if (!NonNegative(weapon.GunshotAcousticIntensity))
            return Fail("Gunshot acoustic intensity must be finite and nonnegative.", out error);

        if (weapon.HitMask.value == 0)
            return Fail("Weapon HitMask must include at least one layer.", out error);

        if (archetype.CanUseCover)
        {
            var cover = archetype.CoverConfig;
            if (cover == null)
                return Fail("Cover configuration is required when CanUseCover is enabled.", out error);

            if (!Positive(cover.MaxSearchRadius) || cover.ObstacleMask.value == 0)
                return Fail("Cover search radius must be finite and positive, and its obstacle mask must include a layer.", out error);

            if (!InRange(cover.ProximityWeight, 0f, 1f) || !InRange(cover.FacingWeight, 0f, 1f) ||
                !InRange(cover.PeekWeight, 0f, 1f) || !InRange(cover.IdealRangeWeight, 0f, 1f))
                return Fail("Cover weights must be finite and between zero and one.", out error);

            float sum = cover.ProximityWeight + cover.FacingWeight + cover.PeekWeight + cover.IdealRangeWeight;
            if (Mathf.Abs(sum - 1f) > 0.001f)
                return Fail("Cover weights must sum to one within a tolerance of 0.001.", out error);

            if (!InRange(cover.MinFacingAlignment, -1f, 1f))
                return Fail("Cover minimum facing alignment must be finite and between -1 and 1.", out error);

            var coverExec = archetype.CoverExecutionConfig;

            if (coverExec == null)
                return Fail("Cover execution configuration is required when CanUseCover is enabled.", out error);

            if (!Positive(coverExec.MaxLateralPeekDistance) || !Positive(coverExec.ArrivalTolerance) ||
                !Positive(coverExec.MaxNavigationSnapDistance) || !Positive(coverExec.MaxVerticalDeviation))
                return Fail("Cover execution distances and tolerances must be finite and positive.", out error);

            if (!Positive(coverExec.ValidationInterval) || !Positive(coverExec.ApproachTimeout) ||
                !Positive(coverExec.ReturnTimeout) || !Positive(coverExec.RetryDelay))
                return Fail("Cover execution timing and intervals must be finite and positive.", out error);

            if (coverExec.ArrivalTolerance >= coverExec.MaxLateralPeekDistance)
                return Fail("Cover arrival tolerance must be less than the maximum lateral peek distance.", out error);

            if (coverExec.MaxNavigationSnapDistance > coverExec.ArrivalTolerance)
                return Fail("Cover navigation snap distance must not exceed arrival tolerance.", out error);

            if (coverExec.BodyClearanceMask.value == 0 || !Positive(coverExec.ClearanceSkin))
                return Fail("Cover body clearance requires a nonempty mask and a finite positive skin.", out error);
        }

        error = null;
        return true;
    }

    private static bool NonNegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    private static bool Positive(float value) => NonNegative(value) && value > 0f;
    private static bool InRange(float value, float minimum, float maximum) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
}
