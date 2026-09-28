using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using BASSLINE.Core;
using BASSLINE.Knowledge;
using BASSLINE.Investigation;
using BASSLINE.Trial;
public sealed class TrialDirectorTests
{
 static readonly string[] Actors={"CH_01","CH_02","CH_03","CH_04"};
 [SetUp] public void Metadata()=>TestContext.Progress.WriteLine("P9 component TestOnly. Production P7/P8 physical incident integration NOT RUN. Editor="+Application.unityVersion+"; URP17.6.0; UTF1.8.0; seed0; Trial_001; source SRC11 P1842-P1877; revision="+File.ReadAllText("Verification/build-revision.txt"));
 static SpeechDraft Speech(int n=1)=>new SpeechDraft{Id="SPEECH_"+n,Speaker="CH_02",Topic="확인 범위",Text="도윤의 위치와 소지에 관한 주장입니다.",Claim=new ClaimRecord{Id="CLAIM_"+n,OwnerId="CH_02",LoopId="K_LOOP_01",Text="위치와 소지",Spans=new[]{new ClaimSpan{Id="SPAN_A",Predicate="AtPlace",SubjectId="CH_04",Value="K_W",FromTick=10,ToTick=11},new ClaimSpan{Id="SPAN_B",Predicate="HeldObject",SubjectId="CH_04",Value="K_BOOK_01",FromTick=10,ToTick=11}}}};
 static TrialDirector Court(){var c=new TrialDirector();Assert.That(c.Start("TRIAL_TEST","K_LOOP_01",Actors,_=>true),Is.EqualTo("Started"));return c;}
 static void Speak(TrialDirector c,int n=1){Assert.That(c.QueueSpeech(Speech(n)),Is.EqualTo("Queued"));for(int i=0;i<180;i++)c.Step(_=>true);}
 static KnowledgeLedger Knowledge(out string record){var k=new KnowledgeLedger(Actors);record=k.Observe("CH_01",new KnownRecord{Kind="Visual",SubjectId="CH_04",Predicate="AtPlace",Value="K_H",PlaceId="K_H",Text="홀에서 직접 봄",Source="CH_01",IdentityConfirmed=true,FromTick=10,ToTick=11,Supports=new[]{"관측 순간의 위치"},DoesNotEstablish=new[]{"관측 밖 시각"}},10);return k;}
 [Test] public void AT_UI_02_UnspokenHidden_MaxThreeClaims_ActualAdmissionRequired()
 {
  var c=new TrialDirector();Assert.That(c.Start("TRIAL_TEST","K_LOOP_01",Actors,a=>a!="CH_04"),Is.EqualTo("GatheringIncomplete"));Assert.That(c.Phase,Is.EqualTo("NotStarted"));c=Court();c.QueueSpeech(Speech());Assert.That(c.Read("CH_01",true).SpokenText,Is.Empty);Assert.That(c.Read("CH_01",true).ActiveClaims,Is.Empty);
  for(int i=0;i<9;i++)c.Step(a=>a!="CH_03");Assert.That(c.Read("CH_01",true).SpokenText,Is.EqualTo(Speech().Text.Substring(0,3)));Assert.That(c.Read("CH_03",false).SpokenText,Is.Empty);
  for(int i=0;i<180;i++)c.Step(a=>a!="CH_03");Assert.That(c.Read("CH_03",false).History,Is.Empty);Speak(c,2);Speak(c,3);Speak(c,4);Assert.That(c.Read("CH_01",true).ActiveClaims.Length,Is.EqualTo(3));Assert.That(c.Capture().Pending.Single().Id,Is.EqualTo("SPEECH_4"));c.RetireClaim("CLAIM_1");for(int i=0;i<180;i++)c.Step(_=>true);Assert.That(c.Read("CH_01",true).ActiveClaims.Length,Is.EqualTo(3));Assert.That(c.Read("CH_01",true).History.Length,Is.EqualTo(4));
 }
 [Test] public void AT_TIME_02_FocusNestedNoteRestoresExactCourtAndVoiceCursor()
 {
  var c=Court();Speak(c);c.QueueSpeech(Speech(2));for(int i=0;i<9;i++)c.Step(_=>true);var k=Knowledge(out var record);Assert.That(c.EnterFocus(k.For("CH_01"),"CLAIM_1","SPAN_A"),Is.EqualTo("Focused"));c.Pause("K_NOTE",true);var frozen=c.Capture();for(int i=0;i<100;i++)c.Step(_=>true);Assert.That(c.Capture().CourtTick,Is.EqualTo(frozen.CourtTick));Assert.That(c.Capture().VoiceCursor,Is.EqualTo(frozen.VoiceCursor));
  c=TrialDirector.Restore(JsonUtility.FromJson<TrialSnapshot>(JsonUtility.ToJson(frozen)));c.CancelFocus("CH_01");c.Step(_=>true);Assert.That(c.Capture().CourtTick,Is.EqualTo(frozen.CourtTick));c.Pause("K_NOTE",false);c.Step(_=>true);Assert.That(c.Capture().CourtTick,Is.EqualTo(frozen.CourtTick+1));
  var bad=TrialDirector.Restore(frozen).Capture();bad.Pauses=Array.Empty<PauseRecord>();Assert.Throws<ArgumentException>(()=>TrialDirector.Restore(bad));
 }
 [Test] public void AT_INFO_03_FocusFiveActions_ScopedCounter_ReceiptAndIdempotence()
 {
  var c=Court();Speak(c);var k=Knowledge(out var record);var q=k.For("CH_01");c.EnterFocus(q,"CLAIM_1","SPAN_A");Assert.That(c.CheckSource(q,new[]{record}).Single().Id,Is.EqualTo(record));int count=c.Read("CH_01",true).History.Length;Assert.That(c.RequestTestimony("CH_01","CH_03","관측 시각을 말씀해 주세요."),Is.EqualTo("Requested"));Assert.That(c.Read("CH_01",true).History.Length,Is.EqualTo(count));
  var r=c.Submit(q,"SUBMISSION_1","Rebut","LR03",new[]{record});Assert.That(r.ResultType,Is.EqualTo("Contradict"));Assert.That(r.AReadCount,Is.Zero);var view=c.Read("CH_01",true);Assert.That(view.Reviews.Single(x=>x.SpanId=="SPAN_A").State,Is.EqualTo("Contradicted"));Assert.That(view.Reviews.Single(x=>x.SpanId=="SPAN_B").State,Is.EqualTo("Asserted"));Assert.That(c.Read("CH_02",true).Reviews.Single(x=>x.SpanId=="SPAN_A").State,Is.EqualTo("Asserted"));c.DeliverSubmission("SUBMISSION_1","CH_02");Assert.That(c.Read("CH_02",true).Reviews.Single(x=>x.SpanId=="SPAN_A").State,Is.EqualTo("Contradicted"));
  Assert.That(c.Submit(q,"SUBMISSION_1","Rebut","LR03",new[]{record}).ResultType,Is.EqualTo("Contradict"));Assert.That(c.Capture().Submissions.Length,Is.EqualTo(1));Assert.Throws<InvalidOperationException>(()=>c.Submit(q,"SUBMISSION_1","Support","LR01",new[]{record}));
  c.EnterFocus(q,"CLAIM_1","SPAN_B");Assert.That(c.Submit(q,"SUBMISSION_2","Support","LR04",new[]{record}).ResultType,Is.EqualTo("NeedPremise"));c.EnterFocus(q,"CLAIM_1","SPAN_B");Assert.That(c.Submit(q,"SUBMISSION_3","LimitScope","LR08",new[]{record}).ResultType,Is.EqualTo("NeedPremise"));
  c.EnterFocus(q,"CLAIM_1","SPAN_A");Assert.That(c.Submit(q,"SUBMISSION_4","Rebut","UNKNOWN",new[]{record}).ReasonCode,Is.EqualTo("UnknownRule"));
 }
 [Test] public void AT_SAVE_03_SecretBallotsFrozenBeforeReveal_ImmutableRestore()
 {
  var c=Court();Speak(c);Assert.That(c.OpenVoting(),Is.EqualTo("Opened"));Assert.That(c.Vote("CH_01","CH_04"),Is.EqualTo("Recorded"));Assert.That(c.Read("CH_02",true).LockedVotes,Is.Empty);Assert.That(c.Vote("CH_01","CH_02"),Is.EqualTo("VoteAlreadyLocked"));foreach(var voter in Actors.Skip(1))c.Vote(voter,"CH_04");Assert.That(c.Read("CH_01",true).LockedVotes.Length,Is.EqualTo(4));var s=c.Capture();string json=JsonUtility.ToJson(s);var restored=TrialDirector.Restore(s);s.Ballots[0].Choice="CH_02";Assert.That(JsonUtility.ToJson(restored.Capture()),Is.EqualTo(json));Assert.That(restored.Phase,Is.EqualTo("VotesLocked"));Assert.That(restored.Vote("CH_01","CH_02"),Is.EqualTo("Unavailable"));
 }
}
