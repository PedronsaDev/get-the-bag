using System;
using System.Collections.Generic;
using Project.Definitions.Weapons;

namespace Project.Weapons.Editor
{
public static class AnimationCompatibilityValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(
        CharacterVisualDefinition characterDefinition,
        WeaponDefinition weaponDefinition)
    {
        var issues = new List<ValidationIssue>();
        var characterFieldsValid = ValidateCharacterFields(characterDefinition, issues);
        var weaponFieldsValid = ValidateWeaponFields(weaponDefinition, issues);

        if (characterFieldsValid && weaponFieldsValid)
        {
            var presentationDefinition = weaponDefinition.PresentationDefinition;

            if (!string.Equals(
                    characterDefinition.FirstPersonArmsFamily,
                    presentationDefinition.RequiredFirstPersonArmsFamily,
                    StringComparison.Ordinal))
            {
                issues.Add(new ValidationIssue(
                    $"Character arms family '{characterDefinition.FirstPersonArmsFamily}' differs from weapon requirement '{presentationDefinition.RequiredFirstPersonArmsFamily}'.",
                    characterDefinition));
            }

            var supportedHoldingFamilies = characterDefinition.SupportedHoldingFamilies;
            var holdingFamilySupported = false;
            for (var i = 0; i < supportedHoldingFamilies.Length; i++)
            {
                if (!string.Equals(
                        supportedHoldingFamilies[i],
                        presentationDefinition.BodyHoldingFamily,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                holdingFamilySupported = true;
                break;
            }

            if (!holdingFamilySupported)
            {
                issues.Add(new ValidationIssue(
                    $"Weapon holding family '{presentationDefinition.BodyHoldingFamily}' is absent from character '{characterDefinition.name}' supported holding families.",
                    characterDefinition));
            }
        }

        return issues;
    }

    public static bool ValidateCharacterFields(
        CharacterVisualDefinition characterDefinition,
        ICollection<ValidationIssue> issues)
    {
        if (!characterDefinition)
        {
            issues.Add(new ValidationIssue("Character definition is missing.", null));
            return false;
        }

        var isValid = true;
        if (string.IsNullOrWhiteSpace(characterDefinition.BodyRigFamily))
        {
            issues.Add(new ValidationIssue(
                $"Character '{characterDefinition.name}' body rig family is blank.",
                characterDefinition));
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(characterDefinition.FirstPersonArmsFamily))
        {
            issues.Add(new ValidationIssue(
                $"Character '{characterDefinition.name}' first-person arms family is blank.",
                characterDefinition));
            isValid = false;
        }

        var supportedHoldingFamilies = characterDefinition.SupportedHoldingFamilies;
        if (supportedHoldingFamilies == null || supportedHoldingFamilies.Length == 0)
        {
            issues.Add(new ValidationIssue(
                $"Character '{characterDefinition.name}' supported holding families are missing or empty.",
                characterDefinition));
            isValid = false;
        }
        else
        {
            var uniqueFamilies = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < supportedHoldingFamilies.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(supportedHoldingFamilies[i]))
                {
                    if (!uniqueFamilies.Add(supportedHoldingFamilies[i]))
                        issues.Add(new ValidationIssue($"Character '{characterDefinition.name}' repeats holding family '{supportedHoldingFamilies[i]}'.", characterDefinition));
                    continue;
                }

                issues.Add(new ValidationIssue(
                    $"Character '{characterDefinition.name}' supported holding family at index {i} is blank.",
                    characterDefinition));
                isValid = false;
            }
        }

        return isValid;
    }

    private static bool ValidateWeaponFields(
        WeaponDefinition weaponDefinition,
        ICollection<ValidationIssue> issues)
    {
        if (!weaponDefinition)
        {
            issues.Add(new ValidationIssue("Weapon definition is missing.", null));
            return false;
        }

        var presentationDefinition = weaponDefinition.PresentationDefinition;
        if (!presentationDefinition)
        {
            issues.Add(new ValidationIssue(
                $"Weapon '{weaponDefinition.name}' presentation definition is missing.",
                weaponDefinition));
            return false;
        }

        var isValid = true;
        if (string.IsNullOrWhiteSpace(presentationDefinition.RequiredFirstPersonArmsFamily))
        {
            issues.Add(new ValidationIssue(
                $"Weapon '{weaponDefinition.name}' required arms family is blank.",
                presentationDefinition));
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(presentationDefinition.BodyHoldingFamily))
        {
            issues.Add(new ValidationIssue(
                $"Weapon '{weaponDefinition.name}' holding family is blank.",
                presentationDefinition));
            isValid = false;
        }

        return isValid;
    }

}
}
