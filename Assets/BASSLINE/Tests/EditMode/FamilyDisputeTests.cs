using System;
using System.Linq;
using NUnit.Framework;
using BASSLINE.Core;
using BASSLINE.Knowledge;
using BASSLINE.NPC;
namespace BASSLINE.Tests
{
    public sealed class FamilyDisputeTests
    {
        KnowledgeLedger Ledger()=>new KnowledgeLedger(new[]{"CH_01","CH_03","CH_05","CH_07"});
        string Say(KnowledgeLedger k,string source,string value,long tick,bool full=true)=>k.Observe(source,new KnownRecord{Kind="Speech",Source=source,SubjectId=source,Predicate=full?"SaidStatement":"HeardFragment",Value=FamilyDisputePlanner.Prefix+value,FromTick=tick,ToTick=tick+1,ProvenanceKey="SPEECH_"+tick},tick);
        IncidentPlanState Plan(KnowledgeLedger k,long tick=10,IncidentPlanState prior=null,SocialExperience[] e=null)=>FamilyDisputePlanner.Consider(k.For("CH_03"),e??Array.Empty<SocialExperience>(),tick,37,prior);
        [Test]public void AnotherPersonsSpeechAndPartialHearingDoNotChooseAnAction()
        {
            var k=Ledger();var full=Say(k,"CH_05","Settlement",1);Assert.That(Plan(k),Is.Null);
            k.Deliver("CH_05","CH_03",Say(k,"CH_05","Settlement",2,false),3);Assert.That(Plan(k),Is.Null);
            k.Deliver("CH_05","CH_03",full,4);Assert.That(Plan(k).Action,Is.EqualTo("Negotiate"));
            Assert.That(FamilyDisputePlanner.KnowsTopic(k.For("CH_01"),10),Is.False);
        }
        [Test]public void OnlyReceivedInterventionChangesChoiceAndTimeCannotReroll()
        {
            var k=Ledger();k.Deliver("CH_05","CH_03",Say(k,"CH_05","Settlement",1),2);var first=Plan(k);
            var offer=Say(k,"CH_01","Mediate",11);Assert.That(Plan(k,12,first),Is.Null);
            k.Deliver("CH_01","CH_03",offer,13);var help=Plan(k,14,first);Assert.That(help.Action,Is.EqualTo("AskForHelp"));Assert.That(help.TargetId,Is.EqualTo("CH_01"));
            Assert.That(Plan(k,999,help),Is.Null);IncidentPlanIntegrity.Validate(help,k.For("CH_03"),14);
        }
        [Test]public void DistanceAndDisclosureExecuteDifferentNonviolentChoices()
        {
            var k=Ledger();k.Deliver("CH_05","CH_03",Say(k,"CH_05","Settlement",1),2);
            k.Deliver("CH_01","CH_03",Say(k,"CH_01","Distance",3),4);Assert.That(Plan(k).Action,Is.EqualTo("Withdraw"));
            k.Observe("CH_03",new KnownRecord{SubjectId="CH_07",Predicate="AtPlace",IdentityConfirmed=true,FromTick=5,ToTick=6},5);
            k.Deliver("CH_01","CH_03",Say(k,"CH_01","Disclose",6),7);
            var p=Plan(k,e:new[]{new SocialExperience{Owner="CH_03",Other="CH_07",Tick=5,Trust=2}});
            Assert.That(p.Action,Is.EqualTo("Disclose"));Assert.That(p.Alternatives.Single(a=>a.Action=="Execute").Eligible,Is.False);
        }
        [Test]public void OtherPersonsTrustCannotInventAHelper()
        {
            var k=Ledger();k.Deliver("CH_05","CH_03",Say(k,"CH_05","Settlement",1),2);
            k.Observe("CH_03",new KnownRecord{SubjectId="CH_07",Predicate="AtPlace",IdentityConfirmed=true,FromTick=3,ToTick=4},3);
            var p=Plan(k,e:new[]{new SocialExperience{Owner="CH_05",Other="CH_07",Tick=3,Trust=5}});
            Assert.That(p.Alternatives.Single(a=>a.Action=="AskForHelp").Eligible,Is.False);Assert.That(p.Alternatives.Length,Is.EqualTo(5));
        }
    }
}
