using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MinigameIntermissionUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private TMP_Text _readyCountText;
    [SerializeField] private Image _previewImage;
    [SerializeField] private Button _readyButton;

    private MinigameIntermissionController _controller;

    private void Start()
    {
        _controller = FindFirstObjectByType<MinigameIntermissionController>();

        _controller.OnDefinitionLoaded += PopulateUI;
        _controller.OnReadyCountUpdated += UpdateReadyCount;
        _controller.OnAllReady += ShowAllReady;

        _readyButton.onClick.AddListener(OnReadyClicked);

        StartCoroutine(InitializeWhenReady());
    }

    private IEnumerator InitializeWhenReady()
    {
        // Wait until ExpectedPlayerCount is replicated
        yield return new WaitUntil(() => _controller.TotalPlayers > 0);

        UpdateReadyCount(0, _controller.TotalPlayers);

        var def = _controller.CurrentDefinition;
        if (def != null)
            PopulateUI(def);
    }

    private void PopulateUI(MinigameDefinitionSO def)
    {
        _nameText.text = def.DisplayName;
        _descriptionText.text = def.Description;

        if (def.PreviewImage != null)
            _previewImage.sprite = def.PreviewImage;



        //_rulesText.text = def.Rules != null
        //    ? string.Join("\n• ", def.Rules.Prepend("• "))
        //    : string.Empty;
    }

    private void UpdateReadyCount(int ready, int total)
    {
        _readyCountText.text = $"{ready} / {total}";
    }

    private void OnReadyClicked()
    {
        _readyButton.interactable = false;

        _controller.LocalPlayerReady();
    }

    private void ShowAllReady()
    {
    }

    private void OnDestroy()
    {
        if (_controller == null) return;
        _controller.OnDefinitionLoaded -= PopulateUI;
        _controller.OnReadyCountUpdated -= UpdateReadyCount;
        _controller.OnAllReady -= ShowAllReady;
    }
}
