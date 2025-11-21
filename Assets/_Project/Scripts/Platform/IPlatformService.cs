namespace Platform
{
    public interface IPlatformService
    {
        string GetPlayerName();
        void Init();
        void Shutdown();
        bool IsAvailable { get; }
    }
}
