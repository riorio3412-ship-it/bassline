using System;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BL23.EditorTools.Physicality
{
    /// <summary>
    /// Edit-mode PhysX has no public local-physics NewScene overload. Require an empty physics world,
    /// then simulate only a disposable additive scene. Never silently advance a user's loaded bodies.
    /// SceneManager.CreateScene(LocalPhysicsMode.Physics3D) is only available in Play mode.
    /// </summary>
    public static class PhysicalityTestScene
    {
        public static void Run(Action<Transform, PhysicsScene> action, bool offset = false)
        {
            CheckWorld();
            var previous = SceneManager.GetActiveScene();
            var previousMode = Physics.simulationMode;
            Scene scene = default;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var stage = new GameObject("Disposable physicality QA").transform;
                if (offset) stage.position = new Vector3(3000, 0, 3000);
                action(stage, scene.GetPhysicsScene());
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                Physics.simulationMode = previousMode;
            }
        }

        public static void CheckWorld()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run this QA from Edit mode in an empty validation project.");
            for (int i = 0; i < SceneManager.sceneCount; ++i)
            {
                var existing = SceneManager.GetSceneAt(i);
                if (!existing.isLoaded) continue;
                if (string.IsNullOrEmpty(existing.path))
                    throw new InvalidOperationException("Physicality QA requires an empty SAVED scene, because Unity cannot add a scene beside an unsaved Untitled scene. Use the isolated validation runner; no scene has been saved or modified by this check.");
                foreach (var root in existing.GetRootGameObjects())
                {
                    if (root.GetComponentsInChildren<Rigidbody>(true).Length > 0 || root.GetComponentsInChildren<Collider>(true).Length > 0)
                        throw new InvalidOperationException("Physicality QA requires loaded scenes without Colliders or Rigidbodies. Use the isolated validation project or an empty scene; existing scenes have not been modified.");
                }
            }
        }
    }
}
