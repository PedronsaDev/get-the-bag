using System;

namespace Project.Combat
{
    /// <summary>
    /// Event hook implemented by Grip components in the Gunsmith Circuit system.
    /// Dictates WHEN electricity/force flows through the weapon's firing cycle
    /// (e.g. standard click, slide discharge, empty magazine burst).
    /// </summary>
    public interface ICircuitTrigger
    {
        void OnTriggerPull(WeaponInstance weapon, WeaponContext context);
        void OnSlideAction(WeaponInstance weapon, WeaponContext context);
        void OnMagExpended(WeaponInstance weapon, WeaponContext context);
    }
}
