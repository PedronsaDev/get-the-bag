using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Project.Core.Interaction;
using Project.Content;
using Project.Logging;
using Project.Loot;
using Project.Loot.Data;
using PurrNet;
using UnityEngine;

[Serializable]
class SpawnDef
{
    public LootDefinition Definition;
    public Transform SpawnTransform;

    public GameObject SpawnedObject { get; set; }
    public bool IsValid => Definition && SpawnTransform;
    public bool IsSpawned => SpawnedObject;
}

public class DEV_LootSpawner : MonoBehaviour, IInteractable
{
    [SerializeField] private List<SpawnDef> _spawnDefs = new List<SpawnDef>();
    private bool _isSpawning;

    public string InteractionPrompt => "[F] Spawn Loot";
    public string HoldPrompt => null;
    public string TargetName => "Loot Spawner";
    public string Subtext => null;
    public Color AccentColor => Color.white;
    public InteractionMode Mode => InteractionMode.Instant;
    public float HoldDuration => 0f;

    public bool CanInteract(IPlayerInteractor interactor, bool isHold = false) =>
        isActiveAndEnabled && !_isSpawning && GameContent.IsLoaded &&
        NetworkManager.main && NetworkManager.main.isServer;

    public string GetDisabledReason(IPlayerInteractor interactor, bool isHold = false)
    {
        if (!GameContent.IsLoaded) return "Game content is not ready.";
        if (!NetworkManager.main || !NetworkManager.main.isServer)
            return "Only the host can spawn test loot.";
        return _isSpawning ? "Loot is spawning." : null;
    }

    public void OnInteract(IPlayerInteractor interactor, bool isHold)
    {
        if (!CanInteract(interactor, isHold)) return;
        CreateLootAsync().Forget(exception =>
            AppLog.LogException(LogChannel.Network, exception, this));
    }

    private async UniTask CreateLootAsync()
    {
        _isSpawning = true;
        var manager = NetworkManager.main;
        try
        {
            foreach (var spawnDef in _spawnDefs)
            {
                if (!this || !isActiveAndEnabled || !GameContent.IsLoaded || !manager || !manager.isServer)
                    return;
                if (spawnDef == null || !spawnDef.IsValid || spawnDef.IsSpawned)
                    continue;

                try
                {
                    var loot = await LootSpawnService.SpawnAsync(spawnDef.Definition.Id,
                        spawnDef.SpawnTransform.position, spawnDef.SpawnTransform.rotation,
                        this.GetCancellationTokenOnDestroy());
                    spawnDef.SpawnedObject = loot.gameObject;
                }
                catch (OperationCanceledException) { return; }
                catch (Exception exception) { AppLog.LogException(LogChannel.Network, exception, this); }

            }
        }
        finally
        {
            _isSpawning = false;
        }
    }

    public void OnHoverEnter(IPlayerInteractor interactor) { }
    public void OnHoverExit(IPlayerInteractor interactor) { }
}
