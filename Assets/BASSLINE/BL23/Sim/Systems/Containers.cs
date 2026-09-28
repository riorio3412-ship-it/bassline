using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>What opening a door, drawer or lid turned up (time-on-demand phase 0, spec §2.1). Opening is instant.</summary>
    public sealed class OpenResult { public List<Item> Revealed = new List<Item>(); public string Text; public string SeenBy; }

    /// <summary>
    /// Containers and seats, instantly: a hidden thing lies inside the piece of furniture it is nearest to; opening that
    /// piece shows it (and prying in someone else's room is noticed, exactly like the old "열어 보기"). Sitting takes a free
    /// seat spot of the chair at once; standing frees it. No world time passes in any of these.
    /// </summary>
    public sealed partial class Simulation
    {
        /// <summary>Furniture that can hold things (doors, drawers, lids, or just rummaged through).</summary>
        public static readonly HashSet<string> ContainerTypes = new HashSet<string>
        {
            "Wardrobe", "Chest", "Cabinet", "Sideboard", "FileCabinet", "Nightstand", "Desk", "Crates", "Shelves", "MedCabinet", "DollShelf", "Barrel", "CostumeRack",
            "Fridge", "ColdLocker", "ButlerDesk", "CardCatalog", "DisplayCase", "Counter", "VanityDesk",
        };
        public static bool IsContainer(Furniture f) => f != null && ContainerTypes.Contains(f.Type);
        static float ContainerReach(Furniture f) => Math.Max(f.W, f.D) * 0.6f + 0.9f;

        /// <summary>The container a loose, hidden item lies in: the nearest container in its room whose reach covers it.</summary>
        Furniture ContainerOf(Item it)
        {
            if (it == null || it.Holder != null || it.Room < 0) return null;
            // --- concealment: a stashed thing is in the piece it was put in (a bed or a bookshelf is no container: null)
            if (it.StashF >= 0) { var sf = it.StashF < S.Layout.Furniture.Count ? S.Layout.Furniture[it.StashF] : null; return IsContainer(sf) ? sf : null; }
            var room = S.Layout.Room(it.Room); if (room == null) return null;
            Furniture best = null; float bd = float.MaxValue;
            foreach (var fid in room.Furniture)
            {
                var f = S.Layout.Furniture[fid]; if (!IsContainer(f) || f.Pos.f != it.Pos.f) continue;
                float d = it.Pos.DistXZ(f.Pos); if (d >= ContainerReach(f)) continue;
                if (d < bd || d == bd && best != null && f.Id < best.Id) { bd = d; best = f; }
            }
            return best;
        }

        /// <summary>Hidden, loose items inside this container (ordered by id).</summary>
        public List<Item> ContainedItems(Furniture f)
        {
            var list = new List<Item>(); if (!IsContainer(f)) return list;
            float reach = ContainerReach(f);
            foreach (var it in S.Items.Values)
            {
                if (it.StashF >= 0) { if (it.StashF == f.Id && it.Hidden && it.Holder == null) list.Add(it); continue; }   // --- concealment: put here on purpose
                if (!it.Hidden || it.Holder != null || it.Room != f.Room || it.Pos.f != f.Pos.f || it.Pos.DistXZ(f.Pos) >= reach) continue;
                if (ContainerOf(it) != f) continue;
                list.Add(it);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        /// <summary>민혁 opens (a part of) a container. Only ids that really lie inside it are revealed; itemIds null = all of them.
        /// firstOpen: the presentation's "first time this part is opened now" (the prying penalty applies once per piece per day).</summary>
        public OpenResult PlayerOpen(Furniture f, IList<string> itemIds, bool firstOpen)
        {
            var res = new OpenResult(); var me = P;
            if (f == null || me == null) { res.Text = "비어 있다"; return res; }
            var inside = ContainedItems(f);
            // --- concealment: what 민혁 put here himself is no discovery (it stays put away; E takes it back out)
            foreach (var it in inside) if ((itemIds == null || itemIds.Contains(it.Id)) && !(it.StashF >= 0 && S.K(Cast.Player).Facts.Contains($"stash:{it.Id}:{it.StashF}"))) res.Revealed.Add(it);
            foreach (var it in res.Revealed)
            {
                it.Hidden = false; it.StashF = -1;
                S.K(Cast.Player).ItemSeen[it.Id] = (f.Room, S.Clock);
                S.Log("ItemFound", Cast.Player, item: it.Id, room: f.Room, data: "open");
                S.Emit(GameEventType.ItemMoved, Cast.Player, data: it.Id, text: "revealed", pos: it.Pos, room: it.Room);
            }
            foreach (var it in res.Revealed.Where(i => i.Bloody || i.Washed || i.Def?.IsWeapon == true)) Evidences.ExamineItem(this, me, it);
            string fk = FurnitureCatalog.Get(f.Type)?.Kor ?? "가구";
            res.Text = res.Revealed.Count == 0 ? "비어 있다" : $"{fk} 안: " + string.Join(" · ", res.Revealed.Select(i => i.Kor + (i.Bloody ? " (붉은 얼룩)" : "")));
            // prying in someone else's room is noticed: the owner minds a lot, anyone else a little (once per piece per day)
            var room = S.Layout.Room(f.Room);
            string pryKey = $"pry:{f.Id}:{S.Day}";
            if (firstOpen && room?.Owner != null && room.Owner != Cast.Player && !S.Flags.ContainsKey(pryKey))
            {
                var seen = S.LivingNpcs.Where(x => x.Room == me.Room && x.Pose != Pose.Sleep && x.Status == ActorStatus.Active).OrderBy(x => x.Id).ToList();
                if (seen.Count > 0)
                {
                    S.Flags[pryKey] = S.Clock;
                    foreach (var w in seen) Relations.Change(S, w.Id, Cast.Player, like: w.Id == room.Owner ? -0.08f : -0.02f, trust: w.Id == room.Owner ? -0.07f : -0.03f, memory: w.Id == room.Owner ? "민혁이 내 방 물건을 뒤졌다" : "민혁이 남의 방을 뒤지는 걸 봤다");
                    var first = seen.FirstOrDefault(x => x.Id == room.Owner) ?? seen[0];
                    res.SeenBy = first.Id;
                    res.Text += $" — {Cast.GivenOf(first.Id)}이(가) 그걸 봤다";
                }
            }
            S.Log("PlayerOpen", Cast.Player, room: f.Room, data: $"f{f.Id}:{res.Revealed.Count}");
            res.Text = LineBank.FixParticles(res.Text);
            return res;
        }

        /// <summary>The nearest container in the room at pos (within maxDist), e.g. for a killer hiding a weapon in a drawer.</summary>
        public Furniture NearestContainer(P3 pos, float maxDist = 6f)
        {
            int rid = S.Layout.RoomAt(pos); var room = S.Layout.Room(rid); if (room == null) return null;
            Furniture best = null; float bd = maxDist;
            foreach (var fid in room.Furniture)
            {
                var f = S.Layout.Furniture[fid]; if (!IsContainer(f) || f.Pos.f != pos.f) continue;
                float d = pos.DistXZ(f.Pos); if (d < bd) { bd = d; best = f; }
            }
            return best;
        }

        static readonly HashSet<string> SitTags = new HashSet<string> { "sit", "eat", "pray", "rest", "read" };

        /// <summary>민혁 sits down (lie: lies down) on the nearest free seat spot of f to aim. Null, and nothing changes, if there is none.</summary>
        public Spot PlayerSit(Furniture f, P3 aim, bool lie = false)
        {
            var me = P; if (f == null || me == null || !me.Alive || me.CarriedBy != null) return null;
            Spot best = null; float bd = float.MaxValue;
            foreach (var sp in S.Layout.Spots)
            {
                if (sp.Furniture != f.Id || !sp.OnFurniture) continue;
                if (sp.Occupant != null && sp.Occupant != Cast.Player) continue;
                if (lie ? sp.Tag != "sleep" && sp.Tag != "rest" : !SitTags.Contains(sp.Tag)) continue;
                float d = sp.Pos.f == aim.f ? sp.Pos.DistXZ(aim) : 999f + sp.Id * 0.001f;
                if (d < bd) { bd = d; best = sp; }
            }
            if (best == null) return null;
            if (me.Spot >= 0 && me.Spot != best.Id) ReleaseSpot(me);
            best.Occupant = Cast.Player; me.Spot = best.Id;
            me.Pos = best.Pos; me.Yaw = best.Yaw; me.Room = best.Room >= 0 ? best.Room : me.Room;
            me.Pose = lie ? Pose.Sleep : Pose.Sit; me.Anim = Anim.Idle; me.Running = false;
            S.Log("PlayerSit", Cast.Player, room: me.Room, data: $"f{f.Id}:s{best.Id}:{(lie ? "lie" : "sit")}");
            return best;
        }

        /// <summary>민혁 gets up: the seat is free again and he stands at its approach point.</summary>
        public void PlayerStand()
        {
            var me = P; if (me == null) return;
            if (me.Spot >= 0 && me.Spot < S.Layout.Spots.Count)
            {
                var sp = S.Layout.Spots[me.Spot];
                if (sp.Occupant == Cast.Player) sp.Occupant = null;
                me.Pos = sp.Approach; int r = S.Layout.RoomAt(me.Pos); if (r >= 0) me.Room = r;
            }
            me.Spot = -1;
            if (me.Pose == Pose.Sit || me.Pose == Pose.Sleep || me.Pose == Pose.LieBack) me.Pose = Pose.Stand;
            me.Anim = Anim.Idle;
        }

        public bool PlayerSeated => S.Player != null && S.Player.Spot >= 0 && (S.Player.Pose == Pose.Sit || S.Player.Pose == Pose.Sleep);

        /// <summary>Probe/test: put a loose item inside this container (hidden). False when it is held or f is not a container.</summary>
        public bool ProbeHide(Item it, Furniture f)
        {
            if (it == null || f == null || !IsContainer(f) || it.Holder != null) return false;
            it.Pos = f.Pos; it.Room = f.Room; it.Hidden = true; it.LastMovedTick = S.Tick;
            S.Emit(GameEventType.ItemMoved, null, data: it.Id, text: "hidden", pos: it.Pos, room: it.Room);
            return ContainerOf(it) == f;
        }
    }
}
