using System;
using System.Collections.Generic;
using System.Linq;
using BL23.Game.Characters;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// charpolish implementer 3 (motion) QA sheets. Sheets: mc_hooks (M0 hooks), plus the motion sheets registered in
    /// Register_Motion. Every sheet renders PNGs into Shots/ and records checks (names with "!" are blockers).
    /// </summary>
    public static partial class CharacterQA
    {
        const float MDT = 1f / 60f;

        static void Register_Motion()
        {
            AddSheet("mc_hooks", ids => { foreach (var id in ids) McHooks(id); });
        }

        // ---------------------------------------------------------------- shared helpers
        sealed class McCtx { public ActorRig R, R2; public Transform H, H2; public float Yaw; public Vector3 Pos; public readonly Dictionary<string, object> Bag = new Dictionary<string, object>(); }

        static Transform Holder(ActorRig r) => r.transform.parent != null ? r.transform.parent : r.transform;

        static void McStep(ActorRig r, float dt)
        {
            if (r == null) return;
            r.Anim.Tick(dt);
            foreach (var c in r.GetComponentsInChildren<IActorStep>().OrderBy(c => c.StepOrder)) c.Step(dt);
        }

        static ActorRig McSpawn(string id, Vector3 pos, float yaw)
        {
            var r = Spawn(id, pos, yaw);
            r.Anim.AutoFidget = false;
            return r;
        }

        static GameObject McBox(string n, Vector3 center, Vector3 size, Color col, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = n;
            go.transform.SetParent(_stage, false);
            go.transform.position = center; go.transform.localScale = size;
            var m = new Material(Shader.Find("BL23/ToonCharacter"));
            m.SetColor("_BaseColor", col);
            m.SetFloat("_OutlineWidth", 0f);
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            var c = go.GetComponent<Collider>(); if (c != null && !collider) UnityEngine.Object.DestroyImmediate(c);
            return go;
        }

        /// <summary>Renders frames of one scripted situation at the given times: scene -> spawn -> start -> tick until each time -> shoot.</summary>
        static List<Texture2D> McStrip(string label, string id, float[] times, Action<McCtx> start, Action<McCtx, float> tick,
            Func<McCtx, (Vector3 target, float yaw, float pitch, float dist, float fov)> cam, Action<McCtx> scene = null, int w = 300, int h = 400, Action<McCtx, float, Texture2D> onShot = null)
        {
            Clear();
            var ctx = new McCtx();
            scene?.Invoke(ctx);
            ctx.R = ctx.R ?? McSpawn(id, ctx.Pos, ctx.Yaw);
            ctx.H = Holder(ctx.R);
            if (ctx.R2 != null) ctx.H2 = Holder(ctx.R2);
            for (float t0 = 0; t0 < 0.5f; t0 += MDT) { McStep(ctx.R, MDT); McStep(ctx.R2, MDT); }
            start?.Invoke(ctx);
            foreach (var smr in _stage.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.forceMatrixRecalculationPerRender = true;
            var tiles = new List<Texture2D>();
            float t = 0f;
            foreach (var at in times)
            {
                while (t < at - 1e-4f) { tick?.Invoke(ctx, t); McStep(ctx.R, MDT); McStep(ctx.R2, MDT); t += MDT; }
                if (ctx.R.Face != null) ctx.R.Face.SnapShapes();
                var c = cam(ctx);
                var fill = AddFill(0.45f);
                LookAt(c.target, c.yaw, c.pitch, c.dist, c.fov);
                fill.transform.rotation = _cam.transform.rotation;
                var tex = ShootTex(w, h);
                UnityEngine.Object.DestroyImmediate(fill.gameObject);
                Label(tex, tiles.Count == 0 ? label : t.ToString("0.00").Replace(".", "_"));
                onShot?.Invoke(ctx, t, tex);
                tiles.Add(tex);
            }
            return tiles;
        }

        static (Vector3, float, float, float, float) McSide(McCtx c, float y = 0.95f, float dist = 3.6f, float yaw = 90f) => (c.H.position + Vector3.up * y, yaw, 6f, dist, 30f);

        // ---------------------------------------------------------------- mc_hooks: the M0 contract hooks (stagger, hit offsets, dead override, inertial blend)
        static void McHooks(string id)
        {
            var tiles = new List<Texture2D>();
            float[] times = { 0f, 0.12f, 0.3f, 0.5f, 0.75f, 1.05f, 1.4f };
            // stagger pushed backward (strength 0.5) and to the right (0.9)
            foreach (var cs in new[] { ("STAG_BACK", new Vector3(0, 0, -1), 0.5f, 90f), ("STAG_RIGHT", new Vector3(1, 0, 0), 0.9f, 0f), ("STAG_FWD", new Vector3(0, 0, 1), 1f, 90f) })
            {
                float maxHips = 0f; Vector3 hips0 = Vector3.zero;
                tiles.AddRange(McStrip(cs.Item1, id, times,
                    c => { hips0 = c.H.InverseTransformPoint(c.R.Bone(HBone.Hips).position); c.R.Anim.PlayStagger(cs.Item2, cs.Item3); },
                    (c, t) => { var hp = c.H.InverseTransformPoint(c.R.Bone(HBone.Hips).position); maxHips = Mathf.Max(maxHips, new Vector2(hp.x - hips0.x, hp.z - hips0.z).magnitude); },
                    c => McSide(c, 0.9f, 3.8f, cs.Item4)));
                Check("mc_hooks stagger displacement " + cs.Item1, id, maxHips > 0.05f, maxHips, 0.05f, "hips horizontal travel (m)");
            }
            // hit offset: a 20 deg forward chest bend written once, then fading
            {
                Clear();
                var r = McSpawn(id, Vector3.zero, 0f);
                for (float t = 0; t < 0.5f; t += MDT) McStep(r, MDT);
                var before = r.Bone(HBone.Chest).rotation;
                r.Anim.SetHitOffset(HBone.Chest, Quaternion.AngleAxis(20f, Vector3.right));
                McStep(r, MDT);
                float a1 = Quaternion.Angle(before, r.Bone(HBone.Chest).rotation);
                for (float t = 0; t < 0.4f; t += MDT) McStep(r, MDT);
                float a2 = Quaternion.Angle(before, r.Bone(HBone.Chest).rotation);
                Check("mc_hooks hit offset applied", id, a1 > 8f, a1, 8f, "chest rotation change the frame it is written (deg; chest carries half on rigs with an UpperChest)");
                Check("mc_hooks hit offset fades", id, a2 < 3f, a2, 3f, "0.4 s after the last write (deg)");
            }
            // dead override: snapshot of a modified dead pose is shown as is
            {
                Clear();
                var r = McSpawn(id, Vector3.zero, 0f);
                r.Anim.SetDeadPose(0);
                for (float t = 0; t < 2.5f; t += MDT) McStep(r, MDT);
                var snap = new ActorPose(); r.Anim.ReadCanonical(snap);
                snap.R[(int)HBone.Hips] = Quaternion.AngleAxis(35f, Vector3.up) * snap.R[(int)HBone.Hips];
                snap.R[(int)HBone.UpperArmR] = snap.R[(int)HBone.UpperArmR] * Quaternion.AngleAxis(40f, Vector3.right);
                r.Anim.SetDeadOverride(snap);
                r.Anim.BlendFromCurrentBones(0.3f);
                var first = r.Bone(HBone.Hips).rotation;
                McStep(r, MDT);
                float jump = Quaternion.Angle(first, r.Bone(HBone.Hips).rotation);
                for (float t = 0; t < 0.6f; t += MDT) McStep(r, MDT);
                var shown = new ActorPose(); r.Anim.ReadCanonical(shown);
                float err = Quaternion.Angle(shown.R[0], snap.R[0]);
                Check("mc_hooks inertial blend no pop", id, jump < 6f, jump, 6f, "hips rotation change on the first blended frame (deg)");
                Check("mc_hooks dead override shown", id, err < 2f, err, 2f, "hips rotation vs the override after 0.6 s (deg)");
                var fill = AddFill(0.45f);
                LookAt(Holder(r).position + Vector3.up * 0.3f, 30f, 25f, 3.2f, 30f);
                fill.transform.rotation = _cam.transform.rotation;
                var tex = ShootTex(300, 400); Label(tex, "DEAD_OVERRIDE"); tiles.Add(tex);
                UnityEngine.Object.DestroyImmediate(fill.gameObject);
            }
            SaveGrid("mc_hooks_" + id, tiles, times.Length);
        }
    }
}
