using Fusion;
using UnityEngine;
public enum InputButtons
{
    Jump,
}
public struct NetworkInputData : INetworkInput
{
    public Vector3 Direction;
    public Quaternion CameraRotation;
    public NetworkButtons Buttons;
}