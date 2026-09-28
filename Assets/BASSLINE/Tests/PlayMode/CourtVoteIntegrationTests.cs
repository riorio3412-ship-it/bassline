using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BASSLINE.Bootstrap;
using BASSLINE.Core;
using BASSLINE.Investigation;
using BASSLINE.Trial;
using BASSLINE.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace BASSLINE.Tests
{
    public sealed class CourtVoteIntegrationTests
    {
        MansionRuntime runtime;TrialDirector court;
        static readonly string[] Actors={"CH_01","CH_02","CH_03","CH_04"};
        [UnitySetUp]public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("Mansion_Playable");yield return null;runtime=UnityEngine.Object.FindAnyObjectByType<MansionRuntime>();runtime.AutomaticTick=false;runtime.UseIsolatedTestStorage();
            UnityEngine.Object.FindAnyObjectByType<FixtureHud>().enabled=false;
            // Explicit court-adapter test; does not claim physical incident, summons or seating coverage.
            court=new TrialDirector();court.Start("TESTONLY_VOTE_INTEGRATION",runtime.Knowledge.LoopId,Actors,_=>true);
            typeof(MansionRuntime).GetField("court",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(runtime,court);
        }
        void SupportedPublicClaim()
        {
            var k=runtime.Knowledge;long tick=runtime.World.Tick;
            string evidence=k.Observe("CH_01",new KnownRecord{Kind="Visual",SubjectId="CH_04",Predicate="CausedOutcome",Value="RESULT_A",PlaceId="R_HALL",Text="검사용 인과 관측",IdentityConfirmed=true,FromTick=tick,ToTick=tick+1},tick);
            court.QueueSpeech(new SpeechDraft{Id="SPEECH_A",Speaker="CH_01",Topic="인과",Text="행동과 결과",Claim=new ClaimRecord{Id="CLAIM_A",OwnerId="CH_01",LoopId=k.LoopId,Text="인과",Spans=new[]{new ClaimSpan{Id="SPAN_A",SubjectId="CH_04",Predicate="CausedOutcome",Value="RESULT_A",PlaceId="R_HALL",FromTick=tick,ToTick=tick+1}}}});
            for(int i=0;i<100;i++)court.Step(_=>true);
            court.EnterFocus(k.For("CH_01"),"CLAIM_A","SPAN_A");court.Submit(k.For("CH_01"),"PROOF_A","Support","LR07",new[]{evidence});
            foreach(var actor in Actors)court.DeliverSubmission("PROOF_A",actor);
        }
        [UnityTest]public IEnumerator RuntimeBallotUsesReceivedProofEvenWhenNpcHasNoPrivateCausalRecord()
        {
            SupportedPublicClaim();Assert.That(runtime.Knowledge.For("CH_02").Records().Any(r=>r.Predicate=="CausedOutcome"),Is.False);
            Assert.That(runtime.OpenTrialVoting(),Is.EqualTo("Opened"));Assert.That(runtime.CastTrialVote("CH_04"),Is.EqualTo("VotesLocked"));
            Assert.That(court.Capture().Ballots.Where(b=>b.Voter!="CH_01").All(b=>b.Choice=="CH_04"),Is.True);
            var before=JsonUtility.ToJson(runtime.CaptureSession().Proceedings);runtime.CastTrialVote("CH_02");Assert.That(JsonUtility.ToJson(runtime.CaptureSession().Proceedings),Is.EqualTo(before),"Repeated input must not redraw locked ballots.");yield return null;
        }
        [UnityTest]public IEnumerator RuntimeRunoffIncludesVotersOutsideTheTiedCandidatePool()
        {
            court.OpenVoting();court.ChooseVote("CH_01","CH_02");court.ChooseVote("CH_02","CH_02");court.ChooseVote("CH_03","CH_04");court.ChooseVote("CH_04","CH_04");court.LockVotes();court.ResolveVoting();
            Assert.That(runtime.CastTrialVote("CH_02"),Is.EqualTo("VotesLocked"));Assert.That(court.Capture().Ballots.Length,Is.EqualTo(4));Assert.That(court.Capture().Ballots.All(b=>b.Choice=="CH_02"||b.Choice=="CH_04"),Is.True);yield return null;
        }
    }
}
