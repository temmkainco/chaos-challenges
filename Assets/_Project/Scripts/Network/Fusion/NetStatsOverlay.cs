using System.Linq;
using System.Text;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Networking
{
    /// <summary>
    /// Playtest diagnostics. F3 shows ping, frame rate and how often your own predicted position turned out wrong once
    /// the host's data arrived (<see cref="PredictionStats"/>: the "rubber-banding" behind most desync reports); the
    /// host also sees every player's ping. While in a session a
    /// one-line summary goes to Player.log every <see cref="_logInterval"/> seconds, so a playtest log shows what the
    /// connection was like before a problem.
    /// </summary>
    public class NetStatsOverlay : MonoBehaviour
    {
        [SerializeField, Tooltip("Seconds between [Net] summaries in Player.log while in a session (0: off)")]
        private float _logInterval = 30f;

        private FusionNetworkRunner _session;
        private bool _visible;
        private float _fps = 60f, _nextLog;
        private int _mispredictionsAtLog;
        private float _metresAtLog;
        private GUIStyle _style;
        private readonly StringBuilder _text = new();

        private void Awake() => _session = GetComponent<FusionNetworkRunner>();

        private NetworkRunner Runner => _session != null ? _session.Runner : null;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame) _visible = !_visible;
            if (Time.unscaledDeltaTime > 0f) _fps = Mathf.Lerp(_fps, 1f / Time.unscaledDeltaTime, 0.05f);

            var runner = Runner;
            if (_logInterval <= 0f || runner == null || !runner.IsRunning || !runner.IsInSession || Time.unscaledTime < _nextLog) return;
            _nextLog = Time.unscaledTime + _logInterval;
            int mispredictions = PredictionStats.Mispredictions - _mispredictionsAtLog;
            float metres = PredictionStats.MispredictedMetres - _metresAtLog;
            _mispredictionsAtLog = PredictionStats.Mispredictions;
            _metresAtLog = PredictionStats.MispredictedMetres;
            Debug.Log($"[Net] {(runner.IsServer ? "host" : "client")} players={runner.ActivePlayers.Count()} {Pings(runner)} fps={_fps:0} " +
                      $"mispredictions={mispredictions} ({(mispredictions > 0 ? metres / mispredictions * 100f : 0f):0}cm avg) in {_logInterval:0}s");
        }

        private static string Pings(NetworkRunner runner)
        {
            if (!runner.IsServer) return $"ping={runner.GetPlayerRtt(runner.LocalPlayer) * 1000.0:0}ms";
            return "pings=[" + string.Join(" ", runner.ActivePlayers.Where(p => p != runner.LocalPlayer)
                .Select(p => $"{p.PlayerId}:{runner.GetPlayerRtt(p) * 1000.0:0}ms")) + "]";
        }

        private void OnGUI()
        {
            if (!_visible) return;
            _style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 15, richText = true };
            var runner = Runner;
            _text.Clear();
            _text.AppendLine($"<b>NET</b>  fps {_fps:0}");
            if (runner == null || !runner.IsRunning || !runner.IsInSession)
                _text.AppendLine("not in a session");
            else
            {
                _text.AppendLine($"{(runner.IsServer ? "HOST" : "CLIENT")}  code {_session.CurrentLobbyCode}  tick {runner.TickRate}/s");
                _text.AppendLine(Pings(runner));
                _text.AppendLine($"mispredictions {PredictionStats.Mispredictions}  avg {(PredictionStats.Mispredictions > 0 ? PredictionStats.MispredictedMetres / PredictionStats.Mispredictions * 100f : 0f):0} cm");
            }
            GUI.Box(new Rect(10f, 10f, 330f, 92f), _text.ToString(), _style);
        }
    }
}
