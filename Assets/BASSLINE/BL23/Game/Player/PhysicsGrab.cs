using System.Collections.Generic;
using BL23.Game.Characters;
using BL23.Game.Mansion;
using BL23.Game.Physicality;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// Hands-on physics: hold Z to pick up and carry any light loose object or light furniture (it swings on a spring
    /// in front of you and collides with the world), mouse wheel to hold it nearer/farther, left click to throw,
    /// release Z to let go. Walking into light things pushes them. Impacts go back into the simulation
    /// (breakage, fragments, noise, someone hit by what you threw).
    /// </summary>
    public sealed class PhysicsGrab : MonoBehaviour
    {
        Session _s; PlayerController _p;
        Rigidbody _held; float _dist = 1.5f;
        readonly PhysicalGrab _grip = new PhysicalGrab();
        readonly RaycastHit[] _obstacles = new RaycastHit[24];
        Vector3 _previousTarget; float _pruneAt;
        public static readonly Dictionary<Rigidbody, float> LastTouchedByPlayer = new Dictionary<Rigidbody, float>();
        public bool Holding => _held != null;
        public string HeldLabel;
        public Rigidbody HeldBody => _held;

        public void Init(Session s, PlayerController p) { _s = s; _p = p; }

        public static bool RecentlyPlayer(Rigidbody rb) => rb != null && LastTouchedByPlayer.TryGetValue(rb, out var t) && Time.time - t < 3.5f;

        void Update()
        {
            if (_s == null || _p == null || _p.Cam == null) return;
            if (InputBlocked) { Release(Vector3.zero); return; }
            if (Time.time >= _pruneAt) { PruneTouches(); _pruneAt = Time.time + 5f; }
            if (Input.GetKeyDown(KeyCode.Z)) TryGrab();
            if (_held != null)
            {
                _dist = Mathf.Clamp(_dist + Input.mouseScrollDelta.y * 0.15f, 0.9f, 2.4f);
                if (Input.GetKeyUp(KeyCode.Z)) Release(Vector3.zero);
                else if (Input.GetMouseButtonDown(0))
                {
                    // Finite work/impulse budget: a heavy chair cannot be thrown as fast as a bottle.
                    float speed = Mathf.Min(10f, Mathf.Sqrt(2f * 90f / Mathf.Max(.05f, _held.mass)));
                    Release((_p.Cam.transform.forward * speed + Vector3.up * .6f) * _held.mass);
                    Audio.Sfx.Play("blade_whoosh", _p.Cam.transform.position, 0.35f);
                }
            }
        }

        void FixedUpdate()
        {
            if (_held == null) { _grip.Release(Vector3.zero); HeldLabel = null; return; }
            if (InputBlocked || !_held.gameObject.activeInHierarchy) { Release(Vector3.zero); return; }
            var cam = _p.Cam.transform;
            var target = cam.position + cam.forward * _dist - Vector3.up * 0.15f;
            var direction = target - cam.position;
            float distance = direction.magnitude;
            int count = Physics.SphereCastNonAlloc(cam.position, .08f, direction.normalized, _obstacles,
                distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = _obstacles[i];
                if (hit.rigidbody == _held || hit.collider == _p.CC) continue;
                var actor = hit.collider.GetComponentInParent<ActorRig>();
                if (actor != null && actor.ActorId == Cast.Player) continue;
                distance = Mathf.Min(distance, Mathf.Max(.25f, hit.distance - .1f));
            }
            target = cam.position + direction.normalized * distance;
            var targetVelocity = (target - _previousTarget) / Mathf.Max(.001f, Time.fixedDeltaTime);
            _previousTarget = target;
            if (!_grip.MoveGrip(target, targetVelocity, Time.fixedDeltaTime)) { _held = null; HeldLabel = null; return; }
            LastTouchedByPlayer[_held] = Time.time;
        }

        bool InputBlocked => _s == null || _p == null || _p.Cam == null || !_p.Controlling || PhysicalCharacter.WorldPaused
            || _s.S?.Player == null || !_s.S.Player.Alive || _s.S.Player.Status != ActorStatus.Active
            || (_s.Dialogue != null && _s.Dialogue.Active) || (_s.Note != null && _s.Note.Open)
            || (_s.Menu != null && _s.Menu.Open) || (_s.Trial != null && _s.Trial.Active)
            || (_s.Reveal != null && _s.Reveal.Active) || (_s.Cine != null && _s.Cine.Busy);

        static readonly List<Rigidbody> ExpiredTouches = new List<Rigidbody>();
        static void PruneTouches()
        {
            ExpiredTouches.Clear();
            foreach (var entry in LastTouchedByPlayer)
                if (entry.Key == null || Time.time - entry.Value > 4f) ExpiredTouches.Add(entry.Key);
            foreach (var body in ExpiredTouches) LastTouchedByPlayer.Remove(body);
        }

        void OnDisable() { Release(Vector3.zero); }
        void OnDestroy() { _grip.Dispose(); }
        public void DropHeld() { Release(Vector3.zero); }

        void TryGrab()
        {
            var cam = _p.Cam.transform;
            if (!Physics.Raycast(new Ray(cam.position, cam.forward), out var hit, 2.6f, ~0, QueryTriggerInteraction.Ignore)) return;
            var rb = hit.rigidbody; if (rb == null || rb.isKinematic) return;
            if (hit.collider.GetComponentInParent<ActorRig>() != null) return;
            if (rb.mass > 30f) { Hud.I?.Toast("너무 무거워서 들 수 없다", Pal.TextDim, 1.2f); return; }
            PhysicalProp.Ensure(rb.gameObject);
            if (!_grip.TryAcquire(rb, hit.point, _p.CC)) return;
            _held = rb;
            _dist = Mathf.Clamp(hit.distance, 1.0f, 2.0f);
            _previousTarget = _grip.WorldGrip;
            Session.I?.World?.ViewOf(Cast.Player)?.Rig?.Anim?.PlayAction(ActionAnim.PickUp, 0.5f);
            var tag = rb.GetComponentInParent<ItemTag>(); var fv = rb.GetComponentInParent<FurnitureView>();
            HeldLabel = tag != null ? Session.I.S.I(tag.ItemId)?.Kor : fv != null ? FurnitureCatalog.Get(fv.Type)?.Kor : "물건";
            Hud.I?.Toast(LineBank.FixParticles(HeldLabel + "을(를) 들었다 — 클릭 던지기 · Z에서 손 떼면 내려놓기 · 휠 거리 조절"), Pal.TextDim, 2f);
            LastTouchedByPlayer[rb] = Time.time;
        }

        void Release(Vector3 impulse)
        {
            if (_held != null)
            {
                if (impulse != Vector3.zero) Session.I?.World?.ViewOf(Cast.Player)?.Rig?.Anim?.PlayAction(ActionAnim.Overhead, 0.45f);
                LastTouchedByPlayer[_held] = Time.time;
            }
            _grip.Release(impulse);
            _held = null; HeldLabel = null;
        }
    }

    /// <summary>Tracks a light furniture rigidbody: when it comes to rest somewhere new, the kernel learns where it is.</summary>
    public sealed class FurnitureSync : MonoBehaviour
    {
        public int FurnitureId; Rigidbody _rb; Vector3 _last; float _lastYaw; float _rest; float _born, _roomCheck;
        int _visualRoom = int.MinValue;
        void Start() { _rb = GetComponent<Rigidbody>(); _last = transform.position; _lastYaw = transform.eulerAngles.y; _born = Time.time; }
        void FixedUpdate()
        {
            if (_rb == null || _rb.isKinematic || Session.I == null) return;
            var mansion = Session.I.World?.Mansion;
            if (mansion != null && Time.time >= _roomCheck)
            {
                _roomCheck = Time.time + .3f;
                int room = mansion.RoomAtWorld(_rb.position + Vector3.up * .25f);
                if (room != _visualRoom) { mansion.RehomePhysicalFurniture(FurnitureId, room); _visualRoom = room; }
            }
            if (PhysicalGrab.IsHeld(_rb)) { _rest = 0f; return; }
            // let the physics settle after the room is built: that is not "moved"
            if (Time.time - _born < 4f && !PhysicsGrab.RecentlyPlayer(_rb)) { _last = transform.position; _lastYaw = transform.eulerAngles.y; return; }
            if (_rb.IsSleeping() || (_rb.linearVelocity.sqrMagnitude < 0.004f && _rb.angularVelocity.sqrMagnitude < .01f))
            {
                _rest += Time.fixedDeltaTime;
                // (perf) ~180 pieces every physics step: the position and the player-touch lookup only once it has rested
                Vector3 pos;
                pos = transform.position;
                if (_rest > 0.5f && ((pos - _last).sqrMagnitude > (PhysicsGrab.RecentlyPlayer(_rb) ? 0.01f : .09f)
                    || Mathf.Abs(Mathf.DeltaAngle(_lastYaw, transform.eulerAngles.y)) > 8f))
                {
                    var S = Session.I.S; if (FurnitureId < 0 || FurnitureId >= S.Layout.Furniture.Count) return;
                    var f = S.Layout.Furniture[FurnitureId]; var p = pos;
                    int floor = f.Pos.f; float nearest = float.MaxValue;
                    foreach (var level in S.Layout.Floors)
                    {
                        if (p.y < level.BaseY - .6f) continue;
                        float distance = Mathf.Abs(p.y - level.BaseY);
                        if (distance < nearest) { nearest = distance; floor = level.F; }
                    }
                    Session.I.Sim.PhysicsFurnitureMoved(f, new P3(floor, p.x, p.z), transform.eulerAngles.y, PhysicsGrab.RecentlyPlayer(_rb) ? Cast.Player : null);
                    _last = p; _lastYaw = transform.eulerAngles.y;
                }
            }
            else _rest = 0;
        }
    }

    /// <summary>Reports physical impacts on world objects back into the simulation.</summary>
    public static class PhysicsBridge
    {
        static bool _hooked;
        public static void Hook()
        {
            if (_hooked) return; _hooked = true;
            PropMaterial.OnDamaged += OnDamaged;
        }

        static void OnDamaged(PropMaterial pm, int dmg, Vector3 point, float impulse)
        {
            var s = Session.I; if (s == null || s.Sim == null) return;
            var S = s.S; var rb = pm.GetComponentInParent<Rigidbody>();
            string by = PhysicsGrab.RecentlyPlayer(rb) ? Cast.Player : null;
            int floor = S.Player?.Pos.f ?? 0;
            var at = new P3(floor, point.x, point.z);
            if (!string.IsNullOrEmpty(pm.ItemId)) { var it = S.I(pm.ItemId); if (it != null && it.Holder == null) s.Sim.PhysicsItemDamaged(it, dmg, at, by); }
            else if (pm.FurnitureId >= 0 && pm.FurnitureId < S.Layout.Furniture.Count) s.Sim.PhysicsFurnitureDamaged(S.Layout.Furniture[pm.FurnitureId], dmg, by);
        }

        /// <summary>An item the player threw/shoved struck a person.</summary>
        public static void ItemHitActor(string itemId, Rigidbody rb, Collision col)
        {
            // The physical character handles both capsule and ragdoll contact with actual energy/region.
            // Keep this bridge only for legacy actors, otherwise one impact produces two wounds.
            var motor = col.collider.GetComponentInParent<PhysicalCharacter>();
            var physicalPart = col.collider.GetComponentInParent<PhysicalBodyPart>();
            if (motor != null || (physicalPart != null && physicalPart.Owner != null)) return;
            var s = Session.I; if (s == null || !PhysicsGrab.RecentlyPlayer(rb)) return;
            var rig = col.collider.GetComponentInParent<ActorRig>();
            if (rig == null) rig = col.collider.GetComponentInParent<ActorView>()?.Rig;
            if (rig == null)
            {
                var part = col.collider.GetComponentInParent<PhysicalBodyPart>();
                if (part != null && part.Owner != null) rig = part.Owner.GetComponent<ActorView>()?.Rig;
            }
            if (rig == null) return;
            var a = s.S.A(rig.ActorId); if (a == null || a.IsPlayer) return;
            float imp = col.impulse.magnitude; if (imp < 1.5f) return;
            var bodyPart = col.collider.GetComponentInParent<PhysicalBodyPart>();
            var region = bodyPart != null ? bodyPart.Region : col.contactCount > 0
                ? (BL23.Sim.BodyRegion)rig.NearestRegion(col.GetContact(0).point)
                : (BL23.Sim.BodyRegion)rig.RegionFromCollider(col.collider);
            s.Sim.PlayerThrownHit(a, s.S.I(itemId), imp, region);
            PhysicsGrab.LastTouchedByPlayer.Remove(rb);
        }
    }
}
