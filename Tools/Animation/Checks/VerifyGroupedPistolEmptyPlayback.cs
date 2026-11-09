using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Project.Combat.Data;
using Project.Combat.Weapons;
using Project.Definitions.Weapons;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VerifyGroupedPistolEmptyPlayback
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(object obj, string name) => obj.GetType().GetField(name, Flags).GetValue(obj);
    static void Call(object obj, string name) => obj.GetType().GetMethod(name, Flags).Invoke(obj, null);
    public static string Main()
    {
        var checks = new List<object>();
        Action<string, bool> check = (name, passed) => { checks.Add(new { name, passed }); if (!passed) throw new Exception(name); };
        var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Project/Data/Weapons/Weapon Definitions/Pistols/WEAP_Pistol_A.asset");
        var presentation = definition.PresentationDefinition;
        var root = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/Weapons/Pistols/WEAP_PF_Pistol_A.prefab");
        var adapter = root.GetComponent<WeaponAnimator>();
        var animator = (Animator)Get(adapter, "_animator");
        var controller = animator.runtimeAnimatorController;
        AnimatorOverrideController overrides = null;
        try
        {
            var weapon = root.GetComponent<WeaponInstance>();
            weapon.Initialize(definition);
            Call(adapter, "Awake");
            overrides = (AnimatorOverrideController)Get(adapter, "_animationOverrides");
            animator.Rebind(); animator.Update(0);
            Call(adapter, "OnEnable");
            var idleSlot = (AnimationClip)Get(adapter, "_idleClip");
            var reloadSlot = (AnimationClip)Get(adapter, "_reloadClip");
            var slide = animator.transform.Find("weap_root/weap_slide");
            weapon.SetLoadedAmmo(1);
            animator.Play("Idle", 0, 0); animator.Update(0);
            var closed = slide.localPosition;
            weapon.SetLoadedAmmo(0);
            animator.Play("Idle", 0, 0); animator.Update(0);
            var open = slide.localPosition;
            check("Zero ammunition selects imported World Empty idle", overrides[idleSlot] == presentation.WorldIdleEmptyClip);
            check("Empty idle holds slide open", Vector3.Distance(open, closed) > .0001f);
            presentation.WorldIdleEmptyClip.SampleAnimation(animator.gameObject, presentation.WorldIdleEmptyClip.length);
            check("Empty idle loops without a slide jump", Vector3.Distance(slide.localPosition, open) < .00001f);
            weapon.SetReloading(true, 1.6f, Time.unscaledTimeAsDouble, 1, ReloadVariant.Empty);
            animator.Update(0);
            check("Empty reload selects imported World mechanism clip", overrides[reloadSlot] == presentation.ReloadEmpty.WeaponClip);
            check("2.4 second Empty reload scales to 1.6 second gameplay", Math.Abs(animator.GetFloat("ReloadSpeed") - 1.5f) < .001f);
            presentation.ReloadEmpty.WeaponClip.SampleAnimation(animator.gameObject, 0);
            check("Reload starts at the Empty idle slide pose", Vector3.Distance(slide.localPosition, open) < .00001f);
            presentation.ReloadEmpty.WeaponClip.SampleAnimation(animator.gameObject, presentation.ReloadEmpty.WeaponClip.length);
            check("Reload ends with slide closed", Vector3.Distance(slide.localPosition, closed) < .00001f);
            weapon.SetReloading(false);
            animator.Update(.1f); animator.Update(0);
            check("Cancelled zero-ammo reload returns to open-slide idle", Vector3.Distance(slide.localPosition, open) < .00001f);
            weapon.SetLoadedAmmo(5);
            animator.Play("Idle", 0, 0); animator.Update(0);
            check("Loaded weapon returns to closed-slide idle", Vector3.Distance(slide.localPosition, closed) < .00001f);
            var result = JsonConvert.SerializeObject(new { checks });
            System.IO.File.WriteAllText(".codex/animation-checks/actual-empty-playback.json", result);
            return result;
        }
        finally
        {
            Call(adapter, "OnDisable");
            animator.runtimeAnimatorController = controller;
            adapter.GetType().GetField("_animationOverrides", Flags).SetValue(adapter, null);
            PrefabUtility.UnloadPrefabContents(root);
            if (overrides) Object.DestroyImmediate(overrides);
        }
    }
}
