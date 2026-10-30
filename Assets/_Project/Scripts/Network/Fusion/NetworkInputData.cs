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
}
