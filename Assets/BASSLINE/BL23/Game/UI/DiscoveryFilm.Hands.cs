using System;
using System.Text;
using BL23.Game.Characters;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// D0's hands: the moment he sees it, 민혁's hands come up in front of him into the lower part of his own view and hang
    /// there, open and trembling. The Surprised gesture gives the fingers, wrists and the flinch of the chest; a two-bone IK
    /// after the animator places the wrists where the lens will see them (lower third, clear of the letterbox), whatever the
    /// pitch or the dolly's pull, and lets go again for the return. First-person calm and the shadow-only body are switched
    /// off for these beats (<see cref="ActorView.FilmHands"/>).
    /// </summary>
    public sealed partial class DiscoveryFilm
    {
        /// <summary>Where the hands were in the lens at the check (viewport x,y,z per hand) and whether one sat in the lower
        /// part of the frame (y 0.1–0.45, x 0.05–0.95, in front of the near plane). Probe.</summary>
        public static string LastHandsVp = ""; public static bool LastHandsOk;

        FilmHandsIK _hands;

        void HandsBegin()
        {
            var rig = _me?.Rig; if (rig == null || _pc?.Cam == null) return;
            if (_me != null) _me.FilmHands = true;
            _hands = rig.GetComponent<FilmHandsIK>() ?? rig.gameObject.AddComponent<FilmHandsIK>();
            _hands.Rig = rig; _hands.Cam = _pc.Cam; _hands.Weight = 0f; _hands.Target = 1f; _hands.Speed = 3.2f;
            _hands.PullOf = () => _pull; _hands.enabled = true;
        }

        /// <summary>Let the hands go (fade seconds); the flag goes when they are down.</summary>
        void HandsEnd(float fade)
        {
            if (_hands != null) { _hands.Target = 0f; _hands.Speed = 1f / Mathf.Max(0.05f, fade); }
            else if (_me != null) _me.FilmHands = false;
        }

        void HandsKill()
        {
            if (_hands != null) { UnityEngine.Object.Destroy(_hands); _hands = null; }
            if (_me != null) _me.FilmHands = false;
        }

        /// <summary>The hands must be up in the lower part of the frame (and never fill it or cut the near plane).</summary>
        void CheckHands()
        {
            var rig = _me?.Rig; var cam = _pc.Cam; if (rig == null || cam == null) return;
            var sb = new StringBuilder("hands"); bool any = false, bad = false;
            foreach (var hb in new[] { HBone.HandL, HBone.HandR })
            {
                var tr = rig.HasBone(hb) ? rig.Bone(hb) : null; if (tr == null) continue;
                var vp = cam.WorldToViewportPoint(tr.position);
                sb.Append($" {hb}=({vp.x:0.00},{vp.y:0.00},{vp.z:0.00})");
                bool inLow = vp.z > cam.nearClipPlane + 0.04f && vp.x > 0.05f && vp.x < 0.95f && vp.y > 0.1f && vp.y < 0.45f;
                if (inLow) any = true;
                if (vp.z > 0f && vp.z < cam.nearClipPlane + 0.04f && vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f) bad = true;
            }
            if (bad && _hands != null) { _hands.Depth += 0.08f; sb.Append(" → further out"); }
            LastHandsVp = sb.ToString(); LastHandsOk = any && !bad;
            Stamp(sb + (any ? " in frame" : " NOT IN FRAME"));
        }
    }

    /// <summary>Film-only two-bone IK for the player's arms (after the animator and every other pose writer). Targets are
    /// viewport points at a depth from the eye (the lens may be pulled back behind it); the elbows fall down and out; a
    /// 9 Hz tremble. Blends with the animated pose by Weight and removes itself once it has let go.</summary>
    [DefaultExecutionOrder(10050)]
    public sealed class FilmHandsIK : MonoBehaviour
    {
        public ActorRig Rig; public Camera Cam;
        public float Weight, Target = 1f, Speed = 3f, Depth = 0.42f;
        public Vector2 VpL = new Vector2(0.35f, 0.26f), VpR = new Vector2(0.63f, 0.23f);
        public Func<float> PullOf;
        /// <summary>The warm spill that finds his hands once the candles gutter (without it they read as black gloves).</summary>
        public float FillMax = 0.7f;
        float _t0 = -1f; Light _fill;

        void LateUpdate()
        {
            if (Rig == null || Cam == null) { Destroy(this); return; }
            if (_t0 < 0f) _t0 = Time.unscaledTime;
            Weight = Mathf.MoveTowards(Weight, Target, Time.unscaledDeltaTime * Speed);
            if (Weight <= 0.001f && Target <= 0f)
            {
                var v = GetComponentInParent<ActorView>(); if (v != null) v.FilmHands = false;
                Destroy(this); return;
            }
            float w = Weight * Weight * (3f - 2f * Weight), t = Time.unscaledTime - _t0;
            try { Solve(true, t, w); Solve(false, t, w); } catch (Exception e) { Debug.LogWarning("[DiscoveryFilm] hands IK: " + e.Message); Destroy(this); }
            Fill(w, t);
        }

        /// <summary>A small, shadowless, candle-warm light just below and in front of his eyes (range 0.8 m: it reaches the
        /// raised hands and his cuffs, never the body across the room); it fades with the hands and breathes like a flame.</summary>
        void Fill(float w, float t)
        {
            if (_fill == null)
            {
                var go = new GameObject("FilmHandFill"); go.transform.SetParent(Cam.transform, false);
                go.transform.localPosition = new Vector3(0f, -0.2f, 0.16f);
                _fill = go.AddComponent<Light>(); _fill.type = LightType.Point; _fill.range = 0.8f; _fill.shadows = LightShadows.None;
                _fill.color = new Color(1f, 0.7f, 0.46f); _fill.renderMode = LightRenderMode.ForcePixel; _fill.intensity = 0f;
            }
            _fill.intensity = FillMax * w * (0.94f + 0.06f * Mathf.PerlinNoise(t * 7.3f, 0.41f));
        }

        void Solve(bool left, float t, float w)
        {
            var ua = Rig.Bone(left ? HBone.UpperArmL : HBone.UpperArmR); var la = Rig.Bone(left ? HBone.LowerArmL : HBone.LowerArmR); var hd = Rig.Bone(left ? HBone.HandL : HBone.HandR);
            if (ua == null || la == null || hd == null) return;
            var vp = left ? VpL : VpR;
            float pull = PullOf != null ? Mathf.Max(0f, PullOf()) : 0f;
            var ct = Cam.transform;
            var target = Cam.ViewportToWorldPoint(new Vector3(vp.x, vp.y, Depth + pull));
            // trembling: fast, small, never a shake
            float s = left ? 1.7f : 5.3f, amp = 0.0055f;
            target += ct.right * (Mathf.PerlinNoise(t * 9f, s) - 0.5f) * 2f * amp + ct.up * (Mathf.PerlinNoise(s + 2f, t * 8.3f) - 0.5f) * 2f * amp;
            // the raised hands drift a little lower as the breath goes out
            target -= ct.up * 0.01f * Mathf.Sin(t * 1.3f);
            var animUA = ua.localRotation; var animLA = la.localRotation;
            var a = ua.position; var b = la.position; var c = hd.position;
            float l1 = (b - a).magnitude, l2 = (c - b).magnitude; if (l1 < 1e-3f || l2 < 1e-3f) return;
            var at = target - a; float d = Mathf.Clamp(at.magnitude, Mathf.Abs(l1 - l2) + 0.02f, l1 + l2 - 0.01f);
            var dir = at.sqrMagnitude > 1e-8f ? at.normalized : ct.forward;
            // elbows down and out
            var pole = -ct.up * 0.8f + (left ? -ct.right : ct.right) * 0.6f + Vector3.down * 0.4f;
            var perp = Vector3.ProjectOnPlane(pole, dir); perp = perp.sqrMagnitude > 1e-8f ? perp.normalized : Vector3.down;
            float x = (l1 * l1 - l2 * l2 + d * d) / (2f * d), h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - x * x));
            var e = a + dir * x + perp * h;
            ua.rotation = Quaternion.FromToRotation(b - a, e - a) * ua.rotation;
            var ikUA = ua.localRotation;
            b = la.position; c = hd.position;
            var tgt = a + dir * d;
            la.rotation = Quaternion.FromToRotation(c - b, tgt - b) * la.rotation;
            var ikLA = la.localRotation;
            ua.localRotation = Quaternion.Slerp(animUA, ikUA, w);
            la.localRotation = Quaternion.Slerp(animLA, ikLA, w);
        }

        void OnDestroy()
        {
            if (_fill != null) Destroy(_fill.gameObject);
            var v = GetComponentInParent<ActorView>(); if (v != null && Target <= 0f) v.FilmHands = false;
        }
    }
}
