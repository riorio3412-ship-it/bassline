using System;
using System.Linq;
using System.Collections.Generic;
using BASSLINE.Core;
namespace BASSLINE.Trial
{
    [Serializable] public sealed class RevealEvent
    {
        public string Id,CaseId,Heading,Text,PlaceId;public long Tick;public bool RevealApproved,Protected,HasRecordedPath;
        public RevealEvent Copy()=>(RevealEvent)MemberwiseClone();
    }
    // Supplied by the trusted adjudication adapter, never by the player UI or knowledge resolver.
    public sealed class VerdictInput
    {
        public string CaseId,CampaignId,ChapterId,ActualCulprit,PlayerId="CH_01";public string[] LivingResidents=Array.Empty<string>(),ConfirmedDead=Array.Empty<string>();
        public RevealEvent[] Events=Array.Empty<RevealEvent>();public int ClockVersion;public uint DrawSeed=1;public bool CaseBundleClosed;
        public string[] ClosedCaseIds=Array.Empty<string>();
    }
    [Serializable] public sealed class SettlementPlan
    {
        public string Id,CaseId,CampaignId,LoopId,ChapterId,RuleId,Target,ActualCulprit,PlayerChoice,PlayerId="CH_01",RandomExecution="";
        public int ClockVersion;public bool Correct,Applied,TransitionApplied;public uint RandomState;public int DrawCount,Residual;
        public string[] LivingBefore=Array.Empty<string>(),ConfirmedDead=Array.Empty<string>(),Executed=Array.Empty<string>(),Escaped=Array.Empty<string>(),EscapeRewardEligible=Array.Empty<string>(),DrawCandidates=Array.Empty<string>();
        public string[] ClosedCaseIds=Array.Empty<string>();
        public SettlementPlan Copy()=>new SettlementPlan{ClockVersion=ClockVersion,Id=Id,CaseId=CaseId,CampaignId=CampaignId,LoopId=LoopId,ChapterId=ChapterId,RuleId=RuleId,Target=Target,ActualCulprit=ActualCulprit,PlayerChoice=PlayerChoice,PlayerId=PlayerId,RandomExecution=RandomExecution,Correct=Correct,Applied=Applied,TransitionApplied=TransitionApplied,RandomState=RandomState,DrawCount=DrawCount,Residual=Residual,LivingBefore=(string[])LivingBefore.Clone(),ConfirmedDead=(string[])ConfirmedDead.Clone(),Executed=(string[])Executed.Clone(),Escaped=(string[])Escaped.Clone(),EscapeRewardEligible=(string[])EscapeRewardEligible.Clone(),DrawCandidates=(string[])DrawCandidates.Clone(),ClosedCaseIds=(string[])(ClosedCaseIds??Array.Empty<string>()).Clone()};
    }
    [Serializable] public sealed class EvaluationObligation
    {
        public string Id,Area;public int Weight;public bool Applicable=true;public ObligationProof[] Alternatives=Array.Empty<ObligationProof>();
        public EvaluationObligation Copy()=>new EvaluationObligation{Id=Id,Area=Area,Weight=Weight,Applicable=Applicable,Alternatives=Alternatives.Select(x=>x.Copy()).ToArray()};
    }
    [Serializable] public sealed class ObligationProof
    {
        public string Id;public double Completion;public string[] Basis=Array.Empty<string>();
        public ObligationProof Copy()=>new ObligationProof{Id=Id,Completion=Completion,Basis=(string[])Basis.Clone()};
    }
    public sealed class EvaluationInput
    {
        public string Version="V11",Complexity="Standard";public EvaluationObligation[] Obligations=Array.Empty<EvaluationObligation>();
        public bool EssentialCausality,IndependentConfirmation,UnresolvedDecisiveError,PersonalReasonedCorrect;
        public double NoveltyFactor=1;public string[] UniqueLearningCredits=Array.Empty<string>();public int BonusPerCredit=10;
    }
    [Serializable] public sealed class CaseEvaluation
    {
        public string Version,Rank,PersonalRank,Complexity,Explanation;public int Score,ApplicableMax,BaseExperience,Bonus,Entitlement;public double RawEarned,NoveltyFactor;
        public EvaluationObligation[] Obligations=Array.Empty<EvaluationObligation>();
        public CaseEvaluation Copy()=>new CaseEvaluation{Version=Version,Rank=Rank,PersonalRank=PersonalRank,Complexity=Complexity,Explanation=Explanation,Score=Score,ApplicableMax=ApplicableMax,BaseExperience=BaseExperience,Bonus=Bonus,Entitlement=Entitlement,RawEarned=RawEarned,NoveltyFactor=NoveltyFactor,Obligations=Obligations.Select(x=>x.Copy()).ToArray()};
    }
    [Serializable] public sealed class ProfileRewardEntry
    {
        public string Signature,RewardId,EvaluationVersion;public int Entitlement,AppliedExperience,PointGrant;public bool Applied;
        public ProfileRewardEntry Copy()=>(ProfileRewardEntry)MemberwiseClone();
    }
    [Serializable] public sealed class TrialProfile
    {
        public string ProfileId="PROFILE_01";public long Revision;public int Level=1,Experience,SkillPoints;public ProfileRewardEntry[] Rewards=Array.Empty<ProfileRewardEntry>();
        public TrialProfile Copy()=>new TrialProfile{ProfileId=ProfileId,Revision=Revision,Level=Level,Experience=Experience,SkillPoints=SkillPoints,Rewards=Rewards.Select(x=>x.Copy()).ToArray()};
    }
    [Serializable] public sealed class TrialEndgameSnapshot
    {
        public string Phase="AwaitingVerdict";public SettlementPlan Plan;public RevealEvent[] Reveal=Array.Empty<RevealEvent>();public int RevealCursor,RewardAppliedExperience;public CaseEvaluation Evaluation;public string RewardId="";
        public TrialEndgameSnapshot Copy()=>new TrialEndgameSnapshot{Phase=Phase,Plan=Plan?.Copy(),Reveal=Reveal.Select(x=>x.Copy()).ToArray(),RevealCursor=RevealCursor,RewardAppliedExperience=RewardAppliedExperience,Evaluation=Evaluation?.Copy(),RewardId=RewardId};
    }
    /// <summary>Verdict -> immutable archive -> evaluation -> durable profile reward -> settlement -> chapter/loop.
    /// No reference to World, KnowledgeLedger or Unity. Integrators persist each successful transaction before showing completion.</summary>
    public sealed class TrialEndgame
    {
        TrialEndgameSnapshot state=new TrialEndgameSnapshot();
        public string Phase=>state.Phase;
        public string Transition=>state.Plan?.Applied==true?(state.Plan.Residual<=3?"Loop":"NextChapter"):"";
        static void Id(string id){_=new StableId(id);}
        public string CommitVerdict(TrialDirector trial,VerdictInput input)
        {
            if(state.Plan!=null)return state.Plan.CaseId==input?.CaseId?"VerdictCommitted":"Unavailable";
            if(trial==null||input==null||trial.Phase!="VerdictTargetLocked"||!input.CaseBundleClosed||!WorldTimeLabel.IsSupported(input.ClockVersion))return "Unavailable";
            Id(input.CaseId);Id(input.CampaignId);Id(input.ChapterId);Id(input.ActualCulprit);
            var closed=input.ClosedCaseIds??Array.Empty<string>();if(closed.Length==0)closed=new[]{input.CaseId};
            if(!closed.Contains(input.CaseId)||closed.Distinct().Count()!=closed.Length)return "InvalidCaseBundle";foreach(string id in closed)Id(id);
            var court=trial.Capture();var residents=input.LivingResidents.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            if(residents.Length<2||residents.Distinct().Count()!=residents.Length||!residents.Contains(input.ActualCulprit)||!residents.Contains(court.SelectedTarget)||!residents.SequenceEqual(court.Participants.OrderBy(x=>x,StringComparer.Ordinal))||input.ConfirmedDead.Intersect(residents).Any())return "InvalidResidents";
            var plan=new SettlementPlan{ClockVersion=input.ClockVersion,Id="SETTLEMENT_"+input.CaseId,CaseId=input.CaseId,CampaignId=input.CampaignId,LoopId=court.LoopId,ChapterId=input.ChapterId,RuleId=court.RuleId,Target=court.SelectedTarget,ActualCulprit=input.ActualCulprit,PlayerId=input.PlayerId,PlayerChoice=court.Ballots.FirstOrDefault(x=>x.Voter==input.PlayerId)?.Choice??"",Correct=court.SelectedTarget==input.ActualCulprit,LivingBefore=residents,ConfirmedDead=(string[])input.ConfirmedDead.Clone(),RandomState=input.DrawSeed==0?1:input.DrawSeed};
            if(plan.RuleId=="R01"){
                plan.Executed=new[]{plan.Target};if(!plan.Correct)plan.Escaped=new[]{plan.ActualCulprit};
            }else if(plan.Correct)plan.Executed=new[]{plan.ActualCulprit};
            else{
                plan.Escaped=new[]{plan.ActualCulprit};plan.DrawCandidates=residents.Where(x=>x!=plan.ActualCulprit).ToArray();
                uint x=plan.RandomState;x^=x<<13;x^=x>>17;x^=x<<5;plan.RandomState=x;plan.DrawCount=1;plan.RandomExecution=plan.DrawCandidates[x%(uint)plan.DrawCandidates.Length];plan.Executed=new[]{plan.RandomExecution};
            }
            plan.EscapeRewardEligible=plan.Escaped.Where(x=>x!=input.PlayerId).ToArray();
            plan.ClosedCaseIds=(string[])closed.Clone();
            // A reveal event must belong to this closed bundle and be explicitly approved. Unrelated secrets cannot ride along.
            var reveal=input.Events.Where(e=>closed.Contains(e.CaseId)&&e.RevealApproved&&!e.Protected).OrderBy(e=>e.Tick).ThenBy(e=>e.Id,StringComparer.Ordinal).Select(e=>e.Copy()).ToArray();
            if(reveal.Any(e=>string.IsNullOrWhiteSpace(e.Id)||e.Tick<0)||reveal.Select(e=>e.Id).Distinct().Count()!=reveal.Length)return "InvalidReveal";
            state.Plan=plan;state.Reveal=reveal;state.Phase="Verdict";return "VerdictCommitted";
        }
        public RevealEvent[] ReadTruth()=>state.Plan==null?Array.Empty<RevealEvent>():state.Reveal.Select(x=>x.Copy()).ToArray();
        public string BeginTruth(){if(state.Phase!="Verdict")return "Unavailable";state.Phase="Truth";return "Truth";}
        public string AdvanceTruth(){if(state.Phase!="Truth")return "Unavailable";state.RevealCursor=Math.Min(state.Reveal.Length,state.RevealCursor+1);return "Advanced";}
        public string FinishTruth(){if(state.Phase!="Truth")return "Unavailable";state.RevealCursor=state.Reveal.Length;state.Phase="AwaitingEvaluation";return "AwaitingEvaluation";}
        static readonly Dictionary<string,int> AreaCaps=new Dictionary<string,int>{{"Investigation",200},{"Logic",200},{"Discovery",150},{"Verification",150},{"Court",200},{"Intervention",100}};
        public string Evaluate(EvaluationInput input)
        {
            if(state.Evaluation!=null)return "Evaluated";
            if(state.Phase!="AwaitingEvaluation"||input==null)return "Unavailable";
            var obligations=input.Obligations;
            if(input.NoveltyFactor<0||input.NoveltyFactor>1||double.IsNaN(input.NoveltyFactor)||input.BonusPerCredit<0||obligations.Length==0||obligations.Any(o=>o==null||string.IsNullOrWhiteSpace(o.Id)||!AreaCaps.ContainsKey(o.Area)||o.Weight<=0)||obligations.Select(o=>o.Id).Distinct().Count()!=obligations.Length||obligations.GroupBy(o=>o.Area).Any(g=>g.Sum(o=>o.Weight)>AreaCaps[g.Key]))return "InvalidObligations";
            if(obligations.Any(o=>o.Alternatives.Any(p=>p==null||!new[]{0d,.5d,1d}.Contains(p.Completion)||p.Completion>0&&(p.Basis==null||p.Basis.Length==0||p.Basis.Any(string.IsNullOrWhiteSpace)))))return "InvalidProof";
            int denominator=obligations.Where(o=>o.Applicable).Sum(o=>o.Weight);if(denominator==0)return "UseDailyEvaluation";
            double earned=obligations.Where(o=>o.Applicable).Sum(o=>o.Weight*o.Alternatives.Select(p=>p.Completion).DefaultIfEmpty(0).Max());
            int score=(int)Math.Round(earned/denominator*1000,MidpointRounding.AwayFromZero);string rank=Rank(score,input.EssentialCausality,input.IndependentConfirmation,input.UnresolvedDecisiveError);
            string personal=rank;bool correct=state.Plan.Correct;string explanation="";
            if(!correct){rank=state.Plan.RuleId=="R01"?Lower(rank,"B"):"E";explanation=state.Plan.RuleId=="R01"?"R01 다수 지목 결과와 실제 책임자 설명을 구분합니다.":"집단 판결 오류. 영역별 기여는 유지됩니다.";}
            if(!correct&&!input.PersonalReasonedCorrect)personal=rank;
            int baseXp;switch(input.Complexity){case "Tutorial":baseXp=200;break;case "Standard":baseXp=500;break;case "Complex":baseXp=800;break;case "Advanced":baseXp=1100;break;default:return "InvalidComplexity";}
            int bonus=Math.Min(baseXp/5,input.UniqueLearningCredits.Distinct().Count()*input.BonusPerCredit);
            double modifier=Math.Max(Modifier(rank),!correct&&input.PersonalReasonedCorrect&&state.Plan.PlayerChoice==state.Plan.ActualCulprit?Modifier(personal):Modifier(rank));
            state.Evaluation=new CaseEvaluation{Version=input.Version,Rank=rank,PersonalRank=personal,Complexity=input.Complexity,Explanation=explanation,Score=score,RawEarned=earned,ApplicableMax=denominator,BaseExperience=baseXp,Bonus=bonus,NoveltyFactor=input.NoveltyFactor,Entitlement=(int)Math.Round(baseXp*modifier*input.NoveltyFactor,MidpointRounding.AwayFromZero)+bonus,Obligations=obligations.Select(o=>o.Copy()).ToArray()};
            state.Phase="Evaluation";return "Evaluated";
        }
        static string Rank(int score,bool essential,bool independent,bool error){string rank=score>=900?"S":score>=800?"A":score>=650?"B":score>=500?"C":"D";if(!essential||error)rank=Lower(rank,"B");else if(!independent)rank=Lower(rank,"A");return rank;}
        static string Lower(string rank,string ceiling)=>"SABCDE".IndexOf(rank,StringComparison.Ordinal)<"SABCDE".IndexOf(ceiling,StringComparison.Ordinal)?ceiling:rank;
        static double Modifier(string rank){switch(rank){case "S":return 1.5;case "A":return 1.25;case "B":return 1;case "C":return .8;case "D":return .5;default:return .25;}}
        /// <summary>Build a new immutable profile candidate. Persist it atomically BEFORE AcceptReward.
        /// Signature omits BranchID so retrying an old campaign save grants only a higher entitlement's difference.</summary>
        public TrialProfile PrepareReward(TrialProfile current)
        {
            if(state.Evaluation==null||state.Plan==null||current==null)throw new InvalidOperationException("Evaluation required");
            ValidateProfile(current);var next=current.Copy();string signature=Signature();int previous=next.Rewards.Where(r=>r.Signature==signature&&r.Applied).Select(r=>r.Entitlement).DefaultIfEmpty(0).Max();int delta=Math.Max(0,state.Evaluation.Entitlement-previous);
            if(delta==0)return next;
            string rewardId="REWARD_"+state.Plan.CaseId+"_"+state.Evaluation.Entitlement;int points=0;next.Experience=checked(next.Experience+delta);
            while(next.Level<20&&next.Experience>=300+100*(next.Level-1)){next.Experience-=300+100*(next.Level-1);next.Level++;next.SkillPoints++;points++;}
            next.Rewards=next.Rewards.Concat(new[]{new ProfileRewardEntry{Signature=signature,RewardId=rewardId,EvaluationVersion=state.Evaluation.Version,Entitlement=state.Evaluation.Entitlement,AppliedExperience=delta,PointGrant=points,Applied=true}}).ToArray();next.Revision++;
            return next;
        }
        public static TrialProfile PrepareReturnLearning(TrialProfile current)
        {
            ValidateProfile(current);var next=current.Copy();const string signature="LIFE_IG01_RETURN_MEANING",reward="LIFE_IG01_RETURN_MEANING_V1";
            if(next.Rewards.Any(r=>r.Signature==signature&&r.Applied))return next;
            const int bonus=10;int points=0;next.Experience=checked(next.Experience+bonus);
            while(next.Level<20&&next.Experience>=300+100*(next.Level-1)){next.Experience-=300+100*(next.Level-1);next.Level++;next.SkillPoints++;points++;}
            next.Rewards=next.Rewards.Concat(new[]{new ProfileRewardEntry{Signature=signature,RewardId=reward,EvaluationVersion="IG01_V1",Entitlement=bonus,AppliedExperience=bonus,PointGrant=points,Applied=true}}).ToArray();next.Revision++;return next;
        }
        string Signature()=>state.Plan.CampaignId+"|"+state.Plan.LoopId+"|"+state.Plan.CaseId;
        public string AcceptReward(TrialProfile durablySaved)
        {
            if(state.Phase!="Evaluation"&&state.Phase!="RewardApplied")return "Unavailable";ValidateProfile(durablySaved);
            var receipt=durablySaved.Rewards.Where(r=>r.Signature==Signature()&&r.Applied&&r.Entitlement>=state.Evaluation.Entitlement).OrderBy(r=>r.Entitlement).FirstOrDefault();
            if(receipt==null&&state.Evaluation.Entitlement>0)return "RewardNotCommitted";
            state.RewardId=receipt?.RewardId??"ZERO_REWARD";state.RewardAppliedExperience=receipt?.AppliedExperience??0;state.Phase="RewardApplied";return "RewardApplied";
        }
        public string ApplySettlement(Func<SettlementPlan,bool> atomicCommit)
        {
            if(state.Plan?.Applied==true)return "SettlementApplied";
            if(state.Phase!="RewardApplied"||atomicCommit==null)return "Unavailable";
            var plan=state.Plan.Copy();plan.Residual=plan.LivingBefore.Except(plan.Executed).Except(plan.Escaped).Count();plan.Applied=true;
            if(!atomicCommit(plan.Copy()))return "SettlementPending";state.Plan=plan;state.Phase="Settlement";return "SettlementApplied";
        }
        public string CompleteTransition(Func<SettlementPlan,string,bool> atomicCommit)
        {
            if(state.Plan?.TransitionApplied==true)return state.Phase;
            if(state.Phase!="Settlement"||!state.Plan.Applied||atomicCommit==null)return "Unavailable";
            string transition=Transition;var plan=state.Plan.Copy();plan.TransitionApplied=true;
            if(!atomicCommit(plan.Copy(),transition))return "TransitionPending";state.Plan=plan;state.Phase=transition=="Loop"?"LoopReady":"NextChapterReady";return state.Phase;
        }
        public TrialEndgameSnapshot Capture()=>state.Copy();
        public static TrialEndgame Restore(TrialEndgameSnapshot snapshot)
        {
            if(snapshot==null||!new[]{"AwaitingVerdict","Verdict","Truth","AwaitingEvaluation","Evaluation","RewardApplied","Settlement","LoopReady","NextChapterReady"}.Contains(snapshot.Phase))throw new ArgumentException("Invalid endgame phase");
            var s=snapshot.Copy();if(s.Phase=="AwaitingVerdict"){if(s.Plan!=null||s.Evaluation!=null||s.Reveal.Length!=0)throw new ArgumentException("Premature verdict data");return new TrialEndgame{state=s};}
            var p=s.Plan;if(p!=null&&p.ClosedCaseIds.Length==0)p.ClosedCaseIds=new[]{p.CaseId};
            if(p==null||p.ClosedCaseIds.Distinct().Count()!=p.ClosedCaseIds.Length||!p.ClosedCaseIds.Contains(p.CaseId)||!WorldTimeLabel.IsSupported(p.ClockVersion)||p.LivingBefore.Distinct().Count()!=p.LivingBefore.Length||p.Executed.Length!=1||p.Escaped.Length>1||p.Executed.Intersect(p.Escaped).Any()||p.Executed.Concat(p.Escaped).Any(x=>!p.LivingBefore.Contains(x))||p.ConfirmedDead.Intersect(p.LivingBefore).Any()||!p.LivingBefore.Contains(p.ActualCulprit)||!p.LivingBefore.Contains(p.Target)||p.Correct!=(p.Target==p.ActualCulprit)||s.RevealCursor<0||s.RevealCursor>s.Reveal.Length||s.Reveal.Any(e=>!p.ClosedCaseIds.Contains(e.CaseId)||!e.RevealApproved||e.Protected))throw new ArgumentException("Invalid settlement or reveal");
            foreach(string id in p.ClosedCaseIds)Id(id);
            if(p.Applied&&(p.Residual!=p.LivingBefore.Except(p.Executed).Except(p.Escaped).Count()||s.Phase!="Settlement"&&s.Phase!="LoopReady"&&s.Phase!="NextChapterReady")||p.TransitionApplied&&(s.Phase!="LoopReady"&&s.Phase!="NextChapterReady")||s.Phase=="LoopReady"&&p.Residual>3||s.Phase=="NextChapterReady"&&p.Residual<=3)throw new ArgumentException("Invalid transition");
            if(new[]{"Evaluation","RewardApplied","Settlement","LoopReady","NextChapterReady"}.Contains(s.Phase)&&s.Evaluation==null||new[]{"RewardApplied","Settlement","LoopReady","NextChapterReady"}.Contains(s.Phase)&&string.IsNullOrEmpty(s.RewardId))throw new ArgumentException("Missing evaluation/reward receipt");
            return new TrialEndgame{state=s};
        }
        static void ValidateProfile(TrialProfile p){if(p==null||string.IsNullOrWhiteSpace(p.ProfileId)||p.Level<1||p.Level>20||p.Experience<0||p.SkillPoints<0||p.Revision<0||p.Rewards==null||p.Rewards.Any(r=>r==null||r.Entitlement<0||r.AppliedExperience<0||r.PointGrant<0||string.IsNullOrEmpty(r.Signature))||p.Rewards.GroupBy(r=>r.Signature+"|"+r.RewardId).Any(g=>g.Count()>1))throw new ArgumentException("Invalid profile ledger");}
    }
}
