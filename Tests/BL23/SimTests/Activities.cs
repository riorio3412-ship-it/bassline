using System;
using System.Linq;
using BL23.Sim;

public static partial class Program
{
    /// <summary>Player spends time with each kind of furniture: outcomes, company, lore, items.</summary>
    static int ActivitiesTest(string[] args)
    {
        var sim = Simulation.NewCampaign(20260926UL, 4); sim.Headless = true; var S = sim.S; sim.EndPrologue();
        sim.RunTicks(3000);
        var done = new System.Collections.Generic.HashSet<string>(); int ok = 0, fail = 0;
        foreach (var f in S.Layout.Furniture.OrderBy(f => f.Id))
        {
            var acts = sim.FurnitureActions(f); if (acts.Count == 0) continue;
            var room = S.Layout.Room(f.Room); if (room == null || room.Type == RoomType.Courtroom || room.Type == RoomType.Bedroom && room.Owner != Cast.Player) continue;
            foreach (var a in acts)
            {
                if (!done.Add(a.Id + ":" + f.Type)) continue;
                if (S.Phase != Phase.Daily) break;
                // stand next to it
                var p = S.Player; p.Pos = sim.SnapPublic(new P3(f.Pos.f, f.Pos.x + 0.9f, f.Pos.z + 0.4f)); p.Room = f.Room;
                if (S.Minute > 21 * 60 || S.Minute < 8 * 60) sim.Wait(Math.Max(10, (1440 - S.Minute + 9 * 60) % 1440));
                var r = sim.PlayerUse(f, a.Id);
                Console.WriteLine($"{ClockFmt.DayHM(S.Clock)} {room.Name} {f.Type} [{a.Label}] {r.Minutes:0}분 → {r.Text}" + (r.Joined != null ? $" (+{Cast.GivenOf(r.Joined)}: {r.JoinLine})" : "") + (r.LoreTitle != null ? $"\n      {r.LoreTitle}: {r.LoreText}" : "") + (r.ItemMade != null ? $" [아이템 {S.I(r.ItemMade)?.Kor}]" : "") + (r.Music != null ? " [음악]" : ""));
                if (r.Text != null && !r.Text.StartsWith("지금은")) ok++; else fail++;
            }
        }
        Console.WriteLine($"activities ok={ok} fail={fail} faults={sim.Faults} clock={ClockFmt.DayHM(S.Clock)}");
        return 0;
    }
}
