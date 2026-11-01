using DG.Tweening;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The show's "please stand by" curtain. It lives for the whole session (DontDestroyOnLoad), drops over the screen
/// whenever the network scene manager is loading (lobby, every round, back to the lobby) and lifts once the new scene
/// is ready on every player's machine (it says how many have loaded meanwhile), so loads never show blank frames or
/// half-built scenes and nobody starts looking at a round before the others are in. A cheap animated TV-static texture runs behind
/// the show logo while it is down.
/// </summary>
public class ShowCurtain : MonoBehaviour
{
    private const float HOLD_AFTER_LOAD = 0.35f;
    private const int NOISE_W = 96, NOISE_H = 54;

    private RectTransform _panel;
    private TextMeshProUGUI _wait;
    private string _shownWait;
    private RawImage _noise;
    private Texture2D _noiseTexture;
    private Color32[] _noisePixels;
    private bool _down;
    private float _readySince = -1f, _nextNoise;
    private uint _seed = 12345;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject("ShowCurtain");
        DontDestroyOnLoad(go);
        go.AddComponent<ShowCurtain>();
    }

    private void Awake()
    {
        var safe = Ui.Canvas("ShowCurtain", 500, transform);
        var canvas = safe.parent;
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(canvas, false);
        panel.transform.SetAsFirstSibling();
        _panel = Ui.Stretch((RectTransform)panel.transform);
        panel.GetComponent<Image>().color = UiTheme.Instance.Ink;
        safe.SetParent(_panel, false);   // the logo travels with the curtain

        _noiseTexture = new Texture2D(NOISE_W, NOISE_H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        _noisePixels = new Color32[NOISE_W * NOISE_H];
        var noise = new GameObject("Static", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        noise.transform.SetParent(_panel, false);
        noise.transform.SetSiblingIndex(0);
        Ui.Stretch((RectTransform)noise.transform);
        _noise = noise.GetComponent<RawImage>();
        _noise.texture = _noiseTexture;
        _noise.color = new Color(1f, 1f, 1f, 0.08f);
        _noise.raycastTarget = false;

        var logo = ShowLogo.Create(safe, "PLEASE STAND BY", UiTheme.Tone.Accent);
        Ui.Place(logo.Rect, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), logo.Rect.sizeDelta);
        _wait = Ui.Text(safe, "", UiTheme.TextRole.Title, UiTheme.Tone.Haze);
        Ui.Place(_wait.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -190f), new Vector2(1200f, 60f));

        // Start lifted.
        _panel.anchoredPosition = new Vector2(0f, 2000f);
        _panel.gameObject.SetActive(false);
    }

    private void Update()
    {
        bool loading = false;
        foreach (var runner in NetworkRunner.Instances)
            if (runner != null && runner.IsRunning && runner.IsSceneManagerBusy)
                loading = true;

        // Loaded here, but the others are still loading: stay down and say so.
        var match = NetworkGameManager.Instance;
        string wait = null;
        if (match != null && match.Object != null && match.Object.IsValid && match.WaitingForPlayers)
        {
            loading = true;
            wait = $"\n<size=22%><color=#FFF6E3>WAITING FOR PLAYERS  {match.LoadedCount}/{Mathf.Max(match.LoadedCount, match.ConnectedPlayerCount)}</color></size>";
        }
        if (wait != _shownWait)
        {
            _shownWait = wait;
            _wait.text = wait ?? "";
        }

        if (loading)
        {
            _readySince = -1f;
            if (!_down) Drop();
        }
        else if (_down)
        {
            if (_readySince < 0f) _readySince = Time.unscaledTime;
            else if (Time.unscaledTime - _readySince > HOLD_AFTER_LOAD) Lift();
        }

        if (_down && Time.unscaledTime >= _nextNoise)
        {
            _nextNoise = Time.unscaledTime + 0.06f;
            for (int i = 0; i < _noisePixels.Length; i++)
            {
                // xorshift: no allocations, no UnityEngine.Random state churn.
                _seed ^= _seed << 13; _seed ^= _seed >> 17; _seed ^= _seed << 5;
                byte v = (byte)(_seed & 0xFF);
                _noisePixels[i] = new Color32(v, v, v, 255);
            }
            _noiseTexture.SetPixels32(_noisePixels);
            _noiseTexture.Apply(false);
        }
    }

    private void Drop()
    {
        _down = true;
        _panel.gameObject.SetActive(true);
        _panel.DOKill();
        if (UiTheme.ReduceMotion) _panel.anchoredPosition = Vector2.zero;
        else _panel.DOAnchorPosY(0f, 0.28f).SetEase(Ease.OutBounce).SetUpdate(true);
    }

    private void Lift()
    {
        _down = false;
        _panel.DOKill();
        _panel.DOAnchorPosY(2000f, UiTheme.ReduceMotion ? 0f : 0.35f).SetEase(Ease.InBack).SetUpdate(true)
            .OnComplete(() => _panel.gameObject.SetActive(false));
    }
}
