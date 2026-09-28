using System;
using BL23.Game;
using BL23.Game.Mansion;
using BL23.Game.Physicality;
using BL23.Sim;
using UnityEngine;

namespace BL23.EditorTools.Physicality
{
    /// <summary>Real-PhysX regression checks, guarded by the shared disposable-scene QA harness.</summary>
    public static class PropsQA
    {
        public static void RunChecks(Action<string, bool, string> report)
        {
            Check(report, "props / resting contact is not impact damage", () =>
            {
                Require(PropPhysicsProfile.ImpactEnergy(100f, .2f) == 0f, "Slow resting contact damaged material.");
                Require(Mathf.Abs(PropPhysicsProfile.ImpactEnergy(8f, 4f) - 16f) < .001f, "Impulse work conversion differs.");
                Require(PropPhysicsProfile.ImpactEnergy(float.NaN, 4f) == 0f, "Non-finite impulse accepted.");
                Require(PropPhysicsProfile.For(Mat.Glass).FractureEnergy < PropPhysicsProfile.For(Mat.Wood).FractureEnergy,
                    "Glass is not more fragile than wood.");
            });

            WithScene(report, "props / same finite push respects mass", physics =>
            {
                var light = CreateProp("light", new Vector3(0, 2, 0), 2f);
                var heavy = CreateProp("heavy", new Vector3(0, 2, 4), 20f);
                light.ApplyPush(light.Body.worldCenterOfMass, Vector3.right * 10f, 100f);
                heavy.ApplyPush(heavy.Body.worldCenterOfMass, Vector3.right * 10f, 100f);
                physics.Simulate(.02f);
                Require(light.Body.linearVelocity.x > heavy.Body.linearVelocity.x * 8f, "Equal force did not preserve mass response.");
                Require(heavy.Body.linearVelocity.x > 0f, "Heavy furniture received no movement.");
                var lightProjectile = CreateProp("bounded light projectile", new Vector3(0, 2, 8), .05f);
                lightProjectile.ApplyImpact(lightProjectile.Body.worldCenterOfMass, Vector3.right * 100000f, 0f);
                physics.Simulate(.02f);
                Require(lightProjectile.Body.linearVelocity.x > 0f && lightProjectile.Body.linearVelocity.x <= 35.01f,
                    "A large input impulse launched a tiny prop beyond the configured velocity-change cap.");
            });

            WithScene(report, "props / fracture removes load-bearing chair legs", physics =>
            {
                var go = new GameObject("breakable chair");
                go.AddComponent<BoxCollider>().size = new Vector3(.55f, .95f, .55f);
                var material = go.AddComponent<PropMaterial>(); material.AutoVisuals = false; material.Mat = Mat.Wood;
                var view = go.AddComponent<FurnitureView>(); view.Type = "Chair"; view.Id = -1;
                var f = new Furniture { Type = "Chair", W = .55f, D = .55f, H = .95f, Id = -1 };
                var prop = PhysicalProp.EnsureFurniture(go, f, FurnitureCatalog.Get("Chair"));
                int before = EnabledColliders(go);
                prop.ApplyImpact(go.transform.position, Vector3.zero, 100000f);
                Require(material.Damage == 3 && prop.Broken, "Fracture did not reach persistent damage state.");
                Require(!prop.CanSupport, "Broken furniture can still be selected for support.");
                Require(EnabledColliders(go) == before - 4, "Four load-bearing legs did not lose collision.");
                Require(prop.Body != null && !prop.Body.isKinematic, "Broken seat cannot fall under gravity.");
            });

            WithScene(report, "props / grip ownership cleanup and momentum", physics =>
            {
                var prop = CreateProp("held object", new Vector3(0, 2, 0), 2f);
                var owner = new GameObject("holder").AddComponent<BoxCollider>(); owner.transform.position = new Vector3(0, 2, 3);
                var ownCollider = prop.GetComponent<Collider>();
                using (var first = new PhysicalGrab())
                using (var second = new PhysicalGrab())
                {
                    Require(first.TryAcquire(prop.Body, prop.Body.worldCenterOfMass, owner), "Initial grip refused.");
                    Require(!second.TryAcquire(prop.Body, prop.Body.worldCenterOfMass), "Two holders acquired one object.");
                    Require(Physics.GetIgnoreCollision(ownCollider, owner), "Grip collides with its holder.");
                    prop.Body.linearVelocity = Vector3.right * 2f;
                    first.Release(Vector3.right * 2f);
                    physics.Simulate(.02f);
                    Require(prop.Body.linearVelocity.x > 2.8f, "Throw discarded existing momentum.");
                    Require(!Physics.GetIgnoreCollision(ownCollider, owner), "Release left an ignored collision pair.");
                    Require(second.TryAcquire(prop.Body, prop.Body.worldCenterOfMass), "Released ownership was not available.");
                    Require(!second.MoveGrip(new Vector3(float.NaN, 0, 0), Vector3.zero, .02f), "Invalid target was accepted.");
                    Require(!PhysicalGrab.IsHeld(prop.Body), "Invalid target leaked ownership.");
                }
            });

            WithScene(report, "props / carry remains blocked by a solid wall", physics =>
            {
                var prop = CreateProp("blocked object", new Vector3(0, 2, 0), 2f);
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "wall"; wall.transform.position = new Vector3(1f, 2, 0); wall.transform.localScale = new Vector3(.2f, 4f, 4f);
                using (var grip = new PhysicalGrab())
                {
                    Require(grip.TryAcquire(prop.Body, prop.Body.worldCenterOfMass), "Cannot grip test prop.");
                    for (int i = 0; i < 80; i++)
                    {
                        if (grip.Active) grip.MoveGrip(new Vector3(1.8f, 2f, 0), Vector3.zero, .02f);
                        physics.Simulate(.02f);
                    }
                    Require(prop.Body.position.x < .85f, "Grip tunneled through the wall.");
                    Require(!grip.Active, "Sustained blocked carry failed to release.");
                }
            });

            WithScene(report, "props / initial saved fracture and shattered simulation state", physics =>
            {
                var go = new GameObject("saved broken chair");
                go.AddComponent<BoxCollider>().size = new Vector3(.55f, .95f, .55f);
                var material = go.AddComponent<PropMaterial>(); material.AutoVisuals = false;
                go.AddComponent<FurnitureView>().Type = "Chair";
                var f = new Furniture { Type = "Chair", W = .55f, D = .55f, H = .95f, Damage = 3 };
                var broken = PhysicalProp.EnsureFurniture(go, f, FurnitureCatalog.Get("Chair"));
                Require(broken.Broken && !broken.CanSupport && EnabledColliders(go) == 2,
                    "Loading a broken chair recreated intact load-bearing collision.");
                int repeated = 0; broken.BrokenEvent += p => repeated++;
                broken.NotifyDamageChanged(); broken.NotifyDamageChanged();
                Require(repeated == 0, "Collapsed prop published duplicate fracture events.");
                material.Damage = 0; broken.NotifyDamageChanged();
                Require(!broken.Broken && broken.CanSupport && EnabledColliders(go) == 6 && broken.AccumulatedDamageEnergy == 0f,
                    "Repair did not restore supports or retained stale fracture energy.");

                var glass = new GameObject("glass furniture");
                glass.AddComponent<BoxCollider>();
                var glassMaterial = glass.AddComponent<PropMaterial>(); glassMaterial.Mat = Mat.Glass; glassMaterial.AutoVisuals = false;
                glass.AddComponent<FurnitureView>().Type = "Mirror";
                var glassProp = PhysicalProp.EnsureFurniture(glass,
                    new Furniture { Type = "Mirror", W = .9f, D = .2f, H = 2f }, FurnitureCatalog.Get("Mirror"));
                glassProp.ApplyImpact(Vector3.zero, Vector3.zero, 10000f);
                Require(!glassProp.AllowsSimulation && glassProp.Body.isKinematic && EnabledColliders(glass) == 0,
                    "Shattered furniture remained eligible for room wake simulation without collision.");
            });

            WithScene(report, "props / pause restores dynamic momentum and original kinematic ownership", physics =>
            {
                var prop = CreateProp("paused moving prop", new Vector3(0, 2, 0), 2f);
                var coordinator = new GameObject("pause coordinator").AddComponent<PhysicalPropPause>();
                prop.Body.linearVelocity = Vector3.right * 3f;
                coordinator.SendMessage("Freeze", prop);
                Require(prop.Body.isKinematic, "Paused prop was still dynamic.");
                var position = prop.Body.position; physics.Simulate(.1f);
                Require((prop.Body.position - position).sqrMagnitude < .0001f, "Paused prop moved.");
                coordinator.SendMessage("Restore");
                Require(!prop.Body.isKinematic && Mathf.Abs(prop.Body.linearVelocity.x - 3f) < .001f,
                    "Resume lost dynamic ownership or pre-pause momentum.");
                prop.Body.isKinematic = true;
                coordinator.SendMessage("Freeze", prop); coordinator.SendMessage("Restore");
                Require(prop.Body.isKinematic, "Pause stole an initially kinematic owner's body.");

                var previousSession = Session.I;
                try
                {
                    var session = new GameObject("paused repair session").AddComponent<Session>();
                    session.Sim = new Simulation { S = new GameState { Phase = Phase.Daily } }; Session.I = session;
                    var mirror = new GameObject("repair during pause"); mirror.AddComponent<BoxCollider>();
                    var mat = mirror.AddComponent<PropMaterial>(); mat.Mat = Mat.Glass; mat.AutoVisuals = false;
                    mirror.AddComponent<FurnitureView>().Type = "Mirror";
                    var repaired = PhysicalProp.EnsureFurniture(mirror, new Furniture { Type = "Mirror", W = .9f, D = .2f, H = 2f }, FurnitureCatalog.Get("Mirror"));
                    var pause = session.GetComponent<PhysicalPropPause>();
                    session.Pause("QA"); pause.SendMessage("FixedUpdate");
                    mat.Damage = 3; repaired.NotifyDamageChanged();
                    mat.Damage = 0; repaired.NotifyDamageChanged();
                    session.Resume("QA"); pause.SendMessage("FixedUpdate");
                    Require(!repaired.Body.isKinematic && repaired.AllowsSimulation,
                        "Repair during pause permanently froze a formerly dynamic prop.");
                }
                finally { Session.I = previousSession; }
            });

            Check(report, "props / moving furniture updates room spots and both navigation floors", () =>
            {
                var layout = new Layout();
                for (int i = 0; i < 2; i++)
                {
                    layout.Floors.Add(new FloorInfo { F = i, BaseY = i * 4f, Height = 4f, Bounds = new RectF(0, 0, 8, 8) });
                    layout.Rooms.Add(new Room { Id = i, Floor = i, Rect = new RectF(0, 0, 8, 8) });
                }
                var furniture = new Furniture { Id = 0, Room = 0, Type = "Desk", Pos = new P3(0, 2, 2), W = 1f, D = 1f, H = .75f, Blocks = true };
                var spot = new Spot { Id = 0, Room = 0, Furniture = 0, Pos = new P3(0, 2, 2.5f), Approach = new P3(0, 3, 2), Yaw = 0 };
                layout.Furniture.Add(furniture); layout.Spots.Add(spot);
                layout.Rooms[0].Furniture.Add(0); layout.Rooms[0].Spots.Add(0);
                var beforeLower = layout.Nav(0); var beforeUpper = layout.Nav(1);
                Require(beforeLower.Block[beforeLower.CellOf(2, 2)], "Fixture did not start as a navigation blocker.");
                var sim = new Simulation { S = new GameState { Layout = layout } };
                sim.PhysicsFurnitureMoved(furniture, new P3(1, 6, 6), 90f, null);
                Require(furniture.Room == 1 && !layout.Rooms[0].Furniture.Contains(0) && layout.Rooms[1].Furniture.Contains(0),
                    "Furniture room/list ownership was not migrated.");
                Require(spot.Room == 1 && spot.Pos.f == 1 && Mathf.Abs(spot.Pos.x - 6.5f) < .001f
                    && Mathf.Abs(spot.Approach.z - 5f) < .001f && !layout.Rooms[0].Spots.Contains(0) && layout.Rooms[1].Spots.Contains(0),
                    "Seat/work spot transform did not follow moved furniture.");
                var afterLower = layout.Nav(0); var afterUpper = layout.Nav(1);
                Require(!ReferenceEquals(beforeLower, afterLower) && !ReferenceEquals(beforeUpper, afterUpper), "Both nav caches were not invalidated.");
                Require(!afterLower.Block[afterLower.CellOf(2, 2)] && afterUpper.Block[afterUpper.CellOf(6, 6)],
                    "Old blocker remained or destination blocker was missing.");
                sim.PhysicsFurnitureMoved(furniture, new P3(99, 0, 0), float.NaN, null);
                Require(furniture.Room == 1 && furniture.Pos.f == 1, "Invalid movement corrupted furniture state.");
            });
        }

        static PhysicalProp CreateProp(string name, Vector3 position, float mass)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.position = position; go.transform.localScale = Vector3.one * .4f;
            var body = go.AddComponent<Rigidbody>(); body.mass = mass; body.useGravity = false;
            var material = go.AddComponent<PropMaterial>(); material.AutoVisuals = false;
            var prop = PhysicalProp.Ensure(go);
            // Carry compensates global gravity; use actual gravity for that check in a suspended horizontal plane.
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            return prop;
        }

        static int EnabledColliders(GameObject go)
        {
            int count = 0; foreach (var collider in go.GetComponentsInChildren<Collider>()) if (collider.enabled) count++;
            return count;
        }

        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static void Check(Action<string, bool, string> report, string name, Action action)
        {
            try { action(); report(name, true, ""); }
            catch (Exception e) { report(name, false, e.ToString()); }
        }

        static void WithScene(Action<string, bool, string> report, string name, Action<PhysicsScene> action)
        {
            Check(report, name, () => PhysicalityTestScene.Run((stage, physics) => action(physics)));
        }
    }
}
