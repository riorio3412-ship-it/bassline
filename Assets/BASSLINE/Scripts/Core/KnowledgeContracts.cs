using System;
using System.Linq;
namespace BASSLINE.Core
{
    [Serializable] public sealed class KnownRecord
    {
        public string OriginKind="",OriginObserver="";
        public string ProvenanceKey="",ConversationWith="";public string Id, RootId, LoopId="K_LOOP_01", Kind, SubjectId, Predicate, Value, PlaceId, Text, Source;
        public long FromTick, ToTick, ReceivedTick;
        public string ActivationId="",DeviceId="",OutcomeTarget="",ActionDefinition="",ActionRevision="",CausalStage="";
        public bool IdentityConfirmed, Direct;
        public Point3 Position;
        public string[] Parents=Array.Empty<string>(), Supports=Array.Empty<string>(), DoesNotEstablish=Array.Empty<string>();
        public KnownRecord Copy()=>new KnownRecord {OriginKind=OriginKind??"",OriginObserver=OriginObserver??"",ActivationId=ActivationId??"",DeviceId=DeviceId??"",OutcomeTarget=OutcomeTarget??"",ActionDefinition=ActionDefinition??"",ActionRevision=ActionRevision??"",CausalStage=CausalStage??"",ProvenanceKey=ProvenanceKey,ConversationWith=ConversationWith??"",Id=Id,RootId=RootId,LoopId=LoopId,Kind=Kind,SubjectId=SubjectId,Predicate=Predicate,Value=Value,PlaceId=PlaceId,Text=Text,Source=Source,FromTick=FromTick,ToTick=ToTick,ReceivedTick=ReceivedTick,IdentityConfirmed=IdentityConfirmed,Direct=Direct,Position=Position,Parents=(string[])Parents.Clone(),Supports=(string[])Supports.Clone(),DoesNotEstablish=(string[])DoesNotEstablish.Clone()};
    }
    public interface IActorKnowledgeQuery
    {
        string OwnerId {get;}
        string LoopId {get;}
        long Revision {get;}
        KnownRecord[] Records();
        KnownRecord Find(string id);
    }
    public sealed class KnownLocationView {public string ActorId,PlaceId,SourceRecordId;public long Tick;public Point3 Position;}
    [Serializable] public sealed class AppointmentView
    {
        public string Id,Organizer,Invitee,PlaceId,State;public long StartTick,Duration;public int Revision;public bool Accepted;
        public long ReceivedTick=-1,ConfirmationTick=-1;public string SourceRootId="";
        public AppointmentView Copy()=>(AppointmentView)MemberwiseClone();
    }
    public sealed class InteractionView {public string TargetId,Label,PrimaryAction;public bool Available;}
    [Serializable] public sealed class PlayerUiSnapshot
    {
        public int[] Pages=Array.Empty<int>();public string SelectedRecordId="",SelectedTarget="";
        public bool TaskNotebook,TaskHistory;public string SelectedTaskId="";
        public string SelectedClaimId="",SelectedSpanId="";public string[] SelectedEvidence=Array.Empty<string>();public int TrialAction,TrialRule;
        public string ReconstructionPanel="Entries",ReconstructionGroup="A",ReconstructionEntry="",ReconstructionRecord="";public int ReconstructionTarget;
        public string ArchiveLoopId="",ArchiveCaseId="",ArchiveCompareId="";public int ArchiveRecordPage;
        public PlayerUiSnapshot Copy()=>new PlayerUiSnapshot{TaskNotebook=TaskNotebook,TaskHistory=TaskHistory,SelectedTaskId=SelectedTaskId??"",ReconstructionPanel=ReconstructionPanel??"Entries",ReconstructionGroup=ReconstructionGroup??"A",ReconstructionEntry=ReconstructionEntry??"",ReconstructionRecord=ReconstructionRecord??"",ReconstructionTarget=ReconstructionTarget,Pages=(int[])Pages.Clone(),SelectedRecordId=SelectedRecordId,SelectedTarget=SelectedTarget,SelectedClaimId=SelectedClaimId,SelectedSpanId=SelectedSpanId,SelectedEvidence=(string[])SelectedEvidence.Clone(),TrialAction=TrialAction,TrialRule=TrialRule,ArchiveLoopId=ArchiveLoopId??"",ArchiveCaseId=ArchiveCaseId??"",ArchiveCompareId=ArchiveCompareId??"",ArchiveRecordPage=Math.Max(0,ArchiveRecordPage)};
    }
    public sealed class HypothesisView {public string Id,Text,Status;public string[] References;}
    [Serializable] public sealed class InspectionState
    {
        public string TargetId="",State="Idle",RecordId="";public int ElapsedTicks,DurationTicks=120;
        // The mansion pins a visible feature for the entire reading interval.
        // Fixture inspections may leave these fields at their defaults.
        public string FocusMode="Target",FocusRoot="",FocusValue="";
        public long StartedTick=-1,LastTick=-1;
        public InspectionState Copy()=>(InspectionState)MemberwiseClone();
    }
    public sealed class NotebookView
    {
        public long Revision;public KnownRecord[] Records=Array.Empty<KnownRecord>();
        public KnownLocationView[] Locations=Array.Empty<KnownLocationView>();
        public AppointmentView[] Appointments=Array.Empty<AppointmentView>();
        public string[] RelationshipExperiences=Array.Empty<string>();
        public HypothesisView[] Hypotheses=Array.Empty<HypothesisView>();
    }
    public interface IPlayerNotebookPort
    {
        NotebookView ReadNotebook();
        string Examine(string targetId);
        string Talk(string actorId);
        string Share(string actorId,string recordId);
        string Invite(string actorId,string placeId,long delayTicks);
        string RespondToInvitation(string appointmentId,bool accept);
        string Assess(string claimPredicate,string subjectId,string value,long from,long to,string[] evidenceIds);
        InteractionView DescribeTarget(string targetId);
        PlayerUiSnapshot ReadUiState();
        void StoreUiState(PlayerUiSnapshot state);
        string Hypothesize(string recordId);
        string SetHypothesisStatus(string id,string status);
        InspectionState ReadInspection();
        void CancelInspection();
    }
}

