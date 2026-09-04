using System;

namespace Project.Combat
{
    /// <summary>
    /// Event hook implemented by Stock components in the Gunsmith Circuit system.
    /// Dictates what happens to the weapon's rebound energy / recoil kick
    /// (e.g. converting recoil into shields, propulsion impulse, or momentum).
    /// </summary>
    public interface ICircuitFeedback
    {
        void OnRecoilImpulse(WeaponInstance weapon, float kickAmount, WeaponContext context);
    }
}
