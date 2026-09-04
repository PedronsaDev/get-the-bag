namespace Project.Combat
{
    /// <summary>
    /// Core interface for any entity, hitbox, or destructible prop capable of taking damage.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// Applies incoming damage to this entity.
        /// </summary>
        /// <param name="payload">Detailed damage information including amount, type, location, and attacker.</param>
        /// <returns>Feedback report detailing actual damage applied, critical status, and fatal result.</returns>
        DamageResult TakeDamage(DamagePayload payload);
    }
}
