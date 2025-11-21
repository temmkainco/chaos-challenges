using Zenject;
using UnityEngine;
using UnityEngine.UI;
using Platform;
using TMPro;
using UnityEngine.SocialPlatforms;

public class MainMenuView : MonoBehaviour
{
    [SerializeField] private TMP_InputField _playerNameInput;
    [SerializeField] private Button _hostButton;
    [SerializeField] private Button _joinButton;

    private MainMenuController _controller;
    private IPlatformService _platformService;

    [Inject]
    public void Construct(MainMenuController controller, IPlatformService platformService)
    {
        _controller = controller;
        _platformService = platformService;
    }

    private void OnEnable()
    {
        _hostButton.onClick.AddListener(_controller.OnHostButtonClicked);
        _joinButton.onClick.AddListener(_controller.OnJoinButtonClicked);

        _platformService.Init();
        _playerNameInput.interactable = _platformService is LocalPlatformService;
        _playerNameInput.text = _platformService.GetPlayerName();
        _playerNameInput.onEndEdit.AddListener(OnPlayerNameInputFieldChanged);
    }
    private void OnPlayerNameInputFieldChanged(string newName)
    {
        if (_platformService is LocalPlatformService local)
        {
            PlayerPrefs.SetString("LocalPlayerName", newName);
        }
    }

    private void OnDestroy()
    {
        _playerNameInput.onEndEdit.RemoveListener(OnPlayerNameInputFieldChanged);

        _hostButton.onClick.RemoveListener(_controller.OnHostButtonClicked);
        _joinButton.onClick.RemoveListener(_controller.OnJoinButtonClicked);
    }
}
