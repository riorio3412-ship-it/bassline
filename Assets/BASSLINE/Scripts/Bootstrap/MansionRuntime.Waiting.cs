using System;
using System.Linq;
using BASSLINE.Core;

namespace BASSLINE.Bootstrap
{
    public sealed partial class MansionRuntime:IPlayerWaitingPort
    {
        WaitingState waiting=new WaitingState();
        public bool IsWaiting=>waiting.Active;
        string AppointmentKey(AppointmentView a)=>a.Id+":"+a.Revision+":"+a.State;
        AppointmentView[] KnownAppointments()=>Social.For("CH_01",World.Tick);
        long DepartureTick(AppointmentView appointment)
        {
            if(PlaceOf(bodies["CH_01"].transform.position)==appointment.PlaceId)return appointment.StartTick-60*60;
            var room=Layout.Room(appointment.PlaceId);if(!room||room.WalkNode<0)return World.Tick;
            int nearest=Layout.NearestNode(bodies["CH_01"].transform.position);if(nearest<0)return World.Tick;
            var path=World.FindPath(nodes[nearest].Id,nodes[room.WalkNode].Id,World.Resident("CH_01").KnownLocked);
            if(path.Length==0)return World.Tick;
            double distance=World.Resident("CH_01").Position.Distance(World.NavigationPoint(path[0]));
            for(int i=1;i<path.Length;i++)distance+=World.NavigationPoint(path[i-1]).Distance(World.NavigationPoint(path[i]));
            return appointment.StartTick-(long)Math.Ceiling(distance/1.4*60)-60*60;
        }
        long NextDeparture()
        {
            var appointments=KnownAppointments().Where(a=>a.State=="Agreed"&&a.Accepted&&a.StartTick+a.Duration>World.Tick).ToArray();
            return appointments.Length==0?long.MaxValue:appointments.Min(DepartureTick);
        }
        public WaitingView ReadWaiting()
        {
            long departure=waiting.Active?waiting.TargetTick:NextDeparture();
            return new WaitingView{Active=waiting.Active,StartedTick=waiting.StartedTick,TargetTick=waiting.TargetTick,CurrentTick=World.Tick,Reason=waiting.Reason,
                AppointmentDepartureTick=departure,CanWaitForAppointment=departure>World.Tick&&departure<=World.Tick+WaitingPolicy.MaximumDuration};
        }
        public string StartWaiting(long durationTicks)=>BeginWaiting(durationTicks,"Duration");
        public string WaitForNextAppointment()
        {
            long departure=NextDeparture();
            if(departure==long.MaxValue)return message="기다릴 약속이 아직 없어요.";
            if(departure<=World.Tick)return message="약속 장소로 출발할 시간이에요.";
            return BeginWaiting(departure-World.Tick,"Appointment");
        }
        string BeginWaiting(long durationTicks,string mode)
        {
            if(World.Paused||toolPress.Running||!World.CanAct("CH_01")||RescueControls("CH_01")||WritingCard||waiting.Active||inspection.State=="Running")return message="지금은 기다릴 수 없어요.";
            if(durationTicks<60||durationTicks>WaitingPolicy.MaximumDuration||World.Tick>long.MaxValue-durationTicks)return message="기다릴 시간을 다시 골라 주세요.";
            long departure=NextDeparture();if(departure<=World.Tick)return message="약속 장소로 출발할 시간이에요.";
            var query=Knowledge.For("CH_01");
            waiting=new WaitingState{Active=true,StartedTick=World.Tick,TargetTick=Math.Min(World.Tick+durationTicks,departure),PlaceId=PlaceOf(bodies["CH_01"].transform.position),Mode=mode,
                KnowledgeRevision=query.Revision,PriorSignals=WaitingPolicy.Signals(query.Records()),PriorInvitations=KnownAppointments().Select(AppointmentKey).ToArray()};
            move=default;running=false;return message="잠깐 시간을 보내는 중이에요.";
        }
        public void StopWaiting(string reason)
        {
            if(!waiting.Active)return;waiting.Active=false;waiting.Reason=reason??"기다리기를 멈췄어요.";message=waiting.Reason;
        }
        void CheckWaiting()
        {
            if(!waiting.Active)return;
            if(!World.CanAct("CH_01")){StopWaiting("몸을 움직일 수 없어 기다리기를 멈췄어요.");return;}
            if(World.Paused){StopWaiting("잠깐 멈췄어요.");return;}
            if(World.Tick>=waiting.TargetTick){StopWaiting(waiting.Mode=="Appointment"||NextDeparture()<=World.Tick?"약속 장소로 출발할 시간이에요.":"정한 만큼 기다렸어요.");return;}
            if(PlaceOf(bodies["CH_01"].transform.position)!=waiting.PlaceId){StopWaiting("자리에서 이동해 기다리기를 멈췄어요.");return;}
            var own=Knowledge.For("CH_01");
            if(own.Revision!=waiting.KnowledgeRevision){
                waiting.KnowledgeRevision=own.Revision;
                if(WaitingPolicy.HasNewSignal(waiting,own.Records())){StopWaiting("새로 들은 안내나 확인한 일이 있어요.");return;}
            }
            // Personal received revisions only, not the other participant's hidden schedule.
            if(KnownAppointments().Any(a=>!waiting.PriorInvitations.Contains(AppointmentKey(a))))StopWaiting("약속에 새 소식이 있어요.");
        }
        public int AdvanceWaitingBatch(int maximumTicks=600,int milliseconds=12)
        {
            int count=0;var timer=System.Diagnostics.Stopwatch.StartNew();CheckWaiting();
            while(waiting.Active&&count<Math.Max(1,Math.Min(600,maximumTicks))&&timer.ElapsedMilliseconds<Math.Max(1,Math.Min(12,milliseconds))){AdvanceOne();count++;}
            return count;
        }
    }
}
