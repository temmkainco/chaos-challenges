using UnityEngine.SceneManagement;

/// <summary>
/// Single place for scene references. Resolves build indices from scene paths so that
/// reordering the Build Settings list does not silently break scene loading.
/// </summary>
public static class GameScenes
{
    public const string MainMenu = "Assets/_Project/Scenes/MainMenu/MainMenu.unity";
    public const string Lobby = "Assets/_Project/Scenes/Lobby.unity";
    public const string Intermission = "Assets/_Project/Scenes/MinigameIntermissionScene.unity";

    public static int LobbyBuildIndex => GetBuildIndex(Lobby);
    public static int IntermissionBuildIndex => GetBuildIndex(Intermission);

    public static int GetBuildIndex(string scenePath)
    {
        int index = SceneUtility.GetBuildIndexByScenePath(scenePath);
        if (index < 0)
            UnityEngine.Debug.LogError($"Scene '{scenePath}' is not in Build Settings.");
        return index;
    }
}
