using Fusion;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    [Networked] public Vector3 CurrentVelocity { get; private set; }

    [SerializeField] private float _movementSpeed;
    private NetworkCharacterController _charachterController;

    private void Awake()
    {
        _charachterController = GetComponent<NetworkCharacterController>();
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData data))
        {
            Vector3 inputDirection = new Vector3(data.Direction.x, 0, data.Direction.z);
            if (inputDirection.sqrMagnitude > 1f) inputDirection.Normalize();

            Quaternion flatCameraRotation = Quaternion.Euler(0, data.CameraRotation.eulerAngles.y, 0);
            Vector3 move = flatCameraRotation * inputDirection;
            Vector3 displacement = _movementSpeed * move * Runner.DeltaTime;
            _charachterController.Move(displacement);

            if(Object.HasStateAuthority)
                CurrentVelocity = displacement / Runner.DeltaTime;
        }
        else
        {
            if (Object.HasStateAuthority)
                CurrentVelocity = Vector3.zero;
        }
    }
}
