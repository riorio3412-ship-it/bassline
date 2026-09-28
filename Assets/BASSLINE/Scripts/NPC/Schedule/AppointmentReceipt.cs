using System;
namespace BASSLINE.NPC
{
    [Serializable] public sealed class AppointmentReceipt
    {
        public string ActorId="",Kind="",SourceRootId="";public long Tick;
        public AppointmentReceipt Copy()=>(AppointmentReceipt)MemberwiseClone();
    }
}
