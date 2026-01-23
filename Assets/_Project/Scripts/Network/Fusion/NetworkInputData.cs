using Fusion;
using UnityEngine;
public enum InputButtons
{
    Jump,
    Interact
}
public struct NetworkInputData : INetworkInput
{
    public Vector3 Direction;
    public Quaternion CameraRotation;
    public NetworkButtons Buttons;
}