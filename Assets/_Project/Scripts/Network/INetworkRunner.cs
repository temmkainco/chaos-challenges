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

        /// <summary>Leave the current session and return to the main menu.</summary>
        void Leave();

        /// <summary>Stop networking entirely (application quit).</summary>
        void Shutdown();
    }
}
