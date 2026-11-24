using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Weapons.Editor
{
public static class VisualHierarchyValidator
{
    private static bool Belongs(Transform target, Transform root) => target && root && target.IsChildOf(root);

    public static IReadOnlyList<ValidationIssue> Validate(GameObject root)
    {
        var issues = new List<ValidationIssue>();
        if (!root)
        {
            issues.Add(new ValidationIssue("Visual hierarchy validation requires a root.", null));
            return issues;
        }
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) CheckSkin(renderer, root.transform, issues);
        CheckMissingScripts(root, issues);
        ValidateAnimationWriters(root, issues);
        return issues;
    }

    public static bool HasShadowMesh(IEnumerable<Renderer> renderers)
    {
        foreach (var r in renderers)
            if (r && r.enabled && r is MeshRenderer or SkinnedMeshRenderer && r.shadowCastingMode != ShadowCastingMode.Off)
                return true;
        return false;
    }

    private static void CheckSkin(Renderer renderer, Transform root, List<ValidationIssue> issues)
    {
        if (renderer is not SkinnedMeshRenderer skin) return;
        if (!skin.sharedMesh) issues.Add(new ValidationIssue("Skinned renderer has no mesh.", skin));
        if (skin.sharedMesh && skin.bones.Length < skin.sharedMesh.bindposes.Length)
            issues.Add(new ValidationIssue("Skinned renderer has fewer bone references than mesh bind poses.", skin));
        if (skin.bones.Length == 0) return;
        foreach (var bone in skin.bones)
            if (!Belongs(bone, root))
            {
                issues.Add(new ValidationIssue("Skinned renderer has missing or foreign skin bones.", skin));
                break;
            }
        if (!Belongs(skin.rootBone, root)) issues.Add(new ValidationIssue("Skinned renderer requires a root bone in its own visual hierarchy.", skin));
    }

    private static void CheckMissingScripts(GameObject root, List<ValidationIssue> issues)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                issues.Add(new ValidationIssue("Visual hierarchy contains missing scripts.", transform.gameObject));
    }

    public static IReadOnlyList<ValidationIssue> ValidateAnimationWriters(GameObject root)
    {
        var issues = new List<ValidationIssue>();
        if (root) ValidateAnimationWriters(root, issues);
        else issues.Add(new ValidationIssue("Animation writer validation requires a visual root.", null));
        return issues;
    }

    private static void ValidateAnimationWriters(GameObject root, List<ValidationIssue> issues)
    {
        var writers = new Dictionary<Transform, Component>();
        var reported = new HashSet<Component>();
        void Register(Transform target, string property, Component owner)
        {
            var key = target;
            if (writers.TryGetValue(key, out var existing) && existing != owner)
            {
                if (reported.Add(owner))
                    issues.Add(new ValidationIssue($"Animation components '{existing.name}' and '{owner.name}' both write '{target.name}' ({property}).", owner));
            }
            else writers[key] = owner;
        }
        void RegisterClip(AnimationClip clip, Component owner)
        {
            if (!clip) return;
            foreach (var curve in AnimationUtility.GetCurveBindings(clip))
            {
                if (curve.type != typeof(Transform)) continue;
                var target = string.IsNullOrEmpty(curve.path) ? owner.transform : owner.transform.Find(curve.path);
                if (target) Register(target, "local pose", owner);
                else issues.Add(new ValidationIssue($"Clip '{clip.name}' targets missing transform path '{curve.path}'.", owner));
            }
        }
        foreach (var animator in root.GetComponentsInChildren<Animator>(true))
        {
            if (!animator.enabled) continue;
            if (animator.isHuman && animator.avatar && animator.avatar.isValid)
                for (var i = 0; i < (int)HumanBodyBones.LastBone; i++)
                {
                    var bone = animator.GetBoneTransform((HumanBodyBones)i);
                    if (bone) Register(bone, "local pose", animator);
                }
            if (animator.runtimeAnimatorController)
                foreach (var clip in animator.runtimeAnimatorController.animationClips) RegisterClip(clip, animator);
        }
        foreach (var legacy in root.GetComponentsInChildren<UnityEngine.Animation>(true))
        {
            if (!legacy.enabled) continue;
            foreach (AnimationState state in legacy) RegisterClip(state.clip, legacy);
        }
    }
}
}
