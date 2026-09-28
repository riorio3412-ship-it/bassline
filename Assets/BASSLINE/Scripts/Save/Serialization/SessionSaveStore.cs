using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Globalization;
using BASSLINE.World.Fixture;
using BASSLINE.Knowledge;
using BASSLINE.NPC;
using BASSLINE.Investigation;
using BASSLINE.Core;
namespace BASSLINE.Save
{
    [Serializable] public sealed class FixtureSessionSnapshot
    {
        public string Magic="BASSLINE_FIXTURE_SESSION";public int Schema=1;
        public LifeSnapshot World;public KnowledgeSnapshot Knowledge;public SocialSnapshot Social;public InvestigationSnapshot Investigation;
        public long ProcessedWorldEvent;
        // Unity's standalone JSON double parser can round the world accumulator's
        // last bit. Persist its IEEE-754 representation to preserve the next tick.
        public string ClockRemainderHex="0000000000000000";
        public PlayerUiSnapshot Ui=new PlayerUiSnapshot();
        public InspectionState Inspection=new InspectionState();
    }
    public sealed class SessionSaveStore
    {
        readonly Func<FixtureSessionSnapshot,string> encode;readonly Func<string,FixtureSessionSnapshot> decode;
        public SessionSaveStore(Func<FixtureSessionSnapshot,string> encode,Func<string,FixtureSessionSnapshot> decode){this.encode=encode;this.decode=decode;}
        public static void Validate(FixtureSessionSnapshot s)
        {
            if(s==null||s.Magic!="BASSLINE_FIXTURE_SESSION"||s.Schema!=1)throw new InvalidDataException("지원되지 않는 생활 세션 저장 버전입니다. 이전 파일은 보존됩니다.");
            LifeWorld.Validate(s.World);if(s.ProcessedWorldEvent<0||s.ProcessedWorldEvent>s.World.Sequence)throw new InvalidDataException("Invalid perception event cursor");
            DecodeClockRemainder(s);
            var knowledge=KnowledgeLedger.Restore(s.Knowledge,FixtureDefinition.Actors,s.World.Tick);
            SocialLedger.Restore(s.Social,FixtureDefinition.Actors,new[]{"K_H","K_W","K_L","K_G"},s.World.Tick);
            InvestigationNotebook.Restore(s.Investigation,knowledge.For);
            if(s.Ui==null||s.Ui.Pages==null||s.Ui.Pages.Length>32||s.Ui.Pages.Any(x=>x<2||x>41||x>=23&&x<=37)||!string.IsNullOrEmpty(s.Ui.SelectedRecordId)&&knowledge.For("CH_01").Find(s.Ui.SelectedRecordId)==null)throw new InvalidDataException("Invalid screen return stack");
            var i=s.Inspection;if(i==null||!new[]{"Idle","Running","Completed","Cancelled"}.Contains(i.State)||i.ElapsedTicks<0||i.DurationTicks<1||i.ElapsedTicks>i.DurationTicks||i.State=="Running"&&!s.World.Objects.Any(x=>x.Id==i.TargetId)&&!s.World.Doors.Any(x=>x.Id==i.TargetId)&&!s.World.Actors.Any(x=>x.Id==i.TargetId&&x.Incapacitated)&&!(s.World.Incident?.Enabled==true&&(i.TargetId=="K_V1"||i.TargetId=="K_P31"))||i.State=="Completed"&&knowledge.For("CH_01").Find(i.RecordId)==null)throw new InvalidDataException("Invalid investigation task");
        }
        public static double DecodeClockRemainder(FixtureSessionSnapshot s)
        {
            if(s?.World==null||s.ClockRemainderHex==null||s.ClockRemainderHex.Length!=16||!long.TryParse(s.ClockRemainderHex,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out long bits))throw new InvalidDataException("Invalid clock fraction encoding");
            double value=BitConverter.Int64BitsToDouble(bits);if(double.IsNaN(value)||double.IsInfinity(value)||value<0||double.IsNaN(s.World.PendingTime)||Math.Abs(value-s.World.PendingTime)>1e-12)throw new InvalidDataException("Clock fraction mismatch");return value;
        }
        public void Save(string path,FixtureSessionSnapshot snapshot,Action<SaveCheckpoint> fault=null)
        {
            Validate(snapshot);string payload=encode(snapshot),full=Path.GetFullPath(path),temp=full+".tmp";Directory.CreateDirectory(Path.GetDirectoryName(full));
            var bytes=Encoding.UTF8.GetBytes(AtomicSaveStore.Hash(payload)+"\n"+payload);
            using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}fault?.Invoke(SaveCheckpoint.AfterTempFlush);Load(temp);fault?.Invoke(SaveCheckpoint.BeforeReplace);
            if(File.Exists(full))File.Replace(temp,full,full+".previous");else File.Move(temp,full);fault?.Invoke(SaveCheckpoint.AfterReplace);
        }
        public FixtureSessionSnapshot Load(string path)
        {
            var value=File.ReadAllText(path,Encoding.UTF8);if(value.Length<65||value[64]!='\n'||AtomicSaveStore.Hash(value.Substring(65))!=value.Substring(0,64))throw new InvalidDataException("세션 checksum 불일치. 현재 상태를 변경하지 않았습니다.");
            var s=decode(value.Substring(65));Validate(s);s.World.PendingTime=DecodeClockRemainder(s);return s;
        }
    }
}



