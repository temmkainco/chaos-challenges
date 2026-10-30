using UnityEngine;

/// <summary>Spins a wearable part (the propeller cap's propeller). Visual only.</summary>
public class CosmeticSpin : MonoBehaviour
{
    [SerializeField] private float _degreesPerSecond = 540f;

    private void Update() => transform.Rotate(0f, _degreesPerSecond * Time.deltaTime, 0f, Space.Self);
}
