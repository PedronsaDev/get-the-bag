object Read(object target, string name)
{
    if (target == null) return null;
    var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
    var property = target.GetType().GetProperty(name, flags);
    return property != null ? property.GetValue(target) : target.GetType().GetField(name, flags)?.GetValue(target);
}

var reports = new System.Collections.Generic.List<object>();
var players = UnityEngine.Object.FindObjectsByType<UnityEngine.MonoBehaviour>(UnityEngine.FindObjectsInactive.Include)
    .Where(component => component && component.GetType().Name == "PlayerWeaponController");
foreach (var player in players)
{
    var components = player.GetComponents<UnityEngine.MonoBehaviour>();
    var presenter = components.First(component => component.GetType().Name == "PlayerWeaponPresenter");
    var rig = components.First(component => component.GetType().Name == "CharacterRigBinding");
    var pose = player.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true).First(component => component.GetType().Name == "UpperBodyPoseController");
    var state = Read(player, "ConfirmedState");
    var weapon = Read(state, "ActiveWeapon");
    var world = Read(presenter, "WorldVisual") as UnityEngine.Component;
    var fp = Read(presenter, "FirstPersonVisual") as UnityEngine.Component;
    var animator = Read(rig, "BodyAnimator") as UnityEngine.Animator;
    var worldAnimator = world ? world.GetComponentInChildren<UnityEngine.Animator>(true) : null;
    var slide = worldAnimator ? worldAnimator.transform.Find("weap_root/weap_slide") : null;
    var bodyClip = Read(pose, "_activeReloadClip") as UnityEngine.AnimationClip;
    var duration = (float)Read(state, "ActionDuration");
    var progress = duration > 0 ? UnityEngine.Mathf.Clamp01((float)(((double)Read(player,"NetworkTime") - (double)Read(state,"ActionStartedAt")) / duration)) : 0f;
    var handCurve = Read(pose,"_reloadSupportHandWeight") as UnityEngine.AnimationCurve;
    reports.Add(new {
        owner = Read(player,"owner")?.ToString(), localOwner = (bool)Read(player,"isOwner"),
        weapon = Read(weapon,"WeaponId"), loaded = Read(weapon,"MagazineAmmo"),
        action = Read(state,"Action").ToString(), variant = Read(state,"ReloadType").ToString(),
        sequence = Read(state,"ActionSequence"), duration, progress,
        worldLoaded = world != null, firstPersonLoaded = fp != null,
        bodyClip = bodyClip ? bodyClip.name : null,
        bodyReloading = (bool)Read(pose,"_playingReload"),
        bodySpeed = animator.GetFloat("BodyReloadSpeed"),
        bodyPhase = animator.GetCurrentAnimatorStateInfo(animator.GetLayerIndex("Upper_Body_Layer")).normalizedTime,
        worldSpeed = worldAnimator ? worldAnimator.GetFloat("ReloadSpeed") : 0f,
        worldPhase = worldAnimator ? worldAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime : 0f,
        handWeight = (Read(rig,"SupportHandRig") as UnityEngine.Component)?.GetType().GetProperty("weight")?.GetValue(Read(rig,"SupportHandRig")),
        expectedHandWeight = handCurve != null && handCurve.length > 0 ? handCurve.Evaluate(progress) : 0f,
        bodyCulling = animator.cullingMode.ToString(), worldCulling = worldAnimator ? worldAnimator.cullingMode.ToString() : null,
        slideY = slide ? slide.localPosition.y : 0f,
        bodyShadows = (Read(rig,"BodyRenderers") as UnityEngine.Renderer[]).Select(r=>r.shadowCastingMode.ToString()).ToArray()
    });
}
return reports;
