using UnityEngine;
using Zenject;

public class LobbyInstaller : MonoInstaller, IInitializable
{
    [Inject] private LoadingPanel _loadingPanel;
    public override void InstallBindings()
    {
        Container.BindInterfacesTo<LobbyInstaller>().FromInstance(this).AsSingle();
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