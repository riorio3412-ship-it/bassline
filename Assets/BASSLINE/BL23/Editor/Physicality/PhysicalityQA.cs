using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BL23.Game;
using BL23.Game.Characters;
using BL23.Game.Physicality;
using BL23.Sim;
using BL23.Sim.Physicality;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BL23.EditorTools.Physicality
{
    /// <summary>
    /// Headless, disposable real-PhysX checks. Run with -executeMethod
    /// BL23.EditorTools.Physicality.PhysicalityQA.Run. Requires an empty loaded physics world;
    /// refuses existing Colliders/Rigidbodies before mutation. Existing scenes are never saved.
    /// Report: Verification/Physicality/results.json. Batch mode exits nonzero on any failed check.
    /// </summary>
    public static class PhysicalityQA
    {
        [Serializable] sealed class CheckResult { public string name; public bool passed; public string details; }
        [Serializable] sealed class Report
        {
            public string utc, unityVersion;
            public int passed, failed;
            public List<CheckResult> checks = new List<CheckResult>();
        }
        static Report _report;
        const float Dt = 1f / 120f;

        [MenuItem("BL23/Physicality/Run System QA")]
        public static void Run()
        {
            _report = new Report { utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion };
            try
            {
                PhysicalityTestScene.CheckWorld();
                Check("motor shove obeys character mass", MotorMass);
                Check("motor capsule cannot cross solid wall", MotorWall);
                Check("ragdoll creates bounded constrained bodies", RagdollConstraints);
                Check("ragdoll pause freezes and resumes momentum", RagdollPause);
                Check("full motor fall and get-up returns control", MotorLifecycle);
                Check("blocked motor get-up keeps physics and action gate", BlockedMotorLifecycle);
                Check("support and blocked standing use real collision queries", SupportAndStand);
                Check("own ragdoll colliders do not block standing", OwnRagdollStand);
                Check("legacy incapacitation blocks motor actions", Incapacitation);
                Check("swept contact records side direction and precise hit location", SweptContact);
                Check("solid wall stops swept attack before character", SweptWall);
                Check("attack wind-up and captured direction allow one contact", MeleeWindow);
                InScene(true, (stage, physics) => PhysicalActionQA.RunChecks(Add));
                PropsQA.RunChecks(Add);
            }
            catch (Exception e) { Add("QA harness", false, e.ToString()); }
            finally
            {
                string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification/Physicality/results.json"));
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllText(output, JsonUtility.ToJson(_report, true));
                Debug.Log($"[PhysicalityQA] {_report.passed} passed, {_report.failed} failed. {output}");
            }
            if (Application.isBatchMode) EditorApplication.Exit(_report.failed == 0 ? 0 : 1);
        }

        static void Add(string name, bool passed, string details)
        {
            _report.checks.Add(new CheckResult { name = name, passed = passed, details = details ?? "" });
            if (passed) ++_report.passed; else ++_report.failed;
            Debug.Log($"[PhysicalityQA] {(passed ? "PASS" : "FAIL")} {name}: {details}");
        }
        static void Check(string name, Action test)
        {
            try { test(); Add(name, true, ""); }
            catch (Exception e) { Add(name, false, e is TargetInvocationException && e.InnerException != null ? e.InnerException.ToString() : e.ToString()); }
        }
        static void Require(bool condition, string detail) { if (!condition) throw new InvalidOperationException(detail); }
        static bool Finite(Vector3 p) => !(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z));

        static void InScene(bool localPhysics, Action<Transform, PhysicsScene> run)
        {
            PhysicalityTestScene.Run(run, !localPhysics);
        }

        static BoxCollider Box(Transform stage, string name, Vector3 position, Vector3 size)
        {
            var go = new GameObject(name); go.transform.SetParent(stage, false); go.transform.localPosition = position;
            var box = go.AddComponent<BoxCollider>(); box.size = size; return box;
        }
        static void Floor(Transform stage) => Box(stage, "Floor", new Vector3(0, -.1f, 0), new Vector3(40, .2f, 40));
        static void Simulate(PhysicsScene scene, float seconds)
        {
            Physics.SyncTransforms();
            for (int i = 0, count = Mathf.CeilToInt(seconds / Dt); i < count; ++i) scene.Simulate(Dt);
        }

        static PhysicalCharacter Character(Transform stage, Vector3 position, float mass = 75, float consciousness = 1)
        {
            var go = new GameObject("QA actor"); go.transform.SetParent(stage, false); go.transform.localPosition = position;
            var visual = new GameObject("Synthetic skeleton"); visual.transform.SetParent(go.transform, false);
            var rig = visual.AddComponent<ActorRig>(); rig.ActorId = "QA"; rig.Height = 1.75f;
            var proportions = BodyProportions.Make(1.75f, false, .5f, .5f, 1f);
            var bones = new Transform[ActorSkeleton.Count];
            foreach (int i in ActorSkeleton.Order)
            {
                var bone = new GameObject(ActorSkeleton.Names[i]).transform;
                int parent = ActorSkeleton.Parent[i]; bone.SetParent(parent < 0 ? visual.transform : bones[parent], false);
                bone.position = visual.transform.TransformPoint(proportions.Joint[i]); bones[i] = bone;
            }
            rig.Bones = bones;
            rig.Hips = bones[(int)HBone.Hips]; rig.Spine = bones[(int)HBone.Spine]; rig.Chest = bones[(int)HBone.Chest];
            rig.Neck = bones[(int)HBone.Neck]; rig.Head = bones[(int)HBone.Head];
            rig.HandL = bones[(int)HBone.HandL]; rig.HandR = bones[(int)HBone.HandR];
            rig.FootL = bones[(int)HBone.FootL]; rig.FootR = bones[(int)HBone.FootR];
            rig.HandAnchorL = rig.HandL; rig.HandAnchorR = rig.HandR;
            rig.HandTipRestL = proportions.HandTipL; rig.HandTipRestR = proportions.HandTipR;
            rig.Init(); rig.Anim.Init();
            var view = go.AddComponent<ActorView>(); view.Rig = rig; view.Id = "QA";
            var actor = new Actor { Id = "QA", StairId = -1 }; actor.Body.Conscious = consciousness;
            var motor = go.AddComponent<PhysicalCharacter>(); motor.Tuning.BodyMassKg = mass; motor.Bind(view, actor);
            Physics.SyncTransforms(); return motor;
        }

        static void MotorMass()
        {
            InScene(true, (stage, physics) =>
            {
                Floor(stage); var light = Character(stage, new Vector3(-3, .05f, 0), 50); var heavy = Character(stage, new Vector3(3, .05f, 0), 100);
                light.Shove(Vector3.forward, 30, light.transform.position + Vector3.up);
                heavy.Shove(Vector3.forward, 30, heavy.transform.position + Vector3.up);
                Simulate(physics, Dt);
                Require(!light.Body.isKinematic && !heavy.Body.isKinematic, "Shove did not activate the character rigidbodies.");
                Require(light.Body.linearVelocity.z > heavy.Body.linearVelocity.z * 1.7f && heavy.Body.linearVelocity.z > .1f,
                    $"Same impulse should move lighter body faster: light={light.Body.linearVelocity}, heavy={heavy.Body.linearVelocity}");
            });
        }

        static void MotorWall()
        {
            InScene(true, (stage, physics) =>
            {
                Floor(stage); Box(stage, "Wall", new Vector3(0, 1.5f, 1.2f), new Vector3(4, 3, .3f));
                var actor = Character(stage, new Vector3(0, .05f, 0)); actor.Shove(Vector3.forward, 150, actor.transform.position + Vector3.up);
                Simulate(physics, 1f);
                Require(Finite(actor.Body.position) && actor.Body.position.z > .1f && actor.Body.position.z < .85f,
                    "Character crossed wall or did not move: " + actor.Body.position);
            });
        }

        static void RagdollConstraints()
        {
            InScene(true, (stage, physics) =>
            {
                Floor(stage); var actor = Character(stage, new Vector3(0, .25f, 0));
                Require(actor.Ragdoll.Begin(Vector3.zero, Vector3.forward * 18, actor.transform.position + Vector3.up), "Ragdoll did not start.");
                Require(actor.Ragdoll.BodyCount == 11, "Expected 11 proxy bodies, got " + actor.Ragdoll.BodyCount);
                var joints = stage.GetComponentsInChildren<ConfigurableJoint>(); Require(joints.Length == 10, "Expected 10 connected joints.");
                actor.Ragdoll.Begin(Vector3.zero, Vector3.zero, actor.transform.position);
                Require(actor.Ragdoll.BodyCount == 11, "Repeated Begin allocated more bodies.");
                Simulate(physics, 3f);
                foreach (var joint in joints)
                {
                    Require(joint.connectedBody != null && joint.xMotion == ConfigurableJointMotion.Locked && joint.angularXMotion == ConfigurableJointMotion.Limited,
                        "Missing body or joint limits on " + joint.name);
                    float separation = Vector3.Distance(joint.transform.TransformPoint(joint.anchor), joint.connectedBody.transform.TransformPoint(joint.connectedAnchor));
                    Require(separation < .18f && Finite(joint.transform.position), "Joint detached/exploded: " + joint.name + ", gap=" + separation);
                }
                Require(actor.Ragdoll.PelvisPosition.y > -.15f && actor.Ragdoll.Speed < 4f,
                    $"Ragdoll failed to settle on floor: pelvis={actor.Ragdoll.PelvisPosition}, speed={actor.Ragdoll.Speed}");
                actor.Ragdoll.End(false);
                Require(!actor.Ragdoll.Active && !actor.GetComponent<ActorView>().Rig.Anim.PhysicsDriven, "Ending ragdoll did not restore animation authority.");
            });
        }

        static void RagdollPause()
        {
            InScene(true, (stage, physics) =>
            {
                Floor(stage); var actor = Character(stage, new Vector3(0, 2f, 0));
                actor.Ragdoll.Begin(Vector3.forward * .5f, Vector3.zero, actor.transform.position); Simulate(physics, .1f);
                actor.Ragdoll.SetPaused(true); Vector3 before = actor.Ragdoll.PelvisPosition;
                Simulate(physics, .5f); Require(Vector3.Distance(before, actor.Ragdoll.PelvisPosition) < .0001f, "Paused proxy moved.");
                actor.Ragdoll.SetPaused(false); Simulate(physics, .1f);
                Require(actor.Ragdoll.PelvisPosition.y < before.y - .01f, "Resume did not restore gravity/momentum.");
            });
        }

        static void TickCharacter(PhysicalCharacter actor, PhysicsScene physics)
        {
            float dt = Time.fixedDeltaTime;
            Invoke(actor, "FixedUpdate", Array.Empty<object>());
            Physics.SyncTransforms(); physics.Simulate(dt);
            // ActorView.Tick normally follows the physical motor. It requires the whole campaign
            // presenter, so reproduce only that exact position hand-off on this disposable rig.
            if (actor.DrivesPosition) actor.transform.position = actor.PhysicalPosition;
            actor.GetComponent<ActorView>().Rig.Anim.Tick(dt);
            Invoke(actor.Ragdoll, "LateUpdate", Array.Empty<object>());
        }

        static void MotorLifecycle()
        {
            InScene(true, (stage, physics) =>
            {
                Floor(stage); var actor = Character(stage, Vector3.up * .035f);
                actor.Shove(Vector3.forward, 160, actor.transform.position + Vector3.up * 1.1f);
                bool fell = false, prone = false, recovered = false;
                for (int i = 0, ticks = Mathf.CeilToInt(14f / Time.fixedDeltaTime); i < ticks; ++i)
                {
                    TickCharacter(actor, physics);
                    fell |= actor.Ragdoll.Active;
                    prone |= actor.State.Posture == PhysicalPosture.Prone;
                    recovered |= fell && !actor.Ragdoll.Active && actor.State.Posture == PhysicalPosture.Standing;
                }
                var domainActor = (Actor)GetField(actor, "_actor");
                Require(fell && prone && recovered, $"Missing full transition: fell={fell} prone={prone} recovered={recovered} posture={actor.State.Posture} pelvis={actor.Ragdoll.PelvisPosition}");
                Require(!actor.Ragdoll.Active && actor.Body.isKinematic && actor.Capsule.enabled && !domainActor.PhysicsDriven && !actor.BlocksActions && actor.State.CanAct,
                    $"Control did not return: ragdoll={actor.Ragdoll.Active}, kinematic={actor.Body.isKinematic}, capsule={actor.Capsule.enabled}, physicsOwns={domainActor.PhysicsDriven}, blocked={actor.BlocksActions}");
                Require(Finite(actor.transform.position) && actor.transform.position.y >= -.05f, "Recovery left actor below the floor.");
            });
        }

        static void BlockedMotorLifecycle()
        {
            InScene(true, (stage, physics) =>
            {
                Floor(stage); var actor = Character(stage, Vector3.up * .035f);
                actor.Shove(Vector3.forward, 160, actor.transform.position + Vector3.up * 1.1f);
                bool ceilingAdded = false;
                for (int i = 0, ticks = Mathf.CeilToInt(14f / Time.fixedDeltaTime); i < ticks; ++i)
                {
                    TickCharacter(actor, physics);
                    if (!ceilingAdded && actor.Ragdoll.Active && actor.State.Posture == PhysicalPosture.Prone)
                    {
                        var p = stage.InverseTransformPoint(actor.Ragdoll.PelvisPosition);
                        Box(stage, "Blocked recovery ceiling", new Vector3(p.x, .95f, p.z), new Vector3(10, .2f, 10));
                        Physics.SyncTransforms(); ceilingAdded = true;
                    }
                }
                var domainActor = (Actor)GetField(actor, "_actor");
                Require(ceilingAdded, "Actor never reached a prone pose for obstruction test.");
                Require(actor.Ragdoll.Active && !actor.Capsule.enabled && domainActor.PhysicsDriven && actor.BlocksActions,
                    $"Blocked get-up released control: ragdoll={actor.Ragdoll.Active}, capsule={actor.Capsule.enabled}, physicsOwns={domainActor.PhysicsDriven}, blocked={actor.BlocksActions}");
            });
        }

        static void SupportAndStand()
        {
            InScene(false, (stage, physics) =>
            {
                Floor(stage); var actor = Character(stage, Vector3.zero);
                Require(CallBool(actor, "ClearToStand", actor.transform.position), "Empty standing volume was blocked.");
                var ceiling = Box(stage, "Low obstruction", new Vector3(0, 1.3f, 0), new Vector3(1, .25f, 1));
                Physics.SyncTransforms(); Require(!CallBool(actor, "ClearToStand", actor.transform.position), "Low obstruction allowed get-up.");
                Object.DestroyImmediate(ceiling.gameObject);
                Box(stage, "Support table", new Vector3(0, .7f, .55f), new Vector3(1, .2f, .5f));
                Physics.SyncTransforms(); object[] args = { Vector3.forward, default(RaycastHit) };
                Require((bool)Invoke(actor, "FindSupport", args), "Reachable table support was missed.");
                var hit = (RaycastHit)args[1]; Require(hit.collider != null && hit.collider.name == "Support table", "Support selected an own/floor collider.");
                actor.State.LegacyGripLeft = actor.State.LegacyGripRight = 0;
                BodySolver.ApplyLegacyLimits(actor.State, 1, 0, 0, 1, false);
                args = new object[] { Vector3.forward, default(RaycastHit) };
                Require(!(bool)Invoke(actor, "FindSupport", args), "Failed hands still offered support.");
            });
        }

        static void OwnRagdollStand()
        {
            InScene(false, (stage, physics) =>
            {
                Floor(stage); var actor = Character(stage, Vector3.zero);
                actor.Ragdoll.Begin(Vector3.zero, Vector3.zero, actor.transform.position + Vector3.up);
                Physics.SyncTransforms();
                Require(CallBool(actor, "ClearToStand", actor.transform.position), "Own sibling ragdoll colliders block get-up.");
                actor.Ragdoll.End(false);
            });
        }

        static void Incapacitation()
        {
            InScene(true, (stage, physics) =>
            {
                var actor = Character(stage, Vector3.zero, consciousness: 0);
                Require(actor.BlocksActions && actor.State.Posture == PhysicalPosture.Incapacitated, "Legacy unconsciousness did not gate actions.");
                actor.Startle(actor.transform.position + Vector3.forward, 1);
                Require(actor.State.Reaction == PhysicalReaction.Incapacitated, "Startle replaced incapacitation.");
            });
        }

        static void WithMelee(Action<Transform, PhysicalMelee, Session, PhysicalCharacter> run)
        {
            InScene(false, (stage, physics) =>
            {
                var previous = Session.I;
                try
                {
                    var go = new GameObject("QA session"); go.transform.SetParent(stage, false);
                    var session = go.AddComponent<Session>(); session.Headless = true;
                    session.Sim = Simulation.NewCampaign(20260927); session.Sim.Headless = true; session.S.Phase = Phase.Daily;
                    session.World = go.AddComponent<WorldPresenter>(); Session.I = session;
                    var playerObject = new GameObject("QA player"); playerObject.transform.SetParent(stage, false);
                    var player = playerObject.AddComponent<PlayerController>(); session.Player = player;
                    var cameraObject = new GameObject("QA aim"); cameraObject.transform.SetParent(player.transform, false); cameraObject.transform.localPosition = Vector3.up * 1.42f;
                    player.Cam = cameraObject.AddComponent<Camera>(); player.Cam.enabled = false;
                    var weapon = new Item { Id = "qa:melee", Type = "KitchenKnife", Holder = Cast.Player, InHand = true };
                    session.S.Items[weapon.Id] = weapon; session.S.Player.HandR = weapon.Id;
                    var target = Character(stage, new Vector3(0, 0, 1.3f)); target.GetComponent<ActorView>().Rig.ActorId = "P03";
                    session.S.Flags["attackconfirm:P03"] = 1;
                    var victim = session.S.A("P03"); victim.Pos = new P3(session.S.Player.Pos.f, session.S.Player.Pos.x, session.S.Player.Pos.z + 1.3f); victim.Room = session.S.Player.Room; victim.Yaw = 180;
                    var melee = playerObject.AddComponent<PhysicalMelee>(); melee.Init(session, player); SetField(melee, "_weapon", weapon);
                    Physics.SyncTransforms(); run(stage, melee, session, target);
                }
                finally { Session.I = previous; }
            });
        }

        static void SweptContact()
        {
            WithMelee((stage, melee, session, target) =>
            {
                var rig = target.GetComponent<ActorView>().Rig; Vector3 contact = rig.HeadCenterWorld();
                bool hit = (bool)Invoke(melee, "Sweep", new object[] { contact - Vector3.right, contact + Vector3.right, contact - Vector3.right, Vector3.right, 5f });
                var wounds = session.S.A("P03").Body.Wounds;
                Require(hit && wounds.Count == 1, "Fast sweep failed to create exactly one contact wound.");
                var wound = wounds[0];
                Require(wound.HasContact && wound.dx > .99f && Mathf.Abs(wound.dz) < .01f && wound.ContactImpulse > 0,
                    "Incoming side direction/impulse did not reach saved wound.");
                Require(wound.ly > 1.25f && wound.lx < 0 && Mathf.Abs(wound.lz) < .3f,
                    $"Hit location is not precise actor-local upper-body contact: ({wound.lx},{wound.ly},{wound.lz}).");
                Require(wound.nx < -.5f, "Contact normal was not preserved.");
            });
        }

        static void SweptWall()
        {
            WithMelee((stage, melee, session, target) =>
            {
                Vector3 point = target.GetComponent<ActorView>().Rig.HeadCenterWorld();
                var wall = Box(stage, "Attack obstruction", stage.InverseTransformPoint(point - Vector3.right * .55f), new Vector3(.1f, 1, 1));
                Physics.SyncTransforms();
                bool stopped = (bool)Invoke(melee, "Sweep", new object[] { point - Vector3.right, point + Vector3.right, point - Vector3.right, Vector3.right, 8f });
                Require(stopped && session.S.A("P03").Body.Wounds.Count == 0, "Swept attack damaged actor through wall.");
            });
        }

        static void MeleeWindow()
        {
            WithMelee((stage, melee, session, target) =>
            {
                Require(melee.Begin(), "Armed ready player could not begin attack.");
                session.Player.Cam.transform.rotation = Quaternion.Euler(0, 180, 0);
                for (int i = 0, ticks = Mathf.FloorToInt(.12f / Time.fixedDeltaTime); i < ticks; ++i) Invoke(melee, "FixedUpdate", Array.Empty<object>());
                Require(session.S.A("P03").Body.Wounds.Count == 0, "Attack damaged during wind-up.");
                for (int i = 0, ticks = Mathf.CeilToInt(.8f / Time.fixedDeltaTime); i < ticks; ++i) Invoke(melee, "FixedUpdate", Array.Empty<object>());
                Require(session.S.A("P03").Body.Wounds.Count == 1, "Turning aim changed committed swing or one swing struck repeatedly.");
                Require(!melee.Swinging, "Attack did not leave recovery window.");
            });
        }

        static bool CallBool(object target, string method, Vector3 argument) => (bool)Invoke(target, method, new object[] { argument });
        static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(target.GetType().Name, name);
            field.SetValue(target, value);
        }
        static object GetField(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(target.GetType().Name, name);
            return field.GetValue(target);
        }
        static object Invoke(object target, string method, object[] args)
        {
            var found = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            if (found == null) throw new MissingMethodException(target.GetType().Name, method);
            return found.Invoke(target, args);
        }
    }
}
