using TMPro;
using UnityEngine;

/// <summary>
/// Name over other contestants, in their body colour. Follows the head bone (not the root, which does not move
/// with knockbacks and animation), keeps the same size on screen at any distance, and is hidden for yourself.
/// </summary>
public class Nameplate : MonoBehaviour
{
    private const float ABOVE_HEAD = 0.75f;
    /// <summary>World height of the text at 10 m from the camera; scales linearly with distance.</summary>
    private const float SIZE_AT_10M = 1f;

    private Player _player;
    private Transform _head;
    private TextMeshPro _text;
    private string _shownName;
    private Color _shownColor;

    public static void Attach(Player player)
    {
        // Not parented to the player: the plate positions itself from the head every frame.
        var go = new GameObject("Nameplate " + player.name);
        var plate = go.AddComponent<Nameplate>();
        plate._player = player;
        player.OnNicknameUpdated += plate.OnNickname;
        plate._text = go.AddComponent<TextMeshPro>();
        plate._text.fontSize = 3.2f;
        plate._text.alignment = TextAlignmentOptions.Center;
        plate._text.fontStyle = FontStyles.Bold;
        plate._text.outlineWidth = 0.25f;
        plate._text.outlineColor = new Color32(46, 26, 71, 255);
        plate._text.rectTransform.sizeDelta = new Vector2(8f, 1.5f);
        plate._text.enableWordWrapping = false;
    }

    private void LateUpdate()
    {
        if (_player == null)
        {
            Destroy(gameObject);
            return;
        }
        if (_player.Object == null || !_player.Object.IsValid)
            return;

        // Your own name is never useful to you.
        bool visible = !_player.Object.HasInputAuthority && _player.gameObject.activeInHierarchy;
        var camera = Camera.main;
        if (_head == null)
            _head = PlayerRig.For(_player.gameObject).Head;
        Vector3 anchor = (_head != null ? _head.position : _player.transform.position + Vector3.up * 2.1f) + Vector3.up * ABOVE_HEAD;
        transform.position = anchor;

        if (camera != null)
        {
            Vector3 toPlate = anchor - camera.transform.position;
            visible &= Vector3.Dot(toPlate, camera.transform.forward) > 0.5f;
            transform.rotation = Quaternion.LookRotation(camera.transform.forward, camera.transform.up);
            float distance = Mathf.Max(2f, toPlate.magnitude);
            transform.localScale = Vector3.one * (SIZE_AT_10M * distance / 10f);
        }
        if (_text.enabled != visible)
            _text.enabled = visible;
        if (!visible)
            return;

        // The name arrives through OnNicknameUpdated (reading the networked string allocates); only the colour is polled.
        if (_shownName == null)
            OnNickname(_player.Nickname);
        var color = _player.OutfitColor;
        if (color != _shownColor)
        {
            _shownColor = color;
            _text.color = color;
        }
    }

    private void OnNickname(string name)
    {
        _shownName = name ?? "";
        if (_text != null) _text.text = _shownName;
    }

    private void OnDestroy()
    {
        if (_player != null) _player.OnNicknameUpdated -= OnNickname;
    }
}
