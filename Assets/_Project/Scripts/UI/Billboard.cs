using UnityEngine;

/// <summary>Keeps world-space labels facing the camera.</summary>
public class Billboard : MonoBehaviour
{
    private void LateUpdate()
    {
        var camera = Camera.main;
        if (camera != null)
            transform.rotation = Quaternion.LookRotation(camera.transform.forward, Vector3.up);
    }
}
