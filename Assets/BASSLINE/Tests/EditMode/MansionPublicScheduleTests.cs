using System;
using NUnit.Framework;
using BASSLINE.World.Mansion;

public sealed class MansionPublicScheduleTests
{
    [Test] public void SecondPublicationExtendsOnceAndDuplicateReportsDoNotMoveDeadline()
    {
        var s=new MansionPublicCaseSchedule();Assert.That(s.ConveneAt,Is.EqualTo(-1));s.PublishConfirmedDeath("DEATH_A",100);
        Assert.That(s.ConveneAt,Is.EqualTo(216100));s.PublishConfirmedDeath("DEATH_A",100000);Assert.That(s.Revision,Is.EqualTo(1));
        s.PublishConfirmedDeath("DEATH_B",180100);Assert.That(s.ConveneAt,Is.EqualTo(288100));s=MansionPublicCaseSchedule.Restore(s.Capture(),180100);
        s.PublishConfirmedDeath("DEATH_C",288000);Assert.That(s.ConveneAt,Is.EqualTo(288100));Assert.That(s.Revision,Is.EqualTo(2));
        var bad=s.Capture();bad.ConveneAt++;Assert.Throws<ArgumentException>(()=>MansionPublicCaseSchedule.Restore(bad,288000));
    }
    [Test] public void EarlySecondPublicationDoesNotShortenTheFirstInvestigation()
    {
        var s=new MansionPublicCaseSchedule();s.PublishConfirmedDeath("DEATH_A",0);s.PublishConfirmedDeath("DEATH_B",100);
        Assert.That(s.ConveneAt,Is.EqualTo(216000));Assert.That(s.Capture().AdditionalDeathExtensionUsed,Is.True);
    }
}
