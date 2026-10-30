using DG.Tweening;
using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fusion object provider that recycles frequently spawned items (anything with a <see cref="GrabbableObject"/>:
/// bombs, keys, prizes) instead of instantiating and destroying them every time. Released items are parked inactive
/// in a DontDestroyOnLoad pool; on reuse their tweens are killed, every child's local transform is restored to the
/// prefab pose and the rigidbody is reset. Everything else (players, managers) uses the default behaviour.
/// </summary>
public class PooledObjectProvider : NetworkObjectProviderDefault
{
    private const int MAX_PER_PREFAB = 64;

    private struct Pose
    {
        public Transform Transform;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public bool Active;
    }

    private readonly Dictionary<NetworkObject, Stack<NetworkObject>> _pools = new();
    private readonly Dictionary<NetworkObject, NetworkObject> _prefabOf = new();
    private readonly Dictionary<NetworkObject, Pose[]> _poses = new();
    private Transform _poolRoot;

    protected override NetworkObject InstantiatePrefab(NetworkRunner runner, NetworkObject prefab)
    {
        if (!IsPooled(prefab))
            return base.InstantiatePrefab(runner, prefab);

        if (_pools.TryGetValue(prefab, out var pool))
        {
            while (pool.Count > 0)
            {
                var reused = pool.Pop();
                if (reused == null) continue;   // destroyed with an unloaded scene
                reused.transform.SetParent(null, false);
                // Unparenting keeps it in the DontDestroyOnLoad scene; it must belong to the round's scene so it goes
                // away with it (otherwise bombs leak into the next round).
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(reused.gameObject, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                Restore(reused);
                reused.gameObject.SetActive(true);
                return reused;
            }
        }

        var instance = Instantiate(prefab);
        _prefabOf[instance] = prefab;
        _poses[instance] = Capture(instance);
        return instance;
    }

    protected override void DestroyPrefabInstance(NetworkRunner runner, NetworkPrefabId prefabId, NetworkObject instance)
    {
        if (instance == null || !_prefabOf.TryGetValue(instance, out var prefab))
        {
            base.DestroyPrefabInstance(runner, prefabId, instance);
            return;
        }

        if (!_pools.TryGetValue(prefab, out var pool))
            _pools[prefab] = pool = new Stack<NetworkObject>();
        if (pool.Count >= MAX_PER_PREFAB)
        {
            _prefabOf.Remove(instance);
            _poses.Remove(instance);
            Destroy(instance.gameObject);
            return;
        }

        foreach (var t in instance.GetComponentsInChildren<Transform>(true))
            t.DOKill();
        instance.gameObject.SetActive(false);
        if (_poolRoot == null)
        {
            _poolRoot = new GameObject("NetworkObjectPool").transform;
            DontDestroyOnLoad(_poolRoot.gameObject);
        }
        instance.transform.SetParent(_poolRoot, false);
        pool.Push(instance);
    }

    private static bool IsPooled(NetworkObject prefab) => prefab.GetComponent<GrabbableObject>() != null;

    private static Pose[] Capture(NetworkObject instance)
    {
        var transforms = instance.GetComponentsInChildren<Transform>(true);
        var poses = new Pose[transforms.Length];
        for (int i = 0; i < transforms.Length; i++)
        {
            var t = transforms[i];
            poses[i] = new Pose { Transform = t, Position = t.localPosition, Rotation = t.localRotation, Scale = t.localScale, Active = t.gameObject.activeSelf };
        }
        return poses;
    }

    private void Restore(NetworkObject instance)
    {
        if (_poses.TryGetValue(instance, out var poses))
            foreach (var pose in poses)
            {
                if (pose.Transform == null || pose.Transform == instance.transform) continue;
                pose.Transform.localPosition = pose.Position;
                pose.Transform.localRotation = pose.Rotation;
                pose.Transform.localScale = pose.Scale;
                pose.Transform.gameObject.SetActive(pose.Active);
            }

        if (instance.TryGetComponent<Rigidbody>(out var body))
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }
    }
}
