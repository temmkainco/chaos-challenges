using Fusion;
using UnityEngine;
public enum InputButtons
{
    Jump,
    Interact,
    Throw,
    Sprint,
}
public struct NetworkInputData : INetworkInput
{
    public Vector3 Direction;
    public Quaternion CameraRotation;
    /// <summary>Camera forward; the host aims interactions and throws with it.</summary>
    public Vector3 LookDirection;
    public NetworkButtons Buttons;
    /// <summary>The tick this client is showing the other contestants at (lag compensation, see <see cref="Rewind"/>).</summary>
    public int ViewTick;
}
