using System;
using System.Linq;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game.Mansion
{
    public sealed partial class MansionView
    {
        /// <summary>Per room, the visuals of every fixed piece of furniture (not movable, no rigidbody, nothing animated)
        /// are merged into one mesh per material — a big room's hundred pieces become a few dozen draw calls. Movable
        /// pieces keep their own renderers and join the room's static batch as before.</summary>
        internal static bool MergeFixedFurniture = false;

        void BatchStatics()
        {
            int merged = 0, batched = 0, meshes = 0;
            foreach (var rv in Rooms)
            {
                if (rv == null || rv.Root == null) continue;
                var list = new System.Collections.Generic.List<GameObject>();
                var groups = new System.Collections.Generic.Dictionary<Material, System.Collections.Generic.List<CombineInstance>>();
                var order = new System.Collections.Generic.List<Material>();
                var inv = rv.Root.worldToLocalMatrix;
                bool court = rv.Room.Type == RoomType.Courtroom;
                foreach (var fid in rv.Room.Furniture)
                {
                    if (!FurnitureGo.TryGetValue(fid, out var go) || go == null || go.GetComponent<Rigidbody>() != null) continue;
                    var f = Layout.Furniture[fid]; var def = FurnitureCatalog.Get(f.Type);
                    bool special = go.GetComponentInChildren<PressView>() != null || go.GetComponentInChildren<Swinger>() != null || go.GetComponentInChildren<Spinner>() != null || go.GetComponentInChildren<FishSchool>() != null || go.GetComponentInChildren<SwitchboardView>() != null;
                    // merging by material did not pay off (scanned models carry unique materials, and room-sized merged casters defeat
                    // per-face shadow culling): fixed pieces stay separate renderers in the room's static batch
                    bool merge = MergeFixedFurniture && !court && !special && def != null && !def.Movable;
                    if (merge)
                    {
                        foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
                        {
                            var mf = mr.GetComponent<MeshFilter>();
                            if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                            var mats = mr.sharedMaterials; var mesh = mf.sharedMesh;
                            if (mats.Length < mesh.subMeshCount) continue;
                            for (int s = 0; s < mesh.subMeshCount; s++)
                            {
                                var m = mats[s]; if (m == null) continue;
                                if (!groups.TryGetValue(m, out var gl)) { gl = new System.Collections.Generic.List<CombineInstance>(); groups[m] = gl; order.Add(m); }
                                gl.Add(new CombineInstance { mesh = mesh, subMeshIndex = s, transform = inv * mf.transform.localToWorldMatrix });
                            }
                            rv.Renderers.Remove(mr);
                            if (Application.isPlaying) { Destroy(mr); Destroy(mf); } else { DestroyImmediate(mr); DestroyImmediate(mf); }
                        }
                        Merged.Add(fid); merged++;
                        continue;
                    }
                    Batched.Add(fid);
                    var opn = go.GetComponent<OpenableParts>();
                    foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (opn != null && opn.IsMoving(mf.transform)) continue;   // doors, drawers and lids must stay movable
                        if (mf.sharedMesh == null || !mf.sharedMesh.isReadable || mf.GetComponent<MeshRenderer>() == null) continue;
                        if (mf.GetComponentInParent<PressView>() != null || mf.GetComponentInParent<Swinger>() != null || mf.GetComponentInParent<Spinner>() != null || mf.GetComponentInParent<FishSchool>() != null || mf.GetComponentInParent<SwitchboardView>() != null) continue;
                        list.Add(mf.gameObject);
                    }
                }
                foreach (var m in order)
                {
                    var gl = groups[m];
                    for (int start = 0; start < gl.Count; start += 200)
                    {
                        var part = gl.GetRange(start, Math.Min(200, gl.Count - start));
                        var mesh = new Mesh { name = "Furniture_" + rv.Room.Id + "_" + m.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                        mesh.CombineMeshes(part.ToArray(), true, true);
                        mesh.RecalculateBounds();
                        var mgo = new GameObject("Furniture_" + m.name); mgo.transform.SetParent(rv.Root, false);
                        mgo.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var mmr = mgo.AddComponent<MeshRenderer>(); mmr.sharedMaterial = m; mmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                        rv.Renderers.Add(mmr); meshes++;
                    }
                }
                if (list.Count > 1) { try { StaticBatchingUtility.Combine(list.ToArray(), rv.Root.gameObject); batched += list.Count; } catch (Exception e) { Debug.LogWarning("[Mansion] static batch " + rv.Room.Name + ": " + e.Message); } }
            }
            Debug.Log($"[Mansion] furniture: {merged} fixed pieces merged into {meshes} meshes, {batched} movable meshes static-batched");
        }

        void BuildAllFurniture()
        {
            foreach (var f in Layout.Furniture)
            {
                var rv = f.Room >= 0 && f.Room < Rooms.Length ? Rooms[f.Room] : null;
                if (rv == null) continue;
                try
                {
                    var go = FurnitureFactory.Build(this, rv, f);
                    FurnitureGo[f.Id] = go;
                    NavGrid.GetFootprint(f, 0.15f, out var fr);
                    rv.Blocked.Add(fr);
                }
                catch (Exception e) { Debug.LogWarning($"[Mansion] furniture {f.Type} #{f.Id}: {e.Message}"); }
            }
            // the power room must show its per-circuit switchboard even when the kernel could not fit one
            foreach (var r in Layout.Rooms)
            {
                if (r.Type != RoomType.PowerRoom || r.Furniture.Any(fid => Layout.Furniture[fid].Type == "Switchboard")) continue;
                var rv = Rooms[r.Id];
                for (int i = 0; i < rv.WallSlots.Count; i++)
                {
                    var p = rv.WallSlots[i]; var n = rv.WallSlotN[i]; var rg = rv.WallSlotRange[i];
                    if (rg.y - rg.x < 2.7f) continue;
                    float along = Mathf.Abs(n.z) > 0.5f ? p.x : p.z;
                    along = Mathf.Clamp((rg.x + rg.y) * 0.5f, rg.x + 1.3f, rg.y - 1.3f);
                    var c = Mathf.Abs(n.z) > 0.5f ? new Vector3(along, p.y, p.z) : new Vector3(p.x, p.y, along);
                    if (!WallFree(rv, c, n, 1.3f, rv.FloorY)) continue;
                    var pos = c + n * 0.2f;
                    var sf = new Furniture { Id = -2000 - r.Id, Room = r.Id, Type = "Switchboard", Pos = new P3(r.Floor, pos.x, pos.z), Yaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg, W = 2.4f, D = 0.4f, H = 2.0f, Material = Mat.Metal };
                    try { FurnitureFactory.Build(this, rv, sf); NavGrid.GetFootprint(sf, 0.15f, out var fr); rv.Blocked.Add(fr); } catch (Exception e) { Debug.LogWarning("[Mansion] synthetic switchboard: " + e.Message); }
                    break;
                }
            }
            // visual-only pools for Pool rooms whose PoolWater could not be placed by the layout
            foreach (var f in SynthPools)
            {
                var rv = Rooms[f.Room];
                try
                {
                    FurnitureFactory.Build(this, rv, f);
                    NavGrid.GetFootprint(f, 0.3f, out var fr);
                    rv.Blocked.Add(fr);
                }
                catch (Exception e) { Debug.LogWarning($"[Mansion] synthetic pool: {e.Message}"); }
            }
        }
    }
}
