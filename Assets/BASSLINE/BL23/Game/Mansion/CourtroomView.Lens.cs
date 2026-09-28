using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// The trial is shot through a clean lens: no film grain and no colour fringing while the court camera is pinned (the house
    /// look keeps a little grain everywhere else). Lighting, fog and framing carry the mood instead. A court-owned global
    /// volume above the mansion's own, weighted 1 only while a trial / court-vista camera holds the court.
    /// </summary>
    public sealed partial class CourtroomView
    {
        Volume _lens; VolumeProfile _lensProfile;

        void CleanLens(bool on)
        {
            if (_lens == null)
            {
                if (!on) return;
                var go = new GameObject("CourtLens"); go.transform.SetParent(transform, false);
                _lens = go.AddComponent<Volume>(); _lens.isGlobal = true; _lens.priority = 60f;
                _lensProfile = ScriptableObject.CreateInstance<VolumeProfile>(); _lensProfile.name = "CourtLens";
                var fg = _lensProfile.Add<FilmGrain>(false); fg.active = true; fg.intensity.Override(0f);
                var ca = _lensProfile.Add<ChromaticAberration>(false); ca.active = true; ca.intensity.Override(0f);
                _lens.sharedProfile = _lensProfile; _lens.weight = 0f;
            }
            float w = on ? 1f : 0f;
            if (!Mathf.Approximately(_lens.weight, w)) _lens.weight = w;
        }
    }
}
