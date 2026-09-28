using System;
using System.Linq;
using BL23.Game.Audio;
using BL23.Game.Characters;
using BL23.Game.Physicality;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BL23.Game
{
    /// <summary>
    /// 김민혁. First-person by default (V toggles an over-the-shoulder view that shows the 3D model).
    /// Walk/run/crouch, stairs by ramps, head bob, flashlight (F), interaction (E/R), inventory, attack (RMB aim + LMB).
    /// The kernel receives the pose every tick so NPCs perceive the player exactly like anyone else.
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        Session _s; GameState S => _s.S;
        public CharacterController CC; public Camera Cam; public Transform Pivot; public Light Flash; public PhysicsGrab Grab;
        public float Yaw, Pitch; public Vector3 Velocity; public bool Controlling = true; public bool FirstPerson = true; public bool Crouching;
        public Vector3 FeetPosition => transform.position;
        public Interaction Interact;
        float _bob, _stepAcc, _vy; bool _running; float _camDist = 2.4f; Vector3 _tpsSmooth; int _shoulder = 1;
        /// <summary>The over-the-shoulder camera is squeezed right up against 민혁 (walls): his body is hidden so it does not fill the frame.</summary>
        public bool CloseCam;
        public const float Walk = 3.0f, Run = 5.6f, CrouchSpeed = 1.6f;
        Vector3 _externalVelocity;
        public void ApplyExternalImpulse(Vector3 velocity) { _externalVelocity = Vector3.ClampMagnitude(_externalVelocity + velocity, 8); }

        public static PlayerController Create(Session s)
        {
            var go = new GameObject("Player (김민혁)"); DontDestroyOnLoad(go);
            var p = go.AddComponent<PlayerController>(); p._s = s;
            p.CC = go.AddComponent<CharacterController>(); p.CC.height = 1.72f; p.CC.radius = 0.28f; p.CC.center = new Vector3(0, 0.86f, 0); p.CC.stepOffset = 0.42f; p.CC.slopeLimit = 55f; p.CC.skinWidth = 0.03f;
            p.Pivot = new GameObject("Pivot").transform; p.Pivot.SetParent(go.transform, false); p.Pivot.localPosition = new Vector3(0, 1.62f, 0);
            var camGo = new GameObject("PlayerCamera", typeof(Camera)); camGo.transform.SetParent(p.Pivot, false); AudioEars.Ensure();   // the listener lives on AudioEars and follows the camera on screen
            p.Cam = camGo.GetComponent<Camera>(); p.Cam.nearClipPlane = 0.05f; p.Cam.farClipPlane = 220f; p.Cam.fieldOfView = Settings.Fov; camGo.tag = "MainCamera";
            var data = camGo.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            BL23.Game.Mansion.MansionAtmosphere.SetupCamera(camGo.GetComponent<Camera>());
            p.Cam.nearClipPlane = 0.16f;   // first person from the body's own eyes: never draw the inside of one's own collar
            p.Flash = new GameObject("Flashlight").AddComponent<Light>(); p.Flash.transform.SetParent(camGo.transform, false); p.Flash.transform.localPosition = new Vector3(0.2f, -0.15f, 0.2f);
            p.Flash.type = LightType.Spot; p.Flash.spotAngle = 48; p.Flash.innerSpotAngle = 22; p.Flash.range = 16; p.Flash.intensity = 6f; p.Flash.color = new Color(1f, 0.93f, 0.8f); p.Flash.shadows = LightShadows.Soft; p.Flash.enabled = false;
            p.Interact = go.AddComponent<Interaction>(); p.Interact.Init(s, p);
            p.Grab = go.AddComponent<PhysicsGrab>(); p.Grab.Init(s, p); PhysicsBridge.Hook();
            p.FirstPerson = true;   // the game is played in first person only (the body is shown from its own eyes)
            p.Respawn();
            return p;
        }

        public void Respawn()
        {
            var a = S.Player; if (a == null) return;
            if (Seated) Unseat(); _walkT = -1f;   // --- time-on-demand: a seat the kernel still gives is taken again next frame
            CC.enabled = false; transform.position = _s.World.ToWorld(a.Pos) + Vector3.up * 0.05f; Yaw = a.Yaw; Pitch = 0; CC.enabled = true; _vy = 0;
            // don't start a scene with the nose against a wall: face the most open direction if the stored yaw is blocked
            var eye = transform.position + Vector3.up * 1.55f;
            if (Physics.Raycast(eye, Quaternion.Euler(0, Yaw, 0) * Vector3.forward, 1.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                float best = -1, bestYaw = Yaw;
                for (int k = 0; k < 16; k++)
                {
                    float y = Yaw + k * 22.5f; var dir = Quaternion.Euler(0, y, 0) * Vector3.forward;
                    float d = Physics.Raycast(eye, dir, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 20f;
                    if (d > best) { best = d; bestYaw = y; }
                }
                Yaw = bestYaw;
            }
        }

        /// <summary>Scripted placement (probe / elevator / after trial). The kernel pose is updated too.</summary>
        public void Teleport(P3 p, float yaw, float pitch = 0)
        {
            // --- time-on-demand (begin): placed onto the seat the kernel gave (time together) → sit there; anywhere else → stand up first
            _walkT = -1f;
            if (TimeLink.PlayerOnSpot(out var onSpot) && onSpot.Pos.f == p.f && onSpot.Pos.DistXZ(p) < 0.05f && S.Player.Pose == BL23.Sim.Pose.Sit)
            { Unseat(); EnterSeat(onSpot, true); Yaw = yaw; Pitch = pitch; _vy = 0; _tpsSmooth = Vector3.zero; return; }
            if (Seated || TimeLink.KernelSeated) { TimeLink.Stand(); Unseat(); }
            // --- time-on-demand (end)
            _s.Sim.SetPlayerPose(p, yaw, false, false);
            CC.enabled = false; transform.position = _s.World.ToWorld(p) + Vector3.up * 0.05f; Yaw = yaw; Pitch = pitch; CC.enabled = true; _vy = 0; _tpsSmooth = Vector3.zero;
            _airT = 0f; var mvw = _s.World?.Mansion; if (mvw != null && mvw.RoomAtWorld(transform.position + Vector3.up * 0.5f) >= 0) { _safePos = transform.position; _hasSafe = true; }
        }

        public void SetControl(bool on)
        {
            Controlling = on; Cam.enabled = on;
            if (on) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            else { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }   // hand-over (심판, replay): never leave the pointer locked (CursorGuard keeps it right)
        }

        public void PushPose()
        {
            var a = S.Player; if (a == null || !a.Alive || !Controlling) return;
            // --- time-on-demand (begin): seated, the kernel keeps the seat itself (not the glide in between)
            if (Seated && _seat != null) { _s.Sim.SetPlayerPose(_seatExiting ? a.Pos : _seat.Pos, Yaw, false, false); return; }
            // --- time-on-demand (end)
            var p = transform.position; int f = FloorOf(p.y);
            _s.Sim.SetPlayerPose(new P3(f, p.x, p.z), Yaw, _running && Velocity.magnitude > 3.5f, Crouching);
        }

        int FloorOf(float y)
        {
            int best = 0; float bd = 999;
            foreach (var fi in S.Layout.Floors) { if (y < fi.BaseY - 0.6f) continue; float d = y - fi.BaseY; if (d < bd) { bd = d; best = fi.F; } }
            return best;
        }

        bool UiBlocking => _s.Paused || _s.Dialogue.Active || _s.Note.Open || _s.Menu.Open || (_s.Trial != null && _s.Trial.Active) || (_s.Reveal != null && _s.Reveal.Active) || (_s.Cine != null && _s.Cine.Busy);

        void Update()
        {
            if (Scripted && S.Player != null) { ScriptCamera(Time.deltaTime); return; }
            if (!Controlling || S.Player == null) return;
            bool alive = S.Player.Alive && S.Player.Status == ActorStatus.Active;
            // --- time-on-demand (begin): follow the kernel's seat; while time is being passed only the mouse looks around
            TimeLink.Poll(); FollowKernelSeat(); SeatTick(Time.deltaTime); WalkTick(Time.deltaTime);
            if (TimeLink.LocksMovement)
            {
                if (alive && !UiBlocking) Look();   // (the cursor itself: CursorGuard)
                if (!Seated && _walkT < 0f) { Velocity = Vector3.Lerp(Velocity, Vector3.zero, 1f - Mathf.Exp(-12f * Time.deltaTime)); }
                UpdateCamera(Time.deltaTime);
                return;
            }
            // --- time-on-demand (end)
            bool block = UiBlocking;
            // (cursor lock and visibility: CursorGuard decides every frame, after everything else)
            if (!block && alive) Look();
            Move(block || !alive, Time.deltaTime);
            if (!block && alive)
            {
                if (Input.GetKeyDown(KeyCode.F)) ToggleFlash();
                if (Input.GetKeyDown(KeyCode.C) && Settings.CrouchToggle) Crouching = !Crouching;
                if (!Settings.CrouchToggle) Crouching = Input.GetKey(KeyCode.LeftControl);
                if (Seated) Crouching = false;   // --- time-on-demand: seated, C does nothing
                Interact.Tick();
            }
            UpdateCamera(Time.deltaTime);
            Flash.enabled = Flash.enabled && HasFlashlight();
        }

        void Look()
        {
            float sens = Settings.Sensitivity;
            Yaw += Input.GetAxisRaw("Mouse X") * sens;
            Pitch += Input.GetAxisRaw("Mouse Y") * sens * (Settings.InvertY ? 1 : -1);
            Pitch = Mathf.Clamp(Pitch, -80, 80);
            // --- time-on-demand (begin): seated, the head turns within reason; in a shared scene it stays with the other person
            if (Seated && _seat != null && !_seatExiting) { float d = Mathf.DeltaAngle(_seat.Yaw, Yaw); if (d > 120f) Yaw = _seat.Yaw + 120f; else if (d < -120f) Yaw = _seat.Yaw - 120f; }
            SceneLookTick(Time.deltaTime); GlanceTick(Time.deltaTime);
            // --- time-on-demand (end)
        }

        void Move(bool frozen, float dt)
        {
            var physical = _s.World?.ViewOf(Cast.Player)?.Physical;
            if (!CC.enabled || (physical?.Ragdoll != null && physical.Ragdoll.Active)) { Velocity = Vector3.zero; return; }
            if (physical != null && physical.BlocksActions) frozen = true;
            // --- time-on-demand (begin): seated (any movement key gets up) or being walked by a scene
            if (Seated || _walkT >= 0f) { SeatedMove(frozen); return; }
            // --- time-on-demand (end)
            var input = frozen ? Vector2.zero : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            bool probeRun = Time.time < ProbeRunUntil; if (probeRun) input = new Vector2(0, 1);
            if (input.sqrMagnitude > 1) input.Normalize();
            var b = S.Player.Body; float mob = Mathf.Max(0.05f, Mathf.Min(b.Mobility, physical?.State?.Mobility ?? 1f));
            _running = !frozen && (Input.GetKey(KeyCode.LeftShift) || probeRun) && !Crouching && b.Mobility > 0.5f;
            float speed = (Crouching ? CrouchSpeed : _running ? Run : Walk) * mob * (S.Player.Carrying != null ? (BL23.Sim.Violence.Dragging(S, S.Player) ? 0.3f : 0.55f) : 1f)
                          * (BL23.Sim.Violence.DoingTo(S, Cast.Player) != null ? 0.25f : 1f);   // (violence track: a body too heavy to lift is dragged; a held victim drags you down)
            var fwd = Quaternion.Euler(0, Yaw, 0);
            var wish = fwd * new Vector3(input.x, 0, input.y) * speed;
            Velocity = Vector3.Lerp(Velocity, wish, 1f - Mathf.Exp(-12f * dt));
            if (CC.isGrounded) _vy = -1f; else _vy -= 18f * dt;
            if (!UiBlocking) { CC.Move((Velocity + _externalVelocity + Vector3.up * _vy) * dt); _externalVelocity *= Mathf.Exp(-5f * dt); }
            // crouch height
            float targetH = Crouching ? 1.15f : 1.72f; CC.height = Mathf.Lerp(CC.height, targetH, 10 * dt); CC.center = new Vector3(0, CC.height / 2, 0);
            Pivot.localPosition = new Vector3(0, CC.height - 0.1f, 0);
            // footsteps
            float sp = new Vector3(Velocity.x, 0, Velocity.z).magnitude;
            if (sp > 0.5f && CC.isGrounded)
            {
                _bob += dt * sp * 2.2f;
                _stepAcc += dt * sp * 0.55f; if (_stepAcc > 1f) { _stepAcc = 0; Sfx.Footstep(Surface(), transform.position, _running ? 0.6f : 0.35f); }
            }
            if (transform.position.y < -60) Respawn();
            // never lost in the void: remember the last place with floor underfoot inside a room; a long fall returns there
            if (CC.isGrounded) { _airT = 0f; var mv = _s.World?.Mansion; if (mv != null && mv.RoomAtWorld(transform.position + Vector3.up * 0.5f) >= 0) { _safePos = transform.position; _hasSafe = true; } }
            else if ((_airT += dt) > 1.4f && _hasSafe) { CC.enabled = false; transform.position = _safePos + Vector3.up * 0.05f; CC.enabled = true; _vy = 0; _airT = 0f; Velocity = Vector3.zero; }
        }
        Vector3 _safePos; bool _hasSafe; float _airT;

        string Surface()
        {
            var r = S.Layout.Room(S.Player.Room); if (r == null) return "Marble";
            switch (r.Type) { case RoomType.Bedroom: case RoomType.Library: case RoomType.Lounge: case RoomType.Chapel: case RoomType.Study: case RoomType.Theater: return "Carpet"; case RoomType.MirrorWater: case RoomType.Pool: case RoomType.RainCorridor: return "Water"; case RoomType.Gallery: case RoomType.Workshop: return "Wood"; case RoomType.WineCellar: case RoomType.BoilerRoom: case RoomType.Storage: case RoomType.MachineRoom: case RoomType.PowerRoom: case RoomType.Courtyard: return "Stone"; }
            return "Marble";
        }

        void UpdateCamera(float dt)
        {
            if (!Cam.enabled) return;
            float bob = Settings.HeadBob && FirstPerson ? Mathf.Sin(_bob * 2f) * 0.035f * Mathf.Clamp01(Velocity.magnitude / 3f) : 0;
            Pivot.rotation = Quaternion.Euler(Pitch, Yaw, 0);
            if (FirstPerson)
            {
                // look out of the body's own eyes: sitting, crouching, bending over or lying down moves the view with the body
                CloseCam = false;
                Cam.transform.position = BodyEye(dt) + NudgeOffset(dt); Cam.transform.rotation = Quaternion.Euler(Pitch, Yaw, 0);   // --- time-on-demand: + a small lean toward what the hands do
            }
            else
            {
                // over-the-shoulder, pulled in by walls; if one shoulder is blocked, try the other; if still squeezed, hide the body
                Vector3 Cast3(float side, out float dist)
                {
                    var want = new Vector3(side, 0.15f, -_camDist); var w = Pivot.TransformPoint(want); dist = want.magnitude;
                    if (Physics.SphereCast(Pivot.position, 0.2f, (w - Pivot.position).normalized, out var hit, want.magnitude, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<PlayerController>() == null && hit.collider.GetComponentInParent<BL23.Game.Characters.ActorRig>() == null) { w = hit.point + hit.normal * 0.2f; dist = hit.distance; }
                    return w;
                }
                if (_shoulder == 0) _shoulder = 1;
                var world = Cast3(0.45f * _shoulder, out float dMain);
                if (dMain < 1.1f) { var alt = Cast3(-0.45f * _shoulder, out float dAlt); if (dAlt > dMain + 0.5f) { _shoulder = -_shoulder; world = alt; dMain = dAlt; } }
                CloseCam = dMain < 0.85f;
                _tpsSmooth = Vector3.Lerp(_tpsSmooth == Vector3.zero ? world : _tpsSmooth, world, 1f - Mathf.Exp(-20 * dt));
                Cam.transform.position = _tpsSmooth; Cam.transform.rotation = Pivot.rotation;
            }
            Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, Settings.Fov + (_running ? 6 : 0), 6 * dt);
        }

        // ---- first-person body
        Vector3 _eyeSmooth, _eyeVel; float _moveBlend;
        /// <summary>Probe only: run forward for a moment (no keyboard in automated checks).</summary>
        public float ProbeRunUntil;
        Vector3 BodyEye(float dt)
        {
            var rig = _s.World?.ViewOf(Cast.Player)?.Rig;
            Vector3 eye = rig != null ? rig.EyeWorld() : Pivot.position;
            if (rig == null || (eye - Pivot.position).sqrMagnitude > 4f) eye = Pivot.position;   // no body yet / body not where we are
            if (_eyeSmooth == Vector3.zero || (eye - _eyeSmooth).sqrMagnitude > 1f) { _eyeSmooth = eye; _eyeVel = Vector3.zero; }
            // on the move the run cycle leans and bobs the head a lot: the lens would dip into the collar and the shoulders would
            // swing through the frame. While walking or running the view rides the steady body centre (the capsule) at eye height,
            // a little further ahead of the face; standing, sitting, crouching and scripted moments still follow the head itself.
            float mv = Scripted ? 0f : Mathf.Clamp01((new Vector3(Velocity.x, 0, Velocity.z).magnitude - 0.3f) / 2.2f);
            _moveBlend = Mathf.MoveTowards(_moveBlend, mv, dt * 4f);
            if (_moveBlend > 0.001f) { var steady = new Vector3(Pivot.position.x, Pivot.position.y - 0.04f, Pivot.position.z); eye = Vector3.Lerp(eye, steady, _moveBlend); }
            // --- time-on-demand (begin): seated, the eye is the seat's (its height plus the upper body) and goes down with the seat glide
            // (done within its 0.45 s) — never waiting for the body's sit animation to catch up (that left the view at standing height)
            bool lowering = Seated && _seat != null && !_seatExiting;
            if (lowering)
            {
                var sw = _s.World.ToWorld(_seat.Pos);
                eye = Vector3.Lerp(eye, new Vector3(sw.x, sw.y + SeatedEyeHeight(_seat), sw.z), SeatLowering());
            }
            _eyeSmooth = Vector3.SmoothDamp(_eyeSmooth, eye, ref _eyeVel, lowering ? 0.05f : Time.time < _eyeSlowUntil ? 0.16f : 0.07f, 30f, Mathf.Max(1e-4f, dt));   // sitting down follows the seat; getting up glides the view
            // --- time-on-demand (end)
            // the lens sits just in front of the face so the collar never fills the frame when looking down
            return _eyeSmooth + Quaternion.Euler(0, Yaw, 0) * Vector3.forward * (0.1f + 0.14f * _moveBlend);
        }

        // ---- scripted first-person moments (sitting down to read, pulling out a chair...): no input; the body is moved by the
        // kernel pose and the view turns smoothly toward what the hands are doing
        public bool Scripted; public float ScriptYaw, ScriptPitch;
        public void BeginScript() { Scripted = true; Controlling = false; ScriptYaw = Yaw; ScriptPitch = Pitch; Cam.enabled = true; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        public void EndScript(P3 at, float yaw) { Scripted = false; Teleport(at, yaw, Mathf.Clamp(Pitch, -20f, 20f)); Controlling = true; Cam.enabled = true; }
        void ScriptCamera(float dt)
        {
            Yaw = Mathf.LerpAngle(Yaw, ScriptYaw, 1f - Mathf.Exp(-4f * dt)); Pitch = Mathf.Lerp(Pitch, ScriptPitch, 1f - Mathf.Exp(-3.5f * dt));
            Cam.transform.position = BodyEye(dt); Cam.transform.rotation = Quaternion.Euler(Pitch, Yaw, 0);
            Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, Settings.Fov, 4 * dt);
        }

        // (perf) checked every frame while the light is on: hands and pocket directly (Carried() + LINQ allocated each time)
        public bool HasFlashlight()
        {
            var a = S.Player; if (a == null) return false;
            if (IsLight(a.HandR) || IsLight(a.HandL)) return true;
            foreach (var id in a.Pocket) if (IsLight(id)) return true;
            return false;
        }
        bool IsLight(string itemId) { var it = S.I(itemId); return it != null && it.Def?.Light == true; }

        void ToggleFlash()
        {
            if (!HasFlashlight()) { Hud.I?.Toast("손전등이 없다", Pal.TextDim, 1.2f); return; }
            Flash.enabled = !Flash.enabled; Sfx.Play("light_switch", null, 0.4f);
            var fl = _s.Sim.Carried(S.Player).First(i => i.Def?.Light == true);
            S.Flags["light:" + fl.Id] = Flash.enabled ? 1 : 0; // others can see the light (kernel perception uses it)
        }

        // --- time-on-demand (begin) ------------------------------------------------------------------------------------------
        // Seated mode: E on a seat sits at once (the view glides down, the chair is pulled out by ActorView's own sitting
        // motion); any movement key gets up (the view glides back to the seat's approach point). The kernel's seat is the
        // truth: if it sits the player down (time spent together, a loaded save) the view follows, and if it stands the
        // player up (the end of a skip, the morning call) so does the view.
        public bool Seated { get; private set; }
        Spot _seat; Vector3 _seatFrom, _seatVia, _seatTo; float _seatT = 1f, _seatDur = 0.45f, _seatWalk; bool _seatExiting; float _eyeSlowUntil = -1f; float _seatedAt;

        /// <summary>Take the seat the kernel gave (after Sim.PlayerSit). snap: no glide (a load, a teleport onto the seat).
        /// From a step or two away the body first walks to the seat's side, then sits.</summary>
        public void EnterSeat(Spot sp, bool snap = false)
        {
            if (sp == null || S.Player == null) return;
            _walkT = -1f;
            _seat = sp; Seated = true; _seatExiting = false; Crouching = false; _running = false; Velocity = Vector3.zero; _vy = 0f; _seatedAt = Time.time;
            CC.enabled = false;
            _seatFrom = transform.position; _seatTo = _s.World.ToWorld(sp.Pos); _seatVia = _seatFrom; _seatWalk = 0f; _seatDur = 0.45f;
            var via = _s.World.ToWorld(sp.Approach); var flat = via - _seatFrom; flat.y = 0f;
            if (!snap && sp.Approach.f == sp.Pos.f && flat.magnitude > 0.6f)
            {
                float wd = Mathf.Clamp(flat.magnitude / Walk, 0.2f, 1.2f);
                _seatVia = via; _seatDur = wd + 0.45f; _seatWalk = wd / _seatDur;
            }
            _seatT = snap ? 1f : 0f;
            if (snap) { transform.position = _seatTo; Yaw = sp.Yaw; }
            _eyeSlowUntil = Time.time + (snap ? 0f : _seatDur + 0.45f);
        }

        /// <summary>0 → 1 while the body lowers onto the seat (after the last steps to its side); 1 once seated.</summary>
        float SeatLowering()
        {
            if (_seatT >= 1f) return 1f;
            float s = _seatWalk > 0f ? Mathf.Clamp01((_seatT - _seatWalk) / Mathf.Max(0.01f, 1f - _seatWalk)) : _seatT;
            return s * s * (3f - 2f * s);
        }

        /// <summary>Eye height above the floor when seated on this spot's furniture (seat height + about 0.7 m of upper body).</summary>
        float SeatedEyeHeight(Spot sp)
        {
            var f = sp != null && sp.Furniture >= 0 && sp.Furniture < S.Layout.Furniture.Count ? S.Layout.Furniture[sp.Furniture] : null;
            switch (f?.Type)
            {
                case "BarStool": return 1.45f;
                case "Lounger": case "DayBed": return 0.98f;
                case "InfirmaryBed": case "Bed": return 1.22f;
                case "Sofa": case "Armchair": case "Ottoman": return 1.1f;
                default: return 1.15f;   // chairs, benches, pews, audience seats
            }
        }

        /// <summary>Get up: the view glides back to where the kernel stood the player (the seat's approach point).</summary>
        public void ExitSeat(bool snap = false)
        {
            if (!Seated) return;
            _seatExiting = true; _seatFrom = transform.position; _seatTo = _s.World.ToWorld(S.Player.Pos); _seatVia = _seatFrom; _seatWalk = 0f;
            var d = _seatTo - _seatFrom; d.y = 0f; if (d.magnitude > 2.5f) snap = true;   // moved far by the kernel: no slide through walls
            _seatDur = 0.4f; _seatT = snap ? 1f : 0f; _eyeSlowUntil = Time.time + (snap ? 0f : 0.8f);
            if (snap) FinishExit();
        }

        /// <summary>Stand up from the seat (kernel and view).</summary>
        public void StandUp() { if (!Seated && !TimeLink.KernelSeated) return; TimeLink.Stand(); if (Seated) ExitSeat(); }

        /// <summary>Drop the seated view at once (the caller moves the player).</summary>
        void Unseat() { if (!Seated) return; Seated = false; _seat = null; _seatExiting = false; _seatT = 1f; if (!Scripted) CC.enabled = true; }

        void FinishExit()
        {
            transform.position = _seatTo; Seated = false; _seat = null; _seatExiting = false; _seatT = 1f;
            CC.enabled = true; _vy = 0f; _airT = 0f;
        }

        void SeatTick(float dt)
        {
            if (!Seated || _seatT >= 1f) return;
            _seatT = Mathf.Min(1f, _seatT + dt / Mathf.Max(0.05f, _seatDur));
            var prev = transform.position;
            if (_seatT < _seatWalk)
            {
                // the last steps to the seat's side (the body walks)
                float w = _seatT / _seatWalk; transform.position = Vector3.Lerp(_seatFrom, _seatVia, w);
                Velocity = (transform.position - prev) / Mathf.Max(dt, 1e-4f);
                var d = _seatVia - _seatFrom; d.y = 0f; if (d.sqrMagnitude > 0.01f) Yaw = Mathf.LerpAngle(Yaw, Quaternion.LookRotation(d).eulerAngles.y, 1f - Mathf.Exp(-8f * dt));
                return;
            }
            Velocity = Vector3.zero;
            float s = _seatWalk > 0f ? (_seatT - _seatWalk) / (1f - _seatWalk) : _seatT;
            float k = s * s * (3f - 2f * s);
            transform.position = Vector3.Lerp(_seatVia, _seatTo, k);
            if (!_seatExiting && _seat != null) Yaw = Mathf.LerpAngle(Yaw, _seat.Yaw, 1f - Mathf.Exp(-9f * dt));
            if (_seatT >= 1f && _seatExiting) FinishExit();
        }

        void FollowKernelSeat()
        {
            var me = S.Player; if (me == null || !me.Alive) return;
            bool onSpot = me.Spot >= 0 && me.Spot < S.Layout.Spots.Count && me.CarriedBy == null;
            if (!Seated && onSpot && me.Pose == BL23.Sim.Pose.Sit && _walkT < 0f) EnterSeat(S.Layout.Spots[me.Spot]);
            else if (Seated && !_seatExiting && !(onSpot && (me.Pose == BL23.Sim.Pose.Sit || me.Pose == BL23.Sim.Pose.Sleep))) ExitSeat();
            else if (Seated && !_seatExiting && _seat != null && onSpot && me.Spot != _seat.Id) EnterSeat(S.Layout.Spots[me.Spot]);
        }

        void SeatedMove(bool frozen)
        {
            Velocity = _walkT >= 0f ? Velocity : Vector3.zero;
            if (frozen || !Seated || _seatExiting || Time.time - _seatedAt < 0.3f) return;
            var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 0.04f) StandUp();
        }

        // ---- a scene walks the player a short way (to the place the kernel gave for time spent together)
        Vector3 _walkFrom, _walkTo; float _walkT = -1f, _walkDur;
        public bool Walking => _walkT >= 0f;
        public void ScriptedWalkTo(Vector3 world, float maxSecs)
        {
            if (Seated) { Unseat(); }
            _walkFrom = transform.position; _walkTo = world;
            var flat = world - _walkFrom; flat.y = 0f;
            _walkDur = Mathf.Clamp(flat.magnitude / Walk, 0.05f, Mathf.Max(0.05f, maxSecs)); _walkT = 0f;
            CC.enabled = false;
        }
        void WalkTick(float dt)
        {
            if (_walkT < 0f) return;
            _walkT = Mathf.Min(1f, _walkT + dt / _walkDur); float k = _walkT * _walkT * (3f - 2f * _walkT);
            var prev = transform.position; transform.position = Vector3.Lerp(_walkFrom, _walkTo, k);
            Velocity = (transform.position - prev) / Mathf.Max(dt, 1e-4f);
            var d = _walkTo - _walkFrom; d.y = 0f;
            if (d.sqrMagnitude > 0.01f && _sceneLook == null) Yaw = Mathf.LerpAngle(Yaw, Quaternion.LookRotation(d).eulerAngles.y, 1f - Mathf.Exp(-8f * dt));
            if (_walkT >= 1f) { _walkT = -1f; Velocity = Vector3.zero; if (!Seated && !Scripted) CC.enabled = true; }
        }

        // ---- a soft look-at: the view stays with someone (±40° of free look), drifting back when the mouse rests
        Transform _sceneLook;
        public void SceneLook(Transform t) { _sceneLook = t; }
        void SceneLookTick(float dt)
        {
            if (_sceneLook == null || Cam == null) return;
            var d = _sceneLook.position - Cam.transform.position; if (d.sqrMagnitude < 0.01f) return;
            var e = Quaternion.LookRotation(d).eulerAngles; float ty = e.y, tp = e.x > 180f ? e.x - 360f : e.x;
            Yaw = ty + Mathf.Clamp(Mathf.DeltaAngle(ty, Yaw), -40f, 40f);
            Pitch = tp + Mathf.Clamp(Pitch - tp, -30f, 30f);
            if (Mathf.Abs(Input.GetAxisRaw("Mouse X")) + Mathf.Abs(Input.GetAxisRaw("Mouse Y")) < 0.01f)
            {
                float k = 1f - Mathf.Exp(-1.4f * dt);
                Yaw = Mathf.LerpAngle(Yaw, ty, k); Pitch = Mathf.Lerp(Pitch, tp, k);
            }
        }

        // ---- a short glance: the view turns to something just found (a cup in the drawer that was opened), then is free again
        Vector3 _glanceAt; float _glanceT = -1f, _glanceDur = 0.35f;
        public void GlanceAt(Vector3 world, float secs = 0.35f) { _glanceAt = world; _glanceDur = Mathf.Max(0.1f, secs); _glanceT = 0f; }
        void GlanceTick(float dt)
        {
            if (_glanceT < 0f || Cam == null) return;
            _glanceT += dt / _glanceDur;
            var d = _glanceAt - Cam.transform.position; if (d.sqrMagnitude < 0.01f || _glanceT >= 1f) { _glanceT = -1f; return; }
            var e = Quaternion.LookRotation(d).eulerAngles; float ty = e.y, tp = e.x > 180f ? e.x - 360f : e.x;
            float k = 1f - Mathf.Exp(-12f * dt);
            Yaw = Mathf.LerpAngle(Yaw, ty, k); Pitch = Mathf.Lerp(Pitch, Mathf.Clamp(tp, -80f, 80f), k);
        }
        /// <summary>Probe: the eye's height above the feet (seated on an armchair it must drop to about 1.1 m).</summary>
        public float EyeHeight => Cam != null ? Cam.transform.position.y - transform.position.y : 0f;

        // ---- a small lean of the head toward what the hands are doing (a drawer, a clock face)
        Vector3 _nudge; float _nudgeT = -1f, _nudgeDur = 0.3f;
        public void Nudge(Vector3 worldOffset, float secs) { _nudge = worldOffset; _nudgeDur = Mathf.Max(0.05f, secs); _nudgeT = 0f; }
        Vector3 NudgeOffset(float dt)
        {
            if (_nudgeT < 0f) return Vector3.zero;
            _nudgeT += dt / _nudgeDur; if (_nudgeT >= 1f) { _nudgeT = -1f; return Vector3.zero; }
            return _nudge * Mathf.Sin(_nudgeT * Mathf.PI);
        }

        // ---- still people step aside: pressing into someone for 0.3 s while the world stands still
        string _pushId; float _pushStart, _pushLast, _yieldCd;
        void PushInto(ControllerColliderHit hit)
        {
            var av = hit.collider != null ? hit.collider.GetComponentInParent<ActorView>() : null; if (av == null || av.Id == Cast.Player) return;
            var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude < 0.04f || !TimeLink.WorldStill) { _pushId = null; return; }
            if (_pushId != av.Id || Time.time - _pushLast > 0.25f) { _pushId = av.Id; _pushStart = Time.time; }
            _pushLast = Time.time;
            if (Time.time - _pushStart < 0.3f || Time.time < _yieldCd) return;
            _yieldCd = Time.time + 1.5f; _pushId = null;
            var npc = S.A(av.Id); if (npc == null || !npc.Alive || npc.Status != ActorStatus.Active || npc.IsPlayer) return;
            if (TimeLink.YieldTo(npc, S.Player.Pos)) av.Rig?.Anim?.PlayGesture(Gesture.Nod, 0.8f);
        }
        // --- time-on-demand (end) --------------------------------------------------------------------------------------------

        /// <summary>Walking into light loose things shoves them (chairs scrape, bottles roll).</summary>
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var person = hit.collider.GetComponentInParent<PhysicalCharacter>();
            if (person != null) { person.Bump(Velocity, hit.point, 1); return; }
            var rb = hit.collider.attachedRigidbody;
            if (rb != null && !rb.isKinematic && hit.moveDirection.y > -.3f)
            {
                var prop = PhysicalProp.Ensure(rb.gameObject);
                if (prop != null) { prop.ApplyPush(hit.point, Vector3.ProjectOnPlane(Velocity, Vector3.up)); PhysicsGrab.LastTouchedByPlayer[rb] = Time.time; return; }
            }
            if (rb == null || rb.isKinematic || rb.mass > 40f || hit.moveDirection.y < -0.3f) return;
            var push = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);
            float speed = CC.velocity.magnitude;
            if (speed < 0.2f) return;
            rb.AddForceAtPosition(push * Mathf.Clamp(speed, 0.5f, 4f) * Mathf.Lerp(6f, 30f, Mathf.InverseLerp(0.5f, 40f, rb.mass)), hit.point, ForceMode.Force);
            PhysicsGrab.LastTouchedByPlayer[rb] = Time.time;
        }
    }
}
