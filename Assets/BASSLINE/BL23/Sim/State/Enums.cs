using System;

namespace BL23.Sim
{
    public enum Phase { Boot, Prologue, Daily, Investigation, Assembly, Trial, Verdict, Execution, Reveal, Settlement, LoopEpilogue, PlayerDead }

    public enum ActorStatus { Active, Unconscious, Dead, Executed, Escaped }

    public enum BodyRegion { Head, Neck, Chest, Abdomen, ShoulderL, ShoulderR, ArmL, ArmR, HandL, HandR, LegL, LegR, FootL, FootR, Back }

    public enum Pose { Stand, Sit, Crouch, Kneel, LieBack, LieFront, LieSide, Slumped, Sleep }

    public enum Anim
    {
        None, Idle, PickUp, PutDown, Use, Operate, Eat, Drink, Cook, Read, Write, Clean, Wash, Knock, OpenDoor, Stab, Slash, Overhead, Shove,
        Strangle, Carry, Drag, Struggle, Fall, Stagger, Crawl, Hurt, FirstAid, Play, Sleep, Talk, Listen, Think, Point, Cry, Laugh, Pray, Swim,
        Garden, Craft, Exercise, Examine, Photo, Search, Hide, Wave, Bow, Shrug, CrossArms, Surprised, Cower, Angry, Present, Slam
    }

    public enum Emotion { Neutral, Smile, Grin, Angry, Sad, Surprised, Fear, Smirk, Disgust, Blank, Dead, Pain, Crying, Laugh, Break }

    public enum SoundKind { Footsteps, Running, Door, DoorSlam, Knock, Talk, Shout, Scream, Strike, Struggle, Fall, GlassBreak, Crash, Splash, Machine, Press, Switch, Music, Laugh, Cry, Bell, Announcement, Rain, Static, Clock,
        Gunshot,  // (appended, violence track: Sim/Violence/Firearms.cs — heard house-wide) never renumber the members above
        Scrape    // (appended) furniture dragged or shoved across the floor (Sim/Systems/FurnitureChanges.cs)
    }

    public enum EvKind { Sighting, Heard, Testimony, Trace, Body, ObjectState, Document, Record, Announcement, Deduction, Ability }

    public enum PropKind
    {
        AtPlace,        // Actor at Room during [T0,T1]
        NotAtPlace,     // Actor not at Room during window
        WithPerson,     // Actor with Target during window (alibi pair)
        SawActor,       // Actor(observer) saw Target at Room at T0
        Held,           // Actor held Item(type) at T0
        Heard,          // Actor heard Value(sound) from Room at T0
        DeathWindow,    // Target died between T0..T1
        AliveAt,        // Target seen alive at T0
        DeathPlace,     // Target died in Room
        FoundPlace,     // Target found in Room
        Wound,          // Target has wound Value(region/type)
        WeaponType,     // Target killed by damage Value using item type Item
        BodyMoved,      // Value "yes"/"no"
        DoorLocked,     // Door(Room) locked at T0 (Value yes/no)
        LightsOut,      // Room dark during window
        Injured,        // Actor injured at region Value
        Bloodied,       // Actor had blood on clothes at T0
        Wet,            // Actor wet at T0
        Disguised,      // someone in disguise Item seen at Room T0
        Culprit,        // Actor is responsible for Target's death
        Motive,         // Actor had motive Value against Target
        Ability,        // Actor has ability Value
        ItemAt,         // Item at Room at T0
        Lie,            // Actor statement Value was false
        TraceAt,        // trace Value at Room, estimated window
        ItemState,      // Item has state Value (blood, washed, broken, hidden)
        ItemMissing,    // Item(type) missing from Room since before T1
        DoorState,      // door in Room observed Value at T0 (locked/unlocked/open)
        LightsChanged,  // circuit Value switched at about T0
        MachineUsed,    // facility Value used at about T0
        Key,            // Actor had key Item at T0
        Invited,        // A invited by B to Room during [T0,T1], Value "revN" (the version A knew)
        Loaned,         // A received Item(type) from B at T0 (lend/return/courier: Value)
        ClockOffset,    // the clock in Room reads Value minutes off official time
        DeviceRecord,   // device log: A passed/recorded at Room T0 (Value in/out/coverage-partial/playback)
        TrapSet,        // a registered trap existed in Room between T0 (earliest) and T1 (fired/found), Value kind
        Staged          // the scene was staged: Value seal / tod-heat / tod-cold / message / swap (A = victim, B = actual author); learned only by solving it
    }

    public enum LogicResult { Support, Contradict, LimitScope, Irrelevant, NeedPremise, Conditional }

    public enum TrialMode { None, TM01_Debate, TM02_Crossfire, TM03_Witness, TM04_Chain, TM05_Theory, TM06_Joint, TM07_Reconstruct, TM08_FinalDefense, Vote }

    public enum GameEventType
    {
        Speech, Subtitle, Sound, Door, Light, Strike, Wound, Collapse, Death, Rescue, Announcement, ItemMoved, ItemState, Trace, PhaseChanged,
        ClockSkip, Discovery, Evidence, Notice, Relationship, Ability, RuleStart, RuleEnd, Vote, Verdict, Execution, Escape, LoopReset,
        Furniture, Press, Pose, Anim, Emotion, Carry, Disguise, Break, TrialLine, TrialMode, TrialResult, ChapterStart, Save, Debug
    }
}
