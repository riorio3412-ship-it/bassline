using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.Knowledge;
using BASSLINE.NPC;
using BASSLINE.Investigation;
using BASSLINE.Save;
using BASSLINE.World.Fixture;
public sealed class KnowledgeLogicTests
{
    [SetUp] public void Metadata()=>TestContext.Progress.WriteLine("checkpoint03 TestOnly; Editor="+Application.unityVersion+"; URP=17.6.0; UTF=1.8.0; seed=0; FixtureK_Life_004; FixtureSession schema1; revision="+File.ReadAllText("Verification/build-revision.txt"));
    static KnownRecord Observation(string value="K_H",long from=100,long to=101)=>new KnownRecord{Kind="Visual",Predicate="AtPlace",SubjectId="CH_04",Value=value,PlaceId=value,Text="관측",Source="CH_01",IdentityConfirmed=true,FromTick=from,ToTick=to,Supports=new[]{"관측 구간의 위치"},DoesNotEstablish=new[]{"관측 밖 위치"}};
    static ClaimRecord Claim(string value="K_H",long from=100,long to=101)=>new ClaimRecord{Id="CL_TEST",OwnerId="CH_01",LoopId="K_LOOP_01",Spans=new[]{new ClaimSpan{Id="SPAN_LOCATION",Predicate="AtPlace",SubjectId="CH_04",Value=value,FromTick=from,ToTick=to},new ClaimSpan{Id="SPAN_OTHER",Predicate="HeldObject",SubjectId="CH_04",Value="K_BOOK_01",FromTick=100,ToTick=101}}};
    [Test] public void AT_INFO_02_ReceiptIsPrivate_ForwardedCopiesShareOneRoot()
    {
        var k=new KnowledgeLedger(FixtureDefinition.Actors);string id=k.Observe("CH_01",Observation(),100);Assert.That(k.For("CH_02").Records(),Is.Empty);
        string copy=k.Deliver("CH_01","CH_02",id,101);Assert.That(k.For("CH_03").Records(),Is.Empty);string forwarded=k.Deliver("CH_02","CH_03",copy,102);
        Assert.That(k.For("CH_03").Find(forwarded).RootId,Is.EqualTo(id));Assert.That(k.For("CH_03").Find(forwarded).Direct,Is.False);
        Assert.That(k.Deliver("CH_01","CH_03",id,103),Is.EqualTo(forwarded));Assert.That(k.For("CH_03").Records().Length,Is.EqualTo(1));Assert.That(k.For("CH_02").Find(id),Is.Null);
        var mutable=k.For("CH_01").Find(id);mutable.Value="secret mutation";Assert.That(k.For("CH_01").Find(id).Value,Is.EqualTo("K_H"));
        var bad=k.Capture();bad.Memories[2].Record.Parents=new[]{forwarded};Assert.Throws<ArgumentException>(()=>KnowledgeLedger.Restore(bad,FixtureDefinition.Actors,103));
    }
    [Test] public void AT_UI_04_LastConfirmedDoesNotReadHiddenMovement()
    {
        var k=new KnowledgeLedger(FixtureDefinition.Actors);k.Observe("CH_01",Observation(),100);var saved=k.Capture();
        var hidden=new LifeWorld();var state=hidden.Capture();state.Actors.Single(a=>a.Id=="CH_04").Position=FixtureDefinition.Nodes["K_G"];
        Assert.That(k.LastConfirmed("CH_01").Single().PlaceId,Is.EqualTo("K_H"));Assert.That(JsonUtility.ToJson(k.Capture()),Is.EqualTo(JsonUtility.ToJson(saved)));
    }
    [Test] public void P3_AppointmentRevisionAndAcceptanceRequireReceipts()
    {
        var s=new SocialLedger(FixtureDefinition.Actors,new[]{"K_H","K_W"});var id=s.Propose("CH_01","CH_02","K_H",600,120,0);
        Assert.That(s.For("CH_02",0),Is.Empty);Assert.That(s.Accept(id,1,"CH_02",true),Is.EqualTo("AccessDenied"));s.Receive(id,1,"CH_02");s.Accept(id,1,"CH_02",true);
        Assert.That(s.For("CH_01",0).Single().State,Is.EqualTo("Proposed"));s.ReceiveAcceptance(id,1,"CH_01");Assert.That(s.For("CH_01",0).Single().State,Is.EqualTo("Agreed"));
        s.Propose("CH_01","CH_02","K_W",900,120,10,id);Assert.That(s.For("CH_02",10).Single().Revision,Is.EqualTo(1));Assert.That(s.For("CH_02",10).Single().PlaceId,Is.EqualTo("K_H"));
        Assert.That(JsonUtility.ToJson(SocialLedger.Restore(s.Capture(),FixtureDefinition.Actors,new[]{"K_H","K_W"},10).Capture()),Is.EqualTo(JsonUtility.ToJson(s.Capture())));
    }
    [Test] public void AT_INFO_03_LogicUsesOnlyKnownScopeAndPreservesOtherSpans()
    {
        var k=new KnowledgeLedger(FixtureDefinition.Actors);var id=k.Observe("CH_01",Observation(),100);var resolver=new LogicResolver();
        var result=resolver.Resolve(k.For("CH_01"),"REQ_01","LR01",Claim(),"SPAN_LOCATION",new[]{id});Assert.That(result.ResultType,Is.EqualTo("Support"));Assert.That(result.AReadCount,Is.Zero);
        result=resolver.Resolve(k.For("CH_01"),"REQ_02","LR01",Claim("K_H",90,110),"SPAN_LOCATION",new[]{id});Assert.That(result.ResultType,Is.EqualTo("LimitScope"));Assert.That(result.UnsupportedSpanIds,Is.EqualTo(new[]{"SPAN_LOCATION"}));
        result=resolver.Resolve(k.For("CH_01"),"REQ_03","LR03",Claim("K_W"),"SPAN_LOCATION",new[]{id});Assert.That(result.ResultType,Is.EqualTo("Contradict"));Assert.That(result.UnsupportedSpanIds,Does.Not.Contain("SPAN_OTHER"));
        result=resolver.Resolve(k.For("CH_02"),"REQ_04","LR01",Claim(),"SPAN_LOCATION",new[]{id});Assert.That(result.ReasonCode,Is.EqualTo("AccessDenied"));
        var copy=k.Deliver("CH_01","CH_02",id,101);result=resolver.Resolve(k.For("CH_02"),"REQ_05","LR10",Claim(),"SPAN_LOCATION",new[]{copy});Assert.That(result.ResultType,Is.EqualTo("Conditional"));
    }
    [Test] public void AT_CASE_04_SameRootAndLaterLockAreNotNewProof()
    {
        var k=new KnowledgeLedger(FixtureDefinition.Actors);var obs=Observation();obs.Predicate="DoorState";obs.SubjectId="K_DOOR_S";obs.Value="Locked";var id=k.Observe("CH_01",obs,100);
        var claim=Claim();claim.Spans[0].Predicate="DoorState";claim.Spans[0].SubjectId="K_DOOR_S";claim.Spans[0].Value="Locked";claim.Spans[0].FromTick=10;claim.Spans[0].ToTick=20;
        var result=new LogicResolver().Resolve(k.For("CH_01"),"REQ_06","LR08",claim,"SPAN_LOCATION",new[]{id});Assert.That(result.ResultType,Is.EqualTo("LimitScope"));
        result=new LogicResolver().Resolve(k.For("CH_01"),"REQ_07","LR09",claim,"SPAN_LOCATION",new[]{id,id});Assert.That(result.ReasonCode,Is.EqualTo("SameRoot"));
    }
    [Test] public void P6_NotebookGraphRejectsCyclesAndUnknownReferences()
    {
        var k=new KnowledgeLedger(FixtureDefinition.Actors);string a=k.Observe("CH_01",Observation(),100),b=k.Observe("CH_01",Observation(),100);var n=new InvestigationNotebook();
        Assert.That(n.Link(k.For("CH_01"),a,b),Is.EqualTo("Committed"));Assert.That(n.Link(k.For("CH_01"),b,a),Is.EqualTo("Cycle"));Assert.That(n.CreateHypothesis(k.For("CH_02"),"추정",new[]{a}),Is.EqualTo("Unavailable"));
        string h=n.CreateHypothesis(k.For("CH_01"),"관측 이후에는 이동했을 수 있다",new[]{a});Assert.That(n.SetStatus("CH_01",h,"Held"),Is.EqualTo("Committed"));
        Assert.That(JsonUtility.ToJson(InvestigationNotebook.Restore(n.Capture(),k.For).Capture()),Is.EqualTo(JsonUtility.ToJson(n.Capture())));
    }
    [Test] public void AT_SAVE_02_ClockFractionSurvivesStandaloneJsonRounding()
    {
        const double fraction=.014712739953150419;var w=new LifeWorld{PendingTime=fraction};long bits=BitConverter.DoubleToInt64Bits(fraction);
        var s=new FixtureSessionSnapshot{World=w.Capture(),ClockRemainderHex=bits.ToString("X16"),Knowledge=new KnowledgeLedger(FixtureDefinition.Actors).Capture(),Social=new SocialLedger(FixtureDefinition.Actors,new[]{"K_H","K_W","K_L","K_G"}).Capture(),Investigation=new InvestigationNotebook().Capture()};
        // Reproduce the one-bit decimal parser change observed in the Mono player.
        var store=new SessionSaveStore(x=>JsonUtility.ToJson(x),text=>{var decoded=JsonUtility.FromJson<FixtureSessionSnapshot>(text);decoded.World.PendingTime=BitConverter.Int64BitsToDouble(bits+1);return decoded;});
        string path=Path.Combine(Path.GetTempPath(),"bassline_clock_"+Guid.NewGuid().ToString("N")+".dat");
        try{store.Save(path,s);Assert.That(BitConverter.DoubleToInt64Bits(store.Load(path).World.PendingTime),Is.EqualTo(bits));s.ClockRemainderHex="7FF8000000000000";Assert.Throws<InvalidDataException>(()=>SessionSaveStore.Validate(s));}finally{foreach(var suffix in new[]{"",".tmp",".previous"})if(File.Exists(path+suffix))File.Delete(path+suffix);}
    }
}
