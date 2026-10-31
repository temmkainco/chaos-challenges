using UnityEngine;

/// <summary>
/// Local player only: hides prediction snaps. When the host corrects your position (a shove or a hit resolved on the
/// host, packet loss) the character's root jumps; the model and the camera's look point are offset back by that jump
/// and slide into place over ~0.1 s, so knockbacks read as a fast push instead of a teleport. Gameplay positions are
/// untouched; real teleports (respawns, lounge) are not smoothed.
/// </summary>
public class PredictionSmoother : MonoBehaviour
{
    private const float MIN_SNAP = 0.08f, MAX_SNAP = 4f, SETTLE = 12f;

    private PlayerMovement _movement;
    private Transform _model, _lookPoint;
    private Vector3 _modelBase, _lookBase, _offset, _lastPosition;
    private bool _primed;

    public static void Attach(Player player)
    {
        if (!player.TryGetComponent<PredictionSmoother>(out _))
            player.gameObject.AddComponent<PredictionSmoother>();
    }

    private void Start()
    {
        _movement = GetComponent<PlayerMovement>();
        var animator = GetComponentInChildren<Animator>(true);
        _model = animator != null && animator.transform != transform ? animator.transform : null;
        _lookPoint = transform.Find("LookPoint");
        if (_model != null) _modelBase = _model.localPosition;
        if (_lookPoint != null) _lookBase = _lookPoint.localPosition;
    }

    private void LateUpdate()
    {
        if (_movement == null || _movement.Object == null || !_movement.Object.IsValid) return;
        var position = transform.position;
        if (_primed)
        {
            var expected = _movement.CurrentVelocity * Time.deltaTime;
            var error = position - _lastPosition - expected;
            float size = error.magnitude;
            if (size >= MAX_SNAP) _offset = Vector3.zero;          // a real teleport: show it as is
            else if (size > MIN_SNAP) _offset -= error;             // a correction: hide the jump, then settle
        }
        _lastPosition = position;
        _primed = true;

        _offset = Vector3.Lerp(_offset, Vector3.zero, 1f - Mathf.Exp(-SETTLE * Time.deltaTime));
        if (_offset.sqrMagnitude < 0.000001f) _offset = Vector3.zero;
        var local = transform.InverseTransformVector(_offset);
        if (_model != null) _model.localPosition = _modelBase + local;
        if (_lookPoint != null) _lookPoint.localPosition = _lookBase + local;
    }
}
