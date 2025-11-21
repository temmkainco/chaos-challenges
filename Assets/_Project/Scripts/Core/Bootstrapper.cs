using Core;
using Networking;
using Platform;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace Core
{
    public class Bootstrapper : MonoBehaviour, IInitializable
    {
        [Inject] private GameSettings _gameSettings;
        [Inject] private IPlatformService _platformService;
        [Inject] private INetworkRunner _networkService;


        [Inject]
        public void Initialize()
        {
            if (_platformService != null)
                Debug.Log($"PlatformService loaded: {_platformService.GetType().Name}");

            if (_networkService != null)
                Debug.Log($"NetworkService loaded: {_networkService.GetType().Name}");
            else
                Debug.Log("NetworkService not bound, local play only.");

            _platformService.Init();

            LoadMainMenu();
        }

        private void LoadMainMenu()
        {
            SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
        }
    }
}
