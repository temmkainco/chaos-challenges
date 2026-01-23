using Fusion;
using UnityEngine;
using Zenject;

public class LobbyInstaller : MonoInstaller, IInitializable
{
    [Header("Game")]
    [field: SerializeField] public NetworkPrefabRef NetworkGameManagerPrefab { get; private set; }
    [field: SerializeField] public MinigameSceneDatabaseSO MinigameSceneDatabase { get; private set; }

    [Inject] private LoadingPanel _loadingPanel;
    public override void InstallBindings()
    {
        Container.BindInterfacesTo<LobbyInstaller>().FromInstance(this).AsSingle();
        Container.Bind<LobbyPlayerSpawner>().FromComponentInHierarchy().AsSingle();
        Container.Bind<LobbyManager>().FromComponentInHierarchy().AsSingle();
        Container.Bind<NetworkPrefabRef>().WithId("NetworkGameManager").FromInstance(NetworkGameManagerPrefab).AsSingle();
        Container.Bind<MinigameSceneDatabaseSO>().FromInstance(MinigameSceneDatabase).AsSingle();
    }

    public void Initialize()
    {
        Cursor.visible = false;
        if (_loadingPanel != null)
        {
            _loadingPanel.Close();
        }
    }
}