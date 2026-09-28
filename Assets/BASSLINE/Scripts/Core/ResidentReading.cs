using System;
namespace BASSLINE.Core
{
    [Serializable] public sealed class ResidentReading
    {
        public string ActorId="",ItemId="",Revision="",Content="",Node="",Phase="Raising",RecordId="",Reason="";
        public long StartedTick,LastTick,ActivityEndTick,ReadAt=-1;public int ElapsedTicks,ReadingTicks,RequiredTicks;public double BodyYaw;
        public Point3 ActorPosition,StartPosition,StartForward,StartUp,TargetPosition,TargetForward,TargetUp,Position;
        public bool Running=>Phase=="Raising"||Phase=="Reading"||Phase=="Lowering";
        public ResidentReading Copy()=>(ResidentReading)MemberwiseClone();
    }
}
