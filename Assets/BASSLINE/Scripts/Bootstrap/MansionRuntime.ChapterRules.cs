using System;
using System.Linq;
using BASSLINE.Core;
using BASSLINE.World.Mansion;
namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime
    {
        // Other catalog entries are design data until their physical activities have an implementation.
        static readonly string[] ImplementedChapterRules={"CH01","CH04"};
        bool HasChapterRule(string id)=>proceedings.Rules!=null&&proceedings.Rules.Active.Any(a=>a.Id==id);
        void ReceiveChapterRules(string actor)
        {
            var plan=proceedings.Rules;if(plan==null||!ReceivesSpeech(actor,"PRES_YUSTI"))return;
            string key="RULES_L"+plan.Loop+"_C"+plan.Chapter;
            if(Knowledge.For(actor).Records().Any(r=>r.ProvenanceKey==key))return;
            if(plan.AnnouncedTick<0)plan.AnnouncedTick=World.Tick;
            Knowledge.Observe(actor,new KnownRecord{Kind="OfficialAnnouncement",Source="PRES_YUSTI",SubjectId="PRES_YUSTI",Predicate="ChapterRulesReceived",Value=plan.Chapter.ToString(),ProvenanceKey=key,Text=ChapterRules.Summary(plan),PlaceId=PlaceOf(Presenter.transform.position),FromTick=World.Tick,ToTick=World.Tick+1,Supports=new[]{"이번 챕터에 공표된 추가·유지·종료 규칙"},DoesNotEstablish=new[]{"다른 참가자의 수신 여부·비공개 행동·사건의 정답"}},World.Tick);
            foreach(var active in plan.Active)active.ReceivedBy=active.ReceivedBy.Concat(new[]{actor}).Distinct().ToArray();
        }
        void SealFirstStatement(string actor,string record)
        {
            var rule=proceedings.Rules?.Active.FirstOrDefault(r=>r.Id=="CH04");if(rule==null)return;
            string prefix="C"+World.Chapter+"|"+actor+"|";
            if(rule.ConsequenceRefs.Any(r=>r.StartsWith(prefix,StringComparison.Ordinal)))return;
            rule.ConsequenceRefs=rule.ConsequenceRefs.Concat(new[]{prefix+record}).ToArray();
            proceedings.StatementSeals=proceedings.StatementSeals.Concat(new[]{new BASSLINE.Save.MansionStatementSeal{Loop=World.Loop,Chapter=World.Chapter,ActorId=actor,RecordId=record,RuleInstanceId=rule.InstanceId}}).ToArray();
            World.Emit("FirstStatementSealed",actor,record,rule.InstanceId);
            // The source record is immutable; later corrections are additional records, never edits.
        }
        KnownRecord[] ReadPersonalRecords()
        {
            var records=Knowledge.For("CH_01").Records();
            foreach(var r in records){
                var seal=proceedings.StatementSeals.FirstOrDefault(s=>s.Loop==World.Loop&&s.RecordId==r.RootId);
                if(seal!=null&&records.Any(n=>n.Predicate=="ChapterRulesReceived"&&n.Value==seal.Chapter.ToString()))
                    r.Supports=r.Supports.Concat(new[]{"챕터 "+seal.Chapter+"에서 봉인한 첫 진술 원문. 이후 발언과 별도로 보존됩니다."}).ToArray();
            }
            var preserved=(ui.SelectedEvidence??Array.Empty<string>()).Concat(new[]{ui.SelectedRecordId,ui.ReconstructionRecord}).Concat(Notebook.For("CH_01").SelectMany(h=>h.EvidenceRefs));
            return PresenceEvidence.Compact(records,preserved);
        }
        void PresenterGreeting()
        {
            string text=World.Chapter==1?"유스티라고 합니다. 이곳에서는 집사라고 불러 주십시오.\n차는 원하실 때 말씀해 주시고요. 직접 확인한 일이 있다면 제게 알려 주십시오.":ChapterRules.Summary(proceedings.Rules);
            BeginConversation("PRES_YUSTI",text,"ButlerGreeting",900);
        }
    }
}
