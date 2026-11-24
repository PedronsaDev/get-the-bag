namespace Project.Core.Raid
{
/// <summary>Where the current level is in its raid. Written by Project.Match, read by anyone.</summary>
public enum RaidPhase : byte
{
    /// <summary>The level is loading or waiting for players. Nothing should act yet.</summary>
    Loading,
    /// <summary>Everyone is in and playing.</summary>
    Live,
    /// <summary>Extraction has been activated and the holdout is running.</summary>
    Extraction,
    /// <summary>The raid is resolved (or failed to start). Stays until the next scene load.</summary>
    Ended
}
}
