using System;
using System.Collections;
using System.IO;
using System.Linq;
using BASSLINE.AuthoringData;
using BASSLINE.Bootstrap;
using BASSLINE.Trial;
using BASSLINE.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace BASSLINE.Tests
{
    public sealed partial class CommonReturnPlayModeTests
    {
        void NearOwner(){runtime.EndConversation();Place("CH_06",new Vector3(-4,0,-4));Place("CH_01",new Vector3(-4,0,-5.3f));runtime.SetLook(0,0);Ticks(1);}
        void FinishReturn(){NearOwner();Assert.That(runtime.ReturnBorrowedItem("CH_06"),Is.EqualTo("물건을 건네고 있어요."));for(int i=0;i<310&&runtime.ReadItemExchange().Running;i++)Ticks(1);Ticks(1);Assert.That(runtime.World.Object(Pen).Owner,Is.EqualTo("CH_06"));}
        TrialProfile ReadLearningProfile(string storage)=>JsonUtility.FromJson<TrialProfile>(File.ReadAllText(Path.Combine(storage,"profile-v1.dat")).Substring(65));
        [UnityTest]public IEnumerator PrivateNoteRequiresPhysicalReadingAndIncompleteReadingDoesNotUnlockQuestion()
        {
            Borrow();Assert.That(runtime.ReadItemExchangeChoices("CH_06").CanAskPrivateNote,Is.False);
            Assert.That(runtime.Examine(CommonReturnStation.PrivateNoteId),Is.EqualTo("Unavailable"));
            FrameSurface(station.PrivateNote);Assert.That(runtime.Examine(CommonReturnStation.PrivateNoteId),Is.EqualTo("Pending"));Ticks(40);NearOwner();Ticks(120);
            Assert.That(runtime.ReadItemExchangeChoices("CH_06").CanAskPrivateNote,Is.False);
            FrameSurface(station.PrivateNote);Assert.That(runtime.Examine(CommonReturnStation.PrivateNoteId),Is.EqualTo("Pending"));Ticks(120);NearOwner();
            Assert.That(runtime.ReadItemExchangeChoices("CH_06").CanAskPrivateNote,Is.True);
            Assert.That(runtime.AskAboutPrivateNote("CH_06"),Is.EqualTo("Dialogue"));MansionUiTestEntry.FinishSpeech(runtime,hud);
            Assert.That(runtime.ReadNotebook().Records.Any(r=>r.Predicate=="SaidStatement"&&r.Value=="LoanPrivateNoteClarification:"+Pen),Is.True);
            Assert.That(runtime.CaptureSession().ItemExchange.Learning,Is.Null);yield return null;
        }
        [UnityTest]public IEnumerator PrivateBagPlacementSurvivesSaveIsNotCollectedAndDirectReturnLearnsOnceAcrossReload()
        {
            string storage=runtime.UseIsolatedTestStorage();Borrow();FrameSurface(station.PrivateNote);runtime.Examine(CommonReturnStation.PrivateNoteId);Ticks(120);
            FrameSurface(station.BagFrontPoint);Assert.That(runtime.Interact(CommonReturnStation.BagFrontId),Is.EqualTo("펜을 옮기고 있어요."));Ticks(45);
            string during=Path.Combine(storage,"private-motion.json");runtime.SaveTo(during);Ticks(50);runtime.LoadFrom(during);Ticks(75);
            Assert.That(runtime.World.Object(Pen).AnchorId,Is.EqualTo(CommonReturnStation.BagFrontId),runtime.ReadItemExchange().Text);
            Assert.That(runtime.CaptureSession().ItemExchange.Loans.Single().Status,Is.EqualTo("Borrowed"));
            Assert.That(runtime.Knowledge.For("CH_06").Records().Any(r=>r.Predicate=="SurfaceObjectTransfer"&&r.Value=="PlacePrivate"),Is.False);
            NearOwner();StartCollector();Ticks(600);Assert.That(runtime.World.Object(Pen).AnchorId,Is.EqualTo(CommonReturnStation.BagFrontId));Assert.That(runtime.CaptureSession().ItemExchange.CommonReturn.Entries,Is.Empty);
            FrameSurface(station.BagFrontPoint);Assert.That(runtime.Interact(Pen),Is.EqualTo("펜을 옮기고 있어요."));Ticks(120);Assert.That(runtime.World.Object(Pen).Owner,Is.EqualTo("CH_01"));
            NearOwner();runtime.AskAboutPrivateNote("CH_06");MansionUiTestEntry.FinishSpeech(runtime,hud);runtime.EndConversation();
            string before=Path.Combine(storage,"before-return.json");runtime.SaveTo(before);FinishReturn();
            var lesson=runtime.CaptureSession().ItemExchange.Learning;Assert.That(lesson,Is.Not.Null);Assert.That(lesson.Route,Is.EqualTo("PrivateNote"));
            Assert.That(ReadLearningProfile(storage).Experience,Is.EqualTo(10));Assert.That(runtime.CaptureSession().Incidents,Is.Empty);
            var forged=runtime.CaptureSession();forged.ItemExchange.Learning.Basis=Array.Empty<string>();string payload=JsonUtility.ToJson(forged),invalid=Path.Combine(storage,"forged-lesson.dat");File.WriteAllText(invalid,AtomicSaveStore.Hash(payload)+"\n"+payload);
            long tick=runtime.World.Tick;Assert.Throws<InvalidDataException>(()=>runtime.LoadFrom(invalid));Assert.That(runtime.World.Tick,Is.EqualTo(tick));
            string complete=Path.Combine(storage,"complete.json");runtime.SaveTo(complete);runtime.LoadFrom(complete);Assert.That(runtime.CaptureSession().ItemExchange.Learning.RecordId,Is.EqualTo(lesson.RecordId));
            runtime.LoadFrom(before);FinishReturn();Assert.That(ReadLearningProfile(storage).Experience,Is.EqualTo(10));Assert.That(ReadLearningProfile(storage).Rewards.Length,Is.EqualTo(1));yield return null;
        }
        [UnityTest]public IEnumerator ClearInstructionsTeachOnReturnButMerelyBorrowingAndReturningDoesNotAwardUnknownLesson()
        {
            string storage=runtime.UseIsolatedTestStorage();Borrow();FinishReturn();Assert.That(runtime.CaptureSession().ItemExchange.Learning,Is.Null);Assert.That(File.Exists(Path.Combine(storage,"profile-v1.dat")),Is.False);
            NearOwner();runtime.AskToBorrowPen("CH_06");MansionUiTestEntry.FinishSpeech(runtime,hud);runtime.ReceivePen("CH_06");for(int i=0;i<310&&runtime.ReadItemExchange().Running;i++)Ticks(1);
            NearOwner();runtime.AskWhereToReturn("CH_06");MansionUiTestEntry.FinishSpeech(runtime,hud);FinishReturn();
            Assert.That(runtime.CaptureSession().ItemExchange.Learning.Route,Is.EqualTo("ClearInstruction"));Assert.That(ReadLearningProfile(storage).Experience,Is.EqualTo(10));yield return null;
        }
        [Test]public void UniqueReturnLearningUsesProfileNotBranchAndLevelsAtExistingThreshold()
        {
            var original=new TrialProfile{Experience=295};var next=TrialEndgame.PrepareReturnLearning(original);var repeated=TrialEndgame.PrepareReturnLearning(next);
            Assert.That(original.Experience,Is.EqualTo(295));Assert.That(next.Level,Is.EqualTo(2));Assert.That(next.Experience,Is.EqualTo(5));Assert.That(next.SkillPoints,Is.EqualTo(1));Assert.That(repeated.Revision,Is.EqualTo(next.Revision));Assert.That(repeated.Rewards.Length,Is.EqualTo(1));
        }
        [UnityTest]public IEnumerator PublicReturnLessonWaitsUntilEveryActualAccusationListenerHearsCorrection()
        {
            string storage=runtime.UseIsolatedTestStorage();Borrow();PlaceOnTray();FrameDiscussion("CH_18",true);Click("민서가 훔쳤다고 말하기");FinishDiscussion();EndDiscussion();
            var accusation=runtime.CaptureSession().ItemExchange.Statements.Single();CollectionAssert.AreEquivalent(new[]{"CH_18","CH_06"},accusation.HeardBy);
            Place("CH_18",new Vector3(8,0,-4));FrameSurface(station.TrayPoint);Assert.That(runtime.Interact(Pen),Is.EqualTo("Committed"));
            NearOwner();runtime.AskAboutMissingPen("CH_06");MansionUiTestEntry.FinishSpeech(runtime,hud);FinishReturn();
            Assert.That(runtime.CaptureSession().ItemExchange.Learning,Is.Null);Assert.That(File.Exists(Path.Combine(storage,"profile-v1.dat")),Is.False);
            FrameDiscussion("CH_18");Click("내가 단정했어. 미안해.");FinishDiscussion();EndDiscussion();Assert.That(runtime.CaptureSession().ItemExchange.Learning,Is.Null);
            FrameDiscussion("CH_06");Click("내가 단정했어. 미안해.");FinishDiscussion();EndDiscussion();Ticks(1);
            Assert.That(runtime.CaptureSession().ItemExchange.Learning?.Route,Is.EqualTo("PublicReturn"));Assert.That(ReadLearningProfile(storage).Experience,Is.EqualTo(10));Assert.That(runtime.CaptureSession().Incidents,Is.Empty);yield return null;
        }
        [UnityTest]public IEnumerator ProfileWriteFailureKeepsReturnedCustodyAndRetriesWithoutDuplicatingReward()
        {
            string storage=runtime.UseIsolatedTestStorage();Borrow();NearOwner();runtime.AskWhereToReturn("CH_06");MansionUiTestEntry.FinishSpeech(runtime,hud);
            string blocker=Path.Combine(storage,"profile-v1.dat");Directory.CreateDirectory(blocker);FinishReturn();
            Assert.That(runtime.World.Object(Pen).Owner,Is.EqualTo("CH_06"));Assert.That(runtime.CaptureSession().ItemExchange.Learning,Is.Null);
            Directory.Delete(blocker);Ticks(620);Assert.That(runtime.CaptureSession().ItemExchange.Learning?.Route,Is.EqualTo("ClearInstruction"));Assert.That(ReadLearningProfile(storage).Experience,Is.EqualTo(10));Ticks(620);Assert.That(ReadLearningProfile(storage).Rewards.Length,Is.EqualTo(1));yield return null;
        }
    }
}
