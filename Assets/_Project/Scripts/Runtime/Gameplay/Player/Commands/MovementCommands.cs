using System;
using System.Text;
using PedronsaDev.Console;
using Project.Core.Player;
using Project.Player.Locomotion;
using UnityEngine;

namespace Project.Player.Commands
{
[CommandGroup("Movement")]
public static class MovementCommands
{
    [Command("loc_gizmos", "Toggles or sets player movement debug gizmos globally. Usage: loc_gizmos [true/false]")]
    public static void ToggleGizmos(string state = "")
    {
        bool newState;
        if (string.IsNullOrWhiteSpace(state))
        {
            newState = !LocomotionController.GlobalGizmosEnabled;
        }
        else if (bool.TryParse(state, out bool parsed))
        {
            newState = parsed;
        }
        else if (state.Equals("on", StringComparison.OrdinalIgnoreCase) || state.Equals("1"))
        {
            newState = true;
        }
        else if (state.Equals("off", StringComparison.OrdinalIgnoreCase) || state.Equals("0"))
        {
            newState = false;
        }
        else
        {
            newState = !LocomotionController.GlobalGizmosEnabled;
        }

        LocomotionController.GlobalGizmosEnabled = newState;

        if (TryResolveTarget(-1, out var player, out var loco) && loco)
        {
            loco.EnableGizmos = newState;
        }

        string stateTag = newState ? "<color=#4ADE80>ENABLED</color>" : "<color=#F43F5E>DISABLED</color>";
        PedronsaDev.Console.Console.Log($"[Movement] Movement debug gizmos are now {stateTag}.");
    }

    [Command("loc_status",
        "Displays comprehensive real-time locomotion metrics (speed, phase, velocity, ground normal, slope, slide, jump buffer). Usage: loc_status [playerId]")]
    public static void MovementStatus(int playerId = -1)
    {
        if (!TryResolveTarget(playerId, out var player, out var loco))
            return;

        if (!loco)
        {
            PedronsaDev.Console.Console.LogError("[Movement] Target has no LocomotionController.");
            return;
        }

        var motor = loco.Motor;
        var state = loco.State;

        Vector3 velocity = motor ? motor.Velocity : Vector3.zero;
        Vector3 up = motor ? motor.CharacterUp : Vector3.up;
        Vector3 planarVel = Vector3.ProjectOnPlane(velocity, up);
        float totalSpeed = velocity.magnitude;
        float hSpeed = planarVel.magnitude;
        float vSpeed = Vector3.Dot(velocity, up);

        StringBuilder sb = new();
        sb.AppendLine("=== PLAYER MOVEMENT STATUS ===");
        sb.AppendLine(
            $"Target: {player.gameObject.name} (PlayerID: {(player.TryGetComponent<PlayerIdentifier>(out var id) && id.PlayerID.HasValue ? id.PlayerID.Value.ToString() : "Local")})");
        sb.AppendLine($"Phase: <b>{state.Phase}</b>");
        sb.AppendLine(
            $"Speed: <b>{totalSpeed:F2} m/s</b> (Horizontal: {hSpeed:F2} m/s, Vertical: {vSpeed:+0.00;-0.00;0.00} m/s)");
        sb.AppendLine($"Velocity: ({velocity.x:F2}, {velocity.y:F2}, {velocity.z:F2})");
        sb.AppendLine(
            $"Move Input: ({state.MoveInput.x:F2}, {state.MoveInput.y:F2}) | Magnitude: {state.MoveInputMagnitude:F2}");

        if (motor)
        {
            var ground = motor.GroundingStatus;
            float slopeAngle = ground.FoundAnyGround ? Vector3.Angle(ground.GroundNormal, Vector3.up) : 0f;
            string groundStatus = ground.FoundAnyGround
                ? (ground.IsStableOnGround
                    ? "<color=#4ADE80>Stable</color>"
                    : "<color=#F43F5E>Unstable / Sliding</color>")
                : "<color=#818CF8>Airborne</color>";

            sb.AppendLine(
                $"Grounding: {groundStatus} | Slope: {slopeAngle:F1}\u00b0 | Normal: ({ground.GroundNormal.x:F2}, {ground.GroundNormal.y:F2}, {ground.GroundNormal.z:F2})");
            sb.AppendLine(
                $"Capsule: Height: {motor.Capsule.height:F2}m (Standing: {loco.StandingHeight:F2}m, Crouch: {loco.CrouchingHeight:F2}m, Downed: {loco.DownedCapsuleHeight:F2}m, Radius: {motor.Capsule.radius:F2}m)");
        }

        sb.AppendLine(
            $"Stance: Sprint: {state.IsSprinting} | Crouch: {state.IsCrouching} | Slide: {state.IsSliding} | Downed: {loco.IsDowned} | Dragging: {loco.IsDraggingTeammate}");
        sb.AppendLine(
            $"Jumping: Status: {state.CurrentJumpStatus} | Buffer: {state.JumpBufferTimer:F2}s | CanWallJump: {state.CanWallJump} | WallJumpNormal: ({state.WallJumpNormal.x:F2}, {state.WallJumpNormal.y:F2}, {state.WallJumpNormal.z:F2})");

        var vaultEval = loco.EvaluateVault();
        string vaultText = vaultEval.CanVault
            ? $"<color=#4ADE80>READY</color> (Height: {vaultEval.LedgeHeight:F2}m)"
            : $"<color=#F43F5E>NOT AVAILABLE</color> ({vaultEval.FailureReason})";
        sb.AppendLine($"Vaulting: {vaultText} | Limits: [{loco.MinVaultHeight:F2}m - {loco.MaxVaultHeight:F2}m]");
        sb.AppendLine(
            $"Gizmos: Global: {(LocomotionController.GlobalGizmosEnabled ? "Enabled" : "Disabled")} | Controller: {(loco.EnableGizmos ? "Enabled" : "Disabled")}");

        PedronsaDev.Console.Console.Log(sb.ToString());
    }

    [Command("loc_noclip", "Toggles noclip mode. Usage: noclip [0/1] [playerId]")]
    public static void ToggleNoclip(int mode = -1, int playerId = -1)
    {
        if (!TryResolveTarget(playerId, out var player, out var loco))
            return;

        if (!loco)
        {
            PedronsaDev.Console.Console.LogError("[Movement] Target has no LocomotionController.");
            return;
        }

        bool targetState = mode switch
        {
            0 => false,
            1 => true,
            _ => !loco.IsNoclipEnabled
        };

        loco.SetNoclipEnabled(targetState);

        string stateTag = targetState ? "<color=#4ADE80>ENABLED</color>" : "<color=#F43F5E>DISABLED</color>";
        PedronsaDev.Console.Console.Log($"[Movement] Noclip mode {stateTag} for {player.gameObject.name}.");
    }

    private static bool TryResolveTarget(int playerId, out PlayerFacade player, out LocomotionController loco)
    {
        player = null;
        loco = null;

        if (playerId < 0)
        {
            player = LocalPlayerContext.Player;
            if (!player)
            {
                player = UnityEngine.Object.FindAnyObjectByType<PlayerFacade>();
            }

            if (!player)
            {
                PedronsaDev.Console.Console.LogError("[Movement] No local player found in scene.");
                return false;
            }

            loco = player.LocomotionController ?? player.GetComponent<LocomotionController>() ??
                player.GetComponentInChildren<LocomotionController>(true) ??
                player.GetComponentInParent<LocomotionController>(true);
            return true;
        }

        var activePlayers = PlayerIdentifier.ActivePlayers;
        if (activePlayers != null)
        {
            for (int i = 0; i < activePlayers.Count; i++)
            {
                var identifier = activePlayers[i];
                if (identifier && identifier.PlayerID.HasValue && identifier.PlayerID.Value.id.value == (ulong)playerId)
                {
                    player = identifier.GetComponent<PlayerFacade>() ??
                             identifier.GetComponentInParent<PlayerFacade>() ??
                             identifier.GetComponentInChildren<PlayerFacade>(true);
                    if (!player)
                        player = identifier.transform.root.GetComponentInChildren<PlayerFacade>(true);

                    if (player)
                    {
                        loco = player.LocomotionController ?? player.GetComponent<LocomotionController>() ??
                            player.GetComponentInChildren<LocomotionController>(true) ??
                            player.GetComponentInParent<LocomotionController>(true);
                        return true;
                    }
                }
            }
        }

        PedronsaDev.Console.Console.LogError($"[Movement] Could not find player with ID {playerId}.");
        return false;
    }
}
}