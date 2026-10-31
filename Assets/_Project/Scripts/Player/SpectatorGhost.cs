using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Knocked-out contestants (<see cref="Player.IsSpectator"/>) are see-through ghosts in their outfit colour: they run
/// around the arena with everyone, but pass straight through the living (ghosts still bump into each other).
/// Swaps the renderers' materials for a transparent tint and back, and sets collision ignoring on every peer.
/// </summary>
public class SpectatorGhost : MonoBehaviour
{
    private Player _player;
    private readonly List<(Renderer renderer, Material[] materials)> _originals = new();
    private Material _ghost;
    private bool _shown;

    public static void Attach(Player player)
    {
        if (!player.TryGetComponent<SpectatorGhost>(out _))
            player.gameObject.AddComponent<SpectatorGhost>()._player = player;
    }

    private void LateUpdate()
    {
        if (_player == null || _player.Object == null || !_player.Object.IsValid) return;
        bool ghost = _player.IsSpectator && MinigameController.Current != null;
        if (ghost == _shown) return;
        _shown = ghost;
        if (ghost) Apply();
        else Restore();
        UpdateCollisions();
    }

    /// <summary>Living and ghosts ignore each other; two of a kind collide.</summary>
    private void UpdateCollisions()
    {
        if (!TryGetComponent<CharacterController>(out var mine)) return;
        foreach (var other in PlayerMovement.Controllers)
        {
            if (other == null || other.gameObject == gameObject || !other.TryGetComponent<CharacterController>(out var theirs)) continue;
            var otherPlayer = other.GetComponent<Player>();
            bool otherGhost = otherPlayer != null && otherPlayer.IsSpectator && MinigameController.Current != null;
            Physics.IgnoreCollision(mine, theirs, _shown != otherGhost);
        }
    }

    private void Apply()
    {
        _originals.Clear();
        if (_ghost == null)
        {
            // Sprites/Default (via the ThrowArc material) is transparent and unlit: a clean silhouette.
            _ghost = new Material(Resources.Load<Material>("ThrowArc"));
        }
        var c = _player.PlayerColor;
        _ghost.color = new Color(c.r, c.g, c.b, 0.3f);
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            if (renderer is not (SkinnedMeshRenderer or MeshRenderer)) continue;
            _originals.Add((renderer, renderer.sharedMaterials));
            var mats = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = _ghost;
            renderer.sharedMaterials = mats;
        }
    }

    private void Restore()
    {
        foreach (var (renderer, materials) in _originals)
            if (renderer != null) renderer.sharedMaterials = materials;
        _originals.Clear();
    }

    private void OnDestroy()
    {
        if (_ghost != null) Destroy(_ghost);
    }
}
