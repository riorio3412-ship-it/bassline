using System;
using System.Linq;
using BASSLINE.NPC;
using NUnit.Framework;
namespace BASSLINE.Tests
{
    public sealed class AppointmentRevisionTests
    {
        readonly string[] actors={"CH_01","CH_02","CH_03"};readonly string[] places={"OLD_ROOM","NEW_ROOM"};
        SocialLedger Ledger()=>new SocialLedger(actors,places);
        string Agreed(SocialLedger ledger,long at=600)
        {
            var id=ledger.Propose("CH_01","CH_02","OLD_ROOM",at,120,0);ledger.RecordSpokenProposal(id,1,"OFFER_1",0);ledger.RecordSpokenReply(id,1,"REPLY_1",10,true);return id;
        }
        [Test]public void PrivateAmendmentDoesNotChangeTheUninformedInviteesTimePlaceOrHistory()
        {
            var s=Ledger();string id=Agreed(s);s.Propose("CH_01","CH_02","NEW_ROOM",900,120,60,id);
            var host=s.For("CH_01",60).Single();var guest=s.For("CH_02",60).Single();Assert.That(host.Revision,Is.EqualTo(2));Assert.That(host.PlaceId,Is.EqualTo("NEW_ROOM"));
            Assert.That(guest.Revision,Is.EqualTo(1));Assert.That(guest.State,Is.EqualTo("Agreed"));Assert.That(guest.PlaceId,Is.EqualTo("OLD_ROOM"));Assert.That(s.HistoryFor("CH_02",60).Length,Is.EqualTo(1));Assert.That(s.For("CH_03",60),Is.Empty);
            Assert.That(s.Due("CH_02",600).PlaceId,Is.EqualTo("OLD_ROOM"));Assert.That(s.Accept(id,2,"CH_02",true),Is.EqualTo("AccessDenied"));
            var restored=SocialLedger.Restore(s.Capture(),actors,places,60);Assert.That(restored.HistoryFor("CH_02",60).Length,Is.EqualTo(1));Assert.That(restored.Due("CH_02",600).PlaceId,Is.EqualTo("OLD_ROOM"));
        }
        [Test]public void LateDeliveryRetainsTheOriginalAndRecordsItsActualReceiptTime()
        {
            var s=Ledger();string id=Agreed(s);s.Propose("CH_01","CH_02","NEW_ROOM",900,120,60,id);
            Assert.That(s.Receive(id,2,"CH_02",59,"NEW_LETTER"),Is.EqualTo("AccessDenied"));Assert.That(s.Receive(id,2,"CH_02",180,"NEW_LETTER"),Is.EqualTo("Received"));
            Assert.That(s.For("CH_02",180).Single().ReceivedTick,Is.EqualTo(180));Assert.That(s.For("CH_02",180).Single().SourceRootId,Is.EqualTo("NEW_LETTER"));
            Assert.That(s.For("CH_02",180).Single().State,Is.EqualTo("Proposed"));Assert.That(s.Due("CH_02",180),Is.Null);
            s.Receive(id,2,"CH_02",240,"NEW_LETTER");Assert.That(s.For("CH_02",240).Single().ReceivedTick,Is.EqualTo(180));
            var history=s.HistoryFor("CH_02",240);Assert.That(history.Length,Is.EqualTo(2));Assert.That(history.Single(a=>a.Revision==1).ReceivedTick,Is.EqualTo(0));Assert.That(history.Single(a=>a.Revision==1).ConfirmationTick,Is.EqualTo(10));
            Assert.That(SocialLedger.Restore(s.Capture(),actors,places,240).For("CH_02",240).Single().ReceivedTick,Is.EqualTo(180));
        }
        [Test]public void EarlierMeetingDoesNotCompleteAChangedAppointmentOrRewardTheSameEpisodeTwice()
        {
            var s=Ledger();string id=Agreed(s);s.Meet(id,"CH_02","CH_01",600);Assert.That(s.For("CH_02",600).Single().State,Is.EqualTo("Met"));
            s.Propose("CH_01","CH_02","NEW_ROOM",900,120,620,id);s.RecordSpokenProposal(id,2,"OFFER_2",620);s.RecordSpokenReply(id,2,"REPLY_2",630,true);
            Assert.That(s.For("CH_02",630).Single().State,Is.EqualTo("Agreed"));Assert.That(s.Due("CH_02",900).Revision,Is.EqualTo(2));s.Meet(id,"CH_02","CH_01",900);
            Assert.That(s.HistoryFor("CH_02",900).All(a=>a.State=="Met"),Is.True);Assert.That(s.Experiences("CH_02").Length,Is.EqualTo(1));
            var restored=SocialLedger.Restore(s.Capture(),actors,places,900);Assert.That(restored.HistoryFor("CH_02",900).All(a=>a.State=="Met"),Is.True);
        }
        [Test]public void LegacyCompletionAppliesOnlyToTheVersionTheParticipantHadReceived()
        {
            var s=Ledger();string id=Agreed(s);s.Propose("CH_01","CH_02","NEW_ROOM",900,120,620,id);var save=s.Capture();save.Completed=new[]{id+"_CH_02"};
            var restored=SocialLedger.Restore(save,actors,places,650);Assert.That(restored.For("CH_02",650).Single().State,Is.EqualTo("Met"));
            restored.RecordSpokenProposal(id,2,"OFFER_2",660);restored.RecordSpokenReply(id,2,"REPLY_2",670,true);Assert.That(restored.For("CH_02",670).Single().State,Is.EqualTo("Agreed"));
        }
        [Test]public void MeetingOneselfCannotCompleteAnAppointmentAndFutureReceiptIsRejectedOnLoad()
        {
            var s=Ledger();string id=Agreed(s);s.Meet(id,"CH_02","CH_02",600);Assert.That(s.For("CH_02",600).Single().State,Is.EqualTo("Agreed"));
            var save=s.Capture();save.Appointments[0].Receipts[0].Tick=700;Assert.Throws<ArgumentException>(()=>SocialLedger.Restore(save,actors,places,600));
            save=s.Capture();save.Appointments[0].Receipts[0]=null;Assert.Throws<ArgumentException>(()=>SocialLedger.Restore(save,actors,places,600));
        }
    }
}
