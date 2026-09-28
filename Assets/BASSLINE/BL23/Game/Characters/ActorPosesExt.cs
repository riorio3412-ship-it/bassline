using UnityEngine;
using static BL23.Game.Characters.MotionKit;

namespace BL23.Game.Characters
{
    /// <summary>Motion-track additions to the procedural pose library: the appended gestures and actions (everyday, firearms,
    /// crossbow, restraint and solo fallbacks of the paired acts). Canonical space, degrees; same conventions as ActorPoses.</summary>
    public static class ActorPosesExt
    {
        /// <summary>What the right hand holds while an action pose is evaluated (set by the animator; main thread only).</summary>
        public static WeaponClass Weapon;
        static float S(float x) => Mathf.Sin(x);

        public static float GestureDuration(Gesture g)
        {
            switch (g)
            {
                case Gesture.CoverMouth: return 2.4f;
                case Gesture.CheckWatch: return 3.2f;
                case Gesture.AdjustClothes: return 2.2f;
                case Gesture.Apologize: return 1.6f;
                case Gesture.DuckCover: return 2.0f;
                case Gesture.HandToBurn: return 1.8f;
                case Gesture.WeightShift: return 2.0f;
                default: return 3.0f;
            }
        }

        public static float ActionDuration(ActionAnim a)
        {
            switch (a)
            {
                case ActionAnim.PushChair: return 1.0f;
                case ActionAnim.OpenDrawer: return 1.2f;
                case ActionAnim.Pour: return 2.0f;
                case ActionAnim.PourPoison: return 2.4f;
                case ActionAnim.Reload: return 2.8f;
                case ActionAnim.DrawWeapon: case ActionAnim.Holster: return 0.8f;
                case ActionAnim.CockCrossbow: return 3.0f;
                case ActionAnim.HandOver: case ActionAnim.Receive: return 1.1f;
                case ActionAnim.LiftBody: return 1.8f;
                case ActionAnim.Slip: return 0.9f;
                case ActionAnim.StumbleStairs: return 1.0f;
                case ActionAnim.Kick: return 0.9f;
                case ActionAnim.Shoot: return 0.55f;
                case ActionAnim.GetUp: return 2.4f;
                case ActionAnim.Startle: return 1.3f;
                case ActionAnim.HitHead: case ActionAnim.HitGut: case ActionAnim.ClutchWound: return 1.2f;
                case ActionAnim.TurnAway: return 1.3f;
                case ActionAnim.Dodge: return 0.75f;
                case ActionAnim.StabUnder: case ActionAnim.StabOver: return 0.95f;
                default: return 4f;
            }
        }

        // ================================================================ gestures (upper body; the layer blends them in and out)
        public static void GestureExt(ActorPose p, ActorPoses.Dims d, Gesture g, float t, float dur, float time)
        {
            float ao = d.ArmOut;
            float u = Mathf.Clamp01(t / Mathf.Max(0.1f, dur));
            switch (g)
            {
                case Characters.Gesture.CoverMouth:
                {
                    // a hand over the mouth in horror, the other arm across the belly holding the elbow; shoulders up, a small recoil
                    float tr = Noise(time * 11f, 1f) * 1.2f;
                    p.SetArm(false, 42f, -14f, 46f, 138f + tr, 40f, 14f);
                    p.SetArm(true, 26f, -18f, 56f, 100f, 30f, 0f);
                    p.SetHand(false, 0.22f, 0.18f, 0.28f); p.SetHand(true, 0.45f, 0.5f, 0.55f);
                    p.SetShoulder(true, 7f, 6f); p.SetShoulder(false, 7f, 6f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(-4f + 3f * u, 0f, 0f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(8f, 0f, 5f);
                    break;
                }
                case Characters.Gesture.Console:
                {
                    // the right hand goes to the partner's shoulder (hand target IK lands it), a small pat, the head tilts in
                    float pat = Mathf.Max(0f, S(time * 4.2f)) * 5f;
                    p.SetArm(false, 68f, 10f, 20f, 38f + pat, 30f, -20f);
                    p.SoftHand(false);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(5f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(8f, 0f, 10f);
                    break;
                }
                case Characters.Gesture.Hug:
                    p.SetArm(false, 76f, 16f, 52f, 96f, 20f, 0f);
                    p.SetArm(true, 72f, 18f, 52f, 100f, 20f, 0f);
                    p.SoftHand(false); p.SoftHand(true);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(6f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(6f, 22f, 10f);
                    break;
                case Characters.Gesture.CheckWatch:
                {
                    // right hand to the waistcoat pocket, the watch up to the chest, the left hand flips the lid, a look, away again
                    float toPocket = Bell(u, 0f, 0.14f, 0.24f, 0.36f), up = Bell(u, 0.22f, 0.38f, 0.74f, 0.88f), back = Bell(u, 0.8f, 0.9f, 0.95f, 1f);
                    float rf = 12f + 12f * toPocket + 30f * up + 10f * back, re = 30f + 60f * toPocket + 90f * up + 60f * back;
                    p.SetArm(false, rf, ao - 14f * (toPocket + up), 30f + 16f * up, re, 40f, 10f * up);
                    p.SetArm(true, 10f + 30f * up, ao - 16f * up, 30f + 22f * up, 20f + 94f * up, 50f, 0f);
                    p.Grip(false); p.SetHand(true, 0.25f, 0.15f + 0.5f * up, 0.3f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(26f * up + 6f * toPocket, 0f, 0f);
                    p.R[(int)HBone.Neck] *= ActorPose.Body(8f * up);
                    break;
                }
                case Characters.Gesture.AdjustClothes:
                {
                    // both hands tug the lapels down, then the left hand straightens the right cuff; the chin comes up after
                    float lap = Bell(u, 0f, 0.14f, 0.4f, 0.52f), cuff = Bell(u, 0.45f, 0.58f, 0.85f, 0.97f);
                    float tug = S(Mathf.Clamp01((u - 0.14f) / 0.26f) * Mathf.PI) * 10f;
                    p.SetArm(false, 30f * lap + 30f * cuff, ao - 22f * lap - 4f * cuff, 50f * lap + 20f * cuff, 120f * lap - tug * lap + 80f * cuff, 40f, 0f);
                    p.SetArm(true, 30f * lap + 36f * cuff, ao - 22f * lap - 22f * cuff, 50f * lap + 52f * cuff, 120f * lap - tug * lap + 88f * cuff, 40f, 0f);
                    p.Grip(false); p.Grip(true);
                    p.R[(int)HBone.Head] *= ActorPose.Body(14f * cuff + 8f * lap - 6f * Win(u, 0.9f, 1f), 0f, 0f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(-3f * lap, 0f, 0f);
                    break;
                }
                case Characters.Gesture.Shiver:
                {
                    float sh = Noise(time * 17f, 2f) * 2f;
                    p.SetArm(true, 34f + sh, -22f, 60f, 112f, 20f, 0f);
                    p.SetArm(false, 30f - sh, -20f, 60f, 116f, 20f, 0f);
                    p.SetHand(true, 0.3f, 0.35f, 0.4f); p.SetHand(false, 0.3f, 0.35f, 0.4f);
                    p.SetShoulder(true, 10f, 8f); p.SetShoulder(false, 10f, 8f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(5f + sh * 0.6f, sh * 0.5f, 0f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(4f + sh * 0.5f, 0f, 0f);
                    break;
                }
                case Characters.Gesture.Apologize:
                {
                    float b = Bell(u, 0.05f, 0.3f, 0.65f, 0.95f);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(9f * b);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(6f * b);
                    p.R[(int)HBone.Neck] *= ActorPose.Body(8f * b);
                    p.R[(int)HBone.Head] *= ActorPose.Body(5f * b, 0f, 5f * b);
                    p.SetArm(false, 44f * b, ao + 2f, 30f, 98f * b, 70f, -10f);
                    p.SetHand(false, 0.08f, 0.05f, 0.1f);
                    break;
                }
                case Characters.Gesture.DuckCover:
                    p.R[(int)HBone.Spine] *= ActorPose.Body(20f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(14f);
                    p.R[(int)HBone.Neck] *= ActorPose.Body(14f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(10f);
                    p.SetArm(false, 148f, 30f, 60f, 128f, 20f, 30f);
                    p.SetArm(true, 146f, 32f, 60f, 130f, 20f, 30f);
                    p.SetHand(false, 0.2f, 0.25f, 0.3f); p.SetHand(true, 0.2f, 0.25f, 0.3f);
                    break;
                case Characters.Gesture.HandToBurn:
                {
                    // jerk the hand back, shake it, then cradle it against the chest with the other hand
                    float jerk = Impulse(t, 0.08f), shake = Bell(u, 0.06f, 0.12f, 0.4f, 0.5f), cradle = Win(u, 0.42f, 0.6f);
                    float sw = S(time * 26f) * 28f * shake;
                    p.SetArm(false, Mathf.Lerp(-10f * jerk + 18f, 30f, cradle), Mathf.Lerp(ao + 16f * jerk + 8f, -10f, cradle), 30f * cradle, Mathf.Lerp(40f + 30f * jerk, 110f, cradle), 40f, sw);
                    p.SetArm(true, 32f * cradle, Mathf.Lerp(ao, -16f, cradle), 50f * cradle, 105f * cradle + 10f, 40f, 0f);
                    p.SetHand(false, 0.35f, 0.3f, 0.35f); p.SoftHand(true);
                    p.SetShoulder(false, 10f * jerk + 5f * shake);
                    p.R[(int)HBone.Head] *= ActorPose.Body(12f * cradle, 0f, 0f);
                    break;
                }
                case Characters.Gesture.HandsUp:
                {
                    float tr = Noise(time * 9f, 3f) * 2f;
                    p.SetArm(false, 12f, 76f + tr, -85f, 96f, 0f, -20f);
                    p.SetArm(true, 12f, 76f - tr, -85f, 96f, 0f, -20f);
                    p.OpenHand(false); p.OpenHand(true);
                    p.SetShoulder(true, 6f); p.SetShoulder(false, 6f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(-4f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(-4f);
                    break;
                }
                case Characters.Gesture.WeightShift:
                    p.R[(int)HBone.Chest] *= ActorPose.Body(0f, 0f, 2f * S(u * Mathf.PI));
                    break;
            }
        }

        // ================================================================ actions
        public static void ActionExt(ActorPose p, ActorPoses.Dims d, ActionAnim a, float t, float dur, float time, ref bool fullBody)
        {
            float ao = d.ArmOut;
            float s = d.H / 1.75f;
            float u = Mathf.Clamp01(t / Mathf.Max(0.1f, dur));
            switch (a)
            {
                case ActionAnim.PushChair:
                {
                    fullBody = true;
                    float reach = Win(u, 0f, 0.3f), push = Win(u, 0.3f, 0.7f), rel = Win(u, 0.72f, 1f);
                    float k = reach * (1f - rel);
                    p.SetArm(false, 30f + 28f * k, ao + 4f, 20f, 40f * k - 26f * push * (1f - rel) + 12f, 40f, -30f * k);
                    p.SetArm(true, 30f + 26f * k, ao + 4f, 20f, 42f * k - 26f * push * (1f - rel) + 12f, 40f, -30f * k);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(12f * k + 6f * push * (1f - rel));
                    p.R[(int)HBone.Chest] *= ActorPose.Body(6f * k);
                    p.HipsOffset += new Vector3(0f, -0.02f * k, 0.05f * push * (1f - rel)) * s;
                    if (p.LegIK > 0.5f) p.FootL += new Vector3(0f, Mathf.Sin(Win(u, 0.28f, 0.5f) * Mathf.PI) * 0.04f, 0.13f * push * (1f - rel)) * s;
                    float g = Win(u, 0.22f, 0.32f) * (1f - rel);
                    MotionKit.BlendHand(p, false, g, 0.1f, 0.08f, 0.12f); MotionKit.BlendHand(p, true, g, 0.1f, 0.08f, 0.12f);
                    break;
                }
                case ActionAnim.OpenDrawer:
                {
                    fullBody = true;
                    float bend = Bell(u, 0f, 0.25f, 0.85f, 1f), grip = Win(u, 0.25f, 0.32f) * (1f - Win(u, 0.8f, 0.9f)), pull = Win(u, 0.32f, 0.62f);
                    p.R[0] *= ActorPose.Body(20f * bend);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(10f * bend);
                    p.R[(int)HBone.Head] *= ActorPose.Body(10f * bend + 12f * pull * bend);
                    p.HipsOffset += new Vector3(0f, -0.06f * bend, -0.05f * bend) * s;
                    p.SetArm(false, Mathf.Lerp(8f, 46f, bend) - 18f * pull, ao + 6f, 30f, Mathf.Lerp(12f, 24f, bend) + 50f * pull, 40f, -10f);
                    p.SetArm(true, 22f * bend, ao - 2f, 30f, 24f * bend, 40f, -30f);        // free hand on the thigh
                    MotionKit.BlendHand(p, false, grip, 0.6f, 0.65f, 0.75f);
                    break;
                }
                case ActionAnim.Pour:
                {
                    float tip = Bell(u, 0.2f, 0.45f, 0.72f, 0.9f);
                    p.SetArm(false, 46f, ao + 6f, 20f, 70f, 12f + 70f * tip, -8f - 10f * tip);
                    p.SetArm(true, 36f, ao - 12f, 42f, 82f, 40f, 0f);
                    p.Grip(false); p.SetHand(true, 0.35f, 0.4f, 0.45f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(18f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(5f);
                    break;
                }
                case ActionAnim.PourPoison:
                {
                    // a furtive look around, the vial tipped quickly over the cup, the hand back into the pocket, another look
                    float look = u < 0.3f ? S(u / 0.3f * Mathf.PI * 2f) : u > 0.82f ? S((u - 0.82f) / 0.18f * Mathf.PI) * 0.6f : 0f;
                    float out1 = Win(u, 0.28f, 0.42f) * (1f - Win(u, 0.64f, 0.78f)), tip = Bell(u, 0.42f, 0.5f, 0.58f, 0.64f), pocket = Win(u, 0.7f, 0.84f);
                    float inCoat = 1f - Win(u, 0.22f, 0.32f);
                    p.SetArm(false, Mathf.Lerp(Mathf.Lerp(20f, 50f, out1), -4f, pocket), Mathf.Lerp(Mathf.Lerp(ao - 12f * inCoat, ao, out1), ao + 6f, pocket),
                        Mathf.Lerp(Mathf.Lerp(50f * inCoat + 20f, 30f, out1), 30f, pocket), Mathf.Lerp(Mathf.Lerp(40f + 70f * inCoat, 60f, out1), 34f, pocket), 20f + 70f * tip, 20f * tip);
                    p.SetHand(false, 0.6f, 0.55f, 0.7f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(10f * out1, 35f * look, 0f);
                    p.R[(int)HBone.Neck] *= ActorPose.Body(0f, 12f * look, 0f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(4f * out1, 6f * look, 0f);
                    break;
                }
                case ActionAnim.Reload:
                    Reload(p, d, u, time, ao);
                    break;
                case ActionAnim.DrawWeapon:
                case ActionAnim.Holster:
                {
                    // the right hand into the coat at the left breast, grip, out and forward (Holster runs it backwards)
                    float x = a == ActionAnim.Holster ? 1f - u : u;
                    float inside = Bell(x, 0f, 0.3f, 0.42f, 0.62f), outK = Win(x, 0.45f, 1f);
                    p.SetArm(false, Mathf.Lerp(8f + 36f * inside, 70f, outK), Mathf.Lerp(ao - 30f * inside, 30f, outK), Mathf.Lerp(20f + 42f * inside, 10f, outK),
                        Mathf.Lerp(20f + 108f * inside, 30f, outK), 40f, 18f * inside);
                    p.SetHand(false, 0.2f + 0.5f * Win(x, 0.3f, 0.42f), 0.2f + 0.55f * Win(x, 0.3f, 0.42f), 0.25f + 0.55f * Win(x, 0.3f, 0.42f));
                    p.R[(int)HBone.Chest] *= ActorPose.Body(0f, -8f * inside - 12f * outK, 0f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(10f * inside, 0f, 0f);
                    break;
                }
                case ActionAnim.CockCrossbow:
                {
                    fullBody = true;
                    float down = Bell(u, 0f, 0.18f, 0.72f, 0.86f), pull = Win(u, 0.22f, 0.7f), lift = Win(u, 0.82f, 1f);
                    float strain = Noise(time * 12f, 4f) * 2f * Bell(u, 0.25f, 0.35f, 0.62f, 0.7f);
                    p.R[0] *= ActorPose.Body(34f * down - 18f * pull * down);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(24f * down - 12f * pull * down + strain);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(10f * down);
                    p.R[(int)HBone.Head] *= ActorPose.Body(14f * down);
                    p.HipsOffset += new Vector3(0f, -0.14f * down + 0.06f * pull * down, -0.05f * down) * s;
                    if (p.LegIK > 0.5f) p.FootR += new Vector3(0f, 0.03f * down, 0.26f * down) * s;          // foot in the stirrup
                    float armF = Mathf.Lerp(52f, 22f, pull), armE = Mathf.Lerp(12f, 104f, pull);
                    p.SetArm(false, Mathf.Lerp(armF, 44f, lift), ao + 6f, 30f, Mathf.Lerp(armE, 84f, lift) + strain, 40f, 0f);
                    p.SetArm(true, Mathf.Lerp(armF, 50f, lift), ao + 6f, 30f, Mathf.Lerp(armE, 70f, lift) - strain, 40f, 0f);
                    p.Grip(false); p.Grip(true);
                    break;
                }
                case ActionAnim.HandOver:
                case ActionAnim.Receive:
                {
                    // solo fallback (the owner's controller runs the synchronised version): arm out at chest height, open / close
                    bool give = a == ActionAnim.HandOver;
                    float ext = Bell(u, 0f, 0.45f, 0.62f, 1f);
                    p.SetArm(false, 14f + 44f * ext, ao + 2f, 20f, 60f - 28f * ext, give ? 40f : 80f, -10f);
                    bool closed = give ? u < 0.55f : u > 0.55f;
                    if (closed) p.Grip(false); else p.OpenHand(false);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(5f * ext);
                    p.R[(int)HBone.Head] *= ActorPose.Body(10f * ext);
                    break;
                }
                case ActionAnim.LeanTable:
                    fullBody = true;
                    p.R[0] *= ActorPose.Body(14f);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(10f + 1.2f * S(time * 1.4f));
                    p.R[(int)HBone.Chest] *= ActorPose.Body(4f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(4f);
                    p.HipsOffset += new Vector3(0f, -0.02f, -0.06f) * s;
                    p.SetArm(false, 42f, ao + 8f, 20f, 8f, 60f, -62f);
                    p.SetArm(true, 42f, ao + 8f, 20f, 8f, 60f, -62f);
                    p.SetShoulder(true, 6f, 4f); p.SetShoulder(false, 6f, 4f);
                    p.OpenHand(false); p.OpenHand(true);
                    break;
                case ActionAnim.CarryHeavy:
                    p.SetArm(false, 36f, ao - 2f, 50f, 86f, 30f, -10f);
                    p.SetArm(true, 36f, ao - 2f, 50f, 86f, 30f, -10f);
                    p.SetHand(false, 0.35f, 0.4f, 0.5f); p.SetHand(true, 0.35f, 0.4f, 0.5f);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(-7f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(-4f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(6f);
                    p.SetShoulder(true, -3f, 6f); p.SetShoulder(false, -3f, 6f);
                    break;
                case ActionAnim.LiftBody:
                {
                    fullBody = true;
                    float squat = Bell(u, 0f, 0.3f, 0.4f, 0.72f), heave = Win(u, 0.35f, 0.75f);
                    float strain = Noise(time * 10f, 5f) * 2f * Bell(u, 0.35f, 0.45f, 0.65f, 0.75f);
                    p.R[0] *= ActorPose.Body(40f * squat);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(22f * squat - 6f * heave + strain, -10f * heave, 0f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(10f * squat, -6f * heave, -6f * heave);
                    p.HipsOffset += new Vector3(0f, -0.34f * squat, -0.08f * squat) * s;
                    if (p.LegIK > 0.5f) { p.FootL += new Vector3(-0.04f, 0f, 0.1f) * s * squat; p.FootR += new Vector3(0.05f, 0f, -0.06f) * s * squat; }
                    p.SetArm(false, Mathf.Lerp(70f, 72f, heave) * Mathf.Max(squat, heave), Mathf.Lerp(ao + 4f, -8f, heave), 30f, Mathf.Lerp(20f, 118f, heave), 30f, 10f);
                    p.SetArm(true, Mathf.Lerp(70f, 50f, heave) * Mathf.Max(squat, heave), ao + 4f, 30f, Mathf.Lerp(24f, 80f, heave), 30f, 0f);
                    p.Grip(false); p.Grip(true);
                    break;
                }
                case ActionAnim.Drag:
                case ActionAnim.DragArmpits:
                    // bent low over the body, both hands forward and down gripping under the arms (walking handled by locomotion)
                    p.R[(int)HBone.Spine] *= ActorPose.Body(30f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(14f);
                    p.R[(int)HBone.Neck] *= ActorPose.Body(-16f);
                    p.SetArm(false, 58f, 4f, 20f, 16f, 40f, -10f); p.SetArm(true, 58f, 4f, 20f, 16f, 40f, -10f);
                    p.Grip(true); p.Grip(false);
                    break;
                case ActionAnim.DragAnkles:
                    p.R[(int)HBone.Spine] *= ActorPose.Body(20f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(8f);
                    p.R[(int)HBone.Neck] *= ActorPose.Body(-10f);
                    p.SetArm(false, -34f, ao + 8f, 30f, 14f, 30f, 10f); p.SetArm(true, -34f, ao + 8f, 30f, 14f, 30f, 10f);
                    p.Grip(true); p.Grip(false);
                    break;
                case ActionAnim.Slip:
                {
                    // the feet shoot forward, arms fling up and back, the seat drops; recovers if no fall follows
                    fullBody = true;
                    float go = Bell(u, 0f, 0.22f, 0.45f, 0.95f);
                    p.R[0] *= ActorPose.Body(-18f * go);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(-8f * go);
                    p.R[(int)HBone.Neck] *= ActorPose.Body(16f * go);
                    p.HipsOffset += new Vector3(0f, -0.14f * go, -0.1f * go) * s;
                    if (p.LegIK > 0.5f) p.FootR += new Vector3(0f, 0.22f * go, 0.34f * go) * s;
                    p.SetArm(false, 100f * go + 10f, ao + 50f * go, 0f, 30f, 0f, -20f);
                    p.SetArm(true, 96f * go + 10f, ao + 54f * go, 0f, 34f, 0f, -20f);
                    p.OpenHand(true); p.OpenHand(false);
                    break;
                }
                case ActionAnim.StumbleStairs:
                {
                    // a toe catches: the body pitches forward, the hands reach for the steps, a catching step, then upright
                    fullBody = true;
                    float go = Bell(u, 0f, 0.2f, 0.45f, 0.95f);
                    p.R[0] *= ActorPose.Body(18f * go);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(16f * go);
                    p.R[(int)HBone.Neck] *= ActorPose.Body(-18f * go);
                    p.HipsOffset += new Vector3(0f, -0.12f * go, 0.1f * go) * s;
                    if (p.LegIK > 0.5f) p.FootL += new Vector3(0f, Mathf.Sin(Win(u, 0.1f, 0.4f) * Mathf.PI) * 0.1f, 0.36f * Win(u, 0.1f, 0.4f) * (1f - Win(u, 0.7f, 1f))) * s;
                    p.SetArm(false, 72f * go + 5f, ao + 18f * go, 0f, 18f, 60f, -40f * go);
                    p.SetArm(true, 70f * go + 5f, ao + 20f * go, 0f, 20f, 60f, -40f * go);
                    p.OpenHand(true); p.OpenHand(false);
                    break;
                }
                case ActionAnim.TieUp:
                case ActionAnim.Untie:
                {
                    // bent over the bound limbs, both hands working a knot / wrapping the rope
                    fullBody = true;
                    bool tie = a == ActionAnim.TieUp;
                    float sp = tie ? 4.2f : 2.6f;
                    p.R[0] *= ActorPose.Body(24f);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(18f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(18f);
                    p.HipsOffset += new Vector3(0f, -0.16f, -0.05f) * s;
                    p.SetArm(false, 52f + 10f * S(time * sp), ao - 8f, 40f, 70f + 16f * Mathf.Cos(time * sp), 40f, 10f * S(time * sp * 1.3f));
                    p.SetArm(true, 50f - 8f * S(time * sp + 1f), ao - 8f, 40f, 72f - 14f * Mathf.Cos(time * sp + 1f), 40f, 0f);
                    p.SetHand(false, 0.5f, 0.55f, 0.6f); p.SetHand(true, 0.5f, 0.55f, 0.6f);
                    break;
                }
                case ActionAnim.StruggleBonds:
                {
                    float n1 = Noise(time * 3f, 1f), n2 = Noise(time * 2.6f, 2f);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(n1 * 6f, n2 * 14f, n1 * 6f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(n2 * 5f, n1 * 10f, 0f);
                    p.R[(int)HBone.Head] *= ActorPose.Body(n1 * 8f, n2 * 16f, 0f);
                    p.SetShoulder(true, 6f + n1 * 6f, n2 * 8f); p.SetShoulder(false, 6f - n1 * 6f, -n2 * 8f);
                    break;
                }
                case ActionAnim.Hop:
                {
                    fullBody = true;
                    float h = Mathf.Abs(S(time * 5.5f));
                    p.HipsOffset += new Vector3(0f, 0.06f * h - 0.03f, 0f) * s;
                    if (p.LegIK > 0.5f)
                    {
                        float cx = (d.FootL.x + d.FootR.x) * 0.5f;
                        p.FootL = new Vector3(cx - 0.055f * s, d.FootL.y + 0.05f * h * s, d.FootL.z);
                        p.FootR = new Vector3(cx + 0.055f * s, d.FootR.y + 0.05f * h * s, d.FootR.z);
                    }
                    p.SetArm(false, 20f, ao + 20f, 0f, 40f); p.SetArm(true, 20f, ao + 20f, 0f, 40f);
                    break;
                }
                case ActionAnim.Garrote:
                case ActionAnim.LigatureFront:
                {
                    // solo (no partner): hands at neck height, crossed and pulling back, leaning back against the resistance
                    bool rear = a == ActionAnim.Garrote;
                    float pull = 0.5f + 0.5f * S(time * 2.3f);
                    p.SetArm(false, 80f, rear ? -18f : 12f, 40f, rear ? 96f : 70f + 10f * pull, 30f, -20f);
                    p.SetArm(true, 80f, rear ? -18f : 12f, 40f, rear ? 96f : 70f + 10f * pull, 30f, -20f);
                    p.SetHand(false, 0.8f, 0.85f, 0.9f); p.SetHand(true, 0.8f, 0.85f, 0.9f);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(rear ? -8f - 4f * pull : 6f);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(rear ? -6f : 6f);
                    p.SetShoulder(true, 8f, 8f); p.SetShoulder(false, 8f, 8f);
                    break;
                }
                case ActionAnim.Smother:
                case ActionAnim.Drown:
                {
                    // solo: bent over, arms straight down driving the weight into the hands
                    float press = 0.5f + 0.5f * S(time * 1.9f);
                    p.R[(int)HBone.Spine] *= ActorPose.Body(28f + 4f * press);
                    p.R[(int)HBone.Chest] *= ActorPose.Body(12f);
                    p.R[(int)HBone.Neck] *= ActorPose.Body(-10f);
                    p.SetArm(false, 64f, 6f, 30f, 22f - 6f * press, 50f, -50f);
                    p.SetArm(true, 64f, 6f, 30f, 22f - 6f * press, 50f, -50f);
                    p.SetShoulder(true, 6f, 12f); p.SetShoulder(false, 6f, 12f);
                    break;
                }
            }
        }

        static void Reload(ActorPose p, ActorPoses.Dims d, float u, float time, float ao)
        {
            float look = Bell(u, 0f, 0.12f, 0.9f, 1f);
            p.R[(int)HBone.Head] *= ActorPose.Body(24f * look);
            p.R[(int)HBone.Neck] *= ActorPose.Body(6f * look);
            if (Weapon == WeaponClass.Rifle)
            {
                // break-action: gun lowered at the hip, barrels dropped over the left forearm, shells in, snapped shut
                float breakK = Bell(u, 0.08f, 0.2f, 0.8f, 0.9f);
                float shells = u > 0.25f && u < 0.78f ? 0.5f + 0.5f * S((u - 0.25f) / 0.53f * Mathf.PI * 4f - Mathf.PI * 0.5f) : 0f;
                p.SetArm(false, 14f, ao + 12f, 20f, 64f, 30f, 0f);
                p.SetArm(true, 36f - 26f * shells, ao - 6f + 20f * shells, 30f, 44f + 40f * breakK - 20f * shells, 70f, 0f);
                p.Grip(false); p.SetHand(true, 0.4f, 0.45f, 0.5f);
                p.R[(int)HBone.Chest] *= ActorPose.Body(6f, -8f, 0f);
            }
            else if (Weapon == WeaponClass.Crossbow)
            {
                // a bolt from the quiver at the hip into the groove
                float fetch = Bell(u, 0.1f, 0.3f, 0.45f, 0.6f), seat = Bell(u, 0.55f, 0.7f, 0.82f, 0.95f);
                p.SetArm(false, 40f, ao + 20f, 40f, 90f, 30f, 0f);
                p.SetArm(true, 30f - 36f * fetch + 20f * seat, ao + 14f * fetch, 40f, 70f - 30f * fetch + 30f * seat, 40f, 0f);
                p.Grip(false); p.SetHand(true, 0.6f, 0.4f, 0.6f);
            }
            else
            {
                // revolver: gun to the chest, cylinder out, rounds from the right coat pocket twice, snapped shut with a wrist flick
                float trips = u > 0.28f && u < 0.8f ? 0.5f - 0.5f * Mathf.Cos((u - 0.28f) / 0.52f * Mathf.PI * 4f) : 0f;
                float flick = Bell(u, 0.84f, 0.88f, 0.9f, 0.96f);
                p.SetArm(false, 38f, ao - 8f, 40f, 104f, 40f, -30f * flick);
                p.SetArm(true, Mathf.Lerp(42f, 16f, trips), Mathf.Lerp(-16f, -34f, trips), Mathf.Lerp(50f, 64f, trips), Mathf.Lerp(100f, 58f, trips), 40f, 0f);
                p.Grip(false); p.SetHand(true, 0.45f, 0.35f, 0.5f);
                p.R[(int)HBone.Chest] *= ActorPose.Body(4f, -6f * trips, 0f);
            }
        }
    }
}
