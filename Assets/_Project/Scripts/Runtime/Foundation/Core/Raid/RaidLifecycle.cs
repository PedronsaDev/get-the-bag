using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Core.Raid
{
/// <summary>
/// The current level's raid phase, valid on server and clients. Read it every frame; it is a plain field read.
///
/// Who writes it (Project.Match only):
/// - A level's ready gate calls <see cref="BeginLoading"/> in Awake, so the phase is Loading before any Update.
/// - The gate marks Live; RaidManager marks Extraction and Ended.
/// - A scene that loads without claiming Loading (the hub, test scenes) becomes Live.
/// Phases only move forward (Loading, Live, Extraction, Ended) until the next level claims Loading.
/// </summary>
public static class RaidLifecycle
{
    private static Scene _claimedScene;
    private static bool _hasClaimedScene;

    public static RaidPhase Phase { get; private set; } = RaidPhase.Live;

    public static bool AllowsAI => Phase is RaidPhase.Live or RaidPhase.Extraction;

    /// <summary>Raised on the peer where the phase changed. Polling <see cref="Phase"/> is equally valid.</summary>
    public static event Action<RaidPhase> PhaseChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Phase = RaidPhase.Live;
        PhaseChanged = null;
        _hasClaimedScene = false;
        _claimedScene = default;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    /// <summary>A level scene starts its raid. Call from Awake of an object in that scene.</summary>
    internal static void BeginLoading(Scene levelScene)
    {
        _claimedScene = levelScene;
        _hasClaimedScene = true;
        Apply(RaidPhase.Loading);
    }

    internal static void MarkLive()
    {
        if (Phase == RaidPhase.Loading)
            Apply(RaidPhase.Live);
    }

    internal static void MarkExtraction()
    {
        if (Phase == RaidPhase.Live)
            Apply(RaidPhase.Extraction);
    }

    internal static void MarkEnded()
    {
        if (Phase != RaidPhase.Ended)
            Apply(RaidPhase.Ended);
    }

    /// <summary>Tests only: force a phase, ignoring the forward-only rule.</summary>
    internal static void SetForTests(RaidPhase phase) => Apply(phase);

    /// <summary>Tests only: back to the start-of-play state (Live, no claimed scene, no listeners).</summary>
    internal static void ResetForTests()
    {
        Phase = RaidPhase.Live;
        PhaseChanged = null;
        _hasClaimedScene = false;
        _claimedScene = default;
    }

    // Scene objects run Awake before sceneLoaded, so a level's gate has already claimed Loading here.
    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        if (_hasClaimedScene && _claimedScene == scene)
            return;

        _hasClaimedScene = false;
        Apply(RaidPhase.Live);
    }

    private static void Apply(RaidPhase phase)
    {
        if (Phase == phase)
            return;

        Phase = phase;
        PhaseChanged?.Invoke(phase);
    }
}
}
