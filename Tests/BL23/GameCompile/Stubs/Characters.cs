// Compile-only stub of the character module contract (Documentation/BL23/Contracts.md §2). Not shipped.
using UnityEngine;
namespace BL23.Game.Characters
{
    public enum Posture { Stand, Sit, Crouch, Kneel, LieBack, LieFront, LieSide, Slumped }
    public enum Gesture { None, Talk, TalkEmphatic, Point, Think, CrossArms, Shrug, Nod, ShakeHead, HandOnChest, Wave, Bow, Surprised, Flinch, Cower, Angry, Cry, Laugh, Listen, LookAround, Present, Slam, Clap, Pray }
    public enum ActionAnim { None, PickUp, PutDown, Use, Operate, Eat, Drink, Cook, Read, Write, Clean, Wash, Knock, OpenDoor, Stab, Slash, Overhead, Shove, Strangle, Carry, Drag, Struggle, Fall, Stagger, Crawl, Hurt, FirstAid, Play, Sleep }
    public enum Expr { Neutral, Smile, Grin, Angry, Sad, Surprised, Fear, Smirk, Disgust, Blank, Dead, Pain, Crying, Laugh, Break }
    public enum BodyRegion { Head, Neck, Chest, Abdomen, ShoulderL, ShoulderR, ArmL, ArmR, HandL, HandR, LegL, LegR, FootL, FootR, Back }
    public static class ActorFactory { public static ActorRig Create(BL23.Sim.CastDef def, Transform parent) => null; }
    public class ActorRig : MonoBehaviour
    {
        public string ActorId; public float Height;
        public Transform Hips, Spine, Chest, Neck, Head, HandL, HandR, FootL, FootR, HandAnchorL, HandAnchorR, EyeAnchor, ChestAnchor, HeadTopAnchor;
        public ActorAnimator Anim;
        public void SetExpression(Expr e, float intensity = 1f) { }
        public void SetTalking(bool on) { }
        public void SetBlink(bool enabled) { }
        public void AddWound(BodyRegion r, BL23.Sim.DamageType t, int severity, Vector3 localPoint, bool postmortem) { }
        public void ClearWounds() { }
        public void SetBloodied(float amount) { }
        public void SetWet(bool on) { }
        public void SetDisguise(string itemType) { }
        public void SetVisible(bool on) { }
        public BodyRegion RegionFromCollider(Collider c) => BodyRegion.Chest;
    }
    public class ActorAnimator : MonoBehaviour
    {
        public void SetMove(Vector3 worldVelocity, bool running) { }
        public void SetPosture(Posture p) { }
        public void SetDeadPose(int variant) { }
        public void PlayGesture(Gesture g, float duration = 0f) { }
        public void PlayAction(ActionAnim a, float duration) { }
        public void SetLookAt(Vector3? worldPoint) { }
        public void SetInjury(float mobility, bool leftArm, bool rightArm, bool limpL, bool limpR, bool conscious) { }
        public void SetCarrying(ActorRig carried) { }
        public void SetBreak(float t) { }
    }
}
