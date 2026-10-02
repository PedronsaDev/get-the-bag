namespace Project.Core.Persistence
{
public interface IDataPersistence
{
    void LoadData(GameData data);

    void SaveData(GameData data);
}
}