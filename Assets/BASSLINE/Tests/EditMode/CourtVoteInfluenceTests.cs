using System;
using System.Linq;
using NUnit.Framework;
using BASSLINE.Core;
using BASSLINE.Knowledge;
using BASSLINE.Investigation;
using BASSLINE.NPC;
using BASSLINE.Trial;
public sealed class CourtVoteInfluenceTests
{
    static readonly string[] Actors={"CH_01","CH_02","CH_03","CH_04"};
    TrialDirector court;KnowledgeLedger knowledge;string evidence;
    [SetUp]public void Setup()
    {
        knowledge=new KnowledgeLedger(Actors);court=new TrialDirector();court.Start("COURT_VOTE",knowledge.LoopId,Actors,_=>true);
        evidence=knowledge.Observe("CH_01",new KnownRecord{Kind="Visual",SubjectId="CH_04",Predicate="CausedOutcome",Value="RESULT_A",PlaceId="ROOM_A",Text="같은 행동과 결과의 연결",IdentityConfirmed=true,FromTick=10,ToTick=11},10);
        court.QueueSpeech(new SpeechDraft{Id="SPEECH_A",Speaker="CH_01",Topic="인과",Text="이 행동과 결과가 연결돼.",Claim=new ClaimRecord{Id="CLAIM_A",OwnerId="CH_01",LoopId=knowledge.LoopId,Text="행동과 결과",Spans=new[]{new ClaimSpan{Id="SPAN_A",SubjectId="CH_04",Predicate="CausedOutcome",Value="RESULT_A",PlaceId="ROOM_A",FromTick=10,ToTick=11}}}});
    }
    void FinishSpeech(){for(int i=0;i<200;i++)court.Step(id=>id!="CH_03");}
    void Support()
    {
        Assert.That(court.EnterFocus(knowledge.For("CH_01"),"CLAIM_A","SPAN_A"),Is.EqualTo("Focused"));
        Assert.That(court.Submit(knowledge.For("CH_01"),"PROOF_A","Support","LR07",new[]{evidence}).ResultType,Is.EqualTo("Support"));
    }
    [Test]public void OnlyFinishedHeardClaimsAndReceivedProofsCanInfluenceBallots()
    {
        court.Step(_=>true);Assert.That(court.ReadHeardClaims("CH_02"),Is.Empty);FinishSpeech();Support();
        var reasoning=new TrialReasoning();Assert.That(reasoning.ChooseVote(knowledge.For("CH_02"),Actors,41,court.ReadHeardClaims("CH_02")).Basis,Is.Empty);
        court.DeliverSubmission("PROOF_A","CH_02");
        var decision=reasoning.ChooseVote(knowledge.For("CH_02"),Actors,41,court.ReadHeardClaims("CH_02"));Assert.That(decision.Choice,Is.EqualTo("CH_04"));Assert.That(decision.Basis,Does.Contain("CLAIM_A"));
        court.DeliverSubmission("PROOF_A","CH_03");Assert.That(court.ReadHeardClaims("CH_03"),Is.Empty,"A proof receipt must not disclose an unheard original claim.");
        Assert.That(court.ReadHeardClaims("CH_99"),Is.Empty);Assert.That(knowledge.For("CH_02").Records(),Is.Empty,"Public review does not manufacture private direct observations.");
    }
    [Test]public void ClearingActiveIssueAndSaveRestorePreservePersonalReviewHistory()
    {
        FinishSpeech();Support();court.DeliverSubmission("PROOF_A","CH_02");court.RetireClaim("CLAIM_A");Assert.That(court.Read("CH_02",false).ActiveClaims,Is.Empty);
        var restored=TrialDirector.Restore(court.Capture());var own=restored.ReadHeardClaims("CH_02").Single();Assert.That(own.Review,Is.EqualTo("SupportedWithinScope"));Assert.That(own.SpeakerId,Is.EqualTo("CH_01"));own.Span.Value="MUTATED";
        Assert.That(restored.ReadHeardClaims("CH_02").Single().Span.Value,Is.EqualTo("RESULT_A"));Assert.That(restored.ReadHeardClaims("CH_04").Single().Review,Is.EqualTo("Asserted"));
    }
    [Test]public void EveryParticipantCanVoteInRunoffWithoutBeingNominated()
    {
        FinishSpeech();court.OpenVoting();court.ChooseVote("CH_01","CH_02");court.ChooseVote("CH_02","CH_02");court.ChooseVote("CH_03","CH_04");court.ChooseVote("CH_04","CH_04");court.LockVotes();Assert.That(court.ResolveVoting(),Is.EqualTo("RevoteOpened"));
        var saved=court.Capture();court=TrialDirector.Restore(saved);
        foreach(var actor in Actors){var decision=new TrialReasoning().ChooseVote(knowledge.For(actor),saved.VoteCandidates,41,court.ReadHeardClaims(actor));Assert.That(saved.VoteCandidates,Does.Contain(decision.Choice));Assert.That(court.ChooseVote(actor,decision.Choice),Is.EqualTo("Recorded"));}
        Assert.That(court.LockVotes(),Is.EqualTo("VotesLocked"));Assert.That(court.ResolveVoting(),Is.EqualTo("VerdictTargetLocked"));
    }
    [Test]public void SavedRunoffCannotVoteForAnEliminatedCandidate()
    {
        FinishSpeech();court.OpenVoting();court.ChooseVote("CH_01","CH_02");court.ChooseVote("CH_02","CH_02");court.ChooseVote("CH_03","CH_04");court.ChooseVote("CH_04","CH_04");court.LockVotes();court.ResolveVoting();court.ChooseVote("CH_01","CH_02");
        var saved=court.Capture();saved.Ballots[0].Choice="CH_03";Assert.Throws<ArgumentException>(()=>TrialDirector.Restore(saved));
    }
}
