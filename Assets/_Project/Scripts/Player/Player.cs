using Fusion;
using Unity.Cinemachine;
using UnityEngine;

public class Player : NetworkBehaviour, ISpawned
{
    [SerializeField] private CinemachineCamera _camera;
    [SerializeField] private PlayerInput _input;
    public override void Spawned()
    {
        if(Object.HasInputAuthority)
        {
            _camera.gameObject.SetActive(true);
        }
    }
}
