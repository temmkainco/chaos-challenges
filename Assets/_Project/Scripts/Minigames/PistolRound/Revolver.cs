using DG.Tweening;
using Fusion;
using TMPro;
using UnityEngine;

/// <summary>
/// Local visuals for Pistol Round: the revolver floats at the current shooter's side and points where they look,
/// a marker hovers over the rival the local shooter is aiming at, and shots play a muzzle flash plus a
/// "BANG!" or "click" popup over the target.
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
    [SerializeField] private Vector3 _holdOffset = new(0.55f, 1.35f, 0.5f);

    private void Update()
    {
        if (_controller.Object == null || !_controller.Object.IsValid || !_controller.IsGameActive
            || !_spawner.Players.TryGet(_controller.CurrentTurn, out var shooter) || shooter == null)
        {
            _model.gameObject.SetActive(false);
            _aimMarker.gameObject.SetActive(false);
            return;
        }

        _model.gameObject.SetActive(true);
        var look = shooter.Interaction != null && shooter.Interaction.LookDirection.sqrMagnitude > 0.01f
            ? shooter.Interaction.LookDirection
            : shooter.transform.forward;

        var flat = new Vector3(look.x, 0f, look.z).normalized;
        if (flat.sqrMagnitude < 0.01f) flat = shooter.transform.forward;
        var right = Vector3.Cross(Vector3.up, flat);
        var target = shooter.transform.position + Vector3.up * _holdOffset.y + right * _holdOffset.x + flat * _holdOffset.z;
        _model.position = Vector3.Lerp(_model.position, target, 1f - Mathf.Exp(-20f * Time.deltaTime));
        _model.rotation = Quaternion.Slerp(_model.rotation, Quaternion.LookRotation(look), 1f - Mathf.Exp(-20f * Time.deltaTime));

        UpdateAimMarker(shooter);
    }

    private void UpdateAimMarker(Player shooter)
    {
        bool localShooter = shooter.Object.InputAuthority == _controller.Runner.LocalPlayer;
        var aimed = localShooter ? _controller.FindTarget(shooter) : PlayerRef.None;
        if (aimed == PlayerRef.None || !_spawner.Players.TryGet(aimed, out var aimedPlayer) || aimedPlayer == null)
        {
            _aimMarker.gameObject.SetActive(false);
            return;
        }

        _aimMarker.gameObject.SetActive(true);
        _aimMarker.position = aimedPlayer.transform.position + Vector3.up * (2.6f + Mathf.Sin(Time.time * 6f) * 0.1f);
        _aimMarker.Rotate(Vector3.up, 180f * Time.deltaTime, Space.World);
    }

    public void PlayShot(PlayerRef target, bool live)
    {
        _model.DOKill(true);
        _model.DOPunchRotation(new Vector3(-35f, 0f, 0f), 0.35f, 6, 0.6f);
        if (_drum != null)
            _drum.DOLocalRotate(new Vector3(0f, 0f, 60f), 0.15f, RotateMode.LocalAxisAdd);

        if (live && _effectMaterial != null)
            LowPolyExplosion.Play(_muzzle.position, 0.6f, _effectMaterial);

        if (_spawner.Players.TryGet(target, out var targetPlayer) && targetPlayer != null)
            Popup(targetPlayer.transform.position + Vector3.up * 3f, live ? "BANG!" : "click...", live ? new Color(1f, 0.3f, 0.2f) : Color.white, live ? 9f : 6f);
    }

    private static void Popup(Vector3 position, string text, Color color, float size)
    {
        var go = new GameObject("ShotPopup");
        go.transform.position = position;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = Color.black;

        var camera = Camera.main;
        if (camera != null) go.transform.rotation = Quaternion.LookRotation(go.transform.position - camera.transform.position);

        go.transform.localScale = Vector3.zero;
        DOTween.Sequence()
            .Append(go.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack))
            .Join(go.transform.DOMoveY(position.y + 1.2f, 1.2f))
            .AppendInterval(0.4f)
            .Append(go.transform.DOScale(0f, 0.2f))
            .OnComplete(() => Destroy(go))
            .SetLink(go);
    }
}
