using System.IO;
using System.Text;
using BL23.Game.Characters;
using BL23.Sim;
using UnityEngine;

namespace BL23.EditorTools.Characters
{
    public static partial class GlbDebugAnim
    {
        /// <summary>Logs the animator state over time for a few scripted scenarios (walk, sit/stand).</summary>
        public static void Trace()
        {
            var sb = new StringBuilder();
            foreach (var id in new[] { "P04" })
            {
                var def = Cast.Get(id);
                var holder = new GameObject("H");
                var rig = ActorFactory.Create(def, holder.transform);
                var an = rig.Anim; an.AutoFidget = false;
                sb.AppendLine($"== {id}");
                for (int i = 0; i < 12; i++) an.Tick(1f / 30f);
                sb.AppendLine("idle: " + an.DebugState);
                an.SetMove(rig.transform.forward * 1.3f, false);
                for (int k = 0; k < 6; k++) { for (int i = 0; i < 6; i++) an.Tick(1f / 30f); sb.AppendLine($"walk {k * 0.2f + 0.2f:F1}s: {an.DebugState} footL {rig.transform.InverseTransformPoint(rig.Bone(HBone.FootL).position).ToString("F3")}"); }
                an.SetMove(Vector3.zero, false);
                for (int i = 0; i < 30; i++) an.Tick(1f / 30f);
                an.SetPosture(Posture.Sit);
                for (int k = 0; k < 8; k++) { for (int i = 0; i < 8; i++) an.Tick(1f / 30f); sb.AppendLine($"sit {k * 0.27f + 0.27f:F2}s: {an.DebugState}"); }
                an.SetPosture(Posture.Stand);
                for (int k = 0; k < 8; k++) { for (int i = 0; i < 8; i++) an.Tick(1f / 30f); sb.AppendLine($"stand {k * 0.27f + 0.27f:F2}s: {an.DebugState}"); }
                for (int i = 0; i < 40; i++) an.Tick(1f / 30f);
                an.PlayAction(ActionAnim.Stab, 0.6f);
                for (int k = 0; k < 5; k++) { for (int i = 0; i < 4; i++) an.Tick(1f / 30f); sb.AppendLine($"stab {k * 0.133f + 0.133f:F2}s: {an.DebugState} handR {rig.transform.InverseTransformPoint(rig.Bone(HBone.HandR).position).ToString("F3")}"); }
                Object.DestroyImmediate(holder);
            }
            File.WriteAllText(Path.Combine(Application.dataPath, "../Logs/animtrace.txt"), sb.ToString());
        }
    }
}
