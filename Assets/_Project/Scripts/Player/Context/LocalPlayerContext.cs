using System;
using Project.Core;
using UnityEngine;

namespace Project.Player
{
public static class LocalPlayerContext
{
    public static PlayerFacade Player { get; private set; }
    public static bool HasLocalPlayer => Player;
    public static Camera MainCamera => CameraRig.Instance ? CameraRig.Instance.Camera : Camera.main;

    public static event Action<PlayerFacade> OnLocalPlayerSpawned;
    public static event Action<PlayerFacade> OnLocalPlayerDespawned;

    public static void RegisterLocalPlayer(PlayerFacade player)
    {
        Player = player;
        OnLocalPlayerSpawned?.Invoke(player);
    }

    public static void UnregisterLocalPlayer(PlayerFacade player)
    {
        if (Player == player)
        {
            Player = null;
            OnLocalPlayerDespawned?.Invoke(player);
        }
    }
}
}