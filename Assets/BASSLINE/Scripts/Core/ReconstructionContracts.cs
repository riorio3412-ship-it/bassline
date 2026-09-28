using System;
namespace BASSLINE.Core
{
    public sealed class ReconstructionEntryView
    {
        public string Id,Group,Role,ActorId,Text,PlaceId,Status;public long FromTick,ToTick;public bool Assumption;
        public string[] RecordIds=Array.Empty<string>();
    }
    public sealed class ReconstructionView
    {
        public string Phase="Editing",AccusedId="",Assessment="InsufficientInformation",DefenseSpeaker="",DefenseText="";
        public int Revision;public bool IncompleteAccepted,Published,CanVote;
        public ReconstructionEntryView[] Entries=Array.Empty<ReconstructionEntryView>();
        public string[] Issues=Array.Empty<string>(),Candidates=Array.Empty<string>();
    }
    public interface IPlayerCausalReconstructionPort
    {
        string ReadCausalConnection(string causeRecordId);
        string AddCausalConnection(string causeRecordId,string group);
    }
    public interface IPlayerReconstructionPort
    {
        ReconstructionView ReadReconstruction();
        string AddReconstructionEvidence(string recordId,string group);
        string ChangeReconstructionRole(string entryId,string role);
        string SetReconstructionAssumption(string entryId,bool assumption);
        string RemoveReconstructionEntry(string entryId);
        string SetReconstructionTarget(string actorId);
        string PublishReconstruction();
        string ReviseReconstruction();
        string ProceedFromReconstruction(bool acceptUnresolved);
    }
}
