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

        [Header("NetworkRunner Prefab (optional)")]
        public FusionNetworkRunner NetworkRunnerPrefab;
        public NetworkSteamInviteListener NetworkSteamInviteListenerPrefab;
        public override void InstallBindings()
        {
            Container.BindInterfacesTo<Bootstrapper>().FromComponentInHierarchy().AsSingle();

            Container.Bind<GameSettings>().AsSingle();

            if (SteamManager.Initialized && UseSteam)
            {
                Container.Bind<IPlatformService>().To<SteamPlatformService>().AsSingle();
                Container.Bind<NetworkSteamInviteListener>()
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

            if (Debug.isDebugBuild)
            {
                Debug.developerConsoleVisible = true;
            }

            Debug.Log("ProjectInstaller: Bindings complete (modular)");
        }
    }
}
