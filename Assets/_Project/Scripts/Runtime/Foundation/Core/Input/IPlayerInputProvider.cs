namespace Project.Core.Input
{
/// <summary>
/// Contract for providing player input. Implemented by LocalPlayerInputProvider
/// for local players and RemotePlayerInputProvider for networked proxies.
/// </summary>
public interface IPlayerInputProvider
{
    PlayerInputData GetCurrentInput();
}
}