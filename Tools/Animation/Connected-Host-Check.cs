using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Project.Combat.Data;
using Project.Combat.Weapons;
using Project.Player.Animation;
using Project.Player.Combat;
using Project.Player.Locomotion;
using UnityEngine;

public static class ConnectedHostCheck
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Read(object target, string field) => target.GetType().GetField(field, Fields).GetValue(target);
    static async UniTask Wait(Func<bool> condition, string message)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 12;
        while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) await UniTask.Delay(20);
        if (!condition()) throw new Exception(message);
    }
    static void Scenario(string name, PlayerWeaponController player) => File.WriteAllText(".codex/v1/current-scenario.json",
        JsonConvert.SerializeObject(new { name, owner = player.owner.ToString(), player.ConfirmedState.ActionSequence }));

    public static async Task<string> Main()
    {
        if (!Application.isPlaying || !PurrNet.NetworkManager.main.isServer)
            throw new Exception("Run on the release host with its separate client connected.");
        var players = UnityEngine.Object.FindObjectsByType<PlayerWeaponController>();
        if (players.Length != 2) throw new Exception("Two connected players are required.");
        var player = players.Single(p => p.isOwner);
        var presenter = player.GetComponent<PlayerWeaponPresenter>();
        var rig = player.GetComponent<CharacterRigBinding>();
        var weapon = (RuntimeWeapon)Read(player, "_mainWeapon");
        var originalStats = weapon.ResolvedStats;
        var statsField = typeof(RuntimeWeapon).GetField("_resolvedStats", Fields);
        statsField.SetValue(weapon, new WeaponResolvedStats(originalStats.Damage, originalStats.MagazineSize, 6, 6, originalStats.FireRate, originalStats.Range));
        var checks = new List<string>();
        int accepted = 0;
        Action<WeaponShot> shot = value => accepted++;
        player.OnShotAccepted += shot;
        try
        {
            int ammo = player.ConfirmedState.ActiveWeapon.MagazineAmmo;
            player.RequestFire(Vector3.up);
            await Wait(() => player.ConfirmedState.ActiveWeapon.MagazineAmmo == ammo - 1, "Shot was not accepted exactly once.");
            if (accepted != 1) throw new Exception("Duplicate or missing accepted shot event.");
            checks.Add("Host accepted one shot and spent one round");
            player.RequestReload();
            await Wait(() => player.ConfirmedState.Action == WeaponActionState.Reloading, "Normal reload did not start.");
            if (player.ConfirmedState.ReloadType != ReloadVariant.Normal) throw new Exception("Wrong Normal variant.");
            Scenario("host-normal-reload", player);
            await UniTask.Delay(200);
            if (!presenter.WorldVisual.IsReloading || !presenter.FirstPersonVisual.IsReloading || rig.UpperBodyRig.weight != 0)
                throw new Exception("Views or aim rig did not enter reload.");
            await Wait(() => player.ConfirmedState.Action == WeaponActionState.Idle, "Normal reload did not complete.");
            if (player.ConfirmedState.ActiveWeapon.MagazineAmmo != originalStats.MagazineSize + 1)
                throw new Exception("Normal reload did not preserve the chambered round.");
            checks.Add("Normal reload completes through confirmed timing with chamber +1");
            while (player.ConfirmedState.ActiveWeapon.MagazineAmmo > 0)
            {
                player.RequestFire(Vector3.up);
                await UniTask.Delay(TimeSpan.FromSeconds(originalStats.FireRateSeconds + .05));
            }
            player.RequestReload();
            await Wait(() => player.ConfirmedState.Action == WeaponActionState.Reloading, "Empty reload did not start.");
            if (player.ConfirmedState.ReloadType != ReloadVariant.Empty) throw new Exception("Wrong Empty variant.");
            Scenario("host-empty-reload", player);
            await Wait(() => player.ConfirmedState.Action == WeaponActionState.Idle, "Empty reload did not complete.");
            if (player.ConfirmedState.ActiveWeapon.MagazineAmmo != originalStats.MagazineSize)
                throw new Exception("Empty reload loaded an extra chambered round.");
            checks.Add("Empty reload completes with magazine capacity and correct variant");
            player.RequestFire(Vector3.up);
            await UniTask.Delay(250);
            player.RequestReload();
            await Wait(() => player.ConfirmedState.Action == WeaponActionState.Reloading, "Cancellation reload did not start.");
            player.RequestEquip(WeaponSlot.Secondary);
            await Wait(() => player.ConfirmedState.ActiveSlot == WeaponSlot.Secondary && player.ConfirmedState.Action == WeaponActionState.Idle, "Switch did not cancel reload.");
            player.RequestEquip(WeaponSlot.Main);
            await Wait(() => player.ConfirmedState.ActiveSlot == WeaponSlot.Main && presenter.FirstPersonVisual && presenter.WorldVisual, "Pistol did not return.");
            checks.Add("Switching cancels reload and restores both pistol views");
            var downed = player.GetComponent<PlayerDownedState>();
            player.RequestReload();
            await Wait(() => player.ConfirmedState.Action == WeaponActionState.Reloading, "Downed cancellation reload did not start.");
            downed.DownServer();
            await Wait(() => downed.IsDowned && player.ConfirmedState.Action == WeaponActionState.Idle, "Downing did not cancel reload.");
            if (!downed.ReviveServer()) throw new Exception("Test player did not revive.");
            await Wait(() => !downed.IsDowned && rig.UpperBodyRig.weight == 1, "Revive did not restore aim rig.");
            checks.Add("Downing cancels reload; revival restores aim correction");
            var fp = presenter.FirstPersonVisual; var world = presenter.WorldVisual;
            presenter.enabled = false; presenter.enabled = true;
            await UniTask.Delay(150);
            if (presenter.FirstPersonVisual != fp || presenter.WorldVisual != world)
                throw new Exception("Re-enabling the presenter duplicated or lost loaded views.");
            checks.Add("Presenter disable/re-enable reuses the loaded views");
            Scenario("host-check-complete", player);
            var report = JsonConvert.SerializeObject(new { checks, acceptedShots = accepted, owner = player.owner.ToString(), temporaryReloadSeconds = 6 });
            File.WriteAllText(".codex/v1/connected-host.json", report);
            return report;
        }
        finally
        {
            player.OnShotAccepted -= shot;
            statsField.SetValue(weapon, originalStats);
        }
    }
}
