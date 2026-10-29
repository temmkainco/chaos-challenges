using UnityEngine;
using Zenject;

public class PistolRoundSceneInstaller : MonoInstaller
{
    [field: SerializeField] public MinigameController GameController { get; private set; }
    public override void InstallBindings()
    {
        Container.Bind<BasePlayerSpawner>().FromComponentInHierarchy().AsSingle();
        Container.Bind<MinigameController>().FromInstance(GameController).AsSingle();
    }
}
