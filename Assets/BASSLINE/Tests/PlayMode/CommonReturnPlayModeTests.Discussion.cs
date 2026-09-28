using System;
using System.Collections;
using System.IO;
using System.Linq;
using BASSLINE.AuthoringData;
using BASSLINE.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace BASSLINE.Tests
{
    public sealed partial class CommonReturnPlayModeTests
    {
        void FrameDiscussion(string actor,bool ownerNearby=false)
        {
            Place("CH_01",new Vector3(-4,0,-5.3f));Place(actor,new Vector3(-4,0,-4));
            if(actor!="CH_06")Place("CH_06",ownerNearby?new Vector3(-2.7f,0,-4):new Vector3(5,0,-4));
            runtime.SetLook(0,0);Ticks(1);hud.Primary(actor);MansionUiTestEntry.FinishSpeech(runtime,hud);Click("펜이 사라진 일");
        }
        void EndDiscussion(){Click("그만 이야기하기");}
        void FinishDiscussion(){MansionUiTestEntry.FinishSpeech(runtime,hud);}
        [UnityTest]public IEnumerator SpokenAccusationAndCorrectionReachOnlyActualListenersAndCannotBeFarmed()
        {
            Borrow();PlaceOnTray();FrameDiscussion("CH_18",true);Click("민서가 훔쳤다고 말하기");
            Assert.That(runtime.ReadConversationPlayback().SpeakerId,Is.EqualTo("CH_01"));Assert.That(runtime.ReadConversationPlayback().HeardText,Is.Empty);
            Ticks(100);Assert.That(runtime.CaptureSession().ItemExchange.Statements,Is.Empty,"Starting a sentence must not commit a complete accusation.");
            FinishDiscussion();var accusation=runtime.CaptureSession().ItemExchange.Statements.Single();
            Assert.That(accusation.Kind,Is.EqualTo("Accuse"));Assert.That(accusation.HeardBy,Does.Contain("CH_18"));Assert.That(accusation.HeardBy,Does.Contain("CH_06"));Assert.That(accusation.HeardBy,Does.Not.Contain("CH_03"));
            Assert.That(runtime.Social.Experiences("CH_18").Single(e=>e.Kind=="UnfoundedAccusation").Trust,Is.EqualTo(-2));
            Assert.That(runtime.Knowledge.For("CH_03").Records().Any(r=>r.Value=="LoanDiscussion:Accuse:"+accusation.LoanId),Is.False);
            Assert.That(runtime.CaptureSession().Incidents,Is.Empty);Assert.That(runtime.ReadLoanDiscussion("CH_18").CanAccuse,Is.False);
            Click("반납대에 있던 펜 봤어?");FinishDiscussion();Assert.That(runtime.ReadConversationPlayback().HeardText,Does.Contain("대답하고 싶지"));
            Click("내가 단정했어. 미안해.");FinishDiscussion();Assert.That(runtime.ReadLoanDiscussion("CH_18").CanWithdraw,Is.False);
            Assert.That(runtime.Social.Experiences("CH_18").Count(e=>e.Kind=="ClaimCorrected"),Is.EqualTo(1));
            Assert.That(runtime.Social.Experiences("CH_18").Where(e=>e.Kind=="ClaimCorrected"||e.Kind=="UnfoundedAccusation").Sum(e=>e.Trust),Is.EqualTo(-1),"An apology must not erase all damage or farm trust.");
            string path=Path.Combine(runtime.UseIsolatedTestStorage(),"correction.json");runtime.SaveTo(path);runtime.LoadFrom(path);
            Assert.That(runtime.CaptureSession().ItemExchange.Statements.Length,Is.EqualTo(2));Assert.That(runtime.CaptureSession().Incidents,Is.Empty);
            Click("다른 이야기로");Click("대화 다시 보기");
            var history=hud.GetComponentsInChildren<BASSLINE.UI.ProductionScreenView>(true).Single(v=>v.gameObject.activeInHierarchy&&v.ScreenId=="UI_10").Body.text;
            Assert.That(history,Does.Contain("민서가 그 펜을 훔쳤어."));Assert.That(history,Does.Contain("사실처럼 말했어. 미안해."));Assert.That(history,Does.Contain("다음엔 먼저 물어봐 주세요."));yield return null;
        }
        [UnityTest]public IEnumerator InterruptedPlayerSpeechRemainsAFragmentAndMidSentenceSaveDoesNotDuplicateIt()
        {
            Borrow();PlaceOnTray();FrameDiscussion("CH_18");Click("민서가 훔쳤다고 말하기");Ticks(60);
            string path=Path.Combine(runtime.UseIsolatedTestStorage(),"player-speaking.json");runtime.SaveTo(path);
            runtime.EndConversation();Assert.That(runtime.CaptureSession().ItemExchange.Statements,Is.Empty);Assert.That(runtime.Social.Experiences("CH_18").Any(e=>e.Kind=="UnfoundedAccusation"),Is.False);
            Assert.That(runtime.Knowledge.For("CH_18").Records().Any(r=>r.Predicate=="HeardFragment"&&r.Source=="CH_01"),Is.True);
            runtime.LoadFrom(path);Assert.That(runtime.CaptureSession().Conversation.ElapsedTicks,Is.EqualTo(60));FinishDiscussion();
            Assert.That(runtime.CaptureSession().ItemExchange.Statements.Length,Is.EqualTo(1));runtime.SaveTo(path);runtime.LoadFrom(path);
            Assert.That(runtime.World.Events.Count(e=>e.Type=="LoanDiscussionSpoken"),Is.EqualTo(1));yield return null;
        }
        [UnityTest]public IEnumerator ACorrectionMustReachEachOriginalListenerAndNeverUpdatesRemoteMemories()
        {
            Borrow();PlaceOnTray();FrameDiscussion("CH_18",true);Click("민서가 훔쳤다고 말하기");FinishDiscussion();
            Place("CH_06",new Vector3(8,0,-4));Click("내가 단정했어. 미안해.");FinishDiscussion();
            Assert.That(runtime.CaptureSession().ItemExchange.Statements.Last().HeardBy,Does.Not.Contain("CH_06"));
            EndDiscussion();FrameDiscussion("CH_06");Assert.That(runtime.ReadLoanDiscussion("CH_06").CanWithdraw,Is.True);Click("내가 단정했어. 미안해.");FinishDiscussion();
            Assert.That(runtime.ReadLoanDiscussion("CH_06").CanWithdraw,Is.False);Assert.That(runtime.CaptureSession().ItemExchange.Statements.Length,Is.EqualTo(3));yield return null;
        }
        [UnityTest]public IEnumerator AListenerMissingTheStartHearsOnlyAFragmentAndIsNotAnAccusationRecipient()
        {
            Borrow();PlaceOnTray();FrameDiscussion("CH_18");Click("민서가 훔쳤다고 말하기");Ticks(160);
            Place("CH_06",new Vector3(-2.7f,0,-4));FinishDiscussion();
            Assert.That(runtime.CaptureSession().ItemExchange.Statements.Single().HeardBy,Does.Not.Contain("CH_06"));
            Assert.That(runtime.Knowledge.For("CH_06").Records().Any(r=>r.Source=="CH_01"&&r.Predicate=="HeardFragment"),Is.True);yield return null;
        }
        [UnityTest]public IEnumerator PublicRecordAndOriginalObjectSupportAnExplanationEvenAfterCollectorRefuses()
        {
            Borrow();PlaceOnTray();FrameDiscussion("CH_18");Click("민서가 훔쳤다고 말하기");FinishDiscussion();EndDiscussion();
            Place("CH_01",new Vector3(-4,0,-5.3f));StartCollector();
            for(int i=0;i<2400&&runtime.CaptureSession().ItemExchange.CommonReturn.Entries.Length==0;i++){Ticks(1);if(i%180==0)yield return null;}
            Assert.That(runtime.CaptureSession().ItemExchange.CommonReturn.Entries.Length,Is.EqualTo(1));
            FrameDiscussion("CH_18");Click("반납대에 있던 펜 봤어?");FinishDiscussion();Assert.That(runtime.ReadConversationPlayback().HeardText,Does.Contain("대답하고 싶지"));EndDiscussion();
            FrameSurface(station.DrawerPoint);Assert.That(runtime.Interact(CommonReturnStation.DrawerId),Does.Contain("열었어요"));runtime.Examine(Pen);Ticks(120);
            // An empty hand and matching pen are not a substitute for the independent written account.
            Place("CH_18",runtime.Bodies.Single(b=>b.ActorId=="CH_01").transform.position+Vector3.right*1.2f);
            Assert.That(runtime.ReadLoanDiscussion("CH_18").CanExplain,Is.False);
            var book=UnityEngine.Object.FindObjectsByType<FixtureTarget>().Single(t=>t.StableId==CommonReturnStation.BookId);FrameSurface(book.transform);runtime.Examine(CommonReturnStation.BookId);Ticks(120);
            FrameDiscussion("CH_18");Assert.That(runtime.ReadLoanDiscussion("CH_18").CanExplain,Is.True);Click("확인한 내용을 설명하기");FinishDiscussion();
            var explanation=runtime.CaptureSession().ItemExchange.Statements.Last();Assert.That(explanation.Kind,Is.EqualTo("Explain"));Assert.That(explanation.EvidenceIds.Length,Is.EqualTo(2));
            foreach(var id in explanation.EvidenceIds){var root=runtime.Knowledge.For("CH_01").Find(id).RootId;Assert.That(runtime.Knowledge.For("CH_18").Records().Any(r=>r.RootId==root),Is.False,"A summary spoken aloud must not transmit the entire book or grant an independent inspection.");}
            Assert.That(runtime.Knowledge.For("CH_18").Records().Any(r=>r.Source=="CH_01"&&r.Predicate=="SaidStatement"&&r.ProvenanceKey==explanation.Id),Is.True);
            Assert.That(runtime.ReadLoanDiscussion("CH_18").CanWithdraw,Is.False);Assert.That(runtime.ReadLoanDiscussion("CH_18").CanExplain,Is.False);Assert.That(runtime.CaptureSession().Incidents,Is.Empty);
            string path=Path.Combine(runtime.UseIsolatedTestStorage(),"explanation.json");runtime.SaveTo(path);runtime.LoadFrom(path);
        }
        [UnityTest]public IEnumerator BothPeopleStayInTheConversationAndResumeTheInterruptedWalkingPlan()
        {
            Borrow();PlaceOnTray();Place("CH_01",new Vector3(-4,0,-5.3f));Place("CH_18",new Vector3(-4,0,-4));Place("CH_06",new Vector3(5,0,-4));runtime.SetLook(0,0);Ticks(1);
            string destination=runtime.Layout.NavigationNodes.Where(n=>n.RoomId=="R_HALL").OrderByDescending(n=>n.Position.z).First().Id;
            Assert.That(runtime.World.Plan("CH_18",destination,"Walk",180),Is.EqualTo("Accepted"));
            hud.Primary("CH_18");FinishDiscussion();Click("펜이 사라진 일");Click("민서가 훔쳤다고 말하기");
            var position=runtime.World.Resident("CH_18").Position;Ticks(80);
            Assert.That(runtime.World.Resident("CH_18").Activity,Is.EqualTo("Listen"));Assert.That(runtime.World.Resident("CH_18").Position.Distance(position),Is.LessThan(.001));
            string path=Path.Combine(runtime.UseIsolatedTestStorage(),"listener-walking.json");runtime.SaveTo(path);runtime.LoadFrom(path);FinishDiscussion();EndDiscussion();
            Assert.That(runtime.World.Resident("CH_18").Phase,Is.EqualTo("Travelling"));Assert.That(runtime.World.Resident("CH_18").Destination,Is.EqualTo(destination));yield return null;
        }
    }
}
