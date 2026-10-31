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
    private TextMeshProUGUI _text;
    private string _shownWait;
    private const string LOGO = "DON'T STOP\nTHE PAR<color=#FF8A3D><size=130%>TV</size></color>\n<size=30%><color=#FFF6E3>PLEASE STAND BY</color></size>";
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
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(transform, false);
        _panel = (RectTransform)panel.transform;
        _panel.anchorMin = Vector2.zero; _panel.anchorMax = Vector2.one; _panel.offsetMin = _panel.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = UiKit.Ink;

        _noiseTexture = new Texture2D(NOISE_W, NOISE_H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        _noisePixels = new Color32[NOISE_W * NOISE_H];
        var noise = new GameObject("Static", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        noise.transform.SetParent(_panel, false);
        var nrt = (RectTransform)noise.transform;
        nrt.anchorMin = Vector2.zero; nrt.anchorMax = Vector2.one; nrt.offsetMin = nrt.offsetMax = Vector2.zero;
        _noise = noise.GetComponent<RawImage>();
        _noise.texture = _noiseTexture;
        _noise.color = new Color(1f, 1f, 1f, 0.12f);
        _noise.raycastTarget = false;

        var logo = new GameObject("Logo", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        logo.transform.SetParent(_panel, false);
        var lrt = (RectTransform)logo.transform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.sizeDelta = new Vector2(1600f, 400f);
        var text = logo.GetComponent<TextMeshProUGUI>();
        var kit = UiKit.Instance;
        if (kit != null)
        {
            text.font = kit.Font;
            if (kit.OutlineThick != null) text.fontSharedMaterial = kit.OutlineThick;
        }
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 110f;
        text.color = UiKit.Pink;
        text.text = LOGO;
        text.raycastTarget = false;
        _text = text;

        // Start lifted.
        _panel.anchoredPosition = new Vector2(0f, 1200f);
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
        if (wait != _shownWait && _text != null)
        {
            _shownWait = wait;
            _text.text = LOGO + wait;
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
        _panel.DOAnchorPosY(0f, 0.28f).SetEase(Ease.OutBounce).SetUpdate(true);
    }

    private void Lift()
    {
        _down = false;
        _panel.DOKill();
        _panel.DOAnchorPosY(1200f, 0.35f).SetEase(Ease.InBack).SetUpdate(true)
            .OnComplete(() => _panel.gameObject.SetActive(false));
    }
}
