using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.World.Mansion
{
    public sealed partial class MansionIncident
    {
        // Explicit player input replaces NPC deliberation. This is a production route, never a test recipe.
        public const int PlayerResultDelay=600,PlayerRescueTicks=120;
        public string CommitPlayerStrike(MansionWorld world,string target,string weapon,Point3 contact,IMansionIncidentPhysics physics)
        {
            if(IsConfigured||world.Paused||!contact.Finite()||target=="CH_01"||!world.Residents.Any(r=>r.Id==target)||!world.CanAct("CH_01")||!world.CanAct(target))return "ParticipantUnavailable";
            var held=world.Objects.FirstOrDefault(o=>o.Id==weapon);
            if(held==null||held.Location!="Hand"||held.Owner!="CH_01"||world.Resident("CH_01").HeldObject!=weapon)return "WeaponNotHeld";
            // The runtime records this only after a swept, unobstructed weapon collider hit.
            var hit=world.Events.LastOrDefault(e=>e.Type=="PlayerWeaponHit"&&e.Actor=="CH_01"&&e.Target==target&&e.Detail==weapon&&e.Tick==world.Tick);
            if(hit==null||!physics.CanReach("CH_01",target,1.5))return "PhysicalContactRequired";
            var chapter=world.CaseBook.Capture();
            var settings=new MansionCaseSettings{Enabled=true,PlayerInitiated=true,Template="PLAYER_STRIKE",Source="PLAYER_INPUT",Id="CASE_PLAYER_L"+world.Loop+"_E"+hit.Sequence,ActorId="CH_01",TargetId=target,ObjectId=weapon,ToolNode=world.Resident("CH_01").Node,ContactNode=world.Resident(target).Node,ChapterStartTick=chapter.ChapterStartTick,ChapterStartingResidents=chapter.StartingResidents,ApproachTick=world.Tick,EarliestCauseTick=world.Tick,OpportunityEndTick=world.Tick,ContactTicks=1,DelayTicks=PlayerResultDelay};
            ValidateSettings(settings);
            string registered=world.RegisterCase(settings);if(registered!="Registered")return registered;
            state=new MansionIncidentSnapshot{Settings=settings,Stage="Cancelled",LastTick=world.Tick,FatalityCap=chapter.StartingResidents<=6?1:2};
            if(!Reserve(world)){state.Reason="FatalityCapacityUnavailable";return state.Reason;}
            state.Stage="CauseCommitted";state.CauseTick=world.Tick;state.DueTick=world.Tick+PlayerResultDelay;state.CausePosition=world.Resident(target).Position;state.ContactPoint=contact;state.ContactProgress=1;
            // Capture sight before incapacitation. No hidden witness list is exposed to the player.
            state.Witnesses=world.Residents.Where(r=>r.Alive&&r.Present&&(r.Id=="CH_01"||physics.CanSee(r.Id,"CH_01")&&physics.CanSee(r.Id,target)&&physics.CanSee(r.Id,weapon))).Select(r=>new MansionCaseWitness{Observer=r.Id,ActorIdentified=r.Id=="CH_01"||physics.Identifies(r.Id,"CH_01"),TargetIdentified=physics.Identifies(r.Id,target)}).ToArray();
            state.CauseEvent="M_EVENT_"+world.CommitCaseCause(CaseId,state.DueTick);
            foreach(var w in state.Witnesses)Observe(world,w.Observer,"UsedObject",w.ActorIdentified?"CH_01":"UNKNOWN_ACTOR",weapon,ContactObservation,world.Tick,world.Tick+1,w.ActorIdentified,contact,new[]{"직접 본 무기와 접촉"},new[]{"사망 여부·관측 밖 행동은 별도 확인"});
            return "CauseCommitted";
        }
        static void ValidatePlayerSettings(MansionCaseSettings s)
        {
            if(s.ExplicitTestSession||s.Execution!=null||s.ActorId!="CH_01"||s.TargetId==s.ActorId||s.Template!="PLAYER_STRIKE"||s.Source!="PLAYER_INPUT"||s.DelayTicks!=PlayerResultDelay||s.ContactTicks!=1||s.ChapterStartingResidents<4||s.ChapterStartingResidents>18||s.ChapterStartTick<0||s.ApproachTick<s.ChapterStartTick||s.EarliestCauseTick!=s.ApproachTick||s.OpportunityEndTick!=s.ApproachTick)throw new ArgumentException("Invalid player strike settings");
            foreach(var id in new[]{s.Id,s.ActorId,s.TargetId,s.ObjectId,s.ToolNode,s.ContactNode})_=new StableId(id);
        }
        static void ValidatePlayerCause(MansionIncidentSnapshot s,MansionWorld world)
        {
            if(s.Plan!=null||s.Admission!=null||s.RiskNoticeTick>=0||s.RiskContinuedTick>=0||s.ActivationId!="")throw new ArgumentException("Player input is not an NPC plan or a device activation");
            var hit=world.Events.SingleOrDefault(e=>e.Type=="PlayerWeaponHit"&&e.Tick==s.Settings.ApproachTick&&e.Actor==s.Settings.ActorId&&e.Target==s.Settings.TargetId&&e.Detail==s.Settings.ObjectId&&s.Settings.Id=="CASE_PLAYER_L"+world.Loop+"_E"+e.Sequence);
            if(hit==null||s.CauseTick>=0&&(s.CauseTick!=hit.Tick||s.Stage=="Cancelled")||s.CauseTick<0&&s.Stage!="Cancelled")throw new ArgumentException("Player incident has no matching physical hit");
        }
    }
}
