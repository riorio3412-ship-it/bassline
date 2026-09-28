using System;
using BASSLINE.Core;
using BASSLINE.Knowledge;
using BASSLINE.NPC;
using NUnit.Framework;
namespace BASSLINE.Tests
{
    public sealed class ResidentMeetingPlannerTests
    {
        KnowledgeLedger Ledger()=>new KnowledgeLedger(new[]{"CH_01","CH_02","CH_03","CH_07"});
        void Seen(KnowledgeLedger k,string owner,string other)=>k.Observe(owner,new KnownRecord{Kind="Visual",SubjectId=other,Predicate="AtPlace",PlaceId="R_HALL",IdentityConfirmed=true,FromTick=0,ToTick=1},0);
        ResidentMeetingIntent Choose(KnowledgeLedger k,int completed=2,AppointmentView[] appointments=null)=>new ResidentMeetingPlanner().Choose("CH_02","R_HALL",completed,30,k.For("CH_02"),appointments??Array.Empty<AppointmentView>(),Array.Empty<SocialExperience>(),new[]{"CH_01","CH_03","CH_07"});
        [Test]public void OnlyPersonallySeenNearbyResidentsCanBeInvited()
        {
            var k=Ledger();Seen(k,"CH_01","CH_03");Assert.That(Choose(k),Is.Null);
            Seen(k,"CH_02","CH_07");var p=Choose(k);Assert.That(p.Invitee,Is.EqualTo("CH_07"));Assert.That(p.StartTick%3600,Is.Zero);
            Assert.Throws<ArgumentException>(()=>new ResidentMeetingPlanner().Choose("CH_02","R_HALL",2,30,k.For("CH_01"),Array.Empty<AppointmentView>(),Array.Empty<SocialExperience>(),new[]{"CH_07"}));
        }
        [Test]public void LeisureProgressAndPersonallyPendingMeetingsControlNewProposals()
        {
            var k=Ledger();Seen(k,"CH_02","CH_07");Assert.That(Choose(k,1),Is.Null);
            Assert.That(Choose(k,2,new[]{new AppointmentView{Id="OWN",State="Proposed",StartTick=900,Duration=6000}}),Is.Null);
            Assert.That(Choose(k,2,new[]{new AppointmentView{Id="OWN",State="Declined",StartTick=900,Duration=6000}}),Is.Not.Null);
        }
        [Test]public void OtherPeoplesExperienceDoesNotChooseThePartner()
        {
            var k=Ledger();Seen(k,"CH_02","CH_03");Seen(k,"CH_02","CH_07");
            var p=new ResidentMeetingPlanner().Choose("CH_02","R_HALL",2,30,k.For("CH_02"),Array.Empty<AppointmentView>(),new[]{new SocialExperience{Owner="CH_01",Other="CH_07",Trust=5}},new[]{"CH_03","CH_07"});
            Assert.That(p.Invitee,Is.EqualTo("CH_03"));
        }
    }
}
