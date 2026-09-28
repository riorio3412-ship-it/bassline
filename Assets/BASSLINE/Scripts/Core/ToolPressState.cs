using System;
namespace BASSLINE.Core
{
    [Serializable] public sealed class ToolPressState
    {
        public string ActorId="CH_01";
        public string Phase="Idle",ToolId="",SurfaceId="",Pattern="",MarkId="",Reason="";
        public long StartedTick=-1,LastTick=-1;public int ElapsedTicks;
        public double BodyYaw;
        public Point3 ActorPosition,StartPosition,ContactPosition,Position,StartForward,StartUp,ContactForward,ContactUp,SurfacePoint,SurfaceNormal;
        public bool Running=>Phase=="Reaching"||Phase=="Pressing"||Phase=="Returning";
        public ToolPressState Copy()=>(ToolPressState)MemberwiseClone();
    }
    [Serializable] public sealed class ResidentToolWork
    {
        public string ActorId="",Activity="",Node="";
        public long ActivityEndTick;
        public ToolPressState Motion=new ToolPressState();
        public ResidentToolWork Copy()=>new ResidentToolWork{ActorId=ActorId,Activity=Activity,Node=Node,ActivityEndTick=ActivityEndTick,Motion=Motion.Copy()};
    }
}
