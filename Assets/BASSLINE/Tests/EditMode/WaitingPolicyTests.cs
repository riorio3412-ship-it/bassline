using System;
using BASSLINE.Core;
using NUnit.Framework;

public sealed class WaitingPolicyTests
{
    [Test] public void AlreadyKnownNoticeAndRoutineSightingsDoNotInterrupt()
    {
        var state=new WaitingState{PriorSignals=new[]{"old"}};
        Assert.That(WaitingPolicy.HasNewSignal(state,new[]{new KnownRecord{Id="old",Predicate="CourtSchedule"},new KnownRecord{Id="seen",Predicate="AtPlace",Kind="Visual"}}),Is.False);
        Assert.That(WaitingPolicy.HasNewSignal(state,new[]{new KnownRecord{Id="new",Predicate="DirectRequest"}}),Is.True);
        Assert.That(WaitingPolicy.HasNewSignal(state,new[]{new KnownRecord{Id="fall",Predicate="AtPlace",Value="Collapsed",Kind="Visual"}}),Is.True,"The actual collapse receipt uses AtPlace, not a generic danger flag");
    }
    [Test] public void EmptyLegacyStateIsIdleAndExpiredActiveStateIsRejected()
    {
        WaitingPolicy.Validate(new WaitingState(),900);
        WaitingPolicy.Validate(new WaitingState{Active=true,StartedTick=30,TargetTick=60,PlaceId="R_HALL",Mode="Duration"},60);
        Assert.Throws<ArgumentException>(()=>WaitingPolicy.Validate(new WaitingState{Active=true,StartedTick=30,TargetTick=60,PlaceId="R_HALL",Mode="Duration"},61));
        Assert.Throws<ArgumentException>(()=>WaitingPolicy.Validate(new WaitingState{StartedTick=0,TargetTick=WaitingPolicy.MaximumDuration+1},0));
    }
}
