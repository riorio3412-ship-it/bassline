using System;
using System.Globalization;
using System.Linq;
using BL23.Sim;
using BL23.Sim.Physicality;
using Newtonsoft.Json.Linq;

static class IntegrationProgram
{
    static int passed;
    static void Main()
    {
        Test("saved contact and physical response roundtrip", ContactRoundtrip);
        Test("old save without physical fields remains loadable", OldSave);
        Test("wound events encode contact invariantly", InvariantEvent);
        Test("glancing physical attack retains direction and scales injury", AngledAttack);
        Console.WriteLine($"Physicality integration: {passed} tests passed.");
    }
    static void Test(string name, Action action)
    {
        try { action(); ++passed; Console.WriteLine("PASS " + name); }
        catch (Exception e) { Console.Error.WriteLine("FAIL " + name + ": " + e); Environment.Exit(1); }
    }
    static void Check(bool yes, string why) { if (!yes) throw new InvalidOperationException(why); }
    static void Near(float actual, float expected) => Check(Math.Abs(actual - expected) < .00001f, $"Expected {expected}, got {actual}");
    static void ContactRoundtrip()
    {
        var s = new GameState(); var a = new Actor { Id = "P03", PhysicsDriven = true, Physical = new PhysicalBody() };
        a.Body.Wounds.Add(new Wound { Region = BodyRegion.ArmR, Type = DamageType.Cut, Sev = 2, lx = .3f, ly = 1.35f, lz = -.1f,
            HasContact = true, dx = .6f, dy = -.2f, dz = -.8f, nx = -.6f, ny = 0, nz = .8f, ContactImpulse = 32.5f, ContactEnergy = 44.25f });
        BodySolver.ApplyImpact(a.Physical, new ImpactInput { Region = BodyRegion.ArmR, Type = DamageType.Cut, EnergyJoules = 44.25f, Impulse = 32.5f,
            DirectionX = .6f, DirectionY = -.2f, DirectionZ = -.8f, HasContact = true, ContactX = .3f, ContactY = 1.35f, ContactZ = -.1f });
        s.Actors[a.Id] = a;
        var restored = SaveStore.Deserialize(SaveStore.Serialize(s)).A(a.Id); var w = restored.Body.Wounds.Single();
        Check(w.HasContact && w.Region == BodyRegion.ArmR && w.Type == DamageType.Cut, "contact metadata lost");
        Near(w.dx, .6f); Near(w.dy, -.2f); Near(w.dz, -.8f); Near(w.lx, .3f); Near(w.ly, 1.35f); Near(w.lz, -.1f);
        Near(w.nx, -.6f); Near(w.nz, .8f); Near(w.ContactImpulse, 32.5f); Near(w.ContactEnergy, 44.25f);
        Near(restored.Physical.ReactionDirectionX, a.Physical.ReactionDirectionX); Near(restored.Physical.ContactY, 1.35f);
        Near(restored.Physical.Region(BodyRegion.ArmR).Cut, a.Physical.Region(BodyRegion.ArmR).Cut);
        Check(!restored.PhysicsDriven, "Transient Unity physics ownership was persisted.");
    }
    static void OldSave()
    {
        var s = new GameState(); var a = new Actor { Id = "P03" }; a.Body.Wounds.Add(new Wound { Region = BodyRegion.Head, Type = DamageType.Blunt, Sev = 1 }); s.Actors[a.Id] = a;
        var json = JObject.Parse(SaveStore.Serialize(s)); var actor = (JObject)json["Actors"]["P03"]; actor.Remove("Physical");
        var wound = (JObject)actor["Body"]["Wounds"][0];
        foreach (var key in new[] { "HasContact", "dx", "dy", "dz", "nx", "ny", "nz", "ContactImpulse", "ContactEnergy" }) wound.Remove(key);
        var old = SaveStore.Deserialize(json.ToString()).A("P03");
        Check(old.Physical == null && old.Body.Wounds.Count == 1 && !old.Body.Wounds[0].HasContact, "Old saves require new fields.");
        var b = old.Physical ?? (old.Physical = new PhysicalBody()); BodySolver.ApplyLegacyLimits(b, old.Body.Mobility, old.Body.HandL, old.Body.HandR, old.Body.Conscious, old.Body.Dead);
        Check(b.CanAct, "Old save failed physical initialization.");
    }
    static Simulation Fresh()
    {
        var sim = Simulation.NewCampaign(20260927); sim.Headless = true; sim.S.Phase = Phase.Daily; sim.S.Out.Clear();
        var p = sim.S.Player; var v = sim.S.A("P03"); v.Pos = new P3(p.Pos.f, p.Pos.x, p.Pos.z + 1); v.Room = p.Room; v.Yaw = 180;
        var item = new Item { Id = "qa:knife", Type = "KitchenKnife", Holder = p.Id, InHand = true }; sim.S.Items[item.Id] = item; p.HandR = item.Id;
        return sim;
    }
    static void InvariantEvent()
    {
        var sim = Fresh(); var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            sim.Strike(Cast.Player, sim.S.A("P03"), BodyRegion.ArmR, DamageType.Cut, 1, "qa:knife", "qa", .125f, 1.25f, -.375f,
                .6f, -.2f, -.8f, 12.5f, 32.25f, -.6f, .2f, .8f);
            var fields = sim.S.Out.Last(e => e.Type == GameEventType.Wound).Data.Split('|');
            Check(fields.Length >= 9 && fields[4] == "0.125,1.25,-0.375", "Event uses locale decimal separators: " + string.Join("|", fields));
            Near(float.Parse(fields[6], CultureInfo.InvariantCulture), 12.5f);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
    static void AngledAttack()
    {
        var direct = Fresh(); var glancing = Fresh();
        direct.PlayerStrike(direct.S.A("P03"), BodyRegion.ArmR, .2f, 1.2f, 0, 0, 0, 1, 20, 40, 0, 0, -1, 1f);
        glancing.PlayerStrike(glancing.S.A("P03"), BodyRegion.ArmR, .2f, 1.2f, 0, 1, 0, 0, 8, 10, 0, 0, -1, .35f);
        var d = direct.S.A("P03").Body.Wounds.Last(); var g = glancing.S.A("P03").Body.Wounds.Last();
        Check(d.Sev > g.Sev, $"Angle strength did not affect severity ({d.Sev}/{g.Sev}).");
        Near(d.dz, 1); Near(g.dx, 1); Check(d.HasContact && g.HasContact, "Runtime contact direction lost by PlayerStrike.");
    }
}
