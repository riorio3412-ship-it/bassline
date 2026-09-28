using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// A door opening with frame, animated leaves (single / double / cage gate) and lock visuals (chain + padlock,
    /// red keyhole) that read as locked up close. Local frame: origin at the opening centre on the floor, +Z toward the
    /// swing side (room A), +X along the wall.
    /// </summary>
    public sealed partial class DoorView : MonoBehaviour
    {
        public int DoorId;
        public bool IsOpen, IsLocked;
        public float Width, Height;
        public bool Cage;
        readonly List<Transform> _leaves = new List<Transform>();
        readonly List<float> _sign = new List<float>();
        GameObject _lockVis, _keyRed, _keyGold;
        float _t, _target;
        float _swing = 1f;          // +1 swings toward room A (+Z), -1 the other way (chosen so the leaf never sweeps through the player)
        const float OpenAngle = 96f;

        public void Set(bool open, bool locked)
        {
            IsOpen = open; IsLocked = locked;
            if (open && _t < 0.05f)
            {
                var cam = Camera.main;
                if (cam != null && (cam.transform.position - transform.position).sqrMagnitude < 36f) _swing = transform.InverseTransformPoint(cam.transform.position).z > 0.05f ? -1f : 1f;
            }
            _target = open ? 1f : 0f;
            if (_lockVis != null) _lockVis.SetActive(locked && !open);
            if (_keyRed != null) _keyRed.SetActive(locked);
            if (_keyGold != null) _keyGold.SetActive(!locked);
            enabled = true;
        }

        GameObject _growth;
        /// <summary>The hungry house grows the doorway shut: knotted flesh-dark vines with thorns across the frame, faintly pulsing.</summary>
        public void SetSealed(bool sealedShut)
        {
            if (!sealedShut) { if (_growth != null) Destroy(_growth); _growth = null; return; }
            if (_growth != null) return;
            var mb = new MeshBuilder(); var rnd = new System.Random(DoorId * 131 + 7);
            float W = Width, H = Height;
            for (int k = 0; k < 14; k++)
            {
                var pts = new List<Vector3>(); float x0 = (float)(rnd.NextDouble() - 0.5) * W, y0 = (float)rnd.NextDouble() * 0.3f;
                for (int s = 0; s < 6; s++) { float t = s / 5f; pts.Add(new Vector3(Mathf.Lerp(x0, (float)(rnd.NextDouble() - 0.5) * W * 1.1f, t) + Mathf.Sin(t * 7 + k) * 0.12f, Mathf.Lerp(y0, H * (0.6f + 0.45f * (float)rnd.NextDouble()), t), 0.05f + Mathf.Sin(t * 5 + k * 2) * 0.05f)); }
                mb.Set(S.Flesh, new Color(0.32f, 0.05f, 0.09f)); mb.Tube(pts, 0.035f + (float)rnd.NextDouble() * 0.04f, 6, true);
                mb.Set(S.Bone, new Color(0.85f, 0.8f, 0.7f));
                for (int s = 1; s < pts.Count - 1; s++) { var p = pts[s]; var d = new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f, 0.6f).normalized; mb.Rod(p, p + d * 0.09f, 0.012f, 4, false); }
            }
            mb.Set(S.Glow, new Color(0.9f, 0.1f, 0.2f), MansionMats.GlowData(1.6f, 0.5f, 0, -1));
            for (int k = 0; k < 5; k++) mb.Sphere(new Vector3((float)(rnd.NextDouble() - 0.5) * W, (float)rnd.NextDouble() * H * 0.9f, 0.1f), 0.04f + (float)rnd.NextDouble() * 0.03f, 8, 6);
            _growth = new GameObject("Overgrowth"); _growth.transform.SetParent(transform, false);
            var mesh = mb.ToMesh("Overgrowth", out var slots);
            _growth.AddComponent<MeshFilter>().sharedMesh = mesh; _growth.AddComponent<MeshRenderer>().sharedMaterials = MansionMats.Materials(slots);
            var mirror = Instantiate(_growth, transform); mirror.transform.localRotation = Quaternion.Euler(0, 180, 0); mirror.transform.SetParent(_growth.transform, true);
        }

        /// <summary>Jump to the target state without animation.</summary>
        public void Snap() { _t = _target; Apply(); }

        void Update()
        {
            if (Mathf.Abs(_t - _target) < 1e-3f) { _t = _target; Apply(); enabled = false; return; }
            _t = Mathf.MoveTowards(_t, _target, Time.deltaTime / 0.7f);
            Apply();
        }

        void Apply()
        {
            float e = _t * _t * (3 - 2 * _t);
            for (int i = 0; i < _leaves.Count; i++)
            {
                if (Cage) _leaves[i].localScale = new Vector3(Mathf.Lerp(1f, 0.12f, e), 1, 1);
                else _leaves[i].localRotation = Quaternion.Euler(0, _sign[i] * _swing * OpenAngle * e, 0);
            }
        }

        // ------------------------------------------------------------------ builder
        internal static DoorView Build(MansionView view, Door d, float height, Transform parent, MansionPalette palA, Room roomA, Room roomB, Vector3 intoA)
        {
            var go = new GameObject("Door_" + d.Id);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(d.Pos.x, view.Layout.FloorY(d.Pos.f), d.Pos.z);
            go.transform.rotation = Quaternion.LookRotation(intoA, Vector3.up);
            var dv = go.AddComponent<DoorView>();
            dv.DoorId = d.Id; dv.Width = d.Width; dv.Height = height;
            float W = d.Width, H = height;
            bool elevator = roomA.Type == RoomType.Elevator || (roomB != null && roomB.Type == RoomType.Elevator);
            bool mystery = RoomInfo.IsMystery(roomA.Type) || (roomB != null && RoomInfo.IsMystery(roomB.Type));
            bool white = roomA.Type == RoomType.WhiteDoors || (roomB != null && roomB.Type == RoomType.WhiteDoors);
            bool utility = roomA.Floor < 0 && (roomA.Type == RoomType.MachineRoom || roomA.Type == RoomType.PowerRoom || roomA.Type == RoomType.BoilerRoom || roomA.Type == RoomType.Storage || roomA.Type == RoomType.Laundry || roomA.Type == RoomType.WaterRoom);
            dv.Cage = elevator;
            // a resident's room behind this door (it may be on either side) and the face that looks onto the corridor
            Room owned = roomA.Type == RoomType.Bedroom && !string.IsNullOrEmpty(roomA.Owner) ? roomA
                       : roomB != null && roomB.Type == RoomType.Bedroom && !string.IsNullOrEmpty(roomB.Owner) ? roomB : null;
            var own = owned != null && !elevator ? OwnerStyles.Get(owned.Owner) : null;
            var ownPal = own != null ? MansionPalette.Get(own.Palette) : palA;
            float cs = owned == roomA ? -1f : 1f;   // local z sign of the corridor face (away from the resident's room)

            // ---- frame (static)
            var fb = new MeshBuilder();
            int trimSlot = white ? S.PlasterWhite : utility ? S.PaintedMetal : S.WoodDark;
            Color trimTint = white ? new Color(1.15f, 1.15f, 1.15f) : utility ? new Color(0.45f, 0.5f, 0.45f) : Color.Lerp(Color.white, palA.Wood * 2f, 0.4f);
            fb.Set(trimSlot, trimTint);
            float fw = 0.13f, ft = 0.045f;
            foreach (float side in new[] { 1f, -1f })
            {
                float z = side * (0.1f + ft * 0.5f);
                fb.Box(new Vector3(-W / 2 - fw / 2, H / 2 + 0.02f, z), new Vector3(fw, H + 0.04f, ft));
                fb.Box(new Vector3(W / 2 + fw / 2, H / 2 + 0.02f, z), new Vector3(fw, H + 0.04f, ft));
                fb.Box(new Vector3(0, H + fw / 2, z), new Vector3(W + fw * 2, fw, ft));
                // plinth blocks
                fb.Box(new Vector3(-W / 2 - fw / 2, 0.12f, z + side * 0.01f), new Vector3(fw + 0.03f, 0.24f, ft + 0.02f));
                fb.Box(new Vector3(W / 2 + fw / 2, 0.12f, z + side * 0.01f), new Vector3(fw + 0.03f, 0.24f, ft + 0.02f));
                if (!utility)
                {
                    // cornice + pediment on top
                    fb.Box(new Vector3(0, H + fw + 0.03f, z + side * 0.03f), new Vector3(W + fw * 2 + 0.16f, 0.06f, ft + 0.06f));
                    fb.Set(white ? S.PlasterWhite : S.Gold, white ? trimTint : palA.Trim);
                    fb.Box(new Vector3(0, H + fw + 0.08f, z + side * 0.02f), new Vector3(W + fw * 2 + 0.06f, 0.04f, ft + 0.03f));
                    if (!white)
                    {
                        // an ornamental eye / moon medallion above the door
                        fb.Push(new Vector3(0, H + fw + 0.22f, z + side * 0.02f), Quaternion.Euler(side > 0 ? -90 : 90, 0, 0), Vector3.one);
                        fb.Lathe(new[] { new Vector2(0.001f, 0f), new Vector2(0.11f, 0.01f), new Vector2(0.12f, 0.03f), new Vector2(0.06f, 0.05f), new Vector2(0.001f, 0.055f) }, 14);
                        fb.Pop();
                    }
                    fb.Set(trimSlot, trimTint);
                }
            }
            // lining inside the opening
            fb.Set(trimSlot, trimTint * 0.9f);
            fb.Box(new Vector3(-W / 2 + 0.015f, H / 2, 0), new Vector3(0.03f, H, 0.2f));
            fb.Box(new Vector3(W / 2 - 0.015f, H / 2, 0), new Vector3(0.03f, H, 0.2f));
            fb.Box(new Vector3(0, H - 0.015f, 0), new Vector3(W, 0.03f, 0.2f));
            // threshold
            fb.Set(S.Marble, Color.Lerp(Color.white, palA.FloorA, 0.3f));
            fb.Box(new Vector3(0, 0.008f, 0), new Vector3(W, 0.016f, 0.24f));
            // a string of paper pennants across the head of the frame (corridor side), above the leaf's swing
            if (own != null && own.Hang == "bunting")
            {
                float bz = cs * 0.215f, y0 = H + 0.2f; int nfl = 9;
                fb.Set(S.Linen, new Color(0.85f, 0.82f, 0.74f));
                var str = new List<Vector3>();
                for (int k = 0; k <= 12; k++) { float t = k / 12f; str.Add(new Vector3(Mathf.Lerp(-W / 2 - 0.1f, W / 2 + 0.1f, t), y0 - Mathf.Sin(t * Mathf.PI) * 0.05f, bz)); }
                fb.Tube(str, 0.003f, 4);
                for (int k = 0; k < nfl; k++)
                {
                    float t = (k + 0.5f) / nfl; float x = Mathf.Lerp(-W / 2 - 0.05f, W / 2 + 0.05f, t), y = y0 - Mathf.Sin(t * Mathf.PI) * 0.05f;
                    fb.Set(S.Paper, k % 3 == 0 ? own.Sig : k % 3 == 1 ? new Color(0.93f, 0.9f, 0.84f) : Color.Lerp(own.Sig, new Color(0.3f, 0.3f, 0.35f), 0.5f));
                    fb.TriAuto(new Vector3(x - 0.045f, y, bz), new Vector3(x + 0.045f, y, bz), new Vector3(x, y - 0.1f, bz), new Vector3(0, 0, cs));
                    fb.TriAuto(new Vector3(x - 0.045f, y, bz - cs * 0.001f), new Vector3(x + 0.045f, y, bz - cs * 0.001f), new Vector3(x, y - 0.1f, bz - cs * 0.001f), new Vector3(0, 0, -cs));
                }
            }
            var frameMesh = fb.ToMesh("DoorFrame", out var fslots);
            var fgo = new GameObject("Frame"); fgo.transform.SetParent(go.transform, false);
            fgo.AddComponent<MeshFilter>().sharedMesh = frameMesh;
            var fmr = fgo.AddComponent<MeshRenderer>(); fmr.sharedMaterials = MansionMats.Materials(fslots);

            // ---- leaves
            int leafCount = elevator ? 2 : (W >= 1.9f ? 2 : 1);
            float lw = W / leafCount - (leafCount == 2 ? 0.004f : 0.015f);   // double leaves meet with a 2 mm gap (no light seam)
            Color leafTint;
            int leafSlot;
            if (white) { leafSlot = S.PlasterWhite; leafTint = new Color(1.12f, 1.12f, 1.12f); }
            else if (utility) { leafSlot = S.PaintedMetal; leafTint = new Color(0.4f, 0.45f, 0.4f); }
            // dark stained wood everywhere; a room's colour appears only in the inset panels (bedrooms, mystery rooms)
            Color? inset = null;
            if (own != null) { leafSlot = S.WoodCherry; leafTint = Color.Lerp(Color.white, ownPal.Wood * 2.2f, 0.3f); inset = own.Door; }   // the resident's signature colour
            else if (roomA.Type == RoomType.Bedroom) { leafSlot = S.WoodCherry; leafTint = Color.Lerp(Color.white, palA.Wood * 2.2f, 0.3f); inset = Color.Lerp(Color.Lerp(palA.Fabric, palA.Wall, 0.3f), new Color(0.2f, 0.12f, 0.1f), 0.45f); }
            else if (mystery) { leafSlot = S.WoodCherry; leafTint = Color.Lerp(Color.white, palA.Wood * 2.2f, 0.3f); inset = Color.Lerp(Color.Lerp(palA.Neon, palA.Wall, 0.55f), new Color(0.15f, 0.1f, 0.1f), 0.4f); }
            else { leafSlot = S.WoodCherry; leafTint = Color.Lerp(Color.white, palA.Wood * 2.2f, 0.35f); }
            for (int i = 0; i < leafCount; i++)
            {
                float hingeX = leafCount == 1 ? -W / 2 + 0.01f : (i == 0 ? -W / 2 + 0.01f : W / 2 - 0.01f);
                float dir = hingeX < 0 ? 1f : -1f;
                var pivot = new GameObject("Leaf" + i).transform;
                pivot.SetParent(go.transform, false);
                pivot.localPosition = new Vector3(hingeX, 0, elevator ? 0.06f : 0.0f);
                var lb = new MeshBuilder();
                if (elevator) BuildCageLeaf(lb, lw, H - 0.02f, dir);
                else BuildPanelLeaf(lb, lw, H - 0.015f, dir, leafSlot, leafTint, palA, own == null && roomA.Type == RoomType.Bedroom, inset, leafCount == 2 && i == 0);
                // the resident's face on the first leaf: lit plaque, emblem, whatever they hung on it (DoorView.Owners)
                const float faceZ = 0.045f;   // proud of the inset panels
                if (own != null && i == 0)
                {
                    float cxl = dir * lw / 2, kxl = dir * (lw / 2 - 0.1f);
                    lb.Push(Matrix4x4.TRS(new Vector3(cxl, 0, cs * faceZ), Quaternion.Euler(0, cs < 0 ? 180f : 0f, 0), Vector3.one));
                    OwnerPlaque(lb, own, 0f, owned.Circuit);
                    lb.Push(new Vector3(0, EmblemY, 0), 0); OwnerEmblem(lb, own, ownPal, owned.Circuit); lb.Pop();
                    OwnerHang(lb, own, cs < 0 ? -kxl : kxl, 0.085f - faceZ, 0f);
                    lb.Pop();
                }
                var lm = lb.ToMesh("Leaf", out var lslots);
                var lgo = new GameObject("mesh"); lgo.transform.SetParent(pivot, false);
                lgo.AddComponent<MeshFilter>().sharedMesh = lm;
                var lmr = lgo.AddComponent<MeshRenderer>(); lmr.sharedMaterials = MansionMats.Materials(lslots);
                var bc = lgo.AddComponent<BoxCollider>();
                bc.center = new Vector3(dir * lw / 2, H / 2, 0); bc.size = new Vector3(lw, H, elevator ? 0.04f : 0.05f);
                dv._leaves.Add(pivot);
                // swing toward +Z (room A): positive yaw rotates +X toward -Z, so left hinge uses negative angle
                dv._sign.Add(elevator ? 1f : -dir);
                // the resident's name on the lit plaque, facing the corridor (TMP text reads from its -Z side)
                if (own != null && i == 0)
                {
                    var tgo = new GameObject("NamePlate"); tgo.transform.SetParent(pivot, false);
                    tgo.transform.localPosition = new Vector3(dir * lw / 2, PlaqueY + 0.008f, cs * (faceZ + 0.0165f));
                    tgo.transform.localRotation = cs < 0 ? Quaternion.identity : Quaternion.Euler(0, 180, 0);
                    var tm = tgo.AddComponent<TMPro.TextMeshPro>();
                    var font = BL23.Game.Fonts.Serif; if (font != null) tm.font = font;
                    tm.text = Cast.NameOf(owned.Owner); tm.alignment = TMPro.TextAlignmentOptions.Center;
                    tm.rectTransform.sizeDelta = new Vector2(PlaqueW - 0.1f, PlaqueH - 0.065f); tm.enableAutoSizing = true; tm.fontSizeMin = 0.1f; tm.fontSizeMax = 0.8f;
                    tm.fontStyle = TMPro.FontStyles.Bold; tm.characterSpacing = 6f;
                    tm.color = new Color(0.1f, 0.07f, 0.05f); tm.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                    var tmr = tgo.GetComponent<MeshRenderer>(); if (tmr != null) tmr.shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            // ---- lock visuals: chain + padlock across the handles (locked & closed), keyhole glow
            // an iron bar in two brackets across a double door, or a hasp over the frame on a single leaf, and a padlock
            var lk = new MeshBuilder();
            float hy = 1.02f;
            var ironC = new Color(0.2f, 0.19f, 0.18f);
            lk.Set(S.Iron, ironC);
            float px;
            if (leafCount == 2)
            {
                lk.Box(new Vector3(0, hy, 0.105f), new Vector3(W * 0.72f, 0.055f, 0.03f));
                foreach (float bx in new[] { -W * 0.3f, W * 0.3f }) { lk.Box(new Vector3(bx, hy, 0.085f), new Vector3(0.07f, 0.14f, 0.02f)); lk.Box(new Vector3(bx, hy - 0.05f, 0.11f), new Vector3(0.07f, 0.02f, 0.05f)); }
                px = 0f;
            }
            else
            {
                lk.Box(new Vector3(W / 2 - 0.05f, hy, 0.09f), new Vector3(0.26f, 0.045f, 0.012f));   // hasp from the leaf over the frame
                lk.Box(new Vector3(W / 2 + 0.07f, hy, 0.1f), new Vector3(0.03f, 0.06f, 0.03f));     // the staple on the frame
                px = W / 2 + 0.07f;
            }
            lk.Set(S.Brass, new Color(0.62f, 0.5f, 0.34f));
            lk.BevelBox(new Vector3(px, hy - 0.1f, 0.125f), new Vector3(0.075f, 0.07f, 0.03f), 0.008f);
            lk.Set(S.Steel, new Color(0.55f, 0.56f, 0.57f));
            lk.Push(new Vector3(px, hy - 0.065f, 0.125f), Quaternion.Euler(90, 0, 0), Vector3.one); lk.Torus(Vector3.zero, 0.026f, 0.006f, 10, 4, 0, 180); lk.Pop();
            var lkm = lk.ToMesh("Lock", out var lkslots);
            dv._lockVis = new GameObject("Chain"); dv._lockVis.transform.SetParent(go.transform, false);
            dv._lockVis.AddComponent<MeshFilter>().sharedMesh = lkm;
            dv._lockVis.AddComponent<MeshRenderer>().sharedMaterials = MansionMats.Materials(lkslots);
            dv._lockVis.SetActive(false);
            if (!elevator)
            {
                dv._keyRed = KeyGlow(go.transform, W, leafCount, new Color(1f, 0.05f, 0.1f), 2.2f, roomA.Circuit);
                dv._keyGold = KeyGlow(go.transform, W, leafCount, new Color(1f, 0.75f, 0.3f), 0.5f, roomA.Circuit);
            }
            dv.Set(d.Open, d.Locked);
            dv.Snap();
            return dv;
        }

        static GameObject KeyGlow(Transform parent, float W, int leaves, Color c, float intensity, int circuit)
        {
            var mb = new MeshBuilder();
            mb.Set(S.Glow, c, MansionMats.GlowData(intensity, 0.2f, 0, -1));
            float x = leaves == 2 ? 0.07f : W / 2 - 0.13f;
            foreach (float side in new[] { 1f, -1f })
            {
                mb.Box(new Vector3(x, 0.93f, side * 0.052f), new Vector3(0.018f, 0.04f, 0.004f));
                if (leaves == 2) mb.Box(new Vector3(-x, 0.93f, side * 0.052f), new Vector3(0.018f, 0.04f, 0.004f));
            }
            var go = new GameObject("Keyhole"); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Keyhole", out var slots);
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = MansionMats.Materials(slots); mr.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Raised-panel leaf, hinge at local x=0 extending along +dir.</summary>
        static void BuildPanelLeaf(MeshBuilder mb, float w, float h, float dir, int slot, Color tint, MansionPalette pal, bool nameplate, Color? inset = null, bool astragal = false)
        {
            float t = 0.05f;
            float cx = dir * w / 2;
            mb.Set(slot, tint);
            mb.Box(new Vector3(cx, h / 2, 0), new Vector3(w, h, t));
            // panels (2 columns x 3 rows, top ones arched-looking via smaller boxes)
            int cols = w > 0.8f ? 2 : 1;
            float pw = (w - 0.2f - (cols - 1) * 0.1f) / cols;
            float[] rows = { 0.18f, 0.95f, 1.65f };
            float[] rh = { 0.62f, 0.55f, h - 1.65f - 0.18f };
            foreach (float side in new[] { 1f, -1f })
                for (int c = 0; c < cols; c++)
                    for (int r = 0; r < 3; r++)
                    {
                        if (rh[r] < 0.2f) continue;
                        float px = dir * (0.1f + pw / 2 + c * (pw + 0.1f));
                        mb.Set(slot, tint * 0.82f);
                        mb.Box(new Vector3(px, rows[r] + rh[r] / 2, side * (t / 2 + 0.006f)), new Vector3(pw, rh[r], 0.012f));
                        if (inset.HasValue) mb.Set(S.WoodPainted, inset.Value); else mb.Set(slot, tint * 1.08f);
                        mb.Box(new Vector3(px, rows[r] + rh[r] / 2, side * (t / 2 + 0.014f)), new Vector3(pw - 0.1f, rh[r] - 0.1f, 0.012f));
                    }
            // handles (brass knob + rose) both sides
            mb.Set(S.Brass, Color.white);
            float hx = dir * (w - 0.1f);
            foreach (float side in new[] { 1f, -1f })
            {
                mb.Box(new Vector3(hx, 0.98f, side * (t / 2 + 0.008f)), new Vector3(0.05f, 0.22f, 0.016f));
                mb.Sphere(new Vector3(hx, 1.02f, side * (t / 2 + 0.06f)), 0.03f, 10, 6);
                mb.Rod(new Vector3(hx, 1.02f, side * (t / 2 + 0.01f)), new Vector3(hx, 1.02f, side * (t / 2 + 0.05f)), 0.009f, 6, false);
            }
            // hinges
            mb.Set(S.Brass, new Color(0.8f, 0.7f, 0.5f));
            foreach (float y in new[] { 0.3f, h * 0.5f, h - 0.3f }) mb.Cyl(new Vector3(0, y - 0.07f, 0), 0.014f, 0.14f, 6);
            // the astragal: a moulded strip on the first leaf covering the meeting edge of a double door
            if (astragal) { mb.Set(slot, tint * 0.9f); mb.Box(new Vector3(dir * (w + 0.012f), h / 2, -(t / 2 + 0.006f)), new Vector3(0.05f, h - 0.02f, 0.012f)); }
            if (nameplate)
            {
                // on the corridor side (-Z), where people read it; the name itself is set as text by Build
                mb.Set(S.Brass, new Color(0.72f, 0.58f, 0.38f));
                mb.BevelBox(new Vector3(cx, 1.55f, -(t / 2 + 0.006f)), new Vector3(0.24f, 0.085f, 0.008f), 0.003f);
                mb.Set(S.Paper, new Color(0.9f, 0.86f, 0.74f));
                mb.Box(new Vector3(cx, 1.55f, -(t / 2 + 0.0105f)), new Vector3(0.2f, 0.06f, 0.001f));
            }
        }

        static void BuildCageLeaf(MeshBuilder mb, float w, float h, float dir)
        {
            mb.Set(S.Brass, Color.white);
            int bars = Mathf.Max(4, (int)(w / 0.09f));
            for (int i = 0; i <= bars; i++)
            {
                float x = dir * (i / (float)bars) * w;
                mb.Box(new Vector3(x, h / 2, 0), new Vector3(0.02f, h, 0.02f));
            }
            mb.Box(new Vector3(dir * w / 2, 0.05f, 0), new Vector3(w, 0.05f, 0.03f));
            mb.Box(new Vector3(dir * w / 2, h - 0.05f, 0), new Vector3(w, 0.05f, 0.03f));
            // lattice diagonals
            for (int i = 0; i < bars; i += 2)
            {
                float x0 = dir * (i / (float)bars) * w, x1 = dir * ((i + 2) / (float)bars) * w;
                for (float y = 0.4f; y < h - 0.4f; y += 0.6f)
                {
                    mb.Bar(new Vector3(x0, y, 0.012f), new Vector3(x1, y + 0.3f, 0.012f), 0.012f);
                    mb.Bar(new Vector3(x1, y, -0.012f), new Vector3(x0, y + 0.3f, -0.012f), 0.012f);
                }
            }
        }
    }
}
