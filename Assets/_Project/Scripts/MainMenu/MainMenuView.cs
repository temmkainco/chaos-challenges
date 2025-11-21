using Zenject;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuView : MonoBehaviour
{
    [SerializeField] private Button _hostButton;
    [SerializeField] private Button _joinButton;

    private MainMenuController _controller;

    [Inject]
    public void Construct(MainMenuController controller)
    {
        _controller = controller;
    }

    private void Awake()
    {
        _hostButton.onClick.AddListener(_controller.OnHostButtonClicked);
        _joinButton.onClick.AddListener(_controller.OnJoinButtonClicked);
    }

    private void OnDestroy()
    {
        _hostButton.onClick.RemoveListener(_controller.OnHostButtonClicked);
        _joinButton.onClick.RemoveListener(_controller.OnJoinButtonClicked);
    }
}
