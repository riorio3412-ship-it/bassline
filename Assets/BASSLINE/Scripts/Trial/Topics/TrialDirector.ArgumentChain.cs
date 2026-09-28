using System;
using System.Linq;
using System.Collections.Generic;
using BASSLINE.Core;
using BASSLINE.Investigation;

namespace BASSLINE.Trial
{
    [Serializable] public sealed class ArgumentSelection
    {
        public string Owner="",ChainId="";
        public ArgumentSelection Copy()=>(ArgumentSelection)MemberwiseClone();
    }
    [Serializable] public sealed class ArgumentNode
    {
        public string ClaimId="",SpanId="";
        public long AddedTick;
        public string[] Requires=Array.Empty<string>();
        public string Key=>ClaimId+"/"+SpanId;
        public ArgumentNode Copy()=>new ArgumentNode{ClaimId=ClaimId,SpanId=SpanId,AddedTick=AddedTick,Requires=(string[])Requires.Clone()};
    }
    [Serializable] public sealed class ArgumentChainState
    {
        public string Id="",Owner="",ParentId="",TargetNode="",SelectedNode="",ReturnTopic="",Phase="Draft",SpeechId="",WithdrawalSpeechId="";
        public int Revision=1;public long CreatedTick;
        public ArgumentNode[] Nodes=Array.Empty<ArgumentNode>();
        public ArgumentChainState Copy(){var c=(ArgumentChainState)MemberwiseClone();c.Nodes=Nodes.Select(n=>n.Copy()).ToArray();return c;}
    }
    public sealed partial class TrialDirector
    {
        ArgumentChainState CurrentArgument(string owner){var selected=state.ArgumentSelections.FirstOrDefault(s=>s.Owner==owner);return state.ArgumentChains.LastOrDefault(c=>c.Owner==owner&&(selected==null||c.Id==selected.ChainId));}
        void SelectArgument(string owner,string id)
        {
            var selection=state.ArgumentSelections.FirstOrDefault(s=>s.Owner==owner);
            if(selection!=null)selection.ChainId=id;else state.ArgumentSelections=state.ArgumentSelections.Concat(new[]{new ArgumentSelection{Owner=owner,ChainId=id}}).ToArray();
        }
        public string NextArgumentChain(string owner)
        {
            if(Focused||state.Phase!="Debate")return "Unavailable";var all=state.ArgumentChains.Where(c=>c.Owner==owner).ToArray();if(all.Length==0)return "Unavailable";
            int index=Array.IndexOf(all,CurrentArgument(owner));SelectArgument(owner,all[(index+1)%all.Length].Id);return "Selected";
        }
        PublicSpeech HeardNode(string owner,string claim,string span)=>state.Transcript.FirstOrDefault(h=>h.ReceivedBy.Contains(owner)&&h.Speech.Claim?.Id==claim&&h.Speech.Claim.Spans.Any(s=>s.Id==span));
        public string BeginArgumentChain(string owner,string claim,string span)
        {
            if(state.Phase!="Debate"||Focused)return "Unavailable";
            var source=HeardNode(owner,claim,span);if(source==null)return "UnheardClaim";
            var previous=CurrentArgument(owner);if(previous!=null&&previous.Phase=="Queued")return "WaitForSpeech";
            var node=new ArgumentNode{ClaimId=claim,SpanId=span,AddedTick=state.CourtTick};
            var existing=state.ArgumentChains.LastOrDefault(c=>c.Owner==owner&&c.TargetNode==node.Key&&c.Phase!="Withdrawn");
            if(existing!=null){SelectArgument(owner,existing.Id);return "Selected";}
            string id="CHAIN_"+(state.ArgumentChains.Length+1);state.ArgumentChains=state.ArgumentChains.Concat(new[]{new ArgumentChainState{Id=id,Owner=owner,TargetNode=node.Key,SelectedNode=node.Key,ReturnTopic=source.Speech.Topic,CreatedTick=state.CourtTick,Nodes=new[]{node}}}).ToArray();SelectArgument(owner,id);return "Created";
        }
        public string SelectArgumentNode(string owner,string key)
        {
            var chain=CurrentArgument(owner);if(chain==null||!chain.Nodes.Any(n=>n.Key==key))return "Unavailable";
            chain.SelectedNode=key;return "Selected";
        }
        static bool Reaches(ArgumentNode[] nodes,string start,string target,HashSet<string> visited=null)
        {
            if(start==target)return true;visited=visited??new HashSet<string>();if(!visited.Add(start))return false;
            return nodes.Single(n=>n.Key==start).Requires.Any(k=>Reaches(nodes,k,target,visited));
        }
        public string ToggleArgumentPremise(string owner,string claim,string span)
        {
            var chain=CurrentArgument(owner);if(state.Phase!="Debate"||Focused||chain?.Phase!="Draft"||HeardNode(owner,claim,span)==null)return "Unavailable";
            var node=chain.Nodes.Single(n=>n.Key==chain.SelectedNode);var premise=new ArgumentNode{ClaimId=claim,SpanId=span,AddedTick=state.CourtTick};
            if(node.Key==premise.Key)return "SelfDependency";
            if(node.Requires.Contains(premise.Key)){node.Requires=node.Requires.Where(k=>k!=premise.Key).ToArray();return "Removed";}
            if(chain.Nodes.Any(n=>n.Key==premise.Key)&&Reaches(chain.Nodes,premise.Key,node.Key))return "Cycle";
            if(!chain.Nodes.Any(n=>n.Key==premise.Key)){if(chain.Nodes.Length>=6)return "NodeLimit";chain.Nodes=chain.Nodes.Concat(new[]{premise}).ToArray();}
            node.Requires=node.Requires.Concat(new[]{premise.Key}).ToArray();return "Linked";
        }
        public string RemoveArgumentNode(string owner)
        {
            var c=CurrentArgument(owner);if(Focused||state.Phase!="Debate"||c?.Phase!="Draft"||c.SelectedNode==c.TargetNode)return "Unavailable";
            string key=c.SelectedNode;c.Nodes=c.Nodes.Where(n=>n.Key!=key).ToArray();foreach(var n in c.Nodes)n.Requires=n.Requires.Where(k=>k!=key).ToArray();c.SelectedNode=c.TargetNode;return "Removed";
        }
        public string PublishArgumentChain(string owner,Func<string,string,string> describe)
        {
            var c=CurrentArgument(owner);if(Focused||state.Phase!="Debate"||c?.Phase!="Draft"||c.Nodes.Length<2)return "Unavailable";
            if(c.Nodes.Any(n=>!Reaches(c.Nodes,c.TargetNode,n.Key)))return "Disconnected";
            string text="제가 이 설명에 필요하다고 보는 전제를 정리하겠습니다. 연결했다는 것만으로 입증된 것은 아닙니다. "+string.Join(" ",c.Nodes.Where(n=>n.Requires.Length>0).Select(n=>"‘"+describe(n.ClaimId,n.SpanId)+"’라는 설명에는 "+string.Join(", ",n.Requires.Select(key=>{var p=c.Nodes.Single(x=>x.Key==key);return "‘"+describe(p.ClaimId,p.SpanId)+"’";}))+"가 필요하다고 봅니다."));
            string id=c.Id+"_EXPLAIN";var speech=new SpeechDraft{Id=id,Speaker=owner,Topic=c.ReturnTopic,Text=text};
            string result=QueueSpeech(speech);if(result!="Queued")return result;
            c.SpeechId=id;c.Phase="Queued";PrioritizeSpeech(id);return "Queued";
        }
        public string ReviseArgumentChain(string owner)
        {
            var c=CurrentArgument(owner);if(Focused||state.Phase!="Debate"||c==null||c.Phase!="Published"||c.WithdrawalSpeechId!="")return "Unavailable";
            var next=c.Copy();next.Id="CHAIN_"+(state.ArgumentChains.Length+1);next.ParentId=c.Id;next.Revision++;next.Phase="Draft";next.SpeechId="";next.WithdrawalSpeechId="";next.CreatedTick=state.CourtTick;
            state.ArgumentChains=state.ArgumentChains.Concat(new[]{next}).ToArray();SelectArgument(owner,next.Id);return "Created";
        }
        public string WithdrawArgumentChain(string owner)
        {
            var c=CurrentArgument(owner);if(Focused||state.Phase!="Debate"||c==null||c.Phase=="Queued"||c.Phase=="Withdrawn"||c.WithdrawalSpeechId!="")return "Unavailable";
            if(c.Phase=="Draft"){c.Phase="Withdrawn";return "Withdrawn";}
            string id=c.Id+"_WITHDRAW";string result=QueueSpeech(new SpeechDraft{Id=id,Speaker=owner,Topic=c.ReturnTopic,Text="제가 방금 연결한 설명은 철회하겠습니다. 각 자료 자체를 없애는 것은 아닙니다. 연결이 성립하는지는 다시 확인하겠습니다."});
            if(result=="Queued"){c.WithdrawalSpeechId=id;PrioritizeSpeech(id);}return result;
        }
        void UpdateArgumentReceipts(PublicSpeech speech)
        {
            foreach(var c in state.ArgumentChains){if(c.SpeechId==speech.Speech.Id)c.Phase="Published";if(c.WithdrawalSpeechId==speech.Speech.Id)c.Phase="Withdrawn";}
        }
        public LogicResult[] SubmitArgumentEvidence(IActorKnowledgeQuery own,string request,string action,string rule,string[] refs)
        {
            var c=CurrentArgument(own.OwnerId);
            if(c?.Phase!="Published"||c.WithdrawalSpeechId!=""||state.Focus?.Owner!=own.OwnerId||c.SelectedNode!=state.Focus.ClaimId+"/"+state.Focus.SpanId||state.Focus.KnowledgeRevision!=own.Revision)return Array.Empty<LogicResult>();
            // Each obligation uses the same resolver and same actual B; no prescribed card order.
            // A proof can support several spans without converting graph edges into proof themselves.
            CancelFocus(own.OwnerId);var results=new List<LogicResult>();
            foreach(var n in c.Nodes){if(EnterFocus(own,n.ClaimId,n.SpanId)!="Focused")continue;
                results.Add(Submit(own,request+"_"+results.Count,action,rule,refs));}
            if(Focused)CancelFocus(own.OwnerId);return results.ToArray();
        }
        public ArgumentChainView[] ReadReceivedArguments(string viewer)
            =>state.ArgumentChains.Where(c=>state.Transcript.Any(h=>h.Speech.Id==c.SpeechId&&h.ReceivedBy.Contains(viewer))).Select(c=>ReadArgumentChain(viewer,c.Id)).ToArray();
        public ArgumentChainView ReadArgumentChain(string viewer,string chainId="")
        {
            var publicView=Read(viewer,false);var heard=ReadHeardClaims(viewer);
            var candidates=publicView.History.Where(h=>h.Speech.Claim!=null).SelectMany(h=>h.Speech.Claim.Spans.Select(s=>new ArgumentNodeView{Key=h.Speech.Claim.Id+"/"+s.Id,ClaimId=h.Speech.Claim.Id,SpanId=s.Id,SpeakerId=h.Speech.Speaker,Review=heard.First(x=>x.Id==h.Speech.Claim.Id&&x.Span.Id==s.Id).Review})).ToArray();
            var c=chainId==""?CurrentArgument(viewer):state.ArgumentChains.FirstOrDefault(a=>a.Id==chainId&&(a.Owner==viewer||publicView.History.Any(h=>h.Speech.Id==a.SpeechId)));
            if(c==null)return new ArgumentChainView{Candidates=candidates};
            // An explanation receipt is not a retroactive receipt of its cited original speeches.
            if(c.Nodes.Any(n=>!candidates.Any(p=>p.Key==n.Key)))return new ArgumentChainView{Id=c.Id,Phase="SourcesIncomplete",Revision=c.Revision};
            string phase=c.Owner==viewer?c.Phase:publicView.History.Any(h=>h.Speech.Id==c.WithdrawalSpeechId)?"Withdrawn":"Published";
            var view=new ArgumentChainView{Id=c.Id,Phase=phase,Revision=c.Revision,ChainCount=state.ArgumentChains.Count(a=>a.Owner==viewer),SelectedNode=c.Owner==viewer?c.SelectedNode:c.TargetNode,TargetNode=c.TargetNode,ReturnTopic=c.ReturnTopic,Candidates=candidates};
            view.Nodes=c.Nodes.Select(n=>{var source=candidates.Single(x=>x.Key==n.Key);var review=publicView.Reviews.LastOrDefault(r=>r.ClaimId==n.ClaimId&&r.SpanId==n.SpanId);var proof=state.Submissions.FirstOrDefault(r=>r.Id==review?.RequestId&&r.ReceivedBy.Contains(viewer));return new ArgumentNodeView{Key=n.Key,ClaimId=n.ClaimId,SpanId=n.SpanId,SpeakerId=source.SpeakerId,Review=source.Review,Requires=(string[])n.Requires.Clone(),ProofIds=proof?.Result.CitedRefs??Array.Empty<string>()};}).ToArray();
            string Evaluate(string key){var n=view.Nodes.Single(x=>x.Key==key);if(n.State!=null)return n.State;
                if(n.Review=="Contradicted"||n.Review=="CorrectedBySpeaker"||n.Review=="UnsupportedScope")return n.State=n.Review;
                var dependencies=n.Requires.Select(Evaluate).ToArray();if(dependencies.Any(s=>s=="Contradicted"||s=="CorrectedBySpeaker"||s=="UnsupportedScope"||s=="NeedsReview"))return n.State="NeedsReview";
                return n.State=n.Review=="SupportedWithinScope"?"SupportedWithinScope":"Unresolved";}
            foreach(var n in view.Nodes)Evaluate(n.Key);
            view.CounterIds=publicView.History.Where(h=>h.Speech.Speaker!=c.Owner&&c.Nodes.Any(n=>n.ClaimId==h.Speech.CounterClaimId&&n.SpanId==h.Speech.CounterSpanId)).Select(h=>h.Speech.Id).ToArray();
            view.Mode=phase=="Published"&&c.Nodes.Length>=3&&view.CounterIds.Length>0?"ArgumentChain":"Preparation";return view;
        }
        static void ValidateArgumentChains(TrialSnapshot s)
        {
            if(s.ArgumentChains==null)s.ArgumentChains=Array.Empty<ArgumentChainState>();
            if(s.ArgumentSelections==null)s.ArgumentSelections=Array.Empty<ArgumentSelection>();
            if(s.ArgumentChains.Any(c=>c==null)||s.ArgumentChains.Select(c=>c.Id).Distinct().Count()!=s.ArgumentChains.Length)throw new ArgumentException("Invalid argument chains");
            if(s.ArgumentSelections.Any(p=>p==null||!s.ArgumentChains.Any(c=>c.Id==p.ChainId&&c.Owner==p.Owner))||s.ArgumentSelections.Select(p=>p.Owner).Distinct().Count()!=s.ArgumentSelections.Length)throw new ArgumentException("Invalid selected argument");
            var speeches=s.Pending.Concat(s.DeferredSpeeches).Concat(s.Transcript.Select(h=>h.Speech)).ToArray();
            foreach(var speech in speeches.Where(p=>!string.IsNullOrEmpty(p.CounterClaimId)||!string.IsNullOrEmpty(p.CounterSpanId)))
                if(!s.Transcript.Any(h=>h.ReceivedBy.Contains(speech.Speaker)&&h.Speech.Claim?.Id==speech.CounterClaimId&&h.Speech.Claim.Spans.Any(n=>n.Id==speech.CounterSpanId)))throw new ArgumentException("Counter refers to an unheard claim");
            foreach(var receipt in s.Submissions.Where(r=>!string.IsNullOrEmpty(r.ClaimId)||!string.IsNullOrEmpty(r.SpanId)))
                if(!s.Transcript.Any(h=>h.Speech.Claim?.Id==receipt.ClaimId&&h.Speech.Claim.Spans.Any(n=>n.Id==receipt.SpanId)))throw new ArgumentException("Proof receipt has no claim obligation");
            foreach(var c in s.ArgumentChains){
                Id(c.Id);if(!s.Participants.Contains(c.Owner)||c.Revision<1||c.CreatedTick<0||c.CreatedTick>s.CourtTick||!new[]{"Draft","Queued","Published","Withdrawn"}.Contains(c.Phase)||c.Nodes==null||c.Nodes.Length<1||c.Nodes.Length>6||c.Nodes.Any(n=>n==null||n.Requires==null)||c.Nodes.Select(n=>n.Key).Distinct().Count()!=c.Nodes.Length||!c.Nodes.Any(n=>n.Key==c.TargetNode)||!c.Nodes.Any(n=>n.Key==c.SelectedNode))throw new ArgumentException("Invalid argument structure");
                foreach(var n in c.Nodes){if(n.AddedTick<0||n.AddedTick>s.CourtTick||n.Requires.Distinct().Count()!=n.Requires.Length||n.Requires.Any(k=>!c.Nodes.Any(p=>p.Key==k))||!s.Transcript.Any(h=>h.CourtTick<=n.AddedTick&&h.ReceivedBy.Contains(c.Owner)&&h.Speech.Claim?.Id==n.ClaimId&&h.Speech.Claim.Spans.Any(p=>p.Id==n.SpanId)))throw new ArgumentException("Argument references unheard premises");}
                foreach(var n in c.Nodes)if(n.Requires.Any(k=>Reaches(c.Nodes,k,n.Key)))throw new ArgumentException("Cyclic argument");
                if(c.ParentId!=""&&!s.ArgumentChains.Any(p=>p.Id==c.ParentId&&p.Owner==c.Owner&&p.Revision==c.Revision-1&&p.CreatedTick<=c.CreatedTick))throw new ArgumentException("Invalid argument revision");
                if(c.Phase=="Draft"&&(c.SpeechId!=""||c.WithdrawalSpeechId!="")||c.SpeechId!=""&&(!speeches.Any(p=>p.Id==c.SpeechId&&p.Speaker==c.Owner)||c.Nodes.Any(n=>!Reaches(c.Nodes,c.TargetNode,n.Key)))||c.Phase=="Queued"&&(c.SpeechId==""||s.Transcript.Any(h=>h.Speech.Id==c.SpeechId))||c.Phase=="Published"&&!s.Transcript.Any(h=>h.Speech.Id==c.SpeechId)||c.WithdrawalSpeechId!=""&&!speeches.Any(p=>p.Id==c.WithdrawalSpeechId&&p.Speaker==c.Owner)||c.Phase=="Withdrawn"&&c.SpeechId!=""&&!s.Transcript.Any(h=>h.Speech.Id==c.WithdrawalSpeechId))throw new ArgumentException("Argument publication has no matching utterance");
            }
        }
    }
}
