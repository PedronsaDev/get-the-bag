using System;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Runtime instance of an installed or inventoried weapon part.
    /// Carries its ScriptableObject definition, an explicit item level (1-100),
    /// and a lock flag preventing signature legendary parts from being detached.
    /// </summary>
    [Serializable]
    public struct WeaponPartInstance : IEquatable<WeaponPartInstance>
    {
        [SerializeField] private WeaponPartDefinition _definition;
        [SerializeField] private byte _level;
        [SerializeField] private bool _isLocked;

        public WeaponPartDefinition Definition => _definition;
        public byte Level => _level;
        public bool IsLocked => _isLocked;

        public bool IsValid => _definition != null;
        public WeaponPartType PartType => _definition ? _definition.PartType : WeaponPartType.Barrel;
        public ItemRarity Rarity => _definition ? _definition.Rarity : ItemRarity.Common;
        public string DisplayName => _definition ? _definition.DisplayName : "Unknown Part";

        public WeaponPartInstance(WeaponPartDefinition definition, byte level = 1, bool isLocked = false)
        {
            _definition = definition;
            _level = (byte)Mathf.Clamp(level, 1, 100);
            _isLocked = isLocked;
        }

        public bool IsCompatibleWith(WeaponType coreType)
        {
            return _definition != null && _definition.IsCompatibleWith(coreType);
        }

        public bool Equals(WeaponPartInstance other)
        {
            return Equals(_definition, other._definition) && _level == other._level && _isLocked == other._isLocked;
        }

        public override bool Equals(object obj)
        {
            return obj is WeaponPartInstance other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_definition, _level, _isLocked);
        }

        public static bool operator ==(WeaponPartInstance left, WeaponPartInstance right) => left.Equals(right);
        public static bool operator !=(WeaponPartInstance left, WeaponPartInstance right) => !left.Equals(right);

        public override string ToString()
        {
            return _definition != null ? $"{_definition.DisplayName} (Lvl {_level}{( _isLocked ? " [LOCKED]" : "" )})" : "Empty Slot";
        }
    }
}
