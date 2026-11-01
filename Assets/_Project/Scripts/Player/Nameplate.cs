using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Over other contestants, their player chip (number disc in their colour, name) as in every other screen. Follows the
/// head bone (not the root, which does not move with knockbacks and animation), keeps the same size on screen at any
/// distance, is drawn over the world (a hat or a wall never hides it) and is hidden for yourself.
/// </summary>
public class Nameplate : MonoBehaviour
{
    private const float ABOVE_HEAD = 0.95f;
    private const float CHIP_HEIGHT = 64f;
    /// <summary>World height of the chip at 10 m from the camera; scales linearly with distance.</summary>
    private const float HEIGHT_AT_10M = 0.42f;

    private Player _player;
    private Transform _head;
    private Canvas _canvas;
    private PlayerChip _chip;
    private string _shownName;
    private Color _shownColor;
    private int _shownNumber = -1;

    public static void Attach(Player player)
    {
        // Not parented to the player: the plate positions itself from the head every frame.
        var go = new GameObject("Nameplate " + player.name, typeof(RectTransform));
        var plate = go.AddComponent<Nameplate>();
        plate._player = player;
        player.OnNicknameUpdated += plate.OnNickname;
        plate._canvas = go.AddComponent<Canvas>();
        plate._canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)go.transform).sizeDelta = new Vector2(520f, CHIP_HEIGHT + 16f);
        plate._chip = PlayerChip.Create(go.transform, CHIP_HEIGHT);
        Ui.Place((RectTransform)plate._chip.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, CHIP_HEIGHT));
    }

    /// <summary>Every graphic of the chip on the depth-ignoring materials (after the chip restyles its text).</summary>
    private void DrawOnTop()
    {
        foreach (var graphic in _chip.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic is TMPro.TMP_Text text)
            {
                if (!OnTopMaterials.ContainsValue(text.fontSharedMaterial)) text.fontSharedMaterial = OnTop(text.fontSharedMaterial);
            }
            else if (!OnTopMaterials.ContainsValue(graphic.material)) graphic.material = OnTop(graphic.defaultMaterial);
        }
    }

    private static readonly Dictionary<Material, Material> OnTopMaterials = new();

    /// <summary>A copy of a UI material that ignores depth (shared per source material, so plates batch).</summary>
    private static Material OnTop(Material source)
    {
        if (source == null) return null;
        if (!OnTopMaterials.TryGetValue(source, out var material) || material == null)
        {
            material = new Material(source) { name = source.name + " (on top)" };
            material.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always);
            OnTopMaterials[source] = material;
        }
        return material;
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
        bool visible = !_player.Object.HasInputAuthority && _player.gameObject.activeInHierarchy && !ContestantReveal.IsHidden(_player);
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
            transform.localScale = Vector3.one * (HEIGHT_AT_10M * distance / 10f / CHIP_HEIGHT);
        }
        if (_canvas.enabled != visible)
            _canvas.enabled = visible;
        if (!visible)
            return;

        // The name arrives through OnNicknameUpdated (reading the networked string allocates); colour and number are polled.
        if (_shownName == null)
            OnNickname(_player.Nickname);
        var color = _player.PlayerColor;
        var match = NetworkGameManager.Instance;
        int colour = match != null && match.Object != null && match.Object.IsValid ? match.ColorIndexOf(_player.Object.InputAuthority) : -1;
        int number = colour >= 0 ? colour + 1 : _player.Object.InputAuthority.PlayerId;
        if (color != _shownColor || number != _shownNumber)
        {
            _shownColor = color;
            _shownNumber = number;
            Refresh();
        }
    }

    private void OnNickname(string name)
    {
        _shownName = name ?? "";
        if (_chip != null) Refresh();
    }

    private void Refresh()
    {
        _chip.Set(_shownNumber < 0 ? 0 : _shownNumber, _shownColor, _shownName ?? "");
        _chip.FitToName(140f, 500f);
        DrawOnTop();
    }

    private void OnDestroy()
    {
        if (_player != null) _player.OnNicknameUpdated -= OnNickname;
    }
}
