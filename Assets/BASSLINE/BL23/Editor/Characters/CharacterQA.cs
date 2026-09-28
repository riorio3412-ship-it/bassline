using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Playables;

namespace BL23.EditorTools.Characters
{
    /// <summary>Renders QA contact sheets of the baked actors to &lt;project&gt;/Shots/*.png (batch mode without -nographics).</summary>
    public static partial class CharacterQA
    {
        static string ShotDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../Shots"));
        static Camera _cam;
        static Transform _stage;
        static readonly List<Material> _tmpMats = new List<Material>();

        [MenuItem("BL23/Characters/QA Shots (all)")]
        public static void ShotsAll()
        {
            Directory.CreateDirectory(ShotDir);
            Setup();
            Lineup("lineup_front", 0f);
            Lineup("lineup_34", 35f);
            Lineup("lineup_back", 180f);
            FaceSheet("faces_all", Cast.All.Where(c => !c.IsButler).Select(c => c.Id).ToArray(), Expr.Neutral);
            foreach (var id in new[] { "P03", "P05", "P07", "P12", "P14", "P17", "P01", "P04" }) ExpressionSheet(id);
            AnimSheet("P03");
            AnimSheet("P10");
            DeadSheet("P05");
            ActionSheet("P06");
            WoundSheet("P09");
            BreakSheet();
            Portraits();
            foreach (var id in new[] { "P01", "P02", "P04" }) FaceCalib(id);
            IdleSheet(new[] { "P01", "P02", "P03", "P04", "P05", "P06", "P07", "P08", "P09", "P10" });
            IdleSheet(new[] { "P11", "P12", "P13", "P14", "P15", "P16", "P17", "P18", "NPC00" });
            Debug.Log("[CharacterQA] done -> " + ShotDir);
        }

        /// <summary>Batch: retarget the clip sets (-animSets) then render the QA sheets (-qaIds / -qaSheets) in one editor session.</summary>
        public static void RetargetThenShots() { ClipRetarget.RetargetAll(); ShotsQuick(); }

        /// <summary>-executeMethod ...CharacterQA.ShotsQuick -qaIds P03,P05 [-qaSheets lineup,faces,anim,dead,action,wound,break,expr]</summary>
        public static void ShotsQuick()
        {
            Directory.CreateDirectory(ShotDir);
            Setup();
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-qaIds");
            var ids = i >= 0 && i + 1 < args.Length ? args[i + 1].Split(',') : new[] { "P03" };
            int j = Array.IndexOf(args, "-qaSheets");
            var sheets = j >= 0 && j + 1 < args.Length ? args[j + 1].Split(',') : new[] { "lineup", "faces" };
            if (sheets.Contains("lineup")) { LineupIds("quick_front", ids, 0f); LineupIds("quick_34", ids, 35f); LineupIds("quick_back", ids, 180f); }
            if (sheets.Contains("faces")) FaceSheet("quick_faces", ids, Expr.Neutral);
            if (sheets.Contains("expr")) foreach (var id in ids) ExpressionSheet(id);
            if (sheets.Contains("anim")) foreach (var id in ids) AnimSheet(id);
            if (sheets.Contains("dead")) foreach (var id in ids) DeadSheet(id);
            if (sheets.Contains("action")) foreach (var id in ids) ActionSheet(id);
            if (sheets.Contains("wound")) foreach (var id in ids) WoundSheet(id);
            if (sheets.Contains("gesture")) foreach (var id in ids) GestureSheet(id);
            if (sheets.Contains("break")) BreakSheet();
            if (sheets.Contains("portrait")) Portraits();
            if (sheets.Contains("distance")) foreach (var id in ids) DistanceSheet(id);
            if (sheets.Contains("styles")) StylesSheet(ids[0]);
            if (sheets.Contains("weights")) foreach (var id in ids) WeightsSheet(id);
            if (sheets.Contains("calib")) foreach (var id in ids) FaceCalib(id);
            if (sheets.Contains("hips")) { var t = new List<Texture2D>(); foreach (var id in ids) { Clear(); var rig = Spawn(id, Vector3.zero, 0f); rig.Anim.AutoFidget = false; Settle(rig, 0.4f); LookAt(new Vector3(0, rig.Height * 0.5f, 0), 20f, 5f, 1.6f, 30f); t.Add(ShootTex(600, 600)); } SaveGrid("hips", t, 4); }
            if (sheets.Contains("idle")) IdleSheet(ids);
            if (sheets.Contains("arms")) foreach (var id in ids) ArmsSheet(id);
            if (sheets.Contains("title")) TitleShot();
            if (sheets.Contains("talk")) foreach (var id in ids) TalkSheet(id);
            if (sheets.Contains("closeup")) foreach (var id in ids) CloseupSheet(id);
            if (sheets.Contains("hands")) foreach (var id in ids) HandsSheet(id);
            if (sheets.Contains("clips")) foreach (var id in ids) ClipsSheet(id);
            if (sheets.Contains("motion")) foreach (var id in ids) MotionSheet(id);
            if (sheets.Contains("fp")) foreach (var id in ids) if (Cast.Get(id) != null && Cast.Get(id).IsPlayer) FirstPersonSheet(id);
            if (sheets.Contains("scan")) foreach (var id in ids) ScanSheet(id);
            if (sheets.Contains("glbface")) GlbFace.DebugMasks();
            if (sheets.Contains("eyezoom")) foreach (var id in ids) EyeZoomSheet(id);
            if (sheets.Contains("dialogue")) foreach (var id in ids) DialogueSheet(id);
            if (sheets.Contains("darkface")) foreach (var id in ids) if (id == "P02" || ids.Length == 1) DarkFaceSheet(id);
            if (sheets.Contains("srcclips")) SourceClipsSheet(new[] { "Idle_Loop", "Idle_Torch_Loop", "Consume", "Yes", "Interact", "Idle_Lantern_Loop" });
            if (sheets.Contains("genclips")) SourceClipsSheet(new[] { "Consume", "Yes", "Idle_Lantern_Loop", "Idle_FoldArms_Loop" }, true);
            RunExtraSheets(ids, sheets);   // charpolish step 0: sheets registered by the CharacterQA.<Owner>.cs partial files
            Debug.Log("[CharacterQA] quick done -> " + ShotDir);
            FinishChecks(args);
        }

        // ================================================================ stage
        static void Setup()
        {
            ShaderUtil.allowAsyncCompilation = false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.4f, 0.52f);
            RenderSettings.ambientEquatorColor = new Color(0.3f, 0.26f, 0.32f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.13f, 0.15f);
            RenderSettings.fog = false;
            var sun = new GameObject("Key").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.85f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(38f, 150f, 0f);
            var rim = new GameObject("RimMagenta").AddComponent<Light>();
            rim.type = LightType.Point; rim.color = new Color(1f, 0.3f, 0.75f); rim.intensity = 3.5f; rim.range = 8f;
            rim.transform.position = new Vector3(-4f, 2.6f, -3f);
            var rim2 = new GameObject("RimCyan").AddComponent<Light>();
            rim2.type = LightType.Point; rim2.color = new Color(0.3f, 0.85f, 1f); rim2.intensity = 3f; rim2.range = 8f;
            rim2.transform.position = new Vector3(4f, 2.2f, -3f);
            var warm = new GameObject("Candle").AddComponent<Light>();
            warm.type = LightType.Point; warm.color = new Color(1f, 0.62f, 0.3f); warm.intensity = 2.2f; warm.range = 6f;
            warm.transform.position = new Vector3(1.5f, 1.4f, 2.5f);
            _stage = new GameObject("Stage").transform;
            // floor: checker marble
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(6f, 1f, 6f);
            var fm = new Material(Shader.Find("BL23/ToonCharacter"));
            fm.SetTexture("_BaseMap", Checker());
            fm.SetTextureScale("_BaseMap", new Vector2(30, 30));
            fm.SetColor("_BaseColor", new Color(0.55f, 0.5f, 0.55f));
            fm.SetFloat("_OutlineWidth", 0f);
            fm.SetFloat("_RimStrength", 0f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = fm;
            // backdrop
            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            back.transform.position = new Vector3(0, 4f, -6f);
            back.transform.localScale = new Vector3(40f, 12f, 1f);
            back.transform.rotation = Quaternion.Euler(0, 180f, 0);
            var bm = new Material(Shader.Find("BL23/ToonCharacter"));
            bm.SetColor("_BaseColor", new Color(0.16f, 0.1f, 0.2f));
            bm.SetFloat("_OutlineWidth", 0f); bm.SetFloat("_RimStrength", 0f);
            back.GetComponent<MeshRenderer>().sharedMaterial = bm;
            back.transform.rotation = Quaternion.identity;
            var camGo = new GameObject("QACam");
            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.1f, 0.07f, 0.12f);
            _cam.nearClipPlane = 0.05f; _cam.farClipPlane = 100f;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var vol = new GameObject("Volume").AddComponent<Volume>();
            vol.isGlobal = true;
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = prof.Add<Bloom>(true); bloom.intensity.Override(0.6f); bloom.threshold.Override(1.0f);
            var tone = prof.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.Neutral);
            var vig = prof.Add<Vignette>(true); vig.intensity.Override(0.22f);
            vol.sharedProfile = prof;
            // warm-up render (the first frame after scene creation can come out black)
            var warmRt = new RenderTexture(64, 64, 24);
            _cam.targetTexture = warmRt; _cam.Render(); _cam.Render(); _cam.targetTexture = null; warmRt.Release();
        }

        static Texture2D Checker()
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            t.SetPixels(new[] { new Color(0.85f, 0.83f, 0.86f), new Color(0.2f, 0.18f, 0.22f), new Color(0.2f, 0.18f, 0.22f), new Color(0.85f, 0.83f, 0.86f) });
            t.Apply();
            return t;
        }

        static void Clear()
        {
            for (int i = _stage.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(_stage.GetChild(i).gameObject);
        }

        static ActorRig Spawn(string id, Vector3 pos, float yaw)
        {
            var def = Cast.Get(id);
            var holder = new GameObject("H_" + id).transform;
            holder.SetParent(_stage, false);
            holder.position = pos;
            holder.rotation = Quaternion.Euler(0, yaw, 0);
            var rig = ActorFactory.Create(def, holder);
            return rig;
        }

        static void Settle(ActorRig rig, float seconds = 0.6f, float dt = 1f / 30f)
        {
            // charpolish step 0: every post-animator component (spring chains, ActorPhysics...) steps in StepOrder
            var steps = rig.GetComponentsInChildren<IActorStep>().OrderBy(c => c.StepOrder).ToArray();
            for (float t = 0; t < seconds; t += dt)
            {
                rig.Anim.Tick(dt);
                foreach (var c in steps) c.Step(dt);
            }
            if (rig.Face != null) rig.Face.SnapShapes();
        }

        static void Shoot(string name, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            _cam.targetTexture = rt;
            _cam.aspect = (float)w / h;
            _cam.Render(); _cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            _cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(ShotDir, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }

        static void LookAt(Vector3 target, float yawDeg, float pitchDeg, float dist, float fov)
        {
            _cam.fieldOfView = fov;
            var rot = Quaternion.Euler(pitchDeg, yawDeg + 180f, 0);
            _cam.transform.rotation = rot;
            _cam.transform.position = target - rot * Vector3.forward * dist;
        }

        // ================================================================ sheets
        static void Lineup(string name, float yaw)
        {
            var ids = Cast.All.Select(c => c.Id).ToArray();
            LineupIds(name + "_a", ids.Take(10).ToArray(), yaw);
            LineupIds(name + "_b", ids.Skip(10).ToArray(), yaw);
        }

        static void LineupIds(string name, string[] ids, float yaw)
        {
            Clear();
            float spacing = 0.78f;
            float x0 = -(ids.Length - 1) * spacing * 0.5f;
            for (int i = 0; i < ids.Length; i++)
            {
                var rig = Spawn(ids[i], new Vector3(x0 + i * spacing, 0, 0), yaw);
                Settle(rig);
            }
            float width = ids.Length * spacing + 0.4f;
            float fov = 18f;
            float dist = Mathf.Max(width * 0.5f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) / 2.0f, 2.2f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * 0.5f);
            LookAt(new Vector3(0, 0.95f, 0), 0f, 3f, dist, fov);
            int w = Mathf.Clamp(ids.Length * 260, 900, 2800);
            Shoot(name, w, 1100);
        }

        static void FaceSheet(string name, string[] ids, Expr e)
        {
            Clear();
            int cols = Mathf.Min(6, ids.Length);
            int rows = Mathf.CeilToInt(ids.Length / (float)cols);
            // render each face separately and tile
            var tiles = new List<Texture2D>();
            foreach (var id in ids)
            {
                Clear();
                var rig = Spawn(id, Vector3.zero, 0f);
                rig.SetExpression(e, 1f);
                Settle(rig);
                var eye = rig.EyeAnchor.position;
                LookAt(eye + new Vector3(0, -0.03f, 0), 0f, 2f, 0.95f, 20f);
                tiles.Add(ShootTex(420, 480));
            }
            SaveGrid(name, tiles, cols);
        }

        static void ExpressionSheet(string id)
        {
            var exprs = (Expr[])Enum.GetValues(typeof(Expr));
            var tiles = new List<Texture2D>();
            foreach (var e in exprs)
            {
                Clear();
                var rig = Spawn(id, Vector3.zero, 0f);
                rig.SetExpression(e, 1f);
                if (e == Expr.Break && rig.CanBreak) rig.Anim.SetBreak(0.7f);
                Settle(rig);
                var eye = rig.EyeAnchor.position;
                LookAt(eye + new Vector3(0, -0.03f, 0), 12f, 2f, 0.8f, 20f);
                tiles.Add(ShootTex(360, 420));
            }
            SaveGrid("expr_" + id, tiles, 5);
        }

        static Texture2D ShootTex(int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            _cam.targetTexture = rt;
            _cam.aspect = (float)w / h;
            _cam.Render(); _cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            _cam.targetTexture = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            return tex;
        }

        static void SaveGrid(string name, List<Texture2D> tiles, int cols)
        {
            if (tiles.Count == 0) return;
            int tw = tiles[0].width, th = tiles[0].height;
            int rows = Mathf.CeilToInt(tiles.Count / (float)cols);
            var outT = new Texture2D(tw * cols, th * rows, TextureFormat.RGB24, false);
            var fill = new Color[outT.width * outT.height];
            for (int i = 0; i < fill.Length; i++) fill[i] = new Color(0.05f, 0.04f, 0.06f);
            outT.SetPixels(fill);
            for (int i = 0; i < tiles.Count; i++)
            {
                int cx = i % cols, cy = rows - 1 - i / cols;
                outT.SetPixels(cx * tw, cy * th, tw, th, tiles[i].GetPixels());
                UnityEngine.Object.DestroyImmediate(tiles[i]);
            }
            outT.Apply();
            File.WriteAllBytes(Path.Combine(ShotDir, name + ".png"), outT.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(outT);
        }

        delegate void PoseSetup(ActorRig rig, int frame);

        static void Sheet(string name, string id, int frames, int cols, PoseSetup setup, float camYaw = 30f, float camDist = 4.2f, float camY = 0.9f, float fov = 30f)
        {
            var tiles = new List<Texture2D>();
            for (int f = 0; f < frames; f++)
            {
                Clear();
                var rig = Spawn(id, Vector3.zero, 0f);
                setup(rig, f);
                LookAt(new Vector3(0, camY, 0.1f), camYaw, 8f, camDist, fov);
                tiles.Add(ShootTex(420, 520));
            }
            SaveGrid(name, tiles, cols);
        }

        static void AnimSheet(string id)
        {
            // walk cycle (8 frames), run (4), idle, sit, crouch, kneel, lie back/front/side, slumped
            Sheet("anim_walk_" + id, id, 8, 8, (rig, f) =>
            {
                rig.Anim.SetMove(new Vector3(0, 0, 1.35f), false);
                Settle(rig, 1.0f + f * (1f / 8f) * (2f * ActorPoses.StepLength(new ActorPoses.Dims(rig.Anim, RestArray(rig), rig), 1.35f, 0f) / 1.35f), 1f / 60f);
            }, 90f, 3.6f);
            Sheet("anim_run_" + id, id, 6, 6, (rig, f) =>
            {
                rig.Anim.SetMove(new Vector3(0, 0, 4.2f), true);
                Settle(rig, 1.5f + f * 0.06f, 1f / 60f);
            }, 90f, 3.6f);
            var postures = new[] { Posture.Stand, Posture.Sit, Posture.Crouch, Posture.Kneel, Posture.LieBack, Posture.LieFront, Posture.LieSide, Posture.Slumped };
            Sheet("anim_postures_" + id, id, postures.Length, 4, (rig, f) =>
            {
                rig.Anim.SetPosture(postures[f]);
                Settle(rig, 1.2f);
            }, 35f, 4.4f, 0.7f);
        }

        static Vector3[] RestArray(ActorRig rig)
        {
            var a = new Vector3[ActorSkeleton.Count];
            for (int i = 0; i < a.Length; i++) a[i] = rig.Anim.RestPos((HBone)i);
            return a;
        }

        static void DeadSheet(string id)
        {
            Sheet("anim_dead_" + id, id, 6, 3, (rig, f) =>
            {
                rig.Anim.SetDeadPose(f);
                rig.SetExpression(Expr.Dead);
                Settle(rig, 1.2f);
            }, 30f, 4.0f, 0.45f, 32f);
        }

        static void ActionSheet(string id)
        {
            var acts = new[] { ActionAnim.Stab, ActionAnim.Slash, ActionAnim.Overhead, ActionAnim.Shove, ActionAnim.Strangle, ActionAnim.PickUp, ActionAnim.Knock, ActionAnim.OpenDoor, ActionAnim.Read, ActionAnim.Cook, ActionAnim.FirstAid, ActionAnim.Crawl, ActionAnim.Struggle, ActionAnim.Play, ActionAnim.Sleep, ActionAnim.Drag };
            float[] times = { 0.55f, 0.5f, 0.66f, 0.3f, 1.0f, 0.7f, 0.55f, 0.6f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
            Sheet("anim_actions_" + id, id, acts.Length, 4, (rig, f) =>
            {
                rig.Anim.PlayAction(acts[f], 4f);
                if (acts[f] == ActionAnim.Stab || acts[f] == ActionAnim.Slash) rig.SetExpression(Expr.Angry);
                Settle(rig, times[f], 1f / 60f);
            }, 50f, 4.0f, 0.8f);
            // carry
            Clear();
            var carrier = Spawn(id, Vector3.zero, 0f);
            var victim = Spawn("P12", new Vector3(1.5f, 0, 0), 0f);
            carrier.Anim.SetCarrying(victim);
            Settle(carrier, 0.8f); Settle(victim, 0.8f);
            LookAt(new Vector3(0, 1.1f, 0), 60f, 6f, 4.5f, 30f);
            Shoot("anim_carry_" + id, 900, 1000);
            GestureSheet(id);
        }

        static void GestureSheet(string id)
        {
            var gs = ((Gesture[])Enum.GetValues(typeof(Gesture))).Where(g => g != Gesture.None).ToArray();
            Sheet("anim_gestures_" + id, id, gs.Length, 6, (rig, f) =>
            {
                rig.Anim.PlayGesture(gs[f], 5f);
                Settle(rig, 0.9f, 1f / 60f);
            }, 20f, 3.6f, 1.0f);
        }

        static void WoundSheet(string id)
        {
            var tiles = new List<Texture2D>();
            var cases = new (BL23.Game.Characters.BodyRegion r, DamageType t, int s)[]
            {
                (BL23.Game.Characters.BodyRegion.Neck, DamageType.Cut, 3), (BL23.Game.Characters.BodyRegion.Abdomen, DamageType.Stab, 3), (BL23.Game.Characters.BodyRegion.ShoulderR, DamageType.Stab, 2),
                (BL23.Game.Characters.BodyRegion.Head, DamageType.Blunt, 3), (BL23.Game.Characters.BodyRegion.Chest, DamageType.Crush, 3), (BL23.Game.Characters.BodyRegion.ArmL, DamageType.Cut, 2)
            };
            foreach (var c in cases)
            {
                Clear();
                var rig = Spawn(id, Vector3.zero, 0f);
                rig.AddWound(c.r, c.t, c.s, Vector3.zero, false);
                rig.Wounds.Settle();
                rig.SetExpression(Expr.Pain);
                Settle(rig, 0.8f);
                var target = rig.Anim.RestPos(rig.RegionBone(c.r));
                if (c.r == BL23.Game.Characters.BodyRegion.Head) target = rig.EyeAnchor.position;
                LookAt(target + new Vector3(0, 0.0f, 0), 18f, 4f, 1.4f, 30f);
                tiles.Add(ShootTex(520, 620));
            }
            // full body after multiple wounds + blood spatter
            Clear();
            var rr = Spawn(id, Vector3.zero, 0f);
            rr.AddWound(BL23.Game.Characters.BodyRegion.Neck, DamageType.Cut, 3, Vector3.zero, false);
            rr.AddWound(BL23.Game.Characters.BodyRegion.Abdomen, DamageType.Stab, 2, Vector3.zero, false);
            rr.AddWound(BL23.Game.Characters.BodyRegion.LegL, DamageType.Cut, 2, Vector3.zero, false);
            rr.SetBloodied(0.6f);
            rr.Wounds.Settle();
            rr.Anim.SetDeadPose(0);
            rr.SetExpression(Expr.Dead);
            Settle(rr, 1.2f);
            LookAt(new Vector3(0, 0.3f, 0.2f), 25f, 35f, 3.2f, 32f);
            tiles.Add(ShootTex(520, 620));
            SaveGrid("wounds_" + id, tiles, 4);
        }

        static readonly string[] AllStyles = { "", "pockets", "clasp", "behind", "crossed", "handOnHip", "cute", "slouch", "stiff", "swagger", "lily", "notebook", "clipboard", "briefcase", "politician", "calm", "curious", "thermos", "actor" };

        static void StylesSheet(string id)
        {
            Sheet("styles_" + id, id, AllStyles.Length, 10, (rig, f) =>
            {
                rig.IdleStyle = AllStyles[f];
                rig.Anim.AutoFidget = false;
                Settle(rig, 0.6f);
            }, 25f, 3.6f, 0.95f);
        }

        /// <summary>Each actor in its own idle, 3/4 view (checks hands vs. clothes).</summary>
        static void IdleSheet(string[] ids)
        {
            var tiles = new List<Texture2D>();
            foreach (var id in ids)
            {
                Clear();
                var rig = Spawn(id, Vector3.zero, 0f);
                rig.Anim.AutoFidget = false;
                Settle(rig, 0.6f);
                LookAt(new Vector3(0, rig.Height * 0.52f, 0.05f), 30f, 6f, 3.5f, 32f);
                tiles.Add(ShootTex(420, 620));
                LookAt(new Vector3(0, rig.Height * 0.52f, 0.05f), 200f, 6f, 3.5f, 32f);
                tiles.Add(ShootTex(420, 620));
            }
            SaveGrid("idle_" + string.Join("_", ids), tiles, 8);
        }

        static Light AddFill(float intensity)
        {
            var l = new GameObject("QAFill").AddComponent<Light>();
            l.type = LightType.Directional; l.color = new Color(0.95f, 0.95f, 1f); l.intensity = intensity; l.shadows = LightShadows.None;
            l.transform.SetParent(_stage, false);
            return l;
        }

        /// <summary>Upper-body arm check: bind pose, idles, walk, sit and the arm-heavy gestures, front + 3/4 with a fill light.</summary>
        static void ArmsSheet(string id)
        {
            var tiles = new List<Texture2D>();
            string[] names = { "bind", "idle", "plain", "walkA", "walkB", "sit", "CrossArms", "Think", "Talk", "Point", "HandOnChest", "Shrug", "Read", "run" };
            for (int f = 0; f < names.Length; f++)
            {
                foreach (float yaw in new[] { 0f, 40f })
                {
                    Clear();
                    var rig = Spawn(id, Vector3.zero, 0f);
                    rig.Anim.AutoFidget = false;
                    float ty = rig.Height * 0.66f;
                    switch (names[f])
                    {
                        case "bind": break;
                        case "idle": Settle(rig, 0.6f); break;
                        case "plain": rig.IdleStyle = ""; Settle(rig, 0.6f); break;
                        case "walkA": rig.Anim.SetMove(new Vector3(0, 0, 1.35f), false); Settle(rig, 1.0f, 1f / 60f); break;
                        case "walkB": rig.Anim.SetMove(new Vector3(0, 0, 1.35f), false); Settle(rig, 1.37f, 1f / 60f); break;
                        case "run": rig.Anim.SetMove(new Vector3(0, 0, 4.2f), true); Settle(rig, 1.6f, 1f / 60f); break;
                        case "sit": rig.Anim.SetPosture(Posture.Sit); Settle(rig, 1.2f); ty = rig.Height * 0.48f; break;
                        case "Read": rig.Anim.PlayAction(ActionAnim.Read, 5f); Settle(rig, 1.0f, 1f / 60f); break;
                        default:
                            rig.Anim.PlayGesture((Gesture)Enum.Parse(typeof(Gesture), names[f]), 5f); Settle(rig, 0.9f, 1f / 60f); break;
                    }
                    var fill = AddFill(0.55f);
                    LookAt(new Vector3(0, ty, 0.05f), yaw, 4f, 2.1f, 34f);
                    fill.transform.rotation = _cam.transform.rotation * Quaternion.Euler(-10f, 10f, 0);
                    tiles.Add(ShootTex(380, 460));
                }
            }
            SaveGrid("arms_" + id, tiles, 8);
        }

        /// <summary>The title-screen trio (P01 look-around, P02 crossed arms, P04 think) at the menu framing and close.</summary>
        static void TitleShot()
        {
            Clear();
            string[] ids = { "P01", "P02", "P04" }; float[] offs = { -1.1f, 0.2f, 1.3f }; float[] back = { 0f, 0.7f, 0.35f };
            var rigs = new List<ActorRig>();
            for (int k = 0; k < 3; k++)
            {
                var rig = Spawn(ids[k], new Vector3(offs[k], 0, -back[k]), (k - 1) * -14f);
                rig.Anim.AutoFidget = false;
                rig.SetExpression(k == 0 ? Expr.Neutral : k == 1 ? Expr.Smirk : Expr.Blank);
                rig.Anim.PlayGesture(k == 0 ? Gesture.LookAround : k == 1 ? Gesture.CrossArms : Gesture.Think, 999);
                rigs.Add(rig);
            }
            foreach (var r in rigs) Settle(r, 1.3f, 1f / 30f);
            var fill = AddFill(0.35f);
            _cam.fieldOfView = 42f;
            _cam.transform.position = new Vector3(0.1f, 1.55f, 3.4f);
            _cam.transform.LookAt(new Vector3(0.1f, 1.2f, 0f));
            fill.transform.rotation = _cam.transform.rotation;
            Shoot("title_wide", 1600, 900);
            var tiles = new List<Texture2D>();
            for (int k = 0; k < 3; k++)
            {
                var t = rigs[k].transform;
                var target = t.position + Vector3.up * rigs[k].Height * 0.7f;
                _cam.fieldOfView = 30f;
                _cam.transform.position = target + t.forward * 1.8f + Vector3.up * 0.1f + t.right * 0.3f;
                _cam.transform.LookAt(target);
                fill.transform.rotation = _cam.transform.rotation;
                tiles.Add(ShootTex(520, 640));
            }
            SaveGrid("title_close", tiles, 3);
        }

        /// <summary>Dialogue-distance face checks: neutral, blink half/closed, talk open frames, smile, head turned 25 deg.</summary>
        static void TalkSheet(string id)
        {
            var tiles = new List<Texture2D>();
            var states = new (Expr e, int eyeOverride, int mouthOverride, float yaw)[]
            {
                (Expr.Neutral, -1, -1, 0f), (Expr.Neutral, FaceCells.EyeHalf, -1, 0f), (Expr.Neutral, FaceCells.EyeClosed, -1, 0f),
                (Expr.Neutral, -1, FaceCells.MouthTalkA, 0f), (Expr.Neutral, -1, FaceCells.MouthTalkO, 0f),
                (Expr.Neutral, -1, -1, 28f), (Expr.Neutral, FaceCells.EyeClosed, FaceCells.MouthTalkA, 28f), (Expr.Neutral, -1, -1, -28f),
                (Expr.Smile, -1, -1, 12f), (Expr.Angry, -1, -1, 12f),
            };
            foreach (var s in states)
            {
                Clear();
                var rig = Spawn(id, Vector3.zero, 0f);
                rig.Anim.AutoFidget = false;
                rig.SetExpression(s.e, 1f);
                Settle(rig, 0.4f);
                if (s.eyeOverride >= 0 || s.mouthOverride >= 0)
                    foreach (var m in rig.Materials)
                        if (m.IsKeywordEnabled("_FACE"))
                        {
                            var s0 = m.GetVector("_FaceState0"); var s1 = m.GetVector("_FaceState1");
                            if (s.eyeOverride >= 0) { s0.x = s.eyeOverride; s0.y = s.eyeOverride; }
                            if (s.mouthOverride >= 0) s1.x = s.mouthOverride;
                            m.SetVector("_FaceState0", s0); m.SetVector("_FaceState1", s1);
                        }
                if (s.mouthOverride == FaceCells.MouthTalkA || s.mouthOverride == FaceCells.MouthTalkO)
                    foreach (var smr in rig.Skins) if (smr != null && smr.sharedMesh != null) { int ji = smr.sharedMesh.GetBlendShapeIndex("JawOpen"); if (ji >= 0) smr.SetBlendShapeWeight(ji, s.mouthOverride == FaceCells.MouthTalkO ? 55f : 32f); }
                var eye = rig.EyeAnchor.position;
                LookAt(eye + new Vector3(0, -0.06f, 0), s.yaw, 3f, 1.0f, 22f);
                tiles.Add(ShootTex(420, 480));
            }
            SaveGrid("talk_" + id, tiles, 5);
        }

        /// <summary>Head + upper body at dialogue distance from several angles in idle (no overlay changes).</summary>
        /// <summary>Raw scan vs texture-fixed scan (lit, textured, original pose): body turnaround + head turnaround.</summary>
        static void ScanSheet(string id)
        {
            var def = Cast.Get(id);
            var log = new System.Text.StringBuilder();
            var scan = GlbRigger.LoadScan(def, log);
            if (scan == null) return;
            var tint = new[] { "RimMagenta", "RimCyan", "Candle" }.Select(GameObject.Find).Where(g => g != null).ToList();
            foreach (var g in tint) g.SetActive(false);
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(scan.V); mesh.SetNormals(scan.N); mesh.SetUVs(0, scan.UV); mesh.SetTriangles(scan.T, 0); mesh.RecalculateBounds();
            var lm = scan.LM;
            foreach (bool fix in new[] { false, true })
            {
                var tex = GlbFix.Texture(def, scan, log, fix);
                Clear();
                var go = new GameObject("Scan"); go.transform.SetParent(_stage, false);
                var shown = mesh;
                if (fix)
                {
                    int s0 = scan.Studs.Count;
                    GlbFix.AddAccessories(def, scan, tex, log);
                    if (scan.Studs.Count > s0 || GlbFix.HasGeometryEdits(id))
                    {
                        var v = new List<Vector3>(scan.V); var n = new List<Vector3>(scan.N); var uv = new List<Vector2>(scan.UV); var t = new List<int>(scan.T);
                        GlbFix.LiftFringe(def, scan, v, uv, tex, log);
                        var auv = GlbFix.AccUV.TryGetValue(id, out var a) ? a : Vector2.zero;
                        for (int si = s0; si < scan.Studs.Count; si++)
                        {
                            var sd = scan.Studs[si]; int b0 = v.Count;
                            v.AddRange(sd.v); n.AddRange(sd.n); uv.AddRange(sd.v.Select(_ => auv)); t.AddRange(sd.t.Select(x => x + b0));
                        }
                        shown = new Mesh { indexFormat = IndexFormat.UInt32 };
                        shown.SetVertices(v); shown.SetNormals(n); shown.SetUVs(0, uv); shown.SetTriangles(t, 0); shown.RecalculateBounds();
                    }
                }
                go.AddComponent<MeshFilter>().sharedMesh = shown;
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetTexture("_BaseMap", tex); mat.SetFloat("_Smoothness", 0.05f); mat.SetFloat("_Cull", 0f);
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                var fill = AddFill(0.9f);
                var tiles = new List<Texture2D>();
                foreach (float yaw in new[] { 0f, 40f, 90f, 180f, 270f })
                {
                    LookAt(new Vector3(0, lm.H * 0.5f, 0), yaw, 3f, 5.2f, 22f);
                    fill.transform.rotation = _cam.transform.rotation;
                    tiles.Add(ShootTex(540, 640));
                }
                SaveGrid("scan_" + id + (fix ? "_fix" : "_raw"), tiles, 5);
                tiles = new List<Texture2D>();
                var head = new Vector3(lm.EyeCenter.x, lm.EyeCenter.y + 0.03f, lm.HeadCenter.z);
                foreach (float yaw in new[] { 0f, 55f, 110f, 180f, 250f, 305f })
                {
                    LookAt(head, yaw, 4f, 0.95f, 22f);
                    fill.transform.rotation = _cam.transform.rotation;
                    tiles.Add(ShootTex(420, 420));
                }
                SaveGrid("scanhead_" + id + (fix ? "_fix" : "_raw"), tiles, 6);
                UnityEngine.Object.DestroyImmediate(tex);
            }
            File.WriteAllText(Path.Combine(ShotDir, "scan_" + id + ".txt"), log.ToString());
            foreach (var g in tint) g.SetActive(true);
        }

        /// <summary>Dialogue close-ups under the game's conversation lighting (warm key, dark room, no coloured rims):
        /// neutral / talk / smile / angry / pain / surprised, front and 3/4.</summary>
        static void DialogueSheet(string id)
        {
            var off = new[] { "RimMagenta", "RimCyan", "Candle", "Key" }.Select(GameObject.Find).Where(g => g != null).ToList();
            foreach (var g in off) g.SetActive(false);
            var key = new GameObject("DlgKey").AddComponent<Light>();
            key.type = LightType.Directional; key.color = new Color(1f, 0.83f, 0.64f); key.intensity = 2.1f; key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(24f, 205f, 0f);
            var fillL = new GameObject("DlgFill").AddComponent<Light>();
            fillL.type = LightType.Point; fillL.color = new Color(0.55f, 0.6f, 0.85f); fillL.intensity = 0.8f; fillL.range = 5f;
            var prevBg = _cam.backgroundColor; _cam.backgroundColor = new Color(0.03f, 0.025f, 0.035f);
            var tiles = new List<Texture2D>();
            var states = new (Expr e, int mouth, float yaw)[]
            {
                (Expr.Neutral, -1, 0f), (Expr.Neutral, FaceCells.MouthTalkA, 0f), (Expr.Smile, -1, 0f), (Expr.Angry, -1, 0f), (Expr.Pain, -1, 0f), (Expr.Surprised, -1, 0f),
                (Expr.Neutral, -1, 32f), (Expr.Angry, -1, 32f), (Expr.Smirk, -1, -32f),
            };
            foreach (var s in states)
            {
                Clear();
                var rig = Spawn(id, Vector3.zero, 0f);
                rig.Anim.AutoFidget = false;
                rig.SetExpression(s.e, 1f);
                if (Environment.GetCommandLineArgs().Contains("-noOutline")) foreach (var m in rig.Materials) m.SetFloat("_OutlineWidth", 0f);
                if (Environment.GetCommandLineArgs().Contains("-cullBack")) foreach (var m in rig.Materials) m.SetFloat("_Cull", 2f);
                Settle(rig, 0.5f);
                if (s.mouth >= 0)
                    foreach (var m in rig.Materials)
                        if (m.IsKeywordEnabled("_FACE")) { var s1 = m.GetVector("_FaceState1"); s1.x = s.mouth; m.SetVector("_FaceState1", s1); }
                var head = rig.Bone(HBone.Head).position + Vector3.up * 0.06f * rig.Height / 1.75f;
                fillL.transform.position = head + new Vector3(-0.9f, 0.2f, 0.9f);
                LookAt(head, s.yaw, 2f, 0.8f, 24f);
                tiles.Add(ShootTex(400, 440));
            }
            SaveGrid("dialogue_" + id + (Environment.GetCommandLineArgs().Contains("-noOutline") ? "_nol" : "") + (Environment.GetCommandLineArgs().Contains("-cullBack") ? "_cull" : ""), tiles, 6);
            UnityEngine.Object.DestroyImmediate(key.gameObject); UnityEngine.Object.DestroyImmediate(fillL.gameObject);
            _cam.backgroundColor = prevBg;
            foreach (var g in off) g.SetActive(true);
        }

        /// <summary>Dark faces (HollowGrin / CorneredStare / VeiledSmirk) under dialogue lighting: front + 3/4, plus the
        /// half-way frame of the shadow creep.</summary>
        static void DarkFaceSheet(string id)
        {
            var off = new[] { "RimMagenta", "RimCyan", "Candle", "Key" }.Select(GameObject.Find).Where(g => g != null).ToList();
            foreach (var g in off) g.SetActive(false);
            var key = new GameObject("DlgKey").AddComponent<Light>();
            key.type = LightType.Directional; key.color = new Color(1f, 0.83f, 0.64f); key.intensity = 2.1f; key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(24f, 205f, 0f);
            var prevBg = _cam.backgroundColor; _cam.backgroundColor = new Color(0.03f, 0.025f, 0.035f);
            var tiles = new List<Texture2D>();
            var states = new (DarkFace k, float amount, float yaw)[]
            {
                (DarkFace.None, 1f, 0f), (DarkFace.HollowGrin, 0.5f, 0f), (DarkFace.HollowGrin, 1f, 0f), (DarkFace.HollowGrin, 1f, 32f),
                (DarkFace.CorneredStare, 1f, 0f), (DarkFace.CorneredStare, 1f, 32f), (DarkFace.VeiledSmirk, 1f, 0f), (DarkFace.VeiledSmirk, 1f, 32f),
            };
            foreach (var s in states)
            {
                Clear();
                var rig = Spawn(id, Vector3.zero, 0f);
                rig.Anim.AutoFidget = false;
                rig.SetDarkFace(s.k, s.amount);
                Settle(rig, s.k == DarkFace.VeiledSmirk ? 1.2f : 0.5f);
                if (rig.Face != null) rig.Face.SnapDarkFace();
                var head = rig.Bone(HBone.Head).position + Vector3.up * 0.06f * rig.Height / 1.75f;
                LookAt(head + (s.k == DarkFace.VeiledSmirk ? Vector3.down * 0.05f : Vector3.zero), s.yaw, 2f, s.k == DarkFace.VeiledSmirk ? 1.05f : 0.8f, 24f);
                var t = ShootTex(400, 440);
                Label(t, s.k + (s.amount < 1f ? " 50%" : ""));
                tiles.Add(t);
            }
            SaveGrid("darkface_" + id, tiles, 4);
            UnityEngine.Object.DestroyImmediate(key.gameObject);
            _cam.backgroundColor = prevBg;
            foreach (var g in off) g.SetActive(true);
        }

        /// <summary>Close-ups of both eyes on the texture-fixed scan (unlit texture / lit / from the side), raw vs fixed.</summary>
        static void EyeZoomSheet(string id)
        {
            var def = Cast.Get(id);
            var log = new System.Text.StringBuilder();
            var scan = GlbRigger.LoadScan(def, log);
            if (scan == null) return;
            var lm = scan.LM; var cal = GlbFace.Get(id);
            var tiles = new List<Texture2D>();
            foreach (bool fix in new[] { false, true })
            {
                var tex = GlbFix.Texture(def, scan, log, fix);
                var v = new List<Vector3>(scan.V);
                var tt = new List<int>(scan.T); var tmc = scan.TM.ToArray();
                if (fix) { GlbFix.LiftFringe(def, scan, v, scan.UV, tex, log); GlbFix.CutFringe(def, scan, v, scan.UV, tt, ref tmc, tex, log); }
                var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(v); mesh.SetNormals(scan.N); mesh.SetUVs(0, scan.UV); mesh.SetTriangles(tt, 0); mesh.RecalculateBounds();
                foreach (bool lit in new[] { false, true })
                {
                    Clear();
                    var go = new GameObject("Scan"); go.transform.SetParent(_stage, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mat = new Material(Shader.Find(lit ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit"));
                    mat.SetTexture("_BaseMap", tex); mat.SetFloat("_Smoothness", 0.05f); mat.SetFloat("_Cull", 0f);
                    go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                    var fill = AddFill(0.9f);
                    for (int s = 0; s < 2; s++)
                    {
                        var eye = new Vector3(lm.EyeCenter.x + (s == 0 ? cal.EyeDX : -cal.EyeDX), lm.EyeCenter.y + cal.EyeDY, lm.HeadCenter.z);
                        LookAt(eye, 0f, 0f, 0.5f, 12f);
                        fill.transform.rotation = _cam.transform.rotation;
                        var t = ShootTex(360, 300);
                        Label(t, (fix ? "FIX " : "RAW ") + (lit ? "LIT " : "TEX ") + (s == 0 ? "R-EYE" : "L-EYE"));
                        tiles.Add(t);
                    }
                }
                UnityEngine.Object.DestroyImmediate(tex);
            }
            SaveGrid("eyezoom_" + id, tiles, 4);
            File.WriteAllText(Path.Combine(ShotDir, "eyezoom_" + id + ".txt"), log.ToString());
        }

        static void CloseupSheet(string id)
        {
            var tiles = new List<Texture2D>();
            foreach (float yaw in new[] { 0f, 25f, -25f, 50f, -50f, 90f })
            {
                Clear();
                var rig = Spawn(id, Vector3.zero, 0f);
                rig.Anim.AutoFidget = false;
                Settle(rig, 0.5f);
                var eye = rig.EyeAnchor.position;
                LookAt(eye + new Vector3(0, -0.2f, 0), yaw, 4f, 1.2f, 34f);
                tiles.Add(ShootTex(440, 520));
            }
            SaveGrid("closeup_" + id, tiles, 6);
        }

        /// <summary>Left hand close-ups (bind pose from 4 sides, then idle / fist / point / grip if the rig has finger bones).</summary>
        static void HandsSheet(string id)
        {
            var tiles = new List<Texture2D>();
            // (state, right hand?, camera yaw relative to the hand's outer side)
            var shots = new (string st, bool right, float yaw)[]
            {
                ("bind", false, 0f), ("bind", false, 90f), ("bind", true, 0f), ("idle", true, 40f),
                ("Angry", true, 20f), ("Angry", true, 100f), ("Point", true, 40f), ("Point", true, -60f),
                ("Read", true, 30f), ("Wave", true, 0f), ("Think", true, 60f), ("CrossArms", true, 80f),
            };
            foreach (var sh in shots)
            {
                Clear();
                var rig = Spawn(id, Vector3.zero, 0f);
                rig.Anim.AutoFidget = false;
                if (sh.st == "idle") Settle(rig, 0.6f);
                else if (sh.st == "Read") { rig.Anim.PlayAction(ActionAnim.Read, 5f); Settle(rig, 1f, 1f / 60f); }
                else if (sh.st != "bind") { rig.Anim.PlayGesture((Gesture)Enum.Parse(typeof(Gesture), sh.st), 5f); Settle(rig, 0.9f, 1f / 60f); }
                var handB = rig.Bone(sh.right ? HBone.HandR : HBone.HandL);
                Vector3 restTip = sh.right ? rig.HandTipRestR : rig.HandTipRestL;
                Vector3 local = restTip - rig.Anim.RestPos(sh.right ? HBone.HandR : HBone.HandL);
                Vector3 c = handB.position + (handB.rotation * Quaternion.Inverse(rig.Anim.RestRot(sh.right ? HBone.HandR : HBone.HandL))) * (rig.transform.rotation * local * 0.5f);
                var fill = AddFill(0.6f);
                float outer = sh.right ? 90f : -90f; // camera on the hand's outer side at yaw 0
                LookAt(c, outer + (sh.right ? -sh.yaw : sh.yaw) - 180f + 180f, 5f, 0.45f, 30f);
                fill.transform.rotation = _cam.transform.rotation;
                tiles.Add(ShootTex(340, 340));
            }
            SaveGrid("hands_" + id, tiles, 6);
        }

        /// <summary>Raw retargeted clips (4 frames each) applied straight to the rig, 3/4 view. One sheet per 12 clips.</summary>
        static void ClipsSheet(string id)
        {
            ActorClipSet.ClearCache();
            Clear();
            var probe = Spawn(id, Vector3.zero, 0f);
            string setName = !string.IsNullOrEmpty(probe.AnimSet) ? probe.AnimSet : (probe.IsGlb ? probe.ActorId : "PROC");
            var set = ActorClipSet.Load(setName);
            if (set == null) { Debug.LogWarning("[QA] no clip set " + setName); return; }
            var names = set.Clips.Keys.OrderBy(n => n).ToList();
            int page = 0;
            for (int start = 0; start < names.Count; start += 12, page++)
            {
                var tiles = new List<Texture2D>();
                foreach (var name in names.Skip(start).Take(12))
                {
                    var c = set.Get(name);
                    for (int k = 0; k < 4; k++)
                    {
                        Clear();
                        var rig = Spawn(id, Vector3.zero, 0f);
                        rig.Anim.UseClips = false;
                        rig.Anim.AutoFidget = false;
                        var p = new ActorPose();
                        ActorClipSet.Sample(c, c.Length * k / 3f, p, rig.Anim.LegLen);
                        rig.Anim.ApplyCanonical(p);
                        var fill = AddFill(0.5f);
                        LookAt(new Vector3(0, rig.Height * 0.5f, 0.05f), 35f, 6f, 3.4f, 34f);
                        fill.transform.rotation = _cam.transform.rotation;
                        var t = ShootTex(260, 380);
                        if (k == 0) Label(t, name);
                        tiles.Add(t);
                    }
                }
                SaveGrid($"clips_{id}_{page}", tiles, 8);
            }
        }

        /// <summary>Motion strips through the full animator (clips + procedural layers): 5 frames per action over time.</summary>
        /// <summary>First-person views of the player's own body (head hidden, FirstPersonCalm), eye 0.24 m ahead of the body
        /// centre like PlayerController.BodyEye: idle / walk / run / crouch / carry / drink / read at pitch 0, 25, 45.</summary>
        static void FirstPersonSheet(string id)
        {
            var tiles = new List<Texture2D>();
            string[] states = { "idle", "walk", "run", "crouch", "carry", "drink", "read" };
            foreach (var st in states)
                foreach (float pitch in new[] { 0f, 25f, 45f })
                {
                    Clear();
                    var rig = Spawn(id, Vector3.zero, 0f);
                    rig.Anim.AutoFidget = false;
                    rig.SetHeadHidden(true);
                    rig.Anim.FirstPersonCalm = true;
                    ActorRig victim = null;
                    switch (st)
                    {
                        case "walk": rig.Anim.SetMove(rig.transform.forward * 1.4f, false); break;
                        case "run": rig.Anim.SetMove(rig.transform.forward * 4f, true); break;
                        case "crouch": rig.Anim.SetPosture(Posture.Crouch); break;
                        case "carry": victim = Spawn("P12", new Vector3(2f, 0, 0), 0f); rig.Anim.SetCarrying(victim); rig.Anim.SetMove(rig.transform.forward * 1.2f, false); break;
                        case "drink": rig.Anim.PlayAction(ActionAnim.Drink, 3f); break;
                        case "read": rig.Anim.PlayAction(ActionAnim.Read, 5f); break;
                    }
                    Settle(rig, st == "drink" ? 1.1f : 1.3f, 1f / 60f);
                    if (victim != null) Settle(victim, 0.3f);
                    foreach (var smr0 in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr0.forceMatrixRecalculationPerRender = true;
                    float eyeY = 1.58f; // PlayerController: eye at 1.58 m, 0.24 m ahead of the capsule centre
                    _cam.fieldOfView = 70f;
                    _cam.transform.position = rig.transform.position + Vector3.up * eyeY + rig.transform.forward * 0.24f;
                    _cam.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
                    var fill = AddFill(0.6f); fill.transform.rotation = _cam.transform.rotation;
                    var t = ShootTex(320, 200);
                    if (pitch == 0f) Label(t, st);
                    tiles.Add(t);
                }
            SaveGrid("fp_" + id, tiles, 6);
        }


        static void MotionSheet(string id)
        {
            var cases = new (string name, float dur, Action<ActorRig> start)[]
            {
                ("walk", 1.2f, r => r.Anim.SetMove(r.transform.forward * 1.3f, false)),
                ("run", 0.9f, r => r.Anim.SetMove(r.transform.forward * 3f, true)),
                ("sprint", 0.8f, r => r.Anim.SetMove(r.transform.forward * 4.6f, true)),
                ("sit_down", 1.6f, r => { r.Anim.PlayAction(ActionAnim.Use, 0.55f); }),
                ("stand_up", 1.4f, r => { }),
                ("get_up", 1.8f, r => { }),
                ("collapse", 2.2f, r => r.Anim.SetDeadPose(0)),
                ("fall", 1.8f, r => r.Anim.PlayAction(ActionAnim.Fall, 0.8f)),
                ("pickup", 0.9f, r => r.Anim.PlayAction(ActionAnim.PickUp, 0.8f)),
                ("stab", 0.7f, r => r.Anim.PlayAction(ActionAnim.Stab, 0.6f)),
                ("slash", 0.7f, r => r.Anim.PlayAction(ActionAnim.Slash, 0.6f)),
                ("overhead", 0.8f, r => r.Anim.PlayAction(ActionAnim.Overhead, 0.7f)),
                ("throw", 1.2f, r => r.Anim.PlayAction(ActionAnim.Throw, 1.1f)),
                ("hurt", 0.5f, r => r.Anim.PlayAction(ActionAnim.Hurt, 0.4f)),
                ("drink", 1.8f, r => r.Anim.PlayAction(ActionAnim.Drink, 1.8f)),
                ("use_long", 3.0f, r => r.Anim.PlayAction(ActionAnim.Use, 999f)),
                ("firstaid", 3.0f, r => r.Anim.PlayAction(ActionAnim.FirstAid, 999f)),
                ("garden", 3.0f, r => r.Anim.PlayAction(ActionAnim.Garden, 999f)),
                ("talk", 2.6f, r => r.Anim.PlayGesture(Gesture.Talk, 3f)),
                ("crossarms", 2.0f, r => r.Anim.PlayGesture(Gesture.CrossArms, 5f)),
                ("shakehead", 1.5f, r => r.Anim.PlayGesture(Gesture.ShakeHead, 1.6f)),
            };
            var tiles = new List<Texture2D>();
            foreach (var cs in cases)
            {
                Clear();
                var rig = Spawn(id, Vector3.zero, 0f);
                rig.Anim.AutoFidget = false;
                // preconditions
                if (cs.name == "stand_up") { rig.Anim.SetPosture(Posture.Sit); Settle(rig, 1.8f, 1f / 30f); rig.Anim.SetPosture(Posture.Stand); }
                else if (cs.name == "get_up") { rig.Anim.SetPosture(Posture.LieBack); Settle(rig, 1.0f, 1f / 30f); rig.Anim.SetPosture(Posture.Stand); }
                else Settle(rig, 0.4f, 1f / 30f);
                cs.start(rig);
                bool sitLater = cs.name == "sit_down";
                // several renders of one rig in the same editor frame: re-skin every render
                foreach (var smr0 in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr0.forceMatrixRecalculationPerRender = true;
                for (int k = 0; k < 5; k++)
                {
                    float t0 = cs.dur * k / 4f;
                    // advance to t0
                    float done = k == 0 ? 0f : cs.dur * (k - 1) / 4f;
                    for (float tt = done; tt < t0; tt += 1f / 60f)
                    {
                        if (sitLater && tt >= 0.7f && rig.Anim.CurrentPosture != Posture.Sit) rig.Anim.SetPosture(Posture.Sit);
                        rig.Anim.Tick(1f / 60f);
                    }
                    if (rig.Face != null) rig.Face.SnapShapes();
                    if (cs.name == "stab")
                    {
                        var smrs = rig.GetComponentsInChildren<SkinnedMeshRenderer>();
                        var sbm = new System.Text.StringBuilder();
                        foreach (var smr in smrs)
                        {
                            var bm = new Mesh(); smr.BakeMesh(bm); float zmax = -9f; foreach (var v in bm.vertices) zmax = Mathf.Max(zmax, v.z);
                            int hi = System.Array.IndexOf(smr.bones, rig.Bone(HBone.HandR));
                            sbm.Append($" [{smr.name} enabled {smr.enabled} bones {smr.bones.Length} handR idx {hi} zmax {zmax:F3} lod {smr.GetComponentInParent<LODGroup>() != null}]");
                            UnityEngine.Object.DestroyImmediate(bm);
                        }
                        Debug.Log($"[QA motion] {id} stab k{k}: {rig.Anim.DebugState} handR {rig.transform.InverseTransformPoint(rig.Bone(HBone.HandR).position).ToString("F3")} {sbm}");
                    }
                    var fill = AddFill(0.5f);
                    LookAt(new Vector3(0, rig.Height * 0.5f, 0.05f), cs.name == "walk" || cs.name == "run" || cs.name == "sprint" ? 90f : 40f, 6f, 3.3f, 34f);
                    fill.transform.rotation = _cam.transform.rotation;
                    var t = ShootTex(240, 340);
                    if (k == 0) Label(t, cs.name);
                    tiles.Add(t);
                }
            }
            SaveGrid("motion_" + id, tiles, 10);
        }

        /// <summary>The source clips on the source mannequin (reference for judging the retarget).</summary>
        static void SourceClipsSheet(string[] clipNames, bool generic = false)
        {
            var tiles = new List<Texture2D>();
            var paths = generic ? new[] { ClipRetarget.LabDir + "/UAL2_Generic.fbx" } : new[] { ClipRetarget.LabDir + "/UAL1_Standard.fbx", ClipRetarget.LabDir + "/UAL2_Standard.fbx" };
            if (generic)
            {
                var imp = (ModelImporter)AssetImporter.GetAtPath(paths[0]);
                if (imp.animationType != ModelImporterAnimationType.Generic) { imp.animationType = ModelImporterAnimationType.Generic; imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; imp.SaveAndReimport(); }
            }
            else
            {
                // log the source avatar's bone mapping
                var av = AssetDatabase.LoadAllAssetsAtPath(paths[0]).OfType<Avatar>().FirstOrDefault();
                if (av != null) Debug.Log("[QA] source avatar map: " + string.Join(", ", av.humanDescription.human.Select(h => h.humanName + "=" + h.boneName)));
            }
            foreach (var path in paths)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToList();
                foreach (var name in clipNames)
                {
                    var clip = clips.FirstOrDefault(c => c.name.EndsWith("|" + name) || c.name == name);
                    if (clip == null) continue;
                    for (int k = 0; k < 4; k++)
                    {
                        Clear();
                        var go = (GameObject)UnityEngine.Object.Instantiate(model, _stage);
                        go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;
                        var an = go.GetComponent<Animator>(); if (an == null) an = go.AddComponent<Animator>();
                        an.avatar = avatar; an.applyRootMotion = false; an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                        var graph = UnityEngine.Playables.PlayableGraph.Create("src");
                        graph.SetTimeUpdateMode(UnityEngine.Playables.DirectorUpdateMode.Manual);
                        var output = UnityEngine.Animations.AnimationPlayableOutput.Create(graph, "o", an);
                        var cp = UnityEngine.Animations.AnimationClipPlayable.Create(graph, clip);
                        output.SetSourcePlayable(cp); graph.Play();
                        cp.SetTime(clip.length * k / 3f); graph.Evaluate(0f);
                        var b = new Bounds(go.transform.position, Vector3.zero);
                        foreach (var r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                        var fill = AddFill(0.5f);
                        LookAt(new Vector3(0, 0.9f, 0.05f), 35f, 6f, 3.6f, 34f);
                        fill.transform.rotation = _cam.transform.rotation;
                        var t = ShootTex(260, 380);
                        if (k == 0) Label(t, name);
                        tiles.Add(t);
                        graph.Destroy();
                    }
                }
            }
            SaveGrid(generic ? "clips_source_generic" : "clips_source", tiles, 8);
        }

        /// <summary>Burns a short label into the top-left corner of a tile (tiny 3x5 pixel font, upper case / digits / _).</summary>
        static void Label(Texture2D t, string s)
        {
            string[] glyph = new string[128];
            void G(char c, string rows) => glyph[c] = rows;
            G('A', "010101111101101"); G('B', "110101110101110"); G('C', "011100100100011"); G('D', "110101101101110"); G('E', "111100110100111");
            G('F', "111100110100100"); G('G', "011100101101011"); G('H', "101101111101101"); G('I', "111010010010111"); G('J', "001001001101010");
            G('K', "101101110101101"); G('L', "100100100100111"); G('M', "101111111101101"); G('N', "110101101101101"); G('O', "010101101101010");
            G('P', "110101110100100"); G('Q', "010101101110011"); G('R', "110101110101101"); G('S', "011100010001110"); G('T', "111010010010010");
            G('U', "101101101101111"); G('V', "101101101101010"); G('W', "101101111111101"); G('X', "101101010101101"); G('Y', "101101010010010");
            G('Z', "111001010100111"); G('_', "000000000000111"); G('0', "111101101101111"); G('1', "010110010010111"); G('2', "110001010100111");
            G('3', "110001010001110"); G('4', "101101111001001");
            int x0 = 4, y0 = t.height - 12, sc = 2;
            foreach (char ch0 in s.ToUpperInvariant())
            {
                char ch = ch0 < 128 ? ch0 : '_';
                var g = glyph[ch];
                if (g != null)
                    for (int r = 0; r < 5; r++)
                        for (int col = 0; col < 3; col++)
                            if (g[r * 3 + col] == '1')
                                for (int dy = 0; dy < sc; dy++) for (int dx = 0; dx < sc; dx++)
                                    { int x = x0 + col * sc + dx, y = y0 - r * sc - dy; if (x >= 0 && x < t.width && y >= 0 && y < t.height) t.SetPixel(x, y, Color.yellow); }
                x0 += 4 * sc;
            }
            t.Apply();
        }

        static readonly Color[] BoneCols =
        {
            new Color(1,1,1), new Color(0.9f,0.9f,0.3f), new Color(1f,0.6f,0.2f), new Color(0.6f,0.3f,0.1f), new Color(0.95f,0.8f,0.7f),
            new Color(0.2f,0.2f,0.9f), new Color(0.2f,0.6f,1f), new Color(0.1f,0.9f,0.9f), new Color(0.6f,0.9f,1f),
            new Color(0.9f,0.1f,0.1f), new Color(1f,0.4f,0.6f), new Color(1f,0.1f,0.8f), new Color(1f,0.7f,0.9f),
            new Color(0.1f,0.6f,0.1f), new Color(0.4f,0.9f,0.3f), new Color(0.1f,0.4f,0.3f), new Color(0.3f,0.3f,0.3f),
            new Color(0.5f,0.2f,0.7f), new Color(0.7f,0.5f,1f), new Color(0.3f,0.1f,0.5f), new Color(0.5f,0.5f,0.5f)
        };

        /// <summary>Bind-pose mesh coloured by skin weights + joint markers (front, side, back).</summary>
        static void WeightsSheet(string id)
        {
            var prefab = Resources.Load<GameObject>("Actors/" + id);
            if (prefab == null) return;
            var smr = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
            var src = smr.sharedMesh;
            var m = UnityEngine.Object.Instantiate(src);
            var bw = src.boneWeights;
            var cols = new Color[bw.Length];
            for (int i = 0; i < bw.Length; i++)
            {
                Color C(int b) => b < BoneCols.Length ? BoneCols[b] : Color.gray;
                var w = bw[i];
                cols[i] = C(w.boneIndex0) * w.weight0 + C(w.boneIndex1) * w.weight1 + C(w.boneIndex2) * w.weight2 + C(w.boneIndex3) * w.weight3;
            }
            m.colors = cols;
            var mat = new Material(Shader.Find("BL23/ToonCharacter"));
            mat.SetFloat("_UseVertexColor", 1f); mat.SetFloat("_CavityStrength", 0f); mat.SetFloat("_OutlineWidth", 0.6f);
            var tiles = new List<Texture2D>();
            foreach (float yaw in new[] { 0f, 90f, 180f })
            {
                Clear();
                var go = new GameObject("W"); go.transform.SetParent(_stage, false);
                go.transform.rotation = Quaternion.Euler(0, yaw, 0);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = Enumerable.Repeat(mat, m.subMeshCount).ToArray();
                foreach (var t in smr.bones)
                {
                    var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    s.transform.SetParent(go.transform, false);
                    s.transform.localPosition = prefab.transform.InverseTransformPoint(t.position);
                    s.transform.localScale = Vector3.one * 0.03f;
                    s.GetComponent<MeshRenderer>().sharedMaterial = mat;
                }
                LookAt(new Vector3(0, 0.9f, 0), 0f, 0f, 6f, 20f);
                tiles.Add(ShootTex(600, 1000));
            }
            SaveGrid("weights_" + id, tiles, 3);
        }

        /// <summary>Orthographic front view of the face with 1 cm ticks (red every 5 cm) through the eye anchor, for face-feature calibration.</summary>
        static void FaceCalib(string id)
        {
            Clear();
            var rig = Spawn(id, Vector3.zero, 0f);
            rig.Anim.AutoFidget = false;
            Settle(rig, 0.3f);
            Vector3 e = rig.EyeAnchor.position;
            var red = ToonRuntime.Mat(Color.red, 0, 0); var yel = ToonRuntime.Mat(Color.yellow, 0, 0);
            for (int i = -9; i <= 9; i++)
            {
                foreach (var p in new[] { new Vector3(e.x + i * 0.01f, e.y, e.z + 0.25f), new Vector3(e.x, e.y + i * 0.01f - 0.04f, e.z + 0.25f) })
                {
                    var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    s.transform.SetParent(_stage, false);
                    s.transform.position = p; s.transform.localScale = Vector3.one * (i % 5 == 0 ? 0.004f : 0.0025f);
                    s.GetComponent<MeshRenderer>().sharedMaterial = i % 5 == 0 ? red : yel;
                }
            }
            _cam.orthographic = true; _cam.orthographicSize = 0.1f;
            _cam.transform.rotation = Quaternion.Euler(0, 180f, 0);
            _cam.transform.position = new Vector3(e.x, e.y - 0.04f, e.z + 1.5f);
            var t = ShootTex(800, 800);
            _cam.orthographic = false;
            File.WriteAllBytes(Path.Combine(ShotDir, "calib_" + id + ".png"), t.EncodeToPNG());
            Debug.Log($"[CALIB] {id} eye anchor {e.x:F4},{e.y:F4},{e.z:F4}");
        }

        static void DistanceSheet(string id)
        {
            var tiles = new List<Texture2D>();
            foreach (float d in new[] { 1f, 2f, 3.5f, 6f, 9f })
            {
                Clear();
                var rig = Spawn(id, Vector3.zero, 0f);
                Settle(rig, 0.5f);
                var sb = new System.Text.StringBuilder();
                foreach (var m in rig.Materials) if (m.IsKeywordEnabled("_FACE")) sb.Append(m.name + " FACE " + m.GetVector("_FaceState0") + " eye " + m.GetVector("_FaceEye") + "; ");
                Debug.Log("[QA] " + id + " face mats: " + sb);
                LookAt(rig.EyeAnchor.position, 0f, 0f, d, 18f);
                float fov = 2f * Mathf.Atan(0.35f / d) * Mathf.Rad2Deg;
                _cam.fieldOfView = fov;
                tiles.Add(ShootTex(360, 360));
            }
            SaveGrid("distance_" + id, tiles, 5);
        }

        static void BreakSheet()
        {
            var tiles = new List<Texture2D>();
            foreach (var id in new[] { "P02", "P04", "NPC00" })
                for (int k = 0; k < 2; k++)
                {
                    Clear();
                    var rig = Spawn(id, Vector3.zero, 0f);
                    rig.SetExpression(k == 0 ? Expr.Neutral : Expr.Break);
                    rig.Anim.SetBreak(k == 0 ? 0f : 0.85f);
                    Settle(rig, 1.0f + k * 0.37f);
                    LookAt(new Vector3(0, rig.Height * 0.72f, 0), 15f, 4f, 2.6f, 30f);
                    tiles.Add(ShootTex(480, 600));
                }
            SaveGrid("break", tiles, 6);
        }

        static void Portraits()
        {
            var tiles = new List<Texture2D>();
            foreach (var c in Cast.All)
            {
                Clear();
                var rig = Spawn(c.Id, Vector3.zero, 0f);
                rig.SetExpression(Expr.Smile, 0.3f);
                Settle(rig, 0.8f);
                var eye = rig.EyeAnchor.position;
                LookAt(eye + new Vector3(0, -0.18f, 0), 20f, 4f, 1.55f, 26f);
                tiles.Add(ShootTex(400, 520));
            }
            SaveGrid("portraits", tiles, 7);
        }
    }
}
