using System;
using System.Linq;
using NUnit.Framework;
using BASSLINE.Core;
using BASSLINE.Knowledge;
using BASSLINE.Investigation;
using BASSLINE.Trial;

public sealed class TrialEndgameTests
{
    static string[] Actors(int n)=>Enumerable.Range(1,n).Select(i=>"CH_"+i.ToString("00")).ToArray();
    static TrialDirector Court(int n=5,string target="CH_04",string rule="Basic")
    {
        var c=new TrialDirector();Assert.That(c.Start("TRIAL_01","LOOP_01",Actors(n),_=>true),Is.EqualTo("Started"));
        c.ConfigureVoting(rule,41);c.OpenVoting();foreach(var a in Actors(n))c.ChooseVote(a,target);c.LockVotes();c.ResolveVoting();return c;
    }
    static VerdictInput Input(int n=5)=>new VerdictInput{CaseId="CASE_01",CampaignId="CAMPAIGN_01",ChapterId="CHAPTER_01",ActualCulprit="CH_04",LivingResidents=Actors(n),ConfirmedDead=new[]{"CH_18"},CaseBundleClosed=true,DrawSeed=42,Events=new[]{new RevealEvent{Id="EVENT_01",CaseId="CASE_01",Text="기록된 원인과 결과",RevealApproved=true},new RevealEvent{Id="PRIVATE_01",CaseId="CASE_01",Text="보호 정보",RevealApproved=true,Protected=true},new RevealEvent{Id="OTHER_01",CaseId="CASE_02",Text="다른 미해결 사건",RevealApproved=true}}};
    static EvaluationInput Evaluation(bool full=false)
    {
        var names=new[]{"Investigation","Logic","Discovery","Verification","Court","Intervention"};var caps=new[]{200,200,150,150,200,100};var earned=full?caps:new[]{182,195,121,143,186,72};
        var obligations=names.SelectMany((area,i)=>new[]{new EvaluationObligation{Id=area+"_DONE",Area=area,Weight=earned[i],Alternatives=new[]{new ObligationProof{Id="PROOF_"+i,Completion=1,Basis=new[]{"OBSERVED_"+i}}}},new EvaluationObligation{Id=area+"_REST",Area=area,Weight=caps[i]-earned[i]}}).Where(o=>o.Weight>0).ToArray();
        return new EvaluationInput{Obligations=obligations,EssentialCausality=true,IndependentConfirmation=true,UniqueLearningCredits=Enumerable.Range(1,15).Select(i=>"CREDIT_"+i).ToArray(),BonusPerCredit=5};
    }
    static TrialEndgame Evaluated(TrialDirector c=null,EvaluationInput evaluation=null)
    {
        var e=new TrialEndgame();Assert.That(e.CommitVerdict(c??Court(),Input()),Is.EqualTo("VerdictCommitted"));e.BeginTruth();e.FinishTruth();Assert.That(e.Evaluate(evaluation??Evaluation()),Is.EqualTo("Evaluated"));return e;
    }
    [Test] public void QueuedUnspokenTopicsAndTextRemainPrivate()
    {
        var c=new TrialDirector();c.Start("TRIAL_01","K_LOOP_01",Actors(4),_=>true);
        c.QueueSpeech(new SpeechDraft{Id="SPEECH_01",Speaker="CH_02",Topic="숨긴 미래 주제",Text="아직 발화하지 않은 증언"});
        Assert.That(c.Read("CH_01",true).Topic,Is.Empty);Assert.That(c.Read("CH_01",true).AnnouncedTopics,Is.Empty);
        for(int i=0;i<3;i++)c.Step(a=>a!="CH_03");Assert.That(c.Read("CH_01",true).SpokenText,Is.EqualTo("아"));Assert.That(c.Read("CH_03",false).Topic,Is.Empty);
    }
    [Test] public void BallotsEditableUntilSimultaneousLock_AnonymousUnlessR02()
    {
        var c=new TrialDirector();c.Start("TRIAL_01","LOOP_01",Actors(4),_=>true);c.OpenVoting();c.ChooseVote("CH_01","CH_02");c.ChooseVote("CH_01","CH_04");Assert.That(c.Read("CH_02",true).LockedVotes,Is.Empty);Assert.That(c.LockVotes(),Is.EqualTo("IncompleteBallots"));
        foreach(var a in Actors(4).Skip(1))c.ChooseVote(a,"CH_04");c.LockVotes();Assert.That(c.Read("CH_01",true).LockedVotes.All(v=>v.Voter==""),Is.True);Assert.That(c.ChooseVote("CH_01","CH_03"),Is.EqualTo("Unavailable"));Assert.That(Court(5,"CH_04","R02").Read("CH_01",true).LockedVotes.All(v=>v.Voter!=""),Is.True);
    }
    [Test] public void TieHasOneRestrictedRevote_ThenPersistedPublicDraw()
    {
        var c=new TrialDirector();c.Start("TRIAL_01","LOOP_01",Actors(4),_=>true);c.ConfigureVoting("Basic",123);c.OpenVoting();
        for(int round=0;round<2;round++){
            c.ChooseVote("CH_01","CH_03");c.ChooseVote("CH_02","CH_03");c.ChooseVote("CH_03","CH_04");c.ChooseVote("CH_04","CH_04");c.LockVotes();var r=c.ResolveVoting();
            if(round==0){Assert.That(r,Is.EqualTo("RevoteOpened"));Assert.That(c.ChooseVote("CH_01","CH_02"),Is.EqualTo("Unavailable"));c=TrialDirector.Restore(c.Capture());}
        }
        var s=c.Capture();Assert.That(s.VoteRound,Is.EqualTo(2));Assert.That(s.DrawCount,Is.EqualTo(1));Assert.That(s.DrawCandidates,Is.EquivalentTo(new[]{"CH_03","CH_04"}));var restored=TrialDirector.Restore(s);restored.ResolveVoting();Assert.That(restored.SelectedTarget,Is.EqualTo(c.SelectedTarget));Assert.That(restored.Capture().DrawCount,Is.EqualTo(1));
    }
    [Test] public void BasicAndR01PopulationArithmeticNeverAddsSecondCulpritExecution()
    {
        for(int count=4;count<=18;count++)foreach(string rule in new[]{"Basic","R01","R02"})foreach(bool correct in new[]{true,false}){
            var c=Court(count,correct?"CH_04":"CH_02",rule);var input=Input(count);input.ConfirmedDead=Array.Empty<string>();var e=new TrialEndgame();e.CommitVerdict(c,input);var p=e.Capture().Plan;
            Assert.That(p.Executed.Length,Is.EqualTo(1));Assert.That(p.Escaped.Length,Is.EqualTo(correct?0:1));Assert.That(p.Executed.Intersect(p.Escaped),Is.Empty);
            Assert.That(p.DrawCount,Is.EqualTo(!correct&&rule!="R01"?1:0));if(rule=="R01")Assert.That(p.Executed.Single(),Is.EqualTo(c.SelectedTarget));Assert.That(e.Transition,Is.Empty);
        }
    }
    [Test] public void TruthDeniedBeforeVerdict_ApprovedBundleOnly_CopyIsolation()
    {
        var e=new TrialEndgame();Assert.That(e.ReadTruth(),Is.Empty);Assert.That(e.BeginTruth(),Is.EqualTo("Unavailable"));var input=Input();input.CaseBundleClosed=false;Assert.That(e.CommitVerdict(Court(),input),Is.EqualTo("Unavailable"));input.CaseBundleClosed=true;e.CommitVerdict(Court(),input);Assert.That(e.ReadTruth().Length,Is.EqualTo(1));e.ReadTruth()[0].Text="changed";Assert.That(e.ReadTruth()[0].Text,Is.EqualTo("기록된 원인과 결과"));
        e.BeginTruth();e.AdvanceTruth();var restored=TrialEndgame.Restore(e.Capture());Assert.That(restored.Capture().RevealCursor,Is.EqualTo(1));Assert.That(restored.Capture().Plan.Applied,Is.False);
    }
    [Test] public void TwoClosedCasesRevealBothButExecuteOnlyAdjudicatedActor()
    {
        var input=Input();input.ClosedCaseIds=new[]{"CASE_01","CASE_02"};input.Events=input.Events.Concat(new[]{new RevealEvent{Id="UNRELATED_03",CaseId="CASE_03",RevealApproved=true,Text="미해결 별건"}}).ToArray();
        var end=new TrialEndgame();Assert.That(end.CommitVerdict(Court(),input),Is.EqualTo("VerdictCommitted"));
        Assert.That(end.ReadTruth().Select(e=>e.CaseId),Is.EquivalentTo(input.ClosedCaseIds));Assert.That(end.Capture().Plan.Executed,Is.EqualTo(new[]{"CH_04"}));
        var restored=TrialEndgame.Restore(end.Capture());Assert.That(restored.ReadTruth().Length,Is.EqualTo(2));
        var bad=restored.Capture();bad.Reveal[1].CaseId="CASE_03";Assert.Throws<ArgumentException>(()=>TrialEndgame.Restore(bad));
    }
    [Test] public void Original899Score700ExperienceExample_RestoreAtEveryBoundary()
    {
        var e=Evaluated();Assert.That(e.Capture().Evaluation.Score,Is.EqualTo(899));Assert.That(e.Capture().Evaluation.Rank,Is.EqualTo("A"));Assert.That(e.Capture().Evaluation.Entitlement,Is.EqualTo(700));
        var profile=new TrialProfile{Experience=250};var durable=e.PrepareReward(profile);Assert.That(profile.Experience,Is.EqualTo(250));Assert.That(durable.Level,Is.EqualTo(3));Assert.That(durable.Experience,Is.EqualTo(250));Assert.That(durable.SkillPoints,Is.EqualTo(2));
        e=TrialEndgame.Restore(e.Capture());e.AcceptReward(durable);e=TrialEndgame.Restore(e.Capture());int commits=0;e.ApplySettlement(p=>{commits++;Assert.That(p.Residual,Is.EqualTo(4));return true;});e=TrialEndgame.Restore(e.Capture());e.ApplySettlement(_=>{commits++;return true;});Assert.That(commits,Is.EqualTo(1));Assert.That(e.Transition,Is.EqualTo("NextChapter"));Assert.That(e.PrepareReward(durable).Rewards.Length,Is.EqualTo(1));
    }
    [Test] public void WrongVerdictPersonalReasoningProtected_LoopOnlyAfterAppliedSettlement()
    {
        var c=new TrialDirector();c.Start("TRIAL_01","LOOP_01",Actors(5),_=>true);c.OpenVoting();c.ChooseVote("CH_01","CH_04");foreach(var a in Actors(5).Skip(1))c.ChooseVote(a,"CH_02");c.LockVotes();c.ResolveVoting();var evaluation=Evaluation();evaluation.PersonalReasonedCorrect=true;var e=Evaluated(c,evaluation);
        Assert.That(e.Capture().Evaluation.Rank,Is.EqualTo("E"));Assert.That(e.Capture().Evaluation.Score,Is.EqualTo(899));Assert.That(e.Capture().Evaluation.Entitlement,Is.EqualTo(700));Assert.That(e.Transition,Is.Empty);Assert.That(e.ApplySettlement(_=>true),Is.EqualTo("Unavailable"));
        e.AcceptReward(e.PrepareReward(new TrialProfile()));Assert.That(e.ApplySettlement(_=>false),Is.EqualTo("SettlementPending"));Assert.That(e.Transition,Is.Empty);e.ApplySettlement(_=>true);Assert.That(e.Capture().Plan.Residual,Is.EqualTo(3));Assert.That(e.Transition,Is.EqualTo("Loop"));int resets=0;e.CompleteTransition((p,t)=>{resets++;Assert.That(t,Is.EqualTo("Loop"));return true;});var restored=TrialEndgame.Restore(e.Capture());restored.CompleteTransition((p,t)=>{resets++;return true;});Assert.That(resets,Is.EqualTo(1));
    }
    [Test] public void OldBranchRewardCannotDuplicate_OnlyHigherEntitlementDelta()
    {
        var low=Evaluated();var profile=low.PrepareReward(new TrialProfile());low.AcceptReward(profile);var replay=Evaluated();var same=replay.PrepareReward(profile);Assert.That(same.Revision,Is.EqualTo(profile.Revision));Assert.That(same.Rewards.Length,Is.EqualTo(1));
        var high=Evaluated(evaluation:Evaluation(true));var improved=high.PrepareReward(profile);Assert.That(improved.Rewards.Last().AppliedExperience,Is.EqualTo(125));Assert.That(improved.Rewards.Sum(r=>r.AppliedExperience),Is.EqualTo(825));Assert.That(high.PrepareReward(improved).Revision,Is.EqualTo(improved.Revision));
    }
    [Test] public void AlternativeProofUsesBestOnly_AndMissingBasisRejected()
    {
        var e=new TrialEndgame();e.CommitVerdict(Court(),Input());e.BeginTruth();e.FinishTruth();var input=Evaluation();input.Obligations[0].Alternatives=new[]{new ObligationProof{Id="A",Completion=1,Basis=new[]{"OBS_A"}},new ObligationProof{Id="B",Completion=1,Basis=new[]{"OBS_B"}}};e.Evaluate(input);Assert.That(e.Capture().Evaluation.Score,Is.EqualTo(899));
        var invalid=new TrialEndgame();invalid.CommitVerdict(Court(),Input());invalid.BeginTruth();invalid.FinishTruth();input.Obligations[0].Alternatives[0].Basis=Array.Empty<string>();Assert.That(invalid.Evaluate(input),Is.EqualTo("InvalidProof"));
    }
    [Test] public void PlayerCulpritEscapesWithoutWishReward_ExitStatesDisjoint()
    {
        var e=new TrialEndgame();var input=Input();input.ActualCulprit="CH_01";e.CommitVerdict(Court(),input);var p=e.Capture().Plan;Assert.That(p.Escaped,Is.EquivalentTo(new[]{"CH_01"}));Assert.That(p.EscapeRewardEligible,Is.Empty);Assert.That(p.Executed,Does.Not.Contain("CH_01"));
        var corrupt=e.Capture();corrupt.Plan.Executed=new[]{"CH_01"};Assert.Throws<ArgumentException>(()=>TrialEndgame.Restore(corrupt));
    }
}
