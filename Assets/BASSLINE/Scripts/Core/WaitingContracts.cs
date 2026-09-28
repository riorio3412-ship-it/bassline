using System;
using System.Linq;

namespace BASSLINE.Core
{
    [Serializable] public sealed class WaitingState
    {
        public bool Active;
        public long StartedTick,TargetTick,KnowledgeRevision;
        public string PlaceId="",Reason="",Mode="";
        public string[] PriorSignals=Array.Empty<string>(),PriorInvitations=Array.Empty<string>();
        public WaitingState Copy(){var c=(WaitingState)MemberwiseClone();c.PriorSignals=(string[])PriorSignals.Clone();c.PriorInvitations=(string[])PriorInvitations.Clone();return c;}
    }
    public sealed class WaitingView
    {
        public bool Active,CanWaitForAppointment;
        public long StartedTick,TargetTick,CurrentTick,AppointmentDepartureTick;
        public string Reason="";
    }
    public interface IPlayerWaitingPort
    {
        bool IsWaiting {get;}
        WaitingView ReadWaiting();
        string StartWaiting(long durationTicks);
        string WaitForNextAppointment();
        void StopWaiting(string reason);
    }
    public static class WaitingPolicy
    {
        public const long MaximumDuration=60L*60*60;
        // Only records actually held by the waiting person are passed to this policy.
        public static bool IsSignal(KnownRecord r)=>r!=null&&(r.Kind=="OfficialReport"||r.Value=="Collapsed"||r.Predicate=="PhysicalCondition"&&r.Value=="Critical"||new[]{"CourtSchedule","CourtSummons","ConfirmedDeath","HelpRequest","DirectRequest","Danger","CausedOutcome"}.Contains(r.Predicate));
        public static string[] Signals(KnownRecord[] records)=>records.Where(IsSignal).Select(r=>r.Id).ToArray();
        public static bool HasNewSignal(WaitingState state,KnownRecord[] records)=>records.Any(r=>IsSignal(r)&&!state.PriorSignals.Contains(r.Id));
        public static void Validate(WaitingState state,long tick)
        {
            if(state==null||state.StartedTick<0||state.StartedTick>tick||state.TargetTick<state.StartedTick||state.TargetTick-state.StartedTick>MaximumDuration||state.KnowledgeRevision<0
                ||state.PlaceId==null||state.Reason==null||state.Mode==null||state.PriorSignals==null||state.PriorInvitations==null||state.PriorSignals.Any(string.IsNullOrEmpty)||state.PriorInvitations.Any(string.IsNullOrEmpty)
                ||state.Active&&(state.TargetTick<tick||state.PlaceId==""||state.Mode==""))throw new ArgumentException("Invalid waiting state");
        }
    }
}
