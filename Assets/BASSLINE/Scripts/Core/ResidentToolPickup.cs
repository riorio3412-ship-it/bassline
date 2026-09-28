using System;
namespace BASSLINE.Core
{
    [Serializable] public sealed class ResidentToolPickup
    {
        public string ActorId="",ToolId="",Activity="",Node="",LocationRecordId="",Phase="Reaching",Reason="";
        public long StartedTick,LastTick,ActivityEndTick,AcquiredTick=-1;
        public int ElapsedTicks;
        public double BodyYaw;
        public Point3 ActorPosition,StartHand,StartHandForward,StartHandUp,ToolStart,ToolForward,ToolUp,RestHand,RestForward,RestUp,HandPosition,ItemPosition;
        public bool Running=>Phase=="Reaching"||Phase=="Gripping"||Phase=="Lifting"||Phase=="Carrying";
        public ResidentToolPickup Copy()=>(ResidentToolPickup)MemberwiseClone();
    }
}
