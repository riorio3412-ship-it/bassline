using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BL23.Game.Characters;
using BL23.Game.Physicality;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// Motion track QA (2026-09-27 night): reactions, everyday actions around PhysicalActionController, attacks and firearms,
    /// paired prolonged kills, restraints and ambient life. Sheets: mo_react, mo_everyday, mo_attack, mo_paired, mo_ambient
    /// (each writes Shots/mo_<sheet>_<id>.png, strips of labelled frames).
    /// </summary>
    public static partial class CharacterQA
    {
        /// <summary>Batch: bake (-bakeIds), retarget the clip sets (-animSets) and render sheets (-qaIds / -qaSheets) in one session.</summary>
        public static void BakeRetargetShots() { CharacterBaker.BakeSome(); ClipRetarget.RetargetAll(); ShotsQuick(); }

        static void Register_Motion2()
        {
            AddSheet("mo_react", ids => { foreach (var id in ids) MoReact(id); });
            AddSheet("mo_everyday", ids => { foreach (var id in ids) MoEveryday(id); });
            AddSheet("mo_attack", ids => { foreach (var id in ids) MoAttack(id); });
            AddSheet("mo_paired", ids => { foreach (var id in ids) MoPaired(id); });
            AddSheet("mo_ambient", ids => { foreach (var id in ids) MoAmbient(id); });
        }

        // ================================================================ stepping / strips for one or two actors
        sealed class Mo
        {
            public ActorRig A, B;
            public Transform HA, HB;
            public PhysicalRagdoll Rag; public MethodInfo RagLate;
            public Vector3 Move; public float MoveSpeed;             // holder motion of A (m/s along Move)
            public float Turn;                                         // holder yaw rate of A (deg/s)
            public readonly Dictionary<string, object> Bag = new Dictionary<string, object>();
        }

        static void MoStep(Mo m, float dt)
        {
            if (m.MoveSpeed > 0f && m.HA != null) m.HA.position += m.Move.normalized * m.MoveSpeed * dt;
            if (m.Turn != 0f && m.HA != null) m.HA.rotation = Quaternion.Euler(0f, m.HA.eulerAngles.y + m.Turn * dt, 0f);
            m.A.Anim.Tick(dt); if (m.B != null) m.B.Anim.Tick(dt);
            if (m.Rag != null && m.Rag.Active) { Physics.Simulate(dt); }
            if (m.Rag != null && m.RagLate != null) m.RagLate.Invoke(m.Rag, null);
            var steps = m.A.GetComponentsInChildren<IActorStep>().ToList();
            if (m.B != null) steps.AddRange(m.B.GetComponentsInChildren<IActorStep>());
            foreach (var s in steps.Distinct().OrderBy(s => s.StepOrder)) s.Step(dt);
        }

        static List<Texture2D> MoStrip(string label, Func<Mo> scene, float[] times, Action<Mo> start, Action<Mo, float> tick,
            Func<Mo, (Vector3 target, float yaw, float pitch, float dist, float fov)> cam, int w = 300, int h = 400, float settle = 0.6f)
            => MoStripF(label, scene, _ => times, start, tick, cam, w, h, settle);

        /// <summary>As MoStrip, with the frame times chosen after start (e.g. around an attack's impact time).</summary>
        static List<Texture2D> MoStripF(string label, Func<Mo> scene, Func<Mo, float[]> timesF, Action<Mo> start, Action<Mo, float> tick,
            Func<Mo, (Vector3 target, float yaw, float pitch, float dist, float fov)> cam, int w = 300, int h = 400, float settle = 0.6f)
        {
            Clear();
            var m = scene();
            m.HA = Holder(m.A); if (m.B != null) m.HB = Holder(m.B);
            Physics.SyncTransforms();
            m.A.Anim.AutoFidget = false; if (m.B != null) m.B.Anim.AutoFidget = false;
            for (float t0 = 0; t0 < settle; t0 += MDT) MoStep(m, MDT);
            start?.Invoke(m);
            Physics.SyncTransforms();
            var times = timesF(m);
            foreach (var smr in _stage.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.forceMatrixRecalculationPerRender = true;
            var tiles = new List<Texture2D>();
            float t = 0f;
            foreach (var at in times)
            {
                while (t < at - 1e-4f) { tick?.Invoke(m, t); MoStep(m, MDT); t += MDT; }
                if (m.A.Face != null) m.A.Face.SnapShapes();
                if (m.B != null && m.B.Face != null) m.B.Face.SnapShapes();
                var c = cam(m);
                var fill = AddFill(0.45f);
                LookAt(c.target, c.yaw, c.pitch, c.dist, c.fov);
                fill.transform.rotation = _cam.transform.rotation;
                var tex = ShootTex(w, h);
                UnityEngine.Object.DestroyImmediate(fill.gameObject);
                Label(tex, tiles.Count == 0 ? label : t.ToString("0.00").Replace(".", "_"));
                tiles.Add(tex);
                Debug.Log($"[MO] {_moSheet} {label} t={t:F2} hips={m.HA.InverseTransformPoint(m.A.Bone(HBone.Hips).position).ToString("F3")} handR={m.HA.InverseTransformPoint(m.A.HandAnchorR.position).ToString("F2")} {m.A.Anim.DebugReach} | {m.A.Anim.DebugFeet} | {m.A.Anim.DebugState}");
            }
            // each strip also on its own (full resolution, one row) for close review
            try
            {
                var dir = System.IO.Path.Combine(ShotDir, "mo"); System.IO.Directory.CreateDirectory(dir);
                var row = new Texture2D(w * tiles.Count, h, TextureFormat.RGB24, false);
                for (int i = 0; i < tiles.Count; i++) row.SetPixels(i * w, 0, w, h, tiles[i].GetPixels());
                row.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, _moSheet + "_" + label + ".png"), row.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(row);
            }
            catch (Exception e) { Debug.LogWarning("[MO] strip save failed " + e.Message); }
            return tiles;
        }

        static string _moSheet = "mo";
        static Mo One(string id, float yaw = 0f) => new Mo { A = McSpawn(id, Vector3.zero, yaw) };
        static (Vector3, float, float, float, float) Side(Mo m, float yaw = 70f, float y = 0.9f, float dist = 3.4f) => (m.HA.position + Vector3.up * y, yaw, 6f, dist, 30f);

        static PhysicalActionController Ctl(ActorRig r)
        {
            var c = r.GetComponent<PhysicalActionController>();
            if (c == null) c = r.gameObject.AddComponent<PhysicalActionController>();
            c.Bind(r);
            return c;
        }

        static GameObject Prop(string n, Vector3 pos, Vector3 size, Color col) => McBox(n, pos, size, col, true);

        static string Other(string id) => id == "P05" ? "P02" : "P05";

        // ================================================================ reactions
        static void MoReact(string id)
        {
            _moSheet = "react_" + id;
            var tiles = new List<Texture2D>();
            float[] T5 = { 0f, 0.08f, 0.22f, 0.5f, 1.0f, 1.6f };
            foreach (var hc in new (string n, BodyRegion r, Vector3 dir, float yaw)[] {
                ("HIT_HEAD", BodyRegion.Head, new Vector3(0.3f, 0, -1f), 60f), ("HIT_GUT", BodyRegion.Abdomen, new Vector3(0, 0, -1f), 80f),
                ("HIT_CHEST", BodyRegion.Chest, new Vector3(0, 0, -1f), 80f), ("HIT_ARM_R", BodyRegion.ArmR, new Vector3(-1f, 0, 0), 30f),
                ("HIT_LEG_L", BodyRegion.LegL, new Vector3(1f, 0, -0.3f), 40f), ("HIT_BACK", BodyRegion.Back, new Vector3(0, 0, 1f), 110f) })
                tiles.AddRange(MoStrip(hc.n, () => One(id), T5, m => m.A.Anim.PlayHit(hc.r, m.HA.TransformDirection(hc.dir), 0.8f), null, m => Side(m, hc.yaw)));
            // stagger: pushed back with the root moved like PhysicalCharacter does, and sideways with no root motion
            float[] T6 = { 0f, 0.15f, 0.35f, 0.6f, 0.9f, 1.3f };
            tiles.AddRange(MoStrip("STAG_BACK_ROOT", () => One(id), T6, m => { m.A.Anim.PlayStagger(-m.HA.forward, 0.8f); m.Move = -m.HA.forward; m.MoveSpeed = 0.9f; },
                (m, t) => m.MoveSpeed = Mathf.Max(0f, 0.9f * Mathf.Exp(-t * 3f)), m => Side(m, 90f, 0.9f, 3.8f)));
            tiles.AddRange(MoStrip("STAG_SIDE_FIXED", () => One(id), T6, m => m.A.Anim.PlayStagger(m.HA.right, 0.7f), null, m => Side(m, 10f, 0.9f, 3.8f)));
            tiles.AddRange(MoStrip("STARTLE", () => One(id), new[] { 0f, 0.1f, 0.25f, 0.5f, 0.9f, 1.4f }, m => m.A.Anim.PlayStartle(m.HA.position + m.HA.right * 2f + Vector3.up, 0.9f), null, m => Side(m, 30f)));
            tiles.AddRange(MoStrip("GUNSHOT_DUCK", () => One(id), new[] { 0f, 0.1f, 0.3f, 0.7f, 1.4f, 2.1f }, m => m.A.Anim.HearNoise(m.HA.position - m.HA.right * 4f + Vector3.up, 1f), null, m => Side(m, 40f)));
            tiles.AddRange(MoStrip("FALL_FWD_ANIM", () => One(id), new[] { 0f, 0.1f, 0.22f, 0.4f, 0.65f, 1.0f }, m => m.A.Anim.PlayFall(m.HA.forward, 0.9f), null, m => Side(m, 90f, 0.7f, 3.6f)));
            tiles.AddRange(MoStrip("FALL_BACK_ANIM", () => One(id), new[] { 0f, 0.1f, 0.22f, 0.4f, 0.65f, 1.0f }, m => m.A.Anim.PlayFall(-m.HA.forward, 0.9f), null, m => Side(m, 90f, 0.7f, 3.6f)));
            // fall into the owner's PhysicalRagdoll (the protective reach overlay), then get up from where the body landed
            tiles.AddRange(MoStrip("FALL_RAGDOLL", () =>
            {
                var m = One(id);
                m.Rag = Holder(m.A).gameObject.AddComponent<PhysicalRagdoll>(); m.Rag.Bind(m.A, null);
                m.RagLate = typeof(PhysicalRagdoll).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
                Physics.simulationMode = SimulationMode.Script;
                return m;
            }, new[] { 0f, 0.12f, 0.3f, 0.5f, 0.8f, 1.4f, 2.0f, 2.6f, 3.3f, 4.2f }, m => m.A.Anim.PlayFall(m.HA.forward, 0.9f),
            (m, t) =>
            {
                if (t >= 0.12f && !m.Bag.ContainsKey("rag")) { m.Bag["rag"] = 1; m.Rag.Begin(m.HA.forward * 1.6f, m.HA.forward * 40f, m.A.Bone(HBone.Chest).position); }
                if (t >= 1.9f && !m.Bag.ContainsKey("up"))
                {
                    m.Bag["up"] = 1;
                    Vector3 pel = m.Rag.PelvisPosition; m.HA.position = new Vector3(pel.x, 0f, pel.z);
                    m.A.Anim.SetPosture(Posture.LieBack); m.Rag.End(false); m.A.Anim.SetPosture(Posture.Stand);
                }
            }, m => (m.A.Bone(HBone.Hips).position + Vector3.up * 0.2f, 80f, 14f, 3.6f, 32f)));
            tiles.AddRange(MoStrip("GETUP_FACEDOWN", () => One(id), new[] { 0f, 0.4f, 0.8f, 1.2f, 1.7f, 2.3f, 2.8f },
                m => { m.A.Anim.SetPosture(Posture.LieFront); for (int i = 0; i < 60; i++) MoStep(m, MDT); m.A.Anim.SetPosture(Posture.Stand); }, null, m => Side(m, 70f, 0.6f, 3.6f)));
            tiles.AddRange(MoStrip("GETUP_FACEUP", () => One(id), new[] { 0f, 0.4f, 0.8f, 1.2f, 1.7f, 2.3f, 2.8f },
                m => { m.A.Anim.SetPosture(Posture.LieBack); for (int i = 0; i < 60; i++) MoStep(m, MDT); m.A.Anim.PlayGetUp(0.3f); }, null, m => Side(m, 70f, 0.6f, 3.6f)));
            // brace on a table while staggering toward it (PhysicalActionController.Brace gives the point)
            tiles.AddRange(MoStrip("BRACE_TABLE", () => { var m = One(id); Prop("Table", new Vector3(0f, 0.37f, 0.85f), new Vector3(1.2f, 0.74f, 0.6f), new Color(0.35f, 0.22f, 0.14f)); return m; },
                new[] { 0f, 0.15f, 0.3f, 0.5f, 0.8f, 1.2f }, m => { m.A.Anim.PlayStagger(m.HA.forward, 0.75f); Ctl(m.A).Brace(new Vector3(0.12f, 0.74f, 0.62f), Vector3.up, false, 1.0f); },
                null, m => Side(m, 80f, 0.8f, 3.6f)));
            tiles.AddRange(MoStrip("PAIN_LIMP_WALK", () => One(id), new[] { 0f, 0.3f, 0.6f, 0.9f },
                m => { m.A.Anim.SetPain(0.8f); m.A.Anim.SetWoundHold(BodyRegion.Abdomen); m.A.Anim.SetInjury(0.5f, false, false, true, false, true); m.A.Anim.SetMove(m.HA.forward * 0.8f, false); m.Move = m.HA.forward; m.MoveSpeed = 0.8f; },
                null, m => Side(m, 70f)));
            SaveGrid("mo_react_" + id, tiles, 10);
        }

        // ================================================================ everyday
        static void MoEveryday(string id)
        {
            _moSheet = "everyday_" + id;
            var tiles = new List<Texture2D>();
            float[] T = { 0f, 0.25f, 0.45f, 0.6f, 0.8f, 1.1f };
            foreach (var pk in new (string n, Vector3 pos, float table)[] { ("PICK_FLOOR", new Vector3(0.12f, 0.03f, 0.45f), 0f), ("PICK_TABLE", new Vector3(0.1f, 0.78f, 0.55f), 0.74f), ("PICK_LOWSHELF", new Vector3(0.1f, 0.47f, 0.5f), 0.44f), ("PICK_HIGHSHELF", new Vector3(0.05f, 1.72f, 0.42f), 1.68f) })
                tiles.AddRange(MoStrip(pk.n, () =>
                {
                    var m = One(id);
                    if (pk.table > 0f) Prop("Surface", new Vector3(0f, pk.table * 0.5f, pk.pos.z + 0.12f), new Vector3(0.9f, pk.table, 0.5f), new Color(0.35f, 0.22f, 0.14f));
                    m.Bag["item"] = Prop("Item", pk.pos, new Vector3(0.08f, 0.06f, 0.08f), new Color(0.8f, 0.7f, 0.2f));
                    return m;
                }, T, m => Ctl(m.A).BeginPickup(((GameObject)m.Bag["item"]).transform, false, () => { var it = ((GameObject)m.Bag["item"]).transform; it.SetParent(m.A.HandAnchorR, true); }), null, m => Side(m, 90f, 0.8f, 3.4f)));
            // synchronised hand-over between two people
            tiles.AddRange(MoStrip("HANDOVER", () =>
            {
                var m = new Mo { A = McSpawn(id, new Vector3(0f, 0f, 0f), 0f), B = McSpawn(Other(id), new Vector3(0f, 0f, 0.95f), 180f) };
                m.Bag["item"] = Prop("Letter", Vector3.zero, new Vector3(0.12f, 0.02f, 0.16f), new Color(0.9f, 0.88f, 0.8f));
                return m;
            }, new[] { 0f, 0.3f, 0.55f, 0.7f, 0.9f, 1.3f }, m =>
            {
                var it = ((GameObject)m.Bag["item"]).transform; it.SetParent(m.A.HandAnchorR, false); it.localPosition = Vector3.zero;
                Ctl(m.B); Ctl(m.A).BeginHandover(Ctl(m.B), false, () => { it.SetParent(m.B.HandAnchorR, true); }, null, false);
            }, null, m => (m.HA.position + new Vector3(0f, 1.1f, 0.47f), 90f, 4f, 3.0f, 32f)));
            tiles.AddRange(MoStrip("CARRY_HEAVY_WALK", () => One(id), new[] { 0f, 0.3f, 0.6f, 0.9f },
                m => { m.A.Anim.SetCarryLoad(16f, true); m.A.Anim.PlayAction(ActionAnim.CarryHeavy, 99f); m.A.Anim.SetMove(m.HA.forward * 0.9f, false); m.Move = m.HA.forward; m.MoveSpeed = 0.9f; }, null, m => Side(m, 80f)));
            tiles.AddRange(MoStrip("LEAN_ON_TABLE", () => { var m = One(id); Prop("Table", new Vector3(0.45f, 0.37f, 0.25f), new Vector3(0.6f, 0.74f, 1.0f), new Color(0.35f, 0.22f, 0.14f)); return m; },
                new[] { 0f, 0.5f, 1.2f, 2.5f }, m => m.A.Anim.LeanOn(new Vector3(0.28f, 0.74f, 0.12f), Vector3.up, false), null, m => Side(m, 20f)));
            foreach (var act in new[] { ActionAnim.PushChair, ActionAnim.OpenDrawer, ActionAnim.Pour, ActionAnim.PourPoison, ActionAnim.LiftBody })
                tiles.AddRange(MoStrip(act.ToString().ToUpperInvariant(), () => One(id), new[] { 0f, 0.25f, 0.45f, 0.65f, 0.85f, 1.0f }.Select(x => x * ActorPoses.DefaultActionDuration(act)).ToArray(),
                    m => m.A.Anim.PlayAction(act, ActorPoses.DefaultActionDuration(act)), null, m => Side(m, 55f)));
            // dragging a body: armpits (walking backward) and ankles (walking forward, arms trailing)
            foreach (var grip in new[] { DragGrip.Armpits, DragGrip.Ankles })
                tiles.AddRange(MoStrip("DRAG_" + grip.ToString().ToUpperInvariant(), () =>
                {
                    var m = new Mo { A = McSpawn(id, Vector3.zero, 0f), B = McSpawn(Other(id), new Vector3(0f, 0f, -1f), 0f) };
                    m.B.Anim.SetDeadPose(0);
                    return m;
                }, new[] { 0f, 0.4f, 0.9f, 1.4f, 2.0f, 2.6f }, m =>
                {
                    m.A.Anim.SetDragging(m.B, grip); m.A.Anim.PlayAction(ActionAnim.Drag, 99f);
                    m.Move = m.HA.forward; m.MoveSpeed = 0.45f; m.A.Anim.SetMove(m.HA.forward * 0.45f, false);
                }, null, m => (m.HA.position + new Vector3(0f, 0.45f, -0.7f), 70f, 16f, 5.4f, 32f)));
            // stairs: feet on the treads while the root glides up the kernel's smooth slope
            tiles.AddRange(MoStrip("STAIRS_UP", () =>
            {
                var m = One(id);
                for (int i = 0; i < 8; i++) Prop("Step" + i, new Vector3(0f, 0.09f + i * 0.09f, 0.74f + i * 0.28f), new Vector3(1.0f, 0.18f + i * 0.18f, 0.28f), new Color(0.3f, 0.2f, 0.14f));
                return m;
            }, new[] { 0f, 0.8f, 1.2f, 1.6f, 2.0f, 2.4f }, m => { m.A.Anim.SetStairs(new Vector3(0f, 0f, 0.6f), new Vector3(0f, 1.44f, 0.6f + 8 * 0.28f), 8); m.Move = m.HA.forward; m.MoveSpeed = 0.9f; m.A.Anim.SetMove(m.HA.forward * 0.9f, false); },
                (m, t) => { var p = m.HA.position; p.y = Mathf.Clamp01((p.z - 0.6f) / (8 * 0.28f)) * 1.44f; m.HA.position = p; }, m => (m.HA.position + Vector3.up * 0.7f, 90f, 4f, 3.6f, 32f)));
            SaveGrid("mo_everyday_" + id, tiles, 6);
        }

        // ================================================================ attacks, firearms, defences
        static void MoAttack(string id)
        {
            _moSheet = "attack_" + id;
            var tiles = new List<Texture2D>();
            foreach (var at in new (ActionAnim k, WeaponClass w, float mass)[] {
                (ActionAnim.Stab, WeaponClass.Knife, 0.25f), (ActionAnim.StabUnder, WeaponClass.Knife, 0.25f), (ActionAnim.StabOver, WeaponClass.Knife, 0.25f),
                (ActionAnim.Slash, WeaponClass.Blade, 0.4f), (ActionAnim.Overhead, WeaponClass.Club, 1.2f), (ActionAnim.SwingSide, WeaponClass.Club, 1.2f),
                (ActionAnim.SwingHeavy, WeaponClass.Heavy, 6f), (ActionAnim.Shove, WeaponClass.None, 0f), (ActionAnim.Kick, WeaponClass.None, 0f), (ActionAnim.Throw, WeaponClass.Club, 0.6f) })
            {
                float impact = 0.4f;
                tiles.AddRange(MoStripF(at.k.ToString().ToUpperInvariant(), () =>
                {
                    var m = new Mo { A = McSpawn(id, Vector3.zero, 0f), B = McSpawn(Other(id), new Vector3(0f, 0f, 0.95f), 180f) };
                    return m;
                }, m => new[] { 0f, impact * 0.45f, impact * 0.8f, impact, impact + 0.1f, impact + 0.28f, impact + 0.7f }, m =>
                {
                    m.A.Anim.SetHeldWeapon(at.w, at.mass);
                    impact = m.A.Anim.PlayAttack(at.k, at.w, m.B.Bone(HBone.Chest).position, 1f);
                }, (m, t) => { if (t >= impact && !m.Bag.ContainsKey("hit")) { m.Bag["hit"] = 1; m.B.Anim.PlayHit(at.k == ActionAnim.Overhead || at.k == ActionAnim.StabOver ? BodyRegion.Head : BodyRegion.Abdomen, m.HA.forward, 0.8f); } },
                m => (m.HA.position + new Vector3(0f, 1.0f, 0.45f), 90f, 4f, 3.4f, 32f)));
            }
            // firearms: pistol duel stance + shot, reload, draw; rifle aim + shot; crossbow cocking, aim + shot
            tiles.AddRange(MoStrip("PISTOL_AIM_SHOOT", () => One(id), new[] { 0f, 0.5f, 0.54f, 0.6f, 0.75f, 1.1f }, m => { m.A.Anim.SetHeldWeapon(WeaponClass.Pistol, 1.1f); m.A.Anim.SetCombatReady(true, WeaponClass.Pistol); m.A.Anim.SetAim(m.HA.position + m.HA.forward * 5f + Vector3.up * 1.3f); },
                (m, t) => { if (t >= 0.5f && !m.Bag.ContainsKey("s")) { m.Bag["s"] = 1; m.A.Anim.PlayAttack(ActionAnim.Shoot, WeaponClass.Pistol); } }, m => Side(m, 70f, 1.1f, 3.4f)));
            tiles.AddRange(MoStrip("PISTOL_RELOAD", () => One(id), new[] { 0f, 0.4f, 0.9f, 1.4f, 2.0f, 2.6f }, m => { m.A.Anim.SetHeldWeapon(WeaponClass.Pistol, 1.1f); m.A.Anim.PlayAction(ActionAnim.Reload, 2.8f); }, null, m => Side(m, 40f, 1.1f, 2.8f)));
            tiles.AddRange(MoStrip("DRAW_FROM_COAT", () => One(id), new[] { 0f, 0.15f, 0.3f, 0.45f, 0.65f, 0.9f }, m => { m.A.Anim.SetHeldWeapon(WeaponClass.Pistol, 1.1f); m.A.Anim.PlayAction(ActionAnim.DrawWeapon, 0.8f); }, null, m => Side(m, 30f, 1.1f, 2.8f)));
            tiles.AddRange(MoStrip("RIFLE_AIM_SHOOT", () => One(id), new[] { 0f, 0.5f, 0.55f, 0.62f, 0.8f, 1.2f }, m => { m.A.Anim.SetHeldWeapon(WeaponClass.Rifle, 3.6f); m.A.Anim.SetCombatReady(true, WeaponClass.Rifle); m.A.Anim.SetAim(m.HA.position + m.HA.forward * 6f + Vector3.up * 1.2f); },
                (m, t) => { if (t >= 0.5f && !m.Bag.ContainsKey("s")) { m.Bag["s"] = 1; m.A.Anim.PlayAttack(ActionAnim.Shoot, WeaponClass.Rifle); } }, m => Side(m, 70f, 1.1f, 3.4f)));
            tiles.AddRange(MoStrip("CROSSBOW_COCK", () => One(id), new[] { 0f, 0.4f, 0.9f, 1.5f, 2.1f, 2.8f }, m => { m.A.Anim.SetHeldWeapon(WeaponClass.Crossbow, 4f); m.A.Anim.PlayAction(ActionAnim.CockCrossbow, 3f); }, null, m => Side(m, 70f, 0.8f, 3.4f)));
            tiles.AddRange(MoStrip("CROSSBOW_AIM_SHOOT", () => One(id), new[] { 0f, 0.5f, 0.54f, 0.6f, 0.9f }, m => { m.A.Anim.SetHeldWeapon(WeaponClass.Crossbow, 4f); m.A.Anim.SetCombatReady(true, WeaponClass.Crossbow); m.A.Anim.SetAim(m.HA.position + m.HA.forward * 6f + Vector3.up * 1.2f); },
                (m, t) => { if (t >= 0.5f && !m.Bag.ContainsKey("s")) { m.Bag["s"] = 1; m.A.Anim.PlayAttack(ActionAnim.Shoot, WeaponClass.Crossbow); } }, m => Side(m, 70f, 1.1f, 3.4f)));
            foreach (var df in new[] { ActionAnim.Defend, ActionAnim.GrabWrist, ActionAnim.TurnAway, ActionAnim.Dodge })
                tiles.AddRange(MoStrip(df.ToString().ToUpperInvariant(), () => new Mo { A = McSpawn(id, Vector3.zero, 0f), B = McSpawn(Other(id), new Vector3(0f, 0f, 0.9f), 180f) },
                    new[] { 0f, 0.15f, 0.3f, 0.5f, 0.8f, 1.1f }, m =>
                    {
                        m.B.Anim.SetHeldWeapon(WeaponClass.Knife, 0.25f); m.B.Anim.PlayAttack(ActionAnim.StabOver, WeaponClass.Knife, m.A.Bone(HBone.Chest).position);
                        m.A.Anim.SetPartner(m.B); m.A.Anim.PlayDefense(df, m.HB.position);
                    }, null, m => (m.HA.position + new Vector3(0f, 1.0f, 0.45f), 90f, 4f, 3.4f, 32f)));
            SaveGrid("mo_attack_" + id, tiles, 7);
        }

        // ================================================================ paired prolonged kills
        static void MoPaired(string id)
        {
            _moSheet = "paired_" + id;
            var tiles = new List<Texture2D>();
            float[] T = { 0.3f, 1.2f, 2.6f, 4.2f, 6.0f, 7.8f, 9.6f };
            foreach (var kind in new[] { PairedKind.Strangle, PairedKind.GarroteRear, PairedKind.LigatureFront, PairedKind.Smother, PairedKind.Drown })
            {
                Func<Mo> scene = () =>
                {
                    ActorAnimator.PairedPlacement(kind, out var vp, out var vy);
                    // the kernel places the victim only roughly: 12 cm off and 15 degrees turned
                    var m = new Mo { A = McSpawn(Other(id), Vector3.zero, 0f), B = McSpawn(id, vp + new Vector3(0.12f, 0f, -0.08f), vy + 15f) };
                    if (kind == PairedKind.Smother) m.B.Anim.SetPosture(Posture.LieBack);
                    if (kind == PairedKind.Drown) Prop("Bath", new Vector3(0f, 0.28f, 1.1f), new Vector3(0.8f, 0.56f, 0.55f), new Color(0.75f, 0.74f, 0.72f));
                    return m;
                };
                Action<Mo> start = m => { m.A.Anim.BeginPaired(kind, PairedRole.Attacker, m.B.Anim, 1f); if (kind == PairedKind.Drown) m.A.Anim.SetPairedSurface(0.55f); };
                Action<Mo, float> tick = (m, t) =>
                {
                    float s = t < 3.5f ? 1f : t < 6.5f ? Mathf.Lerp(1f, 0.35f, (t - 3.5f) / 3f) : t < 8f ? Mathf.Lerp(0.35f, 0f, (t - 6.5f) / 1.5f) : 0f;
                    m.A.Anim.SetPairedIntensity(s);
                };
                bool flat = kind == PairedKind.Smother || kind == PairedKind.Drown;
                tiles.AddRange(MoStrip(kind.ToString().ToUpperInvariant(), scene, T, start, tick, m => (m.HA.position + new Vector3(0f, flat ? 0.6f : 1.0f, 0.4f), 75f, flat ? 20f : 6f, 3.2f, 32f)));
                // the victim's face through the struggle: gasping, reddening, then pale and slack
                tiles.AddRange(MoStrip(kind + "_FACE", scene, new[] { 1.2f, 4.2f, 7.8f, 9.6f }, start, tick,
                    m => { var e = m.B.EyeAnchor != null ? m.B.EyeAnchor.position : m.B.Bone(HBone.Head).position; var f = m.B.Bone(HBone.Head).forward; float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg; return (e, yaw + 20f, flat ? 50f : 5f, 0.8f, 26f); }));
            }
            SaveGrid("mo_paired_" + id, tiles, 7);
        }

        // ================================================================ restraints + ambient life
        static void MoAmbient(string id)
        {
            _moSheet = "ambient_" + id;
            var tiles = new List<Texture2D>();
            tiles.AddRange(MoStrip("BOUND_FRONT_GAG", () => One(id), new[] { 0f, 1f, 2f, 3f }, m => { m.A.Anim.SetRestraint(RestraintFlags.WristsFront | RestraintFlags.Gag); m.A.Anim.SetRestraintStruggle(0.7f); }, null, m => Side(m, 30f, 1.1f, 2.6f)));
            tiles.AddRange(MoStrip("BOUND_BACK_ANKLES", () => One(id), new[] { 0f, 1f, 2f, 3f }, m => { m.A.Anim.SetRestraint(RestraintFlags.WristsBack | RestraintFlags.Ankles); m.A.Anim.SetRestraintStruggle(0.8f); }, null, m => Side(m, 140f, 0.9f, 3.2f)));
            tiles.AddRange(MoStrip("HOP_BOUND", () => One(id), new[] { 0f, 0.2f, 0.4f, 0.6f }, m => { m.A.Anim.SetRestraint(RestraintFlags.WristsBack | RestraintFlags.Ankles); m.A.Anim.SetMove(m.HA.forward * 0.5f, false); m.Move = m.HA.forward; m.MoveSpeed = 0.5f; }, null, m => Side(m, 90f)));
            tiles.AddRange(MoStrip("COLD", () => One(id), new[] { 0f, 1.5f, 3f, 3.2f }, m => m.A.Anim.SetCold(0.95f), null, m => Side(m, 20f, 1.1f, 2.6f)));
            tiles.AddRange(MoStrip("CONSOLE", () => new Mo { A = McSpawn(id, Vector3.zero, 30f), B = McSpawn(Other(id), new Vector3(0.35f, 0f, 0.55f), 210f) },
                new[] { 0f, 0.8f, 1.6f, 2.4f }, m => { m.B.Anim.PlayGesture(Gesture.Cry, 6f); m.B.SetExpression(Expr.Crying, 1f); m.A.Anim.ConsolePartner(m.B, 5f); }, null, m => (m.HA.position + new Vector3(0.2f, 1.2f, 0.3f), 100f, 6f, 2.8f, 32f)));
            foreach (var g in new[] { Gesture.CoverMouth, Gesture.CheckWatch, Gesture.AdjustClothes, Gesture.Apologize, Gesture.HandsUp, Gesture.HandToBurn, Gesture.Hug, Gesture.DuckCover })
            {
                float dur = ActorPoses.DefaultGestureDuration(g);
                tiles.AddRange(MoStrip(g.ToString().ToUpperInvariant(), () => One(id), new[] { 0f, 0.3f, 0.5f, 0.7f }.Select(x => x * dur).ToArray(), m => m.A.Anim.PlayGesture(g, dur), null, m => Side(m, 30f, 1.2f, 2.6f)));
            }
            tiles.AddRange(MoStrip("PASS_BY", () => new Mo { A = McSpawn(id, Vector3.zero, 0f), B = McSpawn(Other(id), new Vector3(0.45f, 0f, 1.2f), 180f) },
                new[] { 0f, 0.3f, 0.5f, 0.9f }, m => m.A.Anim.PassBy(m.HB.position), null, m => (m.HA.position + Vector3.up * 1.1f, 0f, 6f, 3.2f, 32f)));
            tiles.AddRange(MoStrip("BUMPED_SORRY", () => One(id), new[] { 0f, 0.1f, 0.5f, 0.9f }, m => m.A.Anim.Bumped(m.HA.position + m.HA.right * 0.5f, true), null, m => Side(m, 40f, 1.2f, 2.6f)));
            tiles.AddRange(MoStrip("TURN_ON_SPOT", () => One(id), new[] { 0f, 0.4f, 0.8f, 1.2f, 1.6f, 2.0f }, m => m.Turn = 110f, (m, t) => { if (t > 1.0f) m.Turn = 0f; }, m => (m.HA.position + Vector3.up * 0.3f, 20f, 35f, 2.6f, 32f)));
            tiles.AddRange(MoStrip("WEIGHT_SHIFT", () => One(id), new[] { 0f, 2f, 4f, 6f }, m => m.A.Anim.PlayGesture(Gesture.WeightShift), null, m => (m.HA.position + Vector3.up * 0.9f, 0f, 4f, 3.2f, 30f)));
            SaveGrid("mo_ambient_" + id, tiles, 6);
        }
    }
}
