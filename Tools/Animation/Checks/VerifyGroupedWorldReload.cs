using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Project.Combat.Data;
using Project.Combat.Weapons;
using Project.Definitions.Weapons;
using Project.Weapons.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VerifyGroupedWorldReload
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
    private static object Get(object target, string field) => target.GetType().GetField(field, Flags).GetValue(target);
    private static void Call(object target, string method) => target.GetType().GetMethod(method, Flags).Invoke(target, null);
    public static string Main()
    {
        if (EditorApplication.isPlaying) return "Skipped while Play Mode is running.";
        var checks = new List<object>();
        Action<string, bool> check = (name, passed) => checks.Add(new { name, passed });
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        bool initialDirty = scene.isDirty;
        var source = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Project/Data/Weapons/Weapon Definitions/Pistols/WEAP_Pistol_A.asset");
        var definition = Object.Instantiate(source);
        var presentation = Object.Instantiate(source.PresentationDefinition);
        Set(definition, "_presentationDefinition", presentation);
        var roots = new[]
        {
            PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/Weapons/Pistols/WEAP_PF_Pistol_A.prefab"),
            PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/Weapons/Pistols/WEAP_FP_Pistol_A.prefab")
        };
        var adapters = roots.Select(r => r.GetComponent<WeaponAnimator>()).ToArray();
        var animators = adapters.Select(a => (Animator)Get(a, "_animator")).ToArray();
        var originalControllers = animators.Select(a => a.runtimeAnimatorController).ToArray();
        var overrides = new List<AnimatorOverrideController>();
        var emptyReload = new AnimationClip { name = "Temporary_World_Empty" };
        emptyReload.SetCurve("weap_root/weap_slide", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 3f, 0.01f));
        var emptyIdle = new AnimationClip { name = "Temporary_World_Empty_Idle" };
        emptyIdle.SetCurve("weap_root/weap_slide", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Constant(0f, 1f, 0.01f));
        var settings = AnimationUtility.GetAnimationClipSettings(emptyIdle); settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(emptyIdle, settings);
        try
        {
            check("World prefab explicitly selects World view", (WeaponAnimationView)Get(adapters[0], "_view") == WeaponAnimationView.World);
            check("Existing FP prefab defaults to FirstPerson view", (WeaponAnimationView)Get(adapters[1], "_view") == WeaponAnimationView.FirstPerson);
            check("Saved weapon validator has no issues", WeaponAssetValidator.Validate(source).Count == 0);
            for (int i = 0; i < roots.Length; i++)
            {
                roots[i].GetComponent<WeaponInstance>().Initialize(definition);
                Call(adapters[i], "Awake");
                overrides.Add((AnimatorOverrideController)Get(adapters[i], "_animationOverrides"));
                animators[i].Rebind(); animators[i].Update(0f);
                Call(adapters[i], "OnEnable");
            }
            check("Each visual owns its override controller", overrides[0] != overrides[1] && overrides.All(o => o));
            var world = roots[0].GetComponent<WeaponInstance>();
            var fp = roots[1].GetComponent<WeaponInstance>();
            var worldSlot = (AnimationClip)Get(adapters[0], "_reloadClip");
            var fpSlot = (AnimationClip)Get(adapters[1], "_reloadClip");
            var idleSlot = (AnimationClip)Get(adapters[0], "_idleClip");
            var fpIdleSlot = (AnimationClip)Get(adapters[1], "_idleClip");
            var magazine = animators[0].transform.Find("weap_root/weap_mag");
            var neutralPosition = magazine.localPosition;
            var neutralRotation = magazine.localRotation;
            check("World Idle is an explicit looping neutral pose", idleSlot.isLooping && idleSlot.length > 0f && !idleSlot.isHumanMotion);
            world.SetLoadedAmmo(5); fp.SetLoadedAmmo(5);
            world.SetReloading(true, 1.6f, Time.unscaledTimeAsDouble - 0.4, 1, ReloadVariant.Normal);
            fp.SetReloading(true, 1.6f, Time.unscaledTimeAsDouble - 0.4, 1, ReloadVariant.Normal);
            animators[0].Update(0f); animators[1].Update(0f);
            check("World selects supplied 2.4 second mechanism clip", overrides[0][worldSlot] == presentation.ReloadNormal.WeaponClip && Math.Abs(worldSlot.length - 2.4f) < 0.001);
            check("FP retains its own normal clip", overrides[1][fpSlot] == presentation.ReloadNormal.FirstPersonClip);
            check("World speed matches gameplay duration", Math.Abs(animators[0].GetFloat("ReloadSpeed") - 1.5f) < 0.001);
            check("FP speed remains independently derived", Math.Abs(animators[1].GetFloat("ReloadSpeed") - presentation.ReloadNormal.FirstPersonClip.length / 1.6f) < 0.001);
            check("World seeks the confirmed quarter phase", Math.Abs(animators[0].GetCurrentAnimatorStateInfo(0).normalizedTime - 0.25f) < 0.02);
            float phase = animators[0].GetCurrentAnimatorStateInfo(0).normalizedTime;
            world.SetReloading(true, 1.6f, Time.unscaledTimeAsDouble, 1, ReloadVariant.Normal);
            check("Duplicate action does not restart playback", Math.Abs(animators[0].GetCurrentAnimatorStateInfo(0).normalizedTime - phase) < 0.0001);
            world.PlayFire();
            check("Optional absent World Fire does not interrupt reload", (bool)Get(adapters[0], "_reloadOwnsPlayback") && animators[0].GetCurrentAnimatorStateInfo(0).shortNameHash == Animator.StringToHash("Reload"));
            Set(presentation.ReloadEmpty, "_weaponClip", emptyReload);
            world.SetReloading(true, 1.6f, Time.unscaledTimeAsDouble - 0.8, 2, ReloadVariant.Empty);
            fp.SetReloading(true, 1.6f, Time.unscaledTimeAsDouble - 0.8, 2, ReloadVariant.Empty);
            check("World optional Empty variant uses its own clip and length", overrides[0][worldSlot] == emptyReload && Math.Abs(animators[0].GetFloat("ReloadSpeed") - 1.875f) < 0.001);
            check("FP Empty selection remains unchanged", overrides[1][fpSlot] == presentation.ReloadEmpty.FirstPersonClip);
            world.SetReloading(true, 1.6f, Time.unscaledTimeAsDouble, 3, ReloadVariant.Normal);
            check("Normal action restores World Normal after Empty", overrides[0][worldSlot] == presentation.ReloadNormal.WeaponClip);
            Set(presentation.ReloadEmpty, "_weaponClip", null);
            world.SetReloading(true, 1.6f, Time.unscaledTimeAsDouble, 4, ReloadVariant.Empty);
            check("Missing World Empty falls back to World Normal", overrides[0][worldSlot] == presentation.ReloadNormal.WeaponClip);
            Set(presentation.ReloadNormal, "_weaponClip", null);
            world.SetReloading(true, 1.6f, Time.unscaledTimeAsDouble, 5, ReloadVariant.Normal);
            check("Missing World presentation clip falls back to controller slot", overrides[0][worldSlot] == worldSlot);
            Set(presentation.ReloadNormal, "_weaponClip", worldSlot);
            Set(presentation, "_worldIdleEmptyClip", emptyIdle);
            world.SetLoadedAmmo(0); fp.SetLoadedAmmo(0);
            check("World Empty Idle uses the World slot", overrides[0][idleSlot] == emptyIdle);
            check("FP Empty Idle stays isolated", overrides[1][fpIdleSlot] == presentation.FirstPersonIdleEmptyClip);
            world.SetLoadedAmmo(5);
            check("Loaded World restores neutral Idle", overrides[0][idleSlot] == idleSlot);
            world.SetReloading(false);
            animators[0].Update(0.1f); animators[0].Update(0f);
            check("Cancellation returns to Idle", animators[0].GetCurrentAnimatorStateInfo(0).shortNameHash == Animator.StringToHash("Idle"));
            check("Cancellation restores magazine pose", Vector3.Distance(magazine.localPosition, neutralPosition) < 0.00001f && Quaternion.Angle(magazine.localRotation, neutralRotation) < 0.01f);
            Call(adapters[0], "OnDisable"); world.SetLoadedAmmo(0); Call(adapters[0], "OnEnable");
            check("Re-enable reconstructs Empty Idle from current ammo", overrides[0][idleSlot] == emptyIdle);

            var validator = typeof(WeaponAssetValidator).GetMethod("ValidateWeaponAnimator", BindingFlags.Static | BindingFlags.NonPublic);
            Func<List<ValidationIssue>> validate = () => { var issues = new List<ValidationIssue>(); validator.Invoke(null, new object[] { adapters[0], roots[0].transform, definition, "world", issues }); return issues; };
            Set(presentation, "_worldIdleEmptyClip", null);
            Set(adapters[0], "_view", WeaponAnimationView.FirstPerson);
            check("Validator detects a mismatched view", validate().Any(i => i.Message.Contains("view must match")));
            Set(adapters[0], "_view", WeaponAnimationView.World);
            Set(adapters[0], "_idleClip", null);
            check("Validator detects missing original Idle slot", validate().Any(i => i.Message.Contains("original Idle")));
            Set(adapters[0], "_idleClip", idleSlot);
            Set(presentation.ReloadNormal, "_weaponClip", presentation.ReloadNormal.FirstPersonClip);
            check("Validator detects FP paths in World override data", validate().Any(i => i.Message.Contains("missing transform path")));
            Set(presentation.ReloadNormal, "_weaponClip", worldSlot);
            check("Saved shared data still validates", WeaponAssetValidator.Validate(source).Count == 0);
            check("Original scene dirty state preserved", scene.isDirty == initialDirty);
            var result = Newtonsoft.Json.JsonConvert.SerializeObject(new { checks, sceneDirty = scene.isDirty });
            System.IO.File.WriteAllText(".codex/animation-checks/world-regression.json", result);
            return result;
        }
        finally
        {
            for (int i = 0; i < roots.Length; i++)
            {
                Call(adapters[i], "OnDisable");
                animators[i].runtimeAnimatorController = originalControllers[i];
                Set(adapters[i], "_animationOverrides", null);
                PrefabUtility.UnloadPrefabContents(roots[i]);
            }
            foreach (var owned in overrides) Object.DestroyImmediate(owned);
            Object.DestroyImmediate(emptyIdle); Object.DestroyImmediate(emptyReload);
            Object.DestroyImmediate(presentation); Object.DestroyImmediate(definition);
        }
    }
}
