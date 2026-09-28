using System;
namespace BASSLINE.Core
{
    public static class RetiredSurveillance
    {
        public static bool IsRecord(KnownRecord r)=>r!=null&&(r.Kind=="Video"||r.Predicate=="RecordingCoverage"||r.Source=="M_WORK_CAMERA"||r.Source=="M_WORK_RECORDINGS"||(r.ProvenanceKey??"").Contains("_STREAM_L")||(r.ProvenanceKey??"").StartsWith("M_WORK_CAMERA_NOTICE_L",StringComparison.Ordinal));
    }
}
