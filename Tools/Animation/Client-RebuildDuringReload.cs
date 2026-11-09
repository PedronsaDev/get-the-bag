var player = UnityEngine.Object.FindObjectsByType<UnityEngine.MonoBehaviour>(UnityEngine.FindObjectsInactive.Include)
    .Single(component => component.GetType().Name == "PlayerWeaponController" && (bool)component.GetType().GetProperty("isOwner").GetValue(component));
player.GetType().GetMethod("RequestFire").Invoke(player, new object[] { UnityEngine.Vector3.up });
player.GetType().GetMethod("RequestReload").Invoke(player, null);
await System.Threading.Tasks.Task.Delay(1500);
var state = player.GetType().GetProperty("ConfirmedState").GetValue(player);
if (state.GetType().GetField("Action").GetValue(state).ToString() != "Reloading")
    throw new System.Exception("The test needs an active long reload.");
var presenter = player.GetComponents<UnityEngine.MonoBehaviour>().Single(component => component.GetType().Name == "PlayerWeaponPresenter");
presenter.GetType().GetMethod("ClearVisuals").Invoke(presenter, null);
presenter.GetType().GetMethod("PresentAsync").Invoke(presenter, new object[] { state, System.Threading.CancellationToken.None });
await System.Threading.Tasks.Task.Delay(300);
if (presenter.GetType().GetProperty("WorldVisual").GetValue(presenter) == null || presenter.GetType().GetProperty("FirstPersonVisual").GetValue(presenter) == null)
    throw new System.Exception("Late-loaded views failed to return during the action.");
return "Both client views recreated during the confirmed reload";
