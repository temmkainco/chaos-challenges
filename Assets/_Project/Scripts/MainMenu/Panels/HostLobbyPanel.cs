using Networking;
using Platform;
using System;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class HostLobbyPanel : PanelBase
{
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _hostButton;
    [SerializeField] private Toggle _isPublicToggle;
    [SerializeField] private Button _fewerPlayers;
    [SerializeField] private Button _morePlayers;
    [SerializeField] private TMPro.TMP_Text _maxPlayersText;
    [Inject] private LoadingPanel _loadingPanel;
    [Inject] private MessagePanel _messagePanel;

    private MainMenuController _controller;
    private int _maxPlayers = SessionSettings.MAX_PLAYERS;
    private bool _built;
    private TMPro.TMP_Text _roundsText, _lengthText, _chaosText, _shoveText;

    [Inject]
    public void Construct(MainMenuController controller)
    {
        _controller = controller;
    }

    /// <summary>
    /// The panel grows a stepper row per match option (cloned from the max-players stepper): rounds, round length,
    /// chaos and shoving, under the public toggle and max players.
    /// </summary>
    private void BuildOptions()
    {
        if (_built || _maxPlayersText == null || _fewerPlayers == null) return;
        _built = true;
        var card = (RectTransform)_maxPlayersText.transform.parent;
        card.sizeDelta = new Vector2(980f, 900f);
        void Move(Component c, float y) { var rt = (RectTransform)c.transform; rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y); }
        foreach (Transform child in card)
        {
            if (child.name == "PanelTitle") Move(child, 380f);
            if (child.name == "Close_Button") ((RectTransform)child).anchoredPosition = new Vector2(450f, 410f);
        }
        Move(_isPublicToggle, 290f);
        Move(_maxPlayersText, 200f); Move(_fewerPlayers, 200f); Move(_morePlayers, 200f);
        Move(_hostButton, -360f);

        TMPro.TMP_Text Row(float y, System.Action previous, System.Action next)
        {
            var text = Instantiate(_maxPlayersText, card);
            var left = Instantiate(_fewerPlayers, card);
            var right = Instantiate(_morePlayers, card);
            Move(text, y); Move(left, y); Move(right, y);
            ((RectTransform)text.transform).sizeDelta = new Vector2(420f, 80f);
            left.onClick.AddListener(() => { previous(); ShowOptions(); });
            right.onClick.AddListener(() => { next(); ShowOptions(); });
            return text;
        }
        _roundsText = Row(110f, () => MatchSettings.Rounds--, () => MatchSettings.Rounds++);
        _lengthText = Row(20f, () => MatchSettings.RoundLength = (MatchSettings.Length)(((int)MatchSettings.RoundLength + 2) % 3),
                               () => MatchSettings.RoundLength = (MatchSettings.Length)(((int)MatchSettings.RoundLength + 1) % 3));
        _chaosText = Row(-70f, () => MatchSettings.Chaos = (ChaosLevel)(((int)MatchSettings.Chaos + 2) % 3),
                               () => MatchSettings.Chaos = (ChaosLevel)(((int)MatchSettings.Chaos + 1) % 3));
        _shoveText = Row(-160f, () => MatchSettings.Shoving = !MatchSettings.Shoving, () => MatchSettings.Shoving = !MatchSettings.Shoving);
        foreach (var t in new[] { _roundsText, _lengthText, _chaosText, _shoveText }) t.fontSize = 38f;
        _maxPlayersText.fontSize = 38f;
    }

    private void ShowOptions()
    {
        if (_roundsText == null) return;
        _roundsText.text = $"Rounds: {MatchSettings.Rounds}";
        _lengthText.text = $"Round length: {MatchSettings.RoundLength}";
        _chaosText.text = MatchSettings.Chaos == ChaosLevel.Off ? "Chaos: Off" : $"Chaos: {MatchSettings.Chaos}";
        _shoveText.text = MatchSettings.Shoving ? "Shoving: On" : "Shoving: Off";
    }

    private void OnEnable()
    {
        BuildOptions();
        ShowOptions();
        _closeButton.onClick.AddListener(Close);
        _hostButton.onClick.AddListener(OnHostButtonClicked);
        if (_fewerPlayers != null) _fewerPlayers.onClick.AddListener(Fewer);
        if (_morePlayers != null) _morePlayers.onClick.AddListener(More);
        ShowMaxPlayers();
    }

    private void Fewer() { _maxPlayers = Mathf.Max(SessionSettings.MIN_PLAYERS, _maxPlayers - 1); ShowMaxPlayers(); }
    private void More() { _maxPlayers = Mathf.Min(SessionSettings.MAX_PLAYERS, _maxPlayers + 1); ShowMaxPlayers(); }

    private void ShowMaxPlayers()
    {
        if (_maxPlayersText != null) _maxPlayersText.text = $"Max players: {_maxPlayers}";
    }

    private void OnDisable()
    {
        _closeButton.onClick.RemoveListener(Close);
        _hostButton.onClick.RemoveListener(OnHostButtonClicked);
        if (_fewerPlayers != null) _fewerPlayers.onClick.RemoveListener(Fewer);
        if (_morePlayers != null) _morePlayers.onClick.RemoveListener(More);
    }

    private async void OnHostButtonClicked()
    {
        bool isPublic = _isPublicToggle.isOn;
        Close();

        _loadingPanel.Open();

        try
        {
            string code = await _controller.OnHostLobbyButtonClicked(isPublic, _maxPlayers);

            if (string.IsNullOrEmpty(code))
            {
                Debug.LogWarning("Failed to host lobby.");
                _messagePanel.Show(_controller.LastError ?? "Could not create a lobby.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error hosting lobby: {e.Message}");
            _messagePanel.Show("Could not create a lobby. Check your connection.");
        }
        finally
        {
            _loadingPanel.Close();
        }
    }
}
