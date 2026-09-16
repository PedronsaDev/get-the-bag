using System.Text;
using PedronsaDev.Console;
using Project.Combat;
using Project.Core.Player;
using Project.Player.Locomotion;
using UnityEngine;

namespace Project.Player.Commands
{
    [CommandGroup("Combat & Health")]
    public static class HealthCommands
    {
        [Command("hc_health_status", "Displays comprehensive health, shield, downed state, and bleedout timers for a player (default: local player)")]
        public static void HealthStatus(int playerId = -1)
        {
            if (!TryResolveTarget(playerId, out var player, out var health, out var downed))
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
                sb.AppendLine($"IsDown: {downed.IsDown.value}");
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

            PedronsaDev.Console.Console.Log(sb.ToString());
        }

        [Command("hc_damage", "Deals damage to the player (absorbed by shields first). Usage: damage <amount> [playerId]")]
        public static void Damage(float amount = 25f, int playerId = -1)
        {
            if (!TryResolveTarget(playerId, out var player, out var health, out var downed))
                return;

            if (!health)
            {
                PedronsaDev.Console.Console.LogError("[Combat & Health] Target has no HealthComponent.");
                return;
            }

            if (health.IsDead || (downed && downed.IsDown.value))
            {
                PedronsaDev.Console.Console.LogWarning($"[Combat & Health] {player.gameObject.name} is already downed/dead (HP: {health.CurrentHealth:F1}). Use 'revive' first.");
                return;
            }

            var result = health.TakeDamage(new DamagePayload
            {
                Amount = Mathf.Max(0.1f, amount),
                Type = DamageType.Physical,
                Location = HitLocation.Core,
                HitPoint = player.transform.position + Vector3.up,
                HitNormal = Vector3.up
            });

            if (!result.Applied)
            {
                PedronsaDev.Console.Console.LogWarning($"[Combat & Health] Damage was not applied to {player.gameObject.name} (Invulnerable: {health.Invulnerable}, IsDead: {health.IsDead}).");
                return;
            }

            PedronsaDev.Console.Console.Log($"[Combat & Health] Dealt {result.EffectiveDamage:F1} damage to {player.gameObject.name} (Shield: -{result.DamageToShield:F1}, HP: -{result.DamageToHealth:F1}) | Shield: {health.CurrentShield:F1}/{health.MaxShield:F1} | HP: {health.CurrentHealth:F1}/{health.MaxHealth:F1}");
        }

        [Command("hc_damage_hp", "Deals true damage directly to health, bypassing shields. Usage: damage_hp <amount> [playerId]")]
        public static void DamageHp(float amount = 25f, int playerId = -1)
        {
            if (!TryResolveTarget(playerId, out var player, out var health, out var downed))
                return;

            if (!health)
            {
                PedronsaDev.Console.Console.LogError("[Combat & Health] Target has no HealthComponent.");
                return;
            }

            if (health.IsDead || (downed && downed.IsDown.value))
            {
                PedronsaDev.Console.Console.LogWarning($"[Combat & Health] {player.gameObject.name} is already downed/dead (HP: {health.CurrentHealth:F1}). Use 'revive' first.");
                return;
            }

            var result = health.TakeDamage(new DamagePayload
            {
                Amount = Mathf.Max(0.1f, amount),
                Type = DamageType.TrueDamage,
                Location = HitLocation.Core,
                HitPoint = player.transform.position + Vector3.up,
                HitNormal = Vector3.up
            });

            if (!result.Applied)
            {
                PedronsaDev.Console.Console.LogWarning($"[Combat & Health] Damage was not applied to {player.gameObject.name} (Invulnerable: {health.Invulnerable}, IsDead: {health.IsDead}).");
                return;
            }

            PedronsaDev.Console.Console.Log($"[Combat & Health] Dealt {result.EffectiveDamage:F1} TRUE damage to {player.gameObject.name} | Shield: {health.CurrentShield:F1}/{health.MaxShield:F1} | HP: {health.CurrentHealth:F1}/{health.MaxHealth:F1}");
        }

        [Command("hc_heal", "Heals the player by specified amount. Usage: heal <amount> [playerId]")]
        public static void Heal(float amount = 50f, int playerId = -1)
        {
            if (!TryResolveTarget(playerId, out var player, out var health, out _))
                return;

            if (!health)
            {
                PedronsaDev.Console.Console.LogError("[Combat & Health] Target has no HealthComponent.");
                return;
            }

            health.RequestHeal(amount);
            PedronsaDev.Console.Console.Log($"[Combat & Health] Requested {amount:F1} HP heal for {player.gameObject.name}. Current HP: {health.CurrentHealth:F1}/{health.MaxHealth:F1}");
        }

        [Command("hc_heal_full", "Fully heals, restores shields, and revives the player if downed or dead. Usage: heal_full [playerId]")]
        public static void HealFull(int playerId = -1)
        {
            if (!TryResolveTarget(playerId, out var player, out var health, out var downed))
                return;

            if (!health)
            {
                PedronsaDev.Console.Console.LogError("[Combat & Health] Target has no HealthComponent.");
                return;
            }

            if (downed && downed.IsDown.value)
            {
                downed.RequestDirectRevive(1f);
            }
            else if (health.IsDead)
            {
                health.Revive(health.MaxHealth);
            }

            health.RequestHeal(health.MaxHealth);
            health.RequestRechargeShield(health.MaxShield);
            PedronsaDev.Console.Console.Log($"[Combat & Health] Fully restored health ({health.MaxHealth:F1}) and shields ({health.MaxShield:F1}) for {player.gameObject.name}.");
        }

        [Command("hc_down", "Forces the target player into the downed state. Usage: down [playerId]")]
        public static void Down(int playerId = -1)
        {
            if (!TryResolveTarget(playerId, out var player, out _, out var downed))
                return;

            if (!downed)
            {
                PedronsaDev.Console.Console.LogError("[Combat & Health] Target has no PlayerDownedState.");
                return;
            }

            if (downed.IsDown.value)
            {
                PedronsaDev.Console.Console.LogWarning($"[Combat & Health] {player.gameObject.name} is already downed!");
                return;
            }

            downed.RequestDown();
            PedronsaDev.Console.Console.Log($"[Combat & Health] Successfully downed {player.gameObject.name}.");
        }

        [Command("hc_revive", "Revives a downed or dead player. Usage: revive [playerId]")]
        public static void Revive(int playerId = -1)
        {
            if (!TryResolveTarget(playerId, out var player, out var health, out var downed))
                return;

            bool isDowned = downed && downed.IsDown.value;
            bool isDead = health && health.IsDead;

            if (!isDowned && !isDead)
            {
                PedronsaDev.Console.Console.LogWarning($"[Combat & Health] {player.gameObject.name} is neither downed nor dead (HP: {(health ? health.CurrentHealth : 100f):F1}).");
                return;
            }

            if (downed)
            {
                downed.RequestDirectRevive();
            }
            else if (health)
            {
                health.Revive(health.MaxHealth * 0.5f);
            }

            PedronsaDev.Console.Console.Log($"[Combat & Health] Successfully revived {player.gameObject.name}. Remaining HP: {(health ? health.CurrentHealth : 0f):F1}");
        }

        [Command("hc_bleedout_set", "Sets the remaining bleedout timer on a downed player. Usage: bleedout_set <seconds> [playerId]")]
        public static void SetBleedout(float seconds = 5f, int playerId = -1)
        {
            if (!TryResolveTarget(playerId, out var player, out _, out var downed))
                return;

            if (!downed)
            {
                PedronsaDev.Console.Console.LogError("[Combat & Health] Target has no PlayerDownedState.");
                return;
            }

            if (!downed.IsDown.value)
            {
                PedronsaDev.Console.Console.LogWarning($"[Combat & Health] {player.gameObject.name} is not currently downed.");
                return;
            }

            downed.RequestSetBleedOutTimer(seconds);
            PedronsaDev.Console.Console.Log($"[Combat & Health] Set bleedout timer to {seconds:F1}s for {player.gameObject.name}.");
        }

        [Command("hc_god", "Toggles invulnerability on the player. Usage: god [playerId]")]
        public static void ToggleGodMode(int playerId = -1)
        {
            if (!TryResolveTarget(playerId, out var player, out var health, out _))
                return;

            if (!health)
            {
                PedronsaDev.Console.Console.LogError("[Combat & Health] Target has no HealthComponent.");
                return;
            }

            bool newState = !health.Invulnerable;
            health.RequestSetInvulnerable(newState);
            PedronsaDev.Console.Console.Log($"[Combat & Health] Invulnerability for {player.gameObject.name} set to: {newState}");
        }

        [Command("hc_kill", "Instantly executes true death on the player. Usage: kill [playerId]")]
        public static void Kill(int playerId = -1)
        {
            if (!TryResolveTarget(playerId, out var player, out _, out var downed))
                return;

            if (!downed)
            {
                PedronsaDev.Console.Console.LogError("[Combat & Health] Target has no PlayerDownedState.");
                return;
            }

            downed.RequestKill();
            PedronsaDev.Console.Console.Log($"[Combat & Health] Executed true death for {player.gameObject.name}.");
        }

        [Command("hc_drag_release", "Forces release of any active dragging on this player. Usage: drag_release [playerId]")]
        public static void DragRelease(int playerId = -1)
        {
            if (!TryResolveTarget(playerId, out var player, out _, out var downed))
                return;

            if (!downed)
            {
                PedronsaDev.Console.Console.LogError("[Combat & Health] Target has no PlayerDownedState.");
                return;
            }

            downed.RequestStopDrag();
            PedronsaDev.Console.Console.Log($"[Combat & Health] Drag released for {player.gameObject.name}.");
        }

        private static bool TryResolveTarget(int playerId, out PlayerFacade player, out HealthComponent health, out PlayerDownedState downed)
        {
            player = null;
            health = null;
            downed = null;

            if (playerId < 0)
            {
                player = LocalPlayerContext.Player;
                if (!player)
                {
                    player = UnityEngine.Object.FindAnyObjectByType<PlayerFacade>();
                }

                if (!player)
                {
                    PedronsaDev.Console.Console.LogError("[Combat & Health] No local player found in scene.");
                    return false;
                }

                health = player.Health ?? player.GetComponent<HealthComponent>() ?? player.GetComponentInChildren<HealthComponent>(true) ?? player.GetComponentInParent<HealthComponent>(true);
                downed = player.DownedState ?? player.GetComponent<PlayerDownedState>() ?? player.GetComponentInChildren<PlayerDownedState>(true) ?? player.GetComponentInParent<PlayerDownedState>(true);
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
                        player = identifier.GetComponent<PlayerFacade>() ?? identifier.GetComponentInParent<PlayerFacade>() ?? identifier.GetComponentInChildren<PlayerFacade>(true);
                        if (!player)
                            player = identifier.transform.root.GetComponentInChildren<PlayerFacade>(true);

                        if (player)
                        {
                            health = player.Health ?? player.GetComponent<HealthComponent>() ?? player.GetComponentInChildren<HealthComponent>(true) ?? player.GetComponentInParent<HealthComponent>(true);
                            downed = player.DownedState ?? player.GetComponent<PlayerDownedState>() ?? player.GetComponentInChildren<PlayerDownedState>(true) ?? player.GetComponentInParent<PlayerDownedState>(true);
                            return true;
                        }
                    }
                }
            }

            PedronsaDev.Console.Console.LogError($"[Combat & Health] Could not find player with ID {playerId}.");
            return false;
        }
    }
}

