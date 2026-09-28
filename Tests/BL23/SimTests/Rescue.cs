using System;
using System.Linq;
using BL23.Sim;

public static partial class Program
{
    /// <summary>Scenario: someone is critically hurt in front of another person — are they carried to the infirmary and treated?</summary>
    static int RescueTest(string[] args)
    {
        int ok = 0, tries = 0;
        for (int k = 0; k < 8; k++)
        {
            var sim = Simulation.NewCampaign(7000UL + (ulong)k, 4); sim.Headless = true; var S = sim.S; sim.EndPrologue();
            sim.RunTicks(600);
            var inf = S.Layout.First(RoomType.Infirmary); if (inf == null) { Console.WriteLine($"seed {7000 + k}: no infirmary"); continue; }
            // pick two NPCs; put them together in a room near the infirmary
            var v = S.LivingNpcs.First(); var r = S.LivingNpcs.Skip(1).First(x => x.Body.Mobility > 0.9f);
            var room = S.Layout.Rooms.Where(x => x.Floor == inf.Floor && !RoomInfo.IsPassage(x.Type) && x.Id != inf.Id).OrderBy(x => new P3(x.Floor, x.Rect.CX, x.Rect.CZ).DistXZ(new P3(inf.Floor, inf.Rect.CX, inf.Rect.CZ))).First();
            v.Pos = sim.RandomPointIn(room, S.R(Stream.Life)); v.Room = room.Id; v.Act = null;
            r.Pos = sim.RandomPointIn(room, S.R(Stream.Life)); r.Room = room.Id; r.Act = null; r.NextThink = S.Clock;
            tries++;
            sim.Strike(null, v, BodyRegion.Abdomen, DamageType.Stab, 3, null, "test");
            if (v.Status == ActorStatus.Active) sim.Collapse(v, "test");
            for (int i = 0; i < 3000; i++) sim.Step();
            var carried = S.Ledger.Any(e => e.Type == "CarryEnd" && e.Target == v.Id && e.Data == "rescue");
            var aided = v.Body.Stabilized || v.Body.Wounds.Any(w => w.Treated);
            Console.WriteLine($"seed {7000 + k}: rescuer {r.Id} victim {v.Id} status={v.Status} carried={carried} treated={aided} victimRoom={S.RoomName(v.Room)}");
            if (carried) ok++;
        }
        Console.WriteLine($"rescue carried {ok}/{tries}");
        return 0;
    }
}
