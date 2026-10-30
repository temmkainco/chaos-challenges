using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Networking
{
    /// <summary>
    /// Persistent session service. Owns at most one NetworkRunner at a time, created from
    /// <see cref="_runnerPrefab"/> per session, because a runner cannot be restarted after Shutdown.
    /// Mirrors each session into a Steam lobby (when Steam is running) for invites and "Join Game".
    /// </summary>
    public class FusionNetworkRunner : MonoBehaviour, INetworkRunner, INetworkRunnerCallbacks
    {
        [SerializeField] private NetworkRunner _runnerPrefab;

        public string CurrentLobbyCode { get; private set; }
        public CSteamID CurrentSteamLobby => _steam.Current;
        public bool IsConnected => _runner != null && _runner.IsRunning && _runner.IsInSession;
        public IReadOnlyList<SessionInfo> Sessions => _sessions;
        public string LastError { get; private set; }
        public bool CanInviteFriends => IsConnected && _steam.IsInLobby;

        public event Action<IReadOnlyList<SessionInfo>> SessionsUpdated;
        public event Action<string> Disconnected;

        private readonly NetworkLobbyCodeGenerator _codeGenerator = new();
        private readonly SteamLobby _steam = new();
        private List<SessionInfo> _sessions = new();
        private NetworkRunner _runner;
        private bool _isLeaving;
        private string _disconnectReason;

        private const int CREATE_ATTEMPTS = 5;
        private const string CODE_PROPERTY = "code";
        private const string STEAM_LOBBY_PROPERTY = "steamLobby";

        private int PlayerCount => IsConnected ? _runner.ActivePlayers.Count() : 1;

        public async UniTask InitAsync()
        {
            await JoinSessionLobbyAsync();
        }

        public async Task<string> CreateLobbyAsync(SessionSettings settings)
        {
            LastError = null;

            // Steam stays optional: without it the session works, only invites and "Join Game" do not.
            await _steam.CreateAsync(settings.IsPublic, settings.MaxPlayers);

            for (int attempt = 0; attempt < CREATE_ATTEMPTS; attempt++)
            {
                string code = _codeGenerator.GenerateCode(_sessions);
                if (code == null)
                {
                    LastError = "Could not create a lobby code. Try again.";
                    break;
                }

                var properties = new Dictionary<string, SessionProperty> { { CODE_PROPERTY, code } };
                if (_steam.IsInLobby)
                    properties[STEAM_LOBBY_PROPERTY] = _steam.Current.m_SteamID.ToString();

                var runner = await GetFreshRunnerAsync();
                var result = await runner.StartGame(new StartGameArgs
                {
                    GameMode = GameMode.Host,
                    SessionName = code,
                    IsVisible = settings.IsPublic,
                    PlayerCount = settings.MaxPlayers,
                    Scene = SceneRef.FromIndex(GameScenes.LobbyBuildIndex),
                    SceneManager = runner.GetComponent<INetworkSceneManager>(),
                    ObjectProvider = PooledProvider(runner),
                    SessionProperties = properties,
                });

                if (result.Ok)
                {
                    CurrentLobbyCode = code;
                    _steam.SetSessionCode(code);
                    return code;
                }

                Debug.LogWarning($"[Session] Host attempt {attempt + 1} with code {code} failed: {result.ShutdownReason}");
                LastError = DescribeShutdown(result.ShutdownReason);
                if (result.ShutdownReason != ShutdownReason.GameIdAlreadyExists)
                    break;
            }

            _steam.Leave();
            return null;
        }

        public async Task<bool> JoinByCodeAsync(string code)
        {
            LastError = null;
            if (string.IsNullOrEmpty(code))
            {
                LastError = "Enter a lobby code.";
                return false;
            }

            code = code.ToUpperInvariant();
            if (IsConnected && CurrentLobbyCode == code)
                return true;

            return await JoinAsync(code);
        }

        public async Task<bool> JoinRandomPublicAsync()
        {
            LastError = null;
            return await JoinAsync(null);
        }

        /// <summary>
        /// Joins a friend's game from a Steam invite, "Join Game" or launch arguments. Leaves the current
        /// session first. On failure the player is in the main menu and <see cref="LastError"/> says why.
        /// </summary>
        public async UniTask<bool> JoinSteamLobbyAsync(CSteamID lobby)
        {
            LastError = null;
            if (IsConnected && lobby == _steam.Current)
                return true;

            if (IsConnected)
                await LeaveAsync();

            var (code, error) = await _steam.EnterAsync(lobby);
            if (code == null)
            {
                LastError = error;
                return false;
            }

            if (await JoinAsync(code))
                return true;

            _steam.Leave();
            return false;
        }

        public void SetSessionOpen(bool open)
        {
            if (_runner == null || !_runner.IsServer || !_runner.SessionInfo.IsValid)
                return;

            _runner.SessionInfo.IsOpen = open;
            _steam.SetJoinable(open);
        }

        public void SetActivity(string status)
        {
            _steam.UpdatePresence(PlayerCount, status);
        }

        public bool InviteFriends()
        {
            return IsConnected && _steam.OpenInviteOverlay();
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
            _steam.Leave();
            if (_runner != null)
                _runner.Shutdown();
        }

        /// <summary>Recycles bombs, keys and prizes instead of instantiating them every time.</summary>
        private static INetworkObjectProvider PooledProvider(NetworkRunner runner)
        {
            if (!runner.TryGetComponent<PooledObjectProvider>(out var provider))
                provider = runner.gameObject.AddComponent<PooledObjectProvider>();
            return provider;
        }

        private async UniTask<bool> JoinAsync(string sessionName)
        {
            NetworkRunner runner;
            StartGameResult result;
            int attempt = 0;
            while (true)
            {
                runner = await GetFreshRunnerAsync();
                result = await runner.StartGame(new StartGameArgs
                {
                    GameMode = GameMode.Client,
                    SessionName = sessionName,
                    SceneManager = runner.GetComponent<INetworkSceneManager>(),
                    ObjectProvider = PooledProvider(runner),
                    EnableClientSessionCreation = false,
                });

                // A brand-new lobby can take a moment to become visible to the matchmaking server,
                // so a code typed right after the host created it is retried a couple of times.
                if (result.Ok || sessionName == null || result.ShutdownReason != ShutdownReason.GameNotFound || ++attempt > 2)
                    break;
                Debug.Log($"[Session] Lobby '{sessionName}' not found yet, retrying ({attempt}/2)");
                await UniTask.Delay(1500);
            }

            if (!result.Ok)
            {
                Debug.LogWarning($"[Session] Join '{sessionName ?? "random"}' failed: {result.ShutdownReason}");
                LastError = sessionName == null && result.ShutdownReason == ShutdownReason.GameNotFound
                    ? "No public games right now. Host one!"
                    : DescribeShutdown(result.ShutdownReason);
                return false;
            }

            var properties = runner.SessionInfo.Properties;
            CurrentLobbyCode = properties.TryGetValue(CODE_PROPERTY, out var code)
                ? (string)code
                : runner.SessionInfo.Name;

            // Joined by code or from the public list: also enter the host's Steam lobby,
            // so this player's friends get "Join Game" too.
            if (properties.TryGetValue(STEAM_LOBBY_PROPERTY, out var steamLobby)
                && SteamLobby.TryParseLobbyId((string)steamLobby, out var lobby))
            {
                _steam.EnterForSessionAsync(lobby).Forget();
            }
            return true;
        }

        private async UniTask LeaveAsync()
        {
            _isLeaving = true;
            _steam.Leave();
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

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var data = new NetworkInputData();

            var localInput = PlayerInput.Local;
            if (localInput != null)
            {
                data.Direction = new Vector3(localInput.Move.x, 0, localInput.Move.y);
                data.CameraRotation = localInput.CameraRotation;
                data.LookDirection = localInput.LookDirection;
                data.Buttons.Set(InputButtons.Jump, localInput.IsJumpPressed);
                data.Buttons.Set(InputButtons.Sprint, localInput.IsSprintPressed);
                data.Buttons.Set(InputButtons.Interact, localInput.IsInteractPressed);
                data.Buttons.Set(InputButtons.Throw, localInput.IsThrowPressed);
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
            _steam.Leave();
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
            ShutdownReason.GameNotFound => "Lobby not found. Check the code and try again.",
            ShutdownReason.GameIsFull => "The lobby is full.",
            ShutdownReason.GameClosed => "That game is in the middle of a match. Try again when it is back in the lobby.",
            ShutdownReason.ConnectionTimeout => "Connection timed out.",
            ShutdownReason.ConnectionRefused => "Connection refused.",
            ShutdownReason.PhotonCloudTimeout => "Could not reach the game servers. Check your connection.",
            _ => $"Disconnected ({reason}).",
        };

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) => _steam.UpdatePresence(PlayerCount);
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) => _steam.UpdatePresence(PlayerCount);
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
