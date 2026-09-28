using System.Collections.Generic;
using UnityEngine;

namespace BL23.Game.Characters
{
    /// <summary>
    /// Root component of every BL23 actor. Holds the common humanoid skeleton references and exposes the
    /// visual API (expressions, wounds, blood, disguise, hitboxes). Animation lives in <see cref="ActorAnimator"/>.
    /// Coordinates: actor root at the feet, +Z forward, 1 unit = 1 m.
    /// </summary>
    [DisallowMultipleComponent]
    public class ActorRig : MonoBehaviour
    {
        public string ActorId;
        public float Height;
        public Transform Hips, Spine, Chest, Neck, Head, HandL, HandR, FootL, FootR;
        public Transform HandAnchorL, HandAnchorR;
        public Transform EyeAnchor;
        public Transform ChestAnchor, HeadTopAnchor;
        public ActorAnimator Anim;

        [Header("Skeleton (HBone order)")]
        public Transform[] Bones = new Transform[ActorSkeleton.Count];
        public Vector3 HandTipRestL, HandTipRestR;      // rest positions (root space) of the fingertips, for canonical arm frames
        public Vector3[] FingerTipRest = new Vector3[10]; // finger tips (rest, root space): thumb..little of the left hand, then the right
        public bool Female;
        public bool IsGlb;
        public bool KeepArmRestBend;                    // bind pose = relaxed arms (see ActorAnimator.Init)
        public string AnimSet = "";                     // captured clip set (Resources/Actors/Anim/<set>.bytes)
        public bool IsFallback;
        public string IdleStyle = "";
        public bool CanBreak;

        [Header("Rendering")]
        public SkinnedMeshRenderer[] Skins = new SkinnedMeshRenderer[0];
        public Renderer[] ExtraRenderers = new Renderer[0];
        public ActorFace Face;
        public AquariumHead Aquarium;
        public Transform PropsRoot;                     // built-in carried props (clipboard, briefcase...), hidden with SetPropsVisible(false)

        [HideInInspector] public Matrix4x4[] RestBoneToRoot; // rest pose bone matrices (root space), captured at init

        ActorWounds _wounds;
        readonly List<Material> _mats = new List<Material>();
        readonly Dictionary<Collider, BodyRegion> _hit = new Dictionary<Collider, BodyRegion>();
        readonly List<Collider> _hitList = new List<Collider>();
        GameObject _disguise;
        bool _inited;
        float _blood, _wet, _break;

        static readonly int IdBlood = Shader.PropertyToID("_BloodAmount");
        static readonly int IdWet = Shader.PropertyToID("_Wet");
        static readonly int IdBreak = Shader.PropertyToID("_BreakAmount");

        public IReadOnlyList<Material> Materials { get { Init(); return _mats; } }
        public ActorWounds Wounds { get { Init(); return _wounds; } }
        public IReadOnlyList<Collider> Hitboxes { get { Init(); return _hitList; } }

        void Awake() { Init(); }

        public void Init()
        {
            if (_inited) return;
            if (Bones == null || Bones.Length < ActorSkeleton.CoreCount || Bones[0] == null) return; // not assembled yet
            if (Bones.Length != ActorSkeleton.Count)
            {
                // rigs baked with an older bone layout (21 core bones, or the 34-bone two-segment hands): keep the core,
                // re-find the extended bones by name (missing ones simply stay null and follow their parent)
                var old = Bones;
                Bones = new Transform[ActorSkeleton.Count];
                for (int i = 0; i < ActorSkeleton.CoreCount && i < old.Length; i++) Bones[i] = old[i];
                for (int i = ActorSkeleton.CoreCount; i < old.Length; i++)
                {
                    var t = old[i];
                    if (t != null && System.Enum.TryParse<HBone>(t.name, out var hb) && (int)hb >= ActorSkeleton.CoreCount && Bones[(int)hb] == null) Bones[(int)hb] = t;
                }
            }
            _inited = true;
            // capture rest matrices (the prefab is saved in its bind pose)
            RestBoneToRoot = new Matrix4x4[ActorSkeleton.Count];
            var rootInv = transform.worldToLocalMatrix;
            for (int i = 0; i < ActorSkeleton.Count; i++)
                RestBoneToRoot[i] = Bones[i] != null ? rootInv * Bones[i].localToWorldMatrix : Matrix4x4.identity;
            // per-actor material instances
            _mats.Clear();
            foreach (var smr in Skins)
            {
                if (smr == null) continue;
                var arr = smr.materials; // instantiates
                _mats.AddRange(arr);
                if (Face != null) foreach (var fm in arr) if (fm != null && fm.IsKeywordEnabled("_FACE")) Face.Bind(fm);
            }
            foreach (var r in ExtraRenderers)
            {
                if (r == null) continue;
                var arr = r.materials;
                foreach (var m in arr) if (m != null && m.HasProperty(IdBlood)) _mats.Add(m);
            }
            _wounds = GetComponent<ActorWounds>();
            if (_wounds == null) _wounds = gameObject.AddComponent<ActorWounds>();
            _wounds.Bind(this);
            if (Anim == null) Anim = GetComponent<ActorAnimator>();
            if (Anim == null) Anim = gameObject.AddComponent<ActorAnimator>();
            BuildHitboxes();
        }

        public Transform Bone(HBone b) => Bones[(int)b] != null ? Bones[(int)b] : Bones[(int)ActorSkeleton.Core(b)];
        public bool HasBone(HBone b) => (int)b < Bones.Length && Bones[(int)b] != null;

        // ---------------------------------------------------------------- face
        public void SetExpression(Expr e, float intensity = 1f)
        {
            Init();
            if (Face != null) Face.SetExpression(e, intensity);
            if (Aquarium != null) Aquarium.SetMood(e, intensity);
            if (e == Expr.Break && CanBreak && Anim != null && Anim.BreakAmount < 0.5f) { /* BREAK visuals are driven by Anim.SetBreak */ }
        }

        public void SetTalking(bool on)
        {
            Init();
            if (Face != null) Face.SetTalking(on);
            if (Aquarium != null) Aquarium.SetTalking(on);
        }

        public void SetBlink(bool enabled)
        {
            Init();
            if (Face != null) Face.SetBlink(enabled);
        }

        /// <summary>
        /// Dark "ink shadow" face (see <see cref="DarkFace"/>): the shadow creeps down the face over ~0.35 s, the eye glow
        /// fades in, everything reverts smoothly on <see cref="DarkFace.None"/> (or after 'hold' seconds when hold &gt; 0).
        /// VeiledSmirk also raises a finger to the lips and tilts the head. Works on every actor with a painted face
        /// (scanned or modelled); the glow colour is <see cref="ActorFace.DarkGlow"/>.
        /// </summary>
        public void SetDarkFace(DarkFace kind, float intensity = 1f, float hold = -1f)
        {
            Init();
            if (Face != null) Face.SetDarkFace(kind, intensity, hold);
            if (Anim == null) return;
            if (kind == DarkFace.VeiledSmirk) Anim.PlayGesture(Gesture.FingerToLips, hold > 0f ? hold : 3600f);
            else if (Anim.CurrentGesture == Gesture.FingerToLips) Anim.PlayGesture(Gesture.None);
        }

        public DarkFace DarkFaceState => Face != null ? Face.Dark : DarkFace.None;

        // ---------------------------------------------------------------- damage / state visuals
        public void AddWound(BodyRegion r, BL23.Sim.DamageType t, int severity, Vector3 localPoint, bool postmortem)
        {
            Init();
            if (!_inited) return;
            _wounds.Add(r, t, severity, localPoint, postmortem);
        }

        public void ClearWounds()
        {
            Init();
            if (!_inited) return;
            _wounds.Clear();
        }

        public void SetBloodied(float amount)
        {
            Init();
            _blood = Mathf.Clamp01(amount);
            foreach (var m in _mats) m.SetFloat(IdBlood, _blood);
        }

        public void SetWet(bool on)
        {
            Init();
            _wet = on ? 1f : 0f;
            foreach (var m in _mats) m.SetFloat(IdWet, _wet);
            if (Aquarium != null) Aquarium.SetWet(on);
        }

        /// <summary>Internal: shader side of BREAK (driven by ActorAnimator.SetBreak).</summary>
        public void SetBreakVisual(float t)
        {
            Init();
            if (Mathf.Abs(t - _break) < 0.001f) return;
            _break = t;
            foreach (var m in _mats) m.SetFloat(IdBreak, t);
            if (Aquarium != null) Aquarium.SetBreak(t);
        }

        public void SetDisguise(string itemType)
        {
            Init();
            if (_disguise != null) { Destroy(_disguise); _disguise = null; }
            if (string.IsNullOrEmpty(itemType)) return;
            _disguise = DisguiseBuilder.Build(this, itemType);
        }

        public void SetVisible(bool on)
        {
            Init();
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = on;
        }

        // ---- first person: the player looks out of this body. The head (face, hair, hats) collapses for that view only;
        // the eye position is taken from the head bone's unscaled frame so it stays put while the head is hidden.
        Vector3 _eyeLocal, _headScale = Vector3.one; bool _eyeLocalSet; bool _headHidden;
        public bool HeadHidden => _headHidden;
        public void SetHeadHidden(bool hidden)
        {
            if (Bones == null || Bones.Length <= (int)HBone.Head) return;
            var h = Bones[(int)HBone.Head]; if (h == null || hidden == _headHidden) return;
            if (!_eyeLocalSet && EyeAnchor != null && EyeAnchor.parent == h) { _eyeLocal = EyeAnchor.localPosition; _eyeLocalSet = true; }
            if (hidden) { _headScale = h.localScale; h.localScale = _headScale * 0.0005f; } else h.localScale = _headScale;
            _headHidden = hidden;
        }
        public Vector3 EyeWorld()
        {
            var h = Bones != null && Bones.Length > (int)HBone.Head ? Bones[(int)HBone.Head] : null;
            if (h == null || h.parent == null) return EyeAnchor != null ? EyeAnchor.position : transform.position + Vector3.up * 1.6f;
            if (!_eyeLocalSet && EyeAnchor != null && EyeAnchor.parent == h && !_headHidden) { _eyeLocal = EyeAnchor.localPosition; _eyeLocalSet = true; }
            if (!_eyeLocalSet) return EyeAnchor != null ? EyeAnchor.position : h.position + Vector3.up * 0.08f;
            return h.parent.TransformPoint(h.localPosition + h.localRotation * Vector3.Scale(_headHidden ? _headScale : h.localScale, _eyeLocal));
        }

        /// <summary>Hide the built-in character props (clipboard, briefcase, lily...) while the hand holds a game item,
        /// performs an action, is dead or carries someone. Disable to control them manually via SetPropsVisible.</summary>
        public bool AutoHideProps = true;
        bool _propsShown = true;

        void LateUpdate()
        {
            if (!AutoHideProps || !_inited) return;
            bool show = true;
            if (Anim != null)
            {
                var a = Anim.CurrentAction;
                if (a != ActionAnim.None && a != ActionAnim.Read && a != ActionAnim.Write) show = false;
                if (Anim.IsDead || Anim.CurrentPosture == Posture.LieBack || Anim.CurrentPosture == Posture.LieFront || Anim.CurrentPosture == Posture.LieSide || Anim.IsCarrying) show = false;
            }
            if (show) show = !HasForeignChild(HandAnchorL) && !HasForeignChild(HandAnchorR);
            if (show != _propsShown) { _propsShown = show; ShowBuiltIn(show); }
        }

        static bool HasForeignChild(Transform t)
        {
            if (t == null) return false;
            for (int i = 0; i < t.childCount; i++) if (!t.GetChild(i).name.StartsWith("Prop_")) return true;
            return false;
        }

        void ShowBuiltIn(bool on)
        {
            foreach (var t in new[] { HandAnchorL, HandAnchorR })
            {
                if (t == null) continue;
                for (int i = 0; i < t.childCount; i++)
                    if (t.GetChild(i).name.StartsWith("Prop_")) t.GetChild(i).gameObject.SetActive(on);
            }
        }

        public void SetPropsVisible(bool on)
        {
            if (PropsRoot != null)
                foreach (var r in PropsRoot.GetComponentsInChildren<Renderer>(true)) r.enabled = on;
            foreach (var t in new[] { HandAnchorL, HandAnchorR })
            {
                if (t == null) continue;
                for (int i = 0; i < t.childCount; i++)
                    if (t.GetChild(i).name.StartsWith("Prop_")) t.GetChild(i).gameObject.SetActive(on);
            }
        }

        // ---------------------------------------------------------------- grasping & body physics
        [Header("Grasping")]
        public Transform[] FingerTipAnchors = new Transform[10];   // thumb..little tips of the left hand, then the right (children of the distal bones)
        public Transform FingerTipAnchor(bool left, int digit) => FingerTipAnchors != null && FingerTipAnchors.Length == 10 ? FingerTipAnchors[(left ? 0 : 5) + Mathf.Clamp(digit, 0, 4)] : (left ? HandAnchorL : HandAnchorR);

        readonly List<Collider> _body = new List<Collider>();
        bool _bodyOn;
        public IReadOnlyList<Collider> BodyColliders => _body;
        public bool BodyCollidersOn => _bodyOn;

        /// <summary>
        /// Solid (non-trigger) colliders on the head, torso, limbs, hands and feet that follow the animated bones
        /// (kinematic rigidbodies): thrown or pushed props bounce off the body, physics grabs can find the limb they
        /// touch. Off by default. Put them on a layer the actor's own CharacterController / "Body" capsule ignores
        /// (layer &lt; 0 keeps the actor's layer).
        /// </summary>
        public void SetBodyColliders(bool on, int layer = -1)
        {
            Init();
            if (!_inited) return;
            if (on && _body.Count == 0) BuildBodyColliders(layer);
            else if (layer >= 0) foreach (var c in _body) if (c != null) c.gameObject.layer = layer;
            foreach (var c in _body) if (c != null) c.enabled = on;
            _bodyOn = on;
        }

        void BuildBodyColliders(int layer)
        {
            float h = Height > 0 ? Height : 1.7f, s = h / 1.75f;
            Transform B(HBone b) => Bone(b);
            Vector3 L(HBone from, HBone to) => B(from).InverseTransformPoint(B(to).position);
            GameObject Go(string n, Transform parent)
            {
                var go = new GameObject("Col_" + n);
                go.layer = layer >= 0 ? layer : gameObject.layer;
                go.transform.SetParent(parent, false);
                var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.None;
                return go;
            }
            void Cap(string n, Transform parent, Vector3 a, Vector3 b, float r)
            {
                var go = Go(n, parent);
                Vector3 d = b - a;
                go.transform.localPosition = (a + b) * 0.5f;
                go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.up);
                var c = go.AddComponent<CapsuleCollider>(); c.direction = 1; c.radius = r; c.height = d.magnitude + r * 2f;
                _body.Add(c);
            }
            void Box(string n, Transform parent, Vector3 c0, Vector3 size)
            {
                var go = Go(n, parent);
                var c = go.AddComponent<BoxCollider>(); c.center = c0; c.size = size;
                _body.Add(c);
            }
            var head = B(HBone.Head);
            { var go = Go("Head", head); var c = go.AddComponent<SphereCollider>(); c.center = head.InverseTransformPoint(HeadCenterWorld()); c.radius = 0.11f * s; _body.Add(c); }
            Cap("Neck", B(HBone.Neck), Vector3.zero, L(HBone.Neck, HBone.Head), 0.05f * s);
            var chestTop = L(HBone.Chest, HBone.Neck);
            Box("Chest", B(HBone.Chest), new Vector3(0, chestTop.y * 0.45f, 0), new Vector3(0.3f * s, Mathf.Abs(chestTop.y) * 0.95f, 0.2f * s));
            var spineTop = L(HBone.Spine, HBone.Chest);
            Box("Abdomen", B(HBone.Spine), new Vector3(0, spineTop.y * 0.4f, 0f), new Vector3(0.26f * s, Mathf.Abs(spineTop.y) * 1.2f + 0.02f, 0.18f * s));
            Box("Pelvis", B(HBone.Hips), Vector3.zero, new Vector3(0.3f * s, 0.15f * s, 0.19f * s));
            for (int side = 0; side < 2; side++)
            {
                bool l = side == 0; string sn = l ? "L" : "R";
                HBone ua = l ? HBone.UpperArmL : HBone.UpperArmR;
                Cap("UpperArm" + sn, B(ua), Vector3.zero, L(ua, ua + 1), 0.045f * s);
                Cap("LowerArm" + sn, B(ua + 1), Vector3.zero, L(ua + 1, ua + 2), 0.038f * s);
                Vector3 tip = B(ua + 2).InverseTransformPoint(transform.TransformPoint(l ? HandTipRestL : HandTipRestR));
                Cap("Hand" + sn, B(ua + 2), tip * 0.1f, tip * 0.7f, 0.03f * s);
                HBone ul = l ? HBone.UpperLegL : HBone.UpperLegR;
                Cap("UpperLeg" + sn, B(ul), Vector3.zero, L(ul, ul + 1), 0.07f * s);
                Cap("LowerLeg" + sn, B(ul + 1), Vector3.zero, L(ul + 1, ul + 2), 0.055f * s);
                Vector3 toe = L(ul + 2, ul + 3);
                Box("Foot" + sn, B(ul + 2), new Vector3(0, toe.y * 0.5f, toe.z * 0.45f), new Vector3(0.09f * s, 0.08f * s, Mathf.Abs(toe.z) * 1.5f + 0.05f));
            }
        }

        // ---------------------------------------------------------------- hitboxes (layer "Hitbox", fallback index 8)
        public BodyRegion RegionFromCollider(Collider c)
        {
            Init();
            if (c != null && _hit.TryGetValue(c, out var r)) return r;
            // unknown collider: nearest bone
            if (c == null) return BodyRegion.Chest;
            return NearestRegion(c.bounds.center);
        }

        public BodyRegion NearestRegion(Vector3 world)
        {
            float best = float.MaxValue; BodyRegion br = BodyRegion.Chest;
            foreach (var kv in _hit)
            {
                float d = (kv.Key.ClosestPoint(world) - world).sqrMagnitude;
                if (d < best) { best = d; br = kv.Value; }
            }
            return br;
        }

        // ---- charpolish step 0 contract (owner after step 0: implementer 4, physics)
        /// <summary>Region of a hit given the collider AND the world contact point: a hitbox gives its own region, any
        /// other collider on this actor (the view's solid "Body" capsule, body colliders) the hitbox nearest to the point.</summary>
        public BodyRegion RegionFromHit(Collider c, Vector3 worldPoint)
        {
            Init();
            if (c != null && _hit.TryGetValue(c, out var r)) return r;
            return NearestRegion(worldPoint);
        }

        // ---- charpolish step 0b contract: rig-level ragdoll (owner: implementer 4, physics). The murder-foundation Game/Physics
        // adapter and the cinematics replay call these (CharacterPipeline_Current.md, top): keep the signatures.
        /// <summary>Hand the body to physics: blend from the animated pose into a ragdoll over blendIn seconds, with a
        /// world-space impulse (N·s) at the hips. Presentation only; the kernel stays authoritative.</summary>
        public void BeginRagdoll(Vector3 impulse, float blendIn = 0.15f) { Init(); if (_inited) BodyPhysics.BeginRagdoll(impulse, blendIn); }
        /// <summary>Stop simulating. getUpTime &gt; 0: a living, conscious actor gets up (or a dead one blends into its kernel
        /// dead pose) over that time; getUpTime &lt;= 0: the body stays exactly as it settled (snapshot kept as the dead pose).</summary>
        public void EndRagdoll(float getUpTime) { if (_physics != null) _physics.EndRagdoll(getUpTime); }
        /// <summary>True while physics owns the bones (ragdoll or pinned drag/carry).</summary>
        public bool IsRagdoll => _physics != null && _physics.IsRagdoll;
        /// <summary>Pin a grip point of this body to a moving target (a carrier's hand or shoulder anchor); the other limbs hang or
        /// trail physically. target null releases that pin.</summary>
        public void PinLimb(HumanLimb limb, Transform target) { Init(); if (_inited) BodyPhysics.PinLimb(limb, target); }
        /// <summary>The body comes to rest on this surface (floor, stair, bed, sofa, water bottom): collide with it and settle there.</summary>
        public void SettleOn(Collider surface) { Init(); if (_inited) BodyPhysics.SettleOn(surface); }

        /// <summary>charpolish step 0b (owner: implementer 1, face): talk visemes paced by the text on screen (one open/close per
        /// Hangul syllable, vowel-shaped mouths, pauses at punctuation); ends when the text has been "spoken".</summary>
        public void SetTalkText(string text, float charsPerSecond) { Init(); if (Face != null) Face.SetTalkText(text, charsPerSecond); }

        /// <summary>motion track: gasping / reddening / pallor on the face (paired kills, drowning, pain); all 0 = off.</summary>
        public void SetStrain(float gasp01, float flush01, float pale01) { Init(); if (Face != null) Face.SetStrain(gasp01, flush01, pale01); }

        ActorPhysics _physics;
        /// <summary>Presentation physics of this actor (hit springs, ragdoll, falls); added on first use.</summary>
        public ActorPhysics BodyPhysics
        {
            get
            {
                if (_physics == null) { _physics = GetComponent<ActorPhysics>(); if (_physics == null) _physics = gameObject.AddComponent<ActorPhysics>(); }
                return _physics;
            }
        }

        void BuildHitboxes()
        {
            if (_hit.Count > 0 || Bones[0] == null) return;
            int layer = ActorSkeleton.LayerHitbox;
            float h = Height > 0 ? Height : 1.7f;
            float s = h / 1.75f;
            Transform B(HBone b) => Bones[(int)b];
            Vector3 L(HBone from, HBone to) => B(from).InverseTransformPoint(B(to).position);

            AddSphere(BodyRegion.Head, B(HBone.Head), B(HBone.Head).InverseTransformPoint(HeadCenterWorld()), 0.12f * s, layer);
            AddCapsule(BodyRegion.Neck, B(HBone.Neck), Vector3.zero, L(HBone.Neck, HBone.Head), 0.06f * s, layer);
            var chestTop = L(HBone.Chest, HBone.Neck);
            AddBox(BodyRegion.Chest, B(HBone.Chest), new Vector3(0, chestTop.y * 0.45f, 0.055f * s), new Vector3(0.3f * s, chestTop.y * 0.95f, 0.11f * s), layer);
            AddBox(BodyRegion.Back, B(HBone.Chest), new Vector3(0, chestTop.y * 0.45f, -0.065f * s), new Vector3(0.3f * s, chestTop.y * 0.95f, 0.1f * s), layer);
            var spineTop = L(HBone.Spine, HBone.Chest);
            AddBox(BodyRegion.Abdomen, B(HBone.Spine), new Vector3(0, spineTop.y * 0.3f, 0.0f), new Vector3(0.27f * s, Mathf.Abs(spineTop.y) * 1.6f + 0.05f, 0.2f * s), layer);
            AddBox(BodyRegion.Abdomen, B(HBone.Hips), new Vector3(0, 0.0f, 0.0f), new Vector3(0.32f * s, 0.16f * s, 0.2f * s), layer);
            for (int side = 0; side < 2; side++)
            {
                bool l = side == 0;
                HBone ua = l ? HBone.UpperArmL : HBone.UpperArmR;
                AddSphere(l ? BodyRegion.ShoulderL : BodyRegion.ShoulderR, B(ua), Vector3.zero, 0.065f * s, layer);
                AddCapsule(l ? BodyRegion.ArmL : BodyRegion.ArmR, B(ua), L(ua, ua + 1) * 0.25f, L(ua, ua + 1), 0.05f * s, layer);
                AddCapsule(l ? BodyRegion.ArmL : BodyRegion.ArmR, B(ua + 1), Vector3.zero, L(ua + 1, ua + 2), 0.042f * s, layer);
                Vector3 tip = B(ua + 2).InverseTransformPoint(transform.TransformPoint(l ? HandTipRestL : HandTipRestR));
                AddCapsule(l ? BodyRegion.HandL : BodyRegion.HandR, B(ua + 2), tip * 0.15f, tip * 0.75f, 0.045f * s, layer);
                HBone ul = l ? HBone.UpperLegL : HBone.UpperLegR;
                AddCapsule(l ? BodyRegion.LegL : BodyRegion.LegR, B(ul), Vector3.zero, L(ul, ul + 1), 0.075f * s, layer);
                AddCapsule(l ? BodyRegion.LegL : BodyRegion.LegR, B(ul + 1), Vector3.zero, L(ul + 1, ul + 2), 0.06f * s, layer);
                Vector3 toe = L(ul + 2, ul + 3);
                AddBox(l ? BodyRegion.FootL : BodyRegion.FootR, B(ul + 2), new Vector3(0, toe.y * 0.5f, toe.z * 0.45f), new Vector3(0.1f * s, 0.09f * s, Mathf.Abs(toe.z) * 1.6f + 0.05f), layer);
            }
        }

        public Vector3 HeadCenterWorld()
        {
            if (HeadTopAnchor != null && Bones[(int)HBone.Head] != null)
                return Vector3.Lerp(Bones[(int)HBone.Head].position, HeadTopAnchor.position, 0.55f);
            return Bones[(int)HBone.Head] != null ? Bones[(int)HBone.Head].position + Vector3.up * 0.1f : transform.position + Vector3.up * Height * 0.93f;
        }

        GameObject HitGO(BodyRegion r, Transform parent, int layer)
        {
            var go = new GameObject("Hit_" + r);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;
            return go;
        }

        // Hitboxes are triggers on layer "Hitbox" (index 8 if unnamed): queries with QueryTriggerInteraction.Collide find them,
        // while CharacterControllers, props and floor raycasts (QueryTriggerInteraction.Ignore) are unaffected.
        void Register(Collider c, BodyRegion r) { c.isTrigger = true; _hit[c] = r; _hitList.Add(c); }

        void AddSphere(BodyRegion r, Transform parent, Vector3 c, float rad, int layer)
        {
            var go = HitGO(r, parent, layer);
            var col = go.AddComponent<SphereCollider>(); col.center = c; col.radius = rad;
            Register(col, r);
        }

        void AddCapsule(BodyRegion r, Transform parent, Vector3 a, Vector3 b, float rad, int layer)
        {
            var go = HitGO(r, parent, layer);
            Vector3 d = b - a;
            go.transform.localPosition = (a + b) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.up);
            var col = go.AddComponent<CapsuleCollider>();
            col.direction = 1; col.radius = rad; col.height = d.magnitude + rad * 2f;
            Register(col, r);
        }

        void AddBox(BodyRegion r, Transform parent, Vector3 c, Vector3 size, int layer)
        {
            var go = HitGO(r, parent, layer);
            var col = go.AddComponent<BoxCollider>(); col.center = c; col.size = size;
            Register(col, r);
        }

        /// <summary>Bone that carries a given body region.</summary>
        public HBone RegionBone(BodyRegion r)
        {
            switch (r)
            {
                case BodyRegion.Head: return HBone.Head;
                case BodyRegion.Neck: return HBone.Neck;
                case BodyRegion.Chest: case BodyRegion.Back: return HBone.Chest;
                case BodyRegion.Abdomen: return HBone.Spine;
                case BodyRegion.ShoulderL: return HBone.UpperArmL;
                case BodyRegion.ShoulderR: return HBone.UpperArmR;
                case BodyRegion.ArmL: return HBone.LowerArmL;
                case BodyRegion.ArmR: return HBone.LowerArmR;
                case BodyRegion.HandL: return HBone.HandL;
                case BodyRegion.HandR: return HBone.HandR;
                case BodyRegion.LegL: return HBone.UpperLegL;
                case BodyRegion.LegR: return HBone.UpperLegR;
                case BodyRegion.FootL: return HBone.FootL;
                case BodyRegion.FootR: return HBone.FootR;
            }
            return HBone.Chest;
        }
    }
}
