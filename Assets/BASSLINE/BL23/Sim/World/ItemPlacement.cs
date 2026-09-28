using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// Settles every initial item spawn somewhere it would naturally be (user: "바닥에 물건들이 너무 이상하게 떨어져 있다 —
    /// 선반 같은 곳에 놓거나 자연스럽게"). Knives on the counter, tools on the workbench, books and papers on desks, glasses on
    /// the sideboard, ornaments on pedestals and consoles; only things that belong on a floor (buckets, mops, crowbars…) stay
    /// down — and then against a wall, clear of doors and walking lanes. Positions are really inside the furniture's top
    /// (rotation and footprint respected, spread so they don't stack) and within arm's reach of a walkable cell, because the
    /// presentation drops each item onto whatever surface lies under its kernel position. Deterministic (layout stream).
    /// Runs once after decoration (LayoutGenerator); crime-scene drops later in play are untouched.
    /// </summary>
    public static class ItemPlacement
    {
        // resting tops lower than ~1.5 m (the presentation drops items from 1.6 m); seats and tall cabinets are not surfaces
        static readonly HashSet<string> Tops = new HashSet<string>
        {
            "LongTable", "CoffeeTable", "RoundTable", "Sideboard", "Counter", "Island", "Desk", "ReadingTable", "Nightstand", "Workbench", "BarCounter",
            "Console", "SideTable", "Chest", "Pedestal", "FileCabinet", "Crates", "Barrel", "GameTable", "ChessTable", "SewingTable", "DevelopTable",
            "Altar", "Trolley", "TeaCart", "Cart", "PoolTable", "Stage", "Bed", "Planter", "Washer", "Piano", "Piano_Upright", "ButlerDesk", "TicketBooth", "CardCatalog", "InfirmaryBed", "Lectern",
        };

        static readonly Dictionary<string, string[]> Prefer = new Dictionary<string, string[]>
        {
            ["tool"] = new[] { "Workbench", "SewingTable", "Crates", "Console", "Desk", "Chest", "Barrel" },
            ["kitchen"] = new[] { "Counter", "Island", "Sideboard", "LongTable", "Trolley" },
            ["table"] = new[] { "Sideboard", "BarCounter", "LongTable", "RoundTable", "CoffeeTable", "TeaCart", "Trolley", "Console", "SideTable", "GameTable", "Counter", "Barrel" },
            ["paper"] = new[] { "Desk", "ReadingTable", "CardCatalog", "Console", "SideTable", "CoffeeTable", "FileCabinet", "ButlerDesk", "TicketBooth", "Lectern", "Chest" },
            ["decor"] = new[] { "Pedestal", "Console", "Sideboard", "SideTable", "Altar", "Chest", "Nightstand", "Stage", "CoffeeTable", "Piano_Upright", "Piano" },
            ["med"] = new[] { "Desk", "InfirmaryBed", "SideTable", "Console" },
            ["cloth"] = new[] { "Chest", "Bed", "Console", "SideTable", "Crates", "SewingTable" },
            ["treat"] = new[] { "CoffeeTable", "RoundTable", "GameTable", "SideTable", "Sideboard", "TeaCart", "Counter", "Console", "Nightstand" },
            ["plant"] = new[] { "Planter", "Console", "Pedestal", "Altar" },
            ["device"] = new[] { "Desk", "Console", "SideTable", "Workbench", "Crates", "Nightstand", "ReadingTable" },
            ["music"] = new[] { "Piano", "Piano_Upright", "Console", "Desk" },
            ["chem"] = new[] { "DevelopTable", "Workbench", "Desk", "Console" },
            ["pool"] = new[] { "PoolTable", "GameTable", "Console" },
        };

        static string Category(string type)
        {
            switch (type)
            {
                case "Hammer": case "Wrench": case "Chisel": case "Pliers": case "Rope": case "Tripwire": case "Thread": case "ExtensionCord": case "Scissors": case "Fuse": case "PaletteKnife": case "GardenShears": case "SkinningKnife": case "Hacksaw": case "BoneSaw": return "tool";
                case "KitchenKnife": case "Cleaver": case "RollingPin": case "FryingPan": case "IcePick": case "Bread": return "kitchen";
                case "WineGlass": case "Bottle": case "Decanter": case "Cup": case "Plate": case "Beer": case "Soda": case "Tea": case "Thermos": return "table";
                case "Document": case "Envelope": case "Book": case "Notebook": case "Invitation": case "LetterOpener": case "Bookend": return "paper";
                case "Vase": case "Candlestick": case "Trophy": case "Statuette": case "WindupToy": case "PaperModel": case "Button": case "Sticker": return "decor";
                case "FirstAidKit": case "Sedative": case "Scalpel": return "med";
                case "Towel": case "Sheet": case "Cloak": case "Raincoat": case "SpareApron": case "Scarf": case "TheaterMask": case "CurtainCord": case "Pillow": return "cloth";
                case "Snack": case "Candy": case "Chocolate": case "HandWarmer": return "treat";
                case "Flower": case "PoisonVial": case "Foxglove": return "plant";
                case "Recorder": case "Camera": case "Flashlight": return "device";
                case "PianoWire": return "music";
                case "DevChemical": case "Bleach": return "chem";
                case "CueStick": return "pool";
                case "Iron": return "tool";
                case "Bucket": case "Mop": case "PipeSection": case "Crowbar": return "floor";
            }
            return "decor";
        }

        /// <summary>After decoration: every spawn onto a fitting surface (or against a wall when it belongs on the floor).</summary>
        public static void Settle(Layout L, Rng rng)
        {
            var used = new Dictionary<int, int>();   // furniture id → items already on it
            int moved = 0, floor = 0, kept = 0;
            foreach (var sp in L.ItemSpawns)
            {
                if (sp.Type == "RoomKey" && sp.Owner != null) { kept++; continue; }   // carried by the owner from the start
                var room = L.Room(sp.Room); if (room == null) { kept++; continue; }
                string cat = Category(sp.Type);
                if (cat == "floor") { if (AgainstWall(L, room, sp.Pos, rng, out var wp)) { sp.Pos = wp; floor++; } continue; }
                Furniture host = null;
                if (sp.Furniture >= 0 && sp.Furniture < L.Furniture.Count && Tops.Contains(L.Furniture[sp.Furniture].Type)) host = L.Furniture[sp.Furniture];
                var p = host != null ? OnTop(L, host, used, rng) : null;
                if (p == null) p = OnSurface(L, room, Prefer.TryGetValue(cat, out var pref) ? pref : Prefer["decor"], sp.Pos, used, rng, out host);
                if (p == null) p = OnSurface(L, room, Tops.ToArray(), sp.Pos, used, rng, out host);
                if (p != null) { sp.Pos = p.Value; sp.Furniture = host?.Id ?? -1; moved++; continue; }
                if (AgainstWall(L, room, sp.Pos, rng, out var fp)) { sp.Pos = fp; floor++; }
            }
            L.GenLog.Add($"items settled: {moved} on surfaces, {floor} against walls, {kept} kept");
        }

        /// <summary>A resting point on one of the given furniture types in this room (nearest to the wanted point first), or null.</summary>
        public static P3? OnSurface(Layout L, Room room, string[] types, P3 near, Dictionary<int, int> used, Rng rng, out Furniture host)
        {
            host = null;
            for (int ti = 0; ti < types.Length; ti++)
            {
                var cands = room.Furniture.Select(i => L.Furniture[i]).Where(f => f.Type == types[ti] && f.Damage == 0).OrderBy(f => (used.TryGetValue(f.Id, out var n) ? n : 0) * 1.5f + f.Pos.DistXZ(near) * 0.15f).ThenBy(f => f.Id).ToList();
                foreach (var f in cands) { var p = OnTop(L, f, used, rng); if (p != null) { host = f; return p; } }
            }
            return null;
        }

        /// <summary>A point really on this furniture's top: inside its (rotated) footprint, spread along its long side, near the edge people reach from.</summary>
        public static P3? OnTop(Layout L, Furniture f, Dictionary<int, int> used, Rng rng)
        {
            int n = used.TryGetValue(f.Id, out var c) ? c : 0;
            NavGrid.GetFootprint(f, 0f, out var r);
            bool square = Math.Abs(f.Yaw / 90f - Math.Round(f.Yaw / 90f)) < 0.05f;
            float inset = 0.12f;
            float w = r.W - inset * 2, d = r.D - inset * 2;
            if (!square) { float s = Math.Min(f.W, f.D) * 0.5f; r = new RectF(f.Pos.x - s / 2, f.Pos.z - s / 2, f.Pos.x + s / 2, f.Pos.z + s / 2); w = r.W; d = r.D; inset = 0; }
            if (w < 0.08f || d < 0.08f) return null;
            int per = Math.Max(1, (int)((Math.Max(w, d)) / 0.32f));
            if (n >= per * 2) return null;   // full
            var nav = L.Nav(f.Pos.f);
            for (int attempt = 0; attempt < 4; attempt++)
            {
                int slot = n + attempt; if (slot >= per * 2) break;
                float u = ((slot % per) + 0.5f) / per, v = slot < per ? 0.3f : 0.7f;
                float jitter = rng.Range(-0.04f, 0.04f);
                float x, z;
                bool longX = w >= d;
                // deep tops (stages, pool tables, islands): keep things near the edge closest to a walkable cell
                if (longX) { x = r.x0 + inset + u * w + jitter; z = r.z0 + inset + (d > 1.2f ? (v < 0.5f ? 0.35f : d - 0.35f) : v * d); }
                else { z = r.z0 + inset + u * d + jitter; x = r.x0 + inset + (w > 1.2f ? (v < 0.5f ? 0.35f : w - 0.35f) : v * w); }
                var p = new P3(f.Pos.f, x, z);
                int k = Pathfinder.Snap(nav, p); if (k < 0) continue;
                if (nav.Center(k).DistXZ(p) > 1.35f) continue;   // nobody could reach it
                used[f.Id] = n + attempt + 1;
                return p;
            }
            return null;
        }

        /// <summary>Floor things stand against the nearest wall, clear of doorways, beside rather than in front of furniture.</summary>
        public static bool AgainstWall(Layout L, Room room, P3 near, Rng rng, out P3 pos)
        {
            pos = near; var R = room.Rect.Inset(0.35f); if (R.W < 0.5f || R.D < 0.5f) return false;
            var nav = L.Nav(room.Floor);
            var doors = room.Doors.Select(id => L.Doors[id]).ToList();
            var foot = room.Furniture.Select(i => { NavGrid.GetFootprint(L.Furniture[i], 0.15f, out var fr); return fr; }).ToList();
            // candidates along the four walls, the nearest ones to the original point first
            var cands = new List<P3>();
            for (float t = 0.1f; t <= 0.9f; t += 0.1f)
            {
                cands.Add(new P3(room.Floor, R.x0 + t * R.W, R.z0)); cands.Add(new P3(room.Floor, R.x0 + t * R.W, R.z1));
                cands.Add(new P3(room.Floor, R.x0, R.z0 + t * R.D)); cands.Add(new P3(room.Floor, R.x1, R.z0 + t * R.D));
            }
            foreach (var p in cands.OrderBy(p => p.DistXZ(near)).ThenBy(p => p.x).ThenBy(p => p.z))
            {
                if (doors.Any(d => d.Pos.DistXZ(p) < d.Width / 2 + 1.1f)) continue;
                if (foot.Any(fr => fr.Contains(p.x, p.z))) continue;
                int k = Pathfinder.Snap(nav, p); if (k < 0 || nav.Room[k] != room.Id || nav.Center(k).DistXZ(p) > 1.0f) continue;
                pos = new P3(p.f, p.x + rng.Range(-0.05f, 0.05f), p.z + rng.Range(-0.05f, 0.05f));
                return true;
            }
            return false;
        }
    }
}
