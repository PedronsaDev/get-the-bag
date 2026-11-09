var player = UnityEngine.Object.FindObjectsByType<UnityEngine.MonoBehaviour>(UnityEngine.FindObjectsInactive.Include)
    .Single(component => component.GetType().Name == "PlayerWeaponController" && (bool)component.GetType().GetProperty("isOwner").GetValue(component));
int Loaded()
{
    var state = player.GetType().GetProperty("ConfirmedState").GetValue(player);
    var weapon = state.GetType().GetProperty("ActiveWeapon").GetValue(state);
    return (int)weapon.GetType().GetField("MagazineAmmo").GetValue(weapon);
}
var fire = player.GetType().GetMethod("RequestFire");
double deadline = UnityEngine.Time.realtimeSinceStartupAsDouble + 12;
while (Loaded() > 0 && UnityEngine.Time.realtimeSinceStartupAsDouble < deadline)
{
    fire.Invoke(player, new object[] { UnityEngine.Vector3.up });
    await System.Threading.Tasks.Task.Delay(250);
}
if (Loaded() != 0) throw new System.Exception("Client could not empty the magazine through accepted shots.");
player.GetType().GetMethod("RequestReload").Invoke(player, null);
return "Client requested Empty reload after spending all loaded rounds";
