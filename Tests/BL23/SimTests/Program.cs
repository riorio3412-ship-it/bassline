using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using BL23.Sim;

public static partial class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        string mode = args.Length > 0 ? args[0] : "layout";
        try
        {
            switch (mode)
            {
                case "layout": return LayoutTest(args);
                case "gore": return GoreTests.Run(args);
                default:
                    return Extra(mode, args);
            }
        }
        catch (Exception e) { Console.WriteLine("FATAL " + e); return 2; }
    }

    static int LayoutTest(string[] args)
    {
        int n = args.Length > 1 ? int.Parse(args[1]) : 5;
        bool draw = args.Length > 2 && args[2] == "draw";
        int fails = 0; var sw = Stopwatch.StartNew();
        for (int loop = 1; loop <= n; loop++)
        {
            var rngs = new RngSet { CampaignSeed = 12345UL + (ulong)loop * 7 };
            try
            {
                var L = LayoutGenerator.Generate(rngs.CampaignSeed, loop, rngs);
                var types = L.Rooms.GroupBy(r => r.Type).Select(g => $"{g.Key}:{g.Count()}");
                Console.WriteLine($"loop {loop} skel={L.Skeleton} attempts={L.Attempts} rooms={L.Rooms.Count} doors={L.Doors.Count} furn={L.Furniture.Count} spots={L.Spots.Count} items={L.ItemSpawns.Count} mystery={string.Join(",", L.MysteryTypes)} hash={L.Hash}");
                Console.WriteLine("   " + string.Join(" ", types));
                if (draw) { Draw(L, 0); Draw(L, 1); Draw(L, -1); }
                foreach (var g in L.GenLog.Where(x => x.Contains("error"))) Console.WriteLine("   " + g);
                foreach (var (rt, ft) in new[] { (RoomType.PowerRoom, "Switchboard"), (RoomType.PowerRoom, "Generator"), (RoomType.MachineRoom, "PressConsole"), (RoomType.MachineRoom, "Press"), (RoomType.Kitchen, "Stove"), (RoomType.Pool, "PoolWater"), (RoomType.Chapel, "Pew"), (RoomType.BoilerRoom, "Boiler") })
                    foreach (var r in L.Rooms.Where(r => r.Type == rt)) if (!r.Furniture.Any(id => L.Furniture[id].Type == ft)) { Console.WriteLine($"   MISSING {ft} in {rt} {r.Rect.W:0.0}x{r.Rect.D:0.0}"); fails++; }
                // determinism
                var rngs2 = new RngSet { CampaignSeed = rngs.CampaignSeed };
                var L2 = LayoutGenerator.Generate(rngs2.CampaignSeed, loop, rngs2);
                if (L2.Hash != L.Hash) { Console.WriteLine("   NONDETERMINISTIC"); fails++; }
            }
            catch (Exception e) { Console.WriteLine($"loop {loop} FAIL {e.Message}"); fails++; }
        }
        Console.WriteLine($"done fails={fails} time={sw.ElapsedMilliseconds}ms");
        return fails == 0 ? 0 : 1;
    }

    static void Draw(Layout L, int f)
    {
        var fi = L.Floor(f); var g = L.Nav(f);
        Console.WriteLine($"--- floor {f} ---");
        var sb = new StringBuilder();
        for (int j = g.NZ - 1; j >= 0; j -= 2)
        {
            for (int i = 0; i < g.NX; i += 1)
            {
                int k = i + j * g.NX; int r = g.Room[k];
                char c;
                if (r < 0) c = ' ';
                else
                {
                    var room = L.Rooms[r];
                    if (g.Block[k]) c = '#';
                    else if (room.Type == RoomType.Corridor || room.Type == RoomType.Landing) c = '.';
                    else if (room.Type == RoomType.GrandHall) c = ':';
                    else c = Sym(room.Type);
                    if (g.EdgeE[k] >= 0 || g.EdgeN[k] >= 0) c = 'D';
                    else if (g.EdgeE[k] == -2 || (i > 0 && g.EdgeE[k - 1] == -2)) { if (!g.Block[k]) c = c == '.' ? '|' : char.ToLower(c) == c ? c : c; }
                }
                sb.Append(c);
            }
            sb.AppendLine();
        }
        Console.Write(sb.ToString());
        foreach (var r in L.Rooms.Where(r => r.Floor == f && !RoomInfo.IsPassage(r.Type))) Console.Write($"{Sym(r.Type)}={r.Name}({r.Rect.W}x{r.Rect.D}) ");
        Console.WriteLine();
    }

    static char Sym(RoomType t)
    {
        switch (t)
        {
            case RoomType.Dining: return 'I'; case RoomType.Kitchen: return 'K'; case RoomType.Lounge: return 'L'; case RoomType.Library: return 'B';
            case RoomType.Archive: return 'A'; case RoomType.MusicRoom: return 'M'; case RoomType.Theater: return 'T'; case RoomType.Greenhouse: return 'G';
            case RoomType.Infirmary: return '+'; case RoomType.Laundry: return 'W'; case RoomType.Workshop: return 'H'; case RoomType.Storage: return 'S';
            case RoomType.GameRoom: return 'Y'; case RoomType.Pool: return '~'; case RoomType.WaterRoom: return 'w'; case RoomType.PowerRoom: return 'P';
            case RoomType.MachineRoom: return 'X'; case RoomType.Gallery: return 'E'; case RoomType.Wardrobe: return 'O'; case RoomType.Chapel: return 'C';
            case RoomType.Bedroom: return 'b'; case RoomType.ButlerRoom: return 'U'; case RoomType.Elevator: return 'V'; case RoomType.Parlor: return 'p';
            case RoomType.Closet: return 'c'; case RoomType.Stairwell: return '='; case RoomType.Courtroom: return 'J';
        }
        return RoomInfo.IsMystery(t) ? '?' : '*';
    }
}
