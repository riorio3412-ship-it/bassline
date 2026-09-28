using NUnit.Framework;
using BASSLINE.Core;
namespace BASSLINE.Tests
{
    public sealed class LocomotionTests
    {
        [Test] public void SprintAcceleratesAndStopsWithinOneTenthSecond()
        {
            var settings=new LocomotionSettings();Point3 velocity=default;
            for(int i=0;i<60;i++)velocity=Locomotion.Velocity(velocity,new Point3(1,0,1),true,1d/60,settings);
            Assert.That(velocity.Distance(default),Is.EqualTo(settings.RunSpeed).Within(1e-9));
            Assert.That(velocity.Distance(default),Is.GreaterThan(settings.WalkSpeed*1.5));
            for(int i=0;i<10;i++)velocity=Locomotion.Velocity(velocity,default,false,1d/60,settings);
            Assert.That(velocity.Distance(default),Is.Zero);
        }
        [Test] public void SimulationGroupingDoesNotChangeMotion()
        {
            var settings=new LocomotionSettings();Point3 first=default,second=default,a=default,b=default;
            for(int frame=0;frame<30;frame++)for(int tick=0;tick<4;tick++){first=Locomotion.Velocity(first,new Point3(0,0,1),true,1d/60,settings);a=a.Plus(first.Scale(1d/60));}
            for(int frame=0;frame<120;frame++){second=Locomotion.Velocity(second,new Point3(0,0,1),true,1d/60,settings);b=b.Plus(second.Scale(1d/60));}
            Assert.That(a.Distance(b),Is.Zero);Assert.That(first.Distance(second),Is.Zero);
        }
    }
}
