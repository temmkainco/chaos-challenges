using UnityEngine;

/// <summary>A guard tower's searchlight sweeping back and forth across the yard (scenery, local on every peer).</summary>
public class Searchlight : MonoBehaviour
{
    public float Sweep = 50f;
    public float Period = 7f;
    public float Phase;
    public float Tilt = 35f;
    private float _yaw;

    private void Start() => _yaw = transform.eulerAngles.y;

    private void Update()
    {
        float angle = Mathf.Sin((Time.time / Period + Phase) * Mathf.PI * 2f) * Sweep;
        transform.rotation = Quaternion.Euler(Tilt, _yaw + angle, 0f);
    }
}
