using System.Collections.Generic;
using PurrNet;
using UnityEngine;

namespace Project.Player.Mesh
{
public class PlayerMeshHandler : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private List<SkinnedMeshRenderer> _clientMeshes = new();
    
    protected override void OnSpawned()
    {
        base.OnSpawned();

        if (isOwner)
            DisableClientMeshes();
        else
            EnableClientMeshes();
    }

    private void EnableClientMeshes()
    {
        foreach (var mesh in _clientMeshes)
        {
            mesh.enabled = true;
        }
    }

    private void DisableClientMeshes()
    {
        foreach (var mesh in _clientMeshes)
        {
            mesh.enabled = false;
        }
        
    }
}
}