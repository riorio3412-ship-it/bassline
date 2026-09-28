using System;
using System.Linq;
using BASSLINE.Core;
using BASSLINE.Investigation;
namespace BASSLINE.Trial
{
    [Serializable] public sealed class TheoryPosition
    {
        public string Id="",TheoryId="",Actor="",Kind="",ClaimId="",SpanId="",SpeechId="";
        public long RequestedTick;
        public TheoryPosition Copy()=>(TheoryPosition)MemberwiseClone();
    }
    [Serializable] public sealed class TheorySelection
    {
        public string Owner="",Left="",Right="",Focused="",SelectedNode="";
        public TheorySelection Copy()=>(TheorySelection)MemberwiseClone();
    }
    public sealed partial class TrialDirector
    {
        static string TheoryId(ArgumentChainState chain)=>"THEORY_"+chain.Id;
        ArgumentChainState Theory(string id)=>state.ArgumentChains.FirstOrDefault(c=>TheoryId(c)==id);
        bool HeardTheory(string viewer,string id)
        {
            var chain=Theory(id);return chain!=null&&state.Transcript.Any(h=>h.Speech.Id==chain.SpeechId&&h.ReceivedBy.Contains(viewer));
        }
        bool ReceivedPosition(string viewer,TheoryPosition position)=>state.Transcript.Any(h=>h.Speech.Id==position.SpeechId&&h.ReceivedBy.Contains(viewer));
        TheorySelection Selection(string viewer)=>state.TheorySelections.FirstOrDefault(s=>s.Owner==viewer);
        TheorySelection EnsureTheorySelection(string viewer)
        {
            var choice=Selection(viewer);if(choice!=null)return choice;
            choice=new TheorySelection{Owner=viewer};state.TheorySelections=state.TheorySelections.Concat(new[]{choice}).ToArray();return choice;
        }
        ArgumentNodeView NodeFor(string viewer,string claim,string span)
        {
            var source=HeardNode(viewer,claim,span);if(source==null)return null;
            var heard=ReadHeardClaims(viewer).First(s=>s.Id==claim&&s.Span.Id==span);
            var publicView=Read(viewer,false);var review=publicView.Reviews.LastOrDefault(r=>r.ClaimId==claim&&r.SpanId==span);
            var proof=state.Submissions.FirstOrDefault(p=>p.Id==review?.RequestId&&p.ReceivedBy.Contains(viewer));
            return new ArgumentNodeView{Key=claim+"/"+span,ClaimId=claim,SpanId=span,SpeakerId=source.Speech.Speaker,Review=heard.Review,State=heard.Review,ProofIds=proof?.Result.CitedRefs??Array.Empty<string>()};
        }
        public TheoryView[] ReadTheories(string viewer)
        {
            if(!state.Participants.Contains(viewer))return Array.Empty<TheoryView>();
            return state.ArgumentChains.Where(c=>HeardTheory(viewer,TheoryId(c))).Select(c=>{
                var graph=ReadArgumentChain(viewer,c.Id);string id=TheoryId(c);
                var v=new TheoryView{Id=id,ParentId=c.ParentId==""?"":"THEORY_"+c.ParentId,Owner=c.Owner,Revision=c.Revision,Lifecycle=graph.Phase,TargetNode=c.TargetNode,Premises=graph.Nodes};
                var positions=state.TheoryPositions.Where(p=>p.TheoryId==id&&ReceivedPosition(viewer,p)).ToArray();
                if(state.Transcript.Any(h=>h.Speech.Id==c.WithdrawalSpeechId&&h.ReceivedBy.Contains(viewer)))v.Lifecycle="Withdrawn";
                var stances=positions.Where(p=>p.Kind!="Context").GroupBy(p=>p.Actor).Select(g=>g.Last()).ToArray();
                v.ConditionalSupporters=stances.Where(p=>p.Kind=="Conditional").Select(p=>p.Actor).ToArray();v.DeferredBy=stances.Where(p=>p.Kind=="Defer").Select(p=>p.Actor).ToArray();
                if(graph.Phase=="SourcesIncomplete"){v.State="Unresolved";v.Gaps=new[]{"원진술을 직접 듣지 못한 부분이 있습니다."};return v;}
                var context=positions.Where(p=>p.Kind=="Context").Select(p=>NodeFor(viewer,p.ClaimId,p.SpanId)).Where(n=>n!=null).GroupBy(n=>n.Key).Select(g=>g.First()).ToArray();
                v.Counterexamples=graph.Nodes.Concat(context).Where(n=>n.Review=="Contradicted").Select(n=>n.Key).Distinct().ToArray();
                v.Gaps=graph.Nodes.Where(n=>n.State!="SupportedWithinScope").Select(n=>n.Key).Concat(positions.Where(p=>p.Kind=="Context"&&NodeFor(viewer,p.ClaimId,p.SpanId)==null).Select(p=>"참고 진술의 원문을 아직 받지 못했습니다.")).ToArray();
                bool reviewed=state.Submissions.Any(r=>r.ReceivedBy.Contains(viewer)&&graph.Nodes.Any(n=>n.ClaimId==r.ClaimId&&n.SpanId==r.SpanId));
                // Votes, popularity, relationships and hidden case truth never enter this evaluation.
                v.State=graph.Nodes.Any(n=>n.Review=="Contradicted")?"Collapsed":context.Any(n=>n.Review=="Contradicted")?"Contradicted":
                    graph.Nodes.Any(n=>new[]{"UnsupportedScope","CorrectedBySpeaker","NeedsReview"}.Contains(n.State))?"Weakened":
                    graph.Nodes.Any(n=>n.Review=="SupportedWithinScope")?"Strengthened":reviewed?"Unresolved":"Active";
                return v;
            }).ToArray();
        }
        bool DifferentPredictions(TheoryView a,TheoryView b,string viewer)
        {
            if(a==null||b==null||a.Id==b.Id||a.Lifecycle!="Published"||b.Lifecycle!="Published")return false;
            ClaimSpan Span(ArgumentNodeView n)=>HeardNode(viewer,n.ClaimId,n.SpanId)?.Speech.Claim.Spans.First(s=>s.Id==n.SpanId);
            // Only registered exclusive predicates with the exact declared interval qualify here.
            // Other routes or a different suspect alone are not manufactured into a contradiction.
            return a.Premises.Any(x=>b.Premises.Any(y=>{var p=Span(x);var q=Span(y);return p!=null&&q!=null&&p.SubjectId==q.SubjectId&&p.SubjectId!="UNKNOWN_ACTOR"&&p.Predicate==q.Predicate&&new[]{"AtPlace","DoorState"}.Contains(p.Predicate)&&p.Quantifier=="Particular"&&q.Quantifier=="Particular"&&p.FromTick==q.FromTick&&p.ToTick==q.ToTick&&p.Value!=q.Value;}));
        }
        public TheoryComparisonView ReadTheoryComparison(string viewer)
        {
            var theories=ReadTheories(viewer);var saved=Selection(viewer);var selection=saved?.Copy()??new TheorySelection();
            if(!theories.Any(t=>t.Id==selection.Left))selection.Left=theories.FirstOrDefault()?.Id??"";
            if(!theories.Any(t=>t.Id==selection.Right&&t.Id!=selection.Left))selection.Right=theories.FirstOrDefault(t=>t.Id!=selection.Left)?.Id??"";
            if(selection.Focused!=selection.Left&&selection.Focused!=selection.Right||selection.Focused=="")selection.Focused=selection.Left;
            var focus=theories.FirstOrDefault(t=>t.Id==selection.Focused);
            if(focus!=null&&!focus.Premises.Any(n=>n.Key==selection.SelectedNode))selection.SelectedNode=focus.TargetNode;
            return new TheoryComparisonView{Left=selection.Left,Right=selection.Right,Focused=selection.Focused,SelectedNode=selection.SelectedNode,Theories=theories,
                Mode=DifferentPredictions(theories.FirstOrDefault(t=>t.Id==selection.Left),theories.FirstOrDefault(t=>t.Id==selection.Right),viewer)?"TheoryClash":"Comparison",ContextCandidates=ReadArgumentChain(viewer).Candidates};
        }
        TheorySelection ChooseTheory(string viewer)
        {
            var view=ReadTheoryComparison(viewer);var saved=EnsureTheorySelection(viewer);saved.Left=view.Left;saved.Right=view.Right;saved.Focused=view.Focused;saved.SelectedNode=view.SelectedNode;return saved;
        }
        public string SwitchTheorySide(string viewer)
        {
            if(state.Phase!="Debate"||Focused||!state.Participants.Contains(viewer))return "Unavailable";
            var s=ChooseTheory(viewer);s.Focused=s.Focused==s.Left&&s.Right!=""?s.Right:s.Left;s.SelectedNode=Theory(s.Focused)?.TargetNode??"";return "Selected";
        }
        public string NextComparedTheory(string viewer)
        {
            if(state.Phase!="Debate"||Focused||!state.Participants.Contains(viewer))return "Unavailable";
            var s=ChooseTheory(viewer);var all=ReadTheories(viewer);if(all.Length<2)return "Unavailable";
            string other=s.Focused==s.Left?s.Right:s.Left;var choices=all.Where(t=>t.Id!=other).ToArray();int index=Array.FindIndex(choices,t=>t.Id==s.Focused);
            string next=choices[(index+1)%choices.Length].Id;if(s.Focused==s.Left)s.Left=next;else s.Right=next;s.Focused=next;s.SelectedNode=Theory(next).TargetNode;return "Selected";
        }
        public string NextTheoryPremise(string viewer)
        {
            if(state.Phase!="Debate"||Focused||!state.Participants.Contains(viewer))return "Unavailable";var s=ChooseTheory(viewer);var t=ReadTheories(viewer).FirstOrDefault(v=>v.Id==s.Focused);
            if(t==null||t.Premises.Length==0)return "Unavailable";int i=Array.FindIndex(t.Premises,n=>n.Key==s.SelectedNode);s.SelectedNode=t.Premises[(i+1)%t.Premises.Length].Key;return "Selected";
        }
        public string FocusTheoryPremise(IActorKnowledgeQuery own)
        {
            if(own==null||state.Phase!="Debate"||Focused||!state.Participants.Contains(own.OwnerId)||own.LoopId!=state.LoopId)return "Unavailable";
            var s=ChooseTheory(own.OwnerId);var node=ReadTheories(own.OwnerId).FirstOrDefault(t=>t.Id==s.Focused)?.Premises.FirstOrDefault(n=>n.Key==s.SelectedNode);
            return node==null?"Unavailable":EnterFocus(own,node.ClaimId,node.SpanId);
        }
        public string QueueTheoryPosition(string actor,string theoryId,string kind,string claim="",string span="",string contextText="")
        {
            if(state.Phase!="Debate"||Focused||!new[]{"Conditional","Defer","WithdrawSupport","Context"}.Contains(kind)||!HeardTheory(actor,theoryId))return "Unavailable";
            var view=ReadTheories(actor).Single(t=>t.Id==theoryId);if(view.Lifecycle!="Published"&&!(view.Lifecycle=="SourcesIncomplete"&&kind=="Defer"))return "Unavailable";
            if(kind=="Context"&&(HeardNode(actor,claim,span)==null||string.IsNullOrWhiteSpace(contextText)))return "UnheardClaim";
            if(state.TheoryPositions.Any(p=>p.Actor==actor&&!state.Transcript.Any(h=>h.Speech.Id==p.SpeechId)))return "WaitForSpeech";
            var last=state.TheoryPositions.LastOrDefault(p=>p.Actor==actor&&p.TheoryId==theoryId&&(kind=="Context"?p.Kind==kind&&p.ClaimId==claim&&p.SpanId==span:p.Kind!="Context"));
            if(last!=null&&last.Kind==kind)return "AlreadyStated";
            string text=kind=="Conditional"?"이 설명은 공개한 전제가 성립하는 범위에서만 받아들이겠습니다. 아직 확인되지 않은 부분까지 사실로 단정하지 않겠습니다.":kind=="Defer"?"이 설명에 대한 판단은 잠시 미루겠습니다. 부족한 자료를 확인한 뒤 다시 검토하겠습니다.":kind=="WithdrawSupport"?"이 설명에 보냈던 제 지지는 거두겠습니다. 그 자체가 설명을 반증하거나 다른 설명을 입증하는 것은 아닙니다.":"이 설명과 함께 살펴볼 진술이 있습니다. ‘"+contextText+"’. 이것을 필수 전제로 단정하지 않고 참고할 자료로 제안합니다.";
            string id="THEORY_POSITION_"+(state.TheoryPositions.Length+1);var source=Theory(theoryId);
            var root=source.Nodes.Single(n=>n.Key==source.TargetNode);string title=HeardNode(actor,root.ClaimId,root.SpanId)?.Speech.Claim.Text??state.Transcript.First(h=>h.Speech.Id==source.SpeechId&&h.ReceivedBy.Contains(actor)).Speech.Text;
            if(title.Length>180)title=title.Substring(0,180)+"…";
            text="‘"+title+"’라는 설명에 관해 말하겠습니다. "+text;
            var speech=new SpeechDraft{Id=id+"_SPEECH",Speaker=actor,Topic=source.ReturnTopic,Text=text};string result=QueueSpeech(speech);if(result!="Queued")return result;
            state.TheoryPositions=state.TheoryPositions.Concat(new[]{new TheoryPosition{Id=id,TheoryId=theoryId,Actor=actor,Kind=kind,ClaimId=kind=="Context"?claim:"",SpanId=kind=="Context"?span:"",SpeechId=speech.Id,RequestedTick=state.CourtTick}}).ToArray();PrioritizeSpeech(speech.Id);return "Queued";
        }
        public string ReviseComparedTheory(string actor)
        {
            if(state.Phase!="Debate"||Focused||!state.Participants.Contains(actor))return "Unavailable";
            var selection=ChooseTheory(actor);var source=Theory(selection.Focused);if(source==null||source.Owner!=actor)return "Unavailable";
            SelectArgument(actor,source.Id);return ReviseArgumentChain(actor);
        }
        static void ValidateTheories(TrialSnapshot s)
        {
            if(s.TheoryPositions==null)s.TheoryPositions=Array.Empty<TheoryPosition>();if(s.TheorySelections==null)s.TheorySelections=Array.Empty<TheorySelection>();
            bool Heard(string actor,string id,long tick){var c=s.ArgumentChains.FirstOrDefault(a=>TheoryId(a)==id);return c!=null&&s.Transcript.Any(h=>h.Speech.Id==c.SpeechId&&h.ReceivedBy.Contains(actor)&&h.CourtTick<=tick);}
            var speeches=s.Pending.Concat(s.DeferredSpeeches).Concat(s.Transcript.Select(h=>h.Speech)).ToArray();
            if(s.TheoryPositions.Any(p=>p==null)||s.TheoryPositions.Select(p=>p.Id).Distinct().Count()!=s.TheoryPositions.Length||s.TheorySelections.Any(p=>p==null)||s.TheorySelections.Select(p=>p.Owner).Distinct().Count()!=s.TheorySelections.Length)throw new ArgumentException("Invalid theory records");
            foreach(var p in s.TheoryPositions){Id(p.Id);if(!new[]{"Conditional","Defer","WithdrawSupport","Context"}.Contains(p.Kind)||p.RequestedTick<0||p.RequestedTick>s.CourtTick||!Heard(p.Actor,p.TheoryId,p.RequestedTick)||!speeches.Any(x=>x.Id==p.SpeechId&&x.Speaker==p.Actor)||p.Kind=="Context"&&!s.Transcript.Any(h=>h.CourtTick<=p.RequestedTick&&h.ReceivedBy.Contains(p.Actor)&&h.Speech.Claim?.Id==p.ClaimId&&h.Speech.Claim.Spans.Any(n=>n.Id==p.SpanId)))throw new ArgumentException("Theory position has no real received basis");}
            foreach(var p in s.TheorySelections){if(!s.Participants.Contains(p.Owner)||new[]{p.Left,p.Right,p.Focused}.Any(id=>id!=""&&!Heard(p.Owner,id,s.CourtTick))||p.Focused!=""&&p.Focused!=p.Left&&p.Focused!=p.Right||p.SelectedNode!=""&&!s.ArgumentChains.Any(c=>TheoryId(c)==p.Focused&&c.Nodes.Any(n=>n.Key==p.SelectedNode)))throw new ArgumentException("Invalid theory selection");}
        }
    }
}
