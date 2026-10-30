using DG.Tweening;
using Fusion;
using TMPro;
using UnityEngine;

/// <summary>
/// One plate of the minefield (a big arcade button). Pressing it on your turn asks the host to reveal it;
/// the result comes back through <see cref="Minefield"/> and is shown by <see cref="ShowState"/>. A safe plate
/// shows how many mines touch it as a floating number in classic minesweeper colours.
/// </summary>
public class ExplodeButton : MonoBehaviour, IInteractable
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly Color[] CountColors =
    {
        new(0.55f, 0.85f, 0.6f), new(0.2f, 0.45f, 0.95f), new(0.15f, 0.6f, 0.25f), new(0.9f, 0.2f, 0.2f),
        new(0.45f, 0.2f, 0.75f), new(0.65f, 0.15f, 0.15f), new(0.1f, 0.6f, 0.6f), new(0.2f, 0.2f, 0.2f), new(0.5f, 0.5f, 0.5f),
    };

    [SerializeField, Tooltip("The part that goes down when pressed")] private Transform _cap;
    [SerializeField] private Renderer[] _capRenderers;
    [SerializeField, Tooltip("Hidden until the plate turns out to be a mine")] private Transform _mine;
    [SerializeField] private float _pressDepth = 0.12f;

    [Header("Colors")]
    [SerializeField] private Color _safeColor = new(0.3f, 0.85f, 0.4f);
    [SerializeField] private Color _mineColor = new(0.15f, 0.15f, 0.17f);

    public Outline Outline { get; private set; }
    public bool CanBeInteractedWith => _state == PlateState.Hidden && !_removed && _controller != null && _controller.IsLocalTurnOrHost;
    public int Index { get; private set; }

    private MinesMinigameController _controller;
    private PlateState _state;
    private bool _removed;
    private Vector3 _capRestPosition;
    private Vector3 _mineScale;
    private TextMeshPro _countLabel;

    private void Awake()
    {
        Outline = GetComponent<Outline>();
        if (Outline != null) Outline.enabled = false;
        if (_cap != null) _capRestPosition = _cap.localPosition;
        if (_mine != null)
        {
            _mineScale = _mine.localScale;
            _mine.gameObject.SetActive(false);
        }
    }

    public void Initialize(int index, MinesMinigameController controller)
    {
        Index = index;
        _controller = controller;
    }

    /// <summary>Host only (called from the host's interaction raycast).</summary>
    public void Interact(PlayerRef player, NetworkObject playerObject)
    {
        if (_controller != null)
            _controller.TryPressPlate(player, Index);
    }

    public void ShowState(PlateState state, bool animate, int adjacentMines = 0)
    {
        _state = state;
        if (Outline != null && state != PlateState.Hidden) Outline.enabled = false;

        switch (state)
        {
            case PlateState.Safe:
                Tint(_safeColor);
                Press(animate);
                ShowCount(adjacentMines, animate);
                break;
            case PlateState.Mine:
                Tint(_mineColor);
                Press(animate);
                ShowMine(animate);
                break;
        }
    }

    private void ShowCount(int count, bool animate)
    {
        if (_countLabel == null)
        {
            var go = new GameObject("Count");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 1.1f;
            _countLabel = go.AddComponent<TextMeshPro>();
            _countLabel.alignment = TextAlignmentOptions.Center;
            _countLabel.fontSize = 9f;
            _countLabel.fontStyle = FontStyles.Bold;
            _countLabel.outlineWidth = 0.3f;
            _countLabel.outlineColor = new Color32(255, 255, 255, 255);
            _countLabel.rectTransform.sizeDelta = new Vector2(2f, 2f);
        }
        _countLabel.text = count == 0 ? "0" : count.ToString();
        _countLabel.color = CountColors[Mathf.Clamp(count, 0, CountColors.Length - 1)];
        if (animate)
        {
            _countLabel.transform.localScale = Vector3.zero;
            _countLabel.transform.DOScale(1f, 0.35f).SetDelay(0.1f).SetEase(Ease.OutBack).SetLink(gameObject);
        }
    }

    private void LateUpdate()
    {
        if (_countLabel == null) return;
        var camera = Camera.main;
        if (camera != null)
            _countLabel.transform.rotation = Quaternion.LookRotation(camera.transform.forward, Vector3.up);
    }

    public void PopIn(float delay)
    {
        transform.localScale = Vector3.zero;
        transform.DOScale(1f, 0.35f).SetDelay(delay).SetEase(Ease.OutBack).SetLink(gameObject);
    }

    /// <summary>The field is being replaced: shrink away, then destroy.</summary>
    public void Remove()
    {
        _removed = true;
        transform.DOKill();
        transform.DOScale(0f, 0.3f).SetEase(Ease.InBack).SetDelay(Random.Range(0f, 0.25f))
            .OnComplete(() => Destroy(gameObject)).SetLink(gameObject);
    }

    private void Press(bool animate)
    {
        if (_cap == null) return;
        var target = _capRestPosition + Vector3.down * _pressDepth;
        if (animate) _cap.DOLocalMove(target, 0.12f).SetEase(Ease.OutQuad).SetLink(gameObject);
        else _cap.localPosition = target;
    }

    private void ShowMine(bool animate)
    {
        if (_mine == null) return;
        _mine.gameObject.SetActive(true);
        if (!animate)
        {
            _mine.localScale = _mineScale;
            return;
        }

        _mine.localScale = Vector3.zero;
        DOTween.Sequence()
            .Append(_mine.DOScale(_mineScale * 1.3f, 0.15f).SetEase(Ease.OutBack))
            .Append(_mine.DOScale(_mineScale, 0.2f))
            .Join(_mine.DOLocalRotate(new Vector3(0f, 180f, 0f), 0.6f, RotateMode.LocalAxisAdd))
            .SetLink(gameObject);
    }

    private void Tint(Color color)
    {
        if (_capRenderers == null) return;
        var block = new MaterialPropertyBlock();
        foreach (var r in _capRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            r.SetPropertyBlock(block);
        }
    }
}
