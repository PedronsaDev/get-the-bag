using UnityEngine;

namespace Project.Match
{
/// <summary>How a player's carried value turns into banked money. Pure math, no state.</summary>
public static class RaidPayout
{
    public readonly struct Breakdown
    {
        public readonly int StealthBonus;
        public readonly int CivilianPenalty;
        public readonly int Payout;

        public Breakdown(int stealthBonus, int civilianPenalty, int payout)
        {
            StealthBonus = stealthBonus;
            CivilianPenalty = civilianPenalty;
            Payout = payout;
        }
    }

    /// <summary>
    /// Stealth adds a percentage of the run value; every civilian killed costs a flat amount.
    /// The payout never goes below 0. A player who did not extract gets nothing.
    /// </summary>
    public static Breakdown Calculate(int runValue, bool extracted, bool stealth, int civilianKills,
        int stealthBonusPercent, int penaltyPerCivilian)
    {
        if (!extracted)
            return new Breakdown(0, 0, 0);

        int bonus = stealth ? Mathf.RoundToInt(runValue * stealthBonusPercent / 100f) : 0;
        int penalty = Mathf.Max(0, civilianKills) * penaltyPerCivilian;
        int payout = Mathf.Max(0, runValue + bonus - penalty);
        return new Breakdown(bonus, penalty, payout);
    }
}
}
