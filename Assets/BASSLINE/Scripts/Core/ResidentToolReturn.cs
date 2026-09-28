using System;
namespace BASSLINE.Core
{
    [Serializable] public sealed class ResidentToolReturn
    {
        public string ActorId="",ToolId="",Activity="",Node="",SupportId="",Phase="Reaching",Reason="";
        public long PickupStartedTick,StartedTick,LastTick,ActivityEndTick,ReleasedTick=-1;
        public long ReleaseEventSequence;
        public int ElapsedTicks;
        public double BodyYaw;
        public Point3 ActorPosition,StartPosition,StartForward,StartUp,TargetPosition,TargetForward,TargetUp,RestHand,RestForward,RestUp,HandPosition,ItemPosition;
        public bool Running=>Phase=="Reaching"||Phase=="Lowering"||Phase=="Releasing"||Phase=="Withdrawing";
        public ResidentToolReturn Copy()=>(ResidentToolReturn)MemberwiseClone();
    }
}
