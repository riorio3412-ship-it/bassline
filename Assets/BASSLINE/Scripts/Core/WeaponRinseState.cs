using System;
namespace BASSLINE.Core
{
    [Serializable] public sealed class WeaponRinseState
    {
        public string Phase="Idle",WeaponId="",StationId="";
        public long StartedTick,LastTick,CommitSequence;
        public int Elapsed;
        public double Yaw;
        public Point3 Origin,StartGrip,Grip;
        public string[] Witnesses=Array.Empty<string>();
        public bool Running=>Phase=="Running";
        public WeaponRinseState Copy(){var c=(WeaponRinseState)MemberwiseClone();c.Witnesses=(string[])Witnesses.Clone();return c;}
    }
}
