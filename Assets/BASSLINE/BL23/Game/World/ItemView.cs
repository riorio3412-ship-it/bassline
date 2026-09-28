using System;
using BL23.Game.Mansion;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>A portable object. Free items are physical (the player can push/throw them); resting positions are written back
    /// to the kernel so NPC knowledge and evidence follow the real object.</summary>
    public sealed class ItemView : MonoBehaviour
    {
        public string Id; WorldPresenter W; GameState S => Session.I.S;
        public GameObject Visual; Rigidbody _rb; Transform _anchor; float _restTimer; Vector3 _lastWritten;
        public bool Held => _anchor != null;
        public Transform HeldAnchor => _anchor;
        public WorldPresenter World => W;
        public bool ContactTransfer { get; private set; }

        public void Init(WorldPresenter w, Item it)
        {
            W = w; Id = it.Id;
            var def = it.Def ?? ItemCatalog.Get("Book");
            try { Visual = MurderProps.Create(def, it.Id) ?? ViolenceProps.Create(def, it.Id) ?? PropFactory.CreateItem(def, it.Id); } catch (Exception e) { Debug.LogException(e); }   // (+ violence track: guns, crossbow, bolts, ammunition, tape)
            if (Visual == null) Visual = FallbackMansion.ItemBox(def);
            Visual.transform.SetParent(transform, false);
            _rb = Visual.GetComponent<Rigidbody>();
            if (_rb == null) { _rb = Visual.AddComponent<Rigidbody>(); _rb.mass = Mathf.Max(0.05f, def.Mass); }
            _rb.interpolation = RigidbodyInterpolation.Interpolate; _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var tag = Visual.GetComponent<ItemTag>() ?? Visual.AddComponent<ItemTag>(); tag.ItemId = it.Id;
            (Visual.GetComponent<ItemImpact>() ?? Visual.AddComponent<ItemImpact>()).ItemId = it.Id;
            foreach (var c in Visual.GetComponentsInChildren<Collider>()) { var t2 = c.GetComponent<ItemTag>() ?? c.gameObject.AddComponent<ItemTag>(); t2.ItemId = it.Id; }
            Refresh("init");
            if (it.Bloody) SetState("blood");
        }

        public void Refresh(string why)
        {
            var it = S.I(Id); if (it == null) { gameObject.SetActive(false); return; }
            if (ContactTransfer) return;
            if (ViolenceStick(it)) return;   // --- violence track: a bolt stuck in a body / wall, a cord tied on someone
            if (Held && it.Holder == null && why != "contact_complete")
            {
                var owner = _anchor.GetComponentInParent<BL23.Game.Characters.ActorRig>();
                if (owner != null)
                {
                    BL23.Game.Physicality.PhysicalItemTransfer.Release(this, owner, _anchor == owner.HandAnchorL);
                    return;
                }
            }
            // --- time-on-demand (begin): kept in a container → shown in its slot; hidden where there is nothing to open → unseen
            _lastHidden = it.Hidden;
            if (it.Holder != null) { _hopT = -1f; if (_slot != null) LeaveSlot(); }
            // --- time-on-demand (end)
            if (it.Holder != null) { if (!Held) { var holder = W.ViewOf(it.Holder); var anchor = holder?.Rig != null ? (holder.Rig.HandAnchorR ?? holder.transform) : null; bool pocket = S.A(it.Holder)?.Pocket.Contains(it.Id) ?? false; if (pocket) { Hide(true); } } return; }
            Hide(false);
            if (Held) Detach();
            // --- time-on-demand (begin)
            if (_hopT >= 0f) return;   // just found and hopping out: it lands by itself
            if (TrySlot(it)) return;
            if (ContainerView.HiddenInPlain(it)) { Hide(true); return; }
            // --- time-on-demand (end)
            Place(it);
        }

        // --- time-on-demand (begin): an item kept inside a container (a drawer, a shelf behind cabinet doors, under a chest
        // lid) sits in the part's slot and follows it (a drawer carries it out). Never parented to the furniture (a piece that
        // moves is rebuilt as a new object); kinematic, no physics write-back; it can be picked only while its part is open,
        // and seen only then (or through a glazed door).
        ContainerView.Slotting _slot; Collider[] _slotCols; Renderer[] _slotRends; bool _colOn = true, _visOn = true, _lastHidden; float _hidCheck, _slotYaw;
        Vector3 _hopFrom, _hopTo; float _hopT = -1f;
        /// <summary>Kept in a container (shown in a drawer / on a shelf behind doors).</summary>
        public bool InContainer => _slot != null;
        /// <summary>In a container whose part is open (it can be reached).</summary>
        public bool Reachable => _slot == null || _colOn;

        bool TrySlot(Item it)
        {
            var s = ContainerView.SlotOf(it);
            if (s == null) { if (_slot != null) LeaveSlot(); return false; }
            if (_slot != s)
            {
                _slot = s;
                if (!_rb.isKinematic) { _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }
                _rb.isKinematic = true; _rb.interpolation = RigidbodyInterpolation.None;   // posed by hand every frame
                _slotCols = Visual.GetComponentsInChildren<Collider>(true); _slotRends = Visual.GetComponentsInChildren<Renderer>(true); _colOn = _visOn = true;
                // a long thing lies along the drawer's width, not through its back
                var b = new Bounds(); bool any = false;
                foreach (var r in _slotRends) { if (r == null) continue; var lb = r.localBounds; if (!any) { b = lb; any = true; } else b.Encapsulate(lb); }
                _slotYaw = any && b.size.z > 0.3f && b.size.z > b.size.x * 1.5f ? 90f : 0f;
            }
            FollowSlot();
            return true;
        }

        void LeaveSlot()
        {
            _slot = null; SetSlotCols(true); SetSlotVis(true); _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void FollowSlot()
        {
            if (!ContainerView.Pose(_slot, out var pos, out var rot, out float open, out bool glass))
            {
                LeaveSlot(); var it = S.I(Id); if (it != null && it.Holder == null) { if (ContainerView.HiddenInPlain(it)) Hide(true); else Place(it); }
                return;
            }
            Visual.transform.SetPositionAndRotation(pos, rot * Quaternion.Euler(0, _slotYaw, 0));
            SetSlotCols(open > 0.6f);
            SetSlotVis(open > 0.02f || glass);
            _lastWritten = pos;
        }

        void SetSlotCols(bool on) { if (_colOn == on || _slotCols == null) { _colOn = on; return; } _colOn = on; foreach (var c in _slotCols) if (c != null) c.enabled = on; }
        void SetSlotVis(bool on) { if (_visOn == on || _slotRends == null) { _visOn = on; return; } _visOn = on; foreach (var r in _slotRends) if (r != null) r.enabled = on; }

        /// <summary>Found by rummaging: a small arc out onto the top or the front edge, then it lies there like anything else.</summary>
        public void HopTo(Vector3 world)
        {
            if (Visual == null || Held) return;
            if (_slot != null) LeaveSlot();
            Hide(false);
            _hopFrom = Visual.transform.position; _hopTo = world; _hopT = 0f;
            _rb.isKinematic = true; _rb.interpolation = RigidbodyInterpolation.None;
        }

        void LateUpdate()
        {
            if (Visual == null || S == null) return;
            if (ContactTransfer) return;
            // the kernel can reveal (or hide) a thing without moving it (found in the ash, turned up by a search)
            if ((_hidCheck -= Time.deltaTime) <= 0f)
            {
                _hidCheck = 0.4f + (Id.Length % 5) * 0.05f;
                var it = S.I(Id); if (it != null && it.Holder == null && it.Hidden != _lastHidden && _hopT < 0f) Refresh("hidden");
            }
            if (_hopT >= 0f)
            {
                _hopT += Time.deltaTime / 0.35f; float k = Mathf.Clamp01(_hopT);
                Visual.transform.position = Vector3.Lerp(_hopFrom, _hopTo, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.22f;
                if (k >= 1f)
                {
                    _hopT = -1f; _rb.interpolation = RigidbodyInterpolation.Interpolate; _rb.isKinematic = false; _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; _restTimer = 0f;
                    _lastWritten = _hopFrom;   // where it lies now is written back once it settles
                }
                return;
            }
            if (_slot != null && !Held) FollowSlot();
        }
        // --- time-on-demand (end)

        // --- violence track (begin): a crossbow bolt stays where it struck (in a body it rides with the bone it hit); a cord
        // tied round someone is drawn as rings on the body (Game/Violence), so the loose item is hidden
        bool _stuck;
        bool ViolenceStick(Item it)
        {
            var V = S.Violence; if (V == null || Visual == null) return false;
            if (it.Holder != null && V.Bindings.Exists(b => !b.Off && b.Item == it.Id)) { if (Held) Detach(); Hide(true); return true; }
            var e = BL23.Sim.Violence.EmbeddedOf(S, it.Id);
            if (e == null)
            {
                if (_stuck) { _stuck = false; Visual.transform.SetParent(transform, true); foreach (var c in Visual.GetComponentsInChildren<Collider>()) c.enabled = true; }
                return false;
            }
            _stuck = true; Hide(false); _hopT = -1f; if (_slot != null) LeaveSlot();
            if (!_rb.isKinematic) { _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }
            _rb.isKinematic = true;
            var rot = Quaternion.Euler(-e.Pitch, e.Yaw, 0f); var fwd = rot * Vector3.forward;
            if (e.Actor != null)
            {
                var v = W.ViewOf(e.Actor); var bone = v?.Rig != null ? v.Rig.Bone(v.Rig.RegionBone((BL23.Game.Characters.BodyRegion)e.Region)) : null;
                if (bone == null) { Hide(true); return true; }
                foreach (var c in Visual.GetComponentsInChildren<Collider>()) c.enabled = false;
                Visual.transform.SetParent(bone, false);
                Visual.transform.SetPositionAndRotation(bone.position - fwd * 0.04f, rot);   // the head sunk in, the shaft and fletching out behind
                return true;
            }
            Visual.transform.SetParent(transform, true);
            Visual.transform.SetPositionAndRotation(W.ToWorld(e.At) + Vector3.up * e.Y - fwd * 0.12f, rot);
            return true;
        }
        // --- violence track (end)

        void Place(Item it)
        {
            var p = W.ToWorld(it.Pos);
            // drop onto whatever surface is below (tables, counters, floor)
            if (Physics.Raycast(p + Vector3.up * 1.6f, Vector3.down, out var hit, 3.5f, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<ItemTag>() == null) p = hit.point + Vector3.up * 0.02f;
            Visual.transform.position = p; Visual.transform.rotation = Quaternion.Euler(0, it.Yaw, 0);
            _rb.isKinematic = false; _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; _rb.Sleep();
            _lastWritten = p;
        }

        void Hide(bool h) { if (Visual != null && Visual.activeSelf == h) Visual.SetActive(!h); }

        public void AttachTo(Transform anchor, bool firstPersonPlayer)
        {
            BL23.Game.Physicality.PhysicalItemTransfer.Abort(this);
            _anchor = anchor; Hide(false); _rb.isKinematic = true;
            foreach (var c in Visual.GetComponentsInChildren<Collider>()) c.enabled = false;
            Visual.transform.SetParent(anchor, false); Visual.transform.localPosition = Vector3.zero; Visual.transform.localRotation = Quaternion.identity;
            if (firstPersonPlayer) Hide(true); // the first-person view model shows it instead
        }

        /// <summary>Presentation waits for contact while inventory remains owned by the simulation.</summary>
        public void BeginContactTransfer()
        {
            ContactTransfer = true;
            _hopT = -1f;
            if (_slot != null) LeaveSlot();
            if (_rb != null)
            {
                if (!_rb.isKinematic) { _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }
                _rb.isKinematic = true;
            }
        }

        public void EndContactTransfer() { ContactTransfer = false; }

        /// <summary>Release where the hand actually arrived, so a placed object settles on the real surface.</summary>
        public void ReleaseFromContact(Vector3 inheritedVelocity = default)
        {
            ContactTransfer = false; _anchor = null;
            if (Visual == null) return;
            Hide(false);
            Visual.transform.SetParent(transform, true);
            foreach (var collider in Visual.GetComponentsInChildren<Collider>()) collider.enabled = true;
            if (_rb != null)
            {
                _rb.isKinematic = false; _rb.linearVelocity = inheritedVelocity; _rb.angularVelocity = Vector3.zero;
                _rb.WakeUp();
            }
            _restTimer = 0f;
        }

        public void Detach()
        {
            BL23.Game.Physicality.PhysicalItemTransfer.Abort(this);
            if (_anchor == null) return; _anchor = null;
            Visual.transform.SetParent(transform, true);
            foreach (var c in Visual.GetComponentsInChildren<Collider>()) c.enabled = true;
            var it = S.I(Id); if (it != null && it.Holder == null) Place(it);
        }

        public void SetState(string state)
        {
            var rends = Visual.GetComponentsInChildren<Renderer>();
            foreach (var r in rends)
            {
                foreach (var m in r.materials)
                {
                    if (state == "blood") { if (m.HasProperty("_BloodAmount")) m.SetFloat("_BloodAmount", 0.9f); else if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.Lerp(m.GetColor("_BaseColor"), new Color(0.45f, 0.02f, 0.03f), 0.6f)); }
                    if (state == "washed") { if (m.HasProperty("_BloodAmount")) m.SetFloat("_BloodAmount", 0.12f); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.85f); }
                }
            }
        }

        public void Throw(Vector3 from, Vector3 velocity)
        {
            Detach(); Hide(false); Visual.transform.position = from; _rb.isKinematic = false; _rb.WakeUp(); _rb.linearVelocity = velocity; _restTimer = 0;
        }

        void FixedUpdate()
        {
            if (ContactTransfer || Held || _rb == null || _rb.isKinematic) return;
            var vt = Visual.transform; var pos = vt.position;   // (perf) ~200 items every physics step: read once (was up to 4 engine calls)
            // write back when the object has come to rest somewhere new (player pushes, throws, falls)
            if (_rb.IsSleeping() || _rb.linearVelocity.sqrMagnitude < 0.0025f)
            {
                _restTimer += Time.fixedDeltaTime;
                if (_restTimer > 0.4f && (pos - _lastWritten).sqrMagnitude > 0.0225f)
                {
                    var it = S.I(Id); if (it == null || it.Holder != null) return;
                    var p = pos; int f = FloorOf(p.y);
                    Session.I.Sim.PlayerPlaceItemPhysics(it, new P3(f, p.x, p.z)); _lastWritten = p;
                    pos = vt.position;   // the write-back may have placed it again
                }
            }
            else _restTimer = 0;
            if (pos.y < -40f) { var it = S.I(Id); if (it != null) Place(it); }
        }

        int FloorOf(float y)
        {
            int best = 0; float bd = 999;
            foreach (var fi in S.Layout.Floors) { float d = Mathf.Abs(y - fi.BaseY - 0.5f); if (y >= fi.BaseY - 0.5f && d < bd) { bd = d; best = fi.F; } }
            return best;
        }
    }

    public sealed class ItemTag : MonoBehaviour { public string ItemId; }

    /// <summary>
    /// Procedural meshes for the BL23 murder-content items (Sim/Systems/Methods*.cs): the letter opener, bronze bust, marble bookend,
    /// crystal decanter, ice pick, garden shears, crowbar, flat iron, scalpel, billiard cue, skinning knife, curtain cord, piano wire,
    /// extension cord, pliers, pillow, darkroom chemical and a bundle of foxglove. Same structure as PropFactory items (root at the
    /// bottom centre, a "Grip" child, box collider, Rigidbody, PropMaterial). Returns null for every other type.
    /// </summary>
    public static class MurderProps
    {
        static readonly System.Collections.Generic.Dictionary<string, (Mesh mesh, int[] slots, Vector3 grip, Vector3 gripEuler)> _cache = new System.Collections.Generic.Dictionary<string, (Mesh, int[], Vector3, Vector3)>();
        static readonly System.Collections.Generic.HashSet<string> Types = new System.Collections.Generic.HashSet<string> { "LetterOpener", "Statuette", "Bookend", "Decanter", "IcePick", "GardenShears", "Crowbar", "Iron", "Scalpel", "CueStick", "SkinningKnife", "CurtainCord", "PianoWire", "ExtensionCord", "Pliers", "Pillow", "DevChemical", "Foxglove", "Hacksaw", "BoneSaw", "SeveredPart" };

        public static GameObject Create(ItemDef def, string itemId)
        {
            if (def == null || !Types.Contains(def.Type)) return null;
            MansionMats.Init();
            if (!_cache.TryGetValue(def.Type, out var e))
            {
                var mb = new MeshBuilder(); Build(def, mb, out var g, out var ge);
                if (mb.Empty) { mb.Set(S.WoodLight, Color.white); mb.Box(new Vector3(0, def.Size * 0.25f, 0), new Vector3(def.Size * 0.5f, def.Size * 0.5f, def.Size)); }
                e = (mb.ToMesh("Item_" + def.Type, out var slots), slots, g, ge); _cache[def.Type] = e;
            }
            var go = new GameObject(string.IsNullOrEmpty(itemId) ? def.Type : itemId);
            var vis = new GameObject("vis"); vis.transform.SetParent(go.transform, false);
            vis.AddComponent<MeshFilter>().sharedMesh = e.mesh;
            vis.AddComponent<MeshRenderer>().sharedMaterials = MansionMats.Materials(e.slots);
            var grip = new GameObject("Grip").transform; grip.SetParent(go.transform, false); grip.localPosition = e.grip; grip.localRotation = Quaternion.Euler(e.gripEuler);
            var b = e.mesh.bounds; var bc = go.AddComponent<BoxCollider>(); bc.center = b.center; bc.size = Vector3.Max(b.size, new Vector3(0.01f, 0.01f, 0.01f));
            var rb = go.AddComponent<Rigidbody>(); rb.mass = Mathf.Max(0.01f, def.Mass); rb.linearDamping = 0.1f; rb.angularDamping = 0.2f;
            rb.collisionDetectionMode = def.Mass < 0.3f ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.Discrete;
            var pm = go.AddComponent<PropMaterial>(); pm.Mat = def.Mat; pm.ItemId = itemId; pm.Fragile = def.Mat == Mat.Glass || def.Mat == Mat.Ceramic; pm.Toughness = def.Heavy ? 2f : 1f;
            return go;
        }

        static void Build(ItemDef d, MeshBuilder mb, out Vector3 grip, out Vector3 gripEuler)
        {
            float s = d.Size; grip = new Vector3(0, s * 0.3f, 0); gripEuler = Vector3.zero;
            switch (d.Type)
            {
                case "LetterOpener":
                    mb.Set(S.Brass, new Color(0.9f, 0.9f, 0.92f)); mb.BevelBox(new Vector3(0, 0.008f, 0.04f), new Vector3(0.016f, 0.014f, 0.08f), 0.004f);
                    mb.Set(S.Gold, Color.white); mb.Sphere(new Vector3(0, 0.008f, 0.0f), 0.01f, 8, 5);
                    mb.Set(S.Steel, Color.white); mb.Push(new Vector3(0, 0.004f, 0), 0); mb.Prism(new System.Collections.Generic.List<Vector2> { new Vector2(0.007f, 0.085f), new Vector2(0.0f, s), new Vector2(-0.007f, 0.085f) }, 0, 0.003f, true, true); mb.Pop();
                    grip = new Vector3(0, 0.008f, 0.04f); break;
                case "Statuette":
                    {
                        // (env-art) a small bronze bust on a stepped black-marble plinth, green patina in the hollows; held upright
                        mb.Set(S.MarbleDark, Color.white); mb.BevelBox(new Vector3(0, 0.025f, 0), new Vector3(0.11f, 0.05f, 0.1f), 0.006f); mb.BevelBox(new Vector3(0, 0.058f, 0), new Vector3(0.08f, 0.016f, 0.072f), 0.004f);
                        var bronze = new Color(0.4f, 0.29f, 0.17f);
                        mb.Set(S.Brass, bronze);
                        mb.Ellipsoid(new Vector3(0, 0.1f, 0), new Vector3(0.068f, 0.036f, 0.04f), 14, 8);                 // shoulders
                        mb.Ellipsoid(new Vector3(0, 0.082f, 0.004f), new Vector3(0.05f, 0.03f, 0.036f), 12, 6);           // chest
                        mb.Cyl(new Vector3(0, 0.115f, 0), 0.016f, 0.04f, 10);                                              // neck
                        mb.Ellipsoid(new Vector3(0, 0.178f, 0.004f), new Vector3(0.03f, 0.04f, 0.035f), 14, 10);          // head
                        mb.Ellipsoid(new Vector3(0, 0.172f, 0.037f), new Vector3(0.006f, 0.012f, 0.008f), 6, 4);          // nose
                        foreach (float ex in new[] { -1f, 1f }) mb.Ellipsoid(new Vector3(ex * 0.03f, 0.176f, 0.002f), new Vector3(0.005f, 0.011f, 0.008f), 6, 4);   // ears
                        mb.Ellipsoid(new Vector3(0, 0.196f, -0.004f), new Vector3(0.032f, 0.026f, 0.036f), 12, 6);        // hair
                        mb.Set(S.GreenRust, new Color(0.45f, 0.62f, 0.5f));
                        foreach (var pz in new[] { new Vector3(-0.04f, 0.108f, 0.02f), new Vector3(0.035f, 0.1f, 0.025f), new Vector3(-0.012f, 0.205f, 0.02f), new Vector3(0.02f, 0.16f, 0.03f) })
                            mb.Ellipsoid(pz, new Vector3(0.014f, 0.008f, 0.012f), 6, 4);                                  // patina
                        grip = new Vector3(0, 0.05f, 0); gripEuler = Vector3.zero;
                        break;
                    }
                case "Bookend":
                    mb.Set(S.Marble, new Color(0.9f, 0.88f, 0.85f)); mb.BevelBox(new Vector3(0, 0.01f, 0), new Vector3(0.12f, 0.02f, 0.14f), 0.004f); mb.BevelBox(new Vector3(0, 0.09f, -0.06f), new Vector3(0.12f, 0.16f, 0.025f), 0.004f);
                    mb.Set(S.Gold, Color.white); mb.Box(new Vector3(0, 0.12f, -0.047f), new Vector3(0.06f, 0.02f, 0.002f));
                    grip = new Vector3(0, 0.09f, -0.06f); break;
                case "Decanter":
                    mb.Set(S.Glass, new Color(0.9f, 0.95f, 1f)); mb.Lathe(new[] { new Vector2(0.05f, 0), new Vector2(0.075f, 0.05f), new Vector2(0.07f, 0.14f), new Vector2(0.02f, 0.2f), new Vector2(0.022f, 0.24f) }, 16, true);
                    mb.Set(S.Glow, new Color(0.45f, 0.08f, 0.1f), MansionMats.GlowData(0.03f, 0, 0, -1)); mb.Lathe(new[] { new Vector2(0.045f, 0.005f), new Vector2(0.066f, 0.05f), new Vector2(0.062f, 0.09f) }, 14, true, true);
                    mb.Set(S.Glass, Color.white); mb.Sphere(new Vector3(0, 0.26f, 0), 0.025f, 10, 6);
                    grip = new Vector3(0, 0.21f, 0); gripEuler = new Vector3(90, 0, 0); break;
                case "IcePick":
                    mb.Set(S.WoodDark, Color.white); mb.Push(new Vector3(0, 0.014f, 0), Quaternion.Euler(90, 0, 0), Vector3.one); mb.Lathe(new[] { new Vector2(0.012f, 0), new Vector2(0.016f, 0.03f), new Vector2(0.013f, 0.09f), new Vector2(0.008f, 0.095f) }, 10, true, true); mb.Pop();
                    mb.Set(S.Steel, Color.white); mb.Rod(new Vector3(0, 0.014f, 0.095f), new Vector3(0, 0.014f, s), 0.0035f, 6, true);
                    grip = new Vector3(0, 0.014f, 0.05f); break;
                case "GardenShears":
                    mb.Set(S.Steel, Color.white);
                    foreach (float a in new[] { -5f, 5f }) { mb.Push(Vector3.up * 0.01f, a); mb.Box(new Vector3(0, 0, s * 0.62f), new Vector3(0.022f, 0.005f, s * 0.45f)); mb.Pop(); }
                    mb.Set(S.WoodLight, new Color(0.75f, 0.55f, 0.35f));
                    foreach (float a in new[] { -9f, 9f }) { mb.Push(Vector3.up * 0.012f, a); mb.Push(Vector3.zero, Quaternion.Euler(90, 0, 0), Vector3.one); mb.Cyl(new Vector3(0, 0, 0), 0.012f, s * 0.38f, 8); mb.Pop(); mb.Pop(); }
                    grip = new Vector3(0, 0.012f, 0.1f); break;
                case "Crowbar":
                    mb.Set(S.Iron, new Color(0.18f, 0.18f, 0.2f));
                    mb.Rod(new Vector3(0, 0.012f, 0.0f), new Vector3(0, 0.012f, s - 0.08f), 0.011f, 6, true);
                    mb.Rod(new Vector3(0, 0.012f, s - 0.08f), new Vector3(0.05f, 0.012f, s - 0.02f), 0.011f, 6, true);
                    mb.Rod(new Vector3(0.05f, 0.012f, s - 0.02f), new Vector3(0.09f, 0.012f, s - 0.05f), 0.009f, 6, true);
                    mb.Set(S.GlossPaint, new Color(0.7f, 0.1f, 0.08f)); mb.Rod(new Vector3(0, 0.012f, 0.02f), new Vector3(0, 0.012f, 0.2f), 0.013f, 6, true);
                    grip = new Vector3(0, 0.012f, 0.1f); break;
                case "Iron":
                    mb.Set(S.Iron, new Color(0.2f, 0.2f, 0.22f));
                    mb.Push(new Vector3(0, 0, 0), 0); mb.Prism(new System.Collections.Generic.List<Vector2> { new Vector2(-0.055f, -0.1f), new Vector2(0.0f, 0.12f), new Vector2(0.055f, -0.1f) }, 0, 0.05f, true, true); mb.Pop();
                    mb.Set(S.WoodDark, Color.white); mb.Bar(new Vector3(0, 0.1f, -0.06f), new Vector3(0, 0.1f, 0.06f), 0.025f, 0.025f);
                    mb.Set(S.Iron, Color.white); mb.Rod(new Vector3(0, 0.05f, -0.06f), new Vector3(0, 0.1f, -0.06f), 0.007f, 5, false); mb.Rod(new Vector3(0, 0.05f, 0.06f), new Vector3(0, 0.1f, 0.06f), 0.007f, 5, false);
                    grip = new Vector3(0, 0.1f, 0); break;
                case "Scalpel":
                    mb.Set(S.Steel, Color.white); mb.Box(new Vector3(0, 0.004f, 0.045f), new Vector3(0.009f, 0.005f, 0.09f));
                    mb.Push(new Vector3(0, 0.004f, 0), 0); mb.Prism(new System.Collections.Generic.List<Vector2> { new Vector2(0.004f, 0.09f), new Vector2(0.001f, s), new Vector2(-0.004f, 0.095f) }, 0, 0.001f, true, true); mb.Pop();
                    grip = new Vector3(0, 0.004f, 0.045f); break;
                case "CueStick":
                    mb.Push(new Vector3(0, 0.02f, 0), Quaternion.Euler(90, 0, 0), Vector3.one);
                    mb.Set(S.WoodDark, Color.white); mb.Lathe(new[] { new Vector2(0.016f, 0), new Vector2(0.015f, s * 0.35f) }, 10, true, false);
                    mb.Set(S.WoodLight, Color.white); mb.Lathe(new[] { new Vector2(0.015f, s * 0.35f), new Vector2(0.007f, s - 0.01f) }, 10, false, false);
                    mb.Set(S.Plaster, new Color(0.3f, 0.5f, 0.9f)); mb.Lathe(new[] { new Vector2(0.007f, s - 0.01f), new Vector2(0.006f, s) }, 8, false, true);
                    mb.Pop(); grip = new Vector3(0, 0.02f, 0.2f); break;
                case "SkinningKnife":
                    mb.Set(S.Bone, new Color(0.9f, 0.85f, 0.75f)); mb.BevelBox(new Vector3(0, 0.012f, 0.055f), new Vector3(0.026f, 0.022f, 0.11f), 0.007f);
                    mb.Set(S.Steel, Color.white); mb.Push(new Vector3(0, 0.004f, 0), 0);
                    mb.Prism(new System.Collections.Generic.List<Vector2> { new Vector2(0.012f, 0.11f), new Vector2(0.02f, 0.2f), new Vector2(0.0f, s), new Vector2(-0.012f, 0.2f), new Vector2(-0.01f, 0.11f) }, 0, 0.004f, true, true); mb.Pop();
                    grip = new Vector3(0, 0.012f, 0.055f); break;
                case "CurtainCord":
                    mb.Set(S.Velvet, new Color(0.55f, 0.08f, 0.12f));
                    for (int i = 0; i < 3; i++) mb.Torus(new Vector3(0, 0.01f + i * 0.016f, 0), 0.09f - i * 0.006f, 0.009f, 16, 5);
                    mb.Set(S.Gold, new Color(1f, 0.82f, 0.4f)); mb.Ellipsoid(new Vector3(0.1f, 0.03f, 0.02f), new Vector3(0.02f, 0.035f, 0.02f), 8, 6);
                    grip = new Vector3(0.09f, 0.03f, 0); break;
                case "PianoWire":
                    mb.Set(S.Steel, Color.white); for (int i = 0; i < 4; i++) mb.Torus(new Vector3(0, 0.003f + i * 0.003f, 0), 0.035f - i * 0.002f, 0.0015f, 16, 3);
                    mb.Set(S.WoodDark, Color.white); mb.Cyl(new Vector3(0.04f, 0, 0), 0.006f, 0.02f, 6); mb.Cyl(new Vector3(-0.04f, 0, 0), 0.006f, 0.02f, 6);
                    grip = new Vector3(0.035f, 0.006f, 0); break;
                case "ExtensionCord":
                    mb.Set(S.Rubber, new Color(0.12f, 0.12f, 0.12f)); for (int i = 0; i < 4; i++) mb.Torus(new Vector3(0, 0.008f + i * 0.012f, 0), 0.1f - i * 0.004f, 0.006f, 16, 4);
                    mb.Set(S.Plastic, new Color(0.92f, 0.9f, 0.85f)); mb.BevelBox(new Vector3(0.13f, 0.02f, 0), new Vector3(0.05f, 0.035f, 0.1f), 0.008f);
                    mb.Set(S.Brass, Color.white); mb.Box(new Vector3(0.13f, 0.02f, -0.06f), new Vector3(0.006f, 0.012f, 0.02f));
                    grip = new Vector3(0.1f, 0.03f, 0); break;
                case "Pliers":
                    mb.Set(S.Steel, Color.white); foreach (float a in new[] { -6f, 6f }) { mb.Push(Vector3.up * 0.008f, a); mb.Box(new Vector3(0, 0, s * 0.8f), new Vector3(0.012f, 0.008f, s * 0.35f)); mb.Pop(); }
                    mb.Set(S.Rubber, new Color(0.75f, 0.1f, 0.08f)); foreach (float a in new[] { -10f, 10f }) { mb.Push(Vector3.up * 0.01f, a); mb.BevelBox(new Vector3(0, 0, s * 0.3f), new Vector3(0.016f, 0.014f, s * 0.55f), 0.005f); mb.Pop(); }
                    grip = new Vector3(0, 0.01f, s * 0.3f); break;
                case "Pillow":
                    mb.Set(S.Linen, new Color(0.95f, 0.94f, 0.9f)); mb.BevelBox(new Vector3(0, 0.06f, 0), new Vector3(0.55f, 0.12f, 0.36f), 0.05f);
                    mb.Set(S.Linen, new Color(0.9f, 0.85f, 0.85f)); mb.Ellipsoid(new Vector3(0, 0.121f, 0), new Vector3(0.09f, 0.004f, 0.06f), 10, 3);
                    grip = new Vector3(0, 0.06f, 0); break;
                case "DevChemical":
                    mb.Set(S.Glass, new Color(0.35f, 0.18f, 0.05f)); mb.Lathe(new[] { new Vector2(0.028f, 0), new Vector2(0.03f, 0.07f), new Vector2(0.012f, 0.09f), new Vector2(0.012f, 0.1f) }, 12, true);
                    mb.Set(S.Paper, new Color(0.95f, 0.9f, 0.3f)); mb.Cyl(new Vector3(0, 0.02f, 0), 0.0305f, 0.035f, 12, false);
                    mb.Set(S.Rubber, Color.black); mb.Cyl(new Vector3(0, 0.1f, 0), 0.014f, 0.015f, 10);
                    grip = new Vector3(0, 0.05f, 0); break;
                case "Foxglove":
                    mb.Set(S.Leaf, new Color(0.25f, 0.45f, 0.2f)); for (int i = 0; i < 5; i++) { float a = i * 1.25f; mb.Ellipsoid(new Vector3(Mathf.Cos(a) * 0.03f, 0.008f, Mathf.Sin(a) * 0.03f), new Vector3(0.035f, 0.006f, 0.02f), 8, 3); }
                    mb.Set(S.GlossPaint, new Color(0.65f, 0.25f, 0.6f)); for (int i = 0; i < 4; i++) mb.Ellipsoid(new Vector3(0.01f * i - 0.015f, 0.018f, 0.05f), new Vector3(0.009f, 0.009f, 0.014f), 6, 4);
                    grip = new Vector3(0, 0.01f, 0); break;
                case "Hacksaw":
                    // C-frame, thin blade along the bottom, wooden pistol grip at the back
                    mb.Set(S.GlossPaint, new Color(0.15f, 0.25f, 0.55f));
                    mb.Rod(new Vector3(0, 0.1f, 0.06f), new Vector3(0, 0.1f, s - 0.02f), 0.006f, 6, true);
                    mb.Rod(new Vector3(0, 0.1f, s - 0.02f), new Vector3(0, 0.015f, s - 0.02f), 0.006f, 6, true);
                    mb.Rod(new Vector3(0, 0.1f, 0.06f), new Vector3(0, 0.015f, 0.06f), 0.006f, 6, true);
                    mb.Set(S.Steel, Color.white); mb.Box(new Vector3(0, 0.02f, s * 0.5f + 0.02f), new Vector3(0.002f, 0.014f, s - 0.07f));
                    mb.Set(S.WoodDark, Color.white); mb.BevelBox(new Vector3(0, 0.05f, 0.025f), new Vector3(0.024f, 0.09f, 0.04f), 0.008f);
                    grip = new Vector3(0, 0.05f, 0.025f); break;
                case "BoneSaw":
                    // butcher's bow saw: wide steel blade, open bow, pale bone-coloured handle
                    mb.Set(S.Steel, new Color(0.92f, 0.92f, 0.95f)); mb.Box(new Vector3(0, 0.025f, s * 0.55f), new Vector3(0.0025f, 0.03f, s * 0.8f));
                    mb.Set(S.Iron, new Color(0.3f, 0.3f, 0.32f));
                    mb.Rod(new Vector3(0, 0.04f, s * 0.15f), new Vector3(0, 0.12f, s * 0.35f), 0.007f, 6, true);
                    mb.Rod(new Vector3(0, 0.12f, s * 0.35f), new Vector3(0, 0.12f, s * 0.8f), 0.007f, 6, true);
                    mb.Rod(new Vector3(0, 0.12f, s * 0.8f), new Vector3(0, 0.04f, s - 0.01f), 0.007f, 6, true);
                    mb.Set(S.Bone, new Color(0.9f, 0.87f, 0.78f)); mb.BevelBox(new Vector3(0, 0.04f, 0.05f), new Vector3(0.028f, 0.05f, 0.1f), 0.01f);
                    grip = new Vector3(0, 0.04f, 0.05f); break;
                case "SeveredPart":
                    // interim look (until Game/Gore renders the pieces): a long bundle in a stained sheet, tied at both ends
                    mb.Set(S.Linen, new Color(0.78f, 0.74f, 0.68f)); mb.Ellipsoid(new Vector3(0, 0.07f, 0), new Vector3(0.1f, 0.07f, s * 0.5f), 12, 8);
                    mb.Set(S.GlossPaint, new Color(0.32f, 0.03f, 0.04f));
                    mb.Ellipsoid(new Vector3(0.03f, 0.11f, 0.05f), new Vector3(0.05f, 0.03f, 0.09f), 8, 4); mb.Ellipsoid(new Vector3(-0.04f, 0.1f, -0.12f), new Vector3(0.035f, 0.03f, 0.05f), 8, 4);
                    mb.Set(S.Leather, new Color(0.35f, 0.27f, 0.2f));
                    foreach (float z in new[] { -s * 0.36f, s * 0.36f }) mb.Box(new Vector3(0, 0.07f, z), new Vector3(0.15f, 0.105f, 0.018f));
                    grip = new Vector3(0, 0.07f, 0); break;
            }
        }
    }

    /// <summary>Forwards collisions of a loose item (hits on people after a throw or shove).</summary>
    public sealed class ItemImpact : MonoBehaviour
    {
        public string ItemId; Rigidbody _rb;
        void OnCollisionEnter(Collision col) { if (_rb == null) _rb = GetComponent<Rigidbody>(); if (col.relativeVelocity.sqrMagnitude > 4f) PhysicsBridge.ItemHitActor(ItemId, _rb, col); }
    }
}
