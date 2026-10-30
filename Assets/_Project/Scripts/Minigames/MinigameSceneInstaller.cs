using UnityEngine;
using Zenject;

/// <summary>Scene bindings shared by minigames that need nothing beyond the spawner and their controller.</summary>
public class MinigameSceneInstaller : MonoInstaller
{
    [field: SerializeField] public MinigameController GameController { get; private set; }

    public override void InstallBindings()
    {
        Container.Bind<BasePlayerSpawner>().FromComponentInHierarchy().AsSingle();
        Container.Bind<MinigameController>().FromInstance(GameController).AsSingle();
    }
}
