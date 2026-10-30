using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Dev only: lets a built player test itself. Start the exe with <c>-autotest bomb_rush,prize_grab</c> and it hosts a
/// private match with those rounds, readies up, and every few seconds logs what the editor tests can't see in a build
/// (item sizes, outline state) and saves a screenshot next to Player.log. Does nothing without the argument.
/// </summary>
public class BuildAutoTest : MonoBehaviour
{
    private string[] _rounds;
    private float _nextAction, _nextReport;
    private bool _hosted;
    private int _shots;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        var args = System.Environment.GetCommandLineArgs();
        int index = System.Array.IndexOf(args, "-autotest");
        if (index < 0 || index + 1 >= args.Length) return;
        var go = new GameObject("BuildAutoTest");
        DontDestroyOnLoad(go);
        go.AddComponent<BuildAutoTest>()._rounds = args[index + 1].Split(',');
        Debug.Log("[AutoTest] rounds: " + args[index + 1]);
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextAction) return;
        _nextAction = Time.unscaledTime + 1f;
        string scene = SceneManager.GetActiveScene().name;

        var service = FindFirstObjectByType<Networking.FusionNetworkRunner>();
        if (!_hosted && scene == "MainMenu" && service != null && Time.unscaledTime > 8f)
        {
            _hosted = true;
            NetworkGameManager.ForcedRoundIds = _rounds;
            _ = service.CreateLobbyAsync(Networking.SessionSettings.Default(false));
            Debug.Log("[AutoTest] hosting");
            return;
        }

        var lobby = FindFirstObjectByType<LobbyManager>();
        if (scene == "Lobby" && lobby != null && lobby.Object != null && lobby.Object.IsValid)
            foreach (var p in lobby.Runner.ActivePlayers)
                if (!lobby.ReadyStates[p]) lobby.ToggleReady(p);

        var minigame = MinigameController.Current;
        if (minigame != null && minigame.Object != null && minigame.Object.IsValid && minigame.Phase == MinigamePhase.Intro && !minigame.IsReady(minigame.Runner.LocalPlayer))
            minigame.RPC_Ready();

        if (minigame != null && minigame.Object != null && minigame.Object.IsValid && minigame.IsGameActive && Time.unscaledTime >= _nextReport)
        {
            _nextReport = Time.unscaledTime + 4f;
            Report(minigame);
        }
    }

    private void Report(MinigameController minigame)
    {
        var sb = new StringBuilder("[AutoTest] ").Append(minigame.Definition.Id).Append(": ");
        foreach (var item in GrabbableObject.Active.Take(4))
        {
            var renderers = item.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) continue;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            sb.Append(item.name).Append(" size=").Append(bounds.size.ToString("F2"))
              .Append(" root=").Append(item.transform.lossyScale.ToString("F2"))
              .Append(" outline=").Append(item.Outline != null ? item.Outline.enabled.ToString() : "none").Append("; ");
        }
        var target = Player.Local != null && Player.Local.Interaction != null ? Player.Local.Interaction.CurrentTarget : null;
        sb.Append("target=").Append(target != null ? ((Component)target).name : "none");
        Debug.Log(sb.ToString());
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.persistentDataPath, $"autotest_{_shots++}.png"));
    }
}
