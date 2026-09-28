using System.Collections.Generic;
using UnityEngine;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// With the court's vault gone, the whole mansion footprint lies inside the well, 11-26 m above the court floor. Rooms on
    /// other floors are culled by the mansion, but a few things are not registered with any room (loose items, floor traces,
    /// a stray prop): seen from the court they would hang in the air. While a court camera is pinned, those renderers are
    /// switched off (and switched back on exactly as they were when the pin ends). Nothing below the court's floor band is
    /// touched, nor actors (their own view hides them), particles, cameras' children or the court itself.
    /// </summary>
    public sealed partial class CourtroomView
    {
        readonly List<Renderer> _strays = new List<Renderer>();
        readonly HashSet<Renderer> _roomRends = new HashSet<Renderer>();
        float _strayTimer, _strayOff; bool _strayLogged, _roomRendsBuilt;

        void GuardStrays(bool pinned, float dt)
        {
            // a camera hand-over (one frame without the court lens) must not flash the strays back in: wait a moment first
            if (!pinned) { _strayOff += dt; if (_strayOff > 0.5f) { if (_strays.Count > 0) RestoreStrays(); _strayTimer = 0f; _roomRendsBuilt = false; } return; }
            _strayOff = 0f;
            _strayTimer -= dt; if (_strayTimer > 0f) return;
            _strayTimer = 45f;   // the scan runs the moment the court is pinned; loose props rarely appear mid-trial, so only a rare re-check
            if (_view == null || _view.Rooms == null) return;
            if (!_roomRendsBuilt) { _roomRends.Clear(); foreach (var rv in _view.Rooms) if (rv != null) foreach (var r in rv.Renderers) if (r != null) _roomRends.Add(r); _roomRendsBuilt = true; }   // the rooms do not change mid-trial
            float yMin = _c.y + 10.8f, lim = 42f * 42f;
            int hid = 0; string first = null;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (r == null || !r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var b = r.bounds; if (b.max.y < yMin) continue;
                float dx = b.center.x - _c.x, dz = b.center.z - _c.z; if (dx * dx + dz * dz > lim) continue;
                if (_roomRends.Contains(r) || r.transform.IsChildOf(transform)) continue;
                if (r.GetComponentInParent<ActorView>() != null || r.GetComponentInParent<Camera>() != null || r.gameObject.layer == 5) continue;
                r.enabled = false; _strays.Add(r); hid++; if (first == null) first = r.name;
            }
            if (hid > 0 && !_strayLogged) { _strayLogged = true; Debug.Log($"[CourtWell] hid {hid} stray renderers floating in the well (e.g. {first})"); }
        }

        void RestoreStrays()
        {
            foreach (var r in _strays) if (r != null && !r.enabled) r.enabled = true;
            _strays.Clear();
        }
    }
}
