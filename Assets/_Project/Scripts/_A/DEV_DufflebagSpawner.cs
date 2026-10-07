using Project.Core.Interaction;
using Project.Loot.Data;
using Project.Loot.World;
using System.Collections;
using PurrNet;
using UnityEngine;

public class DEV_DufflebagSpawner : MonoBehaviour, IInteractable
{
    [SerializeField] private GameObject _dufflebagPrefab;
    [SerializeField] private Transform _spawnPoint;


    public string InteractionPrompt => "[F] Spawn Duffle Bag";
    public string HoldPrompt { get; }
    public string TargetName { get; }
    public string Subtext { get; }
    public Color AccentColor { get; }
    public InteractionMode Mode => InteractionMode.Instant;
    public float HoldDuration { get; }

    private IEnumerator Start()
    {
        yield return new WaitUntil(() => NetworkManager.main && !NetworkManager.main.isOffline);
        CreateBag();
    }

    public bool CanInteract(IPlayerInteractor interactor, bool isHold = false)
    {
        return NetworkManager.main && NetworkManager.main.isServer;
    }

    public string GetDisabledReason(IPlayerInteractor interactor, bool isHold = false)
    {
        return "Only the host can spawn a test bag.";
    }

    public void OnInteract(IPlayerInteractor interactor, bool isHold)
    {
        CreateBag();
    }

    private void CreateBag()
    {
        if (!NetworkManager.main || !NetworkManager.main.isServer || !_dufflebagPrefab || !_spawnPoint)
            return;
        if (Instantiate(_dufflebagPrefab, _spawnPoint.position, _spawnPoint.rotation).TryGetComponent(out WorldDuffleBag bag))
        {
            bag.Initialize(DuffleBagSnapshot.Empty);
        }
    }

    public void OnHoverEnter(IPlayerInteractor interactor)
    {

    }

    public void OnHoverExit(IPlayerInteractor interactor)
    {

    }
}
