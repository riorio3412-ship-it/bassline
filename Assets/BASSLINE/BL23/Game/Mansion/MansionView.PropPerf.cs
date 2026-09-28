using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BL23.Game.Mansion
{
    /// <summary>
    /// Keeping the dressing cheap: static clutter and model decor are merged into one mesh per material per room, and
    /// the physical props stay kinematic (no simulation, no settling contacts) until the player comes within reach —
    /// then they are real rigidbodies that can be knocked, grabbed and thrown, and they freeze again once at rest and
    /// out of reach.
    /// </summary>
    public sealed partial class MansionView
    {
        const float PropWakeRadius = 3.2f, PropSleepRadius = 4.5f;
        float _propTimer;

        /// <summary>Merge every renderer under 'gos' (except flames) into per-material combined meshes under the room.</summary>
        internal int CombineStatic(RoomView rv, List<GameObject> gos, string name)
        {
            if (gos == null || gos.Count == 0) return 0;
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var order = new List<Material>();
            var toKill = new List<GameObject>();
            var root = rv.Root; var inv = root.worldToLocalMatrix;
            foreach (var g in gos)
            {
                if (g == null) continue;
                bool any = false;
                foreach (var mr in g.GetComponentsInChildren<MeshRenderer>())
                {
                    var mf = mr.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null || mr.gameObject.name == "flame" || !mf.sharedMesh.isReadable) continue;
                    var mats = mr.sharedMaterials; var mesh = mf.sharedMesh;
                    for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
                    {
                        var m = mats[s]; if (m == null) continue;
                        if (!groups.TryGetValue(m, out var list)) { list = new List<CombineInstance>(); groups[m] = list; order.Add(m); }
                        list.Add(new CombineInstance { mesh = mesh, subMeshIndex = s, transform = inv * mf.transform.localToWorldMatrix });
                    }
                    rv.Renderers.Remove(mr);
                    any = true;
                }
                if (any) toKill.Add(g);
            }
            int made = 0;
            foreach (var m in order)
            {
                var list = groups[m];
                // keep each merged mesh within a 32-bit index budget that stays cheap to upload
                for (int start = 0; start < list.Count; start += 256)
                {
                    var part = list.GetRange(start, Math.Min(256, list.Count - start));
                    var mesh = new Mesh { name = name + "_" + m.name, indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(part.ToArray(), true, true);
                    mesh.RecalculateBounds();
                    var go = new GameObject(name + "_" + made); go.transform.SetParent(root, false);
                    go.layer = ClutterLayer;
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = ShadowCastingMode.Off;
                    rv.Renderers.Add(mr);
                    made++;
                }
            }
            // flames (billboards) survive the merge: re-parent them to the room before the props go
            foreach (var g in toKill)
            {
                foreach (Transform t in g.GetComponentsInChildren<Transform>())
                    if (t != null && t.name == "flame") t.SetParent(root, true);
                foreach (var l in g.GetComponentsInChildren<Light>()) l.transform.SetParent(root, true);
                if (Application.isPlaying) Destroy(g); else DestroyImmediate(g);
            }
            return made;
        }

        /// <summary>A physical prop starts frozen (kinematic): nothing to simulate, nothing to settle.</summary>
        internal static void Freeze(Rigidbody rb)
        {
            if (rb == null) return;
            rb.isKinematic = true;
        }

        /// <summary>Wake props within reach of the camera; re-freeze those at rest and out of reach.</summary>
        void UpdateProps(Vector3 cam)
        {
            _propTimer -= Time.unscaledDeltaTime;
            if (_propTimer > 0f || Rooms == null) return;
            _propTimer = 0.2f;
            float w2 = PropWakeRadius * PropWakeRadius, s2 = PropSleepRadius * PropSleepRadius;
            foreach (var rv in Rooms)
            {
                if (rv == null || !rv.Visible || rv.PhysProps.Count == 0) continue;
                // cheap reject: room box far from the camera
                var R = rv.Room.Rect;
                float dx = Math.Max(0, Math.Max(R.x0 - cam.x, cam.x - R.x1)), dz = Math.Max(0, Math.Max(R.z0 - cam.z, cam.z - R.z1));
                bool nearRoom = dx * dx + dz * dz < s2 && Math.Abs(cam.y - (rv.FloorY + 1.2f)) < 3.5f;
                for (int i = rv.PhysProps.Count - 1; i >= 0; i--)
                {
                    var rb = rv.PhysProps[i];
                    if (rb == null) { rv.PhysProps.RemoveAt(i); continue; }
                    var physical = rb.GetComponent<Physicality.PhysicalProp>();
                    if (physical != null && !physical.AllowsSimulation) { rb.isKinematic = true; continue; }
                    var pos = rb.position;
                    // cheap reject only for pieces still inside their own room (a chair carried elsewhere is checked directly)
                    if (!nearRoom && pos.x > R.x0 && pos.x < R.x1 && pos.z > R.z0 && pos.z < R.z1) { if (!rb.isKinematic && rb.IsSleeping()) rb.isKinematic = true; continue; }
                    float d2 = (pos - cam).sqrMagnitude;
                    if (rb.isKinematic)
                    {
                        if (d2 < w2) { rb.isKinematic = false; rb.Sleep(); var pm = rb.GetComponent<PropMaterial>(); if (pm != null) pm.Settle(0.6f); }
                    }
                    else if (d2 > s2 && rb.IsSleeping()) rb.isKinematic = true;
                }
            }
        }
    }
}
