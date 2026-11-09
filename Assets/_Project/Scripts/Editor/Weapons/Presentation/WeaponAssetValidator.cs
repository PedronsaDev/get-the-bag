using System;
using System.Collections.Generic;
using Project.Combat.Data;
using Project.Combat.Weapons;
using Project.Definitions.Weapons;
using Project.Player.Animation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Animations;
using UnityEngine;

namespace Project.Weapons.Editor
{
public static class WeaponAssetValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(WeaponDefinition weaponDefinition)
    {
        var issues = new List<ValidationIssue>();
        if (!weaponDefinition)
        {
            issues.Add(new ValidationIssue("Weapon definition is missing.", null));
            return issues;
        }

        var presentation = weaponDefinition.PresentationDefinition;
        if (presentation)
        {
            ValidateSupportHandCurve(presentation.ReloadNormal, "Normal", presentation, issues);
            ValidateSupportHandCurve(presentation.ReloadEmpty, "Empty", presentation, issues);
        }

        var worldPrefab = ResolvePrefab(
            weaponDefinition.WorldPrefabReference,
            "world",
            weaponDefinition,
            issues);
        if (worldPrefab)
        {
            ValidatePrefab(weaponDefinition, worldPrefab, "world", issues);
        }

        var firstPersonReference = weaponDefinition.FirstPersonPrefabReference;
        if (firstPersonReference == null || string.IsNullOrEmpty(firstPersonReference.AssetGUID))
        {
            return issues;
        }

        if (weaponDefinition.WorldPrefabReference != null &&
            string.Equals(firstPersonReference.AssetGUID, weaponDefinition.WorldPrefabReference.AssetGUID,
                StringComparison.Ordinal))
            issues.Add(new ValidationIssue("Dedicated FP and world references use the same prefab. Leave the FP reference empty for an intentional world-only fallback; otherwise supply a weapon-only world asset.", weaponDefinition));

        var firstPersonPrefab = ResolvePrefab(
            firstPersonReference,
            "first-person",
            weaponDefinition,
            issues);
        if (firstPersonPrefab)
        {
            ValidatePrefab(weaponDefinition, firstPersonPrefab, "first-person", issues);
        }

        return issues;
    }

    private static GameObject ResolvePrefab(
        UnityEngine.AddressableAssets.AssetReferenceT<GameObject> reference,
        string role,
        WeaponDefinition weaponDefinition,
        ICollection<ValidationIssue> issues)
    {
        if (reference == null || !reference.RuntimeKeyIsValid())
        {
            issues.Add(new ValidationIssue(
                $"Weapon '{weaponDefinition.name}' requires a valid {role} prefab reference.",
                weaponDefinition));
            return null;
        }

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (!settings)
        {
            issues.Add(new ValidationIssue(
                $"Weapon '{weaponDefinition.name}' {role} prefab cannot be verified because Addressables settings are missing.",
                weaponDefinition));
            return null;
        }

        var entry = settings.FindAssetEntry(reference.AssetGUID, includeImplicit: true);
        if (entry == null)
        {
            issues.Add(new ValidationIssue(
                $"Weapon '{weaponDefinition.name}' {role} prefab reference is not available through Addressables.",
                weaponDefinition));
            return null;
        }

        var bundled = entry.parentGroup ? entry.parentGroup.GetSchema<UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema>() : null;
        if (bundled && (!bundled.IncludeInBuild || !bundled.IncludeGUIDInCatalog))
            issues.Add(new ValidationIssue($"Weapon '{weaponDefinition.name}' {role} group must include content in builds and GUIDs in its catalog for AssetReference loading.", entry.parentGroup));

        var path = AssetDatabase.GUIDToAssetPath(reference.AssetGUID);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!prefab || !PrefabUtility.IsPartOfPrefabAsset(prefab))
        {
            issues.Add(new ValidationIssue(
                $"Weapon '{weaponDefinition.name}' {role} prefab reference does not resolve to a prefab asset.",
                weaponDefinition));
            return null;
        }

        return prefab;
    }

    private static void ValidatePrefab(
        WeaponDefinition weaponDefinition,
        GameObject prefab,
        string role,
        List<ValidationIssue> issues)
    {
        var weaponInstance = prefab.GetComponent<Project.Combat.Weapons.WeaponInstance>();
        if (!weaponInstance)
        {
            issues.Add(new ValidationIssue(
                $"Weapon '{weaponDefinition.name}' {role} prefab requires WeaponInstance on its root.",
                prefab));
            return;
        }

        var firePoint = weaponInstance.FirePoint;
        if (!firePoint || !firePoint.IsChildOf(weaponInstance.transform))
        {
            issues.Add(new ValidationIssue(
                $"Weapon '{weaponDefinition.name}' {role} prefab requires a FirePoint belonging to its root WeaponInstance.",
                prefab));
        }

        ValidateWeaponHierarchy(weaponDefinition, prefab, role, issues);
    }
    private static void ValidateWeaponHierarchy(WeaponDefinition definition, GameObject prefab, string role, List<ValidationIssue> issues)
    {
        if (prefab.GetComponentsInChildren<WeaponInstance>(true).Length != 1)
            issues.Add(new ValidationIssue("Weapon visual must contain exactly one root WeaponInstance.", prefab));
        if (prefab.GetComponentInChildren<CharacterRigBinding>(true))
            issues.Add(new ValidationIssue("Weapon visual embeds an entire character rig binding.", prefab));
        if (role == "world")
        {
            foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                if (component is WeaponAimPose or WeaponLookSway or WeaponMovementSway or WeaponRecoil)
                    issues.Add(new ValidationIssue("World weapon contains an FP procedural component; use a weapon-only world prefab.", component));
            if (!VisualHierarchyValidator.HasShadowMesh(prefab.GetComponentsInChildren<Renderer>(true)))
                issues.Add(new ValidationIssue("World weapon has no enabled shadow-casting mesh for the owner shadow.", prefab, ValidationSeverity.Warning));
        }

        foreach (var animator in prefab.GetComponentsInChildren<Animator>(true))
            if (animator.applyRootMotion)
                issues.Add(new ValidationIssue("Attached weapon Animator must not apply root motion to its socket hierarchy.", animator));
        foreach (var body in prefab.GetComponentsInChildren<Rigidbody>(true))
            if (!body.isKinematic) issues.Add(new ValidationIssue("Attached weapon contains a dynamic Rigidbody that competes with socket attachment.", body));
        foreach (var adapter in prefab.GetComponentsInChildren<WeaponAnimator>(true))
            ValidateWeaponAnimator(adapter, prefab.transform, definition, role, issues);
        issues.AddRange(VisualHierarchyValidator.Validate(prefab));
    }

    private static void ValidateWeaponAnimator(WeaponAnimator adapter, Transform root, WeaponDefinition definition, string role, List<ValidationIssue> issues)
    {
        var serialized = new SerializedObject(adapter);
        var view = (WeaponAnimationView)serialized.FindProperty("_view").enumValueIndex;
        var expectedView = role == "world" ? WeaponAnimationView.World : WeaponAnimationView.FirstPerson;
        if (view != expectedView)
            issues.Add(new ValidationIssue($"WeaponAnimator view must match its {role} prefab role.", adapter));
        var animator = serialized.FindProperty("_animator").objectReferenceValue as Animator;
        var weapon = serialized.FindProperty("_weapon").objectReferenceValue as WeaponInstance;
        if (!weapon) weapon = adapter.GetComponent<WeaponInstance>();
        if (!weapon || weapon.transform != root || !animator || !IsInHierarchy(animator.transform, root))
        {
            issues.Add(new ValidationIssue("WeaponAnimator requires its own root WeaponInstance and an Animator in this weapon visual.", adapter));
            return;
        }
        RuntimeAnimatorController runtimeController = animator.runtimeAnimatorController;
        while (runtimeController is AnimatorOverrideController overrides) runtimeController = overrides.runtimeAnimatorController;
        if (runtimeController is not AnimatorController controller)
        {
            issues.Add(new ValidationIssue("WeaponAnimator requires an inspectable Animator Controller.", adapter));
            return;
        }
        var layer = serialized.FindProperty("_layer").intValue;
        if (layer < 0 || layer >= controller.layers.Length)
        {
            issues.Add(new ValidationIssue("WeaponAnimator layer index is outside its controller.", adapter));
            return;
        }
        var states = new List<AnimatorState>();
        GatherStates(controller.layers[layer].stateMachine, states);
        var requiredStates = role == "world" ? new[] { "Idle", "Reload" } : new[] { "Idle", "Fire", "Reload" };
        foreach (var required in requiredStates)
        {
            var state = states.Find(s => s.name == required);
            if (!state) issues.Add(new ValidationIssue($"WeaponAnimator controller is missing '{required}' on its configured layer.", adapter));
            else if (!state.motion) issues.Add(new ValidationIssue($"WeaponAnimator '{required}' state has no motion.", controller));
        }
        if (!Array.Exists(controller.parameters, p => p.name == "ReloadSpeed" && p.type == AnimatorControllerParameterType.Float))
            issues.Add(new ValidationIssue("WeaponAnimator requires a Float ReloadSpeed parameter.", adapter));
        var reload = states.Find(s => s.name == "Reload");
        if (reload && (!reload.speedParameterActive || reload.speedParameter != "ReloadSpeed" || !Mathf.Approximately(reload.speed, 1f)))
            issues.Add(new ValidationIssue("Reload state must use ReloadSpeed as its speed multiplier with base speed 1.", controller));
        var slot = serialized.FindProperty("_reloadClip").objectReferenceValue as AnimationClip;
        if (!slot)
            issues.Add(new ValidationIssue("WeaponAnimator requires the original Reload state clip as its override slot.", adapter));
        if (reload && reload.motion is AnimationClip reloadClip)
        {
            if (slot && slot != reloadClip)
                issues.Add(new ValidationIssue("WeaponAnimator reload slot must match the original Reload state clip.", adapter));
            var effectiveClip = ResolveEffectiveClip(animator.runtimeAnimatorController, reloadClip);
            ValidateReloadClip(effectiveClip, animator, issues);
        }
        else if (reload && reload.motion)
            issues.Add(new ValidationIssue("WeaponAnimator Reload slot requires a single AnimationClip, not a blended motion.", controller));
        var idle = states.Find(s => s.name == "Idle");
        var idleSlot = serialized.FindProperty("_idleClip").objectReferenceValue as AnimationClip;
        if (!idleSlot)
            issues.Add(new ValidationIssue("WeaponAnimator requires the original Idle state clip as its override slot.", adapter));
        if (idle && idle.motion is AnimationClip idleClip)
        {
            if (idleSlot && idleSlot != idleClip)
                issues.Add(new ValidationIssue("WeaponAnimator idle slot must match the original Idle state clip.", adapter));
            ValidateIdleClip(ResolveEffectiveClip(animator.runtimeAnimatorController, idleClip), animator, issues);
        }
        var presentation = definition.PresentationDefinition;
        if (presentation)
        {
            ValidateReloadClip(view == WeaponAnimationView.World ? presentation.ReloadNormal?.WeaponClip : presentation.ReloadNormal?.FirstPersonClip, animator, issues);
            ValidateReloadClip(view == WeaponAnimationView.World ? presentation.ReloadEmpty?.WeaponClip : presentation.ReloadEmpty?.FirstPersonClip, animator, issues);
            ValidateIdleClip(view == WeaponAnimationView.World ? presentation.WorldIdleEmptyClip : presentation.FirstPersonIdleEmptyClip, animator, issues);
        }
        if (!animator.enabled) issues.Add(new ValidationIssue("WeaponAnimator references a disabled Animator.", animator));
    }

    private static void ValidateIdleClip(AnimationClip clip, Animator animator, List<ValidationIssue> issues)
    {
        if (!clip) return;
        if (clip.length <= 0f || !clip.isLooping)
            issues.Add(new ValidationIssue($"Idle clip '{clip.name}' must loop with positive length.", clip));
        if (clip.isHumanMotion != animator.isHuman)
            issues.Add(new ValidationIssue($"Idle clip '{clip.name}' does not match this Animator's Humanoid/Generic rig type.", clip));
        ValidateClipBindings(clip, animator, issues);
    }

    internal static void ValidateReloadClip(AnimationClip clip, Animator animator, List<ValidationIssue> issues)
    {
        if (!clip) return;
        if (clip.length <= 0f || clip.isLooping)
            issues.Add(new ValidationIssue($"Reload clip '{clip.name}' must be non-looping with positive length.", clip));
        if (clip.isHumanMotion != animator.isHuman)
            issues.Add(new ValidationIssue($"Reload clip '{clip.name}' does not match this Animator's Humanoid/Generic rig type.", clip));
        ValidateClipBindings(clip, animator, issues);
    }

    private static void ValidateSupportHandCurve(WeaponReloadAnimation reload, string variant,
        WeaponPresentationDefinition presentation, List<ValidationIssue> issues)
    {
        if (reload?.SupportHandWeight == null) return;
        foreach (var key in reload.SupportHandWeight.keys)
        {
            if (float.IsFinite(key.time) && float.IsFinite(key.value) &&
                key.time >= 0f && key.time <= 1f && key.value >= 0f && key.value <= 1f)
                continue;
            issues.Add(new ValidationIssue($"{variant} reload support-hand curve keys require finite progress and weight between 0 and 1.", presentation));
            break;
        }
    }

    private static void ValidateClipBindings(AnimationClip clip, Animator animator, List<ValidationIssue> issues)
    {
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.type != typeof(Transform) || string.IsNullOrEmpty(binding.path)) continue;
            if (animator.transform.Find(binding.path)) continue;
            issues.Add(new ValidationIssue($"Clip '{clip.name}' targets missing transform path '{binding.path}' under this Animator.", animator));
            return;
        }
    }

    private static AnimationClip ResolveEffectiveClip(RuntimeAnimatorController controller, AnimationClip clip)
    {
        if (controller is not AnimatorOverrideController overrides) return clip;
        var inherited = ResolveEffectiveClip(overrides.runtimeAnimatorController, clip);
        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        overrides.GetOverrides(pairs);
        foreach (var pair in pairs)
            if ((pair.Key == inherited || pair.Key == clip) && pair.Value) return pair.Value;
        return inherited;
    }

    private static void GatherStates(AnimatorStateMachine machine, List<AnimatorState> states)
    {
        foreach (var state in machine.states) states.Add(state.state);
        foreach (var child in machine.stateMachines) GatherStates(child.stateMachine, states);
    }

    private static bool IsInHierarchy(Transform target, Transform root) => target && root && target.IsChildOf(root);
}
}
