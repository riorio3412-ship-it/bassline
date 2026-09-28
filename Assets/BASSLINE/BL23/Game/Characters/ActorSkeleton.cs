using UnityEngine;

namespace BL23.Game.Characters
{
    /// <summary>
    /// Common humanoid bone set shared by every BL23 actor (procedural or auto-rigged GLB).
    /// The first 21 bones are the core set; UpperChest and the full hands (thumb 3 + four fingers x 3 segments per hand)
    /// were appended later so the indices of the core bones never change. Rigs baked before that simply have no
    /// transform for the extended bones (see <see cref="ActorSkeleton.CoreCount"/>).
    /// </summary>
    public enum HBone
    {
        Hips, Spine, Chest, Neck, Head,
        ShoulderL, UpperArmL, LowerArmL, HandL,
        ShoulderR, UpperArmR, LowerArmR, HandR,
        UpperLegL, LowerLegL, FootL, ToeL,
        UpperLegR, LowerLegR, FootR, ToeR,
        UpperChest,
        ThumbL1, ThumbL2, ThumbL3, IndexL1, IndexL2, IndexL3, MiddleL1, MiddleL2, MiddleL3, RingL1, RingL2, RingL3, LittleL1, LittleL2, LittleL3,
        ThumbR1, ThumbR2, ThumbR3, IndexR1, IndexR2, IndexR3, MiddleR1, MiddleR2, MiddleR3, RingR1, RingR2, RingR3, LittleR1, LittleR2, LittleR3
    }

    public static class ActorSkeleton
    {
        public const int Count = 52;
        public const int CoreCount = 21;
        public const int FingersPerHand = 15;     // 5 digits x 3 segments

        public static readonly string[] Names = MakeNames();

        static string[] MakeNames()
        {
            var n = new string[Count];
            for (int i = 0; i < Count; i++) n[i] = ((HBone)i).ToString();
            return n;
        }

        /// <summary>Parents with UpperChest present (neck and clavicles hang from it).</summary>
        public static readonly int[] Parent = MakeParents();

        static int[] MakeParents()
        {
            var p = new int[Count];
            int[] core =
            {
                -1, 0, 1, 21, 3,
                21, 5, 6, 7,
                21, 9, 10, 11,
                0, 13, 14, 15,
                0, 17, 18, 19,
                2,
            };
            for (int i = 0; i < core.Length; i++) p[i] = core[i];
            for (int s = 0; s < 2; s++)
            {
                int hand = s == 0 ? (int)HBone.HandL : (int)HBone.HandR, b0 = s == 0 ? (int)HBone.ThumbL1 : (int)HBone.ThumbR1;
                for (int d = 0; d < 5; d++)
                    for (int k = 0; k < 3; k++)
                        p[b0 + d * 3 + k] = k == 0 ? hand : b0 + d * 3 + k - 1;
            }
            return p;
        }

        /// <summary>Parents in the core (21-bone) layout.</summary>
        public static int CoreParent(int i) => i == 3 || i == 5 || i == 9 ? 2 : Parent[i];

        /// <summary>Parents-first evaluation order (UpperChest precedes Neck / the clavicles).</summary>
        public static readonly int[] Order = MakeOrder();

        static int[] MakeOrder()
        {
            var o = new System.Collections.Generic.List<int>();
            var done = new bool[Count];
            while (o.Count < Count)
                for (int i = 0; i < Count; i++)
                    if (!done[i] && (Parent[i] < 0 || done[Parent[i]])) { done[i] = true; o.Add(i); }
            return o.ToArray();
        }

        public static bool IsFinger(HBone b) => b >= HBone.ThumbL1;
        public static bool IsFingerL(HBone b) => b >= HBone.ThumbL1 && b <= HBone.LittleL3;
        /// <summary>First bone of digit d (0 thumb, 1 index, 2 middle, 3 ring, 4 little) of a hand.</summary>
        public static HBone Digit(bool left, int d, int segment = 0) => (left ? HBone.ThumbL1 : HBone.ThumbR1) + d * 3 + segment;
        public static int DigitOf(HBone b) => IsFinger(b) ? ((int)b - (IsFingerL(b) ? (int)HBone.ThumbL1 : (int)HBone.ThumbR1)) / 3 : -1;
        public static int SegmentOf(HBone b) => IsFinger(b) ? ((int)b - (IsFingerL(b) ? (int)HBone.ThumbL1 : (int)HBone.ThumbR1)) % 3 : -1;

        /// <summary>Core bone that carries an extended bone (hit regions, wounds).</summary>
        public static HBone Core(HBone b)
        {
            if (b == HBone.UpperChest) return HBone.Chest;
            if (IsFingerL(b)) return HBone.HandL;
            if (b >= HBone.ThumbR1) return HBone.HandR;
            return b;
        }

        /// <summary>Direction of the bone (joint -> child joint) in the canonical "I-pose" (arms hanging straight down).</summary>
        public static Vector3 CanonicalDir(HBone b)
        {
            switch (b)
            {
                case HBone.Spine: case HBone.Chest: case HBone.UpperChest: case HBone.Neck: case HBone.Head: case HBone.Hips: return Vector3.up;
                case HBone.ShoulderL: return Vector3.left;
                case HBone.ShoulderR: return Vector3.right;
                case HBone.UpperArmL: case HBone.LowerArmL: case HBone.HandL:
                case HBone.UpperArmR: case HBone.LowerArmR: case HBone.HandR:
                case HBone.UpperLegL: case HBone.LowerLegL: case HBone.UpperLegR: case HBone.LowerLegR:
                    return Vector3.down;
                default: return Vector3.forward; // feet / toes keep their rest orientation
            }
        }

        public static bool IsLeft(HBone b) => b == HBone.ShoulderL || b == HBone.UpperArmL || b == HBone.LowerArmL || b == HBone.HandL || b == HBone.UpperLegL || b == HBone.LowerLegL || b == HBone.FootL || b == HBone.ToeL || IsFingerL(b);

        public static HBone Mirror(HBone b)
        {
            switch (b)
            {
                case HBone.ShoulderL: return HBone.ShoulderR; case HBone.ShoulderR: return HBone.ShoulderL;
                case HBone.UpperArmL: return HBone.UpperArmR; case HBone.UpperArmR: return HBone.UpperArmL;
                case HBone.LowerArmL: return HBone.LowerArmR; case HBone.LowerArmR: return HBone.LowerArmL;
                case HBone.HandL: return HBone.HandR; case HBone.HandR: return HBone.HandL;
                case HBone.UpperLegL: return HBone.UpperLegR; case HBone.UpperLegR: return HBone.UpperLegL;
                case HBone.LowerLegL: return HBone.LowerLegR; case HBone.LowerLegR: return HBone.LowerLegL;
                case HBone.FootL: return HBone.FootR; case HBone.FootR: return HBone.FootL;
                case HBone.ToeL: return HBone.ToeR; case HBone.ToeR: return HBone.ToeL;
            }
            if (IsFingerL(b)) return b + FingersPerHand;
            if (b >= HBone.ThumbR1) return b - FingersPerHand;
            switch (b)
            {
            }
            return b;
        }

        public static int LayerHitbox
        {
            get
            {
                int l = LayerMask.NameToLayer("Hitbox");
                return l >= 0 ? l : 8; // documented fallback: layer 8
            }
        }
    }

    /// <summary>
    /// Anime-proportioned body measurements (meters, actor root space: feet at y=0, facing +Z, character left = -X).
    /// Shared by the procedural baker and the runtime fallback actor.
    /// </summary>
    public sealed class BodyProportions
    {
        public float H, HH; // total height, head height (chin..crown)
        public bool Female;
        public float Build, Shoulders, HeadScale;
        public Vector3[] Joint = new Vector3[ActorSkeleton.Count];
        public Vector3 HeadCenter, HeadTop, HandTipL, HandTipR, ToeTipL, ToeTipR, EyeCenter, ChinPos;
        // radii / widths
        public float NeckR, UpperArmR, ElbowR, WristR, ThighR, KneeR, CalfR, AnkleR;
        public float ChestHalfW, ChestHalfD, WaistHalfW, WaistHalfD, HipHalfW, HipHalfD, ShoulderHalfW, BustR;
        public float FootLen, FootHalfW, HandLen;
        public float ArmRestAngle = 11f; // degrees out from vertical in the bind pose

        public Vector3 J(HBone b) => Joint[(int)b];

        /// <summary>Finger tips (rest, root space): digits 0..4 (thumb .. little) of the left hand, then the right hand.</summary>
        public Vector3[] FingerTip = new Vector3[10];

        /// <summary>
        /// Places the 15 finger joints of a hand from its wrist and fingertip (generic hand: palm facing the body, thumb
        /// forward). Scanned and modelled hands overwrite them with measured positions.
        /// </summary>
        public void PlaceHand(bool left)
        {
            int h = left ? (int)HBone.HandL : (int)HBone.HandR;
            Vector3 w = Joint[h], tip = left ? HandTipL : HandTipR;
            Vector3 a = tip - w; float len = a.magnitude; a /= Mathf.Max(1e-5f, len);
            Vector3 fwd = Vector3.ProjectOnPlane(Vector3.forward, a).normalized;
            Vector3 med = Vector3.ProjectOnPlane(new Vector3(left ? 1f : -1f, 0f, 0f), a).normalized; // palm side
            int b0 = left ? (int)HBone.ThumbL1 : (int)HBone.ThumbR1, t0 = left ? 0 : 5;
            // thumb: CMC, MCP, IP, tip
            Joint[b0] = w + a * (0.12f * len) + fwd * (0.1f * len) + med * (0.08f * len);
            Joint[b0 + 1] = w + a * (0.3f * len) + fwd * (0.21f * len) + med * (0.12f * len);
            Joint[b0 + 2] = w + a * (0.43f * len) + fwd * (0.25f * len) + med * (0.14f * len);
            FingerTip[t0] = w + a * (0.56f * len) + fwd * (0.27f * len) + med * (0.15f * len);
            // fingers: MCP along the knuckle line, then PIP / DIP / tip
            float[] lat = { 0.105f, 0.035f, -0.035f, -0.1f }, flen = { 0.48f, 0.52f, 0.48f, 0.39f }, base0 = { 0.5f, 0.52f, 0.5f, 0.47f };
            for (int f = 0; f < 4; f++)
            {
                Vector3 mcp = w + a * (base0[f] * len) + fwd * (lat[f] * len);
                Vector3 dir = (a + fwd * lat[f] * 0.25f).normalized;
                float L = flen[f] * len;
                int b = b0 + (f + 1) * 3;
                Joint[b] = mcp; Joint[b + 1] = mcp + dir * (0.45f * L); Joint[b + 2] = mcp + dir * (0.75f * L);
                FingerTip[t0 + f + 1] = mcp + dir * L;
            }
        }

        public static BodyProportions Make(float heightM, bool female, float build, float shoulders, float headScale)
        {
            var p = new BodyProportions();
            p.H = heightM; p.Female = female; p.Build = build; p.Shoulders = shoulders; p.HeadScale = headScale <= 0 ? 1f : headScale;
            float H = heightM;
            float hh = H / (female ? 6.75f : 7.05f) * p.HeadScale;
            p.HH = hh;
            float crown = H;
            float chin = H - hh;
            p.HeadCenter = new Vector3(0, H - hh * 0.5f, 0.0f);
            p.HeadTop = new Vector3(0, H, 0);
            p.ChinPos = new Vector3(0, chin + hh * 0.05f, hh * 0.12f);
            p.EyeCenter = new Vector3(0, chin + hh * 0.355f, hh * 0.40f);

            float shoulderY = H - hh * 1.40f;
            float sw = (female ? 0.083f : 0.097f) * H * (0.88f + 0.28f * shoulders) * (0.96f + 0.08f * build);
            p.ShoulderHalfW = sw;

            float hipsY = H * (female ? 0.528f : 0.535f);
            float legTop = H * (female ? 0.492f : 0.497f);
            float kneeY = H * 0.272f;
            float ankleY = H * 0.043f;
            float hipX = H * (female ? 0.054f : 0.051f) * (0.95f + 0.12f * build);

            p.Joint[(int)HBone.Hips] = new Vector3(0, hipsY, 0);
            p.Joint[(int)HBone.Spine] = new Vector3(0, H * 0.605f, -0.005f);
            p.Joint[(int)HBone.Chest] = new Vector3(0, H * 0.690f, -0.005f);
            p.Joint[(int)HBone.Neck] = new Vector3(0, shoulderY + hh * 0.12f, -0.012f);
            p.Joint[(int)HBone.Head] = new Vector3(0, chin + hh * 0.10f, -hh * 0.04f);

            for (int s = 0; s < 2; s++)
            {
                float sx = s == 0 ? -1f : 1f; // L = -X
                int sh = s == 0 ? (int)HBone.ShoulderL : (int)HBone.ShoulderR;
                p.Joint[sh] = new Vector3(sx * H * 0.018f, shoulderY + hh * 0.02f, -0.008f);
                Vector3 ua = new Vector3(sx * sw, shoulderY, -0.012f);
                float upperLen = H * 0.170f, lowerLen = H * 0.142f, handLen = H * 0.100f;
                p.HandLen = handLen;
                float a = p.ArmRestAngle * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(sx * Mathf.Sin(a), -Mathf.Cos(a), 0.02f).normalized;
                Vector3 el = ua + dir * upperLen;
                Vector3 wr = el + dir * lowerLen;
                Vector3 tip = wr + dir * handLen;
                p.Joint[sh + 1] = ua; p.Joint[sh + 2] = el; p.Joint[sh + 3] = wr;
                if (s == 0) p.HandTipL = tip; else p.HandTipR = tip;

                int ul = s == 0 ? (int)HBone.UpperLegL : (int)HBone.UpperLegR;
                p.Joint[ul] = new Vector3(sx * hipX, legTop, 0.0f);
                p.Joint[ul + 1] = new Vector3(sx * hipX * 0.97f, kneeY, 0.012f);
                p.Joint[ul + 2] = new Vector3(sx * hipX * 0.95f, ankleY, -0.012f);
                float footLen = H * (female ? 0.132f : 0.140f);
                p.FootLen = footLen;
                p.Joint[ul + 3] = new Vector3(sx * hipX * 0.97f, H * 0.012f, footLen * 0.62f);
                Vector3 toeTip = new Vector3(sx * hipX * 0.98f, 0.01f, footLen * 0.86f);
                if (s == 0) p.ToeTipL = toeTip; else p.ToeTipR = toeTip;
            }

            p.Joint[(int)HBone.UpperChest] = Vector3.Lerp(p.Joint[(int)HBone.Chest], p.Joint[(int)HBone.Neck], 0.45f);
            for (int s = 0; s < 2; s++) p.PlaceHand(s == 0);

            float b = build;
            p.NeckR = hh * (female ? 0.138f : 0.165f) * (0.95f + 0.2f * b);
            p.UpperArmR = H * (female ? 0.026f : 0.031f) * (0.9f + 0.35f * b);
            p.ElbowR = p.UpperArmR * 0.82f;
            p.WristR = p.UpperArmR * 0.62f;
            p.ThighR = H * (female ? 0.048f : 0.047f) * (0.9f + 0.3f * b);
            p.KneeR = p.ThighR * 0.66f;
            p.CalfR = p.ThighR * 0.66f;
            p.AnkleR = p.ThighR * 0.42f;
            p.ChestHalfW = sw * (female ? 0.80f : 0.82f) * (0.95f + 0.1f * b);
            p.ChestHalfD = H * (female ? 0.064f : 0.069f) * (0.9f + 0.3f * b);
            p.WaistHalfW = H * (female ? 0.066f : 0.077f) * (0.85f + 0.45f * b);
            p.WaistHalfD = H * (female ? 0.053f : 0.058f) * (0.85f + 0.45f * b);
            p.HipHalfW = H * (female ? 0.094f : 0.086f) * (0.92f + 0.2f * b);
            p.HipHalfD = H * (female ? 0.066f : 0.062f) * (0.92f + 0.25f * b);
            p.BustR = female ? H * 0.046f * (0.85f + 0.3f * b) : 0f;
            p.FootHalfW = H * (female ? 0.028f : 0.031f);
            return p;
        }
    }
}
