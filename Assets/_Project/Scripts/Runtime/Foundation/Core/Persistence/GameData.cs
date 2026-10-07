using System;
using Project.Core.Collections;
using UnityEngine;

namespace Project.Core.Persistence
{
[Serializable]
public class PlayerProfileSaveData
{
    public string Name = "PLAYER_NAME";
    public int TotalExperience;
    public int Scrip;
}

[Serializable]
public class SettingsSaveData
{
    public float MasterVolume = 1f;
    public float MusicVolume = 0.8f;
    public float SfxVolume = 1f;
    public float MouseSensitivity = 1f;
    public bool InvertY = false;
}


[Serializable]
public class GameData
{
    public long LastUpdated;
    public PlayerProfileSaveData Player = new();
    public SettingsSaveData Settings = new();
    public SerializableDictionary<string, string> CustomBuckets = new();

    public void SetBucket<T>(string key, T data)
    {
        if (data == null)
        {
            CustomBuckets.Remove(key);
            return;
        }

        CustomBuckets[key] = JsonUtility.ToJson(data);
    }

    public bool TryGetBucket<T>(string key, out T data)
    {
        if (CustomBuckets.TryGetValue(key, out string json) && !string.IsNullOrEmpty(json))
        {
            data = JsonUtility.FromJson<T>(json);
            return true;
        }

        data = default;
        return false;
    }

    public T GetBucketOrDefault<T>(string key, T defaultValue = default)
    {
        return TryGetBucket<T>(key, out var data) ? data : defaultValue;
    }
}
}