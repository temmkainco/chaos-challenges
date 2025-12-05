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
            Vector3 move = new Vector3(data.Direction.x, 0, data.Direction.z);
            if (move.sqrMagnitude > 1f) move.Normalize();

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
