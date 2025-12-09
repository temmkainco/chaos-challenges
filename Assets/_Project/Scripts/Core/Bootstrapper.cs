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
            SetTargetFrameRateToMonitorHz();
            _platformService.Init();
            await _networkRunner.InitAsync();
            LoadMainMenu();
        }

        private void LoadMainMenu()
        {
            SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
        }

        private void SetTargetFrameRateToMonitorHz()
        {
            int refreshRate = (int)Screen.currentResolution.refreshRateRatio.value;
            Application.targetFrameRate = refreshRate;

            Debug.Log($"Target FPS set to monitor refresh rate: {refreshRate}");
        }
    }
}
