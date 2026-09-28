using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Audio;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEngine;
using Pose = BL23.Sim.Pose;

namespace BL23.Game.Cinema
{
    /// <summary>
    /// Poses the real actors from a recorded ReplaySegment at any moment (ticks, fractional), and applies what the ledger
    /// says happened as time passes (wounds as they were dealt, disguises, strikes, falls). Read-only with respect to the
    /// kernel: nothing here is invented, positions and poses come from the recording. Shared by the reveal film and the
    /// "그날 밤의 재구성" montage.
    /// </summary>
    public sealed class ReplayStage
    {
        readonly Session _s; GameState S => _s.S;
        public readonly ReplaySegment Seg; public readonly Incident Inc; public readonly MurderPlan Plan;
        public string Culprit => Inc?.Culprit; public string Victim => Inc?.Victim;
        readonly Dictionary<string, (int pose, int anim, int status, int carry)> _last = new Dictionary<string, (int, int, int, int)>();
        long _appliedTo = long.MinValue;
        public double T { get; private set; } = double.NaN;
        /// <summary>Playback speed relative to real time (walk cycles follow it so feet don't skate in slow motion).</summary>
        public float Rate = 1f;
        public bool Frozen, Sounds = true;
        public event Action<LedgerEvent> OnEvent;
        public long T0 => Seg.Frames[0].Tick; public long T1 => Seg.Frames[Seg.Frames.Count - 1].Tick;

        public ReplayStage(Session s, ReplaySegment seg)
        {
            _s = s; Seg = seg;
            Inc = S.Incidents.TryGetValue(seg.Incident, out var i) ? i : null;
            Plan = Inc?.PlanId != null && S.Plans.TryGetValue(Inc.PlanId, out var p) ? p : null;
        }

        /// <summary>A replay segment for an incident without freezing anything into the kernel (read-only twin of Replay.BuildSegments).</summary>
        public static ReplaySegment SegmentFor(GameState S, Incident inc)
        {
            if (inc == null) return null;
            var frozen = S.Replays?.FirstOrDefault(r => r.Incident == inc.Id); if (frozen != null) return frozen;
            var b = S.ReplayBuf; if (b == null || b.Count < 2) return null;
            long t0 = inc.SegmentStartTick, t1 = S.Ledger.Where(e => e.Type == "BodySeen" && e.Target == inc.Victim).Select(e => e.Tick).DefaultIfEmpty(S.Tick).Min() + 60;
            long death = S.Ledger.Where(e => e.Type == "Death" && e.Actor == inc.Victim && e.Tick >= t0).Select(e => e.Tick).DefaultIfEmpty(long.MinValue).Min();
            if (death != long.MinValue && death + 40 > t1 && death - t1 < 1200) t1 = Math.Min(S.Tick, death + 40);
            var frames = b.Where(f => f.Tick >= t0 && f.Tick <= t1).ToList(); if (frames.Count < 2) return null;
            var who = new HashSet<string> { inc.Victim }; if (inc.Culprit != null) who.Add(inc.Culprit);
            foreach (var e in S.Ledger.Where(e => e.Type == "CourierAsk" && e.Actor == inc.Culprit && e.Data == inc.Victim)) who.Add(e.Target);
            var seg = new ReplaySegment { Incident = inc.Id, T0 = t0, T1 = t1, Frames = frames, LayoutHash = S.Layout.Hash, Actors = Replay.Order.ToList() };
            seg.Events = S.Ledger.Where(e => e.Tick >= t0 && e.Tick <= t1 && (who.Contains(e.Actor) || who.Contains(e.Target) || e.Type == "Circuit" || e.Type == "PressArmed" || e.Type == "Death" || e.Type == "Strike" || e.Type == "BodySeen" || e.Type == "RecorderPlay" && e.Actor == inc.Culprit || e.Type == "TrapFired" && e.Actor == inc.Culprit)).ToList();
            return seg;
        }

        public void Begin()
        {
            foreach (var v in _s.World.Actors.Values) { v.ReplayDriven = true; v.ForceVisible = true; v.Rig?.Anim?.SetLookAt(null); }
            foreach (var v in _s.World.Actors.Values) v.Rig?.SetDisguise(null);
            // a body lying as a live ragdoll (Game/Physicality, the present-day corpse) would glue its bones to the floor where it
            // fell and override every recorded pose: the recording owns everyone from here (no blend: the world is paused at the
            // verdict, and a blend-out would never finish)
            foreach (var v in _s.World.Actors.Values) { var rd = v.Physical?.Ragdoll; if (rd != null && rd.Active) rd.End(false); }
            ResetEffects(); T = double.NaN;
        }

        /// <summary>Hand everyone back to the kernel.</summary>
        public void End() { Release(); _s.World.ForceResync(); }
        /// <summary>Put back what the replay changed in the world (the stand-in weapon, hidden blood and marks, its own ragdolls) without resyncing actors.</summary>
        public void Release() { ClearProp(); RestoreTraces(); _pendingDead.Clear(); EndRagdolls(); }

        // ---------------------------------------------------------------- the collapse, physically
        /// <summary>Recorded deaths on one's feet become a real fall: the actor's PhysicalRagdoll takes the body at the death tick
        /// (a push from whoever struck, if they stand close), gravity and the joint limits do the rest. Off → a death clip.</summary>
        public bool UseRagdoll = true;
        readonly Dictionary<string, float> _ragdolled = new Dictionary<string, float>();
        bool Collapse(ActorView v, int variant)
        {
            var rd = UseRagdoll ? v.Physical?.Ragdoll : null; var an = v.Rig?.Anim; if (rd == null || an == null) return false;
            // underneath, the animator already lies the way the body will settle, so ending the ragdoll later never pops it upright
            an.SetPosture(LyingPosture(variant)); an.SetDeadPose(variant); an.Tick(0.8f);
            var chestB = v.Rig.Bone(HBone.Chest); var chest = chestB != null ? chestB.position : v.transform.position + Vector3.up * 1.2f;
            var dir = -v.transform.forward; dir.y = 0; float push = 9f;
            var cv = Culprit != null && Culprit != v.Id ? _s.World.ViewOf(Culprit) : null;
            if (cv != null) { var d = v.transform.position - cv.transform.position; d.y = 0; if (d.magnitude < 2.4f && d.sqrMagnitude > 1e-4f) { dir = d.normalized; push = 20f; } }
            bool ok = false;
            try { ok = rd.Begin(Vector3.zero, (dir.normalized + Vector3.down * 0.3f) * push, chest); } catch (Exception e) { Debug.LogException(e); }
            if (!ok) { an.SetDeadPose(-1); return false; }
            _ragdolled[v.Id] = Time.unscaledTime;
            if (AutoProbe.Active) Debug.Log($"[CINE] replay collapse {v.Id} at tick {T:0}: ragdoll, push {push:0} from {(push > 10f ? Culprit : "none")}");
            return true;
        }
        void EndRagdoll(ActorView v)
        {
            if (v == null || !_ragdolled.Remove(v.Id)) return;
            var rd = v.Physical?.Ragdoll; if (rd != null && rd.Active) rd.End(false);
        }
        void EndRagdolls()
        {
            foreach (var id in _ragdolled.Keys.ToList()) EndRagdoll(_s.World.ViewOf(id));
            _ragdolled.Clear();
        }
        static Posture LyingPosture(int variant) => variant == 1 || variant == 5 ? Posture.LieFront : variant == 2 ? Posture.LieSide : variant == 3 ? Posture.Slumped : Posture.LieBack;

        // ---------------------------------------------------------------- time
        /// <summary>Jump to a moment. Going back rebuilds wounds/disguises from the start; going forward applies the events crossed.</summary>
        public void Seek(double t)
        {
            t = Math.Max(T0, Math.Min(T1, t));
            bool back = double.IsNaN(T) || t < T - 0.01;
            if (back) { ResetEffects(); ClearProp(); _appliedTo = long.MinValue; _last.Clear(); }
            EndRagdolls();   // a jump in time: bodies are posed from the recording again (a live death replays its fall)
            bool snd = Sounds; Sounds = false; ApplyEvents(t); Sounds = snd;
            T = t; Apply(true);
        }
        /// <summary>Play forward to t (events crossed fire with their effects).</summary>
        public void Advance(double t)
        {
            t = Math.Max(T0, Math.Min(T1, t)); if (double.IsNaN(T)) { Seek(t); return; }
            if (t < T) { Seek(t); return; }
            ApplyEvents(t); T = t; Apply(false);
        }

        void ApplyEvents(double t)
        {
            foreach (var e in Seg.Events)
            {
                if (e.Tick <= _appliedTo || e.Tick > t) continue;
                Effect(e);
                try { OnEvent?.Invoke(e); } catch (Exception ex) { Debug.LogException(ex); }
            }
            _appliedTo = Math.Max(_appliedTo, (long)Math.Floor(t));
        }

        // ---------------------------------------------------------------- frames
        (ReplayFrame A, ReplayFrame B, float u) Bracket(double t)
        {
            var fr = Seg.Frames; int lo = 0, hi = fr.Count - 1;
            while (lo < hi) { int mid = (lo + hi + 1) / 2; if (fr[mid].Tick <= t) lo = mid; else hi = mid - 1; }
            var A = fr[lo]; var B = fr[Math.Min(fr.Count - 1, lo + 1)];
            float u = B.Tick > A.Tick ? Mathf.Clamp01((float)((t - A.Tick) / (B.Tick - A.Tick))) : 0f;
            return (A, B, u);
        }
        public double ClockAt(double t) { var (A, B, u) = Bracket(t); return A.Clock + (B.Clock - A.Clock) * u; }

        /// <summary>Recorded state of one actor at the current moment (status: 0 absent, 1 active, 2 down, 3 dead).</summary>
        public (Vector3 pos, float yaw, int status, bool moving) StateOf(string id, double? at = null)
        {
            var (A, B, u) = Bracket(at ?? T); int i = Array.IndexOf(Replay.Order, id); if (i < 0 || A.Data == null || B.Data == null) return (Vector3.zero, 0, 0, false);
            int o = i * Replay.Stride; var da = A.Data; var db = B.Data; if (o + Replay.Stride > da.Length || o + Replay.Stride > db.Length) return (Vector3.zero, 0, 0, false);
            var pa = _s.World.ToWorld(new P3((int)da[o + 2], da[o], da[o + 1])); var pb = _s.World.ToWorld(new P3((int)db[o + 2], db[o], db[o + 1]));
            var pos = (int)da[o + 2] == (int)db[o + 2] && (pb - pa).sqrMagnitude < 36f ? Vector3.Lerp(pa, pb, u) : (u < 0.5f ? pa : pb);
            return (pos, Mathf.LerpAngle(da[o + 3], db[o + 3], u), (int)da[o + 6], (int)da[o + 5] >= 1000);
        }

        // Everyone is posed from the RECORDED state at this tick, never from the kernel's final state: alive (standing, walking,
        // doing what the recording says) until the recorded death tick; at that tick a physical collapse (the captured fall onto
        // the back, or a fall into the ground-level pose it settles in); afterwards the body lies where the recording has it,
        // carried on a shoulder or dragged by the armpits/ankles. (When a kernel Replay.StateAt(...) API exists it will replace
        // the frame lookups here; the frames already carry position, floor, yaw, pose, anim, status and carry for every tick.)
        readonly Dictionary<string, (int variant, float at)> _pendingDead = new Dictionary<string, (int, float)>();
        readonly Dictionary<string, Vector3> _dragLocalHead = new Dictionary<string, Vector3>();
        float _traceSyncAt = -1f;

        void Apply(bool snap)
        {
            var (A, B, u) = Bracket(T);
            var da = A.Data; var db = B.Data; if (da == null || db == null) return;
            var order = Replay.Order;
            // who is carried / dragged by whom at this moment
            var carriedBy = new Dictionary<int, int>();
            for (int i = 0; i < order.Length; i++)
            {
                int o = i * Replay.Stride; if (o + Replay.Stride > da.Length) break;
                int c = (int)da[o + 7]; if (c >= 0 && c < order.Length && c != i) carriedBy[c] = i;
            }
            for (int i = 0; i < order.Length; i++)
            {
                int o = i * Replay.Stride; if (o + Replay.Stride > da.Length || o + Replay.Stride > db.Length) break;
                var v = _s.World.ViewOf(order[i]); if (v == null) continue;
                int status = (int)da[o + 6];
                if (status == 0) { v.SetHidden(true); continue; }
                v.SetHidden(false);
                int fa = (int)da[o + 2], fb = (int)db[o + 2];
                var pa = _s.World.ToWorld(new P3(fa, da[o], da[o + 1])); var pb = _s.World.ToWorld(new P3(fb, db[o], db[o + 1]));
                var pos = fa == fb && (pb - pa).sqrMagnitude < 36f ? Vector3.Lerp(pa, pb, u) : (u < 0.5f ? pa : pb);
                float yaw = Mathf.LerpAngle(da[o + 3], db[o + 3], u);
                var an = v.Rig?.Anim;
                int animRaw = (int)da[o + 5]; int moving = animRaw >= 1000 ? animRaw / 1000 : 0; int anim = animRaw % 1000;
                int pose = (int)da[o + 4]; int carry = (int)da[o + 7];
                (int pose, int anim, int status, int carry) last = _last.TryGetValue(v.Id, out var l) ? l : (-1, -1, -1, -2);
                bool carried = carriedBy.TryGetValue(i, out int moverIdx); bool dragged = carried && IsDrag(moverIdx, i, da, out _);
                if (carried || status != 3) EndRagdoll(v);   // lifted or dragged away (or alive again): the recording poses the body
                if (!carried || dragged)
                {
                    if (dragged) PlaceDragged(v, moverIdx, i, da);
                    else { v.transform.position = pos; v.transform.rotation = Quaternion.Euler(0, yaw, 0); }
                }
                if (an == null) continue;
                if (snap || status != last.status)
                {
                    int variant = ActorView.DeadVariant(v.Id, (Pose)pose);
                    if (status == 3)
                    {
                        v.Rig.SetExpression(Expr.Dead); v.Rig.SetBlink(false);
                        if (!snap && last.status == 1 && !carried)
                        {
                            // the moment of death, on one's feet: a physical fall (ragdoll); failing that the captured collapse onto
                            // the back, or a fall clip that settles into the ground-level pose
                            if (Collapse(v, variant)) { }
                            else if (variant == 0 || variant == 4) an.SetDeadPose(variant);
                            else { an.PlayAction(ActionAnim.Fall, 1.0f); _pendingDead[v.Id] = (variant, Time.unscaledTime + 0.95f); }
                        }
                        else if (_ragdolled.ContainsKey(v.Id)) { }   // still falling / lying where physics put it
                        else
                        {
                            // already dead at this moment (a page after the death): lying as recorded, no second collapse
                            _pendingDead.Remove(v.Id);
                            if (!an.IsDead) { an.SetPosture(LyingPosture(variant)); an.SetDeadPose(variant); an.Tick(0.8f); }
                            else an.SetDeadPose(variant);
                        }
                    }
                    else
                    {
                        // alive (or only unconscious) at this moment, whatever became of them later
                        _pendingDead.Remove(v.Id);
                        if (an.IsDead) an.SetDeadPose(-1);
                        if (status == 2) { an.SetPosture(Posture.LieBack); v.Rig.SetExpression(Expr.Pain); v.Rig.SetBlink(false); }
                        else { v.Rig.SetBlink(true); if (last.status >= 2 || snap) v.Rig.SetExpression(Expr.Neutral); last.pose = -1; last.anim = -1; }
                    }
                }
                if (_pendingDead.TryGetValue(v.Id, out var pd) && Time.unscaledTime >= pd.at) { _pendingDead.Remove(v.Id); an.SetDeadPose(pd.variant); }
                if (status == 1)
                {
                    var vel = moving > 0 ? (pb - pa) / Mathf.Max(0.05f, (B.Tick - A.Tick) * SimTime.Dt) : Vector3.zero;
                    an.SetMove(Frozen ? Vector3.zero : vel * Mathf.Clamp(Rate, 0.15f, 3f), moving == 2);
                    if (pose != last.pose) an.SetPosture(ActorView.MapPose((Pose)pose));
                    bool dragging = carry >= 0 && carry < order.Length && IsDrag(i, carry, da, out _);
                    if (anim != last.anim && !(dragging && last.anim == -7))
                    {
                        if (dragging) an.PlayAction(ActionAnim.Drag, 999f);
                        else ActorView.PlayAnim(an, (Anim)anim);
                    }
                    if (dragging) anim = -7;   // remembered as "dragging" so the clip is not restarted every frame
                }
                if (carry != last.carry)
                {
                    // a shoulder carry parents the body to the carrier; a drag keeps it on the floor behind them
                    bool drag = carry >= 0 && carry < order.Length && IsDrag(i, carry, da, out _);
                    an.SetCarrying(carry >= 0 && carry < order.Length && !drag ? _s.World.ViewOf(order[carry])?.Rig : null);
                }
                _last[v.Id] = (pose, anim, status, carry);
            }
            SyncTraces(snap);
        }

        /// <summary>
        /// Is this a drag (body on the floor) rather than a shoulder carry? The recording says so when the mover's anim is Drag;
        /// the CarryStart ledger entry may also name the grip ("drag", "armpit", "ankle"). The grip decides which end leads.
        /// </summary>
        bool IsDrag(int mover, int body, float[] da, out bool ankles)
        {
            ankles = false; int o = mover * Replay.Stride; if (o + Replay.Stride > da.Length || body < 0 || body >= Replay.Order.Length) return false;
            bool drag = ((int)da[o + 5] % 1000) == (int)Anim.Drag;
            string m = Replay.Order[mover], b = Replay.Order[body]; string data = null;
            foreach (var e in Seg.Events) if (e.Type == "CarryStart" && e.Actor == m && e.Target == b && e.Tick <= T) data = e.Data;
            if (data != null)
            {
                var d = data.ToLowerInvariant();
                if (d.Contains("drag") || d.Contains("armpit") || d.Contains("ankle") || d.Contains("feet")) drag = true;
                if (d.Contains("ankle") || d.Contains("feet")) ankles = true;
            }
            return drag;
        }

        /// <summary>A dragged body lies on the floor behind the mover, the gripped end (shoulders or ankles) toward their hands.</summary>
        void PlaceDragged(ActorView v, int mover, int body, float[] da)
        {
            var mv = _s.World.ViewOf(Replay.Order[mover]); if (mv == null) return;
            IsDrag(mover, body, da, out bool ankles);
            var fwd = mv.transform.forward; fwd.y = 0; if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward; fwd.Normalize();
            var gripAt = mv.transform.position - fwd * 0.45f;                 // the mover's hands, reaching back
            var bodyCentre = gripAt - fwd * 0.85f; bodyCentre.y = mv.transform.position.y;
            // which way does this body's head point in its lying pose (local space, measured from the rig)?
            var rig = v.Rig; Vector3 lh;
            if (rig != null && rig.Head != null && rig.Hips != null)
            {
                var w = rig.Head.position - rig.Hips.position; w.y = 0;
                if (w.sqrMagnitude > 0.04f) { lh = v.transform.InverseTransformDirection(w.normalized); lh.y = 0; if (lh.sqrMagnitude > 1e-4f) _dragLocalHead[v.Id] = lh.normalized; }
            }
            if (!_dragLocalHead.TryGetValue(v.Id, out lh)) lh = Vector3.back;
            var want = ankles ? -fwd : fwd;                                    // the gripped end leads, toward the mover
            float yaw = Mathf.Atan2(want.x, want.z) * Mathf.Rad2Deg - Mathf.Atan2(lh.x, lh.z) * Mathf.Rad2Deg;
            v.transform.rotation = Quaternion.Euler(0, yaw, 0);
            v.transform.position = bodyCentre;
        }

        // ---- blood and marks: only those that existed at this moment (the final state's pools never appear before the act)
        readonly Dictionary<GameObject, bool> _traceObjs = new Dictionary<GameObject, bool>();
        void SyncTraces(bool force)
        {
            if (!force && Time.unscaledTime < _traceSyncAt) return; _traceSyncAt = Time.unscaledTime + 0.25f;
            double now = ClockAt(T);
            try
            {
                foreach (var tv in UnityEngine.Object.FindObjectsByType<TraceView>(FindObjectsInactive.Include, FindObjectsSortMode.None)) ShowTrace(tv.gameObject, tv.TraceId, now);
                foreach (var gv in UnityEngine.Object.FindObjectsByType<GoreDecalView>(FindObjectsInactive.Include, FindObjectsSortMode.None)) ShowTrace(gv.gameObject, gv.TraceId, now);
            }
            catch (Exception e) { Debug.LogException(e); }
        }
        void ShowTrace(GameObject go, string traceId, double now)
        {
            if (go == null || string.IsNullOrEmpty(traceId)) return;
            var t = S.Traces.FirstOrDefault(x => x.Id == traceId); if (t == null) return;
            if (!_traceObjs.ContainsKey(go)) _traceObjs[go] = go.activeSelf;
            bool want = _traceObjs[go] && t.Clock <= now + 0.05;
            if (go.activeSelf != want) go.SetActive(want);
        }
        void RestoreTraces()
        {
            foreach (var kv in _traceObjs) if (kv.Key != null && kv.Key.activeSelf != kv.Value) kv.Key.SetActive(kv.Value);
            _traceObjs.Clear();
        }

        // ---------------------------------------------------------------- ledger effects
        void ResetEffects()
        {
            foreach (var id in Seg.Events.Where(e => e.Type == "Strike" || e.Type == "Death").Select(e => e.Target ?? e.Actor).Distinct())
            { var v = _s.World.ViewOf(id); v?.Rig?.ClearWounds(); v?.Rig?.SetBloodied(0); }
            foreach (var v in _s.World.Actors.Values) v.Rig?.SetDisguise(null);
        }

        void Effect(LedgerEvent e)
        {
            switch (e.Type)
            {
                case "Strike":
                    {
                        var parts = (e.Data ?? "").Split('/'); var v = _s.World.ViewOf(e.Target);
                        if (v?.Rig != null && parts.Length >= 3 && Enum.TryParse(parts[0], out BL23.Game.Characters.BodyRegion r) && Enum.TryParse(parts[1], out DamageType d))
                        {
                            int.TryParse(parts[2], out int sev); int shown = Settings.Gore == 0 ? Math.Min(sev, 1) : Settings.Gore == 1 ? Math.Min(sev, 3) : sev;
                            v.Rig.AddWound(r, d, shown, Vector3.zero, false); if (Sounds) v.Rig.Anim?.PlayAction(ActionAnim.Hurt, 0.4f);
                            if (Sounds) Sfx.Play(d == DamageType.Stab ? "stab" : d == DamageType.Cut ? "slash" : d == DamageType.Choke ? "gasp" : "blunt_hit", v.transform.position + Vector3.up, 0.7f);
                        }
                        if (Sounds) { var a = _s.World.ViewOf(e.Actor)?.Rig?.Anim; if (a != null && a.CurrentAction != ActionAnim.Strangle) a.PlayAction(ActionAnim.Overhead, 0.6f); }
                        break;
                    }
                case "DisguiseOn": { var v = _s.World.ViewOf(e.Actor); v?.Rig?.SetDisguise(S.I(e.Item)?.Type ?? e.Data ?? "cloak"); break; }
                case "DisguiseOff": { _s.World.ViewOf(e.Actor)?.Rig?.SetDisguise(null); break; }
                case "Death": if (Sounds) Sfx.Play("body_fall", null, 0.5f); break;
                case "Circuit": if (Sounds) Sfx.Play("breaker", null, 0.5f); break;
                case "PressContact": if (Sounds) Sfx.Play("hydraulic_press", null, 0.8f); break;
                case "Shove": if (Sounds) { _s.World.ViewOf(e.Actor)?.Rig?.Anim?.PlayAction(ActionAnim.Shove, 0.7f); } break;
                case "Wash": if (Sounds) Sfx.Play("water_splash", null, 0.35f); break;
                case "SealedRoom": case "LockedRoomMade": case "KeySlide": if (Sounds) Sfx.Play("door_lock", null, 0.6f); break;
                case "Burn": if (Sounds) Sfx.Play("candle_flare", null, 0.8f); break;
                case "DumpWater": if (Sounds) Sfx.Play("water_splash", null, 0.7f); break;
            }
        }

        // ---------------------------------------------------------------- the weapon in the hand
        // The recording keeps bodies, not what was in their hands: while the story says the weapon was held, a stand-in of
        // the same item sits in the culprit's weapon hand (the real object is hidden meanwhile, then restored).
        GameObject _prop; string _propFor; ItemView _hiddenReal; bool _hiddenWasActive;
        public void HoldProp(string actor, string itemType, string realItemId = null)
        {
            if (_prop != null && _propFor == actor + "|" + itemType) return;
            ClearProp();
            var v = _s.World.ViewOf(actor); var def = ItemCatalog.Get(itemType); if (v?.Rig == null || def == null) return;
            bool left = Cast.LeftHanded(actor); var anchor = left ? (v.Rig.HandAnchorL ?? v.Rig.HandL) : (v.Rig.HandAnchorR ?? v.Rig.HandR); if (anchor == null) return;
            GameObject go = null;
            try { go = MurderProps.Create(def, null) ?? BL23.Game.Mansion.PropFactory.CreateItem(def, null); } catch (Exception ex) { Debug.LogException(ex); }
            if (go == null) return;
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>()) { rb.isKinematic = true; rb.detectCollisions = false; }
            foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
            go.name = "ReplayProp_" + itemType; go.transform.SetParent(anchor, false); go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            _prop = go; _propFor = actor + "|" + itemType;
            if (realItemId != null && _s.World.Items.TryGetValue(realItemId, out var iv) && iv.Visual != null) { _hiddenReal = iv; _hiddenWasActive = iv.Visual.activeSelf; iv.Visual.SetActive(false); }
        }
        public void ClearProp()
        {
            if (_prop != null) UnityEngine.Object.Destroy(_prop); _prop = null; _propFor = null;
            if (_hiddenReal != null && _hiddenReal.Visual != null) _hiddenReal.Visual.SetActive(_hiddenWasActive); _hiddenReal = null;
        }
        public Transform PropTransform => _prop != null ? _prop.transform : null;
    }
}
