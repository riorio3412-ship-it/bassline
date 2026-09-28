using System;
using System.Collections.Generic;
using System.Linq;
using BASSLINE.Bootstrap;
using BASSLINE.Core;
using BASSLINE.Trial;
using NUnit.Framework;
namespace BASSLINE.Tests.EditMode
{
    public sealed class ArchiveProjectionTests
    {
        static TrialEndgameSnapshot Completed(string caseId="CASE_1",string loop="LOOP_01",string chapter="CHAPTER_1")
            =>new TrialEndgameSnapshot{Phase="NextChapterReady",RewardId="REWARD_1",RewardAppliedExperience=80,Evaluation=new CaseEvaluation{Rank="B",Score=400},
                Plan=new SettlementPlan{Id="SETTLEMENT_"+caseId,CaseId=caseId,CampaignId="CAMPAIGN",LoopId=loop,ChapterId=chapter,PlayerId="CH_01",Target="CH_02",ActualCulprit="CH_02",Correct=true,Applied=true,TransitionApplied=true,Residual=16,Executed=new[]{"CH_02"},ConfirmedDead=new[]{"CH_03"}},
                Reveal=new[]{new RevealEvent{Id="REVEAL_1",CaseId=caseId,Heading="확정된 사건",Text="공개 승인된 결과",PlaceId="R_HALL",Tick=60,RevealApproved=true}}};
        static ArchiveSnapshot Read(params TrialEndgameSnapshot[] entries)=>ArchiveViewProjection.Create("CH_01","CAMPAIGN","LOOP_02",3,entries,id=>"이름 "+id,id=>"장소 "+id);

        [Test] public void Archive_RejectsOtherOwnerCampaignUnfinishedAndFutureCases()
        {
            Assert.That(Read().Cases,Is.Empty);
            var otherOwner=Completed("OTHER_OWNER");otherOwner.Plan.PlayerId="CH_02";
            var otherCampaign=Completed("OTHER_CAMPAIGN");otherCampaign.Plan.CampaignId="OTHER";
            var unapplied=Completed("UNAPPLIED");unapplied.Plan.Applied=false;
            var pending=Completed("PENDING");pending.Plan.TransitionApplied=false;
            var revealedOnly=Completed("REVEALED_ONLY");revealedOnly.Phase="Truth";
            var unrewarded=Completed("UNREWARDED");unrewarded.RewardId="";
            var current=Completed("CURRENT","LOOP_02","CHAPTER_3");var futureChapter=Completed("FUTURE_CHAPTER","LOOP_02","CHAPTER_4");
            var futureLoop=Completed("FUTURE_LOOP","LOOP_03");var malformed=Completed("MALFORMED","LOOP_bad");
            Assert.That(Read(otherOwner,otherCampaign,unapplied,pending,revealedOnly,unrewarded,current,futureChapter,futureLoop,malformed).Cases,Is.Empty);
            Assert.That(ArchiveViewProjection.Create("CH_01","CAMPAIGN","",1,new[]{Completed()}).Cases,Is.Empty);
        }
        [Test] public void Archive_OnlyMatchingApprovedUnprotectedRevealAndNoMutableDomainReferences()
        {
            var source=Completed();source.Reveal=source.Reveal.Concat(new[]{
                new RevealEvent{Id="PRIVATE",CaseId="CASE_1",Heading="非公開",Text="보호된 다른 사람의 기록",RevealApproved=true,Protected=true},
                new RevealEvent{Id="UNAPPROVED",CaseId="CASE_1",Text="미승인 정보"},
                new RevealEvent{Id="SEALED_CASE",CaseId="OTHER_CASE",Text="봉인된 다른 사건",RevealApproved=true}}).ToArray();
            var view=Read(source);var entry=view.Cases.Single();Assert.That(view.Owner,Is.EqualTo("CH_01"));Assert.That(view.Scope,Is.EqualTo("ArchiveMeta"));
            Assert.That(entry.Records.Select(r=>r.Id),Is.EqualTo(new[]{"REVEAL_1"}));Assert.That(entry.Records[0].Place,Is.EqualTo("장소 R_HALL"));
            source.Reveal[0].Text="나중 변조";source.Plan.Executed[0]="CH_04";source.Evaluation.Score=0;
            Assert.That(entry.Records[0].Text,Is.EqualTo("공개 승인된 결과"));Assert.That(entry.Executed.Single(),Is.EqualTo("이름 CH_02"));Assert.That(entry.Score,Is.EqualTo(400));
            Assert.Throws<NotSupportedException>(()=>((IList<ArchiveCaseView>)view.Cases).Clear());
            Assert.Throws<NotSupportedException>(()=>((IList<ArchiveRecordView>)entry.Records).Clear());
            Assert.That(typeof(ArchiveCaseView).GetProperties().Any(p=>p.PropertyType==typeof(SettlementPlan)||p.PropertyType==typeof(TrialEndgameSnapshot)),Is.False);
        }
        [Test] public void Archive_GroupsCompletedSameLoopPriorChaptersAndOlderLoopsWithoutDuplicateCases()
        {
            var old=Completed("OLD","LOOP_01","CHAPTER_9");var prior=Completed("PRIOR","LOOP_02","CHAPTER_1");var recent=Completed("RECENT","LOOP_02","CHAPTER_2");
            var data=Read(old,prior,recent,recent.Copy());Assert.That(data.Cases.Select(c=>c.CaseId),Is.EqualTo(new[]{"RECENT","PRIOR","OLD"}));
            Assert.That(data.Cases.Select(c=>c.IsCurrentLoop),Is.EqualTo(new[]{true,true,false}));Assert.That(data.Cases.First().ExperienceAwarded,Is.EqualTo(80));
            Assert.That(data.Cases.Last().ChapterId,Is.EqualTo("CHAPTER_9"));Assert.That(data.CurrentLoopId,Is.EqualTo("LOOP_02"));
        }
        [Test] public void Archive_PresentationStateCopyKeepsEvidenceAndTargetSelectionsIndependent()
        {
            var state=new PlayerUiSnapshot{Pages=new[]{7,13},SelectedRecordId="B_RECEIPT",SelectedTarget="CH_02",SelectedEvidence=new[]{"B_RECEIPT"},
                ArchiveLoopId="LOOP_01",ArchiveCaseId="LOOP_01/CHAPTER_1/CASE_1",ArchiveCompareId="LOOP_01/CHAPTER_2/CASE_2",ArchiveRecordPage=2};
            var copy=state.Copy();copy.ArchiveCaseId="OTHER_META_CASE";copy.SelectedEvidence[0]="OTHER_B_RECEIPT";
            Assert.That(state.SelectedRecordId,Is.EqualTo("B_RECEIPT"));Assert.That(state.SelectedTarget,Is.EqualTo("CH_02"));Assert.That(state.SelectedEvidence,Is.EqualTo(new[]{"B_RECEIPT"}));
            Assert.That(copy.ArchiveLoopId,Is.EqualTo(state.ArchiveLoopId));Assert.That(copy.ArchiveCompareId,Is.EqualTo(state.ArchiveCompareId));Assert.That(copy.ArchiveRecordPage,Is.EqualTo(2));
        }
    }
}
