using System;
using System.Collections.Generic;
using UnityEngine;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// The moving parts of a container (cabinet doors, drawers, a chest lid), built by FurnitureFactory as separate child
    /// transforms named "Door_L" / "Door_R" (hinged on the furniture's local -X / +X edge), "Lid" (hinged along the back
    /// top edge) and "Drawer_0".."Drawer_n" (sliding out along local +Z). Each part lists "Slot_i" anchors where an item
    /// can be shown: slots on a shelf behind doors belong to the door(s) in front of them; slots inside a drawer are children
    /// of the drawer and ride out with it. Parts have no colliders (an open door never shoves the player); the furniture's
    /// own box collider stays on the carcass.
    /// Drive it with Open(i) / Close(i) / Toggle(i); it animates for ~0.3 s and raises Changed.
    /// </summary>
    public sealed class OpenableParts : MonoBehaviour
    {
        public enum PartKind { Door, Drawer, Lid }

        [Serializable]
        public sealed class Part
        {
            public string Name;
            public PartKind Kind;
            /// <summary>The moving transform (its origin is the hinge / the closed drawer position).</summary>
            public Transform Pivot;
            /// <summary>Hinge axis (door, lid) or slide direction (drawer), in the parent's local space.</summary>
            public Vector3 Axis = Vector3.up;
            /// <summary>Signed opening angle in degrees (door, lid) or distance in metres (drawer).</summary>
            public float Amount = 100f;
            /// <summary>Where an item can be shown for this part (shelf spots behind a door, the floor of a drawer).</summary>
            public List<Transform> Slots = new List<Transform>();
            public bool IsOpen;
            [NonSerialized] public float T;
            [NonSerialized] public Vector3 P0;
            [NonSerialized] public Quaternion R0;
            [NonSerialized] public bool Init;
        }

        public List<Part> Parts = new List<Part>();
        /// <summary>Glazed front: what is inside can be seen with the doors shut (vitrine, display cabinet, medicine cabinet).</summary>
        public bool GlassFront;
        /// <summary>Opening speed (1 / seconds).</summary>
        public float Speed = 3.2f;
        /// <summary>Middle of the interior (local) and its size: a faint warm bounce light lives there while anything is
        /// open, so what is revealed can be seen. No light exists at all until the first opening; it is off when shut.</summary>
        public Vector3 InteriorCenter = new Vector3(0, 0.5f, 0);
        public float InteriorRange = 1.2f;
        public float InteriorIntensity = 0.42f;
        Light _bounce;

        /// <summary>(this, part index, now open)</summary>
        public event Action<OpenableParts, int, bool> Changed;

        public int Count => Parts.Count;
        public bool IsOpen(int i) => i >= 0 && i < Parts.Count && Parts[i].IsOpen;
        public bool AnyOpen { get { foreach (var p in Parts) if (p.IsOpen) return true; return false; } }
        public int IndexOf(string name) { for (int i = 0; i < Parts.Count; i++) if (Parts[i].Name == name) return i; return -1; }
        public IReadOnlyList<Transform> SlotsOf(int i) => i >= 0 && i < Parts.Count ? Parts[i].Slots : (IReadOnlyList<Transform>)Array.Empty<Transform>();

        /// <summary>All slot anchors of the piece, in part order.</summary>
        public List<Transform> AllSlots() { var l = new List<Transform>(); foreach (var p in Parts) foreach (var s in p.Slots) if (s != null && !l.Contains(s)) l.Add(s); return l; }

        /// <summary>The part whose front is nearest a world point (e.g. where the player's ray hit the furniture).</summary>
        public int PartNearest(Vector3 world)
        {
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < Parts.Count; i++)
            {
                var p = Parts[i]; if (p.Pivot == null) continue;
                var r = p.Pivot.GetComponentInChildren<Renderer>();
                var c = r != null ? r.bounds.center : p.Pivot.position;
                float d = (c - world).sqrMagnitude; if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        public void Open(int i) => Set(i, true);
        public void Close(int i) => Set(i, false);
        public void Toggle(int i) { if (i >= 0 && i < Parts.Count) Set(i, !Parts[i].IsOpen); }
        public void OpenAll() { for (int i = 0; i < Parts.Count; i++) Set(i, true); }
        public void CloseAll() { for (int i = 0; i < Parts.Count; i++) Set(i, false); }

        /// <summary>Jump straight to a state (loading a save, offline renders).</summary>
        public void Snap(int i, bool open)
        {
            if (i < 0 || i >= Parts.Count) return;
            var p = Parts[i]; Prepare(p);
            if (open && !p.IsOpen) Set(i, true); else p.IsOpen = open;
            p.T = open ? 1f : 0f; Apply(p);
            if (_bounce != null) { float mx = 0f; foreach (var q in Parts) mx = Mathf.Max(mx, q.T); _bounce.intensity = InteriorIntensity * mx; _bounce.enabled = mx > 0.001f; }
        }

        void Set(int i, bool open)
        {
            if (i < 0 || i >= Parts.Count) return;
            var p = Parts[i]; Prepare(p);
            if (p.IsOpen == open) return;
            p.IsOpen = open; enabled = true;
            if (open && _bounce == null && InteriorRange > 0f)
            {
                var go = new GameObject("InteriorBounce"); go.transform.SetParent(transform, false); go.transform.localPosition = InteriorCenter + Vector3.forward * 0.45f;   // from just outside the opening: a soft fill, no hot spots on the leaves
                _bounce = go.AddComponent<Light>(); _bounce.type = LightType.Point; _bounce.range = InteriorRange + 0.6f; _bounce.color = new Color(1f, 0.84f, 0.64f);
                _bounce.intensity = 0f; _bounce.shadows = LightShadows.None; _bounce.renderMode = LightRenderMode.ForcePixel;
            }
            if (_bounce != null) _bounce.enabled = true;
            Changed?.Invoke(this, i, open);
        }

        static void Prepare(Part p)
        {
            if (p.Init || p.Pivot == null) return;
            p.P0 = p.Pivot.localPosition; p.R0 = p.Pivot.localRotation; p.Init = true;
        }

        static void Apply(Part p)
        {
            if (p.Pivot == null) return;
            float e = p.T * p.T * (3f - 2f * p.T);     // eased
            if (p.Kind == PartKind.Drawer) p.Pivot.localPosition = p.P0 + p.Axis.normalized * (p.Amount * e);
            else p.Pivot.localRotation = p.R0 * Quaternion.AngleAxis(p.Amount * e, p.Axis);
        }

        void Awake() { foreach (var p in Parts) Prepare(p); enabled = false; }

        void Update()
        {
            bool moving = false; float step = Time.deltaTime * Speed;
            foreach (var p in Parts)
            {
                float target = p.IsOpen ? 1f : 0f;
                if (Mathf.Approximately(p.T, target)) continue;
                p.T = Mathf.MoveTowards(p.T, target, step); Apply(p); moving = true;
            }
            if (_bounce != null)
            {
                float mx = 0f; foreach (var p in Parts) mx = Mathf.Max(mx, p.T);
                _bounce.intensity = InteriorIntensity * mx; if (mx <= 0.001f) _bounce.enabled = false;
            }
            if (!moving) enabled = false;
        }

        /// <summary>True when 't' is (or is under) one of the moving parts: such meshes must never be static-batched.</summary>
        public bool IsMoving(Transform t)
        {
            for (var x = t; x != null && x != transform; x = x.parent)
                foreach (var p in Parts) if (p.Pivot == x) return true;
            return false;
        }
    }
}
