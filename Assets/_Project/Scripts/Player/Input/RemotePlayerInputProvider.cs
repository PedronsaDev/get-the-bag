using Project.Core.Input;
using UnityEngine;

namespace Project.Player.Input
{
    /// <summary>
    /// Input provider for remote/proxy player instances.
    /// Remote players receive no local hardware input; their movement is driven
    /// by PurrNet's NetworkTransform replication and interpolation.
    /// </summary>
    public class RemotePlayerInputProvider : MonoBehaviour, IPlayerInputProvider
    {
        public PlayerInputData GetCurrentInput()
        {
            return PlayerInputData.Empty;
        }
    }
}
