using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.AI.Combat;
using Project.AI.Decision;
using Project.AI.Locomotion;
using Project.Core.Match;
using Project.Core.Match.Zones;
using Project.Loot;
using PurrNet;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Game.Mvp
{
    /// <summary>Fixed, authored content for the Bank playtest. Only the server creates raid content.</summary>
    public sealed class MvpRaidSetup : MonoBehaviour
    {
        [Serializable]
        public struct LootPoint
        {
            public string ItemId;
            public Transform Point;
        }

        [SerializeField] private RaidManager _raid;
        [SerializeField] private ExtractionZone _extraction;
        [SerializeField] private LootPoint[] _lootPoints = Array.Empty<LootPoint>();
        [SerializeField] private GameObject _enemyPrefab;
        [SerializeField] private Transform[] _enemyPoints = Array.Empty<Transform>();
        private bool _initialized;

        public RaidManager Raid => _raid;
        public ExtractionZone Extraction => _extraction;

        public async UniTask InitializeServerAsync(CancellationToken cancellation)
        {
            var manager = NetworkManager.main;
            if (!manager || !manager.isServer || _initialized) return;
            if (!_raid || !_extraction) throw new InvalidOperationException("The Bank needs a raid manager and extraction zone.");
            _initialized = true;
            foreach (var point in _lootPoints)
            {
                cancellation.ThrowIfCancellationRequested();
                if (!point.Point) throw new InvalidOperationException("A Bank loot spawn point is missing.");
                await LootSpawnService.SpawnAsync(point.ItemId, point.Point.position, point.Point.rotation, cancellation);
            }
            if (_enemyPoints.Length > 0 && !_enemyPrefab) throw new InvalidOperationException("The Bank enemy prefab is missing.");
            foreach (var point in _enemyPoints)
            {
                cancellation.ThrowIfCancellationRequested();
                if (!point) continue;
                var enemy = Instantiate(_enemyPrefab, point.position, point.rotation);
                SceneManager.MoveGameObjectToScene(enemy, gameObject.scene);
                manager.Spawn(enemy);
            }
        }

        public void StopCombatServer()
        {
            if (!NetworkManager.main || !NetworkManager.main.isServer) return;
            foreach (var brain in FindObjectsByType<AIBrain>())
                if (brain.gameObject.scene == gameObject.scene) brain.enabled = false;
            foreach (var weapon in FindObjectsByType<AIWeaponExecutor>())
                if (weapon.gameObject.scene == gameObject.scene) weapon.enabled = false;
            foreach (var movement in FindObjectsByType<AINavMeshLocomotion>())
                if (movement.gameObject.scene == gameObject.scene) movement.enabled = false;
        }
    }
}
