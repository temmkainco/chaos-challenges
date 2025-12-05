using UnityEngine;
using UnityEngine.SocialPlatforms;

public class PlayerInput : MonoBehaviour
{
    public static PlayerInput Local;

    public Vector2 Move { get; private set; }
    public bool Jump { get; private set; }
    public Quaternion CameraRotation { get; private set; }

    private PlayerInputActions _controls;

    private void Awake()
    {
        Local = this;
        _controls = new PlayerInputActions();
        _controls.Enable();
    }

    private void Update()
    {
        Move = _controls.Player.Move.ReadValue<Vector2>();
        Jump = _controls.Player.Jump.WasPressedThisFrame();

        Quaternion cameraRotation = Camera.main.transform.rotation;
        CameraRotation = Quaternion.Euler(0, cameraRotation.eulerAngles.y, 0);
    }
}
