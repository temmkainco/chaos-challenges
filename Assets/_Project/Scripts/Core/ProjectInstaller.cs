using Networking;
using Platform;
using UnityEditor;
using UnityEngine;
using Zenject;

namespace Core
{
    public class ProjectInstaller : MonoInstaller
    {
        [Header("Module toggles")]
        public bool UseFusion = true;
        public bool UseSteam = true;

        [Header("UI Prefabs")]
        [field: SerializeField] public LoadingPanel LoadingPanelPrefab { get; private set; }

        [Header("NetworkRunner Prefab (optional)")]
        [field: SerializeField] public FusionNetworkRunner NetworkRunnerPrefab { get; private set; }
        [field: SerializeField] public SteamInviteListener NetworkSteamInviteListenerPrefab { get; private set; }

        public override void InstallBindings()
        {
            Container.BindInterfacesTo<Bootstrapper>().FromComponentInHierarchy().AsSingle();

            Container.Bind<GameSettings>().AsSingle();

            if (SteamManager.Initialized && UseSteam)
            {
                Container.Bind<IPlatformService>().To<SteamPlatformService>().AsSingle();
                Container.Bind<SteamInviteListener>()
                    .FromComponentInNewPrefab(NetworkSteamInviteListenerPrefab)
                    .AsSingle()
                    .NonLazy();
            }
            else
            {
                Container.Bind<IPlatformService>().To<LocalPlatformService>().AsSingle();
            }

            if (UseFusion && NetworkRunnerPrefab != null)
            {
                Container.Bind<INetworkRunner>()
                         .FromComponentInNewPrefab(NetworkRunnerPrefab)
                         .AsSingle()
                         .NonLazy();
            }

            if (LoadingPanelPrefab != null)
            {
                Container.Bind<LoadingPanel>()
                    .FromComponentInNewPrefab(LoadingPanelPrefab)
                    .AsSingle()
                    .NonLazy();
            }

            if (Debug.isDebugBuild)
            {
                Debug.developerConsoleVisible = true;
            }

            Debug.Log("ProjectInstaller: Bindings complete (modular)");
        }
    }
}
