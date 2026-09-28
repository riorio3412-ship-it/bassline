using System;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// The one commit path for a change to a piece of furniture, whoever or whatever caused it: the presentation layer's rigid
    /// bodies (PlayerPhysics.cs), a struggle (Assaults, Gore), a trap (Tricks), a chapter rule (Rules), a body dragged over a rug
    /// (ViolencePlans), a shot (Firearms).
    ///
    /// Fact, observation and later notice are kept apart:
    ///  - the fact (who, what, where) goes to the ledger at the call site, marked secret when nobody should be credited;
    ///  - at the moment of the change, only people who can actually see the spot (light, view cone, range, walls, closed doors —
    ///    the same eye as Perception.See) get a Saw note, and they learn who did it only if they could see that person too;
    ///    hearing it is Sound() at the call site (drag → Scrape, fall → Crash);
    ///  - everybody else keeps how they last knew the piece (FurnitureMemory). When they next look at it they may notice it is
    ///    not as they left it (Noticed, never with a name). Someone who never knew the room only notices plain disorder.
    /// </summary>
    public sealed partial class Simulation
    {
        public const string ToppledMark = "넘어져 있다";        // Assaults; Gore adds "넘어져 있다 (몸싸움 중 쓰러진 듯)"
        public const string RumpledMark = "밀려나 주름져 있다";  // ViolencePlans: a body dragged over a rug

        /// <summary>null, "toppled" or "rumpled": disorder anyone can see at a glance.</summary>
        public static string Disorder(Furniture f)
        {
            foreach (var m in f.Marks) if (m.StartsWith(ToppledMark, StringComparison.Ordinal)) return "toppled";
            return f.Marks.Contains(RumpledMark) ? "rumpled" : null;
        }

        /// <summary>
        /// Commit a change already applied to <paramref name="f"/> (position, yaw, damage or marks). <paramref name="fromPos"/>,
        /// <paramref name="fromYaw"/> and <paramref name="fromDamage"/> are how it stood just before. <paramref name="by"/> is the
        /// cause (may be absent from the scene: a trap's owner, a rule); <paramref name="how"/>: moved / damaged / toppled / rug /
        /// rearranged / shot.
        /// </summary>
        public void CommitFurnitureChange(Furniture f, string by, string how, P3 fromPos, float fromYaw, int fromDamage)
        {
            if (f == null) return;
            if (f.Origin.Equals(default(P3))) f.Origin = fromPos;
            f.Rev++;
            // a topple or a rumpled rug is exactly what this change added; any other change leaves the disorder as it was
            string fromDisorder = how == "toppled" || how == "rug" ? null : Disorder(f);
            ProjectFurnitureChange(f, by, how, new FurnitureMemory { Pos = fromPos, Yaw = fromYaw, Damage = fromDamage, Disorder = fromDisorder, Rev = f.Rev - 1 });
        }

        /// <summary>
        /// Move a piece and everything that belongs to it: its room membership, its seats and work spots, the walkable grid.
        /// Does not commit (callers commit once, after all of a change is applied).
        /// </summary>
        public void RelocateFurniture(Furniture f, P3 pos, float yaw)
        {
            var oldPos = f.Pos; float oldYaw = f.Yaw; int oldRoom = f.Room;
            int newRoom = S.Layout.RoomAt(pos); if (newRoom < 0) newRoom = oldRoom;
            f.Pos = pos; f.Yaw = yaw; f.Room = newRoom;
            if (oldRoom != newRoom)
            {
                S.Layout.Room(oldRoom)?.Furniture.Remove(f.Id);
                var room = S.Layout.Room(newRoom);
                if (room != null && !room.Furniture.Contains(f.Id)) room.Furniture.Add(f.Id);
            }
            // Seating/work approach points belong to the piece of furniture, including after a rotation or floor change.
            double angle = (yaw - oldYaw) * Math.PI / 180.0;
            float cosine = (float)Math.Cos(angle), sine = (float)Math.Sin(angle);
            foreach (var spot in S.Layout.Spots)
            {
                if (f.Id < 0 || spot.Furniture != f.Id) continue;
                var oldSpotRoom = S.Layout.Room(spot.Room);
                float x = spot.Pos.x - oldPos.x, z = spot.Pos.z - oldPos.z;
                spot.Pos = new P3(pos.f, pos.x + cosine * x + sine * z, pos.z - sine * x + cosine * z);
                x = spot.Approach.x - oldPos.x; z = spot.Approach.z - oldPos.z;
                spot.Approach = new P3(pos.f, pos.x + cosine * x + sine * z, pos.z - sine * x + cosine * z);
                spot.Yaw += yaw - oldYaw; spot.Room = newRoom;
                if (oldRoom != newRoom)
                {
                    oldSpotRoom?.Spots.Remove(spot.Id);
                    var room = S.Layout.Room(newRoom);
                    if (room != null && !room.Spots.Contains(spot.Id)) room.Spots.Add(spot.Id);
                }
            }
            // Clear both caches: removing a blocker from upstairs must also make the old floor walkable.
            S.Layout.InvalidateNav(oldPos.f);
            if (pos.f != oldPos.f) S.Layout.InvalidateNav(pos.f);
        }

        // ------------------------------------------------------------------ at the moment of the change
        void ProjectFurnitureChange(Furniture f, string by, string how, FurnitureMemory before)
        {
            var mover = by != null ? S.A(by) : null;
            // hands on it: a trap's owner in another room, a rule, a shot from across the hall are not "seen doing it"
            bool moverThere = mover != null && mover.Alive && mover.Pos.f == f.Pos.f && mover.Pos.DistXZ(f.Pos) < 2.5f;
            string root = "furn:" + f.Id + ":" + f.Rev;
            foreach (var o in S.Actors.Values.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                if (!o.Alive || o.IsButler) continue;
                var k = S.K(o.Id);
                bool awake = o.Status == ActorStatus.Active && o.Pose != Pose.Sleep && o.StairId < 0;
                if (moverThere && o.Id == by && awake)
                {
                    AddFurnitureNote(k, new FurnitureNote { Furniture = f.Id, Room = f.Room, Kind = FurnitureNoteKind.Did, How = how, Who = o.Id, WhoConf = 1f, Clock = S.Clock, Rev = f.Rev, Root = root });
                    Remember(k, f); continue;
                }
                if (awake && SightTo(o, f.Pos, f.Room) > 0f)
                {
                    string who = null; float whoConf = 0f;
                    if (moverThere && mover.Id != o.Id) SeenDoing(o, mover, out who, out whoConf);
                    AddFurnitureNote(k, new FurnitureNote { Furniture = f.Id, Room = f.Room, Kind = FurnitureNoteKind.Saw, How = how, Who = who, WhoConf = whoConf, Clock = S.Clock, Rev = f.Rev, Root = root });
                    Remember(k, f); continue;
                }
                // missed it. Whoever has stood in that room knew the piece as it was just before (the first change they miss is
                // the one that counts: a second change on top does not make them remember the in-between state).
                int wasRoom = S.Layout.RoomAt(before.Pos); if (wasRoom < 0) wasRoom = f.Room;
                bool wasThere = k.RoomSeenAt.TryGetValue(wasRoom, out var seen);
                if (k.FurnitureKnown.TryGetValue(f.Id, out var m)) { if (wasThere && m.Rev == before.Rev) m.Seen = Math.Max(m.Seen, seen); }
                else if (wasThere)
                    k.FurnitureKnown[f.Id] = new FurnitureMemory { Rev = before.Rev, Pos = before.Pos, Yaw = before.Yaw, Damage = before.Damage, Disorder = before.Disorder, Seen = seen };
            }
        }

        /// <summary>Who <paramref name="o"/> takes the person at the furniture to be, by the same rules as a sighting (light, masks, a borrowed coat).</summary>
        void SeenDoing(Actor o, Actor t, out string who, out float conf)
        {
            who = null; conf = SightTo(o, t.Pos, t.Room);
            if (conf <= 0f) return;
            if (t.Disguise != null) { var type = S.I(t.Disguise)?.Type; conf *= type == "TheaterMask" ? 0.12f : type == "Cloak" ? 0.45f : 0.55f; }
            if (conf < 0.3f) { conf = 0f; return; }   // a shape at the chair, not a person they could name
            who = t.Id;
            if (t.BorrowedOutfitOf != null && o.Pos.DistXZ(t.Pos) > 4.5f && conf < 0.7f) who = t.BorrowedOutfitOf;   // IG06: coat = identity (misread)
        }

        /// <summary>
        /// How well <paramref name="o"/> can see the spot <paramref name="p"/> right now (0 = not at all): Perception.See's eye —
        /// light in both rooms, a 78° view cone (or right beside it), a light-dependent range, a lit flashlight, walls and closed doors.
        /// </summary>
        float SightTo(Actor o, P3 p, int room)
        {
            if (o.Pos.f != p.f) return 0f;
            float d = o.Pos.DistXZ(p); if (d > 26f) return 0f;
            float light = Math.Max(RoomLight(room), RoomLight(o.Room) * 0.5f);
            float ang = Math.Abs(MathX.DeltaAngle(o.Yaw, MathX.AngleDeg(p.x - o.Pos.x, p.z - o.Pos.z)));
            bool flash = HasLitFlashlight(o);
            float range = 3.0f + 19f * light;
            if (flash && ang < 22f) range = Math.Max(range, 11f);
            if (!(ang < 78f || d < 1.6f) || d > range) return 0f;
            if (!S.Layout.Nav(o.Pos.f).Ray(o.Pos.x, o.Pos.z, p.x, p.z, _doorOpen ??= DoorOpenNow, false, out _)) return 0f;
            float c = MathX.Clamp01((1f - d / (range + 0.01f)) * 0.8f + light * 0.45f);
            if (flash && ang < 22f) c = Math.Max(c, 0.75f - d * 0.03f);
            return Math.Max(c, 0.01f);
        }

        void Remember(Knowledge k, Furniture f)
        {
            if (!k.FurnitureKnown.TryGetValue(f.Id, out var m)) k.FurnitureKnown[f.Id] = m = new FurnitureMemory();
            m.Rev = f.Rev; m.Pos = f.Pos; m.Yaw = f.Yaw; m.Damage = f.Damage; m.Disorder = Disorder(f); m.Seen = S.Clock;
        }

        static void AddFurnitureNote(Knowledge k, FurnitureNote n)
        {
            k.FurnitureNotes.Add(n);
            if (k.FurnitureNotes.Count > 400) k.FurnitureNotes.RemoveRange(0, 100);
        }

        // ------------------------------------------------------------------ afterwards (from Perception.See, twice a second per person)
        /// <summary>
        /// <paramref name="o"/> looks around their room: a changed piece that is not how they last knew it may catch their eye.
        /// They never learn who did it from this. A chair a metre off its place means nothing to someone who never saw where it
        /// stood, but a table on its side does.
        /// </summary>
        void NoticeFurniture(Actor o, Knowledge k)
        {
            var room = S.Layout.Room(o.Room); if (room == null) return;
            k.RoomSeenAt[room.Id] = S.Clock;
            foreach (int id in room.Furniture)
            {
                if (id < 0 || id >= S.Layout.Furniture.Count) continue;
                var f = S.Layout.Furniture[id]; if (f.Rev == 0) continue;
                bool knew = k.FurnitureKnown.TryGetValue(id, out var m);
                if (knew && m.Rev >= f.Rev) { m.Seen = S.Clock; continue; }   // as they know it
                string disorder = Disorder(f);
                bool obvious, nearOnly = false; string how;
                if (!knew)
                {
                    // never knew this room: only plain disorder reads as something having happened here
                    obvious = disorder != null || f.Damage >= 2;
                    if (!obvious) { Remember(k, f); continue; }
                    how = disorder ?? "damaged";
                }
                else
                {
                    float moved = f.Pos.f == m.Pos.f ? f.Pos.DistXZ(m.Pos) : 99f;
                    bool shifted = moved > 0.3f || Math.Abs(MathX.DeltaAngle(m.Yaw, f.Yaw)) > 20f;
                    bool newDisorder = disorder != null && disorder != m.Disorder;
                    bool damaged = f.Damage > m.Damage;
                    if (!shifted && !newDisorder && !damaged) { Remember(k, f); continue; }   // back as it was, or too slight to tell
                    obvious = newDisorder || moved > 1.2f || damaged && f.Damage >= 2;
                    nearOnly = !obvious && !shifted;                                         // a scuff: only up close
                    how = newDisorder ? disorder : damaged && !shifted ? "damaged" : "moved";
                }
                if (SightTo(o, f.Pos, f.Room) <= 0f) continue;                  // not from here (dark, behind them): maybe on a later look
                if (nearOnly && o.Pos.DistXZ(f.Pos) > 2.5f) continue;
                AddFurnitureNote(k, new FurnitureNote { Furniture = id, Room = f.Room, Kind = FurnitureNoteKind.Noticed, How = how, Clock = S.Clock, Since = knew ? m.Seen : -1, Rev = f.Rev, Root = "furn:" + id + ":" + f.Rev + ":" + o.Id });
                Remember(k, f);
                if (o.IsPlayer) S.Emit(GameEventType.Notice, o.Id, text: NoticeText(f, how), room: f.Room, pos: f.Pos, id: f.Id, key: "furniture");
            }
        }

        static string NoticeText(Furniture f, string how)
        {
            string kor = FurnitureCatalog.Get(f.Type)?.Kor ?? "가구";
            switch (how)
            {
                case "toppled": return kor + "이(가) 넘어져 있다";
                case "rumpled": return kor + "이(가) 밀려나 주름져 있다";
                case "moved": return kor + "이(가) 전에 있던 자리에서 옮겨져 있다";
                default: return kor + "에 전에 없던 자국이 있다";
            }
        }
    }
}
