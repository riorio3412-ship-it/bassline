using System.Collections.Generic;
using BL23.Sim;
using UnityEngine;

namespace BL23.Game
{
    /// <summary>
    /// How blood-soaked a body's clothes read: the kernel only bloodies attackers' clothes (BloodOnClothes), so a victim
    /// would otherwise lie in a pool in spotless clothes. clamp01(BloodLoss·0.9 + Σ 0.12·sev over live Cut/Stab wounds with
    /// sev ≥ 2 + Σ 0.07·sev over live blunt head/neck wounds with sev ≥ 3). Cached per (actor, wound count, blood loss
    /// bucket). ActorView turns it into a light spatter (the wounds' own masks carry the local soak).
    /// </summary>
    public static class GoreSoak
    {
        struct Entry { public int Wounds; public int LossBucket; public float Value; }
        static readonly Dictionary<string, Entry> _cache = new Dictionary<string, Entry>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _cache.Clear(); }

        public static float Of(Actor a)
        {
            if (a?.Body == null) return 0f;
            var b = a.Body; int wc = b.Wounds != null ? b.Wounds.Count : 0; int lb = Mathf.RoundToInt(b.BloodLoss * 50f);
            if (a.Id != null && _cache.TryGetValue(a.Id, out var e) && e.Wounds == wc && e.LossBucket == lb) return e.Value;
            float v = b.BloodLoss * 0.9f;
            if (b.Wounds != null)
                foreach (var w in b.Wounds)
                {
                    if (w.Postmortem || w.Sev < 2) continue;
                    if (w.Type == DamageType.Cut || w.Type == DamageType.Stab) v += 0.12f * Mathf.Min(w.Sev, 4);
                    // a split scalp bleeds freely too
                    else if ((w.Type == DamageType.Blunt || w.Type == DamageType.Crush) && (w.Region == BodyRegion.Head || w.Region == BodyRegion.Neck) && w.Sev >= 3) v += 0.07f * Mathf.Min(w.Sev, 4);
                }
            v = Mathf.Clamp01(v);
            if (a.Id != null) _cache[a.Id] = new Entry { Wounds = wc, LossBucket = lb, Value = v };
            return v;
        }
    }
}
