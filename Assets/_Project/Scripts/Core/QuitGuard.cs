using UnityEngine;

/// <summary>
/// Makes quitting a built game actually end the process. Playtesters found the exe hanging on in Task Manager after
/// Exit (network and Steam threads keep it alive), which then stopped the game from starting again. On quit we close
/// the network sessions and Steam, then end the process.
/// </summary>
public static class QuitGuard
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        if (Application.isEditor) return;
        Application.quitting += OnQuitting;
    }

    private static void OnQuitting()
    {
        try
        {
            foreach (var runner in Object.FindObjectsByType<Fusion.NetworkRunner>(FindObjectsSortMode.None))
                if (runner != null && runner.IsRunning) runner.Shutdown();
        }
        catch (System.Exception e) { Debug.LogWarning("[Quit] runner shutdown: " + e.Message); }

        try { if (SteamManager.Initialized) Steamworks.SteamAPI.Shutdown(); }
        catch (System.Exception e) { Debug.LogWarning("[Quit] Steam shutdown: " + e.Message); }

        Debug.Log("[Quit] process exit");
        System.Diagnostics.Process.GetCurrentProcess().Kill();
    }
}
