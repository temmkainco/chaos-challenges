using Fusion;
using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(Player))]
public class PlayerInteraction : NetworkBehaviour
{
    [SerializeField] private float _distance = 3f;
    [SerializeField] private LayerMask _mask;
    [SerializeField] public Transform ObjectHolder;
    [SerializeField] private Transform _eyesPoint;

    [Networked] private NetworkButtons PreviousButtons { get; set; }

    private CinemachineCamera _camera;

    private IGrabbable _currentTarget;

    private void Awake()
    {
        var player = GetComponent<Player>();
        _camera = player.Camera;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasInputAuthority)
            return;

        UpdateTarget();
        HandleInput();
    }

    private void UpdateTarget()
    {
        _currentTarget = null;

        Ray ray = new Ray(_eyesPoint.position, _camera.transform.forward);

        if (Runner.GetPhysicsScene().Raycast(ray.origin, ray.direction, out var hit, _distance, _mask))
        {
            _currentTarget = hit.collider.GetComponent<IGrabbable>();
        }
    }

    private void HandleInput()
    {
        if (!GetInput(out NetworkInputData input))
            return;

        var pressed = input.Buttons.GetPressed(PreviousButtons);

        if (pressed.WasPressed(PreviousButtons, InputButtons.Interact) && _currentTarget != null)
        {
            Debug.Log("Try Interact");
            if (_currentTarget.CanBeGrabbed)
                _currentTarget.RequestGrab(Object.InputAuthority);
            else
                _currentTarget.RequestRelease(Object.InputAuthority);
        }

        PreviousButtons = input.Buttons;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_camera == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(_eyesPoint.position,
                        _eyesPoint.position + _camera.transform.forward * _distance);
    }
#endif
}
