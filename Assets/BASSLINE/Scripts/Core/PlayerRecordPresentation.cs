using System;
using System.Linq;
namespace BASSLINE.Core
{
    // Presentation only: retain original IDs/times and never merge gaps into a new fact.
    public static class PlayerRecordPresentation
    {
        public static KnownRecord[] Compact(KnownRecord[] records)
        {
            return records.Where(r=>r!=null&&r.Kind!="ArchiveMeta"&&!RetiredSurveillance.IsRecord(r))
                .GroupBy(r=>string.Join("|",r.LoopId,r.Source,r.SubjectId,r.Predicate,r.Value,r.PlaceId,r.Direct,r.IdentityConfirmed,
                    string.IsNullOrEmpty(r.ActivationId)?"":r.ActivationId+"/"+r.CausalStage,
                    r.Predicate=="AtPlace"||PhysicalEvidenceScope.IsDoorObservation(r.Predicate)?"":r.Text))
                .Select(g=>g.OrderByDescending(r=>r.ToTick).ThenByDescending(r=>r.ReceivedTick).First())
                .OrderByDescending(Importance).ThenByDescending(r=>r.ReceivedTick).ToArray();
        }
        static int Importance(KnownRecord r)=>!string.IsNullOrEmpty(r.ActivationId)||r.CausalStage=="PhysicalStrike"||r.Predicate=="CausedOutcome"||r.Kind=="OfficialReport"||r.Value=="Collapsed"?4:
            r.Kind=="Document"||r.Predicate=="SurfaceResidue"||r.Predicate=="RinsedObject"||r.Predicate=="ContactPattern"||r.Predicate=="SurfacePattern"?3:
            r.Kind=="Statement"||r.Predicate=="SaidStatement"||r.Predicate=="HandedObject"||r.Predicate=="SurfaceObjectTransfer"?2:
            r.Predicate=="AtPlace"?0:1;
        public static string ComparisonRule(string predicate,string quantifier,long toTick,KnownRecord[] records)
        {
            if(string.IsNullOrEmpty(predicate))return "LR01";
            if(predicate=="CausedOutcome")return "LR07";
            if(predicate=="AtPlace")return "LR03";
            if(predicate=="UsedObject"&&records.Any(r=>r.Predicate=="HeldObject"))return "LR04";
            if(records.Any(r=>r.Kind=="DeviceLog")&&quantifier!="Particular")return "LR02";
            if(quantifier!="Particular")return "LR06";
            if(records.Length>0&&records.All(r=>!r.Direct))return "LR05";
            if(records.Any(r=>r.Direct&&r.Predicate==predicate&&r.FromTick>=toTick))return "LR08";
            return "LR01";
        }
    }
}
