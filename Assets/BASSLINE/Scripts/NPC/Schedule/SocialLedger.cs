using System;
using System.Linq;
using System.Collections.Generic;
using BASSLINE.Core;
namespace BASSLINE.NPC
{
    [Serializable] public sealed class AppointmentRevision
    {
        public string Id,Organizer,Invitee,PlaceId;public int Revision;public long ProposedTick,StartTick,Duration;
        public string[] ReceivedBy=Array.Empty<string>(),AcceptedBy=Array.Empty<string>(),ConfirmationReceivedBy=Array.Empty<string>();
        public string WrittenRecordId="",DocumentId="";public long WrittenTick;
        public string ProposalRecordId="",ReplyRecordId="";public long ProposalReceivedTick,ReplyReceivedTick;public bool Declined;
        public AppointmentReceipt[] Receipts=Array.Empty<AppointmentReceipt>();
        public AppointmentRevision Copy()=>new AppointmentRevision{WrittenRecordId=WrittenRecordId,DocumentId=DocumentId,WrittenTick=WrittenTick,Receipts=(Receipts??Array.Empty<AppointmentReceipt>()).Select(r=>r?.Copy()).ToArray(),ProposalRecordId=ProposalRecordId,ReplyRecordId=ReplyRecordId,ProposalReceivedTick=ProposalReceivedTick,ReplyReceivedTick=ReplyReceivedTick,Declined=Declined,Id=Id,Organizer=Organizer,Invitee=Invitee,PlaceId=PlaceId,Revision=Revision,ProposedTick=ProposedTick,StartTick=StartTick,Duration=Duration,ReceivedBy=(string[])ReceivedBy.Clone(),AcceptedBy=(string[])AcceptedBy.Clone(),ConfirmationReceivedBy=(string[])ConfirmationReceivedBy.Clone()};
    }
    [Serializable] public sealed class SocialExperience {public string Id,Owner,Other,Kind,Reason;public long Tick;public int Trust,Liking;public SocialExperience Copy()=>(SocialExperience)MemberwiseClone();}
    [Serializable] public sealed class SocialSnapshot {public AppointmentCardState Card=new AppointmentCardState();public long Sequence;public AppointmentRevision[] Appointments=Array.Empty<AppointmentRevision>();public SocialExperience[] Experiences=Array.Empty<SocialExperience>();public string[] Completed=Array.Empty<string>();}
    public sealed class SocialLedger
    {
        readonly HashSet<string> actors,places;readonly List<AppointmentRevision> revisions=new List<AppointmentRevision>();
        readonly List<SocialExperience> experiences=new List<SocialExperience>();readonly HashSet<string> completed=new HashSet<string>();long sequence;
        public AppointmentCardState Card {get;private set;}=new AppointmentCardState();
        public SocialLedger(IEnumerable<string> actors,IEnumerable<string> places){this.actors=new HashSet<string>(actors);this.places=new HashSet<string>(places);}
        public string Propose(string organizer,string invitee,string place,long start,long duration,long now,string existingId=null)
        {
            if(!actors.Contains(organizer)||!actors.Contains(invitee)||organizer==invitee||!places.Contains(place)||start<=now||duration<60)return "Unavailable";
            var prior=existingId==null?null:revisions.LastOrDefault(x=>x.Id==existingId);
            if(existingId!=null&&(prior==null||prior.Organizer!=organizer||prior.Invitee!=invitee))return "AccessDenied";
            var id=existingId??"K_APPT_"+(++sequence);
            revisions.Add(new AppointmentRevision{Id=id,Organizer=organizer,Invitee=invitee,PlaceId=place,Revision=(prior?.Revision??0)+1,ProposedTick=now,StartTick=start,Duration=duration,ReceivedBy=new[]{organizer},AcceptedBy=new[]{organizer},Receipts=new[]{new AppointmentReceipt{ActorId=organizer,Kind="ProposalKnown",Tick=now}}});return id;
        }
        public string Receive(string id,int revision,string recipient,long tick=-1,string sourceRoot="")
        {
            var a=revisions.SingleOrDefault(x=>x.Id==id&&x.Revision==revision);
            if(a==null||a.Invitee!=recipient||tick>=0&&tick<a.ProposedTick)return "AccessDenied";
            if(tick>=0)AddReceipt(a,recipient,"OfferReceived",tick,sourceRoot);
            if(!a.ReceivedBy.Contains(recipient))a.ReceivedBy=a.ReceivedBy.Concat(new[]{recipient}).ToArray();return "Received";
        }
        public string Accept(string id,int revision,string actor,bool accept)
        {
            var a=revisions.SingleOrDefault(x=>x.Id==id&&x.Revision==revision);
            if(a==null||!a.ReceivedBy.Contains(actor)||actor!=a.Invitee)return "AccessDenied";
            a.AcceptedBy=a.AcceptedBy.Where(x=>x!=actor).Concat(accept?new[]{actor}:Array.Empty<string>()).Distinct().ToArray();a.ConfirmationReceivedBy=accept?new[]{actor}:Array.Empty<string>();return accept?"Accepted":"Declined";
        }
        public string ReceiveAcceptance(string id,int revision,string organizer){var a=revisions.SingleOrDefault(x=>x.Id==id&&x.Revision==revision);if(a==null||a.Organizer!=organizer||a.AcceptedBy.Length!=2)return "Unavailable";a.ConfirmationReceivedBy=a.ConfirmationReceivedBy.Concat(new[]{organizer}).Distinct().ToArray();return "Received";}
        public void RecordSpokenProposal(string id,int revision,string record,long tick)
        {
            var a=revisions.Single(x=>x.Id==id&&x.Revision==revision);
            a.ProposalRecordId=record;a.ProposalReceivedTick=tick;AddReceipt(a,a.Organizer,"ProposalKnown",tick,record);Receive(id,revision,a.Invitee,tick,record);
        }
        public void RecordWrittenInvitation(string id,int revision,string documentId,string record,long tick)
        {
            var a=revisions.Single(x=>x.Id==id&&x.Revision==revision);
            a.DocumentId=documentId;a.WrittenRecordId=record;a.WrittenTick=tick;AddReceipt(a,a.Organizer,"ProposalKnown",tick,record);
        }
        public void RecordSpokenReply(string id,int revision,string record,long tick,bool accepted)
        {
            var a=revisions.Single(x=>x.Id==id&&x.Revision==revision);
            if(!string.IsNullOrEmpty(a.ReplyRecordId))return;
            a.ReplyRecordId=record;a.ReplyReceivedTick=tick;a.Declined=!accepted;
            AddReceipt(a,a.Organizer,"ReplyReceived",tick,record);AddReceipt(a,a.Invitee,"ReplyReceived",tick,record);
            Accept(id,revision,a.Invitee,accepted);if(accepted)ReceiveAcceptance(id,revision,a.Organizer);
        }
        static void AddReceipt(AppointmentRevision a,string actor,string kind,long tick,string root)
        {
            var receipt=a.Receipts.FirstOrDefault(r=>r.ActorId==actor&&r.Kind==kind);
            if(receipt==null)a.Receipts=a.Receipts.Concat(new[]{new AppointmentReceipt{ActorId=actor,Kind=kind,Tick=tick,SourceRootId=root??""}}).ToArray();
            else if(string.IsNullOrEmpty(receipt.SourceRootId)&&!string.IsNullOrEmpty(root))receipt.SourceRootId=root;
        }
        static string CompletionKey(string id,int revision,string owner)=>id+"|"+revision+"|"+owner;
        AppointmentView View(AppointmentRevision a,string owner,long now)
        {
            var received=a.Receipts.FirstOrDefault(r=>r.ActorId==owner&&(r.Kind=="OfferReceived"||r.Kind=="ProposalKnown"));
            var reply=a.Receipts.FirstOrDefault(r=>r.ActorId==owner&&r.Kind=="ReplyReceived");
            return new AppointmentView{Id=a.Id,Organizer=a.Organizer,Invitee=a.Invitee,PlaceId=a.PlaceId,Revision=a.Revision,StartTick=a.StartTick,Duration=a.Duration,Accepted=a.AcceptedBy.Contains(owner),ReceivedTick=received?.Tick??-1,ConfirmationTick=reply?.Tick??-1,SourceRootId=received?.SourceRootId??"",
                State=a.Declined?"Declined":completed.Contains(CompletionKey(a.Id,a.Revision,owner))?"Met":now>a.StartTick+a.Duration?"WindowEnded":a.ConfirmationReceivedBy.Contains(owner)?"Agreed":"Proposed"};
        }
        public AppointmentView[] For(string owner,long now)=>revisions.Where(x=>x.ReceivedBy.Contains(owner)).GroupBy(x=>x.Id).Select(g=>View(g.OrderByDescending(x=>x.Revision).First(),owner,now)).ToArray();
        public AppointmentView[] HistoryFor(string owner,long now)=>revisions.Where(x=>x.ReceivedBy.Contains(owner)).OrderByDescending(x=>x.ProposedTick).ThenByDescending(x=>x.Revision).Select(a=>View(a,owner,now)).ToArray();
        public AppointmentView Due(string owner,long now)=>For(owner,now).Where(x=>x.State=="Agreed"&&now>=x.StartTick-120*60&&now<=x.StartTick+x.Duration).OrderBy(x=>x.StartTick).FirstOrDefault();
        public void Meet(string appointmentId,string owner,string other,long tick)
        {
            var known=For(owner,tick).FirstOrDefault(x=>x.Id==appointmentId);if(known==null||known.State!="Agreed"||owner==other||tick<known.StartTick||tick>known.StartTick+known.Duration||!new[]{known.Organizer,known.Invitee}.Contains(other))return;
            if(completed.Add(CompletionKey(appointmentId,known.Revision,owner)))Experience(owner,other,"PromiseKept",appointmentId,tick,2,1);
        }
        public void Experience(string owner,string other,string kind,string reason,long tick,int trust=0,int liking=0)
        {
            if(!actors.Contains(owner)||!actors.Contains(other)||owner==other||experiences.Any(x=>x.Owner==owner&&x.Reason==reason&&x.Kind==kind))return;
            experiences.Add(new SocialExperience{Id="K_SOC_"+(++sequence),Owner=owner,Other=other,Kind=kind,Reason=reason,Tick=tick,Trust=Math.Max(-5,Math.Min(5,trust)),Liking=Math.Max(-5,Math.Min(5,liking))});
        }
        public SocialExperience[] Experiences(string owner)=>experiences.Where(x=>x.Owner==owner).Select(x=>x.Copy()).ToArray();
        public SocialSnapshot Capture()=>new SocialSnapshot{Card=Card.Copy(),Sequence=sequence,Appointments=revisions.Select(x=>x.Copy()).ToArray(),Experiences=experiences.Select(x=>x.Copy()).ToArray(),Completed=completed.OrderBy(x=>x,StringComparer.Ordinal).ToArray()};
        public static SocialLedger Restore(SocialSnapshot s,IEnumerable<string> actors,IEnumerable<string> places,long tick)
        {
            var result=new SocialLedger(actors,places);if(s==null||s.Sequence<0||s.Appointments==null||s.Experiences==null||s.Completed==null)throw new ArgumentException("Invalid social snapshot");
            foreach(var a in s.Appointments){if(!result.actors.Contains(a.Organizer)||!result.actors.Contains(a.Invitee)||a.Organizer==a.Invitee||!result.places.Contains(a.PlaceId)||a.ProposedTick>tick||a.StartTick<=a.ProposedTick||a.Duration<60||a.Revision<1||a.ReceivedBy==null||a.AcceptedBy==null||a.ConfirmationReceivedBy==null||a.ConfirmationReceivedBy.Any(x=>!a.AcceptedBy.Contains(x))||a.ReceivedBy.Distinct().Count()!=a.ReceivedBy.Length||a.AcceptedBy.Distinct().Count()!=a.AcceptedBy.Length||a.ProposedTick<0||a.ReceivedBy.Any(x=>x!=a.Organizer&&x!=a.Invitee)||a.AcceptedBy.Any(x=>!a.ReceivedBy.Contains(x)))throw new ArgumentException("Invalid appointment receipt");var copy=a.Copy();
                if(copy.Receipts.Any(r=>r==null||!new[]{copy.Organizer,copy.Invitee}.Contains(r.ActorId)||!new[]{"ProposalKnown","OfferReceived","ReplyReceived"}.Contains(r.Kind)||r.Tick<copy.ProposedTick||r.Tick>tick||!copy.ReceivedBy.Contains(r.ActorId)||r.Kind=="ProposalKnown"&&r.ActorId!=copy.Organizer)||copy.Receipts.Select(r=>r.ActorId+"|"+r.Kind).Distinct().Count()!=copy.Receipts.Length)throw new ArgumentException("Invalid appointment receipt history");
                result.revisions.Add(copy);}
            foreach(var group in s.Appointments.GroupBy(x=>x.Id)){int next=0;foreach(var a in group.OrderBy(x=>x.Revision))if(a.Revision!=++next)throw new ArgumentException("Invalid appointment history");}
            foreach(var e in s.Experiences){if(!result.actors.Contains(e.Owner)||!result.actors.Contains(e.Other)||e.Tick>tick)throw new ArgumentException("Invalid social experience");result.experiences.Add(e.Copy());}
            foreach(var c in s.Completed){
                if(s.Appointments.Any(a=>c==CompletionKey(a.Id,a.Revision,a.Organizer)||c==CompletionKey(a.Id,a.Revision,a.Invitee))){result.completed.Add(c);continue;}
                // Old saves omitted the revision. Preserve the then-current personally received version, never mark future revisions met.
                var matches=s.Appointments.SelectMany(a=>new[]{a.Organizer,a.Invitee}.Where(owner=>c==a.Id+"_"+owner&&a.ReceivedBy.Contains(owner)).Select(owner=>new{Appointment=a,Owner=owner})).OrderByDescending(x=>x.Appointment.Revision).ToArray();
                if(matches.Length==0)throw new ArgumentException("Invalid appointment completion");
                var legacy=matches[0];result.completed.Add(CompletionKey(legacy.Appointment.Id,legacy.Appointment.Revision,legacy.Owner));
            }result.Card=s.Card?.Copy()??new AppointmentCardState();result.sequence=s.Sequence;return result;
        }
    }
}

