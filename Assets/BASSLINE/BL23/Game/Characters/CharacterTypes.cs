namespace BL23.Game.Characters
{
    public enum Posture { Stand, Sit, Crouch, Kneel, LieBack, LieFront, LieSide, Slumped }

    /// <summary>
    /// Psychological "ink shadow" faces (gothic manga trope): HollowGrin = the whole face falls into shadow, pale
    /// glowing ring eyes with pinprick pupils and a wide thin crescent grin; CorneredStare = shadow over the upper face,
    /// wide eyes with tiny trembling pupils, a rigid twitching smile; VeiledSmirk = half-shadowed face, half-lidded
    /// glowing irises, a small closed smile, finger to the lips and a head tilt.
    /// </summary>
    public enum DarkFace { None, HollowGrin, CorneredStare, VeiledSmirk }

    public enum Gesture
    {
        None, Talk, TalkEmphatic, Point, Think, CrossArms, Shrug, Nod, ShakeHead, HandOnChest, Wave, Bow,
        Surprised, Flinch, Cower, Angry, Cry, Laugh, Listen, LookAround, Present, Slam, Clap, Pray,
        FingerToLips,
        // charpolish step 0 (contract, append only): hands to the mouth (scream / shock), a startled step back
        HandsToMouth, Recoil,
        // charpolish step 0b (contract, append only): calling others over to a body (three-witness rule), looking away from a body
        CallOut, CoverEyes,
        // motion track 2026-09-27 (append only; ViolenceMotionContract.md "Published names"): a hand over the mouth in horror,
        // comforting a crying person (hand on their shoulder, head tilted), an embrace, checking a pocket watch, tugging cuffs /
        // collar, shivering with the arms wrapped, a small apologetic bow with a raised palm, ducking with the hands over the head
        // (gunshot, crash), jerking a burnt hand back and cradling it, hands raised in surrender, a slow weight shift
        CoverMouth, Console, Hug, CheckWatch, AdjustClothes, Shiver, Apologize, DuckCover, HandToBurn, HandsUp, WeightShift
    }

    public enum ActionAnim
    {
        None, PickUp, PutDown, Use, Operate, Eat, Drink, Cook, Read, Write, Clean, Wash, Knock, OpenDoor,
        Stab, Slash, Overhead, Shove, Strangle, Carry, Drag, Struggle, Fall, Stagger, Crawl, Hurt, FirstAid, Play, Sleep,
        Throw, Garden, Craft, Examine, Search, Swim,
        // charpolish step 0 (contract, append only): crouched hiding loop, exercise loop (kernel Anim.Hide / Anim.Exercise)
        Hide, Exercise,
        // charpolish step 0b (contract, append only): weapon attacks by class (anticipation, strike, follow-through, recovery),
        // the other murder acts, the victim's defensive reactions and the weapon-ready stance. Kernel Anim values
        // (Stab, Slash, Overhead, Strangle, Shove, Struggle...) map onto ActionAnim in ActorView.PlayAnim / OnStrike.
        SwingSide, SwingHeavy, Garrote, Smother, PourPoison, Defend, GrabWrist, Guard,
        // motion track 2026-09-27 (append only; ViolenceMotionContract.md "Published names")
        // reactions: get up from the floor, startle jump / turn, slip on a wet floor, stumble on stairs, head snap, doubled over,
        // hand pressed on a wound
        GetUp, Startle, Slip, StumbleStairs, HitHead, HitGut, ClutchWound,
        // everyday body actions: give / receive (pair with PhysicalActionController.BeginHandover), lean on a table, push a chair in,
        // open a drawer, carry a heavy load with both hands, drag a body by the armpits / the ankles, lift a body onto the shoulder,
        // pour (water, tea, oil)
        HandOver, Receive, LeanTable, PushChair, OpenDrawer, CarryHeavy, DragArmpits, DragAnkles, LiftBody, Pour,
        // attacks: underhand / overhand stab, kick, firearm aim (hold) / shoot (recoil) / reload / draw from the coat / put away,
        // crossbow cocking (slow, foot in the stirrup)
        StabUnder, StabOver, Kick, Aim, Shoot, Reload, DrawWeapon, Holster, CockCrossbow,
        // victim defence: turn the body away and shield the head, a quick dodge back
        TurnAway, Dodge,
        // paired kills and restraint (see ActorAnimator.BeginPaired / SetRestraint): drowning, front ligature, tying someone up,
        // untying, straining against bonds, hopping with bound ankles
        Drown, LigatureFront, TieUp, Untie, StruggleBonds, Hop
    }

    /// <summary>
    /// charpolish step 0b: how a held item is gripped and swung. Derived from the kernel ItemDef (damage type, mass, size,
    /// Heavy/TwoHanded) by the motion implementer's classifier; set on the animator with ActorAnimator.SetHeldWeapon.
    /// Knife = short stabbing blade, Blade = short cutting edge, Club = one-handed blunt (candlestick, bottle, wrench),
    /// Heavy = heavy compact object (bust, iron, bookend: two hands), Long = long shaft (poker, pipe, crowbar, cue),
    /// Cord = rope / wire / scarf, Pillow = cushion, Vial = poison bottle or packet.
    /// </summary>
    public enum WeaponClass { None, Knife, Blade, Club, Heavy, Long, Cord, Pillow, Vial,
        // motion track 2026-09-27 (append only): revolver / dueling pistol (one hand), hunting shotgun / rifle (two hands, stock to the
        // shoulder), crossbow (two hands, cocked slowly)
        Pistol, Rifle, Crossbow }

    /// <summary>
    /// charpolish step 0b: grip points on a body for ActorRig.PinLimb (shoulder carry, dragging by the armpits, the wrists or
    /// the ankles). The pinned point follows its target; the rest of the body hangs or trails physically.
    /// </summary>
    public enum HumanLimb { Hips, Chest, Head, ArmpitL, ArmpitR, HandL, HandR, ThighL, ThighR, AnkleL, AnkleR }

    /// <summary>Scream (appended by charpolish step 0): eyes wide, mouth wide open, brows up; used for screams / discoveries.</summary>
    public enum Expr { Neutral, Smile, Grin, Angry, Sad, Surprised, Fear, Smirk, Disgust, Blank, Dead, Pain, Crying, Laugh, Break, Scream,
        // motion track 2026-09-27 (append only): Choke = gasping for air (eyes wide, mouth working), Panic = wide-eyed terror, trembling mouth
        Choke, Panic }

    public enum BodyRegion { Head, Neck, Chest, Abdomen, ShoulderL, ShoulderR, ArmL, ArmR, HandL, HandR, LegL, LegR, FootL, FootR, Back }

    /// <summary>motion track 2026-09-27: prolonged two-person kills (ActorAnimator.BeginPaired). Strangle = both hands on the throat, face
    /// to face; GarroteRear = cord from behind; LigatureFront = cord from the front; Smother = pillow pressed on a lying victim's
    /// face; Drown = a fist in the hair forcing a kneeling victim's head into water (bath, basin, fountain, well).</summary>
    public enum PairedKind { None, Strangle, GarroteRear, LigatureFront, Smother, Drown }

    public enum PairedRole { Attacker, Victim }

    /// <summary>motion track 2026-09-27: bindings on an actor (ActorAnimator.SetRestraint). Wrists in front or behind, ankles
    /// (hop / crawl only), a gag over the mouth.</summary>
    [System.Flags]
    public enum RestraintFlags { None = 0, WristsFront = 1, WristsBack = 2, Ankles = 4, Gag = 8 }

    /// <summary>motion track 2026-09-27: where a dragger holds a body (ActorAnimator.SetDragging).</summary>
    public enum DragGrip { Armpits, Ankles, Wrists }
}
