using Cysharp.Threading.Tasks;
using Fusion;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Networking
{
    public struct SessionSettings
    {
        public const int MIN_PLAYERS = 2;
        public const int MAX_PLAYERS = 6;

        public bool IsPublic;
        public int MaxPlayers;

        public static SessionSettings Default(bool isPublic) => new()
        {
            IsPublic = isPublic,
            MaxPlayers = MAX_PLAYERS,
        };
    }

    public interface INetworkRunner
    {
        bool IsConnected { get; }
        string CurrentLobbyCode { get; }
        IReadOnlyList<SessionInfo> Sessions { get; }

        /// <summary>User-facing reason of the last failed host or join attempt, or null.</summary>
        string LastError { get; }

        /// <summary>The local player can open the platform's invite dialog for the current session.</summary>
        bool CanInviteFriends { get; }

        /// <summary>User-facing reason of the last unexpected disconnect, or null. Cleared when read via <see cref="ConsumeDisconnectReason"/>.</summary>
        string ConsumeDisconnectReason();

        /// <summary>Visible (public) sessions changed.</summary>
        event Action<IReadOnlyList<SessionInfo>> SessionsUpdated;

        /// <summary>The session ended without the local player choosing to leave. Argument is a user-facing reason.</summary>
        event Action<string> Disconnected;

        UniTask InitAsync();
        Task<string> CreateLobbyAsync(SessionSettings settings);
        Task<bool> JoinByCodeAsync(string code);
        Task<bool> JoinRandomPublicAsync();

        /// <summary>Host only. Open or close the session (and its platform lobby) to new players, e.g. while a match runs.</summary>
        void SetSessionOpen(bool open);

        /// <summary>Short status shown to friends, e.g. "In the lobby" or "Playing Mines".</summary>
        void SetActivity(string status);

        /// <summary>Opens the platform invite dialog. Returns false if inviting is not available.</summary>
        bool InviteFriends();

        /// <summary>Leave the current session and return to the main menu.</summary>
        void Leave();

        /// <summary>Stop networking entirely (application quit).</summary>
        void Shutdown();
    }
}
