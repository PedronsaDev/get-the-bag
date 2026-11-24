using System.Collections.Generic;
using UnityEngine;

namespace Project.Match
{
public class ValidatedSpawnPointProvider : PurrNet.IProvideSpawnPoints
{
    private readonly PurrNet.PlayerSpawner _spawner;
    private int _currentIndex = 0;

    public ValidatedSpawnPointProvider(PurrNet.PlayerSpawner spawner)
    {
        _spawner = spawner;
    }

    public PurrNet.SpawnPoint NextSpawnPoint(PurrNet.PlayerID player, PurrNet.SceneID scene)
    {
        var active = SpawnPoint.ActiveSpawnPoints;
        var candidates = new List<SpawnPoint>();

        for (int i = 0; i < active.Count; i++)
        {
            var pt = active[i];
            if (pt && pt.IsEnabled && pt.gameObject.activeInHierarchy)
            {
                var validation = pt.Validate(true);
                if (validation.IsValid)
                {
                    candidates.Add(pt);
                }
            }
        }

        Transform target = null;

        if (candidates.Count > 0)
        {
            target = candidates[_currentIndex % candidates.Count].transform;
            _currentIndex = (_currentIndex + 1) % candidates.Count;
        }
        else
        {
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i] && active[i].IsEnabled && active[i].gameObject.activeInHierarchy)
                {
                    target = active[i].transform;
                    break;
                }
            }

            if (!target && _spawner && _spawner.SpawnPoints is { Count: > 0 })
            {
                for (int i = 0; i < _spawner.SpawnPoints.Count; i++)
                {
                    if (_spawner.SpawnPoints[i])
                    {
                        target = _spawner.SpawnPoints[i];
                        break;
                    }
                }
            }
        }

        if (target)
        {
            return new PurrNet.SpawnPoint
            {
                position = target.position,
                rotation = target.rotation
            };
        }

        return new PurrNet.SpawnPoint
        {
            position = Vector3.zero,
            rotation = Quaternion.identity
        };
    }
}
}
