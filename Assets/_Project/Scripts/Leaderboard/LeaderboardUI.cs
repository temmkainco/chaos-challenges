using DG.Tweening;
using Steamworks;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Transform _entriesContainer;
    [SerializeField] private LeaderboardEntryUI _entryPrefab;
    [SerializeField] private TMP_Text _countdownText;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private CinemachineCamera _focusedCamera;

    [Header("Animation")]
    [SerializeField] private float _fadeInDuration = 0.4f;
    [SerializeField] private float _fadeOutDuration = 0.4f;
    [SerializeField, Tooltip("Final reveal: delay between places, revealed from last to first")]
    private float _finalRevealStagger = 1.2f;

    private LeaderboardController _controller;
    private float _timer;
    private bool _isCounting;
    private readonly List<LeaderboardEntryUI> _spawnedEntries = new();
    private bool _isFinal;
    private void Start()
    {
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;

        _controller = FindFirstObjectByType<LeaderboardController>();
        if (_controller != null)
        {
            Subscribe();
        }
        else
        {
            StartCoroutine(InitializeWhenReady());
        }
    }
    private IEnumerator InitializeWhenReady()
    {
        yield return new WaitUntil(() => FindFirstObjectByType<LeaderboardController>() != null);
        _controller = FindFirstObjectByType<LeaderboardController>();
        Subscribe();
    }

    private void Subscribe()
    {
        _controller.OnLeaderboardShown += Show;
        _controller.OnLeaderboardHidden += Hide;
    }

    private void Update()
    {
        if (!_isCounting) return;

        _timer = Mathf.Max(0f, _timer - Time.deltaTime);
        _countdownText.text = _isFinal
            ? $"Return to lobby in {Mathf.CeilToInt(_timer)}..."
            : $"Next round in {Mathf.CeilToInt(_timer)}...";
    }

    private void Show(List<LeaderboardEntry> entries, bool isFinal)
    {
        foreach (var e in _spawnedEntries)
            Destroy(e.gameObject);
        _spawnedEntries.Clear();

        _isFinal = isFinal;
        Sfx.Play(isFinal ? Sound.Fanfare : Sound.RoundOver, 1f, 0f);

        // Mid-match the results are secret, so a short banner over the arena is all a round end needs;
        // the full board with cards, backdrop and camera move is saved for the final reveal.
        var backdrop = _canvasGroup.transform.Find("Backdrop");
        if (backdrop != null)
            backdrop.gameObject.SetActive(isFinal);
        if (_countdownText != null)
            _countdownText.gameObject.SetActive(isFinal);
        if (_titleText != null)
        {
            _titleText.text = isFinal ? "FINAL RESULTS!" : "ROUND OVER!";
            _titleText.rectTransform.anchoredPosition = new Vector2(0f, isFinal ? 380f : 200f);
        }

        // Only real contestants get a card. The final reveal builds suspense from last place to the winner;
        // a round podium (top three of this round) pops in quickly under the banner.
        entries = entries.Where(e => !e.IsEmpty).ToList();
        for (int i = 0; i < entries.Count; i++)
        {
            var ui = Instantiate(_entryPrefab, _entriesContainer);
            ui.Populate(entries[i]);
            ui.AnimateIn(isFinal ? (entries.Count - 1 - i) * _finalRevealStagger : 0.3f + i * 0.15f);
            _spawnedEntries.Add(ui);
        }
        if (isFinal)
        {
            _focusedCamera.Priority = 20;
            _canvasGroup.blocksRaycasts = true;
        }

        _canvasGroup.DOFade(1f, _fadeInDuration).SetEase(Ease.OutSine).SetLink(_canvasGroup.gameObject);
        if (!isFinal && _titleText != null)
        {
            var title = _titleText.transform;
            title.DOKill(true);
            title.localScale = Vector3.zero;
            title.DOScale(1f, 0.35f).SetEase(Ease.OutBack);
        }

        _timer = _controller.RemainingTime;
        _isCounting = true;
    }

    private void Hide()
    {
        _isCounting = false;
        _canvasGroup
            .DOFade(0f, _fadeOutDuration)
            .SetEase(Ease.InSine)
            .SetLink(_canvasGroup.gameObject)
            .OnComplete(() => _canvasGroup.blocksRaycasts = false);
    }

    private void OnDestroy()
    {
        if (_controller == null) return;
        _controller.OnLeaderboardShown -= Show;
        _controller.OnLeaderboardHidden -= Hide;
    }
}