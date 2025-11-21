using UnityEngine;

namespace Platform
{
    public class LocalPlatformService : IPlatformService
    {
        private string _localPlayerName;

        public bool IsAvailable => true;

        public void Init()
        {
            if (PlayerPrefs.HasKey("LocalPlayerName"))
            {
                _localPlayerName = PlayerPrefs.GetString("LocalPlayerName");
            }
            else
            {
                _localPlayerName = $"Player_{Random.Range(1000, 9999)}";
                PlayerPrefs.SetString("LocalPlayerName", _localPlayerName);
            }

            Debug.Log($"LocalPlatformService initialized: {_localPlayerName}");
        }

        public void Shutdown()
        {
            Debug.Log("LocalPlatformService shutdown");
        }

        public string GetPlayerName()
        {
            return _localPlayerName;
        }
    }
}
