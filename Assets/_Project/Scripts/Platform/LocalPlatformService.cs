using UnityEngine;

namespace Platform
{
    public class LocalPlatformService : IPlatformService
    {
        private string _localPlayerName;

        public bool IsAvailable => _isInitialized;

        private bool _isInitialized = false;

        public void Init()
        {
            if(!IsAvailable) return;

            if (PlayerPrefs.HasKey("LocalPlayerName"))
            {
                _localPlayerName = PlayerPrefs.GetString("LocalPlayerName");
            }
            else
            {
                _localPlayerName = $"Player_{Random.Range(1000, 9999)}";
                PlayerPrefs.SetString("LocalPlayerName", _localPlayerName);
            }

            _isInitialized = true;
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
