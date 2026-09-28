using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace BL23.EditorTools.Characters
{
    /// <summary>
    /// Offline retargeting of the CC0 Quaternius Universal Animation Library clips onto the BL23 rigs.
    /// Each rig gets a Unity Human avatar built from a constructed T-pose of its own skeleton; the humanoid clips are
    /// evaluated on it with a PlayableGraph (Mecanim retargeting), and the posed bones are read back into the canonical
    /// ActorPose space (ActorAnimator.ReadCanonical) and written to Resources/Actors/Anim/&lt;set&gt;.bytes.
    /// Sets: "PROC" (shared by the procedural cast, retargeted through a reference actor) and one per scanned actor.
    /// </summary>
    public static class ClipRetarget
    {
        public const string LabDir = "Assets/LabOnly/UAL";
        const string OutDir = "Assets/BASSLINE/BL23/Resources/Actors/Anim";
        static readonly string[] Sources =
        {
            "C:/Users/리오/BL23Lab/dl/anim/Universal Animation Library[Standard]/Unity/UAL1_Standard.fbx",
            "C:/Users/리오/BL23Lab/dl/anim/Universal Animation Library 2[Standard]/Unity/UAL2_Standard.fbx",
        };

        public static readonly string[] Keep =
        {
            "Idle_Loop", "Walk_Loop", "Walk_Formal_Loop", "Jog_Fwd_Loop", "Sprint_Loop", "Sitting_Idle_Loop", "Sitting_Talking_Loop",
            "Idle_Talking_Loop", "Idle_FoldArms_Loop", "Yes", "Idle_No_Loop", "Hit_Chest", "Hit_Head", "Hit_Knockback", "Consume",
            "Interact", "Fixing_Kneeling", "Crouch_Idle_Loop", "PickUp_Table", "Death01", "LayToIdle", "Walk_Carry_Loop", "Push_Loop",
            "Punch_Cross", "Melee_Hook", "Sword_Attack", "OverhandThrow", "Idle_Rail_Loop", "Chest_Open", "Idle_TalkingPhone_Loop",
            "Sitting_Enter", "Sitting_Exit", "Farm_PlantSeed", "Farm_Harvest", "Farm_Watering", "TreeChopping_Loop", "Sword_Regular_A", "Sword_Regular_B",
            "Sword_Regular_C", "Punch_Jab", "Crouch_Fwd_Loop", "Dance_Loop", "Idle_Lantern_Loop", "Idle_Torch_Loop", "Swim_Idle_Loop", "Swim_Fwd_Loop",
            "Idle_Rail_Call", "Sword_Idle", "Jump_Start", "Jump_Land", "Melee_Hook_Rec",
        };

        /// <summary>Copies the source FBX files into the lab project and imports them as Humanoid.</summary>
        public static void ImportSources()
        {
            Directory.CreateDirectory(Path.GetFullPath(LabDir));
            foreach (var src in Sources)
            {
                string dst = LabDir + "/" + Path.GetFileName(src);
                if (!File.Exists(Path.GetFullPath(dst))) File.Copy(src, Path.GetFullPath(dst));
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
                var imp = (ModelImporter)AssetImporter.GetAtPath(dst);
                bool dirty = false;
                if (imp.animationType != ModelImporterAnimationType.Human) { imp.animationType = ModelImporterAnimationType.Human; dirty = true; }
                if (imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; dirty = true; }
                if (!imp.importAnimation) { imp.importAnimation = true; dirty = true; }
                if (dirty) imp.SaveAndReimport();
                var avatar = AssetDatabase.LoadAllAssetsAtPath(dst).OfType<Avatar>().FirstOrDefault();
                var clips = AssetDatabase.LoadAllAssetsAtPath(dst).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToList();
                Debug.Log($"[ClipRetarget] {dst}: avatar {(avatar != null ? avatar.isHuman + "/" + avatar.isValid : "none")}, {clips.Count} clips: {string.Join(", ", clips.Select(c => c.name + " (" + c.length.ToString("F2") + "s)"))}");
            }
        }

        static List<AnimationClip> SourceClips()
        {
            var all = new Dictionary<string, AnimationClip>();
            foreach (var src in Sources)
            {
                string dst = LabDir + "/" + Path.GetFileName(src);
                foreach (var c in AssetDatabase.LoadAllAssetsAtPath(dst).OfType<AnimationClip>())
                {
                    if (c.name.StartsWith("__preview")) continue;
                    string n = c.name.Contains("|") ? c.name.Substring(c.name.LastIndexOf('|') + 1) : c.name;
                    if (Keep.Contains(n) && !all.ContainsKey(n)) all[n] = c;
                }
            }
            return all.Values.ToList();
        }

        static string ShortName(AnimationClip c) => c.name.Contains("|") ? c.name.Substring(c.name.LastIndexOf('|') + 1) : c.name;

        /// <summary>Batch: -executeMethod ...ClipRetarget.RetargetAll [-animSets PROC:P06,P01,P02,P04]</summary>
        public static void RetargetAll()
        {
            ImportSources();
            var args = Environment.GetCommandLineArgs();
            int ai = Array.IndexOf(args, "-animSets");
            var sets = ai >= 0 && ai + 1 < args.Length ? args[ai + 1].Split(',') : new[] { "PROC:P06", "P01", "P02", "P04" };
            CharacterBaker.EnsureFolder(OutDir);
            var clips = SourceClips();
            var log = new System.Text.StringBuilder();
            log.AppendLine($"clips: {string.Join(", ", clips.Select(ShortName))}");
            foreach (var s in sets)
            {
                string set = s.Contains(":") ? s.Split(':')[0] : s;
                string actor = s.Contains(":") ? s.Split(':')[1] : s;
                try { Retarget(set, actor, clips, log); }
                catch (Exception e) { log.AppendLine($"[{set}] FAILED {e}"); Debug.LogException(e); }
            }
            AssetDatabase.Refresh();
            ActorClipSet.ClearCache();
            File.WriteAllText(Path.Combine(Application.dataPath, "../Logs/retarget_report.txt"), log.ToString());
            Debug.Log("[ClipRetarget] " + log);
        }

        static void Retarget(string set, string actorId, List<AnimationClip> clips, System.Text.StringBuilder log)
        {
            var prefab = Resources.Load<GameObject>("Actors/" + actorId);
            if (prefab == null) { log.AppendLine($"[{set}] no prefab {actorId}"); return; }
            var go = (GameObject)UnityEngine.Object.Instantiate(prefab);
            try
            {
                go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;
                var rig = go.GetComponent<ActorRig>();
                rig.Init();
                var anim = rig.Anim != null ? rig.Anim : go.GetComponent<ActorAnimator>();
                anim.UseClips = false;
                anim.Init();
                var avatar = BuildAvatar(go, rig, log, set);
                if (avatar == null) return;
                var animator = go.GetComponent<Animator>();
                if (animator == null) animator = go.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var outClips = new List<ActorClipSet.Clip>();
                var pose = new ActorPose();
                float legLen = Mathf.Max(0.3f, anim.LegLen);
                const float fps = 30f;
                foreach (var clip in clips)
                {
                    var graph = PlayableGraph.Create("BL23Retarget");
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var output = AnimationPlayableOutput.Create(graph, "out", animator);
                    var cp = AnimationClipPlayable.Create(graph, clip);
                    cp.SetApplyFootIK(true);
                    output.SetSourcePlayable(cp);
                    graph.Play();
                    int frames = Mathf.Max(2, Mathf.RoundToInt(clip.length * fps) + 1);
                    string name = ShortName(clip);
                    var c = new ActorClipSet.Clip { Name = name, Fps = fps, Loop = name.EndsWith("_Loop"), Frames = frames, Hips = new Vector3[frames], R = new Quaternion[frames * ActorSkeleton.Count] };
                    var fy = new float[2, frames]; var fz = new float[2, frames];
                    for (int f = 0; f < frames; f++)
                    {
                        float t = Mathf.Min(clip.length, f / fps);
                        cp.SetTime(t);
                        graph.Evaluate(0f);
                        anim.ReadCanonical(pose);
                        c.Hips[f] = pose.HipsOffset / legLen;
                        for (int b = 0; b < ActorSkeleton.Count; b++) c.R[f * ActorSkeleton.Count + b] = ActorSkeleton.IsFinger((HBone)b) ? Quaternion.identity : pose.R[b];
                        fy[0, f] = pose.FootL.y; fz[0, f] = pose.FootL.z; fy[1, f] = pose.FootR.y; fz[1, f] = pose.FootR.z;
                    }
                    graph.Destroy();
                    // ground speed the in-place cycle was made for: how fast a planted foot slides back under the hips
                    var speeds = new List<float>();
                    for (int s2 = 0; s2 < 2; s2++)
                    {
                        float ymin = float.MaxValue; for (int f = 0; f < frames; f++) ymin = Mathf.Min(ymin, fy[s2, f]);
                        for (int f = 0; f + 1 < frames; f++)
                            if (fy[s2, f] < ymin + 0.03f && fy[s2, f + 1] < ymin + 0.03f) speeds.Add(-(fz[s2, f + 1] - fz[s2, f]) * fps);
                    }
                    speeds.Sort();
                    float vClip = speeds.Count > 8 ? speeds[speeds.Count / 2] : 0f;
                    if (vClip < 0.05f && c.Loop)
                    {
                        // runs: short stance, feet mostly in the air -> average backward foot speed over the backward sweep
                        float sum = 0f; int nb = 0;
                        for (int s2 = 0; s2 < 2; s2++) for (int f = 0; f + 1 < frames; f++) { float dz = fz[s2, f] - fz[s2, f + 1]; if (dz > 0f) { sum += dz; nb++; } }
                        vClip = nb > 0 ? sum / nb * fps * 1.2f : 0f;
                    }
                    c.CycleDist = Mathf.Max(0f, vClip) * clip.length / legLen;
                    if (c.Loop && (c.Name.Contains("Jog") || c.Name.Contains("Sprint") || c.Name.Contains("Walk")))
                    {
                        float y0 = float.MaxValue, y1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
                        for (int f = 0; f < frames; f++) { y0 = Mathf.Min(y0, fy[0, f]); y1 = Mathf.Max(y1, fy[0, f]); z0 = Mathf.Min(z0, fz[0, f]); z1 = Mathf.Max(z1, fz[0, f]); }
                        log.AppendLine($"   {c.Name}: frames {frames} footL y {y0:F3}..{y1:F3} z {z0:F3}..{z1:F3} stance samples {speeds.Count} median {vClip:F2} m/s; ys " + string.Join(" ", Enumerable.Range(0, frames).Select(f => fy[0, f].ToString("F2"))) + " zs " + string.Join(" ", Enumerable.Range(0, frames).Select(f => fz[0, f].ToString("F2"))));
                    }
                    outClips.Add(c);
                }
                var bytes = ActorClipSet.Write(outClips);
                string path = OutDir + "/" + set + ".bytes";
                File.WriteAllBytes(Path.GetFullPath(path), bytes);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                log.AppendLine($"[{set}] via {actorId}: {outClips.Count} clips, {bytes.Length / 1024} KB, leg {legLen:F2} m; speeds " + string.Join(", ", outClips.Where(x => x.CycleDist > 0.05f).Select(x => $"{x.Name} {x.CycleDist * legLen / Mathf.Max(0.01f, x.Length):F2} m/s")));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        /// <summary>Human avatar of a BL23 rig: its own skeleton straightened into a T-pose (the avatar's reference pose).</summary>
        public static Avatar BuildAvatar(GameObject go, ActorRig rig, System.Text.StringBuilder log, string tag)
        {
            var all = go.GetComponentsInChildren<Transform>(true);
            var saved = all.ToDictionary(t => t, t => (t.localPosition, t.localRotation, t.localScale));
            Transform B(HBone b) => rig.HasBone(b) ? rig.Bones[(int)b] : null;
            // rest fingertip positions in hand space
            Vector3 tipL = B(HBone.HandL).InverseTransformPoint(go.transform.TransformPoint(rig.HandTipRestL));
            Vector3 tipR = B(HBone.HandR).InverseTransformPoint(go.transform.TransformPoint(rig.HandTipRestR));
            void Aim(HBone b, Vector3 childWorld, Vector3 dir)
            {
                var t = B(b); if (t == null) return;
                Vector3 cur = childWorld - t.position;
                if (cur.sqrMagnitude < 1e-10f) return;
                t.rotation = Quaternion.FromToRotation(cur, dir) * t.rotation;
            }
            Vector3 P(HBone b) => B(b) != null ? B(b).position : Vector3.zero;
            Aim(HBone.Hips, P(HBone.Spine), Vector3.up);
            Aim(HBone.Spine, P(HBone.Chest), Vector3.up);
            if (B(HBone.UpperChest) != null) { Aim(HBone.Chest, P(HBone.UpperChest), Vector3.up); Aim(HBone.UpperChest, P(HBone.Neck), Vector3.up); }
            else Aim(HBone.Chest, P(HBone.Neck), Vector3.up);
            Aim(HBone.Neck, P(HBone.Head), Vector3.up);
            for (int s = 0; s < 2; s++)
            {
                bool L = s == 0;
                Vector3 outDir = L ? Vector3.left : Vector3.right;
                Aim(L ? HBone.UpperArmL : HBone.UpperArmR, P(L ? HBone.LowerArmL : HBone.LowerArmR), outDir);
                Aim(L ? HBone.LowerArmL : HBone.LowerArmR, P(L ? HBone.HandL : HBone.HandR), outDir);
                var hand = B(L ? HBone.HandL : HBone.HandR);
                Aim(L ? HBone.HandL : HBone.HandR, hand.TransformPoint(L ? tipL : tipR), outDir);
                Aim(L ? HBone.UpperLegL : HBone.UpperLegR, P(L ? HBone.LowerLegL : HBone.LowerLegR), Vector3.down);
                Aim(L ? HBone.LowerLegL : HBone.LowerLegR, P(L ? HBone.FootL : HBone.FootR), Vector3.down);
            }
            var map = new List<HumanBone>();
            void M(string human, HBone b) { var t = B(b); if (t != null) map.Add(new HumanBone { humanName = human, boneName = t.name, limit = new HumanLimit { useDefaultValues = true } }); }
            M("Hips", HBone.Hips); M("Spine", HBone.Spine); M("Chest", HBone.Chest); M("UpperChest", HBone.UpperChest); M("Neck", HBone.Neck); M("Head", HBone.Head);
            M("LeftShoulder", HBone.ShoulderL); M("LeftUpperArm", HBone.UpperArmL); M("LeftLowerArm", HBone.LowerArmL); M("LeftHand", HBone.HandL);
            M("RightShoulder", HBone.ShoulderR); M("RightUpperArm", HBone.UpperArmR); M("RightLowerArm", HBone.LowerArmR); M("RightHand", HBone.HandR);
            M("LeftUpperLeg", HBone.UpperLegL); M("LeftLowerLeg", HBone.LowerLegL); M("LeftFoot", HBone.FootL); M("LeftToes", HBone.ToeL);
            M("RightUpperLeg", HBone.UpperLegR); M("RightLowerLeg", HBone.LowerLegR); M("RightFoot", HBone.FootR); M("RightToes", HBone.ToeR);
            var skel = all.Where(t => t == go.transform || t.name == "Armature" || ActorSkeleton.Names.Contains(t.name)).Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray();
            var desc = new HumanDescription
            {
                human = map.ToArray(), skeleton = skel,
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false,
            };
            // unique names are required
            var dup = all.GroupBy(t => t.name).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            foreach (var t in all) if (dup.Contains(t.name) && !map.Any(m => m.boneName == t.name)) { }
            Avatar avatar = null;
            try { avatar = AvatarBuilder.BuildHumanAvatar(go, desc); }
            catch (Exception e) { log.AppendLine($"[{tag}] avatar build threw {e.Message}"); }
            foreach (var kv in saved) { kv.Key.localPosition = kv.Value.Item1; kv.Key.localRotation = kv.Value.Item2; kv.Key.localScale = kv.Value.Item3; }
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                log.AppendLine($"[{tag}] avatar invalid (valid {avatar?.isValid}, human {avatar?.isHuman}); duplicate names: {string.Join(",", dup)}");
                return null;
            }
            avatar.name = tag + "_Avatar";
            return avatar;
        }
    }
}
