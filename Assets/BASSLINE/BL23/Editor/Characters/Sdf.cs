using System;
using System.Collections.Generic;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>Signed distance field node. Immutable after construction (thread-safe evaluation).</summary>
    public abstract class Sdf
    {
        public Bounds B;
        public abstract float D(Vector3 p);

        public static float BoxDist(in Bounds b, Vector3 p)
        {
            Vector3 c = b.center, e = b.extents;
            float dx = Mathf.Max(Mathf.Abs(p.x - c.x) - e.x, 0f);
            float dy = Mathf.Max(Mathf.Abs(p.y - c.y) - e.y, 0f);
            float dz = Mathf.Max(Mathf.Abs(p.z - c.z) - e.z, 0f);
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public static float SMin(float a, float b, float k)
        {
            if (k <= 0f) return a < b ? a : b;
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        public static float SMax(float a, float b, float k) => -SMin(-a, -b, k);

        public Vector3 Grad(Vector3 p, float e)
        {
            float dx = D(new Vector3(p.x + e, p.y, p.z)) - D(new Vector3(p.x - e, p.y, p.z));
            float dy = D(new Vector3(p.x, p.y + e, p.z)) - D(new Vector3(p.x, p.y - e, p.z));
            float dz = D(new Vector3(p.x, p.y, p.z + e)) - D(new Vector3(p.x, p.y, p.z - e));
            return new Vector3(dx, dy, dz);
        }

        // --------------------------------------------------------------- builders
        public static Sdf Sphere(Vector3 c, float r) => new SSphere(c, r);
        public static Sdf Ellipsoid(Vector3 c, Vector3 r) => new SEllipsoid(c, r, Quaternion.identity);
        public static Sdf Ellipsoid(Vector3 c, Vector3 r, Quaternion rot) => new SEllipsoid(c, r, rot);
        public static Sdf Capsule(Vector3 a, Vector3 b, float r) => new SRoundCone(a, b, r, r);
        public static Sdf Cone(Vector3 a, Vector3 b, float ra, float rb) => new SRoundCone(a, b, ra, rb);
        public static Sdf Box(Vector3 c, Vector3 half, float round) => new SRoundBox(c, half, Quaternion.identity, round);
        public static Sdf Box(Vector3 c, Vector3 half, Quaternion rot, float round) => new SRoundBox(c, half, rot, round);
        public static Sdf Torus(Vector3 c, Quaternion rot, float R, float r) => new STorus(c, rot, R, r);
        public static Sdf Cyl(Vector3 a, Vector3 b, float r, float round) => new SCylinder(a, b, r, round);
        public static Sdf Union(float k, params Sdf[] kids) => new SUnion(new List<Sdf>(kids), k);
        public static Sdf Union(float k, List<Sdf> kids) => new SUnion(kids, k);
        public static Sdf Sub(Sdf a, Sdf b, float k = 0f) => new SSub(a, b, k);
        public static Sdf Inter(Sdf a, Sdf b, float k = 0f) => new SInter(a, b, k);
        public static Sdf Offset(Sdf a, float o) => new SOffset(a, o);
        public static Sdf Shell(Sdf a, float t) => new SShell(a, t);
        public static Sdf Displace(Sdf a, Func<Vector3, float> f, float amp) => new SDisplace(a, f, amp);
        public static Sdf Func(Func<Vector3, float> f, Bounds b) => new SFunc(f, b);
        /// <summary>Half space: negative where dot(p - point, n) &lt; 0 (i.e. keeps the side opposite to n).</summary>
        public static Sdf Plane(Vector3 point, Vector3 n) => new SPlane(point, n.normalized);
        public static Sdf Tube(IList<Vector3> pts, IList<float> radii, float k = 0f)
        {
            var kids = new List<Sdf>();
            for (int i = 0; i + 1 < pts.Count; i++) kids.Add(new SRoundCone(pts[i], pts[i + 1], radii[i], radii[i + 1]));
            return kids.Count == 1 ? kids[0] : new SUnion(kids, k);
        }
    }

    public sealed class SSphere : Sdf
    {
        readonly Vector3 c; readonly float r;
        public SSphere(Vector3 c, float r) { this.c = c; this.r = r; B = new Bounds(c, Vector3.one * (2 * r)); }
        public override float D(Vector3 p) => (p - c).magnitude - r;
    }

    public sealed class SEllipsoid : Sdf
    {
        readonly Vector3 c, r; readonly Quaternion inv;
        public SEllipsoid(Vector3 c, Vector3 r, Quaternion rot)
        {
            this.c = c; this.r = r; inv = Quaternion.Inverse(rot);
            float m = Mathf.Max(r.x, Mathf.Max(r.y, r.z));
            if (rot == Quaternion.identity) B = new Bounds(c, r * 2f); else B = new Bounds(c, Vector3.one * (2 * m));
        }
        public override float D(Vector3 p)
        {
            Vector3 q = inv * (p - c);
            float k0 = new Vector3(q.x / r.x, q.y / r.y, q.z / r.z).magnitude;
            float k1 = new Vector3(q.x / (r.x * r.x), q.y / (r.y * r.y), q.z / (r.z * r.z)).magnitude;
            if (k1 < 1e-9f) return -Mathf.Min(r.x, Mathf.Min(r.y, r.z));
            return k0 * (k0 - 1f) / k1;
        }
    }

    /// <summary>Exact round cone (Inigo Quilez).</summary>
    public sealed class SRoundCone : Sdf
    {
        readonly Vector3 a, b, ba; readonly float r1, r2, l2, rr, a2, il2;
        public SRoundCone(Vector3 a, Vector3 b, float ra, float rb)
        {
            this.a = a; this.b = b; r1 = ra; r2 = rb; ba = b - a; l2 = Vector3.Dot(ba, ba);
            rr = r1 - r2; a2 = l2 - rr * rr; il2 = l2 > 1e-12f ? 1f / l2 : 0f;
            Vector3 mn = Vector3.Min(a - Vector3.one * ra, b - Vector3.one * rb);
            Vector3 mx = Vector3.Max(a + Vector3.one * ra, b + Vector3.one * rb);
            B = new Bounds((mn + mx) * 0.5f, mx - mn);
        }
        public override float D(Vector3 p)
        {
            if (l2 < 1e-12f) return (p - a).magnitude - Mathf.Max(r1, r2);
            Vector3 pa = p - a;
            float y = Vector3.Dot(pa, ba);
            float z = y - l2;
            Vector3 xv = pa * l2 - ba * y;
            float x2 = Vector3.Dot(xv, xv);
            float y2 = y * y * l2;
            float z2 = z * z * l2;
            float k = Mathf.Sign(rr) * rr * rr * x2;
            if (Mathf.Sign(z) * a2 * z2 > k) return Mathf.Sqrt(x2 + z2) * il2 - r2;
            if (Mathf.Sign(y) * a2 * y2 < k) return Mathf.Sqrt(x2 + y2) * il2 - r1;
            return (Mathf.Sqrt(x2 * a2 * il2) + y * rr) * il2 - r1;
        }
    }

    public sealed class SRoundBox : Sdf
    {
        readonly Vector3 c, h; readonly Quaternion inv; readonly float rad;
        public SRoundBox(Vector3 c, Vector3 half, Quaternion rot, float round)
        {
            this.c = c; rad = Mathf.Min(round, Mathf.Min(half.x, Mathf.Min(half.y, half.z)));
            h = half - Vector3.one * rad; inv = Quaternion.Inverse(rot);
            if (rot == Quaternion.identity) B = new Bounds(c, half * 2f);
            else B = new Bounds(c, Vector3.one * (2f * half.magnitude));
        }
        public override float D(Vector3 p)
        {
            Vector3 q = inv * (p - c);
            float qx = Mathf.Abs(q.x) - h.x, qy = Mathf.Abs(q.y) - h.y, qz = Mathf.Abs(q.z) - h.z;
            float ox = Mathf.Max(qx, 0), oy = Mathf.Max(qy, 0), oz = Mathf.Max(qz, 0);
            return Mathf.Sqrt(ox * ox + oy * oy + oz * oz) + Mathf.Min(Mathf.Max(qx, Mathf.Max(qy, qz)), 0f) - rad;
        }
    }

    public sealed class STorus : Sdf
    {
        readonly Vector3 c; readonly Quaternion inv; readonly float R, r;
        public STorus(Vector3 c, Quaternion rot, float R, float r)
        {
            this.c = c; inv = Quaternion.Inverse(rot); this.R = R; this.r = r;
            B = new Bounds(c, Vector3.one * (2f * (R + r)));
        }
        public override float D(Vector3 p)
        {
            Vector3 q = inv * (p - c); // torus lies in local XZ plane
            float qx = Mathf.Sqrt(q.x * q.x + q.z * q.z) - R;
            return Mathf.Sqrt(qx * qx + q.y * q.y) - r;
        }
    }

    public sealed class SCylinder : Sdf
    {
        readonly Vector3 a, b, ba; readonly float r, rad, baba, len;
        public SCylinder(Vector3 a, Vector3 b, float r, float round)
        {
            this.a = a; this.b = b; rad = Mathf.Min(round, r); this.r = r - rad; ba = b - a; baba = Vector3.Dot(ba, ba); len = Mathf.Sqrt(baba);
            Vector3 mn = Vector3.Min(a, b) - Vector3.one * r, mx = Vector3.Max(a, b) + Vector3.one * r;
            B = new Bounds((mn + mx) * 0.5f, mx - mn);
        }
        public override float D(Vector3 p)
        {
            Vector3 dir = ba / len;
            Vector3 mid = (a + b) * 0.5f;
            Vector3 pm = p - mid;
            float along = Vector3.Dot(pm, dir);
            float radial = (pm - dir * along).magnitude;
            float dx = radial - r, dy = Mathf.Abs(along) - (len * 0.5f - rad);
            float ox = Mathf.Max(dx, 0), oy = Mathf.Max(dy, 0);
            return Mathf.Min(Mathf.Max(dx, dy), 0f) + Mathf.Sqrt(ox * ox + oy * oy) - rad;
        }
    }

    public sealed class SPlane : Sdf
    {
        readonly Vector3 pt, n;
        public SPlane(Vector3 pt, Vector3 n) { this.pt = pt; this.n = n; B = new Bounds(Vector3.zero, Vector3.one * 100f); }
        public override float D(Vector3 p) => Vector3.Dot(p - pt, n);
    }

    public sealed class SUnion : Sdf
    {
        readonly Sdf[] kids; readonly float k;
        public SUnion(List<Sdf> list, float k)
        {
            kids = list.ToArray(); this.k = k;
            if (kids.Length == 0) { B = new Bounds(Vector3.zero, Vector3.zero); return; }
            Bounds b = kids[0].B;
            for (int i = 1; i < kids.Length; i++) b.Encapsulate(kids[i].B);
            B = b;
        }
        public override float D(Vector3 p)
        {
            if (kids.Length == 0) return 1e6f;
            float best = 1e6f;
            float margin = k + 0.01f;
            for (int i = 0; i < kids.Length; i++)
            {
                var kd = kids[i];
                if (best < 1e5f && BoxDist(kd.B, p) > best + margin) continue;
                float d = kd.D(p);
                best = k > 0f && best < 1e5f ? SMin(best, d, k) : Mathf.Min(best, d);
            }
            return best;
        }
    }

    public sealed class SSub : Sdf
    {
        readonly Sdf a, b; readonly float k;
        public SSub(Sdf a, Sdf b, float k) { this.a = a; this.b = b; this.k = k; B = a.B; }
        public override float D(Vector3 p)
        {
            float da = a.D(p);
            if (BoxDist(b.B, p) > Mathf.Abs(da) + k + 0.01f) return da;
            float db = b.D(p);
            return k > 0f ? SMax(da, -db, k) : Mathf.Max(da, -db);
        }
    }

    public sealed class SInter : Sdf
    {
        readonly Sdf a, b; readonly float k;
        public SInter(Sdf a, Sdf b, float k) { this.a = a; this.b = b; this.k = k; B = a.B; }
        public override float D(Vector3 p)
        {
            float da = a.D(p), db = b.D(p);
            return k > 0f ? SMax(da, db, k) : Mathf.Max(da, db);
        }
    }

    public sealed class SOffset : Sdf
    {
        readonly Sdf a; readonly float o;
        public SOffset(Sdf a, float o) { this.a = a; this.o = o; var bb = a.B; bb.Expand(Mathf.Max(0f, o) * 2f); B = bb; }
        public override float D(Vector3 p) => a.D(p) - o;
    }

    public sealed class SShell : Sdf
    {
        readonly Sdf a; readonly float t;
        public SShell(Sdf a, float t) { this.a = a; this.t = t; var bb = a.B; bb.Expand(t * 2f); B = bb; }
        public override float D(Vector3 p) => Mathf.Abs(a.D(p)) - t;
    }

    public sealed class SDisplace : Sdf
    {
        readonly Sdf a; readonly Func<Vector3, float> f; readonly float amp;
        public SDisplace(Sdf a, Func<Vector3, float> f, float amp) { this.a = a; this.f = f; this.amp = amp; var bb = a.B; bb.Expand(amp * 2f); B = bb; }
        public override float D(Vector3 p)
        {
            float d = a.D(p);
            if (d > amp * 1.5f + 0.01f) return d;
            return d - f(p) * amp;
        }
    }

    public sealed class SFunc : Sdf
    {
        readonly Func<Vector3, float> f;
        public SFunc(Func<Vector3, float> f, Bounds b) { this.f = f; B = b; }
        public override float D(Vector3 p) => f(p);
    }

    public static class Noise
    {
        static float Hash(int x, int y, int z)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + z * 1274126177;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xffffff) / 16777215f;
            }
        }

        public static float Value(Vector3 p)
        {
            int ix = Mathf.FloorToInt(p.x), iy = Mathf.FloorToInt(p.y), iz = Mathf.FloorToInt(p.z);
            float fx = p.x - ix, fy = p.y - iy, fz = p.z - iz;
            float ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy), uz = fz * fz * (3 - 2 * fz);
            float a = Mathf.Lerp(Hash(ix, iy, iz), Hash(ix + 1, iy, iz), ux);
            float b = Mathf.Lerp(Hash(ix, iy + 1, iz), Hash(ix + 1, iy + 1, iz), ux);
            float c = Mathf.Lerp(Hash(ix, iy, iz + 1), Hash(ix + 1, iy, iz + 1), ux);
            float d = Mathf.Lerp(Hash(ix, iy + 1, iz + 1), Hash(ix + 1, iy + 1, iz + 1), ux);
            return Mathf.Lerp(Mathf.Lerp(a, b, uy), Mathf.Lerp(c, d, uy), uz);
        }

        public static float Fbm(Vector3 p, int oct = 3)
        {
            float s = 0, a = 0.5f;
            for (int i = 0; i < oct; i++) { s += a * Value(p); p = p * 2.03f + new Vector3(11.7f, 3.1f, 7.9f); a *= 0.5f; }
            return s;
        }
    }
}
