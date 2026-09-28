using System;
using System.Linq;
using System.Collections.Generic;
using BASSLINE.Core;
namespace BASSLINE.Investigation
{
    [Serializable] public sealed class NotebookLink {public string OwnerId,From,To;public NotebookLink Copy()=>(NotebookLink)MemberwiseClone();}
    [Serializable] public sealed class HypothesisRecord {public string Id,OwnerId,Text,Status="Unverified";public string[] EvidenceRefs=Array.Empty<string>();public HypothesisRecord Copy()=>new HypothesisRecord{Id=Id,OwnerId=OwnerId,Text=Text,Status=Status,EvidenceRefs=(string[])EvidenceRefs.Clone()};}
    [Serializable] public sealed class InvestigationSnapshot {public long Sequence;public HypothesisRecord[] Hypotheses=Array.Empty<HypothesisRecord>();public NotebookLink[] Links=Array.Empty<NotebookLink>();}
    public sealed class InvestigationNotebook
    {
        readonly List<HypothesisRecord> hypotheses=new List<HypothesisRecord>();readonly List<NotebookLink> links=new List<NotebookLink>();long sequence;
        public string CreateHypothesis(IActorKnowledgeQuery query,string text,string[] references)
        {
            if(string.IsNullOrWhiteSpace(text)||text.Length>2000||references==null||references.Any(id=>query.Find(id)==null))return "Unavailable";
            var old=hypotheses.FirstOrDefault(x=>x.OwnerId==query.OwnerId&&x.Text==text);if(old!=null)return old.Id;
            var h=new HypothesisRecord{Id="K_HYP_"+(++sequence),OwnerId=query.OwnerId,Text=text,EvidenceRefs=references.Distinct().ToArray()};hypotheses.Add(h);return h.Id;
        }
        public string SetStatus(string owner,string id,string status){var h=hypotheses.FirstOrDefault(x=>x.Id==id&&x.OwnerId==owner);if(h==null||!new[]{"Unverified","Held","Withdrawn"}.Contains(status))return "Unavailable";h.Status=status;return "Committed";}
        public HypothesisRecord[] For(string owner)=>hypotheses.Where(x=>x.OwnerId==owner).Select(x=>x.Copy()).ToArray();
        public string Link(IActorKnowledgeQuery query,string from,string to)
        {
            bool Allowed(string id)=>query.Find(id)!=null||hypotheses.Any(x=>x.Id==id&&x.OwnerId==query.OwnerId);
            if(from==to||!Allowed(from)||!Allowed(to))return "Unavailable";
            var stack=new Stack<string>();var visited=new HashSet<string>();stack.Push(to);while(stack.Count>0){var next=stack.Pop();if(next==from)return "Cycle";if(visited.Add(next))foreach(var edge in links.Where(x=>x.OwnerId==query.OwnerId&&x.From==next))stack.Push(edge.To);}
            if(!links.Any(x=>x.OwnerId==query.OwnerId&&x.From==from&&x.To==to))links.Add(new NotebookLink{OwnerId=query.OwnerId,From=from,To=to});return "Committed";
        }
        public InvestigationSnapshot Capture()=>new InvestigationSnapshot{Sequence=sequence,Hypotheses=hypotheses.Select(x=>x.Copy()).ToArray(),Links=links.Select(x=>x.Copy()).ToArray()};
        public static InvestigationNotebook Restore(InvestigationSnapshot s,Func<string,IActorKnowledgeQuery> query)
        {
            if(s==null||s.Hypotheses==null||s.Links==null||s.Sequence!=s.Hypotheses.Length)throw new ArgumentException("Invalid investigation snapshot");var n=new InvestigationNotebook();
            foreach(var h in s.Hypotheses){if(h.Id!="K_HYP_"+(n.sequence+1)||n.CreateHypothesis(query(h.OwnerId),h.Text,h.EvidenceRefs)!=h.Id||n.SetStatus(h.OwnerId,h.Id,h.Status)!="Committed")throw new ArgumentException("Invalid hypothesis");}
            foreach(var link in s.Links){if(link.OwnerId==null||n.Link(query(link.OwnerId),link.From,link.To)!="Committed")throw new ArgumentException("Invalid notebook graph");}return n;
        }
    }
}
