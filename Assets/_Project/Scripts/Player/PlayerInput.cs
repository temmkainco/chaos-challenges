using Fusion;
using UnityEngine;

public class PlayerInput : NetworkBehaviour
{
    public static PlayerInput Local { get; private set; }
    public NetworkButtons Buttons { get; private set; }
    public Vector2 Move { get; private set; }
    public Quaternion CameraRotation { get; private set; }
    public bool IsJumpPressed { get; private set; }
    public bool IsInteractPressed { get; private set; }

    public PlayerInputActions Controls;
    private Camera _camera;
     
    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            Controls = new PlayerInputActions();
            Local = this;

            Controls.Enable();
            _camera = Camera.main;
        }
    }

    private void Update()
    {
        if (Controls == null)
            return;

        Move = Controls.Player.Move.ReadValue<Vector2>();
        IsJumpPressed = Controls.Player.Jump.IsPressed();
        IsInteractPressed = Controls.Player.Interact.IsPressed();

        Quaternion cameraRotation = _camera.transform.rotation;
        CameraRotation = Quaternion.Euler(0, cameraRotation.eulerAngles.y, 0);
    }

    private void OnDestroy()
    {
        if (Local == this)
        {
            Local = null;
        }

        if (Controls != null)
        {
            Controls.Disable();
            Controls.Dispose();
            Controls = null;
        }
    }
}
