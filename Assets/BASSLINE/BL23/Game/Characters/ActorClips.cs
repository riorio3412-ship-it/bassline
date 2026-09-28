using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BL23.Game.Characters
{
    /// <summary>
    /// Motion-captured / hand-keyed clips retargeted offline (Editor: ClipRetarget) into the canonical pose space of
    /// <see cref="ActorPose"/>. One set per rig convention: "PROC" for the procedural cast, one per scanned actor.
    /// Stored as Resources/Actors/Anim/&lt;set&gt;.bytes (quaternions quantised to 16 bit).
    /// Source clips: Quaternius Universal Animation Library 1 + 2 (CC0 1.0).
    /// </summary>
    public sealed class ActorClipSet
    {
        public sealed class Clip
        {
            public string Name;
            public float Fps;
            public bool Loop;
            public int Frames;
            public float CycleDist;          // metres travelled per loop, divided by the rig's leg length (locomotion)
            public Vector3[] Hips;           // hips offset per frame / leg length
            public Quaternion[] R;           // Frames * ActorSkeleton.Count
            public float Length => Frames > 1 ? (Frames - 1) / Fps : 0f;
        }

        public readonly Dictionary<string, Clip> Clips = new Dictionary<string, Clip>();
        public const int Magic = 0x43414C42; // "BLAC"
        static readonly Dictionary<string, ActorClipSet> Cache = new Dictionary<string, ActorClipSet>();

        public Clip Get(string name) => name != null && Clips.TryGetValue(name, out var c) ? c : null;

        public static ActorClipSet Load(string set)
        {
            if (string.IsNullOrEmpty(set)) return null;
            if (Cache.TryGetValue(set, out var s)) return s;
            var ta = Resources.Load<TextAsset>("Actors/Anim/" + set);
            s = ta != null ? Read(ta.bytes) : null;
            Cache[set] = s;
            return s;
        }

        public static void ClearCache() => Cache.Clear();

        public static ActorClipSet Read(byte[] data)
        {
            var set = new ActorClipSet();
            using (var r = new BinaryReader(new MemoryStream(data)))
            {
                if (r.ReadInt32() != Magic) return null;
                int version = r.ReadInt32();
                int bones = r.ReadInt32();
                int n = r.ReadInt32();
                for (int c = 0; c < n; c++)
                {
                    var clip = new Clip { Name = r.ReadString(), Fps = r.ReadSingle(), Loop = r.ReadByte() != 0, Frames = r.ReadInt32(), CycleDist = r.ReadSingle() };
                    clip.Hips = new Vector3[clip.Frames];
                    clip.R = new Quaternion[clip.Frames * ActorSkeleton.Count];
                    for (int f = 0; f < clip.Frames; f++)
                    {
                        clip.Hips[f] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        for (int b = 0; b < bones; b++)
                        {
                            var q = new Quaternion(r.ReadInt16() / 32767f, r.ReadInt16() / 32767f, r.ReadInt16() / 32767f, r.ReadInt16() / 32767f);
                            // files written with another bone layout keep only the core bones + UpperChest (finger
                            // indices changed between layouts; hands come from the hand-shape channels then)
                            bool keep = b < ActorSkeleton.Count && (bones == ActorSkeleton.Count || b <= (int)HBone.UpperChest);
                            if (b < ActorSkeleton.Count) clip.R[f * ActorSkeleton.Count + b] = keep ? Norm(q) : Quaternion.identity;
                        }
                        for (int b = bones; b < ActorSkeleton.Count; b++) clip.R[f * ActorSkeleton.Count + b] = Quaternion.identity;
                    }
                    set.Clips[clip.Name] = clip;
                }
            }
            return set;
        }

        static Quaternion Norm(Quaternion q)
        {
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return m > 1e-6f ? new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m) : Quaternion.identity;
        }

        public static byte[] Write(IList<Clip> clips)
        {
            var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Magic); w.Write(1); w.Write(ActorSkeleton.Count); w.Write(clips.Count);
                foreach (var c in clips)
                {
                    w.Write(c.Name); w.Write(c.Fps); w.Write((byte)(c.Loop ? 1 : 0)); w.Write(c.Frames); w.Write(c.CycleDist);
                    for (int f = 0; f < c.Frames; f++)
                    {
                        w.Write(c.Hips[f].x); w.Write(c.Hips[f].y); w.Write(c.Hips[f].z);
                        for (int b = 0; b < ActorSkeleton.Count; b++)
                        {
                            var q = c.R[f * ActorSkeleton.Count + b];
                            if (q.w < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                            w.Write((short)Mathf.RoundToInt(Mathf.Clamp(q.x, -1f, 1f) * 32767f)); w.Write((short)Mathf.RoundToInt(Mathf.Clamp(q.y, -1f, 1f) * 32767f));
                            w.Write((short)Mathf.RoundToInt(Mathf.Clamp(q.z, -1f, 1f) * 32767f)); w.Write((short)Mathf.RoundToInt(Mathf.Clamp(q.w, -1f, 1f) * 32767f));
                        }
                    }
                }
            }
            return ms.ToArray();
        }

        /// <summary>Samples a clip at time t (seconds; wraps when looping) into the rotations / hips of a pose. Fingers and
        /// feet targets are left alone (hand shapes come from the finger channels, legs are FK).</summary>
        public static void Sample(Clip c, float t, ActorPose p, float legLen)
        {
            if (c == null || c.Frames == 0) return;
            float len = c.Length;
            if (c.Loop && len > 0f) { t %= len; if (t < 0) t += len; }
            else t = Mathf.Clamp(t, 0f, len);
            float fi = t * c.Fps;
            int f0 = Mathf.Clamp(Mathf.FloorToInt(fi), 0, c.Frames - 1);
            int f1 = Mathf.Min(f0 + 1, c.Frames - 1);
            if (c.Loop && f0 == c.Frames - 1) f1 = 0;
            float k = Mathf.Clamp01(fi - f0);
            int n = ActorSkeleton.Count;
            for (int b = 0; b < n; b++)
            {
                if (ActorSkeleton.IsFinger((HBone)b)) continue;
                p.R[b] = Quaternion.Slerp(c.R[f0 * n + b], c.R[f1 * n + b], k);
            }
            p.HipsOffset = Vector3.Lerp(c.Hips[f0], c.Hips[f1], k) * legLen;
            p.LegIK = 0f;
            p.RootRot = Quaternion.identity;
            p.RootOffset = Vector3.zero;
        }
    }
}
