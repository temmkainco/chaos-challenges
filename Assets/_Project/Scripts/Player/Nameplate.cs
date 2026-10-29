using TMPro;
using UnityEngine;

/// <summary>Floating name over other contestants, in their body colour, always facing the camera.</summary>
public class Nameplate : MonoBehaviour
{
    private const float HEIGHT = 2.9f;

    private Player _player;
    private TextMeshPro _text;
    private string _shown;

    public static void Attach(Player player)
    {
        var go = new GameObject("Nameplate");
        go.transform.SetParent(player.transform, false);
        go.transform.localPosition = Vector3.up * HEIGHT;
        var plate = go.AddComponent<Nameplate>();
        plate._player = player;
        plate._text = go.AddComponent<TextMeshPro>();
        plate._text.fontSize = 3.2f;
        plate._text.alignment = TextAlignmentOptions.Center;
        plate._text.fontStyle = FontStyles.Bold;
        plate._text.outlineWidth = 0.25f;
        plate._text.outlineColor = new Color32(20, 10, 35, 255);
        plate._text.rectTransform.sizeDelta = new Vector2(8f, 1.5f);
    }

    private void LateUpdate()
    {
        if (_player == null || _player.Object == null || !_player.Object.IsValid)
            return;

        string name = string.IsNullOrEmpty(_player.Nickname) ? "" : _player.Nickname;
        string label = $"<color=#{ColorUtility.ToHtmlStringRGB(_player.OutfitColor)}>{name}</color>";
        if (label != _shown)
        {
            _shown = label;
            _text.text = label;
        }

        var camera = Camera.main;
        if (camera != null)
            transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position);
    }
}
