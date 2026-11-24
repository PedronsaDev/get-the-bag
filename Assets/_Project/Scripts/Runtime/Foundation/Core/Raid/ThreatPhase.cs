namespace Project.Core.Raid
{
/// <summary>Where the level's threat (alarm and police) is. Written by Project.AI, read by the HUD and results.</summary>
public enum ThreatPhase : byte
{
    Stealth,
    Alarmed,
    Assault,
    Break,
    FinalPush,
    Ended
}
}
