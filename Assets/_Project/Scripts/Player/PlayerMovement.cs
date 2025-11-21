using Fusion;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float _movementSpeed = 5f;
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
            _charachterController.Move(_movementSpeed * move);
        }
    }
}
