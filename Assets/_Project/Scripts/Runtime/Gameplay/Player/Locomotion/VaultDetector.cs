using UnityEngine;
using UnityPhysics = UnityEngine.Physics;

namespace Project.Player.Locomotion
{

internal static class VaultDetector
{
    // Spacing of the downward probes used to find thin ledges and measure how deep they are.
    private const float ProbeStep = 0.1f;

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
        // Vault in the direction the player is trying to move (or already moving), not where the camera looks.
        Vector3 forward;
        Vector3 moveInput = Vector3.ProjectOnPlane(controller.MoveInputVector, up);
        Vector3 planarVelocity = Vector3.ProjectOnPlane(controller.Motor.BaseVelocity, up);
        if (moveInput.sqrMagnitude > 0.09f)
            forward = moveInput.normalized;
        else if (planarVelocity.sqrMagnitude > 1f)
            forward = planarVelocity.normalized;
        else
        {
            eval.FailureReason = "No movement intent";
            return eval;
        }

        float radius = controller.Motor.Capsule ? controller.Motor.Capsule.radius : 0.5f;

        // Short ray: only walls the player is actually touching (or about to) count.
        float rayDistance = Mathf.Min(controller.VaultCheckDistance, radius + controller.VaultTriggerDistance);
        // Landing space must fit the standing capsule, even if the player is currently crouched.
        float height = controller.StandingHeight;

        float lowCheckHeight = Mathf.Clamp(controller.MinVaultHeight, 0.3f, 0.6f);
        float midCheckHeight = Mathf.Clamp(controller.MaxVaultHeight * 0.5f, 0.7f, 1.1f);

        Vector3 rayOriginLow = basePos + up * lowCheckHeight;
        Vector3 rayOriginMid = basePos + up * midCheckHeight;

        bool hitMid = UnityPhysics.Raycast(rayOriginMid, forward, out RaycastHit wallHitMid, rayDistance,
            controller.VaultLayerMask, QueryTriggerInteraction.Ignore);
        bool hitLow = UnityPhysics.Raycast(rayOriginLow, forward, out RaycastHit wallHitLow, rayDistance,
            controller.VaultLayerMask, QueryTriggerInteraction.Ignore);

        bool haveWall = hitLow || hitMid;
        RaycastHit wallHit = default;
        if (hitLow && hitMid)
            wallHit = wallHitLow.distance <= wallHitMid.distance ? wallHitLow : wallHitMid;
        else if (hitLow)
            wallHit = wallHitLow;
        else if (hitMid)
            wallHit = wallHitMid;

        RaycastHit ledgeHit = default;
        Vector3 inward = forward;
        Vector3 targetFootPos = default;
        bool gotLedge = false;

        if (haveWall)
        {
            eval.HasWallHit = true;
            eval.WallHit = wallHit;

            eval.IsWallVertical = Mathf.Abs(Vector3.Dot(wallHit.normal, up)) < 0.35f;
            if (!eval.IsWallVertical)
            {
                eval.FailureReason = "Wall is not vertical";
                return eval;
            }

            inward = -Vector3.ProjectOnPlane(wallHit.normal, up).normalized;
            if (Vector3.Dot(forward, inward) < 0.35f)
            {
                eval.InwardDirection = inward;
                eval.FailureReason = "Approach too oblique";
                return eval;
            }

            Vector3 inwardPos = wallHit.point - (wallHit.normal * 0.15f);
            Vector3 ledgeRayOrigin =
                Vector3.ProjectOnPlane(inwardPos - basePos, up) + basePos + up * (controller.MaxVaultHeight + 0.15f);
            float dropDistance = controller.MaxVaultHeight + 0.15f - (controller.MinVaultHeight * 0.5f);

            if (UnityPhysics.Raycast(ledgeRayOrigin, -up, out ledgeHit, dropDistance, controller.VaultLayerMask,
                    QueryTriggerInteraction.Ignore))
            {
                gotLedge = true;
                // Offset along the wall normal so oblique approaches still land fully on the ledge.
                targetFootPos = ledgeHit.point + (inward * (radius * 1.15f));
            }
        }

        // No wall face at probe height (thin plank / rail), or the wall-based probe missed a narrow top:
        // look straight down in a column ahead of the player instead.
        if (!gotLedge)
        {
            if (!TryProbeColumn(controller, basePos, up, forward, radius, out ledgeHit, out targetFootPos))
            {
                eval.FailureReason = haveWall ? "Wall too high (No ledge surface)" : "No wall or ledge in range";
                return eval;
            }

            inward = forward;
        }

        eval.InwardDirection = inward;
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

    /// <summary>
    /// Finds a ledge without needing a wall face: drops rays in a column ahead of the player, takes the first walkable
    /// surface inside the vault height window, measures how deep it is, and centres the target on what supports the
    /// capsule (so a 10 cm plank puts the player on the plank, not past it).
    /// </summary>
    private static bool TryProbeColumn(LocomotionController controller, Vector3 basePos, Vector3 up, Vector3 forward,
        float radius, out RaycastHit ledgeHit, out Vector3 targetFootPos)
    {
        ledgeHit = default;
        targetFootPos = default;

        float top = controller.MaxVaultHeight + 0.15f;
        float drop = top - controller.MinVaultHeight * 0.5f;
        float reach = radius + controller.VaultTriggerDistance;
        LayerMask mask = controller.VaultLayerMask;

        for (float d = radius + 0.05f; d <= reach + 0.001f; d += ProbeStep)
        {
            Vector3 origin = basePos + forward * d + up * top;
            if (!UnityPhysics.Raycast(origin, -up, out RaycastHit hit, drop, mask, QueryTriggerInteraction.Ignore))
                continue;

            float h = Vector3.Dot(hit.point - basePos, up);
            if (h < controller.MinVaultHeight || h > controller.MaxVaultHeight) continue;
            if (Vector3.Dot(hit.normal, up) < 0.7f) continue;

            // Something taller than the ledge between us and it (a thin wall) means it isn't reachable.
            Vector3 pathOrigin = basePos + up * (h + 0.25f);
            if (UnityPhysics.Raycast(pathOrigin, forward, d, mask, QueryTriggerInteraction.Ignore)) continue;

            // Measure how far the surface continues at this height.
            float supportEnd = d;
            float maxDepth = radius * 1.15f + 0.15f;
            for (float s = d + ProbeStep; s <= d + maxDepth + 0.001f; s += ProbeStep)
            {
                Vector3 sOrigin = basePos + forward * s + up * top;
                if (UnityPhysics.Raycast(sOrigin, -up, out RaycastHit sHit, drop, mask, QueryTriggerInteraction.Ignore) &&
                    Mathf.Abs(Vector3.Dot(sHit.point - hit.point, up)) < 0.06f)
                    supportEnd = s;
                else
                    break;
            }

            float depth = supportEnd - d;
            bool wide = depth >= maxDepth - 0.001f;
            float targetDistance = wide ? d + maxDepth : d + depth * 0.5f + 0.05f;

            ledgeHit = hit;
            targetFootPos = basePos + forward * targetDistance + up * h;
            return true;
        }

        return false;
    }

}
}
