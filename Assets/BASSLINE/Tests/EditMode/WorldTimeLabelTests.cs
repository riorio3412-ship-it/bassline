using BASSLINE.Core;
using BASSLINE.World.Mansion;
using BASSLINE.Trial;
using NUnit.Framework;
using UnityEngine;
public sealed class WorldTimeLabelTests
{
    [Test] public void MissingClockVersionKeepsHistoricalSaveAndArchiveTimes()
    {
        var old=JsonUtility.FromJson<MansionState>("{\"Tick\":3600}");
        var plan=JsonUtility.FromJson<SettlementPlan>("{\"CaseId\":\"OLD_CASE\"}");
        Assert.That(WorldTimeLabel.Format(old.Tick,old.ClockVersion),Is.EqualTo("14:51:00"));
        Assert.That(WorldTimeLabel.Format(3600,plan.ClockVersion),Is.EqualTo("14:51:00"));
    }
    [Test] public void MorningClockSurvivesSerializationAndWrapsTheDayWithoutChangingTicks()
    {
        var state=new MansionState{ClockVersion=WorldTimeLabel.Morning,Tick=17*60*60*60};
        var restored=JsonUtility.FromJson<MansionState>(JsonUtility.ToJson(state));
        Assert.That(restored.Tick,Is.EqualTo(state.Tick));
        Assert.That(WorldTimeLabel.Format(0,restored.ClockVersion),Is.EqualTo("07:00:00"));
        Assert.That(WorldTimeLabel.Format(restored.Tick,restored.ClockVersion),Is.EqualTo("2일째 · 00:00:00"));
        var plan=new SettlementPlan{ClockVersion=WorldTimeLabel.Morning};
        Assert.That(plan.Copy().ClockVersion,Is.EqualTo(WorldTimeLabel.Morning));
    }
}
