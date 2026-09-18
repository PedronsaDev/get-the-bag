using PedronsaDev.Console;

namespace Project.Persistence
{
[CommandGroup("DataPersistence")]
public static class DataPersistenceCommands
{
    [Command("dp_save", "Saves the current game state.")]
    public static void SaveGame()
    {
        DataPersistenceManager.Instance.SaveGame();
    }

    [Command("dp_load", "Loads the game state.")]
    public static void LoadGame()
    {
        DataPersistenceManager.Instance.LoadGame();
    }
    
}
}