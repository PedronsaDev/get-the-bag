using System.Collections.Generic;
using Project.Logging;
using PurrNet;
using UnityEngine;

namespace Project.AI.Combat
{
[DisallowMultipleComponent]
public class CombatTokenDirector : NetworkBehaviour
{
    public static CombatTokenDirector Instance { get; private set; }

    [Header("Token Configuration")]
    [SerializeField] private int _baseRangedTokens = 3;
    [SerializeField] private int _tokensPerAdditionalPlayer = 1;
    [SerializeField] private float _tokenLeaseTimeout = 4.5f;

    // Acompanha o ID do atual dono e o momento do contrato (Time.time + _tokenLeaseTimeout)
    private readonly Dictionary<EntityId, float> _activeRangedHolders = new(16);

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            AppLog.LogWarning(LogChannel.AI, $"[{nameof(CombatTokenDirector)}] Multiple instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    protected override void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Calcula o novo maximo de tokens com o numero de players.
    public int GetMaxRangedTokens()
    {
        int players = (NetworkManager.main && NetworkManager.main.playerModule != null)
            ? NetworkManager.main.playerModule.players.Count
            : 1;
        return _baseRangedTokens + Mathf.Max(0, players - 1) * _tokensPerAdditionalPlayer;
    }

    public bool TryAcquireToken(EntityId requesterId, CombatTokenType tokenType)
    {
        if (!isServer) return false;

        CleanExpiredTokens();

        if (_activeRangedHolders.ContainsKey(requesterId))
        {
            // Renova os contratos.
            _activeRangedHolders[requesterId] = Time.time + _tokenLeaseTimeout;
            return true;
        }

        if (_activeRangedHolders.Count < GetMaxRangedTokens())
        {
            _activeRangedHolders.Add(requesterId, Time.time + _tokenLeaseTimeout);
            return true;
        }

        return false;
    }

    /// <summary>Renews an existing live claim only. Never acquires a new token after expiration.</summary>
    public bool TryRenewToken(EntityId requesterId, CombatTokenType tokenType)
    {
        if (!isServer || tokenType != CombatTokenType.RangedBurst)
            return false;
        if (!_activeRangedHolders.TryGetValue(requesterId, out float expiresAt))
            return false;
        if (Time.time >= expiresAt)
        {
            _activeRangedHolders.Remove(requesterId);
            return false;
        }
        _activeRangedHolders[requesterId] = Time.time + _tokenLeaseTimeout;
        return true;
    }

    public void ReleaseToken(EntityId requesterId, CombatTokenType tokenType)
    {
        if (!isServer) return;
        _activeRangedHolders.Remove(requesterId);
    }

    private readonly List<EntityId> _staleKeysBuffer = new(16);

    private void CleanExpiredTokens()
    {
        float currentTime = Time.time;
        _staleKeysBuffer.Clear();
        foreach (var kvp in _activeRangedHolders)
        {
            if (currentTime >= kvp.Value)
                _staleKeysBuffer.Add(kvp.Key);
        }

        for (int i = 0; i < _staleKeysBuffer.Count; i++)
        {
            _activeRangedHolders.Remove(_staleKeysBuffer[i]);
        }
    }
}
}
