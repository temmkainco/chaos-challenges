using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

[RequireComponent(typeof(NetworkRigidbody3D))]
public class GrabbableObject : NetworkBehaviour, IGrabbable
{
    [Networked] public PlayerRef CurrentHolder { get; private set; } = PlayerRef.None;

    [SerializeField] private Transform VisualRoot;

    private NetworkRigidbody3D _rb;

    public bool CanBeGrabbed => CurrentHolder == PlayerRef.None;
    private Vector3 _renderPosition;
    private Quaternion _renderRotation;
    public override void Spawned()
    {
        _rb = GetComponent<NetworkRigidbody3D>();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestGrab(PlayerRef player)
    {
        if (!Object.HasStateAuthority || !CanBeGrabbed)
            return;

        CurrentHolder = player;
        _rb.RBIsKinematic = true;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestRelease(PlayerRef player)
    {
        if (CurrentHolder != player) return;

        CurrentHolder = PlayerRef.None;
        _rb.RBIsKinematic = false;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || CurrentHolder == PlayerRef.None)
            return;

        // Move server physics to the holder's hand
        var playerObj = Runner.GetPlayerObject(CurrentHolder);
        if (!playerObj) return;

        var hand = playerObj.GetComponent<PlayerInteraction>().ObjectHolder;
        if (!hand) return;

        _rb.Teleport(hand.position, hand.rotation);
    }

    public override void Render()
    {
        if (CurrentHolder == PlayerRef.None)
            return;

        var playerObj = Runner.GetPlayerObject(CurrentHolder);
        if (!playerObj) return;

        var interaction = playerObj.GetComponent<PlayerInteraction>();
        if (interaction == null || interaction.ObjectHolder == null) return;

        var hand = interaction.ObjectHolder;

        if (CurrentHolder == Runner.LocalPlayer)
        {
            // Fully predicted: instant movement with hand
            VisualRoot.SetPositionAndRotation(hand.position, hand.rotation);
        }
        else
        {
            // Non-holder: smooth interpolation from server Rigidbody
            float smoothSpeed = 20f; // tweak this
            _renderPosition = Vector3.Lerp(_renderPosition, _rb.transform.position, 1f - Mathf.Exp(-smoothSpeed * Runner.DeltaTime));
            _renderRotation = Quaternion.Slerp(_renderRotation, _rb.transform.rotation, 1f - Mathf.Exp(-smoothSpeed * Runner.DeltaTime));
            VisualRoot.SetPositionAndRotation(_renderPosition, _renderRotation);
        }
    }

}
