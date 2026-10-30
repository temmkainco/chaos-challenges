using UnityEngine;

/// <summary>Set dressing: swings or bobs a prop back and forth around a local axis (robot hammers, signs). Visual only.</summary>
public class SetSwing : MonoBehaviour
{
    [SerializeField] private Vector3 _axis = Vector3.forward;
    [SerializeField] private float _angle = 50f;
    [SerializeField] private float _period = 2.4f;
    [SerializeField] private float _phase;

    private Quaternion _rest;

    private void Awake() => _rest = transform.localRotation;

    private void Update()
    {
        float t = Mathf.Sin((Time.time / _period + _phase) * Mathf.PI * 2f);
        transform.localRotation = _rest * Quaternion.AngleAxis(t * _angle, _axis);
    }

    public void Configure(Vector3 axis, float angle, float period, float phase)
    {
        _axis = axis;
        _angle = angle;
        _period = period;
        _phase = phase;
    }
}
