namespace Project.Combat
{
    /// <summary>
    /// Identifies the targeted body part or hitbox region.
    /// Used to apply locational damage multipliers and critical hit rules.
    /// </summary>
    public enum HitLocation
    {
        Core,          // Default torso/center-of-mass (1.0x)
        Head,          // Critical spot (e.g. 2.0x)
        WeakPoint,     // Specialized monster/boss vulnerability (e.g. 1.5x)
        Limb,          // Arms, legs (e.g. 0.75x)
        ArmorPlating   // Heavy protective plating (absorbs or heavily mitigates damage)
    }

    /// <summary>
    /// Elemental or mechanical nature of the damage.
    /// Can interact with HealthType resistances (e.g. Corrosive vs Armored, Incendiary vs Flesh).
    /// </summary>
    public enum DamageType
    {
        Physical,      // Standard ballistic/kinetic damage
        Incendiary,    // Fire/heat damage (highly effective against Flesh)
        Corrosive,     // Chemical/acid damage (highly effective against Armor)
        Shock,         // Electric/energy damage (highly effective against Shields)
        Explosive,     // High-concussion area damage
        TrueDamage     // Ignores all shields, armor, and resistances directly
    }

    /// <summary>
    /// Primary defensive classification of a target's vitality pool.
    /// </summary>
    public enum HealthType
    {
        Flesh,         // Standard biological health
        Armored,       // Hardened hull or armored carapaces (resists kinetic, weak to corrosive)
        Mechanical,    // Synthetic robots, turrets, drones
        Structure      // Breakable world props, crates, barricades, barrels
    }
}
