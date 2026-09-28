using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Audio;
using BL23.Game.Characters;
using BL23.Game.Physicality;
using BL23.Game.Mansion;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>Mirrors the kernel into the 3D scene: mansion, actors, props, traces, doors, lights, facilities.</summary>
    public sealed class WorldPresenter : MonoBehaviour
    {
        Session _s; GameState S => _s.S;
        public MansionView Mansion; public Transform Root;
        public readonly Dictionary<string, ActorView> Actors = new Dictionary<string, ActorView>();
        public readonly Dictionary<string, ItemView> Items = new Dictionary<string, ItemView>();
        readonly Dictionary<string, GameObject> _traces = new Dictionary<string, GameObject>();
        readonly Dictionary<int, int> _doorState = new Dictionary<int, int>();
        int _circuitMask = int.MinValue; float _dark = -1;
        public CourtroomView Court;

        public void Init(Session s) { _s = s; Rebuild(); }

        public void Teardown() { if (Root != null) Destroy(Root.gameObject); Actors.Clear(); Items.Clear(); _traces.Clear(); _doorState.Clear(); }

        public void Rebuild()
        {
            Teardown();
            Root = new GameObject("World").transform;
            try { Mansion = MansionView.Build(S.Layout, Root); } catch (Exception e) { Debug.LogException(e); Mansion = null; }
            if (Mansion == null) Mansion = FallbackMansion.Build(S.Layout, Root);
            Court = Root.GetComponentInChildren<CourtroomView>(true);
            foreach (var a in S.Actors.Values) EnsureActor(a);
            foreach (var it in S.Items.Values) EnsureItem(it);
            foreach (var t in S.Traces) SpawnTrace(t);
            _circuitMask = int.MinValue; _dark = -1; _doorState.Clear();
            ViolencePresenter.Attach(this);   // --- violence track (Game/Violence): holds, bindings, drags, shots, reactions
            Sync(0);
        }

        public void AfterTrial() { foreach (var v in Actors.Values) v.Snap(); }
        public void ForceResync()
        {
            _circuitMask = int.MinValue; _dark = -1; _doorState.Clear();
            foreach (var v in Actors.Values) { v.ReplayDriven = false; v.ForceVisible = false; v.RestoreFromKernel(); }
            Sync(0);
        }

        public Vector3 ToWorld(P3 p) => Mansion != null ? Mansion.ToWorld(p) : new Vector3(p.x, S.Layout.FloorY(p.f), p.z);

        ActorView EnsureActor(Actor a)
        {
            if (Actors.TryGetValue(a.Id, out var v)) return v;
            var go = new GameObject("Actor_" + a.Id); go.transform.SetParent(Root, false);
            v = go.AddComponent<ActorView>(); v.Init(this, a); Actors[a.Id] = v; return v;
        }

        ItemView EnsureItem(Item it)
        {
            if (Items.TryGetValue(it.Id, out var v)) return v;
            var go = new GameObject("Item_" + it.Id); go.transform.SetParent(Root, false);
            v = go.AddComponent<ItemView>(); v.Init(this, it); Items[it.Id] = v; return v;
        }

        void SpawnTrace(Trace t)
        {
            if (_traces.ContainsKey(t.Id) || t.Cleaned) return;
            // --- violence track: a bullet hole sits on the wall / door / furniture it struck, at the height it struck
            if (t.Type == "BulletHole") { var bh = BulletHole(t); if (bh != null) { bh.name = "Trace_" + t.Id; bh.transform.SetParent(Root, true); bh.AddComponent<TraceView>().TraceId = t.Id; _traces[t.Id] = bh; } return; }
            var pos = ToWorld(t.Pos) + Vector3.up * 0.3f; Vector3 n = Vector3.up;
            if (Physics.Raycast(pos, Vector3.down, out var hit, 2f, ~0, QueryTriggerInteraction.Ignore)) { pos = hit.point; n = hit.normal; } else pos.y -= 0.3f;
            Color c = t.Type.StartsWith("Blood") || t.Type == "FootprintBlood" ? new Color(0.35f, 0.02f, 0.03f, 0.95f) : t.Type.Contains("Wet") || t.Type == "Water" ? new Color(0.5f, 0.6f, 0.7f, 0.45f) : new Color(0.2f, 0.18f, 0.16f, 0.7f);
            string tt = t.Type == "Scuff" ? "Scratch" : t.Type == "SwitchTouched" || t.Type == "PowerResidue" ? null : t.Type == "ThreadFiber" ? "Scratch" : t.Type;
            if (tt == null) return;
            GameObject go = null;
            if (t.Type == "BloodWriting") go = BloodGlyph(t, pos, n);
            if (t.Type == "ThreadFiber") c = new Color(0.9f, 0.92f, 0.95f, 0.55f);
            if (go == null) try { go = TraceFactory.Create(tt == "BloodWriting" ? "BloodSmear" : tt, pos, n, t.Size, c); } catch (Exception) { }
            if (go == null) go = FallbackMansion.Decal(pos, n, t.Size, c);
            go.name = "Trace_" + t.Id; go.transform.SetParent(Root, true);
            var tv = go.AddComponent<TraceView>(); tv.TraceId = t.Id;
            _traces[t.Id] = go;
        }

        /// <summary>A glyph written in blood on the floor: the character itself in a rough brush-like face, smeared at the edge.</summary>
        GameObject BloodGlyph(Trace t, Vector3 pos, Vector3 n)
        {
            string d = t.Desc ?? ""; int a = d.IndexOf('「'), b = d.IndexOf('」');
            string glyph = a >= 0 && b > a ? d.Substring(a + 1, b - a - 1) : "?";
            var go = new GameObject("BloodGlyph");
            go.transform.position = pos + n * 0.006f;
            go.transform.rotation = Quaternion.LookRotation(-n, Vector3.forward) * Quaternion.Euler(0, 0, (t.Pos.x * 37f) % 25f - 12f);
            var tm = go.AddComponent<TMPro.TextMeshPro>();
            tm.font = Fonts.Serif; tm.text = glyph; tm.fontSize = 3.2f; tm.alignment = TMPro.TextAlignmentOptions.Center;
            tm.color = new Color(0.36f, 0.02f, 0.03f, 0.95f); tm.fontStyle = TMPro.FontStyles.Bold;
            tm.rectTransform.sizeDelta = new Vector2(0.6f, 0.6f);
            // a smear beside it
            try { var sm = TraceFactory.Create("BloodSmear", pos + new Vector3(0.18f, 0, -0.1f), n, 0.22f, new Color(0.35f, 0.02f, 0.03f, 0.9f)); sm.transform.SetParent(go.transform, true); } catch (Exception) { }
            return go;
        }

        // --- time-on-demand: doors and blows are not heard while the house is fast-forwarded (the last 0.6 s are); the view still changes
        static bool MuteLapse => Session.I?.TimeDir != null && Session.I.TimeDir.MuteWorldAudio;

        /// <summary>(violence track) A bullet hole: back along the recorded line of fire to find the real surface and its normal.</summary>
        GameObject BulletHole(Trace t)
        {
            float y = 1.2f, pitch = 0f; var note = t.Note ?? "";
            foreach (var part in note.Split(';')) { var kv = part.Split('='); if (kv.Length != 2) continue; if (kv[0] == "y") float.TryParse(kv[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y); if (kv[0] == "pitch") float.TryParse(kv[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out pitch); }
            var dir = Quaternion.Euler(-pitch, t.Dir, 0f) * Vector3.forward;
            var at = ToWorld(t.Pos) + Vector3.up * y; Vector3 n = -dir;
            if (Physics.Raycast(at - dir * 0.7f, dir, out var hit, 1.4f, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<ActorView>() == null) { at = hit.point; n = hit.normal; }
            return ViolenceFx.BulletHole(at, n, t.Desc != null && t.Desc.Contains("산탄"));
        }

        public void OnEvent(GameEvent e)
        {
            ViolencePresenter.I?.OnEvent(e);   // --- violence track: shots, holds, bindings, gunshot flinches, knocked furniture, defences
            switch (e.Type)
            {
                case GameEventType.Trace: { var t = S.Traces.FirstOrDefault(x => x.Id == e.Data); if (t != null) SpawnTrace(t); break; }
                case GameEventType.ItemMoved:
                    {
                        var it = S.I(e.Data);
                        if (it == null) { if (e.Data != null && Items.TryGetValue(e.Data, out var gone)) { Destroy(gone.gameObject); Items.Remove(e.Data); } break; }
                        EnsureItem(it).Refresh(e.Text); break;
                    }
                case GameEventType.ItemState: { if (Items.TryGetValue(e.Data ?? "", out var iv)) iv.SetState(e.Text); break; }
                case GameEventType.Wound: { if (Actors.TryGetValue(e.Target, out var av)) av.OnWound(e); break; }
                case GameEventType.Death: { if (Actors.TryGetValue(e.Actor, out var av)) av.OnDeath(); Sfx.Play("body_fall", ToWorld(e.Pos), 0.8f); break; }
                case GameEventType.Collapse: { if (Actors.TryGetValue(e.Actor, out var av)) av.OnCollapse(); Sfx.Play("body_fall", ToWorld(e.Pos), 0.7f); break; }
                case GameEventType.Strike: { if (Actors.TryGetValue(e.Actor, out var av)) av.OnStrike(e.Data); if (!MuteLapse) Sfx.Play(e.Data == "Stab" ? "stab" : e.Data == "Slash" ? "slash" : "blunt_hit", ToWorld(e.Pos), 0.8f); break; }
                case GameEventType.Disguise:
                    {
                        if (!Actors.TryGetValue(e.Actor, out var av)) break;
                        // borrowed coat (IG06) and the Guise power show the other person's outer look; costumes use their own item type
                        string look = e.Data == null ? null : e.Data.StartsWith("coat:") ? "Coat:" + e.Data.Substring(5) : e.Data == "guise" ? "Coat:" + e.Target : e.Data;
                        av.Rig?.SetDisguise(look); break;
                    }
                case GameEventType.Carry: { if (Actors.TryGetValue(e.Actor, out var av)) av.OnCarry(e.Target, e.Value > 0.5f); break; }
                case GameEventType.Speech:
                    {
                        // a recorder playing someone's voice: the sound comes from the device, the person doesn't move their lips
                        if (e.Key == "recording") { Sfx.Play("static_noise", ToWorld(e.Pos) + Vector3.up, 0.5f); if (e.Data != null && Items.TryGetValue(e.Data, out var rv) && rv.Visual != null) { PropFactory.SetRecording(rv.Visual, true); StartCoroutine(LampOff(rv.Visual, 6f)); } break; }
                        if (Actors.TryGetValue(e.Actor, out var av)) av.OnSpeech(e.Text, e.Key); break;
                    }
                case GameEventType.Sound: PlaySound(e); break;
                case GameEventType.Door: { var d = S.Layout.Door(e.Id); if (d != null && (e.Text == "sealed" || e.Text == "unsealed")) { if (Mansion != null && Mansion.Doors.TryGetValue(d.Id, out var dvs)) dvs.SetSealed(e.Text == "sealed"); Sfx.Play("door_lock", ToWorld(d.Pos) + Vector3.up, 0.8f); break; } if (d != null && !MuteLapse) { bool open = (e.Value % 2) >= 1; Sfx.Play(e.Value >= 2 ? "door_lock" : open ? ((d.Id * 7 + (int)(Time.time * 0.5f)) % 4 == 0 ? "door_creak" : "door_open") : "door_close", ToWorld(d.Pos) + Vector3.up, 0.6f); } break; }
                case GameEventType.Light: Sfx.Play("breaker", null, 0.5f); break;
                case GameEventType.Press: if (e.Data == "fire") Sfx.Play("hydraulic_press", PressPos(), 1f); break;
                case GameEventType.Furniture: { var f = S.Layout.Furniture.ElementAtOrDefault(e.Id); if (f != null) { Mansion?.SetFurnitureState(f.Id, f.Damage, f.Pos, f.Yaw); if (e.Text == "stoked") Mansion?.StokeFire(f.Id); } break; }
                case GameEventType.Rescue: { if (Actors.TryGetValue(e.Target, out var av)) av.Rig?.SetExpression(Expr.Pain, 0.6f); break; }
                case GameEventType.LoopReset: break;
                // --- concealment (Sim/Systems/Concealment.cs): someone tucks a thing away, draws it, puts it in a hiding place, searches one
                case GameEventType.Anim: if (e.Actor != Cast.Player && Actors.TryGetValue(e.Actor ?? "", out var cav)) ConcealGesture(cav, e); break;
            }
        }

        // --- concealment (begin): an NPC's hands for the hiding acts — a hand to the chest for the coat, a reach for the place
        void ConcealGesture(ActorView av, GameEvent e)
        {
            var an = av.Rig?.Anim; if (an == null || e.Key != "tuck" && e.Key != "draw" && e.Key != "stash" && e.Key != "retrieve") return;
            float secs = Mathf.Clamp(e.Value, 0.4f, 2.5f);
            var it = S.I(e.Data); var slot = it != null ? Concealment.SlotOf(S, it) : BodySlot.None;
            var f = e.Id >= 0 && e.Id < S.Layout.Furniture.Count ? S.Layout.Furniture[e.Id] : null;
            switch (e.Key)
            {
                case "tuck": case "draw": if (slot == BodySlot.Coat || e.Key == "draw") an.PlayGesture(Gesture.HandOnChest, secs); break;
                case "stash": case "retrieve":
                    if (f != null && (f.Type == "Planter" || f.Type == "Plant")) an.PlayAction(ActionAnim.Garden, secs);
                    else if (f != null && (Concealment.PlaceFor(f)?.Low ?? false)) an.PlayAction(ActionAnim.PickUp, secs);
                    break;
            }
            if (!MuteLapse && S.Player != null && e.Pos.f == S.Player.Pos.f && e.Pos.DistXZ(S.Player.Pos) < 8f) Sfx.Play(f == null ? "parchment" : "amb_creak", ToWorld(e.Pos) + Vector3.up, 0.18f);
        }
        // --- concealment (end)

        Vector3 PressPos() { var mr = S.Layout.First(RoomType.MachineRoom); return mr != null ? ToWorld(new P3(mr.Floor, mr.Rect.CX, mr.Rect.CZ)) : Vector3.zero; }

        void PlaySound(GameEvent e)
        {
            Enum.TryParse(e.Text ?? "", out SoundKind k); var pos = ToWorld(e.Pos) + Vector3.up;
            if (!MuteLapse && (k == SoundKind.Crash || k == SoundKind.GlassBreak || k == SoundKind.Scream || k == SoundKind.Strike))
            {
                foreach (var pair in Actors)
                {
                    var observer = pair.Value; if (observer == null || observer.Id == e.Actor) continue;
                    float distance = Vector3.Distance(observer.HeadPos, pos); if (distance > 8f) continue;
                    // Local reflex only: this grants no suspect identity, knowledge or testimony.
                    bool occluded = Physics.Linecast(observer.HeadPos, pos, out var obstacle, ~0, QueryTriggerInteraction.Ignore) &&
                                    obstacle.collider.GetComponentInParent<PhysicalCharacter>() == null && obstacle.collider.GetComponentInParent<ActorRig>() == null;
                    observer.Physical?.Startle(pos, Mathf.Clamp01(1 - distance / 10f) * (occluded ? .2f : 1f));
                }
            }
            // --- time-on-demand: while the house is fast-forwarded, its everyday noises (knocks, bells, switches, splashes) are not
            // heard all at once (hours pass in seconds); what is urgent still is (a scream, glass, a crash, a fall)
            if (MuteLapse && (k == SoundKind.Knock || k == SoundKind.Bell || k == SoundKind.Switch || k == SoundKind.Splash)) return;
            switch (k)
            {
                case SoundKind.Scream: Sfx.PlayEx("scream_muffled", pos, 1f, 1f, -1, IsSameRoomAsPlayer(e.Room) ? 0 : 1100f); break;
                case SoundKind.GlassBreak: Sfx.Play("glass_shatter", pos, 0.9f); break;
                case SoundKind.Crash: Sfx.Play("wood_crack", pos, 0.8f); break;
                case SoundKind.Knock: Sfx.Play("door_knock", pos, 0.8f); break;
                case SoundKind.Bell: Sfx.Play("chime", pos, 0.7f); break;
                case SoundKind.Splash: Sfx.Play("water_splash", pos, 0.8f); break;
                case SoundKind.Switch: Sfx.Play("light_switch", pos, 0.6f); break;
                case SoundKind.Fall: Sfx.Play("body_fall", pos, 0.7f); break;
                case SoundKind.Struggle: Sfx.Play("slap", pos, 0.5f); break;
                case SoundKind.Scrape: Sfx.Play("chair_scrape", pos, Mathf.Clamp(e.Value * 1.6f, 0.2f, 0.6f)); break;   // furniture dragged/shoved (FurnitureChanges.cs)
            }
            if (e.Text == "Noise") Mansion?.SetNoise(e.Value);
        }

        bool IsSameRoomAsPlayer(int room) => S.Player != null && S.Player.Room == room;

        System.Collections.IEnumerator LampOff(GameObject go, float secs) { yield return new WaitForSeconds(secs); if (go != null) PropFactory.SetRecording(go, false); }

        static readonly Camera[] _cams = new Camera[16];
        /// <summary>The mansion culls lights/floors around whichever camera is actually showing the scene.</summary>
        void TrackViewCamera()
        {
            if (Mansion == null) return;
            int n = Camera.GetAllCameras(_cams); Camera best = null;
            for (int i = 0; i < n; i++) { var c = _cams[i]; if (c != null && c.isActiveAndEnabled && (best == null || c.depth > best.depth)) best = c; }
            if (best != null && Mansion.ViewCamera != best) Mansion.ViewCamera = best;
        }

        public void Sync(float dt)
        {
            if (S?.Layout == null) return;
            TrackViewCamera();
            foreach (var a in S.Actors.Values) EnsureActor(a).Tick(dt);
            foreach (var d in S.Layout.Doors)
            {
                int st = (d.Open ? 1 : 0) + (d.Locked ? 2 : 0);
                if (!_doorState.TryGetValue(d.Id, out var old) || old != st) { _doorState[d.Id] = st; Mansion?.SetDoor(d.Id, d.Open, d.Locked); }
            }
            if (_circuitMask != S.CircuitMask) { for (int c = 0; c < S.Layout.Circuits.Count; c++) { bool on = S.CircuitOn(c); if (_circuitMask == int.MinValue || ((_circuitMask >> c) & 1) != (on ? 1 : 0)) Mansion?.SetCircuit(c, on); } _circuitMask = S.CircuitMask; }
            if (Math.Abs(_dark - S.Darkness) > 0.01f) { _dark = S.Darkness; Mansion?.SetDarkness(_dark); }
            Mansion?.SetPress((float)S.PressRam, S.PressPowered && S.CircuitOn(7));
            PruneTraces();
        }

        // (perf) was, every frame: a copy of _traces plus a search of the whole kernel list (and a closure) per shown trace —
        // O(shown × all) and a few KB of garbage per frame at a crime scene. Now one pass over each, nothing allocated.
        readonly Dictionary<string, Trace> _traceById = new Dictionary<string, Trace>();
        readonly List<string> _deadTraces = new List<string>();
        void PruneTraces()
        {
            if (_traces.Count == 0) return;
            _traceById.Clear();
            foreach (var t in S.Traces) if (t?.Id != null && !_traceById.ContainsKey(t.Id)) _traceById.Add(t.Id, t);   // first with that id, like FirstOrDefault
            foreach (var kv in _traces) if (!_traceById.TryGetValue(kv.Key, out var t) || t.Cleaned) _deadTraces.Add(kv.Key);
            foreach (var id in _deadTraces) { Destroy(_traces[id]); _traces.Remove(id); }
            _deadTraces.Clear();
        }

        public ActorView ViewOf(string id) => id != null && Actors.TryGetValue(id, out var v) ? v : null;
    }

    public sealed class TraceView : MonoBehaviour { public string TraceId; }
}
