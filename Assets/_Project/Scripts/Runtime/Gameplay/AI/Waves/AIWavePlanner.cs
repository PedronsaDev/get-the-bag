using System.Collections.Generic;

namespace Project.AI.Waves
{
public static class AIWavePlanner
{
    public static int Budget(AIWaveConfig config, int waveNumber, int playerCount)
    {
        int extraPlayers = playerCount > 1 ? playerCount - 1 : 0;
        int earlierWaves = waveNumber > 1 ? waveNumber - 1 : 0;
        return config.BaseBudget + config.BudgetGrowthPerWave * earlierWaves + config.BudgetPerExtraPlayer * extraPlayers;
    }

    public static int MaxAlive(AIWaveConfig config, int playerCount)
    {
        int extraPlayers = playerCount > 1 ? playerCount - 1 : 0;
        return config.MaxAlive + config.MaxAlivePerExtraPlayer * extraPlayers;
    }

    public static void Plan(IReadOnlyList<AIWaveConfig.Entry> entries, int waveNumber, int budget,
        System.Random random, List<int> picked)
    {
        picked.Clear();
        int remaining = budget;
        while (true)
        {
            int totalWeight = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (Fits(entries[i], waveNumber, remaining))
                    totalWeight += entries[i].Weight;
            }
            if (totalWeight == 0)
                return;

            int roll = random.Next(totalWeight);
            for (int i = 0; i < entries.Count; i++)
            {
                if (!Fits(entries[i], waveNumber, remaining))
                    continue;
                if (roll < entries[i].Weight)
                {
                    picked.Add(i);
                    remaining -= entries[i].Cost;
                    break;
                }
                roll -= entries[i].Weight;
            }
        }
    }

    private static bool Fits(AIWaveConfig.Entry entry, int waveNumber, int remaining)
    {
        return entry.Prefab != null && entry.FirstWave <= waveNumber && entry.Cost <= remaining;
    }
}
}
