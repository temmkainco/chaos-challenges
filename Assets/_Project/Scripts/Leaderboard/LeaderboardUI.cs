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
    [SerializeField] private float _entryStaggerDelay = 0.1f;

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

        _timer -= Time.deltaTime;
        _countdownText.text = _isFinal
            ? $"Return to lobby in {Mathf.CeilToInt(_timer)}..."
            : $"Next in {Mathf.CeilToInt(_timer)}...";
    }

    private void Show(List<LeaderboardEntry> entries)
    {
        foreach (var e in _spawnedEntries)
            Destroy(e.gameObject);
        _spawnedEntries.Clear();

        _isFinal = entries.Any(e => e.IsOverallWinner);

        if (_titleText != null)
            _titleText.text = _isFinal ? "Final Results!" : "Round Results";

        for (int i = 0; i < entries.Count; i++)
        {
            var ui = Instantiate(_entryPrefab, _entriesContainer);
            ui.Populate(entries[i]);

            float delay = entries[i].IsEmpty ? 0f : i * _entryStaggerDelay;
            ui.AnimateIn(delay);
            _spawnedEntries.Add(ui);
        }

        _focusedCamera.Priority = 20;
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.DOFade(1f, _fadeInDuration).SetEase(Ease.OutSine);

        _timer = _isFinal ? 12f : 7f;
        _isCounting = true;
    }

    private void Hide()
    {
        _isCounting = false;
        _canvasGroup
            .DOFade(0f, _fadeOutDuration)
            .SetEase(Ease.InSine)
            .OnComplete(() => _canvasGroup.blocksRaycasts = false);
    }

    private void OnDestroy()
    {
        if (_controller == null) return;
        _controller.OnLeaderboardShown -= Show;
        _controller.OnLeaderboardHidden -= Hide;
    }
}