using Project.Core.Input;
using UnityEngine;

namespace Project.Player.Input
{
public class RemotePlayerInputProvider : MonoBehaviour, IPlayerInputProvider
{
    public PlayerInputData GetCurrentInput()
    {
        return PlayerInputData.Empty;
    }
}
}