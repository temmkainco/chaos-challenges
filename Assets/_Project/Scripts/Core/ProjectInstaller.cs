using Networking;
using Platform;
using UnityEngine;
using Zenject;

namespace Core
{
    public class ProjectInstaller : MonoInstaller
    {
        [Header("Module toggles")]
        public bool UseFusion = true;

        [Header("NetworkRunner Prefab (optional)")]
        public NetworkRunner NetworkRunnerPrefab;

        public override void InstallBindings()
        {
            Container.BindInterfacesTo<Bootstrapper>().FromComponentInHierarchy().AsSingle();

            Container.Bind<GameSettings>().AsSingle();

            if (SteamManager.Initialized)
            {
                Container.Bind<IPlatformService>().To<SteamPlatformService>().AsSingle();
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

            Debug.Log("ProjectInstaller: Bindings complete (modular)");
        }
    }
}
