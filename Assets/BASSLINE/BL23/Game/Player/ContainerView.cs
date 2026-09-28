using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Audio;
using BL23.Game.Characters;
using BL23.Game.Mansion;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// Opening things, simply: a cabinet door swings, a drawer slides out, a chest lid lifts — at once, the hand reaching
    /// in first person, no time passing. What the kernel keeps inside (hidden items) sits in the part's slots; opening
    /// the part shows it (a toast names it, the crosshair picks it: E 줍기 · R 살펴보기). Containers without moving parts
    /// are rummaged instead, and whatever is found hops out onto the top or the front edge.
    /// Open state is presentation only: it is not saved, and containers load closed.
    /// </summary>
    public static class ContainerView
    {
        static Session Ses => Session.I;
        static GameState S => Ses != null && Ses.Sim != null ? Ses.S : null;

        /// <summary>The moving parts of this piece (re-resolved every call: a piece that moved is rebuilt as a new object).</summary>
        public static bool Has(Furniture f, out OpenableParts op)
        {
            op = null; if (f == null) return false;
            var go = Ses?.World?.Mansion?.FurnitureObject(f.Id); if (go == null) return false;
            op = go.GetComponent<OpenableParts>(); if (op == null) op = go.GetComponentInChildren<OpenableParts>();
            return op != null && op.Count > 0;
        }

        public static string PartName(OpenableParts.Part p) => p == null ? "문" : p.Kind == OpenableParts.PartKind.Drawer ? "서랍" : p.Kind == OpenableParts.PartKind.Lid ? "뚜껑" : "문";

        // ------------------------------------------------------------------ where the things inside are shown
        /// <summary>One item kept in a container: which slot of which piece shows it. Sticky while the item stays where it
        /// is (revealing it does not move it to another slot), dropped as soon as it is picked up or moved.</summary>
        public sealed class Slotting
        {
            public string Item; public int Fid; public int Index; public P3 Pos; public int Room; public Layout Layout;
            public GameObject Go; public OpenableParts Op; public Transform Slot; public int Part = -1; public int Stack;
        }
        static readonly Dictionary<string, Slotting> _byItem = new Dictionary<string, Slotting>();

        static bool Same(P3 a, P3 b) => a.f == b.f && Math.Abs(a.x - b.x) < 0.01f && Math.Abs(a.z - b.z) < 0.01f;

        /// <summary>The slot showing this item, or null when it is not kept in a container with moving parts.</summary>
        public static Slotting SlotOf(Item it)
        {
            if (it == null || S == null) return null;
            if (_byItem.TryGetValue(it.Id, out var s) && (s.Layout != S.Layout || it.Holder != null || it.Room != s.Room || !Same(it.Pos, s.Pos))) { _byItem.Remove(it.Id); s = null; }
            if (s == null)
            {
                if (!it.Hidden || it.Holder != null) return null;
                var f = ContainerOf(it); if (f == null || !Has(f, out var op) || op.AllSlots().Count == 0) return null;
                s = new Slotting { Item = it.Id, Fid = f.Id, Pos = it.Pos, Room = it.Room, Layout = S.Layout, Index = FreeIndex(f.Id) };
                _byItem[it.Id] = s;
            }
            if (!Resolve(s)) { _byItem.Remove(it.Id); return null; }
            return s;
        }

        /// <summary>A hidden item kept in a container that has no moving parts (a desk, crates, open shelves): unseen until found.</summary>
        public static bool HiddenInPlain(Item it)
        {
            if (it == null || !it.Hidden || it.Holder != null || S == null) return false;
            // --- concealment: stashed under a mattress, behind the books, in the soil... unseen unless it is a piece with moving parts
            if (it.StashF >= 0) { var sf = it.StashF < S.Layout.Furniture.Count ? S.Layout.Furniture[it.StashF] : null; return sf != null && !Has(sf, out _); }
            var f = ContainerOf(it); return f != null && !Has(f, out _);
        }

        /// <summary>The container that keeps this (hidden) item, by the kernel's rule (null when it lies loose).</summary>
        public static Furniture ContainerOf(Item it)
        {
            if (it == null || S == null) return null;
            var room = S.Layout.Room(it.Room); if (room == null) return null;
            var cands = new List<(float d, Furniture f)>();
            foreach (var fid in room.Furniture) { var f = S.Layout.Furniture[fid]; if (TimeLink.IsContainer(f) && f.Pos.f == it.Pos.f) cands.Add((it.Pos.DistXZ(f.Pos), f)); }
            foreach (var (_, f) in cands.OrderBy(c => c.d)) foreach (var x in TimeLink.ContainedItems(f)) if (x == it) return f;
            return null;
        }

        static int FreeIndex(int fid)
        {
            var used = new HashSet<int>(); foreach (var s in _byItem.Values) if (s.Fid == fid && s.Layout == S.Layout) used.Add(s.Index);
            int i = 0; while (used.Contains(i)) i++; return i;
        }

        static bool Resolve(Slotting s)
        {
            var go = Ses?.World?.Mansion?.FurnitureObject(s.Fid); if (go == null) return false;
            if (go != s.Go || s.Op == null || s.Slot == null)
            {
                var op = go.GetComponent<OpenableParts>(); if (op == null) op = go.GetComponentInChildren<OpenableParts>();
                if (op == null || op.Count == 0) return false;
                var slots = op.AllSlots(); if (slots.Count == 0) return false;
                s.Go = go; s.Op = op; s.Slot = slots[s.Index % slots.Count]; s.Stack = s.Index / slots.Count; s.Part = -1;
                for (int i = 0; i < op.Count && s.Part < 0; i++) if (op.Parts[i].Slots.Contains(s.Slot)) s.Part = i;
            }
            return true;
        }

        /// <summary>Where the item sits now (the slot rides out with its drawer), how open its part is (0..1) and whether it
        /// can be seen through glass while shut.</summary>
        public static bool Pose(Slotting s, out Vector3 pos, out Quaternion rot, out float open, out bool glass)
        {
            pos = default; rot = Quaternion.identity; open = 0f; glass = false;
            if (s == null || !Resolve(s)) return false;
            var sl = s.Slot; int h = 17; foreach (var c in s.Item) h = h * 31 + c;
            float yaw = ((h & 0x7fffffff) % 41 - 20) * 1.1f;
            rot = sl.rotation * Quaternion.Euler(0, yaw, 0);
            float side = s.Stack % 2 == 1 ? -1f : 1f;
            pos = sl.position + sl.rotation * new Vector3(side * 0.07f * ((s.Stack + 1) / 2), 0.004f * s.Stack, 0.03f * ((s.Stack + 1) / 2));
            for (int i = 0; i < s.Op.Count; i++)
            {
                var p = s.Op.Parts[i]; if (!p.Slots.Contains(sl)) continue;
                open = Mathf.Max(open, p.IsOpen ? Mathf.Max(p.T, 0.05f) : p.T);
                if (s.Op.GlassFront && p.Kind == OpenableParts.PartKind.Door) glass = true;   // the glazed doors of a vitrine, not its wooden drawers
            }
            return true;
        }

        /// <summary>Items shown in one part (in id order).</summary>
        public static List<Slotting> InPart(Furniture f, OpenableParts op, int part)
        {
            var res = new List<Slotting>(); if (f == null || op == null || part < 0 || part >= op.Count || S == null) return res;
            var slots = op.SlotsOf(part);
            foreach (var s in _byItem.Values.ToList())
                if (s.Fid == f.Id && s.Layout == S.Layout && Resolve(s) && s.Op == op && slots.Contains(s.Slot)) res.Add(s);
            res.Sort((a, b) => string.CompareOrdinal(a.Item, b.Item));
            return res;
        }

        /// <summary>Every item of this piece has its slot (the kernel may have hidden something here without a move event).</summary>
        static void EnsureSlotted(Furniture f)
        {
            foreach (var it in TimeLink.ContainedItems(f))
            {
                if (_byItem.ContainsKey(it.Id)) continue;
                if (Ses.World.Items.TryGetValue(it.Id, out var iv)) iv.Refresh("contained"); else SlotOf(it);
            }
        }

        // ------------------------------------------------------------------ opening and closing
        /// <summary>E on a part: it opens (or shuts) at once, the hand reaching for it. Opening shows what is inside.</summary>
        public static void Toggle(Furniture f, OpenableParts op, int part)
        {
            if (f == null || op == null || part < 0 || part >= op.Count || S == null) return;
            var p = op.Parts[part]; bool opening = !p.IsOpen; bool first = !op.AnyOpen;
            float floorY = Ses.World.ToWorld(f.Pos).y;
            Hands(p, floorY);
            op.Toggle(part);
            TameBounce(f, op);
            Sound(p, opening);
            Lean(p);
            if (opening) Reveal(f, op, new List<int> { part }, first, false);
        }

        /// <summary>X: open everything (or shut everything when all is open).</summary>
        public static void ToggleAll(Furniture f, OpenableParts op)
        {
            if (f == null || op == null || S == null) return;
            bool anyClosed = false; for (int i = 0; i < op.Count; i++) if (!op.IsOpen(i)) anyClosed = true;
            Motion(ActionAnim.Use, 0.7f);
            if (anyClosed)
            {
                bool first = !op.AnyOpen; var opened = new List<int>(); var kinds = new HashSet<OpenableParts.PartKind>();
                for (int i = 0; i < op.Count; i++) if (!op.IsOpen(i)) { op.Open(i); opened.Add(i); if (kinds.Add(op.Parts[i].Kind)) Sound(op.Parts[i], true); }
                TameBounce(f, op);
                Lean(op.Parts[opened[0]]);
                // one line for everything that opened: what was inside, or that it was all empty (never "찻잔" then "비어 있다")
                var every = new List<int>(); for (int i = 0; i < op.Count; i++) every.Add(i);
                Reveal(f, op, every, first, true, opened);
            }
            else
            {
                var kinds = new HashSet<OpenableParts.PartKind>();
                for (int i = 0; i < op.Count; i++) if (kinds.Add(op.Parts[i].Kind)) Sound(op.Parts[i], false);
                op.CloseAll();
            }
        }

        /// <summary>What the opened part(s) show: revealed to the kernel, named in one toast, and the view turns to the first thing found
        /// (a short glance and a soft glint) so the crosshair rests on it — "찻잔 · E 줍기 · R 살펴보기". all: X opened everything
        /// (one line for all of it; 'opened' are the parts that were shut).</summary>
        static void Reveal(Furniture f, OpenableParts op, List<int> parts, bool first, bool all, List<int> opened = null)
        {
            EnsureSlotted(f);
            var inside = new List<Slotting>(); foreach (var i in parts) foreach (var s in InPart(f, op, i)) if (!inside.Contains(s)) inside.Add(s);
            var ids = inside.Select(s => s.Item).ToList();
            var (rev, _, seen) = TimeLink.Open(f, ids, first);
            foreach (var it in rev) if (Ses.World.Items.TryGetValue(it.Id, out var iv)) iv.Refresh("reveal");
            var items = inside.Select(s => S.I(s.Item)).Where(i => i != null && i.Holder == null).ToList();
            var kind = parts.Count == 1 && parts[0] >= 0 && parts[0] < op.Count ? op.Parts[parts[0]].Kind : (OpenableParts.PartKind?)null;
            string list = string.Join(" · ", items.Select(i => i.Kor + (i.Bloody ? " (붉은 얼룩)" : "")));
            string msg;
            if (all)
            {
                var kinds = (opened ?? parts).Where(i => i >= 0 && i < op.Count).Select(i => op.Parts[i].Kind).Distinct().ToList();
                string what = kinds.Count == 1 && kinds[0] == OpenableParts.PartKind.Drawer ? "서랍을 모두 열었다" : kinds.Count == 1 && kinds[0] == OpenableParts.PartKind.Door ? "문을 모두 열었다" : kinds.Count == 1 && kinds[0] == OpenableParts.PartKind.Lid ? "뚜껑을 열었다" : "모두 열었다";
                msg = items.Count == 0 ? what + " — 모두 비어 있다" : what + " — " + list;
            }
            else
            {
                string where = kind == OpenableParts.PartKind.Drawer ? "서랍" : kind == OpenableParts.PartKind.Lid ? "상자" : (FurnitureCatalog.Get(f.Type)?.Kor ?? "장");
                msg = items.Count == 0 ? (kind == OpenableParts.PartKind.Drawer ? "이 서랍은 비어 있다" : kind == OpenableParts.PartKind.Lid ? "상자는 비어 있다" : "안은 비어 있다") : $"{where} 안: {list}";
            }
            if (!string.IsNullOrEmpty(seen)) msg += " — " + (S.A(seen) != null ? Cast.GivenOf(seen) : seen) + "이(가) 그걸 봤다";
            msg = LineBank.FixParticles(msg);
            if (rev.Count > 0) Sfx.Play("parchment", null, 0.35f);
            Hud.I?.Toast(msg, rev.Count > 0 ? Pal.Gold : items.Count > 0 ? Pal.Text : Pal.TextDim, items.Count > 0 ? 2.6f : 1.3f);
            LastOpened = f.Id;
            if (items.Count > 0 && Ses != null) Ses.StartCoroutine(Found(items[0].Id));
        }

        /// <summary>The view turns to what was found (a short glance: the crosshair lands on it) and a small warm glint rests on it for
        /// a moment, while the drawer finishes sliding out.</summary>
        static IEnumerator Found(string itemId)
        {
            for (float w = 0; w < 0.12f; w += Time.unscaledDeltaTime) yield return null;   // the part is on its way out
            if (Ses == null || Ses.World == null || !Ses.World.Items.TryGetValue(itemId, out var iv) || iv == null || iv.Visual == null) yield break;
            var r = iv.Visual.GetComponentInChildren<Renderer>();
            Vector3 C() => r != null && r.enabled ? r.bounds.center : iv.Visual.transform.position;
            Ses.Player?.GlanceAt(C(), 0.35f);
            var go = new GameObject("FoundGlint"); var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.range = 0.32f; l.color = new Color(1f, 0.86f, 0.62f); l.intensity = 0f; l.shadows = LightShadows.None;
            for (float t = 0; t < 1.6f; t += Time.unscaledDeltaTime)
            {
                if (iv == null || iv.Visual == null || go == null) break;
                var it = S?.I(itemId); if (it == null || it.Holder != null) break;   // picked up: the glint goes with the moment
                go.transform.position = C() + Vector3.up * 0.1f;
                if (t > 0.3f && t < 0.36f) Ses.Player?.GlanceAt(C(), 0.2f);   // the drawer has slid out: settle on where it now lies
                l.intensity = 1.1f * Mathf.Clamp01(t < 0.25f ? t / 0.25f : 1f - (t - 0.25f) / 1.35f);
                yield return null;
            }
            if (go != null) UnityEngine.Object.Destroy(go);
        }

        /// <summary>The warm fill light an open piece gets inside has no shadows: it must reach its back panel and no further, or it
        /// paints the wall behind the piece (a bright slab above a sideboard).</summary>
        static void TameBounce(Furniture f, OpenableParts op)
        {
            if (f == null || op == null || Ses?.World == null) return;
            var t = op.transform.Find("InteriorBounce"); var l = t != null ? t.GetComponent<Light>() : null; if (l == null) return;
            var fwd = Quaternion.Euler(0, f.Yaw, 0) * Vector3.forward;
            var back = Ses.World.ToWorld(f.Pos) - fwd * (f.D * 0.5f);
            float toBack = Vector3.Dot(l.transform.position - back, fwd);   // how far in front of the back panel the light hangs
            if (toBack > 0.1f) l.range = Mathf.Min(l.range, toBack + 0.02f);
        }

        /// <summary>The piece opened last (probe: pick up what was found in it).</summary>
        public static int LastOpened = -1;

        /// <summary>A revealed item lying in an open part of this piece (probe helper).</summary>
        public static Item FirstVisibleIn(Furniture f)
        {
            if (f == null || !Has(f, out var op)) return null;
            for (int i = 0; i < op.Count; i++)
            {
                if (!op.IsOpen(i)) continue;
                foreach (var s in InPart(f, op, i)) { var it = S.I(s.Item); if (it != null && it.Holder == null && !it.Hidden) return it; }
            }
            return null;
        }

        // ------------------------------------------------------------------ rummaging (no moving parts)
        /// <summary>A desk, crates, open shelves: bend (or reach) in, the piece knocks about a little, whatever turns up hops out.</summary>
        public static void Rummage(Furniture f)
        {
            if (f == null || S == null) return;
            bool low = f.H < 0.9f;
            Motion(low ? ActionAnim.PickUp : ActionAnim.Search, 0.7f);
            Ses.StartCoroutine(Wobble(f));
            Sfx.Play("amb_creak", Ses.World.ToWorld(f.Pos) + Vector3.up * 0.5f, 0.35f);
            float reach = Math.Max(f.W, f.D) * 0.6f + 0.9f;
            var before = S.Items.Values.Where(i => i.Hidden && i.Holder == null && i.Room == f.Room && i.Pos.f == f.Pos.f && i.Pos.DistXZ(f.Pos) < reach).ToList();
            var res = Ses.Sim.PlayerUse(f, "search");
            int k = 0;
            foreach (var it in before.Where(i => !i.Hidden).OrderBy(i => i.Id, StringComparer.Ordinal))
                if (Ses.World.Items.TryGetValue(it.Id, out var iv)) { iv.Refresh("reveal"); iv.HopTo(HopSpot(f, k++)); }
            if (res != null && !string.IsNullOrEmpty(res.Text)) Hud.I?.Toast(res.Text, k > 0 ? Pal.Gold : Pal.Text, k > 0 ? 3f : 1.8f);
        }

        static Vector3 HopSpot(Furniture f, int k)
        {
            var basePos = Ses.World.ToWorld(f.Pos); var rot = Quaternion.Euler(0, f.Yaw, 0);
            var right = rot * Vector3.right; var fwd = rot * Vector3.forward;
            float lateral = (k % 3 - 1) * Mathf.Min(0.2f, f.W * 0.25f);
            if (f.H <= 1.25f) return basePos + Vector3.up * (f.H + 0.03f) + right * lateral + fwd * Mathf.Min(0.12f, f.D * 0.2f);   // onto the top, toward the front
            return basePos + fwd * (f.D * 0.5f + 0.28f) + right * lateral + Vector3.up * 0.05f;                                        // a tall piece: out in front, on the floor
        }

        static IEnumerator Wobble(Furniture f)
        {
            var mv = Ses?.World?.Mansion; var go = mv?.FurnitureObject(f.Id); if (go == null) yield break;
            // a piece merged into the room mesh cannot move: the view gives a little knock instead
            if (mv.Batched.Contains(f.Id) || mv.Merged.Contains(f.Id) || go.GetComponent<Rigidbody>() != null) { Ses.Player?.Nudge(Vector3.down * 0.03f, 0.25f); yield break; }
            var t = go.transform; var r0 = t.rotation;
            for (float x = 0; x < 0.25f; x += Time.deltaTime)
            {
                if (t == null) yield break;
                t.rotation = r0 * Quaternion.Euler(0, Mathf.Sin(x / 0.25f * Mathf.PI * 3f) * 1.5f * (1f - x / 0.25f), 0);
                yield return null;
            }
            if (t != null) t.rotation = r0;
        }

        // ------------------------------------------------------------------ the hands
        static void Motion(ActionAnim a, float secs) { var an = Ses?.World?.ViewOf(Cast.Player)?.Rig?.Anim; if (an != null) an.PlayAction(a, secs); }

        static void Hands(OpenableParts.Part p, float floorY)
        {
            float h = p.Pivot != null ? p.Pivot.position.y - floorY : 1f;
            switch (p.Kind)
            {
                case OpenableParts.PartKind.Door: Motion(ActionAnim.OpenDoor, 0.5f); break;
                case OpenableParts.PartKind.Drawer: if (h < 0.7f) Motion(ActionAnim.PickUp, 0.5f); else Motion(ActionAnim.Use, 0.45f); break;   // the part is open well within 0.5 s
                default: Motion(ActionAnim.PickUp, 0.55f); break;
            }
        }

        static void Sound(OpenableParts.Part p, bool opening)
        {
            var at = p.Pivot != null ? p.Pivot.position : (Ses.Player != null ? Ses.Player.transform.position : Vector3.zero);
            switch (p.Kind)
            {
                case OpenableParts.PartKind.Door: Sfx.Play(opening ? "door_creak" : "door_close", at, opening ? 0.35f : 0.3f); break;
                case OpenableParts.PartKind.Drawer: Sfx.Play("chair_scrape", at, 0.25f); break;
                default: Sfx.Play("door_creak", at, 0.45f); Sfx.Play("amb_creak", at, 0.3f); break;
            }
        }

        /// <summary>The head leans toward the part for a moment (at most ~8°).</summary>
        static void Lean(OpenableParts.Part p)
        {
            var pc = Ses?.Player; if (pc == null || pc.Cam == null || p.Pivot == null) return;
            var r = p.Pivot.GetComponentInChildren<Renderer>(); var c = r != null ? r.bounds.center : p.Pivot.position;
            var d = c - pc.Cam.transform.position; if (d.sqrMagnitude < 1e-4f) return;
            pc.Nudge(d.normalized * Mathf.Min(0.12f, d.magnitude * 0.14f), 0.3f);
        }
    }
}
