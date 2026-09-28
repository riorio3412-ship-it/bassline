using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// The one AudioListener in the game. It follows whichever full-screen camera is on screen (player, conversation,
    /// cinematic, trial, reveal, title), so music and sound never go silent when a scene switches cameras.
    /// (Before this, the listener lived on the player camera and was switched off with it — the whole trial was mute.)
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class AudioEars : MonoBehaviour
    {
        static AudioEars _i;
        public static Transform Ear => _i != null ? _i.transform : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { Ensure(); }

        public static void Ensure()
        {
            if (_i != null) return;
            var go = new GameObject("AudioEars"); DontDestroyOnLoad(go);
            _i = go.AddComponent<AudioEars>();
            go.AddComponent<AudioListener>();
        }

        readonly Camera[] _buf = new Camera[64];
        float _nextSweep;

        void LateUpdate()
        {
            // any other listener (legacy cameras) would split or mute the mix: keep exactly one (checked once a second)
            if (Time.unscaledTime >= _nextSweep)
            {
                _nextSweep = Time.unscaledTime + 1f;
                foreach (var l in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude))
                    if (l != null && l.gameObject != gameObject && l.enabled) l.enabled = false;
            }
            var best = Pick();
            if (best != null) transform.SetPositionAndRotation(best.transform.position, best.transform.rotation);
        }

        Camera Pick()
        {
            int n = Camera.GetAllCameras(_buf);
            Camera best = null;
            for (int i = 0; i < n; i++)
            {
                var c = _buf[i];
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy || c.targetTexture != null) continue;
                if (c.cullingMask == 0) continue;                                      // letterbox / clear-only cameras
                var r = c.rect; if (r.width < 0.5f || r.height < 0.5f) continue;     // insets and picture-in-picture
                if (best == null || c.depth > best.depth) best = c;
            }
            return best ?? Camera.main;
        }
    }
}
