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
        [Inject] private IPlatformService _platformService;
        [Inject] private INetworkRunner _networkRunner;
        [InjectOptional] private SteamInviteListener _steamInviteListener;

        [Inject]
        public async void Initialize()
        {
            SetTargetFrameRateToMonitorHz();
            _platformService.Init();
            await _networkRunner.InitAsync();
            await SceneManager.LoadSceneAsync(GameScenes.MainMenu, LoadSceneMode.Single);

            // Started from a Steam invite or "Join Game" while the game was closed.
            _steamInviteListener?.HandleLaunchArguments();
        }

        private void SetTargetFrameRateToMonitorHz()
        {
            int refreshRate = (int)Screen.currentResolution.refreshRateRatio.value;
            Application.targetFrameRate = refreshRate;

            Debug.Log($"Target FPS set to monitor refresh rate: {refreshRate}");
        }
    }
}
