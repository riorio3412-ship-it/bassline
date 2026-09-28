using System;
namespace BASSLINE.Core
{
    [Serializable] public sealed class NpcTrialSpan
    {
        public string Id,Predicate,SubjectId,Value,PlaceId,Quantifier="Particular";public long FromTick,ToTick;
    }
    [Serializable] public sealed class NpcTrialSpeech
    {
        public string Id,SpeakerId,Topic,Text;public string[] RecordIds=Array.Empty<string>();public NpcTrialSpan[] Spans=Array.Empty<NpcTrialSpan>();
    }
    [Serializable] public sealed class NpcHeardClaim
    {
        public string Id,ReceiverId,SpeakerId,LoopId,Review="Asserted";public NpcTrialSpan Span;public string[] RootGroups=Array.Empty<string>();
    }
    [Serializable] public sealed class NpcTrialResponse
    {
        public string Action,RuleId,ClaimId,SpanId,Explanation;public string[] RecordIds=Array.Empty<string>();
    }
    [Serializable] public sealed class NpcVoteDecision
    {
        public string VoterId,Choice,Explanation;public bool Uncertain;public uint NextRandomState;public int DrawCount;
        public string[] Basis=Array.Empty<string>(),EquallySupportedCandidates=Array.Empty<string>();
    }
}
