using System.Collections.Generic;
using Project.Player.Animation;
using Project.Player.Combat;
using Project.Combat.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Project.Weapons.Editor
{
public static class CharacterRigValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(CharacterRigBinding binding)
    {
        var issues = new List<ValidationIssue>();
        if (!binding)
        {
            issues.Add(new ValidationIssue("Character requires a CharacterRigBinding.", null));
            return issues;
        }

        if (!binding.VisualDefinition)
            issues.Add(new ValidationIssue("Character definition is missing.", binding));
        else AnimationCompatibilityValidator.ValidateCharacterFields(binding.VisualDefinition, issues);
        var root = binding.transform;
        var animator = binding.BodyAnimator;
        var human = animator && animator.avatar && animator.avatar.isValid && animator.isHuman;
        if (!animator || !Belongs(animator.transform, root))
            issues.Add(new ValidationIssue("Body Animator must belong to this character.", binding));
        if (!human)
            issues.Add(new ValidationIssue("Body Animator requires a valid Humanoid Avatar.", animator ? animator : binding));
        else
        {
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (!hand || !animator.GetBoneTransform(HumanBodyBones.LeftHand))
                issues.Add(new ValidationIssue("Body Humanoid must resolve both hand bones.", animator));
            if (!hand || !Belongs(binding.WorldWeaponSocket, hand))
                issues.Add(new ValidationIssue("World weapon socket must belong to the dominant right-hand hierarchy.", binding));
        }

        CheckReference(binding.WorldWeaponSocket, root, "World weapon socket", binding, issues);
        CheckReference(binding.FirstPersonSocket, root, "FP socket", binding, issues);
        CheckReference(binding.AimOrigin, root, "Aim origin", binding, issues);
        CheckReference(binding.BodyAimTarget, root, "Body aim target", binding, issues);
        if (binding.WorldWeaponSocket && binding.FirstPersonSocket &&
            (Belongs(binding.FirstPersonSocket, binding.WorldWeaponSocket) || Belongs(binding.WorldWeaponSocket, binding.FirstPersonSocket)))
            issues.Add(new ValidationIssue("FP and world sockets must be independent hierarchies.", binding));
        if (animator && binding.FirstPersonSocket && Belongs(binding.FirstPersonSocket, animator.transform))
            issues.Add(new ValidationIssue("FP socket must be outside the animated world body hierarchy.", binding));

        if (!binding.RigBuilder || !Belongs(binding.RigBuilder.transform, root) ||
            binding.RigBuilder.GetComponent<Animator>() != animator)
            issues.Add(new ValidationIssue("RigBuilder must belong to the body Animator GameObject.", binding));
        if (binding.RigBuilder && !binding.RigBuilder.enabled)
            issues.Add(new ValidationIssue("Body RigBuilder is disabled.", binding.RigBuilder));
        if (!binding.UpperBodyRig || !binding.RigBuilder || !Belongs(binding.UpperBodyRig.transform, binding.RigBuilder.transform))
            issues.Add(new ValidationIssue("Upper-body Rig must belong to this body's RigBuilder hierarchy.", binding));
        else
        {
            var registered = false;
            var layers = new HashSet<Rig>();
            foreach (var layer in binding.RigBuilder.layers)
            {
                if (layer == null || !layer.rig)
                {
                    issues.Add(new ValidationIssue("RigBuilder contains a missing rig layer.", binding.RigBuilder));
                    continue;
                }
                if (!layers.Add(layer.rig))
                    issues.Add(new ValidationIssue("RigBuilder registers the same Rig more than once.", binding.RigBuilder));
                if (layer.rig == binding.UpperBodyRig)
                {
                    registered = true;
                    if (!layer.active)
                        issues.Add(new ValidationIssue("Upper-body rig layer is inactive; verify this is intentional.", layer.rig, ValidationSeverity.Warning));
                }
            }
            if (!registered)
                issues.Add(new ValidationIssue("Upper-body Rig is not registered in RigBuilder.layers.", binding.RigBuilder));
            var usesAimTarget = false;
            foreach (var aim in binding.UpperBodyRig.GetComponentsInChildren<MultiAimConstraint>(true))
            {
                var bone = aim.data.constrainedObject;
                if (!bone || !animator || !Belongs(bone, animator.transform))
                    issues.Add(new ValidationIssue("Aim constraint requires a constrained bone in this body.", aim));
                if (aim.data.sourceObjects.Count == 0)
                    issues.Add(new ValidationIssue("Aim constraint has no source targets.", aim));
                foreach (var source in aim.data.sourceObjects)
                {
                    if (source.transform && source.transform == binding.BodyAimTarget) usesAimTarget = true;
                    if (!Belongs(source.transform, root))
                        issues.Add(new ValidationIssue("Aim constraint source target is missing or belongs to another character.", aim));
                    else if (bone && Belongs(source.transform, bone))
                        issues.Add(new ValidationIssue("Aim target is below its constrained bone, creating a transform feedback loop.", aim));
                }
            }
            if (!usesAimTarget)
                issues.Add(new ValidationIssue("Upper-body aim constraints do not reference the declared BodyAimTarget.", binding.UpperBodyRig));
        }

        var renderers = new HashSet<Renderer>();
        if (binding.BodyRenderers == null || binding.BodyRenderers.Length == 0)
            issues.Add(new ValidationIssue("Body renderer group is missing or empty.", binding));
        else foreach (var renderer in binding.BodyRenderers)
        {
            if (!renderer || !Belongs(renderer.transform, root))
                issues.Add(new ValidationIssue("Body renderer group contains a missing or foreign renderer.", binding));
            else if (!renderers.Add(renderer))
                issues.Add(new ValidationIssue("Body renderer group contains a duplicate renderer.", renderer));
        }
        foreach (var renderer in binding.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is MeshRenderer or SkinnedMeshRenderer && !renderers.Contains(renderer))
                issues.Add(new ValidationIssue("Character mesh is outside the visibility renderer group and can leak into the owner camera.", renderer));

        }
        if (renderers.Count > 0 && !VisualHierarchyValidator.HasShadowMesh(renderers))
            issues.Add(new ValidationIssue("Character has no enabled mesh renderer that can supply owner shadows.", binding, ValidationSeverity.Warning));

        var visibility = binding.GetComponent<CharacterVisualVisibility>();
        if (!visibility || !visibility.enabled)
            issues.Add(new ValidationIssue("CharacterRigBinding requires an enabled CharacterVisualVisibility on the same GameObject.", binding));
        var presenters = binding.GetComponentsInChildren<PlayerWeaponPresenter>(true);
        if (presenters.Length != 1)
            issues.Add(new ValidationIssue("Character requires exactly one PlayerWeaponPresenter.", binding));
        foreach (var presenter in presenters)
        {
            var serialized = new SerializedObject(presenter);
            if (serialized.FindProperty("_characterRigBinding").objectReferenceValue != binding ||
                serialized.FindProperty("_visibility").objectReferenceValue != visibility)
                issues.Add(new ValidationIssue("Weapon presenter must reference this rig binding and visibility component.", presenter));
        }
        var bodyDrivers = binding.GetComponentsInChildren<BodyAnimationDriver>(true);
        if (bodyDrivers.Length > 1)
            issues.Add(new ValidationIssue("Character has multiple body animation drivers.", binding));
        foreach (var driver in bodyDrivers)
        {
            if (driver.BodyAnimator != animator || !Belongs(driver.FacingReference, root))
                issues.Add(new ValidationIssue("BodyAnimationDriver must reference this body Animator and a local visual facing transform.", driver));
        }
        if (binding.GetComponentsInChildren<CharacterRigBinding>(true).Length != 1)
            issues.Add(new ValidationIssue("Character contains competing CharacterRigBindings.", binding));
        if (binding.GetComponentsInChildren<WeaponInstance>(true).Length != 0)
            issues.Add(new ValidationIssue("Saved character contains embedded weapons; the presenter owns runtime weapon instances.", binding));
        if (LayerMask.NameToLayer("LocalViewmodel") < 0)
            issues.Add(new ValidationIssue("Project requires the LocalViewmodel layer for owner FP visibility.", binding));

        if (animator)
        {
            if (!animator.enabled || !animator.runtimeAnimatorController)
                issues.Add(new ValidationIssue("Body Animator must be enabled and have a controller.", animator));
            if (animator.applyRootMotion)
                issues.Add(new ValidationIssue("Body Apply Root Motion is enabled. Validate root trajectories on a duplicate before the motor-owned root-motion migration.", animator, ValidationSeverity.Warning));
            if (animator.cullingMode != AnimatorCullingMode.AlwaysAnimate && (!visibility || !visibility.enabled))
                issues.Add(new ValidationIssue("Culled bodies require active CharacterVisualVisibility to keep the owner's sockets and shadows animated.", animator, ValidationSeverity.Warning));
        }
        issues.AddRange(VisualHierarchyValidator.Validate(binding.gameObject));
        return issues;
    }

    private static bool Belongs(Transform target, Transform root) => target && root && target.IsChildOf(root);

    private static void CheckReference(Transform target, Transform root, string label, UnityEngine.Object context, List<ValidationIssue> issues)
    {
        if (!Belongs(target, root)) issues.Add(new ValidationIssue($"{label} is missing or belongs to another character.", context));
    }

}
}
