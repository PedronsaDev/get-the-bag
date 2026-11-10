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
    public int RunValue;
    public int BankedTotal;
    public string[] ItemIds;
    public int[] ItemQuantities;
}

public struct RaidResults : IPackedAuto
{
    public int RunId;
    public RaidOutcome Outcome;
    public RaidPlayerResult[] Players;

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
