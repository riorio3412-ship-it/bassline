using System;
namespace BASSLINE.Core
{
    public interface IPlayerSmallTalkPort { string SmallTalk(string actorId,int topic); }
    public sealed class SaveSlotView { public int Slot; public bool Exists; public string Description; }
    public interface IPlayerSaveLibraryPort
    {
        SaveSlotView[] ReadSaveSlots();
        string SaveManual(int slot);
        string LoadManual(int slot);
    }
    public interface IPlayerWelcomePort
    {
        bool WelcomeRead {get;}
        void ReadWelcome();
    }
    public static class PersonalRecordFilter
    {
        public static bool Includes(KnownRecord r,int category)
        {
            if(r==null)return false;
            bool dialogue=r.Predicate=="SaidStatement"||r.Predicate=="HeardFragment";
            bool official=r.Kind=="Document"||r.Kind=="OfficialAnnouncement"||r.Source=="PRES_YUSTI";
            bool map=(r.ProvenanceKey??"").StartsWith("MAP|",StringComparison.Ordinal);
            return category==0&&!map||category==1&&!dialogue&&!official&&!map||category==2&&dialogue||category==3&&official;
        }
    }
}
