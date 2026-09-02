namespace Project.Core.Persistence
{
    /// <summary>
    /// Contract implemented by any system or component that requires saving and loading state.
    /// Eliminates hardcoded dependencies between DataPersistenceManager and individual managers.
    /// </summary>
    public interface IDataPersistence
    {
        /// <summary>
        /// Invoked when game data has been read from disk and is being dispatched to systems.
        /// </summary>
        void LoadData(GameData data);

        /// <summary>
        /// Invoked before saving; implementations should write their current state into the GameData container.
        /// </summary>
        void SaveData(GameData data);
    }
}
