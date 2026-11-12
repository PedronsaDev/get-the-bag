using PurrNet;
using PurrNet.Packing;

namespace Project.Match
{
public enum RaidOutcome : byte
{
    None,
    Extracted,
    NobodyExtracted,
    Wiped,
    TimedOut
}

public struct RaidPlayerResult : IPackedAuto
{
    public PlayerID Player;
    public bool Extracted;
    /// <summary>Value of the bag at the end of the raid, before bonus and penalty.</summary>
    public int RunValue;
    public int StealthBonus;
    public int CivilianPenalty;
    /// <summary>What was actually banked this run: RunValue + StealthBonus - CivilianPenalty, never below 0. 0 if not extracted.</summary>
    public int Payout;
    public int BankedTotal;
    public string[] ItemIds;
    public int[] ItemQuantities;
}

public struct RaidResults : IPackedAuto
{
    public int RunId;
    public RaidOutcome Outcome;
    public RaidPlayerResult[] Players;

    /// <summary>True when no alarm was raised during the raid (or the level has no AI).</summary>
    public bool Stealth;
    /// <summary>Project.AI AlarmReason as a byte; Project.Core.Raid.ThreatStatus.NoAlarmReason when stealth.</summary>
    public byte AlarmReason;
    public int CivilianKills;
    /// <summary>The highest assault number reached (0 = police never assaulted).</summary>
    public int AssaultsReached;

    public bool IsValid => Outcome != RaidOutcome.None;

    public bool TryGetPlayer(PlayerID player, out RaidPlayerResult result)
    {
        if (Players != null)
        {
            for (int i = 0; i < Players.Length; i++)
            {
                if (Players[i].Player == player)
                {
                    result = Players[i];
                    return true;
                }
            }
        }

        result = default;
        return false;
    }
}
}
