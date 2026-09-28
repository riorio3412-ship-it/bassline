using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>Naive surface nets mesher with narrow-band sampling, surface projection and SDF-gradient normals.</summary>
    public static class SurfaceNets
    {
        public sealed class Result
        {
            public List<Vector3> V = new List<Vector3>();
            public List<Vector3> N = new List<Vector3>();
            public List<int> T = new List<int>();
        }

        public static Result Mesh(Sdf f, Bounds region, float h, int projectIters = 2)
        {
            region.Expand(h * 4f);
            Vector3 mn = region.min;
            int nx = Mathf.CeilToInt(region.size.x / h) + 1;
            int ny = Mathf.CeilToInt(region.size.y / h) + 1;
            int nz = Mathf.CeilToInt(region.size.z / h) + 1;
            // coarse grid (step 4)
            const int C = 4;
            int cx = (nx + C - 1) / C + 1, cy = (ny + C - 1) / C + 1, cz = (nz + C - 1) / C + 1;
            var coarse = new float[cx * cy * cz];
            Parallel.For(0, cz, k =>
            {
                for (int j = 0; j < cy; j++)
                    for (int i = 0; i < cx; i++)
                        coarse[(k * cy + j) * cx + i] = f.D(mn + new Vector3(i * C * h, j * C * h, k * C * h));
            });
            var grid = new float[nx * ny * nz];
            float band = C * h * 1.9f + h;
            Parallel.For(0, cz - 1, ck =>
            {
                for (int cj = 0; cj < cy - 1; cj++)
                    for (int ci = 0; ci < cx - 1; ci++)
                    {
                        float c000 = coarse[(ck * cy + cj) * cx + ci], c100 = coarse[(ck * cy + cj) * cx + ci + 1];
                        float c010 = coarse[(ck * cy + cj + 1) * cx + ci], c110 = coarse[(ck * cy + cj + 1) * cx + ci + 1];
                        float c001 = coarse[((ck + 1) * cy + cj) * cx + ci], c101 = coarse[((ck + 1) * cy + cj) * cx + ci + 1];
                        float c011 = coarse[((ck + 1) * cy + cj + 1) * cx + ci], c111 = coarse[((ck + 1) * cy + cj + 1) * cx + ci + 1];
                        float mnAbs = Mathf.Min(Mathf.Min(Mathf.Min(Mathf.Abs(c000), Mathf.Abs(c100)), Mathf.Min(Mathf.Abs(c010), Mathf.Abs(c110))),
                                                Mathf.Min(Mathf.Min(Mathf.Abs(c001), Mathf.Abs(c101)), Mathf.Min(Mathf.Abs(c011), Mathf.Abs(c111))));
                        bool mixed = !(c000 > 0 && c100 > 0 && c010 > 0 && c110 > 0 && c001 > 0 && c101 > 0 && c011 > 0 && c111 > 0) &&
                                     !(c000 < 0 && c100 < 0 && c010 < 0 && c110 < 0 && c001 < 0 && c101 < 0 && c011 < 0 && c111 < 0);
                        bool active = mixed || mnAbs < band;
                        for (int dk = 0; dk <= C; dk++)
                        {
                            int k = ck * C + dk; if (k >= nz) break;
                            for (int dj = 0; dj <= C; dj++)
                            {
                                int j = cj * C + dj; if (j >= ny) break;
                                for (int di = 0; di <= C; di++)
                                {
                                    int i = ci * C + di; if (i >= nx) break;
                                    int idx = (k * ny + j) * nx + i;
                                    if (active)
                                        grid[idx] = f.D(mn + new Vector3(i * h, j * h, k * h));
                                    else
                                    {
                                        float u = di / (float)C, v = dj / (float)C, w = dk / (float)C;
                                        float x00 = Mathf.Lerp(c000, c100, u), x10 = Mathf.Lerp(c010, c110, u);
                                        float x01 = Mathf.Lerp(c001, c101, u), x11 = Mathf.Lerp(c011, c111, u);
                                        grid[idx] = Mathf.Lerp(Mathf.Lerp(x00, x10, v), Mathf.Lerp(x01, x11, v), w);
                                    }
                                }
                            }
                        }
                    }
            });

            // vertices: one per cell with a sign change
            var cellVert = new int[(nx - 1) * (ny - 1) * (nz - 1)];
            var res = new Result();
            int[] cornerOff = new int[8];
            for (int c = 0; c < 8; c++) cornerOff[c] = ((c >> 2) & 1) * ny * nx + ((c >> 1) & 1) * nx + (c & 1);
            int[,] edges = { { 0, 1 }, { 2, 3 }, { 4, 5 }, { 6, 7 }, { 0, 2 }, { 1, 3 }, { 4, 6 }, { 5, 7 }, { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 } };
            float[] cv = new float[8];
            for (int k = 0; k < nz - 1; k++)
                for (int j = 0; j < ny - 1; j++)
                    for (int i = 0; i < nx - 1; i++)
                    {
                        int ci = (k * (ny - 1) + j) * (nx - 1) + i;
                        int baseIdx = (k * ny + j) * nx + i;
                        int mask = 0;
                        for (int c = 0; c < 8; c++) { cv[c] = grid[baseIdx + cornerOff[c]]; if (cv[c] < 0) mask |= 1 << c; }
                        if (mask == 0 || mask == 255) { cellVert[ci] = -1; continue; }
                        Vector3 sum = Vector3.zero; int cnt = 0;
                        for (int e = 0; e < 12; e++)
                        {
                            int a = edges[e, 0], b = edges[e, 1];
                            float va = cv[a], vb = cv[b];
                            if ((va < 0) == (vb < 0)) continue;
                            float t = va / (va - vb);
                            Vector3 pa = new Vector3(a & 1, (a >> 1) & 1, (a >> 2) & 1);
                            Vector3 pb = new Vector3(b & 1, (b >> 1) & 1, (b >> 2) & 1);
                            sum += Vector3.Lerp(pa, pb, t); cnt++;
                        }
                        Vector3 local = sum / cnt;
                        cellVert[ci] = res.V.Count;
                        res.V.Add(mn + new Vector3((i + local.x) * h, (j + local.y) * h, (k + local.z) * h));
                    }

            // quads across sign-changing grid edges
            for (int k = 1; k < nz - 1; k++)
                for (int j = 1; j < ny - 1; j++)
                    for (int i = 1; i < nx - 1; i++)
                    {
                        int idx = (k * ny + j) * nx + i;
                        float v0 = grid[idx];
                        // x edge (i,j,k)-(i+1,j,k): cells sharing it: (i, j-1..j, k-1..k)
                        if (i < nx - 1)
                        {
                            float v1 = grid[idx + 1];
                            if ((v0 < 0) != (v1 < 0))
                                Quad(res, cellVert, nx, ny, i, j, k, 0, v0 < 0);
                        }
                        if (j < ny - 1)
                        {
                            float v1 = grid[idx + nx];
                            if ((v0 < 0) != (v1 < 0))
                                Quad(res, cellVert, nx, ny, i, j, k, 1, v0 < 0);
                        }
                        if (k < nz - 1)
                        {
                            float v1 = grid[idx + nx * ny];
                            if ((v0 < 0) != (v1 < 0))
                                Quad(res, cellVert, nx, ny, i, j, k, 2, v0 < 0);
                        }
                    }

            // project to surface + normals
            var V = res.V;
            var Ns = new Vector3[V.Count];
            float ge = h * 0.35f;
            Parallel.For(0, V.Count, vi =>
            {
                Vector3 p = V[vi];
                for (int it = 0; it < projectIters; it++)
                {
                    float d = f.D(p);
                    Vector3 g = f.Grad(p, ge);
                    float gl = g.sqrMagnitude;
                    if (gl < 1e-12f) break;
                    float glen = Mathf.Sqrt(gl);
                    Vector3 gn = g / glen;
                    float slope = Mathf.Max(glen / (2f * ge), 0.25f);
                    float step = Mathf.Clamp(d / slope, -h * 0.7f, h * 0.7f);
                    p -= gn * step;
                }
                V[vi] = p;
                Vector3 n = f.Grad(p, ge);
                Ns[vi] = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
            });
            res.N.AddRange(Ns);
            return res;
        }

        static void Quad(Result r, int[] cellVert, int nx, int ny, int i, int j, int k, int axis, bool flip)
        {
            int cnx = nx - 1, cny = ny - 1;
            int a, b, c, d;
            if (axis == 0)
            {
                a = cellVert[((k - 1) * cny + (j - 1)) * cnx + i];
                b = cellVert[((k - 1) * cny + j) * cnx + i];
                c = cellVert[(k * cny + j) * cnx + i];
                d = cellVert[(k * cny + (j - 1)) * cnx + i];
            }
            else if (axis == 1)
            {
                a = cellVert[((k - 1) * cny + j) * cnx + (i - 1)];
                b = cellVert[(k * cny + j) * cnx + (i - 1)];
                c = cellVert[(k * cny + j) * cnx + i];
                d = cellVert[((k - 1) * cny + j) * cnx + i];
            }
            else
            {
                a = cellVert[(k * cny + (j - 1)) * cnx + (i - 1)];
                b = cellVert[(k * cny + (j - 1)) * cnx + i];
                c = cellVert[(k * cny + j) * cnx + i];
                d = cellVert[(k * cny + j) * cnx + (i - 1)];
            }
            if (a < 0 || b < 0 || c < 0 || d < 0) return;
            if (flip) { int t = b; b = d; d = t; }
            // split along the shorter diagonal
            float d1 = (r.V[a] - r.V[c]).sqrMagnitude, d2 = (r.V[b] - r.V[d]).sqrMagnitude;
            if (d1 <= d2) { r.T.Add(a); r.T.Add(b); r.T.Add(c); r.T.Add(a); r.T.Add(c); r.T.Add(d); }
            else { r.T.Add(a); r.T.Add(b); r.T.Add(d); r.T.Add(b); r.T.Add(c); r.T.Add(d); }
        }
    }
}
