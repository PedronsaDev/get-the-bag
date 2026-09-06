using System;
using UnityEngine;

namespace Project.Combat
{
    /// <summary>
    /// Base ScriptableObject defining modular weapon-level perks and circuit lifecycle hooks.
    /// Can be attached to Grips, Receivers, Accessories, or Stocks to dictate weapon procs,
    /// ammo refunds, recoil-to-shield conversion, and on-hit procs.
    /// </summary>
    public abstract class WeaponPerkDefinition : ScriptableObject
    {
        [Header("Perk Identity")]
        [SerializeField] private string _perkId = "perk_custom";
        [SerializeField] private string _displayName = "Custom Perk";
        [TextArea(2, 4)]
        [SerializeField] private string _description = "Custom weapon lifecycle hook.";

        public string PerkId => _perkId;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public string Description => _description;

        /// <summary>
        /// Called immediately before ammunition is consumed. Return true to refund/prevent ammo consumption (e.g. The Butcher).
        /// </summary>
        public virtual bool OnPreFire(WeaponInstance weapon, WeaponContext context) => false;

        /// <summary>
        /// Called after a round is successfully discharged.
        /// </summary>
        public virtual void OnPostFire(WeaponInstance weapon, WeaponContext context) { }

        /// <summary>
        /// Called when a bullet hits an IDamageable target.
        /// </summary>
        public virtual void OnHitConfirmed(WeaponInstance weapon, DamageResult result) { }

        /// <summary>
        /// Called during recoil kick impulse (e.g. converting kick into shields or movement speed).
        /// </summary>
        public virtual void OnRecoilImpulse(WeaponInstance weapon, float kickAmount, WeaponContext context) { }

        /// <summary>
        /// Called when the weapon starts or completes a reload cycle.
        /// </summary>
        public virtual void OnReload(WeaponInstance weapon) { }
    }
}
