using System.Linq;
using NUnit.Framework;
using BASSLINE.Core;
using BASSLINE.Knowledge;
using BASSLINE.NPC;
public sealed class IncidentConversationTests
{
    [Test] public void SeeingHearingAndIgnoranceStayDifferentAfterDenial()
    {
        var k=new KnowledgeLedger(new[]{"CH_01","CH_02","CH_03","CH_04"});
        string id=k.Observe("CH_03",new KnownRecord{Kind="Visual",Source="CH_03",SubjectId="CH_01",Predicate="UsedObject",Value="KNIFE",CausalStage="PhysicalStrike",OutcomeTarget="CH_02",IdentityConfirmed=true,Text="공격을 보았다.",FromTick=10,ToTick=11},10);
        Assert.That(IncidentConversation.Greeting(k.For("CH_04"),0),Is.Empty);
        k.Deliver("CH_03","CH_04",id,11);
        Assert.That(IncidentConversation.SawPlayerStrike(k.For("CH_03"),0),Is.True);Assert.That(IncidentConversation.SawPlayerStrike(k.For("CH_04"),0),Is.False);
        Assert.That(IncidentConversation.Explain(IncidentConversation.Select(k.For("CH_04"),0),s=>s,true),Does.Contain("직접 본 건 아니야"));
        k.Observe("CH_01",new KnownRecord{Kind="Speech",Source="CH_01",SubjectId="CH_01",Predicate="SaidStatement",Value="IncidentPosition:Deny",Text="내가 공격한 게 아니야.",FromTick=12,ToTick=13},12);
        Assert.That(IncidentConversation.Reply(k.For("CH_03"),0,false),Does.Contain("직접 봤어"));Assert.That(k.For("CH_03").Find(id),Is.Not.Null);
        Assert.That(IncidentConversation.SawPlayerStrike(k.For("CH_03"),20),Is.False);
    }
    [Test] public void UnidentifiedAttackDoesNotNameHiddenPeopleAndImportantEventBeatsRoutineSight()
    {
        var k=new KnowledgeLedger(new[]{"CH_01","CH_03"});
        k.Observe("CH_03",new KnownRecord{Kind="Visual",SubjectId="UNKNOWN_ACTOR",Source="CH_03",Predicate="UsedObject",Value="KNIFE",CausalStage="PhysicalStrike",IdentityConfirmed=false,FromTick=1,ToTick=2,Text="누군가를 보았다."},1);
        k.Observe("CH_03",new KnownRecord{Kind="Visual",SubjectId="TABLE",Source="CH_03",Predicate="AtPlace",Value="ROOM_A",IdentityConfirmed=true,FromTick=3,ToTick=4,Text="책상을 보았다."},3);
        var selected=IncidentConversation.Select(k.For("CH_03"),0);Assert.That(selected.Predicate,Is.EqualTo("UsedObject"));
        string text=IncidentConversation.Explain(selected,s=>s=="KNIFE"?"칼":"HIDDEN_NAME",false);Assert.That(text,Does.Not.Contain("HIDDEN_NAME"));
        Assert.That(IncidentConversation.SawPlayerStrike(k.For("CH_03"),0),Is.False);
    }
}
