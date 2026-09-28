using System;
using System.Linq;
using NUnit.Framework;
using BASSLINE.Core;
using BASSLINE.Knowledge;
using BASSLINE.NPC;
public sealed class NpcTrialReasoningTests
{
    static readonly string[] Actors={"CH_01","CH_02","CH_03","CH_04"};
    static KnownRecord Fact(string predicate="CausedOutcome",string subject="CH_04",string value="Contact_Then_Collapse")=>new KnownRecord{Kind="Visual",SubjectId=subject,Predicate=predicate,Value=value,Text="관측한 사실",IdentityConfirmed=true,FromTick=10,ToTick=11,PlaceId="ROOM_A",DoesNotEstablish=new[]{"숨은 의도는 미확인"}};
    [Test] public void PrivateKnowledgeOfOtherActorNeverBecomesOwnSpeechOrVoteBasis()
    {
        var k=new KnowledgeLedger(Actors);k.Observe("CH_02",Fact(),10);var r=new TrialReasoning();Assert.That(r.DraftSpeech(k.For("CH_01"),"SPEECH_01",Array.Empty<string>()),Is.Null);var decision=r.ChooseVote(k.For("CH_01"),Actors,123);Assert.That(decision.Basis,Is.Empty);Assert.That(decision.Uncertain,Is.True);Assert.That(decision.EquallySupportedCandidates,Is.EquivalentTo(Actors));
    }
    [Test] public void DirectCausalObservationSupportsScopedChoice_HeardSoundNeverBecomesLocation()
    {
        var k=new KnowledgeLedger(Actors);string id=k.Observe("CH_01",Fact(),10);var r=new TrialReasoning();var speech=r.DraftSpeech(k.For("CH_01"),"SPEECH_01",Array.Empty<string>());Assert.That(speech.Spans.Single().FromTick,Is.EqualTo(10));Assert.That(speech.Spans.Single().ToTick,Is.EqualTo(11));Assert.That(speech.Text,Does.Contain("숨은 의도는 미확인"));var vote=r.ChooseVote(k.For("CH_01"),Actors,41);Assert.That(vote.Choice,Is.EqualTo("CH_04"));Assert.That(vote.Basis,Is.EquivalentTo(new[]{id}));Assert.That(vote.DrawCount,Is.Zero);
        string sound=k.Observe("CH_01",Fact("HeardSound","UNKNOWN_ACTOR","Voice"),10);var next=r.DraftSpeech(k.For("CH_01"),"SPEECH_02",new[]{id});Assert.That(next.Spans.Single().Predicate,Is.EqualTo("HeardSound"));Assert.That(next.Spans.Single().SubjectId,Is.EqualTo("UNKNOWN_ACTOR"));
    }
    [Test] public void SameSourceRumorDoesNotCountAsIndependentConfirmation()
    {
        var k=new KnowledgeLedger(Actors);string original=k.Observe("CH_02",Fact(),10);string received=k.Deliver("CH_02","CH_01",original,10);string forwarded=k.Deliver("CH_02","CH_03",original,10);k.Deliver("CH_03","CH_01",forwarded,10);var r=new TrialReasoning();var decision=r.ChooseVote(k.For("CH_01"),Actors,41);Assert.That(decision.Choice,Is.EqualTo("CH_04"));Assert.That(decision.Explanation,Does.Contain("독립 확인이 부족"));Assert.That(decision.Basis,Is.EquivalentTo(new[]{received}));Assert.That(r.DraftSpeech(k.For("CH_01"),"SPEECH_01",Array.Empty<string>()).Text,Does.Contain("전달받은 진술"));
    }
    [Test] public void OnlyReceivedCurrentLoopPublicClaimMayInfluenceVote()
    {
        var k=new KnowledgeLedger(Actors);var r=new TrialReasoning();var claim=new NpcHeardClaim{Id="CLAIM_01",ReceiverId="CH_02",LoopId=k.LoopId,Review="SupportedWithinScope",Span=new NpcTrialSpan{Id="SPAN_01",SubjectId="CH_04",Predicate="CausedOutcome",FromTick=10,ToTick=11,PlaceId="ROOM_A"}};
        Assert.That(r.ChooseVote(k.For("CH_01"),Actors,41,new[]{claim}).Basis,Is.Empty);claim.ReceiverId="CH_01";claim.LoopId="PAST_LOOP";Assert.That(r.ChooseVote(k.For("CH_01"),Actors,41,new[]{claim}).Basis,Is.Empty);claim.LoopId=k.LoopId;Assert.That(r.ChooseVote(k.For("CH_01"),Actors,41,new[]{claim}).Choice,Is.EqualTo("CH_04"));claim.Review="Contradicted";Assert.That(r.ChooseVote(k.For("CH_01"),Actors,41,new[]{claim}).Basis,Is.Empty);
    }
    [Test] public void AlibiCountersOnlyMatchingTimeAndPlace_NeverWholeCharacterInnocence()
    {
        var k=new KnowledgeLedger(Actors);string alibi=k.Observe("CH_01",Fact("AtPlace","CH_04","ROOM_B"),10);var r=new TrialReasoning();var claim=new NpcHeardClaim{Id="CLAIM_01",ReceiverId="CH_01",LoopId=k.LoopId,Review="SupportedWithinScope",Span=new NpcTrialSpan{Id="SPAN_01",SubjectId="CH_04",Predicate="AtPlace",Value="ROOM_A",PlaceId="ROOM_A",FromTick=10,ToTick=11}};
        var response=r.RespondToClaim(k.For("CH_01"),claim);Assert.That(response.Action,Is.EqualTo("Rebut"));Assert.That(response.RuleId,Is.EqualTo("LR03"));Assert.That(response.RecordIds,Is.EquivalentTo(new[]{alibi}));claim.Span.Predicate="CausedOutcome";Assert.That(r.ChooseVote(k.For("CH_01"),Actors,41,new[]{claim}).Basis,Is.Empty);claim.Span.FromTick=20;claim.Span.ToTick=21;Assert.That(r.ChooseVote(k.For("CH_01"),Actors,41,new[]{claim}).Choice,Is.EqualTo("CH_04"));
    }
    [Test] public void NoEvidenceTieDecisionIsDeterministicAndStateCanBePersisted()
    {
        var k=new KnowledgeLedger(Actors);var r=new TrialReasoning();var first=r.ChooseVote(k.For("CH_01"),Actors,123);var replay=r.ChooseVote(k.For("CH_01"),Actors.Reverse().ToArray(),123);Assert.That(replay.Choice,Is.EqualTo(first.Choice));Assert.That(replay.NextRandomState,Is.EqualTo(first.NextRandomState));Assert.That(replay.DrawCount,Is.EqualTo(1));Assert.That(replay.Explanation,Does.Contain("기권 불가"));
        uint state=123;var votes=Enumerable.Range(0,12).Select(_=>{var choice=r.ChooseVote(k.For("CH_01"),Actors,state);state=choice.NextRandomState;return choice.Choice;}).ToArray();Assert.That(votes.Distinct().Count(),Is.GreaterThan(1));
    }
}
