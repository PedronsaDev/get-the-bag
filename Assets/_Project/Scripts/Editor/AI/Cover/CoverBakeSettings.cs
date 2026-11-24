using System;
using UnityEditor;
using UnityEngine;

namespace Project.AI.Cover.Editor
{
[Serializable]
public class CoverBakeSettings
{
    private const string PrefsKey = "GetTheBag.CoverBaker.Settings";

    public LayerMask ObstacleMask = ~0;
    public float StepInterval = 1.0f;
    public float MinSpacing = 0.9f;
    public float WallSearchDistance = 1.5f;
    public float LowCoverHeight = CoverDimensions.LowObstacleHeight;
    public float HighCoverHeight = CoverDimensions.HighObstacleHeight;
    public float MaxPeekDistance = CoverDimensions.MaxPeekDistance;
    public float AgentRadius = CoverDimensions.AgentRadius;
    public float MaxNavigationSnap = 0.1f;

    public static CoverBakeSettings Load()
    {
        string json = EditorPrefs.GetString(PrefsKey, string.Empty);
        if (string.IsNullOrEmpty(json))
            return new CoverBakeSettings();

        var settings = new CoverBakeSettings();
        JsonUtility.FromJsonOverwrite(json, settings);
        return settings;
    }

    public void Save() => EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(this));
}
}
