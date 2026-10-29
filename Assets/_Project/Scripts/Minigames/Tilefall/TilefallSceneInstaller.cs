using UnityEngine;
using Zenject;

public class TilefallSceneInstaller : MonoInstaller
{
    [field: SerializeField] public MinigameController GameController { get; private set; }
    public override void InstallBindings()
    {
        Container.Bind<BasePlayerSpawner>().FromComponentInHierarchy().AsSingle();
        Container.Bind<TilefallMinigameController>().FromComponentInHierarchy().AsSingle();
        Container.Bind<MinigameController>().FromInstance(GameController).AsSingle();
        Container.Bind<TileCourse>().FromComponentInHierarchy().AsSingle();
    }
}
