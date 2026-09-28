using System;
namespace BASSLINE.Core
{
    // PRODUCTION PROPOSAL: comfort defaults, independent of NPC schedule speed.
    [Serializable] public sealed class LocomotionSettings
    {
        public double WalkSpeed=2.7, RunSpeed=4.8, Acceleration=24, Deceleration=32;
    }
    public static class Locomotion
    {
        public static Point3 Velocity(Point3 current, Point3 input, bool run, double seconds, LocomotionSettings settings)
        {
            if (!current.Finite() || !input.Finite() || double.IsNaN(seconds) || seconds<0 || seconds>1) throw new ArgumentException("Invalid locomotion input");
            var direction=new Point3(input.X,0,input.Z);double length=direction.Distance(default);
            if(length>1)direction=direction.Scale(1/length);
            var target=direction.Scale(run?settings.RunSpeed:settings.WalkSpeed);
            var delta=target.Minus(current);double distance=delta.Distance(default);
            double step=(length<.0001?settings.Deceleration:settings.Acceleration)*seconds;
            return distance<=step?target:current.Plus(delta.Scale(step/distance));
        }
    }
}
