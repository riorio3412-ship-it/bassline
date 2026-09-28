using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using BASSLINE.Core;
using BASSLINE.Investigation;
namespace BASSLINE.Trial
{
 [Serializable] public sealed class SpeechDraft
 {
  public string Id,Speaker,Topic,Text;public string ExaminationId="",ResponseToClaimId="",CounterClaimId="",CounterSpanId="";public ClaimRecord Claim;
  public SpeechDraft Copy()=>new SpeechDraft{Id=Id,Speaker=Speaker,Topic=Topic,Text=Text,ExaminationId=ExaminationId??"",ResponseToClaimId=ResponseToClaimId??"",CounterClaimId=CounterClaimId??"",CounterSpanId=CounterSpanId??"",Claim=Claim?.Copy()};
 }
 [Serializable] public sealed class PublicSpeech
 {
  public SpeechDraft Speech;public string[] ReceivedBy=Array.Empty<string>();public long CourtTick;
  public PublicSpeech Copy()=>new PublicSpeech{Speech=Speech.Copy(),ReceivedBy=(string[])ReceivedBy.Clone(),CourtTick=CourtTick};
 }
 [Serializable] public sealed class SpanReview {public string ClaimId,SpanId,State="Asserted",RequestId;public SpanReview Copy()=>(SpanReview)MemberwiseClone();}
 [Serializable] public sealed class FocusDraft
 {
  public string Owner,ClaimId,SpanId;public long CourtTick,KnowledgeRevision;public int VoiceCursor;
  public FocusDraft Copy()=>(FocusDraft)MemberwiseClone();
 }
 [Serializable] public sealed class SubmissionReceipt
 {
  public string Id,Payload,ClaimId="",SpanId="";public string[] ReceivedBy=Array.Empty<string>();public LogicResult Result;
  public SubmissionReceipt Copy()=>new SubmissionReceipt{Id=Id,Payload=Payload,ClaimId=ClaimId??"",SpanId=SpanId??"",ReceivedBy=(string[])ReceivedBy.Clone(),Result=CloneResult(Result)};
  internal static LogicResult CloneResult(LogicResult r)=>new LogicResult{RequestId=r.RequestId,ResultType=r.ResultType,ReasonCode=r.ReasonCode,Explanation=r.Explanation,KnowledgeRevision=r.KnowledgeRevision,AReadCount=r.AReadCount,ProvenScope=(string[])r.ProvenScope.Clone(),UnsupportedSpanIds=(string[])r.UnsupportedSpanIds.Clone(),MissingPremises=(string[])r.MissingPremises.Clone(),CitedRefs=(string[])r.CitedRefs.Clone(),RootGroups=(string[])r.RootGroups.Clone()};
 }
 [Serializable] public sealed class Ballot {public string Voter,Choice;public Ballot Copy()=>(Ballot)MemberwiseClone();}
 [Serializable] public sealed class TrialSnapshot
 {
  public string TrialId,LoopId,Phase="NotStarted",RuleId="Basic",SelectedTarget="";public long CourtTick;public int VoiceCursor,VoteRound=1,DrawCount;public uint VoteRandomState=1;public string[] VoteCandidates=Array.Empty<string>(),DrawCandidates=Array.Empty<string>();
  public string[] Participants=Array.Empty<string>(),ActiveListeners=Array.Empty<string>(),ActiveClaimIds=Array.Empty<string>(),AnnouncedRequests=Array.Empty<string>();
  public SpeechDraft[] Pending=Array.Empty<SpeechDraft>();public PublicSpeech[] Transcript=Array.Empty<PublicSpeech>();public SpanReview[] Reviews=Array.Empty<SpanReview>();
  public WitnessExaminationState[] Examinations=Array.Empty<WitnessExaminationState>();
  public SpeechDraft[] DeferredSpeeches=Array.Empty<SpeechDraft>();
  public ArgumentChainState[] ArgumentChains=Array.Empty<ArgumentChainState>();
  public ArgumentSelection[] ArgumentSelections=Array.Empty<ArgumentSelection>();
  public TheoryPosition[] TheoryPositions=Array.Empty<TheoryPosition>();
  public TheorySelection[] TheorySelections=Array.Empty<TheorySelection>();
  public JointArgumentState[] JointArguments=Array.Empty<JointArgumentState>();
  public FocusDraft Focus;public SubmissionReceipt[] Submissions=Array.Empty<SubmissionReceipt>();public PauseRecord[] Pauses=Array.Empty<PauseRecord>();public Ballot[] Ballots=Array.Empty<Ballot>();
 }
 public sealed class CourtPublicView
 {
  public string Phase,Topic,Speaker,SpokenText,SelectedTarget;public long CourtTick;public int VoiceCursor,VoteRound;public string[] VoteCandidates=Array.Empty<string>();
  public ClaimRecord[] ActiveClaims=Array.Empty<ClaimRecord>();public SpanReview[] Reviews=Array.Empty<SpanReview>();public string[] AnnouncedTopics=Array.Empty<string>();public PublicSpeech[] History=Array.Empty<PublicSpeech>();public Ballot[] LockedVotes=Array.Empty<Ballot>();
 }
 /// <summary>Public debate authority. No World, Unity, NPC private memory, or adjudication references.
 /// Admission and hearing are checked by the physical integration adapter. P9 production entry remains gated.</summary>
 public sealed partial class TrialDirector
 {
  TrialSnapshot state=new TrialSnapshot();PauseCoordinator pauses=new PauseCoordinator();
  public string Phase=>state.Phase;public bool Focused=>state.Focus!=null;
  static void Id(string id){_=new StableId(id);}
  public string Start(string trialId,string loop,string[] participants,Func<string,bool> actuallyPresentAndEligible)
  {
   if(state.Phase!="NotStarted"||participants==null||participants.Length<2||participants.Distinct().Count()!=participants.Length)return "Unavailable";
   Id(trialId);Id(loop);foreach(var actor in participants){Id(actor);if(!actuallyPresentAndEligible(actor))return "GatheringIncomplete";}
   state=new TrialSnapshot{TrialId=trialId,LoopId=loop,Participants=(string[])participants.Clone(),Phase="Debate"};return "Started";
  }
  public string QueueSpeech(SpeechDraft speech)
  {
   if(state.Phase!="Debate"||speech==null||!state.Participants.Contains(speech.Speaker)||string.IsNullOrWhiteSpace(speech.Text)||string.IsNullOrWhiteSpace(speech.Topic)||speech.Text.Length>6000)return "Unavailable";
   Id(speech.Id);if(state.Pending.Any(x=>x.Id==speech.Id)||state.DeferredSpeeches.Any(x=>x.Id==speech.Id)||state.Transcript.Any(x=>x.Speech.Id==speech.Id))return "DuplicateSpeech";
   if(speech.Claim!=null){var c=speech.Claim;if(c.OwnerId!=speech.Speaker||c.LoopId!=state.LoopId||c.Spans.Length==0||c.Spans.Select(x=>x.Id).Distinct().Count()!=c.Spans.Length||state.Transcript.Any(x=>x.Speech.Claim?.Id==c.Id)||state.Pending.Any(x=>x.Claim?.Id==c.Id)||state.DeferredSpeeches.Any(x=>x.Claim?.Id==c.Id))return "InvalidClaim";Id(c.Id);foreach(var span in c.Spans){Id(span.Id);if(span.FromTick<0||span.ToTick<=span.FromTick)return "InvalidClaim";}}
   state.Pending=state.Pending.Concat(new[]{speech.Copy()}).ToArray();return "Queued";
  }
  public void Pause(string owner,bool acquire)
  {
   Id(owner);bool has=pauses.Capture().Any(x=>x.Owner==owner);if(acquire&&!has)pauses.Acquire(new StableId(owner),ClockScope.Court);if(!acquire&&has)pauses.Release(new StableId(owner));
  }
  public void Step(Func<string,bool> receivedCurrentUtterance)
  {
   if(state.Phase!="Debate"||pauses.IsPaused(ClockScope.Court))return;
   state.CourtTick++;if(state.Pending.Length==0)return;var speech=state.Pending[0];
   if(speech.Claim!=null&&state.ActiveClaimIds.Length>=3&&!state.ActiveClaimIds.Contains(speech.ResponseToClaimId))return;
   var hearing=state.Participants.Where(receivedCurrentUtterance).ToArray();state.ActiveListeners=state.VoiceCursor==0?hearing:state.ActiveListeners.Intersect(hearing).ToArray();
   var positions=StringInfo.ParseCombiningCharacters(speech.Text);if(state.CourtTick%3!=0)return;
   state.VoiceCursor=Math.Min(positions.Length,state.VoiceCursor+1);if(state.VoiceCursor<positions.Length)return;
   var recipients=state.ActiveListeners;
   state.Transcript=state.Transcript.Concat(new[]{new PublicSpeech{Speech=speech.Copy(),ReceivedBy=recipients,CourtTick=state.CourtTick}}).ToArray();state.Pending=state.Pending.Skip(1).ToArray();state.VoiceCursor=0;state.ActiveListeners=Array.Empty<string>();
   UpdateExaminationReceipts(state.Transcript.Last());
   UpdateArgumentReceipts(state.Transcript.Last());
   UpdateJointReceipts(state.Transcript.Last());
   if(speech.Claim!=null){state.ActiveClaimIds=state.ActiveClaimIds.Where(id=>id!=speech.ResponseToClaimId).Concat(new[]{speech.Claim.Id}).ToArray();state.Reviews=state.Reviews.Concat(speech.Claim.Spans.Select(x=>new SpanReview{ClaimId=speech.Claim.Id,SpanId=x.Id})).ToArray();}
  }
  public CourtPublicView Read(string viewer,bool receivingCurrentUtterance)
  {
   if(!state.Participants.Contains(viewer))return new CourtPublicView{Phase="Unavailable"};
   var history=state.Transcript.Where(x=>x.ReceivedBy.Contains(viewer)).ToArray();var allowed=history.Where(x=>x.Speech.Claim!=null).Select(x=>x.Speech.Claim.Id).ToArray();var current=state.Pending.FirstOrDefault();string text="";
   if(current!=null&&receivingCurrentUtterance&&state.ActiveListeners.Contains(viewer)&&state.VoiceCursor>0){var offsets=StringInfo.ParseCombiningCharacters(current.Text);int end=state.VoiceCursor==offsets.Length?current.Text.Length:offsets[state.VoiceCursor];text=current.Text.Substring(0,end);}
   return new CourtPublicView{Phase=state.Phase,CourtTick=state.CourtTick,VoiceCursor=receivingCurrentUtterance?state.VoiceCursor:0,Topic=text.Length>0?current?.Topic??"":"",Speaker=text.Length>0?current?.Speaker??"":"",SpokenText=text,History=history.Select(x=>x.Copy()).ToArray(),ActiveClaims=history.Where(x=>x.Speech.Claim!=null&&state.ActiveClaimIds.Contains(x.Speech.Claim.Id)).Select(x=>x.Speech.Claim.Copy()).ToArray(),Reviews=state.Reviews.Where(x=>allowed.Contains(x.ClaimId)&&(x.RequestId==null||state.Submissions.Any(r=>r.Id==x.RequestId&&r.ReceivedBy.Contains(viewer)))).GroupBy(x=>x.ClaimId+"|"+x.SpanId).Select(x=>x.Last().Copy()).ToArray(),AnnouncedTopics=state.AnnouncedRequests.ToArray(),VoteRound=state.VoteRound,VoteCandidates=(string[])state.VoteCandidates.Clone(),SelectedTarget=state.SelectedTarget,LockedVotes=state.Phase=="VotesLocked"||state.Phase=="VerdictTargetLocked"?state.Ballots.Select(x=>new Ballot{Voter=state.RuleId=="R02"?x.Voter:"",Choice=x.Choice}).ToArray():Array.Empty<Ballot>()};
  }
  public string RetireClaim(string claimId){if(state.Focus!=null||!state.ActiveClaimIds.Contains(claimId))return "Unavailable";state.ActiveClaimIds=state.ActiveClaimIds.Where(x=>x!=claimId).ToArray();return "Retired";}
  // Voting uses each listener's completed public history, including issues moved out of the active three slots.
  // Read filters both original utterances and review receipts by that listener's actual reception.
  public NpcHeardClaim[] ReadHeardClaims(string listener)
  {
   var own=Read(listener,false);
   return own.History.Where(h=>h.Speech.Claim!=null).SelectMany(h=>h.Speech.Claim.Spans.Select(span=>new NpcHeardClaim{
    Id=h.Speech.Claim.Id,SpeakerId=h.Speech.Speaker,ReceiverId=listener,LoopId=state.LoopId,
    Review=state.Examinations.Any(e=>e.StatementId==h.Speech.Claim.Id&&e.SpanId==span.Id&&e.AnswerState=="Correction"&&own.History.Any(answer=>answer.Speech.Id==e.AnswerSpeechId))?"CorrectedBySpeaker":own.Reviews.LastOrDefault(r=>r.ClaimId==h.Speech.Claim.Id&&r.SpanId==span.Id)?.State??"Asserted",
    Span=new NpcTrialSpan{Id=span.Id,SubjectId=span.SubjectId,Predicate=span.Predicate,Value=span.Value,PlaceId=span.PlaceId,FromTick=span.FromTick,ToTick=span.ToTick,Quantifier=span.Quantifier}
   })).ToArray();
  }
  // Spectator projection contains only completed public utterances. It never imports private B into the player ledger.
  public CourtPublicView ReadPublicArchive()
  {
   if(state.Participants.Length==0)return new CourtPublicView{Phase="Unavailable"};
   var view=Read(state.Participants[0],false);var heard=state.Transcript.Where(s=>s.ReceivedBy.Length>0).ToArray();
   var claims=heard.Where(s=>s.Speech.Claim!=null).Select(s=>s.Speech.Claim).ToArray();view.History=heard.Select(s=>s.Copy()).ToArray();
   view.ActiveClaims=claims.Where(c=>state.ActiveClaimIds.Contains(c.Id)).Select(c=>c.Copy()).ToArray();
   view.Reviews=state.Reviews.Where(r=>claims.Any(c=>c.Id==r.ClaimId)&&(r.RequestId==null||state.Submissions.Any(s=>s.Id==r.RequestId&&s.ReceivedBy.Length>0))).GroupBy(r=>r.ClaimId+"|"+r.SpanId).Select(g=>g.Last().Copy()).ToArray();return view;
  }
  public string EnterFocus(IActorKnowledgeQuery query,string claimId,string spanId)
  {
   if(query==null||state.Phase!="Debate"||state.Focus!=null||query.LoopId!=state.LoopId)return "Unavailable";
   var claim=state.Transcript.Where(x=>x.ReceivedBy.Contains(query.OwnerId)).Select(x=>x.Speech.Claim).FirstOrDefault(x=>x!=null&&x.Id==claimId);if(claim==null||!claim.Spans.Any(x=>x.Id==spanId))return "AccessDenied";
   state.Focus=new FocusDraft{Owner=query.OwnerId,ClaimId=claimId,SpanId=spanId,CourtTick=state.CourtTick,VoiceCursor=state.VoiceCursor,KnowledgeRevision=query.Revision};Pause("FOCUS",true);return "Focused";
  }
  public string CancelFocus(string owner){if(state.Focus==null||state.Focus.Owner!=owner)return "Unavailable";state.CourtTick=state.Focus.CourtTick;state.VoiceCursor=state.Focus.VoiceCursor;state.Focus=null;Pause("FOCUS",false);return "Cancelled";}
  public LogicResult Submit(IActorKnowledgeQuery query,string requestId,string action,string rule,string[] refs)
  {
   Id(requestId);string payload=query.OwnerId+"|"+action+"|"+rule+"|"+string.Join(",",refs??Array.Empty<string>());var previous=state.Submissions.FirstOrDefault(x=>x.Id==requestId);
   if(previous!=null){if(!SameSubmissionInput(previous.Payload,payload))throw new InvalidOperationException("Conflicting submission id");return SubmissionReceipt.CloneResult(previous.Result);}
   if(state.Focus==null||state.Focus.Owner!=query.OwnerId||!new[]{"Rebut","Support","LimitScope"}.Contains(action))return Failure(requestId,"AccessDenied");
   if(state.Focus.KnowledgeRevision!=query.Revision)return Failure(requestId,"InputStale");
   var repeated=state.Submissions.FirstOrDefault(r=>r.ClaimId==state.Focus.ClaimId&&r.SpanId==state.Focus.SpanId&&SameSubmissionInput(r.Payload,payload));
   if(repeated!=null){CancelFocus(query.OwnerId);return SubmissionReceipt.CloneResult(repeated.Result);}
   var claim=state.Transcript.Select(x=>x.Speech.Claim).Single(x=>x!=null&&x.Id==state.Focus.ClaimId);
   var result=new LogicResolver().Resolve(query,requestId,rule,claim,state.Focus.SpanId,refs);
   // The chosen action cannot override the resolver. Only returned spans change status.
   foreach(var span in claim.Spans){
    var review=new SpanReview{ClaimId=claim.Id,SpanId=span.Id};
    if(result.UnsupportedSpanIds.Contains(review.SpanId)){review.State=result.ResultType=="Contradict"?"Contradicted":"UnsupportedScope";review.RequestId=requestId;}
    if(result.ProvenScope.Contains(review.SpanId)){review.State="SupportedWithinScope";review.RequestId=requestId;}
    if(review.RequestId!=null)state.Reviews=state.Reviews.Concat(new[]{review}).ToArray();
   }
   state.Submissions=state.Submissions.Concat(new[]{new SubmissionReceipt{Id=requestId,Payload=payload,ClaimId=state.Focus.ClaimId,SpanId=state.Focus.SpanId,ReceivedBy=new[]{query.OwnerId},Result=SubmissionReceipt.CloneResult(result)}}).ToArray();CancelFocus(query.OwnerId);return result;
  }
  static bool SameSubmissionInput(string left,string right)
  {
   var a=(left??"").Split('|');var b=(right??"").Split('|');
   return a.Length==4&&b.Length==4&&a.Take(3).SequenceEqual(b.Take(3))&&a[3].Split(',').Distinct().OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(b[3].Split(',').Distinct().OrderBy(x=>x,StringComparer.Ordinal));
  }
  static LogicResult Failure(string id,string reason)=>new LogicResult{RequestId=id,ResultType="NeedPremise",ReasonCode=reason,Explanation=reason=="InputStale"?"자료가 바뀌었습니다. 주장을 다시 선택하세요.":"접근 가능한 발화와 행동을 선택하세요."};
  public string DeliverSubmission(string requestId,string recipient){var receipt=state.Submissions.SingleOrDefault(x=>x.Id==requestId);if(receipt==null||!state.Participants.Contains(recipient))return "Unavailable";receipt.ReceivedBy=receipt.ReceivedBy.Concat(new[]{recipient}).Distinct().ToArray();return "Received";}
  public KnownRecord[] CheckSource(IActorKnowledgeQuery query,string[] refs)=>state.Focus?.Owner==query.OwnerId?(refs??Array.Empty<string>()).Select(query.Find).Where(x=>x!=null).ToArray():Array.Empty<KnownRecord>();
  public string RequestTestimony(string requester,string witness,string publicQuestion)
  {
   if(state.Focus?.Owner!=requester||!state.Participants.Contains(witness)||string.IsNullOrWhiteSpace(publicQuestion)||publicQuestion.Length>300)return "Unavailable";
   state.AnnouncedRequests=state.AnnouncedRequests.Concat(new[]{witness+" · "+publicQuestion}).ToArray();return "Requested"; // No private answer is revealed; a later actual speech is required.
  }
  public string ConfigureVoting(string rule,uint seed)
  {
   if(state.Phase!="Debate"||state.Ballots.Length!=0||!new[]{"Basic","R01","R02"}.Contains(rule))return "Unavailable";
   state.RuleId=rule;state.VoteRandomState=seed==0?1:seed;return "Configured";
  }
  public string SelectedTarget=>state.SelectedTarget;
  public string RuleId=>state.RuleId;
  public string OpenVoting(){if(state.Phase!="Debate"||state.Focus!=null||JointArgumentBusy||state.Pending.Length!=0)return "Unavailable";state.VoteCandidates=(string[])state.Participants.Clone();state.Phase="Voting";return "Opened";}
  // UI choices remain editable and secret until the simultaneous lock transaction.
  public string ChooseVote(string voter,string choice)
  {
   if(state.Phase!="Voting"||!state.Participants.Contains(voter)||!state.VoteCandidates.Contains(choice))return "Unavailable";
   var old=state.Ballots.FirstOrDefault(x=>x.Voter==voter);if(old!=null)old.Choice=choice;
   else state.Ballots=state.Ballots.Concat(new[]{new Ballot{Voter=voter,Choice=choice}}).ToArray();return "Recorded";
  }
  public string LockVotes(){if(state.Phase!="Voting"||state.Ballots.Length!=state.Participants.Length)return "IncompleteBallots";state.Phase="VotesLocked";return "VotesLocked";}
  public string ResolveVoting()
  {
   if(state.Phase=="VerdictTargetLocked")return "VerdictTargetLocked";
   if(state.Phase!="VotesLocked")return "Unavailable";
   var tallies=state.Ballots.GroupBy(x=>x.Choice).Select(g=>new{Id=g.Key,Count=g.Count()}).ToArray();int max=tallies.Max(x=>x.Count);
   var tied=tallies.Where(x=>x.Count==max).Select(x=>x.Id).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
   if(tied.Length>1&&state.VoteRound==1){state.VoteRound=2;state.VoteCandidates=tied;state.Ballots=Array.Empty<Ballot>();state.Phase="Voting";return "RevoteOpened";}
   if(tied.Length>1){state.DrawCandidates=tied;state.SelectedTarget=tied[DrawIndex(tied.Length)];}else state.SelectedTarget=tied[0];
   state.Phase="VerdictTargetLocked";return "VerdictTargetLocked";
  }
  int DrawIndex(int count){uint x=state.VoteRandomState;x^=x<<13;x^=x>>17;x^=x<<5;state.VoteRandomState=x;state.DrawCount++;return (int)(x%(uint)count);}

  public string Vote(string voter,string choice)
  {
   if(state.Phase!="Voting"||!state.Participants.Contains(voter)||!state.VoteCandidates.Contains(choice))return "Unavailable";var old=state.Ballots.FirstOrDefault(x=>x.Voter==voter);if(old!=null)return old.Choice==choice?"Recorded":"VoteAlreadyLocked";
   state.Ballots=state.Ballots.Concat(new[]{new Ballot{Voter=voter,Choice=choice}}).ToArray();if(state.Ballots.Length==state.Participants.Length)state.Phase="VotesLocked";return "Recorded";
  }
  public TrialSnapshot Capture()=>new TrialSnapshot{JointArguments=state.JointArguments.Select(j=>j.Copy()).ToArray(),TheoryPositions=state.TheoryPositions.Select(p=>p.Copy()).ToArray(),TheorySelections=state.TheorySelections.Select(p=>p.Copy()).ToArray(),ArgumentSelections=state.ArgumentSelections.Select(s=>s.Copy()).ToArray(),ArgumentChains=state.ArgumentChains.Select(c=>c.Copy()).ToArray(),DeferredSpeeches=state.DeferredSpeeches.Select(s=>s.Copy()).ToArray(),Examinations=state.Examinations.Select(e=>e.Copy()).ToArray(),TrialId=state.TrialId,LoopId=state.LoopId,Phase=state.Phase,RuleId=state.RuleId,SelectedTarget=state.SelectedTarget,VoteRound=state.VoteRound,VoteRandomState=state.VoteRandomState,DrawCount=state.DrawCount,VoteCandidates=(string[])state.VoteCandidates.Clone(),DrawCandidates=(string[])state.DrawCandidates.Clone(),CourtTick=state.CourtTick,VoiceCursor=state.VoiceCursor,Participants=(string[])state.Participants.Clone(),ActiveListeners=(string[])state.ActiveListeners.Clone(),ActiveClaimIds=(string[])state.ActiveClaimIds.Clone(),AnnouncedRequests=(string[])state.AnnouncedRequests.Clone(),Pending=state.Pending.Select(x=>x.Copy()).ToArray(),Transcript=state.Transcript.Select(x=>x.Copy()).ToArray(),Reviews=state.Reviews.Select(x=>x.Copy()).ToArray(),Focus=state.Focus?.Copy(),Submissions=state.Submissions.Select(x=>x.Copy()).ToArray(),Pauses=pauses.Capture(),Ballots=state.Ballots.Select(x=>x.Copy()).ToArray()};
  public static TrialDirector Restore(TrialSnapshot snapshot)
  {
   if(snapshot==null||snapshot.CourtTick<0||snapshot.VoiceCursor<0||!new[]{"NotStarted","Debate","Voting","VotesLocked","VerdictTargetLocked"}.Contains(snapshot.Phase))throw new ArgumentException("Invalid trial snapshot");
   if(snapshot.DeferredSpeeches==null)snapshot.DeferredSpeeches=Array.Empty<SpeechDraft>();
   ValidateExaminations(snapshot);
   ValidateArgumentChains(snapshot);
   ValidateTheories(snapshot);
   ValidateJointArguments(snapshot);
   var result=new TrialDirector{state=snapshot,pauses=PauseCoordinator.Restore(snapshot.Pauses)};if(snapshot.Phase!="NotStarted"){
    Id(snapshot.TrialId);Id(snapshot.LoopId);if(snapshot.Participants.Length<2||snapshot.Participants.Distinct().Count()!=snapshot.Participants.Length||snapshot.ActiveClaimIds.Length>3)throw new ArgumentException("Invalid court admission");
    var speeches=snapshot.Pending.Concat(snapshot.DeferredSpeeches).Concat(snapshot.Transcript.Select(x=>x.Speech)).ToArray();if(speeches.Select(x=>x.Id).Distinct().Count()!=speeches.Length||speeches.Any(x=>!snapshot.Participants.Contains(x.Speaker)))throw new ArgumentException("Invalid speech history");
    if(snapshot.Pending.Length==0&&snapshot.VoiceCursor!=0||snapshot.Pending.Length>0&&snapshot.VoiceCursor>StringInfo.ParseCombiningCharacters(snapshot.Pending[0].Text).Length)throw new ArgumentException("Invalid voice cursor");
    var published=snapshot.Transcript.Where(x=>x.Speech.Claim!=null).Select(x=>x.Speech.Claim).ToArray();if(snapshot.ActiveClaimIds.Any(x=>!published.Any(c=>c.Id==x))||snapshot.Transcript.Any(x=>x.CourtTick>snapshot.CourtTick||x.ReceivedBy.Any(p=>!snapshot.Participants.Contains(p))))throw new ArgumentException("Invalid public receipts");
    if(snapshot.Focus!=null&&(!published.Any(x=>x.Id==snapshot.Focus.ClaimId&&x.Spans.Any(y=>y.Id==snapshot.Focus.SpanId))||snapshot.Focus.CourtTick!=snapshot.CourtTick||snapshot.Focus.VoiceCursor!=snapshot.VoiceCursor||!snapshot.Transcript.Any(x=>x.Speech.Claim?.Id==snapshot.Focus.ClaimId&&x.ReceivedBy.Contains(snapshot.Focus.Owner))))throw new ArgumentException("Invalid focus snapshot");
    if((snapshot.Focus!=null)!=snapshot.Pauses.Any(x=>x.Owner=="FOCUS"))throw new ArgumentException("Orphan focus token");
    if(snapshot.Ballots.Select(x=>x.Voter).Distinct().Count()!=snapshot.Ballots.Length||snapshot.Ballots.Any(x=>!snapshot.Participants.Contains(x.Voter)||!snapshot.VoteCandidates.Contains(x.Choice))||(snapshot.Phase=="VotesLocked"||snapshot.Phase=="VerdictTargetLocked")&&snapshot.Ballots.Length!=snapshot.Participants.Length)throw new ArgumentException("Invalid frozen ballots");
   }
   if(!new[]{"Basic","R01","R02"}.Contains(snapshot.RuleId)||snapshot.VoteRound<1||snapshot.VoteRound>2||snapshot.DrawCount<0||snapshot.DrawCount>1||snapshot.VoteRandomState==0||snapshot.VoteCandidates.Distinct().Count()!=snapshot.VoteCandidates.Length||snapshot.VoteCandidates.Any(x=>!snapshot.Participants.Contains(x))||snapshot.DrawCandidates.Any(x=>!snapshot.VoteCandidates.Contains(x))||snapshot.Phase=="VerdictTargetLocked"&&!snapshot.VoteCandidates.Contains(snapshot.SelectedTarget))throw new ArgumentException("Invalid vote resolution");
   result.state=result.Capture();return result;
  }
 }
}


