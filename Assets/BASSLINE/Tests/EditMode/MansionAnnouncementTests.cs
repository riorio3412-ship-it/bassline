using System;
using System.Linq;
using NUnit.Framework;
using BASSLINE.Bootstrap;
using BASSLINE.Core;
using BASSLINE.Knowledge;

namespace BASSLINE.Tests.EditMode
{
    public sealed class MansionAnnouncementTests
    {
        const string Notice="OFFICIAL_L1_C1_100";
        [Test] public void BroadcastDoesNotReachSleepingAbsentDeadSilentOrOutOfRangeResidents()
        {
            Assert.That(MansionAnnouncementPolicy.CanReceive(true,true,"Read",false,true),Is.True);
            Assert.That(MansionAnnouncementPolicy.CanReceive(true,true,"Sleep",false,true),Is.False);
            Assert.That(MansionAnnouncementPolicy.CanReceive(true,true,"Sleeping",false,true),Is.False);
            Assert.That(MansionAnnouncementPolicy.CanReceive(true,false,"Read",false,true),Is.False);
            Assert.That(MansionAnnouncementPolicy.CanReceive(false,true,"Read",false,true),Is.False);
            Assert.That(MansionAnnouncementPolicy.CanReceive(true,true,"Read",true,true),Is.False);
            Assert.That(MansionAnnouncementPolicy.CanReceive(true,true,"Read",false,false),Is.False);
        }
        [Test] public void AnnouncementScheduleIsNotAReceivedSummonsForDistantActor()
        {
            var ledger=new KnowledgeLedger(new[]{"CH_01","CH_02"},"LOOP_01");
            ledger.Observe("CH_01",MansionAnnouncementPolicy.Notice(Notice,"CourtSchedule","공개 예정 시각","R_HALL",default,100),100);
            Assert.That(MansionAnnouncementPolicy.HasReceipt(ledger.For("CH_01"),Notice,"CourtSchedule"),Is.True);
            Assert.That(MansionAnnouncementPolicy.HasReceipt(ledger.For("CH_01"),Notice,"CourtSummons"),Is.False);
            Assert.That(MansionAnnouncementPolicy.HasReceipt(ledger.For("CH_02"),Notice,"CourtSchedule"),Is.False);
            Assert.That(ledger.For("CH_02").Records(),Is.Empty);
        }
        [Test] public void PersonalSummonsPersistsWithoutDuplicateReceptionOrNewIndependentSource()
        {
            var ledger=new KnowledgeLedger(new[]{"CH_01","CH_02"},"LOOP_01");
            foreach(string actor in new[]{"CH_01","CH_02"})ledger.Observe(actor,MansionAnnouncementPolicy.Notice(Notice,"CourtSummons","재판장으로 와 주세요.","R_HALL",default,180),180);
            var restored=KnowledgeLedger.Restore(ledger.Capture(),new[]{"CH_01","CH_02"},200);
            if(!MansionAnnouncementPolicy.HasReceipt(restored.For("CH_01"),Notice,"CourtSummons"))restored.Observe("CH_01",MansionAnnouncementPolicy.Notice(Notice,"CourtSummons","재공지","R_HALL",default,200),200);
            Assert.That(restored.For("CH_01").Records().Length,Is.EqualTo(1));
            Assert.That(restored.For("CH_01").Records()[0].ReceivedTick,Is.EqualTo(180));
            Assert.That(restored.For("CH_01").Records()[0].ProvenanceKey,Is.EqualTo(restored.For("CH_02").Records()[0].ProvenanceKey));
            Assert.That(MansionAnnouncementPolicy.HasReceipt(restored.For("CH_01"),"OFFICIAL_L2_C1_100","CourtSummons"),Is.False);
        }
        [Test] public void CarrierRoundUsesPublicStopsAndResumesAfterRecordedDestination()
        {
            string[] authored={"R_BED_01","R_BED_COR","R_HALL","R_BED_18","R_ANNOUNCE"};
            var publicStops=authored.Where(MansionAnnouncementPolicy.IsPublicSearchRoom).ToArray();
            Assert.That(publicStops,Is.EqualTo(new[]{"R_BED_COR","R_HALL","R_ANNOUNCE"}));
            Assert.That(MansionAnnouncementPolicy.NextStop(publicStops,null),Is.EqualTo("R_BED_COR"));
            string persistedDestination="R_HALL";
            Assert.That(MansionAnnouncementPolicy.NextStop(publicStops,persistedDestination),Is.EqualTo("R_ANNOUNCE"));
            Assert.That(MansionAnnouncementPolicy.NextStop(publicStops,"R_ANNOUNCE"),Is.EqualTo("R_BED_COR"));
            Assert.That(MansionAnnouncementPolicy.NextStop(Array.Empty<string>(),"R_HALL"),Is.Empty);
        }
        [Test] public void AnnouncementContainsOnlyGivenPublicSpeechAndDoesNotBecomeDeathObservation()
        {
            var record=MansionAnnouncementPolicy.Notice(Notice,"CourtSummons","재판장으로 와 주세요.","R_HALL",new Point3(1,0,2),200);
            Assert.That(record.Source,Is.EqualTo("PRES_YUSTI"));
            Assert.That(record.Kind,Is.EqualTo("OfficialAnnouncement"));
            Assert.That(record.SubjectId,Is.EqualTo("PRES_YUSTI"));
            Assert.That(record.Text,Is.EqualTo("재판장으로 와 주세요."));
            Assert.That(record.Predicate,Is.Not.EqualTo("CausedOutcome"));
            Assert.That(record.DoesNotEstablish.Any(s=>s.Contains("원인")),Is.True);
        }
    }
}
