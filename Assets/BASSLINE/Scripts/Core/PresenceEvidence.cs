using System;
using System.Linq;
using System.Collections.Generic;
namespace BASSLINE.Core
{
    public static class PresenceEvidence
    {
        public static bool Covers(IEnumerable<KnownRecord> records,string subject,string place,long from,long to,out string[] used)
        {
            used=Array.Empty<string>();if(from<0||to<=from)return false;
            long cursor=from;var ids=new List<string>();
            foreach(var r in records.Where(r=>r!=null&&r.Direct&&r.IdentityConfirmed&&r.Predicate=="AtPlace"&&r.SubjectId==subject&&r.Value==place&&r.PlaceId==place&&r.FromTick<to&&r.ToTick>from).OrderBy(r=>r.FromTick).ThenByDescending(r=>r.ToTick)){
                if(r.FromTick>cursor)break;
                if(r.ToTick<=cursor)continue;
                cursor=r.ToTick;ids.Add(r.Id);if(cursor>=to){used=ids.ToArray();return true;}
            }
            return false;
        }
        public static bool IsSnapshot(KnownRecord r)=>r!=null&&r.Direct&&r.Predicate=="AtPlace"&&(r.ProvenanceKey??"").StartsWith("PRESENCE_L",StringComparison.Ordinal);
        public static KnownRecord[] Compact(KnownRecord[] records,IEnumerable<string> preserve)
        {
            var keep=new HashSet<string>(preserve??Array.Empty<string>());
            foreach(var group in records.Where(IsSnapshot).GroupBy(r=>r.ProvenanceKey)){
                var newest=group.OrderByDescending(r=>r.ToTick).ThenByDescending(r=>r.IdentityConfirmed).ThenByDescending(r=>r.ReceivedTick).First();keep.Add(newest.Id);
            }
            return records.Where(r=>!IsSnapshot(r)||keep.Contains(r.Id)).ToArray();
        }
    }
    public static class EvidenceOrigins
    {
        public static string IndependenceKey(KnownRecord r)
        {
            string kind=string.IsNullOrEmpty(r.OriginKind)?r.Kind:r.OriginKind;
            if((kind=="Visual"||kind=="Touch")&&!PhysicalEvidenceScope.IsSurfaceState(r)){
                string observer=string.IsNullOrEmpty(r.OriginObserver)?(r.Direct?r.Source:""):r.OriginObserver;
                return string.IsNullOrEmpty(observer)?"UNKNOWN_WITNESS":"WITNESS_"+observer;
            }
            return string.IsNullOrEmpty(r.ProvenanceKey)?r.RootId:r.ProvenanceKey;
        }
    }
}
