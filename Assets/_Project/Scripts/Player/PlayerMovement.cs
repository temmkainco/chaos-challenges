using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkCharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Networked] public Vector3 CurrentVelocity { get; private set; }
    [Networked] public bool IsGrounded { get; private set; } = true;
    [Networked] public NetworkButtons PreviousButtons { get; private set; }

    [SerializeField] private float _movementSpeed = 5f;
    [SerializeField] private float _rotationSpeed = 10f;

    private NetworkCharacterController _characterController;

    private void Awake()
    {
        _characterController = GetComponent<NetworkCharacterController>();
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out NetworkInputData data))
        {
            if (Object.HasStateAuthority)
                CurrentVelocity = Vector3.zero;
            return;
        }

        Vector3 inputDirection = new Vector3(data.Direction.x, 0f, data.Direction.z);
        if (inputDirection.sqrMagnitude > 1f)
            inputDirection.Normalize();

        Quaternion flatCameraRotation = Quaternion.Euler(0f, data.CameraRotation.eulerAngles.y, 0f);
        Vector3 targetDir = flatCameraRotation * inputDirection;

        Vector3 displacement = targetDir * _movementSpeed * Runner.DeltaTime;

        _characterController.Move(displacement);

        if (targetDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDir);
            _characterController.transform.rotation = Quaternion.Slerp(
                _characterController.transform.rotation,
                targetRotation,
                _rotationSpeed * Runner.DeltaTime
            );
        }

        var pressed = data.Buttons.GetPressed(PreviousButtons);
        if (pressed.WasPressed(PreviousButtons, InputButtons.Jump) && IsGrounded)
        {
            _characterController.Jump();
            IsGrounded = false;
        }

        if (!IsGrounded && _characterController.IsGrounded && _characterController.Velocity.y <= 0f)
        {
            IsGrounded = true;
        }

        PreviousButtons = data.Buttons;

        if (Object.HasStateAuthority)
        {
            CurrentVelocity = displacement / Runner.DeltaTime;
        }
    }
}
