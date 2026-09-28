using System;
using System.Collections.Generic;
using System.Linq;

namespace BL23.Sim
{
    /// <summary>
    /// Personal space. Bodies do not pass through each other: people standing or walking keep ~0.6 m apart
    /// (a little more when both are standing still), sidestep each other in corridors, and nobody picks a place to stand
    /// right where someone already is or is heading. Seated people, sleepers, the fallen and the player are obstacles
    /// that are never pushed. Deterministic (fixed iteration order, no randomness).
    /// </summary>
    public sealed partial class Simulation
    {
        const float SpaceMoving = 0.62f, SpaceStill = 0.8f, PushPerTick = 0.06f;
        readonly List<Actor> _crowd = new List<Actor>();

        bool Pushable(Actor a)
        {
            if (a.IsPlayer || !a.Alive || a.Status != ActorStatus.Active || a.StairId >= 0 || a.CarriedBy != null) return false;
            if (a.Pose != Pose.Stand && a.Pose != Pose.Crouch) return false;
            if (a.Spot >= 0 && a.Spot < S.Layout.Spots.Count && S.Layout.Spots[a.Spot].OnFurniture) return false;
            if (a.Act != null && !a.Act.Interruptible && a.Act.Id != null && a.Act.Id.StartsWith("murder") && a.Act.Cur?.Kind == "Attack") return false;
            return true;
        }

        void Crowd()
        {
            _crowd.Clear();
            foreach (var a in S.Actors.Values) if (a.StairId < 0 && a.CarriedBy == null && (a.Alive || a.Status == ActorStatus.Unconscious || a.Status == ActorStatus.Dead)) _crowd.Add(a);
            for (int i = 0; i < _crowd.Count; i++)
            {
                var a = _crowd[i];
                for (int j = i + 1; j < _crowd.Count; j++)
                {
                    var b = _crowd[j];
                    if (a.Pos.f != b.Pos.f) continue;
                    float dx = b.Pos.x - a.Pos.x, dz = b.Pos.z - a.Pos.z; float d2 = dx * dx + dz * dz;
                    bool still = a.Speed < 0.05f && b.Speed < 0.05f;
                    float min = still ? SpaceStill : SpaceMoving;
                    if (a.TalkingTo == b.Id || b.TalkingTo == a.Id) min = 0.95f;   // conversation distance
                    if (a.Carrying == b.Id || b.Carrying == a.Id) continue;
                    // nobody stands on the dead: people who have stopped next to a body step back off it (those still walking may pass)
                    bool deadA = a.Status == ActorStatus.Dead, deadB = b.Status == ActorStatus.Dead;
                    if (deadA || deadB) { if (deadA && deadB) continue; if ((deadA ? b : a).Speed > 0.05f) continue; min = 0.85f; }
                    if (d2 >= min * min) continue;
                    bool pa = Pushable(a), pb = Pushable(b); if (!pa && !pb) continue;
                    float d = (float)Math.Sqrt(d2);
                    float nx, nz;
                    if (d > 1e-3f) { nx = dx / d; nz = dz / d; }
                    else { double ang = (i * 2.399963 + j * 0.7); nx = (float)Math.Cos(ang); nz = (float)Math.Sin(ang); }   // exactly on top: split along a fixed direction
                    float overlap = Math.Min(min - d, PushPerTick * 2f);
                    float wa = pa && pb ? 0.5f : pa ? 1f : 0f, wb = pa && pb ? 0.5f : pb ? 1f : 0f;
                    // someone walking yields to someone standing (they are the one who can go around)
                    if (pa && pb && a.Speed > 0.05f && b.Speed < 0.05f) { wa = 0.8f; wb = 0.2f; }
                    else if (pa && pb && b.Speed > 0.05f && a.Speed < 0.05f) { wa = 0.2f; wb = 0.8f; }
                    if (wa > 0) Nudge(a, -nx * overlap * wa, -nz * overlap * wa, nx, nz);
                    if (wb > 0) Nudge(b, nx * overlap * wb, nz * overlap * wb, nx, nz);
                }
            }
        }

        void Nudge(Actor a, float mx, float mz, float nx, float nz)
        {
            var g = S.Layout.Nav(a.Pos.f);
            // moving people also slide a little sideways (a natural sidestep instead of a head-on shove)
            if (a.Speed > 0.05f) { float mag = (float)Math.Sqrt(mx * mx + mz * mz); float s = a.Id[a.Id.Length - 1] % 2 == 0 ? 0.5f : -0.5f; mx += -nz * s * mag; mz += nx * s * mag; }
            var np = new P3(a.Pos.f, a.Pos.x + mx, a.Pos.z + mz);
            int k = g.CellOf(np.x, np.z);
            if (!g.Walkable(k) || !g.InMain(k)) return;
            a.Pos = np; UpdateRoom(a);
        }

        /// <summary>Distance from p to the nearest other person on that floor, counting where people are walking to as well.</summary>
        float NearestOther(P3 p, Actor self = null)
        {
            float best = 99f;
            foreach (var o in S.Actors.Values)
            {
                if (o == self || !(o.Alive || o.Status == ActorStatus.Unconscious || o.Status == ActorStatus.Dead && o.CarriedBy == null) || o.Pos.f != p.f) continue;
                float d = o.Pos.DistXZ(p); if (d < best) best = d;
                var st = o.Act?.Cur;
                if (st != null && st.Kind == "GoTo" && st.Target.f == p.f) { float dt = st.Target.DistXZ(p); if (dt < best) best = dt; }
            }
            return best;
        }
    }
}
