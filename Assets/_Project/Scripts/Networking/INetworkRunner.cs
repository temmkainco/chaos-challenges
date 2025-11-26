using Cysharp.Threading.Tasks;
using Fusion;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Networking
{
    public interface INetworkRunner
    {
        bool IsConnected { get; }
        string CurrentLobbyCode { get; }
        void Shutdown();

        Task<string> CreateLobbyAsync(int maxPlayers, bool isPublic);
        Task<bool> JoinByCodeAsync(string code);
        Task<bool> JoinRandomPublicAsync();
        UniTask Init();
        void Leave();
    }
}
