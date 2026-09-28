using System;
namespace BASSLINE.Core
{
    [Serializable] public sealed class SurfaceReadingProgress
    {
        public string Observer="",Target="",Root="",Pattern="";
        public long StartedTick,LastTick;public int ElapsedTicks;
        public SurfaceReadingProgress Copy()=>(SurfaceReadingProgress)MemberwiseClone();
    }
}
