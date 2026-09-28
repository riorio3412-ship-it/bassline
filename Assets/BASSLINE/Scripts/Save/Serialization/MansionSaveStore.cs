using System;
using System.IO;
using System.Text;
using BASSLINE.Core;
using BASSLINE.World.Mansion;
using BASSLINE.Knowledge;
using BASSLINE.NPC;
using BASSLINE.Investigation;
namespace BASSLINE.Save
{
    [Serializable] public sealed class MansionSessionSnapshot
    {
        public int Schema=2;public string Magic="BASSLINE_MANSION_SESSION";
        public int OptionalObjects;public string[] SpeechesWithoutClaim=Array.Empty<string>();
        public MansionState World;public KnowledgeSnapshot Knowledge;public SocialSnapshot Social;public InvestigationSnapshot Notebook;
        public WaitingState Waiting=new WaitingState();
        public ConversationPlaybackState Conversation=new ConversationPlaybackState();
        public ConversationPlaybackState[] ResidentConversations=Array.Empty<ConversationPlaybackState>();
        public ResidentResponseState[] ResidentResponses=Array.Empty<ResidentResponseState>();
        public FamilyDisputeState FamilyDispute=new FamilyDisputeState{Version=0};
        public ResidentIntentSnapshot ResidentIntents=new ResidentIntentSnapshot();
        public ItemExchangeSnapshot ItemExchange=new ItemExchangeSnapshot();
        public DoorObservationState DoorObservations=new DoorObservationState();
        public PresenceObservationState PresenceObservations=new PresenceObservationState();
        public WeaponResidueSnapshot WeaponResidues=new WeaponResidueSnapshot{Version=0};
        public WeaponRinseState WeaponRinse=new WeaponRinseState();
        public WeaponMotionState WeaponMotion=new WeaponMotionState();
        public ToolPressState ToolPress=new ToolPressState();
        public ResidentToolWork[] ResidentToolWork=Array.Empty<ResidentToolWork>();
        public ResidentToolPickup[] ResidentToolPickups=Array.Empty<ResidentToolPickup>();
        public ResidentToolReturn[] ResidentToolReturns=Array.Empty<ResidentToolReturn>();
        public ResidentReading[] ResidentReadings=Array.Empty<ResidentReading>();
        public SurfaceReadingProgress[] SurfaceReadings=Array.Empty<SurfaceReadingProgress>();
        public SurfaceTraceSnapshot SurfaceTraces=new SurfaceTraceSnapshot();
        public MansionRecordingSnapshot Recordings=new MansionRecordingSnapshot();
        public MansionActionJournalSnapshot ActionJournal=new MansionActionJournalSnapshot();
        public MansionIncidentSnapshot Incident;public MansionProceedings Proceedings; public InspectionState Inspection;public PlayerUiSnapshot Ui;public string ClockRemainderHex;
        // Version 0 files contain one Incident. Version 1 owns the collection and checks the old first-case mirror.
        public int CaseCollectionVersion;
        public MansionIncidentSnapshot[] Incidents=Array.Empty<MansionIncidentSnapshot>();
    }
    public sealed class MansionSaveStore
    {
        readonly Func<MansionSessionSnapshot,string> encode;readonly Func<string,MansionSessionSnapshot> decode;readonly Action<MansionSessionSnapshot> validate;
        public MansionSaveStore(Func<MansionSessionSnapshot,string> encoder,Func<string,MansionSessionSnapshot> decoder,Action<MansionSessionSnapshot> validator){encode=encoder;decode=decoder;validate=validator;}
        public void Save(string path,MansionSessionSnapshot snapshot)
        {
            validate(snapshot);string payload=encode(snapshot),temporary=path+".tmp";Directory.CreateDirectory(Path.GetDirectoryName(path));
            byte[] bytes=Encoding.UTF8.GetBytes(AtomicSaveStore.Hash(payload)+"\n"+payload);
            using(var file=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
            Load(temporary);if(File.Exists(path))File.Replace(temporary,path,path+".previous");else File.Move(temporary,path);
        }
        public MansionSessionSnapshot Load(string path)
        {
            string text=File.ReadAllText(path,Encoding.UTF8);if(text.Length<65||text[64]!='\n'||AtomicSaveStore.Hash(text.Substring(65))!=text.Substring(0,64))throw new InvalidDataException("저장 파일 무결성을 확인할 수 없습니다. 이전 상태를 보존합니다.");
            var snapshot=decode(text.Substring(65));validate(snapshot);return snapshot;
        }
    }
}
