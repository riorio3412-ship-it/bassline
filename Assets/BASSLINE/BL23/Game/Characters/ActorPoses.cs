using UnityEngine;

namespace BL23.Game.Characters
{
    /// <summary>Procedural pose library (canonical space, degrees). All functions write into an ActorPose.</summary>
    public static class ActorPoses
    {
        static readonly ActorPose _s1 = new ActorPose(), _s2 = new ActorPose(), _s3 = new ActorPose(); // scratch (main thread only)
        public struct Dims
        {
            public float H, LegLen, UpperLeg, LowerLeg, AnkleH, ArmOut, HipJointY;
            public Vector3 RestHips, FootL, FootR, HipL, HipR;
            public bool Female;
            public string Style;

            public Dims(ActorAnimator a, Vector3[] rest, ActorRig rig)
            {
                H = a.Height; LegLen = a.LegLen; UpperLeg = a.UpperLegLen; LowerLeg = a.LowerLegLen; AnkleH = a.AnkleH;
                ArmOut = a.ArmIdleOut; RestHips = rest[(int)HBone.Hips];
                FootL = rest[(int)HBone.FootL]; FootR = rest[(int)HBone.FootR];
                HipL = rest[(int)HBone.UpperLegL]; HipR = rest[(int)HBone.UpperLegR];
                HipJointY = HipL.y;
                Female = rig != null && rig.Female;
                Style = rig != null ? rig.IdleStyle : "";
            }

            public float HipsAboveJoint => RestHips.y - HipJointY;
        }

        static float S(float t) => Mathf.Sin(t);
        static float N(float t, float seed) => Mathf.PerlinNoise(t, seed) * 2f - 1f;
        static float Env(float t, float dur, float a = 0.25f, float b = 0.3f) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / a)) * Mathf.SmoothStep(0, 1, Mathf.Clamp01((dur - t) / b));
        static float Ease(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));

        public static float DefaultGestureDuration(Gesture g)
        {
            switch (g)
            {
                case Gesture.Nod: return 1.0f;
                case Gesture.ShakeHead: return 1.3f;
                case Gesture.Flinch: return 0.8f;
                case Gesture.Surprised: return 1.4f;
                case Gesture.Slam: return 1.1f;
                case Gesture.Shrug: return 1.5f;
                case Gesture.Bow: return 1.8f;
                case Gesture.Clap: return 1.8f;
                case Gesture.Point: return 2.0f;
                case Gesture.Wave: return 2.0f;
                case Gesture.Present: return 2.0f;
                case Gesture.Laugh: return 2.2f;
                case Gesture.Talk: case Gesture.TalkEmphatic: return 2.6f;
                case Gesture.LookAround: return 3.2f;
                case Gesture.Cry: return 3.5f;
                default: return ActorPosesExt.GestureDuration(g);
            }
        }

        public static float DefaultActionDuration(ActionAnim a)
        {
            switch (a)
            {
                case ActionAnim.PickUp: case ActionAnim.PutDown: return 1.4f;
                case ActionAnim.Knock: return 1.2f;
                case ActionAnim.OpenDoor: return 1.4f;
                case ActionAnim.Stab: return 0.9f;
                case ActionAnim.Slash: return 0.9f;
                case ActionAnim.Overhead: return 1.2f;
                case ActionAnim.Shove: return 0.8f;
                case ActionAnim.Fall: return 1.1f;
                case ActionAnim.Stagger: return 1.2f;
                case ActionAnim.Hurt: return 0.8f;
                case ActionAnim.Drink: return 1.8f;
                case ActionAnim.Use: return 1.2f;
                case ActionAnim.Throw: return 1.1f;
                case ActionAnim.Examine: return 2.5f;
                default: return ActorPosesExt.ActionDuration(a);
            }
        }

        // ================================================================ idle
        public static void Idle(ActorPose p, Dims d, float t, string style, float mobility)
        {
            float breath = S(t * 1.6f);
            float sway = S(t * 0.55f) * 0.6f + N(t * 0.15f, 3f) * 0.4f;
            p.HipsOffset = new Vector3(sway * 0.013f, -0.006f + breath * 0.0015f, 0f);
            p.R[0] = ActorPose.Body(1.5f, sway * 2.5f, -sway * 2.2f);
            p.LegIK = 1f;
            float footSpread = d.Female ? 0.0f : 0.012f;
            p.FootL = d.FootL + new Vector3(-footSpread, 0, 0.01f);
            p.FootR = d.FootR + new Vector3(footSpread, 0, -0.02f);
            p.FootRotL = ActorPose.E(0, -7f, 0);
            p.FootRotR = ActorPose.E(0, 9f, 0);
            p.R[(int)HBone.Spine] = ActorPose.Body(-1.5f + breath * 0.6f, -sway * 1.5f, sway * 1.2f);
            p.R[(int)HBone.Chest] = ActorPose.Body(breath * 1.2f, -sway, sway * 0.8f);
            p.R[(int)HBone.Neck] = ActorPose.Body(3f, N(t * 0.2f, 5f) * 4f, 0f);
            p.R[(int)HBone.Head] = ActorPose.Body(-2f + N(t * 0.25f, 9f) * 3f, N(t * 0.18f, 11f) * 6f, N(t * 0.13f, 13f) * 2f);
            float ao = d.ArmOut;
            p.SetArm(true, 3f + breath * 0.8f, ao + 1f, 0f, 10f + breath, 10f, 5f);
            p.SetArm(false, 3f + breath * 0.8f, ao + 1f, 0f, 10f + breath, 10f, 5f);
            p.SetShoulder(true, breath * 0.8f); p.SetShoulder(false, breath * 0.8f);
            ApplyStyle(p, d, t, style);
        }

        static void ApplyStyle(ActorPose p, Dims d, float t, string style)
        {
            float breath = S(t * 1.6f);
            float ao = d.ArmOut;
            switch (style)
            {
                case "pockets":
                    p.SetHand(true, 0.5f, 0.55f, 0.6f); p.SetHand(false, 0.5f, 0.55f, 0.6f);
                    p.SetArm(true, -4f, ao + 6f, 30f, 34f, 20f, 10f);
                    p.SetArm(false, -4f, ao + 6f, 30f, 34f, 20f, 10f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(4f, 0, 3f);
                    break;
                case "clasp":
                    p.SetHand(true, 0.3f, 0.35f, 0.4f); p.SetHand(false, 0.3f, 0.35f, 0.4f);
                    p.SetArm(true, 16f, ao - 2f, 52f, 78f, 35f, -8f);
                    p.SetArm(false, 18f, ao - 2f, 50f, 72f, 35f, -8f);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(-2f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(-2f);
                    break;
                case "behind":
                    p.SetHand(true, 0.35f, 0.35f, 0.45f); p.SetHand(false, 0.35f, 0.35f, 0.45f);
                    p.SetArm(true, -22f, ao + 4f, 88f, 78f, 20f, 0f);
                    p.SetArm(false, -22f, ao + 4f, 88f, 74f, 20f, 0f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(-3f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(-3f, 0, -6f);
                    break;
                case "crossed":
                    p.SetHand(true, 0.35f, 0.4f, 0.45f); p.SetHand(false, 0.35f, 0.4f, 0.45f);
                    p.SetArm(true, 18f, ao + 4f, 72f, 112f, 10f, 0f);
                    p.SetArm(false, 22f, ao + 4f, 70f, 104f, 10f, 0f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(-2f);
                    break;
                case "handOnHip":
                    p.SetHand(false, 0.1f, 0.15f, 0.2f);
                    p.SetArm(false, -14f, ao + 34f, 78f, 100f, 10f, 20f);
                    p.HipsOffset += new Vector3(0.018f, 0, 0);
                    p.R[0] *= ActorPose.Body(0, 0, -4f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(0, 0, 3f);
                    break;
                case "cute":
                    p.SetArm(true, 10f, ao - 2f, 50f, 58f, 30f, -10f);
                    p.SetArm(false, 10f, ao - 2f, 50f, 58f, 30f, -10f);
                    p.SetShoulder(true, 5f + breath); p.SetShoulder(false, 5f + breath);
                    p.R[(int)HBone.Head] *= ActorPose.Body(3f, 0f, 7f);
                    p.FootL = d.FootL + new Vector3(0.025f, 0, 0.0f); p.FootR = d.FootR + new Vector3(-0.025f, 0, 0.01f);
                    p.FootRotL = ActorPose.E(0, 12f, 0); p.FootRotR = ActorPose.E(0, -12f, 0);
                    break;
                case "slouch":
                    p.SetShoulder(true, -3f, 7f); p.SetShoulder(false, -3f, 7f);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(5f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(7f);
                    p.R[(int)HBone.Neck] *= ActorPose.Body(3f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(-10f, 0, 4f);
                    p.SetArm(true, 6f, ao + 2f, 48f, 62f, 20f, 10f);
                    p.SetArm(false, 6f, ao + 2f, 48f, 62f, 20f, 10f);
                    break;
                case "stiff":
                    p.SetHand(true, 0.1f, 0.08f, 0.12f); p.SetHand(false, 0.1f, 0.08f, 0.12f);
                    p.FootL += new Vector3(-0.03f, 0, 0.015f); p.FootR += new Vector3(0.03f, 0, 0.015f);
                    p.SetArm(true, 1f, ao + 2f, 0f, 6f, 5f, 0f);
                    p.SetArm(false, 1f, ao + 2f, 0f, 6f, 5f, 0f);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(-2f);
                    break;
                case "swagger":
                    p.SetHand(true, 0.4f, 0.45f, 0.55f); p.SetHand(false, 0.4f, 0.45f, 0.55f);
                    p.FootL += new Vector3(-0.05f, 0, 0.03f); p.FootR += new Vector3(0.05f, 0, -0.02f);
                    p.FootRotL = ActorPose.E(0, -18f, 0); p.FootRotR = ActorPose.E(0, 16f, 0);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(-6f, 6f, 0);
                    p.R[(int)HBone.Head] *= ActorPose.Body(-6f, -6f, -6f);
                    p.SetArm(true, -3f, ao + 12f, 12f, 22f, 15f, 10f);
                    p.SetArm(false, 5f, ao + 12f, 12f, 30f, 15f, 10f);
                    p.SetShoulder(true, 2f, -4f); p.SetShoulder(false, 2f, -4f);
                    break;
                case "lily":
                    p.SetArm(true, 26f, ao - 2f, 55f, 92f, 40f, -10f);
                    p.SetArm(false, 20f, ao - 2f, 52f, 76f, 40f, -10f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(6f, 0, 3f);
                    break;
                case "notebook":
                    p.SetArm(true, 24f, ao + 2f, 45f, 100f, 50f, 0f);
                    p.SetArm(false, 10f, ao + 2f, 20f, 50f, 20f, 0f);
                    break;
                case "clipboard":
                    p.SetArm(true, 2f, ao + 4f, 5f, 12f, 10f, 0f);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(-2.5f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(-1.5f);
                    break;
                case "briefcase":
                    p.SetArm(false, 1f, ao + 7f, 0f, 4f, 0f, 0f);
                    p.SetShoulder(false, -3f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(0, 0, -2f);
                    break;
                case "politician":
                    p.SetHand(true, 0.3f, 0.3f, 0.4f); p.SetHand(false, 0.3f, 0.3f, 0.4f);
                    p.SetArm(true, 14f, ao - 2f, 50f, 70f, 35f, -8f);
                    p.SetArm(false, 16f, ao - 2f, 50f, 64f, 35f, -8f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(-4f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(-3f);
                    break;
                case "calm":
                    p.R[(int)HBone.Spine] *= ActorPose.Body(2f);
                    p.SetArm(true, 4f, ao + 3f, 6f, 16f, 10f, 5f);
                    p.SetArm(false, 4f, ao + 3f, 6f, 16f, 10f, 5f);
                    p.FootL += new Vector3(-0.02f, 0, 0); p.FootR += new Vector3(0.02f, 0, 0);
                    break;
                case "curious":
                    p.SetArm(false, -14f, ao + 34f, 78f, 100f, 10f, 20f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(2f, 8f, 9f);
                    p.HipsOffset += new Vector3(-0.015f, 0, 0);
                    break;
                case "thermos":
                    p.SetArm(true, 8f, ao + 3f, 25f, 40f, 25f, 0f);
                    p.FootL += new Vector3(-0.025f, 0, 0.01f); p.FootR += new Vector3(0.025f, 0, 0.01f);
                    break;
                case "actor":
                    p.SetArm(true, -14f, ao + 34f, 78f, 100f, 10f, 20f);
                    p.HipsOffset += new Vector3(-0.02f, 0, 0);
                    p.R[0] *= ActorPose.Body(0, 0, 4f);
                    p.FootR += new Vector3(0.02f, 0, 0.06f); p.FootRotR = ActorPose.E(0, 25f, 0);
                    p.R[(int)HBone.Head] *= ActorPose.Body(-3f, -5f, -4f);
                    break;
            }
        }

        // ================================================================ locomotion
        public static float StepLength(Dims d, float speed, float runW)
        {
            float walk = d.LegLen * (0.46f + 0.2f * Mathf.Clamp(speed, 0f, 2f));
            float run = d.LegLen * (0.95f + 0.22f * Mathf.Clamp(speed - 2f, 0f, 3f));
            return Mathf.Lerp(walk, run, runW);
        }

        public static void Walk(ActorPose p, Dims d, float phase, float speed, Vector3 dir, float runW, bool limpL, bool limpR, float mobility, float time)
        {
            p.Reset();
            float stepLen = StepLength(d, speed, runW);
            float beta = Mathf.Lerp(0.6f, 0.38f, runW);
            float lift = Mathf.Lerp(0.075f, 0.2f, runW) * (d.H / 1.75f);
            float ph = phase * Mathf.PI * 2f;
            dir.y = 0; if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward; dir.Normalize();
            Vector3 side = new Vector3(dir.z, 0, -dir.x);
            float back = Vector3.Dot(dir, Vector3.forward) < -0.3f ? -1f : 1f; // walking backwards: flip lean

            p.LegIK = 1f;
            FootCycle(p, d, true, phase, stepLen * (limpL ? 0.7f : 1f), beta, lift * (limpL ? 0.4f : 1f), dir, side);
            FootCycle(p, d, false, phase + 0.5f, stepLen * (limpR ? 0.7f : 1f), beta, lift * (limpR ? 0.4f : 1f), dir, side);

            float reach = beta * stepLen;
            float dip = d.LegLen - Mathf.Sqrt(Mathf.Max(0.01f, d.LegLen * d.LegLen - reach * reach * 0.85f)) + 0.004f;
            float bob = Mathf.Lerp(0.016f, 0.035f, runW);
            float bobPhase = runW > 0.5f ? Mathf.Cos(ph * 2f + Mathf.PI) : Mathf.Cos(ph * 2f);
            float limpDip = 0f;
            if (limpL) limpDip += Mathf.Max(0f, Mathf.Cos(ph)) * 0.035f;
            if (limpR) limpDip += Mathf.Max(0f, -Mathf.Cos(ph)) * 0.035f;
            p.HipsOffset = new Vector3(Mathf.Sin(ph) * 0.016f * (1f - runW * 0.5f), -dip - bob * (0.5f + 0.5f * bobPhase) - limpDip - runW * 0.03f, 0f);
            p.HipsOffset += dir * (runW * 0.03f);
            float lean = Mathf.Lerp(3f, 11f, runW) * back + (1f - mobility) * 10f;
            p.R[0] = ActorPose.Body(lean * 0.4f, Mathf.Cos(ph) * Mathf.Lerp(6f, 9f, runW), -Mathf.Sin(ph) * 3f + (limpL ? -4f : 0f) + (limpR ? 4f : 0f));
            p.R[(int)HBone.Spine] = ActorPose.Body(lean * 0.3f, -Mathf.Cos(ph) * 3f, Mathf.Sin(ph) * 1.5f);
            p.R[(int)HBone.Chest] = ActorPose.Body(lean * 0.3f, -Mathf.Cos(ph) * Mathf.Lerp(7f, 13f, runW), Mathf.Sin(ph) * 1.5f);
            p.R[(int)HBone.Neck] = ActorPose.Body(2f - lean * 0.3f, Mathf.Cos(ph) * 3f, 0);
            p.R[(int)HBone.Head] = ActorPose.Body(-1f - lean * 0.3f, Mathf.Cos(ph) * 3f + N(time * 0.3f, 4f) * 4f, -Mathf.Sin(ph) * 1.5f);
            float armA = Mathf.Lerp(17f, 42f, runW) * Mathf.Clamp01(speed / 1.2f + 0.3f);
            float ao = d.ArmOut + runW * 4f;
            float swingL = -Mathf.Cos(ph) * armA, swingR = Mathf.Cos(ph) * armA;
            float elbowBase = Mathf.Lerp(14f, 88f, runW);
            { float c = Mathf.Lerp(0.32f, 0.62f, runW); p.SetHand(true, c * 0.8f, c * 0.9f, c); p.SetHand(false, c * 0.8f, c * 0.9f, c); }
            p.SetArm(true, swingL + 2f, ao, 0f, elbowBase + Mathf.Max(0, swingL) * 0.6f, 12f, 5f);
            p.SetArm(false, swingR + 2f, ao, 0f, elbowBase + Mathf.Max(0, swingR) * 0.6f, 12f, 5f);
            p.SetShoulder(true, runW * 2f, Mathf.Cos(ph) * -3f);
            p.SetShoulder(false, runW * 2f, Mathf.Cos(ph) * 3f);
        }

        static void FootCycle(ActorPose p, Dims d, bool left, float u, float stepLen, float beta, float lift, Vector3 dir, Vector3 side)
        {
            u -= Mathf.Floor(u);
            Vector3 rest = left ? d.FootL : d.FootR;
            float z, y, pitch;
            float reach = beta * stepLen;
            if (u < beta)
            {
                float t = u / beta;
                z = Mathf.Lerp(reach, -reach, t);
                float heel = Mathf.SmoothStep(0f, 1f, (t - 0.65f) / 0.35f);
                pitch = heel * 28f;
                y = heel * 0.035f;
            }
            else
            {
                float t = (u - beta) / (1f - beta);
                float s = Mathf.SmoothStep(0f, 1f, t);
                z = Mathf.Lerp(-reach, reach, s);
                y = lift * Mathf.Sin(t * Mathf.PI) * (t < 0.5f ? 1f : 0.85f);
                pitch = Mathf.Lerp(30f, -14f, Mathf.SmoothStep(0f, 1f, t * 1.2f));
                if (t > 0.85f) pitch = Mathf.Lerp(-14f, 0f, (t - 0.85f) / 0.15f);
            }
            Vector3 pos = rest + dir * z + Vector3.up * y;
            pos += side * (left ? -0.004f : 0.004f);
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg * (Vector3.Dot(dir, Vector3.forward) > -0.3f ? 1f : 0f);
            Quaternion rot = Quaternion.Euler(0, yaw + (left ? -5f : 5f), 0) * Quaternion.AngleAxis(pitch, Vector3.right);
            if (left) { p.FootL = pos; p.FootRotL = rot; } else { p.FootR = pos; p.FootRotR = rot; }
        }

        // ================================================================ postures
        public static void PostureBase(ActorPose p, Dims d, Posture posture, float t, string style)
        {
            p.Reset();
            float breath = S(t * 1.5f);
            float s = d.H / 1.75f;
            switch (posture)
            {
                case Posture.Sit:
                {
                    float seat = 0.45f;
                    float hipJoint = seat + 0.085f * s;
                    float hipsY = hipJoint + d.HipsAboveJoint * 0.9f;
                    p.HipsOffset = new Vector3(0, hipsY - d.RestHips.y, -0.06f - d.RestHips.z);
                    p.R[0] = ActorPose.Body(-14f);
                    p.LegIK = 1f;
                    float knee = d.UpperLeg * 0.97f;
                    p.FootL = new Vector3(d.FootL.x - 0.03f, d.AnkleH, -0.06f + knee + 0.06f);
                    p.FootR = new Vector3(d.FootR.x + 0.03f, d.AnkleH, -0.06f + knee + 0.02f);
                    p.FootRotL = ActorPose.E(0, -6f, 0); p.FootRotR = ActorPose.E(0, 8f, 0);
                    // the hips are rotated back so the thighs point forward; bring the torso up again
                    p.R[(int)HBone.Spine] = ActorPose.Body(9f + breath * 0.5f);
                    p.R[(int)HBone.Chest] = ActorPose.Body(3f + breath);
                    p.R[(int)HBone.Neck] = ActorPose.Body(3f);
                    p.R[(int)HBone.Head] = ActorPose.Body(-3f + N(t * 0.2f, 2f) * 3f, N(t * 0.17f, 5f) * 5f, 0);
                    p.SetArm(true, 30f, d.ArmOut + 2f, 18f, 48f, 35f, 12f);
                    p.SetArm(false, 30f, d.ArmOut + 2f, 18f, 48f, 35f, 12f);
                    break;
                }
                case Posture.Crouch:
                {
                    float hipJoint = d.AnkleH + d.LegLen * 0.45f;
                    p.HipsOffset = new Vector3(0, hipJoint + d.HipsAboveJoint - d.RestHips.y, -0.1f * s);
                    p.R[0] = ActorPose.Body(28f);
                    p.LegIK = 1f;
                    p.FootL = d.FootL + new Vector3(-0.035f, 0, 0.07f); p.FootR = d.FootR + new Vector3(0.035f, 0, 0.03f);
                    p.FootRotL = ActorPose.E(0, -14f, 0); p.FootRotR = ActorPose.E(0, 14f, 0);
                    p.R[(int)HBone.Spine] = ActorPose.Body(12f + breath * 0.5f);
                    p.R[(int)HBone.Chest] = ActorPose.Body(8f);
                    p.R[(int)HBone.Neck] = ActorPose.Body(-18f);
                    p.R[(int)HBone.Head] = ActorPose.Body(-16f);
                    p.SetArm(true, 55f, d.ArmOut + 8f, 20f, 38f, 30f, 10f);
                    p.SetArm(false, 55f, d.ArmOut + 8f, 20f, 38f, 30f, 10f);
                    break;
                }
                case Posture.Kneel:
                {
                    float thigh = 8f;
                    float hipJoint = 0.055f * s + d.UpperLeg * Mathf.Cos(thigh * Mathf.Deg2Rad);
                    p.HipsOffset = new Vector3(0, hipJoint + d.HipsAboveJoint - d.RestHips.y, -0.02f);
                    p.R[0] = ActorPose.Body(3f);
                    p.LegIK = 0f;
                    p.SetLegFK(true, thigh, 3f, 100f, 55f);
                    p.SetLegFK(false, thigh, 3f, 100f, 55f);
                    p.R[(int)HBone.Spine] = ActorPose.Body(2f + breath * 0.5f);
                    p.R[(int)HBone.Chest] = ActorPose.Body(2f + breath);
                    p.R[(int)HBone.Head] = ActorPose.Body(4f);
                    p.SetArm(true, 22f, d.ArmOut + 2f, 10f, 30f, 20f, 5f);
                    p.SetArm(false, 22f, d.ArmOut + 2f, 10f, 30f, 20f, 5f);
                    break;
                }
                case Posture.LieBack: LieBack(p, d, t); break;
                case Posture.LieFront: LieFront(p, d, t); break;
                case Posture.LieSide: LieSide(p, d, t, 0f); break;
                case Posture.Slumped: Slumped(p, d, t, 0f); break;
                default: Idle(p, d, t, style, 1f); break;
            }
        }

        static void LieBack(ActorPose p, Dims d, float t)
        {
            float s = d.H / 1.75f;
            float breath = S(t * 1.2f);
            p.LegIK = 0f;
            p.R[0] = ActorPose.Body(-90f);
            p.HipsOffset = new Vector3(0, 0.11f * s - d.RestHips.y, 0f);
            p.R[(int)HBone.Spine] = ActorPose.Body(-2f + breath * 0.5f);
            p.R[(int)HBone.Chest] = ActorPose.Body(breath * 1.2f);
            p.R[(int)HBone.Neck] = ActorPose.Body(14f);
            p.R[(int)HBone.Head] = ActorPose.Body(6f, 12f, 0f);
            p.SetArm(true, 0f, 14f, 0f, 12f, 30f, 0f);
            p.SetArm(false, 0f, 14f, 0f, 12f, 30f, 0f);
            p.SetLegFK(true, 2f, 4f, 6f, -35f, -12f);
            p.SetLegFK(false, 2f, 4f, 6f, -35f, 12f);
        }

        static void LieFront(ActorPose p, Dims d, float t)
        {
            float s = d.H / 1.75f;
            p.LegIK = 0f;
            p.R[0] = ActorPose.Body(90f);
            p.HipsOffset = new Vector3(0, 0.1f * s - d.RestHips.y, 0f);
            p.R[(int)HBone.Spine] = ActorPose.Body(0f);
            p.R[(int)HBone.Chest] = ActorPose.Body(-3f);
            p.R[(int)HBone.Neck] = ActorPose.Body(-18f, 40f, 0f);
            p.R[(int)HBone.Head] = ActorPose.Body(-8f, 40f, 0f);
            p.SetArm(true, 100f, 30f, -30f, 90f, 0f, 0f);
            p.SetArm(false, 0f, 12f, 0f, 10f, 0f, 0f);
            p.SetLegFK(true, 0f, 8f, 5f, 40f);
            p.SetLegFK(false, 0f, 4f, 10f, 40f);
        }

        static void LieSide(ActorPose p, Dims d, float t, float curl)
        {
            float s = d.H / 1.75f;
            float breath = S(t * 1.1f);
            p.LegIK = 0f;
            p.R[0] = Quaternion.AngleAxis(-90f, Vector3.forward) * ActorPose.Body(-90f);
            p.HipsOffset = new Vector3(0, 0.16f * s - d.RestHips.y, 0f);
            p.R[(int)HBone.Spine] = ActorPose.Body(8f + curl * 15f + breath * 0.5f);
            p.R[(int)HBone.Chest] = ActorPose.Body(6f + curl * 10f + breath);
            p.R[(int)HBone.Neck] = ActorPose.Body(10f + curl * 10f, 0, -12f);
            p.R[(int)HBone.Head] = ActorPose.Body(8f, 0, -10f);
            p.SetArm(true, 60f + curl * 20f, 0f, 20f, 60f + curl * 40f, 10f, 0f);
            p.SetArm(false, 45f + curl * 25f, 5f, 10f, 70f + curl * 40f, 10f, 10f);
            p.SetLegFK(true, 40f + curl * 30f, 0f, 60f + curl * 40f, 20f);
            p.SetLegFK(false, 25f + curl * 35f, 4f, 45f + curl * 50f, 20f);
        }

        static void Slumped(ActorPose p, Dims d, float t, float droop)
        {
            float s = d.H / 1.75f;
            p.LegIK = 0f;
            p.R[0] = ActorPose.Body(-40f);
            p.HipsOffset = new Vector3(0, 0.14f * s - d.RestHips.y, -0.05f);
            p.R[(int)HBone.Spine] = ActorPose.Body(14f);
            p.R[(int)HBone.Chest] = ActorPose.Body(15f + droop * 8f);
            p.R[(int)HBone.Neck] = ActorPose.Body(20f + droop * 20f, 0, 8f + droop * 12f);
            p.R[(int)HBone.Head] = ActorPose.Body(10f + droop * 15f, 5f, 10f + droop * 12f);
            p.SetArm(true, 10f, 18f, 0f, 20f, 30f, 10f);
            p.SetArm(false, 15f, 20f, 0f, 25f, 30f, 10f);
            p.SetLegFK(true, 48f, 12f, 25f, -20f, -15f);
            p.SetLegFK(false, 42f, 8f, 40f, -20f, 15f);
        }

        public static void Unconscious(ActorPose p, Dims d, float t)
        {
            p.R[(int)HBone.Head] *= ActorPose.Body(12f, 0, 10f);
            p.R[(int)HBone.Neck] *= ActorPose.Body(10f);
        }

        // ================================================================ dead
        public static void Dead(ActorPose p, Dims d, int v)
        {
            p.Reset();
            switch (v)
            {
                case 0: // supine, arms thrown out, head turned
                    LieBack(p, d, 0f);
                    p.SetArm(true, 5f, 75f, 20f, 25f, 40f, 10f);
                    p.SetArm(false, 10f, 62f, 20f, 40f, 40f, 15f);
                    p.SetLegFK(true, 6f, 12f, 8f, -35f, -20f);
                    p.SetLegFK(false, 12f, 6f, 25f, -35f, 20f);
                    p.R[(int)HBone.Neck] = ActorPose.Body(12f, 30f, 0f);
                    p.R[(int)HBone.Head] = ActorPose.Body(4f, 35f, 0f);
                    break;
                case 1: // prone, chalk-outline pose
                    LieFront(p, d, 0f);
                    p.SetArm(true, 150f, 25f, -20f, 70f, 0f, 0f);
                    p.SetArm(false, 10f, 25f, 0f, 20f, 0f, 20f);
                    p.SetLegFK(true, 0f, 38f, 28f, 30f, 25f);
                    p.SetLegFK(false, 0f, 6f, 5f, 40f);
                    break;
                case 2: // fetal, on the side
                    LieSide(p, d, 0f, 1f);
                    break;
                case 3: // slumped against the wall, head fallen
                    Slumped(p, d, 0f, 1f);
                    p.SetArm(true, 0f, 25f, 0f, 10f, 40f, 20f);
                    p.SetArm(false, 5f, 30f, 0f, 15f, 40f, 20f);
                    break;
                case 4: // supine, arm across chest, knees fallen aside
                    LieBack(p, d, 0f);
                    p.SetArm(true, 60f, -10f, 50f, 100f, 30f, 0f);
                    p.SetArm(false, 5f, 30f, 0f, 20f, 30f, 10f);
                    p.SetLegFK(true, 55f, -20f, 90f, 0f, -20f);
                    p.SetLegFK(false, 45f, 10f, 80f, 0f, -30f);
                    p.R[(int)HBone.Neck] = ActorPose.Body(-6f, -20f, 0f);
                    p.R[(int)HBone.Head] = ActorPose.Body(-12f, -25f, 0f);
                    break;
                default: // 5: face down, twisted, one arm reaching
                    LieFront(p, d, 0f);
                    p.R[(int)HBone.Spine] = ActorPose.Body(0f, 15f, -8f);
                    p.R[(int)HBone.Chest] = ActorPose.Body(0f, 10f, -5f);
                    p.SetArm(true, 170f, 8f, -10f, 15f, 0f, 20f);
                    p.SetArm(false, 30f, 40f, 0f, 70f, 0f, 0f);
                    p.SetLegFK(true, 30f, 25f, 60f, 30f, 20f);
                    p.SetLegFK(false, -5f, 5f, 12f, 40f);
                    p.R[(int)HBone.Neck] = ActorPose.Body(-15f, 50f, 0f);
                    p.R[(int)HBone.Head] = ActorPose.Body(-6f, 35f, 0f);
                    break;
            }
        }

        // ================================================================ carrying
        public static void Carried(ActorPose p, Dims d, float t, Vector3 hipsTarget)
        {
            p.Reset();
            p.LegIK = 0f;
            // folded over the carrier's shoulder: spine hangs down the carrier's back, belly on the shoulder, legs down the front
            p.R[0] = Quaternion.LookRotation(new Vector3(0f, -0.9f, 0.42f), new Vector3(0f, -0.42f, -0.9f));
            p.RootOffset = hipsTarget - d.RestHips;
            p.R[(int)HBone.Spine] = ActorPose.Body(15f + S(t * 2f) * 2f);
            p.R[(int)HBone.Chest] = ActorPose.Body(10f);
            p.R[(int)HBone.Neck] = ActorPose.Body(10f);
            p.R[(int)HBone.Head] = ActorPose.Body(6f + S(t * 2.3f) * 4f, 20f, 0f);
            p.SetArm(true, 160f + S(t * 2f) * 5f, 6f, 0f, 12f, 0f, 0f);
            p.SetArm(false, 158f + S(t * 2.2f) * 5f, 4f, 0f, 16f, 0f, 0f);
            p.SetLegFK(true, 95f, 4f, 22f + S(t * 2f) * 4f, -20f);
            p.SetLegFK(false, 100f, 6f, 30f + S(t * 2.1f) * 4f, -20f);
        }

        public static void CarryOverlay(ActorPose p, Dims d, float t)
        {
            p.R[(int)HBone.Spine] *= ActorPose.Body(7f);
            p.R[(int)HBone.Chest] *= ActorPose.Body(4f, 0, -4f);
            p.SetArm(false, 72f, -8f, 30f, 118f, 30f, 10f);
            p.SetShoulder(false, 6f, 4f);
            p.HipsOffset += new Vector3(0, -0.02f, 0);
        }

        // ================================================================ injuries
        public static void LimpArm(ActorPose p, bool left, float t)
        {
            int ua = left ? (int)HBone.UpperArmL : (int)HBone.UpperArmR;
            p.R[ua] = ActorPose.Arm(left, -2f + S(t * 1.3f) * 1.5f, 4f, 10f);
            p.R[ua + 1] = ActorPose.Elbow(left, 4f, 30f);
            p.R[ua + 2] = ActorPose.Wrist(left, 15f, 5f);
            p.SetShoulder(left, -6f, 4f);
        }

        public static void Hunch(ActorPose p, float a)
        {
            p.R[(int)HBone.Spine] *= ActorPose.Body(10f * a);
            p.R[(int)HBone.Chest] *= ActorPose.Body(8f * a);
            p.R[(int)HBone.Head] *= ActorPose.Body(-8f * a);
        }

        // ================================================================ gestures
        public static void GesturePose(ActorPose p, Dims d, Gesture g, float t, float dur, float time)
        {
            float ao = d.ArmOut;
            switch (g)
            {
                case Characters.Gesture.Talk:
                    p.SetHand(false, 0.12f, 0.1f, 0.2f); p.SoftHand(true);
                    p.SetArm(false, 22f + 8f * S(time * 2.1f), ao + 6f, 10f, 68f + 16f * S(time * 1.7f + 1f), -40f + 20f * S(time * 1.3f), -10f);
                    p.SetArm(true, 10f + 5f * S(time * 1.5f + 2f), ao + 3f, 5f, 35f + 10f * S(time * 1.9f), 20f, 0f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(3f * S(time * 3.1f), 4f * S(time * 0.9f), 2f * S(time * 1.1f));
                    p.R[(int)HBone.Chest] *= ActorPose.Body(0, 4f * S(time * 0.8f), 0);
                    break;
                case Characters.Gesture.TalkEmphatic:
                    p.SetHand(false, 0.08f, 0.06f, 0.14f); p.SetHand(true, 0.08f, 0.06f, 0.14f);
                    p.SetArm(false, 38f + 14f * S(time * 3.1f), ao + 14f, 10f, 75f + 20f * S(time * 2.6f), -70f, -15f);
                    p.SetArm(true, 32f + 14f * S(time * 3.1f + 1.4f), ao + 14f, 10f, 75f + 20f * S(time * 2.3f + 1f), -70f, -15f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(6f + 3f * S(time * 3.1f), 6f * S(time * 1.2f), 0);
                    p.R[(int)HBone.Head] *= ActorPose.Body(4f * S(time * 3.1f), 0, 0);
                    break;
                case Characters.Gesture.Point:
                    p.PointHand(false); p.SoftHand(true);
                    p.SetArm(false, 84f, 8f, 0f, 6f, -10f, -10f);
                    p.SetArm(true, 8f, ao + 3f, 0f, 22f, 10f, 0f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(3f, 12f, 0);
                    p.R[(int)HBone.Head] *= ActorPose.Body(-3f, -6f, 0);
                    break;
                case Characters.Gesture.Think:
                    p.SetHand(false, 0.45f, 0.25f, 0.62f); p.SoftHand(true);
                    p.SetArm(false, 32f, -4f, 42f, 135f, 60f, 20f);
                    p.SetArm(true, 26f, -8f, 45f, 96f, 30f, 0f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(6f, -4f, 7f);
                    break;
                case Characters.Gesture.FingerToLips:
                    // index finger raised to the lips (right hand), the others curled, head tilted; a slow smug sway
                    p.SetHand(false, 0.55f, 0.0f, 0.9f); p.SoftHand(true);
                    p.SetArm(false, 64f, -14f, 34f, 158f, 80f, -22f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(5f, -6f, -9f + 1.5f * Mathf.Sin(time * 0.8f));
                    p.R[(int)HBone.Neck] *= ActorPose.Body(2f, -2f, -3f);
                    break;
                case Characters.Gesture.CrossArms:
                    p.SetHand(true, 0.35f, 0.4f, 0.45f); p.SetHand(false, 0.35f, 0.4f, 0.45f);
                    p.SetArm(true, 24f, ao - 8f, 55f, 110f, 10f, 0f);
                    p.SetArm(false, 28f, ao - 8f, 55f, 102f, 10f, 0f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(-3f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(-3f, 0, -3f);
                    break;
                case Characters.Gesture.Shrug:
                {
                    p.OpenHand(true); p.OpenHand(false);
                    float k = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
                    p.SetShoulder(true, 14f * k); p.SetShoulder(false, 14f * k);
                    p.SetArm(true, 18f, ao + 24f * k, 0f, 85f * k + 10f, -80f * k, -20f * k);
                    p.SetArm(false, 18f, ao + 24f * k, 0f, 85f * k + 10f, -80f * k, -20f * k);
                    p.R[(int)HBone.Head] *= ActorPose.Body(0, 0, 10f * k);
                    break;
                }
                case Characters.Gesture.Nod:
                    p.R[(int)HBone.Head] *= ActorPose.Body(14f * Mathf.Max(0f, S(t * Mathf.PI * 2f * 1.6f)) * Env(t, dur, 0.1f, 0.2f));
                    p.R[(int)HBone.Neck] *= ActorPose.Body(5f * Mathf.Max(0f, S(t * Mathf.PI * 2f * 1.6f)));
                    break;
                case Characters.Gesture.ShakeHead:
                    p.R[(int)HBone.Head] *= ActorPose.Body(3f, 24f * S(t * Mathf.PI * 2f * 1.7f) * Env(t, dur, 0.1f, 0.25f), 0);
                    p.R[(int)HBone.Neck] *= ActorPose.Body(0, 8f * S(t * Mathf.PI * 2f * 1.7f), 0);
                    break;
                case Characters.Gesture.HandOnChest:
                    p.SetHand(false, 0.1f, 0.08f, 0.12f);
                    p.SetArm(false, 34f, -12f, 50f, 118f, 30f, 10f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(-2f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(4f, 0, 4f);
                    break;
                case Characters.Gesture.Wave:
                    p.OpenHand(false);
                    p.SetArm(false, 18f, 128f, -10f, 45f + 22f * S(time * 9f), 0f, 0f);
                    p.R[(int)HBone.LowerArmR] = ActorPose.Elbow(false, 40f + 25f * S(time * 9f), 0f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(-2f, -5f, -5f);
                    break;
                case Characters.Gesture.Bow:
                {
                    float k = Env(t, dur, 0.5f, 0.6f);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(22f * k);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(16f * k);
                    p.R[(int)HBone.Neck] *= ActorPose.Body(6f * k);
                    p.SetArm(true, 30f * k + 4f, ao - 2f, 20f, 18f, 10f, 0f);
                    p.SetArm(false, 30f * k + 4f, ao - 2f, 20f, 18f, 10f, 0f);
                    break;
                }
                case Characters.Gesture.Surprised:
                {
                    p.OpenHand(true); p.OpenHand(false);
                    float k = Mathf.Clamp01(t / 0.15f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(-9f * k);
                    p.R[(int)HBone.Head] *= ActorPose.Body(-6f * k);
                    p.SetShoulder(true, 9f * k); p.SetShoulder(false, 9f * k);
                    p.SetArm(true, 42f, ao + 12f, 20f, 115f, -30f, -35f);
                    p.SetArm(false, 42f, ao + 12f, 20f, 115f, -30f, -35f);
                    break;
                }
                case Characters.Gesture.Flinch:
                    p.SetHand(false, 0.2f, 0.1f, 0.15f); p.Fist(true);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(8f, -22f, 0);
                    p.R[(int)HBone.Neck] *= ActorPose.Body(10f, -18f, 0);
                    p.R[(int)HBone.Head] *= ActorPose.Body(8f, -15f, 0);
                    p.SetShoulder(true, 12f); p.SetShoulder(false, 12f);
                    p.SetArm(false, 70f, 10f, 30f, 125f, 20f, 0f);
                    p.SetArm(true, 40f, ao, 30f, 100f, 20f, 0f);
                    break;
                case Characters.Gesture.Cower:
                    p.SetHand(true, 0.6f, 0.7f, 0.8f); p.SetHand(false, 0.6f, 0.7f, 0.8f);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(22f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(15f);
                    p.R[(int)HBone.Neck] *= ActorPose.Body(15f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(15f);
                    p.SetShoulder(true, 10f, 6f); p.SetShoulder(false, 10f, 6f);
                    p.SetArm(true, 120f, 30f, 40f, 120f, 20f, 10f);
                    p.SetArm(false, 125f, 30f, 40f, 115f, 20f, 10f);
                    p.HipsOffset += new Vector3(0, -0.08f, -0.04f);
                    break;
                case Characters.Gesture.Angry:
                    p.Fist(true); p.Fist(false);
                    p.SetShoulder(true, 7f, 5f); p.SetShoulder(false, 7f, 5f);
                    p.SetArm(true, -4f, ao + 8f, 10f, 28f, 10f, 10f);
                    p.SetArm(false, -4f, ao + 8f, 10f, 28f, 10f, 10f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(8f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(-9f + 2f * S(time * 12f));
                    break;
                case Characters.Gesture.Cry:
                    p.SetHand(true, 0.3f, 0.35f, 0.5f); p.SetHand(false, 0.3f, 0.35f, 0.5f);
                    p.SetArm(true, 38f, -6f, 42f, 140f, 50f, 10f);
                    p.SetArm(false, 38f, -6f, 42f, 140f, 50f, 10f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(20f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(8f + 2f * S(time * 14f));
                    p.SetShoulder(true, 4f + 3f * S(time * 14f)); p.SetShoulder(false, 4f + 3f * S(time * 14f));
                    break;
                case Characters.Gesture.Laugh:
                    p.SetHand(false, 0.3f, 0.3f, 0.45f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(-8f + 3f * S(time * 13f));
                    p.R[(int)HBone.Head] *= ActorPose.Body(-10f + 3f * S(time * 13f));
                    p.SetArm(false, 22f, -14f, 30f, 92f, 20f, 0f);
                    p.SetArm(true, 8f, ao + 6f, 0f, 30f, 10f, 0f);
                    break;
                case Characters.Gesture.Listen:
                    p.R[(int)HBone.Head] *= ActorPose.Body(4f, -6f, 11f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(4f);
                    break;
                case Characters.Gesture.LookAround:
                    p.R[(int)HBone.Head] *= ActorPose.Body(-4f, 48f * S(t * Mathf.PI * 2f * 0.33f), 0);
                    p.R[(int)HBone.Neck] *= ActorPose.Body(0, 18f * S(t * Mathf.PI * 2f * 0.33f), 0);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(0, 12f * S(t * Mathf.PI * 2f * 0.33f - 0.3f), 0);
                    break;
                case Characters.Gesture.Present:
                    p.OpenHand(false);
                    p.SetArm(false, 48f, 32f, 0f, 25f, -80f, -10f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(4f, 8f, 0);
                    p.R[(int)HBone.Head] *= ActorPose.Body(2f, -8f, 0);
                    break;
                case Characters.Gesture.Slam:
                {
                    p.Fist(true); p.Fist(false);
                    float up = Ease(t / 0.3f) * (1f - Ease((t - 0.3f) / 0.12f));
                    float down = Ease((t - 0.3f) / 0.12f);
                    float fwd = Mathf.Lerp(Mathf.Lerp(20f, 95f, up), 58f, down);
                    float el = Mathf.Lerp(Mathf.Lerp(30f, 100f, up), 22f, down);
                    p.SetArm(true, fwd, ao + 6f, 0f, el, 60f, -20f);
                    p.SetArm(false, fwd, ao + 6f, 0f, el, 60f, -20f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(-4f * up + 16f * down);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(8f * down);
                    p.R[(int)HBone.Head] *= ActorPose.Body(-12f * down);
                    break;
                }
                case Characters.Gesture.Clap:
                {
                    p.OpenHand(true); p.OpenHand(false);
                    float c = 0.5f + 0.5f * S(time * 16f);
                    p.SetArm(true, 48f, -6f + 10f * c, 60f, 82f, 40f, 0f);
                    p.SetArm(false, 48f, -6f + 10f * c, 60f, 82f, 40f, 0f);
                    break;
                }
                case Characters.Gesture.Pray:
                    p.OpenHand(true); p.OpenHand(false);
                    p.SetArm(true, 30f, -16f, 48f, 124f, 60f, 20f);
                    p.SetArm(false, 30f, -16f, 48f, 124f, 60f, 20f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(16f);
                    break;
                default: ActorPosesExt.GestureExt(p, d, g, t, dur, time); break;
            }
        }

        // ================================================================ actions
        public static void ActionPose(ActorPose p, Dims d, ActionAnim a, float t, float dur, float time, out bool fullBody)
        {
            fullBody = false;
            float ao = d.ArmOut;
            float s = d.H / 1.75f;
            switch (a)
            {
                case ActionAnim.PickUp:
                case ActionAnim.PutDown:
                {
                    fullBody = true;
                    float k = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
                    k = Mathf.SmoothStep(0, 1, Mathf.Min(1f, k * 1.4f));
                    { float g = Mathf.Clamp01((t / dur - 0.35f) / 0.2f); p.SetHand(false, Mathf.Lerp(0.1f, 0.6f, g), Mathf.Lerp(0.08f, 0.65f, g), Mathf.Lerp(0.1f, 0.72f, g)); }
                    float hipJoint = Mathf.Lerp(d.HipJointY - 0.01f, d.AnkleH + d.LegLen * 0.5f, k);
                    p.HipsOffset = new Vector3(0, hipJoint + d.HipsAboveJoint - d.RestHips.y, -0.12f * k * s);
                    p.R[0] = ActorPose.Body(40f * k);
                    p.R[(int)HBone.Spine] = ActorPose.Body(20f * k);
                    p.R[(int)HBone.Chest] = ActorPose.Body(12f * k);
                    p.R[(int)HBone.Neck] = ActorPose.Body(-10f * k);
                    p.LegIK = 1f;
                    p.FootL = d.FootL + new Vector3(-0.02f, 0, 0.02f); p.FootR = d.FootR + new Vector3(0.02f, 0, -0.08f * k);
                    p.FootRotL = ActorPose.E(0, -8f, 0); p.FootRotR = ActorPose.E(0, 10f, 0);
                    p.SetArm(false, Mathf.Lerp(5f, 55f, k), ao, 0f, Mathf.Lerp(12f, 8f, k), 20f, 10f);
                    p.SetArm(true, Mathf.Lerp(5f, 30f, k), ao + 8f * k, 0f, Mathf.Lerp(12f, 40f, k), 20f, 0f);
                    break;
                }
                case ActionAnim.Use:
                    if (dur <= 1.05f)
                    {
                        // short use = pulling a chair out: lean in, both hands on the chair back, draw it toward the body
                        fullBody = true;
                        float u = Mathf.Clamp01(t / dur);
                        float reach = Ease(u / 0.35f), pull = Ease((u - 0.35f) / 0.45f);
                        p.R[(int)HBone.Spine] *= ActorPose.Body(10f * reach - 4f * pull);
                        p.R[(int)HBone.Chest] *= ActorPose.Body(8f * reach - 3f * pull);
                        p.R[(int)HBone.Head] *= ActorPose.Body(14f * reach);
                        p.HipsOffset += new Vector3(0, -0.02f * reach, -0.03f * pull);
                        float fwd = Mathf.Lerp(8f, 46f, reach) - 14f * pull, el = Mathf.Lerp(15f, 38f, reach) + 30f * pull;
                        p.SetArm(false, fwd, ao + 4f, 20f, el, 40f, -10f);
                        p.SetArm(true, fwd - 4f, ao + 4f, 20f, el, 40f, -10f);
                        float g = Ease((u - 0.25f) / 0.15f);
                        p.SetHand(false, Mathf.Lerp(0.15f, 0.6f, g), Mathf.Lerp(0.1f, 0.7f, g), Mathf.Lerp(0.15f, 0.75f, g));
                        p.SetHand(true, Mathf.Lerp(0.15f, 0.6f, g), Mathf.Lerp(0.1f, 0.7f, g), Mathf.Lerp(0.15f, 0.75f, g));
                        break;
                    }
                    p.Grip(false);
                    p.SetArm(false, 45f + 3f * S(time * 6f), ao, 10f, 62f, -10f, 10f * S(time * 6f));
                    p.SetArm(true, 25f, ao, 10f, 45f, 10f, 0f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(12f);
                    break;
                case ActionAnim.Craft:
                    p.SetHand(false, 0.5f, 0.55f, 0.6f); p.SetHand(true, 0.35f, 0.3f, 0.4f);
                    p.SetArm(false, 42f + 4f * S(time * 5f), ao - 4f, 15f, 78f + 8f * S(time * 7f), -10f, 5f * S(time * 5f));
                    p.SetArm(true, 38f, ao - 4f, 15f, 72f, 20f, 0f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(10f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(18f);
                    break;
                case ActionAnim.Throw:
                {
                    fullBody = true;
                    float u = Mathf.Clamp01(t / dur);
                    float wind = Ease(u / 0.4f) * (1f - Ease((u - 0.4f) / 0.15f));
                    float rel = Ease((u - 0.4f) / 0.15f) * (1f - Ease((u - 0.75f) / 0.25f));
                    p.SetArm(false, Mathf.Lerp(Mathf.Lerp(10f, 150f, wind), 80f, rel), ao + 10f, 0f, Mathf.Lerp(Mathf.Lerp(20f, 110f, wind), 10f, rel), 0f, 0f);
                    p.SetArm(true, 30f + 20f * wind, ao + 12f, 0f, 30f, 0f, 0f);
                    p.SetHand(false, rel > 0.3f ? 0.1f : 0.6f, rel > 0.3f ? 0.05f : 0.65f, rel > 0.3f ? 0.1f : 0.7f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(-8f * wind + 14f * rel, -20f * wind + 20f * rel, 0);
                    p.FootR += new Vector3(0, 0, 0.1f * rel);
                    break;
                }
                case ActionAnim.Garden:
                case ActionAnim.Examine:
                case ActionAnim.Search:
                {
                    fullBody = true;
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.5f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dur - t) / 0.5f));
                    float hipJoint = Mathf.Lerp(d.HipJointY - 0.01f, d.AnkleH + d.LegLen * 0.55f, k);
                    p.HipsOffset = new Vector3(0, hipJoint + d.HipsAboveJoint - d.RestHips.y, -0.1f * k * s);
                    p.R[0] = ActorPose.Body(35f * k);
                    p.R[(int)HBone.Spine] = ActorPose.Body(15f * k);
                    p.R[(int)HBone.Chest] = ActorPose.Body(10f * k);
                    p.R[(int)HBone.Neck] = ActorPose.Body(-6f * k);
                    p.LegIK = 1f;
                    p.FootL = d.FootL + new Vector3(-0.03f, 0, 0.03f); p.FootR = d.FootR + new Vector3(0.03f, 0, -0.06f * k);
                    p.SetArm(false, 40f * k + 5f * S(time * 3f), ao + 4f, 10f, 20f + 10f * S(time * 2.2f), 20f, 10f);
                    p.SetArm(true, 30f * k, ao + 6f, 10f, 30f, 20f, 0f);
                    p.SoftHand(false); p.SoftHand(true);
                    break;
                }
                case ActionAnim.Swim:
                    break;
                case ActionAnim.Operate:
                    p.SetHand(true, 0.3f, 0.25f, 0.4f); p.SetHand(false, 0.3f, 0.25f, 0.4f);
                    p.SetArm(false, 46f + 4f * S(time * 7f), ao - 2f, 12f, 72f + 6f * S(time * 5f), -20f, 0f);
                    p.SetArm(true, 46f + 4f * S(time * 7f + 2f), ao - 2f, 12f, 72f + 6f * S(time * 5f + 1f), -20f, 0f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(8f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(14f);
                    break;
                case ActionAnim.Eat:
                {
                    p.Grip(false); p.Grip(true);
                    float c = Mathf.Max(0f, S(time * 2.4f));
                    p.SetArm(false, 30f + 10f * c, ao - 4f, 25f, 95f + 45f * c, 40f, 0f);
                    p.SetArm(true, 38f, ao - 4f, 30f, 88f, 50f, 0f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(10f - 6f * c);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(6f);
                    break;
                }
                case ActionAnim.Drink:
                {
                    p.Grip(false);
                    float k = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
                    p.SetArm(false, 35f + 12f * k, ao - 6f, 25f, 90f + 50f * k, 40f, 10f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(-18f * k);
                    break;
                }
                case ActionAnim.Cook:
                    p.Grip(false); p.SoftHand(true);
                    p.SetArm(false, 45f + 8f * S(time * 4f), ao + 8f + 8f * Mathf.Cos(time * 4f), 10f, 80f, 0f, 10f);
                    p.SetArm(true, 40f, ao + 4f, 20f, 85f, 30f, 0f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(8f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(18f);
                    break;
                case ActionAnim.Read:
                    p.Grip(true); p.Grip(false);
                    p.SetArm(true, 34f, ao - 8f, 30f, 108f, 50f, -10f);
                    p.SetArm(false, 34f, ao - 8f, 30f, 108f, 50f, -10f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(20f);
                    p.R[(int)HBone.Neck] *= ActorPose.Body(6f);
                    break;
                case ActionAnim.Write:
                    p.Grip(false); p.OpenHand(true);
                    p.SetArm(false, 42f + 2f * S(time * 11f), ao, 20f, 70f + 3f * S(time * 9f), 40f, 10f);
                    p.SetArm(true, 40f, ao + 4f, 25f, 65f, 70f, 0f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(12f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(22f);
                    break;
                case ActionAnim.Clean:
                    p.SoftHand(false);
                    p.SetArm(false, 50f + 12f * S(time * 3.5f), ao + 10f + 14f * Mathf.Cos(time * 3.5f), 20f, 45f, 20f, 10f);
                    p.SetArm(true, 25f, ao + 5f, 10f, 40f, 10f, 0f);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(10f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(10f, 6f * S(time * 3.5f), 0);
                    p.R[(int)HBone.Head] *= ActorPose.Body(15f);
                    break;
                case ActionAnim.Wash:
                    p.SoftHand(true); p.SoftHand(false);
                    p.SetArm(false, 40f + 4f * S(time * 9f), ao - 8f, 30f, 75f, 50f, 0f);
                    p.SetArm(true, 40f + 4f * S(time * 9f + 3f), ao - 8f, 30f, 75f, 50f, 0f);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(10f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(10f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(20f);
                    break;
                case ActionAnim.Knock:
                {
                    p.Fist(false);
                    float knock = t > 0.3f && t < dur - 0.2f ? Mathf.Max(0f, S((t - 0.3f) * Mathf.PI * 2f * 3.2f)) : 0f;
                    p.SetArm(false, 62f - 8f * knock, ao, 10f, 112f - 12f * knock, 60f, 0f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(-2f);
                    break;
                }
                case ActionAnim.OpenDoor:
                {
                    p.Grip(false);
                    float u = t / dur;
                    float reach = Ease(u / 0.35f);
                    float twist = Ease((u - 0.35f) / 0.2f);
                    float push = Ease((u - 0.55f) / 0.3f);
                    p.SetArm(false, Mathf.Lerp(5f, 50f, reach) + 12f * push, ao, 0f, Mathf.Lerp(12f, 35f, reach) - 20f * push, 60f * twist, 0f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(6f * push, 5f * reach, 0);
                    break;
                }
                case ActionAnim.Stab:
                {
                    p.Grip(false); p.Fist(true);
                    fullBody = true;
                    float cyc = 0.8f;
                    float u = (t % cyc) / cyc;
                    float wind = Ease(u / 0.35f) * (1f - Ease((u - 0.35f) / 0.12f));
                    float thrust = Ease((u - 0.35f) / 0.12f) * (1f - Ease((u - 0.72f) / 0.28f));
                    p.SetArm(false, Mathf.Lerp(Mathf.Lerp(10f, -22f, wind), 78f, thrust), ao + 6f, 0f, Mathf.Lerp(Mathf.Lerp(20f, 105f, wind), 8f, thrust), 70f, -10f);
                    p.SetArm(true, 20f + 15f * thrust, ao + 10f, 10f, 60f, 20f, 0f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(4f + 12f * thrust, -10f * wind + 18f * thrust, 0);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(8f * thrust, 8f * thrust, 0);
                    p.HipsOffset += new Vector3(0, -0.04f * thrust, 0.07f * thrust);
                    p.FootR += new Vector3(0, 0, 0.12f * thrust);
                    break;
                }
                case ActionAnim.Slash:
                {
                    p.Grip(false);
                    float u = Mathf.Clamp01(t / dur);
                    float wind = Ease(u / 0.35f);
                    float sw = Ease((u - 0.35f) / 0.2f);
                    p.SetArm(false, Mathf.Lerp(Mathf.Lerp(10f, 70f, wind), 45f, sw), Mathf.Lerp(Mathf.Lerp(ao, 80f, wind), -25f, sw), 0f, Mathf.Lerp(Mathf.Lerp(20f, 75f, wind), 15f, sw), 40f, 0f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(10f * sw, Mathf.Lerp(Mathf.Lerp(0f, 25f, wind), -30f, sw), 0);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(6f * sw, Mathf.Lerp(10f * wind, -15f, sw), 0);
                    break;
                }
                case ActionAnim.Overhead:
                {
                    p.Grip(true); p.Grip(false);
                    fullBody = true;
                    float u = Mathf.Clamp01(t / dur);
                    float up = Ease(u / 0.45f);
                    float dn = Ease((u - 0.45f) / 0.15f);
                    float fwd = Mathf.Lerp(Mathf.Lerp(20f, 165f, up), 45f, dn);
                    float el = Mathf.Lerp(Mathf.Lerp(30f, 70f, up), 10f, dn);
                    p.SetArm(true, fwd, 8f, 20f, el, 30f, 0f);
                    p.SetArm(false, fwd, 8f, 20f, el, 30f, 0f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(-10f * up * (1f - dn) + 22f * dn);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(-5f * up * (1f - dn) + 16f * dn);
                    p.HipsOffset += new Vector3(0, -0.06f * dn, 0.04f * dn);
                    p.FootR += new Vector3(0, 0, 0.1f * dn);
                    break;
                }
                case ActionAnim.Shove:
                {
                    p.OpenHand(true); p.OpenHand(false);
                    fullBody = true;
                    float u = Mathf.Clamp01(t / dur);
                    float push = Ease(u / 0.3f) * (1f - Ease((u - 0.65f) / 0.35f));
                    p.SetArm(true, Mathf.Lerp(30f, 82f, push), ao - 4f, 30f, Mathf.Lerp(100f, 8f, push), 20f, -40f * push);
                    p.SetArm(false, Mathf.Lerp(30f, 82f, push), ao - 4f, 30f, Mathf.Lerp(100f, 8f, push), 20f, -40f * push);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(12f * push);
                    p.HipsOffset += new Vector3(0, -0.03f * push, 0.1f * push);
                    p.FootL += new Vector3(0, 0, 0.18f * push);
                    break;
                }
                case ActionAnim.Strangle:
                {
                    p.SetHand(true, 0.7f, 0.75f, 0.8f); p.SetHand(false, 0.7f, 0.75f, 0.8f);
                    float sq = S(time * 18f) * 2f;
                    p.SetArm(true, 72f + sq, ao - 10f, 45f, 48f, 30f, -30f);
                    p.SetArm(false, 72f - sq, ao - 10f, 45f, 48f, 30f, -30f);
                    p.SetShoulder(true, 6f, 6f); p.SetShoulder(false, 6f, 6f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(12f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(-8f);
                    break;
                }
                case ActionAnim.Carry:
                    CarryOverlay(p, d, time);
                    break;
                case ActionAnim.Struggle:
                {
                    p.Fist(true); p.Fist(false);
                    fullBody = false;
                    p.SetArm(true, 40f + 45f * N(time * 3f, 1f), ao + 20f + 25f * N(time * 3.3f, 2f), 20f, 70f + 40f * N(time * 4f, 3f), 0f, 0f);
                    p.SetArm(false, 40f + 45f * N(time * 3.1f, 4f), ao + 20f + 25f * N(time * 3.5f, 5f), 20f, 70f + 40f * N(time * 4.2f, 6f), 0f, 0f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(10f * N(time * 2f, 7f), 25f * N(time * 2.5f, 8f), 10f * N(time * 2.2f, 9f));
                    p.R[(int)HBone.Head] *= ActorPose.Body(15f * N(time * 3f, 10f), 30f * N(time * 3.2f, 11f), 0);
                    break;
                }
                case ActionAnim.Fall:
                {
                    fullBody = true;
                    float u = Mathf.Clamp01(t / dur);
                    // buckle -> kneel collapse -> lying
                    var kneel = _s1; kneel.Reset(); PostureBase(kneel, d, Posture.Kneel, time, "");
                    kneel.R[(int)HBone.Spine] = ActorPose.Body(30f); kneel.R[(int)HBone.Head] = ActorPose.Body(25f);
                    var lie = _s2; lie.Reset(); LieFront(lie, d, time);
                    if (u < 0.45f) p.Blend(p, kneel, Ease(u / 0.45f));
                    else { var tmp = _s3; tmp.Blend(kneel, lie, Ease((u - 0.45f) / 0.55f)); p.CopyFrom(tmp); }
                    break;
                }
                case ActionAnim.Stagger:
                {
                    fullBody = true;
                    float u = Mathf.Clamp01(t / dur);
                    float k = Mathf.Sin(u * Mathf.PI);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(-14f * k, 10f * k, 12f * k);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(-6f * k, 0, 8f * k);
                    p.R[(int)HBone.Head] *= ActorPose.Body(10f * k, -15f * k, 8f * k);
                    p.SetArm(true, 25f * k, ao + 45f * k, 0f, 30f, 0f, 0f);
                    p.SetArm(false, 10f * k, ao + 30f * k, 0f, 40f, 0f, 0f);
                    p.HipsOffset += new Vector3(-0.06f * k, -0.05f * k, -0.05f * k);
                    p.FootL += new Vector3(-0.1f * k, 0, -0.08f * k);
                    break;
                }
                case ActionAnim.Crawl:
                {
                    p.OpenHand(true); p.OpenHand(false);
                    fullBody = true;
                    p.Reset();
                    p.LegIK = 0f;
                    float c = time * 2.4f;
                    float hipJointH = 0.07f * s + d.UpperLeg * 0.92f;
                    p.HipsOffset = new Vector3(0, hipJointH + d.HipsAboveJoint * 0.2f - d.RestHips.y, -0.1f);
                    p.R[0] = ActorPose.Body(78f);
                    p.R[(int)HBone.Spine] = ActorPose.Body(4f);
                    p.R[(int)HBone.Chest] = ActorPose.Body(2f, 5f * S(c), 0);
                    p.R[(int)HBone.Neck] = ActorPose.Body(-25f);
                    p.R[(int)HBone.Head] = ActorPose.Body(-20f);
                    p.SetArm(true, 88f + 18f * S(c), ao + 4f, 0f, 20f + 20f * Mathf.Max(0, -S(c)), 0f, -60f);
                    p.SetArm(false, 88f - 18f * S(c), ao + 4f, 0f, 20f + 20f * Mathf.Max(0, S(c)), 0f, -60f);
                    p.SetLegFK(true, 70f - 14f * S(c), 6f, 92f - 10f * S(c), 50f);
                    p.SetLegFK(false, 70f + 14f * S(c), 6f, 92f + 10f * S(c), 50f);
                    break;
                }
                case ActionAnim.Hurt:
                {
                    p.SetHand(true, 0.5f, 0.55f, 0.65f); p.SetHand(false, 0.5f, 0.55f, 0.65f);
                    float k = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(18f * k);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(14f * k);
                    p.R[(int)HBone.Head] *= ActorPose.Body(10f * k);
                    p.SetArm(true, 30f * k + 5f, ao - 10f * k, 40f * k, 90f * k + 10f, 30f, 0f);
                    p.SetArm(false, 30f * k + 5f, ao - 10f * k, 40f * k, 90f * k + 10f, 30f, 0f);
                    break;
                }
                case ActionAnim.FirstAid:
                {
                    p.OpenHand(true); p.OpenHand(false);
                    fullBody = true;
                    var kneel = _s1; kneel.Reset(); PostureBase(kneel, d, Posture.Kneel, time, "");
                    p.CopyFrom(kneel);
                    float press = Mathf.Max(0f, S(time * 7f));
                    p.R[(int)HBone.Spine] = ActorPose.Body(28f + 5f * press);
                    p.R[(int)HBone.Chest] = ActorPose.Body(10f);
                    p.R[(int)HBone.Head] = ActorPose.Body(-10f);
                    p.SetArm(true, 62f + 4f * press, ao - 10f, 40f, 12f, 40f, -30f);
                    p.SetArm(false, 62f + 4f * press, ao - 10f, 40f, 12f, 40f, -30f);
                    break;
                }
                case ActionAnim.Play:
                    p.SetHand(true, 0.35f, 0.3f, 0.45f); p.Grip(false);
                    p.SetArm(true, 42f, ao + 22f, 0f, 102f + 5f * S(time * 3f), -40f, 20f);
                    p.SetArm(false, 22f, ao - 8f, 20f, 88f + 6f * S(time * 8f), 30f, 10f * S(time * 8f));
                    p.R[(int)HBone.Head] *= ActorPose.Body(8f + 6f * Mathf.Abs(S(time * 4.2f)), 0, 4f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(4f);
                    break;
                case ActionAnim.Sleep:
                    fullBody = true;
                    p.Reset();
                    LieSide(p, d, time * 0.6f, 0.4f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(5f);
                    break;
                default: ActorPosesExt.ActionExt(p, d, a, t, dur, time, ref fullBody); break;
            }
        }
    }
}
