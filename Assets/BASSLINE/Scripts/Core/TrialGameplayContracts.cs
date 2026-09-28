using System;
namespace BASSLINE.Core
{
    public sealed class TrialSpanView { public string Id,Text,State; }
    public sealed class TrialClaimView { public string Id,SpeakerId,Text; public TrialSpanView[] Spans=Array.Empty<TrialSpanView>(); }
    public sealed class TrialTallyView { public string ActorId; public int Count; }
    public sealed class TrialTruthView { public string Id,Heading,Text,PlaceId;public long Tick;public bool HasRecordedPath; }
    /// <summary>Only public court receipts and post-verdict archive material. Never authoritative case plans or NPC private memory.</summary>
    public sealed class PlayerTrialView
    {
        public string Phase="NotStarted",Topic="",SpeakerId="",SpokenText="",Message="",FocusClaimId="",FocusSpanId="",Verdict="",Rank="",Transition="";
        public bool Focused,CanContinue,CanVote,Observer,TruthAvailable,SettlementApplied;
        public long CourtTick;public int VoteRound=1,TruthCursor,Score,ExperienceAwarded,Level=1,Experience,SkillPoints,Residual;
        public string[] Participants=Array.Empty<string>(),VoteCandidates=Array.Empty<string>(),AnnouncedTopics=Array.Empty<string>(),History=Array.Empty<string>(),Executed=Array.Empty<string>(),Escaped=Array.Empty<string>();
        public TrialClaimView[] Claims=Array.Empty<TrialClaimView>();public TrialTallyView[] Tallies=Array.Empty<TrialTallyView>();public TrialTruthView[] Truth=Array.Empty<TrialTruthView>();
    }
    public interface IPlayerTrialPort
    {
        PlayerTrialView ReadTrial();
        string EnterTrialFocus(string claimId,string spanId);
        string CancelTrialFocus();
        string SubmitTrial(string action,string ruleId,string[] recordIds);
        KnownRecord[] CheckTrialSource(string[] recordIds);
        string RequestTrialTestimony(string witnessId,string question);
        string RetireTrialClaim(string claimId);
        string OpenTrialVoting();
        string CastTrialVote(string actorId);
        string ContinueTrial();
    }
}
