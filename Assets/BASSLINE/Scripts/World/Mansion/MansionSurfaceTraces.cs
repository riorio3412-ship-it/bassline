using System;
using System.Linq;
using BASSLINE.Core;
namespace BASSLINE.World.Mansion
{
    [Serializable] public sealed class SurfaceTrace
    {
        public string Id="",SurfaceId="",SourceId=""; // SourceId is authority-only; never part of a public observation.
        public long DepositedTick;
        public Point3 LocalPoint,LocalNormal,LocalUp;
        public SurfacePattern Pattern=new SurfacePattern();
        public SurfaceTrace Copy(){var c=(SurfaceTrace)MemberwiseClone();c.Pattern=Pattern.Copy();return c;}
    }
    [Serializable] public sealed class SurfaceTraceSnapshot
    {
        public int Loop=1;public long InstalledTick,LastTick=-1,Sequence;
        public SurfaceTrace[] Marks=Array.Empty<SurfaceTrace>();
        public SurfaceTraceSnapshot Copy()=>new SurfaceTraceSnapshot{Loop=Loop,InstalledTick=InstalledTick,LastTick=LastTick,Sequence=Sequence,Marks=Marks.Select(m=>m.Copy()).ToArray()};
    }
    public sealed class MansionSurfaceTraces
    {
        SurfaceTraceSnapshot state;
        public MansionSurfaceTraces(int loop,long tick){if(loop<1||tick<0)throw new ArgumentException("Invalid trace clock");state=new SurfaceTraceSnapshot{Loop=loop,InstalledTick=tick};}
        public void BeginTick(int loop,long tick){if(loop!=state.Loop||tick<state.InstalledTick||tick<state.LastTick)throw new InvalidOperationException("Trace clock mismatch");state.LastTick=tick;}
        // Only the physical contact adapter calls this. No incident, culprit, or death lookup.
        public SurfaceTrace Deposit(string source,string surface,Point3 point,Point3 normal,Point3 up,SurfacePattern pattern)
        {
            SurfacePattern.Validate(pattern);
            if(state.LastTick<state.InstalledTick||string.IsNullOrEmpty(source)||string.IsNullOrEmpty(surface)||source==surface||!point.Finite()||!normal.Finite()||!up.Finite()||Math.Abs(up.X*up.X+up.Y*up.Y+up.Z*up.Z-1)>.01||Math.Abs(up.X*normal.X+up.Y*normal.Y+up.Z*normal.Z)>.01||Math.Abs(normal.X*normal.X+normal.Y*normal.Y+normal.Z*normal.Z-1)>.01)throw new ArgumentException("Invalid surface contact");
            // Remaining in one place does not create hundreds of independent clues.
            var prior=state.Marks.LastOrDefault(m=>m.SurfaceId==surface&&m.Pattern.Key==pattern.Key&&m.LocalPoint.Distance(point)<pattern.SizeMm/2000d);
            if(prior!=null)return prior.Copy();
            var mark=new SurfaceTrace{Id="SURFACE_L"+state.Loop+"_"+(++state.Sequence),SourceId=source,SurfaceId=surface,LocalPoint=point,LocalNormal=normal,LocalUp=up,Pattern=pattern.Copy(),DepositedTick=state.LastTick};
            state.Marks=state.Marks.Concat(new[]{mark}).ToArray();return mark.Copy();
        }
        public SurfaceTraceSnapshot Capture()=>state.Copy();
        public static MansionSurfaceTraces Restore(SurfaceTraceSnapshot s,int loop,long tick,string[] surfaces,string[] sources)
        {
            if(s==null||s.Loop!=loop||s.InstalledTick<0||s.InstalledTick>tick||s.LastTick< -1||s.LastTick>tick||s.LastTick>=0&&s.LastTick<s.InstalledTick||s.Marks==null||s.Sequence!=s.Marks.LongLength)throw new ArgumentException("Invalid trace snapshot");
            for(int i=0;i<s.Marks.Length;i++){
                var m=s.Marks[i];if(m==null||m.Id!="SURFACE_L"+loop+"_"+(i+1)||!surfaces.Contains(m.SurfaceId)||!sources.Contains(m.SourceId)||m.SourceId==m.SurfaceId||m.DepositedTick<s.InstalledTick||m.DepositedTick>s.LastTick||i>0&&m.DepositedTick<s.Marks[i-1].DepositedTick||!m.LocalPoint.Finite()||!m.LocalNormal.Finite()||!m.LocalUp.Finite()||Math.Abs(m.LocalUp.X*m.LocalUp.X+m.LocalUp.Y*m.LocalUp.Y+m.LocalUp.Z*m.LocalUp.Z-1)>.01||Math.Abs(m.LocalUp.X*m.LocalNormal.X+m.LocalUp.Y*m.LocalNormal.Y+m.LocalUp.Z*m.LocalNormal.Z)>.01||Math.Abs(m.LocalNormal.X*m.LocalNormal.X+m.LocalNormal.Y*m.LocalNormal.Y+m.LocalNormal.Z*m.LocalNormal.Z-1)>.01)throw new ArgumentException("Invalid deposited mark");
                SurfacePattern.Validate(m.Pattern);
            }
            return new MansionSurfaceTraces(loop,s.InstalledTick){state=s.Copy()};
        }
    }
}
