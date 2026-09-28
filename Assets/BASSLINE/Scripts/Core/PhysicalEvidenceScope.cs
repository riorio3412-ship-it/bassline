namespace BASSLINE.Core
{
    public static class PhysicalEvidenceScope
    {
        public static bool IsSurfaceState(string predicate)=>predicate=="SurfacePattern"||predicate=="ContactPattern"||predicate=="SurfaceResidue";
        public static bool IsSurfaceState(KnownRecord record)=>record!=null&&IsSurfaceState(record.Predicate);
        public static bool IsDoorObservation(string predicate)=>predicate=="DoorState"||predicate=="DoorMotion"||predicate=="DoorAttempt";
        public static bool IsObjectState(string predicate)=>IsSurfaceState(predicate)||IsDoorObservation(predicate);
        public static bool IsObjectState(KnownRecord record)=>record!=null&&IsObjectState(record.Predicate);
        // A surface's identity is not the identity of whoever made its mark.
        public static string ObservationSubject(KnownRecord record)=>IsObjectState(record)?record.SubjectId:record.IdentityConfirmed?record.SubjectId:"UNKNOWN_ACTOR";
    }
}
