using System;
using System.Linq;
using BL23.Game.Audio;
using BL23.Game.Characters;
using BL23.Game.Physicality;
using BL23.Sim;
using UnityEngine;
using Pose = BL23.Sim.Pose;

namespace BL23.Game
{
    /// <summary>Presentation of one actor: follows the kernel pose, maps activities to animation, wounds, held props, speech.</summary>
    public sealed class ActorView : MonoBehaviour
    {
        public string Id; public ActorRig Rig; WorldPresenter W; GameState S => Session.I.S;
        public PhysicalCharacter Physical { get; private set; }
        Vector3 _pos, _vel; float _yaw; Anim _lastAnim = (Anim)(-1); Pose _lastPose = (Pose)(-1); Emotion _lastEmo = (Emotion)(-1); ActorStatus _lastStatus = (ActorStatus)(-1);
        string _heldR, _heldL; float _talkUntil; float _stepAcc; DarkFace _darkFace; bool _hidden; public bool ForceVisible; public bool ForceHidden; public bool ReplayDriven;
        public Vector3 HeadPos => Rig != null && Rig.EyeAnchor != null ? Rig.EyeAnchor.position : transform.position + Vector3.up * 1.55f;
        public Vector3 Velocity => _vel;
        public bool Talking => Time.time < _talkUntil;
        readonly IdleLife _life = new IdleLife();

        public void Init(WorldPresenter w, Actor a)
        {
            W = w; Id = a.Id;
            try { Rig = ActorFactory.Create(a.Def, transform); } catch (Exception e) { Debug.LogException(e); }
            if (Rig == null) Rig = FallbackActor.Create(a.Def, transform);
            Snap();
            Physical = gameObject.AddComponent<PhysicalCharacter>(); Physical.Bind(this, a);
        }

        public void Snap()
        {
            var a = S.A(Id); if (a == null) return;
            _pos = Target(a); _yaw = a.Yaw; transform.position = _pos; transform.rotation = Quaternion.Euler(0, _yaw, 0);
            _lastAnim = (Anim)(-1); _lastPose = (Pose)(-1);
            Physical?.ResetMotion();
        }

        Vector3 Target(Actor a)
        {
            if (a.StairId >= 0)
            {
                float t = (float)Math.Min(1, a.StairUntil);
                var p0 = W.ToWorld(a.StairFrom); var p1 = W.ToWorld(a.StairTo);
                return Vector3.Lerp(p0, p1, t);
            }
            var stand = TrialStandPos(a); if (stand.HasValue) return stand.Value;   // (cinematics) the trial: behind the podium, not inside it
            return W.ToWorld(a.Pos);
        }

        /// <summary>
        /// (Cinematics, trial blocking) The kernel puts a trial participant ON the podium's position; the court builds a stand
        /// anchor behind each podium, facing the ring. While the trial (and the verdict) runs, people stand there, a hand's width
        /// behind the rail, so the podium reads as a lectern in front of them instead of a barrel they stand inside.
        /// </summary>
        Vector3? TrialStandPos(Actor a)
        {
            var T = S.Trial; var court = W != null ? W.Court : null;
            if (T == null || court == null || a.IsButler || (S.Phase != Phase.Trial && S.Phase != Phase.Verdict && S.Phase != Phase.Execution)) return null;
            if (!T.Seat.TryGetValue(a.Id, out var seat) || !T.Participants.Contains(a.Id)) return null;
            var anchor = court.StandAnchor(seat); if (anchor == null) return null;
            var pod = W.ToWorld(a.Pos); var ap = anchor.position;
            if ((new Vector2(pod.x - ap.x, pod.z - ap.z)).sqrMagnitude > 1.4f * 1.4f || Mathf.Abs(pod.y - ap.y) > 1.5f) return null;   // only while the kernel has them at that podium
            var back = new Vector3(ap.x - pod.x, 0, ap.z - pod.z); if (back.sqrMagnitude > 1e-4f) ap += back.normalized * 0.06f;
            ap.y = pod.y;
            return ap;
        }

        public void Tick(float dt)
        {
            var a = S.A(Id); if (a == null || Rig == null || ReplayDriven) return;
            bool gone = a.Status == ActorStatus.Executed || a.Status == ActorStatus.Escaped;
            // first person: the player's own body stays visible (hands, arms, legs); only the head is hidden from its own eyes.
            // The trial and the reveal film everyone from outside, so the head comes back there.
            bool fp = false;
            if (a.IsPlayer && Rig != null) { Rig.SetHeadHidden(!ForceVisible && (Session.I.Trial == null || !Session.I.Trial.Active) && (Session.I.Reveal == null || !Session.I.Reveal.Active)); if (Rig.Anim != null) Rig.Anim.FirstPersonCalm = Rig.HeadHidden && !FilmHands; FirstPersonBody(); }
            // people in rooms the mansion has culled (other floors, far away behind walls) are not drawn either
            var mv = W.Mansion; bool roomCulled = !ForceVisible && mv != null && mv.Rooms != null && a.Room >= 0 && a.Room < mv.Rooms.Length && mv.Rooms[a.Room] != null && !mv.Rooms[a.Room].Visible && a.StairId < 0;
            SetHidden(ForceHidden || gone || fp || roomCulled);
            EnsureBody(); if (_body != null) _body.enabled = !_hidden && a.Status == ActorStatus.Active && (a.Pose == Pose.Stand || a.Pose == Pose.Crouch) && a.CarriedBy == null && a.StairId < 0;
            if (gone) return;
            if (Physical != null && Physical.DrivesPosition) { var before = _pos; _pos = Physical.PhysicalPosition; _yaw = Physical.FacingYaw; _vel = dt > 0 ? (_pos - before) / dt : Vector3.zero; }
            else if (a.IsPlayer && Session.I.Player != null && Session.I.Player.Controlling) { _pos = Session.I.Player.FeetPosition; _yaw = Session.I.Player.Yaw; }
            else
            {
                var target = Target(a);
                if (Time.time < _sitAt && _seatSpot >= 0) target = _seatApproach;   // still standing by the chair
                bool dragged = false; Transform dragger = null;   // --- violence track: too heavy to lift → dragged along the floor behind the carrier
                if (a.CarriedBy != null) { var c = W.ViewOf(a.CarriedBy); if (c != null) { dragged = ViolencePresenter.DraggedBy(S, a, out _); dragger = c.transform; target = dragged ? c.transform.position - c.transform.forward * 0.95f : c.transform.position + Vector3.up * 0.0f; } }
                if ((target - _pos).sqrMagnitude > 25f) _pos = target; // teleports (loop reset, elevator)
                var prev = _pos;
                _pos = Vector3.Lerp(_pos, target, 1f - Mathf.Exp(-14f * dt));
                _vel = dt > 0 ? (_pos - prev) / dt : Vector3.zero;
                _yaw = Mathf.LerpAngle(_yaw, dragged && dragger != null ? dragger.eulerAngles.y + 180f : a.Yaw, 1f - Mathf.Exp(-10f * dt));
                // Yusti in the court: seated on the judge's throne for the whole trial
                var seat = JudgeSeat(a); if (seat != null) { _pos = seat.position - Vector3.up * 0.44f; _yaw = seat.eulerAngles.y; }
            }
            transform.position = _pos; transform.rotation = Quaternion.Euler(0, _yaw, 0);
            Physical?.FollowPresentation(_pos);
            var an = Rig.Anim;
            if (an == null) return;
            // body state
            if (a.Status != _lastStatus)
            {
                _lastStatus = a.Status;
                if (a.Status == ActorStatus.Dead) { an.SetDeadPose(DeadVariant(Id, a.Pose)); Rig.SetExpression(Expr.Dead); Rig.SetBlink(false); _deadAt = Time.time; _headSoakFor = -1; }
                else if (a.Status == ActorStatus.Unconscious) { an.SetPosture(a.Pose == Pose.LieFront ? Posture.LieFront : Posture.LieBack); Rig.SetExpression(Expr.Pain); Rig.SetBlink(false); }
                else { an.SetDeadPose(-1); Rig.SetBlink(true); _lastPose = (Pose)(-1); }
            }
            if (a.Status == ActorStatus.Active)
            {
                an.SetMove(a.IsPlayer && Session.I.Player != null && Session.I.Player.Controlling ? Session.I.Player.Velocity : _vel, a.Running);
                var pose = JudgeSeat(a) != null ? Pose.Sit : a.Pose;
                // sitting down is a motion, not a switch: face the seat, pull the chair out, then sit (and the reverse on rising)
                if (pose == Pose.Sit && a.Spot >= 0 && a.Spot != _seatSpot) BeginSit(a, an);
                else if (pose != Pose.Sit && _seatSpot >= 0) EndSit(an);
                if (pose == Pose.Sit && Time.time < _sitAt) pose = Pose.Stand;
                // lying down: sit on the edge first, then lie back; getting up: sit up, then stand
                bool lying = pose == Pose.Sleep || pose == Pose.LieBack;
                if (lying && !_wasLying && a.Spot >= 0) { _lieAt = !_hidden ? Time.time + 0.9f : -1f; _wasLying = true; }
                else if (!lying && _wasLying) { _riseUntil = !_hidden ? Time.time + 0.7f : -1f; _wasLying = false; }
                if (lying && Time.time < _lieAt) pose = Pose.Sit;
                else if (!lying && Time.time < _riseUntil && pose == Pose.Stand) pose = Pose.Sit;
                if (pose != _lastPose) { _lastPose = pose; an.SetPosture(MapPose(pose)); }
                if (a.Anim != _lastAnim) { _lastAnim = a.Anim; PlayAnim(an, a.Anim); SetHandProp(a); }
                var b = a.Body; an.SetInjury(Mathf.Min(b.Mobility, a.Physical?.Mobility ?? 1), Mathf.Min(b.HandL, a.Physical?.GripLeft ?? 1) <= .3f, Mathf.Min(b.HandR, a.Physical?.GripRight ?? 1) <= .3f, LegHurt(b, BL23.Sim.BodyRegion.LegL, BL23.Sim.BodyRegion.FootL), LegHurt(b, BL23.Sim.BodyRegion.LegR, BL23.Sim.BodyRegion.FootR), true);
                // why people move: someone near you says what they are off to do
                var actId = a.Act?.Id;
                if (actId != _lastActId) { _lastActId = actId; if (!a.IsPlayer && !a.IsButler && !_hidden) IntentBark(a, actId); }
                var emo = a.Needs.Fear > 0.75f && a.Emotion == Emotion.Neutral ? Emotion.Fear : a.Emotion;
                if (emo != _lastEmo) { _lastEmo = emo; Rig.SetExpression(MapEmo(emo)); }
                // look at conversation partner / nearby player
                Vector3? look = null;
                var partner = a.TalkingTo != null ? W.ViewOf(a.TalkingTo) : null;
                if (partner != null && !a.IsPlayer) look = partner.HeadPos;
                // small signs of life (who walks in, who passes by, fidgets by mood); only near the camera
                Vector3? life = null;
                if (!a.IsPlayer && !a.IsButler && !_hidden && Camera.main != null && (Camera.main.transform.position - _pos).sqrMagnitude < 900f && !TimeLink.Lapsing) life = _life.Tick(this, a, an, S, W, dt);   // (time-on-demand: not while time is passed)
                // --- time-on-demand (begin): the clock stands still, people do not look frozen (FrozenLife, AmbientChatter)
                if (!a.IsPlayer && !_hidden && FrozenLife.Frame(S, W)) { var still = _still.Tick(this, a, an, S, W, dt); if (still != null) life = still; }
                // --- time-on-demand (end)
                if (look == null && life != null) look = life;
                else if (look == null && !a.IsPlayer && S.Player != null && S.Player.Room == a.Room) look = GlanceAtPlayer(a);
                an.SetLookAt(look);
                // footsteps
                if (!a.IsPlayer && _vel.magnitude > 0.4f && !_hidden)
                {
                    _stepAcc += dt * (a.Running ? 3.2f : 1.9f);
                    if (_stepAcc > 1f) { _stepAcc = 0; float d = Session.I.Player != null ? Vector3.Distance(_pos, Session.I.Player.FeetPosition) : 99; if (d < 18f && !(Session.I.TimeDir != null && Session.I.TimeDir.MuteWorldAudio)) Sfx.Footstep(SurfaceAt(a), _pos, a.Running ? 0.7f : 0.45f); }   // (time-on-demand: not heard while the house is fast-forwarded)
                }
            }
            Rig.SetTalking(Time.time < _talkUntil);
            // the kernel only bloodies attackers' clothes: a victim's soak comes from their own wounds (Game/Gore/GoreSoak).
            // The body shader's spatter is spread evenly over the clothes, so a dead victim's is kept light (the wounds' own
            // blood masks soak the cloth round each wound); the head bleeds into the hair on the side it lies on.
            Rig.SetBloodied(a.Status == ActorStatus.Dead ? DeadSpatter(a) : a.BloodOnClothes); Rig.SetWet(a.Wet);
            if (a.Status == ActorStatus.Dead) HeadSoak(a);
            SyncHeld(a);
            // BREAK: the old hologram glitch (magenta/white flicker + scanlines) read as a rendering bug to the user — it is gone
            // for everyone. A publicly cornered character shows a dark face instead (panic → cornered stare, counter → hollow grin).
            an.SetBreak(0f);
            {
                var tr = Session.I.Trial; bool br = tr != null && tr.Active && tr.BreakFor == Id;
                var want = br ? (tr.BreakKind == "counter" ? DarkFace.HollowGrin : DarkFace.CorneredStare) : DarkFace.None;
                if (want != _darkFace) { _darkFace = want; Rig.SetDarkFace(want, 1f); }
            }
        }

        // (perf) every person, every frame: a plain loop instead of two LINQ Any() (each boxed the list's enumerator)
        static bool LegHurt(BL23.Sim.Body b, BL23.Sim.BodyRegion leg, BL23.Sim.BodyRegion foot)
        {
            var ws = b.Wounds; for (int i = 0; i < ws.Count; i++) { var r = ws[i].Region; if (r == leg || r == foot) return true; }
            return false;
        }

        // people do not stare: now and then, when 민혁 is close and in front of them and they are not busy, a short glance
        // (friends and the curious more often), then back to what they were doing
        float _glanceUntil = -1f, _glanceNext;
        Vector3? GlanceAtPlayer(Actor a)
        {
            var pv = W.ViewOf(Cast.Player); if (pv == null) return null;
            float d = S.Player.Pos.DistXZ(a.Pos);
            if (Time.time < _glanceUntil) return d < 5f ? pv.HeadPos : (Vector3?)null;
            if (Time.time < _glanceNext || d > 3.2f) return null;
            _glanceNext = Time.time + UnityEngine.Random.Range(9f, 26f);
            bool busy = a.TalkingTo != null || (a.Act != null && a.Anim != Anim.Idle && a.Anim != Anim.None);
            var to = pv.HeadPos - HeadPos; to.y = 0f;
            if (busy || Vector3.Angle(transform.forward, to) > 100f) return null;
            float like = S.HasRel(a.Id, Cast.Player) ? S.R(a.Id, Cast.Player).Like : 0f;
            if (UnityEngine.Random.value > 0.22f + Mathf.Max(0f, like) * 0.35f + a.Def.P.Curiosity * 0.15f) return null;
            _glanceUntil = Time.time + UnityEngine.Random.Range(1.1f, 2.6f);
            return pv.HeadPos;
        }

        string _lastActId; float _barkAt = -99f;
        // --- time-on-demand (begin): life in a still world, and no burst of barks after time was passed
        readonly FrozenLife _still = new FrozenLife();
        /// <summary>Forget the activity changes that happened while time was passed (called when a skip ends).</summary>
        public void ResyncIntent() { var a = S.A(Id); _lastActId = a?.Act?.Id; }
        // --- time-on-demand (end)
        void IntentBark(Actor a, string actId)
        {
            if (Time.time - _barkAt < 75f || a.TalkingTo != null || Session.I.Dialogue.Active || (Session.I.Cine != null && Session.I.Cine.Busy) || TimeLink.Lapsing) return;   // (time-on-demand: silent while time is passed)
            var me = S.Player; if (me == null || me.Room != a.Room || me.Pos.DistXZ(a.Pos) > 9f) return;
            var line = IntentLines.Line(a.Id, actId); if (line == null || UnityEngine.Random.value > 0.6f) return;
            _barkAt = Time.time;
            Hud.I?.Overheard(a.Id, line, me.Pos.DistXZ(a.Pos));
            BL23.Game.Audio.VoiceBabble.Speak(a.Id, line); Talk(Mathf.Clamp(line.Length * 0.06f, 0.8f, 2.5f));
        }

        // a solid body (kinematic capsule): the player cannot walk through people, and loose things get nudged when they pass
        CapsuleCollider _body;
        void EnsureBody()
        {
            if (Physical != null) return;
            if (_body != null || Rig == null || Id == Cast.Player) return;
            var go = new GameObject("Body"); go.transform.SetParent(Rig.transform, false);
            _body = go.AddComponent<CapsuleCollider>(); _body.radius = 0.24f; _body.height = 1.62f; _body.center = new Vector3(0, 0.84f, 0);
            var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.None;
        }

        // ---- a book, a cup, a pen: the small thing in the hand while doing something (only when the hand is otherwise empty)
        GameObject _handProp;
        void SetHandProp(Actor a)
        {
            if (_handProp != null) { Destroy(_handProp); _handProp = null; }
            var kind = HandProps.For(a.Anim); if (kind == null || Rig == null || Rig.HandAnchorR == null || a.HandR != null) return;
            _handProp = HandProps.Attach(Rig.HandAnchorR, kind);
        }

        // ---- sitting down / getting up
        int _seatSpot = -1, _seatFid = -1; float _sitAt = -1f; Vector3 _seatApproach, _seatBack;
        bool _wasLying; float _lieAt = -1f, _riseUntil = -1f;
        static bool IsChair(string type) => type == "Chair" || type == "BarStool";
        void BeginSit(Actor a, ActorAnimator an)
        {
            _seatSpot = a.Spot; var sp = S.Layout.Spots[a.Spot]; _seatFid = sp.Furniture;
            _seatApproach = W.ToWorld(sp.Approach); _seatBack = -(Quaternion.Euler(0, sp.Yaw, 0) * Vector3.forward);
            var f = _seatFid >= 0 && _seatFid < S.Layout.Furniture.Count ? S.Layout.Furniture[_seatFid] : null;
            bool chair = f != null && IsChair(f.Type) && sp.OnFurniture;
            bool visible = !_hidden && (transform.position - _seatApproach).sqrMagnitude < 9f;
            _sitAt = visible ? Time.time + (chair ? 0.7f : 0.3f) : -1f;
            if (chair && visible) { ChairNudge.Sit(W.Mansion?.FurnitureObject(_seatFid), _seatBack); an.PlayAction(ActionAnim.Use, 0.55f); Audio.Sfx.Play("chair_scrape", transform.position, 0.35f); }
        }
        void EndSit(ActorAnimator an)
        {
            var f = _seatFid >= 0 && _seatFid < S.Layout.Furniture.Count ? S.Layout.Furniture[_seatFid] : null;
            if (f != null && IsChair(f.Type)) ChairNudge.Stand(W.Mansion?.FurnitureObject(_seatFid), _seatBack);
            _seatSpot = -1; _seatFid = -1; _sitAt = -1f;
        }

        Transform JudgeSeat(Actor a)
        {
            if (!a.IsButler) return null; var court = W.Court; if (court == null || court.ButlerAnchor == null) return null;
            var r = S.Layout.Room(a.Room); return r != null && r.Type == RoomType.Courtroom ? court.ButlerAnchor : null;
        }

        string SurfaceAt(Actor a)
        {
            var r = S.Layout.Room(a.Room); if (r == null) return "Marble";
            switch (r.Type)
            {
                case RoomType.Bedroom: case RoomType.GuestRoom: case RoomType.Library: case RoomType.Lounge: case RoomType.Theater: case RoomType.Study: case RoomType.Chapel: return "Carpet";
                case RoomType.MirrorWater: case RoomType.RainCorridor: case RoomType.Pool: return "Water";
                case RoomType.Gallery: case RoomType.MusicRoom: case RoomType.Workshop: case RoomType.Stairwell: return "Wood";
                case RoomType.WineCellar: case RoomType.BoilerRoom: case RoomType.Storage: case RoomType.MachineRoom: case RoomType.PowerRoom: case RoomType.Courtyard: return "Stone";
            }
            return "Marble";
        }

        // Playtest 16:26: looking down in first person showed the player's own coat as a black blob at their feet ("like a corpse").
        // While looking down (and not doing something with the hands) the body draws only its shadow; hands still show for actions.
        bool _fpShadowOnly;
        /// <summary>(discovery film) The player's hands are the shot: first-person calm off (the gesture's arms are not pulled
        /// down) and the body drawn however far down he looks. Set only by the film, for its first-person beats.</summary>
        public bool FilmHands;
        void FirstPersonBody()
        {
            var pc = Session.I.Player; bool fpNow = Rig.HeadHidden && pc != null && pc.FirstPerson;
            float pitch = pc == null ? 0f : (pc.Scripted ? pc.ScriptPitch : pc.Pitch);
            bool hands = Physical?.Actions?.Busy == true || Rig.Anim != null && (Rig.Anim.CurrentAction != ActionAnim.None || Rig.Anim.IsCarrying);
            bool want = fpNow && !FilmHands && (pitch > 20f && !hands || FirstPersonHands.Active);   // (violence track: the first-person hands draw the action instead)
            if (want == _fpShadowOnly) return;
            _fpShadowOnly = want;
            var mode = want ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
            foreach (var r in Rig.Skins) if (r != null) r.shadowCastingMode = mode;
            foreach (var r in Rig.ExtraRenderers) if (r != null) r.shadowCastingMode = mode;
        }

        public void SetHidden(bool h)
        {
            if (h == _hidden) return; _hidden = h; Rig?.SetVisible(!h);
        }

        /// <summary>After a replay: rebuild wounds/disguise/carry from the kernel and forget cached animation state.</summary>
        public void RestoreFromKernel()
        {
            var a = S.A(Id); if (a == null || Rig == null) return;
            Rig.ClearWounds(); _headSoakFor = -1;
            foreach (var w in a.Body.Wounds)
            {
                if (w.Sev >= 5) continue;   // severed: drawn by Game/Gore (torso cut, caps), never as a wound patch
                Enum.TryParse(w.Region.ToString(), out BL23.Game.Characters.BodyRegion r);
                int shown = Settings.Gore == 0 ? Math.Min(w.Sev, 1) : Settings.Gore == 1 ? Math.Min(w.Sev, 3) : w.Sev;
                shown = Math.Min(shown, 4);
                Rig.AddWound(r, w.Type, shown, new Vector3(w.lx, w.ly, w.lz), w.Postmortem);
            }
            Rig.SetDisguise(a.Disguise != null ? S.I(a.Disguise)?.Type : null);
            Rig.Anim?.SetCarrying(a.Carrying != null ? W.ViewOf(a.Carrying)?.Rig : null);
            _lastStatus = (ActorStatus)(-1); _lastEmo = (Emotion)(-1);
            _hidden = !_hidden; SetHidden(!_hidden);
            Snap();
            var gt = GetComponent<GoreTorso>(); if (gt != null) gt.Reapply();   // a dismembered body is cut again after a replay
        }

        public void OnSpeech(string text, string key) { _talkUntil = Time.time + Mathf.Clamp((text?.Length ?? 10) * 0.07f, 0.8f, 5f); if (key == "scream") { Rig?.SetExpression(Expr.Fear); Rig?.Anim?.PlayGesture(Gesture.Surprised, 1.2f); } else SpeechGestures.Perform(this, text, Emotion.Neutral, key == "accuse" || key == "claim_accuse"); }
        public void Talk(float seconds) { _talkUntil = Time.time + seconds; }
        public void OnStrike(string anim) { if (Rig?.Anim == null) return; Enum.TryParse(anim ?? "", out ActionAnim aa); if (aa == ActionAnim.None) aa = ActionAnim.Overhead; Rig.Anim.PlayAction(aa, 0.6f); Physical?.Actions?.Swing(aa, transform.position + transform.forward * .8f + Vector3.up, false); }
        public void OnCollapse() { Rig?.Anim?.PlayAction(ActionAnim.Fall, 0.8f); Physical?.Collapse(); }
        public void OnDeath() { _lastStatus = (ActorStatus)(-1); SnapIfFar(); Physical?.Collapse(); }

        /// <summary>(Game/Gore) The kernel moved this person in one step (a staged scene, a body carried off in one tick) and
        /// the view has not followed yet: wound and death events are handled before the views tick, so without this the
        /// physical reaction (and the ragdoll the dead fall into, whose resting place is written back to the kernel) would
        /// start where the person no longer is. Views that merely lag behind a walk are never moved.</summary>
        void SnapIfFar()
        {
            var a = S.A(Id); if (a == null || a.IsPlayer || ReplayDriven || a.CarriedBy != null || a.StairId >= 0 || W == null) return;
            if ((Target(a) - transform.position).sqrMagnitude > 2.5f * 2.5f) Snap();
        }
        public void OnCarry(string target, bool start)
        {
            var t = W.ViewOf(target);
            if (start && t != null && BL23.Sim.Violence.Dragging(S, S.A(Id))) return;   // --- violence track: dragged, not shouldered (ViolencePresenter)
            Rig?.Anim?.SetCarrying(start && t != null ? t.Rig : null);
        }

        public void OnWound(GameEvent e)
        {
            var parts = (e.Data ?? "").Split('|'); if (parts.Length < 5) return;
            if (!Enum.TryParse(parts[0], out BL23.Game.Characters.BodyRegion r)) return;
            Enum.TryParse(parts[1], out DamageType dt); int.TryParse(parts[2], out int sev); bool pm = parts[3] == "1";
            Vector3 lp = ParseContactVector(parts[4]);
            Vector3 direction = Vector3.zero, normal = Vector3.zero; float contactImpulse = 0, contactEnergy = 0;
            if (parts.Length >= 8)
            {
                direction = ParseContactVector(parts[5]);
                float.TryParse(parts[6], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out contactImpulse);
                float.TryParse(parts[7], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out contactEnergy);
                if (parts.Length > 8) normal = ParseContactVector(parts[8]);
            }
            // (Game/Gore) never react where the kernel no longer has this person, nor push from where the striker no longer is
            SnapIfFar();
            var byView = !string.IsNullOrEmpty(e.Actor) ? W?.ViewOf(e.Actor) : null; if (byView != null && byView != this) byView.SnapIfFar();
            // (Game/Gore) sawing a dead body apart is not a blow: the part is severed, the body is not kicked across the floor
            bool severing = pm && sev >= 5;
            if (severing) { direction = Vector3.down; contactImpulse = 0.01f; contactEnergy = 0f; }
            Physical?.OnWound((BL23.Sim.BodyRegion)r, dt, sev, lp, e.Actor, pm, direction, contactImpulse, contactEnergy, normal, parts.Length > 9 && parts[9] == "1");
            // severed (severity 5): drawn by Game/Gore (torso cut, caps), never as a wound patch
            if (sev >= 5) return;
            // presentation strength follows the setting; the kernel keeps the same facts either way
            int shown = Settings.Gore == 0 ? Math.Min(sev, 1) : Settings.Gore == 1 ? Math.Min(sev, 3) : sev;
            shown = Math.Min(shown, 4);
            Rig?.AddWound(r, dt, shown, lp, pm);
            if (!pm) { Rig?.Anim?.PlayAction(ActionAnim.Hurt, 0.4f); Rig?.SetExpression(Expr.Pain); }
        }

        void SyncHeld(Actor a)
        {
            if (a.HandR != _heldR) { Detach(_heldR, false); _heldR = a.HandR; Attach(_heldR, Rig.HandAnchorR); }
            if (a.HandL != _heldL) { Detach(_heldL, true); _heldL = a.HandL; Attach(_heldL, Rig.HandAnchorL); }
        }

        static Vector3 ParseContactVector(string value)
        {
            var f = value.Split(','); if (f.Length != 3) return Vector3.zero;
            var ci = System.Globalization.CultureInfo.InvariantCulture; var style = System.Globalization.NumberStyles.Float;
            float.TryParse(f[0], style, ci, out float x); float.TryParse(f[1], style, ci, out float y); float.TryParse(f[2], style, ci, out float z);
            return new Vector3(x, y, z);
        }
        void Attach(string itemId, Transform anchor)
        {
            if (itemId == null || anchor == null) return;
            if (W.Items.TryGetValue(itemId, out var iv)) PhysicalItemTransfer.Attach(iv, Rig, anchor == Rig.HandAnchorL, Session.I.Player != null && a_IsPlayerFirstPerson());
        }
        bool a_IsPlayerFirstPerson() => Id == Cast.Player && Session.I.Player.FirstPerson;
        void Detach(string itemId, bool left) { if (itemId != null && W.Items.TryGetValue(itemId, out var iv)) PhysicalItemTransfer.Release(iv, Rig, left); }

        public static Posture MapPose(Pose p)
        {
            switch (p)
            {
                case Pose.Sit: return Posture.Sit; case Pose.Crouch: return Posture.Crouch; case Pose.Kneel: return Posture.Kneel;
                case Pose.LieBack: case Pose.Sleep: return Posture.LieBack; case Pose.LieFront: return Posture.LieFront; case Pose.LieSide: return Posture.LieSide; case Pose.Slumped: return Posture.Slumped;
            }
            return Posture.Stand;
        }

        /// <summary>The body lies the way the kernel says it fell (on the back, face down, curled on the side, slumped); which
        /// of the two versions of each follows the person and how they died (the same body in a replay lies the same way).</summary>
        public static int DeadVariant(string id, Pose p)
        {
            int h = 0; foreach (var ch in id ?? "") h = h * 31 + ch;
            var a = Session.I?.S?.A(id);
            if (a?.Body != null) { foreach (var ch in a.Body.DeathCause ?? "") h = h * 31 + ch; h ^= (int)(a.Body.DeathClock >= 0 ? a.Body.DeathClock : 0) * 7919; }
            bool alt = ((h >> 3) & 1) == 1;
            switch (p) { case Pose.LieFront: return alt ? 5 : 1; case Pose.LieSide: return 2; case Pose.Slumped: return 3; default: return alt ? 4 : 0; }
        }

        // ---- (Game/Gore) blood on a dead body
        float _deadAt = -99f; int _headSoakFor = -1;

        /// <summary>The body shader's spatter for a dead victim: light (it is spread evenly over the clothes and hair, and above
        /// ~0.3 its large blobs read as a camouflage print); the wounds' own masks carry the soak where the blood came from,
        /// HeadSoak the hair.</summary>
        static float DeadSpatter(Actor a) => Mathf.Min(0.24f, Mathf.Max(a.BloodOnClothes * 0.8f, GoreSoak.Of(a) * 0.3f));

        /// <summary>A head or neck wound that bled: once the body has settled into its dead pose, the blood soaks the hair
        /// and skin on the side the head lies on (one more blood mask on the rig's wound path; no wound patch). Added again
        /// after a replay rebuilds the wounds.</summary>
        void HeadSoak(Actor a)
        {
            var b = a.Body; if (b == null || Rig == null || Rig.Anim == null || !Rig.Anim.IsDead) return;
            int wc = b.Wounds.Count; if (_headSoakFor == wc || Time.time - _deadAt < 1.2f) return;
            _headSoakFor = wc;
            if (Settings.Gore <= 0) return;
            int best = 0; bool severed = false;
            foreach (var w in b.Wounds)
            {
                if (w.Region == BL23.Sim.BodyRegion.Neck && w.Sev >= 5) severed = true;
                if ((w.Region != BL23.Sim.BodyRegion.Head && w.Region != BL23.Sim.BodyRegion.Neck) || w.Sev < 2 || w.Sev >= 5) continue;
                if (w.Type == DamageType.Choke || w.Type == DamageType.Drown || w.Type == DamageType.Shock || w.Type == DamageType.Burn) continue;
                best = Math.Max(best, w.Sev);
            }
            if (severed || best == 0 || !Rig.HasBone(HBone.Head)) return;
            // the lowest point of the head as it lies: where the blood runs to and gathers
            var hc = Rig.HeadCenterWorld(); float s = Rig.Height > 0.5f ? Rig.Height / 1.75f : 1f;
            var low = hc + Vector3.down * 0.085f * s;
            Rig.AddWound(BL23.Game.Characters.BodyRegion.Head, DamageType.Fall, Mathf.Clamp(best + 1, 3, 5), Rig.transform.InverseTransformPoint(low), false);
        }

        public static void PlayAnim(ActorAnimator an, Anim a)
        {
            switch (a)
            {
                case Anim.Eat: an.PlayAction(ActionAnim.Eat, 999); break; case Anim.Drink: an.PlayAction(ActionAnim.Drink, 999); break;
                case Anim.Cook: an.PlayAction(ActionAnim.Cook, 999); break; case Anim.Read: an.PlayAction(ActionAnim.Read, 999); break;
                case Anim.Write: an.PlayAction(ActionAnim.Write, 999); break; case Anim.Clean: an.PlayAction(ActionAnim.Clean, 999); break;
                case Anim.Wash: an.PlayAction(ActionAnim.Wash, 3); break; case Anim.Knock: an.PlayAction(ActionAnim.Knock, 1.2f); break;
                case Anim.OpenDoor: an.PlayAction(ActionAnim.OpenDoor, 0.6f); break; case Anim.PickUp: an.PlayAction(ActionAnim.PickUp, 0.8f); break;
                case Anim.PutDown: an.PlayAction(ActionAnim.PutDown, 0.8f); break; case Anim.Use: case Anim.Craft: case Anim.Garden: an.PlayAction(ActionAnim.Use, 999); break;
                case Anim.Operate: an.PlayAction(ActionAnim.Operate, 999); break; case Anim.Play: an.PlayAction(ActionAnim.Play, 999); break;
                case Anim.Sleep: an.PlayAction(ActionAnim.Sleep, 999); break; case Anim.FirstAid: an.PlayAction(ActionAnim.FirstAid, 999); break;
                case Anim.Carry: an.PlayAction(ActionAnim.Carry, 999); break; case Anim.Struggle: an.PlayAction(ActionAnim.Struggle, 1.5f); break;
                case Anim.Talk: an.PlayGesture(Gesture.Talk, 3); break; case Anim.Listen: an.PlayGesture(Gesture.Listen, 3); break;
                case Anim.Think: an.PlayGesture(Gesture.Think, 4); break; case Anim.Point: an.PlayGesture(Gesture.Point, 1.5f); break;
                case Anim.Cry: an.PlayGesture(Gesture.Cry, 5); break; case Anim.Laugh: an.PlayGesture(Gesture.Laugh, 2); break;
                case Anim.Pray: an.PlayGesture(Gesture.Pray, 999); break; case Anim.Cower: an.PlayGesture(Gesture.Cower, 3); break;
                case Anim.Search: case Anim.Examine: an.PlayGesture(Gesture.LookAround, 3); break; case Anim.Photo: an.PlayGesture(Gesture.Present, 2); break;
                case Anim.Exercise: an.PlayAction(ActionAnim.Struggle, 2); break;
                case Anim.Angry: an.PlayGesture(Gesture.Angry, 2.5f); break; case Anim.CrossArms: an.PlayGesture(Gesture.CrossArms, 999); break;
                case Anim.Shrug: an.PlayGesture(Gesture.Shrug, 1.6f); break; case Anim.Wave: an.PlayGesture(Gesture.Wave, 1.4f); break; case Anim.Bow: an.PlayGesture(Gesture.Bow, 1.8f); break;
                case Anim.Surprised: an.PlayGesture(Gesture.Surprised, 1.4f); break; case Anim.Present: an.PlayGesture(Gesture.Present, 2f); break; case Anim.Slam: an.PlayGesture(Gesture.Slam, 1.2f); break;
                case Anim.Stab: an.PlayAction(ActionAnim.Stab, 0.6f); break; case Anim.Slash: an.PlayAction(ActionAnim.Slash, 0.6f); break;
                case Anim.Overhead: an.PlayAction(ActionAnim.Overhead, 0.7f); break; case Anim.Strangle: an.PlayAction(ActionAnim.Strangle, 999); break;
                case Anim.Drag: an.PlayAction(ActionAnim.Drag, 999); break;   // (violence track: a body too heavy to lift)
                default: an.PlayAction(ActionAnim.None, 0); an.PlayGesture(Gesture.None, 0); break;
            }
        }

        public static Expr MapEmo(Emotion e)
        {
            switch (e)
            {
                case Emotion.Smile: return Expr.Smile; case Emotion.Grin: return Expr.Grin; case Emotion.Angry: return Expr.Angry; case Emotion.Sad: return Expr.Sad;
                case Emotion.Surprised: return Expr.Surprised; case Emotion.Fear: return Expr.Fear; case Emotion.Smirk: return Expr.Smirk; case Emotion.Disgust: return Expr.Disgust;
                case Emotion.Blank: return Expr.Blank; case Emotion.Dead: return Expr.Dead; case Emotion.Pain: return Expr.Pain; case Emotion.Crying: return Expr.Crying;
                case Emotion.Laugh: return Expr.Laugh; case Emotion.Break: return Expr.Break;
            }
            return Expr.Neutral;
        }
    }
}
