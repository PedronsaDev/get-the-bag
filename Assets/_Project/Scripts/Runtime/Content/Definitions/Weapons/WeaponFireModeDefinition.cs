using UnityEngine;

namespace Project.Combat.Data
{
public enum WeaponFireMode
{
    SemiAutomatic,
    FullAutomatic,
    Burst
}

public interface IWeaponFireStrategy
{
    void SetTrigger(bool held);
    bool TryScheduleShot(double now);
    void Reset(bool requireRelease);
}

public abstract class WeaponFireModeDefinition : ScriptableObject
{
    public abstract IWeaponFireStrategy CreateStrategy(WeaponDefinition weapon);
}
}