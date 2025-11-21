using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Networking
{
    public class NetworkRunner : MonoBehaviour, INetworkRunner, INetworkRunnerCallbacks
    {
        private PlayerInputActions _controls;
        private Fusion.NetworkRunner _runner;

        public bool IsConnected => _runner != null && _runner.IsRunning;

        void Awake()
        {
            _controls = new PlayerInputActions();
            _controls.Enable();
        }

        public void Init()
        {
            if (_runner != null) return;

            _runner = gameObject.AddComponent<Fusion.NetworkRunner>();
            _runner.ProvideInput = true;
            _runner.AddCallbacks(this);
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

        public void Host(string roomName, string password = "")
        {
            if (_runner == null) Init();
            StartGame(GameMode.Host, roomName);
        }

        public void Join(string roomName, string password = "")
        {
            if (_runner == null) Init();
            StartGame(GameMode.Client, roomName);
        }

        public void Leave()
        {
            if (_runner != null && _runner.IsRunning)
            {
                _runner.Shutdown();
                Debug.Log("Left session.");
            }
        }

        private async void StartGame(GameMode mode, string roomName)
        {
            const int GAME_SCENE_BUILD_INDEX = 2;
            var scene = SceneRef.FromIndex(GAME_SCENE_BUILD_INDEX);

            await _runner.StartGame(new StartGameArgs
            {
                GameMode = mode,
                SessionName = roomName,
                Scene = scene,
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
            });

            Debug.Log($"NetworkRunner started as {mode}");
        }


        public void OnInput(Fusion.NetworkRunner runner, NetworkInput input)
        {
            var data = new NetworkInputData();

            Vector2 move = _controls.Player.Move.ReadValue<Vector2>();
            data.Direction = new Vector3(move.x, 0, move.y);

            input.Set(data);
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
        public void OnSessionListUpdated(Fusion.NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(Fusion.NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(Fusion.NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnSceneLoadDone(Fusion.NetworkRunner runner) { }
        public void OnSceneLoadStart(Fusion.NetworkRunner runner) { }
        public void OnObjectExitAOI(Fusion.NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(Fusion.NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataReceived(Fusion.NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
        public void OnReliableDataProgress(Fusion.NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    }
}