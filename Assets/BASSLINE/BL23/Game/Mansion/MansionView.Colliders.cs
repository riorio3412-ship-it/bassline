using System;
using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// Static colliders for tall decorative meshes (columns, newels, benches, cages, tall props) so the player and camera
    /// occlusion casts can't pass through them. A collider never covers a door opening (+ approach) or a nav cell the
    /// kernel considers walkable: it is shrunk toward its centre until it fits, or skipped.
    /// </summary>
    public sealed partial class MansionView
    {
        /// <summary>Decor colliders added, per room type (QA / reporting).</summary>
        public readonly Dictionary<string, int> DecorColliderCounts = new Dictionary<string, int>();
        public int DecorCollidersSkipped;
        Transform _decorColRoot;

        /// <summary>True when the XZ rect overlaps no walkable kernel cell and no door opening on that floor.</summary>
        internal bool DecorRectFree(int floor, RectF r)
        {
            var nav = Layout.Nav(floor);
            int i0 = nav.I(r.x0 + 1e-3f), i1 = nav.I(r.x1 - 1e-3f), j0 = nav.J(r.z0 + 1e-3f), j1 = nav.J(r.z1 - 1e-3f);
            for (int i = i0; i <= i1; i++)
                for (int j = j0; j <= j1; j++)
                {
                    if (i < 0 || j < 0 || i >= nav.NX || j >= nav.NZ) continue;
                    if (nav.Walkable(i + j * nav.NX)) return false;
                }
            foreach (var d in Layout.Doors)
            {
                if (d.Pos.f != floor) continue;
                float hw = d.Width * 0.5f + 0.15f, dep = 1.0f;
                var dr = d.AlongX ? new RectF(d.Pos.x - hw, d.Pos.z - dep, d.Pos.x + hw, d.Pos.z + dep) : new RectF(d.Pos.x - dep, d.Pos.z - hw, d.Pos.x + dep, d.Pos.z + hw);
                if (dr.Overlaps(r)) return false;
            }
            return true;
        }

        /// <summary>Largest centred shrink of r (scale 1 .. minScale) that is free; false if none.</summary>
        internal bool FitDecorRect(int floor, RectF r, float minScale, out RectF fitted)
        {
            fitted = r;
            for (float s = 1f; s >= minScale - 1e-3f; s -= 0.1f)
            {
                var q = new RectF(r.CX - r.W * 0.5f * s, r.CZ - r.D * 0.5f * s, r.CX + r.W * 0.5f * s, r.CZ + r.D * 0.5f * s);
                if (DecorRectFree(floor, q)) { fitted = q; return true; }
            }
            return false;
        }

        /// <summary>
        /// Add a static box collider for a decor piece occupying world bounds b (axis aligned). Applies the size rule
        /// (taller than 1.2 m and wider than 0.25 m, or hanging lower than 2.3 m) and the walkable/door rule.
        /// </summary>
        internal bool AddDecorCollider(RoomView rv, Bounds b, string label, bool force = false)
        {
            if (rv == null) return false;
            float floorY = rv.FloorY;
            bool tall = b.size.y >= 1.2f && Mathf.Max(b.size.x, b.size.z) >= 0.25f;
            bool lowHanging = b.min.y - floorY < 2.3f && b.min.y - floorY > 0.3f && Mathf.Max(b.size.x, b.size.z) >= 0.25f;
            if (!force && !tall && !lowHanging) return false;
            var r = new RectF(b.min.x, b.min.z, b.max.x, b.max.z);
            RectF f;
            if (force && rv.Room.Type == RoomType.Courtroom)
            {
                // the kernel's courtroom is a full rectangle but nobody walks outside the ring of stands (actors only go
                // elevator -> stand), so pieces beyond 7 m from the centre only need to respect the door/elevator openings
                f = r;
                float dist = new Vector2(r.CX - rv.Room.Rect.CX, r.CZ - rv.Room.Rect.CZ).magnitude;
                if (dist < 7f && !FitDecorRect(rv.Room.Floor, r, 0.4f, out f)) { DecorCollidersSkipped++; return false; }
            }
            else if (!FitDecorRect(rv.Room.Floor, r, 0.4f, out f)) { DecorCollidersSkipped++; return false; }
            if (_decorColRoot == null) { _decorColRoot = new GameObject("DecorColliders").transform; _decorColRoot.SetParent(_root, false); }
            var go = new GameObject("DC_" + label); go.transform.SetParent(_decorColRoot, false);
            go.isStatic = true;
            var bc = go.AddComponent<BoxCollider>();
            float y0 = Mathf.Max(b.min.y, floorY), y1 = b.max.y;
            bc.center = new Vector3(f.CX, (y0 + y1) * 0.5f, f.CZ);
            bc.size = new Vector3(Mathf.Max(0.05f, f.W), Mathf.Max(0.05f, y1 - y0), Mathf.Max(0.05f, f.D));
            string key = rv.Room.Type.ToString();
            DecorColliderCounts.TryGetValue(key, out int n); DecorColliderCounts[key] = n + 1;
            return true;
        }

        internal bool AddDecorCollider(RoomView rv, Vector3 center, Vector3 size, string label, bool force = false)
            => AddDecorCollider(rv, new Bounds(center, size), label, force);

        /// <summary>Footprint of a floor-standing decor piece is free for a full collider (used to choose standing vs hanging variants).</summary>
        internal bool DecorFootprintFree(RoomView rv, Vector3 center, float halfExtent) =>
            DecorRectFree(rv.Room.Floor, new RectF(center.x - halfExtent, center.z - halfExtent, center.x + halfExtent, center.z + halfExtent));
    }
}
