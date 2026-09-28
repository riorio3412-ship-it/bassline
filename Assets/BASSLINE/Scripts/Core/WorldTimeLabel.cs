using System;
namespace BASSLINE.Core
{
    // Stored separately from simulation ticks. Missing version means the historical 14:50 epoch.
    public static class WorldTimeLabel
    {
        public const int Legacy=0, Morning=1;
        public static bool IsSupported(int version)=>version==Legacy||version==Morning;
        public static string Format(long tick,int version=Legacy)
        {
            if(tick<0||!IsSupported(version))return "시각 미확인";
            long seconds=tick/60+(version==Morning?7*3600:14*3600+50*60);
            long day=seconds/86400+1;seconds%=86400;
            return (day>1?day+"일째 · ":"")+$"{seconds/3600:00}:{seconds/60%60:00}:{seconds%60:00}";
        }
    }
}
