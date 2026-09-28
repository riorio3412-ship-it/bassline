using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// Small procedural modelling kit. Geometry is emitted into material "slots" (submeshes). UVs default to a
    /// world-scale planar projection (1 unit = 1 m) along the dominant normal axis so textures tile consistently.
    /// Vertex color = tint A, TEXCOORD2 (Vector4) = tint B / custom data (see MansionLit.shader).
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> _v = new List<Vector3>(1024);
        readonly List<Vector3> _n = new List<Vector3>(1024);
        readonly List<Vector2> _uv = new List<Vector2>(1024);
        readonly List<Color32> _c = new List<Color32>(1024);
        readonly List<Vector4> _x = new List<Vector4>(1024);
        readonly SortedDictionary<int, List<int>> _t = new SortedDictionary<int, List<int>>();
        readonly Stack<Matrix4x4> _stack = new Stack<Matrix4x4>();

        public Matrix4x4 M = Matrix4x4.identity;
        public int Slot;
        public Color32 Color = new Color32(255, 255, 255, 255);
        public Vector4 Custom = Vector4.zero;
        public float UVScale = 1f;          // uv = meters * UVScale
        public Vector2 UVOffset;

        public int VertexCount => _v.Count;
        public bool Empty => _v.Count == 0;

        public void Clear() { _v.Clear(); _n.Clear(); _uv.Clear(); _c.Clear(); _x.Clear(); _t.Clear(); _stack.Clear(); M = Matrix4x4.identity; }

        // ---------------------------------------------------------------- transform stack
        public void Push() { _stack.Push(M); }
        public void Pop() { M = _stack.Pop(); }
        public void Push(Vector3 pos, Quaternion rot, Vector3 scale) { _stack.Push(M); M = M * Matrix4x4.TRS(pos, rot, scale); }
        public void Push(Vector3 pos, float yawDeg = 0f) { _stack.Push(M); M = M * Matrix4x4.TRS(pos, Quaternion.Euler(0, yawDeg, 0), Vector3.one); }
        public void Push(Matrix4x4 m) { _stack.Push(M); M = M * m; }

        public MeshBuilder Set(int slot) { Slot = slot; return this; }
        public MeshBuilder Set(int slot, Color c) { Slot = slot; Color = c; return this; }
        public MeshBuilder Set(int slot, Color c, Vector4 custom) { Slot = slot; Color = c; Custom = custom; return this; }

        List<int> Tris(int slot)
        {
            if (!_t.TryGetValue(slot, out var l)) { l = new List<int>(512); _t[slot] = l; }
            return l;
        }

        int V(Vector3 pWorld, Vector3 nWorld, Vector2 uv)
        {
            _v.Add(pWorld); _n.Add(nWorld); _uv.Add(uv); _c.Add(Color); _x.Add(Custom);
            return _v.Count - 1;
        }

        Vector2 PlanarUV(Vector3 p, Vector3 n)
        {
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            Vector2 uv;
            if (ay >= ax && ay >= az) uv = new Vector2(p.x, p.z * (n.y >= 0 ? 1 : -1));
            else if (ax >= az) uv = new Vector2(n.x >= 0 ? -p.z : p.z, p.y);
            else uv = new Vector2(n.z >= 0 ? p.x : -p.x, p.y);
            return uv * UVScale + UVOffset;
        }

        // ---------------------------------------------------------------- primitives
        /// <summary>Triangle (a,b,c) front-facing clockwise (Unity convention). Local coordinates.</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            a = M.MultiplyPoint3x4(a); b = M.MultiplyPoint3x4(b); c = M.MultiplyPoint3x4(c);
            var n = Vector3.Cross(b - a, c - a); if (n.sqrMagnitude < 1e-12f) return; n.Normalize();
            var t = Tris(Slot);
            int i0 = V(a, n, PlanarUV(a, n)), i1 = V(b, n, PlanarUV(b, n)), i2 = V(c, n, PlanarUV(c, n));
            t.Add(i0); t.Add(i1); t.Add(i2);
        }

        /// <summary>Quad a-b-c-d (clockwise seen from the front). Local coordinates.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            a = M.MultiplyPoint3x4(a); b = M.MultiplyPoint3x4(b); c = M.MultiplyPoint3x4(c); d = M.MultiplyPoint3x4(d);
            var n = Vector3.Cross(b - a, c - a); if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(c - a, d - a);
            if (n.sqrMagnitude < 1e-12f) return; n.Normalize();
            var t = Tris(Slot);
            int i0 = V(a, n, PlanarUV(a, n)), i1 = V(b, n, PlanarUV(b, n)), i2 = V(c, n, PlanarUV(c, n)), i3 = V(d, n, PlanarUV(d, n));
            t.Add(i0); t.Add(i1); t.Add(i2); t.Add(i0); t.Add(i2); t.Add(i3);
        }

        /// <summary>Quad with explicit UVs (0..1 painting etc).</summary>
        public void QuadUV(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            a = M.MultiplyPoint3x4(a); b = M.MultiplyPoint3x4(b); c = M.MultiplyPoint3x4(c); d = M.MultiplyPoint3x4(d);
            var n = Vector3.Cross(b - a, c - a); if (n.sqrMagnitude < 1e-12f) return; n.Normalize();
            var t = Tris(Slot);
            int i0 = V(a, n, ua), i1 = V(b, n, ub), i2 = V(c, n, uc), i3 = V(d, n, ud);
            t.Add(i0); t.Add(i1); t.Add(i2); t.Add(i0); t.Add(i2); t.Add(i3);
        }

        /// <summary>Quad with explicit UVs and normal, no degeneracy test (GPU-expanded billboards).</summary>
        public void QuadRaw(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 normal)
        {
            a = M.MultiplyPoint3x4(a); b = M.MultiplyPoint3x4(b); c = M.MultiplyPoint3x4(c); d = M.MultiplyPoint3x4(d);
            var n = M.MultiplyVector(normal).normalized;
            var t = Tris(Slot);
            int i0 = V(a, n, ua), i1 = V(b, n, ub), i2 = V(c, n, uc), i3 = V(d, n, ud);
            t.Add(i0); t.Add(i1); t.Add(i2); t.Add(i0); t.Add(i2); t.Add(i3);
        }

        /// <summary>Face spanned by u (right) and v (up) from origin; faces the viewer that sees u to the right. Normal = cross(v,u).</summary>
        public void Face(Vector3 o, Vector3 u, Vector3 v) => Quad(o, o + v, o + u + v, o + u);
        public void FaceUV(Vector3 o, Vector3 u, Vector3 v, Rect uv) =>
            QuadUV(o, o + v, o + u + v, o + u, new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMin, uv.yMax), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMax, uv.yMin));

        [System.Flags] public enum Faces { None = 0, PX = 1, NX = 2, PY = 4, NY = 8, PZ = 16, NZ = 32, All = 63, NoBottom = 55, Sides = 51 }

        public void Box(Vector3 c, Vector3 s, Faces f = Faces.All)
        {
            Vector3 h = s * 0.5f;
            Vector3 x = new Vector3(s.x, 0, 0), y = new Vector3(0, s.y, 0), z = new Vector3(0, 0, s.z);
            Vector3 mn = c - h;
            if ((f & Faces.NZ) != 0) Face(mn, x, y);                       // -Z
            if ((f & Faces.PZ) != 0) Face(mn + z + x, -x, y);              // +Z
            if ((f & Faces.NX) != 0) Face(mn + z, -z, y);                  // -X
            if ((f & Faces.PX) != 0) Face(mn + x, z, y);                   // +X
            if ((f & Faces.PY) != 0) Face(mn + y, x, z);                   // +Y
            if ((f & Faces.NY) != 0) Face(mn + z, x, -z);                  // -Y
        }
        public void BoxMM(Vector3 mn, Vector3 mx, Faces f = Faces.All) => Box((mn + mx) * 0.5f, mx - mn, f);

        /// <summary>Box with chamfered top edges (cheap bevel look).</summary>
        public void BevelBox(Vector3 c, Vector3 s, float b)
        {
            b = Mathf.Min(b, Mathf.Min(s.x, Mathf.Min(s.y, s.z)) * 0.45f);
            Vector3 h = s * 0.5f;
            float x0 = c.x - h.x, x1 = c.x + h.x, y0 = c.y - h.y, y1 = c.y + h.y, z0 = c.z - h.z, z1 = c.z + h.z;
            // sides up to y1-b
            BoxMM(new Vector3(x0, y0, z0), new Vector3(x1, y1 - b, z1), Faces.Sides | Faces.NY);
            // top inset
            Quad(new Vector3(x0 + b, y1, z0 + b), new Vector3(x0 + b, y1, z1 - b), new Vector3(x1 - b, y1, z1 - b), new Vector3(x1 - b, y1, z0 + b));
            // chamfers
            Quad(new Vector3(x0, y1 - b, z0), new Vector3(x0 + b, y1, z0 + b), new Vector3(x1 - b, y1, z0 + b), new Vector3(x1, y1 - b, z0));
            Quad(new Vector3(x1, y1 - b, z1), new Vector3(x1 - b, y1, z1 - b), new Vector3(x0 + b, y1, z1 - b), new Vector3(x0, y1 - b, z1));
            Quad(new Vector3(x0, y1 - b, z1), new Vector3(x0 + b, y1, z1 - b), new Vector3(x0 + b, y1, z0 + b), new Vector3(x0, y1 - b, z0));
            Quad(new Vector3(x1, y1 - b, z0), new Vector3(x1 - b, y1, z0 + b), new Vector3(x1 - b, y1, z1 - b), new Vector3(x1, y1 - b, z1));
        }

        /// <summary>Surface of revolution around local Y. profile: (radius, y) from bottom to top. Smooth normals.</summary>
        public void Lathe(IList<Vector2> prof, int seg, bool capBottom = false, bool capTop = false, float a0 = 0f, float a1 = 360f)
        {
            int n = prof.Count; if (n < 2) return;
            var t = Tris(Slot);
            float len = 0; var vs = new float[n];
            for (int i = 1; i < n; i++) { len += (prof[i] - prof[i - 1]).magnitude; vs[i] = len; }
            // profile normals (2D) averaged at vertices
            var pn = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 d0 = i > 0 ? prof[i] - prof[i - 1] : prof[1] - prof[0];
                Vector2 d1 = i < n - 1 ? prof[i + 1] - prof[i] : d0;
                Vector2 e0 = new Vector2(d0.y, -d0.x).normalized, e1 = new Vector2(d1.y, -d1.x).normalized;
                pn[i] = (e0 + e1).normalized; if (pn[i].sqrMagnitude < 1e-6f) pn[i] = e0;
            }
            int baseIdx = _v.Count;
            bool full = Mathf.Approximately(a1 - a0, 360f);
            int cols = seg + 1;
            for (int s = 0; s <= seg; s++)
            {
                float a = Mathf.Deg2Rad * Mathf.Lerp(a0, a1, s / (float)seg);
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = new Vector3(prof[i].x * ca, prof[i].y, prof[i].x * sa);
                    Vector3 nn = new Vector3(pn[i].x * ca, pn[i].y, pn[i].x * sa);
                    Vector3 pw = M.MultiplyPoint3x4(p); Vector3 nw = M.MultiplyVector(nn).normalized;
                    V(pw, nw, new Vector2(s / (float)seg * (2 * Mathf.PI * Mathf.Max(0.05f, prof[i].x)) * UVScale, vs[i] * UVScale));
                }
            }
            for (int s = 0; s < seg; s++)
                for (int i = 0; i < n - 1; i++)
                {
                    int a = baseIdx + s * n + i, b = baseIdx + (s + 1) * n + i;
                    // winding so that outward normal faces out (profile going up, angle increasing)
                    t.Add(a); t.Add(a + 1); t.Add(b + 1);
                    t.Add(a); t.Add(b + 1); t.Add(b);
                }
            if (capBottom && prof[0].x > 1e-4f) Disc(new Vector3(0, prof[0].y, 0), prof[0].x, seg, false);
            if (capTop && prof[n - 1].x > 1e-4f) Disc(new Vector3(0, prof[n - 1].y, 0), prof[n - 1].x, seg, true);
        }

        /// <summary>Flat disc in local XZ at center, facing up or down.</summary>
        public void Disc(Vector3 c, float r, int seg, bool up, float r2 = -1f)
        {
            if (r2 < 0) r2 = r;
            var t = Tris(Slot);
            Vector3 nn = M.MultiplyVector(up ? Vector3.up : Vector3.down).normalized;
            int ci = V(M.MultiplyPoint3x4(c), nn, PlanarUV(M.MultiplyPoint3x4(c), nn));
            int first = _v.Count;
            for (int s = 0; s <= seg; s++)
            {
                float a = s / (float)seg * Mathf.PI * 2f;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r2);
                var pw = M.MultiplyPoint3x4(p);
                V(pw, nn, PlanarUV(pw, nn));
            }
            for (int s = 0; s < seg; s++)
            {
                if (up) { t.Add(ci); t.Add(first + s + 1); t.Add(first + s); }
                else { t.Add(ci); t.Add(first + s); t.Add(first + s + 1); }
            }
        }

        public void Cyl(Vector3 b, float r, float h, int seg = 12, bool caps = true, float rTop = -1f)
        {
            if (rTop < 0) rTop = r;
            Push(b, 0);
            Lathe(new[] { new Vector2(r, 0), new Vector2(rTop, h) }, seg, caps, caps);
            Pop();
        }

        /// <summary>Cylinder between two arbitrary points.</summary>
        public void Rod(Vector3 a, Vector3 b, float r, int seg = 8, bool caps = true)
        {
            Vector3 d = b - a; float len = d.magnitude; if (len < 1e-5f) return;
            Push(a, Quaternion.FromToRotation(Vector3.up, d / len), Vector3.one);
            Lathe(new[] { new Vector2(r, 0), new Vector2(r, len) }, seg, caps, caps);
            Pop();
        }

        /// <summary>Square bar between two points (cheaper than Rod).</summary>
        public void Bar(Vector3 a, Vector3 b, float w, float h = -1f)
        {
            if (h < 0) h = w;
            Vector3 d = b - a; float len = d.magnitude; if (len < 1e-5f) return;
            Quaternion q = Quaternion.LookRotation(d / len, Mathf.Abs(Vector3.Dot(d / len, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up);
            Push((a + b) * 0.5f, q, Vector3.one);
            Box(Vector3.zero, new Vector3(w, h, len));
            Pop();
        }

        public void Sphere(Vector3 c, float r, int seg = 12, int rings = 8, float squashY = 1f)
        {
            var prof = new Vector2[rings + 1];
            for (int i = 0; i <= rings; i++) { float a = -Mathf.PI / 2 + Mathf.PI * i / rings; prof[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * squashY); }
            prof[0].x = 0.0001f; prof[rings].x = 0.0001f;
            Push(c, 0); Lathe(prof, seg); Pop();
        }

        public void Ellipsoid(Vector3 c, Vector3 radii, int seg = 12, int rings = 8)
        {
            Push(c, Quaternion.identity, radii); Sphere(Vector3.zero, 1f, seg, rings); Pop();
        }

        public void Torus(Vector3 c, float R, float r, int segU = 16, int segV = 8, float a0 = 0, float a1 = 360)
        {
            var t = Tris(Slot);
            int baseIdx = _v.Count;
            for (int i = 0; i <= segU; i++)
            {
                float u = Mathf.Deg2Rad * Mathf.Lerp(a0, a1, i / (float)segU);
                Vector3 dir = new Vector3(Mathf.Cos(u), 0, Mathf.Sin(u));
                for (int j = 0; j <= segV; j++)
                {
                    float v = j / (float)segV * Mathf.PI * 2f;
                    Vector3 nn = dir * Mathf.Cos(v) + Vector3.up * Mathf.Sin(v);
                    Vector3 p = c + dir * R + nn * r;
                    V(M.MultiplyPoint3x4(p), M.MultiplyVector(nn).normalized, new Vector2(i / (float)segU * R * 6.28f, j / (float)segV * r * 6.28f) * UVScale);
                }
            }
            int cols = segV + 1;
            for (int i = 0; i < segU; i++)
                for (int j = 0; j < segV; j++)
                {
                    int a = baseIdx + i * cols + j, b = a + cols;
                    t.Add(a); t.Add(a + 1); t.Add(b + 1); t.Add(a); t.Add(b + 1); t.Add(b);
                }
        }

        /// <summary>Tube along a polyline (e.g. pipes, handrails). Parallel-transport frames.</summary>
        public void Tube(IList<Vector3> path, float r, int seg = 8, bool caps = false)
        {
            int n = path.Count; if (n < 2) return;
            var t = Tris(Slot);
            int baseIdx = _v.Count;
            Vector3 prevT = (path[1] - path[0]).normalized;
            Vector3 nrm = Vector3.Cross(prevT, Mathf.Abs(prevT.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            float acc = 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 tg = i == 0 ? (path[1] - path[0]).normalized : i == n - 1 ? (path[n - 1] - path[n - 2]).normalized : ((path[i + 1] - path[i]).normalized + (path[i] - path[i - 1]).normalized).normalized;
                if (i > 0) { acc += (path[i] - path[i - 1]).magnitude; var q = Quaternion.FromToRotation(prevT, tg); nrm = q * nrm; }
                prevT = tg;
                Vector3 bin = Vector3.Cross(tg, nrm);
                for (int s = 0; s <= seg; s++)
                {
                    float a = s / (float)seg * Mathf.PI * 2f;
                    Vector3 dir = nrm * Mathf.Cos(a) + bin * Mathf.Sin(a);
                    V(M.MultiplyPoint3x4(path[i] + dir * r), M.MultiplyVector(dir).normalized, new Vector2(s / (float)seg * r * 6.28f, acc) * UVScale);
                }
            }
            int cols = seg + 1;
            for (int i = 0; i < n - 1; i++)
                for (int s = 0; s < seg; s++)
                {
                    int a = baseIdx + i * cols + s, b = a + cols;
                    t.Add(a); t.Add(b + 1); t.Add(b); t.Add(a); t.Add(a + 1); t.Add(b + 1);
                }
            if (caps)
            {
                Sphere(path[0], r, seg, 4); Sphere(path[n - 1], r, seg, 4);
            }
        }

        /// <summary>Prism: convex/concave-simple polygon in local XZ (clockwise seen from above) extruded from y0 to y1.</summary>
        public void Prism(IList<Vector2> poly, float y0, float y1, bool top = true, bool bottom = false)
        {
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % n];
                Quad(new Vector3(a.x, y0, a.y), new Vector3(a.x, y1, a.y), new Vector3(b.x, y1, b.y), new Vector3(b.x, y0, b.y));
            }
            if (top) for (int i = 1; i < n - 1; i++) Tri(new Vector3(poly[0].x, y1, poly[0].y), new Vector3(poly[i].x, y1, poly[i].y), new Vector3(poly[i + 1].x, y1, poly[i + 1].y));
            if (bottom) for (int i = 1; i < n - 1; i++) Tri(new Vector3(poly[0].x, y0, poly[0].y), new Vector3(poly[i + 1].x, y0, poly[i + 1].y), new Vector3(poly[i].x, y0, poly[i].y));
        }

        /// <summary>Profile swept along a straight segment (moldings). profile: (out, up) in meters, from bottom-wall to top-wall.</summary>
        public void Molding(Vector3 a, Vector3 b, Vector3 outDir, IList<Vector2> prof, bool caps = false)
        {
            Vector3 up = Vector3.up;
            int n = prof.Count;
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 p0 = outDir * prof[i].x + up * prof[i].y, p1 = outDir * prof[i + 1].x + up * prof[i + 1].y;
                // orientation: seen from outside, a->b to the right when outDir faces viewer... choose winding by test
                Vector3 e = (b - a);
                Vector3 na = Vector3.Cross(p1 - p0, e);
                Vector3 expect = outDir * (prof[i + 1].y - prof[i].y) - up * (prof[i + 1].x - prof[i].x);
                if (Vector3.Dot(na, expect) >= 0) Quad(a + p0, a + p1, b + p1, b + p0);
                else Quad(b + p0, b + p1, a + p1, a + p0);
            }
            if (caps)
            {
                // flat caps (fan)
                for (int i = 1; i < n - 1; i++)
                {
                    Vector3 q0 = outDir * prof[0].x + up * prof[0].y, q1 = outDir * prof[i].x + up * prof[i].y, q2 = outDir * prof[i + 1].x + up * prof[i + 1].y;
                    TriAuto(a + q0, a + q1, a + q2, (a - b));
                    TriAuto(b + q0, b + q1, b + q2, (b - a));
                }
            }
        }

        /// <summary>Triangle whose winding is chosen so that its normal points along 'want'.</summary>
        public void TriAuto(Vector3 a, Vector3 b, Vector3 c, Vector3 want)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, want) >= 0) Tri(a, b, c); else Tri(a, c, b);
        }
        public void QuadAuto(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 want)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, want) >= 0) Quad(a, b, c, d); else Quad(d, c, b, a);
        }

        /// <summary>Append another mesh (all submeshes into the current slot unless slotMap given).</summary>
        public void Append(Mesh m, Matrix4x4 xf, int[] slotMap = null)
        {
            var mv = m.vertices; var mn = m.normals; var muv = m.uv;
            Matrix4x4 full = M * xf;
            int baseIdx = _v.Count;
            for (int i = 0; i < mv.Length; i++)
            {
                var p = full.MultiplyPoint3x4(mv[i]);
                var nn = mn != null && mn.Length == mv.Length ? full.MultiplyVector(mn[i]).normalized : Vector3.up;
                V(p, nn, muv != null && muv.Length == mv.Length ? muv[i] : Vector2.zero);
            }
            bool flip = full.determinant < 0;
            for (int s = 0; s < m.subMeshCount; s++)
            {
                var idx = m.GetTriangles(s);
                var t = Tris(slotMap != null && s < slotMap.Length ? slotMap[s] : Slot);
                for (int i = 0; i < idx.Length; i += 3)
                {
                    if (flip) { t.Add(baseIdx + idx[i]); t.Add(baseIdx + idx[i + 2]); t.Add(baseIdx + idx[i + 1]); }
                    else { t.Add(baseIdx + idx[i]); t.Add(baseIdx + idx[i + 1]); t.Add(baseIdx + idx[i + 2]); }
                }
            }
        }

        public List<int> SlotsUsed()
        {
            var l = new List<int>();
            foreach (var kv in _t) if (kv.Value.Count > 0) l.Add(kv.Key);
            return l;
        }

        /// <summary>Build a Unity mesh. slots receives the material slot of each submesh.</summary>
        public Mesh ToMesh(string name, out int[] slots, bool tangents = true)
        {
            var mesh = new Mesh { name = name };
            if (_v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_v); mesh.SetNormals(_n); mesh.SetUVs(0, _uv); mesh.SetColors(_c); mesh.SetUVs(2, _x);
            var used = SlotsUsed();
            mesh.subMeshCount = used.Count;
            for (int i = 0; i < used.Count; i++) mesh.SetTriangles(_t[used[i]], i, false);
            mesh.RecalculateBounds();
            if (tangents) mesh.RecalculateTangents();
            slots = used.ToArray();
            return mesh;
        }
    }
}
