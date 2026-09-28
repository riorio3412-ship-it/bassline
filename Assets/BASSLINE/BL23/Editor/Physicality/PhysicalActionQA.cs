using System;
using System.Reflection;
using BL23.Game;
using BL23.Game.Characters;
using BL23.Game.Physicality;
using BL23.Sim;
using UnityEngine;

namespace BL23.EditorTools.Physicality
{
    public static class PhysicalActionQA
    {
        public static void RunChecks(Action<string, bool, string> report)
        {
            var root = new GameObject("ContactIK_QA");
            try
            {
                var lower = new GameObject("Lower").transform; lower.SetParent(root.transform, false); lower.localPosition = new Vector3(0, -0.4f, 0);
                var hand = new GameObject("Hand").transform; hand.SetParent(lower, false); hand.localPosition = new Vector3(0, -0.35f, 0);
                Vector3 target = new Vector3(0.3f, -0.5f, 0.1f);
                ProceduralContactIK.SolveTwoBone(root.transform, lower, hand, target, Vector3.forward);
                report("Two-bone hand contact", Vector3.Distance(hand.position, target) < 0.002f, "Reach error " + Vector3.Distance(hand.position, target));
                report("IK preserves limb lengths", Mathf.Abs(Vector3.Distance(root.transform.position, lower.position) - 0.4f) < 0.0001f &&
                    Mathf.Abs(Vector3.Distance(lower.position, hand.position) - 0.35f) < 0.0001f, "No skeletal stretching");
                ProceduralContactIK.SolveTwoBone(root.transform, lower, hand, Vector3.right * 5f, Vector3.right);
                report("Unreachable and collinear IK stays finite", Finite(hand.position) && hand.position.magnitude <= 0.751f,
                    "Clamped reach " + hand.position.magnitude);
                ProceduralContactIK.SolveTwoBone(root.transform, lower, hand, Vector3.zero, Vector3.zero);
                report("Zero target and pole stays finite", Finite(hand.position), hand.position.ToString());
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }

            ActorRig rig = MakeRig();
            var targetObject = new GameObject("ContactTarget_QA");
            try
            {
                var actions = rig.gameObject.AddComponent<PhysicalActionController>();
                actions.Bind(rig); actions.EnableFootPlacement = false;
                rig.Anim.AutoFidget = false;
                rig.Anim.Tick(0.02f);
                targetObject.transform.position = rig.HandAnchorR.position + Vector3.forward * 0.12f;
                int contacts = 0, completions = 0;
                bool result = false;
                actions.BeginPickup(targetObject.transform, false, () => contacts++, ok => { completions++; result = ok; });
                for (int i = 0; i < 100; i++) rig.Anim.Tick(0.02f);
                report("Pickup commits once at contact", contacts == 1 && completions == 1 && result,
                    "contacts=" + contacts + " completed=" + completions + " success=" + result);
                contacts = completions = 0;
                actions.BeginPickup(targetObject.transform, false, () => contacts++, ok => completions++);
                actions.Cancel(); actions.Cancel();
                report("Interrupted action does not transfer", contacts == 0 && completions == 1, "Exactly one cancellation callback");
                targetObject.transform.position = new Vector3(0.22f, 0.1f, 0.32f);
                contacts = 0;
                actions.BeginPickup(targetObject.transform, false, () => contacts++);
                for (int i = 0; i < 100; i++) rig.Anim.Tick(0.02f);
                report("Floor pickup uses squat and reaches contact", contacts == 1, "Low target at 0.1 metres");
                var tablePoint = new Vector3(0.22f, 0.78f, 0.42f);
                actions.Brace(tablePoint, Vector3.up, false, 0.7f);
                for (int i = 0; i < 12; i++) rig.Anim.Tick(0.02f);
                report("Table brace reaches real support", Vector3.Distance(rig.HandAnchorR.position, tablePoint) < 0.14f,
                    "Hand error=" + Vector3.Distance(rig.HandAnchorR.position, tablePoint));
                actions.Cancel();
                rig.Anim.SetInjury(1f, false, true, false, false, true);
                bool accepted = actions.BeginPickup(targetObject.transform, false, () => contacts++);
                report("Injured hand cannot accept action", !accepted && !actions.Busy, "Right arm disabled");
                rig.Anim.SetInjury(1f, false, false, false, false, true);
                rig.Anim.PhysicsDriven = true;
                report("Ragdoll cannot accept hand action", !actions.Reach(targetObject.transform.position), "Physics owns the skeleton");
                rig.Anim.PhysicsDriven = false;
                var front = new ActorPose(); var back = new ActorPose();
                actions.ReactToImpact(new Vector3(0.2f, 1.3f, 0), Vector3.forward, 1f); actions.PreparePose(0f, front);
                actions.ReactToImpact(new Vector3(0.2f, 1.3f, 0), Vector3.back, 1f); actions.PreparePose(0f, back);
                report("Hit direction reverses recoil", Quaternion.Angle(front.R[(int)HBone.Spine], back.R[(int)HBone.Spine]) > 20f,
                    "Signed force changes torso rotation");
                var high = new ActorPose(); var low = new ActorPose();
                actions.ReactToImpact(new Vector3(0, 1.7f, 0), Vector3.forward, 1f); actions.PreparePose(0f, high);
                actions.ReactToImpact(new Vector3(0, 0.4f, 0), Vector3.forward, 1f); actions.PreparePose(0f, low);
                report("Head and leg contacts affect different bones", Quaternion.Angle(high.R[(int)HBone.Head], low.R[(int)HBone.Head]) > 10f &&
                    Quaternion.Angle(high.R[(int)HBone.Hips], low.R[(int)HBone.Hips]) > 5f, "Local hit height selects response distribution");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(targetObject);
                UnityEngine.Object.DestroyImmediate(rig.gameObject);
            }
            RunTransferChecks(report);
        }

        static void RunTransferChecks(Action<string, bool, string> report)
        {
            Session previous = Session.I;
            var sessionObject = new GameObject("TransferSession_QA");
            var itemObject = new GameObject("TransferItem_QA");
            ActorRig giver = MakeRig(), receiver = MakeRig();
            ItemView view = null;
            try
            {
                var session = sessionObject.AddComponent<Session>(); session.Headless = true;
                session.Sim = new Simulation { Headless = true, S = new GameState { Phase = Phase.Daily, Layout = new Layout() } };
                Session.I = session;
                session.World = sessionObject.AddComponent<WorldPresenter>(); SetField(session.World, "_s", session);
                giver.ActorId = "QA:giver"; receiver.ActorId = "QA:receiver";
                receiver.transform.position = new Vector3(0.75f, 0, 0.1f);
                var a = new Actor { Id = giver.ActorId, StairId = -1 };
                var b = new Actor { Id = receiver.ActorId, StairId = -1 };
                session.S.Actors[a.Id] = a; session.S.Actors[b.Id] = b;
                var aView = giver.gameObject.AddComponent<ActorView>(); aView.Rig = giver; aView.Id = a.Id;
                var bView = receiver.gameObject.AddComponent<ActorView>(); bView.Rig = receiver; bView.Id = b.Id;
                session.World.Actors[a.Id] = aView; session.World.Actors[b.Id] = bView;
                var item = new Item { Id = "QA:transfer", Type = "Book", Holder = a.Id, InHand = true };
                session.S.Items[item.Id] = item; a.HandR = item.Id;
                view = itemObject.AddComponent<ItemView>(); view.Id = item.Id;
                view.Visual = new GameObject("Only visual instance"); view.Visual.transform.SetParent(itemObject.transform, false);
                var body = view.Visual.AddComponent<Rigidbody>(); view.Visual.AddComponent<BoxCollider>().size = Vector3.one * 0.04f;
                SetField(view, "_rb", body); SetField(view, "W", session.World); session.World.Items[item.Id] = view;
                giver.Anim.Tick(0.02f); receiver.Anim.Tick(0.02f);
                view.Visual.transform.position = giver.HandAnchorR.position + Vector3.forward * 0.1f;
                PhysicalItemTransfer.Attach(view, giver, false, false);
                report("Pickup transfer retains authoritative owner", view.ContactTransfer && item.Holder == a.Id && a.HandR == item.Id,
                    "Only presentation is pending");
                a.HandR = null; item.Holder = null; item.InHand = false;
                PhysicalItemTransfer.Release(view, giver, false);
                report("Rapid pickup then drop releases once", !view.ContactTransfer && !view.Held && !body.isKinematic && item.Holder == null,
                    "No stale attach callback or frozen object");

                a.HandR = item.Id; item.Holder = a.Id; item.InHand = true;
                PhysicalItemTransfer.Attach(view, giver, false, false);
                giver.GetComponent<PhysicalActionController>().Cancel();
                report("Pickup interruption reconciles latest inventory", !view.ContactTransfer && view.HeldAnchor == giver.HandAnchorR && item.Holder == a.Id,
                    "Cancelled presentation still matches authoritative ownership");
                a.HandR = null; b.HandL = item.Id; item.Holder = b.Id;
                PhysicalItemTransfer.Attach(view, receiver, true, false);
                bool awaiting = view.ContactTransfer && view.HeldAnchor == giver.HandAnchorR;
                giver.GetComponent<PhysicalActionController>().Cancel();
                report("Cancelled handover ends in recipient hand", awaiting && !view.ContactTransfer && view.HeldAnchor == receiver.HandAnchorL &&
                    item.Holder == b.Id && b.HandL == item.Id, "Transfer uses the requested receiving hand");

                b.HandL = null; a.HandR = item.Id; item.Holder = a.Id;
                PhysicalItemTransfer.Attach(view, giver, false, false);
                a.HandR = null; b.HandL = item.Id; item.Holder = b.Id;
                PhysicalItemTransfer.Attach(view, receiver, true, false);
                receiver.GetComponent<PhysicalActionController>().Cancel();
                report("Rapid handover replacement cannot duplicate item", !view.ContactTransfer && view.HeldAnchor == receiver.HandAnchorL &&
                    session.S.Items.Count == 1 && session.World.Items.Count == 1 && item.Holder == b.Id,
                    "Stale callbacks cannot reparent to previous recipient");
            }
            finally
            {
                if (view != null) PhysicalItemTransfer.Abort(view);
                UnityEngine.Object.DestroyImmediate(itemObject);
                UnityEngine.Object.DestroyImmediate(giver.gameObject);
                UnityEngine.Object.DestroyImmediate(receiver.gameObject);
                Session.I = previous;
                UnityEngine.Object.DestroyImmediate(sessionObject);
            }
        }

        static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(target.GetType().Name, name);
            field.SetValue(target, value);
        }

        static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z) &&
            !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);

        static ActorRig MakeRig()
        {
            var go = new GameObject("ActionRig_QA");
            var dimensions = BodyProportions.Make(1.75f, false, 0.5f, 0.5f, 1f);
            var rig = go.AddComponent<ActorRig>();
            rig.Height = dimensions.H; rig.ActorId = "ActionQA";
            rig.HandTipRestL = dimensions.HandTipL; rig.HandTipRestR = dimensions.HandTipR;
            rig.FingerTipRest = dimensions.FingerTip;
            foreach (int index in ActorSkeleton.Order)
            {
                var bone = new GameObject(((HBone)index).ToString()).transform;
                int parent = ActorSkeleton.Parent[index];
                bone.SetParent(parent < 0 ? go.transform : rig.Bones[parent], false);
                bone.position = dimensions.Joint[index]; rig.Bones[index] = bone;
            }
            rig.Hips = rig.Bone(HBone.Hips); rig.Spine = rig.Bone(HBone.Spine); rig.Chest = rig.Bone(HBone.Chest);
            rig.Neck = rig.Bone(HBone.Neck); rig.Head = rig.Bone(HBone.Head);
            rig.HandL = rig.HandAnchorL = rig.Bone(HBone.HandL); rig.HandR = rig.HandAnchorR = rig.Bone(HBone.HandR);
            rig.FootL = rig.Bone(HBone.FootL); rig.FootR = rig.Bone(HBone.FootR);
            var animator = go.AddComponent<ActorAnimator>(); animator.UseClips = false; rig.Anim = animator;
            rig.Init(); animator.Init();
            return rig;
        }
    }
}
