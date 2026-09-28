using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// Probe steps for hiding things (Sim/Systems/Concealment.cs, Game/Player/Concealer.cs), at the "daily" point:
    /// the hands line with a knife ("Q 품에 숨기기") and with a candlestick (greyed reason), the tuck and the line after it,
    /// the quick-draw wheel, "E 넣어 두기" at a nightstand, the stash motion, the opened drawer showing the knife, the bed
    /// ("매트리스 밑", then "E 숨겨 둔 … 꺼내기"), being seen doing it (진우), seeing 진우 do it, and the notebook's 소지품 /
    /// 숨겨 둔 물건 lists. The things it made are taken away again at the end.
    /// </summary>
    public sealed partial class AutoProbe
    {
        partial void ConcealHook(string at, List<IEnumerator> run)
        {
            if (at == "daily") run.Add(Safe(ConcealDaily(), "conceal"));
        }

        readonly List<string> _concealMade = new List<string>();

        Item ConcealSpawn(string type, bool bloody = false)
        {
            var me = S.Player;
            var it = new Item { Id = S.NewId("it"), Type = type, Pos = me.Pos, Room = me.Room, HomeRoom = me.Room, HomePos = me.Pos, Bloody = bloody };
            if (bloody) it.Surface.Add("blood");
            S.Items[it.Id] = it; _concealMade.Add(it.Id);
            S.Emit(GameEventType.ItemMoved, Cast.Player, data: it.Id, text: "new", pos: me.Pos);
            return it;
        }

        /// <summary>Stand in front of the piece (its open side, inside its room), `gap` metres off, looking at it.</summary>
        void ConcealFace(Furniture f, float gap, float pitch)
        {
            var at = Ses.Sim.FrontOf(f, gap);
            Ses.Player.Teleport(at, MathX.AngleDeg(f.Pos.x - at.x, f.Pos.z - at.z), pitch);
        }

        void ConcealLogHands(string tag) => Log($"conceal: {tag} — hands line \"{Strip(Concealer.I?.HandsLine())}\" · hint \"{Strip(Ses.Player.Interact.Hint)}\"");
        static string Strip(string s) => s == null ? "-" : System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", "");

        IEnumerator ConcealDaily()
        {
            TimeClean(); Ses.Speed = 1f;
            yield return WaitSkipIdle(10f);
            var me = S.Player; var conc = Ses.Player.Interact.Conceal;   // (creates the concealer if the keys have not yet)
            var bedroom = S.Layout.BedroomOf(Cast.Player);
            var bed = bedroom?.Furniture.Select(i => S.Layout.Furniture[i]).FirstOrDefault(f => f.Type == "Bed");
            // an openable piece with drawers, on the player's floor: his own nightstand first
            BL23.Game.Mansion.OpenableParts op = null; Furniture drawer = null;
            foreach (var f in S.Layout.Furniture.Where(x => (x.Type == "Nightstand" || x.Type == "Sideboard" || x.Type == "FileCabinet" || x.Type == "Chest") && Livable(x.Room)).OrderBy(x => x.Room == bedroom?.Id ? 0 : 1).ThenBy(x => x.Type == "Nightstand" ? 0 : 1).ThenBy(x => x.Id))
                if (ContainerView.Has(f, out var o)) { drawer = f; op = o; break; }
            Log($"conceal: bed {(bed != null ? "#" + bed.Id : "-")} drawer {(drawer != null ? drawer.Type + "#" + drawer.Id + " " + S.RoomName(drawer.Room) : "-")}");

            // ---- 1. a knife in hand: the prompt
            if (bed != null) ConcealFace(bed, 1.2f, 12f); else Ses.Player.Teleport(me.Pos, me.Yaw, 4f);
            yield return Wait(0.6f);
            var knife = ConcealSpawn("KitchenKnife"); Ses.Sim.PlayerPickUp(knife);
            yield return Wait(0.9f); ConcealLogHands("knife in hand");
            Ses.Player.Pitch = 38f; yield return Wait(0.5f);
            yield return ShotCo("conceal_hands_prompt"); Ses.Player.Pitch = 4f;
            // ---- 2. a candlestick: it can't be hidden (the greyed reason)
            conc.Conceal(knife); yield return Wait(1.0f);
            var candle = ConcealSpawn("Candlestick"); Ses.Sim.PlayerPickUp(candle);
            yield return Wait(0.8f); ConcealLogHands("candlestick");
            yield return ShotCo("conceal_toobig");
            Ses.Sim.PlayerDrop(candle, me.Pos); yield return Wait(0.5f);
            // ---- 3. the tuck, mid-motion and after (the jacket line)
            conc.Draw(knife); yield return Wait(1.0f);
            Ses.Player.Pitch = 30f;
            conc.Conceal(knife); yield return Wait(0.3f); yield return ShotCo("conceal_tuck_motion");
            yield return Wait(1.0f); Ses.Player.Pitch = 4f; yield return Wait(0.3f); ConcealLogHands("after the tuck");
            Log($"conceal: knife worn={S.I(knife.Id)?.Worn} slot={Concealment.SlotWord(Cast.Player, Concealment.SlotOf(S, knife))} last=\"{conc.LastText}\"");
            yield return ShotCo("conceal_after_tuck");
            // ---- 4. the quick-draw wheel (two things inside the jacket)
            var pick = ConcealSpawn("IcePick"); Ses.Sim.PlayerPickUp(pick); yield return Wait(0.4f); conc.Conceal(pick); yield return Wait(1.0f);
            conc.ProbeOpenWheel(); yield return Wait(0.5f); yield return ShotCo("conceal_wheel"); conc.ProbeCloseWheel(); yield return Wait(0.3f);
            // ---- 5. E 넣어 두기 at the drawer, the motion, the opened drawer with the knife in it
            if (drawer != null)
            {
                conc.Draw(knife); yield return Wait(1.0f);
                ConcealFace(drawer, 0.75f, 30f); yield return Wait(0.8f); ConcealLogHands("facing the drawer");
                yield return ShotCo("conceal_stash_prompt");
                bool ok = conc.Stash(drawer); yield return Wait(0.45f); yield return ShotCo("conceal_stash_motion");
                yield return Wait(1.6f);
                Log($"conceal: stashed {ok}: hidden={knife.Hidden} stashF={knife.StashF} contained={TimeLink.ContainedItems(drawer).Contains(knife)} last=\"{conc.LastText}\"");
                ItemVisibility(knife, out bool seen0, out bool pick0); Log($"conceal: drawer shut — knife seen={seen0} pickable={pick0} (want false/false)");
                var sl = ContainerView.SlotOf(knife); int part = sl?.Part ?? 0;
                Ses.Player.Interact.ProbeOpen(drawer.Id, Mathf.Max(0, part)); yield return Wait(0.8f);
                ItemVisibility(knife, out bool seen1, out bool pick1); Log($"conceal: drawer part {part} open — knife seen={seen1} pickable={pick1} hidden={knife.Hidden} (own stash: still hidden)");
                yield return ShotCo("conceal_drawer_open");
                yield return Wait(0.6f); ConcealLogHands("open drawer"); yield return ShotCo("conceal_drawer_take_prompt");
                if (op != null) op.CloseAll(); yield return Wait(0.4f);
            }
            else Log("conceal: no openable drawer on this layout");
            // ---- 6. under the mattress, then "E 숨겨 둔 … 꺼내기"
            if (bed != null)
            {
                conc.Draw(pick); yield return Wait(1.0f);
                FaceFurniture(bed, 1.3f, 26f); yield return Wait(0.8f); ConcealLogHands("facing the bed with the ice pick");
                yield return ShotCo("conceal_bed_prompt");
                conc.Stash(bed); yield return Wait(0.5f); yield return ShotCo("conceal_bed_motion"); yield return Wait(1.8f);
                ItemVisibility(pick, out bool seenB, out _); Log($"conceal: under the mattress — stashF={pick.StashF} hidden={pick.Hidden} seen={seenB} (want false)");
                ConcealLogHands("bed, hands free"); yield return ShotCo("conceal_bed_retrieve_prompt");
            }
            // ---- 7. the notebook: 소지품 and 숨겨 둔 물건
            var carry = ConcealSpawn("LetterOpener"); Ses.Sim.PlayerPickUp(carry); yield return Wait(0.3f);
            Ses.Note.ProbeInventory("item:" + carry.Id); yield return Wait(0.9f); yield return ShotCo("conceal_note_inventory");
            var st = Concealment.Remembered(S).FirstOrDefault();
            if (st.it != null) { Ses.Note.ProbeInventory("stash:" + st.it.Id); yield return Wait(0.9f); yield return ShotCo("conceal_note_stash"); }
            Log("conceal: remembered " + string.Join(", ", Concealment.Remembered(S).Select(x => x.it.Kor + " @ " + Concealment.PlaceName(S, x.f))));
            Ses.Note.Close(); yield return Wait(0.4f);
            // ---- 8. being seen (진우, two steps away, looking) — and seeing 진우 do it
            var jin = S.A("P02");
            if (jin != null && jin.Alive && bed != null)
            {
                FaceFurniture(bed, 1.6f, 6f); yield return Wait(0.3f);
                var fwd = new Vector2(Mathf.Sin(Ses.Player.Yaw * Mathf.Deg2Rad), Mathf.Cos(Ses.Player.Yaw * Mathf.Deg2Rad));
                var at = Ses.Sim.SnapPublic(new P3(me.Pos.f, me.Pos.x + fwd.x * 1.7f, me.Pos.z + fwd.y * 1.7f));
                if (S.Layout.RoomAt(at) != me.Room) at = Ses.Sim.SnapPublic(new P3(me.Pos.f, me.Pos.x - fwd.x * 1.7f, me.Pos.z - fwd.y * 1.7f));
                jin.Act = null; jin.TalkingTo = null; jin.NextThink = S.Clock + 60; jin.Pos = at; jin.Room = S.Layout.RoomAt(at); jin.Pose = BL23.Sim.Pose.Stand;
                jin.Yaw = MathX.AngleDeg(me.Pos.x - at.x, me.Pos.z - at.z); Ses.World.ViewOf("P02")?.Snap();
                Ses.Player.Teleport(me.Pos, MathX.AngleDeg(at.x - me.Pos.x, at.z - me.Pos.z), 8f); yield return Wait(0.8f);
                var bloody = ConcealSpawn("SkinningKnife", true); Ses.Sim.PlayerPickUp(bloody); yield return Wait(0.6f);
                conc.Conceal(bloody); yield return Wait(0.9f);
                Log($"conceal: seen by 진우 — \"{conc.LastText}\" · 진우 suspicion {(S.K("P02").Suspicion.TryGetValue(Cast.Player, out var sv) ? sv : 0):0.00} · jacket blood {me.BloodOnClothes:0.00}");
                yield return ShotCo("conceal_witnessed");
                var jk = ConcealSpawn("IcePick"); jk.Pos = jin.Pos; jk.Room = jin.Room; Ses.Sim.PickUp(jin, jk); yield return Wait(0.6f);
                var rj = Concealment.Conceal(Ses.Sim, jin, jk); yield return Wait(0.8f);
                Log($"conceal: 진우 tucks an ice pick — seen by {string.Join(",", rj.Seen)}; player cards {S.K(Cast.Player).Evidence.Count(e => e.Root != null && e.Root.StartsWith("conceal:"))}");
                yield return ShotCo("conceal_npc_seen");
                jin.NextThink = S.Clock;
                me.BloodOnClothes = 0;
            }
            // ---- clean up what the probe made (nothing of it stays in the world)
            foreach (var id in _concealMade)
            {
                var it = S.I(id); if (it == null) continue;
                var h = it.Holder != null ? S.A(it.Holder) : null;
                if (h != null) { h.Pocket.Remove(id); if (h.HandR == id) h.HandR = null; if (h.HandL == id) h.HandL = null; }
                S.Items.Remove(id); S.Emit(GameEventType.ItemMoved, Cast.Player, data: id, text: "remove");
                foreach (var k in S.Know.Values) foreach (var fact in k.Facts.Where(x => x.Contains(":" + id + ":")).ToList()) k.Facts.Remove(fact);
            }
            _concealMade.Clear(); TimeClean();
            yield return Wait(0.5f);
            Log("conceal: done");
        }
    }
}
