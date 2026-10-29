using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Networking
{
    /// <summary>
    /// Persistent session service. Owns at most one NetworkRunner at a time, created from
    /// <see cref="_runnerPrefab"/> per session, because a runner cannot be restarted after Shutdown.
    /// </summary>
    public class FusionNetworkRunner : MonoBehaviour, INetworkRunner, INetworkRunnerCallbacks
    {
        [SerializeField] private NetworkRunner _runnerPrefab;

        public string CurrentLobbyCode { get; private set; }
        public CSteamID CurrentSteamLobby { get; private set; }
        public bool IsConnected => _runner != null && _runner.IsRunning && _runner.IsInSession;
        public IReadOnlyList<SessionInfo> Sessions => _sessions;

        public event Action<IReadOnlyList<SessionInfo>> SessionsUpdated;
        public event Action<string> Disconnected;

        private readonly NetworkLobbyCodeGenerator _codeGenerator = new();
        private List<SessionInfo> _sessions = new();
        private NetworkRunner _runner;
        private bool _isLeaving;
        private string _disconnectReason;

        private UniTaskCompletionSource<bool> _steamLobbyReady;
        private Callback<LobbyCreated_t> _steamLobbyCreatedCallback;

        private const int CREATE_ATTEMPTS = 5;
        private const float STEAM_LOBBY_TIMEOUT_SECONDS = 10f;

        public async UniTask InitAsync()
        {
            if (SteamManager.Initialized && _steamLobbyCreatedCallback == null)
                _steamLobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnSteamLobbyCreated);

            await JoinSessionLobbyAsync();
        }

        public async Task<string> CreateLobbyAsync(SessionSettings settings)
        {
            for (int attempt = 0; attempt < CREATE_ATTEMPTS; attempt++)
            {
                string code = _codeGenerator.GenerateCode(_sessions);
                if (code == null)
                    return null;

                var runner = await GetFreshRunnerAsync();
                var result = await runner.StartGame(new StartGameArgs
                {
                    GameMode = GameMode.Host,
                    SessionName = code,
                    IsVisible = settings.IsPublic,
                    PlayerCount = settings.MaxPlayers,
                    Scene = SceneRef.FromIndex(GameScenes.LobbyBuildIndex),
                    SceneManager = runner.GetComponent<INetworkSceneManager>(),
                    SessionProperties = new Dictionary<string, SessionProperty> { { "code", code } },
                });

                if (result.Ok)
                {
                    CurrentLobbyCode = code;
                    await CreateSteamLobbyAsync(settings);
                    return code;
                }

                Debug.LogWarning($"[Session] Host attempt {attempt + 1} with code {code} failed: {result.ShutdownReason}");
                if (result.ShutdownReason != ShutdownReason.GameIdAlreadyExists)
                    return null;
            }

            return null;
        }

        public async Task<bool> JoinByCodeAsync(string code)
        {
            if (string.IsNullOrEmpty(code))
                return false;

            code = code.ToUpperInvariant();
            if (IsConnected && CurrentLobbyCode == code)
                return true;

            return await JoinAsync(code);
        }

        public async Task<bool> JoinRandomPublicAsync()
        {
            return await JoinAsync(null);
        }

        public string ConsumeDisconnectReason()
        {
            string reason = _disconnectReason;
            _disconnectReason = null;
            return reason;
        }

        public void Leave()
        {
            LeaveAsync().Forget();
        }

        public void Shutdown()
        {
            _isLeaving = true;
            LeaveSteamLobby();
            if (_runner != null)
                _runner.Shutdown();
        }

        private async UniTask<bool> JoinAsync(string sessionName)
        {
            var runner = await GetFreshRunnerAsync();
            var result = await runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Client,
                SessionName = sessionName,
                SceneManager = runner.GetComponent<INetworkSceneManager>(),
                EnableClientSessionCreation = false,
            });

            if (!result.Ok)
            {
                Debug.LogWarning($"[Session] Join '{sessionName ?? "random"}' failed: {result.ShutdownReason}");
                return false;
            }

            CurrentLobbyCode = runner.SessionInfo.Properties.TryGetValue("code", out var code)
                ? (string)code
                : runner.SessionInfo.Name;
            return true;
        }

        private async UniTask LeaveAsync()
        {
            _isLeaving = true;
            LeaveSteamLobby();
            CurrentLobbyCode = null;

            if (_runner != null)
                await _runner.Shutdown();

            await SceneManager.LoadSceneAsync(GameScenes.MainMenu);
            await JoinSessionLobbyAsync();
        }

        /// <summary>
        /// Returns a runner that has not started a game yet. Reuses the lobby-browsing runner
        /// when possible; otherwise shuts down the old one and spawns a new one from the prefab.
        /// </summary>
        private async UniTask<NetworkRunner> GetFreshRunnerAsync()
        {
            if (_runner != null && !_runner.IsShutdown && _runner.IsCloudReady && !_runner.IsInSession)
                return _runner;

            if (_runner != null)
            {
                _isLeaving = true;
                await _runner.Shutdown();
            }

            _isLeaving = false;
            _runner = Instantiate(_runnerPrefab);
            _runner.name = "SessionRunner";
            DontDestroyOnLoad(_runner.gameObject);
            _runner.ProvideInput = true;
            _runner.AddCallbacks(this);
            return _runner;
        }

        private async UniTask JoinSessionLobbyAsync()
        {
            var runner = await GetFreshRunnerAsync();
            var result = await runner.JoinSessionLobby(SessionLobby.ClientServer);
            if (!result.Ok)
                Debug.LogWarning($"[Session] Could not join session lobby: {result.ShutdownReason}");
        }

        private async UniTask CreateSteamLobbyAsync(SessionSettings settings)
        {
            if (!SteamManager.Initialized)
                return;

            _steamLobbyReady = new UniTaskCompletionSource<bool>();
            SteamMatchmaking.CreateLobby(
                settings.IsPublic ? ELobbyType.k_ELobbyTypePublic : ELobbyType.k_ELobbyTypeFriendsOnly,
                settings.MaxPlayers);

            var (completed, ok) = await _steamLobbyReady.Task
                .TimeoutWithoutException(TimeSpan.FromSeconds(STEAM_LOBBY_TIMEOUT_SECONDS));

            if (completed || !ok)
                Debug.LogWarning("[Session] Steam lobby was not created; Steam invites will not work for this session.");
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

        private void LeaveSteamLobby()
        {
            if (!SteamManager.Initialized || CurrentSteamLobby == CSteamID.Nil)
                return;

            SteamMatchmaking.LeaveLobby(CurrentSteamLobby);
            CurrentSteamLobby = CSteamID.Nil;
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var data = new NetworkInputData();

            var localInput = PlayerInput.Local;
            if (localInput != null)
            {
                data.Direction = new Vector3(localInput.Move.x, 0, localInput.Move.y);
                data.CameraRotation = localInput.CameraRotation;
                data.Buttons.Set(InputButtons.Jump, localInput.IsJumpPressed);
                data.Buttons.Set(InputButtons.Interact, localInput.IsInteractPressed);
            }

            input.Set(data);
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            _sessions = sessionList;
            SessionsUpdated?.Invoke(_sessions);
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            // Fusion can report the same shutdown more than once; only the current runner counts.
            if (runner != _runner)
                return;

            _runner = null;

            if (_isLeaving || shutdownReason == ShutdownReason.Ok)
                return;

            // Unexpected: host left, connection lost, kicked...
            Debug.LogWarning($"[Session] Session ended unexpectedly: {shutdownReason}");
            CurrentLobbyCode = null;
            LeaveSteamLobby();
            _disconnectReason = DescribeShutdown(shutdownReason);
            Disconnected?.Invoke(_disconnectReason);
            ReturnToMenuAsync().Forget();
        }

        private async UniTaskVoid ReturnToMenuAsync()
        {
            await SceneManager.LoadSceneAsync(GameScenes.MainMenu);
            await JoinSessionLobbyAsync();
        }

        private static string DescribeShutdown(ShutdownReason reason) => reason switch
        {
            ShutdownReason.DisconnectedByPluginLogic => "The host closed the lobby.",
            ShutdownReason.GameNotFound => "Lobby not found.",
            ShutdownReason.GameIsFull => "The lobby is full.",
            ShutdownReason.GameClosed => "The lobby is closed.",
            ShutdownReason.ConnectionTimeout => "Connection timed out.",
            ShutdownReason.ConnectionRefused => "Connection refused.",
            _ => $"Disconnected ({reason}).",
        };

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    }
}
