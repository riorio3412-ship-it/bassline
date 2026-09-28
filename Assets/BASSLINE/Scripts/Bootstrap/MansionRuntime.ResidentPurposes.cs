using System;
using System.Linq;
using BASSLINE.Core;
using BASSLINE.NPC;
using BASSLINE.AuthoringData;
using BASSLINE.Save;
using BASSLINE.Knowledge;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        MansionResidentPurpose[] purposeAuthors=Array.Empty<MansionResidentPurpose>();
        void InitializeResidentPurposes()=>purposeAuthors=Bodies.Select(b=>b.GetComponent<MansionResidentPurpose>()).Where(p=>p).ToArray();
        ResidentIntentRandom IntentRandom(string actor)
        {
            var random=residentIntents.Random.FirstOrDefault(r=>r.OwnerId==actor);
            if(random!=null)return random;
            uint seed=2166136261;foreach(char c in actor)seed=unchecked((seed^c)*16777619);seed^=unchecked((uint)World.Loop*7919);
            random=new ResidentIntentRandom{OwnerId=actor,State=seed==0?1:seed};residentIntents.Random=residentIntents.Random.Concat(new[]{random}).ToArray();return random;
        }
        void SelectResidentPurposes()
        {
            foreach(var author in purposeAuthors.Where(p=>p&&p.isActiveAndEnabled).OrderBy(p=>p.GetComponent<FixtureActorBody>().ActorId,StringComparer.Ordinal)){
                string actor=author.GetComponent<FixtureActorBody>().ActorId;
                if(!IntentActorAvailable(actor)||residentIntents.Intents.Any(t=>ActiveIntent(t)&&t.Plan.OwnerId==actor))continue;
                var own=Knowledge.For(actor);var known=own.Records();
                // Stable IDs come only from this person's observations/receipts, not the live cast positions.
                var candidates=known.SelectMany(r=>new[]{r.SubjectId,r.Source}).Where(id=>id!=actor&&id!=null&&bodies.ContainsKey(id)).Distinct().OrderBy(id=>id,StringComparer.Ordinal).ToArray();
                var drafts=new System.Collections.Generic.List<ResidentIntentState>();var random=IntentRandom(actor);
                foreach(var definition in author.Goals.OrderBy(d=>d.Id,StringComparer.Ordinal)){
                    ResidentPurposeDefinition.Validate(definition);
                    foreach(string target in candidates){
                        var plate=IncidentRulePlate(author.RulePlateId);var action=plate?plate.Definition:null;
                        var rule=action==null?null:known.FirstOrDefault(r=>r.Direct&&r.Kind=="Document"&&r.Predicate=="IncidentActionRule"&&r.SubjectId==action.Id&&r.Value==action.Revision&&r.Text==action.PublicRule&&r.ProvenanceKey=="ACTION_RULE_"+action.Id+"_"+action.Revision);
                        bool registered=plate&&plate.isActiveAndEnabled&&action.ReviewStatus=="Reviewed"&&action.MotiveTags.Contains(definition.MotiveTag)&&rule!=null;
                        var binding=new ResidentPurposeBinding{Definition=definition.Copy(),TargetId=target,PlateId=author.RulePlateId,ObjectId=author.ObjectId,ToolNode=author.ToolNode,ContactNode=author.ContactNode,MapVersion=Layout.MapVersion,RuleRecordId=rule?.Id??"",AppliedTaboos=action==null?Array.Empty<string>():author.PersonalTaboos.Intersect(action.TabooTags).ToArray()};
                        var previous=residentIntents.Intents.LastOrDefault(t=>t.Plan.OwnerId==actor&&t.PurposeId==definition.Id&&t.Purpose?.TargetId==target);
                        string id="INTENT_L"+World.Loop+"_"+(residentIntents.Sequence+1);
                        var task=new ResidentPurposePlanner().Consider(own,binding,Social.Experiences(actor),id,World.Tick,random.State,registered,previous?.Plan);
                        if(task==null)continue;
                        drafts.Add(task);
                    }
                }
                if(drafts.Count==0)continue;
                int Score(ResidentIntentState candidate)=>candidate.Plan.Alternatives.Single(a=>a.Action==candidate.Plan.Action&&a.TargetId==candidate.Plan.TargetId).Score;
                int best=drafts.Max(Score);var tied=drafts.Where(t=>Score(t)==best).OrderBy(t=>t.PurposeId,StringComparer.Ordinal).ThenBy(t=>t.Purpose.TargetId,StringComparer.Ordinal).ToArray();
                uint next=random.State;int choice=0;
                if(tied.Length>1){next=NextIntentRandom(next);choice=(int)(next%(uint)tied.Length);}
                var chosen=tied[choice];
                if(chosen.Plan.RandomAfter!=random.State)next=NextIntentRandom(next);
                chosen.Plan.RandomAfter=next;random.State=next;residentIntents.Sequence++;residentIntents.Intents=residentIntents.Intents.Concat(new[]{chosen}).ToArray();
                World.Emit("ResidentIntentSelected",actor,chosen.Plan.TargetId,chosen.Plan.Id+"|"+chosen.Plan.Action);
                if(chosen.Plan.Action=="Withdraw")EndResidentIntent(chosen,"Withdrawn","ChoseDistance");
                else if(chosen.Plan.Action=="Execute"){
                    var b=chosen.Purpose;string result=BindIncidentPlan(chosen.Plan,b.PlateId,b.ObjectId,b.ToolNode,b.ContactNode,b.Definition.MotiveTag,b.AppliedTaboos,chosen.DeadlineTick,b);
                    if(result=="Configured")chosen.Phase="Executing";
                    else EndResidentIntent(chosen,"Withdrawn",result);
                }
            }
        }
        static uint NextIntentRandom(uint value){value^=value<<13;value^=value>>17;value^=value<<5;return value;}
        void AdvancePurposeRuleReading()
        {
            // Any resident deliberately examining a visible plate may read it.
            // Registry membership alone never reveals its existence or content.
            ruleReadersThisTick.Clear();UnityEngine.Physics.SyncTransforms();
            var active=new System.Collections.Generic.List<PurposeReadingProgress>();
            foreach(var resident in World.Residents.Where(r=>r.Id!="CH_01"&&World.CanAct(r.Id)&&r.Activity=="Examine"&&r.Phase=="Performing")){
                string actor=resident.Id;
                if(PlayerTalkingTo(actor)||incidents.Controls(actor)||ResidentIsSpeaking(actor)||IntentIsSpeaking(actor))continue;
                var own=Knowledge.For(actor).Records();
                var previous=(residentIntents.PurposeReadings??Array.Empty<PurposeReadingProgress>()).FirstOrDefault(p=>p.OwnerId==actor&&p.LastTick==World.Tick-1);
                var plate=rulePlates.Where(p=>p&&p.isActiveAndEnabled&&p.HasDisplayedContent())
                    .Where(p=>!own.Any(k=>k.Direct&&k.Kind=="Document"&&k.ProvenanceKey==IncidentRuleReading.Root(p.Definition)&&k.Text==p.Definition.PublicRule))
                    .Where(p=>CanReadIncidentRule(actor,p.GetComponent<FixtureTarget>().StableId))
                    .OrderBy(p=>p.GetComponent<FixtureTarget>().StableId==previous?.PlateId?0:1)
                    .ThenBy(p=>UnityEngine.Vector3.Distance(bodies[actor].transform.position,p.ReadPosition))
                    .ThenBy(p=>p.GetComponent<FixtureTarget>().StableId,StringComparer.Ordinal).FirstOrDefault();
                if(!plate)continue;
                ruleReadersThisTick.Add(actor);
                string plateId=plate.GetComponent<FixtureTarget>().StableId;
                var definition=plate.Definition;IncidentExecutionDefinition.Validate(definition);
                var progress=previous!=null&&previous.PlateId==plateId&&previous.DefinitionId==definition.Id&&previous.Revision==definition.Revision&&previous.Text==definition.PublicRule
                    ?previous:new PurposeReadingProgress{OwnerId=actor,PlateId=plateId,DefinitionId=definition.Id,Revision=definition.Revision,Text=definition.PublicRule,StartedTick=World.Tick};
                progress.LastTick=World.Tick;progress.ElapsedTicks++;
                if(progress.ElapsedTicks>=IncidentRuleReading.Duration(definition.PublicRule))ReadIncidentRule(actor,plateId);else active.Add(progress);
            }
            residentIntents.PurposeReadings=active.ToArray();
        }
        void ValidatePurposeReadings(ResidentIntentSnapshot snapshot,long tick)
        {
            var readings=snapshot.PurposeReadings??Array.Empty<PurposeReadingProgress>();
            if(readings.Select(p=>p?.OwnerId).Distinct().Count()!=readings.Length)throw new System.IO.InvalidDataException("같은 인물의 안내문 읽기가 중복되었습니다.");
            foreach(var p in readings){
                if(p==null||p.OwnerId=="CH_01"||!bodies.ContainsKey(p.OwnerId)||string.IsNullOrEmpty(p.PlateId)||string.IsNullOrEmpty(p.DefinitionId)||string.IsNullOrEmpty(p.Revision)||string.IsNullOrEmpty(p.Text)||p.StartedTick<0||p.LastTick>tick||p.LastTick<p.StartedTick||p.ElapsedTicks<1||p.ElapsedTicks!=p.LastTick-p.StartedTick+1||p.ElapsedTicks>=IncidentRuleReading.Duration(p.Text))throw new System.IO.InvalidDataException("안내문을 읽던 진행 상태가 올바르지 않습니다.");
            }
        }
        bool AdvancePurposeExecution(ResidentIntentState task)
        {
            if(task.Phase!="Executing")return false;
            var incident=incidents.Find("CASE_"+task.Plan.Id);
            if(incident==null){EndResidentIntent(task,"Cancelled","BoundExecutionMissing");return true;}
            var snapshot=incident.Capture();task.Plan=snapshot.Plan.Copy();
            if(snapshot.Stage=="Cancelled")EndResidentIntent(task,"Withdrawn",snapshot.Reason);
            else if(snapshot.Stage=="RiskResolved")EndResidentIntent(task,"Completed","RiskResolved");
            else if(snapshot.ResultTick>=0)EndResidentIntent(task,"Completed","ActionResultCommitted");
            return true;
        }
        void ValidateResidentPurpose(ResidentIntentState task,MansionSessionSnapshot session,KnowledgeLedger ledger)
        {
            var own=ledger.For(task.Plan.OwnerId);ResidentPurposeBinding.Validate(task.Purpose,task.Plan,own,session.World.Tick);var binding=task.Purpose;
            if(task.PurposeId!=binding.Definition.Id||task.ContextId!=task.PurposeId||task.SourceExperienceId!="PURPOSE_"+task.Plan.Id||task.ItemId!=binding.ObjectId||task.RecipientId!=task.Plan.TargetId||task.ProofRecordId!=binding.ProofRecordId||binding.MapVersion!=session.World.MapVersion||!bodies.ContainsKey(binding.TargetId)||task.RecipientId!=""&&!bodies.ContainsKey(task.RecipientId))throw new System.IO.InvalidDataException("개인 목표와 행동의 대상·자료가 다릅니다.");
            if(new[]{"Negotiate","Disclose","Execute"}.Contains(task.Plan.Action)&&task.Plan.TargetId!=binding.TargetId||task.Plan.Action=="AskForHelp"&&(task.Plan.TargetId==binding.TargetId||task.Plan.TargetId==task.Plan.OwnerId)||task.Plan.Action=="Disclose"&&(!binding.BasisIds.Contains(task.ProofRecordId)||own.Find(task.ProofRecordId)?.Direct!=true))throw new System.IO.InvalidDataException("선택한 목표의 응답 상대와 근거가 다릅니다.");
            var bound=session.Incidents.FirstOrDefault(c=>c.Settings?.Id=="CASE_"+task.Plan.Id);
            if(task.Phase=="Executing"&&(task.Plan.Action!="Execute"||bound?.Plan==null||bound.Plan.Id!=task.Plan.Id||bound.Settings.Execution?.PurposeId!=task.PurposeId)||task.Plan.Action=="Execute"&&new[]{"Selected","Seeking","Speaking","Replying"}.Contains(task.Phase))throw new System.IO.InvalidDataException("개인 실행 계획과 실제 사건의 연결이 없습니다.");
            if(task.Phase=="Resolved"&&!ResidentPurposeBinding.Resolved(binding,own,session.World.Tick,task.Plan.DecisionTick))throw new System.IO.InvalidDataException("해결 정보를 받지 않은 목표입니다.");
        }
    }
}
