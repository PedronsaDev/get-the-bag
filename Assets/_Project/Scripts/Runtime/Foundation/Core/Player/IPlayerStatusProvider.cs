namespace Project.Core
{
public interface IPlayerStatusProvider
{
    public bool IsDead { get; }
    public bool IsDowned { get; }
    public bool IsStanding => !IsDead && !IsDowned;
}
}