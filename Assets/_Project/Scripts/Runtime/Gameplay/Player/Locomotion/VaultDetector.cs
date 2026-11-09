using UnityEngine;
using UnityPhysics = UnityEngine.Physics;

namespace Project.Player.Locomotion
{

internal static class VaultDetector
{
    public static VaultEvaluation Evaluate(LocomotionController controller)
    {
        var eval = new VaultEvaluation();
        if (controller.IsDowned || controller.IsDraggingTeammate || !controller.Motor || controller.State.Phase == LocomotionPhase.Noclip)
        {
            eval.FailureReason = "Disabled / Downed / Noclip";
            return eval;
        }

        Vector3 basePos = controller.Motor.TransientPosition;
        Vector3 up = controller.Motor.CharacterUp;
        Vector3 forward = (Application.isPlaying && controller.LookInputVector.sqrMagnitude > 0.1f)
            ? controller.LookInputVector.normalized
            : controller.transform.forward;

        float radius = controller.Motor.Capsule ? controller.Motor.Capsule.radius : 0.5f;
        float height = controller.Motor.Capsule ? controller.Motor.Capsule.height : controller.StandingHeight;

        float lowCheckHeight = Mathf.Clamp(controller.MinVaultHeight, 0.3f, 0.6f);
        float midCheckHeight = Mathf.Clamp(controller.MaxVaultHeight * 0.5f, 0.7f, 1.1f);

        Vector3 rayOriginLow = basePos + up * lowCheckHeight;
        Vector3 rayOriginMid = basePos + up * midCheckHeight;

        bool hitMid = UnityPhysics.Raycast(rayOriginMid, forward, out RaycastHit wallHitMid, controller.VaultCheckDistance,
            controller.VaultLayerMask, QueryTriggerInteraction.Ignore);
        bool hitLow = UnityPhysics.Raycast(rayOriginLow, forward, out RaycastHit wallHitLow, controller.VaultCheckDistance,
            controller.VaultLayerMask, QueryTriggerInteraction.Ignore);

        RaycastHit wallHit;
        if (hitLow && hitMid)
        {
            wallHit = wallHitLow.distance <= wallHitMid.distance ? wallHitLow : wallHitMid;
            eval.HasWallHit = true;
        }
        else if (hitLow)
        {
            wallHit = wallHitLow;
            eval.HasWallHit = true;
        }
        else if (hitMid)
        {
            wallHit = wallHitMid;
            eval.HasWallHit = true;
        }
        else
        {
            eval.FailureReason = "No wall in range";
            return eval;
        }

        eval.WallHit = wallHit;

        eval.IsWallVertical = Mathf.Abs(Vector3.Dot(wallHit.normal, up)) < 0.35f;
        if (!eval.IsWallVertical)
        {
            eval.FailureReason = "Wall is not vertical";
            return eval;
        }

        Vector3 inwardPos = wallHit.point - (wallHit.normal * 0.15f);
        Vector3 ledgeRayOrigin =
            Vector3.ProjectOnPlane(inwardPos - basePos, up) + basePos + up * (controller.MaxVaultHeight + 0.15f);
        float dropDistance = controller.MaxVaultHeight + 0.15f - (controller.MinVaultHeight * 0.5f);

        if (!UnityPhysics.Raycast(ledgeRayOrigin, -up, out RaycastHit ledgeHit, dropDistance, controller.VaultLayerMask,
                QueryTriggerInteraction.Ignore))
        {
            eval.FailureReason = "Wall too high (No ledge surface)";
            return eval;
        }

        eval.HasLedgeHit = true;
        eval.LedgeHit = ledgeHit;

        float ledgeHeight = Vector3.Dot(ledgeHit.point - basePos, up);
        eval.LedgeHeight = ledgeHeight;

        if (ledgeHeight < controller.MinVaultHeight)
        {
            eval.FailureReason = $"Ledge too low ({ledgeHeight:F2}m < {controller.MinVaultHeight:F2}m)";
            return eval;
        }

        if (ledgeHeight > controller.MaxVaultHeight)
        {
            eval.FailureReason = $"Ledge too high ({ledgeHeight:F2}m > {controller.MaxVaultHeight:F2}m)";
            return eval;
        }

        eval.IsLedgeSurfaceWalkable = Vector3.Dot(ledgeHit.normal, up) >= 0.7f;
        if (!eval.IsLedgeSurfaceWalkable)
        {
            eval.FailureReason = "Ledge surface too steep";
            return eval;
        }

        Vector3 targetFootPos = ledgeHit.point + (forward * (radius * 1.15f));
        eval.TargetFootPos = targetFootPos;

        Vector3 capsuleBottom = targetFootPos + up * (radius + 0.05f);
        Vector3 capsuleTop = targetFootPos + up * (height - radius);

        eval.IsLandingClear = !UnityPhysics.CheckCapsule(capsuleBottom, capsuleTop, radius * 0.95f, controller.VaultLayerMask,
            QueryTriggerInteraction.Ignore);
        if (!eval.IsLandingClear)
        {
            eval.FailureReason = "Landing space obstructed";
            return eval;
        }

        eval.CanVault = true;
        return eval;
    }

}
}
