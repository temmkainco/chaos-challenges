namespace Networking
{
    public interface INetworkRunner
    {
        bool IsConnected { get; }
        void Init();
        void Shutdown();

        void Host(string roomName, string password = "");
        void Join(string roomName, string password = "");
        void Leave();
    }
}
