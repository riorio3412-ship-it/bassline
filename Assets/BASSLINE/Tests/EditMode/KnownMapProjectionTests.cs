using System;
using System.Linq;
using NUnit.Framework;
using BASSLINE.Core;
using BASSLINE.Knowledge;
namespace BASSLINE.Tests.EditMode
{
    public sealed class KnownMapProjectionTests
    {
        static string Add(KnowledgeLedger ledger,string owner,string subject,string predicate,string value,string place,string text,long tick,double x=1,double z=2,string version="M01",bool identity=false)
            =>ledger.Observe(owner,new KnownRecord{Kind="Visual",SubjectId=subject,Predicate=predicate,Value=value,PlaceId=place,Text=text,Source=owner,ProvenanceKey=version==null?"":"MAP|"+version,FromTick=tick,ToTick=tick+1,Position=new Point3(x,0,z),IdentityConfirmed=identity},tick);
        static KnownMapSnapshot Read(KnowledgeLedger ledger)=>KnownMapProjection.Create(ledger.For("CH_01"),"M01",id=>"확인한 인물");

        [Test] public void M01_MapEmptyAndOtherOwnersMemoryCannotPublishRooms()
        {
            var ledger=new KnowledgeLedger(new[]{"CH_01","CH_02"},"LOOP_01");Assert.That(Read(ledger).Places,Is.Empty);
            Add(ledger,"CH_02","R_SECRET","MapAreaSeen","F2","R_SECRET","본 공간",10);
            string name=Add(ledger,"CH_02","R_SECRET","MapNameRead","F2","R_SECRET","다른 사람만 읽은 이름",11);
            Assert.That(Read(ledger).Places,Is.Empty);ledger.Deliver("CH_02","CH_01",name,12);
            Assert.That(Read(ledger).Places,Is.Empty,"A received statement is not a directly surveyed room or published floor plan");
            Assert.That(Read(ledger).HasPublishedPlan,Is.False);
        }
        [Test] public void M01_MapNamesRequireReadSignsAndConnectionsRequireTraversedReceipt()
        {
            var ledger=new KnowledgeLedger(new[]{"CH_01","CH_02"},"LOOP_01");
            Add(ledger,"CH_01","R_HALL","MapAreaSeen","F1","R_HALL","내부 표면",10,2,4);
            var anonymous=Read(ledger);Assert.That(anonymous.Places.Single().Label,Does.StartWith("확인한 공간"));Assert.That(anonymous.Places.Single().Visited,Is.True);Assert.That(anonymous.Connections,Is.Empty);
            Add(ledger,"CH_01","R_HALL","MapNameRead","F1","R_HALL","중앙 홀",20,5,6);
            Add(ledger,"CH_01","R_LIBRARY","MapNameRead","F1","R_LIBRARY","도서관",21,20,6);
            var named=Read(ledger);Assert.That(named.Places.Single(p=>p.Id=="R_HALL").Label,Is.EqualTo("중앙 홀"));Assert.That(named.Places.Single(p=>p.Id=="R_HALL").X,Is.EqualTo(2),"A later sign cannot replace an observed interior point");Assert.That(named.Places.Single(p=>p.Id=="R_LIBRARY").Visited,Is.False);Assert.That(named.Connections,Is.Empty);
            Add(ledger,"CH_01","R_LIBRARY","MapAreaSeen","F1","R_LIBRARY","내부 표면",30,21,5);
            Add(ledger,"CH_01","R_HALL","MapPassageUsed","R_LIBRARY","R_LIBRARY","통과",31);
            Assert.That(Read(ledger).Connections.Single().From,Is.EqualTo("R_HALL"));Assert.That(anonymous.Places.Single().Label,Does.StartWith("확인한 공간"),"An immutable previously returned view cannot update itself");
        }
        [Test] public void M01_MapDoorAndPersonStayAtRecordedStateAcrossLaterTimeAndRestore()
        {
            var ledger=new KnowledgeLedger(new[]{"CH_01","CH_02"},"LOOP_01");
            Add(ledger,"CH_01","R_HALL","MapAreaSeen","F1","R_HALL","표면",10);
            string door=Add(ledger,"CH_01","D_001","MapDoorSeen","F1","R_HALL","문",11,3,4);
            Assert.That(Read(ledger).Doors.Single().State,Is.EqualTo("Uninspected"));
            string state=Add(ledger,"CH_01","D_001","DoorState","Closed","R_HALL","직접 조사",12,3,4,null);
            string person=Add(ledger,"CH_01","CH_02","AtPlace","R_HALL","R_HALL","목격",13,7,8,null,true);
            Add(ledger,"CH_02","D_001","DoorState","Open","R_HALL","플레이어 미수신",20,3,4,null);
            string other=Add(ledger,"CH_02","CH_02","AtPlace","R_LIBRARY","R_LIBRARY","전언 위치",21,77,88,null,true);ledger.Deliver("CH_02","CH_01",other,22);
            var map=Read(ledger);Assert.That(map.Doors.Single().State,Is.EqualTo("Closed"));Assert.That(map.Doors.Single().ReceiptId,Is.EqualTo(state));Assert.That(map.People.Single().X,Is.EqualTo(7));Assert.That(map.People.Single().Tick,Is.EqualTo(13));Assert.That(map.People.Single().ReceiptId,Is.EqualTo(person));
            var restored=KnowledgeLedger.Restore(ledger.Capture(),new[]{"CH_01","CH_02"},1000);var after=Read(restored);Assert.That(after.Doors.Single().State,Is.EqualTo("Closed"));Assert.That(after.People.Single().X,Is.EqualTo(7));Assert.That(after.People.Single().Tick,Is.EqualTo(13));
        }
        [Test] public void M01_MapOlderVersionCannotMergeWithCurrentRoomOrLiveDoorInspection()
        {
            var ledger=new KnowledgeLedger(new[]{"CH_01"},"LOOP_01");
            Add(ledger,"CH_01","R_HALL","MapAreaSeen","F1","R_HALL","옛 공간",10,2,4,"M00");
            Add(ledger,"CH_01","D_001","MapDoorSeen","F1","R_HALL","옛 문",11,3,4,"M00");
            Add(ledger,"CH_01","R_HALL","MapAreaSeen","F1","R_HALL","지금 공간",20,12,14);
            Add(ledger,"CH_01","D_001","DoorState","Open","R_HALL","지금 조사",21,13,14,null);
            var map=Read(ledger);Assert.That(map.Places.Count,Is.EqualTo(2));Assert.That(map.Places.Single(p=>p.Version=="M00").X,Is.EqualTo(2));Assert.That(map.Places.Single(p=>p.Version=="M01").X,Is.EqualTo(12));Assert.That(map.Doors.Single().State,Is.EqualTo("Uninspected"));
        }
    }
}
