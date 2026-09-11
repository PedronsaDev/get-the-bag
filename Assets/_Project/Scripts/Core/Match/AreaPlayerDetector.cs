using System;
using System.Collections.Generic;
using PurrNet;
using UnityEngine;
using Project.Core.Player;

namespace Project.Core.Match
{
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class AreaPlayerDetector : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private LayerMask _playerLayer;
    [SerializeField] private bool _enableDebugLogs = false;
    [SerializeField] private bool _startEnabled = true;

    private readonly Dictionary<Collider, PlayerID> _colliderToPlayer = new();

    private readonly Dictionary<PlayerID, HashSet<Collider>> _playerColliders = new();

    private readonly Collider[] _overlapBuffer = new Collider[32];
    private readonly HashSet<Collider> _currentScanColliders = new();
    private readonly List<Collider> _collidersToRemove = new();

    private Collider _cachedCollider;
    private BoxCollider _cachedBox;
    private SphereCollider _cachedSphere;

    [Header("Networked State")] 
    public SyncVar<int> PlayersInsideCount = new(0);
    public SyncVar<int> TotalRequiredCount = new(0);
    public SyncVar<bool> AreAllPlayersInside = new(false);
    
    public IReadOnlyCollection<PlayerID> PlayersInside => _playerColliders.Keys;

    public event Action OnAllPlayersEntered;
    public event Action OnPlayerLeft;
    public event Action<PlayerID> OnPlayerEnteredArea;
    public event Action<PlayerID> OnPlayerExitedArea;
    public event Action OnAreaOccupancyChanged;
    
    private bool _isEnabled = true;

    private bool IsServerAuthority =>
        isServer || (NetworkManager.main && NetworkManager.main.isServer) ||
        (!NetworkManager.main || NetworkManager.main.isOffline);

    private void Awake()
    {
        _cachedCollider = GetComponent<Collider>();
        _cachedBox = _cachedCollider as BoxCollider;
        _cachedSphere = _cachedCollider as SphereCollider;
        _isEnabled = _startEnabled;

        if (_cachedCollider)
        {
            _cachedCollider.isTrigger = true;
        }

        if (TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        int selfLayer = LayerMask.NameToLayer("PlayerSelf");
        int othersLayer = LayerMask.NameToLayer("PlayerOthers");

        if (selfLayer != -1) _playerLayer |= (1 << selfLayer);
        if (othersLayer != -1) _playerLayer |= (1 << othersLayer);
    }

    private void FixedUpdate()
    {
        if (!IsServerAuthority || !_isEnabled)
            return;

        PerformSpatialScan();
    }

    private void PerformSpatialScan()
    {
        _currentScanColliders.Clear();

        int hitCount = 0;
        if (_cachedBox)
        {
            Vector3 center = _cachedBox.transform.TransformPoint(_cachedBox.center);
            Vector3 halfExtents = Vector3.Scale(_cachedBox.size * 0.5f, _cachedBox.transform.lossyScale);
            halfExtents = new Vector3(Mathf.Abs(halfExtents.x), Mathf.Abs(halfExtents.y), Mathf.Abs(halfExtents.z));
            hitCount = UnityEngine.Physics.OverlapBoxNonAlloc(center, halfExtents, _overlapBuffer,
                _cachedBox.transform.rotation, _playerLayer, QueryTriggerInteraction.Collide);
        }
        else if (_cachedSphere)
        {
            Vector3 center = _cachedSphere.transform.TransformPoint(_cachedSphere.center);
            float radius = _cachedSphere.radius * Mathf.Max(Mathf.Abs(_cachedSphere.transform.lossyScale.x),
                Mathf.Max(Mathf.Abs(_cachedSphere.transform.lossyScale.y),
                    Mathf.Abs(_cachedSphere.transform.lossyScale.z)));
            hitCount = UnityEngine.Physics.OverlapSphereNonAlloc(center, radius, _overlapBuffer, _playerLayer,
                QueryTriggerInteraction.Collide);
        }
        else if (_cachedCollider)
        {
            hitCount = UnityEngine.Physics.OverlapBoxNonAlloc(_cachedCollider.bounds.center,
                _cachedCollider.bounds.extents, _overlapBuffer, Quaternion.identity, _playerLayer,
                QueryTriggerInteraction.Collide);
        }

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _overlapBuffer[i];
            if (hit)
            {
                _currentScanColliders.Add(hit);
                RegisterColliderEnter(hit);
            }
        }

        _collidersToRemove.Clear();
        foreach (var trackedCollider in _colliderToPlayer.Keys)
        {
            if (!trackedCollider || !_currentScanColliders.Contains(trackedCollider))
            {
                _collidersToRemove.Add(trackedCollider);
            }
        }

        foreach (var t in _collidersToRemove)
        {
            RegisterColliderExit(t);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServerAuthority || !_isEnabled)
            return;

        RegisterColliderEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServerAuthority || !_isEnabled)
            return;

        RegisterColliderExit(other);
    }

    private void RegisterColliderEnter(Collider other)
    {
        if (!other)
            return;

        if (_colliderToPlayer.ContainsKey(other))
            return;

        if (!TryGetPlayerID(other, out PlayerID playerId))
            return;

        _colliderToPlayer[other] = playerId;

        if (!_playerColliders.TryGetValue(playerId, out var colliders))
        {
            colliders = new HashSet<Collider>();
            _playerColliders[playerId] = colliders;
        }

        bool isFirstCollider = colliders.Count == 0;
        colliders.Add(other);

        if (isFirstCollider)
        {
            if (_enableDebugLogs)
            {
                Debug.Log(
                    $"[AreaPlayerDetector] Player '{playerId}' ENTERED zone '{name}'. Active players inside: {_playerColliders.Count}");
            }

            OnPlayerEnteredArea?.Invoke(playerId);
            EvaluateAreaState();
        }
    }

    private void RegisterColliderExit(Collider other)
    {
        if (!other)
            return;

        if (!_colliderToPlayer.Remove(other, out PlayerID playerId))
            return;

        if (_playerColliders.TryGetValue(playerId, out var colliders))
        {
            colliders.Remove(other);

            if (colliders.Count == 0)
            {
                _playerColliders.Remove(playerId);

                if (_enableDebugLogs)
                {
                    Debug.Log(
                        $"[AreaPlayerDetector] Player '{playerId}' EXITED zone '{name}'. Remaining players inside: {_playerColliders.Count}");
                }

                OnPlayerExitedArea?.Invoke(playerId);
                OnPlayerLeft?.Invoke();
                EvaluateAreaState();
            }
        }
    }

    private bool TryGetPlayerID(Collider other, out PlayerID playerId)
    {
        playerId = default;

        if (!other)
            return false;

        if ((_playerLayer.value & (1 << other.gameObject.layer)) == 0)
            return false;

        var playerIdentifier = other.GetComponentInParent<PlayerIdentifier>();
        if (playerIdentifier && playerIdentifier.PlayerID.HasValue)
        {
            playerId = playerIdentifier.PlayerID.Value;
            return true;
        }

        var identity = other.GetComponentInParent<NetworkIdentity>();
        if (identity && identity.owner.HasValue)
        {
            playerId = identity.owner.Value;
            return true;
        }

        if (NetworkManager.main)
        {
            if (playerIdentifier || identity || other.CompareTag("Player") ||
                other.transform.root.name.Contains("Player"))
            {
                playerId = NetworkManager.main.localPlayer;
                return true;
            }
        }

        if (playerIdentifier || identity || other.CompareTag("Player") || other.transform.root.name.Contains("Player"))
        {
            playerId = PurrNet.PlayerID.Server;
            return true;
        }

        return false;
    }

    public void EvaluateAreaState()
    {
        CleanNullColliders();
        PruneDisconnectedPlayers();

        int totalConnected = GetTotalConnectedPlayerCount();
        int insideCount = _playerColliders.Count;

        PlayersInsideCount.value = insideCount;
        TotalRequiredCount.value = totalConnected;

        bool allInside = totalConnected > 0 && insideCount >= totalConnected;

        if (allInside && !AreAllPlayersInside.value)
        {
            AreAllPlayersInside.value = true;
            if (_enableDebugLogs)
            {
                Debug.Log(
                    $"[AreaPlayerDetector] All required players are INSIDE zone '{name}' ({insideCount}/{totalConnected})!");
            }

            OnAllPlayersEntered?.Invoke();
        }
        else if (!allInside && AreAllPlayersInside.value)
        {
            AreAllPlayersInside.value = false;
            if (_enableDebugLogs)
            {
                Debug.Log(
                    $"[AreaPlayerDetector] Players are no longer all inside zone '{name}' ({insideCount}/{totalConnected}).");
            }
        }

        OnAreaOccupancyChanged?.Invoke();
    }

    private void CleanNullColliders()
    {
        var deadPlayers = new List<PlayerID>();

        foreach (var kvp in _playerColliders)
        {
            kvp.Value.RemoveWhere(c => !c);
            if (kvp.Value.Count == 0)
            {
                deadPlayers.Add(kvp.Key);
            }
        }

        foreach (var playerId in deadPlayers)
        {
            _playerColliders.Remove(playerId);
            OnPlayerExitedArea?.Invoke(playerId);
            OnPlayerLeft?.Invoke();
        }
    }

    private int GetTotalConnectedPlayerCount()
    {
        if (!NetworkManager.main || NetworkManager.main.isOffline)
            return 1;

        var playerModule = (networkManager ? networkManager.playerModule : null)
                           ?? NetworkManager.main.playerModule;

        if (playerModule != null && playerModule.players.Count > 0)
            return playerModule.players.Count;

        return 1;
    }

    private void PruneDisconnectedPlayers()
    {
        if (!NetworkManager.main || NetworkManager.main.isOffline)
            return;

        var playerModule = (networkManager ? networkManager.playerModule : null)
                           ?? NetworkManager.main.playerModule;

        if (playerModule == null)
            return;

        var activePlayers = playerModule.players;
        var disconnected = new List<PlayerID>();

        foreach (var playerId in _playerColliders.Keys)
        {
            bool isStillActive = false;
            for (int i = 0; i < activePlayers.Count; i++)
            {
                if (activePlayers[i] == playerId)
                {
                    isStillActive = true;
                    break;
                }
            }

            if (!isStillActive)
            {
                disconnected.Add(playerId);
            }
        }

        foreach (var playerId in disconnected)
        {
            if (_playerColliders.TryGetValue(playerId, out var colliders))
            {
                foreach (var col in colliders)
                {
                    if (col)
                        _colliderToPlayer.Remove(col);
                }

                _playerColliders.Remove(playerId);
                OnPlayerExitedArea?.Invoke(playerId);
                OnPlayerLeft?.Invoke();
            }
        }
    }

    public bool IsPlayerInside(PlayerID playerId)
    {
        return _playerColliders.ContainsKey(playerId);
    }

    private void OnDisable()
    {
        _colliderToPlayer.Clear();
        _playerColliders.Clear();
        _currentScanColliders.Clear();
        _collidersToRemove.Clear();
    }
    
    public void Activate()
    {
        _isEnabled = true;  
    }
    
    public void Deactivate()
    {
        _isEnabled = false;  
    }
    
#if UNITY_EDITOR

    private void OnDrawGizmos()
    {
        var gizmoColor = AreAllPlayersInside.value
            ? new Color(0f, 1f, 0f, 0.35f)
            : (_playerColliders.Count > 0 ? new Color(1f, 0.9f, 0f, 0.35f) : new Color(0f, 0.7f, 1f, 0.2f));

        if (!_isEnabled)
        {
            gizmoColor = new Color(1f, 0.0f, 0.0f, 0.35f);
        }

        Gizmos.color = gizmoColor;

        BoxCollider box = _cachedBox ? _cachedBox : GetComponent<BoxCollider>();
        if (box)
        {
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
#endif
}
}