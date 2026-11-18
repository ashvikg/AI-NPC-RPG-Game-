using UnityEngine;

/// <summary>
/// Starts the whole game automatically when you press Play in ANY scene (even an empty one).
/// Set AutoStart to false if you would rather add a GameManager to your own scene.
/// </summary>
public static class GameBootstrap
{
    public const bool AutoStart = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (!AutoStart) return;
        if (GameManager.I != null) return;
        var go = new GameObject("GameManager");
        go.AddComponent<GameManager>();
    }
}
