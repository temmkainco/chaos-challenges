using DG.Tweening;
using System.Collections;
using System.Linq;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

public class MinigameIntermissionUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private TMP_Text _readyCountText;
    [SerializeField] private Image _previewImage;
    [SerializeField] private Button _readyButton;
    [SerializeField] private CanvasGroup _descriptionCanvasGroup;
    [SerializeField] private CinemachineCamera _focusedCinemachine;

    [Header("Animation Settings")]
    [SerializeField] private float _fadeInDuration = 0.5f;
    [SerializeField] private float _cameraBlendDuration = 0.5f;

    private MinigameIntermissionController _controller;
    private int _ready;
    private int _total;
    private int _shownSeconds = -1;


    private void Start()
    {
        _controller = FindFirstObjectByType<MinigameIntermissionController>();

        _controller.OnDefinitionLoaded += PopulateUI;
        _controller.OnReadyCountUpdated += UpdateReadyCount;
        _descriptionCanvasGroup.alpha = 0f;
        _readyButton.onClick.AddListener(OnReadyClicked);

        Cursor.visible = true;

        StartCoroutine(InitializeWhenReady());
    }

    private IEnumerator InitializeWhenReady()
    {
        yield return new WaitUntil(() => _controller.TotalPlayers > 0);

        UpdateReadyCount(0, _controller.TotalPlayers);

        var def = _controller.CurrentDefinition;
        if (def != null)
        {
            PopulateUI(def);
        }
    }


    private void PopulateUI(MinigameDefinitionSO def)
    {
        _nameText.text = def.DisplayName.ToUpperInvariant();
        var text = def.Description;
        if (def.Rules != null && def.Rules.Length > 0)
            text += "\n\n<size=85%>" + string.Join("\n", System.Array.ConvertAll(def.Rules, r => "<color=#E8590C>-</color> " + r)) + "</size>";
        _descriptionText.text = text;

        if (def.PreviewImage != null)
        {
            _previewImage.sprite = def.PreviewImage;
            Thumbnail.Frame(_previewImage);
        }


        PlayIntroAnimations();
    }

    private void PlayIntroAnimations()
    {
        _descriptionCanvasGroup
            .DOFade(1f, _fadeInDuration)
            .SetEase(Ease.InOutSine);

        DOTween
            .To(
                () => _focusedCinemachine.Priority,
                x => _focusedCinemachine.Priority = x,
                10,
                _cameraBlendDuration
            )
            .SetEase(Ease.InOutSine);
    }

    private void Update()
    {
        if (_controller == null || _controller.Object == null || !_controller.Object.IsValid)
            return;

        int seconds = Mathf.CeilToInt(_controller.ReadyTimeRemaining);
        if (seconds != _shownSeconds)
        {
            _shownSeconds = seconds;
            RefreshReadyText();
        }
    }

    private void UpdateReadyCount(int ready, int total)
    {
        _ready = ready;
        _total = total;
        RefreshReadyText();
    }

    private void RefreshReadyText()
    {
        _readyCountText.text = _shownSeconds > 0
            ? $"{_ready} / {_total}\n<size=50%>Starting in {_shownSeconds}s</size>"
            : $"{_ready} / {_total}";
    }

    private void OnReadyClicked()
    {
        _readyButton.interactable = false;

        _controller.LocalPlayerReady();
    }

    private void OnDestroy()
    {
        if (_controller == null) return;
        _controller.OnDefinitionLoaded -= PopulateUI;
        _controller.OnReadyCountUpdated -= UpdateReadyCount;
    }
}
