using Fusion;
using UnityEngine;

public class PlayerInput : NetworkBehaviour
{
    public static PlayerInput Local { get; private set; }
    public NetworkButtons Buttons { get; private set; }
    public Vector2 Move { get; private set; }
    public Quaternion CameraRotation { get; private set; }
    public Vector3 LookDirection { get; private set; }
    public bool IsJumpPressed { get; private set; }
    public bool IsInteractPressed { get; private set; }
    public bool IsThrowPressed { get; private set; }

    public PlayerInputActions Controls;
    private Camera _camera;
    private const float WHEEL_JUMP_HOLD = 0.35f;
    private float _jumpHoldUntil;
     
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

    public void SetPlayerControlsActive(bool value)
    {
        if (value)
        {
            Controls.Player.Enable();
            return;
        }

        Controls.Player.Disable();
    }

    private void Update()
    {
        if (Controls == null)
            return;

        if (PauseMenu.IsOpen || GamePackPanel.IsOpen)
        {
            Move = Vector2.zero;
            IsJumpPressed = false;
            IsInteractPressed = false;
            IsThrowPressed = false;
        }
        else
        {
            Move = Controls.Player.Move.ReadValue<Vector2>();
            // The mouse wheel jumps too (playtesters asked for it). A wheel notch lasts one frame, so it is held for a
            // full jump: the tick sees it and releasing early doesn't cut it to a short hop.
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f)
                _jumpHoldUntil = Time.unscaledTime + WHEEL_JUMP_HOLD;
            IsJumpPressed = Controls.Player.Jump.IsPressed() || Time.unscaledTime < _jumpHoldUntil;
            IsInteractPressed = Controls.Player.Interact.IsPressed();
            IsThrowPressed = Controls.Player.Attack.IsPressed();
        }

        if (_camera == null)
            _camera = Camera.main;
        if (_camera == null)
            return;

        Quaternion cameraRotation = _camera.transform.rotation;
        CameraRotation = Quaternion.Euler(0, cameraRotation.eulerAngles.y, 0);
        LookDirection = _camera.transform.forward;
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
