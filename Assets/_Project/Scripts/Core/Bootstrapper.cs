using Core;
using Cysharp.Threading.Tasks;
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
        [Inject] private INetworkRunner _networkRunner;


        [Inject]
        public async void Initialize()
        {
            if (_platformService != null)
                Debug.Log($"PlatformService loaded: {_platformService.GetType().Name}");

            if (_networkRunner != null)
                Debug.Log($"NetworkService loaded: {_networkRunner.GetType().Name}");
            else
                Debug.Log("NetworkService not bound, local play only.");

            _platformService.Init();

            if (_networkRunner != null)
            {
                await _networkRunner.InitAsync();
            }

            LoadMainMenu();
        }

        private void LoadMainMenu()
        {
            SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
        }
    }
}
