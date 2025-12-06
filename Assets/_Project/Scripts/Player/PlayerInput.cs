using Fusion;
using UnityEngine;
using UnityEngine.SocialPlatforms;

public class PlayerInput : NetworkBehaviour
{
    public static PlayerInput Local { get; private set; }
    public Vector2 Move { get; private set; }
    public bool Jump { get; private set; }
    public Quaternion CameraRotation { get; private set; }

    private PlayerInputActions _controls;

    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            _controls = new PlayerInputActions();
            Local = this;

            Debug.Log("Enabling Player Controls and Setting Local Singleton.");
            _controls.Enable();
        }
    }

    private void Update()
    {
        if (_controls != null)
        {
            Move = _controls.Player.Move.ReadValue<Vector2>();
            Jump = _controls.Player.Jump.WasPressedThisFrame();

            if (Camera.main != null)
            {
                Quaternion cameraRotation = Camera.main.transform.rotation;
                CameraRotation = Quaternion.Euler(0, cameraRotation.eulerAngles.y, 0);
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Local == this)
        {
            Local = null;
            _controls.Disable();    
            Debug.Log("Clearing Local Singleton reference on Despawn.");
        }

    }

    private void OnDestroy()
    {
        if (Local == this)
        {
            Local = null;
        }
    }
}
