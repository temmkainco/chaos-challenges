using Fusion;
using UnityEngine;
using UnityEngine.SocialPlatforms;

public class PlayerInput : NetworkBehaviour
{
    public static PlayerInput Local { get; private set; }
    public NetworkButtons Buttons { get; private set; }
    public Vector2 Move { get; private set; }
    public Quaternion CameraRotation { get; private set; }
    public bool IsJumpPressed => _isJumpPressed;


    private bool _isJumpPressed;

    public PlayerInputActions _controls;
    private Camera _camera;
     
    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            _controls = new PlayerInputActions();
            Local = this;

            _controls.Enable();
            _camera = Camera.main;
        }
    }

    private void Update()
    {
        if (_controls == null)
            return;

        Move = _controls.Player.Move.ReadValue<Vector2>();
        _isJumpPressed = _controls.Player.Jump.IsPressed();

        Quaternion cameraRotation = _camera.transform.rotation;
        CameraRotation = Quaternion.Euler(0, cameraRotation.eulerAngles.y, 0);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Local != this)
            return;

        Local = null;
        _controls.Disable();    
    }

    private void OnDestroy()
    {
        if (Local != this)
            return;

        Local = null; 
    }
}
