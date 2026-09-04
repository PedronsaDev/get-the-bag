using System;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// A single numeric modifier applied to weapon statistics.
    /// Fully serializable in the Unity Inspector.
    /// </summary>
    [Serializable]
    public struct StatModifier
    {
        public StatType TargetStat;
        public ModifierOperation Operation;
        public float Value;

        public StatModifier(StatType targetStat, ModifierOperation operation, float value)
        {
            TargetStat = targetStat;
            Operation = operation;
            Value = value;
        }

        /// <summary>
        /// Calculates the updated stat given the unmodded base value and current accumulated value.
        /// </summary>
        public readonly float Apply(float baseStat, float currentStat)
        {
            return Operation switch
            {
                ModifierOperation.FlatAdditive => currentStat + Value,
                ModifierOperation.PercentageAdditive => currentStat + (baseStat * (Value * 0.01f)),
                ModifierOperation.PercentageMultiplicative => currentStat * (1f + (Value * 0.01f)),
                _ => currentStat
            };
        }
    }
}
