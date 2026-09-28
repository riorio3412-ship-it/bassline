using System.Collections.Generic;
using BL23.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BL23.Game
{
    /// <summary>
    /// Overheard chatter as light, modern "whisper captions" above the speaker's head (user: "말하는 캐릭터 위에 뜨는게 나을 것
    /// 같아", then "말풍선이 너무 난잡하고 많고 이상해 … 트렌디하게"). Design rules:
    /// - no boxes, tails or frames: a small gold name over clean text with a soft outline and a feathered dark glow behind it;
    /// - at most two captions on screen, one new caption every ~1.8 s at most (nearer speakers win), two lines max with an ellipsis;
    /// - only speakers you can actually see (on screen, in line of sight, within 10 m) get a caption; a voice you only hear
    ///   (behind you, behind a wall, off screen) is a tiny "···" ping at the screen edge with the name — no text;
    /// - typewriter reveal, a short rise on entry, drift and fade on exit, screen position smoothed so it never jitters.
    /// Hidden while a conversation, menu, notebook, cinematic or the trial owns the screen.
    /// </summary>
    public sealed class SpeechBubbles : MonoBehaviour
    {
        public static SpeechBubbles I;

        sealed class Cap
        {
            public string Actor, Line; public RectTransform Rt; public CanvasGroup Cg; public TextMeshProUGUI Name, Body; public Image Glow;
            public float Born, Until; public Vector2 Pos, Vel; public bool Placed; public float Dist; public int Chars;
        }
        sealed class Ping { public string Actor; public RectTransform Rt; public CanvasGroup Cg; public TextMeshProUGUI Label; public float Born, Until; }

        Canvas _c; RectTransform _root; CanvasGroup _rootCg;
        readonly List<Cap> _caps = new List<Cap>();
        Ping _ping; float _lastNew = -9f;
        static Sprite _glowSprite;

        const int MaxCaps = 2; const float SeeRange = 10f, HearRange = 15f, MinGap = 1.8f, MaxW = 360f, RevealCps = 45f;

        static void Ensure()
        {
            if (I != null) return;
            var go = new GameObject("SpeechBubbles"); DontDestroyOnLoad(go);
            I = go.AddComponent<SpeechBubbles>();
            I._c = UIKit.Root("SpeechBubbleCanvas", 9);          // just under the HUD (10)
            I._c.GetComponent<GraphicRaycaster>().enabled = false;
            I._root = UIKit.Full(I._c.transform, "Captions");
            I._rootCg = I._root.gameObject.AddComponent<CanvasGroup>(); I._rootCg.blocksRaycasts = false; I._rootCg.interactable = false;
        }

        /// <summary>Show a line from this speaker. Returns false when the speaker has no view (the caller may fall back).</summary>
        public static bool Show(string actor, string line, float dist)
        {
            if (string.IsNullOrEmpty(actor) || string.IsNullOrEmpty(line)) return false;
            var ses = Session.I; if (ses == null || ses.World == null || ses.World.ViewOf(actor) == null) return false;
            Ensure(); I.Add(actor, line); return true;   // handled (shown, pinged or deliberately dropped — never the old corner list)
        }

        /// <summary>Probe: captions and pings currently alive.</summary>
        public static int ProbeCount => I == null ? 0 : I._caps.Count + (I._ping != null ? 1 : 0);

        // ------------------------------------------------------------------ intake
        void Add(string actor, string line)
        {
            var cam = Cam(); if (cam == null) return;
            var v = Session.I.World.ViewOf(actor); if (v == null) return;
            var head = v.HeadPos + Vector3.up * 0.34f; var camPos = cam.transform.position;
            float d = Vector3.Distance(camPos, head);
            if (d > HearRange) return;
            bool seen = d <= SeeRange && OnScreen(cam, head) && Visible(camPos, head, v.transform);
            if (!seen) { PingVoice(actor); return; }
            float now = Time.time;
            foreach (var c in _caps) if (c.Actor == actor)                       // the same speaker: replace the line in place
            {
                if (c.Line != line) { SetText(c, line); c.Born = now; }
                c.Until = now + Life(line); return;
            }
            if (_caps.Count >= MaxCaps || now - _lastNew < MinGap)
            {
                // a nearer speaker may take the place of the farthest caption; otherwise the line is simply not shown
                Cap far = null; foreach (var c in _caps) if (far == null || c.Dist > far.Dist) far = c;
                if (far == null || d >= far.Dist - 1.5f) return;
                Kill(far);
            }
            var nc = Build(actor); SetText(nc, line); nc.Born = now; nc.Until = now + Life(line); nc.Dist = d; _caps.Add(nc); _lastNew = now;
        }

        static float Life(string line) => Mathf.Clamp(1.8f + line.Length * 0.06f, 2.6f, 5.5f);

        void PingVoice(string actor)
        {
            float now = Time.time;
            if (_ping != null && _ping.Actor != actor && now - _ping.Born < 1.2f) return;   // one ping at a time
            if (_ping == null)
            {
                _ping = new Ping();
                _ping.Rt = UIKit.Rect(_root, "VoicePing", Vector2.zero, Vector2.zero); _ping.Rt.sizeDelta = new Vector2(220, 40); _ping.Rt.pivot = new Vector2(0.5f, 0.5f);
                _ping.Cg = _ping.Rt.gameObject.AddComponent<CanvasGroup>();
                var g = UIKit.Img(_ping.Rt, "Glow", new Color(0, 0, 0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-110, -20), new Vector2(110, 20)); g.sprite = GlowSprite();
                _ping.Label = UIKit.Text(_ping.Rt, "L", "", 15, Pal.A(Pal.Text, 0.85f), TextAlignmentOptions.Center, Fonts.Body);
                Outline(_ping.Label, 0.18f);
            }
            _ping.Actor = actor; _ping.Born = now; _ping.Until = now + 1.6f;
            _ping.Label.text = $"<color=#D6AD62>···</color>  {Cast.GivenOf(actor)}";
        }

        // ------------------------------------------------------------------ building
        Cap Build(string actor)
        {
            var c = new Cap { Actor = actor };
            c.Rt = UIKit.Rect(_root, "Cap_" + actor, Vector2.zero, Vector2.zero); c.Rt.pivot = new Vector2(0.5f, 0f);
            c.Cg = c.Rt.gameObject.AddComponent<CanvasGroup>(); c.Cg.alpha = 0f;
            c.Glow = UIKit.Img(c.Rt, "Glow", new Color(0.02f, 0.015f, 0.012f, 0.62f), Vector2.zero, Vector2.one, new Vector2(-46, -26), new Vector2(46, 22));
            c.Glow.sprite = GlowSprite();
            c.Name = UIKit.Text(c.Rt, "Name", "", 13, Pal.Gold, TextAlignmentOptions.Bottom, Fonts.Bold, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(0, 18));
            c.Name.characterSpacing = 6f; c.Name.text = Cast.GivenOf(actor);
            Outline(c.Name, 0.16f);
            c.Body = UIKit.Text(c.Rt, "Body", "", 19, Pal.A(Pal.Text, 0.97f), TextAlignmentOptions.Top, Fonts.Body, Vector2.zero, Vector2.one);
            c.Body.overflowMode = TextOverflowModes.Ellipsis; c.Body.maxVisibleLines = 2; c.Body.lineSpacing = -6f;
            Outline(c.Body, 0.2f);
            return c;
        }

        static void Outline(TextMeshProUGUI t, float w)
        {
            t.outlineWidth = w; t.outlineColor = new Color32(12, 8, 6, 220);
        }

        void SetText(Cap c, string line)
        {
            c.Line = line; c.Body.text = line; c.Chars = line.Length; c.Body.maxVisibleCharacters = 0;
            var pref = c.Body.GetPreferredValues(line, MaxW, 0f);
            float w = Mathf.Clamp(pref.x + 4f, 90f, MaxW);
            float h = Mathf.Min(c.Body.GetPreferredValues(line, w, 0f).y, 58f);
            c.Rt.sizeDelta = new Vector2(w, h);
        }

        void Kill(Cap c) { if (c.Rt != null) Destroy(c.Rt.gameObject); _caps.Remove(c); }

        static Sprite GlowSprite()
        {
            if (_glowSprite != null) return _glowSprite;
            const int W = 64, H = 32; var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W * 2f - 1f, v = (y + 0.5f) / H * 2f - 1f;
                float r = Mathf.Sqrt(u * u * 0.85f + v * v);
                float a = Mathf.Clamp01(1f - r); a = a * a * (3f - 2f * a);    // feathered, no hard edge
                px[x + y * W] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px); tex.Apply(false, true);
            _glowSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f));
            return _glowSprite;
        }

        // ------------------------------------------------------------------ per frame
        Camera Cam()
        {
            var pc = Session.I != null ? Session.I.Player : null;
            return pc != null && pc.Cam != null && pc.Cam.enabled && pc.Cam.gameObject.activeInHierarchy ? pc.Cam : null;
        }

        static bool OnScreen(Camera cam, Vector3 p)
        {
            var v = cam.WorldToViewportPoint(p); return v.z > 0.2f && v.x > 0.06f && v.x < 0.94f && v.y > 0.08f && v.y < 0.92f;
        }

        static bool Visible(Vector3 from, Vector3 to, Transform self)
        {
            var dir = to - from; float len = dir.magnitude; if (len < 0.3f) return true;
            if (!Physics.Raycast(from, dir / len, out var hit, len - 0.25f, ~0, QueryTriggerInteraction.Ignore)) return true;
            return hit.collider.transform.IsChildOf(self);
        }

        bool Blocked()
        {
            var ses = Session.I; if (ses == null) return true;
            return (ses.Dialogue != null && ses.Dialogue.Active) || (ses.Note != null && ses.Note.Open) || (ses.Menu != null && ses.Menu.Open)
                || (ses.Cine != null && ses.Cine.Busy) || (ses.Trial != null && ses.Trial.Active);
        }

        void LateUpdate()
        {
            if (_root == null) return;
            float now = Time.time, dt = Time.unscaledDeltaTime;
            var cam = Cam(); bool hide = cam == null || Blocked();
            _rootCg.alpha = Mathf.MoveTowards(_rootCg.alpha, hide ? 0f : 1f, dt * 8f);
            for (int i = _caps.Count - 1; i >= 0; i--) if (now > _caps[i].Until + 0.35f || Session.I?.World?.ViewOf(_caps[i].Actor) == null) Kill(_caps[i]);
            if (_ping != null && now > _ping.Until + 0.3f) { Destroy(_ping.Rt.gameObject); _ping = null; }
            if (hide) return;
            float k = _c.scaleFactor > 0.0001f ? _c.scaleFactor : 1f;
            var canvas = new Vector2(Screen.width / k, Screen.height / k);
            var camPos = cam.transform.position;
            Rect? first = null;
            // (perf) at most two captions: swap instead of List.Sort (which allocated a comparer every frame); same order
            if (_caps.Count == 2) { if (_caps[0].Dist.CompareTo(_caps[1].Dist) > 0) { var c0 = _caps[0]; _caps[0] = _caps[1]; _caps[1] = c0; } }
            else if (_caps.Count > 2) _caps.Sort((a, b) => a.Dist.CompareTo(b.Dist));
            foreach (var c in _caps)
            {
                var v = Session.I.World.ViewOf(c.Actor); if (v == null) continue;
                var head = v.HeadPos + Vector3.up * 0.34f;
                c.Dist = Vector3.Distance(camPos, head);
                var sp = cam.WorldToScreenPoint(head);
                bool gone = sp.z < 0.2f || c.Dist > HearRange;
                var target = new Vector2(sp.x / k, sp.y / k + 8f);
                float scale = Mathf.Lerp(1f, 0.86f, Mathf.InverseLerp(2f, SeeRange, c.Dist));
                var size = c.Rt.sizeDelta * scale;
                target.x = Mathf.Clamp(target.x, size.x * 0.5f + 20f, canvas.x - size.x * 0.5f - 20f);
                target.y = Mathf.Clamp(target.y, 60f, canvas.y - size.y - 40f);
                if (first.HasValue)
                {
                    var r = new Rect(target.x - size.x * 0.5f, target.y, size.x, size.y + 20f);
                    if (r.Overlaps(first.Value)) target.y = first.Value.yMax + 6f;
                }
                if (!c.Placed) { c.Pos = target; c.Placed = true; }
                c.Pos = Vector2.SmoothDamp(c.Pos, target, ref c.Vel, 0.08f, Mathf.Infinity, dt);
                float age = now - c.Born, left = c.Until + 0.35f - now;
                float enter = Mathf.Clamp01(age / 0.22f), exit = Mathf.Clamp01(left / 0.35f);
                float rise = (1f - Ease(enter)) * -8f + (1f - exit) * 6f;
                c.Rt.anchoredPosition = c.Pos + new Vector2(0, rise);
                c.Rt.localScale = Vector3.one * scale;
                c.Body.maxVisibleCharacters = Mathf.Min(c.Chars, Mathf.CeilToInt(age * RevealCps) + 1);
                float distA = Mathf.Clamp01(1.25f - c.Dist / (SeeRange * 1.2f));
                c.Cg.alpha = (gone ? 0f : 1f) * Mathf.Min(Ease(enter), exit) * Mathf.Max(0.45f, distA);
                if (!first.HasValue) first = new Rect(c.Pos.x - size.x * 0.5f, c.Pos.y, size.x, size.y + 20f);
            }
            if (_ping != null)
            {
                // the ping sits at the screen edge on the side the voice comes from
                var v = Session.I.World.ViewOf(_ping.Actor);
                if (v != null)
                {
                    var local = cam.transform.InverseTransformPoint(v.HeadPos);
                    var dir2 = new Vector2(local.x, local.z < 0 ? -Mathf.Abs(local.y) - 1f : local.y); if (dir2.sqrMagnitude < 0.01f) dir2 = Vector2.down;
                    dir2.Normalize();
                    var c = canvas * 0.5f; var ext = c - new Vector2(140f, 70f);
                    float t = Mathf.Min(ext.x / Mathf.Max(0.001f, Mathf.Abs(dir2.x)), ext.y / Mathf.Max(0.001f, Mathf.Abs(dir2.y)));
                    _ping.Rt.anchoredPosition = c + dir2 * t;
                }
                float age = now - _ping.Born, left = _ping.Until + 0.3f - now;
                _ping.Cg.alpha = Mathf.Min(Mathf.Clamp01(age / 0.2f), Mathf.Clamp01(left / 0.3f)) * 0.9f;
            }
        }

        static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
    }
}
