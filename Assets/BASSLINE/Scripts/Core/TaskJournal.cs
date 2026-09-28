using System;
namespace BASSLINE.Core
{
    [Serializable] public sealed class TaskJournalSettings
    {
        public string PinnedId="";public bool Hidden;
        public TaskJournalSettings Copy()=>(TaskJournalSettings)MemberwiseClone();
    }
    public sealed class TaskJournalEntry
    {
        public string Id="",Title="",ActorId="",Status="",Instruction="",SourceText="",SourceRecordId="";
        public string LastItemPlace="",LastActorPlace="";public long ItemSeenTick=-1,ActorSeenTick=-1,StartedTick;
        public bool CanPin,Completed,Pinned;
    }
    public sealed class TaskJournalView
    {
        public TaskJournalEntry[] Entries=Array.Empty<TaskJournalEntry>();
        public TaskJournalEntry Pinned;public bool Hidden;
    }
    public interface IPlayerTaskJournalPort
    {
        TaskJournalView ReadTaskJournal();
        TaskJournalEntry ReadPinnedTask();
        string PinTask(string id);
        void HidePinnedTask();
        void ShowPinnedTask();
    }
}
