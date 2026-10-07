using Project.Core.Persistence;
using Project.Persistence;
using UnityEngine;

namespace Project.Player.Loot
{
public class InventoryPersistenceBridge : MonoBehaviour, IDataPersistence
{
    private void OnEnable()
    {
        if (DataPersistenceManager.Instance)
        {
            DataPersistenceManager.Instance.Register(this);
        }
    }
    
    private void OnDisable()
    {
        if (DataPersistenceManager.HasInstance)
        {
            DataPersistenceManager.Instance.Unregister(this);
        }
    }
    
    private void OnExtractionSuccess()
    {
        
    }

    private void OnSquadWipe()
    {
        
    }

    public void LoadData(GameData data)
    {
        
    }

    public void SaveData(GameData data)
    {
        
    }
}
}