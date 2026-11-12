namespace Project.UI.Match.Reveal
{
/// <summary>Extra filter on a reveal step, on top of its outcome flags.</summary>
public enum RevealCondition : byte
{
    Always,
    /// <summary>Only when the raid ended without an alarm.</summary>
    StealthOnly,
    /// <summary>Only when the alarm was raised.</summary>
    LoudOnly,
    /// <summary>Only when at least one civilian was killed.</summary>
    CiviliansKilled
}
}
