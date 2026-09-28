using System;
namespace BASSLINE.Core
{
    [Serializable] public sealed class MansionCaseSettings
    {
        public bool Enabled,ExplicitTestSession,PlayerInitiated;
        public IncidentExecutionBinding Execution;
        public string Id="",Template="X31",Source="SRC11_P0465_P0496_P1638",ActorId="",TargetId="",ObjectId="",ToolNode="",ContactNode="",DecisionReason="",IntentSpeech="";
        public string[] RejectedAlternatives=Array.Empty<string>();
        public long ChapterStartTick,ApproachTick,EarliestCauseTick,OpportunityEndTick;
        public int ChapterStartingResidents=18,ContactTicks=30,DelayTicks=300,KnowledgeFreshnessTicks=600,MinimumIntentTicks=120;
        public MansionCaseSettings Copy(){var c=(MansionCaseSettings)MemberwiseClone();c.RejectedAlternatives=(string[])RejectedAlternatives.Clone();c.Execution=ExplicitTestSession||PlayerInitiated?null:Execution?.Copy();return c;}
    }
    public interface IMansionIncidentRiskDisplay
    {
        bool ShowVisibleWarning(MansionCaseSettings settings);
    }
    public interface IMansionWitnessDisplay
    {
        ObservedActionDisplay ReadIncidentDisplay(string observer,string device);
    }
    public interface IMansionIncidentPhysics
    {
        bool CanReach(string observer,string subject,double maximumDistance);
        bool CanSee(string observer,string subject);
        bool Identifies(string observer,string subject);
        bool HasContact(string actor,string target,string item,out Point3 point);
        bool ReceivesSpeech(string observer,string speaker);
    }
    public sealed class MansionCasePublicView
    {
        public string State="Unknown",VictimId="",Text="";public long ObservedTick=-1,ConfirmationTick=-1;
        public bool Discovered,Confirmed;
    }
}
