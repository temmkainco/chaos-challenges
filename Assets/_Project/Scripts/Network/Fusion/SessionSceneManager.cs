using Fusion;
using System.Collections;
using UnityEngine.SceneManagement;

namespace Networking
{
    /// <summary>
    /// Fusion's default scene manager, except that runtime-spawned objects (players, thrown props) are destroyed
    /// one frame before the next scene starts loading instead of in the same frame.
    /// The default order makes Unity commit those destroys while the async load already runs on the loading thread,
    /// which deadlocks the Unity 6000.4 Editor (main thread holds the object lock and waits for the PersistentManager
    /// lock in AssetChangedNotifier; the loading thread holds the PersistentManager lock and waits for the object lock).
    /// </summary>
    public class SessionSceneManager : NetworkSceneManagerDefault
    {
        protected override IEnumerator LoadSceneCoroutine(SceneRef sceneRef, NetworkLoadSceneParameters sceneParams)
        {
            bool singlePeer = Runner.Config.PeerMode == NetworkProjectConfig.PeerModes.Single;
            if (singlePeer && DestroySpawnedPrefabsOnSceneUnload && sceneParams.LoadSceneMode == LoadSceneMode.Single)
            {
                DestroyRuntimeSpawnedObjects();
                // Unity destroys at the end of the frame; let that finish before the loading thread starts.
                yield return null;
            }

            var load = base.LoadSceneCoroutine(sceneRef, sceneParams);
            while (load.MoveNext())
                yield return load.Current;
        }

        /// <summary>Same objects the base class destroys on a single-mode load, just earlier.</summary>
        private void DestroyRuntimeSpawnedObjects()
        {
            foreach (var networkObject in Runner.GetAllNetworkObjects())
            {
                if (networkObject == null || networkObject.NetworkTypeId.IsSceneObject)
                    continue;

                // The game manager lives in DontDestroyOnLoad and must survive scene changes.
                if (GetSceneRef(networkObject.gameObject.scene.path) == SceneRef.None)
                    continue;

                if (networkObject.HasStateAuthority)
                    Runner.Despawn(networkObject);
                else
                    Destroy(networkObject.gameObject);
            }
        }
    }
}
