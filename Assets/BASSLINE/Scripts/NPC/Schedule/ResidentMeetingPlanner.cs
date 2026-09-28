using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.NPC
{
    public sealed class ResidentMeetingPlanner
    {
        public static string Activity(string place)=>place=="R_LIBRARY"?"Read":place=="R_DINING"?"Meal":place=="R_GREEN"||place=="R_GARDEN"?"Walk":place=="R_ARCADE"?"Game":"Talk";
        public ResidentMeetingIntent Choose(string actor,string currentPlace,int completed,long now,IActorKnowledgeQuery own,AppointmentView[] appointments,SocialExperience[] experiences,string[] nearby)
        {
            if(own==null||own.OwnerId!=actor)throw new ArgumentException("A resident planner requires its own knowledge.");
            if(completed<2||appointments.Any(a=>a.StartTick+a.Duration>=now&&a.State!="Declined"&&a.State!="Met"))return null;
            var voice=new ResidentConversation().Profiles().Single(p=>p.ActorId==actor);
            if(!voice.PreferredPlaces.Contains(currentPlace)&&!voice.PreferredActivities.Contains(Activity(currentPlace)))return null;
            var seen=own.Records().Where(r=>r.Direct&&r.Predicate=="AtPlace"&&r.PlaceId==currentPlace&&r.IdentityConfirmed&&now-r.FromTick<=600).Select(r=>r.SubjectId).Distinct().ToArray();
            var candidates=nearby.Where(id=>id!=actor&&id!="CH_01"&&seen.Contains(id)).Distinct().OrderByDescending(id=>experiences.Where(e=>e.Owner==actor&&e.Other==id).Sum(e=>e.Trust)).ThenBy(id=>id,StringComparer.Ordinal).ToArray();
            if(candidates.Length==0)return null;
            return new ResidentMeetingIntent{Organizer=actor,Invitee=candidates[0],PlaceId=currentPlace,StartTick=(now/3600+6)*3600};
        }
    }
}
