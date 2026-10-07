using Project.Combat.Core;

namespace Project.Combat.Core
{
public interface IDamageable
{
    DamageResult TakeDamage(DamagePayload payload);
}
}