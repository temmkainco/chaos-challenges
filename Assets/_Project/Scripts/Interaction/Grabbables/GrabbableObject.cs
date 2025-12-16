using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

[RequireComponent(typeof(NetworkRigidbody3D))]
public class GrabbableObject : NetworkBehaviour, IGrabbable
{
    [Networked] public PlayerRef CurrentHolder { get; private set; }
    public bool CanBeGrabbed => CurrentHolder == PlayerRef.None;

    private NetworkRigidbody3D _rb;
    [SerializeField] private Collider[] _objectColliders;
    [SerializeField] private Collider[] _playerColliders;


    public override void Spawned()
    {
        _rb = GetComponent<NetworkRigidbody3D>();
        _objectColliders = GetComponentsInChildren<Collider>();
    }

    public void RequestGrab(PlayerRef player)
    {
        RPC_RequestGrab(player);
    }

    public void RequestRelease(PlayerRef player)
    {
        RPC_RequestRelease(player);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestGrab(PlayerRef player)
    {
        if (!CanBeGrabbed)
            return;

        CurrentHolder = player;
        _rb.RBIsKinematic = true;

        var playerObj = Runner.GetPlayerObject(player);
        _playerColliders = playerObj.GetComponentsInChildren<Collider>();
        foreach (var objCol in _objectColliders)
            foreach (var playerCol in _playerColliders)
                Physics.IgnoreCollision(objCol, playerCol, true);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestRelease(PlayerRef player)
    {
        if (CurrentHolder != player)
            return;

        CurrentHolder = PlayerRef.None;
        _rb.RBIsKinematic = false;

        foreach (var objCol in _objectColliders)
            foreach (var playerCol in _playerColliders)
                Physics.IgnoreCollision(objCol, playerCol, false);

        _playerColliders = null;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        if (CurrentHolder == PlayerRef.None)
            return;

        var playerObj = Runner.GetPlayerObject(CurrentHolder);
        if (!playerObj) return;


        var player = playerObj.GetComponentInChildren<Player>();
        if (!player || !player.Interaction) return;

        Transform grabPoint = player.Interaction.ObjectHolder;

        _rb.Teleport(grabPoint.position, grabPoint.rotation);
    }
}
