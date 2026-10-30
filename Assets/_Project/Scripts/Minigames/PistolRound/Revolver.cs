using DG.Tweening;
using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Local visuals for Pistol Round: every contestant still in the duel holds a revolver in their right hand that
/// points where they look, a marker hovers over the rival the local player is aiming at, and shots play a muzzle
/// flash plus a "BANG!" / "JAMMED" popup. The scene's revolver model is the template for the copies.
/// </summary>
public class Revolver : MonoBehaviour
{
    [SerializeField] private PistolRoundMinigameController _controller;
    [SerializeField] private BasePlayerSpawner _spawner;
    [SerializeField] private Transform _model;
    [SerializeField] private Transform _muzzle;
    [SerializeField] private Transform _drum;
    [SerializeField] private Transform _aimMarker;
    [SerializeField] private Material _effectMaterial;
    [SerializeField, Tooltip("From the right hand, in the holder's space")] private Vector3 _handOffset = new(0.05f, 0f, 0.2f);

    private readonly Dictionary<PlayerRef, Transform> _guns = new();

    private void Update()
    {
        bool active = _controller.Object != null && _controller.Object.IsValid && _controller.IsGameActive;
        _model.gameObject.SetActive(false);

        var seen = new HashSet<PlayerRef>();
        if (active)
        {
            foreach (var entry in _spawner.Players)
            {
                var player = entry.Value;
                if (player == null || player.Object == null || player.ControlsLocked)
                    continue;
                seen.Add(entry.Key);
                Hold(GunFor(entry.Key), player);
            }
        }

        foreach (var pair in _guns)
            pair.Value.gameObject.SetActive(seen.Contains(pair.Key));

        UpdateAimMarker(active);
    }

    private Transform GunFor(PlayerRef player)
    {
        if (_guns.TryGetValue(player, out var gun) && gun != null)
            return gun;
        gun = Instantiate(_model, transform);
        gun.name = "Revolver " + player;
        _guns[player] = gun;
        return gun;
    }

    private void Hold(Transform gun, Player holder)
    {
        var look = holder.Interaction != null && holder.Interaction.LookDirection.sqrMagnitude > 0.01f
            ? holder.Interaction.LookDirection
            : holder.transform.forward;
        var hand = PlayerRig.For(holder.gameObject).RightHand;
        var anchor = hand != null ? hand.position : holder.transform.position + Vector3.up * 1.2f;
        var target = anchor + holder.transform.rotation * _handOffset;
        float k = 1f - Mathf.Exp(-25f * Time.deltaTime);
        gun.position = Vector3.Lerp(gun.position, target, k);
        gun.rotation = Quaternion.Slerp(gun.rotation, Quaternion.LookRotation(look), k);
    }

    private void UpdateAimMarker(bool active)
    {
        var local = _controller.Runner != null ? _controller.Runner.LocalPlayer : PlayerRef.None;
        if (!active || !_spawner.Players.TryGet(local, out var me) || me == null || me.ControlsLocked
            || _controller.IsJammed(local))
        {
            _aimMarker.gameObject.SetActive(false);
            return;
        }

        var aimed = _controller.FindTarget(me);
        if (aimed == PlayerRef.None || !_spawner.Players.TryGet(aimed, out var aimedPlayer) || aimedPlayer == null)
        {
            _aimMarker.gameObject.SetActive(false);
            return;
        }

        _aimMarker.gameObject.SetActive(true);
        _aimMarker.position = aimedPlayer.transform.position + Vector3.up * (2.6f + Mathf.Sin(Time.time * 6f) * 0.1f);
        _aimMarker.Rotate(Vector3.up, 180f * Time.deltaTime, Space.World);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    public void PlayShot(PlayerRef shooter, PlayerRef target, bool hit)
    {
        Sfx.Play(hit ? Sound.Bang : Sound.Blank);
        var gun = GunFor(shooter);
        gun.DOKill(true);
        gun.DOPunchRotation(new Vector3(-35f, 0f, 0f), 0.35f, 6, 0.6f);

        if (hit && _effectMaterial != null)
        {
            var muzzle = FindDeep(gun, _muzzle.name);
            LowPolyExplosion.Play(muzzle != null ? muzzle.position : gun.position, 0.6f, _effectMaterial);
        }

        if (hit && _spawner.Players.TryGet(target, out var targetPlayer) && targetPlayer != null)
            WorldPopup.Show(targetPlayer.transform.position + Vector3.up * 3f, "BANG!", new Color(1f, 0.3f, 0.2f), 9f);
        else if (!hit && _spawner.Players.TryGet(shooter, out var shooterPlayer) && shooterPlayer != null)
            WorldPopup.Show(shooterPlayer.transform.position + Vector3.up * 3f, "JAMMED", Color.white, 6f);
    }
}
