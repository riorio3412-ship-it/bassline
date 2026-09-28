using System;
namespace BASSLINE.Core
{
    [Serializable] public sealed class NpcCounterProgress
    {
        public string Id="",ActorId="",ClaimId="",SpanId="",SpeechId="",Action="",RuleId="",RequestId="",Phase="Queued",BasisKey="";
        public string[] RecordIds=Array.Empty<string>();public long QueuedTick;
        public NpcCounterProgress Copy(){var c=(NpcCounterProgress)MemberwiseClone();c.RecordIds=(string[])RecordIds.Clone();return c;}
    }
}
