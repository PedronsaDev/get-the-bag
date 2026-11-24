using Project.Player.Locomotion;
using UnityEditor;
using UnityEngine;
using UnityPhysics = UnityEngine.Physics;

namespace Project.Core.Editor
{
internal static class LocomotionGizmos
{
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void Draw(LocomotionController controller, GizmoType type)
    {
        var selected = (type & GizmoType.Selected) != 0;
        var settings = new SerializedObject(controller);
        if (!selected && settings.FindProperty("_drawOnlyWhenSelected").boolValue) return;
        var motor = controller.Motor;

        if (!LocomotionController.GlobalGizmosEnabled || !controller.EnableGizmos) return;
        if (!motor) return;

        Vector3 pos = controller.transform.position;
        Vector3 up = motor.CharacterUp;
        Vector3 fwd = controller.transform.forward;
        Vector3 rgt = controller.transform.right;

        float radius = motor.Capsule ? motor.Capsule.radius : 0.5f;
        float height = motor.Capsule ? motor.Capsule.height : controller.StandingHeight;
        Vector3 center = pos + up * (height * 0.5f);

        if (settings.FindProperty("_drawCapsuleBounds").boolValue)
        {
            Color capsuleColor = GetStateColor(controller);
            capsuleColor.a = selected ? 0.9f : 0.6f;
            DrawWireCapsule(pos, up, fwd, rgt, radius, height, capsuleColor);
        }

        if (settings.FindProperty("_drawCeilingCheck").boolValue && height < controller.StandingHeight - 0.05f)
        {
            float checkDist = controller.StandingHeight - height;
            Vector3 checkOrigin = pos + up * (height - radius);
            bool blocked = UnityPhysics.SphereCast(checkOrigin, radius * 0.9f, up, out RaycastHit ceilHit, checkDist,
                settings.FindProperty("_ceilingCheckLayerMask").intValue, QueryTriggerInteraction.Ignore);

            if (blocked)
            {
                UnityEditor.Handles.color = new Color(0.95f, 0.25f, 0.25f, 0.9f);
                UnityEditor.Handles.DrawDottedLine(checkOrigin, ceilHit.point, 3f);
                UnityEditor.Handles.DrawWireDisc(ceilHit.point, ceilHit.normal, radius * 0.5f);
                UnityEditor.Handles.Label(ceilHit.point + up * 0.15f, "CEILING BLOCKED",
                    GetDebugMiniLabelStyle(new Color(0.95f, 0.25f, 0.25f, 1f)));
            }
            else
            {
                UnityEditor.Handles.color = new Color(0.2f, 0.9f, 0.3f, 0.6f);
                UnityEditor.Handles.DrawLine(checkOrigin, checkOrigin + up * checkDist);
                UnityEditor.Handles.DrawWireDisc(checkOrigin + up * checkDist, up, radius);
            }
        }

        if (settings.FindProperty("_drawVelocityVector").boolValue)
        {
            Vector3 velocity = motor.Velocity;
            float speed = velocity.magnitude;
            if (speed > 0.05f)
            {
                Color velColor = GetStateColor(controller);
                velColor.a = 1f;
                float arrowLen = Mathf.Clamp(speed * 0.3f, 0.5f, 4f);
                Vector3 velDir = velocity.normalized;
                Vector3 velTip = center + velDir * arrowLen;

                UnityEditor.Handles.color = velColor;
                UnityEditor.Handles.DrawLine(center, velTip);
                DrawArrowHead(velTip, velDir, up, 0.18f, velColor);

                UnityEditor.Handles.DrawSolidDisc(center, up, 0.06f);

                if (selected)
                {
                    UnityEditor.Handles.Label(velTip + up * 0.1f, $"{speed:F1} m/s", GetDebugMiniLabelStyle(velColor));
                }
            }
        }

        if (settings.FindProperty("_drawInputVector").boolValue && controller.MoveInputVector.sqrMagnitude > 0.01f)
        {
            Color inputColor = new Color(1.0f, 0.65f, 0.0f, 0.95f);
            Vector3 inputOrigin = pos + up * 0.05f;
            Vector3 inputDir = controller.MoveInputVector.normalized;
            float inputLen = Mathf.Clamp(controller.MoveInputVector.magnitude * 1.2f, 0.4f, 1.5f);
            Vector3 inputTip = inputOrigin + inputDir * inputLen;

            UnityEditor.Handles.color = inputColor;
            UnityEditor.Handles.DrawLine(inputOrigin, inputTip);
            DrawArrowHead(inputTip, inputDir, up, 0.14f, inputColor);
            UnityEditor.Handles.DrawWireDisc(inputOrigin, up, 0.1f);
        }

        if (settings.FindProperty("_drawGroundStatus").boolValue)
        {
            if (motor.GroundingStatus.FoundAnyGround)
            {
                Vector3 gPoint = motor.GroundingStatus.GroundPoint;
                Vector3 gNormal = motor.GroundingStatus.GroundNormal;
                bool isStable = motor.GroundingStatus.IsStableOnGround;
                Color groundColor = isStable ? new Color(0.2f, 0.85f, 0.3f, 0.9f) : new Color(0.95f, 0.3f, 0.2f, 0.9f);

                UnityEditor.Handles.color = groundColor;
                UnityEditor.Handles.DrawWireDisc(gPoint, gNormal, 0.35f);
                UnityEditor.Handles.color = new Color(groundColor.r, groundColor.g, groundColor.b, 0.15f);
                UnityEditor.Handles.DrawSolidDisc(gPoint, gNormal, 0.35f);

                UnityEditor.Handles.color = groundColor;
                Vector3 normalTip = gPoint + gNormal * 0.7f;
                UnityEditor.Handles.DrawLine(gPoint, normalTip);
                DrawArrowHead(normalTip, gNormal, fwd, 0.12f, groundColor);

                float slopeAngle = Vector3.Angle(gNormal, Vector3.up);
                if (selected)
                {
                    string slopeText = isStable ? $"Slope: {slopeAngle:F1}\u00b0" : $"UNSTABLE ({slopeAngle:F1}\u00b0)";
                    UnityEditor.Handles.Label(normalTip + up * 0.1f, slopeText, GetDebugMiniLabelStyle(groundColor));
                }
            }
            else
            {
                if (UnityPhysics.Raycast(pos, -up, out RaycastHit hit, 50f, settings.FindProperty("_ceilingCheckLayerMask").intValue,
                        QueryTriggerInteraction.Ignore))
                {
                    UnityEditor.Handles.color = new Color(0.6f, 0.4f, 0.95f, 0.8f);
                    UnityEditor.Handles.DrawDottedLine(pos, hit.point, 3f);
                    UnityEditor.Handles.DrawWireDisc(hit.point, hit.normal, radius * 0.6f);

                    float altitude = hit.distance;
                    if (selected && altitude > 0.3f)
                    {
                        UnityEditor.Handles.Label(hit.point + hit.normal * 0.1f, $"Alt: {altitude:F1}m",
                            GetDebugMiniLabelStyle(new Color(0.6f, 0.4f, 0.95f, 1f)));
                    }
                }
            }
        }

        if (settings.FindProperty("_drawWallAndVaultChecks").boolValue)
        {
            Vector3 waistPos = pos + up * (height * 0.5f);
            float wallCheckDist = radius + 0.25f;

            UnityEditor.Handles.color = new Color(0.2f, 0.75f, 0.9f, 0.5f);
            UnityEditor.Handles.DrawLine(waistPos, waistPos + fwd * wallCheckDist);
            UnityEditor.Handles.DrawLine(waistPos, waistPos - fwd * wallCheckDist);
            UnityEditor.Handles.DrawLine(waistPos, waistPos + rgt * wallCheckDist);
            UnityEditor.Handles.DrawLine(waistPos, waistPos - rgt * wallCheckDist);

            if (controller.State is { CanWallJump: true, WallJumpNormal: { sqrMagnitude: > 0.1f } })
            {
                Color wjColor = new Color(0.1f, 0.9f, 0.9f, 1f);
                Vector3 wjTip = waistPos + controller.State.WallJumpNormal * 1.2f;
                UnityEditor.Handles.color = wjColor;
                UnityEditor.Handles.DrawLine(waistPos, wjTip);
                DrawArrowHead(wjTip, controller.State.WallJumpNormal, up, 0.15f, wjColor);
                UnityEditor.Handles.Label(wjTip + up * 0.15f, "WALL JUMP READY", GetDebugMiniLabelStyle(wjColor));
            }

            var vaultEval = controller.EvaluateVault();

            Vector3 lookFwd = (Application.isPlaying && controller.LookInputVector.sqrMagnitude > 0.1f)
                ? controller.LookInputVector.normalized
                : fwd;
            Vector3 rayOriginMid = pos + up * Mathf.Clamp(controller.MaxVaultHeight * 0.5f, 0.7f, 1.1f);

            UnityEditor.Handles.color = vaultEval.HasWallHit
                ? (vaultEval.CanVault ? Color.green : new Color(0.95f, 0.6f, 0.1f, 0.8f))
                : new Color(0.2f, 0.5f, 1.0f, 0.4f);
            UnityEditor.Handles.DrawLine(rayOriginMid, rayOriginMid + lookFwd * settings.FindProperty("_vaultCheckDistance").floatValue);

            if (vaultEval.HasWallHit)
            {
                UnityEditor.Handles.color = vaultEval.IsWallVertical ? Color.cyan : Color.red;
                UnityEditor.Handles.DrawWireDisc(vaultEval.WallHit.point, vaultEval.WallHit.normal, 0.08f);

                Vector3 inwardPos = vaultEval.WallHit.point - (vaultEval.WallHit.normal * 0.15f);
                Vector3 ledgeRayOrigin =
                    Vector3.ProjectOnPlane(inwardPos - pos, up) + pos + up * (controller.MaxVaultHeight + 0.15f);
                float dropDistance = controller.MaxVaultHeight + 0.15f - (controller.MinVaultHeight * 0.5f);

                UnityEditor.Handles.color = vaultEval.HasLedgeHit
                    ? (vaultEval.CanVault ? Color.green : new Color(0.95f, 0.35f, 0.2f, 0.9f))
                    : Color.gray;
                UnityEditor.Handles.DrawLine(ledgeRayOrigin, ledgeRayOrigin - up * dropDistance);

                if (vaultEval.HasLedgeHit)
                {
                    UnityEditor.Handles.DrawSolidDisc(vaultEval.LedgeHit.point, vaultEval.LedgeHit.normal, 0.06f);

                    if (vaultEval.CanVault)
                    {
                        DrawWireCapsule(vaultEval.TargetFootPos, up, fwd, rgt, radius, height,
                            new Color(0.2f, 0.9f, 0.4f, 0.6f));
                        UnityEditor.Handles.Label(vaultEval.TargetFootPos + up * height,
                            $"VAULT READY ({vaultEval.LedgeHeight:F2}m)", GetDebugMiniLabelStyle(Color.green));
                    }
                    else if (selected)
                    {
                        Color failColor = new Color(0.95f, 0.35f, 0.2f, 1f);
                        DrawWireCapsule(vaultEval.TargetFootPos, up, fwd, rgt, radius, height,
                            new Color(0.95f, 0.35f, 0.2f, 0.25f));
                        UnityEditor.Handles.Label(vaultEval.TargetFootPos + up * (height * 0.5f),
                            $"CANNOT VAULT:\n{vaultEval.FailureReason}", GetDebugMiniLabelStyle(failColor));
                    }
                }
                else if (selected)
                {
                    UnityEditor.Handles.Label(ledgeRayOrigin - up * (dropDistance * 0.5f),
                        "WALL TOO HIGH\n(No Ledge In Reach)",
                        GetDebugMiniLabelStyle(new Color(0.95f, 0.35f, 0.2f, 1f)));
                }
            }
        }

        if (settings.FindProperty("_drawOverheadStatus").boolValue)
        {
            Vector3 headPos = pos + up * (height + 0.4f);
            float totalSpeed = motor.Velocity.magnitude;
            Vector3 planarVel = Vector3.ProjectOnPlane(motor.Velocity, up);
            float hSpeed = planarVel.magnitude;
            float vSpeed = Vector3.Dot(motor.Velocity, up);
            float slopeAngle = motor.GroundingStatus.FoundAnyGround
                ? Vector3.Angle(motor.GroundingStatus.GroundNormal, Vector3.up)
                : 0f;

            string statusText = $"[LOCO] {controller.State.Phase}\n" +
                                $"Speed: {totalSpeed:F1} m/s (H:{hSpeed:F1} V:{vSpeed:+0.0;-0.0;0.0})\n" +
                                $"Ground: {(motor.GroundingStatus.FoundAnyGround ? (motor.GroundingStatus.IsStableOnGround ? "Stable" : "Unstable") : "Airborne")} ({slopeAngle:F1}\u00b0)\n" +
                                $"Height: {height:F2}m / {controller.StandingHeight:F2}m";

            Color labelColor = GetStateColor(controller);
            var labelStyle = new GUIStyle
            {
                normal = { textColor = labelColor },
                fontSize = selected ? 11 : 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            UnityEditor.Handles.Label(headPos, statusText, labelStyle);
        }
    }

    private static Color GetStateColor(LocomotionController controller)
    {
        if (controller.State.Phase == LocomotionPhase.Noclip) return new Color(0.2f, 0.9f, 1f, 1f);
        if (controller.IsDowned) return new Color(0.96f, 0.26f, 0.21f, 1f);
        if (controller.State.Phase == LocomotionPhase.Vaulting) return new Color(0.13f, 0.77f, 0.36f, 1f);
        if (controller.State.IsSliding) return new Color(0.14f, 0.78f, 0.86f, 1f);
        if (controller.State.Phase == LocomotionPhase.Airborne) return new Color(0.66f, 0.44f, 0.98f, 1f);
        if (controller.State.IsSprinting) return new Color(0.96f, 0.62f, 0.07f, 1f);
        if (controller.State.IsCrouching) return new Color(0.92f, 0.45f, 0.15f, 1f);
        return new Color(0.22f, 0.74f, 0.58f, 1f);
    }

    private static void DrawArrowHead(Vector3 tip, Vector3 direction, Vector3 normal, float size, Color color)
    {
        Vector3 right = Vector3.Cross(direction, normal).normalized;
        if (right.sqrMagnitude < 0.01f)
            right = Vector3.Cross(direction, Vector3.forward).normalized;

        Vector3 back = tip - direction * size;
        UnityEditor.Handles.color = color;
        UnityEditor.Handles.DrawLine(tip, back + right * (size * 0.5f));
        UnityEditor.Handles.DrawLine(tip, back - right * (size * 0.5f));
        UnityEditor.Handles.DrawLine(back + right * (size * 0.5f), back - right * (size * 0.5f));
    }

    private static void DrawWireCapsule(Vector3 basePos, Vector3 up, Vector3 forward, Vector3 right, float radius,
        float height, Color color)
    {
        UnityEditor.Handles.color = color;
        float halfHeightMinusRadius = Mathf.Max(0.01f, (height * 0.5f) - radius);
        Vector3 center = basePos + up * (height * 0.5f);
        Vector3 bottomSphere = center - up * halfHeightMinusRadius;
        Vector3 topSphere = center + up * halfHeightMinusRadius;

        UnityEditor.Handles.DrawWireDisc(bottomSphere, up, radius);
        UnityEditor.Handles.DrawWireArc(bottomSphere, right, forward, 180f, radius);
        UnityEditor.Handles.DrawWireArc(bottomSphere, -forward, right, 180f, radius);

        UnityEditor.Handles.DrawWireDisc(topSphere, up, radius);
        UnityEditor.Handles.DrawWireArc(topSphere, -right, forward, 180f, radius);
        UnityEditor.Handles.DrawWireArc(topSphere, forward, right, 180f, radius);

        UnityEditor.Handles.DrawLine(bottomSphere + forward * radius, topSphere + forward * radius);
        UnityEditor.Handles.DrawLine(bottomSphere - forward * radius, topSphere - forward * radius);
        UnityEditor.Handles.DrawLine(bottomSphere + right * radius, topSphere + right * radius);
        UnityEditor.Handles.DrawLine(bottomSphere - right * radius, topSphere - right * radius);
    }

    private static GUIStyle GetDebugMiniLabelStyle(Color textColor)
    {
        return new GUIStyle
        {
            normal = { textColor = textColor },
            fontSize = 9,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
    }

}
}
