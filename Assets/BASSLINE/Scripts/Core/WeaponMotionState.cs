using System;
namespace BASSLINE.Core
{
    [Serializable] public sealed class WeaponMotionState
    {
        public int Version;
        public string Phase="Idle",WeaponId="",ContactTarget="";
        public long StartedTick,LastTick;public int Elapsed,StoppedAt=-1;public double Yaw;
        public Point3 Origin,Position;public bool Running=>Phase=="Swinging"||Phase=="Recovering";
        public WeaponMotionState Copy()=>(WeaponMotionState)MemberwiseClone();
    }
    public interface IPlayerWeaponPort { string HeldWeaponId(); string HeldWeaponHint(); string SwingWeapon(); }
}
