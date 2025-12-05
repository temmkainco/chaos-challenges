using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using Steamworks;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Networking
{
    public class FusionNetworkRunner : MonoBehaviour, INetworkRunner, INetworkRunnerCallbacks
    {
        public string CurrentLobbyCode { get; private set; }
        public CSteamID CurrentSteamLobby { get; private set; }

        private NetworkLobbyCodeGenerator _codeGenerator;

        private NetworkRunner _runner;

        private List<SessionInfo> _sessions = new();

        private TaskCompletionSource<bool> _steamLobbyReady;
        private Callback<LobbyCreated_t> _steamLobbyCreatedCallback;

        private const int GAME_SCENE_BUILD_INDEX = 2;

        public bool IsConnected => _runner != null && _runner.IsRunning;

        public async UniTask InitAsync()
        {
            if (_runner != null) return;

            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;
            _runner.AddCallbacks(this);

            _codeGenerator = new NetworkLobbyCodeGenerator();

            await JoinSessionLobbyAsync();

            if (!SteamManager.Initialized)
                return;

            _steamLobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnSteamLobbyCreated);
        }

        public void Shutdown()
        {
            if(_runner == null)
                return;

            if (!_runner.IsRunning)
                return;

            _runner.Shutdown();
            Destroy(_runner.gameObject);
            _runner = null;
        }

        public async Task<string> CreateLobbyAsync(int maxPlayers, bool isPublic)
        {
            string code = _codeGenerator.GenerateCode(_sessions);
            if (code == null) return null;

            CurrentLobbyCode = code;

            if (SteamManager.Initialized)
            {
                _steamLobbyReady = new TaskCompletionSource<bool>();

                SteamMatchmaking.CreateLobby(
                    isPublic ? ELobbyType.k_ELobbyTypePublic : ELobbyType.k_ELobbyTypeFriendsOnly,
                    maxPlayers
                );

                await _steamLobbyReady.Task;
            }

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
                return false;
            
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
            if (_runner == null)
                return false;

            if (IsConnected)
                return true;

            var lobby = _sessions.Find(s =>
                s.Properties.ContainsKey("code") &&
                ((string)s.Properties["code"]) == code);

            if (lobby == null)    
                return false;

            var args = new StartGameArgs()
            {
                GameMode = GameMode.Client,
                SessionName = lobby.Name
            };

            var result = await _runner.StartGame(args);
            return result.Ok;
        }

        private void OnSteamLobbyCreated(LobbyCreated_t callback)
        {
            if (callback.m_eResult != EResult.k_EResultOK)
            {
                _steamLobbyReady?.TrySetResult(false);
                return;
            }

            CurrentSteamLobby = new CSteamID(callback.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(CurrentSteamLobby, "FusionCode", CurrentLobbyCode);
            _steamLobbyReady?.TrySetResult(true);
        }

        public void Leave()
        {
            if (_runner == null || !_runner.IsRunning)
                return;

            CurrentLobbyCode = null;
            
            _runner.Shutdown();
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var data = new NetworkInputData();

            var localInput = PlayerInput.Local;

            if (localInput == null)
                return;

            data.Direction = new Vector3(localInput.Move.x, 0, localInput.Move.y);
            data.CameraRotation = localInput.CameraRotation;

            input.Set(data);
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            _sessions = sessionList;
        }

        public async UniTask JoinSessionLobbyAsync()
        {
            if (_runner == null)
                return;

            await _runner.JoinSessionLobby(SessionLobby.ClientServer);
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }

    }
}