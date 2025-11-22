using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace Networking
{
    public class NetworkRunner : MonoBehaviour, INetworkRunner, INetworkRunnerCallbacks
    {
        private PlayerInputActions _controls;
        private Fusion.NetworkRunner _runner;
        private NetworkLobbyCodeGenerator _codeGenerator;

        private List<SessionInfo> _sessions = new();
        private const int GAME_SCENE_BUILD_INDEX = 2;

        public bool IsConnected => _runner != null && _runner.IsRunning;

        void Awake()
        {
            _codeGenerator = new NetworkLobbyCodeGenerator();

            _controls = new PlayerInputActions();
            _controls.Enable();
        }

        public async UniTask Init()
        {
            if (_runner != null) return;

            _runner = gameObject.AddComponent<Fusion.NetworkRunner>();
            _runner.ProvideInput = true;
            _runner.AddCallbacks(this);

            await JoinLobbyAsync();
        }

        public void Shutdown()
        {
            if (_runner != null)
            {
                if (_runner.IsRunning) _runner.Shutdown();
                Destroy(_runner.gameObject);
                _runner = null;
            }
        }

        public async Task<string> CreateLobbyAsync(int maxPlayers, bool isPublic)
        {
            string code = _codeGenerator.GenerateCode(_sessions);
            if (code == null) return null;

            var props = new Dictionary<string, SessionProperty>()
            {
                { "code", code },
                { "public", isPublic ? 1 : 0 },
                { "maxPlayers", maxPlayers }
            };

            var args = new StartGameArgs()
            {
                GameMode = GameMode.Host,
                SessionName = code,
                Scene = SceneRef.FromIndex(GAME_SCENE_BUILD_INDEX),
                PlayerCount = maxPlayers,
                SessionProperties = props
            };

            var result = await _runner.StartGame(args);

            if (result.Ok)
            {
                return code;
            }

            Debug.LogError("Failed to create lobby: " + result.ShutdownReason);
            return null;
        }

        public async Task<bool> JoinRandomPublicAsync()
        {
            SessionInfo best = null;

            foreach (var s in _sessions)
            {
                if (s.Properties.TryGetValue("public", out var p) && (int)p == 1)
                {
                    if (best == null || s.PlayerCount > best.PlayerCount)
                        best = s;
                }
            }

            if (best == null)
            {
                Debug.LogError("No public rooms found");
                return false;
            }

            var args = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = best.Name
            };

            var result = await _runner.StartGame(args);

            return result.Ok;
        }

        public async Task<bool> JoinByCodeAsync(string code)
        {
            var lobby = _sessions.Find(s =>
                s.Properties.ContainsKey("code") &&
                ((string)s.Properties["code"]) == code);

            if (lobby == null)
            {
                Debug.Log("Lobby not found");
                return false;
            }

            var args = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = lobby.Name
            };

            var result = await _runner.StartGame(args);

            return result.Ok;
        }


        public void Leave()
        {
            if (_runner == null || !_runner.IsRunning)
                return;
            
            _runner.Shutdown();
        }

        public void OnInput(Fusion.NetworkRunner runner, NetworkInput input)
        {
            var data = new NetworkInputData();

            Vector2 move = _controls.Player.Move.ReadValue<Vector2>();
            data.Direction = new Vector3(move.x, 0, move.y);

            input.Set(data);
        }

        public void OnSessionListUpdated(Fusion.NetworkRunner runner, List<SessionInfo> sessionList)
        {
            _sessions = sessionList;
            Debug.Log($"[Fusion] Session list updated: {_sessions.Count} sessions");
            foreach (var s in _sessions)
            {
                if (s.Properties.TryGetValue("public", out var pub))
                    Debug.Log($" - Session: {s.Name}, Players: {s.PlayerCount}, Public: {pub}");
            }
        }

        public void OnPlayerJoined(Fusion.NetworkRunner runner, PlayerRef player) { }
        public void OnPlayerLeft(Fusion.NetworkRunner runner, PlayerRef player) { }
        public void OnInputMissing(Fusion.NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(Fusion.NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnConnectedToServer(Fusion.NetworkRunner runner) { }
        public void OnDisconnectedFromServer(Fusion.NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(Fusion.NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(Fusion.NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(Fusion.NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnCustomAuthenticationResponse(Fusion.NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(Fusion.NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnSceneLoadDone(Fusion.NetworkRunner runner) { }
        public void OnSceneLoadStart(Fusion.NetworkRunner runner) { }
        public void OnObjectExitAOI(Fusion.NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(Fusion.NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataReceived(Fusion.NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
        public void OnReliableDataProgress(Fusion.NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

        public async UniTask JoinLobbyAsync()
        {
            if (_runner == null)
            {
                Debug.LogError("Runner not initialized!");
                return;
            }

            var result = await _runner.JoinSessionLobby(SessionLobby.ClientServer);

            if (result.Ok)
            {
                Debug.Log("[Fusion] Joined lobby successfully");
            }
            else
            {
                Debug.LogError($"Failed to join lobby: {result.ShutdownReason}");
            }
        }
    }
}