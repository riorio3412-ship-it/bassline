using System;
using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// Time of day. The house is lit by lamps at every hour, but what comes through the glass follows the clock: an
    /// overcast grey-blue daylight in the windows and under the glass roofs (greenhouse, pool, courtyard) by day, amber
    /// at dusk and dawn, moonlight at night. A body found at noon under the greenhouse roof is readable.
    /// Drives: the window / sky-fill lights (the "moon" lights), the per-room ambient of windowed and glass-roofed
    /// rooms, and the shader global _BL_Day (x = daylight 0..1, y = dusk warmth 0..1) used by the sky and window glass.
    /// </summary>
    public sealed partial class MansionView
    {
        /// <summary>0 = night .. 1 = full day, from the game clock (night when there is no running game).</summary>
        public float Sun { get; private set; }
        public float Dusk { get; private set; }
        /// <summary>Offline renders / QA: force an hour (0..24); negative = follow the game clock.</summary>
        public static float HourOverride = -1f;
        float _dayTimer, _sunAmbient = -1f;

        internal static float SunAt(float h)
        {
            if (h < 5.5f || h > 19.5f) return 0f;
            if (h < 7.5f) return Mathf.SmoothStep(0f, 1f, (h - 5.5f) / 2f);
            if (h > 17.5f) return 1f - Mathf.SmoothStep(0f, 1f, (h - 17.5f) / 2f);
            return 1f;
        }
        internal static float DuskAt(float h) => Mathf.Clamp01(Mathf.Max(1f - Mathf.Abs(h - 18.4f) / 1.4f, 1f - Mathf.Abs(h - 6.6f) / 1.2f));

        static float ClockHour()
        {
            if (HourOverride >= 0f) return HourOverride;
            var s = Session.I;
            if (s == null || s.Sim == null || s.Sim.S == null) return -1f;
            return (float)(s.Sim.S.Clock % 1440.0) / 60f;
        }

        /// <summary>Offline renders: apply HourOverride now.</summary>
        public void RefreshDaylight() => UpdateDaylight(true);

        void UpdateDaylight(bool force = false)
        {
            _dayTimer -= Time.unscaledDeltaTime;
            if (!force && _dayTimer > 0f) return;
            _dayTimer = 0.5f;
            float h = ClockHour();
            float sun = h < 0 ? 0f : SunAt(h), dusk = h < 0 ? 0f : DuskAt(h) * (0.35f + 0.65f * Mathf.Min(1f, sun * 3f));
            bool changed = force || Mathf.Abs(sun - Sun) > 0.004f || Mathf.Abs(dusk - Dusk) > 0.004f;
            Sun = sun; Dusk = dusk;
            Shader.SetGlobalVector("_BL_Day", new Vector4(sun, dusk, 0f, 0f));
            if (!changed) return;
            // the light through the glass: pale overcast daylight, amber at the ends of the day, moonlight at night
            Color day = Color.Lerp(new Color(0.86f, 0.9f, 1f), new Color(1f, 0.72f, 0.48f), dusk);
            foreach (var l in AllLights)
            {
                if (!l.Moon || l.Light == null) continue;
                if (!l.HasNight) { l.NightColor = l.Light.color; l.NightBase = l.Base; l.HasNight = true; }
                l.Light.color = Color.Lerp(l.NightColor, day, sun);
                l.Base = l.NightBase * Mathf.Lerp(1f, l.SkyFill ? 3.2f : 1.9f, sun);
                ApplyLight(l);
            }
            // ambient lattice: only when the change is visible (it is a full texture rebuild)
            if (force || Mathf.Abs(sun - _sunAmbient) > 0.05f || (sun == 0f) != (_sunAmbient == 0f)) { _sunAmbient = sun; UpdateAmbient(); }
        }

        // ------------------------------------------------------------------ the crime scene
        readonly List<Light> _sceneKeys = new List<Light>();
        float _sceneTimer;

        /// <summary>While the investigation is on, each body gets a warm key light from above (the scene must be readable
        /// even in a dark corridor); the lights go away when the investigation ends.</summary>
        void UpdateSceneKeys()
        {
            _sceneTimer -= Time.unscaledDeltaTime; if (_sceneTimer > 0f) return; _sceneTimer = 0.5f;
            var S = Session.I?.Sim?.S;
            bool on = S != null && S.Phase == BL23.Sim.Phase.Investigation;
            int k = 0;
            if (on)
                foreach (var a in S.Actors.Values)
                {
                    if (a.Status != ActorStatus.Dead || a.Pos.f < -1) continue;
                    if (k >= _sceneKeys.Count)
                    {
                        var go = new GameObject("SceneKey"); go.transform.SetParent(transform, false);
                        var l = go.AddComponent<Light>(); l.type = LightType.Spot; l.spotAngle = 70f; l.innerSpotAngle = 30f; l.range = 5f;
                        l.color = new Color(1f, 0.82f, 0.62f); l.intensity = 4.5f; l.shadows = LightShadows.Soft; l.shadowStrength = 0.6f;
                        _sceneKeys.Add(l);
                    }
                    var key = _sceneKeys[k++];
                    var p = ToWorld(a.Pos);
                    key.transform.position = p + new Vector3(0.6f, 2.6f, 0.4f);
                    key.transform.rotation = Quaternion.LookRotation((p + Vector3.up * 0.2f) - key.transform.position, Vector3.up);
                    key.enabled = true;
                }
            for (int i = k; i < _sceneKeys.Count; i++) if (_sceneKeys[i] != null) _sceneKeys[i].enabled = false;
        }

        /// <summary>Soft fill from the sky under a glass roof: a few wide lights along the room (moonlight at night,
        /// overcast daylight by day).</summary>
        void SkyFills(RoomView rv)
        {
            var R = rv.Room.Rect; bool alongX = R.W >= R.D; float len = Math.Max(R.W, R.D);
            int n = Mathf.Clamp(Mathf.RoundToInt(len / 8f), 1, 3);
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n;
                var p = alongX ? new Vector3(R.x0 + t * R.W, rv.CeilY - 0.6f, R.CZ) : new Vector3(R.CX, rv.CeilY - 0.6f, R.z0 + t * R.D);
                var rec = AddLight(rv, p, new Color(0.55f, 0.65f, 1f), 7f / Mathf.Sqrt(n), Mathf.Min(14f, len * 0.8f / Mathf.Sqrt(n) + 3f), moon: true);
                if (rec != null) rec.SkyFill = true;
            }
        }

        /// <summary>Daylight share of a room's ambient fill (added in RoomAmbient).</summary>
        Color DaylightAmbient(RoomView rv)
        {
            if (Sun <= 0f || rv == null) return Color.clear;
            var r = rv.Room; if (r.Floor < 0) return Color.clear;
            Color sky = Color.Lerp(new Color(0.16f, 0.175f, 0.2f), new Color(0.2f, 0.15f, 0.11f), Dusk);
            bool roof = rv.Style != null && (rv.Style.CeilKind == 3 || rv.Style.CeilKind == 2) || r.Type == RoomType.Greenhouse || r.Type == RoomType.Courtyard;
            float k = roof ? 1.35f : r.Exterior ? 0.32f : 0f;
            return sky * k * Sun * (1f - Darkness * 0.8f);
        }
    }
}
