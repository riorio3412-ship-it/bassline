using System;
using BL23.Sim;
using BL23.Sim.Physicality;

static class Program
{
    static int passed;
    static void Main()
    {
        Test("neutral contact does not injure or react", Neutral);
        Test("push unbalances without adding wounds", Push);
        Test("mass, leverage and support govern balance", BalanceMechanics);
        Test("cut, stab and blunt preserve separate regional channels", DamageChannels);
        Test("injured hand drops without disabling opposite hand", GripFailure);
        Test("lower priority startle cannot replace protective fall", Priority);
        Test("unconscious legacy state cannot recover independently", LegacyAuthority);
        Test("legacy recovery is accepted without multiplying limits", LegacyRecovery);
        Test("fall grounds before prone and get-up", FallRecovery);
        Test("two unusable legs prevent automatic standing", LegFailure);
        Test("dt partitions produce equivalent recovery", DeterministicTime);
        Test("pause is a true no-op and invalid dt is rejected", TimeValidation);
        Test("old or sparse regional state repairs safely", OldSave);
        Test("reset clears reactions and optional injuries", Reset);
        Test("invalid contact numbers do not poison state", InvalidNumbers);
        Test("severing only happens through authoritative import", Severing);
        Test("zero recovery profile never auto stands", ZeroRecovery);
        Test("front, side, rear and off-centre contact differ", DirectionalBalance);
        Test("glancing punctures transfer less energy than aligned contact", Incidence);
        Test("reaction stores accepted local contact and direction", ContactContext);
        Console.WriteLine($"Physicality: {passed} tests passed.");
    }

    static ImpactInput Hit(BodyRegion region = BodyRegion.Chest, DamageType type = DamageType.Blunt, float energy = 25, float impulse = 15)
        => new ImpactInput { Region = region, Type = type, EnergyJoules = energy, Impulse = impulse };
    static void Test(string label, Action test)
    {
        try { test(); ++passed; Console.WriteLine("PASS " + label); }
        catch (Exception e) { Console.Error.WriteLine("FAIL " + label + ": " + e.Message); Environment.Exit(1); }
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Near(float actual, float expected, float epsilon = 0.0001f) => Check(Math.Abs(actual - expected) <= epsilon, $"Expected {expected}, got {actual}");
    static void Advance(PhysicalBody b, float seconds, float dt, bool grounded = true, bool support = false)
    {
        int count = (int)Math.Round(seconds / dt);
        for (int i = 0; i < count; ++i) BodySolver.Step(b, dt, grounded, support);
    }
    static void Neutral()
    {
        var b = new PhysicalBody(); var r = BodySolver.ApplyImpact(b, default);
        Check(r.Reaction == PhysicalReaction.None && r.Severity == 0, "neutral hit reacted"); Near(b.Balance, 1); Near(b.Pain, 0);
    }
    static void Push()
    {
        var b = new PhysicalBody(); var hit = Hit(energy: 15, impulse: 65); hit.TransientOnly = true;
        BodySolver.ApplyImpact(b, hit);
        Check(b.Posture == PhysicalPosture.Staggering, "push should stagger"); Near(b.Region(BodyRegion.Chest).Bruise, 0);
        Check(b.Balance < 0.7f && b.Pain > 0f, "push should produce momentum response");
    }
    static void BalanceMechanics()
    {
        var light = new PhysicalBody(); var heavy = new PhysicalBody(); var supported = new PhysicalBody(); var elevated = new PhysicalBody();
        var h = Hit(energy: 0, impulse: 50); h.TransientOnly = true;
        BodySolver.ApplyImpact(light, h, new PhysicalityTuning { BodyMassKg = 50 });
        BodySolver.ApplyImpact(heavy, h, new PhysicalityTuning { BodyMassKg = 100 });
        h.Supported = true; BodySolver.ApplyImpact(supported, h, new PhysicalityTuning { BodyMassKg = 50 });
        h.Supported = false; h.Leverage = 1.5f; BodySolver.ApplyImpact(elevated, h, new PhysicalityTuning { BodyMassKg = 50 });
        Check(heavy.Balance > light.Balance && supported.Balance > light.Balance && elevated.Balance < light.Balance, "physical modifiers ignored");
    }
    static void DamageChannels()
    {
        var b = new PhysicalBody();
        BodySolver.ApplyImpact(b, Hit(BodyRegion.HandL, DamageType.Cut));
        BodySolver.ApplyImpact(b, Hit(BodyRegion.ArmR, DamageType.Stab));
        BodySolver.ApplyImpact(b, Hit(BodyRegion.LegL, DamageType.Blunt));
        Check(b.Region(BodyRegion.HandL).Cut > 0 && b.Region(BodyRegion.HandL).Puncture == 0, "cut mapped incorrectly");
        Check(b.Region(BodyRegion.ArmR).Puncture > 0 && b.Region(BodyRegion.ArmR).Bruise == 0, "stab mapped incorrectly");
        Check(b.Region(BodyRegion.LegL).Bruise > 0 && b.Region(BodyRegion.LegL).Cut == 0, "blunt mapped incorrectly");
        Near(b.Region(BodyRegion.Head).Integrity, 1);
    }
    static void GripFailure()
    {
        var b = new PhysicalBody(); ImpactResult r = default;
        for (int i = 0; i < 5; ++i) r = BodySolver.ApplyImpact(b, Hit(BodyRegion.HandL, DamageType.Crush, 160, 0));
        Check(r.ShouldDropLeft && !r.ShouldDropRight, "hand loss not localized"); Near(b.GripRight, 1);
    }
    static void Priority()
    {
        var b = new PhysicalBody(); BodySolver.ApplyImpact(b, Hit(impulse: 160));
        var rev = b.ReactionRevision;
        Check(b.Reaction == PhysicalReaction.Fall, "not falling");
        Check(!BodySolver.RequestStartle(b, 1), "startle preempted fall");
        Check(b.ReactionRevision == rev && b.Reaction == PhysicalReaction.Fall, "reaction priority was corrupted");
    }
    static void LegacyAuthority()
    {
        var b = new PhysicalBody(); BodySolver.ApplyLegacyLimits(b, 0.7f, 1, 1, 0.05f, false);
        long revision = b.ReactionRevision;
        BodySolver.ApplyLegacyLimits(b, 0.7f, 1, 1, 0.05f, false);
        Check(b.ReactionRevision == revision, "unchanged legacy state retriggered reaction");
        Advance(b, 10, 0.1f);
        Check(!b.CanAct && b.Posture == PhysicalPosture.Incapacitated, "unconscious body auto woke"); Near(b.Consciousness, 0.05f);
        BodySolver.ApplyLegacyLimits(b, 1, 1, 1, 1, true); Advance(b, 10, 0.1f);
        Check(b.Dead && !BodySolver.RequestStartle(b, 1), "dead body reacted");
    }
    static void LegacyRecovery()
    {
        var b = new PhysicalBody(); BodySolver.ApplyLegacyLimits(b, 0.5f, 0.6f, 0.7f, 1, false);
        for (int i = 0; i < 100; ++i) BodySolver.ApplyLegacyLimits(b, 0.5f, 0.6f, 0.7f, 1, false);
        Near(b.Mobility, 0.5f); Near(b.GripLeft, 0.6f);
        BodySolver.ApplyLegacyLimits(b, 0.5f, 0.6f, 0.7f, 0f, false);
        BodySolver.ApplyLegacyLimits(b, 1, 1, 1, 1, false);
        Check(b.Posture == PhysicalPosture.Prone, "wakeup skipped recovery"); Advance(b, 5, 0.02f);
        Check(b.CanAct && b.Posture == PhysicalPosture.Standing, "legacy recovery stuck"); Near(b.Mobility, 1);
    }
    static void FallRecovery()
    {
        var b = new PhysicalBody(); var h = Hit(energy: 0, impulse: 160); h.TransientOnly = true; BodySolver.ApplyImpact(b, h);
        Advance(b, 1, 0.02f, false); Check(b.Posture == PhysicalPosture.Falling, "midair auto got up");
        Advance(b, 0.1f, 0.02f); Check(b.Posture != PhysicalPosture.Standing, "ground contact instantly stood");
        Advance(b, 5, 0.02f); Check(b.Posture == PhysicalPosture.Standing && b.CanAct, "did not recover after landing");
    }
    static void LegFailure()
    {
        var b = new PhysicalBody(); BodySolver.MarkSevered(b, BodyRegion.LegL); BodySolver.MarkSevered(b, BodyRegion.LegR);
        BodySolver.ApplyImpact(b, Hit(impulse: 160)); Advance(b, 10, 0.02f);
        Check(b.Posture == PhysicalPosture.Prone && !b.CanAct, "unsupported body stood up"); Near(b.Mobility, 0);
    }
    static void DeterministicTime()
    {
        var a = new PhysicalBody(); var b = new PhysicalBody();
        BodySolver.ApplyImpact(a, Hit(energy: 100, impulse: 150)); BodySolver.ApplyImpact(b, Hit(energy: 100, impulse: 150));
        Advance(a, 3, 0.01f); Advance(b, 3, 0.05f);
        Near(a.Pain, b.Pain, 0.001f); Near(a.Balance, b.Balance, 0.015f); Near(a.Alarm, b.Alarm, 0.001f);
        Check(a.Posture == b.Posture, "frame partitions change posture");
    }
    static void TimeValidation()
    {
        var b = new PhysicalBody(); BodySolver.RequestStartle(b, 1); float time = b.ReactionRemaining;
        BodySolver.Step(b, 0, true, false); Near(time, b.ReactionRemaining);
        foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity, 61f })
        {
            bool threw = false; try { BodySolver.Step(b, invalid, true, false); } catch (ArgumentOutOfRangeException) { threw = true; }
            Check(threw, "invalid time accepted");
        }
    }
    static void OldSave()
    {
        var b = new PhysicalBody { Regions = new[] { new RegionState { Region = BodyRegion.HandR, Cut = 0.7f } } };
        BodySolver.Step(b, 0.1f, true, false); Near(b.Region(BodyRegion.HandR).Cut, 0.7f);
        b.Regions[(int)BodyRegion.LegL] = null; BodySolver.ApplyLegacyLimits(b, 1, 1, 1, 1, false);
        Check(b.Region(BodyRegion.LegL) != null, "missing regional entry not restored");
    }
    static void Reset()
    {
        var b = new PhysicalBody(); BodySolver.MarkSevered(b, BodyRegion.HandL); BodySolver.RequestStartle(b, 1);
        BodySolver.Reset(b, false); Check(b.Region(BodyRegion.HandL).Severed && b.Reaction == PhysicalReaction.None, "preserve injuries reset failed");
        BodySolver.Reset(b); Check(!b.Region(BodyRegion.HandL).Severed && b.CanAct, "full reset failed"); Near(b.GripLeft, 1);
    }
    static void InvalidNumbers()
    {
        var b = new PhysicalBody(); BodySolver.ApplyImpact(b, Hit(energy: float.NaN, impulse: float.PositiveInfinity));
        Near(b.Balance, 1); Near(b.Pain, 0);
        BodySolver.ApplyLegacyLimits(b, float.NaN, -4, 10, 1, false);
        Near(b.Mobility, 0); Near(b.GripLeft, 0); Near(b.GripRight, 1);
    }
    static void Severing()
    {
        var b = new PhysicalBody(); for (int i = 0; i < 10; ++i) BodySolver.ApplyImpact(b, Hit(BodyRegion.ArmR, DamageType.Cut, 200, 0));
        Check(!b.Region(BodyRegion.ArmR).Severed, "damage invented authoritative dismemberment");
        BodySolver.MarkSevered(b, BodyRegion.ArmR); Check(b.Region(BodyRegion.ArmR).Severed, "explicit severing ignored"); Near(b.GripRight, 0);
    }
    static void ZeroRecovery()
    {
        var b = new PhysicalBody(); BodySolver.ApplyImpact(b, Hit(impulse: 160));
        BodySolver.Step(b, 10, true, false, new PhysicalityTuning { RecoverySpeed = 0 });
        Check(b.Posture == PhysicalPosture.Prone, "zero recovery still got up");
    }
    static void DirectionalBalance()
    {
        var front = new PhysicalBody(); var rear = new PhysicalBody(); var side = new PhysicalBody(); var edge = new PhysicalBody(); var leg = new PhysicalBody();
        var h = Hit(energy: 0, impulse: 35); h.TransientOnly = true; h.DirectionZ = -1;
        BodySolver.ApplyImpact(front, h); h.DirectionZ = 1; BodySolver.ApplyImpact(rear, h);
        h.DirectionZ = 0; h.DirectionX = 1; BodySolver.ApplyImpact(side, h);
        h.HasContact = true; h.ContactZ = .4f; BodySolver.ApplyImpact(edge, h);
        h.HasContact = false; h.DirectionX = 0; h.DirectionZ = -1; h.Region = BodyRegion.LegL; BodySolver.ApplyImpact(leg, h);
        Check(rear.Balance < front.Balance && side.Balance < front.Balance && edge.Balance < side.Balance && leg.Balance < front.Balance, "direction, offset or low limb contact ignored");
    }
    static void Incidence()
    {
        var aligned = new PhysicalBody(); var glance = new PhysicalBody(); var slash = new PhysicalBody();
        var h = Hit(type: DamageType.Stab, energy: 90, impulse: 0); h.DirectionZ = -1; h.NormalZ = 1;
        var a = BodySolver.ApplyImpact(aligned, h); h.NormalZ = 0; h.NormalX = 1; var g = BodySolver.ApplyImpact(glance, h);
        h.Type = DamageType.Cut; var s = BodySolver.ApplyImpact(slash, h);
        Check(a.EnergyTransferred > g.EnergyTransferred && aligned.Region(BodyRegion.Chest).Puncture > glance.Region(BodyRegion.Chest).Puncture, "puncture angle ignored");
        Check(s.EnergyTransferred > g.EnergyTransferred, "tangential cutting not distinguished from puncture");
    }
    static void ContactContext()
    {
        var b = new PhysicalBody(); var h = Hit(BodyRegion.ShoulderR, impulse: 50);
        h.DirectionX = 10; h.HasContact = true; h.ContactX = .2f; h.ContactY = 1.4f; h.SourceId = "side";
        BodySolver.ApplyImpact(b, h); Near(b.ReactionDirectionX, 1); Near(b.ContactY, 1.4f);
        Check(b.ReactionRegion == BodyRegion.ShoulderR, "region context lost");
        h.Impulse = 1; h.EnergyJoules = 1; h.DirectionX = 0; h.DirectionZ = 1; h.ContactY = .3f;
        BodySolver.ApplyImpact(b, h); Near(b.ReactionDirectionX, 1); Near(b.ContactY, 1.4f);
    }
}
