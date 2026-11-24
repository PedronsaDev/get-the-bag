using System.Collections.Generic;
using Project.Definitions.Weapons;
using Project.Logging;
using Project.Player.Animation;
using UnityEditor;
using UnityEngine;

namespace Project.Weapons.Editor
{
public static class AnimationValidationCommands
{
    public static IReadOnlyList<ValidationIssue> ValidatePresentation(
        GameObject characterPrefab, IEnumerable<WeaponDefinition> weapons)
    {
        var issues = new List<ValidationIssue>();
        if (!characterPrefab)
        {
            issues.Add(new ValidationIssue("Select a character prefab.", null));
            return issues;
        }
        var bindings = characterPrefab.GetComponentsInChildren<CharacterRigBinding>(true);
        if (bindings.Length != 1)
        {
            issues.Add(new ValidationIssue("Character prefab requires exactly one CharacterRigBinding.", characterPrefab));
            return issues;
        }
        issues.AddRange(CharacterRigValidator.Validate(bindings[0]));
        if (weapons != null)
            foreach (var weapon in weapons)
            {
                AddUnique(issues, AnimationCompatibilityValidator.Validate(bindings[0].VisualDefinition, weapon));
                AddUnique(issues, WeaponAssetValidator.Validate(weapon));
                var presentation = weapon ? weapon.PresentationDefinition : null;
                var animator = bindings[0].BodyAnimator;
                if (presentation && animator)
                {
                    if (!presentation.ReloadNormal?.BodyClip)
                        issues.Add(new ValidationIssue("Body reload requires a Normal body clip in the weapon presentation.", presentation));
                    WeaponAssetValidator.ValidateReloadClip(presentation.ReloadNormal?.BodyClip, animator, issues);
                    WeaponAssetValidator.ValidateReloadClip(presentation.ReloadEmpty?.BodyClip, animator, issues);
                }
            }
        return issues;
    }

    private static void AddUnique(List<ValidationIssue> target,
        IReadOnlyList<ValidationIssue> source)
    {
        foreach (var issue in source)
            if (!target.Exists(existing => (!issue.Asset || existing.Asset == issue.Asset) && existing.Message == issue.Message && existing.Level == issue.Level))
                target.Add(issue);
    }

    [MenuItem("Tools/Animation/Validate Selected Character", priority = 20)]
    public static void ValidateSelectedCharacter()
    {
        if (!CanValidateSavedAssets()) return;
        var characters = Selection.GetFiltered<GameObject>(SelectionMode.Assets);
        if (characters.Length != 1)
        {
            AppLog.LogError(LogChannel.Player, "Select exactly one character prefab in the Project window.", context: Selection.activeObject);
            return;
        }
        Report(ValidatePresentation(characters[0], null), characters[0]);
    }

    [MenuItem("Tools/Animation/Validate Selected Character and Weapons", priority = 30)]
    public static void ValidateSelectedCharacterAndWeapons()
    {
        if (!CanValidateSavedAssets()) return;
        var characters = Selection.GetFiltered<GameObject>(SelectionMode.Assets);
        var weapons = Selection.GetFiltered<WeaponDefinition>(SelectionMode.Assets);
        if (characters.Length != 1 || weapons.Length == 0)
        {
            AppLog.LogError(LogChannel.Player, "Select one character prefab and its supported WeaponDefinition assets together. Do not select weapon prefabs or presentation profiles instead of weapon definitions.", context: Selection.activeObject);
            return;
        }
        Report(ValidatePresentation(characters[0], weapons), characters[0]);
    }

    [MenuItem("Tools/Animation/Validate All Weapon Assets", priority = 40)]
    public static void ValidateAllWeaponAssets()
    {
        if (!CanValidateSavedAssets()) return;
        var issues = new List<ValidationIssue>();
        var guids = AssetDatabase.FindAssets("t:WeaponDefinition", new[] { "Assets/_Project" });
        foreach (var guid in guids)
            issues.AddRange(WeaponAssetValidator.Validate(
                AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid))));
        if (guids.Length == 0)
        {
            AppLog.LogWarning(LogChannel.Combat, "No WeaponDefinition assets were found under Assets/_Project.");
            return;
        }
        Report(issues, null);
    }

    [MenuItem("Tools/Animation/Validate Selected Compatibility", priority = 10)]
    public static void ValidateSelectedCompatibility()
    {
        var characters = Selection.GetFiltered<CharacterVisualDefinition>(SelectionMode.Assets);
        var weapons = Selection.GetFiltered<WeaponDefinition>(SelectionMode.Assets);

        if (characters.Length != 1 || weapons.Length != 1)
        {
            AppLog.LogError(
                LogChannel.Combat,
                "Select exactly one CharacterVisualDefinition and one WeaponDefinition together.",
                context: Selection.activeObject);
            return;
        }

        var issues = new List<ValidationIssue>(
            AnimationCompatibilityValidator.Validate(characters[0], weapons[0]));
        issues.AddRange(WeaponAssetValidator.Validate(weapons[0]));
        for (var i = 0; i < issues.Count; i++)
        {
            var issue = issues[i];
            AnimationValidationCommands.LogIssue(issue);
        }

        AnimationValidationCommands.LogSummary(issues, characters[0]);
    }

    private static bool CanValidateSavedAssets()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;
        AppLog.LogWarning(LogChannel.Player, "Run saved character/weapon validation in Edit Mode. Runtime instances have different visibility and loaded weapon children.");
        return false;
    }

    private static void Report(IReadOnlyList<ValidationIssue> issues, Object context)
    {
        foreach (var issue in issues) LogIssue(issue);
        LogSummary(issues, context);
    }

    public static void LogIssue(ValidationIssue issue)
    {
        if (issue.Level == ValidationSeverity.Error)
            AppLog.LogError(LogChannel.Combat, issue.Message, context: issue.Asset);
        else AppLog.LogWarning(LogChannel.Combat, issue.Message, context: issue.Asset);
    }

    public static void LogSummary(IReadOnlyList<ValidationIssue> issues, Object context)
    {
        var errors = 0;
        foreach (var issue in issues)
            if (issue.Level == ValidationSeverity.Error) errors++;
        var warnings = issues.Count - errors;
        var message = $"Animation validation: {errors} error(s), {warnings} warning(s). Warnings require review; this does not certify visual grip, root trajectories or network parity.";
        if (issues.Count > 0) AppLog.LogWarning(LogChannel.Combat, message, context: context);
        else AppLog.Log(LogChannel.Combat, message, context: context);
    }
}
}
