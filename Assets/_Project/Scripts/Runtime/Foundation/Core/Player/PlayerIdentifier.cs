using System.Collections.Generic;
using PurrNet;
using UnityEngine;

namespace Project.Core.Player
{
[DisallowMultipleComponent]
public class PlayerIdentifier : NetworkBehaviour
{
    [SerializeField] private Transform _playerBody;
    
    public Transform BodyTransform => _playerBody ? _playerBody : transform;
    
    public IPlayerStatusProvider StatusProvider { get; set; }
    
    public bool IsDead => StatusProvider is { IsDead: true };
    public bool IsDowned => StatusProvider is { IsDowned: true };
    public bool IsStanding => StatusProvider == null || StatusProvider.IsStanding;
    
    private static readonly List<PlayerIdentifier> _activePlayers = new();
    public static IReadOnlyList<PlayerIdentifier> ActivePlayers => _activePlayers;

    public PlayerID? PlayerID
    {
        get
        {
            if (isSpawned && owner.HasValue)
                return owner.Value;

            if (NetworkManager.main && !NetworkManager.main.isOffline)
                return NetworkManager.main.localPlayer;

            return PurrNet.PlayerID.Server;
        }
    }

    protected override void OnSpawned()
    {
        base.OnSpawned();
        if (!_activePlayers.Contains(this))
            _activePlayers.Add(this);
    }

    protected override void OnDespawned()
    {
        base.OnDespawned();
        _activePlayers.Remove(this);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _activePlayers.Remove(this);
    }

    public static bool TryGetPlayer(PlayerID playerId, out PlayerIdentifier player)
    {
        for (int i = 0; i < _activePlayers.Count; i++)
        {
            var p = _activePlayers[i];
            if (p && p.PlayerID.HasValue && p.PlayerID.Value == playerId)
            {
                player = p;
                return true;
            }
        }

        player = null;
        return false;
    }
}
}