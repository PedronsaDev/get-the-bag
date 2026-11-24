using UnityEngine;

namespace Project.Weapons.Editor
{
public enum ValidationSeverity { Error, Warning }

public sealed class ValidationIssue
{
    public ValidationIssue(string message, Object asset, ValidationSeverity level = ValidationSeverity.Error)
    {
        Message = message;
        Asset = asset;
        Level = level;
    }

    public string Message { get; }
    public Object Asset { get; }
    public ValidationSeverity Level { get; }
}
}
