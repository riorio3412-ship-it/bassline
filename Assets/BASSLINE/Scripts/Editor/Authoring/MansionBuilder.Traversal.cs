using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BASSLINE.AuthoringData;
using UnityEngine;

namespace BASSLINE.Authoring
{
    public static partial class MansionBuilder
    {
        [Serializable] sealed class TraversalFailure
        {
            public string Route, Direction, Collider, ControllerFlags;
            public float Speed;
            public int Waypoint;
            public Vector3 Expected, Actual;
        }
        [Serializable] sealed class TraversalReceipt
        {
            public string Builder, Editor, Scope, Result;
            public int Routes, Probes, Passed, Moves;
            public float Height = TallestResidentHeight, Radius = .28f, StepOffset = .32f, ArrivalTolerance = .14f;
            public TraversalFailure[] Failures;
        }

        static void VerifyCharacterTraversal()
        {
            var receipt = new TraversalReceipt { Builder = BuilderVersion, Editor = Application.unityVersion, Scope = "TestOnly Editor CharacterController probes against generated physical geometry, actual door leaves open; not PlayMode progression or crowded navigation." };
            var failures = new List<TraversalFailure>();
            var routes = connections.Where(c => c.Kind != "Stair").Select(c => (name: c.ConnectionId, points: c.Route)).ToList();
            routes.AddRange(stairPaths.Select((s, i) => ("STAIR_" + s.room + "_" + i, s.points))); receipt.Routes = routes.Count;
            var initial = connections.Select(c => c.OpenAmount).ToArray();
            var probe = new GameObject("TEST_ONLY_M01_TRAVERSAL_CAPSULE");
            var controller = probe.AddComponent<UnityEngine.CharacterController>(); controller.height = TallestResidentHeight; controller.radius = .28f;
            controller.center = Vector3.up * TallestResidentHeight * .5f; controller.stepOffset = .32f; controller.skinWidth = .025f; controller.minMoveDistance = 0;
            try
            {
                foreach (var c in connections) c.ApplyOpenAmount(1);
                Physics.SyncTransforms();
                foreach (var route in routes) foreach (float speed in new[] { 1.4f, 4.8f }) foreach (bool reverse in new[] { false, true })
                {
                    receipt.Probes++; var points = reverse ? route.points.Reverse().ToArray() : route.points;
                    controller.enabled = false; probe.transform.position = points[0] + Vector3.up * .035f; controller.enabled = true; Physics.SyncTransforms();
                    for (int settle = 0; settle < 3; settle++) controller.Move(Vector3.down * (2f / 60));
                    bool passed = true;
                    for (int i = 1; i < points.Length; i++)
                    {
                        int maximum = Mathf.CeilToInt(Vector3.Distance(probe.transform.position, points[i]) / speed * 120) + 180;
                        int count = 0; CollisionFlags flags = CollisionFlags.None;
                        while (Vector3.Distance(probe.transform.position, points[i]) >= .14f && count++ < maximum)
                        {
                            Vector3 delta = points[i] - probe.transform.position;
                            flags = controller.Move(Vector3.ClampMagnitude(delta, speed / 60) + Vector3.down * (2f / 60)); receipt.Moves++;
                        }
                        if (Vector3.Distance(probe.transform.position, points[i]) < .14f) continue;
                        physicalFailure = "No body overlap at rest; blocked during motion or step-up"; BodyClear(probe.transform.position, false);
                        failures.Add(new TraversalFailure { Route = route.name, Direction = reverse ? "Reverse" : "Forward", Speed = speed, Waypoint = i, Expected = points[i], Actual = probe.transform.position, Collider = physicalFailure, ControllerFlags = flags.ToString() });
                        passed = false; break;
                    }
                    if (passed) receipt.Passed++;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
                for (int i = 0; i < connections.Count; i++) connections[i].ApplyOpenAmount(initial[i]);
                Physics.SyncTransforms();
            }
            receipt.Failures = failures.ToArray(); receipt.Result = failures.Count == 0 ? "PASS_EDITOR_PHYSICAL_TRAVERSAL" : "FAIL";
            Directory.CreateDirectory("Verification"); File.WriteAllText("Verification/mansion-character-traversal.json", JsonUtility.ToJson(receipt, true));
            if (failures.Count > 0) throw new InvalidOperationException("M01 actual CharacterController traversal failed " + failures.Count + "/" + receipt.Probes + ": " + string.Join(" | ", failures.Take(6).Select(f => f.Route + " " + f.Direction + " " + f.Speed + " at " + f.Actual + " expected " + f.Expected)) + ". See Verification/mansion-character-traversal.json.");
        }
    }
}
