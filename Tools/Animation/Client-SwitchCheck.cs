var player = UnityEngine.Object.FindObjectsByType<UnityEngine.MonoBehaviour>(UnityEngine.FindObjectsInactive.Include)
    .Single(component => component.GetType().Name == "PlayerWeaponController" && (bool)component.GetType().GetProperty("isOwner").GetValue(component));
var equip = player.GetType().GetMethod("RequestEquip");
var slotType = equip.GetParameters()[0].ParameterType;
var presenter = player.GetComponents<UnityEngine.MonoBehaviour>().Single(component => component.GetType().Name == "PlayerWeaponPresenter");
for (int round = 0; round < 3; round++)
{
    foreach (int slot in new[] { 2, 1 })
    {
        equip.Invoke(player, new[] { System.Enum.ToObject(slotType, slot) });
        await System.Threading.Tasks.Task.Delay(400);
        var state = player.GetType().GetProperty("ConfirmedState").GetValue(player);
        if (System.Convert.ToInt32(state.GetType().GetField("ActiveSlot").GetValue(state)) != slot)
            throw new System.Exception("Client switch was not accepted.");
        if (presenter.GetType().GetProperty("WorldVisual").GetValue(presenter) == null || presenter.GetType().GetProperty("FirstPersonVisual").GetValue(presenter) == null)
            throw new System.Exception("A client view failed to load after switching.");
        int instances = player.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true).Count(component => component.GetType().Name == "WeaponInstance");
        if (instances != 2) throw new System.Exception("Switch left a missing or duplicate visual: " + instances);
    }
}
return "Six client-owned switches passed; exactly two owner views remain";
