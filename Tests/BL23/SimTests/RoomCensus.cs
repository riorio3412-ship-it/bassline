using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Sim;

/// <summary>
/// rooms [n] — the mansion's rooms over n layouts: how often each room type appears (per floor), and which types the house's
/// evenings, the hunt and the residents' own evenings can use. For adding room types (SocialEventsDesign §5).
/// </summary>
public static partial class Program
{
    static int RoomCensus(string[] args)
    {
        int n = args.Length > 1 ? int.Parse(args[1]) : 20;
        var count = new Dictionary<string, int>(); var leftovers = new Dictionary<int, List<int>>();
        for (int s = 1; s <= n; s++)
        {
            var sim = Simulation.NewCampaign((ulong)s, 4); var L = sim.S.Layout;
            foreach (var r in L.Rooms.Where(r => !r.Void)) { var k = $"{r.Floor}:{r.Type}"; count[k] = (count.TryGetValue(k, out var c) ? c : 0) + 1; }
        }
        foreach (var f in new[] { 0, 1, -1, -2 })
            Console.WriteLine($"floor {f}: " + string.Join(", ", count.Where(kv => kv.Key.StartsWith(f + ":")).OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key.Substring(kv.Key.IndexOf(':') + 1)} {kv.Value}")));
        return 0;
    }
}
