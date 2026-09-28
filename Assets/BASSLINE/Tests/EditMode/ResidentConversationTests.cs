using System;
using System.Linq;
using System.IO;
using NUnit.Framework;
using BASSLINE.Core;
using BASSLINE.Knowledge;
using BASSLINE.NPC;
public sealed class ResidentConversationTests
{
    static readonly string[] Actors=Enumerable.Range(1,18).Select(i=>"CH_"+i.ToString("00")).ToArray();
    static SocialExperience Experience(string id,string owner="CH_04",string other="CH_01",string kind="PromiseKept",string reason=null)=>new SocialExperience{Id=id,Owner=owner,Other=other,Kind=kind,Reason=reason??id};
    [Test] public void All17NpcVoicesHaveOriginalSourceAndDistinctEverydayLines()
    {
        var r=new ResidentConversation();var profiles=r.Profiles();var k=new KnowledgeLedger(Actors);
        Assert.That(profiles.Select(p=>p.ActorId),Is.EquivalentTo(Actors.Skip(1)));Assert.That(profiles.Select(p=>p.Greeting).Distinct().Count(),Is.EqualTo(17));Assert.That(profiles.Select(p=>p.BusyGreeting).Distinct().Count(),Is.EqualTo(17));
        for(int i=0;i<17;i++){Assert.That(profiles[i].SourceParagraph,Is.EqualTo("SRC11_P"+(771+23*i).ToString("0000")));Assert.That(profiles[i].WritingSourceParagraph,Is.EqualTo("SRC11_P"+(773+23*i).ToString("0000")));Assert.That(r.Greeting(profiles[i].ActorId,"Talk",k.For(profiles[i].ActorId)),Is.EqualTo(profiles[i].Greeting));}
        Assert.That(profiles.Single(p=>p.ActorId=="CH_04").Greeting,Does.Contain("민혁 씨"));Assert.That(profiles.Single(p=>p.ActorId=="CH_15").Greeting,Does.Contain("직접 보신"));Assert.That(profiles.Single(p=>p.ActorId=="CH_17").Greeting,Does.Contain("오늘의 질문"));
    }
    [Test] public void PreferredPlacesAreExistingMansionRooms()
    {
        var rooms=File.ReadAllLines("SourcePackage/07_ASSET_MANIFEST/ROOM_MANIFEST.csv").Skip(1).Select(line=>line.Split(',')[0]).ToArray();Assert.That(new ResidentConversation().Profiles().SelectMany(p=>p.PreferredPlaces).Except(rooms),Is.Empty);
    }
    [Test] public void GreetingNeverVolunteersOwnOrOtherPrivateKnowledge()
    {
        var r=new ResidentConversation();var k=new KnowledgeLedger(Actors);string before=r.Greeting("CH_04","Rest",k.For("CH_04"));
        foreach(string owner in new[]{"CH_04","CH_02"})k.Observe(owner,new KnownRecord{Kind="Visual",SubjectId="CH_03",Predicate="AtPlace",Value="SECRET_PLACE",Text="SECRET_PRIVATE_CONTRACT_ABC",FromTick=1,ToTick=2,IdentityConfirmed=true},1);
        Assert.That(r.Greeting("CH_04","Rest",k.For("CH_04")),Is.EqualTo(before));Assert.That(r.Profiles().SelectMany(p=>new[]{p.Greeting,p.BusyGreeting,p.ReadingGreeting,p.RestGreeting,p.Accepted,p.Negotiating,p.Declined}).Any(x=>x.Contains("SECRET_PRIVATE")||x.Contains("범행 기록")||x.Contains("불법 장부")||x.Contains("연쇄 살인")||x.Contains("계약")),Is.False);
        Assert.Throws<ArgumentException>(()=>r.Greeting("CH_04","Talk",k.For("CH_02")));Assert.Throws<ArgumentException>(()=>r.Greeting("CH_01","Talk",k.For("CH_01")));
    }
    [Test] public void ActualActivitySelectsGreetingWithoutPretendingTaskAlreadyCompleted()
    {
        var r=new ResidentConversation();var k=new KnowledgeLedger(Actors);foreach(string actor in Actors.Skip(1)){
            var p=r.Profiles().Single(v=>v.ActorId==actor);Assert.That(r.Greeting(actor,"Repair",k.For(actor)),Is.EqualTo(p.BusyGreeting));Assert.That(r.Greeting(actor,"Read",k.For(actor)),Is.EqualTo(p.ReadingGreeting));Assert.That(r.Greeting(actor,"Rest",k.For(actor)),Is.EqualTo(p.RestGreeting));Assert.That(r.Greeting(actor,"UNKNOWN",k.For(actor)),Is.EqualTo(p.Greeting));}
    }
    [Test] public void KnownScheduleConflictOffersNegotiationEvenWithPositiveHistory()
    {
        var r=new ResidentConversation();foreach(string actor in Actors.Skip(1)){
            var p=r.Profiles().Single(v=>v.ActorId==actor);var history=Enumerable.Range(0,10).Select(i=>Experience("SOC_"+i,actor)).ToArray();var d=r.EvaluateInvitation(actor,p.PreferredActivities[0],"R_DINING",history,true);
            Assert.That(d.Accepted,Is.False);Assert.That(d.Outcome,Is.EqualTo("CounterOffer"));Assert.That(d.SuggestedDelayTicks,Is.GreaterThan(0));Assert.That(d.SuggestedPlace,Is.EqualTo("R_DINING"));Assert.That(d.PolicyStatus,Is.EqualTo("PRODUCTION_PROPOSAL"));}
    }
    [Test] public void InvitationsAreNotAlwaysAcceptedAndOwnBoundaryExperienceMatters()
    {
        var r=new ResidentConversation();var accepted=r.EvaluateInvitation("CH_04","Tea","R_DINING",Array.Empty<SocialExperience>(),false);Assert.That(accepted.Accepted,Is.True);
        var uncertain=r.EvaluateInvitation("CH_06","Game","R_GREEN",Array.Empty<SocialExperience>(),false);Assert.That(uncertain.Outcome,Is.EqualTo("CounterOffer"));Assert.That(uncertain.SuggestedActivity,Is.EqualTo("Organize"));
        var negative=Enumerable.Range(1,3).Select(i=>Experience("SOC_"+i,kind:"BoundaryViolation")).ToArray();var declined=r.EvaluateInvitation("CH_04","Tea","R_DINING",negative,false);Assert.That(declined.Outcome,Is.EqualTo("Declined"));Assert.That(declined.Accepted,Is.False);Assert.That(declined.BasisExperienceIds.Length,Is.EqualTo(3));
        foreach(var e in negative)e.Owner="CH_02";Assert.That(r.EvaluateInvitation("CH_04","Tea","R_DINING",negative,false).Outcome,Is.EqualTo("Accepted"));foreach(var e in negative){e.Owner="CH_04";e.Other="CH_03";}Assert.That(r.EvaluateInvitation("CH_04","Tea","R_DINING",negative,false).Outcome,Is.EqualTo("Accepted"));
    }
    [Test] public void DuplicateExperienceDoesNotMultiplyAndProposalPolicyCanChange()
    {
        var r=new ResidentConversation();var negative=Enumerable.Range(1,5).Select(i=>Experience("SOC_"+i,kind:"BoundaryViolation",reason:"SAME_EVENT")).ToArray();var policy=new InvitationPolicy{AcceptThreshold=3};var d=r.EvaluateInvitation("CH_04","Tea","R_DINING",negative,false,policy);Assert.That(d.BasisExperienceIds.Length,Is.EqualTo(1));Assert.That(d.Outcome,Is.EqualTo("CounterOffer"));policy.AcceptThreshold=2;Assert.That(r.EvaluateInvitation("CH_04","Tea","R_DINING",negative,false,policy).Outcome,Is.EqualTo("Accepted"));Assert.That(negative.All(e=>e.Reason=="SAME_EVENT"),Is.True);
    }
    [Test] public void ProfileAndNegotiationReturnValuesCannotMutateSharedVoiceTable()
    {
        var r=new ResidentConversation();var copy=r.Profiles();copy[0].PreferredActivities[0]="FAKE";copy[0].Greeting="FAKE";Assert.That(r.Profiles()[0].Greeting,Is.Not.EqualTo("FAKE"));Assert.That(r.Profiles()[0].PreferredActivities,Does.Not.Contain("FAKE"));Assert.That(r.EvaluateInvitation("CH_02","","",null,false).Outcome,Is.EqualTo("Declined"));Assert.Throws<ArgumentException>(()=>r.EvaluateInvitation("CH_02","Talk","R_HALL",null,false,new InvitationPolicy{AcceptThreshold=-3,DeclineThreshold=0}));
    }
}

