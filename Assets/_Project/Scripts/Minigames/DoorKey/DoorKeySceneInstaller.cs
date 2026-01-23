using UnityEngine;
using Zenject;

public class DoorKeySceneInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Container.Bind<BasePlayerSpawner>().FromComponentInHierarchy().AsSingle();
    }
}