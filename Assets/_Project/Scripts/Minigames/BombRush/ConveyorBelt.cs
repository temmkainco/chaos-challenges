using UnityEngine;

/// <summary>
/// A straight conveyor that carries bombs from <see cref="_start"/> to <see cref="_end"/>, where they drop into the arena.
/// The host moves the bombs along it (<see cref="Evaluate"/>); the rollers spin locally for show.
/// </summary>
public class ConveyorBelt : MonoBehaviour
{
    [SerializeField] private Transform _start;
    [SerializeField] private Transform _end;
    [SerializeField] private Transform[] _rollers;
    [SerializeField] private float _speed = 3f;

    public float Speed => _speed;
    public float Length => Vector3.Distance(_start.position, _end.position);
    public Vector3 Direction => (_end.position - _start.position).normalized;

    /// <summary>Position on the belt surface for progress 0..1.</summary>
    public Vector3 Evaluate(float progress) => Vector3.Lerp(_start.position, _end.position, progress);

    private void Update()
    {
        if (_rollers == null) return;
        float degrees = _speed / 0.25f * Mathf.Rad2Deg * Time.deltaTime;
        foreach (var roller in _rollers)
            if (roller != null) roller.Rotate(Vector3.up, degrees, Space.Self);
    }
}
