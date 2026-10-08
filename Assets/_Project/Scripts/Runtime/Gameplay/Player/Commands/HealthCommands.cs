using System.Text;
using PedronsaDev.Console;
using Project.Combat;
using Project.Combat.Core;
using Project.Combat.Health;
using Project.Core.Player;
using Project.Player.Locomotion;
using UnityEngine;

namespace Project.Player.Commands
{
    [CommandGroup("Combat & Health")]
    public static class HealthCommands
    {
        [Command("hp_status", "Displays comprehensive health, shield, downed state, and bleedout timers for a player (default: local player). Usage: hp_status [playerId]")]
        public static void HealthStatus(int playerId = -1)
        {
            if (!TryResolveTarget(playerId, out var player, out var health, out var downed, requireServer: false))
                return;

            StringBuilder sb = new();
            sb.AppendLine("=== PLAYER HEALTH & DOWNED STATUS ===");
            sb.AppendLine($"Target: {player.gameObject.name} (PlayerID: {(player.TryGetComponent<PlayerIdentifier>(out var id) && id.PlayerID.HasValue ? id.PlayerID.Value.ToString() : "N/A")})");

            if (health)
            {
                sb.AppendLine($"Health: {health.CurrentHealth:F1} / {health.MaxHealth:F1} ({(health.CurrentHealth / health.MaxHealth * 100f):F0}%)");
                sb.AppendLine($"Shield: {health.CurrentShield:F1} / {health.MaxShield:F1}");
                sb.AppendLine($"Invulnerable: {health.Invulnerable} | IsDead: {health.IsDead}");
            }
            else
            {
                sb.AppendLine("HealthComponent: None");
            }

            if (downed)
            {
                sb.AppendLine($"IsDown: {downed.IsDowned}");
                sb.AppendLine($"Bleedout Timer: {downed.BleedOutTimer.value:F1}s");
                sb.AppendLine($"IsBeingDragged: {downed.IsBeingDragged}");
                sb.AppendLine($"DraggedBy: {(downed.DraggedByPlayer.value.HasValue ? downed.DraggedByPlayer.value.Value.ToString() : "None")}");
            }
            else
            {
                sb.AppendLine("PlayerDownedState: None");
            }

            if (player.LocomotionController)
            {
                var loco = player.LocomotionController;
                sb.AppendLine($"Locomotion Phase: {loco.State.Phase} | AnimationState: {loco.State.AnimationState}");
                sb.AppendLine($"IsDowned: {loco.IsDowned} | IsDraggingTeammate: {loco.IsDraggingTeammate}");
            }

            Console.Log(sb.ToString());
        }

        [Command("hp_damage", "Deals damage to the player (absorbed by shields first). Usage: hp_damage <amount> [playerId]")]
        public static void Damage(float amount = 25f, int playerId = -1)
        {
            if (!TryResolveTarget(playerId, out var player, out var health, out var downed))
                return;

            if (!health)
            {
                Console.LogError("[Combat & Health] Target has no HealthComponent.");
                return;
            }

            if (health.IsDead || (downed && downed.IsDowned))
            {
                Console.LogWarning($"[Combat & Health] {player.gameObject.name} is already downed/dead (HP: {health.CurrentHealth:F1}). Use 'revive' first.");
                return;
            }

            var result = health.TakeDamage(new DamagePayload
            {
                Amount = amount,
                Location = HitLocation.Core,
                HitPoint = player.transform.position + Vector3.up,
                HitNormal = Vector3.up
            });

            if (!result.Applied)
            {
                Console.LogWarning($"[Combat & Health] Damage was not applied to {player.gameObject.name} (Invulnerable: {health.Invulnerable}, IsDead: {health.IsDead}).");
                return;
            }

            Console.Log($"[Combat & Health] Dealt {result.EffectiveDamage:F1} damage to {player.gameObject.name} (Shield: -{result.DamageToShield:F1}, HP: -{result.DamageToHealth:F1}) | Shield: {health.CurrentShield:F1}/{health.MaxShield:F1} | HP: {health.CurrentHealth:F1}/{health.MaxHealth:F1}");
        }

        [Command("hp_heal", "Heals the player by specified amount. Usage: hp_heal <amount> [playerId]")]
        public static void Heal(float amount = 50f, int playerId = -1)
        {
            if (!TryResolveTarget(playerId, out var player, out var health, out _))
                return;

            if (!health)
            {
                Console.LogError("[Combat & Health] Target has no HealthComponent.");
                return;
            }

            health.Heal(amount);
            Console.Log($"[Combat & Health] Requested {amount:F1} HP heal for {player.gameObject.name}. Current HP: {health.CurrentHealth:F1}/{health.MaxHealth:F1}");
        }

        [Command("hp_heal_full", "Fully heals, restores shields, and revives the player if downed or dead. Usage: hp_heal_full [playerId]")]
        public static void HealFull(int playerId = -1)
        {
            if (!TryResolveTarget(playerId, out var player, out var health, out var downed))
                return;

            if (!health)
            {
                Console.LogError("[Combat & Health] Target has no HealthComponent.");
                return;
            }

            if (downed && downed.IsDowned)
            {
                downed.ReviveServer(1f);
            }
            else if (health.IsDead)
            {
                health.Revive(health.MaxHealth);
            }

            health.Heal(health.MaxHealth);
            health.RechargeShield(health.MaxShield);
            Console.Log($"[Combat & Health] Fully restored health ({health.MaxHealth:F1}) and shields ({health.MaxShield:F1}) for {player.gameObject.name}.");
        }

        [Command("hp_down", "Forces the target player into the downed state. Usage: hp_down [playerId]")]
        public static void Down(int playerId = -1)
        {
            if (!TryResolveTarget(playerId, out var player, out _, out var downed))
                return;

            if (!downed)
            {
                Console.LogError("[Combat & Health] Target has no PlayerDownedState.");
                return;
            }

            if (downed.IsDowned)
            {
                Console.LogWarning($"[Combat & Health] {player.gameObject.name} is already downed!");
                return;
            }

            downed.DownServer();
            Console.Log($"[Combat & Health] Successfully downed {player.gameObject.name}.");
        }

        [Command("hp_revive", "Revives a downed or dead player. Usage: hp_revive [playerId]")]
        public static void Revive(int playerId = -1)
        {
            if (!TryResolveTarget(playerId, out var player, out var health, out var downed))
                return;

            bool isDowned = downed && downed.IsDowned;
            bool isDead = health && health.IsDead;

            if (!isDowned && !isDead)
            {
                PedronsaDev.Console.Console.LogWarning($"[Combat & Health] {player.gameObject.name} is neither downed nor dead (HP: {(health ? health.CurrentHealth : 100f):F1}).");
                return;
            }

            if (downed)
            {
                downed.ReviveServer();
            }
            else if (health)
            {
                health.Revive(health.MaxHealth * 0.5f);
            }

            Console.Log($"[Combat & Health] Successfully revived {player.gameObject.name}. Remaining HP: {(health ? health.CurrentHealth : 0f):F1}");
        }

        [Command("hp_bleedout_set", "Sets the remaining bleedout timer on a downed player. Usage: hp_bleedout_set <seconds> [playerId]")]
        public static void SetBleedout(float seconds = 5f, int playerId = -1)
        {
            if (!TryResolveTarget(playerId, out var player, out _, out var downed))
                return;

            if (!downed)
            {
                Console.LogError("[Combat & Health] Target has no PlayerDownedState.");
                return;
            }

            if (!downed.IsDowned)
            {
                Console.LogWarning($"[Combat & Health] {player.gameObject.name} is not currently downed.");
                return;
            }

            downed.SetBleedOutTimerServer(seconds);
            Console.Log($"[Combat & Health] Set bleedout timer to {seconds:F1}s for {player.gameObject.name}.");
        }

        [Command("hp_god", "Toggles invulnerability on the player. Usage: hp_god [playerId]")]
        public static void ToggleGodMode(int playerId = -1)
        {
            if (!TryResolveTarget(playerId, out var player, out var health, out _))
                return;

            if (!health)
            {
                Console.LogError("[Combat & Health] Target has no HealthComponent.");
                return;
            }

            bool newState = !health.Invulnerable;
            health.SetInvulnerable(newState);
            Console.Log($"[Combat & Health] Invulnerability for {player.gameObject.name} set to: {newState}");
        }

        [Command("hp_kill", "Instantly executes true death on the player. Usage: hp_kill [playerId]")]
        public static void Kill(int playerId = -1)
        {
            if (!TryResolveTarget(playerId, out var player, out _, out var downed))
                return;

            if (!downed)
            {
                Console.LogError("[Combat & Health] Target has no PlayerDownedState.");
                return;
            }

            downed.KillServer();
            Console.Log($"[Combat & Health] Executed true death for {player.gameObject.name}.");
        }

        [Command("hc_drag_release", "Forces release of any active dragging on this player. Usage: drag_release [playerId]")]
        public static void DragRelease(int playerId = -1)
        {
            if (!TryResolveTarget(playerId, out var player, out _, out var downed))
                return;

            if (!downed)
            {
                Console.LogError("[Combat & Health] Target has no PlayerDownedState.");
                return;
            }

            downed.RequestStopDrag();
            Console.Log($"[Combat & Health] Drag released for {player.gameObject.name}.");
        }

        private static bool CheckAuthority(PlayerFacade player, bool requireServer)
        {
            if (!requireServer || player.isServer) return true;
            Console.LogWarning("[Combat & Health] Run health-changing commands on the host/server.");
            return false;
        }

        private static bool TryResolveTarget(int playerId, out PlayerFacade player, out HealthComponent health, out PlayerDownedState downed, bool requireServer = true)
        {
            player = null;
            health = null;
            downed = null;

            if (playerId < 0)
            {
                player = LocalPlayerContext.Player;
                if (!player)
                {
                    player = Object.FindAnyObjectByType<PlayerFacade>();
                }

                if (!player)
                {
                    Console.LogError("[Combat & Health] No local player found in scene.");
                    return false;
                }

                health = player.HealthComponent ?? player.GetComponent<HealthComponent>() ?? player.GetComponentInChildren<HealthComponent>(true) ?? player.GetComponentInParent<HealthComponent>(true);
                downed = player.DownedState ?? player.GetComponent<PlayerDownedState>() ?? player.GetComponentInChildren<PlayerDownedState>(true) ?? player.GetComponentInParent<PlayerDownedState>(true);
                return CheckAuthority(player, requireServer);
            }

            var activePlayers = PlayerIdentifier.ActivePlayers;
            if (activePlayers != null)
            {
                for (int i = 0; i < activePlayers.Count; i++)
                {
                    var identifier = activePlayers[i];
                    if (identifier && identifier.PlayerID.HasValue && identifier.PlayerID.Value.id.value == (ulong)playerId)
                    {
                        player = identifier.GetComponent<PlayerFacade>() ?? identifier.GetComponentInParent<PlayerFacade>() ?? identifier.GetComponentInChildren<PlayerFacade>(true);
                        if (!player)
                            player = identifier.transform.root.GetComponentInChildren<PlayerFacade>(true);

                        if (player)
                        {
                            health = player.HealthComponent ?? player.GetComponent<HealthComponent>() ?? player.GetComponentInChildren<HealthComponent>(true) ?? player.GetComponentInParent<HealthComponent>(true);
                            downed = player.DownedState ?? player.GetComponent<PlayerDownedState>() ?? player.GetComponentInChildren<PlayerDownedState>(true) ?? player.GetComponentInParent<PlayerDownedState>(true);
                            return CheckAuthority(player, requireServer);
                        }
                    }
                }
            }

            Console.LogError($"[Combat & Health] Could not find player with ID {playerId}.");
            return false;
        }
    }
}
