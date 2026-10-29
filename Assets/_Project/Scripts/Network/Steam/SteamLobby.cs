using Cysharp.Threading.Tasks;
using Steamworks;
using System;
using UnityEngine;

namespace Networking
{
    /// <summary>
    /// Steam side of a Fusion session. Every player in a session is also a member of one Steam lobby,
    /// which is what makes overlay invites and "Join Game" from the friends list work. The lobby carries
    /// the Fusion session code; rich presence carries the lobby id for players launching the game from Steam.
    /// </summary>
    public class SteamLobby
    {
        public const string CONNECT_ARGUMENT = "+connect_lobby";
        private const string CODE_KEY = "FusionCode";
        private const string VERSION_KEY = "version";
        private const float TIMEOUT_SECONDS = 10f;

        public CSteamID Current { get; private set; } = CSteamID.Nil;
        public bool IsAvailable => SteamManager.Initialized;
        public bool IsInLobby => IsAvailable && Current != CSteamID.Nil;

        // Steam only keeps a weak link to pending call results, so they must stay referenced.
        private CallResult<LobbyCreated_t> _createResult;
        private CallResult<LobbyEnter_t> _enterResult;

        private int _presencePlayers = 1;
        private string _presenceStatus = "In the lobby";

        /// <summary>Host: creates the lobby before the session starts, so its id can go into the session properties. Returns false if Steam is unavailable or fails.</summary>
        public async UniTask<bool> CreateAsync(bool isPublic, int maxPlayers)
        {
            if (!IsAvailable)
                return false;

            Leave();

            var completion = new UniTaskCompletionSource<CSteamID>();
            _createResult ??= CallResult<LobbyCreated_t>.Create();
            _createResult.Set(
                SteamMatchmaking.CreateLobby(isPublic ? ELobbyType.k_ELobbyTypePublic : ELobbyType.k_ELobbyTypeFriendsOnly, maxPlayers),
                (result, ioFailure) => completion.TrySetResult(!ioFailure && result.m_eResult == EResult.k_EResultOK
                    ? new CSteamID(result.m_ulSteamIDLobby)
                    : CSteamID.Nil));

            var (timedOut, lobby) = await completion.Task.TimeoutWithoutException(TimeSpan.FromSeconds(TIMEOUT_SECONDS));
            if (timedOut || lobby == CSteamID.Nil)
            {
                Debug.LogWarning("[Steam] Lobby was not created; Steam invites will not work for this session.");
                return false;
            }

            Current = lobby;
            SteamMatchmaking.SetLobbyData(lobby, VERSION_KEY, Application.version);
            Debug.Log($"[Steam] Created lobby {lobby}");
            return true;
        }

        /// <summary>Host: the session is running; publish its code so invited friends can join it.</summary>
        public void SetSessionCode(string sessionCode)
        {
            if (!IsInLobby)
                return;

            SteamMatchmaking.SetLobbyData(Current, CODE_KEY, sessionCode);
            UpdatePresence(1, "In the lobby");
        }

        /// <summary>
        /// Enters a Steam lobby (from an invite or "Join Game") and returns the Fusion session code stored in it.
        /// On failure the lobby is left and <c>error</c> is a user-facing reason.
        /// </summary>
        public async UniTask<(string code, string error)> EnterAsync(CSteamID lobby)
        {
            if (!IsAvailable)
                return (null, "Steam is not running.");

            Leave();

            var (timedOut, response) = await EnterLobbyAsync(lobby);
            if (timedOut)
                return (null, "Steam did not respond. Try again.");

            if (response != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                return (null, DescribeEnterResponse(response));

            Current = lobby;
            string code = SteamMatchmaking.GetLobbyData(lobby, CODE_KEY);
            string version = SteamMatchmaking.GetLobbyData(lobby, VERSION_KEY);

            string error = null;
            if (string.IsNullOrEmpty(code))
                error = "That game is no longer available.";
            else if (!string.IsNullOrEmpty(version) && version != Application.version)
                error = $"Your friend is playing a different version ({version}). Update the game and try again.";

            if (error != null)
            {
                Leave();
                return (null, error);
            }

            ApplyPresence();
            return (code, null);
        }

        /// <summary>Client: joined a session by code or from the public list; becomes a lobby member so friends can join through them.</summary>
        public async UniTaskVoid EnterForSessionAsync(CSteamID lobby)
        {
            if (!IsAvailable || lobby == CSteamID.Nil || lobby == Current)
                return;

            Leave();

            var (timedOut, response) = await EnterLobbyAsync(lobby);
            if (timedOut || response != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Debug.LogWarning($"[Steam] Could not enter lobby {lobby} ({(timedOut ? "timeout" : response.ToString())}); invites from this player will not work.");
                return;
            }

            Current = lobby;
            ApplyPresence();
        }

        /// <summary>Host: stops new players from joining while a match runs, so invites fail early with a clear message.</summary>
        public void SetJoinable(bool joinable)
        {
            if (!IsInLobby || SteamMatchmaking.GetLobbyOwner(Current) != SteamUser.GetSteamID())
                return;

            SteamMatchmaking.SetLobbyJoinable(Current, joinable);
        }

        /// <summary>Rich presence for friends: status text, "Join Game" (connect) and party grouping.</summary>
        public void UpdatePresence(int playerCount, string status = null)
        {
            _presencePlayers = Mathf.Max(1, playerCount);
            if (status != null)
                _presenceStatus = status;

            ApplyPresence();
        }

        private void ApplyPresence()
        {
            if (!IsInLobby)
                return;

            SteamFriends.SetRichPresence("status", _presenceStatus);
            SteamFriends.SetRichPresence("connect", $"{CONNECT_ARGUMENT} {Current.m_SteamID}");
            SteamFriends.SetRichPresence("steam_player_group", Current.m_SteamID.ToString());
            SteamFriends.SetRichPresence("steam_player_group_size", _presencePlayers.ToString());
        }

        public bool OpenInviteOverlay()
        {
            if (!IsInLobby)
                return false;

            SteamFriends.ActivateGameOverlayInviteDialog(Current);
            return true;
        }

        public void Leave()
        {
            if (!IsAvailable)
                return;

            SteamFriends.ClearRichPresence();
            _presencePlayers = 1;
            _presenceStatus = "In the lobby";

            if (Current == CSteamID.Nil)
                return;

            SteamMatchmaking.LeaveLobby(Current);
            Debug.Log($"[Steam] Left lobby {Current}");
            Current = CSteamID.Nil;
        }

        /// <summary>Parses a connect string such as "+connect_lobby 109775241234567890" (rich presence join or launch arguments).</summary>
        public static bool TryParseConnect(string[] args, out CSteamID lobby)
        {
            lobby = CSteamID.Nil;
            if (args == null)
                return false;

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == CONNECT_ARGUMENT && ulong.TryParse(args[i + 1], out ulong id) && id != 0)
                {
                    lobby = new CSteamID(id);
                    return true;
                }
            }
            return false;
        }

        public static bool TryParseConnect(string connect, out CSteamID lobby) =>
            TryParseConnect(connect?.Split(' ', StringSplitOptions.RemoveEmptyEntries), out lobby);

        public static bool TryParseLobbyId(string value, out CSteamID lobby)
        {
            bool ok = ulong.TryParse(value, out ulong id) && id != 0;
            lobby = ok ? new CSteamID(id) : CSteamID.Nil;
            return ok;
        }

        private async UniTask<(bool timedOut, EChatRoomEnterResponse response)> EnterLobbyAsync(CSteamID lobby)
        {
            var completion = new UniTaskCompletionSource<EChatRoomEnterResponse>();
            _enterResult ??= CallResult<LobbyEnter_t>.Create();
            _enterResult.Set(
                SteamMatchmaking.JoinLobby(lobby),
                (result, ioFailure) => completion.TrySetResult(ioFailure
                    ? EChatRoomEnterResponse.k_EChatRoomEnterResponseError
                    : (EChatRoomEnterResponse)result.m_EChatRoomEnterResponse));

            return await completion.Task.TimeoutWithoutException(TimeSpan.FromSeconds(TIMEOUT_SECONDS));
        }

        private static string DescribeEnterResponse(EChatRoomEnterResponse response) => response switch
        {
            EChatRoomEnterResponse.k_EChatRoomEnterResponseDoesntExist => "That game no longer exists.",
            EChatRoomEnterResponse.k_EChatRoomEnterResponseFull => "That game is full.",
            EChatRoomEnterResponse.k_EChatRoomEnterResponseNotAllowed => "That game is in the middle of a match. Try again when it is back in the lobby.",
            EChatRoomEnterResponse.k_EChatRoomEnterResponseBanned => "You can't join that game.",
            EChatRoomEnterResponse.k_EChatRoomEnterResponseLimited => "Limited Steam accounts can't join lobbies.",
            _ => $"Could not join the game ({response}).",
        };
    }
}
